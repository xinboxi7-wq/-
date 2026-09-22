// ControllerSettings
//
// Extracted verbatim from ControllerLab.cs (lines 5833-5906) on 2026-09-22
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
    public sealed class ControllerSettings
    {
        public double OffsetLX;
        public double OffsetLY;
        public double OffsetRX;
        public double OffsetRY;
        public double LeftDeadzone = 0.08;
        public double RightDeadzone = 0.08;
        public int ControllerIndex = -1;
        public bool ReducedMotion;
        public string ConnectionMethodOverride = "自动";
        public string WiredUsbRoute;
        public string ReceiverUsbRoute;
        public string ControllerFamily = "Auto";
        public string Language = "zh-CN";
        public string StartupPage = "Monitor";
        public bool AutoConnect = true;
        public bool RememberWindowPosition = true;
        public bool AnimationsEnabled = true;
        public bool HasWindowPlacement;
        public double WindowLeft;
        public double WindowTop;
        public double WindowWidth = 1440;
        public double WindowHeight = 1024;
        public int DecimalPlaces = 3;
        public int TrailLength = 900;
        public int UiRefreshRate = 60;
        public bool ShowAdvancedData;
        public double StationarySampleDuration = 5.0;
        public double DeadzoneSafetyMarginPercent = 0.5;
        public bool SaveHistory = true;
        public double DefaultRumbleStrengthPercent = 40.0;
        public double DefaultRumbleDurationSeconds = 5.0;
        public double RumbleSafetyMaximumPercent = 40.0;

        public void Normalize()
        {
            if (Language != "zh-CN") Language = "zh-CN";
            if (StartupPage != "Health" && StartupPage != "Devices") StartupPage = "Monitor";
            DecimalPlaces = Math.Max(1, Math.Min(3, DecimalPlaces));
            TrailLength = Math.Max(100, Math.Min(1600, TrailLength));
            UiRefreshRate = Math.Max(20, Math.Min(60, UiRefreshRate));
            StationarySampleDuration = Math.Max(3.0, Math.Min(10.0, StationarySampleDuration));
            DeadzoneSafetyMarginPercent = Math.Max(0.5, Math.Min(5.0, DeadzoneSafetyMarginPercent));
            DefaultRumbleDurationSeconds = Math.Max(0.5, Math.Min(5.0, DefaultRumbleDurationSeconds));
            RumbleSafetyMaximumPercent = Math.Max(30.0, Math.Min(70.0, RumbleSafetyMaximumPercent));
            DefaultRumbleStrengthPercent = Math.Max(0, Math.Min(Math.Min(40.0, RumbleSafetyMaximumPercent), DefaultRumbleStrengthPercent));
            WindowWidth = Math.Max(1020, Math.Min(3840, WindowWidth));
            WindowHeight = Math.Max(680, Math.Min(2160, WindowHeight));
            AnimationsEnabled = !ReducedMotion;
        }

        public void CopyProductSettingsFrom(ControllerSettings source)
        {
            if (source == null) return;
            Language = source.Language;
            StartupPage = source.StartupPage;
            AutoConnect = source.AutoConnect;
            RememberWindowPosition = source.RememberWindowPosition;
            AnimationsEnabled = source.AnimationsEnabled;
            ReducedMotion = source.ReducedMotion;
            DecimalPlaces = source.DecimalPlaces;
            TrailLength = source.TrailLength;
            UiRefreshRate = source.UiRefreshRate;
            ShowAdvancedData = source.ShowAdvancedData;
            StationarySampleDuration = source.StationarySampleDuration;
            DeadzoneSafetyMarginPercent = source.DeadzoneSafetyMarginPercent;
            SaveHistory = source.SaveHistory;
            DefaultRumbleStrengthPercent = source.DefaultRumbleStrengthPercent;
            DefaultRumbleDurationSeconds = source.DefaultRumbleDurationSeconds;
            RumbleSafetyMaximumPercent = source.RumbleSafetyMaximumPercent;
            Normalize();
        }
    }
}
