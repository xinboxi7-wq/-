using System;
using System.Collections.Generic;
using System.Globalization;

namespace ControllerLab
{
    // Motion data is an input-layer value object. It carries both the unmodified HID
    // counts and their documented physical-unit conversion so the UI never inspects
    // raw report bytes itself.
    public sealed class MotionSample
    {
        public DateTime TimestampUtc;
        public long Sequence;
        public int RawGyroX;
        public int RawGyroY;
        public int RawGyroZ;
        public int RawAccelX;
        public int RawAccelY;
        public int RawAccelZ;
        public double GyroX;
        public double GyroY;
        public double GyroZ;
        public double AccelX;
        public double AccelY;
        public double AccelZ;
        public bool IsValid;
        public byte SourceReportId;
        public ControllerConnectionType ConnectionType;
        public string ConnectionLabel = string.Empty;
        public int ReportLength;
        public bool CrcValidated;
        public string Layout = string.Empty;
        public string AvailabilityMessage = string.Empty;

        public MotionSample Copy()
        {
            return (MotionSample)MemberwiseClone();
        }
    }

    public static class DualSenseMotionUnits
    {
        // Linux hid-playstation names these DS_GYRO_RES_PER_DEG_S and DS_ACC_RES_PER_G.
        // The native report therefore converts directly to degree/second and g without
        // relying on a guessed per-device scale factor.
        public const double GyroCountsPerDegreePerSecond = 1024.0;
        public const double AccelCountsPerG = 8192.0;

        public static double GyroToDegreesPerSecond(int raw)
        {
            return raw / GyroCountsPerDegreePerSecond;
        }

        public static double AccelToG(int raw)
        {
            return raw / AccelCountsPerG;
        }
    }

    public enum MotionCalibrationState
    {
        Unsupported,
        NotCalibrated,
        Settling,
        Sampling,
        Calibrated,
        Failed
    }

    public enum MotionTrackingQuality
    {
        Unsupported,
        Uncalibrated,
        Good,
        DataJitter,
        DataInterrupted
    }

    public sealed class MotionCalibrationResult
    {
        public string DeviceId = string.Empty;
        public double BiasX;
        public double BiasY;
        public double BiasZ;
        public double StandardDeviationX;
        public double StandardDeviationY;
        public double StandardDeviationZ;
        public int SampleCount;
        public bool IsValid;
        public string FailureReason = string.Empty;

        public MotionCalibrationResult Copy()
        {
            return (MotionCalibrationResult)MemberwiseClone();
        }
    }

    public struct MotionQuaternion
    {
        public double W;
        public double X;
        public double Y;
        public double Z;

        public static MotionQuaternion Identity
        {
            get { return new MotionQuaternion { W = 1, X = 0, Y = 0, Z = 0 }; }
        }

        public void Normalize()
        {
            double length = Math.Sqrt(W * W + X * X + Y * Y + Z * Z);
            if (length < 0.00000001 || double.IsNaN(length) || double.IsInfinity(length))
            {
                this = Identity;
                return;
            }
            W /= length;
            X /= length;
            Y /= length;
            Z /= length;
        }
    }

    public sealed class MotionFusionSnapshot
    {
        public MotionQuaternion Quaternion;
        public double Pitch;
        public double Roll;
        public double Yaw;
        public MotionCalibrationState CalibrationState;
        public MotionTrackingQuality TrackingQuality;
        public DateTime TimestampUtc;
        public long Sequence;
        public bool HasPose;

        public MotionFusionSnapshot Copy()
        {
            return (MotionFusionSnapshot)MemberwiseClone();
        }
    }

    internal sealed class MotionCalibrationService
    {
        private const double SettleSeconds = 1.0;
        private const double SampleSeconds = 3.0;
        private const int MinimumSamples = 60;
        private const double MaximumRmsDegreesPerSecond = 5.0;
        private const double MaximumStandardDeviationDegreesPerSecond = 2.5;
        private DateTime startedUtc;
        private DateTime sampleStartedUtc;
        private string deviceId = string.Empty;
        private int sampleCount;
        private double sumX;
        private double sumY;
        private double sumZ;
        private double sumSquaresX;
        private double sumSquaresY;
        private double sumSquaresZ;

        public MotionCalibrationState State { get; private set; }
        public MotionCalibrationResult Result { get; private set; }

