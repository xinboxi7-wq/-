// SettingsStore
//
// Extracted verbatim from ControllerLab.cs (lines 5753-5910) on 2026-09-22
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
    public static class SettingsStore
    {
        private static readonly string DirectoryPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerLab");
        private static readonly string FilePath = System.IO.Path.Combine(DirectoryPath, "settings.ini");
        private static readonly string LegacyFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxControllerLab", "settings.ini");

        public static string ApplicationDataDirectory { get { return DirectoryPath; } }
        public static string SettingsFilePath { get { return FilePath; } }

        public static ControllerSettings Load()
        {
            ControllerSettings settings = new ControllerSettings();
            try
            {
                string sourcePath = File.Exists(FilePath) ? FilePath : LegacyFilePath;
                if (!File.Exists(sourcePath)) { settings.Normalize(); return settings; }
                string[] lines = File.ReadAllLines(sourcePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    int split = lines[i].IndexOf('=');
                    if (split <= 0) continue;
                    string key = lines[i].Substring(0, split).Trim();
                    string raw = lines[i].Substring(split + 1).Trim();
                    if (key == "controllerIndex")
                    {
                        int controllerIndex;
                        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out controllerIndex)) settings.ControllerIndex = Math.Max(-1, Math.Min(3, controllerIndex));
                        continue;
                    }
                    if (key == "reducedMotion")
                    {
                        bool reduced;
                        if (bool.TryParse(raw, out reduced)) settings.ReducedMotion = reduced;
                        continue;
                    }
                    if (key == "connectionMethodOverride")
                    {
                        settings.ConnectionMethodOverride = raw;
                        continue;
                    }
                    if (key == "wiredUsbRoute")
                    {
                        settings.WiredUsbRoute = raw;
                        continue;
                    }
                    if (key == "receiverUsbRoute")
                    {
                        settings.ReceiverUsbRoute = raw;
                        continue;
                    }
                    if (key == "controllerFamily")
                    {
                        settings.ControllerFamily = raw;
                        continue;
                    }
                    if (key == "language") { settings.Language = raw; continue; }
                    if (key == "startupPage") { settings.StartupPage = raw; continue; }
                    bool boolean;
                    if (key == "autoConnect" && bool.TryParse(raw, out boolean)) { settings.AutoConnect = boolean; continue; }
                    if (key == "rememberWindowPosition" && bool.TryParse(raw, out boolean)) { settings.RememberWindowPosition = boolean; continue; }
                    if (key == "animationsEnabled" && bool.TryParse(raw, out boolean)) { settings.AnimationsEnabled = boolean; settings.ReducedMotion = !boolean; continue; }
                    if (key == "hasWindowPlacement" && bool.TryParse(raw, out boolean)) { settings.HasWindowPlacement = boolean; continue; }
                    if (key == "showAdvancedData" && bool.TryParse(raw, out boolean)) { settings.ShowAdvancedData = boolean; continue; }
                    if (key == "saveHistory" && bool.TryParse(raw, out boolean)) { settings.SaveHistory = boolean; continue; }
                    int integer;
                    if (key == "decimalPlaces" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) { settings.DecimalPlaces = integer; continue; }
                    if (key == "trailLength" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) { settings.TrailLength = integer; continue; }
                    if (key == "uiRefreshRate" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) { settings.UiRefreshRate = integer; continue; }
                    double value;
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
                    if (key == "offsetLX") settings.OffsetLX = ClampOffset(value);
                    else if (key == "offsetLY") settings.OffsetLY = ClampOffset(value);
                    else if (key == "offsetRX") settings.OffsetRX = ClampOffset(value);
                    else if (key == "offsetRY") settings.OffsetRY = ClampOffset(value);
                    else if (key == "leftDeadzone") settings.LeftDeadzone = ClampDeadzone(value);
                    else if (key == "rightDeadzone") settings.RightDeadzone = ClampDeadzone(value);
                    else if (key == "windowLeft") settings.WindowLeft = value;
                    else if (key == "windowTop") settings.WindowTop = value;
                    else if (key == "windowWidth") settings.WindowWidth = value;
                    else if (key == "windowHeight") settings.WindowHeight = value;
                    else if (key == "stationarySampleDuration") settings.StationarySampleDuration = value;
                    else if (key == "deadzoneSafetyMarginPercent") settings.DeadzoneSafetyMarginPercent = value;
                    else if (key == "defaultRumbleStrengthPercent") settings.DefaultRumbleStrengthPercent = value;
                    else if (key == "defaultRumbleDurationSeconds") settings.DefaultRumbleDurationSeconds = value;
                    else if (key == "rumbleSafetyMaximumPercent") settings.RumbleSafetyMaximumPercent = value;
                }
            }
            catch (Exception ex)
            {
                LabLogger.Warning("Settings", "Settings file could not be read; defaults were used. " + ex.GetType().Name);
                return new ControllerSettings();
            }
            settings.Normalize();
            return settings;
        }

        public static void Save(ControllerSettings settings)
        {
            try
            {
                if (settings == null) return;
                settings.Normalize();
                Directory.CreateDirectory(DirectoryPath);
                string[] lines =
                {
                    "version=5",
                    "offsetLX=" + ClampOffset(settings.OffsetLX).ToString("0.###", CultureInfo.InvariantCulture),
                    "offsetLY=" + ClampOffset(settings.OffsetLY).ToString("0.###", CultureInfo.InvariantCulture),
                    "offsetRX=" + ClampOffset(settings.OffsetRX).ToString("0.###", CultureInfo.InvariantCulture),
                    "offsetRY=" + ClampOffset(settings.OffsetRY).ToString("0.###", CultureInfo.InvariantCulture),
                    "leftDeadzone=" + ClampDeadzone(settings.LeftDeadzone).ToString("0.###", CultureInfo.InvariantCulture),
                    "rightDeadzone=" + ClampDeadzone(settings.RightDeadzone).ToString("0.###", CultureInfo.InvariantCulture),
                    "controllerIndex=" + Math.Max(-1, Math.Min(3, settings.ControllerIndex)).ToString(CultureInfo.InvariantCulture),
                    "reducedMotion=" + settings.ReducedMotion.ToString(CultureInfo.InvariantCulture),
                    "connectionMethodOverride=" + (settings.ConnectionMethodOverride ?? "自动"),
                    "wiredUsbRoute=" + (settings.WiredUsbRoute ?? string.Empty),
                    "receiverUsbRoute=" + (settings.ReceiverUsbRoute ?? string.Empty),
                    "controllerFamily=" + (settings.ControllerFamily ?? "Auto"),
                    "language=" + (settings.Language ?? "zh-CN"),
                    "startupPage=" + (settings.StartupPage ?? "Monitor"),
                    "autoConnect=" + settings.AutoConnect.ToString(CultureInfo.InvariantCulture),
                    "rememberWindowPosition=" + settings.RememberWindowPosition.ToString(CultureInfo.InvariantCulture),
                    "animationsEnabled=" + settings.AnimationsEnabled.ToString(CultureInfo.InvariantCulture),
                    "hasWindowPlacement=" + settings.HasWindowPlacement.ToString(CultureInfo.InvariantCulture),
                    "windowLeft=" + settings.WindowLeft.ToString("0.###", CultureInfo.InvariantCulture),
                    "windowTop=" + settings.WindowTop.ToString("0.###", CultureInfo.InvariantCulture),
                    "windowWidth=" + settings.WindowWidth.ToString("0.###", CultureInfo.InvariantCulture),
                    "windowHeight=" + settings.WindowHeight.ToString("0.###", CultureInfo.InvariantCulture),
                    "decimalPlaces=" + settings.DecimalPlaces.ToString(CultureInfo.InvariantCulture),
                    "trailLength=" + settings.TrailLength.ToString(CultureInfo.InvariantCulture),
                    "uiRefreshRate=" + settings.UiRefreshRate.ToString(CultureInfo.InvariantCulture),
                    "showAdvancedData=" + settings.ShowAdvancedData.ToString(CultureInfo.InvariantCulture),
                    "stationarySampleDuration=" + settings.StationarySampleDuration.ToString("0.0", CultureInfo.InvariantCulture),
                    "deadzoneSafetyMarginPercent=" + settings.DeadzoneSafetyMarginPercent.ToString("0.0", CultureInfo.InvariantCulture),
                    "saveHistory=" + settings.SaveHistory.ToString(CultureInfo.InvariantCulture),
                    "defaultRumbleStrengthPercent=" + settings.DefaultRumbleStrengthPercent.ToString("0.0", CultureInfo.InvariantCulture),
                    "defaultRumbleDurationSeconds=" + settings.DefaultRumbleDurationSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                    "rumbleSafetyMaximumPercent=" + settings.RumbleSafetyMaximumPercent.ToString("0.0", CultureInfo.InvariantCulture)
                };
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception ex)
            {
                LabLogger.Error("Settings", "Settings file could not be saved.", ex);
                // Settings failure must not prevent live monitoring.
            }
        }

        private static double ClampOffset(double value)
        {
            return Math.Max(-32768, Math.Min(32767, value));
        }

        private static double ClampDeadzone(double value)
        {
            return Math.Max(0, Math.Min(0.25, value));
        }
    }
}
