using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;

namespace ControllerLab
{
    public sealed class HealthCheckStep
    {
        public HealthCheckStepKind Kind;
        public string Title = string.Empty;
        public string Category = string.Empty;
        public string Instruction = string.Empty;
        public string CompletionCondition = string.Empty;
        public HealthCheckStepStatus Status = HealthCheckStepStatus.Waiting;
        public HealthCheckStepRecord Record = new HealthCheckStepRecord();
    }

    public sealed class HealthButtonProgress
    {
        public string Id = string.Empty;
        public string Label = string.Empty;
        public bool Pressed;
        public bool Released;
        public bool Complete { get { return Pressed && Released; } }
    }

    public sealed class ControllerHealthCheckViewModel : IDisposable
    {
        private enum ReturnPhase { Prepare, Hold, Release }

        private readonly ControllerRumbleController rumble;
        private readonly ControllerHealthReportStore store;
        private readonly ControllerInputTestEngine buttonEngine = new ControllerInputTestEngine();
        private readonly Dictionary<string, HealthButtonProgress> buttonProgress = new Dictionary<string, HealthButtonProgress>();
        private readonly Dictionary<string, bool> lastButtonPressed = new Dictionary<string, bool>();
        private readonly List<StickSample> samples = new List<StickSample>();
        private readonly List<StickReturnResult> currentReturnResults = new List<StickReturnResult>();
        private readonly HashSet<HealthCheckStepKind> severeSteps = new HashSet<HealthCheckStepKind>();
        private CancellationTokenSource stepCancellation;
        private ControllerState lastState;
        private string sessionDeviceId = string.Empty;
        private string sessionDeviceName = string.Empty;
        private ControllerType sessionDeviceType;
        private string sessionConnection = string.Empty;
        private DateTime sessionStartedUtc;
        private DateTime stepStartedUtc;
        private DateTime lastSampleTimestampUtc;
        private DateTime returnStableSinceUtc;
        private ReturnPhase returnPhase;
        private int returnDirectionIndex;
        private int currentIndex = -1;
        private bool simultaneousButtonsObserved;
        private double touchMinX;
        private double touchMaxX;
        private double touchMinY;
        private double touchMaxY;
        private bool touchSingle;
        private bool touchDouble;
        private bool touchPressed;
        private int gyroBaselineSamples;
        private double gyroNoiseSquared;
        private double gyroMaxX;
        private double gyroMaxY;
        private double gyroMaxZ;
        private int rumblePhaseIndex;
        private bool awaitingRumbleFeedback;
        private readonly bool[] rumbleSystemSuccess = new bool[3];
        private readonly RumbleUserFeedback[] rumbleFeedback = new RumbleUserFeedback[3];