        public MotionCalibrationService()
        {
            Reset();
        }

        public bool Start(string id, DateTime now, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                reason = "未找到可校准的 DualSense 设备。";
                return false;
            }
            deviceId = id;
            startedUtc = now;
            sampleStartedUtc = DateTime.MinValue;
            sampleCount = 0;
            sumX = sumY = sumZ = 0;
            sumSquaresX = sumSquaresY = sumSquaresZ = 0;
            Result = new MotionCalibrationResult { DeviceId = id, FailureReason = string.Empty };
            State = MotionCalibrationState.Settling;
            return true;
        }

        public void Update(string id, MotionSample sample, DateTime now)
        {
            if (sample == null || !sample.IsValid || State == MotionCalibrationState.Unsupported || State == MotionCalibrationState.Calibrated || State == MotionCalibrationState.Failed || State == MotionCalibrationState.NotCalibrated) return;
            if (!string.Equals(id, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                Fail("校准设备已切换，结果已丢弃。");
                return;
            }
            if (State == MotionCalibrationState.Settling)
            {
                if ((now - startedUtc).TotalSeconds < SettleSeconds) return;
                State = MotionCalibrationState.Sampling;
                sampleStartedUtc = now;
                sampleCount = 0;
                sumX = sumY = sumZ = 0;
                sumSquaresX = sumSquaresY = sumSquaresZ = 0;
            }
            if (State != MotionCalibrationState.Sampling) return;
            if ((now - sampleStartedUtc).TotalSeconds <= SampleSeconds)
            {
                Add(sample.GyroX, sample.GyroY, sample.GyroZ);
                return;
            }
            Complete();
        }

        public void MarkUnavailable(string reason)
        {
            if (State == MotionCalibrationState.Settling || State == MotionCalibrationState.Sampling) Fail(reason);
        }

        public void Reset()
        {
            State = MotionCalibrationState.NotCalibrated;
            Result = new MotionCalibrationResult();
            startedUtc = DateTime.MinValue;
            sampleStartedUtc = DateTime.MinValue;
            deviceId = string.Empty;
            sampleCount = 0;
            sumX = sumY = sumZ = 0;
            sumSquaresX = sumSquaresY = sumSquaresZ = 0;
        }

        private void Add(double x, double y, double z)
        {
            sampleCount++;
            sumX += x;
            sumY += y;
            sumZ += z;
            sumSquaresX += x * x;
            sumSquaresY += y * y;
            sumSquaresZ += z * z;
        }

        private void Complete()
        {
            if (sampleCount < MinimumSamples)
            {
                Fail("有效运动样本不足，请检查连接后重新校准。");
                return;
            }
            double meanX = sumX / sampleCount;
            double meanY = sumY / sampleCount;
            double meanZ = sumZ / sampleCount;
            double stdX = StandardDeviation(sumSquaresX, meanX, sampleCount);
            double stdY = StandardDeviation(sumSquaresY, meanY, sampleCount);
            double stdZ = StandardDeviation(sumSquaresZ, meanZ, sampleCount);
            double rms = Math.Sqrt(sumSquaresX / sampleCount + sumSquaresY / sampleCount + sumSquaresZ / sampleCount);
            if (rms > MaximumRmsDegreesPerSecond || Math.Max(stdX, Math.Max(stdY, stdZ)) > MaximumStandardDeviationDegreesPerSecond)
            {
                Fail("校准期间检测到明显移动，请将手柄平放并保持静止后重试。", stdX, stdY, stdZ);
                return;
            }
            Result = new MotionCalibrationResult
            {
                DeviceId = deviceId,
                BiasX = meanX,
                BiasY = meanY,
                BiasZ = meanZ,
                StandardDeviationX = stdX,
                StandardDeviationY = stdY,
                StandardDeviationZ = stdZ,
                SampleCount = sampleCount,
                IsValid = true
            };
            State = MotionCalibrationState.Calibrated;
        }

        private void Fail(string reason)
        {
            Fail(reason, 0, 0, 0);
        }

        private void Fail(string reason, double stdX, double stdY, double stdZ)
        {
            Result = new MotionCalibrationResult
            {
                DeviceId = deviceId,
                StandardDeviationX = stdX,
                StandardDeviationY = stdY,
                StandardDeviationZ = stdZ,
                SampleCount = sampleCount,
                IsValid = false,
                FailureReason = reason
            };
            State = MotionCalibrationState.Failed;
        }

        private static double StandardDeviation(double sumSquares, double mean, int count)
        {
            return Math.Sqrt(Math.Max(0, sumSquares / Math.Max(1, count) - mean * mean));
        }
    }

