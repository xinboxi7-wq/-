using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace ControllerLab
{
    public enum TouchContactPhase
    {
        None,
        Started,
        Moved,
        Stationary,
        Ended
    }

    public enum TouchpadTestStage
    {
        NotStarted,
        LeftTopToRightBottom,
        RightTopToLeftBottom,
        EdgeTrace,
        TwoFinger,
        Press,
        Completed,
        Cancelled
    }

    public sealed class TouchPointState
    {
        public int ContactId;
        public bool IsActive;
        public double X;
        public double Y;
        public double Speed;
        public TouchContactPhase Phase;
        public DateTime TimestampUtc;

        public TouchPointState Copy()
        {
            return (TouchPointState)MemberwiseClone();
        }
    }

    public sealed class TouchTrailPoint
    {
        public int ContactId;
        public double X;
        public double Y;
        public DateTime TimestampUtc;
        public bool StartsStroke;

        public TouchTrailPoint Copy()
        {
            return (TouchTrailPoint)MemberwiseClone();
        }
    }

    public sealed class TouchpadAnalysisSnapshot
    {
        public const int GridColumns = 24;
        public const int GridRows = 12;
        public bool IsAvailable;
        public bool IsPressed;
        public bool IsPaused;
        public bool IsRecording;
        public double RecordingSecondsRemaining;
        public int ActiveContactCount;
        public int MaximumSimultaneousContacts;
        public int SampleCount;
        public double CoveragePercent;
        public double EdgeCoveragePercent;
        public bool TwoFingerRecognized;
        public bool PressRecognized;
        public string AvailabilityMessage = string.Empty;
        public TouchpadTestStage TestStage;
        public string Guidance = string.Empty;
        public string TestSummary = string.Empty;
        public TouchPointState[] Points = new TouchPointState[0];
        public TouchTrailPoint[] Trail = new TouchTrailPoint[0];
        public bool[] CoveredCells = new bool[GridColumns * GridRows];
    }

    public sealed class DualSenseTouchpadAnalyzer
    {
        private const int TrailCapacity = 1600;
        private readonly object sync = new object();
        private readonly Dictionary<int, TouchPointState> active = new Dictionary<int, TouchPointState>();
        private readonly List<TouchTrailPoint> trail = new List<TouchTrailPoint>(TrailCapacity);
        private readonly bool[] covered = new bool[TouchpadAnalysisSnapshot.GridColumns * TouchpadAnalysisSnapshot.GridRows];
        private long lastReportSequence = -1;
        private DateTime recordingEndsUtc = DateTime.MinValue;
        private bool paused;
        private bool pressed;
        private bool isAvailable;
        private bool twoFingerRecognized;
        private bool pressRecognized;
        private int sampleCount;
        private int maximumSimultaneousContacts;
        private TouchpadTestStage testStage = TouchpadTestStage.NotStarted;
        private bool stageStartSeen;

        public void Update(ControllerState controller, DateTime now)
        {
            lock (sync)
            {
                if (!IsNativeTouch(controller))
                {
                    EndAll(now);
                    pressed = false;
                    isAvailable = false;
                    return;
                }
                isAvailable = true;
                InputSnapshot source = controller.SourceSnapshot;
                long sequence = source == null ? controller.TimestampUtc.Ticks : source.TouchReportSequence;
                if (sequence == 0) sequence = controller.TimestampUtc.Ticks;
                if (sequence == lastReportSequence) return;
                lastReportSequence = sequence;
                if (recordingEndsUtc != DateTime.MinValue && now >= recordingEndsUtc) recordingEndsUtc = DateTime.MinValue;
                pressed = controller.DualSense.TouchpadPressed;
                if (pressed)
                {
                    pressRecognized = true;
                    if (testStage == TouchpadTestStage.Press) AdvanceStage();
                }
                if (paused) return;

                HashSet<int> seen = new HashSet<int>();
                DualSenseTouchPoint[] points = controller.DualSense.TouchPoints ?? new DualSenseTouchPoint[0];
                int activeCount = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    DualSenseTouchPoint point = points[i];
                    if (point == null || !point.IsActive) continue;
                    activeCount++;
                    seen.Add(point.Id);
                    AddPoint(point, controller.TimestampUtc == DateTime.MinValue ? now : controller.TimestampUtc);
                }
                List<int> ended = new List<int>();
                foreach (KeyValuePair<int, TouchPointState> item in active)
                {
                    if (!seen.Contains(item.Key)) ended.Add(item.Key);
                }
                for (int i = 0; i < ended.Count; i++) active.Remove(ended[i]);
                maximumSimultaneousContacts = Math.Max(maximumSimultaneousContacts, activeCount);
                if (activeCount >= 2)
                {
                    twoFingerRecognized = true;
                    if (testStage == TouchpadTestStage.TwoFinger) AdvanceStage();
                }
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                active.Clear();
                trail.Clear();
                Array.Clear(covered, 0, covered.Length);
                sampleCount = 0;
                maximumSimultaneousContacts = 0;
                twoFingerRecognized = false;
                pressRecognized = false;
                recordingEndsUtc = DateTime.MinValue;
                testStage = TouchpadTestStage.NotStarted;
                stageStartSeen = false;
            }
        }

        public void SetPaused(bool value)
        {
            lock (sync) paused = value;
        }

        public void StartRecording(DateTime now)
        {
            lock (sync)
            {
                trail.Clear();
                Array.Clear(covered, 0, covered.Length);
                sampleCount = 0;
                recordingEndsUtc = now.AddSeconds(5);
                paused = false;
            }
        }

        public void StartGuidedTest()
        {
            lock (sync)
            {
                ClearUnsafe();
                testStage = TouchpadTestStage.LeftTopToRightBottom;
            }
        }

        public void CancelTransient()
        {
            lock (sync)
            {
                recordingEndsUtc = DateTime.MinValue;
                paused = false;
                if (testStage != TouchpadTestStage.NotStarted && testStage != TouchpadTestStage.Completed) testStage = TouchpadTestStage.Cancelled;
                active.Clear();
            }
        }

        public TouchpadAnalysisSnapshot GetSnapshot(DateTime now)
        {
            lock (sync)
            {
                TouchPointState[] points = new TouchPointState[active.Count];
                int pointIndex = 0;
                foreach (TouchPointState point in active.Values) points[pointIndex++] = point.Copy();
                TouchTrailPoint[] trailCopy = new TouchTrailPoint[trail.Count];
                for (int i = 0; i < trail.Count; i++) trailCopy[i] = trail[i].Copy();
                bool[] cells = new bool[covered.Length];
                Array.Copy(covered, cells, covered.Length);
                int coveredCount = 0;
                int edgeTotal = 0;
                int edgeCovered = 0;
                for (int y = 0; y < TouchpadAnalysisSnapshot.GridRows; y++)
                {
                    for (int x = 0; x < TouchpadAnalysisSnapshot.GridColumns; x++)
                    {
                        int index = y * TouchpadAnalysisSnapshot.GridColumns + x;
                        if (covered[index]) coveredCount++;
                        bool edge = x == 0 || y == 0 || x == TouchpadAnalysisSnapshot.GridColumns - 1 || y == TouchpadAnalysisSnapshot.GridRows - 1;
                        if (edge)
                        {
                            edgeTotal++;
                            if (covered[index]) edgeCovered++;
                        }
                    }
                }
                return new TouchpadAnalysisSnapshot
                {
                    IsAvailable = isAvailable,
                    IsPressed = pressed,
                    IsPaused = paused,
                    IsRecording = recordingEndsUtc != DateTime.MinValue && now < recordingEndsUtc,
                    RecordingSecondsRemaining = recordingEndsUtc == DateTime.MinValue ? 0 : Math.Max(0, (recordingEndsUtc - now).TotalSeconds),
                    ActiveContactCount = active.Count,
                    MaximumSimultaneousContacts = maximumSimultaneousContacts,
                    SampleCount = sampleCount,
                    CoveragePercent = 100.0 * coveredCount / covered.Length,
                    EdgeCoveragePercent = edgeTotal == 0 ? 0 : 100.0 * edgeCovered / edgeTotal,
                    TwoFingerRecognized = twoFingerRecognized,
                    PressRecognized = pressRecognized,
                    AvailabilityMessage = isAvailable ? string.Empty : "当前连接未提供已验证的 DualSense 触摸坐标。",
                    TestStage = testStage,
                    Guidance = GuidanceFor(testStage),
                    TestSummary = SummaryFor(testStage, coveredCount, edgeCovered, edgeTotal),
                    Points = points,
                    Trail = trailCopy,
                    CoveredCells = cells
                };
            }
        }

        private void AddPoint(DualSenseTouchPoint source, DateTime timestamp)
        {
            double x = Clamp01(source.X);
            double y = Clamp01(source.Y);
            TouchPointState previous;
            bool exists = active.TryGetValue(source.Id, out previous);
            double speed = 0;
            TouchContactPhase phase = TouchContactPhase.Started;
            if (exists)
            {
                double seconds = Math.Max(0.001, (timestamp - previous.TimestampUtc).TotalSeconds);
                double distance = Math.Sqrt((x - previous.X) * (x - previous.X) + (y - previous.Y) * (y - previous.Y));
                speed = Math.Min(20, distance / seconds);
                phase = distance > 0.001 ? TouchContactPhase.Moved : TouchContactPhase.Stationary;
            }
            active[source.Id] = new TouchPointState { ContactId = source.Id, IsActive = true, X = x, Y = y, Speed = speed, Phase = phase, TimestampUtc = timestamp };
            trail.Add(new TouchTrailPoint { ContactId = source.Id, X = x, Y = y, TimestampUtc = timestamp, StartsStroke = !exists });
            if (trail.Count > TrailCapacity) trail.RemoveRange(0, Math.Min(200, trail.Count - TrailCapacity));
            int column = Math.Min(TouchpadAnalysisSnapshot.GridColumns - 1, (int)(x * TouchpadAnalysisSnapshot.GridColumns));
            int row = Math.Min(TouchpadAnalysisSnapshot.GridRows - 1, (int)(y * TouchpadAnalysisSnapshot.GridRows));
            covered[row * TouchpadAnalysisSnapshot.GridColumns + column] = true;
            sampleCount++;
            UpdateGuidedStage(x, y);
        }

        private void UpdateGuidedStage(double x, double y)
        {
            const double corner = 0.22;
            if (testStage == TouchpadTestStage.LeftTopToRightBottom)
            {
                if (x <= corner && y <= corner) stageStartSeen = true;
                if (stageStartSeen && x >= 1 - corner && y >= 1 - corner) AdvanceStage();
            }
            else if (testStage == TouchpadTestStage.RightTopToLeftBottom)
            {
                if (x >= 1 - corner && y <= corner) stageStartSeen = true;
                if (stageStartSeen && x <= corner && y >= 1 - corner) AdvanceStage();
            }
            else if (testStage == TouchpadTestStage.EdgeTrace && EdgeCoverageUnsafe() >= 70.0) AdvanceStage();
        }

        private void AdvanceStage()
        {
            if (testStage < TouchpadTestStage.Completed) testStage++;
            stageStartSeen = false;
        }

        private double EdgeCoverageUnsafe()
        {
            int total = 0;
            int hit = 0;
            for (int y = 0; y < TouchpadAnalysisSnapshot.GridRows; y++)
            {
                for (int x = 0; x < TouchpadAnalysisSnapshot.GridColumns; x++)
                {
                    if (x != 0 && y != 0 && x != TouchpadAnalysisSnapshot.GridColumns - 1 && y != TouchpadAnalysisSnapshot.GridRows - 1) continue;
                    total++;
                    if (covered[y * TouchpadAnalysisSnapshot.GridColumns + x]) hit++;
                }
            }
            return total == 0 ? 0 : 100.0 * hit / total;
        }

        private void EndAll(DateTime now)
        {
            active.Clear();
            recordingEndsUtc = DateTime.MinValue;
        }

        private void ClearUnsafe()
        {
            active.Clear();
            trail.Clear();
            Array.Clear(covered, 0, covered.Length);
            sampleCount = 0;
            maximumSimultaneousContacts = 0;
            twoFingerRecognized = false;
            pressRecognized = false;
            recordingEndsUtc = DateTime.MinValue;
            paused = false;
            stageStartSeen = false;
        }

        private static bool IsNativeTouch(ControllerState controller)
        {
            return controller != null && controller.IsConnected && controller.ControllerType == ControllerType.DualSense && controller.InputSource == ControllerInputSource.DualSenseHid && controller.DualSense != null && controller.DualSense.TouchCoordinatesAvailable;
        }

        private static string GuidanceFor(TouchpadTestStage stage)
        {
            switch (stage)
            {
                case TouchpadTestStage.LeftTopToRightBottom: return "单指从左上角连续滑到右下角";
                case TouchpadTestStage.RightTopToLeftBottom: return "单指从右上角连续滑到左下角";
                case TouchpadTestStage.EdgeTrace: return "单指沿触摸板四周边缘移动一圈";
                case TouchpadTestStage.TwoFinger: return "请同时放上两根手指";
                case TouchpadTestStage.Press: return "请按下触摸板";
                case TouchpadTestStage.Completed: return "触摸板引导检测已完成";
                case TouchpadTestStage.Cancelled: return "检测已取消，可重新开始";
                default: return "点击“开始引导检测”，或自由触摸查看实时轨迹";
            }
        }

        private static string SummaryFor(TouchpadTestStage stage, int coveredCount, int edgeCovered, int edgeTotal)
        {
            if (stage != TouchpadTestStage.Completed) return "尚未完整完成五步检测；未覆盖区域只能作为复测线索，不能单凭一次轨迹判定硬件坏区。";
            double coverage = 100.0 * coveredCount / (TouchpadAnalysisSnapshot.GridColumns * TouchpadAnalysisSnapshot.GridRows);
            double edge = edgeTotal == 0 ? 0 : 100.0 * edgeCovered / edgeTotal;
            return string.Format(CultureInfo.InvariantCulture, "五步完成 · 网格覆盖 {0:0.0}% · 边缘覆盖 {1:0.0}% · 双指与按压已识别", coverage, edge);
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0, Math.Min(1, value));
        }
    }

    public sealed class GyroscopeDiagnosticsSnapshot
    {
        public bool IsAvailable;
        public int SampleCount;
        public int InterruptedSamples;
        public int JumpCount;
        public bool AxisXResponded;
        public bool AxisYResponded;
        public bool AxisZResponded;
        public bool AxisXPositive;
        public bool AxisXNegative;
        public bool AxisYPositive;
        public bool AxisYNegative;
        public bool AxisZPositive;
        public bool AxisZNegative;
        public bool AxisFrozen;
        public double MaximumGyroX;
        public double MaximumGyroY;
        public double MaximumGyroZ;
        public double ZeroBiasMagnitude;
        public double NoiseMagnitude;
        public int Score;
        public string Status = "未检测";
        public string Notes = string.Empty;
    }

    public sealed class DualSenseGyroscopeAnalyzer
    {
        private readonly object sync = new object();
        private long lastSequence = -1;
        private DateTime lastTimestamp = DateTime.MinValue;
        private MotionSample last;
        private int sampleCount;
        private int interrupted;
        private int jumps;
        private int identicalRun;
        private double maxX;
        private double maxY;
        private double maxZ;
        private bool positiveX;
        private bool negativeX;
        private bool positiveY;
        private bool negativeY;
        private bool positiveZ;
        private bool negativeZ;

        public void Update(MotionSample sample)
        {
            if (sample == null || !sample.IsValid) return;
            lock (sync)
            {
                if (sample.Sequence == lastSequence) return;
                if (lastSequence >= 0 && (sample.Sequence > lastSequence + 1 || (lastTimestamp != DateTime.MinValue && (sample.TimestampUtc - lastTimestamp).TotalMilliseconds > 100))) interrupted++;
                if (last != null)
                {
                    if (Math.Abs(sample.GyroX - last.GyroX) > 500 || Math.Abs(sample.GyroY - last.GyroY) > 500 || Math.Abs(sample.GyroZ - last.GyroZ) > 500) jumps++;
                    if (sample.RawGyroX == last.RawGyroX && sample.RawGyroY == last.RawGyroY && sample.RawGyroZ == last.RawGyroZ) identicalRun++;
                    else identicalRun = 0;
                }
                lastSequence = sample.Sequence;
                lastTimestamp = sample.TimestampUtc;
                last = sample.Copy();
                sampleCount++;
                maxX = Math.Max(maxX, Math.Abs(sample.GyroX));
                maxY = Math.Max(maxY, Math.Abs(sample.GyroY));
                maxZ = Math.Max(maxZ, Math.Abs(sample.GyroZ));
                if (sample.GyroX >= 12) positiveX = true;
                if (sample.GyroX <= -12) negativeX = true;
                if (sample.GyroY >= 12) positiveY = true;
                if (sample.GyroY <= -12) negativeY = true;
                if (sample.GyroZ >= 12) positiveZ = true;
                if (sample.GyroZ <= -12) negativeZ = true;
            }
        }

        public GyroscopeDiagnosticsSnapshot GetSnapshot(MotionViewState motion)
        {
            lock (sync)
            {
                MotionCalibrationResult calibration = motion == null ? null : motion.Calibration;
                double bias = calibration == null ? 0 : Math.Sqrt(calibration.BiasX * calibration.BiasX + calibration.BiasY * calibration.BiasY + calibration.BiasZ * calibration.BiasZ);
                double noise = calibration == null ? 0 : Math.Sqrt(calibration.StandardDeviationX * calibration.StandardDeviationX + calibration.StandardDeviationY * calibration.StandardDeviationY + calibration.StandardDeviationZ * calibration.StandardDeviationZ);
                bool frozen = identicalRun > 120 && sampleCount > 120;
                int score = 100;
                if (motion == null || !motion.IsAvailable) score = 0;
                else
                {
                    score -= Math.Min(25, interrupted * 3);
                    score -= Math.Min(25, jumps * 5);
                    if (frozen) score -= 45;
                    if (sampleCount >= 120)
                    {
                        if (!(positiveX && negativeX)) score -= 8;
                        if (!(positiveY && negativeY)) score -= 8;
                        if (!(positiveZ && negativeZ)) score -= 8;
                    }
                    if (calibration != null && calibration.IsValid)
                    {
                        score -= (int)Math.Min(20, noise * 8);
                        score -= (int)Math.Min(15, bias * 2);
                    }
                    else score -= 10;
                }
                score = Math.Max(0, Math.Min(100, score));
                string status = motion == null || !motion.IsAvailable ? "未检测" : score >= 90 ? "优秀" : score >= 75 ? "良好" : score >= 55 ? "需要注意" : "异常";
                string notes = frozen ? "检测到三轴长时间完全不变，建议复测轴冻结。" : jumps > 0 ? "检测到异常大跳变，请检查连接稳定性。" : "Yaw 没有磁力计绝对参考，缓慢漂移属于预期现象。";
                return new GyroscopeDiagnosticsSnapshot
                {
                    IsAvailable = motion != null && motion.IsAvailable,
                    SampleCount = sampleCount,
                    InterruptedSamples = interrupted,
                    JumpCount = jumps,
                    AxisXResponded = positiveX && negativeX,
                    AxisYResponded = positiveY && negativeY,
                    AxisZResponded = positiveZ && negativeZ,
                    AxisXPositive = positiveX,
                    AxisXNegative = negativeX,
                    AxisYPositive = positiveY,
                    AxisYNegative = negativeY,
                    AxisZPositive = positiveZ,
                    AxisZNegative = negativeZ,
                    AxisFrozen = frozen,
                    MaximumGyroX = maxX,
                    MaximumGyroY = maxY,
                    MaximumGyroZ = maxZ,
                    ZeroBiasMagnitude = bias,
                    NoiseMagnitude = noise,
                    Score = score,
                    Status = status,
                    Notes = notes
                };
            }
        }

        public void Reset()
        {
            lock (sync)
            {
                lastSequence = -1;
                lastTimestamp = DateTime.MinValue;
                last = null;
                sampleCount = interrupted = jumps = identicalRun = 0;
                maxX = maxY = maxZ = 0;
                positiveX = negativeX = positiveY = negativeY = positiveZ = negativeZ = false;
            }
        }
    }

    public sealed class DualSenseAdvancedSnapshot
    {
        public string DeviceId = string.Empty;
        public TouchpadAnalysisSnapshot Touchpad = new TouchpadAnalysisSnapshot();
        public GyroscopeDiagnosticsSnapshot Gyroscope = new GyroscopeDiagnosticsSnapshot();
    }

    internal sealed class DualSenseAdvancedSession
    {
        public readonly DualSenseTouchpadAnalyzer Touchpad = new DualSenseTouchpadAnalyzer();
        public readonly DualSenseGyroscopeAnalyzer Gyroscope = new DualSenseGyroscopeAnalyzer();

        public void Update(ControllerState state, DateTime now)
        {
            Touchpad.Update(state, now);
            if (state != null && state.DualSense != null) Gyroscope.Update(state.DualSense.Motion);
        }

        public void Reset()
        {
            Touchpad.Clear();
            Gyroscope.Reset();
        }
    }

    public sealed class DualSenseAdvancedManager
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, DualSenseAdvancedSession> sessions = new Dictionary<string, DualSenseAdvancedSession>(StringComparer.OrdinalIgnoreCase);

        public void Synchronize(ControllerState[] controllers, DualSenseMotionManager motionManager)
        {
            DateTime now = DateTime.UtcNow;
            HashSet<string> online = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (controllers != null)
            {
                for (int i = 0; i < controllers.Length; i++)
                {
                    ControllerState state = controllers[i];
                    if (state == null || state.ControllerType != ControllerType.DualSense || string.IsNullOrEmpty(state.DeviceId)) continue;
                    online.Add(state.DeviceId);
                    GetOrCreate(state.DeviceId).Update(state, now);
                }
            }
            lock (sync)
            {
                List<string> removed = new List<string>();
                foreach (string id in sessions.Keys) if (!online.Contains(id)) removed.Add(id);
                for (int i = 0; i < removed.Count; i++)
                {
                    sessions[removed[i]].Reset();
                    sessions.Remove(removed[i]);
                }
            }
        }

        public DualSenseAdvancedSnapshot Get(string deviceId, DualSenseMotionManager motionManager)
        {
            if (string.IsNullOrEmpty(deviceId)) return new DualSenseAdvancedSnapshot();
            DualSenseAdvancedSession session;
            lock (sync)
            {
                if (!sessions.TryGetValue(deviceId, out session)) return new DualSenseAdvancedSnapshot { DeviceId = deviceId };
            }
            MotionViewState motion = motionManager == null ? null : motionManager.Get(deviceId);
            return new DualSenseAdvancedSnapshot { DeviceId = deviceId, Touchpad = session.Touchpad.GetSnapshot(DateTime.UtcNow), Gyroscope = session.Gyroscope.GetSnapshot(motion) };
        }

        public void ClearTouch(string deviceId) { WithSession(deviceId, delegate(DualSenseAdvancedSession s) { s.Touchpad.Clear(); }); }
        public void PauseTouch(string deviceId, bool paused) { WithSession(deviceId, delegate(DualSenseAdvancedSession s) { s.Touchpad.SetPaused(paused); }); }
        public void RecordTouch(string deviceId) { WithSession(deviceId, delegate(DualSenseAdvancedSession s) { s.Touchpad.StartRecording(DateTime.UtcNow); }); }
        public void StartTouchTest(string deviceId) { WithSession(deviceId, delegate(DualSenseAdvancedSession s) { s.Touchpad.StartGuidedTest(); }); }
        public void CancelTransient(string deviceId) { WithSession(deviceId, delegate(DualSenseAdvancedSession s) { s.Touchpad.CancelTransient(); }); }
        public void ResetGyroscope(string deviceId) { WithSession(deviceId, delegate(DualSenseAdvancedSession s) { s.Gyroscope.Reset(); }); }

        private DualSenseAdvancedSession GetOrCreate(string deviceId)
        {
            lock (sync)
            {
                DualSenseAdvancedSession session;
                if (!sessions.TryGetValue(deviceId, out session))
                {
                    session = new DualSenseAdvancedSession();
                    sessions[deviceId] = session;
                }
                return session;
            }
        }

        private void WithSession(string deviceId, Action<DualSenseAdvancedSession> action)
        {
            if (string.IsNullOrEmpty(deviceId) || action == null) return;
            action(GetOrCreate(deviceId));
        }
    }

    public sealed class DualSenseAdvancedCapabilities
    {
        public bool TouchpadPressedAvailable;
        public bool TouchPoint1Available;
        public bool TouchPoint2Available;
        public bool AccelerometerAvailable;
        public bool GyroscopeAvailable;
        public bool BatteryAvailable;
        public bool ConnectionModeAvailable;
        public bool LightbarOutputAvailable;
        public bool AdaptiveTriggerOutputAvailable;
        public string UsbStatus = "待验证";
        public string BluetoothStatus = "待验证";

        public static DualSenseAdvancedCapabilities From(ControllerState state)
        {
            bool real = state != null && state.IsConnected && state.ControllerType == ControllerType.DualSense && state.InputSource == ControllerInputSource.DualSenseHid;
            bool touch = real && state.DualSense != null && state.DualSense.TouchCoordinatesAvailable;
            bool motion = real && state.DualSense != null && state.DualSense.Motion != null && state.DualSense.Motion.IsValid;
            return new DualSenseAdvancedCapabilities
            {
                TouchpadPressedAvailable = real,
                TouchPoint1Available = touch,
                TouchPoint2Available = touch,
                AccelerometerAvailable = motion,
                GyroscopeAvailable = motion,
                BatteryAvailable = real && state.BatteryLevel >= 0,
                ConnectionModeAvailable = real && state.ConnectionType != ControllerConnectionType.Unknown,
                LightbarOutputAvailable = false,
                AdaptiveTriggerOutputAvailable = false,
                UsbStatus = "完整 0x01 输入已实现并有构造自检；灯带/自适应扳机输出待实机验证，未开放",
                BluetoothStatus = "完整 0x31 输入与 CRC 已实现并有构造自检；高级输出待实机验证，未开放"
            };
        }
    }

    public enum AdaptiveTriggerPreset
    {
        Off,
        FixedResistance,
        ResistanceWall,
        SegmentedResistance,
        Spring,
        Click
    }

    public struct DualSenseLightbarColor
    {
        public byte Red;
        public byte Green;
        public byte Blue;
    }

    // Advanced output is deliberately separated from ordinary dual-motor rumble.
    // The current implementation is a closed capability boundary: it never writes
    // an unverified USB or Bluetooth packet and gives the UI an explicit reason.
    public interface IDualSenseAdvancedOutputService
    {
        bool IsLightbarVerified { get; }
        bool IsAdaptiveTriggerVerified { get; }
        string VerificationStatus { get; }
        bool TrySetLightbar(DualSenseLightbarColor color, out string error);
        bool TrySetAdaptiveTrigger(bool left, AdaptiveTriggerPreset preset, double strength, out string error);
        void RestoreSafeDefaults();
    }

    public sealed class UnverifiedDualSenseAdvancedOutputService : IDualSenseAdvancedOutputService
    {
        public bool IsLightbarVerified { get { return false; } }
        public bool IsAdaptiveTriggerVerified { get { return false; } }
        public string VerificationStatus { get { return "USB 与蓝牙高级输出均待实机验证，当前版本不发送报告。"; } }

        public bool TrySetLightbar(DualSenseLightbarColor color, out string error)
        {
            error = "灯带输出待实机验证，当前版本未开放。";
            return false;
        }

        public bool TrySetAdaptiveTrigger(bool left, AdaptiveTriggerPreset preset, double strength, out string error)
        {
            error = "自适应扳机输出待实机验证，当前版本未开放。";
            return false;
        }

        public void RestoreSafeDefaults() { }
    }

    [DataContract]
    public sealed class DualSenseDeviceCalibrationProfile
    {
        [DataMember] public string DeviceKey = string.Empty;
        [DataMember] public DateTime SavedUtc;
        [DataMember] public double GyroBiasX;
        [DataMember] public double GyroBiasY;
        [DataMember] public double GyroBiasZ;
        [DataMember] public double GyroNoiseX;
        [DataMember] public double GyroNoiseY;
        [DataMember] public double GyroNoiseZ;
        [DataMember] public int SampleCount;
        [DataMember] public double Sensitivity = 1.0;
        [DataMember] public double Smoothing = 0.72;
    }

    public sealed class DualSenseCalibrationStore
    {
        private readonly string root;

        public DualSenseCalibrationStore()
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerLab", "DualSense", "Devices");
        }

        internal DualSenseCalibrationStore(string rootDirectory)
        {
            root = rootDirectory;
        }

        public string RootDirectory { get { return root; } }

        public string Save(string deviceId, MotionCalibrationResult result, double sensitivity, double smoothing)
        {
            if (result == null || !result.IsValid) throw new InvalidOperationException("只有成功的静止校准可以保存。");
            Directory.CreateDirectory(root);
            DualSenseDeviceCalibrationProfile profile = new DualSenseDeviceCalibrationProfile
            {
                DeviceKey = StableKey(deviceId),
                SavedUtc = DateTime.UtcNow,
                GyroBiasX = result.BiasX,
                GyroBiasY = result.BiasY,
                GyroBiasZ = result.BiasZ,
                GyroNoiseX = result.StandardDeviationX,
                GyroNoiseY = result.StandardDeviationY,
                GyroNoiseZ = result.StandardDeviationZ,
                SampleCount = result.SampleCount,
                Sensitivity = Math.Max(0.5, Math.Min(2.0, sensitivity)),
                Smoothing = Math.Max(0, Math.Min(1, smoothing))
            };
            string path = PathFor(deviceId);
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DualSenseDeviceCalibrationProfile));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) serializer.WriteObject(stream, profile);
            return path;
        }

        public DualSenseDeviceCalibrationProfile Load(string deviceId)
        {
            try
            {
                string path = PathFor(deviceId);
                if (!File.Exists(path)) return null;
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DualSenseDeviceCalibrationProfile));
                using (FileStream stream = File.OpenRead(path)) return serializer.ReadObject(stream) as DualSenseDeviceCalibrationProfile;
            }
            catch
            {
                return null;
            }
        }

        private string PathFor(string deviceId)
        {
            return Path.Combine(root, "device-" + StableKey(deviceId) + ".json");
        }

        private static string StableKey(string deviceId)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(deviceId ?? "dualsense-unknown"));
                StringBuilder value = new StringBuilder(16);
                for (int i = 0; i < 8; i++) value.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return value.ToString();
            }
        }
    }

    public static class DualSenseAdvancedSelfTest
    {
        public static string Run()
        {
            List<string> passed = new List<string>();
            VerifyTouchLifecycle(passed);
            VerifyCoverageAndGuidance(passed);
            VerifyGyroscopeDiagnostics(passed);
            VerifySessionCancellationAndCalibration(passed);
            VerifyCapabilities(passed);
            VerifyConfiguration(passed);
            return "DualSense advanced self-test passed: " + string.Join(", ", passed.ToArray());
        }

        private static void VerifyTouchLifecycle(List<string> passed)
        {
            DualSenseTouchpadAnalyzer analyzer = new DualSenseTouchpadAnalyzer();
            DateTime now = DateTime.UtcNow;
            analyzer.Update(TouchState(1, now, new DualSenseTouchPoint { Id = 3, IsActive = true, X = 0.1, Y = 0.2 }, new DualSenseTouchPoint { Id = 7, IsActive = true, X = 0.8, Y = 0.7 }, false), now);
            TouchpadAnalysisSnapshot snapshot = analyzer.GetSnapshot(now);
            Require(snapshot.ActiveContactCount == 2 && snapshot.TwoFingerRecognized && snapshot.Points[0].X >= 0 && snapshot.Points[0].X <= 1, "two finger mapping failed");
            analyzer.Update(TouchState(2, now.AddMilliseconds(8), new DualSenseTouchPoint { Id = 3, IsActive = false }, new DualSenseTouchPoint { Id = 7, IsActive = false }, true), now.AddMilliseconds(8));
            snapshot = analyzer.GetSnapshot(now.AddMilliseconds(8));
            Require(snapshot.ActiveContactCount == 0 && snapshot.IsPressed && snapshot.PressRecognized, "touch release or press lifecycle failed");
            passed.Add("touch-direction-two-finger-release-press");
        }

        private static void VerifyCoverageAndGuidance(List<string> passed)
        {
            DualSenseTouchpadAnalyzer analyzer = new DualSenseTouchpadAnalyzer();
            analyzer.StartGuidedTest();
            DateTime now = DateTime.UtcNow;
            int sequence = 1;
            analyzer.Update(TouchState(sequence++, now, Point(0.05, 0.05), null, false), now);
            analyzer.Update(TouchState(sequence++, now.AddMilliseconds(8), Point(0.95, 0.95), null, false), now.AddMilliseconds(8));
            analyzer.Update(TouchState(sequence++, now.AddMilliseconds(16), Point(0.95, 0.05), null, false), now.AddMilliseconds(16));
            analyzer.Update(TouchState(sequence++, now.AddMilliseconds(24), Point(0.05, 0.95), null, false), now.AddMilliseconds(24));
            TouchpadAnalysisSnapshot snapshot = analyzer.GetSnapshot(now.AddMilliseconds(24));
            Require(snapshot.TestStage == TouchpadTestStage.EdgeTrace && snapshot.CoveragePercent > 0 && snapshot.CoveragePercent < 10, "coverage must be grid based and guided diagonals must advance");
            passed.Add("fixed-grid-coverage-and-guided-diagonals");
        }

        private static void VerifyGyroscopeDiagnostics(List<string> passed)
        {
            DualSenseGyroscopeAnalyzer analyzer = new DualSenseGyroscopeAnalyzer();
            DateTime now = DateTime.UtcNow;
            analyzer.Update(new MotionSample { IsValid = true, Sequence = 1, TimestampUtc = now, GyroX = 20, GyroY = 30, GyroZ = 40 });
            analyzer.Update(new MotionSample { IsValid = true, Sequence = 3, TimestampUtc = now.AddMilliseconds(150), GyroX = -20, GyroY = -30, GyroZ = -40 });
            MotionViewState view = new MotionViewState { IsAvailable = true, Calibration = new MotionCalibrationResult { IsValid = true, BiasX = 0.2, StandardDeviationX = 0.1 } };
            GyroscopeDiagnosticsSnapshot snapshot = analyzer.GetSnapshot(view);
            Require(snapshot.AxisXResponded && snapshot.AxisYResponded && snapshot.AxisZResponded && snapshot.InterruptedSamples == 1 && snapshot.NoiseMagnitude > 0, "gyro response/interruption/noise failed");
            passed.Add("gyro-response-interruption-noise-score");
        }

        private static void VerifyCapabilities(List<string> passed)
        {
            ControllerState usb = TouchState(1, DateTime.UtcNow, Point(0.5, 0.5), null, false);
            usb.ConnectionType = ControllerConnectionType.Wired;
            usb.BatteryLevel = 75;
            usb.DualSense.Motion = new MotionSample { IsValid = true };
            DualSenseAdvancedCapabilities capabilities = DualSenseAdvancedCapabilities.From(usb);
            Require(capabilities.TouchPoint1Available && capabilities.GyroscopeAvailable && capabilities.BatteryAvailable && !capabilities.LightbarOutputAvailable && !capabilities.AdaptiveTriggerOutputAvailable, "capability audit must not expose unverified outputs");
            ControllerState xbox = new ControllerState { IsConnected = true, ControllerType = ControllerType.Xbox, InputSource = ControllerInputSource.XboxXInput };
            Require(!DualSenseAdvancedCapabilities.From(xbox).TouchpadPressedAvailable, "Xbox must remain outside DualSense advanced features");
            IDualSenseAdvancedOutputService output = new UnverifiedDualSenseAdvancedOutputService();
            string error;
            Require(!output.TrySetLightbar(new DualSenseLightbarColor { Blue = 255 }, out error) && !output.TrySetAdaptiveTrigger(true, AdaptiveTriggerPreset.Click, 0.5, out error), "unverified advanced output must remain closed");
            passed.Add("usb-bluetooth-boundaries-and-xbox-isolation");
        }

        private static void VerifySessionCancellationAndCalibration(List<string> passed)
        {
            DateTime now = DateTime.UtcNow;
            ControllerState state = TouchState(1, now, Point(0.3, 0.4), null, false);
            state.DualSense.Motion = new MotionSample { IsValid = true, Sequence = 1, TimestampUtc = now, GyroX = 0.5, AccelZ = 1, CrcValidated = true };
            state.Capabilities.HasMotionSensors = true;
            DualSenseMotionManager motion = new DualSenseMotionManager();
            DualSenseAdvancedManager advanced = new DualSenseAdvancedManager();
            motion.Synchronize(new[] { state });
            advanced.Synchronize(new[] { state }, motion);
            advanced.RecordTouch(state.DeviceId);
            advanced.CancelTransient(state.DeviceId);
            TouchpadAnalysisSnapshot touch = advanced.Get(state.DeviceId, motion).Touchpad;
            Require(!touch.IsRecording && touch.ActiveContactCount == 0, "page-leave transient cancellation failed");
            string reason;
            Require(motion.StartCalibration(state.DeviceId, out reason), "calibration did not start for page-leave test");
            motion.CancelCalibration(state.DeviceId, "page leave selftest");
            Require(motion.Get(state.DeviceId).CalibrationState == MotionCalibrationState.Failed, "page leave did not cancel active calibration");
            Require(motion.ApplyCalibration(state.DeviceId, new MotionCalibrationResult { IsValid = true, SampleCount = 80, BiasX = 0.5 }, out reason), "saved calibration did not apply");
            Require(motion.Get(state.DeviceId).Calibration.IsValid, "applied calibration missing from session");
            motion.Synchronize(new ControllerState[0]);
            advanced.Synchronize(new ControllerState[0], motion);
            Require(!advanced.Get(state.DeviceId, motion).Touchpad.IsAvailable, "disconnect did not release advanced session");
            passed.Add("page-leave-cancel-saved-calibration-disconnect-release");
        }

        private static void VerifyConfiguration(List<string> passed)
        {
            string temp = Path.Combine(Path.GetTempPath(), "ControllerLab-DualSense-" + Guid.NewGuid().ToString("N"));
            try
            {
                DualSenseCalibrationStore store = new DualSenseCalibrationStore(temp);
                string id = "SELFTEST-PROFILE";
                string path = store.Save(id, new MotionCalibrationResult { IsValid = true, SampleCount = 100, BiasX = 0.4, StandardDeviationX = 0.12 }, 1.25, 0.8);
                DualSenseDeviceCalibrationProfile loaded = store.Load(id);
                Require(loaded != null && Math.Abs(loaded.GyroBiasX - 0.4) < 0.001 && Math.Abs(loaded.Sensitivity - 1.25) < 0.001, "saved profile did not reload");
                File.WriteAllText(path, "{broken-json", Encoding.UTF8);
                Require(store.Load(id) == null, "corrupt profile must be ignored safely");
                passed.Add("device-profile-save-reload-corruption-safe");
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch { }
            }
        }

        private static ControllerState TouchState(int sequence, DateTime timestamp, DualSenseTouchPoint first, DualSenseTouchPoint second, bool pressed)
        {
            InputSnapshot source = new InputSnapshot { DeviceId = "ds:selftest", Connected = true, Family = ControllerFamily.PlayStation, InputBackend = "Sony 原生 HID", TimestampUtc = timestamp, TouchCoordinatesAvailable = true, TouchReportSequence = sequence, TouchPoint1 = first, TouchPoint2 = second, TouchpadPressed = pressed };
            ControllerState state = ControllerStateAdapter.FromSnapshot(source);
            state.InputSource = ControllerInputSource.DualSenseHid;
            return state;
        }

        private static DualSenseTouchPoint Point(double x, double y)
        {
            return new DualSenseTouchPoint { Id = 1, IsActive = true, X = x, Y = y };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
