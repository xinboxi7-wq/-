using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControllerLab
{
    public enum RumbleVerificationStatus
    {
        Unsupported,
        ImplementedUnverified,
        Verified
    }

    public enum RumbleInterpolation
    {
        Step,
        Linear
    }

    [DataContract]
    public sealed class RumbleCapabilities
    {
        [DataMember(Order = 1)] public bool IsSupported;
        [DataMember(Order = 2)] public bool SupportsLeftMotor;
        [DataMember(Order = 3)] public bool SupportsRightMotor;
        [DataMember(Order = 4)] public bool SupportsIndependentChannels;
        [DataMember(Order = 5)] public bool SupportsAdvancedHaptics;
        [DataMember(Order = 6)] public bool SupportsTriggerEffects;
        [DataMember(Order = 7)] public bool SupportsUsb;
        [DataMember(Order = 8)] public bool SupportsBluetooth;
        [DataMember(Order = 9)] public double MaximumSafeDuration = 30;
        [DataMember(Order = 10)] public string ConnectionMode = "Unknown";
        [DataMember(Order = 11)] public RumbleVerificationStatus VerifiedStatus = RumbleVerificationStatus.Unsupported;
        [DataMember(Order = 12)] public string Details = string.Empty;

        public RumbleCapabilities Copy()
        {
            return (RumbleCapabilities)MemberwiseClone();
        }

        public string VerificationLabel
        {
            get
            {
                if (VerifiedStatus == RumbleVerificationStatus.Verified) return "已实机验证";
                if (VerifiedStatus == RumbleVerificationStatus.ImplementedUnverified) return "已实现，待实机验证";
                return "不支持";
            }
        }
    }

    [DataContract]
    public sealed class RumblePatternStep
    {
        [DataMember(Order = 1)] public double StartTime;
        [DataMember(Order = 2)] public double Duration = 0.1;
        [DataMember(Order = 3)] public double LeftStrength;
        [DataMember(Order = 4)] public double RightStrength;
        [DataMember(Order = 5)] public RumbleInterpolation Interpolation = RumbleInterpolation.Linear;
        [DataMember(Order = 6)] public string Label = string.Empty;

        public RumblePatternStep Copy()
        {
            return (RumblePatternStep)MemberwiseClone();
        }
    }

    [DataContract]
    public sealed class RumblePatternDefinition
    {
        [DataMember(Order = 1)] public string Id = string.Empty;
        [DataMember(Order = 2)] public string Name = string.Empty;
        [DataMember(Order = 3)] public bool IsBuiltIn;
        [DataMember(Order = 4)] public List<RumblePatternStep> Steps = new List<RumblePatternStep>();

        public double TotalDuration
        {
            get
            {
                double total = 0;
                for (int i = 0; i < Steps.Count; i++) total = Math.Max(total, Math.Max(0, Steps[i].StartTime) + Math.Max(0.01, Steps[i].Duration));
                return total;
            }
        }

        public double MaximumStrength
        {
            get
            {
                double value = 0;
                for (int i = 0; i < Steps.Count; i++) value = Math.Max(value, Math.Max(Steps[i].LeftStrength, Steps[i].RightStrength));
                return value;
            }
        }

        public RumblePatternDefinition Copy()
        {
            RumblePatternDefinition copy = new RumblePatternDefinition { Id = Id, Name = Name, IsBuiltIn = IsBuiltIn };
            for (int i = 0; i < Steps.Count; i++) copy.Steps.Add(Steps[i].Copy());
            return copy;
        }
    }

    public static class RumblePatternCatalog
    {
        public static List<RumblePatternDefinition> CreateBuiltIns()
        {
            List<RumblePatternDefinition> patterns = new List<RumblePatternDefinition>();
            patterns.Add(Pattern("left-only", "左侧单独", Step(0, 0.15, 0.40, 0, RumbleInterpolation.Linear, "左侧启动"), Step(0.15, 0.30, 0.40, 0, RumbleInterpolation.Step, "左侧保持"), Step(0.45, 0.15, 0, 0, RumbleInterpolation.Linear, "停止")));
            patterns.Add(Pattern("right-only", "右侧单独", Step(0, 0.15, 0, 0.40, RumbleInterpolation.Linear, "右侧启动"), Step(0.15, 0.30, 0, 0.40, RumbleInterpolation.Step, "右侧保持"), Step(0.45, 0.15, 0, 0, RumbleInterpolation.Linear, "停止")));
            patterns.Add(Pattern("soft-notice", "柔和提示", Step(0, 0.12, 0.16, 0.12, RumbleInterpolation.Linear, "柔和启动"), Step(0.12, 0.22, 0, 0, RumbleInterpolation.Linear, "柔和收尾")));
            patterns.Add(Pattern("low-impact", "低频冲击", Step(0, 0.04, 0.40, 0.05, RumbleInterpolation.Step, "低频冲击"), Step(0.04, 0.24, 0, 0, RumbleInterpolation.Linear, "衰减")));
            patterns.Add(Pattern("high-frequency", "高频震动", Step(0, 0.06, 0.05, 0.36, RumbleInterpolation.Step, "高频启动"), Step(0.06, 0.32, 0.05, 0.36, RumbleInterpolation.Step, "高频保持"), Step(0.38, 0.10, 0, 0, RumbleInterpolation.Linear, "停止")));
            patterns.Add(Pattern("balanced", "双侧均衡", Step(0, 0.12, 0.32, 0.32, RumbleInterpolation.Linear, "双侧启动"), Step(0.12, 0.36, 0.32, 0.32, RumbleInterpolation.Step, "均衡保持"), Step(0.48, 0.12, 0, 0, RumbleInterpolation.Linear, "停止")));
            patterns.Add(Pattern("alternating", "左右交替", Step(0, 0.04, 0.35, 0, RumbleInterpolation.Step, "左"), Step(0.04, 0.04, 0, 0, RumbleInterpolation.Step, "间隔"), Step(0.18, 0.04, 0, 0.35, RumbleInterpolation.Step, "右"), Step(0.22, 0.04, 0, 0, RumbleInterpolation.Step, "间隔"), Step(0.36, 0.04, 0.35, 0, RumbleInterpolation.Step, "左"), Step(0.40, 0.04, 0, 0, RumbleInterpolation.Step, "间隔"), Step(0.54, 0.04, 0, 0.35, RumbleInterpolation.Step, "右"), Step(0.58, 0.08, 0, 0, RumbleInterpolation.Step, "停止")));
            patterns.Add(Pattern("heartbeat", "心跳", Step(0, 0.05, 0.30, 0.18, RumbleInterpolation.Step, "第一拍"), Step(0.05, 0.05, 0, 0, RumbleInterpolation.Step, "间隔"), Step(0.16, 0.08, 0.38, 0.24, RumbleInterpolation.Step, "第二拍"), Step(0.24, 0.42, 0, 0, RumbleInterpolation.Linear, "余韵")));
            patterns.Add(Pattern("continuous-pulse", "连续脉冲", Step(0, 0.05, 0.30, 0.30, RumbleInterpolation.Step, "脉冲 1"), Step(0.05, 0.05, 0, 0, RumbleInterpolation.Step, "间隔"), Step(0.30, 0.05, 0.30, 0.30, RumbleInterpolation.Step, "脉冲 2"), Step(0.35, 0.05, 0, 0, RumbleInterpolation.Step, "间隔"), Step(0.60, 0.05, 0.30, 0.30, RumbleInterpolation.Step, "脉冲 3"), Step(0.65, 0.05, 0, 0, RumbleInterpolation.Step, "停止")));
            patterns.Add(Pattern("ramp-up", "渐强", Step(0, 0.90, 0.40, 0.40, RumbleInterpolation.Linear, "渐强"), Step(0.90, 0.10, 0, 0, RumbleInterpolation.Linear, "停止")));
            patterns.Add(Pattern("ramp-down", "渐弱", Step(0, 0.02, 0.40, 0.40, RumbleInterpolation.Step, "起始"), Step(0.02, 0.98, 0, 0, RumbleInterpolation.Linear, "渐弱")));
            patterns.Add(Pattern("ramp-up-down", "渐强后渐弱", Step(0, 0.50, 0.40, 0.40, RumbleInterpolation.Linear, "渐强"), Step(0.50, 0.50, 0, 0, RumbleInterpolation.Linear, "渐弱")));
            patterns.Add(Pattern("short-click", "短促点击", Step(0, 0.045, 0.18, 0.34, RumbleInterpolation.Step, "点击"), Step(0.045, 0.08, 0, 0, RumbleInterpolation.Step, "停止")));
            patterns.Add(Pattern("impact-aftershock", "强冲击后余震", Step(0, 0.06, 0.65, 0.45, RumbleInterpolation.Step, "冲击"), Step(0.06, 0.18, 0.24, 0.16, RumbleInterpolation.Linear, "余震"), Step(0.24, 0.25, 0, 0, RumbleInterpolation.Linear, "衰减")));
            return patterns;
        }

        public static RumblePatternDefinition FromLegacy(ControllerRumblePattern pattern, double left, double right, double duration)
        {
            double safeDuration = Math.Max(0.1, Math.Min(30, duration));
            RumblePatternDefinition definition;
            if (pattern == ControllerRumblePattern.LeftOnly) definition = Find("left-only");
            else if (pattern == ControllerRumblePattern.RightOnly) definition = Find("right-only");
            else if (pattern == ControllerRumblePattern.Balanced) definition = Find("balanced");
            else if (pattern == ControllerRumblePattern.Alternating) definition = Find("alternating");
            else if (pattern == ControllerRumblePattern.Ramp) definition = Find("ramp-up");
            else if (pattern == ControllerRumblePattern.Pulse) definition = Find("continuous-pulse");
            else definition = Pattern("manual", "自定义", Step(0, safeDuration, left, right, RumbleInterpolation.Step, "持续输出"));
            if (definition == null) definition = Pattern("manual", "自定义", Step(0, safeDuration, left, right, RumbleInterpolation.Step, "持续输出"));
            double maxLeft = 0;
            double maxRight = 0;
            for (int i = 0; i < definition.Steps.Count; i++)
            {
                maxLeft = Math.Max(maxLeft, definition.Steps[i].LeftStrength);
                maxRight = Math.Max(maxRight, definition.Steps[i].RightStrength);
            }
            double leftScale = maxLeft <= 0 ? 0 : left / maxLeft;
            double rightScale = maxRight <= 0 ? 0 : right / maxRight;
            for (int i = 0; i < definition.Steps.Count; i++)
            {
                definition.Steps[i].LeftStrength = Clamp01(definition.Steps[i].LeftStrength * leftScale);
                definition.Steps[i].RightStrength = Clamp01(definition.Steps[i].RightStrength * rightScale);
            }
            return ScaleDuration(definition, safeDuration);
        }

        public static RumblePatternDefinition Find(string id)
        {
            List<RumblePatternDefinition> all = CreateBuiltIns();
            for (int i = 0; i < all.Count; i++) if (string.Equals(all[i].Id, id, StringComparison.OrdinalIgnoreCase)) return all[i];
            return null;
        }

        public static RumblePatternDefinition ScaleDuration(RumblePatternDefinition source, double requestedDuration)
        {
            RumblePatternDefinition copy = source.Copy();
            double total = Math.Max(0.01, copy.TotalDuration);
            double scale = Math.Max(0.1, Math.Min(30, requestedDuration)) / total;
            for (int i = 0; i < copy.Steps.Count; i++)
            {
                copy.Steps[i].StartTime *= scale;
                copy.Steps[i].Duration = Math.Max(0.01, copy.Steps[i].Duration * scale);
            }
            return copy;
        }

        private static RumblePatternDefinition Pattern(string id, string name, params RumblePatternStep[] steps)
        {
            RumblePatternDefinition pattern = new RumblePatternDefinition { Id = id, Name = name, IsBuiltIn = true };
            pattern.Steps.AddRange(steps);
            return pattern;
        }

        private static RumblePatternStep Step(double start, double duration, double left, double right, RumbleInterpolation interpolation, string label)
        {
            return new RumblePatternStep { StartTime = start, Duration = duration, LeftStrength = left, RightStrength = right, Interpolation = interpolation, Label = label };
        }

        private static double Clamp01(double value) { return Math.Max(0, Math.Min(1, value)); }
    }

    public static class RumblePatternMath
    {
        public static void Evaluate(RumblePatternDefinition pattern, double time, out double left, out double right, out string label)
        {
            left = 0;
            right = 0;
            label = "等待";
            if (pattern == null || pattern.Steps == null || pattern.Steps.Count == 0 || time < 0) return;
            List<RumblePatternStep> steps = SortedSteps(pattern);
            int selected = -1;
            for (int i = 0; i < steps.Count; i++)
            {
                if (Math.Max(0, steps[i].StartTime) <= time) selected = i;
                else break;
            }
            if (selected < 0) return;
            RumblePatternStep current = steps[selected];
            double previousLeft = selected <= 0 ? 0 : Clamp01(steps[selected - 1].LeftStrength);
            double previousRight = selected <= 0 ? 0 : Clamp01(steps[selected - 1].RightStrength);
            double start = Math.Max(0, current.StartTime);
            double progress = Math.Max(0, Math.Min(1, (time - start) / Math.Max(0.01, current.Duration)));
            if (current.Interpolation == RumbleInterpolation.Linear)
            {
                left = previousLeft + (Clamp01(current.LeftStrength) - previousLeft) * progress;
                right = previousRight + (Clamp01(current.RightStrength) - previousRight) * progress;
            }
            else
            {
                left = Clamp01(current.LeftStrength);
                right = Clamp01(current.RightStrength);
            }
            label = string.IsNullOrEmpty(current.Label) ? "时间线步骤 " + (selected + 1) : current.Label;
        }

        public static List<RumblePatternStep> SortedSteps(RumblePatternDefinition pattern)
        {
            List<RumblePatternStep> steps = new List<RumblePatternStep>();
            if (pattern != null && pattern.Steps != null) for (int i = 0; i < pattern.Steps.Count; i++) steps.Add(pattern.Steps[i]);
            steps.Sort(delegate(RumblePatternStep a, RumblePatternStep b) { return a.StartTime.CompareTo(b.StartTime); });
            return steps;
        }

        private static double Clamp01(double value) { return Math.Max(0, Math.Min(1, value)); }
    }

    [DataContract]
    public sealed class RumbleDeviceProfile
    {
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public string DeviceKey = string.Empty;
        [DataMember(Order = 3)] public double DefaultLeftStrength = 0.40;
        [DataMember(Order = 4)] public double DefaultRightStrength = 0.40;
        [DataMember(Order = 5)] public double DefaultDurationSeconds = 5.0;
        [DataMember(Order = 6)] public string LastPatternId = "balanced";
        [DataMember(Order = 7)] public bool SafetyLimitsEnabled = true;
        [DataMember(Order = 8)] public bool ApplyDeviceCalibration;
        [DataMember(Order = 9)] public double LeftMinimumPerceptible;
        [DataMember(Order = 10)] public double RightMinimumPerceptible;
        [DataMember(Order = 11)] public double LeftComfortMaximum = 0.65;
        [DataMember(Order = 12)] public double RightComfortMaximum = 0.65;
        [DataMember(Order = 13)] public string OutputConnectionMode = "Unknown";
        [DataMember(Order = 14)] public DateTime UpdatedUtc;

        public RumbleDeviceProfile Copy() { return (RumbleDeviceProfile)MemberwiseClone(); }
    }

    [DataContract]
    internal sealed class RumbleCustomPatternDocument
    {
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public List<RumblePatternDefinition> Patterns = new List<RumblePatternDefinition>();
    }

    public sealed class RumbleSettingsStore
    {
        private readonly string root;
        private readonly string deviceDirectory;
        private readonly string customPatternsPath;
        private double globalDefaultStrength = 0.40;
        private double globalDefaultDurationSeconds = 5.0;
        private double globalSafetyMaximum = 0.40;

        public static string DefaultDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerLab", "rumble"); } }

        public RumbleSettingsStore() : this(DefaultDirectory) { }

        public RumbleSettingsStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Rumble settings directory is required.", "directory");
            root = Path.GetFullPath(directory);
            deviceDirectory = Path.Combine(root, "devices");
            customPatternsPath = Path.Combine(root, "custom-patterns.json");
        }

        public string RootDirectory { get { return root; } }

        public void SetGlobalDefaults(double strength, double durationSeconds, double safetyMaximum)
        {
            globalSafetyMaximum = Clamp(safetyMaximum, 0.30, 0.70);
            globalDefaultStrength = Clamp(strength, 0, Math.Min(0.40, globalSafetyMaximum));
            globalDefaultDurationSeconds = Clamp(durationSeconds, 0.1, 5.0);
        }

        public string GetStableDeviceKey(ControllerState state)
        {
            string identity;
            if (state != null && !string.IsNullOrWhiteSpace(state.DeviceId)) identity = "device-id|" + state.DeviceId.Trim();
            else identity = string.Format(CultureInfo.InvariantCulture, "fallback|{0}|{1}|{2}|{3}", state == null ? ControllerType.Unknown : state.ControllerType, state == null ? string.Empty : state.DeviceName, state == null ? string.Empty : state.InputBackend, state == null ? string.Empty : state.ConnectionTypeLabel);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
                StringBuilder value = new StringBuilder("device-");
                for (int i = 0; i < 16; i++) value.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return value.ToString();
            }
        }

        public RumbleDeviceProfile LoadDeviceProfile(ControllerState state)
        {
            string key = GetStableDeviceKey(state);
            string path = DevicePath(key);
            RumbleDeviceProfile fallback = CreateDefaultProfile(state, key);
            if (!File.Exists(path)) return fallback;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    RumbleDeviceProfile profile = Serializer(typeof(RumbleDeviceProfile)).ReadObject(stream) as RumbleDeviceProfile;
                    if (profile == null || profile.SchemaVersion != 1 || !string.Equals(profile.DeviceKey, key, StringComparison.Ordinal)) return fallback;
                    NormalizeProfile(profile);
                    return profile;
                }
            }
            catch (IOException) { return fallback; }
            catch (UnauthorizedAccessException) { return fallback; }
            catch (SerializationException) { return fallback; }
            catch (ArgumentException) { return fallback; }
        }

        public string SaveDeviceProfile(RumbleDeviceProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.DeviceKey)) throw new ArgumentException("A stable device key is required.", "profile");
            NormalizeProfile(profile);
            profile.UpdatedUtc = DateTime.UtcNow;
            Directory.CreateDirectory(deviceDirectory);
            string path = DevicePath(profile.DeviceKey);
            WriteObject(profile, typeof(RumbleDeviceProfile), path);
            return path;
        }

        public List<RumblePatternDefinition> LoadCustomPatterns()
        {
            if (!File.Exists(customPatternsPath)) return new List<RumblePatternDefinition>();
            try
            {
                using (FileStream stream = new FileStream(customPatternsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    RumbleCustomPatternDocument document = Serializer(typeof(RumbleCustomPatternDocument)).ReadObject(stream) as RumbleCustomPatternDocument;
                    if (document == null || document.SchemaVersion != 1 || document.Patterns == null) return new List<RumblePatternDefinition>();
                    List<RumblePatternDefinition> result = new List<RumblePatternDefinition>();
                    for (int i = 0; i < document.Patterns.Count; i++) if (IsValidCustomPattern(document.Patterns[i])) result.Add(SanitizeCustomPattern(document.Patterns[i]));
                    return result;
                }
            }
            catch (IOException) { return new List<RumblePatternDefinition>(); }
            catch (UnauthorizedAccessException) { return new List<RumblePatternDefinition>(); }
            catch (SerializationException) { return new List<RumblePatternDefinition>(); }
            catch (ArgumentException) { return new List<RumblePatternDefinition>(); }
        }

        public string SaveCustomPattern(RumblePatternDefinition pattern)
        {
            if (!IsValidCustomPattern(pattern)) throw new ArgumentException("Custom pattern requires a name and at least one valid step.", "pattern");
            RumblePatternDefinition safe = SanitizeCustomPattern(pattern);
            safe.IsBuiltIn = false;
            if (string.IsNullOrEmpty(safe.Id) || safe.Id.StartsWith("built-in-", StringComparison.OrdinalIgnoreCase)) safe.Id = "custom-" + Guid.NewGuid().ToString("N");
            List<RumblePatternDefinition> patterns = LoadCustomPatterns();
            int existing = -1;
            for (int i = 0; i < patterns.Count; i++) if (string.Equals(patterns[i].Id, safe.Id, StringComparison.OrdinalIgnoreCase)) { existing = i; break; }
            if (existing >= 0) patterns[existing] = safe; else patterns.Add(safe);
            WriteCustomPatterns(patterns);
            pattern.Id = safe.Id;
            pattern.IsBuiltIn = false;
            return customPatternsPath;
        }

        public bool DeleteCustomPattern(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            List<RumblePatternDefinition> patterns = LoadCustomPatterns();
            int removed = patterns.RemoveAll(delegate(RumblePatternDefinition pattern) { return string.Equals(pattern.Id, id, StringComparison.OrdinalIgnoreCase); });
            if (removed <= 0) return false;
            WriteCustomPatterns(patterns);
            return true;
        }

        private void WriteCustomPatterns(List<RumblePatternDefinition> patterns)
        {
            Directory.CreateDirectory(root);
            RumbleCustomPatternDocument document = new RumbleCustomPatternDocument { Patterns = patterns };
            WriteObject(document, typeof(RumbleCustomPatternDocument), customPatternsPath);
        }

        private string DevicePath(string key)
        {
            string safe = key ?? string.Empty;
            for (int i = 0; i < safe.Length; i++) if (!char.IsLetterOrDigit(safe[i]) && safe[i] != '-' && safe[i] != '_') throw new ArgumentException("Invalid device key.", "key");
            string path = Path.GetFullPath(Path.Combine(deviceDirectory, safe + ".json"));
            string prefix = Path.GetFullPath(deviceDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Device profile path escaped settings directory.");
            return path;
        }

        private RumbleDeviceProfile CreateDefaultProfile(ControllerState state, string key)
        {
            return new RumbleDeviceProfile
            {
                DeviceKey = key,
                DefaultLeftStrength = globalDefaultStrength,
                DefaultRightStrength = globalDefaultStrength,
                DefaultDurationSeconds = globalDefaultDurationSeconds,
                LeftComfortMaximum = globalSafetyMaximum,
                RightComfortMaximum = globalSafetyMaximum,
                OutputConnectionMode = state == null ? "Unknown" : state.ConnectionTypeLabel,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private static void NormalizeProfile(RumbleDeviceProfile profile)
        {
            profile.DefaultLeftStrength = Clamp(profile.DefaultLeftStrength, 0, 0.40);
            profile.DefaultRightStrength = Clamp(profile.DefaultRightStrength, 0, 0.40);
            profile.DefaultDurationSeconds = Clamp(profile.DefaultDurationSeconds, 0.1, 5.0);
            profile.LeftMinimumPerceptible = Clamp(profile.LeftMinimumPerceptible, 0, 0.40);
            profile.RightMinimumPerceptible = Clamp(profile.RightMinimumPerceptible, 0, 0.40);
            profile.LeftComfortMaximum = Clamp(profile.LeftComfortMaximum, Math.Max(0.30, profile.LeftMinimumPerceptible), 0.70);
            profile.RightComfortMaximum = Clamp(profile.RightComfortMaximum, Math.Max(0.30, profile.RightMinimumPerceptible), 0.70);
            if (string.IsNullOrEmpty(profile.LastPatternId)) profile.LastPatternId = "balanced";
            if (string.IsNullOrEmpty(profile.OutputConnectionMode)) profile.OutputConnectionMode = "Unknown";
        }

        private static bool IsValidCustomPattern(RumblePatternDefinition pattern)
        {
            return pattern != null && !string.IsNullOrWhiteSpace(pattern.Name) && pattern.Steps != null && pattern.Steps.Count > 0;
        }

        private static RumblePatternDefinition SanitizeCustomPattern(RumblePatternDefinition source)
        {
            RumblePatternDefinition pattern = new RumblePatternDefinition { Id = source.Id ?? string.Empty, Name = (source.Name ?? "自定义预设").Trim(), IsBuiltIn = false };
            List<RumblePatternStep> sorted = RumblePatternMath.SortedSteps(source);
            for (int i = 0; i < sorted.Count && i < 64; i++)
            {
                RumblePatternStep item = sorted[i];
                if (item == null) continue;
                pattern.Steps.Add(new RumblePatternStep
                {
                    StartTime = Clamp(item.StartTime, 0, 29.9),
                    Duration = Clamp(item.Duration, 0.01, 30),
                    LeftStrength = Clamp(item.LeftStrength, 0, 1),
                    RightStrength = Clamp(item.RightStrength, 0, 1),
                    Interpolation = item.Interpolation,
                    Label = string.IsNullOrWhiteSpace(item.Label) ? "节点 " + (i + 1) : item.Label.Trim()
                });
            }
            return TrimDuration(pattern, 30);
        }

        internal static RumblePatternDefinition TrimDuration(RumblePatternDefinition source, double maximumDuration)
        {
            RumblePatternDefinition result = new RumblePatternDefinition { Id = source.Id, Name = source.Name, IsBuiltIn = source.IsBuiltIn };
            double maximum = Math.Max(0.1, maximumDuration);
            List<RumblePatternStep> sorted = RumblePatternMath.SortedSteps(source);
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].StartTime >= maximum) break;
                RumblePatternStep step = sorted[i].Copy();
                step.StartTime = Math.Max(0, step.StartTime);
                step.Duration = Math.Max(0.01, Math.Min(step.Duration, maximum - step.StartTime));
                step.LeftStrength = Clamp(step.LeftStrength, 0, 1);
                step.RightStrength = Clamp(step.RightStrength, 0, 1);
                result.Steps.Add(step);
            }
            return result;
        }

        private static void WriteObject(object value, Type type, string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) Serializer(type).WriteObject(stream, value);
        }

        private static DataContractJsonSerializer Serializer(Type type)
        {
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        }

        private static double Clamp(double value, double minimum, double maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }
    }

    public static class RumbleSafetyPolicy
    {
        public static RumblePatternDefinition Apply(RumblePatternDefinition source, double overallStrength, RumbleCapabilities capabilities, RumbleDeviceProfile profile)
        {
            if (source == null) return null;
            double overall = Math.Max(0, Math.Min(1, overallStrength));
            RumblePatternDefinition scaled = source.Copy();
            double maximum = 0;
            for (int i = 0; i < scaled.Steps.Count; i++)
            {
                scaled.Steps[i].LeftStrength = Math.Max(0, Math.Min(1, scaled.Steps[i].LeftStrength * overall));
                scaled.Steps[i].RightStrength = Math.Max(0, Math.Min(1, scaled.Steps[i].RightStrength * overall));
                maximum = Math.Max(maximum, Math.Max(scaled.Steps[i].LeftStrength, scaled.Steps[i].RightStrength));
            }
            double allowed = capabilities == null || capabilities.MaximumSafeDuration <= 0 ? 30 : Math.Min(30, capabilities.MaximumSafeDuration);
            bool safety = profile == null || profile.SafetyLimitsEnabled;
            if (safety)
            {
                if (maximum > 0.80) allowed = Math.Min(allowed, 3.0);
                else if (maximum > 0.60) allowed = Math.Min(allowed, 8.0);
                else if (maximum > 0.40) allowed = Math.Min(allowed, 15.0);
            }
            return RumbleSettingsStore.TrimDuration(scaled, allowed);
        }

        public static double ApplyCalibration(double strength, bool left, RumbleDeviceProfile profile)
        {
            double value = Math.Max(0, Math.Min(1, strength));
            if (value <= 0 || profile == null || !profile.ApplyDeviceCalibration) return value;
            double minimum = left ? profile.LeftMinimumPerceptible : profile.RightMinimumPerceptible;
            double maximum = left ? profile.LeftComfortMaximum : profile.RightComfortMaximum;
            minimum = Math.Max(0, Math.Min(0.40, minimum));
            maximum = Math.Max(Math.Max(0.30, minimum), Math.Min(0.70, maximum));
            return minimum + value * (maximum - minimum);
        }
    }

    public sealed class RumblePatternPlayerSnapshot
    {
        public bool IsRunning;
        public bool IsPaused;
        public double LeftStrength;
        public double RightStrength;
        public double ElapsedSeconds;
        public double TotalSeconds;
        public double RemainingSeconds;
        public double Progress;
        public string PatternId = string.Empty;
        public string PatternName = "未运行";
        public string CurrentStepLabel = "等待";
        public string Status = "等待开始";
        public bool LastOutputSucceeded;
        public DateTime LastStoppedUtc = DateTime.MinValue;
    }

    public sealed class RumblePatternPlayer : IDisposable
    {
        public const int OutputRefreshRateHz = 25;
        private const int FrameMilliseconds = 1000 / OutputRefreshRateHz;
        private readonly object sync = new object();
        private CancellationTokenSource cancellation;
        private Task activeTask;
        private IControllerRumbleService activeService;
        private int generation;
        private bool running;
        private bool paused;
        private double left;
        private double right;
        private double elapsed;
        private double total;
        private string patternId = string.Empty;
        private string patternName = "未运行";
        private string stepLabel = "等待";
        private string status = "等待开始";
        private bool lastOutputSucceeded;
        private DateTime lastStoppedUtc = DateTime.MinValue;

        public bool IsRunning { get { lock (sync) return running; } }

        public bool Start(IControllerRumbleService service, RumblePatternDefinition pattern, double overallStrength, RumbleDeviceProfile profile, out string error)
        {
            error = string.Empty;
            if (service == null || !service.IsSupported || service.Capabilities == null || !service.Capabilities.IsSupported)
            {
                error = service == null ? "当前设备不支持震动" : service.SupportDetails;
                return false;
            }
            RumblePatternDefinition safe = RumbleSafetyPolicy.Apply(pattern, overallStrength, service.Capabilities, profile);
            if (safe == null || safe.Steps.Count == 0 || safe.TotalDuration <= 0)
            {
                error = "时间线没有可播放的节点";
                return false;
            }
            if (safe.MaximumStrength <= 0)
            {
                service.StopRumble();
                lock (sync) status = "0% 输出保持停止";
                error = "时间线强度为 0%，未启动输出";
                return false;
            }
            Stop("已切换震动时间线");
            CancellationTokenSource ownCancellation = new CancellationTokenSource();
            int ownGeneration;
            lock (sync)
            {
                generation++;
                ownGeneration = generation;
                cancellation = ownCancellation;
                activeService = service;
                running = true;
                paused = false;
                left = right = elapsed = 0;
                total = safe.TotalDuration;
                patternId = safe.Id ?? string.Empty;
                patternName = string.IsNullOrEmpty(safe.Name) ? "自定义时间线" : safe.Name;
                stepLabel = "准备输出";
                status = "正在启动震动";
                lastOutputSucceeded = false;
                activeTask = Task.Run(() => PlayAsync(service, safe, profile == null ? null : profile.Copy(), ownGeneration, ownCancellation.Token));
            }
            return true;
        }

        public bool Pause(out string reason)
        {
            IControllerRumbleService service;
            lock (sync)
            {
                if (!running || paused) { reason = "当前没有可暂停的播放任务。"; return false; }
                paused = true;
                left = right = 0;
                status = "播放已暂停，输出已归零";
                service = activeService;
            }
            try { if (service != null) service.StopRumble(); }
            catch { }
            reason = string.Empty;
            return true;
        }

        public bool Resume(out string reason)
        {
            lock (sync)
            {
                if (!running || !paused) { reason = "当前没有已暂停的播放任务。"; return false; }
                paused = false;
                status = "震动输出中";
            }
            reason = string.Empty;
            return true;
        }

        public void Stop(string reason)
        {
            CancellationTokenSource oldCancellation;
            Task oldTask;
            IControllerRumbleService service;
            bool hadOutput;
            lock (sync)
            {
                generation++;
                hadOutput = running || left > 0 || right > 0;
                oldCancellation = cancellation;
                oldTask = activeTask;
                service = activeService;
                cancellation = null;
                activeTask = null;
                activeService = null;
                running = false;
                paused = false;
                left = right = 0;
                elapsed = total = 0;
                stepLabel = "已停止";
                status = string.IsNullOrEmpty(reason) ? "震动已停止" : reason;
                if (hadOutput) lastStoppedUtc = DateTime.UtcNow;
            }
            if (oldCancellation != null) oldCancellation.Cancel();
            if (oldTask != null && !oldTask.IsCompleted)
            {
                try { oldTask.Wait(250); }
                catch (AggregateException) { }
            }
            try { if (service != null) service.StopRumble(); }
            catch { }
            if (oldCancellation != null) oldCancellation.Dispose();
        }

        public RumblePatternPlayerSnapshot GetSnapshot()
        {
            lock (sync)
            {
                return new RumblePatternPlayerSnapshot
                {
                    IsRunning = running,
                    IsPaused = paused,
                    LeftStrength = left,
                    RightStrength = right,
                    ElapsedSeconds = elapsed,
                    TotalSeconds = total,
                    RemainingSeconds = Math.Max(0, total - elapsed),
                    Progress = total <= 0 ? 0 : Math.Max(0, Math.Min(1, elapsed / total)),
                    PatternId = patternId,
                    PatternName = patternName,
                    CurrentStepLabel = stepLabel,
                    Status = status,
                    LastOutputSucceeded = lastOutputSucceeded,
                    LastStoppedUtc = lastStoppedUtc
                };
            }
        }

        private async Task PlayAsync(IControllerRumbleService service, RumblePatternDefinition pattern, RumbleDeviceProfile profile, int ownGeneration, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            double pausedAt = -1;
            double pausedSeconds = 0;
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    bool isPaused;
                    lock (sync) isPaused = generation == ownGeneration && paused;
                    if (isPaused)
                    {
                        if (pausedAt < 0) pausedAt = stopwatch.Elapsed.TotalSeconds;
                        await Task.Delay(FrameMilliseconds, token).ConfigureAwait(false);
                        continue;
                    }
                    if (pausedAt >= 0)
                    {
                        pausedSeconds += stopwatch.Elapsed.TotalSeconds - pausedAt;
                        pausedAt = -1;
                    }
                    double currentTime = stopwatch.Elapsed.TotalSeconds - pausedSeconds;
                    if (currentTime >= pattern.TotalDuration) break;
                    double targetLeft;
                    double targetRight;
                    string currentLabel;
                    RumblePatternMath.Evaluate(pattern, currentTime, out targetLeft, out targetRight, out currentLabel);
                    targetLeft = RumbleSafetyPolicy.ApplyCalibration(targetLeft, true, profile);
                    targetRight = RumbleSafetyPolicy.ApplyCalibration(targetRight, false, profile);
                    string error;
                    if (!service.TrySetRumble(targetLeft, targetRight, out error)) throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "未知输出错误" : error);
                    lock (sync)
                    {
                        if (generation != ownGeneration) return;
                        left = targetLeft;
                        right = targetRight;
                        elapsed = currentTime;
                        stepLabel = currentLabel;
                        status = "震动输出中";
                        lastOutputSucceeded = true;
                    }
                    await Task.Delay(FrameMilliseconds, token).ConfigureAwait(false);
                }
                Finish(service, ownGeneration, "时间线播放完成，输出已自动归零", true);
            }
            catch (OperationCanceledException)
            {
                Finish(service, ownGeneration, "播放已取消并归零", false);
            }
            catch (Exception ex)
            {
                Finish(service, ownGeneration, "播放异常，输出已归零：" + ex.Message, false);
            }
        }

        private void Finish(IControllerRumbleService service, int ownGeneration, string message, bool succeeded)
        {
            try { service.StopRumble(); }
            catch { succeeded = false; }
            CancellationTokenSource completed = null;
            lock (sync)
            {
                if (generation != ownGeneration) return;
                running = false;
                paused = false;
                left = right = 0;
                elapsed = total;
                stepLabel = "完成";
                status = message;
                lastOutputSucceeded = succeeded;
                lastStoppedUtc = DateTime.UtcNow;
                completed = cancellation;
                cancellation = null;
                activeTask = null;
                activeService = null;
            }
            if (completed != null) completed.Dispose();
        }

        public void Dispose() { Stop("播放器已关闭，输出已归零"); }
    }

    public enum RumbleCalibrationPhase
    {
        Idle,
        LeftMinimum,
        RightMinimum,
        LeftComfortMaximum,
        RightComfortMaximum,
        Completed
    }

    public sealed class RumbleCalibrationController
    {
        private readonly ControllerRumbleController controller;
        private RumbleDeviceProfile calibrationProfile;
        public RumbleCalibrationPhase Phase { get; private set; }
        public string Instruction { get; private set; }

        public RumbleCalibrationController(ControllerRumbleController controller)
        {
            this.controller = controller;
            Phase = RumbleCalibrationPhase.Idle;
            Instruction = "尚未开始校准";
        }

        public bool Start(RumbleDeviceProfile profile, out string error)
        {
            if (profile == null) { error = "设备配置不可用"; return false; }
            RumbleDeviceProfile rawProfile = profile.Copy();
            rawProfile.ApplyDeviceCalibration = false;
            calibrationProfile = profile;
            controller.SetDeviceProfile(rawProfile);
            Phase = RumbleCalibrationPhase.LeftMinimum;
            return StartCurrentPhase(out error);
        }

        public bool Confirm(RumbleDeviceProfile profile, out string error)
        {
            error = string.Empty;
            if (profile == null || Phase == RumbleCalibrationPhase.Idle || Phase == RumbleCalibrationPhase.Completed) { error = "当前没有可确认的校准步骤"; return false; }
            RumbleStatusSnapshot snapshot = controller.GetSnapshot();
            if (!snapshot.IsRunning) { error = "校准渐强已结束，请重新开始并在感受到目标强度时及时确认。"; return false; }
            double current = Phase == RumbleCalibrationPhase.LeftMinimum || Phase == RumbleCalibrationPhase.LeftComfortMaximum ? snapshot.LeftStrength : snapshot.RightStrength;
            if (Phase == RumbleCalibrationPhase.LeftMinimum) profile.LeftMinimumPerceptible = Math.Max(0.01, Math.Min(0.40, current));
            else if (Phase == RumbleCalibrationPhase.RightMinimum) profile.RightMinimumPerceptible = Math.Max(0.01, Math.Min(0.40, current));
            else if (Phase == RumbleCalibrationPhase.LeftComfortMaximum) profile.LeftComfortMaximum = Math.Max(0.30, Math.Min(0.65, current));
            else if (Phase == RumbleCalibrationPhase.RightComfortMaximum) profile.RightComfortMaximum = Math.Max(0.30, Math.Min(0.65, current));
            controller.Stop("已记录当前校准强度");
            Phase++;
            if (Phase == RumbleCalibrationPhase.Completed)
            {
                controller.SetDeviceProfile(profile);
                calibrationProfile = null;
                Instruction = "校准完成；舒适上限不会超过 65%。";
                return true;
            }
            return StartCurrentPhase(out error);
        }

        public void Cancel()
        {
            controller.Stop("震动校准已取消");
            if (calibrationProfile != null) controller.SetDeviceProfile(calibrationProfile);
            calibrationProfile = null;
            Phase = RumbleCalibrationPhase.Idle;
            Instruction = "校准已取消";
        }

        private bool StartCurrentPhase(out string error)
        {
            bool left = Phase == RumbleCalibrationPhase.LeftMinimum || Phase == RumbleCalibrationPhase.LeftComfortMaximum;
            bool minimum = Phase == RumbleCalibrationPhase.LeftMinimum || Phase == RumbleCalibrationPhase.RightMinimum;
            double start = minimum ? 0.01 : 0.20;
            double maximum = minimum ? 0.40 : 0.65;
            RumblePatternDefinition pattern = new RumblePatternDefinition
            {
                Id = "calibration-" + Phase.ToString().ToLowerInvariant(),
                Name = "最小可感知与舒适上限校准"
            };
            pattern.Steps.Add(new RumblePatternStep { StartTime = 0, Duration = 0.05, LeftStrength = left ? start : 0, RightStrength = left ? 0 : start, Interpolation = RumbleInterpolation.Step, Label = "安全起始" });
            pattern.Steps.Add(new RumblePatternStep { StartTime = 0.05, Duration = 8.0, LeftStrength = left ? maximum : 0, RightStrength = left ? 0 : maximum, Interpolation = RumbleInterpolation.Linear, Label = minimum ? "逐渐增加到 40%" : "逐渐增加到安全上限 65%" });
            Instruction = minimum
                ? (left ? "左侧电机逐渐增强；刚刚能感觉到时点击确认。" : "右侧电机逐渐增强；刚刚能感觉到时点击确认。")
                : (left ? "左侧电机从中等强度逐渐增强；感觉已经足够强时点击确认。" : "右侧电机从中等强度逐渐增强；感觉已经足够强时点击确认。");
            return controller.PlayPattern(pattern, 1.0, out error);
        }
    }

    public static class RumbleProfessionalSelfTest
    {
        public static string Run()
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(), "ControllerLab-rumble-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                VerifyInterpolation();
                VerifyPlayerSafety();
                VerifyPersistence(temporaryRoot);
                return "professional-player-25hz-zero-channels-interpolation-complete-cancel-page-exception-single-task-persistence-corruption-unsupported";
            }
            finally
            {
                string full = Path.GetFullPath(temporaryRoot);
                string prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(full)) Directory.Delete(full, true);
            }
        }

        private static void VerifyInterpolation()
        {
            RumblePatternDefinition pattern = Pattern("linear", 1.0, 1.0, 0.50, RumbleInterpolation.Linear);
            double left;
            double right;
            string label;
            RumblePatternMath.Evaluate(pattern, 0.5, out left, out right, out label);
            Require(Math.Abs(left - 0.5) < 0.02 && Math.Abs(right - 0.25) < 0.02, "timeline linear interpolation failed");
            Require(RumblePatternPlayer.OutputRefreshRateHz >= 20 && RumblePatternPlayer.OutputRefreshRateHz <= 60, "output refresh rate is outside safe range");
        }

        private static void VerifyPlayerSafety()
        {
            RumblePatternPlayer player = new RumblePatternPlayer();
            RumbleDeviceProfile profile = new RumbleDeviceProfile { SafetyLimitsEnabled = true };
            string error;

            TrackingRumbleService zero = new TrackingRumbleService(true);
            Require(!player.Start(zero, Pattern("zero", 0.10, 0, 0, RumbleInterpolation.Step), 1, profile, out error), "0% pattern should not start");
            Require(zero.SetCount == 0 && zero.Left == 0 && zero.Right == 0, "0% pattern must remain stopped");

            TrackingRumbleService leftService = new TrackingRumbleService(true);
            Require(player.Start(leftService, Pattern("left", 0.14, 0.35, 0, RumbleInterpolation.Step), 1, profile, out error), "left pattern failed to start");
            Thread.Sleep(70);
            Require(leftService.MaxLeft > 0 && leftService.MaxRight == 0, "left channel activated right channel");
            Thread.Sleep(180);
            Require(!player.IsRunning && leftService.Left == 0 && leftService.Right == 0, "completion did not clear output");

            TrackingRumbleService rightService = new TrackingRumbleService(true);
            Require(player.Start(rightService, Pattern("right", 0.30, 0, 0.35, RumbleInterpolation.Step), 1, profile, out error), "right pattern failed to start");
            Thread.Sleep(70);
            Require(rightService.MaxRight > 0 && rightService.MaxLeft == 0, "right channel activated left channel");
            player.Stop("page switch selftest");
            Require(rightService.Left == 0 && rightService.Right == 0 && rightService.StopCount > 0, "page switch stop did not clear output");

            TrackingRumbleService cancelService = new TrackingRumbleService(true);
            Require(player.Start(cancelService, Pattern("cancel", 0.60, 0.30, 0.30, RumbleInterpolation.Step), 1, profile, out error), "cancel pattern failed to start");
            Thread.Sleep(55);
            Require(player.Pause(out error) && cancelService.Left == 0 && cancelService.Right == 0, "pause did not clear output");
            Require(player.Resume(out error), "paused player did not resume");
            Thread.Sleep(45);
            player.Stop("cancel selftest");
            Require(!player.IsRunning && cancelService.Left == 0 && cancelService.Right == 0, "cancellation did not clear output");

            RumblePatternDefinition high = RumbleSafetyPolicy.Apply(Pattern("high", 30, 0.95, 0.95, RumbleInterpolation.Step), 1, cancelService.Capabilities, profile);
            Require(high != null && high.TotalDuration <= 3.01, "high-strength continuous output was not shortened");

            TrackingRumbleService throwing = new TrackingRumbleService(true) { ThrowOnSet = true };
            Require(player.Start(throwing, Pattern("exception", 0.20, 0.30, 0.30, RumbleInterpolation.Step), 1, profile, out error), "exception pattern failed to enter player");
            Thread.Sleep(120);
            Require(!player.IsRunning && throwing.Left == 0 && throwing.Right == 0 && throwing.StopCount > 0, "exception did not clear output");

            TrackingRumbleService rapid = new TrackingRumbleService(true) { SetDelayMilliseconds = 8 };
            Require(player.Start(rapid, Pattern("rapid-a", 0.50, 0.25, 0.25, RumbleInterpolation.Step), 1, profile, out error), "rapid pattern A failed");
            Require(player.Start(rapid, Pattern("rapid-b", 0.14, 0.15, 0.30, RumbleInterpolation.Step), 1, profile, out error), "rapid pattern B failed");
            Thread.Sleep(250);
            Require(!player.IsRunning && rapid.MaximumConcurrentSetCalls <= 1 && rapid.Left == 0 && rapid.Right == 0, "rapid clicks created concurrent playback");

            TrackingRumbleService unsupported = new TrackingRumbleService(false);
            Require(!player.Start(unsupported, Pattern("unsupported", 0.20, 0.30, 0.30, RumbleInterpolation.Step), 1, profile, out error), "unsupported service should reject playback");
            Require(unsupported.SetCount == 0, "unsupported service received an output call");
            player.Dispose();
        }

        private static void VerifyPersistence(string temporaryRoot)
        {
            RumbleSettingsStore store = new RumbleSettingsStore(Path.Combine(temporaryRoot, "settings"));
            RumblePatternDefinition custom = Pattern("custom-selftest", 0.25, 0.22, 0.18, RumbleInterpolation.Linear);
            custom.Name = "Selftest Custom";
            custom.IsBuiltIn = false;
            store.SaveCustomPattern(custom);
            List<RumblePatternDefinition> loaded = store.LoadCustomPatterns();
            Require(loaded.Count == 1 && loaded[0].Name == custom.Name && loaded[0].Steps.Count == 1, "custom pattern did not reload");
            Require(store.DeleteCustomPattern(custom.Id) && store.LoadCustomPatterns().Count == 0, "custom pattern did not delete");

            ControllerState state = new ControllerState { DeviceId = "private-device-id", DeviceName = "Selftest", ControllerType = ControllerType.Xbox, ConnectionTypeLabel = "USB" };
            RumbleDeviceProfile profile = store.LoadDeviceProfile(state);
            profile.LeftMinimumPerceptible = 0.08;
            string profilePath = store.SaveDeviceProfile(profile);
            Require(!Path.GetFileName(profilePath).Contains("private-device-id"), "device profile path leaked raw device id");
            File.WriteAllText(profilePath, "{ this is damaged json", new UTF8Encoding(false));
            RumbleDeviceProfile recovered = store.LoadDeviceProfile(state);
            Require(recovered != null && recovered.LeftMinimumPerceptible == 0, "damaged profile did not recover to defaults");

            string customPath = Path.Combine(store.RootDirectory, "custom-patterns.json");
            Directory.CreateDirectory(store.RootDirectory);
            File.WriteAllText(customPath, "not json", new UTF8Encoding(false));
            Require(store.LoadCustomPatterns().Count == 0, "damaged custom pattern file should return empty list");
        }

        private static RumblePatternDefinition Pattern(string id, double duration, double left, double right, RumbleInterpolation interpolation)
        {
            RumblePatternDefinition pattern = new RumblePatternDefinition { Id = id, Name = id };
            pattern.Steps.Add(new RumblePatternStep { StartTime = 0, Duration = duration, LeftStrength = left, RightStrength = right, Interpolation = interpolation, Label = id });
            return pattern;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Professional rumble selftest failed: " + message);
        }

        private sealed class TrackingRumbleService : IControllerRumbleService
        {
            private int concurrent;
            public readonly bool Supported;
            public double Left;
            public double Right;
            public double MaxLeft;
            public double MaxRight;
            public int SetCount;
            public int StopCount;
            public int MaximumConcurrentSetCalls;
            public bool ThrowOnSet;
            public int SetDelayMilliseconds;

            public TrackingRumbleService(bool supported) { Supported = supported; }
            public string DeviceId { get { return "professional-selftest"; } }
            public bool IsSupported { get { return Supported; } }
            public string SupportDetails { get { return Supported ? "selftest supported" : "selftest unsupported"; } }
            public RumbleCapabilities Capabilities { get { return new RumbleCapabilities { IsSupported = Supported, SupportsLeftMotor = Supported, SupportsRightMotor = Supported, SupportsIndependentChannels = Supported, MaximumSafeDuration = 30, VerifiedStatus = Supported ? RumbleVerificationStatus.ImplementedUnverified : RumbleVerificationStatus.Unsupported }; } }

            public bool TrySetRumble(double leftStrength, double rightStrength, out string error)
            {
                if (!Supported) { error = SupportDetails; return false; }
                int active = Interlocked.Increment(ref concurrent);
                MaximumConcurrentSetCalls = Math.Max(MaximumConcurrentSetCalls, active);
                try
                {
                    if (SetDelayMilliseconds > 0) Thread.Sleep(SetDelayMilliseconds);
                    if (ThrowOnSet) throw new InvalidOperationException("synthetic output failure");
                    Left = leftStrength;
                    Right = rightStrength;
                    MaxLeft = Math.Max(MaxLeft, leftStrength);
                    MaxRight = Math.Max(MaxRight, rightStrength);
                    SetCount++;
                    error = string.Empty;
                    return true;
                }
                finally { Interlocked.Decrement(ref concurrent); }
            }

            public void StopRumble() { Left = 0; Right = 0; StopCount++; }
            public void Dispose() { StopRumble(); }
        }
    }
}