    // Mahony-style IMU fusion: gyro provides short-term movement, accelerometer gravity
    // corrects pitch/roll. Yaw deliberately has no magnetometer correction and can drift.
    public sealed class MotionFusionService
    {
        private const double DegreesToRadians = Math.PI / 180.0;
        private const double RadiansToDegrees = 180.0 / Math.PI;
        private const double ProportionalGain = 0.55;
        private const double MaximumDeltaSeconds = 0.10;
        private MotionQuaternion quaternion = MotionQuaternion.Identity;
        private DateTime lastTimestamp = DateTime.MinValue;
        private double centerPitch;
        private double centerRoll;
        private double centerYaw;
        private bool hasCenter;
        private MotionFusionSnapshot snapshot = new MotionFusionSnapshot
        {
            Quaternion = MotionQuaternion.Identity,
            CalibrationState = MotionCalibrationState.NotCalibrated,
            TrackingQuality = MotionTrackingQuality.Uncalibrated
        };

        public void Reset()
        {
            quaternion = MotionQuaternion.Identity;
            lastTimestamp = DateTime.MinValue;
            centerPitch = centerRoll = centerYaw = 0;
            hasCenter = false;
            snapshot = new MotionFusionSnapshot
            {
                Quaternion = quaternion,
                CalibrationState = MotionCalibrationState.NotCalibrated,
                TrackingQuality = MotionTrackingQuality.Uncalibrated
            };
        }

        public void Recenter()
        {
            double pitch;
            double roll;
            double yaw;
            ToEuler(quaternion, out pitch, out roll, out yaw);
            centerPitch = pitch;
            centerRoll = roll;
            centerYaw = yaw;
            hasCenter = true;
        }

        public MotionFusionSnapshot Update(MotionSample sample, MotionCalibrationResult calibration, DateTime now)
        {
            if (sample == null || !sample.IsValid)
            {
                snapshot.TrackingQuality = MotionTrackingQuality.Unsupported;
                snapshot.CalibrationState = MotionCalibrationState.Unsupported;
                snapshot.HasPose = false;
                return snapshot.Copy();
            }
            DateTime timestamp = sample.TimestampUtc == DateTime.MinValue ? now : sample.TimestampUtc;
            if (lastTimestamp == DateTime.MinValue)
            {
                lastTimestamp = timestamp;
                UpdateSnapshot(sample, calibration, now, MotionTrackingQualityFor(calibration, sample, now));
                return snapshot.Copy();
            }
            double delta = (timestamp - lastTimestamp).TotalSeconds;
            lastTimestamp = timestamp;
            if (delta <= 0 || delta > MaximumDeltaSeconds)
            {
                UpdateSnapshot(sample, calibration, now, delta > MaximumDeltaSeconds ? MotionTrackingQuality.DataInterrupted : MotionTrackingQualityFor(calibration, sample, now));
                return snapshot.Copy();
            }
            delta = Math.Max(0.001, Math.Min(0.025, delta));
            double biasX = calibration != null && calibration.IsValid ? calibration.BiasX : 0;
            double biasY = calibration != null && calibration.IsValid ? calibration.BiasY : 0;
            double biasZ = calibration != null && calibration.IsValid ? calibration.BiasZ : 0;
            IntegrateMahony((sample.GyroX - biasX) * DegreesToRadians, (sample.GyroY - biasY) * DegreesToRadians, (sample.GyroZ - biasZ) * DegreesToRadians, sample.AccelX, sample.AccelY, sample.AccelZ, delta);
            UpdateSnapshot(sample, calibration, now, MotionTrackingQualityFor(calibration, sample, now));
            return snapshot.Copy();
        }

