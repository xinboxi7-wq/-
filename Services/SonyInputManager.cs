// SonyInputManager
//
// Extracted verbatim from ControllerLab.cs (lines 7038-7825) on 2026-09-22
// as part of the ControllerLab structural split (batch 3).
// No logic was changed.
// See docs/redesign/ControllerLab-结构拆分施工图.md
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ControllerLab
{
    public sealed class SonyInputManager
    {
        private sealed class SonyDeviceRecord
        {
            public InputSnapshot Latest;
            public string RawPath;
            public DateTime LastPacketAt = DateTime.MinValue;
            public DateTime LastSeenAt = DateTime.MinValue;
            public bool Present;
        }

        private sealed class DualSenseReportLayout
        {
            public string Name;
            public byte ReportId;
            public int MinimumLength;
            public int BodyStart;
            public int AxisStart;
            public int ButtonStart;
            public int LeftTriggerIndex;
            public int RightTriggerIndex;
            public int TouchOffset;
            public bool RequiresCrc;
            public bool HasTouchCoordinates;
            public bool HasMotionSamples;
        }

        private const int DualSenseTouchRawWidth = 1920;
        private const int DualSenseTouchRawHeight = 1080;
        private readonly object sync = new object();
        private InputSnapshot latest = new InputSnapshot { Family = ControllerFamily.PlayStation, DeviceName = "索尼 DS 手柄", InputBackend = "Sony 原生 HID" };
        private DateTime lastPacketAt = DateTime.MinValue;
        private readonly Dictionary<string, SonyDeviceRecord> devices = new Dictionary<string, SonyDeviceRecord>(StringComparer.OrdinalIgnoreCase);
        private uint packet;
        private long touchReportSequence;
        private long motionReportSequence;
        private DateTime lastDiscoveryAt = DateTime.MinValue;
        private DateTime touchRateWindowStarted = DateTime.UtcNow;
        private int touchReportsInWindow;
        private double touchUpdatesPerSecond;
        private DateTime motionRateWindowStarted = DateTime.UtcNow;
        private int motionReportsInWindow;
        private double motionUpdatesPerSecond;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr handle, byte[] buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        // Debug output is deliberately opt-in: a connected controller can report at several hundred Hz.
        public bool EnableRawTouchLogging { get; set; }
        public bool EnableRawMotionLogging { get; set; }

        public bool WasRecentlyActive
        {
            get
            {
                lock (sync)
                {
                    foreach (SonyDeviceRecord record in devices.Values)
                    {
                        if ((DateTime.UtcNow - record.LastPacketAt).TotalSeconds < 0.9) return true;
                    }
                    return false;
                }
            }
        }

        // Polls the Raw Input device table at a low rate so an idle DS5 is visible
        // before it emits its first input report. Report parsing remains unchanged.
        public void DiscoverConnectedDevices()
        {
            DateTime now = DateTime.UtcNow;
            lock (sync)
            {
                if ((now - lastDiscoveryAt).TotalMilliseconds < 750) return;
                lastDiscoveryAt = now;
            }

            IList<string> paths = InputManager.EnumerateRawHidDevicePaths();
            Dictionary<string, string> present = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (path.IndexOf("VID_054C", StringComparison.OrdinalIgnoreCase) < 0) continue;
                present[BuildDeviceIdentity(path)] = path;
            }

            lock (sync)
            {
                foreach (SonyDeviceRecord record in devices.Values) record.Present = false;
                foreach (KeyValuePair<string, string> item in present)
                {
                    SonyDeviceRecord record;
                    if (!devices.TryGetValue(item.Key, out record))
                    {
                        bool edge = item.Value.IndexOf("PID_0DF2", StringComparison.OrdinalIgnoreCase) >= 0;
                        record = new SonyDeviceRecord
                        {
                            RawPath = item.Value,
                            Latest = new InputSnapshot
                            {
                                DeviceId = "sony:" + item.Key,
                                TimestampUtc = now,
                                Connected = true,
                                Family = ControllerFamily.PlayStation,
                                DeviceName = edge ? "DualSense Edge" : "DualSense",
                                InputBackend = "Sony Native HID",
                                Battery = "Unknown",
                                BatteryPercent = -1,
                                ConnectionMethod = InputManager.DescribeRawInputConnection(item.Value),
                                ConnectionIsWireless = InputManager.DescribeRawInputConnection(item.Value).IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) >= 0 || InputManager.DescribeRawInputConnection(item.Value).IndexOf("蓝牙", StringComparison.OrdinalIgnoreCase) >= 0
                            }
                        };
                        devices[item.Key] = record;
                    }
                    record.RawPath = item.Value;
                    record.Present = true;
                    record.LastSeenAt = now;
                }
            }
        }

        public void ObserveRawInput(string rawPath, byte[] report)
        {
            if (string.IsNullOrEmpty(rawPath) || report == null || report.Length < 10) return;
            if (rawPath.IndexOf("VID_054C", StringComparison.OrdinalIgnoreCase) < 0) return;
            bool dualSense = rawPath.IndexOf("PID_0CE6", StringComparison.OrdinalIgnoreCase) >= 0 || rawPath.IndexOf("PID_0DF2", StringComparison.OrdinalIgnoreCase) >= 0;
            string deviceIdentity = BuildDeviceIdentity(rawPath);
            DateTime now = DateTime.UtcNow;
            InputSnapshot parsed;
            string connectionMethod = InputManager.DescribeRawInputConnection(rawPath);
            if (!TryParse(report, dualSense, deviceIdentity, connectionMethod, out parsed))
            {
                if (EnableRawTouchLogging && dualSense) Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "DS5 HID report ignored: connection={0}, report=0x{1:X2}, length={2}, device={3}", connectionMethod, report[0], report.Length, deviceIdentity));
                return;
            }
            parsed.Connected = true;
            parsed.Family = ControllerFamily.PlayStation;
            parsed.DeviceName = dualSense
                ? (rawPath.IndexOf("PID_0DF2", StringComparison.OrdinalIgnoreCase) >= 0 ? "DualSense Edge" : "DualSense 无线控制器")
                : "DUALSHOCK 4 无线控制器";
            parsed.InputBackend = "Sony 原生 HID";
            parsed.ConnectionMethod = connectionMethod;
            parsed.ConnectionIsWireless = parsed.ConnectionMethod == "蓝牙";
            parsed.Packet = unchecked(++packet);
            parsed.DeviceId = "sony:" + deviceIdentity;
            parsed.TimestampUtc = now;
            if (parsed.Motion != null) parsed.Motion.TimestampUtc = now;
            lock (sync)
            {
                SonyDeviceRecord record;
                if (!devices.TryGetValue(deviceIdentity, out record))
                {
                    record = new SonyDeviceRecord();
                    devices[deviceIdentity] = record;
                }
                record.RawPath = rawPath;
                record.Latest = parsed;
                record.LastPacketAt = now;
                record.LastSeenAt = now;
                record.Present = true;
                latest = parsed;
                lastPacketAt = now;
            }
        }

        public InputSnapshot Read()
        {
            lock (sync)
            {
                if ((DateTime.UtcNow - lastPacketAt).TotalSeconds < 3.0) return latest;
                return new InputSnapshot
                {
                    Family = ControllerFamily.PlayStation,
                    DeviceName = latest.DeviceName ?? "索尼 DS 手柄",
                    InputBackend = "Sony 原生 HID",
                    ConnectionMethod = "未连接"
                };
            }
        }

        public IList<InputSnapshot> ReadAll()
        {
            List<InputSnapshot> result = new List<InputSnapshot>();
            lock (sync)
            {
                DateTime now = DateTime.UtcNow;
                foreach (SonyDeviceRecord record in devices.Values)
                {
                    if (record.Present && (now - record.LastSeenAt).TotalSeconds < 2.0 && record.Latest != null && record.Latest.Connected) result.Add(record.Latest);
                }
            }
            return result;
        }

        public bool TryWriteOutputReport(string controllerDeviceId, byte[] report, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(controllerDeviceId) || report == null || report.Length == 0)
            {
                error = "DualSense 输出参数无效";
                return false;
            }

            string rawPath = null;
            InputSnapshot snapshot = null;
            lock (sync)
            {
                foreach (SonyDeviceRecord record in devices.Values)
                {
                    if (record.Latest == null || !string.Equals(record.Latest.DeviceId, controllerDeviceId, StringComparison.OrdinalIgnoreCase)) continue;
                    rawPath = record.RawPath;
                    snapshot = record.Latest;
                    break;
                }
            }
            if (string.IsNullOrEmpty(rawPath) || snapshot == null || !snapshot.Connected)
            {
                error = "DualSense 设备已经断开或输出路径不可用";
                return false;
            }
            if (rawPath.IndexOf("PID_0CE6", StringComparison.OrdinalIgnoreCase) < 0
                && rawPath.IndexOf("PID_0DF2", StringComparison.OrdinalIgnoreCase) < 0)
            {
                error = "当前 Sony 设备不是已识别的 DualSense 输出目标";
                return false;
            }

            bool bluetooth = snapshot.ConnectionMethod != null
                && snapshot.ConnectionMethod.IndexOf("蓝牙", StringComparison.OrdinalIgnoreCase) >= 0;
            if (bluetooth && (report.Length != DualSenseOutputReportBuilder.BluetoothReportLength || report[0] != 0x31))
            {
                error = "拒绝向蓝牙 DualSense 发送 USB 输出报告";
                return false;
            }
            if (!bluetooth && (report.Length != DualSenseOutputReportBuilder.UsbReportLength || report[0] != 0x02))
            {
                error = "拒绝向 USB DualSense 发送蓝牙输出报告";
                return false;
            }
            if (bluetooth && !DualSenseOutputReportBuilder.HasValidBluetoothCrc(report))
            {
                error = "DualSense 蓝牙输出 CRC 校验失败";
                return false;
            }

            const uint GenericWrite = 0x40000000;
            const uint ShareReadWrite = 0x00000003;
            const uint OpenExisting = 3;
            IntPtr handle = CreateFile(rawPath, GenericWrite, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                error = "无法打开 DualSense HID 输出路径，Windows 错误 " + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture);
                return false;
            }
            try
            {
                uint written;
                if (!WriteFile(handle, report, (uint)report.Length, out written, IntPtr.Zero))
                {
                    error = "DualSense HID 写入失败，Windows 错误 " + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture);
                    return false;
                }
                if (written != report.Length)
                {
                    error = string.Format(CultureInfo.InvariantCulture, "DualSense HID 写入长度不完整：{0}/{1}", written, report.Length);
                    return false;
                }
                return true;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                devices.Clear();
                latest = new InputSnapshot { Family = ControllerFamily.PlayStation };
                lastPacketAt = DateTime.MinValue;
            }
        }

        public static string RunTouchParserSelfTest()
        {
            SonyInputManager manager = new SonyInputManager();
            List<string> passed = new List<string>();
            InputSnapshot state;

            byte[] usb = new byte[64];
            usb[0] = 0x01;
            usb[10] = 0x02;
            usb[53] = 0x15;
            WriteSyntheticTouch(usb, 33, 3, true, 960, 540);
            WriteSyntheticTouch(usb, 37, 4, false, 120, 890);
            if (!manager.TryParseDualSense(usb, "SELFTEST#USB", "有线", out state) || !state.TouchpadPressed || !state.TouchCoordinatesAvailable || state.TouchPoint1 == null || !state.TouchPoint1.IsActive || state.TouchPoint1.Id != 3 || state.TouchPoint1.RawX != 960 || state.TouchPoint1.RawY != 540 || state.TouchPoint1.X < 0.49 || state.TouchPoint1.X > 0.51 || state.TouchPoint1.Y < 0.49 || state.TouchPoint1.Y > 0.51 || state.TouchPoint2 == null || state.TouchPoint2.IsActive || state.BatteryPercent != 50 || state.BatteryChargingState != "charging" || state.LightbarState != "not-parsed") throw new InvalidOperationException("USB touch/button/battery layout self-test failed.");
            passed.Add("usb-0x01-touch-press-battery-lightbar-boundary");

            byte[] bluetooth = new byte[78];
            bluetooth[0] = 0x31;
            WriteSyntheticTouch(bluetooth, 34, 12, true, 1800, 1010);
            WriteSyntheticTouch(bluetooth, 38, 13, true, 120, 50);
            WriteBluetoothCrc(bluetooth);
            if (!manager.TryParseDualSense(bluetooth, "SELFTEST#BT", "蓝牙", out state) || !state.TouchCoordinatesAvailable || state.TouchPoint1 == null || state.TouchPoint1.Id != 12 || state.TouchPoint1.RawX != 1800 || state.TouchPoint1.RawY != 1010 || state.TouchPoint2 == null || state.TouchPoint2.Id != 13) throw new InvalidOperationException("Bluetooth touch layout self-test failed.");
            passed.Add("bluetooth-0x31-78-crc");

            bluetooth[77] ^= 0x01;
            if (!manager.TryParseDualSense(bluetooth, "SELFTEST#BT", "蓝牙", out state) || state.TouchCoordinatesAvailable) throw new InvalidOperationException("Bluetooth CRC rejection self-test failed.");
            passed.Add("bluetooth-invalid-crc-rejected");

            byte[] compact = new byte[10];
            compact[0] = 0x01;
            if (!manager.TryParseDualSense(compact, "SELFTEST#BT", "蓝牙", out state) || state.TouchCoordinatesAvailable || state.HasTouchCoordinates) throw new InvalidOperationException("Bluetooth compact input self-test failed.");
            passed.Add("bluetooth-compact-no-coordinates");

            DualSenseTouchSensorDefinition mapping = new DualSenseTouchSensorDefinition
            {
                TopLeft = new DualSenseLogicalPoint { X = 10, Y = 20 },
                TopRight = new DualSenseLogicalPoint { X = 110, Y = 30 },
                BottomLeft = new DualSenseLogicalPoint { X = 20, Y = 220 },
                BottomRight = new DualSenseLogicalPoint { X = 120, Y = 230 }
            };
            Point center = DualSenseTouchVisualizer.Map(mapping, 0.5, 0.5);
            if (Math.Abs(center.X - 65) > 0.001 || Math.Abs(center.Y - 125) > 0.001) throw new InvalidOperationException("Touchpad bilinear mapping self-test failed.");
            passed.Add("bilinear-touchpad-mapping");

            DualSenseTouchVisualizer visualizer = new DualSenseTouchVisualizer();
            DateTime touchTime = DateTime.UtcNow;
            InputSnapshot touchDown = new InputSnapshot { Family = ControllerFamily.PlayStation, TouchCoordinatesAvailable = true, TouchReportSequence = 1, TouchReportUtc = touchTime, TouchPoint1 = new DualSenseTouchPoint { Id = 7, IsActive = true, X = 0.4, Y = 0.6 } };
            visualizer.Update(touchDown, false);
            if (!visualizer.HasVisibleContacts) throw new InvalidOperationException("Touch visualizer activation self-test failed.");
            InputSnapshot touchUp = new InputSnapshot { Family = ControllerFamily.PlayStation, TouchCoordinatesAvailable = true, TouchReportSequence = 2, TouchReportUtc = touchTime.AddMilliseconds(10), TouchPoint1 = new DualSenseTouchPoint { Id = 7, IsActive = false }, TouchPoint2 = new DualSenseTouchPoint { Id = 8, IsActive = false } };
            visualizer.Update(touchUp, false);
            visualizer.Advance(touchTime.AddMilliseconds(300), false);
            if (visualizer.HasVisibleContacts) throw new InvalidOperationException("Touch visualizer fade self-test failed.");
            passed.Add("touch-animation-lifecycle");
            return "DS5 touch parser self-test passed: " + string.Join(", ", passed.ToArray());
        }

        public static string RunMotionParserSelfTest()
        {
            SonyInputManager manager = new SonyInputManager();
            List<string> passed = new List<string>();
            InputSnapshot state;

            byte[] usb = new byte[64];
            usb[0] = 0x01;
            WriteSyntheticMotion(usb, 16, 1024, -2048, 512, 8192, -4096, 16384);
            if (!manager.TryParseDualSense(usb, "SELFTEST#USB", "wired", out state) || state.Motion == null || !state.Motion.IsValid || state.Motion.SourceReportId != 0x01 || state.Motion.RawGyroX != 1024 || Math.Abs(state.Motion.GyroY + 2.0) > 0.0001 || Math.Abs(state.Motion.AccelZ - 2.0) > 0.0001) throw new InvalidOperationException("USB motion layout self-test failed.");
            passed.Add("usb-0x01-64-body-gyro15-accel21");

            byte[] bluetooth = new byte[78];
            bluetooth[0] = 0x31;
            WriteSyntheticMotion(bluetooth, 17, -3072, 2048, -1024, -8192, 4096, 8192);
            WriteBluetoothCrc(bluetooth);
            if (!manager.TryParseDualSense(bluetooth, "SELFTEST#BT", "Bluetooth", out state) || state.Motion == null || !state.Motion.IsValid || state.Motion.SourceReportId != 0x31 || !state.Motion.CrcValidated || state.Motion.RawGyroX != -3072 || Math.Abs(state.Motion.AccelX + 1.0) > 0.0001) throw new InvalidOperationException("Bluetooth motion layout self-test failed.");
            passed.Add("bluetooth-0x31-78-common-body-and-crc");

            bluetooth[77] ^= 0x01;
            if (!manager.TryParseDualSense(bluetooth, "SELFTEST#BT", "Bluetooth", out state) || state.Motion == null || state.Motion.IsValid || state.Motion.CrcValidated) throw new InvalidOperationException("Bluetooth motion CRC rejection self-test failed.");
            passed.Add("bluetooth-crc-failure-rejects-motion-only");

            byte[] compact = new byte[10];
            compact[0] = 0x01;
            if (!manager.TryParseDualSense(compact, "SELFTEST#BT", "Bluetooth", out state) || state.Motion == null || state.Motion.IsValid) throw new InvalidOperationException("Compact motion compatibility self-test failed.");
            passed.Add("compact-report-motion-unsupported");
            return string.Join(", ", passed.ToArray());
        }

        private static void WriteSyntheticMotion(byte[] data, int gyroOffset, int gyroX, int gyroY, int gyroZ, int accelX, int accelY, int accelZ)
        {
            WriteInt16(data, gyroOffset, gyroX);
            WriteInt16(data, gyroOffset + 2, gyroY);
            WriteInt16(data, gyroOffset + 4, gyroZ);
            WriteInt16(data, gyroOffset + 6, accelX);
            WriteInt16(data, gyroOffset + 8, accelY);
            WriteInt16(data, gyroOffset + 10, accelZ);
        }

        private static void WriteInt16(byte[] data, int offset, int value)
        {
            short signed = (short)value;
            data[offset] = (byte)(signed & 0xFF);
            data[offset + 1] = (byte)((signed >> 8) & 0xFF);
        }

        private static void WriteSyntheticTouch(byte[] data, int offset, int id, bool active, int rawX, int rawY)
        {
            data[offset] = (byte)((active ? 0 : 0x80) | (id & 0x7F));
            data[offset + 1] = (byte)(rawX & 0xFF);
            data[offset + 2] = (byte)(((rawX >> 8) & 0x0F) | ((rawY & 0x0F) << 4));
            data[offset + 3] = (byte)((rawY >> 4) & 0xFF);
        }

        private static void WriteBluetoothCrc(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            crc = Crc32Le(crc, 0xA1);
            for (int i = 0; i < data.Length - 4; i++) crc = Crc32Le(crc, data[i]);
            uint final = ~crc;
            data[data.Length - 4] = (byte)(final & 0xFF);
            data[data.Length - 3] = (byte)((final >> 8) & 0xFF);
            data[data.Length - 2] = (byte)((final >> 16) & 0xFF);
            data[data.Length - 1] = (byte)((final >> 24) & 0xFF);
        }

        private bool TryParse(byte[] report, bool dualSense, string deviceIdentity, string connectionMethod, out InputSnapshot state)
        {
            state = null;
            if (dualSense) return TryParseDualSense(report, deviceIdentity, connectionMethod, out state);
            return TryParseDualShock4(report, out state);
        }

        private bool TryParseDualSense(byte[] data, string deviceIdentity, string connectionMethod, out InputSnapshot state)
        {
            state = null;
            DualSenseReportLayout layout;
            if (!TryGetDualSenseLayout(data, connectionMethod, out layout)) return false;
            if (!HasIndices(data, layout.AxisStart + 3, layout.ButtonStart + 2, layout.LeftTriggerIndex, layout.RightTriggerIndex)) return false;
            bool crcValidated = !layout.RequiresCrc || HasValidDualSenseBluetoothCrc(data);
            state = BuildState(data, layout.AxisStart, layout.ButtonStart, layout.LeftTriggerIndex, layout.RightTriggerIndex);
            state.TouchpadPressed = (data[layout.ButtonStart + 2] & 0x02) != 0;
            state.MicrophoneMuted = (data[layout.ButtonStart + 2] & 0x04) != 0;
            state.Motion = ParseDualSenseMotion(data, layout, connectionMethod, crcValidated);
            if (state.Motion != null && state.Motion.IsValid)
            {
                state.GyroscopeX = state.Motion.GyroX;
                state.GyroscopeY = state.Motion.GyroY;
                state.GyroscopeZ = state.Motion.GyroZ;
                state.AccelerometerX = state.Motion.AccelX;
                state.AccelerometerY = state.Motion.AccelY;
                state.AccelerometerZ = state.Motion.AccelZ;
                UpdateMotionRate(state.Motion.TimestampUtc);
            }
            // The input report does not expose the currently displayed RGB color in a
            // form this project has verified. Keep the field explicit instead of
            // presenting physical lightbar presence as a parsed state.
            state.LightbarState = "not-parsed";
            if (layout.HasTouchCoordinates && crcValidated && HasIndices(data, layout.TouchOffset, layout.TouchOffset + 7))
            {
                state.TouchPoint1 = ParseDualSenseTouchPoint(data, layout.TouchOffset);
                state.TouchPoint2 = ParseDualSenseTouchPoint(data, layout.TouchOffset + 4);
                state.TouchCoordinatesAvailable = true;
                state.HasTouchCoordinates = true;
                state.TouchReportSequence = Interlocked.Increment(ref touchReportSequence);
                state.TouchReportUtc = DateTime.UtcNow;
                UpdateTouchRate(state.TouchReportUtc);
            }
            if (layout.HasTouchCoordinates && HasIndices(data, layout.BodyStart + 52)) ApplyDualSenseBattery(state, data[layout.BodyStart + 52]);
            state.TouchDebug = CreateTouchDebugInfo(data, layout, deviceIdentity, connectionMethod, crcValidated, state);
            if (EnableRawTouchLogging && layout.HasTouchCoordinates)
            {
                Debug.WriteLine(FormatTouchDebug(state.TouchDebug, state.TouchPoint1, state.TouchPoint2));
            }
            if (EnableRawMotionLogging && state.Motion != null)
            {
                Debug.WriteLine(FormatMotionDebug(deviceIdentity, state.Motion));
            }
            return true;
        }

        private static bool TryGetDualSenseLayout(byte[] data, string connectionMethod, out DualSenseReportLayout layout)
        {
            layout = null;
            if (data == null || data.Length == 0) return false;
            bool bluetoothPath = string.Equals(connectionMethod, "蓝牙", StringComparison.OrdinalIgnoreCase);
            if (data[0] == 0x01 && !bluetoothPath && data.Length == 64)
            {
                // USB full input: Report ID 0x01, then the 63-byte shared DS5 body.
                layout = new DualSenseReportLayout { Name = "USB full input", ReportId = 0x01, MinimumLength = 64, BodyStart = 1, AxisStart = 1, ButtonStart = 8, LeftTriggerIndex = 5, RightTriggerIndex = 6, TouchOffset = 33, HasTouchCoordinates = true, HasMotionSamples = true };
                return true;
            }
            if (data[0] == 0x31 && data.Length == 78)
            {
                // Bluetooth full input: ID + sequence/tag precede the same body; final four bytes are CRC32.
                layout = new DualSenseReportLayout { Name = "Bluetooth full input", ReportId = 0x31, MinimumLength = 78, BodyStart = 2, AxisStart = 2, ButtonStart = 9, LeftTriggerIndex = 6, RightTriggerIndex = 7, TouchOffset = 34, RequiresCrc = true, HasTouchCoordinates = true, HasMotionSamples = true };
                return true;
            }
            if (data[0] == 0x01 && data.Length >= 10)
            {
                // Bluetooth can expose a compact ID 0x01 compatibility report. It contains normal buttons
                // but no native touch records, so keep the click path and explicitly expose coordinates as unavailable.
                layout = new DualSenseReportLayout { Name = "Bluetooth compact compatibility input", ReportId = 0x01, MinimumLength = 10, BodyStart = 1, AxisStart = 1, ButtonStart = 5, LeftTriggerIndex = 8, RightTriggerIndex = 9, TouchOffset = -1, HasTouchCoordinates = false, HasMotionSamples = false };
                return true;
            }
            return false;
        }

        private static DualSenseTouchPoint ParseDualSenseTouchPoint(byte[] data, int offset)
        {
            byte contact = data[offset];
            int rawX = data[offset + 1] | ((data[offset + 2] & 0x0F) << 8);
            int rawY = ((data[offset + 2] >> 4) & 0x0F) | (data[offset + 3] << 4);
            bool active = (contact & 0x80) == 0;
            return new DualSenseTouchPoint
            {
                Id = contact & 0x7F,
                RawId = (byte)(contact & 0x7F),
                IsActive = active && rawX >= 0 && rawX < DualSenseTouchRawWidth && rawY >= 0 && rawY < DualSenseTouchRawHeight,
                RawX = rawX,
                RawY = rawY,
                X = Math.Max(0, Math.Min(1, rawX / (double)(DualSenseTouchRawWidth - 1))),
                Y = Math.Max(0, Math.Min(1, rawY / (double)(DualSenseTouchRawHeight - 1)))
            };
        }

        private static short ReadInt16(byte[] data, int offset)
        {
            return (short)(data[offset] | (data[offset + 1] << 8));
        }

        private MotionSample ParseDualSenseMotion(byte[] data, DualSenseReportLayout layout, string connectionMethod, bool crcValidated)
        {
            MotionSample sample = new MotionSample
            {
                TimestampUtc = DateTime.UtcNow,
                Sequence = Interlocked.Increment(ref motionReportSequence),
                SourceReportId = layout == null ? (byte)0 : layout.ReportId,
                ConnectionType = ControllerStateAdapter.ParseConnectionType(connectionMethod),
                ConnectionLabel = connectionMethod ?? string.Empty,
                ReportLength = data == null ? 0 : data.Length,
                CrcValidated = crcValidated,
                Layout = layout == null ? string.Empty : layout.Name
            };
            if (layout == null || !layout.HasMotionSamples)
            {
                sample.AvailabilityMessage = "当前兼容输入报告未包含运动传感器字段。";
                return sample;
            }
            if (!crcValidated)
            {
                sample.AvailabilityMessage = "蓝牙 HID CRC 校验失败，已拒绝运动传感器数据。";
                return sample;
            }
            if (!HasIndices(data, layout.BodyStart + 26))
            {
                sample.AvailabilityMessage = "运动传感器报告长度不足，无法安全读取字段。";
                return sample;
            }
            // Linux hid-playstation dualsense_input_report: gyro[3] starts at shared body +15,
            // accel[3] at +21. Both USB and BT layouts above point BodyStart to that same body.
            sample.RawGyroX = ReadInt16(data, layout.BodyStart + 15);
            sample.RawGyroY = ReadInt16(data, layout.BodyStart + 17);
            sample.RawGyroZ = ReadInt16(data, layout.BodyStart + 19);
            sample.RawAccelX = ReadInt16(data, layout.BodyStart + 21);
            sample.RawAccelY = ReadInt16(data, layout.BodyStart + 23);
            sample.RawAccelZ = ReadInt16(data, layout.BodyStart + 25);
            sample.GyroX = DualSenseMotionUnits.GyroToDegreesPerSecond(sample.RawGyroX);
            sample.GyroY = DualSenseMotionUnits.GyroToDegreesPerSecond(sample.RawGyroY);
            sample.GyroZ = DualSenseMotionUnits.GyroToDegreesPerSecond(sample.RawGyroZ);
            sample.AccelX = DualSenseMotionUnits.AccelToG(sample.RawAccelX);
            sample.AccelY = DualSenseMotionUnits.AccelToG(sample.RawAccelY);
            sample.AccelZ = DualSenseMotionUnits.AccelToG(sample.RawAccelZ);
            sample.IsValid = IsFiniteMotion(sample);
            sample.AvailabilityMessage = sample.IsValid ? string.Empty : "运动传感器换算结果无效。";
            return sample;
        }

        private static bool IsFiniteMotion(MotionSample sample)
        {
            if (sample == null) return false;
            return !double.IsNaN(sample.GyroX) && !double.IsInfinity(sample.GyroX)
                && !double.IsNaN(sample.GyroY) && !double.IsInfinity(sample.GyroY)
                && !double.IsNaN(sample.GyroZ) && !double.IsInfinity(sample.GyroZ)
                && !double.IsNaN(sample.AccelX) && !double.IsInfinity(sample.AccelX)
                && !double.IsNaN(sample.AccelY) && !double.IsInfinity(sample.AccelY)
                && !double.IsNaN(sample.AccelZ) && !double.IsInfinity(sample.AccelZ);
        }

        private void UpdateMotionRate(DateTime now)
        {
            motionReportsInWindow++;
            double elapsed = (now - motionRateWindowStarted).TotalSeconds;
            if (elapsed >= 0.5)
            {
                motionUpdatesPerSecond = motionReportsInWindow / elapsed;
                motionReportsInWindow = 0;
                motionRateWindowStarted = now;
            }
        }

        private static string FormatMotionDebug(string deviceIdentity, MotionSample sample)
        {
            if (sample == null) return "DS5 motion: unavailable";
            return string.Format(CultureInfo.InvariantCulture,
                "DS5 motion: device={0}, connection={1}, report=0x{2:X2}, length={3}, crc={4}, rawGyro=({5},{6},{7}), rawAccel=({8},{9},{10}), gyro=({11:0.000},{12:0.000},{13:0.000}) deg/s, accel=({14:0.000},{15:0.000},{16:0.000}) g, valid={17}",
                deviceIdentity, sample.ConnectionLabel, sample.SourceReportId, sample.ReportLength, sample.CrcValidated,
                sample.RawGyroX, sample.RawGyroY, sample.RawGyroZ, sample.RawAccelX, sample.RawAccelY, sample.RawAccelZ,
                sample.GyroX, sample.GyroY, sample.GyroZ, sample.AccelX, sample.AccelY, sample.AccelZ, sample.IsValid);
        }

        private void UpdateTouchRate(DateTime now)
        {
            touchReportsInWindow++;
            double elapsed = (now - touchRateWindowStarted).TotalSeconds;
            if (elapsed >= 0.5)
            {
                touchUpdatesPerSecond = touchReportsInWindow / elapsed;
                touchReportsInWindow = 0;
                touchRateWindowStarted = now;
            }
        }

        private DualSenseTouchDebugInfo CreateTouchDebugInfo(byte[] data, DualSenseReportLayout layout, string deviceIdentity, string connectionMethod, bool crcValidated, InputSnapshot state)
        {
            DualSenseTouchDebugInfo info = new DualSenseTouchDebugInfo
            {
                DeviceIdentity = deviceIdentity,
                ConnectionMethod = connectionMethod,
                ReportId = layout.ReportId,
                ReportLength = data == null ? 0 : data.Length,
                TouchOffset = layout.TouchOffset,
                Layout = layout.Name,
                CrcValidated = crcValidated,
                CoordinatesAvailable = state != null && state.TouchCoordinatesAvailable,
                UpdatesPerSecond = touchUpdatesPerSecond,
                AvailabilityMessage = layout.HasTouchCoordinates
                    ? (crcValidated ? "原生 HID 触摸坐标可用" : "蓝牙 HID CRC 校验失败，已禁用触摸坐标")
                    : "当前连接模式不支持触摸坐标（仅触摸板按压）"
            };
            if (layout.HasTouchCoordinates && HasIndices(data, layout.TouchOffset, layout.TouchOffset + 7))
            {
                info.RawTouchBytes = new byte[8];
                Buffer.BlockCopy(data, layout.TouchOffset, info.RawTouchBytes, 0, 8);
            }
            return info;
        }

        private static bool HasValidDualSenseBluetoothCrc(byte[] data)
        {
            if (data == null || data.Length < 78) return false;
            uint crc = 0xFFFFFFFF;
            crc = Crc32Le(crc, 0xA1);
            for (int i = 0; i < data.Length - 4; i++) crc = Crc32Le(crc, data[i]);
            uint expected = (uint)(data[data.Length - 4] | (data[data.Length - 3] << 8) | (data[data.Length - 2] << 16) | (data[data.Length - 1] << 24));
            return ~crc == expected;
        }

        private static uint Crc32Le(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320U : crc >> 1;
            return crc;
        }

        private static string BuildDeviceIdentity(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath)) return string.Empty;
            string[] segments = rawPath.ToUpperInvariant().Split('#');
            if (segments.Length < 3) return rawPath.ToUpperInvariant();
            string instance = segments[2];
            int collection = instance.IndexOf("&COL", StringComparison.Ordinal);
            if (collection >= 0) instance = instance.Substring(0, collection);
            return segments[1] + "#" + instance;
        }

        private static string FormatTouchDebug(DualSenseTouchDebugInfo info, DualSenseTouchPoint first, DualSenseTouchPoint second)
        {
            string bytes = info == null || info.RawTouchBytes == null ? "-" : BitConverter.ToString(info.RawTouchBytes);
            return string.Format(CultureInfo.InvariantCulture,
                "DS5 touch: connection={0}, report=0x{1:X2}, length={2}, offset={3}, bytes={4}, p1={5}, p2={6}",
                info == null ? "-" : info.ConnectionMethod, info == null ? 0 : info.ReportId, info == null ? 0 : info.ReportLength, info == null ? -1 : info.TouchOffset, bytes, DescribeTouch(first), DescribeTouch(second));
        }

        private static string DescribeTouch(DualSenseTouchPoint point)
        {
            if (point == null) return "none";
            return string.Format(CultureInfo.InvariantCulture, "id={0},active={1},raw=({2},{3}),norm=({4:0.000},{5:0.000})", point.Id, point.IsActive, point.RawX, point.RawY, point.X, point.Y);
        }

        private static bool TryParseDualShock4(byte[] data, out InputSnapshot state)
        {
            state = null;
            int axisStart;
            if (data[0] == 0x01) axisStart = 1;
            else if (data[0] == 0x11) axisStart = 3;
            else return false;
            int buttonStart = axisStart + 4;
            int leftTrigger = axisStart + 7;
            int rightTrigger = axisStart + 8;
            if (!HasIndices(data, axisStart + 3, buttonStart + 2, leftTrigger, rightTrigger)) return false;
            state = BuildState(data, axisStart, buttonStart, leftTrigger, rightTrigger);
            state.TouchpadPressed = (data[buttonStart + 2] & 0x02) != 0;
            int statusIndex = axisStart + 29;
            if (statusIndex < data.Length) ApplyDualShock4Battery(state, data[statusIndex]);
            return true;
        }

        private static InputSnapshot BuildState(byte[] data, int axisStart, int buttonStart, int leftTriggerIndex, int rightTriggerIndex)
        {
            byte faceDpad = data[buttonStart];
            byte shoulders = data[buttonStart + 1];
            byte system = data[buttonStart + 2];
            ushort buttons = MapButtons(faceDpad, shoulders, system);
            return new InputSnapshot
            {
                LeftX = Axis(data[axisStart]),
                LeftY = AxisY(data[axisStart + 1]),
                RightX = Axis(data[axisStart + 2]),
                RightY = AxisY(data[axisStart + 3]),
                LeftTrigger = data[leftTriggerIndex],
                RightTrigger = data[rightTriggerIndex],
                Buttons = buttons,
                Battery = "电量读取中",
                BatteryPercent = -1
            };
        }

        private static bool HasIndices(byte[] data, params int[] indices)
        {
            for (int i = 0; i < indices.Length; i++) if (indices[i] < 0 || indices[i] >= data.Length) return false;
            return true;
        }

        private static int Axis(byte value)
        {
            return Math.Max(-32768, Math.Min(32767, (value - 128) * 257));
        }

        private static int AxisY(byte value)
        {
            return Math.Max(-32768, Math.Min(32767, (128 - value) * 257));
        }

        private static ushort MapButtons(byte faceDpad, byte shoulders, byte system)
        {
            ushort value = MapDpad((byte)(faceDpad & 0x0F));
            if ((faceDpad & 0x10) != 0) value |= 0x4000; // Square -> X slot
            if ((faceDpad & 0x20) != 0) value |= 0x1000; // Cross -> A slot
            if ((faceDpad & 0x40) != 0) value |= 0x2000; // Circle -> B slot
            if ((faceDpad & 0x80) != 0) value |= 0x8000; // Triangle -> Y slot
            if ((shoulders & 0x01) != 0) value |= 0x0100;
            if ((shoulders & 0x02) != 0) value |= 0x0200;
            if ((shoulders & 0x10) != 0) value |= 0x0020; // Create / Share
            if ((shoulders & 0x20) != 0) value |= 0x0010; // Options
            if ((shoulders & 0x40) != 0) value |= 0x0040;
            if ((shoulders & 0x80) != 0) value |= 0x0080;
            if ((system & 0x01) != 0) value |= 0x0400; // PS
            if ((system & 0x02) != 0) value |= 0x0800; // Touchpad click
            return value;
        }

        private static ushort MapDpad(byte hat)
        {
            if (hat == 0) return 0x0001;
            if (hat == 1) return 0x0001 | 0x0008;
            if (hat == 2) return 0x0008;
            if (hat == 3) return 0x0008 | 0x0002;
            if (hat == 4) return 0x0002;
            if (hat == 5) return 0x0002 | 0x0004;
            if (hat == 6) return 0x0004;
            if (hat == 7) return 0x0004 | 0x0001;
            return 0;
        }

        private static void ApplyDualSenseBattery(InputSnapshot state, byte status)
        {
            int capacity = status & 0x0F;
            if (capacity > 10) return;
            state.BatteryPercent = Math.Min(100, capacity * 10);
            int powerState = (status >> 4) & 0x0F;
            state.BatteryChargingState = powerState == 1 ? "charging" : powerState == 2 ? "complete" : powerState == 0 ? "discharging" : "unknown";
            state.Battery = powerState == 1 ? "充电中" : powerState == 2 ? "已充满" : state.BatteryPercent >= 90 ? "满电" : state.BatteryPercent >= 40 ? "使用中" : "低电量";
        }

        private static void ApplyDualShock4Battery(InputSnapshot state, byte status)
        {
            int capacity = status & 0x0F;
            if (capacity > 11) return;
            state.BatteryPercent = Math.Min(100, capacity * 10);
            state.Battery = capacity >= 10 ? "满电" : capacity >= 4 ? "使用中" : "低电量";
        }
    }
}