        public readonly List<HealthCheckStep> Steps = new List<HealthCheckStep>();
        public readonly List<string> SupportedItems = new List<string>();
        public readonly List<string> UnsupportedItems = new List<string>();
        public JoystickStationaryResult LeftStationary { get; private set; }
        public JoystickStationaryResult RightStationary { get; private set; }
        public DeadzoneRecommendation LeftDeadzone { get; private set; }
        public DeadzoneRecommendation RightDeadzone { get; private set; }
        public StickCircularityResult LeftCircularity { get; private set; }
        public StickCircularityResult RightCircularity { get; private set; }
        public readonly List<StickReturnResult> LeftReturnResults = new List<StickReturnResult>();
        public readonly List<StickReturnResult> RightReturnResults = new List<StickReturnResult>();
        public TriggerTestResult LeftTriggerResult { get; private set; }
        public TriggerTestResult RightTriggerResult { get; private set; }
        public ControllerHealthReport Report { get; private set; }
        public string SavedReportPath { get; private set; }
        public bool IsPrepared { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsReportReady { get { return Report != null; } }
        public bool IsDisconnected { get; private set; }
        public bool IsDeviceChanged { get; private set; }
        public bool CanResumeCurrentStep { get; private set; }
        public string StatusMessage { get; private set; }
        public string DynamicInstruction { get; private set; }
        public string CurrentDetail { get; private set; }
        public int EstimatedSeconds { get; private set; }
        public bool AwaitingRumbleFeedback { get { return awaitingRumbleFeedback; } }
        public string RumblePrompt { get; private set; }
        public bool IsQuietSamplingActive
        {
            get
            {
                HealthCheckStep step = CurrentStep;
                if (!IsRunning || step == null || step.Status != HealthCheckStepStatus.Testing) return false;
                return step.Kind == HealthCheckStepKind.LeftStickStationary || step.Kind == HealthCheckStepKind.RightStickStationary || step.Kind == HealthCheckStepKind.Gyroscope;
            }
        }

        public HealthCheckStep CurrentStep
        {
            get { return currentIndex >= 0 && currentIndex < Steps.Count ? Steps[currentIndex] : null; }
        }

        public int CurrentStepNumber { get { return currentIndex < 0 ? 0 : Math.Min(Steps.Count, currentIndex + 1); } }

        public ControllerHealthCheckViewModel(ControllerRumbleController rumble, ControllerHealthReportStore store)
        {
            if (rumble == null) throw new ArgumentNullException("rumble");
            this.rumble = rumble;
            this.store = store ?? new ControllerHealthReportStore();
            StatusMessage = "等待开始完整检测";
            DynamicInstruction = "连接真实手柄后查看检测能力。";
            CurrentDetail = string.Empty;
        }

        public bool Prepare(ControllerState state, out string reason)
        {
            reason = string.Empty;
            if (IsRunning)
            {
                reason = "完整检测正在进行。";
                return false;
            }
            if (state == null || !state.IsConnected || !state.HasRealInput)
            {
                ResetPreparedState();
                reason = "完整检测需要真实 Xbox XInput 或 DualSense HID 设备。";
                StatusMessage = reason;
                return false;
            }
            lastState = state;
            sessionDeviceId = state.DeviceId ?? string.Empty;
            sessionDeviceName = state.DeviceName ?? "Controller";
            sessionDeviceType = state.ControllerType;
            sessionConnection = state.ConnectionTypeLabel ?? "未知";
            BuildSteps(state, rumble.GetSnapshot());
            IsPrepared = true;
            IsDisconnected = false;
            IsDeviceChanged = false;
            StatusMessage = "设备能力已识别，可以开始完整检测";
            DynamicInstruction = "检测过程约需 " + FormatDuration(EstimatedSeconds) + "，跳过项目不会被判定正常。";
            return true;
        }

        public bool Start(out string reason)
        {
            reason = string.Empty;
            if (!IsPrepared || lastState == null || !lastState.IsConnected || !lastState.HasRealInput)
            {
                reason = "请先连接并识别真实手柄。";
                return false;
            }
            if (!string.Equals(sessionDeviceId, lastState.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                reason = "设备已变化，请重新确认检测设备。";
                return false;
            }
            ResetResults();
            sessionStartedUtc = DateTime.UtcNow;
            IsRunning = true;
            IsDisconnected = false;
            IsDeviceChanged = false;
            CanResumeCurrentStep = false;
            currentIndex = 0;
            EnterCurrentStep(DateTime.UtcNow);
            return true;
        }

        public void Update(ControllerState state, DateTime nowUtc)
        {
            lastState = state;
            if (!IsRunning) return;
            if (state == null || !state.IsConnected || !state.HasRealInput)
            {
                HandleDisconnect("设备已断开：已完成结果保留在内存中，可重新连接后重测当前步骤。");
                return;
            }
            if (!string.Equals(sessionDeviceId, state.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                rumble.Stop("设备已切换，完整检测震动已停止");
                CancelStepToken();
                IsDeviceChanged = true;
                IsDisconnected = true;
                CanResumeCurrentStep = false;
                IsRunning = false;
                StatusMessage = "检测设备已切换；上一台手柄的结果不会写入当前设备报告。";
                DynamicInstruction = "请退出并为当前设备重新开始完整检测。";
                return;
            }
            if (IsDisconnected)
            {
                CanResumeCurrentStep = true;
                StatusMessage = "原设备已重新连接";
                DynamicInstruction = "点击“重新测试”重新开始当前步骤。";
                return;
            }
            HealthCheckStep step = CurrentStep;
            if (step == null || step.Status != HealthCheckStepStatus.Testing) return;
            if (stepCancellation == null || stepCancellation.IsCancellationRequested) return;

            switch (step.Kind)
            {
                case HealthCheckStepKind.Buttons:
                case HealthCheckStepKind.DPad: UpdateButtons(state); break;
                case HealthCheckStepKind.LeftStickStationary:
                case HealthCheckStepKind.RightStickStationary: UpdateStationary(state, nowUtc); break;
                case HealthCheckStepKind.LeftStickCircularity:
                case HealthCheckStepKind.RightStickCircularity: UpdateCircularity(state, nowUtc); break;
                case HealthCheckStepKind.LeftStickReturn:
                case HealthCheckStepKind.RightStickReturn: UpdateReturn(state, nowUtc); break;
                case HealthCheckStepKind.LeftTrigger:
                case HealthCheckStepKind.RightTrigger: UpdateTrigger(state, nowUtc); break;
                case HealthCheckStepKind.Rumble: UpdateRumble(); break;
                case HealthCheckStepKind.Touchpad: UpdateTouchpad(state); break;
                case HealthCheckStepKind.Gyroscope: UpdateGyroscope(state, nowUtc); break;
            }
        }

        public bool Next(out string reason)
        {
            reason = string.Empty;
            HealthCheckStep step = CurrentStep;
            if (!IsRunning || step == null) { reason = "当前没有正在进行的检测。"; return false; }
            if (IsDisconnected) { reason = "设备断开，无法继续。"; return false; }
            if (step.Status == HealthCheckStepStatus.Testing && !TryCompleteManualStep(step, out reason)) return false;
            if (!IsResolved(step.Status)) { reason = "请先完成、重新测试或跳过当前项目。"; return false; }
            if (currentIndex >= Steps.Count - 1) return false;
            currentIndex++;
            HealthCheckStep next = CurrentStep;
            if (next.Status == HealthCheckStepStatus.Waiting) EnterCurrentStep(DateTime.UtcNow);
            else
            {
                StatusMessage = next.Title + " · " + StatusChinese(next.Status);
                DynamicInstruction = "该步骤已有结果；可继续下一步或选择重新测试。";
                CurrentDetail = next.Record.Summary;
            }
            return true;
        }

        public bool Previous(out string reason)
        {
            reason = string.Empty;
            if (!IsRunning || currentIndex <= 0) { reason = "已经是第一步。"; return false; }
            HealthCheckStep leaving = CurrentStep;
            StopCurrentActivity("已返回上一步");
            if (leaving != null && leaving.Status == HealthCheckStepStatus.Testing)
            {
                leaving.Status = HealthCheckStepStatus.Waiting;
                leaving.Record.Status = leaving.Status.ToString();
                leaving.Record.Summary = "已离开此步骤，需要重新开始。";
            }
            currentIndex--;
            HealthCheckStep step = CurrentStep;
            StatusMessage = "已返回：" + step.Title;
            DynamicInstruction = step.Status == HealthCheckStepStatus.Waiting ? step.Instruction : "该步骤已有结果；可直接下一步或选择重新测试。";
            CurrentDetail = step.Record.Summary;
            return true;
        }

        public bool RetestCurrent(out string reason)
        {
            reason = string.Empty;
            if (!IsRunning || CurrentStep == null) { reason = "当前没有可重测的步骤。"; return false; }
            if (lastState == null || !lastState.IsConnected || !string.Equals(sessionDeviceId, lastState.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                reason = "请先重新连接原检测设备。";
                return false;
            }
            IsDisconnected = false;
            CanResumeCurrentStep = false;
            ResetStep(CurrentStep);
            EnterCurrentStep(DateTime.UtcNow);
            return true;
        }

        public bool SkipCurrent(out string reason)
        {
            reason = string.Empty;
            HealthCheckStep step = CurrentStep;
            if (!IsRunning || step == null || step.Kind == HealthCheckStepKind.DeviceInfo || step.Kind == HealthCheckStepKind.GenerateReport)
            {
                reason = "当前步骤不能跳过。";
                return false;
            }
            StopCurrentActivity("当前检测项目已跳过");
            step.Status = HealthCheckStepStatus.Skipped;
            step.Record.Status = step.Status.ToString();
            step.Record.ParticipatesInScore = false;
            step.Record.Score = 0;
            step.Record.Summary = "用户已跳过；本项目不参与评分，也不会被判定为正常。";
            step.Record.Issue = "未检测";
            step.Record.Advice = "如需完整健康结论，请重新检测本项目。";
            return Next(out reason);
        }

        public void CancelAndDiscard()
        {
            rumble.Stop("完整检测已退出，震动已停止");
            CancelStepToken();
            IsRunning = false;
            IsDisconnected = false;
            CanResumeCurrentStep = false;
            currentIndex = -1;
            Report = null;
            SavedReportPath = null;
            StatusMessage = "完整检测已退出，当前进度已放弃";
            DynamicInstruction = "可以重新确认设备并开始检测。";
        }

        public bool SubmitRumbleFeedback(RumbleUserFeedback feedback, out string reason)
        {
            reason = string.Empty;
            if (CurrentStep == null || CurrentStep.Kind != HealthCheckStepKind.Rumble || !awaitingRumbleFeedback)
            {
                reason = "当前没有等待确认的震动步骤。";
                return false;
            }
            rumbleFeedback[rumblePhaseIndex] = feedback;
            awaitingRumbleFeedback = false;
            rumble.Stop("已记录当前震动反馈");
            rumblePhaseIndex++;
            if (rumblePhaseIndex >= 3)
            {
                CompleteRumbleStep();
                return true;
            }
            StartRumblePhase();
            return true;
        }

        public IList<HealthButtonProgress> GetButtonProgress()
        {
            List<HealthButtonProgress> result = new List<HealthButtonProgress>(buttonProgress.Values);
            result.Sort(delegate(HealthButtonProgress a, HealthButtonProgress b) { return string.Compare(a.Label, b.Label, StringComparison.CurrentCulture); });
            return result;
        }

        public void Dispose()
        {
            rumble.Stop("完整检测模块已关闭，震动已停止");
            CancelStepToken();
        }

        private void BuildSteps(ControllerState state, RumbleStatusSnapshot rumbleStatus)
        {
            Steps.Clear();
            SupportedItems.Clear();
            UnsupportedItems.Clear();
            AddStep(HealthCheckStepKind.DeviceInfo, "设备信息确认", string.Empty, "确认当前设备、类型和连接方式。", "设备身份与检测会话绑定");
            AddStep(HealthCheckStepKind.Buttons, "按键检测", "Buttons", "逐个按下并释放所有显示的按键。", "每个按键完成一次按下和释放");
            AddStep(HealthCheckStepKind.DPad, "十字键检测", "Buttons", "依次按下并释放十字键上、下、左、右。", "四个方向均完成一次按下和释放");
            AddStep(HealthCheckStepKind.LeftStickStationary, "左摇杆静止漂移", "Sticks", "松开左摇杆并平稳放置手柄。", "3 秒倒计时后连续采样 5 秒");
            AddStep(HealthCheckStepKind.RightStickStationary, "右摇杆静止漂移", "Sticks", "松开右摇杆并平稳放置手柄。", "3 秒倒计时后连续采样 5 秒");
            AddStep(HealthCheckStepKind.LeftStickCircularity, "左摇杆圆周测试", "Sticks", "将左摇杆推到外圈，沿边缘缓慢旋转一整圈并回到中心。", "至少 72 个样本并形成可分析角度覆盖");
            AddStep(HealthCheckStepKind.RightStickCircularity, "右摇杆圆周测试", "Sticks", "将右摇杆推到外圈，沿边缘缓慢旋转一整圈并回到中心。", "至少 72 个样本并形成可分析角度覆盖");
            AddStep(HealthCheckStepKind.LeftStickReturn, "左摇杆回中测试", "Sticks", "按提示依次完成上、下、左、右四个方向。", "四个方向均检测回中和稳定过程");
            AddStep(HealthCheckStepKind.RightStickReturn, "右摇杆回中测试", "Sticks", "按提示依次完成上、下、左、右四个方向。", "四个方向均检测回中和稳定过程");
            AddStep(HealthCheckStepKind.LeftTrigger, state.ControllerType == ControllerType.Xbox ? "LT 扳机行程" : "L2 扳机行程", "Triggers", "先松开，再缓慢按到底，最后缓慢释放。", "记录起点、峰值、回零和平滑度");
            AddStep(HealthCheckStepKind.RightTrigger, state.ControllerType == ControllerType.Xbox ? "RT 扳机行程" : "R2 扳机行程", "Triggers", "先松开，再缓慢按到底，最后缓慢释放。", "记录起点、峰值、回零和平滑度");
            if (rumbleStatus != null && rumbleStatus.IsSupported)
            {
                AddStep(HealthCheckStepKind.Rumble, "震动测试", "Rumble", "依次确认左侧、右侧和双侧震动。", "三次系统输出均获得用户反馈");
                SupportedItems.Add("基础震动");
            }
            else UnsupportedItems.Add("震动：" + (rumbleStatus == null ? "当前版本暂不支持检测" : rumbleStatus.SupportDetails));

            bool dualSense = state.ControllerType == ControllerType.DualSense;
            bool touch = dualSense && state.Capabilities != null && state.Capabilities.HasTouchCoordinates && state.DualSense != null && state.DualSense.TouchCoordinatesAvailable;
            bool motion = dualSense && state.Capabilities != null && state.Capabilities.HasMotionSensors && state.DualSense != null && state.DualSense.Motion != null && state.DualSense.Motion.IsValid;
            if (touch)
            {
                AddStep(HealthCheckStepKind.Touchpad, "DualSense 触摸板", "Touchpad", "完成单指移动、双指触摸、触摸板按压，并覆盖较大轨迹范围。", "单指、双指、按压与轨迹范围均有真实输入");
                SupportedItems.Add("DualSense 触摸板坐标");
            }
            else if (dualSense) UnsupportedItems.Add("DualSense 触摸板坐标：当前版本暂不支持检测");
            if (motion)
            {
                AddStep(HealthCheckStepKind.Gyroscope, "DualSense 陀螺仪", "Gyroscope", "先静止，再左右倾斜、前后倾斜并绕轴旋转。", "完成静止噪声及三个旋转轴响应");
                SupportedItems.Add("DualSense 陀螺仪");
            }
            else if (dualSense) UnsupportedItems.Add("DualSense 陀螺仪：当前版本暂不支持检测");
            if (dualSense && state.Capabilities != null)
            {
                SupportedItems.Add("高级功能状态：灯条 " + (state.Capabilities.HasLightbar ? "可识别" : "未验证") + "，麦克风键 " + (state.Capabilities.HasMicrophoneButton ? "可识别" : "未验证"));
            }
            AddStep(HealthCheckStepKind.GenerateReport, "生成健康报告", string.Empty, "汇总已完成、跳过和不支持项目。", "生成并保存本地 JSON 与 Markdown 报告");
            SupportedItems.Insert(0, "按键与十字键");
            SupportedItems.Insert(1, "左右摇杆漂移、圆周与回中");
            SupportedItems.Insert(2, "LT/RT 或 L2/R2 扳机行程");
            EstimatedSeconds = 70 + (touch ? 15 : 0) + (motion ? 15 : 0) + (rumbleStatus != null && rumbleStatus.IsSupported ? 10 : 0);
        }

        private void AddStep(HealthCheckStepKind kind, string title, string category, string instruction, string completion)
        {
            HealthCheckStep step = new HealthCheckStep
            {
                Kind = kind,
                Title = title,
                Category = category,
                Instruction = instruction,
                CompletionCondition = completion
            };
            step.Record.Kind = kind.ToString();
            step.Record.Title = title;
            step.Record.Category = category;
            Steps.Add(step);
        }

        private void EnterCurrentStep(DateTime nowUtc)
        {
            CancelStepToken();
            stepCancellation = new CancellationTokenSource();
            HealthCheckStep step = CurrentStep;
            if (step == null) return;
            step.Status = HealthCheckStepStatus.Testing;
            step.Record.Status = step.Status.ToString();
            step.Record.ParticipatesInScore = false;
            step.Record.Metrics.Clear();
            step.Record.UserFeedback.Clear();
            step.Record.Summary = string.Empty;
            step.Record.Issue = string.Empty;
            step.Record.Advice = string.Empty;
            stepStartedUtc = nowUtc;
            lastSampleTimestampUtc = DateTime.MinValue;
            samples.Clear();
            CurrentDetail = "完成条件：" + step.CompletionCondition;
            StatusMessage = step.Title;
            DynamicInstruction = step.Instruction;

            switch (step.Kind)
            {
                case HealthCheckStepKind.DeviceInfo: CompleteDeviceInfo(); break;
                case HealthCheckStepKind.Buttons: InitializeButtons(false); break;
                case HealthCheckStepKind.DPad: InitializeButtons(true); break;
                case HealthCheckStepKind.LeftStickStationary:
                case HealthCheckStepKind.RightStickStationary: DynamicInstruction = "3 · 请松开摇杆，并将手柄平稳放置。"; break;
                case HealthCheckStepKind.LeftStickReturn:
                case HealthCheckStepKind.RightStickReturn:
                    returnDirectionIndex = 0;
                    currentReturnResults.Clear();
                    StartReturnDirection();
                    break;
                case HealthCheckStepKind.LeftTrigger:
                case HealthCheckStepKind.RightTrigger: DynamicInstruction = "保持扳机完全松开 0.5 秒，然后缓慢按到底并缓慢释放。"; break;
                case HealthCheckStepKind.Rumble:
                    rumblePhaseIndex = 0;
                    awaitingRumbleFeedback = false;
                    for (int i = 0; i < 3; i++) { rumbleFeedback[i] = RumbleUserFeedback.NotAnswered; rumbleSystemSuccess[i] = false; }
                    StartRumblePhase();
                    break;
                case HealthCheckStepKind.Touchpad:
                    touchMinX = touchMinY = 1;
                    touchMaxX = touchMaxY = 0;
                    touchSingle = touchDouble = touchPressed = false;
                    break;
                case HealthCheckStepKind.Gyroscope:
                    gyroBaselineSamples = 0;
                    gyroNoiseSquared = gyroMaxX = gyroMaxY = gyroMaxZ = 0;
                    DynamicInstruction = "先保持手柄静止 1.5 秒。";
                    break;
                case HealthCheckStepKind.GenerateReport: GenerateReport(); break;
            }
        }

        private void CompleteDeviceInfo()
        {
            HealthCheckStep step = CurrentStep;
            AddMetric(step, "设备类型", 0, string.Empty, sessionDeviceType.ToString());
            AddMetric(step, "连接方式", 0, string.Empty, sessionConnection);
            step.Record.Summary = sessionDeviceName + " · " + sessionDeviceType + " · " + sessionConnection;
            CompleteStep(step, 100, HealthCheckStepStatus.Passed, string.Empty, "检测会话已绑定此设备，切换设备将终止会话。", step.Record.Summary);
        }

        private void InitializeButtons(bool dpadOnly)
        {
            buttonEngine.Reset(lastState);
            buttonProgress.Clear();
            lastButtonPressed.Clear();
            simultaneousButtonsObserved = false;
            IList<ControllerButtonTestResult> definitions = buttonEngine.Results;
            for (int i = 0; i < definitions.Count; i++)
            {
                ControllerButtonTestResult item = definitions[i];
                bool isDpad = item.Id.StartsWith("dpad-", StringComparison.OrdinalIgnoreCase);
                bool isTrigger = item.Id == "l2" || item.Id == "r2";
                bool unsupportedTouchButton = item.Id == "touchpad" && (lastState.Capabilities == null || !lastState.Capabilities.HasTouchpad);
                bool unsupportedMicButton = item.Id == "mic" && (lastState.Capabilities == null || !lastState.Capabilities.HasMicrophoneButton);
                bool unsupportedGuideButton = (item.Id == "guide" || item.Id == "ps") && (lastState.Capabilities == null || !lastState.Capabilities.HasGuideButton);
                if (isDpad != dpadOnly || isTrigger || unsupportedTouchButton || unsupportedMicButton || unsupportedGuideButton) continue;
                buttonProgress[item.Id] = new HealthButtonProgress { Id = item.Id, Label = item.Label };
                lastButtonPressed[item.Id] = false;
            }
            CurrentDetail = "完成 0 / " + buttonProgress.Count + "；必须先按下再释放。";
        }

        private void UpdateButtons(ControllerState state)
        {
            buttonEngine.Update(state);
            int complete = 0;
            int pressedNow = 0;
            foreach (KeyValuePair<string, HealthButtonProgress> pair in buttonProgress)
            {
                bool pressed = ControllerInputTestEngine.IsCurrentlyPressed(pair.Key, state);
                if (pressed) pressedNow++;
                bool previous = lastButtonPressed[pair.Key];
                if (pressed) pair.Value.Pressed = true;
                if (!pressed && previous && pair.Value.Pressed) pair.Value.Released = true;
                lastButtonPressed[pair.Key] = pressed;
                if (pair.Value.Complete) complete++;
            }
            if (pressedNow >= 3) simultaneousButtonsObserved = true;
            CurrentDetail = "完成 " + complete + " / " + buttonProgress.Count + "；绿色表示完成一次按下和释放。";
            if (buttonProgress.Count > 0 && complete == buttonProgress.Count)
            {
                HealthCheckStep step = CurrentStep;
                double score = simultaneousButtonsObserved ? 90 : 100;
                AddMetric(step, "完成按键", complete, "个", complete + "/" + buttonProgress.Count);
                foreach (KeyValuePair<string, HealthButtonProgress> item in buttonProgress)
                    AddMetric(step, item.Value.Label, item.Value.Complete ? 1 : 0, string.Empty, item.Value.Complete ? "已按下并释放" : "未完成");
                CompleteStep(step, score, simultaneousButtonsObserved ? HealthCheckStepStatus.Attention : HealthCheckStepStatus.Passed,
                    simultaneousButtonsObserved ? "检测到多个不相关按键同时触发；可能是用户同时按压，也可能需要复测。" : string.Empty,
                    simultaneousButtonsObserved ? "建议逐个按键重新测试以排除串键。" : "按键均完成按下与释放。",
                    "已完成 " + complete + "/" + buttonProgress.Count + " 次完整按压");
            }
        }

        private void UpdateStationary(ControllerState state, DateTime nowUtc)
        {
            double elapsed = (nowUtc - stepStartedUtc).TotalSeconds;
            if (elapsed < 3.0)
            {
                int countdown = Math.Max(1, 3 - (int)Math.Floor(elapsed));
                DynamicInstruction = countdown + " · 请松开摇杆，并将手柄平稳放置。";
                return;
            }
            DateTime timestamp = SampleTimestamp(state, nowUtc);
            if (timestamp > lastSampleTimestampUtc)
            {
                lastSampleTimestampUtc = timestamp;
                bool left = CurrentStep.Kind == HealthCheckStepKind.LeftStickStationary;
                samples.Add(new StickSample(timestamp, left ? state.LeftStickX : state.RightStickX, left ? state.LeftStickY : state.RightStickY));
            }
            double samplingElapsed = elapsed - 3.0;
            DynamicInstruction = "静止采样 " + Math.Min(5.0, samplingElapsed).ToString("0.0", CultureInfo.InvariantCulture) + " / 5.0 秒";
            CurrentDetail = "有效采样 " + samples.Count + " 个";
            if (samplingElapsed < 5.0) return;
            bool isLeft = CurrentStep.Kind == HealthCheckStepKind.LeftStickStationary;
            JoystickStationaryResult result = JoystickAnalyzer.AnalyzeStationary(isLeft ? StickSide.Left : StickSide.Right, samples);
            DeadzoneRecommendation deadzone = JoystickAnalyzer.RecommendDeadzone(isLeft ? StickSide.Left : StickSide.Right, result);
            if (isLeft) { LeftStationary = result; LeftDeadzone = deadzone; }
            else { RightStationary = result; RightDeadzone = deadzone; }
            CompleteStationary(result, deadzone);
        }

        private void CompleteStationary(JoystickStationaryResult result, DeadzoneRecommendation deadzone)
        {
            HealthCheckStep step = CurrentStep;
            if (result == null || !result.IsValid)
            {
                CompleteStep(step, 0, HealthCheckStepStatus.Abnormal, result == null ? "无有效结果" : result.InvalidReason, "请重新进行静止采样。", "采样无效");
                return;
            }
            AddMetric(step, "中心偏移", result.CenterOffsetPercent, "%", result.CenterOffsetPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%");
            AddMetric(step, "最大偏移", result.MaximumOffsetPercent, "%", result.MaximumOffsetPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%");
            AddMetric(step, "噪声", result.NoiseLevelPercent, "%", result.NoiseLevelPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%");
            AddMetric(step, "平均 X", result.AverageXPercent, "%", result.AverageXPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%");
            AddMetric(step, "平均 Y", result.AverageYPercent, "%", result.AverageYPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%");
            AddMetric(step, "X 标准差", result.StandardDeviationXPercent, "%", result.StandardDeviationXPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%");
            AddMetric(step, "Y 标准差", result.StandardDeviationYPercent, "%", result.StandardDeviationYPercent.ToString("0.00", CultureInfo.InvariantCulture) + "%");
            AddMetric(step, "有效采样", result.SampleCount, "个", result.SampleCount.ToString(CultureInfo.InvariantCulture));
            AddMetric(step, "采样频率", result.SamplingFrequencyHz, "Hz", result.SamplingFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture) + " Hz");
            AddMetric(step, "漂移方向", 0, string.Empty, result.DriftDirection);
            AddMetric(step, "推荐死区", deadzone == null ? 0 : deadzone.RecommendedDeadzonePercent, "%", (deadzone == null ? 0 : deadzone.RecommendedDeadzonePercent).ToString("0.0", CultureInfo.InvariantCulture) + "%");
            if (deadzone != null)
            {
                AddMetric(step, "最低死区", deadzone.MinimumDeadzonePercent, "%", deadzone.MinimumDeadzonePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
                AddMetric(step, "稳定死区", deadzone.StableDeadzonePercent, "%", deadzone.StableDeadzonePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
                AddMetric(step, "轴向死区 X", deadzone.AxialDeadzoneXPercent, "%", deadzone.AxialDeadzoneXPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
                AddMetric(step, "轴向死区 Y", deadzone.AxialDeadzoneYPercent, "%", deadzone.AxialDeadzoneYPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            }
            HealthCheckStepStatus status = result.StabilityScore >= 80 ? HealthCheckStepStatus.Passed : result.StabilityScore >= 55 ? HealthCheckStepStatus.Attention : HealthCheckStepStatus.Abnormal;
            if (result.CenterOffsetPercent >= 12.0) severeSteps.Add(CurrentStep.Kind);
            string issue = result.CenterOffsetPercent <= 3 ? string.Empty : "中心偏移为 " + result.CenterOffsetPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
            string advice = deadzone == null ? "建议重新测试。" : "建议将游戏内圆形死区设置为约 " + deadzone.RecommendedDeadzonePercent.ToString("0", CultureInfo.InvariantCulture) + "%；本应用不会自动修改设置。";
            CompleteStep(step, result.StabilityScore, status, issue, advice,
                "中心偏移 " + result.CenterOffsetPercent.ToString("0.0", CultureInfo.InvariantCulture) + "% · 噪声 " + result.NoiseLevelPercent.ToString("0.0", CultureInfo.InvariantCulture) + "% · " + result.DriftDirection);
        }

        private void UpdateCircularity(ControllerState state, DateTime nowUtc)
        {
            DateTime timestamp = SampleTimestamp(state, nowUtc);
            if (timestamp <= lastSampleTimestampUtc) return;
            lastSampleTimestampUtc = timestamp;
            bool left = CurrentStep.Kind == HealthCheckStepKind.LeftStickCircularity;
            samples.Add(new StickSample(timestamp, left ? state.LeftStickX : state.RightStickX, left ? state.LeftStickY : state.RightStickY));
            CurrentDetail = "已记录 " + samples.Count + " 个样本；完成一整圈并回到中心后点击“下一步”。";
        }

        private bool CompleteCircularity(out string reason)
        {
            reason = string.Empty;
            bool left = CurrentStep.Kind == HealthCheckStepKind.LeftStickCircularity;
            StickCircularityResult result = JoystickAnalyzer.AnalyzeCircularity(left ? StickSide.Left : StickSide.Right, samples);
            if (result == null || !result.IsValid)
            {
                reason = result == null ? "没有可分析的圆周数据。" : result.InvalidReason + " 可重新测试或跳过。";
                return false;
            }
            if (left) LeftCircularity = result; else RightCircularity = result;
            double roundScore = Math.Max(0, 100.0 - result.CircularityErrorPercent * 7.0 - (result.IsSquareLimited ? 25 : 0) - (result.HasCornerCutting ? 18 : 0));
            double directionScore = Math.Max(0, 100.0 - result.DirectionalAsymmetryPercent * 5.0);
            double score = result.OuterCoveragePercent * 0.45 + roundScore * 0.35 + directionScore * 0.20;
            HealthCheckStepStatus status = score >= 80 && !result.HasMissingTravel ? HealthCheckStepStatus.Passed : score >= 55 ? HealthCheckStepStatus.Attention : HealthCheckStepStatus.Abnormal;
            if (result.OuterCoveragePercent < 50 || Math.Min(Math.Min(result.MaximumXPositivePercent, result.MaximumXNegativePercent), Math.Min(result.MaximumYPositivePercent, result.MaximumYNegativePercent)) < 70) severeSteps.Add(CurrentStep.Kind);
            AddMetric(CurrentStep, "外圈覆盖率", result.OuterCoveragePercent, "%", result.OuterCoveragePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "平均半径", result.AverageRadiusPercent, "%", result.AverageRadiusPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "圆度误差", result.CircularityErrorPercent, "%", result.CircularityErrorPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "象限覆盖率", result.QuadrantCoveragePercent, "%", result.QuadrantCoveragePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "+X 行程", result.MaximumXPositivePercent, "%", result.MaximumXPositivePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "-X 行程", result.MaximumXNegativePercent, "%", result.MaximumXNegativePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "+Y 行程", result.MaximumYPositivePercent, "%", result.MaximumYPositivePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "-Y 行程", result.MaximumYNegativePercent, "%", result.MaximumYNegativePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "最大半径", result.MaximumRadiusPercent, "%", result.MaximumRadiusPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "最小有效半径", result.MinimumEffectiveRadiusPercent, "%", result.MinimumEffectiveRadiusPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "方向不对称", result.DirectionalAsymmetryPercent, "%", result.DirectionalAsymmetryPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "角度区间", result.AngleBinCount, "个", result.AngleBinCount.ToString(CultureInfo.InvariantCulture));
            AddMetric(CurrentStep, "有效采样", result.SampleCount, "个", result.SampleCount.ToString(CultureInfo.InvariantCulture));
            AddMetric(CurrentStep, "采样频率", result.SamplingFrequencyHz, "Hz", result.SamplingFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture) + " Hz");
            AddMetric(CurrentStep, "方形限制", result.IsSquareLimited ? 1 : 0, string.Empty, result.IsSquareLimited ? "是" : "否");
            AddMetric(CurrentStep, "明显削角", result.HasCornerCutting ? 1 : 0, string.Empty, result.HasCornerCutting ? "是" : "否");
            string issue = result.MissingDirections == "无" && !result.IsSquareLimited && !result.HasCornerCutting ? string.Empty : "缺失方向：" + result.MissingDirections + (result.IsSquareLimited ? "；存在方形输入限制" : string.Empty) + (result.HasCornerCutting ? "；存在明显削角" : string.Empty);
            CompleteStep(CurrentStep, score, status, issue, issue.Length == 0 ? "圆周行程覆盖良好。" : "建议重新校准或在其他工具中复核摇杆外圈行程。",
                "覆盖 " + result.OuterCoveragePercent.ToString("0.0", CultureInfo.InvariantCulture) + "% · 圆度误差 " + result.CircularityErrorPercent.ToString("0.0", CultureInfo.InvariantCulture) + "% · 缺失 " + result.MissingDirections);
            return true;
        }

        private void StartReturnDirection()
        {
            returnPhase = ReturnPhase.Prepare;
            samples.Clear();
            lastSampleTimestampUtc = DateTime.MinValue;
            returnStableSinceUtc = DateTime.MinValue;
            StickReturnDirection direction = (StickReturnDirection)returnDirectionIndex;
            DynamicInstruction = "将摇杆推向“" + JoystickAnalyzer.DirectionLabel(direction) + "”方向最外侧。";
            CurrentDetail = "方向 " + (returnDirectionIndex + 1) + " / 4";
        }

        private void UpdateReturn(ControllerState state, DateTime nowUtc)
        {
            DateTime timestamp = SampleTimestamp(state, nowUtc);
            if (timestamp <= lastSampleTimestampUtc) return;
            lastSampleTimestampUtc = timestamp;
            bool left = CurrentStep.Kind == HealthCheckStepKind.LeftStickReturn;
            StickSample sample = new StickSample(timestamp, left ? state.LeftStickX : state.RightStickX, left ? state.LeftStickY : state.RightStickY);
            StickReturnDirection direction = (StickReturnDirection)returnDirectionIndex;
            double projection = Projection(sample, direction);
            if (returnPhase == ReturnPhase.Prepare)
            {
                if (projection >= 0.75)
                {
                    returnPhase = ReturnPhase.Hold;
                    stepStartedUtc = nowUtc;
                    samples.Clear();
                    samples.Add(sample);
                    DynamicInstruction = "保持“" + JoystickAnalyzer.DirectionLabel(direction) + "”方向 1.0 秒。";
                }
                return;
            }
            if (returnPhase == ReturnPhase.Hold)
            {
                if (projection < 0.68) { StartReturnDirection(); return; }
                samples.Add(sample);
                while (samples.Count > 20) samples.RemoveAt(0);
                if ((nowUtc - stepStartedUtc).TotalSeconds < 1.0) return;
                returnPhase = ReturnPhase.Release;
                stepStartedUtc = nowUtc;
                DynamicInstruction = "现在快速松手。";
                return;
            }
            samples.Add(sample);
            double radius = Math.Sqrt(sample.X * sample.X + sample.Y * sample.Y);
            if (radius <= JoystickAnalysisConfiguration.StableCenterThreshold)
            {
                if (returnStableSinceUtc == DateTime.MinValue) returnStableSinceUtc = nowUtc;
            }
            else returnStableSinceUtc = DateTime.MinValue;
            bool stable = returnStableSinceUtc != DateTime.MinValue && (nowUtc - returnStableSinceUtc).TotalMilliseconds >= 300;
            bool timeout = (nowUtc - stepStartedUtc).TotalSeconds >= 4;
            if (!stable && !timeout) return;
            StickReturnResult result = JoystickAnalyzer.AnalyzeReturn(left ? StickSide.Left : StickSide.Right, direction, samples);
            currentReturnResults.Add(result);
            returnDirectionIndex++;
            if (returnDirectionIndex < 4) { StartReturnDirection(); return; }
            if (left) { LeftReturnResults.Clear(); LeftReturnResults.AddRange(currentReturnResults); }
            else { RightReturnResults.Clear(); RightReturnResults.AddRange(currentReturnResults); }
            CompleteReturnStep();
        }

        private void CompleteReturnStep()
        {
            double total = 0;
            int valid = 0;
            int bounces = 0;
            double maximumDuration = 0;
            double maximumOvershoot = 0;
            bool failed = false;
            for (int i = 0; i < currentReturnResults.Count; i++)
            {
                StickReturnResult result = currentReturnResults[i];
                if (result == null || !result.IsValid) { failed = true; continue; }
                valid++;
                maximumDuration = Math.Max(maximumDuration, result.ReturnDurationMilliseconds < 0 ? 500 : result.ReturnDurationMilliseconds);
                maximumOvershoot = Math.Max(maximumOvershoot, result.OvershootPercent);
                bounces += result.BounceCount;
                string prefix = JoystickAnalyzer.DirectionLabel(result.Direction) + " · ";
                AddMetric(CurrentStep, prefix + "回中开始", result.ReturnStartTimeMilliseconds, "ms", result.ReturnStartTimeMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms");
                AddMetric(CurrentStep, prefix + "首次入中心", result.FirstCenterEntryTimeMilliseconds, "ms", result.FirstCenterEntryTimeMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms");
                AddMetric(CurrentStep, prefix + "稳定中心", result.StableCenterTimeMilliseconds, "ms", result.StableCenterTimeMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms");
                AddMetric(CurrentStep, prefix + "回中耗时", result.ReturnDurationMilliseconds, "ms", result.ReturnDurationMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms");
                AddMetric(CurrentStep, prefix + "过冲", result.OvershootPercent, "%", result.OvershootPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
                AddMetric(CurrentStep, prefix + "反弹", result.BounceCount, "次", result.BounceCount.ToString(CultureInfo.InvariantCulture));
                AddMetric(CurrentStep, prefix + "稳定耗时", result.SettlingTimeMilliseconds, "ms", result.SettlingTimeMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms");
                AddMetric(CurrentStep, prefix + "最终偏移", result.FinalOffsetPercent, "%", result.FinalOffsetPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
                total += Math.Max(0, 100 - Math.Max(0, result.ReturnDurationMilliseconds - 110) * 0.34 - result.BounceCount * 12 - result.OvershootPercent * 2 - (result.FailedToStabilize ? 35 : 0));
                if (result.FailedToStabilize) failed = true;
            }
            double score = valid == 0 ? 0 : total / valid;
            HealthCheckStepStatus status = !failed && score >= 80 ? HealthCheckStepStatus.Passed : score >= 55 ? HealthCheckStepStatus.Attention : HealthCheckStepStatus.Abnormal;
            if (failed) severeSteps.Add(CurrentStep.Kind);
            AddMetric(CurrentStep, "最慢回中", maximumDuration, "ms", maximumDuration.ToString("0", CultureInfo.InvariantCulture) + " ms");
            AddMetric(CurrentStep, "最大过冲", maximumOvershoot, "%", maximumOvershoot.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "反弹次数", bounces, "次", bounces.ToString(CultureInfo.InvariantCulture));
            CompleteStep(CurrentStep, score, status, failed ? "存在无法稳定回中的方向。" : maximumOvershoot >= 3 ? "检测到回中过冲或反弹。" : string.Empty,
                failed ? "建议复测；持续无法稳定可能是机械回中问题。" : "回中数据已记录。",
                valid + "/4 方向有效 · 最慢 " + maximumDuration.ToString("0", CultureInfo.InvariantCulture) + " ms · 反弹 " + bounces);
        }

        private void UpdateTrigger(ControllerState state, DateTime nowUtc)
        {
            DateTime timestamp = SampleTimestamp(state, nowUtc);
            if (timestamp <= lastSampleTimestampUtc) return;
            lastSampleTimestampUtc = timestamp;
            bool left = CurrentStep.Kind == HealthCheckStepKind.LeftTrigger;
            double value = left ? state.LeftTrigger : state.RightTrigger;
            samples.Add(new StickSample(timestamp, value, 0));
            double maximum = 0;
            for (int i = 0; i < samples.Count; i++) maximum = Math.Max(maximum, samples[i].X);
            if ((nowUtc - stepStartedUtc).TotalSeconds < 0.5) DynamicInstruction = "保持扳机完全松开。";
            else if (maximum < 0.85) DynamicInstruction = "缓慢按下扳机直到最底。";
            else DynamicInstruction = "缓慢释放扳机回到零点。";
            CurrentDetail = "当前 " + (value * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "% · 峰值 " + (maximum * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "% · " + samples.Count + " 点";
            if (samples.Count >= TriggerHealthAnalyzer.MinimumSamples && maximum >= 0.85 && value <= 0.05) CompleteTrigger();
        }

        private bool CompleteTrigger()
        {
            TriggerTestResult result = TriggerHealthAnalyzer.Analyze(samples);
            if (!result.IsValid) return false;
            bool left = CurrentStep.Kind == HealthCheckStepKind.LeftTrigger;
            if (left) LeftTriggerResult = result; else RightTriggerResult = result;
            double score = result.EffectiveTravelPercent * 0.45 + result.SmoothnessScore * 0.25 + (result.ReturnedToZero ? 20 : 0) + (result.ReachedFullRange ? 10 : 0);
            HealthCheckStepStatus status = score >= 85 ? HealthCheckStepStatus.Passed : score >= 60 ? HealthCheckStepStatus.Attention : HealthCheckStepStatus.Abnormal;
            if (result.MaximumValuePercent < 80 || !result.ReturnedToZero) severeSteps.Add(CurrentStep.Kind);
            AddMetric(CurrentStep, "静止值", result.RestingValuePercent, "%", result.RestingValuePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "最小值", result.MinimumValuePercent, "%", result.MinimumValuePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "最大值", result.MaximumValuePercent, "%", result.MaximumValuePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "有效行程", result.EffectiveTravelPercent, "%", result.EffectiveTravelPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "平滑度", result.SmoothnessScore, "%", result.SmoothnessScore.ToString("0", CultureInfo.InvariantCulture) + "/100");
            AddMetric(CurrentStep, "达到满量程", result.ReachedFullRange ? 1 : 0, string.Empty, result.ReachedFullRange ? "是" : "否");
            AddMetric(CurrentStep, "返回零点", result.ReturnedToZero ? 1 : 0, string.Empty, result.ReturnedToZero ? "是" : "否");
            AddMetric(CurrentStep, "明显跳变", result.JumpCount, "次", result.JumpCount.ToString(CultureInfo.InvariantCulture));
            AddMetric(CurrentStep, "有效采样", result.SampleCount, "个", result.SampleCount.ToString(CultureInfo.InvariantCulture));
            AddMetric(CurrentStep, "采样频率", result.SamplingFrequencyHz, "Hz", result.SamplingFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture) + " Hz");
            string issue = !result.ReturnedToZero ? "扳机无法稳定回到零点。" : !result.ReachedFullRange ? "最大输入仅达到 " + result.MaximumValuePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%" : result.JumpCount > 2 ? "检测到 " + result.JumpCount + " 次明显跳变。" : string.Empty;
            string advice = issue.Length == 0 ? "扳机行程与回零正常。" : "可能存在行程不足或校准问题，建议复测并检查游戏内校准。";
            CompleteStep(CurrentStep, score, status, issue, advice,
                "有效行程 " + result.EffectiveTravelPercent.ToString("0.0", CultureInfo.InvariantCulture) + "% · 起始 " + result.RestingValuePercent.ToString("0.0", CultureInfo.InvariantCulture) + "% · 平滑度 " + result.SmoothnessScore.ToString("0", CultureInfo.InvariantCulture));
            return true;
        }

        private void StartRumblePhase()
        {
            ControllerRumblePattern[] patterns = { ControllerRumblePattern.LeftOnly, ControllerRumblePattern.RightOnly, ControllerRumblePattern.Balanced };
            string[] names = { "左侧震动", "右侧震动", "双侧震动" };
            string error;
            awaitingRumbleFeedback = false;
            RumblePrompt = names[rumblePhaseIndex] + "：正在发送，请等待震动结束。";
            DynamicInstruction = RumblePrompt;
            bool started = rumble.Start(patterns[rumblePhaseIndex], 0.45, 0.45, 1.0, 1.0, out error);
            if (!started)
            {
                rumbleSystemSuccess[rumblePhaseIndex] = false;
                awaitingRumbleFeedback = true;
                RumblePrompt = names[rumblePhaseIndex] + "：系统发送失败（" + error + "），请选择实际感受或跳过。";
                DynamicInstruction = RumblePrompt;
            }
        }

        private void UpdateRumble()
        {
            if (awaitingRumbleFeedback) return;
            RumbleStatusSnapshot snapshot = rumble.GetSnapshot();
            if (snapshot.IsRunning)
            {
                CurrentDetail = "剩余 " + snapshot.RemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒";
                return;
            }
            rumbleSystemSuccess[rumblePhaseIndex] = snapshot.LastOutputSucceeded;
            awaitingRumbleFeedback = true;
            string[] names = { "左侧", "右侧", "双侧" };
            RumblePrompt = "是否感受到对应位置的" + names[rumblePhaseIndex] + "震动？";
            DynamicInstruction = RumblePrompt;
            CurrentDetail = snapshot.LastOutputSucceeded ? "系统已成功发送输出，请提供主观反馈。" : "系统未确认输出成功，请按实际情况反馈。";
        }

        private void CompleteRumbleStep()
        {
            double total = 0;
            bool abnormal = false;
            bool skipped = false;
            string[] channelNames = { "左侧", "右侧", "双侧" };
            for (int i = 0; i < 3; i++)
            {
                double userScore = RumbleFeedbackScore(rumbleFeedback[i]);
                if (!rumbleSystemSuccess[i]) userScore = Math.Min(userScore, 40);
                if (rumbleFeedback[i] == RumbleUserFeedback.NoRumble || rumbleFeedback[i] == RumbleUserFeedback.WrongPosition || !rumbleSystemSuccess[i]) abnormal = true;
                if (rumbleFeedback[i] == RumbleUserFeedback.Skipped) skipped = true;
                total += userScore;
                CurrentStep.Record.UserFeedback.Add(channelNames[i] + "：系统发送 " + (rumbleSystemSuccess[i] ? "成功" : "失败") + "；用户反馈 " + RumbleFeedbackLabel(rumbleFeedback[i]));
            }
            if (skipped)
            {
                CurrentStep.Status = HealthCheckStepStatus.Skipped;
                CurrentStep.Record.Status = CurrentStep.Status.ToString();
                CurrentStep.Record.ParticipatesInScore = false;
                CurrentStep.Record.Summary = "震动反馈未完整完成。";
                return;
            }
            double score = total / 3.0;
            CompleteStep(CurrentStep, score, abnormal ? HealthCheckStepStatus.Abnormal : score >= 85 ? HealthCheckStepStatus.Passed : HealthCheckStepStatus.Attention,
                abnormal ? "至少一个震动通道发送失败、无震动或位置不正确。" : string.Empty,
                abnormal ? "建议重新连接设备并单独复测左右震动通道。" : "三个震动通道反馈已记录。",
                "左/右/双侧反馈已完成");
        }

        private void UpdateTouchpad(ControllerState state)
        {
            if (state.DualSense == null || !state.DualSense.TouchCoordinatesAvailable)
            {
                DynamicInstruction = "当前报告不提供已解析的触摸坐标，不能判定通过。";
                return;
            }
            int active = 0;
            DualSenseTouchPoint[] points = state.DualSense.TouchPoints ?? new DualSenseTouchPoint[0];
            for (int i = 0; i < points.Length; i++)
            {
                DualSenseTouchPoint point = points[i];
                if (point == null || !point.IsActive) continue;
                active++;
                touchMinX = Math.Min(touchMinX, point.X);
                touchMaxX = Math.Max(touchMaxX, point.X);
                touchMinY = Math.Min(touchMinY, point.Y);
                touchMaxY = Math.Max(touchMaxY, point.Y);
            }
            if (active == 1) touchSingle = true;
            if (active >= 2) touchDouble = true;
            if (state.DualSense.TouchpadPressed) touchPressed = true;
            double rangeX = Math.Max(0, touchMaxX - touchMinX);
            double rangeY = Math.Max(0, touchMaxY - touchMinY);
            CurrentDetail = "单指 " + Mark(touchSingle) + " · 双指 " + Mark(touchDouble) + " · 按压 " + Mark(touchPressed) + " · 范围 X " + (rangeX * 100).ToString("0", CultureInfo.InvariantCulture) + "% / Y " + (rangeY * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            if (touchSingle && touchDouble && touchPressed && rangeX >= 0.50 && rangeY >= 0.35) CompleteTouchpad();
        }

        private bool CompleteTouchpad()
        {
            double rangeX = Math.Max(0, touchMaxX - touchMinX);
            double rangeY = Math.Max(0, touchMaxY - touchMinY);
            bool range = rangeX >= 0.50 && rangeY >= 0.35;
            double score = (touchSingle ? 25 : 0) + (touchDouble ? 25 : 0) + (touchPressed ? 25 : 0) + (range ? 25 : Math.Min(25, (rangeX + rangeY) * 20));
            HealthCheckStepStatus status = score >= 95 ? HealthCheckStepStatus.Passed : score >= 50 ? HealthCheckStepStatus.Attention : HealthCheckStepStatus.Abnormal;
            AddMetric(CurrentStep, "轨迹范围 X", rangeX * 100, "%", (rangeX * 100).ToString("0", CultureInfo.InvariantCulture) + "%");
            AddMetric(CurrentStep, "轨迹范围 Y", rangeY * 100, "%", (rangeY * 100).ToString("0", CultureInfo.InvariantCulture) + "%");
            CompleteStep(CurrentStep, score, status, score < 95 ? "触摸板部分动作尚未识别。" : string.Empty,
                score < 95 ? "请重新测试单指、双指、按压和更大范围轨迹。" : "触摸板基础输入完整。",
                "单指 " + Mark(touchSingle) + " · 双指 " + Mark(touchDouble) + " · 按压 " + Mark(touchPressed));
            return true;
        }

        private void UpdateGyroscope(ControllerState state, DateTime nowUtc)
        {
            MotionSample motion = state.DualSense == null ? null : state.DualSense.Motion;
            if (motion == null || !motion.IsValid)
            {
                DynamicInstruction = "当前报告没有可验证的陀螺仪数据，不能判定通过。";
                return;
            }
            double elapsed = (nowUtc - stepStartedUtc).TotalSeconds;
            double magnitudeSquared = motion.GyroX * motion.GyroX + motion.GyroY * motion.GyroY + motion.GyroZ * motion.GyroZ;
            if (elapsed < 1.5)
            {
                gyroBaselineSamples++;
                gyroNoiseSquared += magnitudeSquared;
                DynamicInstruction = "静止噪声采样 " + elapsed.ToString("0.0", CultureInfo.InvariantCulture) + " / 1.5 秒";
                return;
            }
            gyroMaxX = Math.Max(gyroMaxX, Math.Abs(motion.GyroX));
            gyroMaxY = Math.Max(gyroMaxY, Math.Abs(motion.GyroY));
            gyroMaxZ = Math.Max(gyroMaxZ, Math.Abs(motion.GyroZ));
            DynamicInstruction = "请左右倾斜、前后倾斜，并绕轴旋转手柄。";
            CurrentDetail = "X " + gyroMaxX.ToString("0.0", CultureInfo.InvariantCulture) + "°/s · Y " + gyroMaxY.ToString("0.0", CultureInfo.InvariantCulture) + "°/s · Z " + gyroMaxZ.ToString("0.0", CultureInfo.InvariantCulture) + "°/s";
            if (gyroBaselineSamples >= 20 && gyroMaxX >= 15 && gyroMaxY >= 15 && gyroMaxZ >= 15) CompleteGyroscope();
        }

        private bool CompleteGyroscope()
        {
            if (gyroBaselineSamples <= 0) return false;
            double noise = Math.Sqrt(gyroNoiseSquared / gyroBaselineSamples);
            bool x = gyroMaxX >= 15;
            bool y = gyroMaxY >= 15;
            bool z = gyroMaxZ >= 15;
            double responseScore = (x ? 25 : 0) + (y ? 25 : 0) + (z ? 25 : 0);
            double noiseScore = Math.Max(0, Math.Min(25, 25 - Math.Max(0, noise - 1.5) * 3));
            double score = responseScore + noiseScore;
            HealthCheckStepStatus status = score >= 90 ? HealthCheckStepStatus.Passed : score >= 55 ? HealthCheckStepStatus.Attention : HealthCheckStepStatus.Abnormal;
            AddMetric(CurrentStep, "静止噪声", noise, "°/s", noise.ToString("0.00", CultureInfo.InvariantCulture) + " °/s RMS");
            AddMetric(CurrentStep, "X 轴峰值", gyroMaxX, "°/s", gyroMaxX.ToString("0.0", CultureInfo.InvariantCulture) + " °/s");
            AddMetric(CurrentStep, "Y 轴峰值", gyroMaxY, "°/s", gyroMaxY.ToString("0.0", CultureInfo.InvariantCulture) + " °/s");
            AddMetric(CurrentStep, "Z 轴峰值", gyroMaxZ, "°/s", gyroMaxZ.ToString("0.0", CultureInfo.InvariantCulture) + " °/s");
            CompleteStep(CurrentStep, score, status, !(x && y && z) ? "至少一个旋转轴未达到响应阈值。" : noise > 4 ? "静止噪声偏高。" : string.Empty,
                status == HealthCheckStepStatus.Passed ? "三轴响应和静止噪声已记录。" : "请平稳放置后重新测试，并确认完成三个方向动作。",
                "静止噪声 " + noise.ToString("0.00", CultureInfo.InvariantCulture) + " °/s · 三轴 " + Mark(x && y && z));
            return true;
        }

        private bool TryCompleteManualStep(HealthCheckStep step, out string reason)
        {
            reason = string.Empty;
            switch (step.Kind)
            {
                case HealthCheckStepKind.LeftStickCircularity:
                case HealthCheckStepKind.RightStickCircularity: return CompleteCircularity(out reason);
                case HealthCheckStepKind.LeftTrigger:
                case HealthCheckStepKind.RightTrigger:
                    if (CompleteTrigger()) return true;
                    reason = "扳机采样不足，请完成松开、按到底和释放，或选择跳过。";
                    return false;
                case HealthCheckStepKind.Touchpad:
                    if (CompleteTouchpad()) return true;
                    reason = "尚无足够触摸板数据。";
                    return false;
                case HealthCheckStepKind.Gyroscope:
                    if (CompleteGyroscope()) return true;
                    reason = "陀螺仪静止采样或动作响应不足。";
                    return false;
                default:
                    reason = "当前步骤尚未达到完成条件。";
                    return false;
            }
        }

        private void CompleteStep(HealthCheckStep step, double score, HealthCheckStepStatus status, string issue, string advice, string summary)
        {
            if (step == null) return;
            step.Status = status;
            step.Record.Status = status.ToString();
            step.Record.Score = Math.Round(Math.Max(0, Math.Min(100, score)), 1);
            step.Record.ParticipatesInScore = !string.IsNullOrEmpty(step.Category) && status != HealthCheckStepStatus.Skipped && status != HealthCheckStepStatus.Unsupported;
            step.Record.Issue = issue ?? string.Empty;
            step.Record.Advice = advice ?? string.Empty;
            step.Record.Summary = summary ?? string.Empty;
            StatusMessage = step.Title + " · " + StatusChinese(status);
            DynamicInstruction = "当前步骤已完成，可以下一步或重新测试。";
            CurrentDetail = step.Record.Summary;
            CancelStepToken();
        }

        private void GenerateReport()
        {
            ControllerHealthReport report = new ControllerHealthReport
            {
                ReportId = Guid.NewGuid().ToString("N"),
                DeviceId = sessionDeviceId,
                DeviceName = sessionDeviceName,
                DeviceType = sessionDeviceType.ToString(),
                ConnectionType = sessionConnection,
                TestDateUtc = DateTime.UtcNow,
                TestDurationSeconds = Math.Max(0, (DateTime.UtcNow - sessionStartedUtc).TotalSeconds),
                SevereHardwareIssue = severeSteps.Count > 0
            };
            report.SupportedItems.AddRange(SupportedItems);
            report.UnsupportedItems.AddRange(UnsupportedItems);
            for (int i = 0; i < Steps.Count; i++)
            {
                HealthCheckStep step = Steps[i];
                if (step.Kind == HealthCheckStepKind.GenerateReport)
                {
                    step.Status = HealthCheckStepStatus.Passed;
                    step.Record.Status = step.Status.ToString();
                    step.Record.Summary = store.SaveEnabled ? "报告已在本地生成。" : "报告已生成；按当前设置未写入历史记录。";
                }
                report.Steps.Add(step.Record);
            }
            for (int i = 0; i < 3; i++) if (rumbleFeedback[i] != RumbleUserFeedback.NotAnswered) report.UserRumbleFeedback.Add(RumbleFeedbackLabel(rumbleFeedback[i]));
            ControllerHealthScoring.Score(report);
            Report = report;
            try
            {
                SavedReportPath = store.Save(report);
            }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is SerializationException)) throw;
                SavedReportPath = null;
                report.Steps.Add(new HealthCheckStepRecord
                {
                    Kind = "ReportStorage",
                    Title = "本地报告保存",
                    Status = HealthCheckStepStatus.Attention.ToString(),
                    Summary = "报告已在内存中生成，但本地保存失败。",
                    Issue = ex.Message,
                    Advice = "检查应用数据目录权限后重试导出。",
                    ParticipatesInScore = false
                });
            }
            IsRunning = false;
            CancelStepToken();
            StatusMessage = SavedReportPath == null ? "健康报告已生成，但本地保存失败" : string.IsNullOrEmpty(SavedReportPath) ? "健康报告已生成；按设置未保存历史" : "健康报告已生成并保存到本地数据目录";
            DynamicInstruction = ControllerLabStatusLabels.Overall(report.OverallStatus) + " · " + report.OverallScore.ToString("0", CultureInfo.InvariantCulture) + "/100";
            CurrentDetail = SavedReportPath == null ? "报告仍可在当前页面查看和导出。" : string.IsNullOrEmpty(SavedReportPath) ? "可在当前结果页查看或手动导出。" : "报告已保存到本地数据目录，可在设置页打开数据目录。";
        }

        private void HandleDisconnect(string message)
        {
            if (IsDisconnected) return;
            rumble.Stop("设备已断开，完整检测震动已停止");
            CancelStepToken();
            IsDisconnected = true;
            CanResumeCurrentStep = false;
            HealthCheckStep step = CurrentStep;
            if (step != null && step.Status == HealthCheckStepStatus.Testing)
            {
                step.Status = HealthCheckStepStatus.Waiting;
                step.Record.Status = step.Status.ToString();
                step.Record.Summary = "设备断开，当前步骤未完成；已完成步骤仍保留在内存中。";
            }
            StatusMessage = "设备已断开";
            DynamicInstruction = message;
        }

        private void StopCurrentActivity(string reason)
        {
            rumble.Stop(reason);
            awaitingRumbleFeedback = false;
            CancelStepToken();
        }

        private void ResetStep(HealthCheckStep step)
        {
            StopCurrentActivity("当前步骤正在重新测试");
            step.Status = HealthCheckStepStatus.Waiting;
            step.Record.Status = step.Status.ToString();
            step.Record.Score = 0;
            step.Record.ParticipatesInScore = false;
            step.Record.Summary = string.Empty;
            step.Record.Issue = string.Empty;
            step.Record.Advice = string.Empty;
            step.Record.Metrics.Clear();
            step.Record.UserFeedback.Clear();
            severeSteps.Remove(step.Kind);
            if (step.Kind == HealthCheckStepKind.LeftStickReturn) LeftReturnResults.Clear();
            if (step.Kind == HealthCheckStepKind.RightStickReturn) RightReturnResults.Clear();
        }

        private void ResetResults()
        {
            Report = null;
            SavedReportPath = null;
            severeSteps.Clear();
            LeftStationary = RightStationary = null;
            LeftDeadzone = RightDeadzone = null;
            LeftCircularity = RightCircularity = null;
            LeftReturnResults.Clear();
            RightReturnResults.Clear();
            LeftTriggerResult = RightTriggerResult = null;
            for (int i = 0; i < Steps.Count; i++) ResetStep(Steps[i]);
        }

        private void ResetPreparedState()
        {
            Steps.Clear();
            SupportedItems.Clear();
            UnsupportedItems.Clear();
            IsPrepared = false;
            currentIndex = -1;
        }

        private void CancelStepToken()
        {
            CancellationTokenSource old = stepCancellation;
            stepCancellation = null;
            if (old == null) return;
            try { old.Cancel(); }
            finally { old.Dispose(); }
        }

        private static void AddMetric(HealthCheckStep step, string name, double value, string unit, string display)
        {
            step.Record.Metrics.Add(new HealthReportMetric { Name = name, Value = value, Unit = unit, DisplayValue = display });
        }

        private static DateTime SampleTimestamp(ControllerState state, DateTime fallback)
        {
            return state != null && state.TimestampUtc != DateTime.MinValue ? state.TimestampUtc : fallback;
        }

        private static double Projection(StickSample sample, StickReturnDirection direction)
        {
            if (direction == StickReturnDirection.Up) return sample.Y;
            if (direction == StickReturnDirection.Down) return -sample.Y;
            if (direction == StickReturnDirection.Left) return -sample.X;
            return sample.X;
        }

        private static bool IsResolved(HealthCheckStepStatus status)
        {
            return status == HealthCheckStepStatus.Passed || status == HealthCheckStepStatus.Attention || status == HealthCheckStepStatus.Abnormal || status == HealthCheckStepStatus.Skipped || status == HealthCheckStepStatus.Unsupported;
        }

        public static string StatusChinese(HealthCheckStepStatus status)
        {
            switch (status)
            {
                case HealthCheckStepStatus.Testing: return "检测中";
                case HealthCheckStepStatus.Passed: return "已通过";
                case HealthCheckStepStatus.Attention: return "需要注意";
                case HealthCheckStepStatus.Abnormal: return "严重异常";
                case HealthCheckStepStatus.Skipped: return "已跳过";
                case HealthCheckStepStatus.Unsupported: return "不支持";
                default: return "等待";
            }
        }

        private static string RumbleFeedbackLabel(RumbleUserFeedback feedback)
        {
            switch (feedback)
            {
                case RumbleUserFeedback.Normal: return "正常";
                case RumbleUserFeedback.Weak: return "很弱";
                case RumbleUserFeedback.NoRumble: return "无震动";
                case RumbleUserFeedback.WrongPosition: return "位置不正确";
                case RumbleUserFeedback.Skipped: return "跳过";
                default: return "未反馈";
            }
        }

        private static double RumbleFeedbackScore(RumbleUserFeedback feedback)
        {
            if (feedback == RumbleUserFeedback.Normal) return 100;
            if (feedback == RumbleUserFeedback.Weak) return 65;
            if (feedback == RumbleUserFeedback.WrongPosition) return 20;
            if (feedback == RumbleUserFeedback.NoRumble) return 0;
            return 0;
        }

        private static string Mark(bool value) { return value ? "✓" : "待完成"; }

        private static string FormatDuration(int seconds)
        {
            if (seconds < 60) return seconds + " 秒";
            return (seconds / 60) + " 分 " + (seconds % 60) + " 秒";
        }
    }
}