        private void IntegrateMahony(double gx, double gy, double gz, double ax, double ay, double az, double delta)
        {
            double magnitude = Math.Sqrt(ax * ax + ay * ay + az * az);
            if (magnitude > 0.0001 && !double.IsNaN(magnitude) && !double.IsInfinity(magnitude))
            {
                ax /= magnitude;
                ay /= magnitude;
                az /= magnitude;
                double vx = 2.0 * (quaternion.X * quaternion.Z - quaternion.W * quaternion.Y);
                double vy = 2.0 * (quaternion.W * quaternion.X + quaternion.Y * quaternion.Z);
                double vz = quaternion.W * quaternion.W - quaternion.X * quaternion.X - quaternion.Y * quaternion.Y + quaternion.Z * quaternion.Z;
                double ex = ay * vz - az * vy;
                double ey = az * vx - ax * vz;
                double ez = ax * vy - ay * vx;
                gx += ProportionalGain * ex;
                gy += ProportionalGain * ey;
                gz += ProportionalGain * ez;
            }
            double halfDelta = delta * 0.5;
            double qw = quaternion.W;
            double qx = quaternion.X;
            double qy = quaternion.Y;
            double qz = quaternion.Z;
            quaternion.W += (-qx * gx - qy * gy - qz * gz) * halfDelta;
            quaternion.X += (qw * gx + qy * gz - qz * gy) * halfDelta;
            quaternion.Y += (qw * gy - qx * gz + qz * gx) * halfDelta;
            quaternion.Z += (qw * gz + qx * gy - qy * gx) * halfDelta;
            quaternion.Normalize();
        }

        private void UpdateSnapshot(MotionSample sample, MotionCalibrationResult calibration, DateTime now, MotionTrackingQuality quality)
        {
            double pitch;
            double roll;
            double yaw;
            ToEuler(quaternion, out pitch, out roll, out yaw);
            if (hasCenter)
            {
                pitch = WrapDegrees(pitch - centerPitch);
                roll = WrapDegrees(roll - centerRoll);
                yaw = WrapDegrees(yaw - centerYaw);
            }
            snapshot.Quaternion = quaternion;
            snapshot.Pitch = FiniteOrZero(pitch);
            snapshot.Roll = FiniteOrZero(roll);
            snapshot.Yaw = FiniteOrZero(yaw);
            snapshot.TimestampUtc = sample.TimestampUtc;
            snapshot.Sequence = sample.Sequence;
            snapshot.CalibrationState = calibration == null ? MotionCalibrationState.NotCalibrated : (calibration.IsValid ? MotionCalibrationState.Calibrated : MotionCalibrationState.NotCalibrated);
            snapshot.TrackingQuality = quality;
            snapshot.HasPose = true;
        }

        private static MotionTrackingQuality MotionTrackingQualityFor(MotionCalibrationResult calibration, MotionSample sample, DateTime now)
        {
            if (sample == null || !sample.IsValid) return MotionTrackingQuality.Unsupported;
            if ((now - sample.TimestampUtc).TotalMilliseconds > 250) return MotionTrackingQuality.DataInterrupted;
            if (calibration == null || !calibration.IsValid) return MotionTrackingQuality.Uncalibrated;
            double magnitude = Math.Sqrt(sample.AccelX * sample.AccelX + sample.AccelY * sample.AccelY + sample.AccelZ * sample.AccelZ);
            return magnitude < 0.72 || magnitude > 1.30 ? MotionTrackingQuality.DataJitter : MotionTrackingQuality.Good;
        }

        private static void ToEuler(MotionQuaternion value, out double pitch, out double roll, out double yaw)
        {
            double sinPitch = 2.0 * (value.W * value.Y - value.Z * value.X);
            sinPitch = Math.Max(-1.0, Math.Min(1.0, sinPitch));
            pitch = Math.Asin(sinPitch) * RadiansToDegrees;
            roll = Math.Atan2(2.0 * (value.W * value.X + value.Y * value.Z), 1.0 - 2.0 * (value.X * value.X + value.Y * value.Y)) * RadiansToDegrees;
            yaw = Math.Atan2(2.0 * (value.W * value.Z + value.X * value.Y), 1.0 - 2.0 * (value.Y * value.Y + value.Z * value.Z)) * RadiansToDegrees;
        }

        private static double WrapDegrees(double value)
        {
            while (value > 180) value -= 360;
            while (value < -180) value += 360;
            return value;
        }

        private static double FiniteOrZero(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
        }
    }

