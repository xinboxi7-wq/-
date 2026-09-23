// InputSnapshot
//
// Extracted verbatim from ControllerLab.cs (lines 6630-6831) on 2026-09-22
// as part of the ControllerLab structural split. No logic was changed.
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
    public sealed class InputSnapshot
    {
        // Stable identity and timestamp make raw input interchangeable at the UI boundary.
        public string DeviceId;
        public DateTime TimestampUtc;
        public bool Connected;
        public ControllerFamily Family = ControllerFamily.Xbox;
        public string DeviceName = "Xbox 无线手柄";
        public string InputBackend = "XInput";
        public int Index;
        public uint Packet;
        public ushort Buttons;
        public int LeftTrigger;
        public int RightTrigger;
        public int LeftX;
        public int LeftY;
        public int RightX;
        public int RightY;
        public string Battery = "—";
        public int BatteryPercent = -1;
        public bool BatteryTelemetryUnavailable;
        public string ConnectionMethod = "检测中";
        public bool ConnectionIsWireless;
        public bool TouchpadPressed;
        public bool MicrophoneMuted;
        public double GyroscopeX;
        public double GyroscopeY;
        public double GyroscopeZ;
        public double AccelerometerX;
        public double AccelerometerY;
        public double AccelerometerZ;
        public MotionSample Motion;
        public string LightbarState;
        public string BatteryChargingState;
        // True DualSense touch data is supplied only by a validated native HID report. XInput and demo
        // paths intentionally leave these fields empty instead of inventing input.
        public bool TouchCoordinatesAvailable;
        public bool HasTouchCoordinates;
        public DualSenseTouchPoint TouchPoint1;
        public DualSenseTouchPoint TouchPoint2;
        public long TouchReportSequence;
        public DateTime TouchReportUtc;
        public DualSenseTouchDebugInfo TouchDebug;

        public double LeftNormalizedX { get { return Normalize(LeftX); } }
        public double LeftNormalizedY { get { return Normalize(LeftY); } }
        public double RightNormalizedX { get { return Normalize(RightX); } }
        public double RightNormalizedY { get { return Normalize(RightY); } }

        public InputSnapshot WithOffsets(double lx, double ly, double rx, double ry)
        {
            return new InputSnapshot
            {
                DeviceId = DeviceId,
                TimestampUtc = TimestampUtc,
                Connected = Connected,
                Family = Family,
                DeviceName = DeviceName,
                InputBackend = InputBackend,
                Index = Index,
                Packet = Packet,
                Buttons = Buttons,
                LeftTrigger = LeftTrigger,
                RightTrigger = RightTrigger,
                LeftX = ClampShort(LeftX - lx),
                LeftY = ClampShort(LeftY - ly),
                RightX = ClampShort(RightX - rx),
                RightY = ClampShort(RightY - ry),
                Battery = Battery,
                BatteryPercent = BatteryPercent,
                BatteryTelemetryUnavailable = BatteryTelemetryUnavailable,
                ConnectionMethod = ConnectionMethod,
                ConnectionIsWireless = ConnectionIsWireless,
                TouchpadPressed = TouchpadPressed,
                MicrophoneMuted = MicrophoneMuted,
                GyroscopeX = GyroscopeX,
                GyroscopeY = GyroscopeY,
                GyroscopeZ = GyroscopeZ,
                AccelerometerX = AccelerometerX,
                AccelerometerY = AccelerometerY,
                AccelerometerZ = AccelerometerZ,
                Motion = Motion == null ? null : Motion.Copy(),
                LightbarState = LightbarState,
                BatteryChargingState = BatteryChargingState,
                TouchCoordinatesAvailable = TouchCoordinatesAvailable,
                HasTouchCoordinates = HasTouchCoordinates,
                TouchPoint1 = TouchPoint1 == null ? null : TouchPoint1.Copy(),
                TouchPoint2 = TouchPoint2 == null ? null : TouchPoint2.Copy(),
                TouchReportSequence = TouchReportSequence,
                TouchReportUtc = TouchReportUtc,
                TouchDebug = TouchDebug
            };
        }

        private static int ClampShort(double value)
        {
            return (int)Math.Max(-32768, Math.Min(32767, Math.Round(value)));
        }

        public static double Normalize(int value)
        {
            return Math.Max(-1.0, Math.Min(1.0, value < 0 ? value / 32768.0 : value / 32767.0));
        }

        public static InputSnapshot CreateDemo()
        {
            double t = (DateTime.UtcNow.Ticks % TimeSpan.TicksPerMinute) / (double)TimeSpan.TicksPerSecond;
            double lx = Math.Cos(t * 0.72 + 2.2) * 0.58;
            double ly = Math.Sin(t * 0.72 + 2.2) * 0.58;
            double rx = Math.Cos(t * 0.94 - 0.45) * 0.31;
            double ry = Math.Sin(t * 0.94 - 0.45) * 0.22;
            int lt = (int)((Math.Sin(t * 0.43 + 2.1) * 0.5 + 0.5) * 175);
            int rt = (int)((Math.Sin(t * 0.56) * 0.5 + 0.5) * 205);
            int phase = ((int)(t * 1.7)) % 18;
            ushort buttons = phase == 1 ? (ushort)0x1000 :
                phase == 3 ? (ushort)0x4000 :
                phase == 5 ? (ushort)0x0200 :
                phase == 7 ? (ushort)0x0001 :
                phase == 8 ? (ushort)0x0008 :
                phase == 9 ? (ushort)0x0002 :
                phase == 10 ? (ushort)0x0004 :
                phase == 11 ? (ushort)0x0009 :
                phase == 12 ? (ushort)0x0006 :
                phase == 14 ? (ushort)0x0040 :
                phase == 16 ? (ushort)0x2000 :
                phase == 17 ? (ushort)0x8000 : (ushort)0;
            return new InputSnapshot
            {
                DeviceId = "demo:xbox:0",
                TimestampUtc = DateTime.UtcNow,
                Connected = true,
                Family = ControllerFamily.Xbox,
                DeviceName = "Xbox 无线手柄",
                InputBackend = "动态演示",
                Index = 0,
                Packet = (uint)(t * 125),
                Buttons = buttons,
                LeftTrigger = lt,
                RightTrigger = rt,
                LeftX = (int)(lx * 32767),
                LeftY = (int)(ly * 32767),
                RightX = (int)(rx * 32767),
                RightY = (int)(ry * 32767),
                Battery = "满电",
                BatteryPercent = 100,
                ConnectionMethod = "动态演示",
                ConnectionIsWireless = true
            };
        }

        public static InputSnapshot CreateSonyDemo()
        {
            double t = (DateTime.UtcNow.Ticks % TimeSpan.TicksPerMinute) / (double)TimeSpan.TicksPerSecond;
            double lx = Math.Cos(t * 0.70 + 2.1) * 0.72;
            double ly = Math.Sin(t * 0.70 + 2.1) * 0.68;
            double rx = Math.Cos(t * 0.98 - 0.35) * 0.58;
            double ry = Math.Sin(t * 0.98 - 0.35) * 0.54;
            int phase = ((int)(t * 1.8)) % 24;
            ushort buttons = phase == 1 ? (ushort)0x1000 :
                phase == 2 ? (ushort)0x2000 :
                phase == 3 ? (ushort)0x4000 :
                phase == 4 ? (ushort)0x8000 :
                phase == 5 ? (ushort)0x0001 :
                phase == 6 ? (ushort)0x0008 :
                phase == 7 ? (ushort)0x0002 :
                phase == 8 ? (ushort)0x0004 :
                phase == 9 ? (ushort)0x0009 :
                phase == 10 ? (ushort)0x0006 :
                phase == 11 ? (ushort)0x0040 :
                phase == 12 ? (ushort)0x0080 :
                phase == 13 ? (ushort)0x0800 :
                phase == 14 ? (ushort)0x0400 :
                phase == 16 ? (ushort)0x0100 :
                phase == 17 ? (ushort)0x0200 :
                phase == 18 ? (ushort)0x0020 :
                phase == 19 ? (ushort)0x0010 : (ushort)0;
            return new InputSnapshot
            {
                DeviceId = "demo:dualsense:0",
                TimestampUtc = DateTime.UtcNow,
                Connected = true,
                Family = ControllerFamily.PlayStation,
                DeviceName = "DualSense 无线控制器",
                InputBackend = "动态演示",
                Index = 0,
                Packet = (uint)(t * 125),
                Buttons = buttons,
                LeftTrigger = (int)((Math.Sin(t * 0.49 + 1.8) * 0.5 + 0.5) * 255),
                RightTrigger = (int)((Math.Sin(t * 0.61) * 0.5 + 0.5) * 255),
                LeftX = (int)(lx * 32767),
                LeftY = (int)(ly * 32767),
                RightX = (int)(rx * 32767),
                RightY = (int)(ry * 32767),
                Battery = "满电",
                BatteryPercent = 100,
                ConnectionMethod = "动态演示",
                ConnectionIsWireless = true,
                TouchpadPressed = phase == 13,
                MicrophoneMuted = phase == 15
            };
        }
    }
}
