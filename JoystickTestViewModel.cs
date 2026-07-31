using System;
using System.Collections.Generic;
using System.Threading;

namespace ControllerLab
{
    // Owns sampling and test state only. It has no WPF dependency and never reads HID/XInput directly.
    public sealed class JoystickTestViewModel : IDisposable
    {
        private enum ReturnPhase
        {
            Prepare,
            Hold,
            Release
        }

        private readonly List<StickSample> stationaryLeftSamples = new List<StickSample>();
        private readonly List<StickSample> stationaryRightSamples = new List<StickSample>();
        private readonly List<StickSample> circularSamples = new List<StickSample>();
        private readonly List<StickSample> returnSamples = new List<StickSample>();
        private CancellationTokenSource cancellation;
        private DateTime phaseStartedUtc;
        private DateTime lastLeftTimestampUtc;
        private DateTime lastRightTimestampUtc;
        private DateTime stableCenterSinceUtc;
        private string activeDeviceId = string.Empty;
        private ReturnPhase returnPhase;
        private int returnSequenceIndex;
        private ControllerState lastState;

        public readonly StickTestResult LeftResult = new StickTestResult { StickSide = StickSide.Left };
        public readonly StickTestResult RightResult = new StickTestResult { StickSide = StickSide.Right };
        public readonly List<StickSample> LeftTrace = new List<StickSample>();
        public readonly List<StickSample> RightTrace = new List<StickSample>();

        public JoystickTestMode Mode { get; private set; }
        public string StatusMessage { get; private set; }
        public string Instruction { get; private set; }
        public int CountdownValue { get; private set; }
        public int CurrentSampleCount { get; private set; }
        public double CurrentSamplingFrequencyHz { get; private set; }
        public StickSide ActiveStickSide { get; private set; }
        public StickReturnDirection ActiveReturnDirection { get; private set; }
        public bool IsTestActive { get { return Mode != JoystickTestMode.Idle && Mode != JoystickTestMode.Cancelled; } }
        public bool IsCircularityActive { get { return Mode == JoystickTestMode.CircularityLeft || Mode == JoystickTestMode.CircularityRight; } }
        public double StationarySampleDurationSeconds { get; set; }
        public double DeadzoneSafetyMarginPercent { get; set; }

        public JoystickTestViewModel()
        {
            Mode = JoystickTestMode.Idle;
            StationarySampleDurationSeconds = 5.0;
            DeadzoneSafetyMarginPercent = 0.5;
            StatusMessage = "等待开始检测";
            Instruction = "请选择静止漂移、圆周测试或回中测试。";
        }

        public bool StartStationary(ControllerState state, bool rumbleRunning, DateTime lastRumbleStoppedUtc, out string reason)
        {
            if (!CanStart(state, rumbleRunning, lastRumbleStoppedUtc, out reason)) return false;
            BeginSession(state, JoystickTestMode.StationaryCountdown);
            stationaryLeftSamples.Clear();
            stationaryRightSamples.Clear();
            LeftTrace.Clear();
            RightTrace.Clear();
            LeftResult.Stationary = null;
            RightResult.Stationary = null;
            LeftResult.Deadzone = null;
            RightResult.Deadzone = null;
            CountdownValue = 3;
            StatusMessage = "静止漂移检测准备中";
            Instruction = "请松开左右摇杆，并将手柄平稳放置。";
            return true;
        }

        public bool StartCircularity(ControllerState state, bool rumbleRunning, DateTime lastRumbleStoppedUtc, out string reason)
        {
            if (!CanStart(state, rumbleRunning, lastRumbleStoppedUtc, out reason)) return false;
            BeginSession(state, JoystickTestMode.CircularityLeft);
            circularSamples.Clear();
            LeftTrace.Clear();
            RightTrace.Clear();
            LeftResult.Circularity = null;
            RightResult.Circularity = null;
            ActiveStickSide = StickSide.Left;
            StatusMessage = "左摇杆圆周测试";
            Instruction = "将左摇杆推到最外圈，沿边缘缓慢旋转一整圈，然后返回中心并点击“完成当前摇杆”。";
            return true;
        }