    public sealed class MotionViewState
    {
        public string DeviceId = string.Empty;
        public bool IsAvailable;
        public string AvailabilityMessage = string.Empty;
        public MotionFusionSnapshot Pose = new MotionFusionSnapshot { Quaternion = MotionQuaternion.Identity };
        public MotionCalibrationResult Calibration = new MotionCalibrationResult();
        public MotionSample Sample;
        public MotionCalibrationState CalibrationState;
        public MotionTrackingQuality TrackingQuality;
        public double UpdatesPerSecond;
        public DateTime LastUpdatedUtc;
        public bool SmoothingEnabled = true;

        public MotionViewState Copy()
        {
            return new MotionViewState
            {
                DeviceId = DeviceId,
                IsAvailable = IsAvailable,
                AvailabilityMessage = AvailabilityMessage,
                Pose = Pose == null ? null : Pose.Copy(),
                Calibration = Calibration == null ? null : Calibration.Copy(),
                Sample = Sample == null ? null : Sample.Copy(),
                CalibrationState = CalibrationState,
                TrackingQuality = TrackingQuality,
                UpdatesPerSecond = UpdatesPerSecond,
                LastUpdatedUtc = LastUpdatedUtc,
                SmoothingEnabled = SmoothingEnabled
            };
        }
    }

    internal sealed class MotionHistoryBuffer
    {
        private const int Capacity = 240;
        private readonly double[] pitch = new double[Capacity];
        private readonly double[] roll = new double[Capacity];
        private readonly double[] yaw = new double[Capacity];
        private int next;
        private int count;

        public void Add(double valuePitch, double valueRoll, double valueYaw)
        {
            pitch[next] = valuePitch;
            roll[next] = valueRoll;
            yaw[next] = valueYaw;
            next = (next + 1) % Capacity;
            if (count < Capacity) count++;
        }

        public void Clear()
        {
            next = 0;
            count = 0;
        }
    }

    internal sealed class DualSenseMotionSession
    {
        private readonly object sync = new object();
        private readonly MotionFusionService fusion = new MotionFusionService();
        private readonly MotionCalibrationService calibration = new MotionCalibrationService();
        private readonly MotionHistoryBuffer history = new MotionHistoryBuffer();
        private long lastSequence = -1;
        private DateTime rateWindowStarted = DateTime.UtcNow;
        private int reportsInWindow;
        private double updatesPerSecond;
        private readonly MotionViewState state = new MotionViewState();

        public void Update(ControllerState controller, DateTime now)
        {
            lock (sync)
            {
                MotionSample sample = controller == null || controller.DualSense == null ? null : controller.DualSense.Motion;
                if (controller == null || !controller.IsConnected || controller.ControllerType != ControllerType.DualSense || controller.InputSource != ControllerInputSource.DualSenseHid || sample == null || !sample.IsValid)
                {
                    MarkUnavailable(controller, sample);
                    return;
                }
                state.DeviceId = controller.DeviceId ?? string.Empty;
                state.IsAvailable = true;
                state.AvailabilityMessage = string.Empty;
                if (sample.Sequence == lastSequence) return;
                lastSequence = sample.Sequence;
                calibration.Update(state.DeviceId, sample, now);
                state.Pose = fusion.Update(sample, calibration.Result, now);
                state.Calibration = calibration.Result.Copy();
                state.CalibrationState = calibration.State;
                state.TrackingQuality = state.Pose.TrackingQuality;
                state.Sample = sample.Copy();
                state.LastUpdatedUtc = now;
                history.Add(state.Pose.Pitch, state.Pose.Roll, state.Pose.Yaw);
                reportsInWindow++;
                double elapsed = (now - rateWindowStarted).TotalSeconds;
                if (elapsed >= 0.5)
                {
                    updatesPerSecond = reportsInWindow / elapsed;
                    reportsInWindow = 0;
                    rateWindowStarted = now;
                }
                state.UpdatesPerSecond = updatesPerSecond;
            }
        }

