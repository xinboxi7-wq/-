using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ControllerLab
{
    public enum ControllerRumblePattern
    {
        Manual,
        LeftOnly,
        RightOnly,
        Balanced,
        Ramp,
        Pulse,
        Alternating
    }

    public interface IControllerRumbleService : IDisposable
    {
        string DeviceId { get; }
        bool IsSupported { get; }
        string SupportDetails { get; }
        bool TrySetRumble(double leftStrength, double rightStrength, out string error);
        void StopRumble();
    }

    public sealed class RumbleStatusSnapshot
    {
        public string DeviceId = string.Empty;
        public bool IsSupported;
        public string SupportDetails = "当前设备不支持震动";
        public bool IsRunning;
        public double LeftStrength;
        public double RightStrength;
        public double RemainingSeconds;
        public ControllerRumblePattern Pattern;
        public string PatternLabel = "未运行";
        public string Status = "等待开始";
        public bool LastOutputSucceeded;
        public DateTime LastStoppedUtc = DateTime.MinValue;
    }

    public sealed class XInputRumbleService : IControllerRumbleService
    {
        private readonly InputManager input;
        private readonly int playerIndex;
        private readonly bool supportsLeft;
        private readonly bool supportsRight;

        public XInputRumbleService(InputManager input, ControllerState state)
        {
            this.input = input;
            playerIndex = state == null ? -1 : state.PlayerIndex;
            DeviceId = state == null ? string.Empty : state.DeviceId;
            string ignored;
            if (input == null || !input.TryGetVibrationCapabilities(playerIndex, out supportsLeft, out supportsRight, out ignored))
            {
                supportsLeft = input != null && input.CanSetVibration;
                supportsRight = input != null && input.CanSetVibration;
            }
        }

        public string DeviceId { get; private set; }
        public bool IsSupported { get { return input != null && input.CanSetVibration && playerIndex >= 0 && playerIndex < 4 && (supportsLeft || supportsRight); } }
        public string SupportDetails
        {
            get
            {
                return IsSupported
                    ? string.Format(CultureInfo.InvariantCulture, "Xbox XInput：左侧低频 {0}，右侧高频 {1}", supportsLeft ? "可用" : "不支持", supportsRight ? "可用" : "不支持")
                    : "当前 XInput 库未提供 XInputSetState，无法输出震动";
            }
        }

        public bool TrySetRumble(double leftStrength, double rightStrength, out string error)
        {
            if (!IsSupported)
            {
                error = SupportDetails;
                return false;
            }
            if (leftStrength > 0 && !supportsLeft)
            {
                error = "当前 XInput 手柄不支持左侧低频电机";
                return false;
            }
            if (rightStrength > 0 && !supportsRight)
            {
                error = "当前 XInput 手柄不支持右侧高频电机";
                return false;
            }
            return input.TrySetVibration(playerIndex, StrengthToMotor(leftStrength), StrengthToMotor(rightStrength), out error);
        }

        public void StopRumble()
        {
            string ignored;
            if (input != null && playerIndex >= 0 && playerIndex < 4) input.TrySetVibration(playerIndex, 0, 0, out ignored);
        }

        public void Dispose()
        {
            StopRumble();
        }

        public static ushort StrengthToMotor(double strength)
        {
            double safe = Math.Max(0, Math.Min(1, strength));
            return (ushort)Math.Round(safe * ushort.MaxValue, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class DualSenseRumbleService : IControllerRumbleService
    {
        private readonly SonyInputManager input;
        private readonly ControllerConnectionType connectionType;
        private byte sequence;

        public DualSenseRumbleService(SonyInputManager input, ControllerState state)
        {
            this.input = input;
            DeviceId = state == null ? string.Empty : state.DeviceId;
            connectionType = state == null ? ControllerConnectionType.Unknown : state.ConnectionType;
        }

        public string DeviceId { get; private set; }
        public bool IsSupported
        {
            get
            {
                return input != null
                    && !string.IsNullOrEmpty(DeviceId)
                    && (connectionType == ControllerConnectionType.Wired || connectionType == ControllerConnectionType.Bluetooth);
            }
        }

        public string SupportDetails
        {
            get
            {
                if (connectionType == ControllerConnectionType.Wired)
                    return "DualSense USB 基础双通道输出已接入；当前连接模式的震动输出尚待实机验证";
                if (connectionType == ControllerConnectionType.Bluetooth)
                    return "DualSense 蓝牙 0x31 输出与 CRC 已接入；当前连接模式的震动输出尚待实机验证";
                return "当前 DualSense 连接模式暂不支持震动输出";
            }
        }

        public bool TrySetRumble(double leftStrength, double rightStrength, out string error)
        {
            if (!IsSupported)
            {
                error = SupportDetails;
                return false;
            }
            byte[] report = DualSenseOutputReportBuilder.Build(connectionType, leftStrength, rightStrength, sequence++);
            if (report == null)
            {
                error = "无法为当前连接模式构造 DualSense 输出报告";
                return false;
            }
            return input.TryWriteOutputReport(DeviceId, report, out error);
        }

        public void StopRumble()
        {
            if (input == null || string.IsNullOrEmpty(DeviceId)) return;
            byte[] report = DualSenseOutputReportBuilder.Build(connectionType, 0, 0, sequence++);
            if (report == null) return;
            string ignored;
            input.TryWriteOutputReport(DeviceId, report, out ignored);
        }

        public void Dispose()
        {
            StopRumble();
        }
    }

    public sealed class UnsupportedRumbleService : IControllerRumbleService
    {
        public UnsupportedRumbleService(ControllerState state, string details)
        {
            DeviceId = state == null ? string.Empty : state.DeviceId;
            SupportDetails = string.IsNullOrEmpty(details) ? "当前设备不支持震动" : details;
        }

        public string DeviceId { get; private set; }
        public bool IsSupported { get { return false; } }
        public string SupportDetails { get; private set; }

        public bool TrySetRumble(double leftStrength, double rightStrength, out string error)
        {
            error = SupportDetails;
            return false;
        }

        public void StopRumble() { }
        public void Dispose() { }
    }

    public static class ControllerRumbleServiceFactory
    {
        public static IControllerRumbleService Create(InputManager xbox, SonyInputManager sony, ControllerState state)
        {
            if (state == null || !state.IsConnected)
                return new UnsupportedRumbleService(state, "设备未连接");
            if (!state.HasRealInput)
                return new UnsupportedRumbleService(state, "动态演示和构造自检数据不会发送真实震动");
            if (state.ControllerType == ControllerType.Xbox)
                return new XInputRumbleService(xbox, state);
            if (state.ControllerType == ControllerType.DualSense)
                return new DualSenseRumbleService(sony, state);
            return new UnsupportedRumbleService(state, "当前设备类型不支持震动");
        }
    }

    public static class DualSenseOutputReportBuilder
    {
        public const int UsbReportLength = 63;
        public const int BluetoothReportLength = 78;

        public static byte[] Build(ControllerConnectionType connectionType, double leftStrength, double rightStrength, byte sequence)
        {
            byte left = StrengthToByte(leftStrength);
            byte right = StrengthToByte(rightStrength);
            if (connectionType == ControllerConnectionType.Wired)
            {
                byte[] usb = new byte[UsbReportLength];
                usb[0] = 0x02;
                WriteCommon(usb, 1, left, right);
                return usb;
            }
            if (connectionType == ControllerConnectionType.Bluetooth)
            {
                byte[] bluetooth = new byte[BluetoothReportLength];
                bluetooth[0] = 0x31;
                bluetooth[1] = (byte)((sequence & 0x0F) << 4);
                bluetooth[2] = 0x10;
                WriteCommon(bluetooth, 3, left, right);
                WriteBluetoothCrc(bluetooth);
                return bluetooth;
            }
            return null;
        }

        private static void WriteCommon(byte[] report, int commonOffset, byte left, byte right)
        {
            // Linux hid-playstation dualsense_output_report_common:
            // valid_flag0, valid_flag1, motor_right, motor_left.
            report[commonOffset] = 0x03; // haptics select + compatible vibration
            report[commonOffset + 1] = 0x00;
            report[commonOffset + 2] = right;
            report[commonOffset + 3] = left;
        }

        public static byte StrengthToByte(double strength)
        {
            double safe = Math.Max(0, Math.Min(1, strength));
            return (byte)Math.Round(safe * byte.MaxValue, MidpointRounding.AwayFromZero);
        }

        private static void WriteBluetoothCrc(byte[] report)
        {
            uint crc = 0xFFFFFFFF;
            crc = Crc32Le(crc, 0xA2);
            for (int i = 0; i < report.Length - 4; i++) crc = Crc32Le(crc, report[i]);
            uint final = ~crc;
            int offset = report.Length - 4;
            report[offset] = (byte)(final & 0xFF);
            report[offset + 1] = (byte)((final >> 8) & 0xFF);
            report[offset + 2] = (byte)((final >> 16) & 0xFF);
            report[offset + 3] = (byte)((final >> 24) & 0xFF);
        }

        public static bool HasValidBluetoothCrc(byte[] report)
        {
            if (report == null || report.Length != BluetoothReportLength) return false;
            uint crc = 0xFFFFFFFF;
            crc = Crc32Le(crc, 0xA2);
            for (int i = 0; i < report.Length - 4; i++) crc = Crc32Le(crc, report[i]);
            int offset = report.Length - 4;
            uint expected = (uint)(report[offset] | (report[offset + 1] << 8) | (report[offset + 2] << 16) | (report[offset + 3] << 24));
            return ~crc == expected;
        }

        private static uint Crc32Le(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320U : crc >> 1;
            return crc;
        }
    }

    public sealed class ControllerRumbleController : IDisposable
    {
        public const double DefaultStrength = 0.40;
        public const double DefaultDurationSeconds = 5.0;
        public const double MaximumDurationSeconds = 30.0;

        private readonly object sync = new object();
        private readonly InputManager xbox;
        private readonly SonyInputManager sony;
        private IControllerRumbleService service;
        private CancellationTokenSource cancellation;
        private Task activeTask;
        private int generation;
        private string deviceId = string.Empty;
        private bool running;
        private double currentLeft;
        private double currentRight;
        private double remainingSeconds;
        private ControllerRumblePattern pattern;
        private string status = "等待开始";
        private bool lastOutputSucceeded;
        private DateTime lastStoppedUtc = DateTime.MinValue;

        public ControllerRumbleController(InputManager xbox, SonyInputManager sony)
        {
            this.xbox = xbox;
            this.sony = sony;
            service = new UnsupportedRumbleService(null, "设备未连接");
        }

        internal ControllerRumbleController(IControllerRumbleService testService)
        {
            service = testService ?? new UnsupportedRumbleService(null, "selftest service unavailable");
            deviceId = service.DeviceId;
        }

        public bool IsRunning { get { lock (sync) return running; } }
        public DateTime LastStoppedUtc { get { lock (sync) return lastStoppedUtc; } }

        public void Synchronize(ControllerState state)
        {
            string nextId = state == null || !state.IsConnected ? string.Empty : state.DeviceId;
            bool changed;
            lock (sync) changed = !string.Equals(deviceId, nextId, StringComparison.OrdinalIgnoreCase);
            if (!changed) return;

            Stop(changed ? "设备已切换，震动已停止" : "设备已断开，震动已停止");
            IControllerRumbleService old;
            lock (sync)
            {
                old = service;
                service = ControllerRumbleServiceFactory.Create(xbox, sony, state);
                deviceId = nextId;
                status = state == null || !state.IsConnected ? "设备未连接" : "等待开始";
                lastOutputSucceeded = false;
            }
            if (old != null) old.Dispose();
        }

        public bool Start(ControllerRumblePattern requestedPattern, double leftStrength, double rightStrength, double overallStrength, double durationSeconds, out string error)
        {
            error = null;
            IControllerRumbleService current;
            lock (sync) current = service;
            if (current == null || !current.IsSupported)
            {
                error = current == null ? "当前设备不支持震动" : current.SupportDetails;
                lock (sync) status = error;
                return false;
            }

            double left = Clamp01(leftStrength) * Clamp01(overallStrength);
            double right = Clamp01(rightStrength) * Clamp01(overallStrength);
            if (requestedPattern == ControllerRumblePattern.LeftOnly) right = 0;
            if (requestedPattern == ControllerRumblePattern.RightOnly) left = 0;
            if (requestedPattern == ControllerRumblePattern.Balanced)
            {
                left = 0.5 * Clamp01(overallStrength);
                right = 0.5 * Clamp01(overallStrength);
            }
            double duration = Math.Max(0.1, Math.Min(MaximumDurationSeconds, durationSeconds));

            Stop("已切换震动模式");
            int ownGeneration;
            CancellationTokenSource ownCancellation = new CancellationTokenSource();
            lock (sync)
            {
                generation++;
                ownGeneration = generation;
                cancellation = ownCancellation;
                running = true;
                pattern = requestedPattern;
                currentLeft = 0;
                currentRight = 0;
                remainingSeconds = duration;
                status = "正在启动震动";
                lastOutputSucceeded = false;
                activeTask = Task.Run(() => PlayAsync(current, requestedPattern, left, right, duration, ownGeneration, ownCancellation.Token));
            }
            return true;
        }

        public void Stop(string reason)
        {
            CancellationTokenSource oldCancellation;
            Task oldTask;
            IControllerRumbleService current;
            bool hadActiveOutput;
            lock (sync)
            {
                generation++;
                hadActiveOutput = running || currentLeft > 0 || currentRight > 0;
                oldCancellation = cancellation;
                oldTask = activeTask;
                cancellation = null;
                activeTask = null;
                running = false;
                currentLeft = 0;
                currentRight = 0;
                remainingSeconds = 0;
                status = string.IsNullOrEmpty(reason) ? "震动已停止" : reason;
                if (hadActiveOutput) lastStoppedUtc = DateTime.UtcNow;
                current = service;
            }
            if (oldCancellation != null) oldCancellation.Cancel();
            if (oldTask != null && !oldTask.IsCompleted)
            {
                try { oldTask.Wait(250); }
                catch (AggregateException) { }
            }
            try { if (current != null) current.StopRumble(); }
            catch { }
            if (oldCancellation != null) oldCancellation.Dispose();
        }

        public RumbleStatusSnapshot GetSnapshot()
        {
            lock (sync)
            {
                return new RumbleStatusSnapshot
                {
                    DeviceId = deviceId,
                    IsSupported = service != null && service.IsSupported,
                    SupportDetails = service == null ? "当前设备不支持震动" : service.SupportDetails,
                    IsRunning = running,
                    LeftStrength = currentLeft,
                    RightStrength = currentRight,
                    RemainingSeconds = remainingSeconds,
                    Pattern = pattern,
                    PatternLabel = PatternLabel(pattern),
                    Status = status,
                    LastOutputSucceeded = lastOutputSucceeded,
                    LastStoppedUtc = lastStoppedUtc
                };
            }
        }

        private async Task PlayAsync(IControllerRumbleService current, ControllerRumblePattern requestedPattern, double left, double right, double duration, int ownGeneration, CancellationToken token)
        {
            DateTime started = DateTime.UtcNow;
            try
            {
                if (requestedPattern == ControllerRumblePattern.Ramp)
                {
                    while ((DateTime.UtcNow - started).TotalSeconds < duration)
                    {
                        token.ThrowIfCancellationRequested();
                        double progress = Math.Min(1, (DateTime.UtcNow - started).TotalSeconds / duration);
                        Apply(current, left * progress, right * progress, duration - (DateTime.UtcNow - started).TotalSeconds, ownGeneration);
                        await Task.Delay(40, token).ConfigureAwait(false);
                    }
                }
                else if (requestedPattern == ControllerRumblePattern.Pulse)
                {
                    bool on = true;
                    while ((DateTime.UtcNow - started).TotalSeconds < duration)
                    {
                        token.ThrowIfCancellationRequested();
                        Apply(current, on ? left : 0, on ? right : 0, duration - (DateTime.UtcNow - started).TotalSeconds, ownGeneration);
                        on = !on;
                        await Task.Delay(200, token).ConfigureAwait(false);
                    }
                }
                else if (requestedPattern == ControllerRumblePattern.Alternating)
                {
                    bool leftTurn = true;
                    while ((DateTime.UtcNow - started).TotalSeconds < duration)
                    {
                        token.ThrowIfCancellationRequested();
                        Apply(current, leftTurn ? left : 0, leftTurn ? 0 : right, duration - (DateTime.UtcNow - started).TotalSeconds, ownGeneration);
                        await Task.Delay(200, token).ConfigureAwait(false);
                        Apply(current, 0, 0, duration - (DateTime.UtcNow - started).TotalSeconds, ownGeneration);
                        await Task.Delay(100, token).ConfigureAwait(false);
                        leftTurn = !leftTurn;
                    }
                }
                else
                {
                    Apply(current, left, right, duration, ownGeneration);
                    await Task.Delay(TimeSpan.FromSeconds(duration), token).ConfigureAwait(false);
                }
                FinishGeneration(current, ownGeneration, "预设已完成，震动已自动停止", true);
            }
            catch (OperationCanceledException)
            {
                FinishGeneration(current, ownGeneration, "震动已取消并停止", false);
            }
            catch (Exception ex)
            {
                FinishGeneration(current, ownGeneration, "震动输出失败，已自动停止：" + ex.Message, false);
            }
        }

        private void Apply(IControllerRumbleService current, double left, double right, double remaining, int ownGeneration)
        {
            string error;
            if (!current.TrySetRumble(left, right, out error)) throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "未知输出错误" : error);
            lock (sync)
            {
                if (generation != ownGeneration) return;
                currentLeft = left;
                currentRight = right;
                remainingSeconds = Math.Max(0, remaining);
                status = "震动输出中";
                lastOutputSucceeded = true;
            }
        }

        private void FinishGeneration(IControllerRumbleService current, int ownGeneration, string message, bool succeeded)
        {
            try { current.StopRumble(); }
            catch { succeeded = false; }
            CancellationTokenSource completedCancellation = null;
            lock (sync)
            {
                if (generation != ownGeneration) return;
                running = false;
                currentLeft = 0;
                currentRight = 0;
                remainingSeconds = 0;
                status = message;
                lastOutputSucceeded = succeeded;
                lastStoppedUtc = DateTime.UtcNow;
                completedCancellation = cancellation;
                cancellation = null;
                activeTask = null;
            }
            if (completedCancellation != null) completedCancellation.Dispose();
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0, Math.Min(1, value));
        }

        public static string PatternLabel(ControllerRumblePattern value)
        {
            switch (value)
            {
                case ControllerRumblePattern.LeftOnly: return "左侧低频";
                case ControllerRumblePattern.RightOnly: return "右侧高频";
                case ControllerRumblePattern.Balanced: return "均衡震动";
                case ControllerRumblePattern.Ramp: return "渐强";
                case ControllerRumblePattern.Pulse: return "脉冲";
                case ControllerRumblePattern.Alternating: return "左右交替";
                default: return "自定义";
            }
        }

        public void Dispose()
        {
            Stop("应用退出，震动已停止");
            IControllerRumbleService current;
            lock (sync)
            {
                current = service;
                service = null;
            }
            if (current != null) current.Dispose();
        }
    }

    public static class ControllerRumbleSelfTest
    {
        public static string Run()
        {
            List<string> passed = new List<string>();
            Require(XInputRumbleService.StrengthToMotor(0) == 0, "XInput 0% mapping failed");
            Require(XInputRumbleService.StrengthToMotor(1) == ushort.MaxValue, "XInput 100% mapping failed");
            Require(XInputRumbleService.StrengthToMotor(0.5) >= 32767 && XInputRumbleService.StrengthToMotor(0.5) <= 32768, "XInput 50% mapping failed");
            passed.Add("xinput-strength-map");

            byte[] usb = DualSenseOutputReportBuilder.Build(ControllerConnectionType.Wired, 1, 0.25, 0);
            Require(usb != null && usb.Length == 63 && usb[0] == 0x02, "DualSense USB report header failed");
            Require(usb[1] == 0x03 && usb[3] == DualSenseOutputReportBuilder.StrengthToByte(0.25) && usb[4] == 255, "DualSense USB motor order failed");
            passed.Add("dualsense-usb-0x02-63");

            byte[] bluetooth = DualSenseOutputReportBuilder.Build(ControllerConnectionType.Bluetooth, 0.25, 1, 7);
            Require(bluetooth != null && bluetooth.Length == 78 && bluetooth[0] == 0x31 && bluetooth[1] == 0x70 && bluetooth[2] == 0x10, "DualSense Bluetooth report header failed");
            Require(bluetooth[5] == 255 && bluetooth[6] == DualSenseOutputReportBuilder.StrengthToByte(0.25), "DualSense Bluetooth motor order failed");
            Require(DualSenseOutputReportBuilder.HasValidBluetoothCrc(bluetooth), "DualSense Bluetooth CRC failed");
            bluetooth[20] ^= 0x01;
            Require(!DualSenseOutputReportBuilder.HasValidBluetoothCrc(bluetooth), "DualSense Bluetooth CRC rejection failed");
            passed.Add("dualsense-bt-0x31-78-crc");

            FakeRumbleService fake = new FakeRumbleService();
            string error;
            Require(fake.TrySetRumble(1, 0, out error) && fake.Left == 1 && fake.Right == 0, "left channel failed");
            Require(fake.TrySetRumble(0, 1, out error) && fake.Left == 0 && fake.Right == 1, "right channel failed");
            fake.StopRumble();
            Require(fake.Left == 0 && fake.Right == 0 && fake.StopCount == 1, "stop command failed");
            passed.Add("channel-and-stop");

            ControllerRumbleController controller = new ControllerRumbleController(fake);
            Require(controller.Start(ControllerRumblePattern.Manual, 0.4, 0.2, 1, 0.12, out error), "manual pattern did not start");
            Thread.Sleep(300);
            Require(!controller.IsRunning && fake.Left == 0 && fake.Right == 0, "preset completion did not stop");
            int stopsAfterCompletion = fake.StopCount;
            Require(controller.Start(ControllerRumblePattern.Pulse, 0.4, 0.4, 1, 0.8, out error), "pulse pattern did not start");
            Thread.Sleep(80);
            controller.Stop("selftest cancel");
            Require(!controller.IsRunning && fake.Left == 0 && fake.Right == 0 && fake.StopCount > stopsAfterCompletion, "cancellation did not stop");
            Require(controller.Start(ControllerRumblePattern.Alternating, 0.4, 0.4, 1, 0.8, out error), "alternating pattern did not start");
            Require(controller.Start(ControllerRumblePattern.Ramp, 0.4, 0.4, 1, 0.2, out error), "rapid restart did not replace prior task");
            Thread.Sleep(350);
            Require(!controller.IsRunning && fake.Left == 0 && fake.Right == 0, "rapid restart left a task running");
            controller.Synchronize(null);
            Require(!controller.IsRunning && fake.Left == 0 && fake.Right == 0, "disconnect did not stop");
            controller.Dispose();
            Require(fake.Left == 0 && fake.Right == 0, "dispose did not stop");
            passed.Add("patterns-cancel-disconnect-dispose");

            Require(ControllerRumbleController.DefaultStrength <= 0.40, "default strength safety failed");
            Require(ControllerRumbleController.DefaultDurationSeconds <= 5.0, "default duration safety failed");
            Require(ControllerRumbleController.MaximumDurationSeconds == 30.0, "maximum duration safety failed");
            passed.Add("safety-limits");
            return string.Join(", ", passed.ToArray());
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class FakeRumbleService : IControllerRumbleService
        {
            public string DeviceId { get { return "selftest"; } }
            public bool IsSupported { get { return true; } }
            public string SupportDetails { get { return "selftest"; } }
            public double Left;
            public double Right;
            public int StopCount;

            public bool TrySetRumble(double leftStrength, double rightStrength, out string error)
            {
                Left = leftStrength;
                Right = rightStrength;
                error = null;
                return true;
            }

            public void StopRumble()
            {
                Left = 0;
                Right = 0;
                StopCount++;
            }

            public void Dispose() { StopRumble(); }
        }
    }
}
