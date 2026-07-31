using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ControllerLab
{
    public enum HealthCheckStepKind
    {
        DeviceInfo,
        Buttons,
        DPad,
        LeftStickStationary,
        RightStickStationary,
        LeftStickCircularity,
        RightStickCircularity,
        LeftStickReturn,
        RightStickReturn,
        LeftTrigger,
        RightTrigger,
        Rumble,
        Touchpad,
        Gyroscope,
        GenerateReport
    }

    public enum HealthCheckStepStatus
    {
        Waiting,
        Testing,
        Passed,
        Attention,
        Abnormal,
        Skipped,
        Unsupported
    }

    public enum HealthReportOverallStatus
    {
        Excellent,
        Good,
        Attention,
        Poor,
        Incomplete
    }

    public enum RumbleUserFeedback
    {
        NotAnswered,
        Normal,
        Weak,
        NoRumble,
        WrongPosition,
        Skipped
    }

    [DataContract]
    public sealed class HealthReportMetric
    {
        [DataMember(Order = 1)] public string Name = string.Empty;
        [DataMember(Order = 2)] public double Value;
        [DataMember(Order = 3)] public string Unit = string.Empty;
        [DataMember(Order = 4)] public string DisplayValue = string.Empty;
    }

    [DataContract]
    public sealed class HealthCheckStepRecord
    {
        [DataMember(Order = 1)] public string Kind = string.Empty;
        [DataMember(Order = 2)] public string Title = string.Empty;
        [DataMember(Order = 3)] public string Category = string.Empty;
        [DataMember(Order = 4)] public string Status = "Waiting";
        [DataMember(Order = 5)] public double Score;
        [DataMember(Order = 6)] public bool ParticipatesInScore;
        [DataMember(Order = 7)] public string Summary = string.Empty;
        [DataMember(Order = 8)] public string Issue = string.Empty;
        [DataMember(Order = 9)] public string Advice = string.Empty;
        [DataMember(Order = 10)] public List<HealthReportMetric> Metrics = new List<HealthReportMetric>();
        [DataMember(Order = 11)] public List<string> UserFeedback = new List<string>();
    }

    [DataContract]
    public sealed class HealthCategoryScore
    {
        [DataMember(Order = 1)] public string Category = string.Empty;
        [DataMember(Order = 2)] public string DisplayName = string.Empty;
        [DataMember(Order = 3)] public double Weight;
        [DataMember(Order = 4)] public double Score;
        [DataMember(Order = 5)] public bool Supported;
        [DataMember(Order = 6)] public bool Tested;
        [DataMember(Order = 7)] public string Status = string.Empty;
    }

    [DataContract]
    public sealed class ControllerHealthReport
    {
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public string ReportId = string.Empty;
        [DataMember(Order = 3)] public string DeviceId = string.Empty;
        [DataMember(Order = 4)] public string DeviceName = string.Empty;
        [DataMember(Order = 5)] public string DeviceType = string.Empty;
        [DataMember(Order = 6)] public string ConnectionType = string.Empty;
        [DataMember(Order = 7)] public DateTime TestDateUtc;
        [DataMember(Order = 8)] public double TestDurationSeconds;
        [DataMember(Order = 9)] public string AppVersion = "v1.3.0-health-test";
        [DataMember(Order = 10)] public bool IsComplete;
        [DataMember(Order = 11)] public double OverallScore;
        [DataMember(Order = 12)] public string OverallStatus = "Incomplete";
        [DataMember(Order = 13)] public string OverallStatusChinese = "检测未完成";
        [DataMember(Order = 14)] public List<HealthCheckStepRecord> Steps = new List<HealthCheckStepRecord>();
        [DataMember(Order = 15)] public List<HealthCategoryScore> Categories = new List<HealthCategoryScore>();
        [DataMember(Order = 16)] public List<string> SupportedItems = new List<string>();
        [DataMember(Order = 17)] public List<string> UnsupportedItems = new List<string>();
        [DataMember(Order = 18)] public List<string> UserRumbleFeedback = new List<string>();
        [DataMember(Order = 19)] public bool SevereHardwareIssue;

        public string ToMarkdown()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# ControllerLab 手柄健康报告");
            text.AppendLine();
            text.AppendLine("- ReportId: `" + ReportId + "`");
            text.AppendLine("- 设备: " + DeviceName);
            text.AppendLine("- 类型: " + DeviceType);
            text.AppendLine("- 连接: " + ConnectionType);
            text.AppendLine("- 检测时间: " + TestDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            text.AppendLine("- 测试时长: " + TestDurationSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒");
            text.AppendLine("- 综合评分: " + OverallScore.ToString("0", CultureInfo.InvariantCulture) + "/100");
            text.AppendLine("- 总体状态: " + OverallStatus + " / " + OverallStatusChinese);
            text.AppendLine("- 完整完成: " + (IsComplete ? "是" : "否"));
            text.AppendLine();
            text.AppendLine("## 分类评分");
            text.AppendLine();
            text.AppendLine("| 分类 | 状态 | 分数 |");
            text.AppendLine("| --- | --- | ---: |");
            for (int i = 0; i < Categories.Count; i++)
            {
                HealthCategoryScore category = Categories[i];
                text.AppendLine("| " + category.DisplayName + " | " + category.Status + " | " + (category.Tested ? category.Score.ToString("0", CultureInfo.InvariantCulture) : "—") + " |");
            }
            text.AppendLine();
            text.AppendLine("## 检测项目");
            for (int i = 0; i < Steps.Count; i++)
            {
                HealthCheckStepRecord step = Steps[i];
                text.AppendLine();
                text.AppendLine("### " + step.Title);
                text.AppendLine();
                text.AppendLine("- 状态: " + step.Status);
                if (step.ParticipatesInScore) text.AppendLine("- 分数: " + step.Score.ToString("0", CultureInfo.InvariantCulture) + "/100");
                if (!string.IsNullOrEmpty(step.Summary)) text.AppendLine("- 结果: " + step.Summary);
                if (!string.IsNullOrEmpty(step.Issue)) text.AppendLine("- 问题: " + step.Issue);
                if (!string.IsNullOrEmpty(step.Advice)) text.AppendLine("- 建议: " + step.Advice);
                for (int m = 0; m < step.Metrics.Count; m++)
                {
                    HealthReportMetric metric = step.Metrics[m];
                    text.AppendLine("- " + metric.Name + ": " + (!string.IsNullOrEmpty(metric.DisplayValue) ? metric.DisplayValue : metric.Value.ToString("0.###", CultureInfo.InvariantCulture) + metric.Unit));
                }
                for (int f = 0; f < step.UserFeedback.Count; f++) text.AppendLine("- 用户反馈: " + step.UserFeedback[f]);
            }
            text.AppendLine();
            text.AppendLine("> 不支持与跳过项目不参与评分；跳过项目会使报告标记为不完整。未经过真实手柄确认的数据不会自动判定为正常。");
            return text.ToString();
        }
    }

    public sealed class TriggerTestResult
    {
        public bool IsValid;
        public int SampleCount;
        public double SamplingFrequencyHz;
        public double RestingValuePercent;
        public double MinimumValuePercent;
        public double MaximumValuePercent;
        public double EffectiveTravelPercent;
        public bool ReachedFullRange;
        public bool ReturnedToZero;
        public double SmoothnessScore;
        public int JumpCount;
        public string Status = "未检测";
    }

    public static class TriggerHealthAnalyzer
    {
        public const int MinimumSamples = 30;
        public const double FullRangeThreshold = 0.95;
        public const double ReturnThreshold = 0.05;
        public const double JumpThreshold = 0.12;

        public static TriggerTestResult Analyze(IList<StickSample> samples)
        {
            TriggerTestResult result = new TriggerTestResult { SampleCount = samples == null ? 0 : samples.Count };
            if (samples == null || samples.Count < MinimumSamples) return result;
            double minimum = 1;
            double maximum = 0;
            double restSum = 0;
            int restCount = Math.Min(20, samples.Count);
            int jumps = 0;
            double previous = Math.Max(0, Math.Min(1, samples[0].X));
            for (int i = 0; i < samples.Count; i++)
            {
                double value = Math.Max(0, Math.Min(1, samples[i].X));
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
                if (i < restCount) restSum += value;
                if (i > 0 && Math.Abs(value - previous) >= JumpThreshold) jumps++;
                previous = value;
            }
            double tail = 0;
            int tailCount = Math.Min(20, samples.Count);
            for (int i = samples.Count - tailCount; i < samples.Count; i++) tail += Math.Max(0, Math.Min(1, samples[i].X));
            double resting = restSum / restCount;
            double finalValue = tail / tailCount;
            result.IsValid = true;
            result.RestingValuePercent = resting * 100.0;
            result.MinimumValuePercent = minimum * 100.0;
            result.MaximumValuePercent = maximum * 100.0;
            result.EffectiveTravelPercent = Math.Max(0, maximum - resting) * 100.0;
            result.ReachedFullRange = maximum >= FullRangeThreshold;
            result.ReturnedToZero = finalValue <= ReturnThreshold;
            result.JumpCount = jumps;
            result.SmoothnessScore = Math.Max(0, Math.Min(100, 100.0 - jumps * 12.0));
            result.SamplingFrequencyHz = SamplingFrequency(samples);
            result.Status = result.ReachedFullRange && result.ReturnedToZero && jumps <= 2 ? "正常" : maximum >= 0.85 && finalValue <= 0.08 && jumps <= 5 ? "需要注意" : "异常";
            return result;
        }

        private static double SamplingFrequency(IList<StickSample> samples)
        {
            if (samples == null || samples.Count < 2) return 0;
            double seconds = (samples[samples.Count - 1].Timestamp - samples[0].Timestamp).TotalSeconds;
            return seconds <= 0 ? 0 : (samples.Count - 1) / seconds;
        }
    }

    public static class ControllerHealthScoring
    {
        private static readonly Dictionary<string, double> CategoryWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "Buttons", 15 }, { "Sticks", 35 }, { "Triggers", 20 }, { "Rumble", 10 }, { "Touchpad", 10 }, { "Gyroscope", 10 }
        };

        public static void Score(ControllerHealthReport report)
        {
            if (report == null) throw new ArgumentNullException("report");
            report.Categories.Clear();
            string[] categories = { "Buttons", "Sticks", "Triggers", "Rumble", "Touchpad", "Gyroscope" };
            string[] labels = { "按键", "摇杆", "扳机", "震动", "触摸板", "陀螺仪" };
            double weighted = 0;
            double includedWeight = 0;
            bool skipped = false;
            bool unfinished = false;
            for (int c = 0; c < categories.Length; c++)
            {
                string categoryName = categories[c];
                double scoreSum = 0;
                double scoreCount = 0;
                bool supported = false;
                bool tested = false;
                for (int i = 0; i < report.Steps.Count; i++)
                {
                    HealthCheckStepRecord step = report.Steps[i];
                    if (!string.Equals(step.Category, categoryName, StringComparison.OrdinalIgnoreCase)) continue;
                    supported = true;
                    if (string.Equals(step.Status, HealthCheckStepStatus.Skipped.ToString(), StringComparison.OrdinalIgnoreCase)) skipped = true;
                    if (string.Equals(step.Status, HealthCheckStepStatus.Waiting.ToString(), StringComparison.OrdinalIgnoreCase) || string.Equals(step.Status, HealthCheckStepStatus.Testing.ToString(), StringComparison.OrdinalIgnoreCase)) unfinished = true;
                    if (!step.ParticipatesInScore) continue;
                    tested = true;
                    scoreSum += step.Score;
                    scoreCount++;
                }
                double categoryScore = scoreCount <= 0 ? 0 : scoreSum / scoreCount;
                HealthCategoryScore category = new HealthCategoryScore
                {
                    Category = categoryName,
                    DisplayName = labels[c],
                    Weight = CategoryWeights[categoryName],
                    Score = categoryScore,
                    Supported = supported,
                    Tested = tested,
                    Status = !supported ? "不支持" : !tested ? "未检测" : categoryScore >= 90 ? "优秀" : categoryScore >= 75 ? "良好" : categoryScore >= 55 ? "需要注意" : "异常"
                };
                report.Categories.Add(category);
                if (tested)
                {
                    weighted += categoryScore * category.Weight;
                    includedWeight += category.Weight;
                }
            }
            double overall = includedWeight <= 0 ? 0 : weighted / includedWeight;
            if (report.SevereHardwareIssue) overall = Math.Min(overall, 69.0);
            report.OverallScore = Math.Round(Math.Max(0, Math.Min(100, overall)));
            report.IsComplete = !skipped && !unfinished && AllSupportedStepsResolved(report.Steps);
            if (!report.IsComplete)
            {
                report.OverallStatus = HealthReportOverallStatus.Incomplete.ToString();
                report.OverallStatusChinese = "检测未完成";
            }
            else if (report.OverallScore >= 90)
            {
                report.OverallStatus = HealthReportOverallStatus.Excellent.ToString();
                report.OverallStatusChinese = "优秀";
            }
            else if (report.OverallScore >= 75)
            {
                report.OverallStatus = HealthReportOverallStatus.Good.ToString();
                report.OverallStatusChinese = "良好";
            }
            else if (report.OverallScore >= 55)
            {
                report.OverallStatus = HealthReportOverallStatus.Attention.ToString();
                report.OverallStatusChinese = "需要注意";
            }
            else
            {
                report.OverallStatus = HealthReportOverallStatus.Poor.ToString();
                report.OverallStatusChinese = "状态较差";
            }
        }

        private static bool AllSupportedStepsResolved(IList<HealthCheckStepRecord> steps)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                string status = steps[i].Status;
                if (status == HealthCheckStepStatus.Waiting.ToString() || status == HealthCheckStepStatus.Testing.ToString() || status == HealthCheckStepStatus.Skipped.ToString()) return false;
            }
            return true;
        }
    }

    public sealed class ControllerHealthReportStore
    {
        private readonly string directory;
        public bool SaveEnabled { get; set; }

        public static string DefaultDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerLab", "health-reports"); }
        }

        public ControllerHealthReportStore() : this(DefaultDirectory) { }

        public ControllerHealthReportStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Report directory is required.", "directory");
            this.directory = Path.GetFullPath(directory);
            SaveEnabled = true;
        }

        public string DirectoryPath { get { return directory; } }

        public string Save(ControllerHealthReport report)
        {
            if (report == null) throw new ArgumentNullException("report");
            if (!SaveEnabled) return string.Empty;
            if (string.IsNullOrEmpty(report.ReportId)) report.ReportId = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(directory);
            string path = ReportPath(report.ReportId, ".json");
            WriteJson(report, path);
            File.WriteAllText(ReportPath(report.ReportId, ".md"), report.ToMarkdown(), new UTF8Encoding(false));
            return path;
        }

        public List<ControllerHealthReport> LoadAll()
        {
            List<ControllerHealthReport> reports = new List<ControllerHealthReport>();
            if (!Directory.Exists(directory)) return reports;
            string[] files = Directory.GetFiles(directory, "health-report-*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    using (FileStream stream = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ControllerHealthReport report = new DataContractJsonSerializer(typeof(ControllerHealthReport)).ReadObject(stream) as ControllerHealthReport;
                        if (report != null) reports.Add(report);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (SerializationException) { }
            }
            reports.Sort(delegate(ControllerHealthReport left, ControllerHealthReport right) { return right.TestDateUtc.CompareTo(left.TestDateUtc); });
            return reports;
        }

        public void Delete(string reportId)
        {
            if (string.IsNullOrEmpty(reportId)) return;
            string json = ReportPath(reportId, ".json");
            string markdown = ReportPath(reportId, ".md");
            if (File.Exists(json)) File.Delete(json);
            if (File.Exists(markdown)) File.Delete(markdown);
        }

        public void Export(ControllerHealthReport report, string destination, bool markdown)
        {
            if (report == null) throw new ArgumentNullException("report");
            if (string.IsNullOrEmpty(destination)) throw new ArgumentException("Destination is required.", "destination");
            string full = Path.GetFullPath(destination);
            string parent = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            if (markdown) File.WriteAllText(full, report.ToMarkdown(), new UTF8Encoding(false));
            else WriteJson(report, full);
        }

        private string ReportPath(string reportId, string extension)
        {
            string safe = SanitizeId(reportId);
            string path = Path.GetFullPath(Path.Combine(directory, "health-report-" + safe + extension));
            string prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Report path escaped the application data directory.");
            return path;
        }

        private static string SanitizeId(string reportId)
        {
            StringBuilder value = new StringBuilder();
            for (int i = 0; i < reportId.Length; i++) if (char.IsLetterOrDigit(reportId[i]) || reportId[i] == '-' || reportId[i] == '_') value.Append(reportId[i]);
            if (value.Length == 0) throw new ArgumentException("Invalid report id.", "reportId");
            return value.ToString();
        }

        private static void WriteJson(ControllerHealthReport report, string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                new DataContractJsonSerializer(typeof(ControllerHealthReport), settings).WriteObject(stream, report);
            }
        }
    }

    public static class ControllerHealthCheckSelfTest
    {
        public static string Run()
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(), "ControllerLab-health-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                VerifyTriggerAnalysis();
                VerifyScoring();
                VerifyReportRoundTrip(temporaryRoot);
                VerifyDynamicWizardAndDisconnect(temporaryRoot);
                return "Controller health-check selftest passed: trigger analysis, dynamic capability steps, skip semantics, severe score cap, disconnect recovery, JSON/Markdown save-load-export-delete.";
            }
            finally
            {
                string full = Path.GetFullPath(temporaryRoot);
                string tempPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(full)) Directory.Delete(full, true);
            }
        }

        private static void VerifyTriggerAnalysis()
        {
            List<StickSample> samples = new List<StickSample>();
            DateTime started = DateTime.UtcNow;
            for (int i = 0; i < 20; i++) samples.Add(new StickSample(started.AddMilliseconds(i * 5), 0.004, 0));
            for (int i = 0; i <= 100; i++) samples.Add(new StickSample(started.AddMilliseconds((20 + i) * 5), i / 100.0, 0));
            for (int i = 100; i >= 0; i--) samples.Add(new StickSample(started.AddMilliseconds((121 + 100 - i) * 5), i / 100.0, 0));
            for (int i = 0; i < 20; i++) samples.Add(new StickSample(started.AddMilliseconds((222 + i) * 5), 0.003, 0));
            TriggerTestResult result = TriggerHealthAnalyzer.Analyze(samples);
            Require(result.IsValid, "trigger result should be valid");
            Require(result.ReachedFullRange, "trigger should reach full range");
            Require(result.ReturnedToZero, "trigger should return to zero");
            Require(result.EffectiveTravelPercent >= 99, "trigger effective travel should be full");
            Require(result.JumpCount == 0, "smooth trigger should not contain jumps");
        }

        private static void VerifyScoring()
        {
            ControllerHealthReport incomplete = new ControllerHealthReport { ReportId = "score-incomplete" };
            incomplete.Steps.Add(Step("buttons", "Buttons", HealthCheckStepStatus.Passed, 96, true));
            incomplete.Steps.Add(Step("left-stick", "Sticks", HealthCheckStepStatus.Skipped, 0, false));
            ControllerHealthScoring.Score(incomplete);
            Require(incomplete.OverallStatus == HealthReportOverallStatus.Incomplete.ToString(), "skipped item must produce Incomplete");
            Require(Math.Abs(incomplete.OverallScore - 96) < 0.1, "skipped item must not dilute the score");

            ControllerHealthReport severe = new ControllerHealthReport { ReportId = "score-severe", SevereHardwareIssue = true };
            severe.Steps.Add(Step("buttons", "Buttons", HealthCheckStepStatus.Passed, 100, true));
            severe.Steps.Add(Step("sticks", "Sticks", HealthCheckStepStatus.Abnormal, 90, true));
            ControllerHealthScoring.Score(severe);
            Require(severe.IsComplete, "resolved severe result should still be complete");
            Require(severe.OverallScore <= 69, "severe hardware issue must cap the overall score");
            Require(severe.OverallStatus != HealthReportOverallStatus.Excellent.ToString(), "severe hardware issue must not be excellent");
        }

        private static void VerifyReportRoundTrip(string temporaryRoot)
        {
            string directory = Path.Combine(temporaryRoot, "roundtrip");
            ControllerHealthReportStore store = new ControllerHealthReportStore(directory);
            ControllerHealthReport report = new ControllerHealthReport
            {
                ReportId = "roundtrip-report",
                DeviceId = "xinput:0",
                DeviceName = "Selftest Controller",
                DeviceType = ControllerType.Xbox.ToString(),
                ConnectionType = "USB",
                TestDateUtc = DateTime.UtcNow
            };
            report.Steps.Add(Step("buttons", "Buttons", HealthCheckStepStatus.Passed, 98, true));
            ControllerHealthScoring.Score(report);
            string json = store.Save(report);
            string markdown = Path.ChangeExtension(json, ".md");
            Require(File.Exists(json) && File.Exists(markdown), "store should write JSON and Markdown");
            List<ControllerHealthReport> loaded = store.LoadAll();
            Require(loaded.Count == 1 && loaded[0].ReportId == report.ReportId, "saved report should load back");
            string exportJson = Path.Combine(temporaryRoot, "export.json");
            string exportMarkdown = Path.Combine(temporaryRoot, "export.md");
            store.Export(report, exportJson, false);
            store.Export(report, exportMarkdown, true);
            Require(File.ReadAllText(exportJson).Contains("roundtrip-report"), "JSON export should contain report id");
            Require(File.ReadAllText(exportMarkdown).Contains("Selftest Controller"), "Markdown export should contain device name");
            store.Delete(report.ReportId);
            Require(store.LoadAll().Count == 0, "deleted report should not remain in history");
        }

        private static void VerifyDynamicWizardAndDisconnect(string temporaryRoot)
        {
            FakeRumbleService service = new FakeRumbleService();
            using (ControllerRumbleController rumble = new ControllerRumbleController(service))
            using (ControllerHealthCheckViewModel viewModel = new ControllerHealthCheckViewModel(rumble, new ControllerHealthReportStore(Path.Combine(temporaryRoot, "wizard"))))
            {
                string reason;
                Require(viewModel.Prepare(DualSenseState(false), out reason), "limited DualSense wizard should prepare: " + reason);
                Require(!ContainsStep(viewModel.Steps, HealthCheckStepKind.Touchpad) && !ContainsStep(viewModel.Steps, HealthCheckStepKind.Gyroscope), "unparsed DualSense data must not create exclusive test steps");
                Require(viewModel.UnsupportedItems.Count >= 2, "limited DualSense should disclose unsupported touch and motion checks");
                Require(viewModel.Prepare(DualSenseState(true), out reason), "full DualSense wizard should prepare: " + reason);
                Require(ContainsStep(viewModel.Steps, HealthCheckStepKind.Touchpad) && ContainsStep(viewModel.Steps, HealthCheckStepKind.Gyroscope), "real parsed DualSense capabilities should create exclusive steps");

                ControllerState state = XboxState(true);
                Require(viewModel.Prepare(state, out reason), "Xbox wizard should prepare: " + reason);
                for (int i = 0; i < viewModel.Steps.Count; i++)
                    Require(viewModel.Steps[i].Kind != HealthCheckStepKind.Touchpad && viewModel.Steps[i].Kind != HealthCheckStepKind.Gyroscope, "Xbox wizard must not show DualSense-only steps");
                Require(viewModel.Start(out reason), "wizard should start: " + reason);
                Require(viewModel.Next(out reason), "device confirmation should advance: " + reason);
                while (viewModel.IsRunning)
                {
                    Require(viewModel.CurrentStep != null, "running wizard must have a current step");
                    Require(viewModel.CurrentStep.Kind != HealthCheckStepKind.GenerateReport, "report step should auto-complete");
                    Require(viewModel.SkipCurrent(out reason), "test step should skip and advance: " + reason);
                }
                Require(viewModel.Report != null, "wizard should generate a report");
                Require(!viewModel.Report.IsComplete && viewModel.Report.OverallStatus == HealthReportOverallStatus.Incomplete.ToString(), "skipped wizard must be incomplete");
                Require(File.Exists(viewModel.SavedReportPath), "wizard report should be persisted");
            }

            FakeRumbleService disconnectService = new FakeRumbleService();
            using (ControllerRumbleController rumble = new ControllerRumbleController(disconnectService))
            using (ControllerHealthCheckViewModel viewModel = new ControllerHealthCheckViewModel(rumble, new ControllerHealthReportStore(Path.Combine(temporaryRoot, "disconnect"))))
            {
                string reason;
                ControllerState state = XboxState(true);
                Require(viewModel.Prepare(state, out reason) && viewModel.Start(out reason) && viewModel.Next(out reason), "disconnect wizard should reach an active step");
                viewModel.Update(XboxState(false), DateTime.UtcNow);
                Require(viewModel.IsDisconnected && viewModel.IsRunning, "disconnect should pause and retain the session");
                Require(!rumble.IsRunning, "disconnect should stop rumble");
                viewModel.Update(XboxState(true), DateTime.UtcNow.AddMilliseconds(20));
                Require(viewModel.CanResumeCurrentStep, "same device reconnect should allow current-step retest");
            }
        }

        private static HealthCheckStepRecord Step(string id, string category, HealthCheckStepStatus status, double score, bool participates)
        {
            return new HealthCheckStepRecord
            {
                Kind = id,
                Title = id,
                Category = category,
                Status = status.ToString(),
                Score = score,
                ParticipatesInScore = participates
            };
        }

        private static ControllerState XboxState(bool connected)
        {
            return new ControllerState
            {
                DeviceId = "xinput:0",
                DeviceName = "Selftest Xbox Controller",
                ControllerType = ControllerType.Xbox,
                ConnectionType = ControllerConnectionType.Wired,
                ConnectionTypeLabel = "USB",
                IsConnected = connected,
                InputSource = ControllerInputSource.XboxXInput,
                InputBackend = "XInput",
                TimestampUtc = DateTime.UtcNow,
                Capabilities = new ControllerCapabilities { HasGuideButton = true, HasTriggerRumble = true }
            };
        }

        private static ControllerState DualSenseState(bool advancedData)
        {
            return new ControllerState
            {
                DeviceId = "hid:dualsense-selftest",
                DeviceName = "Selftest DualSense",
                ControllerType = ControllerType.DualSense,
                ConnectionType = ControllerConnectionType.NativeHid,
                ConnectionTypeLabel = "USB HID",
                IsConnected = true,
                InputSource = ControllerInputSource.DualSenseHid,
                InputBackend = "Raw HID",
                TimestampUtc = DateTime.UtcNow,
                Capabilities = new ControllerCapabilities { HasTouchpad = true, HasTouchCoordinates = advancedData, HasMotionSensors = advancedData, HasLightbar = true },
                DualSense = new DualSenseControllerExtensions
                {
                    TouchCoordinatesAvailable = advancedData,
                    Motion = advancedData ? new MotionSample { IsValid = true, TimestampUtc = DateTime.UtcNow, GyroX = 1, GyroY = 1, GyroZ = 1 } : null
                }
            };
        }

        private static bool ContainsStep(IList<HealthCheckStep> steps, HealthCheckStepKind kind)
        {
            for (int i = 0; i < steps.Count; i++) if (steps[i].Kind == kind) return true;
            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Controller health-check selftest failed: " + message);
        }

        private sealed class FakeRumbleService : IControllerRumbleService
        {
            public string DeviceId { get { return "xinput:0"; } }
            public bool IsSupported { get { return true; } }
            public string SupportDetails { get { return "selftest rumble"; } }
            public RumbleCapabilities Capabilities { get { return new RumbleCapabilities { IsSupported = true, SupportsLeftMotor = true, SupportsRightMotor = true, SupportsIndependentChannels = true, MaximumSafeDuration = 30, VerifiedStatus = RumbleVerificationStatus.ImplementedUnverified }; } }
            public bool TrySetRumble(double leftStrength, double rightStrength, out string error) { error = string.Empty; return true; }
            public void StopRumble() { }
            public void Dispose() { }
        }
    }
}