        public bool StartCalibration(string deviceId, DateTime now, out string reason)
        {
            lock (sync)
            {
                if (!state.IsAvailable || !string.Equals(deviceId, state.DeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "当前设备或输入模式未提供运动传感器数据。";
                    return false;
                }
                bool started = calibration.Start(deviceId, now, out reason);
                state.CalibrationState = calibration.State;
                state.Calibration = calibration.Result.Copy();
                return started;
            }
        }

        public bool Recenter(string deviceId, out string reason)
        {
            lock (sync)
            {
                if (!state.IsAvailable || !state.Pose.HasPose || !string.Equals(deviceId, state.DeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "尚未收到有效的 DualSense 运动数据。";
                    return false;
                }
                fusion.Recenter();
                reason = string.Empty;
                return true;
            }
        }

        public void Reset()
        {
            lock (sync)
            {
                fusion.Reset();
                calibration.Reset();
                history.Clear();
                lastSequence = -1;
                state.Pose = new MotionFusionSnapshot { Quaternion = MotionQuaternion.Identity, CalibrationState = MotionCalibrationState.NotCalibrated, TrackingQuality = MotionTrackingQuality.Uncalibrated };
                state.Calibration = new MotionCalibrationResult();
                state.CalibrationState = MotionCalibrationState.NotCalibrated;
                state.TrackingQuality = MotionTrackingQuality.Uncalibrated;
            }
        }

        public MotionViewState Get(DateTime now)
        {
            lock (sync)
            {
                if (state.IsAvailable && state.Sample != null && (now - state.Sample.TimestampUtc).TotalMilliseconds > 250)
                {
                    state.TrackingQuality = MotionTrackingQuality.DataInterrupted;
                    if (state.Pose != null) state.Pose.TrackingQuality = MotionTrackingQuality.DataInterrupted;
                }
                return state.Copy();
            }
        }

        private void MarkUnavailable(ControllerState controller, MotionSample sample)
        {
            bool wasCalibrating = calibration.State == MotionCalibrationState.Settling || calibration.State == MotionCalibrationState.Sampling;
            string reason;
            if (controller == null || !controller.IsConnected) reason = "当前设备或输入模式未提供运动传感器数据。";
            else if (controller.ControllerType != ControllerType.DualSense) reason = "当前设备或输入模式未提供运动传感器数据。";
            else if (controller.InputSource != ControllerInputSource.DualSenseHid) reason = "动态演示或非原生输入不会生成姿态数据。";
            else if (sample != null && !string.IsNullOrEmpty(sample.AvailabilityMessage)) reason = sample.AvailabilityMessage;
            else reason = "当前连接模式未提供完整的 DualSense 运动传感器报告。";
            if (wasCalibrating) calibration.MarkUnavailable("校准已中断：" + reason);
            fusion.Reset();
            history.Clear();
            lastSequence = -1;
            state.IsAvailable = false;
            state.AvailabilityMessage = reason;
            state.Sample = sample == null ? null : sample.Copy();
            state.Calibration = calibration.Result.Copy();
            state.CalibrationState = calibration.State == MotionCalibrationState.Failed ? MotionCalibrationState.Failed : MotionCalibrationState.Unsupported;
            state.TrackingQuality = MotionTrackingQuality.Unsupported;
            state.Pose = new MotionFusionSnapshot { Quaternion = MotionQuaternion.Identity, CalibrationState = state.CalibrationState, TrackingQuality = MotionTrackingQuality.Unsupported, HasPose = false };
            state.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public sealed class DualSenseMotionManager
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, DualSenseMotionSession> sessions = new Dictionary<string, DualSenseMotionSession>(StringComparer.OrdinalIgnoreCase);

        public void Synchronize(ControllerState[] controllers)
        {
            DateTime now = DateTime.UtcNow;
            HashSet<string> online = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (controllers != null)
            {
                for (int i = 0; i < controllers.Length; i++)
                {
                    ControllerState controller = controllers[i];
                    if (controller == null || controller.ControllerType != ControllerType.DualSense || string.IsNullOrEmpty(controller.DeviceId)) continue;
                    online.Add(controller.DeviceId);
                    DualSenseMotionSession session;
                    lock (sync)
                    {
                        if (!sessions.TryGetValue(controller.DeviceId, out session))
                        {
                            session = new DualSenseMotionSession();
                            sessions[controller.DeviceId] = session;
                        }
                    }
                    session.Update(controller, now);
                }
            }
            lock (sync)
            {
                List<string> removed = new List<string>();
                foreach (KeyValuePair<string, DualSenseMotionSession> item in sessions) if (!online.Contains(item.Key)) removed.Add(item.Key);
                for (int i = 0; i < removed.Count; i++)
                {
                    sessions[removed[i]].Reset();
                    sessions.Remove(removed[i]);
                }
            }
        }

        public MotionViewState Get(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return new MotionViewState { AvailabilityMessage = "当前设备或输入模式未提供运动传感器数据。", CalibrationState = MotionCalibrationState.Unsupported, TrackingQuality = MotionTrackingQuality.Unsupported };
            lock (sync)
            {
                DualSenseMotionSession session;
                if (!sessions.TryGetValue(deviceId, out session)) return new MotionViewState { DeviceId = deviceId, AvailabilityMessage = "当前设备或输入模式未提供运动传感器数据。", CalibrationState = MotionCalibrationState.Unsupported, TrackingQuality = MotionTrackingQuality.Unsupported };
                return session.Get(DateTime.UtcNow);
            }
        }

        public bool StartCalibration(string deviceId, out string reason)
        {
            lock (sync)
            {
                DualSenseMotionSession session;
                if (!sessions.TryGetValue(deviceId ?? string.Empty, out session))
                {
                    reason = "当前设备或输入模式未提供运动传感器数据。";
                    return false;
                }
                return session.StartCalibration(deviceId, DateTime.UtcNow, out reason);
            }
        }

        public bool Recenter(string deviceId, out string reason)
        {
            lock (sync)
            {
                DualSenseMotionSession session;
                if (!sessions.TryGetValue(deviceId ?? string.Empty, out session))
                {
                    reason = "尚未收到有效的 DualSense 运动数据。";
                    return false;
                }
                return session.Recenter(deviceId, out reason);
            }
        }

        public void Reset(string deviceId)
        {
            lock (sync)
            {
                DualSenseMotionSession session;
                if (sessions.TryGetValue(deviceId ?? string.Empty, out session)) session.Reset();
            }
        }
    }

    public static class DualSenseMotionSelfTest
    {
        public static string Run()
        {
            List<string> passed = new List<string>();
            VerifyStationaryAndQuaternion(passed);
            VerifyAxisIntegration(passed);
            VerifyTimingAndGravityCorrection(passed);
            VerifyCalibration(passed);
            VerifyRecenteringAndRecovery(passed);
            passed.Add(SonyInputManager.RunMotionParserSelfTest());
            return "DualSense motion self-test passed: " + string.Join(", ", passed.ToArray());
        }

        private static void VerifyStationaryAndQuaternion(List<string> passed)
        {
            MotionFusionService fusion = new MotionFusionService();
            MotionCalibrationResult calibration = new MotionCalibrationResult { IsValid = true };
            DateTime now = DateTime.UtcNow;
            MotionFusionSnapshot result = null;
            for (int i = 0; i < 180; i++) result = fusion.Update(Sample(now.AddMilliseconds(i * 8), i, 0, 0, 0, 0, 0, 1), calibration, now.AddMilliseconds(i * 8));
            double norm = Math.Sqrt(result.Quaternion.W * result.Quaternion.W + result.Quaternion.X * result.Quaternion.X + result.Quaternion.Y * result.Quaternion.Y + result.Quaternion.Z * result.Quaternion.Z);
            Require(result.HasPose && Math.Abs(result.Pitch) < 1 && Math.Abs(result.Roll) < 1 && Math.Abs(norm - 1) < 0.0001, "stationary orientation or quaternion normalization failed");
            passed.Add("stationary-flat-and-normalized-quaternion");
        }

        private static void VerifyAxisIntegration(List<string> passed)
        {
            VerifyAxis(90, 0, 0, "x-axis-rotation", passed);
            VerifyAxis(0, 90, 0, "y-axis-rotation", passed);
            VerifyAxis(0, 0, 90, "z-axis-rotation", passed);
        }

        private static void VerifyAxis(double x, double y, double z, string label, List<string> passed)
        {
            MotionFusionService fusion = new MotionFusionService();
            MotionCalibrationResult calibration = new MotionCalibrationResult { IsValid = true };
            DateTime now = DateTime.UtcNow;
            MotionFusionSnapshot result = null;
            for (int i = 0; i < 100; i++) result = fusion.Update(Sample(now.AddMilliseconds(i * 10), i, x, y, z, 0, 0, 1), calibration, now.AddMilliseconds(i * 10));
            Require(Math.Abs(result.Pitch) > 12 || Math.Abs(result.Roll) > 12 || Math.Abs(result.Yaw) > 12, label + " did not integrate");
            passed.Add(label);
        }

        private static void VerifyTimingAndGravityCorrection(List<string> passed)
        {
            MotionFusionService fusion = new MotionFusionService();
            MotionCalibrationResult calibration = new MotionCalibrationResult { IsValid = true };
            DateTime now = DateTime.UtcNow;
            MotionFusionSnapshot result = fusion.Update(Sample(now, 1, 0, 0, 0, 0, 0, 1), calibration, now);
            result = fusion.Update(Sample(now.AddSeconds(1), 2, 600, 0, 0, 0, 0, 1), calibration, now.AddSeconds(1));
            Require(Math.Abs(result.Roll) < 5, "oversized delta time was not rejected");
            for (int i = 0; i < 200; i++) result = fusion.Update(Sample(now.AddSeconds(1).AddMilliseconds(i * 8), 3 + i, 0, 0, 0, 0, 0.55, 0.83), calibration, now.AddSeconds(1).AddMilliseconds(i * 8));
            Require(!double.IsNaN(result.Pitch) && !double.IsInfinity(result.Pitch), "gravity correction emitted invalid angles");
            passed.Add("irregular-and-oversized-delta-rejected");
            passed.Add("accelerometer-gravity-correction");
        }

        private static void VerifyCalibration(List<string> passed)
        {
            MotionCalibrationService calibration = new MotionCalibrationService();
            DateTime now = DateTime.UtcNow;
            string reason;
            Require(calibration.Start("selftest", now, out reason), "calibration did not start");
            for (int i = 0; i < 560; i++) calibration.Update("selftest", Sample(now.AddMilliseconds(i * 8), i, 0.8, -0.4, 0.2, 0, 0, 1), now.AddMilliseconds(i * 8));
            calibration.Update("selftest", Sample(now.AddSeconds(4.1), 570, 0.8, -0.4, 0.2, 0, 0, 1), now.AddSeconds(4.1));
            Require(calibration.Result.IsValid && Math.Abs(calibration.Result.BiasX - 0.8) < 0.2, "stationary calibration failed");
            passed.Add("stationary-calibration");

            calibration = new MotionCalibrationService();
            Require(calibration.Start("selftest", now, out reason), "movement calibration did not start");
            for (int i = 0; i < 560; i++) calibration.Update("selftest", Sample(now.AddMilliseconds(i * 8), i, 9, 0, 0, 0, 0, 1), now.AddMilliseconds(i * 8));
            calibration.Update("selftest", Sample(now.AddSeconds(4.1), 570, 9, 0, 0, 0, 0, 1), now.AddSeconds(4.1));
            Require(!calibration.Result.IsValid && calibration.State == MotionCalibrationState.Failed, "moving calibration must fail");
            passed.Add("calibration-movement-rejected");
        }

        private static void VerifyRecenteringAndRecovery(List<string> passed)
        {
            MotionFusionService fusion = new MotionFusionService();
            MotionCalibrationResult calibration = new MotionCalibrationResult { IsValid = true };
            DateTime now = DateTime.UtcNow;
            MotionFusionSnapshot result = null;
            for (int i = 0; i < 80; i++) result = fusion.Update(Sample(now.AddMilliseconds(i * 10), i, 0, 0, 60, 0, 0, 1), calibration, now.AddMilliseconds(i * 10));
            fusion.Recenter();
            result = fusion.Update(Sample(now.AddMilliseconds(820), 90, 0, 0, 0, 0, 0, 1), calibration, now.AddMilliseconds(820));
            Require(Math.Abs(result.Yaw) < 3, "recenter did not zero the display orientation");
            result = fusion.Update(Sample(now.AddSeconds(2), 91, 0, 0, 0, 0, 0, 1), calibration, now.AddSeconds(2));
            Require(result.TrackingQuality == MotionTrackingQuality.DataInterrupted, "data interruption was not surfaced");
            passed.Add("recenter-and-interruption-recovery");
        }

        private static MotionSample Sample(DateTime time, long sequence, double gyroX, double gyroY, double gyroZ, double accelX, double accelY, double accelZ)
        {
            return new MotionSample
            {
                TimestampUtc = time,
                Sequence = sequence,
                GyroX = gyroX,
                GyroY = gyroY,
                GyroZ = gyroZ,
                AccelX = accelX,
                AccelY = accelY,
                AccelZ = accelZ,
                IsValid = true,
                SourceReportId = 1,
                CrcValidated = true
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("DualSense motion self-test failed: " + message);
        }
    }
}
