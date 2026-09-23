// InputManager
//
// Extracted verbatim from ControllerLab.cs (lines 6435-7036) on 2026-09-22
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
    public sealed class InputManager : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short LeftX;
            public short LeftY;
            public short RightX;
            public short RightY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint PacketNumber;
            public XInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputBattery
        {
            public byte Type;
            public byte Level;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputVibration
        {
            public ushort LeftMotorSpeed;
            public ushort RightMotorSpeed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputCapabilities
        {
            public byte Type;
            public byte SubType;
            public ushort Flags;
            public XInputGamepad Gamepad;
            public XInputVibration Vibration;
        }

        private struct BatteryReading
        {
            public string Label;
            public int ApproxPercent;
            public bool TelemetryUnavailable;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetStateDelegate(uint index, out XInputState state);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetBatteryDelegate(uint index, byte deviceType, out XInputBattery battery);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetCapabilitiesDelegate(uint index, uint flags, out XInputCapabilities capabilities);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint SetStateDelegate(uint index, ref XInputVibration vibration);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr module);

        private IntPtr module;
        private GetStateDelegate getState;
        private GetBatteryDelegate getBattery;
        private GetCapabilitiesDelegate getCapabilities;
        private SetStateDelegate setState;
        private readonly BatteryReading[] cachedBattery =
        {
            new BatteryReading { Label = "—", ApproxPercent = -1 },
            new BatteryReading { Label = "—", ApproxPercent = -1 },
            new BatteryReading { Label = "—", ApproxPercent = -1 },
            new BatteryReading { Label = "—", ApproxPercent = -1 }
        };
        private readonly DateTime[] batteryCheckedAt = { DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue };
        private readonly bool[] cachedWireless = { false, false, false, false };
        private readonly DateTime[] wirelessCheckedAt = { DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue };
        private string cachedConnectionMethod = "检测中";
        private DateTime connectionMethodCheckedAt = DateTime.MinValue;
        private readonly object rawInputLock = new object();
        private string lastActiveRawDevicePath;
        private DateTime lastActiveRawDeviceAt = DateTime.MinValue;
        private string wiredUsbRoute;
        private string receiverUsbRoute;
        private string currentUsbRoute;
        public string LibraryName { get; private set; }

        public string WiredUsbRoute
        {
            get { lock (rawInputLock) return wiredUsbRoute; }
        }

        public string ReceiverUsbRoute
        {
            get { lock (rawInputLock) return receiverUsbRoute; }
        }

        public InputManager()
        {
            string[] libraries = { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" };
            for (int i = 0; i < libraries.Length; i++)
            {
                module = LoadLibrary(libraries[i]);
                if (module == IntPtr.Zero) continue;
                IntPtr statePtr = GetProcAddress(module, "XInputGetState");
                if (statePtr != IntPtr.Zero)
                {
                    getState = (GetStateDelegate)Marshal.GetDelegateForFunctionPointer(statePtr, typeof(GetStateDelegate));
                    IntPtr batteryPtr = GetProcAddress(module, "XInputGetBatteryInformation");
                    if (batteryPtr != IntPtr.Zero) getBattery = (GetBatteryDelegate)Marshal.GetDelegateForFunctionPointer(batteryPtr, typeof(GetBatteryDelegate));
                    IntPtr capabilitiesPtr = GetProcAddress(module, "XInputGetCapabilities");
                    if (capabilitiesPtr != IntPtr.Zero) getCapabilities = (GetCapabilitiesDelegate)Marshal.GetDelegateForFunctionPointer(capabilitiesPtr, typeof(GetCapabilitiesDelegate));
                    IntPtr setStatePtr = GetProcAddress(module, "XInputSetState");
                    if (setStatePtr != IntPtr.Zero) setState = (SetStateDelegate)Marshal.GetDelegateForFunctionPointer(setStatePtr, typeof(SetStateDelegate));
                    LibraryName = libraries[i].Replace(".dll", "");
                    break;
                }
                FreeLibrary(module);
                module = IntPtr.Zero;
            }
            if (getState == null) LibraryName = "XInput 不可用";
        }

        public bool CanSetVibration { get { return setState != null; } }

        public bool TrySetVibration(int playerIndex, ushort leftMotorSpeed, ushort rightMotorSpeed, out string error)
        {
            error = null;
            if (setState == null)
            {
                error = "当前 XInput 库未提供 XInputSetState";
                return false;
            }
            if (playerIndex < 0 || playerIndex > 3)
            {
                error = "XInput 玩家槽位无效";
                return false;
            }
            XInputVibration vibration = new XInputVibration
            {
                LeftMotorSpeed = leftMotorSpeed,
                RightMotorSpeed = rightMotorSpeed
            };
            uint result = setState((uint)playerIndex, ref vibration);
            if (result == 0) return true;
            error = result == 1167
                ? "设备已经断开"
                : "XInputSetState 失败，错误码 " + result.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        public bool TryGetVibrationCapabilities(int playerIndex, out bool supportsLeft, out bool supportsRight, out string details)
        {
            supportsLeft = false;
            supportsRight = false;
            details = null;
            if (playerIndex < 0 || playerIndex > 3)
            {
                details = "XInput 玩家槽位无效";
                return false;
            }
            if (getCapabilities == null)
            {
                // Older XInput DLLs may expose SetState without capabilities.
                supportsLeft = setState != null;
                supportsRight = setState != null;
                details = "XInput 未提供能力查询，按标准双电机兼容路径处理";
                return setState != null;
            }
            XInputCapabilities capabilities;
            uint result = getCapabilities((uint)playerIndex, 0, out capabilities);
            if (result != 0)
            {
                details = result == 1167 ? "设备已经断开" : "XInputGetCapabilities 失败";
                return false;
            }
            supportsLeft = capabilities.Vibration.LeftMotorSpeed != 0;
            supportsRight = capabilities.Vibration.RightMotorSpeed != 0;
            details = string.Format(CultureInfo.InvariantCulture, "左电机 {0}，右电机 {1}", supportsLeft ? "可用" : "不支持", supportsRight ? "可用" : "不支持");
            return true;
        }

        public InputSnapshot Read(int preferredIndex)
        {
            if (getState == null) return new InputSnapshot { Index = Math.Max(0, preferredIndex) };
            if (preferredIndex >= 0 && preferredIndex < 4)
            {
                InputSnapshot selected = ReadIndex((uint)preferredIndex);
                return selected ?? new InputSnapshot { Index = preferredIndex };
            }
            for (uint index = 0; index < 4; index++)
            {
                InputSnapshot found = ReadIndex(index);
                if (found != null) return found;
            }
            return new InputSnapshot();
        }

        public InputSnapshot ReadFirst()
        {
            return Read(-1);
        }

        // A catalog consumer needs every online XInput slot, whereas the legacy Read
        // method intentionally returns only the current preferred controller.
        public IList<InputSnapshot> ReadAll()
        {
            List<InputSnapshot> result = new List<InputSnapshot>();
            if (getState == null) return result;
            for (uint index = 0; index < 4; index++)
            {
                InputSnapshot state = ReadIndex(index);
                if (state != null) result.Add(state);
            }
            return result;
        }

        private InputSnapshot ReadIndex(uint index)
        {
            XInputState state;
            if (getState(index, out state) != 0) return null;
            BatteryReading battery = ReadBatteryCached(index);
            bool wireless = ReadWirelessCapability(index);
            return new InputSnapshot
            {
                DeviceId = "xinput:" + index.ToString(CultureInfo.InvariantCulture),
                TimestampUtc = DateTime.UtcNow,
                Connected = true,
                Family = ControllerFamily.Xbox,
                DeviceName = "Xbox 无线手柄",
                InputBackend = LibraryName,
                Index = (int)index,
                Packet = state.PacketNumber,
                Buttons = state.Gamepad.Buttons,
                LeftTrigger = state.Gamepad.LeftTrigger,
                RightTrigger = state.Gamepad.RightTrigger,
                LeftX = state.Gamepad.LeftX,
                LeftY = state.Gamepad.LeftY,
                RightX = state.Gamepad.RightX,
                RightY = state.Gamepad.RightY,
                Battery = battery.Label,
                BatteryPercent = battery.ApproxPercent,
                BatteryTelemetryUnavailable = battery.TelemetryUnavailable,
                ConnectionMethod = ReadConnectionMethod(wireless),
                ConnectionIsWireless = wireless
            };
        }

        private bool ReadWirelessCapability(uint index)
        {
            int slot = (int)index;
            if ((DateTime.UtcNow - wirelessCheckedAt[slot]).TotalSeconds < 1.0) return cachedWireless[slot];
            wirelessCheckedAt[slot] = DateTime.UtcNow;
            if (getCapabilities == null) return cachedWireless[slot];
            XInputCapabilities capabilities;
            if (getCapabilities(index, 0, out capabilities) != 0) return cachedWireless[slot];
            cachedWireless[slot] = (capabilities.Flags & 0x0002) != 0; // XINPUT_CAPS_WIRELESS
            return cachedWireless[slot];
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDeviceList
        {
            public IntPtr Device;
            public uint Type;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList([In, Out] RawInputDeviceList[] list, ref uint count, uint size);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder data, ref uint size);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNode(out uint devInst, string deviceId, int flags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Parent(out uint parentDevInst, uint devInst, int flags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Device_ID_Size(out uint length, uint devInst, int flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_ID(uint devInst, StringBuilder buffer, uint bufferLength, int flags);

        public void SetUsbRouteProfiles(string wiredRoute, string receiverRoute)
        {
            lock (rawInputLock)
            {
                wiredUsbRoute = NormalizeRouteKey(wiredRoute);
                receiverUsbRoute = NormalizeRouteKey(receiverRoute);
                connectionMethodCheckedAt = DateTime.MinValue;
            }
        }

        public bool MarkCurrentUsbRoute(string mode)
        {
            lock (rawInputLock)
            {
                if (string.IsNullOrEmpty(currentUsbRoute)) return false;
                if (mode == "有线")
                {
                    wiredUsbRoute = currentUsbRoute;
                    if (string.Equals(receiverUsbRoute, currentUsbRoute, StringComparison.OrdinalIgnoreCase)) receiverUsbRoute = null;
                }
                else if (mode == "USB 2.4G")
                {
                    receiverUsbRoute = currentUsbRoute;
                    if (string.Equals(wiredUsbRoute, currentUsbRoute, StringComparison.OrdinalIgnoreCase)) wiredUsbRoute = null;
                }
                else return false;
                connectionMethodCheckedAt = DateTime.MinValue;
                return true;
            }
        }

        private string ReadConnectionMethod(bool isWireless)
        {
            if ((DateTime.UtcNow - connectionMethodCheckedAt).TotalSeconds < 1.0) return cachedConnectionMethod;
            connectionMethodCheckedAt = DateTime.UtcNow;
            try
            {
                // Bluetooth XInput HID stays enumerable even when the most recent WM_INPUT packet belongs to an
                // older USB route.  Give a live Bluetooth XInput endpoint priority over that short-lived cache.
                if (isWireless && FindBluetoothXInputRawDevice())
                {
                    cachedConnectionMethod = "蓝牙";
                    return cachedConnectionMethod;
                }
                string rawPath = GetRecentActiveRawDevice();
                if (string.IsNullOrEmpty(rawPath)) rawPath = FindActiveXInputRawDevice();
                if (string.IsNullOrEmpty(rawPath))
                {
                    cachedConnectionMethod = "检测中";
                    return cachedConnectionMethod;
                }
                string deviceId = ToDeviceInstanceId(rawPath);
                string current = deviceId;
                bool foundUsbTransport = false;
                for (int i = 0; i < 6 && !string.IsNullOrEmpty(current); i++)
                {
                    string upper = current.ToUpperInvariant();
                    if (IsBluetoothTransportNode(current))
                    {
                        cachedConnectionMethod = "蓝牙";
                        return cachedConnectionMethod;
                    }
                    if (upper.StartsWith("USB\\")) foundUsbTransport = true;
                    current = GetParentDeviceInstanceId(current);
                }
                string routeKey = foundUsbTransport ? NormalizeRouteKey(deviceId) : null;
                string wiredRoute;
                string receiverRoute;
                lock (rawInputLock)
                {
                    currentUsbRoute = routeKey;
                    wiredRoute = wiredUsbRoute;
                    receiverRoute = receiverUsbRoute;
                }
                if (!string.IsNullOrEmpty(routeKey) && string.Equals(routeKey, wiredRoute, StringComparison.OrdinalIgnoreCase))
                {
                    cachedConnectionMethod = "有线";
                    return cachedConnectionMethod;
                }
                if (!string.IsNullOrEmpty(routeKey) && string.Equals(routeKey, receiverRoute, StringComparison.OrdinalIgnoreCase))
                {
                    cachedConnectionMethod = "USB 2.4G 接收器";
                    return cachedConnectionMethod;
                }
                // XInput wireless capability is not a reliable transport discriminator for this controller's
                // emulated XInput endpoint.  An unknown USB path remains explicitly unclassified.
                cachedConnectionMethod = foundUsbTransport ? "USB 通道（待标记）" : "检测中";
                return cachedConnectionMethod;
            }
            catch
            {
                cachedConnectionMethod = "检测中";
                return cachedConnectionMethod;
            }
        }

        public void ObserveRawInputDevicePath(string rawPath)
        {
            // WM_INPUT is registered only for generic-desktop Gamepad/Joystick usages.  Bluetooth HID paths do not
            // consistently carry the XInput "&IG_" marker, so keeping that old filter made us reuse a stale USB path.
            if (string.IsNullOrEmpty(rawPath)) return;
            lock (rawInputLock)
            {
                lastActiveRawDevicePath = rawPath;
                lastActiveRawDeviceAt = DateTime.UtcNow;
                connectionMethodCheckedAt = DateTime.MinValue;
            }
        }

        private string GetRecentActiveRawDevice()
        {
            lock (rawInputLock)
            {
                return (DateTime.UtcNow - lastActiveRawDeviceAt).TotalSeconds < 12.0 ? lastActiveRawDevicePath : null;
            }
        }

        private static string FindActiveXInputRawDevice()
        {
            uint count = 0;
            uint size = (uint)Marshal.SizeOf(typeof(RawInputDeviceList));
            if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue || count == 0) return null;
            RawInputDeviceList[] devices = new RawInputDeviceList[count];
            if (GetRawInputDeviceList(devices, ref count, size) == uint.MaxValue) return null;
            for (int i = 0; i < count; i++)
            {
                // RIM_TYPEHID = 2; XInput HID interfaces carry the &IG_ marker in their device path.
                if (devices[i].Type != 2) continue;
                string value = GetRawDevicePath(devices[i].Device);
                if (string.IsNullOrEmpty(value)) continue;
                if (value.IndexOf("&IG_", StringComparison.OrdinalIgnoreCase) >= 0) return value;
            }
            return null;
        }

        private static bool FindBluetoothXInputRawDevice()
        {
            uint count = 0;
            uint size = (uint)Marshal.SizeOf(typeof(RawInputDeviceList));
            if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue || count == 0) return false;
            RawInputDeviceList[] devices = new RawInputDeviceList[count];
            if (GetRawInputDeviceList(devices, ref count, size) == uint.MaxValue) return false;
            for (int i = 0; i < count; i++)
            {
                if (devices[i].Type != 2) continue;
                string rawPath = GetRawDevicePath(devices[i].Device);
                if (string.IsNullOrEmpty(rawPath) || rawPath.IndexOf("&IG_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string current = ToDeviceInstanceId(rawPath);
                for (int depth = 0; depth < 6 && !string.IsNullOrEmpty(current); depth++)
                {
                    if (IsBluetoothTransportNode(current)) return true;
                    current = GetParentDeviceInstanceId(current);
                }
            }
            return false;
        }

        public static string GetRawDevicePath(IntPtr device)
        {
            if (device == IntPtr.Zero) return null;
            uint chars = 0;
            GetRawInputDeviceInfo(device, 0x20000007, null, ref chars);
            if (chars == 0) return null;
            StringBuilder path = new StringBuilder((int)chars + 1);
            if (GetRawInputDeviceInfo(device, 0x20000007, path, ref chars) == uint.MaxValue) return null;
            return path.ToString();
        }

        // Discovery uses the same Raw Input device table as live reports, but does
        // not require a button press. This keeps a connected DualSense visible on
        // the home page while it is idle.
        public static IList<string> EnumerateRawHidDevicePaths()
        {
            List<string> result = new List<string>();
            uint count = 0;
            uint size = (uint)Marshal.SizeOf(typeof(RawInputDeviceList));
            if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue || count == 0) return result;
            RawInputDeviceList[] devices = new RawInputDeviceList[count];
            if (GetRawInputDeviceList(devices, ref count, size) == uint.MaxValue) return result;
            for (int i = 0; i < count; i++)
            {
                if (devices[i].Type != 2) continue;
                string path = GetRawDevicePath(devices[i].Device);
                if (!string.IsNullOrEmpty(path)) result.Add(path);
            }
            return result;
        }

        private static string ToDeviceInstanceId(string rawPath)
        {
            string value = rawPath;
            if (value.StartsWith("\\\\?\\", StringComparison.Ordinal)) value = value.Substring(4);
            // Bluetooth HID instance IDs can begin with a service GUID (for example HID#{00001124...}).
            // The Raw Input interface GUID is the final "#{...}" component, so only remove that last component.
            int guidStart = value.LastIndexOf("#{", StringComparison.Ordinal);
            if (guidStart >= 0) value = value.Substring(0, guidStart);
            return value.Replace('#', '\\');
        }

        private static string NormalizeRouteKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        private static string GetParentDeviceInstanceId(string deviceId)
        {
            uint devInst;
            if (CM_Locate_DevNode(out devInst, deviceId, 0) != 0) return null;
            uint parent;
            if (CM_Get_Parent(out parent, devInst, 0) != 0) return null;
            uint length;
            if (CM_Get_Device_ID_Size(out length, parent, 0) != 0) return null;
            StringBuilder buffer = new StringBuilder((int)length + 1);
            if (CM_Get_Device_ID(parent, buffer, length + 1, 0) != 0) return null;
            return buffer.ToString();
        }

        private static bool IsBluetoothTransportNode(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            string upper = deviceId.ToUpperInvariant();
            if (upper.StartsWith("BTHLE\\") || upper.StartsWith("BTHENUM\\") || upper.StartsWith("BTH\\")) return true;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + deviceId))
                {
                    if (key == null) return false;
                    string[] names = { "Service", "Class", "ClassGUID", "DeviceDesc", "FriendlyName", "Mfg" };
                    for (int i = 0; i < names.Length; i++)
                    {
                        string value = key.GetValue(names[i]) as string;
                        if (string.IsNullOrEmpty(value)) continue;
                        if (value.IndexOf("BLUETOOTH", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            value.IndexOf("BTH", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    }
                }
            }
            catch
            {
                // Device metadata is an optional signal.  A missing registry value must not interrupt live input.
            }
            return false;
        }

        public static string DescribeRawInputConnection(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath)) return "检测中";
            try
            {
                string current = ToDeviceInstanceId(rawPath);
                bool usb = false;
                for (int depth = 0; depth < 6 && !string.IsNullOrEmpty(current); depth++)
                {
                    if (IsBluetoothTransportNode(current)) return "蓝牙";
                    if (current.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)) usb = true;
                    current = GetParentDeviceInstanceId(current);
                }
                return usb ? "有线" : "原生 HID";
            }
            catch
            {
                return rawPath.IndexOf("BTH", StringComparison.OrdinalIgnoreCase) >= 0 ? "蓝牙" : "原生 HID";
            }
        }

        private BatteryReading ReadBatteryCached(uint index)
        {
            int slot = (int)index;
            if ((DateTime.UtcNow - batteryCheckedAt[slot]).TotalSeconds < 2.0) return cachedBattery[slot];
            batteryCheckedAt[slot] = DateTime.UtcNow;
            cachedBattery[slot] = ReadBattery(index);
            return cachedBattery[slot];
        }

        private BatteryReading ReadBattery(uint index)
        {
            if (getBattery == null) return new BatteryReading { Label = "状态未知", ApproxPercent = -1 };
            XInputBattery battery;
            if (getBattery(index, 0, out battery) != 0) return new BatteryReading { Label = "状态未知", ApproxPercent = -1 };
            if (battery.Type == 0) return new BatteryReading { Label = "未连接", ApproxPercent = -1 };
            // Bluetooth and 2.4 GHz receivers can expose an XInput USB-style path even while the controller is physically wireless.
            // BATTERY_TYPE_WIRED therefore means this API did not provide a usable battery reading, not proof of a cable connection.
            if (battery.Type == 1) return new BatteryReading { Label = "电量未上报", ApproxPercent = -1, TelemetryUnavailable = true };
            if (battery.Type == 0xFF) return new BatteryReading { Label = "类型未知", ApproxPercent = -1 };
            if (battery.Level == 3) return new BatteryReading { Label = "满电", ApproxPercent = 100 };
            if (battery.Level == 2) return new BatteryReading { Label = "中等", ApproxPercent = 65 };
            if (battery.Level == 1) return new BatteryReading { Label = "低电量", ApproxPercent = 25 };
            return new BatteryReading { Label = "电量耗尽", ApproxPercent = 5 };
        }

        public void Dispose()
        {
            if (setState != null)
            {
                for (uint index = 0; index < 4; index++)
                {
                    XInputVibration stop = new XInputVibration();
                    try { setState(index, ref stop); }
                    catch { }
                }
            }
            if (module != IntPtr.Zero) FreeLibrary(module);
            module = IntPtr.Zero;
            setState = null;
        }
    }
}