        public bool CompleteCircularityStep(out string reason)
        {
            reason = string.Empty;
            if (!IsCircularityActive)
            {
                reason = "当前没有正在进行的圆周测试。";
                return false;
            }
            StickCircularityResult result = JoystickAnalyzer.AnalyzeCircularity(ActiveStickSide, circularSamples);
            if (ActiveStickSide == StickSide.Left) LeftResult.Circularity = result;
            else RightResult.Circularity = result;
            circularSamples.Clear();
            phaseStartedUtc = DateTime.UtcNow;
            lastLeftTimestampUtc = DateTime.MinValue;
            lastRightTimestampUtc = DateTime.MinValue;
            if (Mode == JoystickTestMode.CircularityLeft)
            {
                Mode = JoystickTestMode.CircularityRight;
                ActiveStickSide = StickSide.Right;
                StatusMessage = result.IsValid ? "左摇杆完成，开始右摇杆" : "左摇杆数据不足，仍可继续右摇杆";
                Instruction = "将右摇杆推到最外圈，沿边缘缓慢旋转一整圈，然后返回中心并点击“完成当前摇杆”。";
                return true;
            }
            FinishSession("左右摇杆圆周测试已完成");
            RecalculateHealth();
            return true;
        }

        public bool StartReturnTest(ControllerState state, bool rumbleRunning, DateTime lastRumbleStoppedUtc, out string reason)
        {
            if (!CanStart(state, rumbleRunning, lastRumbleStoppedUtc, out reason)) return false;
            BeginSession(state, JoystickTestMode.ReturnTest);
            LeftResult.ReturnResults.Clear();
            RightResult.ReturnResults.Clear();
            LeftTrace.Clear();
            RightTrace.Clear();
            returnSequenceIndex = 0;
            StartReturnDirection();
            return true;
        }

