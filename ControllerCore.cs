using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace ControllerLab
{
    // The UI consumes this model only.  InputSnapshot remains an adapter boundary for
    // the established XInput and Raw HID implementations.
    public enum ControllerType
    {
        Unknown,
        Xbox,
        DualSense,
        DualShock4
    }

    public enum ControllerConnectionType
    {
        Unknown,
        Wired,
        Bluetooth,
        UsbReceiver,
        NativeHid,
        Demo
    }

    public enum ControllerInputSource
    {
        Unknown,
        XboxXInput,
        DualSenseHid,
        DynamicDemo,
        SyntheticSelfTest
    }

    [Flags]
    public enum ControllerDPad
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8
    }

    public sealed class ControllerCapabilities
    {
        public bool HasTouchpad;
        public bool HasTouchCoordinates;
        public bool HasMotionSensors;
        public bool HasMicrophoneButton;
        public bool HasLightbar;
        public bool HasShareButton;
        public bool HasGuideButton;
        public bool HasElitePaddles;
        public bool HasTriggerRumble;
    }

    public sealed class DualSenseControllerExtensions
    {
        public bool TouchpadPressed;
        public DualSenseTouchPoint[] TouchPoints = new DualSenseTouchPoint[0];
        public bool TouchCoordinatesAvailable;
        public double GyroscopeX;
        public double GyroscopeY;
        public double GyroscopeZ;
        public double AccelerometerX;
        public double AccelerometerY;
        public double AccelerometerZ;
        public MotionSample Motion;
        public bool MicrophoneButton;
        public string LightbarState = "unknown";
    }

    public sealed class XboxControllerExtensions
    {
        public bool ShareButton;
        public bool GuideButton;
        public int ElitePaddles;
        public bool TriggerRumbleSupport;
    }

    public sealed class ControllerState
    {
        public string DeviceId = string.Empty;
        public string DeviceName = "Controller";
        public ControllerType ControllerType;
        public ControllerConnectionType ConnectionType;
        public string ConnectionTypeLabel = "Unknown";
        public bool IsConnected;
        public int BatteryLevel = -1;
        public string BatteryLabel = "-";
        public ushort Buttons;
        public ControllerDPad DPad;
        public double LeftStickX;
        public double LeftStickY;
        public double RightStickX;
        public double RightStickY;
        public double LeftTrigger;
        public double RightTrigger;
        public DateTime TimestampUtc;
        public int PlayerIndex = -1;
        public uint Packet;
        public string InputBackend = string.Empty;
        public ControllerInputSource InputSource;
        public ControllerCapabilities Capabilities = new ControllerCapabilities();
        public DualSenseControllerExtensions DualSense;
        public XboxControllerExtensions Xbox;
        internal InputSnapshot SourceSnapshot;

        public bool HasRealInput
        {
            get { return InputSource == ControllerInputSource.XboxXInput || InputSource == ControllerInputSource.DualSenseHid; }
        }

        public string InputSourceLabel
        {
            get { return ControllerStateAdapter.InputSourceLabel(InputSource); }
        }

        public InputSnapshot ToInputSnapshot()
        {
            if (SourceSnapshot != null) return SourceSnapshot;
            return new InputSnapshot
            {
                DeviceId = DeviceId,
                TimestampUtc = TimestampUtc,
                Connected = IsConnected,
                Family = ControllerType == ControllerType.Xbox ? ControllerFamily.Xbox : ControllerFamily.PlayStation,
                DeviceName = DeviceName,
                InputBackend = InputBackend,
                Index = PlayerIndex < 0 ? 0 : PlayerIndex,
                Packet = Packet,
                Buttons = Buttons,
                LeftTrigger = (int)Math.Round(LeftTrigger * 255.0),
                RightTrigger = (int)Math.Round(RightTrigger * 255.0),
                LeftX = (int)Math.Round(LeftStickX * 32767.0),
                LeftY = (int)Math.Round(LeftStickY * 32767.0),
                RightX = (int)Math.Round(RightStickX * 32767.0),
                RightY = (int)Math.Round(RightStickY * 32767.0),
                Battery = BatteryLabel,
                BatteryPercent = BatteryLevel,
                ConnectionMethod = ConnectionTypeLabel,
                ConnectionIsWireless = ConnectionType == ControllerConnectionType.Bluetooth || ConnectionType == ControllerConnectionType.UsbReceiver,
                TouchpadPressed = DualSense != null && DualSense.TouchpadPressed,
                MicrophoneMuted = DualSense != null && DualSense.MicrophoneButton,
                TouchCoordinatesAvailable = DualSense != null && DualSense.TouchCoordinatesAvailable,
                HasTouchCoordinates = DualSense != null && DualSense.TouchCoordinatesAvailable,
                TouchPoint1 = DualSense != null && DualSense.TouchPoints.Length > 0 ? DualSense.TouchPoints[0] : null,
                TouchPoint2 = DualSense != null && DualSense.TouchPoints.Length > 1 ? DualSense.TouchPoints[1] : null,
                Motion = DualSense != null && DualSense.Motion != null ? DualSense.Motion.Copy() : null,
                GyroscopeX = DualSense != null && DualSense.Motion != null ? DualSense.Motion.GyroX : 0,
                GyroscopeY = DualSense != null && DualSense.Motion != null ? DualSense.Motion.GyroY : 0,
                GyroscopeZ = DualSense != null && DualSense.Motion != null ? DualSense.Motion.GyroZ : 0,
                AccelerometerX = DualSense != null && DualSense.Motion != null ? DualSense.Motion.AccelX : 0,
                AccelerometerY = DualSense != null && DualSense.Motion != null ? DualSense.Motion.AccelY : 0,
                AccelerometerZ = DualSense != null && DualSense.Motion != null ? DualSense.Motion.AccelZ : 0
            };
        }
    }

    public static class ControllerStateAdapter
    {
        public static ControllerState FromSnapshot(InputSnapshot snapshot)
        {
            if (snapshot == null) return CreateDisconnected();
            bool sony = snapshot.Family == ControllerFamily.PlayStation;
            ControllerType type = sony ? ControllerType.DualSense : ControllerType.Xbox;
            string id = snapshot.DeviceId;
            if (string.IsNullOrEmpty(id)) id = sony ? "sony:unknown" : "xinput:" + Math.Max(0, snapshot.Index).ToString(CultureInfo.InvariantCulture);
            ControllerState state = new ControllerState
            {
                DeviceId = id,
                DeviceName = snapshot.DeviceName ?? (sony ? "DualSense" : "Xbox Controller"),
                ControllerType = type,
                ConnectionType = ParseConnectionType(snapshot.ConnectionMethod),
                ConnectionTypeLabel = snapshot.ConnectionMethod ?? "Unknown",
                IsConnected = snapshot.Connected,
                BatteryLevel = snapshot.BatteryPercent,
                BatteryLabel = snapshot.Battery ?? "-",
                Buttons = snapshot.Buttons,
                DPad = ToDPad(snapshot.Buttons),
                LeftStickX = snapshot.LeftNormalizedX,
                LeftStickY = snapshot.LeftNormalizedY,
                RightStickX = snapshot.RightNormalizedX,
                RightStickY = snapshot.RightNormalizedY,
                LeftTrigger = NormalizeTrigger(snapshot.LeftTrigger),
                RightTrigger = NormalizeTrigger(snapshot.RightTrigger),
                TimestampUtc = snapshot.TimestampUtc == DateTime.MinValue ? DateTime.UtcNow : snapshot.TimestampUtc,
                PlayerIndex = snapshot.Index,
                Packet = snapshot.Packet,
                InputBackend = snapshot.InputBackend ?? string.Empty,
                InputSource = DetectInputSource(snapshot, sony),
                SourceSnapshot = snapshot,
                Capabilities = new ControllerCapabilities
                {
                    HasTouchpad = sony,
                    HasTouchCoordinates = sony && snapshot.TouchCoordinatesAvailable,
                    HasMotionSensors = sony && snapshot.Motion != null && snapshot.Motion.IsValid,
                    HasMicrophoneButton = sony,
                    HasLightbar = sony,
                    HasShareButton = !sony,
                    HasGuideButton = !sony,
                    HasElitePaddles = !sony,
                    HasTriggerRumble = !sony
                }
            };
            if (sony)
            {
                List<DualSenseTouchPoint> contacts = new List<DualSenseTouchPoint>();
                if (snapshot.TouchPoint1 != null) contacts.Add(snapshot.TouchPoint1.Copy());
                if (snapshot.TouchPoint2 != null) contacts.Add(snapshot.TouchPoint2.Copy());
                state.DualSense = new DualSenseControllerExtensions
                {
                    TouchpadPressed = snapshot.TouchpadPressed,
                    TouchCoordinatesAvailable = snapshot.TouchCoordinatesAvailable,
                    TouchPoints = contacts.ToArray(),
                    MicrophoneButton = snapshot.MicrophoneMuted,
                    GyroscopeX = snapshot.GyroscopeX,
                    GyroscopeY = snapshot.GyroscopeY,
                    GyroscopeZ = snapshot.GyroscopeZ,
                    AccelerometerX = snapshot.AccelerometerX,
                    AccelerometerY = snapshot.AccelerometerY,
                    AccelerometerZ = snapshot.AccelerometerZ,
                    Motion = snapshot.Motion == null ? null : snapshot.Motion.Copy(),
                    LightbarState = snapshot.LightbarState ?? "available"
                };
            }
            else
            {
                state.Xbox = new XboxControllerExtensions
                {
                    ShareButton = (snapshot.Buttons & 0x0400) != 0,
                    GuideButton = (snapshot.Buttons & 0x0400) != 0,
                    TriggerRumbleSupport = true
                };
            }
            return state;
        }

        public static ControllerState CreateDisconnected()
        {
            return new ControllerState { TimestampUtc = DateTime.UtcNow, DeviceName = "No controller", ConnectionTypeLabel = "Disconnected" };
        }

        public static string InputSourceLabel(ControllerInputSource source)
        {
            switch (source)
            {
                case ControllerInputSource.XboxXInput: return "Xbox XInput";
                case ControllerInputSource.DualSenseHid: return "DualSense HID";
                case ControllerInputSource.DynamicDemo: return "动态演示";
                case ControllerInputSource.SyntheticSelfTest: return "构造自检数据";
                default: return "未知输入来源";
            }
        }

        private static ControllerInputSource DetectInputSource(InputSnapshot snapshot, bool sony)
        {
            string backend = snapshot == null ? string.Empty : (snapshot.InputBackend ?? string.Empty);
            if (backend.IndexOf("演示", StringComparison.OrdinalIgnoreCase) >= 0 || backend.IndexOf("demo", StringComparison.OrdinalIgnoreCase) >= 0) return ControllerInputSource.DynamicDemo;
            if (backend.IndexOf("selftest", StringComparison.OrdinalIgnoreCase) >= 0 || backend.IndexOf("self-test", StringComparison.OrdinalIgnoreCase) >= 0 || backend.IndexOf(" test", StringComparison.OrdinalIgnoreCase) >= 0) return ControllerInputSource.SyntheticSelfTest;
            return sony ? ControllerInputSource.DualSenseHid : ControllerInputSource.XboxXInput;
        }

        private static double NormalizeTrigger(int value)
        {
            return Math.Max(0, Math.Min(1, value / 255.0));
        }

        private static ControllerDPad ToDPad(ushort buttons)
        {
            ControllerDPad value = ControllerDPad.None;
            if ((buttons & 0x0001) != 0) value |= ControllerDPad.Up;
            if ((buttons & 0x0002) != 0) value |= ControllerDPad.Down;
            if ((buttons & 0x0004) != 0) value |= ControllerDPad.Left;
            if ((buttons & 0x0008) != 0) value |= ControllerDPad.Right;
            return value;
        }

        public static ControllerConnectionType ParseConnectionType(string value)
        {
            if (string.IsNullOrEmpty(value)) return ControllerConnectionType.Unknown;
            if (value.IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("蓝牙", StringComparison.OrdinalIgnoreCase) >= 0) return ControllerConnectionType.Bluetooth;
            if (value.IndexOf("2.4", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("receiver", StringComparison.OrdinalIgnoreCase) >= 0) return ControllerConnectionType.UsbReceiver;
            if (value.IndexOf("demo", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("演示", StringComparison.OrdinalIgnoreCase) >= 0) return ControllerConnectionType.Demo;
            if (value.IndexOf("wired", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("有线", StringComparison.OrdinalIgnoreCase) >= 0) return ControllerConnectionType.Wired;
            if (value.IndexOf("HID", StringComparison.OrdinalIgnoreCase) >= 0) return ControllerConnectionType.NativeHid;
            return ControllerConnectionType.Unknown;
        }
    }

    public interface IControllerInputProvider
    {
        IEnumerable<ControllerState> ReadStates();
    }

    public sealed class XInputControllerProvider : IControllerInputProvider
    {
        private readonly InputManager input;
        public XInputControllerProvider(InputManager input) { this.input = input; }
        public IEnumerable<ControllerState> ReadStates()
        {
            IList<InputSnapshot> snapshots = input.ReadAll();
            for (int i = 0; i < snapshots.Count; i++) yield return ControllerStateAdapter.FromSnapshot(snapshots[i]);
        }
    }

    public sealed class SonyHidControllerProvider : IControllerInputProvider
    {
        private readonly SonyInputManager input;
        public SonyHidControllerProvider(SonyInputManager input) { this.input = input; }
        public IEnumerable<ControllerState> ReadStates()
        {
            IList<InputSnapshot> snapshots = input.ReadAll();
            for (int i = 0; i < snapshots.Count; i++) yield return ControllerStateAdapter.FromSnapshot(snapshots[i]);
        }
    }

    public sealed class ControllerDeviceCatalog
    {
        private readonly IControllerInputProvider[] providers;
        public ControllerDeviceCatalog(InputManager xbox, SonyInputManager sony)
        {
            providers = new IControllerInputProvider[] { new XInputControllerProvider(xbox), new SonyHidControllerProvider(sony) };
        }

        public ControllerState[] Poll()
        {
            Dictionary<string, ControllerState> online = new Dictionary<string, ControllerState>(StringComparer.OrdinalIgnoreCase);
            for (int p = 0; p < providers.Length; p++)
            {
                foreach (ControllerState state in providers[p].ReadStates())
                {
                    if (state != null && state.IsConnected && !string.IsNullOrEmpty(state.DeviceId)) online[state.DeviceId] = state;
                }
            }
            ControllerState[] result = new ControllerState[online.Count];
            online.Values.CopyTo(result, 0);
            Array.Sort(result, Compare);
            return result;
        }

        private static int Compare(ControllerState left, ControllerState right)
        {
            int byType = left.ControllerType.CompareTo(right.ControllerType);
            if (byType != 0) return byType;
            return string.Compare(left.DeviceId, right.DeviceId, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Public device boundary used by the home view. Individual devices never own a
    // second polling thread; the manager controls their lifetime and supplies state.
    public interface IControllerDevice
    {
        string DeviceId { get; }
        string DisplayName { get; }
        ControllerType ControllerType { get; }
        ControllerConnectionType ConnectionType { get; }
        bool IsConnected { get; }
        double? BatteryLevel { get; }
        ControllerCapabilities Capabilities { get; }
        ControllerState CurrentState { get; }
        void Start();
        void Stop();
    }

    public abstract class ControllerDeviceBase : IControllerDevice, INotifyPropertyChanged
    {
        private ControllerState currentState;
        private bool started;

        protected ControllerDeviceBase(ControllerState initialState)
        {
            currentState = initialState ?? ControllerStateAdapter.CreateDisconnected();
        }

        public string DeviceId { get { return currentState.DeviceId; } }
        public string DisplayName { get { return currentState.DeviceName; } }
        public ControllerType ControllerType { get { return currentState.ControllerType; } }
        public ControllerConnectionType ConnectionType { get { return currentState.ConnectionType; } }
        public bool IsConnected { get { return currentState.IsConnected; } }
        public double? BatteryLevel { get { return currentState.BatteryLevel < 0 ? (double?)null : currentState.BatteryLevel; } }
        public ControllerCapabilities Capabilities { get { return currentState.Capabilities; } }
        public ControllerState CurrentState { get { return currentState; } }
        public string ControllerTypeLabel { get { return ControllerType == ControllerType.Xbox ? "Xbox" : ControllerType == ControllerType.DualSense ? "DualSense" : "Controller"; } }
        public string ConnectionStatusLabel { get { return IsConnected ? "已连接" : "已断开"; } }
        public string ConnectionLabel { get { return string.IsNullOrEmpty(currentState.ConnectionTypeLabel) ? "未知" : currentState.ConnectionTypeLabel; } }
        public string BatteryLabel { get { return BatteryLevel.HasValue ? string.Format(CultureInfo.InvariantCulture, "{0:0}%", BatteryLevel.Value) : "未知"; } }
        public bool IsStarted { get { return started; } }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Start()
        {
            if (started) return;
            started = true;
            Raise("IsStarted");
        }

        public void Stop()
        {
            if (!started) return;
            started = false;
            Raise("IsStarted");
        }

        internal void Update(ControllerState value)
        {
            if (value == null) return;
            currentState = value;
            Raise("DisplayName");
            Raise("ControllerType");
            Raise("ConnectionType");
            Raise("IsConnected");
            Raise("BatteryLevel");
            Raise("Capabilities");
            Raise("CurrentState");
            Raise("ControllerTypeLabel");
            Raise("ConnectionStatusLabel");
            Raise("ConnectionLabel");
            Raise("BatteryLabel");
        }

        protected void Raise(string property)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(property));
        }
    }

    public sealed class XboxControllerDevice : ControllerDeviceBase
    {
        public XboxControllerDevice(ControllerState state) : base(state) { }
    }

    public sealed class DualSenseControllerDevice : ControllerDeviceBase
    {
        public DualSenseControllerDevice(ControllerState state) : base(state) { }
    }

    public sealed class ControllerDeviceManager : IDisposable
    {
        private readonly ControllerDeviceCatalog catalog;
        private readonly SonyInputManager sony;
        private readonly Dictionary<string, ControllerDeviceBase> known = new Dictionary<string, ControllerDeviceBase>(StringComparer.OrdinalIgnoreCase);
        private bool disposed;

        // This collection is modified only by Synchronize on the UI Dispatcher.
        public ObservableCollection<IControllerDevice> Devices { get; private set; }

        public ControllerDeviceManager(InputManager xbox, SonyInputManager sony)
        {
            this.sony = sony;
            catalog = new ControllerDeviceCatalog(xbox, sony);
            Devices = new ObservableCollection<IControllerDevice>();
        }

        // Called from the existing input sampler. It returns an immutable snapshot
        // reference and deliberately does not touch WPF-bound collections.
        public ControllerState[] Scan()
        {
            if (disposed) return new ControllerState[0];
            sony.DiscoverConnectedDevices();
            return catalog.Poll();
        }

        // Called from the Dispatcher thread once per render tick.
        public void Synchronize(ControllerState[] states)
        {
            if (disposed) return;
            states = states ?? new ControllerState[0];
            HashSet<string> present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < states.Length; i++)
            {
                ControllerState state = states[i];
                if (state == null || !state.IsConnected || string.IsNullOrEmpty(state.DeviceId)) continue;
                present.Add(state.DeviceId);
                ControllerDeviceBase device;
                if (!known.TryGetValue(state.DeviceId, out device))
                {
                    device = state.ControllerType == ControllerType.Xbox
                        ? (ControllerDeviceBase)new XboxControllerDevice(state)
                        : new DualSenseControllerDevice(state);
                    known[state.DeviceId] = device;
                    device.Start();
                    Devices.Add(device);
                }
                else
                {
                    device.Update(state);
                }
            }

            List<string> removed = new List<string>();
            foreach (KeyValuePair<string, ControllerDeviceBase> pair in known)
            {
                if (!present.Contains(pair.Key)) removed.Add(pair.Key);
            }
            for (int i = 0; i < removed.Count; i++)
            {
                ControllerDeviceBase device = known[removed[i]];
                device.Stop();
                Devices.Remove(device);
                known.Remove(removed[i]);
            }
        }

        public IControllerDevice Find(string deviceId)
        {
            ControllerDeviceBase value;
            return !string.IsNullOrEmpty(deviceId) && known.TryGetValue(deviceId, out value) ? value : null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (ControllerDeviceBase device in known.Values) device.Stop();
            known.Clear();
            Devices.Clear();
        }
    }

    public sealed class ControllerButtonTestResult
    {
        public string Id;
        public string Label;
        public bool Passed;
    }

    public sealed class ControllerTestReport
    {
        public string DeviceName;
        public ControllerType ControllerType;
        public DateTime TestTime;
        public Dictionary<string, bool> ButtonTestResults = new Dictionary<string, bool>();
        public double LeftStickDrift;
        public double RightStickDrift;
        public double SuggestedDeadzone;
        public double LeftTriggerMaximum;
        public double RightTriggerMaximum;
        public bool TriggerReturnToZero;
        public string OverallStatus;
        public int ButtonTestPassedCount;
        public int ButtonTestTotalCount;
        public List<string> UnpassedButtons = new List<string>();
        public bool IsFormalInput;
    }

    public sealed class ControllerInputTestEngine
    {
        public const double TriggerPassThreshold = 0.75;
        private string activeDeviceId;
        private ControllerType activeType;
        private readonly Dictionary<string, ControllerButtonTestResult> results = new Dictionary<string, ControllerButtonTestResult>();

        public IList<ControllerButtonTestResult> Results
        {
            get { return new List<ControllerButtonTestResult>(results.Values); }
        }

        public void Reset(ControllerState state)
        {
            activeDeviceId = state == null ? null : state.DeviceId;
            activeType = state == null ? ControllerType.Unknown : state.ControllerType;
            results.Clear();
            if (state != null && state.IsConnected && state.HasRealInput) AddDefinitions(activeType);
        }

        public void Update(ControllerState state)
        {
            if (state == null || !state.IsConnected || !state.HasRealInput)
            {
                if (results.Count > 0 || !string.IsNullOrEmpty(activeDeviceId)) Reset(null);
                return;
            }
            if (!string.Equals(activeDeviceId, state.DeviceId, StringComparison.OrdinalIgnoreCase) || activeType != state.ControllerType) Reset(state);
            foreach (KeyValuePair<string, ControllerButtonTestResult> pair in results)
            {
                if (!pair.Value.Passed && IsPressed(pair.Key, state)) pair.Value.Passed = true;
            }
        }

        public ControllerTestReport BuildReport(ControllerState state, StickTriggerTestEngine sticks)
        {
            ControllerTestReport report = new ControllerTestReport
            {
                DeviceName = state == null ? "No controller" : state.DeviceName,
                ControllerType = state == null ? ControllerType.Unknown : state.ControllerType,
                TestTime = DateTime.Now,
                LeftStickDrift = sticks == null ? 0 : sticks.LeftDriftPercent,
                RightStickDrift = sticks == null ? 0 : sticks.RightDriftPercent,
                SuggestedDeadzone = sticks == null ? 0.08 : sticks.SuggestedDeadzone,
                LeftTriggerMaximum = sticks == null ? 0 : sticks.LeftTriggerMaximum,
                RightTriggerMaximum = sticks == null ? 0 : sticks.RightTriggerMaximum,
                TriggerReturnToZero = sticks != null && sticks.TriggersReturnToZero
            };
            report.IsFormalInput = state != null && state.IsConnected && state.HasRealInput;
            if (!report.IsFormalInput)
            {
                report.OverallStatus = "当前数据来源不能用于正式按键检测";
                return report;
            }
            int passed = 0;
            foreach (KeyValuePair<string, ControllerButtonTestResult> pair in results)
            {
                report.ButtonTestResults[pair.Key] = pair.Value.Passed;
                if (pair.Value.Passed) passed++;
                else report.UnpassedButtons.Add(pair.Value.Label);
            }
            report.ButtonTestPassedCount = passed;
            report.ButtonTestTotalCount = results.Count;
            report.OverallStatus = results.Count == 0 ? "Waiting" : passed == results.Count ? "Passed" : string.Format(CultureInfo.InvariantCulture, "{0}/{1} buttons verified", passed, results.Count);
            return report;
        }

        private void AddDefinitions(ControllerType type)
        {
            Add("dpad-up", "D-pad Up"); Add("dpad-down", "D-pad Down"); Add("dpad-left", "D-pad Left"); Add("dpad-right", "D-pad Right");
            if (type == ControllerType.Xbox)
            {
                Add("face-a", "A"); Add("face-b", "B"); Add("face-x", "X"); Add("face-y", "Y");
                Add("view", "View"); Add("menu", "Menu"); Add("guide", "Guide");
            }
            else
            {
                Add("face-cross", "Cross"); Add("face-circle", "Circle"); Add("face-square", "Square"); Add("face-triangle", "Triangle");
                Add("create", "Create"); Add("options", "Options"); Add("ps", "PS"); Add("touchpad", "Touchpad"); Add("mic", "Microphone");
            }
            Add("l1", type == ControllerType.Xbox ? "LB" : "L1"); Add("r1", type == ControllerType.Xbox ? "RB" : "R1");
            Add("l3", "L3"); Add("r3", "R3"); Add("l2", type == ControllerType.Xbox ? "LT" : "L2"); Add("r2", type == ControllerType.Xbox ? "RT" : "R2");
        }

        private void Add(string id, string label) { results[id] = new ControllerButtonTestResult { Id = id, Label = label }; }

        // Presentation-only state for the compact WPF input grid. This delegates to
        // the same mapping used by the test engine and does not affect pass logic.
        public static bool IsCurrentlyPressed(string id, ControllerState state)
        {
            return state != null && state.IsConnected && IsPressed(id, state);
        }

        private static bool IsPressed(string id, ControllerState state)
        {
            ushort b = state.Buttons;
            switch (id)
            {
                case "dpad-up": return (b & 0x0001) != 0; case "dpad-down": return (b & 0x0002) != 0;
                case "dpad-left": return (b & 0x0004) != 0; case "dpad-right": return (b & 0x0008) != 0;
                case "view": case "create": return (b & 0x0020) != 0; case "menu": case "options": return (b & 0x0010) != 0;
                case "l3": return (b & 0x0040) != 0; case "r3": return (b & 0x0080) != 0;
                case "l1": return (b & 0x0100) != 0; case "r1": return (b & 0x0200) != 0;
                case "guide": case "ps": return (b & 0x0400) != 0; case "touchpad": return (b & 0x0800) != 0;
                case "face-a": case "face-cross": return (b & 0x1000) != 0; case "face-b": case "face-circle": return (b & 0x2000) != 0;
                case "face-x": case "face-square": return (b & 0x4000) != 0; case "face-y": case "face-triangle": return (b & 0x8000) != 0;
                case "l2": return state.LeftTrigger >= TriggerPassThreshold; case "r2": return state.RightTrigger >= TriggerPassThreshold;
                case "mic": return state.DualSense != null && state.DualSense.MicrophoneButton;
                default: return false;
            }
        }
    }

    public sealed class StickTriggerTestEngine
    {
        private string activeDeviceId;
        private int leftCenterSamples;
        private int rightCenterSamples;
        private double leftCenterX;
        private double leftCenterY;
        private double rightCenterX;
        private double rightCenterY;
        public double LeftTriggerMaximum { get; private set; }
        public double RightTriggerMaximum { get; private set; }
        public double LeftDriftPercent { get { return Math.Sqrt(leftCenterX * leftCenterX + leftCenterY * leftCenterY) * 100.0; } }
        public double RightDriftPercent { get { return Math.Sqrt(rightCenterX * rightCenterX + rightCenterY * rightCenterY) * 100.0; } }
        public double SuggestedDeadzone { get { return Math.Max(0.04, Math.Min(0.25, Math.Max(LeftDriftPercent, RightDriftPercent) / 100.0 + 0.02)); } }
        public bool TriggersReturnToZero { get; private set; }
        public string LeftRating { get { return Rating(LeftDriftPercent); } }
        public string RightRating { get { return Rating(RightDriftPercent); } }

        public void Reset(ControllerState state)
        {
            activeDeviceId = state == null ? null : state.DeviceId;
            leftCenterSamples = rightCenterSamples = 0;
            leftCenterX = leftCenterY = rightCenterX = rightCenterY = 0;
            LeftTriggerMaximum = RightTriggerMaximum = 0;
            TriggersReturnToZero = false;
        }

        public void Update(ControllerState state)
        {
            if (state == null || !state.IsConnected) return;
            if (!string.Equals(activeDeviceId, state.DeviceId, StringComparison.OrdinalIgnoreCase)) Reset(state);
            SampleCenter(state.LeftStickX, state.LeftStickY, ref leftCenterX, ref leftCenterY, ref leftCenterSamples);
            SampleCenter(state.RightStickX, state.RightStickY, ref rightCenterX, ref rightCenterY, ref rightCenterSamples);
            LeftTriggerMaximum = Math.Max(LeftTriggerMaximum, state.LeftTrigger);
            RightTriggerMaximum = Math.Max(RightTriggerMaximum, state.RightTrigger);
            TriggersReturnToZero = state.LeftTrigger <= 0.03 && state.RightTrigger <= 0.03;
        }

        private static void SampleCenter(double x, double y, ref double centerX, ref double centerY, ref int samples)
        {
            if (Math.Sqrt(x * x + y * y) > 0.20) return;
            samples++;
            centerX += (x - centerX) / samples;
            centerY += (y - centerY) / samples;
        }

        private static string Rating(double percent)
        {
            if (percent <= 3) return "Normal";
            if (percent <= 8) return "Slight drift";
            return "Noticeable drift";
        }
    }

    public enum StickSide
    {
        Left,
        Right
    }

    public enum StickDriftRating
    {
        Pending,
        Normal,
        SlightDrift,
        NoticeableDrift,
        SevereDrift,
        Invalid
    }

    public enum StickTestStage
    {
        Idle,
        Settling,
        Sampling,
        Completed,
        RangeRecording,
        Cancelled
    }

    // All values are in the same -1..1 coordinate system exposed by ControllerState.
    // The timestamp lets a future report retain the original sampling cadence.
    public sealed class StickSample
    {
        public DateTime Timestamp;
        public double X;
        public double Y;
        public double Distance;

        public StickSample()
        {
        }

        public StickSample(DateTime timestamp, double x, double y)
        {
            Timestamp = timestamp;
            X = Math.Max(-1.0, Math.Min(1.0, x));
            Y = Math.Max(-1.0, Math.Min(1.0, y));
            Distance = Math.Sqrt(X * X + Y * Y);
        }
    }

    public sealed class StickDriftResult
    {
        public StickSide StickSide;
        public int SampleCount;
        public double AverageX;
        public double AverageY;
        // Magnitude of the averaged centre vector. This distinguishes a
        // consistently off-centre stick from random sample noise.
        public double CenterOffsetPercent;
        public double AverageDriftPercent;
        public double P95DriftPercent;
        public double MaximumDriftPercent;
        // Position standard deviation, still in normalized stick units (-1..1).
        public double StandardDeviation;
        public int AnomalySpikeCount;
        public double SuggestedDeadzonePercent;
        public StickDriftRating Rating;
        public bool IsValid;
        public string InvalidReason = string.Empty;
        public JoystickHealthScore Health;
    }

    public enum JoystickHealthGrade
    {
        Pending,
        Excellent,
        Good,
        Warning,
        Critical
    }

    // A compact, UI-independent health summary built only from the completed
    // stationary drift sample. It deliberately does not read XInput/HID.
    public sealed class JoystickHealthScore
    {
        public int Score;
        public JoystickHealthGrade Grade;
        public double CenterOffsetPercent;
        public double NoisePercent;
        public double RequiredDeadzonePercent;
    }

    public static class JoystickHealthAnalyzer
    {
        public static JoystickHealthScore Evaluate(StickDriftResult result)
        {
            JoystickHealthScore health = new JoystickHealthScore();
            if (result == null || !result.IsValid)
            {
                health.Grade = JoystickHealthGrade.Critical;
                health.Score = 0;
                return health;
            }

            health.CenterOffsetPercent = result.CenterOffsetPercent;
            health.NoisePercent = result.StandardDeviation * 100.0;
            health.RequiredDeadzonePercent = result.SuggestedDeadzonePercent;

            // Centre bias is the primary signal. Noise and required deadzone
            // reduce the score more gently so one isolated spike is not a
            // health verdict (the drift analyser already gates unstable runs).
            double centrePenalty = Math.Min(45.0, Math.Max(0.0, health.CenterOffsetPercent - 1.0) * 3.3);
            double noisePenalty = Math.Min(25.0, health.NoisePercent * 7.0);
            double deadzonePenalty = Math.Min(25.0, Math.Max(0.0, health.RequiredDeadzonePercent - 3.0) * 1.5);
            health.Score = (int)Math.Round(Math.Max(0.0, Math.Min(100.0, 100.0 - centrePenalty - noisePenalty - deadzonePenalty)));
            health.Grade = health.Score >= 90 ? JoystickHealthGrade.Excellent
                : health.Score >= 75 ? JoystickHealthGrade.Good
                : health.Score >= 45 ? JoystickHealthGrade.Warning
                : JoystickHealthGrade.Critical;
            return health;
        }

        public static string Label(JoystickHealthScore health)
        {
            if (health == null) return "Pending";
            switch (health.Grade)
            {
                case JoystickHealthGrade.Excellent: return "Excellent";
                case JoystickHealthGrade.Good: return "Good";
                case JoystickHealthGrade.Warning: return "Warning";
                case JoystickHealthGrade.Critical: return "Critical";
                default: return "Pending";
            }
        }
    }

    public sealed class StickRangeResult
    {
        public StickSide StickSide;
        public int SampleCount;
        public double MaxUp;
        public double MaxDown;
        public double MaxLeft;
        public double MaxRight;
        public double MaxRadius;
        public double MinimumOuterRadius;
        public double CoveragePercent;
        public string MissingDirections = string.Empty;
        public string Status = "Not tested";
    }

    public sealed class StickStabilityResult
    {
        public StickSide StickSide;
        public int CompletedRuns;
        public int TargetRuns;
        public double[] P95DriftPercent = new double[0];
        public double AverageP95DriftPercent;
        public double MaximumDifferencePercent;
        public string Status = "等待检测";
    }

    public sealed class ControllerStickTestResult
    {
        public string DeviceId = string.Empty;
        public string DeviceName = string.Empty;
        public ControllerType ControllerType;
        public ControllerInputSource InputSource;
        public DateTime TestTime;
        public StickDriftResult LeftStickDrift;
        public StickDriftResult RightStickDrift;
        public StickRangeResult LeftStickRange;
        public StickRangeResult RightStickRange;
        public StickStabilityResult LeftStickStability;
        public StickStabilityResult RightStickStability;
    }

    public static class StickDriftAnalyzer
    {
        public const double NormalThresholdPercent = 3.0;
        public const double SlightThresholdPercent = 7.0;
        public const double NoticeableThresholdPercent = 12.0;
        public const double SafetyMarginPercent = 1.5;
        public const double MinimumDeadzonePercent = 3.0;
        public const double MaximumDeadzonePercent = 20.0;
        public const double UnstableStandardDeviation = 0.08;
        public const double SpikeDelta = 0.08;
        public const int MinimumSamples = 30;

        public static StickDriftResult Analyze(StickSide side, IList<StickSample> samples)
        {
            StickDriftResult result = new StickDriftResult { StickSide = side, Rating = StickDriftRating.Pending };
            if (samples == null || samples.Count < MinimumSamples)
            {
                result.SampleCount = samples == null ? 0 : samples.Count;
                result.IsValid = false;
                result.Rating = StickDriftRating.Invalid;
                result.Health = JoystickHealthAnalyzer.Evaluate(result);
                result.InvalidReason = "采样不足，请重新检测";
                return result;
            }

            int count = samples.Count;
            double[] distances = new double[count];
            double sumX = 0;
            double sumY = 0;
            double sumDistance = 0;
            double maximum = 0;
            int spikes = 0;
            StickSample previous = null;
            for (int i = 0; i < count; i++)
            {
                StickSample sample = samples[i] ?? new StickSample(DateTime.UtcNow, 0, 0);
                double x = Math.Max(-1.0, Math.Min(1.0, sample.X));
                double y = Math.Max(-1.0, Math.Min(1.0, sample.Y));
                double distance = Math.Sqrt(x * x + y * y);
                distances[i] = distance;
                sumX += x;
                sumY += y;
                sumDistance += distance;
                if (distance > maximum) maximum = distance;
                if (previous != null)
                {
                    double dx = x - previous.X;
                    double dy = y - previous.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) > SpikeDelta) spikes++;
                }
                previous = sample;
            }

            double averageX = sumX / count;
            double averageY = sumY / count;
            double varianceX = 0;
            double varianceY = 0;
            for (int i = 0; i < count; i++)
            {
                StickSample sample = samples[i];
                double dx = sample.X - averageX;
                double dy = sample.Y - averageY;
                varianceX += dx * dx;
                varianceY += dy * dy;
            }
            double positionStandardDeviation = Math.Sqrt((varianceX + varianceY) / count);
            Array.Sort(distances);
            double p95 = Percentile(distances, 0.95);
            double p95Percent = p95 * 100.0;
            int unstableSpikeLimit = Math.Max(3, count / 20);

            result.SampleCount = count;
            result.AverageX = averageX;
            result.AverageY = averageY;
            result.CenterOffsetPercent = Math.Sqrt(averageX * averageX + averageY * averageY) * 100.0;
            result.AverageDriftPercent = sumDistance / count * 100.0;
            result.P95DriftPercent = p95Percent;
            result.MaximumDriftPercent = maximum * 100.0;
            result.StandardDeviation = positionStandardDeviation;
            result.AnomalySpikeCount = spikes;
            result.SuggestedDeadzonePercent = Clamp(p95Percent + SafetyMarginPercent, MinimumDeadzonePercent, MaximumDeadzonePercent);
            result.Rating = RatingFromPercent(p95Percent);
            result.IsValid = positionStandardDeviation <= UnstableStandardDeviation && spikes <= unstableSpikeLimit;
            if (!result.IsValid)
            {
                result.Rating = StickDriftRating.Invalid;
                result.InvalidReason = "检测期间摇杆可能被触碰，请重新检测";
            }
            result.Health = JoystickHealthAnalyzer.Evaluate(result);
            return result;
        }

        public static StickDriftRating RatingFromPercent(double percent)
        {
            if (percent <= NormalThresholdPercent) return StickDriftRating.Normal;
            if (percent <= SlightThresholdPercent) return StickDriftRating.SlightDrift;
            if (percent <= NoticeableThresholdPercent) return StickDriftRating.NoticeableDrift;
            return StickDriftRating.SevereDrift;
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            if (sorted == null || sorted.Length == 0) return 0;
            if (sorted.Length == 1) return sorted[0];
            double position = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            double fraction = position - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    // This engine is intentionally UI-independent. MainWindow feeds it immutable
    // ControllerState snapshots from its existing sampler; it never opens XInput or HID.
    public sealed class StickDriftTestEngine : IDisposable
    {
        private const int MaximumDriftSamples = 2048;
        private const int MaximumRangeSamples = 4096;
        public const double SettlingDurationSeconds = 1.0;
        public const double DriftSamplingDurationSeconds = 5.0;
        private readonly List<StickSample> leftSamples = new List<StickSample>();
        private readonly List<StickSample> rightSamples = new List<StickSample>();
        private readonly List<ControllerStickTestResult> completedDriftRuns = new List<ControllerStickTestResult>();
        private readonly StickRangeTracker leftRangeTracker = new StickRangeTracker(StickSide.Left, MaximumRangeSamples);
        private readonly StickRangeTracker rightRangeTracker = new StickRangeTracker(StickSide.Right, MaximumRangeSamples);
        private CancellationTokenSource cancellationSource;
        private string deviceId = string.Empty;
        private string deviceName = string.Empty;
        private ControllerType controllerType;
        private ControllerInputSource inputSource;
        private DateTime samplingStartsAt;
        private DateTime samplingEndsAt;
        private int targetRuns = 1;
        private bool disposed;

        public StickTestStage Stage { get; private set; }
        public string StatusMessage { get; private set; }
        public ControllerStickTestResult LastResult { get; private set; }
        public StickRangeResult LeftRange { get { return leftRangeTracker.CreateResult(); } }
        public StickRangeResult RightRange { get { return rightRangeTracker.CreateResult(); } }
        public StickStabilityResult LeftStability { get { return BuildStability(StickSide.Left); } }
        public StickStabilityResult RightStability { get { return BuildStability(StickSide.Right); } }
        public int DriftSampleCount { get { return Math.Min(leftSamples.Count, rightSamples.Count); } }
        public int CompletedRuns { get { return completedDriftRuns.Count; } }
        public int TargetRuns { get { return targetRuns; } }
        public bool IsActive { get { return Stage == StickTestStage.Settling || Stage == StickTestStage.Sampling || Stage == StickTestStage.RangeRecording; } }

        public StickDriftTestEngine()
        {
            Stage = StickTestStage.Idle;
            StatusMessage = "连接手柄后可开始检测";
        }

        public void Start(ControllerState state)
        {
            Start(state, false, DateTime.UtcNow);
        }

        public void Start(ControllerState state, bool runThreeTimes)
        {
            Start(state, runThreeTimes, DateTime.UtcNow);
        }

        public void Start(ControllerState state, DateTime now)
        {
            Start(state, false, now);
        }

        public void Start(ControllerState state, bool runThreeTimes, DateTime now)
        {
            if (disposed) return;
            if (!CanUseFormalInput(state))
            {
                RejectNonFormalInput(state);
                return;
            }
            BeginForDevice(state);
            leftSamples.Clear();
            rightSamples.Clear();
            leftRangeTracker.Reset();
            rightRangeTracker.Reset();
            LastResult = null;
            completedDriftRuns.Clear();
            targetRuns = runThreeTimes ? 3 : 1;
            ReplaceCancellationSource();
            samplingStartsAt = now.AddSeconds(SettlingDurationSeconds);
            samplingEndsAt = samplingStartsAt.AddSeconds(DriftSamplingDurationSeconds);
            Stage = StickTestStage.Settling;
            StatusMessage = targetRuns == 1 ? "请不要触碰摇杆，正在等待稳定（1 秒）" : "连续检测 1/3：请不要触碰摇杆，正在等待稳定（1 秒）";
        }

        public void StartRangeTest(ControllerState state)
        {
            if (disposed) return;
            if (!CanUseFormalInput(state))
            {
                RejectNonFormalInput(state);
                return;
            }
            BeginForDevice(state);
            leftRangeTracker.Reset();
            rightRangeTracker.Reset();
            ReplaceCancellationSource();
            Stage = StickTestStage.RangeRecording;
            StatusMessage = "请沿摇杆边缘完整旋转一圈，再点击结束范围测试";
        }

        public void FinishRangeTest()
        {
            if (Stage != StickTestStage.RangeRecording) return;
            CancelTokenOnly();
            Stage = StickTestStage.Completed;
            StatusMessage = "范围测试已完成";
            if (LastResult != null && string.Equals(LastResult.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                LastResult.LeftStickRange = LeftRange;
                LastResult.RightStickRange = RightRange;
            }
        }

        public void Cancel(string reason)
        {
            CancelTokenOnly();
            if (Stage == StickTestStage.Idle && string.IsNullOrEmpty(reason)) return;
            Stage = StickTestStage.Cancelled;
            StatusMessage = string.IsNullOrEmpty(reason) ? "检测已结束" : reason;
        }

        public void Reset(ControllerState state)
        {
            CancelTokenOnly();
            leftSamples.Clear();
            rightSamples.Clear();
            leftRangeTracker.Reset();
            rightRangeTracker.Reset();
            LastResult = null;
            completedDriftRuns.Clear();
            targetRuns = 1;
            if (state != null && state.IsConnected) BeginForDevice(state);
            else
            {
                deviceId = string.Empty;
                deviceName = string.Empty;
                controllerType = ControllerType.Unknown;
                inputSource = ControllerInputSource.Unknown;
            }
            Stage = StickTestStage.Idle;
            StatusMessage = CanUseFormalInput(state) ? "准备就绪：松开摇杆后点击开始检测" : "连接真实 Xbox XInput 或 DualSense HID 手柄后可开始检测";
        }

        public void Update(ControllerState state)
        {
            Update(state, DateTime.UtcNow);
        }

        public void Update(ControllerState state, DateTime now)
        {
            if (disposed) return;
            if (state == null || !state.IsConnected)
            {
                if (IsActive) Cancel("设备已断开，检测未完成");
                else if (LastResult == null) StatusMessage = "设备未连接，正式检测不可用";
                return;
            }
            if (!state.HasRealInput)
            {
                if (IsActive) Cancel("当前数据来源不是实际设备输入，检测未完成");
                else if (LastResult == null) StatusMessage = "当前数据来源为 " + state.InputSourceLabel + "，正式检测仅支持 Xbox XInput 或 DualSense HID";
                return;
            }
            if (string.IsNullOrEmpty(deviceId)) BeginForDevice(state);
            if (!string.Equals(deviceId, state.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                Reset(state);
                StatusMessage = "已切换设备，上一台设备的检测状态已清空";
                return;
            }
            if (cancellationSource != null && cancellationSource.IsCancellationRequested) return;

            if (Stage == StickTestStage.Settling)
            {
                if (now < samplingStartsAt) return;
                Stage = StickTestStage.Sampling;
                StatusMessage = targetRuns == 1 ? "正在采样，请继续不要触碰摇杆（5 秒）" : string.Format(CultureInfo.InvariantCulture, "连续检测 {0}/{1}：正在采样，请继续不要触碰摇杆（5 秒）", completedDriftRuns.Count + 1, targetRuns);
            }
            if (Stage == StickTestStage.Sampling)
            {
                AddDriftSample(leftSamples, new StickSample(now, state.LeftStickX, state.LeftStickY));
                AddDriftSample(rightSamples, new StickSample(now, state.RightStickX, state.RightStickY));
                if (now >= samplingEndsAt) CompleteDriftTest(now);
                return;
            }
            if (Stage == StickTestStage.RangeRecording)
            {
                leftRangeTracker.Add(new StickSample(now, state.LeftStickX, state.LeftStickY));
                rightRangeTracker.Add(new StickSample(now, state.RightStickX, state.RightStickY));
            }
        }

        public string CreateCopyText()
        {
            if (LastResult == null || LastResult.LeftStickDrift == null || LastResult.RightStickDrift == null) return "ControllerLab 摇杆检测\n尚无完整漂移检测结果。";
            return string.Format(CultureInfo.InvariantCulture,
                "ControllerLab 摇杆检测\n设备：{0}\n左摇杆：{1}，健康 {2} {3}/100，P95 漂移 {4:0.0}%，建议死区 {5:0.0}%\n右摇杆：{6}，健康 {7} {8}/100，P95 漂移 {9:0.0}%，建议死区 {10:0.0}%",
                LastResult.DeviceName,
                RatingLabel(LastResult.LeftStickDrift), JoystickHealthAnalyzer.Label(LastResult.LeftStickDrift.Health), LastResult.LeftStickDrift.Health == null ? 0 : LastResult.LeftStickDrift.Health.Score, LastResult.LeftStickDrift.P95DriftPercent, LastResult.LeftStickDrift.SuggestedDeadzonePercent,
                RatingLabel(LastResult.RightStickDrift), JoystickHealthAnalyzer.Label(LastResult.RightStickDrift.Health), LastResult.RightStickDrift.Health == null ? 0 : LastResult.RightStickDrift.Health.Score, LastResult.RightStickDrift.P95DriftPercent, LastResult.RightStickDrift.SuggestedDeadzonePercent);
        }

        public static string RatingLabel(StickDriftResult result)
        {
            if (result == null) return "待检测";
            if (!result.IsValid) return "请重新检测";
            switch (result.Rating)
            {
                case StickDriftRating.Normal: return "正常";
                case StickDriftRating.SlightDrift: return "轻微漂移";
                case StickDriftRating.NoticeableDrift: return "明显漂移";
                case StickDriftRating.SevereDrift: return "严重漂移";
                default: return "待检测";
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancelTokenOnly();
            leftSamples.Clear();
            rightSamples.Clear();
        }

        private void CompleteDriftTest(DateTime now)
        {
            CancelTokenOnly();
            StickDriftResult left = StickDriftAnalyzer.Analyze(StickSide.Left, leftSamples);
            StickDriftResult right = StickDriftAnalyzer.Analyze(StickSide.Right, rightSamples);
            LastResult = new ControllerStickTestResult
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                ControllerType = controllerType,
                InputSource = inputSource,
                TestTime = now,
                LeftStickDrift = left,
                RightStickDrift = right,
                LeftStickRange = LeftRange,
                RightStickRange = RightRange,
                LeftStickStability = BuildStability(StickSide.Left),
                RightStickStability = BuildStability(StickSide.Right)
            };
            if (!left.IsValid || !right.IsValid)
            {
                Stage = StickTestStage.Completed;
                StatusMessage = "检测期间摇杆可能被触碰，请保持摇杆静止后重新检测。";
                return;
            }

            completedDriftRuns.Add(LastResult);
            LastResult.LeftStickStability = BuildStability(StickSide.Left);
            LastResult.RightStickStability = BuildStability(StickSide.Right);
            if (completedDriftRuns.Count < targetRuns)
            {
                leftSamples.Clear();
                rightSamples.Clear();
                ReplaceCancellationSource();
                samplingStartsAt = now.AddSeconds(SettlingDurationSeconds);
                samplingEndsAt = samplingStartsAt.AddSeconds(DriftSamplingDurationSeconds);
                Stage = StickTestStage.Settling;
                StatusMessage = string.Format(CultureInfo.InvariantCulture, "第 {0}/{1} 轮完成，请继续不要触碰摇杆，准备下一轮。", completedDriftRuns.Count, targetRuns);
                return;
            }
            Stage = StickTestStage.Completed;
            StickStabilityResult leftStability = LastResult.LeftStickStability;
            StickStabilityResult rightStability = LastResult.RightStickStability;
            StatusMessage = leftStability.MaximumDifferencePercent > 3.0 || rightStability.MaximumDifferencePercent > 3.0
                ? "检测结果波动较大，建议重新检测或检查连接稳定性。"
                : (targetRuns == 3 ? "连续 3 次漂移检测完成，结果稳定。" : "漂移检测完成");
        }

        private static bool CanUseFormalInput(ControllerState state)
        {
            return state != null && state.IsConnected && state.HasRealInput;
        }

        private void RejectNonFormalInput(ControllerState state)
        {
            CancelTokenOnly();
            leftSamples.Clear();
            rightSamples.Clear();
            leftRangeTracker.Reset();
            rightRangeTracker.Reset();
            LastResult = null;
            completedDriftRuns.Clear();
            targetRuns = 1;
            if (state == null || !state.IsConnected)
            {
                Stage = StickTestStage.Cancelled;
                StatusMessage = "设备未连接，正式检测不可用";
                return;
            }
            BeginForDevice(state);
            Stage = StickTestStage.Cancelled;
            StatusMessage = "当前数据来源为 " + state.InputSourceLabel + "，正式检测仅支持 Xbox XInput 或 DualSense HID";
        }

        private StickStabilityResult BuildStability(StickSide side)
        {
            List<double> values = new List<double>();
            for (int i = 0; i < completedDriftRuns.Count; i++)
            {
                ControllerStickTestResult run = completedDriftRuns[i];
                StickDriftResult result = side == StickSide.Left ? run.LeftStickDrift : run.RightStickDrift;
                if (result != null && result.IsValid) values.Add(result.P95DriftPercent);
            }
            double[] p95 = values.ToArray();
            double average = 0;
            double minimum = double.MaxValue;
            double maximum = 0;
            for (int i = 0; i < p95.Length; i++)
            {
                average += p95[i];
                minimum = Math.Min(minimum, p95[i]);
                maximum = Math.Max(maximum, p95[i]);
            }
            if (p95.Length > 0) average /= p95.Length;
            double difference = p95.Length > 0 ? maximum - minimum : 0;
            string status;
            if (p95.Length < targetRuns) status = string.Format(CultureInfo.InvariantCulture, "已完成 {0}/{1} 轮", p95.Length, targetRuns);
            else if (difference > 3.0) status = "结果波动较大";
            else status = "稳定";
            return new StickStabilityResult
            {
                StickSide = side,
                CompletedRuns = p95.Length,
                TargetRuns = targetRuns,
                P95DriftPercent = p95,
                AverageP95DriftPercent = average,
                MaximumDifferencePercent = difference,
                Status = status
            };
        }

        private void BeginForDevice(ControllerState state)
        {
            deviceId = state.DeviceId ?? string.Empty;
            deviceName = state.DeviceName ?? "Controller";
            controllerType = state.ControllerType;
            inputSource = state.InputSource;
        }

        private static void AddDriftSample(List<StickSample> target, StickSample sample)
        {
            if (target.Count >= MaximumDriftSamples) target.RemoveAt(0);
            target.Add(sample);
        }

        private void ReplaceCancellationSource()
        {
            CancelTokenOnly();
            cancellationSource = new CancellationTokenSource();
        }

        private void CancelTokenOnly()
        {
            if (cancellationSource == null) return;
            cancellationSource.Cancel();
            cancellationSource.Dispose();
            cancellationSource = null;
        }

        private sealed class StickRangeTracker
        {
            private const int CoverageBins = 36;
            private readonly StickSide side;
            private readonly int maximumSamples;
            private readonly bool[] coverage = new bool[CoverageBins];
            private readonly double[] outerRadiusByBin = new double[CoverageBins];
            private readonly List<StickSample> samples = new List<StickSample>();
            private double maxUp;
            private double maxDown;
            private double maxLeft;
            private double maxRight;
            private double maxRadius;

            public StickRangeTracker(StickSide side, int maximumSamples)
            {
                this.side = side;
                this.maximumSamples = maximumSamples;
            }

            public void Reset()
            {
                samples.Clear();
                Array.Clear(coverage, 0, coverage.Length);
                Array.Clear(outerRadiusByBin, 0, outerRadiusByBin.Length);
                maxUp = maxDown = maxLeft = maxRight = maxRadius = 0;
            }

            public void Add(StickSample sample)
            {
                if (sample == null) return;
                if (samples.Count >= maximumSamples) samples.RemoveAt(0);
                samples.Add(sample);
                maxUp = Math.Max(maxUp, Math.Max(0, sample.Y));
                maxDown = Math.Max(maxDown, Math.Max(0, -sample.Y));
                maxLeft = Math.Max(maxLeft, Math.Max(0, -sample.X));
                maxRight = Math.Max(maxRight, Math.Max(0, sample.X));
                maxRadius = Math.Max(maxRadius, sample.Distance);
                if (sample.Distance >= 0.80)
                {
                    double angle = Math.Atan2(sample.Y, sample.X);
                    if (angle < 0) angle += Math.PI * 2.0;
                    int bin = Math.Min(CoverageBins - 1, (int)(angle / (Math.PI * 2.0) * CoverageBins));
                    coverage[bin] = true;
                    outerRadiusByBin[bin] = Math.Max(outerRadiusByBin[bin], sample.Distance);
                }
            }

            public StickRangeResult CreateResult()
            {
                int covered = 0;
                for (int i = 0; i < coverage.Length; i++) if (coverage[i]) covered++;
                double coveragePercent = covered * 100.0 / coverage.Length;
                double lowestDirection = Math.Min(Math.Min(maxUp, maxDown), Math.Min(maxLeft, maxRight));
                double minimumOuterRadius = double.MaxValue;
                for (int i = 0; i < outerRadiusByBin.Length; i++)
                {
                    if (outerRadiusByBin[i] > 0) minimumOuterRadius = Math.Min(minimumOuterRadius, outerRadiusByBin[i]);
                }
                if (minimumOuterRadius == double.MaxValue) minimumOuterRadius = 0;
                List<string> missing = new List<string>();
                if (maxUp < 0.85) missing.Add("上");
                if (maxDown < 0.85) missing.Add("下");
                if (maxLeft < 0.85) missing.Add("左");
                if (maxRight < 0.85) missing.Add("右");
                string status;
                if (samples.Count == 0) status = "未测试";
                else if (lowestDirection < 0.85) status = "某方向行程不足";
                else if (coveragePercent < 50.0) status = "尚未完成一整圈";
                else if (coveragePercent < 70.0) status = "圆周覆盖不完整";
                else status = "范围正常";
                return new StickRangeResult
                {
                    StickSide = side,
                    SampleCount = samples.Count,
                    MaxUp = maxUp,
                    MaxDown = maxDown,
                    MaxLeft = maxLeft,
                    MaxRight = maxRight,
                    MaxRadius = maxRadius,
                    MinimumOuterRadius = minimumOuterRadius,
                    CoveragePercent = coveragePercent,
                    MissingDirections = missing.Count == 0 ? (coveragePercent < 70.0 ? "部分圆周" : "无") : string.Join("、", missing.ToArray()),
                    Status = status
                };
            }
        }
    }

    public static class ControllerCoreSelfTest
    {
        public static string Run()
        {
            List<string> passed = new List<string>();
            InputSnapshot xbox = new InputSnapshot
            {
                DeviceId = "xinput:2",
                TimestampUtc = DateTime.UtcNow,
                Connected = true,
                Family = ControllerFamily.Xbox,
                DeviceName = "Xbox test",
                InputBackend = "XInput1_4",
                Index = 2,
                Buttons = 0xFFFF,
                LeftTrigger = 255,
                RightTrigger = 255,
                LeftX = 655,
                LeftY = -327,
                RightX = 0,
                RightY = 0,
                ConnectionMethod = "Bluetooth"
            };
            ControllerState xboxState = ControllerStateAdapter.FromSnapshot(xbox);
            if (xboxState.ControllerType != ControllerType.Xbox || xboxState.DeviceId != "xinput:2" || xboxState.DPad != (ControllerDPad.Up | ControllerDPad.Down | ControllerDPad.Left | ControllerDPad.Right) || xboxState.ConnectionType != ControllerConnectionType.Bluetooth) throw new InvalidOperationException("Xbox state adapter self-test failed.");
            passed.Add("xinput-adapter");

            InputSnapshot dualSense = new InputSnapshot
            {
                DeviceId = "sony:test",
                TimestampUtc = DateTime.UtcNow,
                Connected = true,
                Family = ControllerFamily.PlayStation,
                DeviceName = "DualSense test",
                InputBackend = "Sony Native HID",
                TouchpadPressed = true,
                TouchCoordinatesAvailable = true,
                TouchPoint1 = new DualSenseTouchPoint { Id = 3, IsActive = true, X = 0.25, Y = 0.75 },
                GyroscopeX = 12,
                AccelerometerY = -34,
                Motion = new MotionSample { TimestampUtc = DateTime.UtcNow, Sequence = 7, IsValid = true, SourceReportId = 0x01, GyroX = 12, AccelY = -0.25, CrcValidated = true },
                MicrophoneMuted = true,
                LightbarState = "available"
            };
            ControllerState dsState = ControllerStateAdapter.FromSnapshot(dualSense);
            if (dsState.ControllerType != ControllerType.DualSense || dsState.DualSense == null || !dsState.DualSense.TouchpadPressed || dsState.DualSense.TouchPoints.Length != 1 || dsState.DualSense.TouchPoints[0].Id != 3 || !dsState.DualSense.MicrophoneButton || dsState.DualSense.Motion == null || !dsState.DualSense.Motion.IsValid || !dsState.Capabilities.HasMotionSensors) throw new InvalidOperationException("DualSense state adapter self-test failed.");
            passed.Add("dualsense-extension-adapter");

            ControllerInputTestEngine buttons = new ControllerInputTestEngine();
            buttons.Update(xboxState);
            ControllerTestReport buttonReport = buttons.BuildReport(xboxState, null);
            if (buttonReport.ButtonTestResults.Count != 17) throw new InvalidOperationException("Button test definition self-test failed.");
            foreach (bool value in buttonReport.ButtonTestResults.Values) if (!value) throw new InvalidOperationException("Button test pass tracking self-test failed.");
            passed.Add("shared-button-test");

            ControllerState synthetic = new ControllerState
            {
                DeviceId = "synthetic:selftest",
                DeviceName = "Synthetic self-test",
                ControllerType = ControllerType.Xbox,
                InputSource = ControllerInputSource.SyntheticSelfTest,
                IsConnected = true,
                Buttons = 0xFFFF,
                LeftTrigger = 1.0,
                RightTrigger = 1.0
            };
            ControllerInputTestEngine gatedButtons = new ControllerInputTestEngine();
            gatedButtons.Update(synthetic);
            if (gatedButtons.BuildReport(synthetic, null).IsFormalInput || gatedButtons.Results.Count != 0) throw new InvalidOperationException("Synthetic button data entered a formal report.");
            passed.Add("formal-input-gating");

            StickTriggerTestEngine sticks = new StickTriggerTestEngine();
            for (int i = 0; i < 60; i++) sticks.Update(xboxState);
            if (sticks.LeftDriftPercent < 1.0 || sticks.LeftDriftPercent > 3.0 || sticks.LeftTriggerMaximum < 0.99 || sticks.RightTriggerMaximum < 0.99) throw new InvalidOperationException("Stick/trigger measurement self-test failed.");
            passed.Add("drift-and-trigger-test");
            RunStickDriftSelfTest();
            passed.Add("p95-stick-drift-test");
            return "Controller core self-test passed: " + string.Join(", ", passed.ToArray());
        }

        public static string RunStickDriftSelfTest()
        {
            StickDriftResult centered = StickDriftAnalyzer.Analyze(StickSide.Left, BuildSamples(120, 0, 0));
            Require(centered.IsValid && centered.Rating == StickDriftRating.Normal && centered.SuggestedDeadzonePercent == StickDriftAnalyzer.MinimumDeadzonePercent, "Centered stick result failed.");
            Require(centered.Health != null && centered.Health.Grade == JoystickHealthGrade.Excellent && centered.Health.Score == 100, "Centered health score failed.");

            StickDriftResult twoPercent = StickDriftAnalyzer.Analyze(StickSide.Left, BuildSamples(120, 0.02, 0));
            Require(twoPercent.IsValid && twoPercent.Rating == StickDriftRating.Normal && twoPercent.P95DriftPercent > 1.9 && twoPercent.P95DriftPercent < 2.1, "Two-percent drift result failed.");

            StickDriftResult fivePercent = StickDriftAnalyzer.Analyze(StickSide.Left, BuildSamples(120, 0.05, 0));
            Require(fivePercent.IsValid && fivePercent.Rating == StickDriftRating.SlightDrift, "Five-percent drift rating failed.");
            Require(fivePercent.Health != null && fivePercent.Health.Grade == JoystickHealthGrade.Good, "Five-percent health score failed.");

            StickDriftResult tenPercent = StickDriftAnalyzer.Analyze(StickSide.Left, BuildSamples(120, 0.10, 0));
            Require(tenPercent.IsValid && tenPercent.Rating == StickDriftRating.NoticeableDrift, "Ten-percent drift rating failed.");
            Require(tenPercent.Health != null && tenPercent.Health.Grade == JoystickHealthGrade.Warning, "Ten-percent health score failed.");

            StickDriftResult fifteenPercent = StickDriftAnalyzer.Analyze(StickSide.Left, BuildSamples(120, 0.15, 0));
            Require(fifteenPercent.IsValid && fifteenPercent.Rating == StickDriftRating.SevereDrift, "Fifteen-percent drift rating failed.");
            Require(fifteenPercent.Health != null && fifteenPercent.Health.Grade == JoystickHealthGrade.Critical, "Fifteen-percent health score failed.");

            List<StickSample> singleSpike = BuildSamples(120, 0, 0);
            singleSpike[60] = new StickSample(DateTime.UtcNow, 0.70, 0);
            StickDriftResult spike = StickDriftAnalyzer.Analyze(StickSide.Left, singleSpike);
            Require(spike.IsValid && spike.Rating == StickDriftRating.Normal && spike.AnomalySpikeCount == 2, "Single spike must not become a drift verdict.");

            List<StickSample> moving = new List<StickSample>();
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < 120; i++)
            {
                double angle = i * Math.PI * 2.0 / 120.0;
                moving.Add(new StickSample(now.AddMilliseconds(i * 8), Math.Cos(angle) * 0.50, Math.Sin(angle) * 0.50));
            }
            StickDriftResult continuousMovement = StickDriftAnalyzer.Analyze(StickSide.Left, moving);
            Require(!continuousMovement.IsValid && continuousMovement.InvalidReason.IndexOf("可能被触碰", StringComparison.Ordinal) >= 0, "Continuous movement must request a retest.");

            DateTime testStart = DateTime.UtcNow;
            ControllerState xbox = CreateStickTestState("xinput:stick-selftest", "Xbox test", ControllerType.Xbox, true, 0.02, 0, 0.05, 0);
            ControllerState dualSense = CreateStickTestState("sony:stick-selftest", "DualSense test", ControllerType.DualSense, true, 0.02, 0, 0.05, 0);
            StickDriftTestEngine engine = new StickDriftTestEngine();
            try
            {
                engine.Start(xbox, testStart);
                for (int i = 0; i < 800; i++) engine.Update(xbox, testStart.AddSeconds(1.01).AddMilliseconds(i * 8));
                Require(engine.LastResult != null && engine.LastResult.ControllerType == ControllerType.Xbox, "Xbox must use the shared stick test engine.");

                engine.Start(dualSense, testStart.AddSeconds(5));
                for (int i = 0; i < 800; i++) engine.Update(dualSense, testStart.AddSeconds(6.01).AddMilliseconds(i * 8));
                Require(engine.LastResult != null && engine.LastResult.ControllerType == ControllerType.DualSense, "DualSense must use the shared stick test engine.");

                engine.Start(xbox, true, testStart.AddSeconds(9));
                for (int i = 0; i < 2400; i++) engine.Update(xbox, testStart.AddSeconds(10.01).AddMilliseconds(i * 8));
                Require(engine.CompletedRuns == 3 && engine.LeftStability.Status == "稳定" && engine.RightStability.Status == "稳定", "Three-run stability session failed.");

                engine.Start(xbox, testStart.AddSeconds(25));
                dualSense.IsConnected = false;
                engine.Update(dualSense, testStart.AddSeconds(25.5));
                Require(engine.Stage == StickTestStage.Cancelled && engine.StatusMessage.IndexOf("断开", StringComparison.Ordinal) >= 0, "Disconnect must cancel an active stick test.");

                ControllerState rangeState = CreateStickTestState("xinput:range-selftest", "Xbox range test", ControllerType.Xbox, true, 0, 0, 0, 0);
                engine.StartRangeTest(rangeState);
                for (int i = 0; i < 144; i++)
                {
                    double angle = i * Math.PI * 2.0 / 144.0;
                    rangeState.LeftStickX = rangeState.RightStickX = Math.Cos(angle) * 0.95;
                    rangeState.LeftStickY = rangeState.RightStickY = Math.Sin(angle) * 0.95;
                    engine.Update(rangeState, testStart.AddSeconds(26).AddMilliseconds(i * 8));
                }
                engine.FinishRangeTest();
                Require(engine.LeftRange.Status == "范围正常" && engine.RightRange.Status == "范围正常" && engine.LeftRange.MinimumOuterRadius >= 0.94 && engine.LeftRange.MissingDirections == "无", "Complete range sweep failed.");

                engine.StartRangeTest(rangeState);
                for (int i = 0; i < 60; i++)
                {
                    rangeState.LeftStickX = rangeState.RightStickX = 0.95;
                    rangeState.LeftStickY = rangeState.RightStickY = 0;
                    engine.Update(rangeState, testStart.AddSeconds(28).AddMilliseconds(i * 8));
                }
                engine.FinishRangeTest();
                Require(engine.LeftRange.Status == "某方向行程不足" && engine.RightRange.Status == "某方向行程不足" && engine.LeftRange.MissingDirections.IndexOf("上", StringComparison.Ordinal) >= 0, "Insufficient directional range failed.");

                ControllerState demo = CreateStickTestState("demo:stick-selftest", "Demo controller", ControllerType.Xbox, true, 0, 0, 0, 0);
                demo.InputSource = ControllerInputSource.DynamicDemo;
                engine.Start(demo, testStart.AddSeconds(30));
                Require(engine.Stage == StickTestStage.Cancelled && engine.LastResult == null && engine.StatusMessage.IndexOf("动态演示", StringComparison.Ordinal) >= 0, "Demo data must not enter a formal stick test.");
            }
            finally
            {
                engine.Dispose();
            }
            return "Stick drift self-test passed: centered, 2%, 5%, 10%, 15%, single spike, sustained movement, Xbox/DualSense shared engine, three-run stability, disconnect cancellation, range coverage, demo gating.";
        }

        private static List<StickSample> BuildSamples(int count, double x, double y)
        {
            List<StickSample> samples = new List<StickSample>();
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < count; i++) samples.Add(new StickSample(now.AddMilliseconds(i * 8), x, y));
            return samples;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static ControllerState CreateStickTestState(string id, string name, ControllerType type, bool connected, double leftX, double leftY, double rightX, double rightY)
        {
            return new ControllerState
            {
                DeviceId = id,
                DeviceName = name,
                ControllerType = type,
                InputSource = type == ControllerType.DualSense ? ControllerInputSource.DualSenseHid : ControllerInputSource.XboxXInput,
                IsConnected = connected,
                TimestampUtc = DateTime.UtcNow,
                LeftStickX = leftX,
                LeftStickY = leftY,
                RightStickX = rightX,
                RightStickY = rightY
            };
        }

        public static string RunDeviceManagerSelfTest()
        {
            InputManager input = new InputManager();
            SonyInputManager sony = new SonyInputManager();
            ControllerDeviceManager manager = new ControllerDeviceManager(input, sony);
            try
            {
                ControllerState state = ControllerStateAdapter.FromSnapshot(new InputSnapshot
                {
                    DeviceId = "xinput:selftest",
                    TimestampUtc = DateTime.UtcNow,
                    Connected = true,
                    Family = ControllerFamily.Xbox,
                    DeviceName = "Xbox self-test",
                    ConnectionMethod = "Wired"
                });
                manager.Synchronize(new ControllerState[] { state, state });
                ControllerDeviceBase device = manager.Devices[0] as ControllerDeviceBase;
                if (device == null || manager.Devices.Count != 1 || !device.IsStarted) throw new InvalidOperationException("Duplicate registration self-test failed.");
                manager.Synchronize(new ControllerState[] { state });
                if (manager.Devices.Count != 1) throw new InvalidOperationException("Stable registration self-test failed.");
                manager.Synchronize(new ControllerState[0]);
                if (manager.Devices.Count != 0 || device.IsStarted) throw new InvalidOperationException("Disconnect lifecycle self-test failed.");
                return "Controller device manager self-test passed: deduplication, stable update, disconnect stop.";
            }
            finally
            {
                manager.Dispose();
                sony.Dispose();
                input.Dispose();
            }
        }
    }
}