        public void Update(ControllerState state, DateTime nowUtc)
        {
            lastState = state;
            if (!IsTestActive) return;
            if (cancellation == null || cancellation.IsCancellationRequested)
            {
                Cancel("检测已取消");
                return;
            }
            if (state == null || !state.IsConnected || !state.HasRealInput)
            {
                Cancel("设备已断开，摇杆检测已取消");
                return;
            }
            if (!string.Equals(activeDeviceId, state.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                Cancel("检测期间设备发生切换，摇杆检测已取消");
                return;
            }

            if (Mode == JoystickTestMode.StationaryCountdown) UpdateStationaryCountdown(nowUtc);
            else if (Mode == JoystickTestMode.StationarySampling) UpdateStationarySampling(state, nowUtc);
            else if (Mode == JoystickTestMode.CircularityLeft || Mode == JoystickTestMode.CircularityRight) UpdateCircularity(state, nowUtc);
            else if (Mode == JoystickTestMode.ReturnTest) UpdateReturn(state, nowUtc);
        }

        public void Cancel(string reason)
        {
            CancelSource();
            Mode = JoystickTestMode.Cancelled;
            StatusMessage = string.IsNullOrEmpty(reason) ? "检测已取消" : reason;
            Instruction = "可以重新选择检测项目。";
            CountdownValue = 0;
            CurrentSampleCount = 0;
            CurrentSamplingFrequencyHz = 0;
        }

        public void ClearAll()
        {
            CancelSource();
            LeftResult.Stationary = null;
            LeftResult.Circularity = null;
            LeftResult.Deadzone = null;
            LeftResult.ReturnResults.Clear();
            LeftResult.Health = new StickHealthScoreResult();
            RightResult.Stationary = null;
            RightResult.Circularity = null;
            RightResult.Deadzone = null;
            RightResult.ReturnResults.Clear();
            RightResult.Health = new StickHealthScoreResult();
            stationaryLeftSamples.Clear();
            stationaryRightSamples.Clear();
            circularSamples.Clear();
            returnSamples.Clear();
            LeftTrace.Clear();
            RightTrace.Clear();
            Mode = JoystickTestMode.Idle;
            StatusMessage = "检测数据已清除";
            Instruction = "请选择静止漂移、圆周测试或回中测试。";
            CountdownValue = 0;
            CurrentSampleCount = 0;
            CurrentSamplingFrequencyHz = 0;
        }

        public void ResetForDeviceChange(string reason)
        {
            if (IsTestActive) Cancel(reason);
            activeDeviceId = string.Empty;
            lastState = null;
        }

        public void ReportBlocked(string reason)
        {
            if (IsTestActive) return;
            StatusMessage = string.IsNullOrEmpty(reason) ? "当前无法开始检测" : reason;
            Instruction = "请处理提示后重试。";
        }

        public void Dispose()
        {
            CancelSource();
        }

        private bool CanStart(ControllerState state, bool rumbleRunning, DateTime lastRumbleStoppedUtc, out string reason)
        {
            reason = string.Empty;
            if (IsTestActive)
            {
                reason = "同一时间只能执行一种摇杆检测。";
                return false;
            }
            if (state == null || !state.IsConnected || !state.HasRealInput)
            {
                reason = "专业检测需要真实 Xbox XInput 或 DualSense HID 输入。";
                return false;
            }
            if (rumbleRunning)
            {
                reason = "震动正在运行，请先停止震动。";
                return false;
            }
            if (lastRumbleStoppedUtc != DateTime.MinValue)
            {
                double remaining = 1.0 - (DateTime.UtcNow - lastRumbleStoppedUtc).TotalSeconds;
                if (remaining > 0)
                {
                    reason = "震动刚刚停止，请等待 " + remaining.ToString("0.0") + " 秒后再开始检测。";
                    return false;
                }
            }
            return true;
        }

        private void BeginSession(ControllerState state, JoystickTestMode mode)
        {
            CancelSource();
            cancellation = new CancellationTokenSource();
            Mode = mode;
            activeDeviceId = state.DeviceId ?? string.Empty;
            phaseStartedUtc = DateTime.UtcNow;
            lastLeftTimestampUtc = DateTime.MinValue;
            lastRightTimestampUtc = DateTime.MinValue;
            stableCenterSinceUtc = DateTime.MinValue;
            CurrentSampleCount = 0;
            CurrentSamplingFrequencyHz = 0;
        }

        private void FinishSession(string status)
        {
            CancelSource();
            Mode = JoystickTestMode.Idle;
            StatusMessage = status;
            Instruction = "检测结果已生成；详细数字可在下方展开查看。";
            CountdownValue = 0;
        }

        private void UpdateStationaryCountdown(DateTime nowUtc)
        {
            double elapsed = (nowUtc - phaseStartedUtc).TotalSeconds;
            CountdownValue = Math.Max(1, 3 - (int)Math.Floor(elapsed));
            StatusMessage = "静止漂移倒计时 " + CountdownValue;
            if (elapsed < 3.0) return;
            Mode = JoystickTestMode.StationarySampling;
            phaseStartedUtc = nowUtc;
            CountdownValue = 0;
            StatusMessage = "正在采样静止漂移";
            Instruction = "请继续松开左右摇杆，并保持手柄平稳。";
        }

        private void UpdateStationarySampling(ControllerState state, DateTime nowUtc)
        {
            AddBothSamples(state, stationaryLeftSamples, stationaryRightSamples, true);
            CurrentSampleCount = Math.Min(stationaryLeftSamples.Count, stationaryRightSamples.Count);
            CurrentSamplingFrequencyHz = Math.Min(Frequency(stationaryLeftSamples), Frequency(stationaryRightSamples));
            double duration = Math.Max(3.0, Math.Min(10.0, StationarySampleDurationSeconds));
            StatusMessage = "静止采样 " + Math.Min(duration, (nowUtc - phaseStartedUtc).TotalSeconds).ToString("0.0") + " / " + duration.ToString("0.0") + " 秒";
            if ((nowUtc - phaseStartedUtc).TotalSeconds < duration) return;
            LeftResult.Stationary = JoystickAnalyzer.AnalyzeStationary(StickSide.Left, stationaryLeftSamples);
            RightResult.Stationary = JoystickAnalyzer.AnalyzeStationary(StickSide.Right, stationaryRightSamples);
            LeftResult.Deadzone = JoystickAnalyzer.RecommendDeadzone(StickSide.Left, LeftResult.Stationary, DeadzoneSafetyMarginPercent);
            RightResult.Deadzone = JoystickAnalyzer.RecommendDeadzone(StickSide.Right, RightResult.Stationary, DeadzoneSafetyMarginPercent);
            RecalculateHealth();
            FinishSession(LeftResult.Stationary.IsValid && RightResult.Stationary.IsValid ? "静止漂移检测已完成" : "采样不足，未生成正常结论");
        }

        private void UpdateCircularity(ControllerState state, DateTime nowUtc)
        {
            DateTime timestamp = SampleTimestamp(state, nowUtc);
            if (ActiveStickSide == StickSide.Left)
            {
                if (timestamp <= lastLeftTimestampUtc) return;
                lastLeftTimestampUtc = timestamp;
                StickSample sample = new StickSample(timestamp, state.LeftStickX, state.LeftStickY);
                circularSamples.Add(sample);
                AppendTrace(LeftTrace, sample);
            }
            else
            {
                if (timestamp <= lastRightTimestampUtc) return;
                lastRightTimestampUtc = timestamp;
                StickSample sample = new StickSample(timestamp, state.RightStickX, state.RightStickY);
                circularSamples.Add(sample);
                AppendTrace(RightTrace, sample);
            }
            CurrentSampleCount = circularSamples.Count;
            CurrentSamplingFrequencyHz = Frequency(circularSamples);
        }

        private void UpdateReturn(ControllerState state, DateTime nowUtc)
        {
            DateTime timestamp = SampleTimestamp(state, nowUtc);
            if (ActiveStickSide == StickSide.Left)
            {
                if (timestamp <= lastLeftTimestampUtc) return;
                lastLeftTimestampUtc = timestamp;
            }
            else
            {
                if (timestamp <= lastRightTimestampUtc) return;
                lastRightTimestampUtc = timestamp;
            }
            StickSample sample = ActiveStickSide == StickSide.Left
                ? new StickSample(timestamp, state.LeftStickX, state.LeftStickY)
                : new StickSample(timestamp, state.RightStickX, state.RightStickY);
            double projection = Projection(sample, ActiveReturnDirection);

            if (returnPhase == ReturnPhase.Prepare)
            {
                if (projection >= 0.75)
                {
                    returnPhase = ReturnPhase.Hold;
                    phaseStartedUtc = nowUtc;
                    returnSamples.Clear();
                    returnSamples.Add(sample);
                    StatusMessage = "保持指定方向 1.0 秒";
                    Instruction = BuildReturnInstruction("保持");
                }
                return;
            }

            if (returnPhase == ReturnPhase.Hold)
            {
                if (projection < 0.68)
                {
                    returnPhase = ReturnPhase.Prepare;
                    returnSamples.Clear();
                    StatusMessage = "行程不足，请重新推到指定方向";
                    Instruction = BuildReturnInstruction("推向");
                    return;
                }
                returnSamples.Add(sample);
                while (returnSamples.Count > 20) returnSamples.RemoveAt(0);
                double remaining = 1.0 - (nowUtc - phaseStartedUtc).TotalSeconds;
                StatusMessage = "保持 " + Math.Max(0.0, remaining).ToString("0.0") + " 秒";
                if (remaining > 0) return;
                returnPhase = ReturnPhase.Release;
                phaseStartedUtc = nowUtc;
                stableCenterSinceUtc = DateTime.MinValue;
                StatusMessage = "现在快速松手";
                Instruction = "快速松开摇杆，系统正在检测开始回中、首次进圈和最终稳定时间。";
                return;
            }

            returnSamples.Add(sample);
            AppendTrace(ActiveStickSide == StickSide.Left ? LeftTrace : RightTrace, sample);
            CurrentSampleCount = returnSamples.Count;
            CurrentSamplingFrequencyHz = Frequency(returnSamples);
            double radius = Math.Sqrt(sample.X * sample.X + sample.Y * sample.Y);
            if (radius <= JoystickAnalysisConfiguration.StableCenterThreshold)
            {
                if (stableCenterSinceUtc == DateTime.MinValue) stableCenterSinceUtc = nowUtc;
            }
            else stableCenterSinceUtc = DateTime.MinValue;
            bool stable = stableCenterSinceUtc != DateTime.MinValue && (nowUtc - stableCenterSinceUtc).TotalMilliseconds >= 300.0;
            bool timedOut = (nowUtc - phaseStartedUtc).TotalSeconds >= 4.0;
            if (stable || timedOut) CompleteReturnDirection();
        }

        private void StartReturnDirection()
        {
            ActiveStickSide = returnSequenceIndex < 4 ? StickSide.Left : StickSide.Right;
            ActiveReturnDirection = (StickReturnDirection)(returnSequenceIndex % 4);
            returnPhase = ReturnPhase.Prepare;
            returnSamples.Clear();
            stableCenterSinceUtc = DateTime.MinValue;
            lastLeftTimestampUtc = DateTime.MinValue;
            lastRightTimestampUtc = DateTime.MinValue;
            StatusMessage = (ActiveStickSide == StickSide.Left ? "左摇杆" : "右摇杆") + " · " + JoystickAnalyzer.DirectionLabel(ActiveReturnDirection) + "方向回中";
            Instruction = BuildReturnInstruction("推向");
        }

        private void CompleteReturnDirection()
        {
            StickReturnResult result = JoystickAnalyzer.AnalyzeReturn(ActiveStickSide, ActiveReturnDirection, returnSamples);
            if (ActiveStickSide == StickSide.Left) LeftResult.ReturnResults.Add(result);
            else RightResult.ReturnResults.Add(result);
            returnSequenceIndex++;
            if (returnSequenceIndex >= 8)
            {
                RecalculateHealth();
                FinishSession("左右摇杆四方向回中测试已完成");
                return;
            }
            StartReturnDirection();
        }

        private string BuildReturnInstruction(string action)
        {
            return action + (ActiveStickSide == StickSide.Left ? "左摇杆" : "右摇杆") + "到“" + JoystickAnalyzer.DirectionLabel(ActiveReturnDirection) + "”方向最外侧。";
        }

        private void RecalculateHealth()
        {
            LeftResult.Health = JoystickAnalyzer.CalculateHealth(LeftResult);
            RightResult.Health = JoystickAnalyzer.CalculateHealth(RightResult);
        }

        private void AddBothSamples(ControllerState state, List<StickSample> left, List<StickSample> right, bool trace)
        {
            DateTime timestamp = SampleTimestamp(state, DateTime.UtcNow);
            if (timestamp > lastLeftTimestampUtc)
            {
                lastLeftTimestampUtc = timestamp;
                StickSample sample = new StickSample(timestamp, state.LeftStickX, state.LeftStickY);
                left.Add(sample);
                if (trace) AppendTrace(LeftTrace, sample);
            }
            if (timestamp > lastRightTimestampUtc)
            {
                lastRightTimestampUtc = timestamp;
                StickSample sample = new StickSample(timestamp, state.RightStickX, state.RightStickY);
                right.Add(sample);
                if (trace) AppendTrace(RightTrace, sample);
            }
        }

        private static void AppendTrace(List<StickSample> trace, StickSample sample)
        {
            trace.Add(sample);
            if (trace.Count > 1800) trace.RemoveRange(0, trace.Count - 1800);
        }

        private static DateTime SampleTimestamp(ControllerState state, DateTime fallback)
        {
            return state != null && state.TimestampUtc != DateTime.MinValue ? state.TimestampUtc : fallback;
        }

        private static double Frequency(List<StickSample> samples)
        {
            if (samples == null || samples.Count < 2) return 0;
            double seconds = (samples[samples.Count - 1].Timestamp - samples[0].Timestamp).TotalSeconds;
            return seconds <= 0 ? 0 : (samples.Count - 1) / seconds;
        }

        private static double Projection(StickSample sample, StickReturnDirection direction)
        {
            if (direction == StickReturnDirection.Up) return sample.Y;
            if (direction == StickReturnDirection.Down) return -sample.Y;
            if (direction == StickReturnDirection.Left) return -sample.X;
            return sample.X;
        }

        private void CancelSource()
        {
            CancellationTokenSource source = cancellation;
            cancellation = null;
            if (source == null) return;
            try { source.Cancel(); }
            finally { source.Dispose(); }
        }
    }
}
