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

    public delegate void ControllerDeviceSelectedEventHandler(object sender, IControllerDevice device);

    public sealed partial class MainWindow : Window
    {
        private bool demoMode;
        private bool sonyDemoMode;
        private readonly bool multiDemoMode;
        private readonly InputManager input;
        private readonly SonyInputManager sonyInput;
        private readonly ControllerDeviceManager deviceManager;
        private readonly DualSenseMotionManager motionManager;
        private readonly DualSenseAdvancedManager dualSenseAdvancedManager;
        private readonly ControllerInputTestEngine inputTestEngine = new ControllerInputTestEngine();
        private readonly StickTriggerTestEngine stickTriggerTestEngine = new StickTriggerTestEngine();
        private readonly StickDriftTestEngine stickDriftTestEngine = new StickDriftTestEngine();
        // The main dashboard used to run through a DispatcherTimer at 8 ms.
        // On some systems that timer repeatedly exhausted Window Manager timer
        // handles and terminated WPF with Win32Exception 0x80004005. Use the
        // existing WPF render pulse instead: it owns no additional Win32 timer
        // handle and keeps visual work bounded to the display refresh rate.
        private bool renderLoopAttached;
        private TimeSpan lastRenderFrame;
        private readonly ControllerVisual controllerVisual;
        private readonly DualSenseVisual dualSenseVisual;
        private readonly Grid controllerVisualHost;
        private readonly StickPlot leftPlot;
        private readonly StickPlot rightPlot;
        private readonly TriggerTelemetryBuffer leftTriggerTelemetry = new TriggerTelemetryBuffer();
        private readonly TriggerTelemetryBuffer rightTriggerTelemetry = new TriggerTelemetryBuffer();
        private readonly TriggerChart leftTriggerChart;
        private readonly TriggerChart rightTriggerChart;
        private TextBlock leftTriggerTitle;
        private TextBlock rightTriggerTitle;
        private readonly DeadzoneSlider leftDeadzone;
        private readonly DeadzoneSlider rightDeadzone;
        private TextBlock connectionText;
        private Ellipse connectionDot;
        private TextBlock deviceMetaText;
        private TextBlock connectionMethodText;
        private Ellipse connectionMethodDot;
        private ContextMenu connectionMethodMenu;
        private string connectionMethodOverride = "自动";
        private TextBlock refreshRateText;
        private TextBlock samplingRateText;
        private TextBlock leftDriftX;
        private TextBlock leftDriftY;
        private TextBlock rightDriftX;
        private TextBlock rightDriftY;
        private TextBlock leftDeadzoneText;
        private TextBlock rightDeadzoneText;
        private TextBlock leftStickStatusText;
        private TextBlock rightStickStatusText;
        private TextBlock leftStickAdviceText;
        private TextBlock rightStickAdviceText;
        private TextBlock leftTriggerCurrentText;
        private TextBlock rightTriggerCurrentText;
        private TextBlock leftRealtimeTriggerLabel;
        private TextBlock rightRealtimeTriggerLabel;
        private TextBlock triggerStatusText;
        private TextBlock footerStatus;
        private TextBlock diagnosticScoreText;
        private TextBlock diagnosticDetailText;
        private Button controllerSelectorButton;
        private Button controllerFamilySelectorButton;
        private Button demoModeButton;
        private TextBlock deviceNameText;
        private TextBlock deviceLogoText;
        private CheckBox reducedMotionCheck;
        private Button calibrateButton;
        private ProgressBar calibrationProgress;
        private Grid guidedOverlay;
        private TextBlock guidedStageText;
        private TextBlock guidedInstructionText;
        private TextBlock guidedDetailText;
        private TextBlock guidedProgressText;
        private ProgressBar guidedProgress;
        private Button guidedActionButton;
        private Button guidedRestartButton;
        private Button guidedLaunchButton;
        private Button guidedCloseButton;
        private TextBlock guidedChecklistTitle;
        private WrapPanel guidedChecklistPanel;
        private GuidedStage renderedGuidedStage = GuidedStage.Idle;
        private readonly TextBlock[] guidedResultTexts = new TextBlock[6];
        private readonly Dictionary<int, Border> guidedButtonChips = new Dictionary<int, Border>();
        private MenuItem pauseHistoryMenuItem;
        private readonly MenuItem[] controllerMenuItems = new MenuItem[5];
        private Style darkMenuItemStyle;
        private Border refreshRateBadge;
        private Border connectionMethodBadge;
        private StackPanel deviceMetadataPanel;
        private Border deviceCard;
        private StackPanel footerRightPanel;
        private UIElement shellTitle;
        private UIElement shellContent;
        private UIElement shellFooter;
        private Grid pageHost;
        private DeviceHomeView deviceHomeView;
        private UIElement homePage;
        private UIElement visualizerPage;
        private UIElement inputTestPage;
        private UIElement stickDriftTestPage;
        private UIElement motionPage;
        private UIElement rumblePage;
        private UIElement healthCheckPage;
        private UIElement historyReportsPage;
        private UIElement settingsPage;
        private HistoryReportsPage historyReportsView;
        private SettingsPage settingsView;
        private ProductNoticeBanner noticeBanner;
        private int currentPage = 1;
        private bool controllerNavigationEnabled;
        private bool controllerNavigationComboLatched;
        private ushort controllerNavigationPreviousButtons;
        private int controllerNavigationPreviousLeftTrigger;
        private int controllerNavigationPreviousRightTrigger;
        private ushort controllerNavigationHeldDpad;
        private DateTime controllerNavigationNextRepeatUtc = DateTime.MinValue;
        // Controller navigation deliberately owns its selection state instead of
        // relying on WPF keyboard focus. Several code-built controls suppress the
        // default focus visual, which made D-pad/A navigation appear unresponsive.
        private readonly List<Control> controllerNavigationTargets = new List<Control>();
        private int controllerNavigationTargetIndex = -1;
        private Control controllerNavigationHighlightedTarget;
        private Brush controllerNavigationOriginalBorderBrush;
        private Thickness controllerNavigationOriginalBorderThickness;
        private Button visualizerPageButton;
        private Button homePageButton;
        private Button stickDriftPageButton;
        private Button motionPageButton;
        private Button rumblePageButton;
        private Button healthCheckPageButton;
        private Button inputTestPageButton;
        private Button historyPageButton;
        private Button settingsPageButton;
        private StackPanel primaryNavigation;
        private WrapPanel inputTestChipPanel;
        private TextBlock inputTestProgressText;
        private ProgressBar inputTestProgressBar;
        private TextBlock inputTestEmptyText;
        private TextBlock inputTestHintText;
        private TextBlock inputTestReportText;
        private Button inputTestResetButton;
        private readonly StickPlot stickTestLeftPlot;
        private readonly StickPlot stickTestRightPlot;
        private TextBlock stickTestLeftInfo;
        private TextBlock stickTestRightInfo;
        private TextBlock stickTestLeftSummary;
        private TextBlock stickTestRightSummary;
        private TextBlock triggerTestInfo;
        private TextBlock stickRangeSummaryText;
        private TextBlock stickTestStatusText;
        private TextBlock stickTestHintText;
        private TextBlock stickTestDeviceText;
        private CheckBox stickTestThreeRunsCheck;
        private Button stickTestStartButton;
        private Button stickTestRestartButton;
        private Button stickTestStopButton;
        private Button stickRangeStartButton;
        private Button stickRangeStopButton;
        private Button stickTestCopyButton;
        private Button stickTestSaveButton;
        private bool stickTestVisualsClearedForUnavailableState;
        private bool showStickRangeVisuals;
        private string stickTestRenderedDeviceId;
        private string renderedInputTestSignature;
        private DualSenseMotionPoseView motionPoseView;
        private Border motionUnavailablePanel;
        private TextBlock motionUnavailableText;
        private TextBlock motionPitchText;
        private TextBlock motionRollText;
        private TextBlock motionYawText;
        private TextBlock motionConnectionText;
        private TextBlock motionRateText;
        private TextBlock motionCalibrationText;
        private TextBlock motionQualityText;
        private TextBlock motionDetailText;
        private Button motionCalibrateButton;
        private Button motionRecenterButton;
        private Button motionResetButton;
        private CheckBox motionSmoothingCheck;
        private CheckBox motionRawLoggingCheck;
        private Slider rumbleLeftSlider;
        private Slider rumbleRightSlider;
        private Slider rumbleOverallSlider;
        private Slider rumbleDurationSlider;
        private TextBlock rumbleLeftValueText;
        private TextBlock rumbleRightValueText;
        private TextBlock rumbleOverallValueText;
        private TextBlock rumbleDurationValueText;
        private TextBlock rumbleDeviceText;
        private TextBlock rumbleSupportText;
        private TextBlock rumbleStatusText;
        private TextBlock rumbleRemainingText;
        private TextBlock rumblePatternText;
        private Border rumbleLeftVisual;
        private Border rumbleRightVisual;
        private Button rumbleStartButton;
        private Button rumbleStopButton;
        private readonly List<Button> rumblePatternButtons = new List<Button>();
        private DateTime nextMotionUiRefresh = DateTime.MinValue;
        private string selectedDeviceId;
        private HwndSource rawInputSource;
        private readonly DiagnosticEngine diagnostics = new DiagnosticEngine();
        private readonly GuidedTestEngine guidedTest = new GuidedTestEngine();
        private bool calibrating;
        private bool calibrationStatusVisible;
        private bool calibrationSuggestionPending;
        private DateTime calibrationMessageUntil = DateTime.MinValue;
        private double recommendedLeftDeadzone;
        private double recommendedRightDeadzone;
        private DateTime calibrationStarted;
        private long sumLX;
        private long sumLY;
        private long sumRX;
        private long sumRY;
        private int minLX;
        private int minLY;
        private int minRX;
        private int minRY;
        private int maxLX;
        private int maxLY;
        private int maxRX;
        private int maxRY;
        private int calibrationSamples;
        private double offsetLX;
        private double offsetLY;
        private double offsetRX;
        private double offsetRY;
        private int refreshTicks;
        private int samplingTicks;
        private double actualSamplingHz;
        private double actualDisplayHz;
        private DateTime rateWindowStarted = DateTime.UtcNow;
        private bool lastConnected;
        private readonly DateTime applicationStartedUtc = DateTime.UtcNow;
        private DateTime capabilitiesReadyUtc = DateTime.MinValue;
        private volatile int selectedControllerIndex = -1;
        private ControllerFamily selectedControllerFamily = ControllerFamily.Auto;
        private ControllerFamily renderedControllerFamily = ControllerFamily.Xbox;
        private bool reducedMotion;
        private bool historyPaused;
        private InputSnapshot currentState = new InputSnapshot();
        private ControllerState currentControllerState = ControllerStateAdapter.CreateDisconnected();
        private Thread samplingThread;
        private volatile bool sampling;
        private volatile InputSnapshot latestInput = new InputSnapshot();
        private volatile ControllerState[] latestControllerStates = new ControllerState[0];
        private readonly ControllerRumbleController rumbleController;
        private readonly RumbleSettingsStore rumbleSettingsStore;
        private RumbleStudioPage rumbleStudioPage;
        private DualSenseAdvancedPage dualSenseAdvancedPage;
        private readonly JoystickTestViewModel joystickTestViewModel;
        private JoystickTestPage joystickTestPage;
        private readonly ControllerHealthReportStore healthReportStore;
        private readonly ControllerHealthCheckViewModel healthCheckViewModel;
        private ControllerHealthCheckPage healthCheckView;
        private readonly ControllerSettings productSettings;
        private TimeSpan uiRefreshInterval = TimeSpan.FromMilliseconds(16);
        private Button realtimeAdvancedButton;

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWaitableTimerEx(IntPtr timerAttributes, string timerName, uint flags, uint desiredAccess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int period, IntPtr completionRoutine, IntPtr argument, bool resume);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelWaitableTimer(IntPtr timer);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public MainWindow()
        {
            sonyDemoMode = HasArgument("--sony-demo");
            multiDemoMode = HasArgument("--multi-demo");
            demoMode = HasArgument("--demo") || HasArgument("--product-ui-render-audit") || sonyDemoMode || multiDemoMode;
            productSettings = demoMode ? new ControllerSettings() : SettingsStore.Load();
            productSettings.Normalize();
            input = new InputManager();
            sonyInput = new SonyInputManager();
            rumbleController = new ControllerRumbleController(input, sonyInput);
            rumbleSettingsStore = new RumbleSettingsStore();
            joystickTestViewModel = new JoystickTestViewModel();
            healthReportStore = new ControllerHealthReportStore();
            healthCheckViewModel = new ControllerHealthCheckViewModel(rumbleController, healthReportStore);
            deviceManager = new ControllerDeviceManager(input, sonyInput);
            motionManager = new DualSenseMotionManager();
            dualSenseAdvancedManager = new DualSenseAdvancedManager();
            ApplyProductSettings(productSettings, false);
            Title = "手柄实验室 · " + ControllerLabVersion.Display;
            MinWidth = 1020;
            MinHeight = 680;
            Rect workArea = SystemParameters.WorkArea;
            if (demoMode)
            {
                Width = Math.Min(1440, Math.Max(MinWidth, workArea.Width - 24));
                Height = Math.Min(1024, Math.Max(MinHeight, workArea.Height - 24));
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                Width = Math.Min(productSettings.RememberWindowPosition && productSettings.HasWindowPlacement ? productSettings.WindowWidth : 1440, Math.Max(MinWidth, workArea.Width - 24));
                Height = Math.Min(productSettings.RememberWindowPosition && productSettings.HasWindowPlacement ? productSettings.WindowHeight : 1024, Math.Max(MinHeight, workArea.Height - 24));
                WindowStartupLocation = WindowStartupLocation.Manual;
                double desiredLeft = productSettings.RememberWindowPosition && productSettings.HasWindowPlacement ? productSettings.WindowLeft : workArea.Left + (workArea.Width - Width) / 2.0;
                double desiredTop = productSettings.RememberWindowPosition && productSettings.HasWindowPlacement ? productSettings.WindowTop : workArea.Top + (workArea.Height - Height) / 2.0;
                Left = Math.Max(workArea.Left - Width + 160, Math.Min(workArea.Right - 160, desiredLeft));
                Top = Math.Max(workArea.Top, Math.Min(workArea.Bottom - 80, desiredTop));
            }
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Palette.WindowBrush;
            Foreground = Palette.TextBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });

            controllerVisual = new ControllerVisual(LoadControllerImage());
            dualSenseVisual = new DualSenseVisual();
            controllerVisualHost = new Grid { ClipToBounds = true };
            controllerVisualHost.Children.Add(controllerVisual);
            controllerVisualHost.Children.Add(dualSenseVisual);
            leftPlot = new StickPlot(Palette.Blue);
            rightPlot = new StickPlot(Palette.Blue);
            stickTestLeftPlot = new StickPlot(Palette.Blue);
            stickTestRightPlot = new StickPlot(Palette.Blue);
            leftTriggerChart = new TriggerChart(Palette.Green, leftTriggerTelemetry);
            rightTriggerChart = new TriggerChart(Palette.Blue, rightTriggerTelemetry);
            ControllerSettings saved = productSettings;
            offsetLX = saved.OffsetLX;
            offsetLY = saved.OffsetLY;
            offsetRX = saved.OffsetRX;
            offsetRY = saved.OffsetRY;
            selectedControllerIndex = Math.Max(-1, Math.Min(3, saved.ControllerIndex));
            reducedMotion = saved.ReducedMotion;
            uiRefreshInterval = TimeSpan.FromSeconds(1.0 / Math.Max(20, Math.Min(60, saved.UiRefreshRate)));
            connectionMethodOverride = NormalizeConnectionMethodOverride(saved.ConnectionMethodOverride);
            selectedControllerFamily = NormalizeControllerFamily(saved.ControllerFamily);
            if (sonyDemoMode) selectedControllerFamily = ControllerFamily.PlayStation;
            input.SetUsbRouteProfiles(saved.WiredUsbRoute, saved.ReceiverUsbRoute);
            if (demoMode) diagnostics.UseDemoBaseline();
            leftDeadzone = new DeadzoneSlider(Palette.Blue, saved.LeftDeadzone);
            rightDeadzone = new DeadzoneSlider(Palette.Blue, saved.RightDeadzone);
            ApplyReducedMotion();

            Content = BuildRoot();
            ApplyProductSettings(productSettings, true);
            leftDeadzone.ValueChanged += OnDeadzoneChanged;
            rightDeadzone.ValueChanged += OnDeadzoneChanged;
            SizeChanged += OnWindowSizeChanged;
            SourceInitialized += delegate { InitializeRawInputTracking(); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape && guidedOverlay != null && guidedOverlay.Visibility == Visibility.Visible)
                {
                    CloseGuidedTest();
                    e.Handled = true;
                }
                if (e.Key == Key.F9)
                {
                    ToggleDemoMode();
                    e.Handled = true;
                }
            };

            Loaded += delegate
            {
                StartSampling();
                StartRenderLoop();
                int startupPage = !productSettings.AutoConnect || productSettings.StartupPage == "Devices" ? 0 : productSettings.StartupPage == "Health" ? 6 : 1;
                ShowPage(startupPage);
                UpdatePrimaryNavigationLayout();
                UpdateDeviceCardResponsiveLayout();
                if (HasArgument("--ds5-calibrate")) Dispatcher.BeginInvoke(new Action(OpenDualSenseCalibration), DispatcherPriority.Background);
                if (HasArgument("--xbox-calibrate")) Dispatcher.BeginInvoke(new Action(OpenXboxCalibration), DispatcherPriority.Background);
                if (HasArgument("--visualizer")) Dispatcher.BeginInvoke(new Action(delegate { ShowPage(1); }), DispatcherPriority.Background);
                if (HasArgument("--rumble-page")) Dispatcher.BeginInvoke(new Action(delegate { ShowPage(5); }), DispatcherPriority.Background);
                if (HasArgument("--health-check-page")) Dispatcher.BeginInvoke(new Action(delegate { ShowPage(6); }), DispatcherPriority.Background);
                if (HasArgument("--history-page")) Dispatcher.BeginInvoke(new Action(delegate { ShowPage(7); }), DispatcherPriority.Background);
                if (HasArgument("--settings-page")) Dispatcher.BeginInvoke(new Action(delegate { ShowPage(8); }), DispatcherPriority.Background);
            };
            Closed += delegate
            {
                LabLogger.Info("Application", "ControllerLab closing; active output and page sessions will be stopped.");
                StopRenderLoop();
                StopSampling();
                if (joystickTestPage != null) joystickTestPage.Cancel("应用退出，摇杆检测已取消");
                if (healthCheckView != null) healthCheckView.Dispose();
                if (dualSenseAdvancedPage != null) dualSenseAdvancedPage.Dispose();
                if (rumbleStudioPage != null) rumbleStudioPage.Dispose();
                rumbleController.Dispose();
                stickDriftTestEngine.Dispose();
                if (joystickTestPage != null) joystickTestPage.Dispose();
                if (!demoMode) SaveSettings();
                if (deviceHomeView != null) deviceHomeView.Dispose();
                deviceManager.Dispose();
                sonyInput.Dispose();
                input.Dispose();
            };
        }

        private void StartRenderLoop()
        {
            if (renderLoopAttached) return;
            renderLoopAttached = true;
            lastRenderFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopRenderLoop()
        {
            if (!renderLoopAttached) return;
            CompositionTarget.Rendering -= OnRendering;
            renderLoopAttached = false;
            lastRenderFrame = TimeSpan.Zero;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            RenderingEventArgs rendering = e as RenderingEventArgs;
            if (rendering == null) return;

            // The monitor may render faster than the UI needs. A 60 Hz cap
            // prevents expensive UI work from queueing while preserving smooth
            // stick and trigger feedback.
            if (lastRenderFrame != TimeSpan.Zero && rendering.RenderingTime - lastRenderFrame < uiRefreshInterval) return;
            lastRenderFrame = rendering.RenderingTime;
            OnTick(this, EventArgs.Empty);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputRegistration
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr TargetWindow;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputHeader
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RawInputRegistration[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        private void InitializeRawInputTracking()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            rawInputSource = HwndSource.FromHwnd(handle);
            if (rawInputSource == null) return;
            rawInputSource.AddHook(RawInputWindowProc);
            RawInputRegistration[] devices =
            {
                new RawInputRegistration { UsagePage = 0x01, Usage = 0x05, Flags = 0x00000100, TargetWindow = handle }, // gamepad
                new RawInputRegistration { UsagePage = 0x01, Usage = 0x04, Flags = 0x00000100, TargetWindow = handle }  // joystick
            };
            RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(RawInputRegistration)));
        }

        private IntPtr RawInputWindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;
            const uint RID_HEADER = 0x10000005;
            const uint RIM_TYPEHID = 2;
            if (message != WM_INPUT) return IntPtr.Zero;
            uint size = (uint)Marshal.SizeOf(typeof(RawInputHeader));
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(lParam, RID_HEADER, buffer, ref size, (uint)Marshal.SizeOf(typeof(RawInputHeader))) == uint.MaxValue) return IntPtr.Zero;
                RawInputHeader header = (RawInputHeader)Marshal.PtrToStructure(buffer, typeof(RawInputHeader));
                if (header.Type == RIM_TYPEHID)
                {
                    string rawPath = InputManager.GetRawDevicePath(header.Device);
                    input.ObserveRawInputDevicePath(rawPath);
                    // Keep the existing Xbox XInput path lightweight. Full Raw HID packets are copied only
                    // for Sony's vendor interface, where their native reports are the input source.
                    if (!string.IsNullOrEmpty(rawPath) && rawPath.IndexOf("VID_054C", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        List<byte[]> reports = ReadRawHidPayloads(lParam);
                        if (reports != null)
                        {
                            for (int i = 0; i < reports.Count; i++) sonyInput.ObserveRawInput(rawPath, reports[i]);
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return IntPtr.Zero;
        }

        private static List<byte[]> ReadRawHidPayloads(IntPtr rawInput)
        {
            const uint RID_INPUT = 0x10000003;
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(RawInputHeader));
            if (GetRawInputData(rawInput, RID_INPUT, IntPtr.Zero, ref size, headerSize) == uint.MaxValue || size == 0) return null;
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(rawInput, RID_INPUT, buffer, ref size, headerSize) == uint.MaxValue) return null;
                int rawHeaderSize = Marshal.SizeOf(typeof(RawInputHeader));
                if (size < rawHeaderSize + 8) return null;
                int reportSize = Marshal.ReadInt32(buffer, rawHeaderSize);
                int reportCount = Marshal.ReadInt32(buffer, rawHeaderSize + 4);
                int availableBytes = (int)size - rawHeaderSize - 8;
                if (reportSize <= 0 || reportCount <= 0 || availableBytes < reportSize) return null;
                int count = Math.Min(reportCount, availableBytes / reportSize);
                List<byte[]> reports = new List<byte[]>(count);
                for (int i = 0; i < count; i++)
                {
                    byte[] payload = new byte[reportSize];
                    Marshal.Copy(IntPtr.Add(buffer, rawHeaderSize + 8 + i * reportSize), payload, 0, reportSize);
                    reports.Add(payload);
                }
                return reports;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool HasArgument(string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private ImageSource LoadControllerImage()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ControllerLab.Assets.controller.png");
            if (stream == null) return null;
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            stream.Dispose();
            return image;
        }

        private UIElement BuildRoot()
        {
            Grid root = new Grid { Background = Palette.WindowBrush };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

            shellTitle = BuildTitleBar();
            Grid.SetRow(shellTitle, 0);
            root.Children.Add(shellTitle);

            Grid content = new Grid { Margin = new Thickness(22, 10, 22, 0) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });

            Grid left = BuildLeftColumn();
            Grid.SetColumn(left, 0);
            content.Children.Add(left);

            Grid right = BuildRightColumn();
            ScrollViewer rightScroll = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetColumn(rightScroll, 2);
            content.Children.Add(rightScroll);
            // Optional header metadata must not force the left star column wider
            // than the viewport before its responsive collapse logic executes.
            content.SizeChanged += delegate
            {
                double allowed = Math.Max(720, content.ActualWidth - 374);
                if (Math.Abs(left.MaxWidth - allowed) > 0.5) left.MaxWidth = allowed;
            };
            visualizerPage = new ControllerVisualizerView(content);
            deviceHomeView = new DeviceHomeView(deviceManager.Devices);
            deviceHomeView.DeviceSelected += OnHomeDeviceSelected;
            homePage = deviceHomeView;
            visualizerPage.Visibility = Visibility.Collapsed;
            inputTestPage = BuildInputTestPage();
            inputTestPage.Visibility = Visibility.Collapsed;
            joystickTestPage = new JoystickTestPage(joystickTestViewModel);
            stickTestThreeRunsCheck = joystickTestPage.NavigationCheckBox;
            stickDriftTestPage = joystickTestPage;
            stickDriftTestPage.Visibility = Visibility.Collapsed;
            dualSenseAdvancedPage = new DualSenseAdvancedPage(motionManager, dualSenseAdvancedManager, StartMotionCalibration, RecenterMotion, ResetMotion);
            motionPage = dualSenseAdvancedPage;
            motionPage.Visibility = Visibility.Collapsed;
            rumbleStudioPage = new RumbleStudioPage(rumbleController, rumbleSettingsStore);
            rumblePage = rumbleStudioPage;
            rumblePage.Visibility = Visibility.Collapsed;
            healthCheckView = new ControllerHealthCheckPage(healthCheckViewModel, healthReportStore);
            healthCheckPage = healthCheckView;
            healthCheckPage.Visibility = Visibility.Collapsed;
            historyReportsView = new HistoryReportsPage(healthReportStore, delegate
            {
                healthCheckView.ResetForDeviceChange();
                ShowPage(6);
            });
            historyReportsPage = historyReportsView;
            historyReportsPage.Visibility = Visibility.Collapsed;
            settingsView = new SettingsPage(productSettings, delegate(ControllerSettings value) { ApplyProductSettings(value, true); }, OpenApplicationDataDirectory, ClearAllHistoryReports);
            settingsPage = settingsView;
            settingsPage.Visibility = Visibility.Collapsed;
            pageHost = new Grid();
            pageHost.Children.Add(homePage);
            pageHost.Children.Add(visualizerPage);
            pageHost.Children.Add(inputTestPage);
            pageHost.Children.Add(stickDriftTestPage);
            pageHost.Children.Add(motionPage);
            pageHost.Children.Add(rumblePage);
            pageHost.Children.Add(healthCheckPage);
            pageHost.Children.Add(historyReportsPage);
            pageHost.Children.Add(settingsPage);
            Grid.SetRow(pageHost, 1);
            root.Children.Add(pageHost);
            shellContent = pageHost;

            noticeBanner = new ProductNoticeBanner();
            Grid.SetRow(noticeBanner, 1);
            Panel.SetZIndex(noticeBanner, 20);
            root.Children.Add(noticeBanner);

            shellFooter = BuildFooter();
            Grid.SetRow(shellFooter, 2);
            root.Children.Add(shellFooter);

            guidedOverlay = BuildGuidedOverlay();
            Grid.SetRowSpan(guidedOverlay, 3);
            root.Children.Add(guidedOverlay);
            return root;
        }

        private void OnHomeDeviceSelected(object sender, IControllerDevice device)
        {
            if (device == null || !device.IsConnected) return;
            SelectDevice(device.DeviceId);
            ShowPage(1);
        }

        private UIElement BuildInputTestPage()
        {
            Grid page = new Grid { Margin = new Thickness(32, 24, 32, 0) };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel();
            title.Children.Add(LabVisualStyles.CreatePageTitle("按键测试"));
            inputTestHintText = LabVisualStyles.CreateSecondaryText("按下每个按键一次即可标记通过。未通过项目会优先显示。设备切换时自动建立独立会话。");
            inputTestHintText.FontSize = 14;
            inputTestHintText.Margin = new Thickness(0, 7, 0, 0);
            title.Children.Add(inputTestHintText);
            heading.Children.Add(title);
            inputTestResetButton = MakeButton("重置测试", false);
            inputTestResetButton.MinWidth = 104;
            inputTestResetButton.Height = 36;
            inputTestResetButton.Click += delegate { inputTestEngine.Reset(currentControllerState); renderedInputTestSignature = null; };
            Grid.SetColumn(inputTestResetButton, 1);
            heading.Children.Add(inputTestResetButton);
            page.Children.Add(heading);

            Grid cards = new Grid();
            cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(292) });

            Grid inputBody = new Grid { Margin = new Thickness(22, 20, 22, 20) };
            inputBody.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inputBody.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            inputBody.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            inputBody.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            inputBody.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            inputTestProgressText = new TextBlock { Text = "等待设备", Foreground = Palette.MutedBrush, FontSize = 18, FontWeight = FontWeights.SemiBold };
            inputBody.Children.Add(inputTestProgressText);
            inputTestProgressBar = new ProgressBar { Minimum = 0, Maximum = 1, Value = 0, Height = 5, Foreground = Palette.BlueBrush, Background = Palette.SurfaceHoverBrush, BorderThickness = new Thickness(0) };
            Grid.SetRow(inputTestProgressBar, 2);
            inputBody.Children.Add(inputTestProgressBar);
            inputTestChipPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 0) };
            ScrollViewer chipScroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = inputTestChipPanel };
            Grid chipHost = new Grid();
            chipHost.Children.Add(chipScroller);
            inputTestEmptyText = LabVisualStyles.CreateSecondaryText("连接真实 Xbox XInput 或 DualSense HID 手柄后，按键网格会显示在这里。\n动态演示不会生成正式检测结果。");
            inputTestEmptyText.FontSize = 14;
            inputTestEmptyText.TextAlignment = TextAlignment.Center;
            inputTestEmptyText.TextWrapping = TextWrapping.Wrap;
            inputTestEmptyText.MaxWidth = 390;
            inputTestEmptyText.HorizontalAlignment = HorizontalAlignment.Center;
            inputTestEmptyText.VerticalAlignment = VerticalAlignment.Center;
            chipHost.Children.Add(inputTestEmptyText);
            Grid.SetRow(chipHost, 4);
            inputBody.Children.Add(chipHost);
            cards.Children.Add(LabVisualStyles.CreateSectionCard(inputBody));

            StackPanel report = new StackPanel { Margin = new Thickness(22, 20, 22, 20) };
            report.Children.Add(new TextBlock { Text = "检测结论", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            inputTestReportText = new TextBlock { Text = "连接手柄后开始记录。", Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Margin = new Thickness(0, 14, 0, 0) };
            report.Children.Add(inputTestReportText);
            Border reportCard = LabVisualStyles.CreateMetricCard(report);
            Grid.SetColumn(reportCard, 2);
            cards.Children.Add(reportCard);
            Grid.SetRow(cards, 2);
            page.Children.Add(cards);
            return page;
        }

        private void ShowPage(int page)
        {
            if (homePage == null || visualizerPage == null || inputTestPage == null || stickDriftTestPage == null || motionPage == null || rumblePage == null || healthCheckPage == null || historyReportsPage == null || settingsPage == null) return;
            if (page == 4 && motionPageButton != null && motionPageButton.Visibility != Visibility.Visible)
            {
                if (noticeBanner != null) noticeBanner.Show(ProductNoticeKind.Info, "当前设备不提供 DualSense 专属检测", "连接支持触摸板或运动传感器的 DualSense 后，此入口会自动出现。", null, null);
                page = 1;
            }
            int previousPage = currentPage;
            currentPage = Math.Max(0, Math.Min(8, page));
            page = currentPage;
            if (page != 3)
            {
                if (stickDriftTestEngine.IsActive) stickDriftTestEngine.Cancel("已离开摇杆检测页面，检测未完成");
                if (joystickTestPage != null) joystickTestPage.Cancel("已离开摇杆检测页面，检测已取消");
                ClearStickTestVisualState();
            }
            if (previousPage == 5 && page != 5)
            {
                if (rumbleStudioPage != null) rumbleStudioPage.CancelForPageLeave();
                else rumbleController.Stop("已离开震动测试页面，震动已停止");
            }
            if (previousPage == 6 && page != 6 && healthCheckView != null) healthCheckView.CancelForPageLeave();
            if (previousPage == 4 && page != 4 && dualSenseAdvancedPage != null) dualSenseAdvancedPage.CancelForPageLeave();
            homePage.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
            visualizerPage.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
            inputTestPage.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;
            stickDriftTestPage.Visibility = page == 3 ? Visibility.Visible : Visibility.Collapsed;
            motionPage.Visibility = page == 4 ? Visibility.Visible : Visibility.Collapsed;
            rumblePage.Visibility = page == 5 ? Visibility.Visible : Visibility.Collapsed;
            healthCheckPage.Visibility = page == 6 ? Visibility.Visible : Visibility.Collapsed;
            historyReportsPage.Visibility = page == 7 ? Visibility.Visible : Visibility.Collapsed;
            settingsPage.Visibility = page == 8 ? Visibility.Visible : Visibility.Collapsed;
            UpdatePageButton(homePageButton, page == 0);
            UpdatePageButton(visualizerPageButton, page == 1);
            UpdatePageButton(inputTestPageButton, page == 2);
            UpdatePageButton(stickDriftPageButton, page == 3);
            UpdatePageButton(motionPageButton, page == 4);
            UpdatePageButton(rumblePageButton, page == 5);
            UpdatePageButton(healthCheckPageButton, page == 6);
            UpdatePageButton(historyPageButton, page == 7);
            UpdatePageButton(settingsPageButton, page == 8);
            if (page == 7 && historyReportsView != null) historyReportsView.Refresh();
            if (page == 8 && settingsView != null) settingsView.RefreshFromSettings();
            UIElement visiblePage = page == 0 ? homePage : page == 1 ? visualizerPage : page == 2 ? inputTestPage : page == 3 ? stickDriftTestPage : page == 4 ? motionPage : page == 5 ? rumblePage : page == 6 ? healthCheckPage : page == 7 ? historyReportsPage : settingsPage;
            LabVisualStyles.FadeIn(visiblePage, reducedMotion);
            if (controllerNavigationEnabled)
            {
                visiblePage.UpdateLayout();
                RebuildControllerNavigationTargets(true);
            }
        }

        // Controller navigation is intentionally opt-in so a formal button test
        // cannot accidentally turn an A/B/LB/RB verification into a UI action.
        // View + Menu is available on both normalized Xbox and DualSense states.
        private void HandleControllerNavigation(InputSnapshot state)
        {
            if (demoMode || state == null || !state.Connected)
            {
                ResetControllerNavigationInput();
                return;
            }

            ushort buttons = state.Buttons;
            const ushort ViewMask = 0x0020;
            const ushort MenuMask = 0x0010;
            const ushort ComboMask = ViewMask | MenuMask;
            bool comboHeld = (buttons & ComboMask) == ComboMask;
            if (comboHeld && !controllerNavigationComboLatched)
            {
                controllerNavigationComboLatched = true;
                controllerNavigationEnabled = !controllerNavigationEnabled;
                controllerNavigationHeldDpad = 0;
                controllerNavigationNextRepeatUtc = DateTime.MinValue;
                if (controllerNavigationEnabled) RebuildControllerNavigationTargets(true);
                else ClearControllerNavigationSelection();
                if (footerStatus != null)
                {
                    footerStatus.Text = controllerNavigationEnabled
                        ? "手柄导航已开启：十字键/左摇杆导航 · A 确认 · B 回首页 · LB/RB 切页 · LT/RT 滚动 · View+Menu 关闭"
                        : "手柄导航已关闭。";
                }
            }
            else if (!comboHeld) controllerNavigationComboLatched = false;

            if (!controllerNavigationEnabled)
            {
                controllerNavigationPreviousButtons = buttons;
                controllerNavigationPreviousLeftTrigger = state.LeftTrigger;
                controllerNavigationPreviousRightTrigger = state.RightTrigger;
                return;
            }

            ushort pressed = (ushort)(buttons & ~controllerNavigationPreviousButtons);
            DateTime now = DateTime.UtcNow;
            if ((pressed & 0x0100) != 0) ShowControllerPage(AdjacentPrimaryPage(-1));
            if ((pressed & 0x0200) != 0) ShowControllerPage(AdjacentPrimaryPage(1));
            if ((pressed & 0x1000) != 0) InvokeControllerFocusedAction();
            if ((pressed & 0x2000) != 0) ShowControllerPage(1);

            ushort dpad = (ushort)(buttons & 0x000F);
            ushort direction = dpad != 0 ? dpad : GetLeftStickNavigationDirection(state);
            if (direction == 0)
            {
                controllerNavigationHeldDpad = 0;
                controllerNavigationNextRepeatUtc = DateTime.MinValue;
            }
            else if (direction != controllerNavigationHeldDpad || now >= controllerNavigationNextRepeatUtc)
            {
                bool repeatingDirection = direction == controllerNavigationHeldDpad;
                NavigateControllerSelection(direction);
                controllerNavigationHeldDpad = direction;
                controllerNavigationNextRepeatUtc = now.AddMilliseconds(repeatingDirection ? 105 : 260);
            }

            if (state.LeftTrigger >= 153 && controllerNavigationPreviousLeftTrigger < 153) ScrollControllerPage(-80);
            if (state.RightTrigger >= 153 && controllerNavigationPreviousRightTrigger < 153) ScrollControllerPage(80);
            controllerNavigationPreviousButtons = buttons;
            controllerNavigationPreviousLeftTrigger = state.LeftTrigger;
            controllerNavigationPreviousRightTrigger = state.RightTrigger;

            // Keep the mode discoverable after the normal connection/status
            // updater has run earlier in the same render tick.
            if (footerStatus != null)
                footerStatus.Text = "手柄导航：十字键/左摇杆导航 · A 确认 · B 首页 · LB/RB 切页 · LT/RT 滚动 · View+Menu 关闭";
        }

        private static ushort GetLeftStickNavigationDirection(InputSnapshot state)
        {
            if (state == null) return 0;
            double x = state.LeftNormalizedX;
            double y = state.LeftNormalizedY;
            const double threshold = 0.72;
            if (Math.Abs(x) < threshold && Math.Abs(y) < threshold) return 0;
            if (Math.Abs(y) >= Math.Abs(x)) return y >= 0 ? (ushort)0x0001 : (ushort)0x0002;
            return x < 0 ? (ushort)0x0004 : (ushort)0x0008;
        }

        private void ResetControllerNavigationInput()
        {
            controllerNavigationPreviousButtons = 0;
            controllerNavigationPreviousLeftTrigger = 0;
            controllerNavigationPreviousRightTrigger = 0;
            controllerNavigationHeldDpad = 0;
            controllerNavigationNextRepeatUtc = DateTime.MinValue;
            controllerNavigationComboLatched = false;
        }

        private void ShowControllerPage(int page)
        {
            ShowPage(page);
        }

        private int AdjacentPrimaryPage(int direction)
        {
            List<int> pages = new List<int> { 1, 6, 3, 5 };
            if (motionPageButton == null || motionPageButton.Visibility == Visibility.Visible) pages.Add(4);
            pages.Add(7);
            pages.Add(8);
            int index = pages.IndexOf(currentPage);
            if (index < 0) return direction >= 0 ? pages[0] : pages[pages.Count - 1];
            index = (index + (direction >= 0 ? 1 : -1) + pages.Count) % pages.Count;
            return pages[index];
        }

        private UIElement CurrentPageRoot
        {
            get
            {
                return currentPage == 0 ? homePage : currentPage == 1 ? visualizerPage : currentPage == 2 ? inputTestPage : currentPage == 3 ? stickDriftTestPage : currentPage == 4 ? motionPage : currentPage == 5 ? rumblePage : currentPage == 6 ? healthCheckPage : currentPage == 7 ? historyReportsPage : settingsPage;
            }
        }

        private void RebuildControllerNavigationTargets(bool selectFirst)
        {
            Control previous = controllerNavigationHighlightedTarget;
            ClearControllerNavigationHighlight();
            controllerNavigationTargets.Clear();
            CollectControllerNavigationTargets(CurrentPageRoot);
            // The professional stick page keeps its trace toggle in a ScrollViewer.
            // Add it explicitly because WPF may not materialize off-viewport logical
            // children during the navigation self-test or at compact window sizes.
            if (currentPage == 3) AddControllerNavigationTarget(stickTestThreeRunsCheck);
            // The visualizer is intentionally almost pure rendering, so it has
            // no page-body buttons in some device states. Keep D-pad/A useful
            // there by exposing the always-visible shell controls as a fallback.
            if (controllerNavigationTargets.Count == 0)
            {
                AddControllerNavigationTarget(controllerSelectorButton);
                AddControllerNavigationTarget(controllerFamilySelectorButton);
                AddControllerNavigationTarget(demoModeButton);
                AddControllerNavigationTarget(homePageButton);
                AddControllerNavigationTarget(visualizerPageButton);
                AddControllerNavigationTarget(inputTestPageButton);
                AddControllerNavigationTarget(stickDriftPageButton);
                AddControllerNavigationTarget(motionPageButton);
                AddControllerNavigationTarget(rumblePageButton);
                AddControllerNavigationTarget(healthCheckPageButton);
                AddControllerNavigationTarget(historyPageButton);
                AddControllerNavigationTarget(settingsPageButton);
            }
            if (controllerNavigationTargets.Count == 0)
            {
                controllerNavigationTargetIndex = -1;
                return;
            }

            int preservedIndex = selectFirst || previous == null ? -1 : controllerNavigationTargets.IndexOf(previous);
            controllerNavigationTargetIndex = preservedIndex >= 0 ? preservedIndex : 0;
            HighlightControllerNavigationTarget();
        }

        private void CollectControllerNavigationTargets(DependencyObject root)
        {
            if (root == null) return;
            Control control = root as Control;
            if (control != null && control.IsVisible && control.IsEnabled && (control is Button || control is CheckBox))
                controllerNavigationTargets.Add(control);
            int childCount;
            try { childCount = VisualTreeHelper.GetChildrenCount(root); }
            catch (InvalidOperationException) { return; }
            for (int i = 0; i < childCount; i++) CollectControllerNavigationTargets(VisualTreeHelper.GetChild(root, i));
        }

        private void AddControllerNavigationTarget(Control control)
        {
            if (control != null && control.IsVisible && control.IsEnabled && !controllerNavigationTargets.Contains(control))
                controllerNavigationTargets.Add(control);
        }

        private void NavigateControllerSelection(ushort dpad)
        {
            if (controllerNavigationTargets.Count == 0) RebuildControllerNavigationTargets(true);
            if (controllerNavigationTargets.Count == 0) return;
            bool backwards = (dpad & 0x0001) != 0 || (dpad & 0x0004) != 0;
            controllerNavigationTargetIndex += backwards ? -1 : 1;
            if (controllerNavigationTargetIndex < 0) controllerNavigationTargetIndex = controllerNavigationTargets.Count - 1;
            if (controllerNavigationTargetIndex >= controllerNavigationTargets.Count) controllerNavigationTargetIndex = 0;
            HighlightControllerNavigationTarget();
        }

        private void HighlightControllerNavigationTarget()
        {
            if (controllerNavigationTargetIndex < 0 || controllerNavigationTargetIndex >= controllerNavigationTargets.Count) return;
            Control target = controllerNavigationTargets[controllerNavigationTargetIndex];
            if (target == null || !target.IsVisible || !target.IsEnabled)
            {
                RebuildControllerNavigationTargets(true);
                return;
            }
            ClearControllerNavigationHighlight();
            controllerNavigationHighlightedTarget = target;
            controllerNavigationOriginalBorderBrush = target.BorderBrush;
            controllerNavigationOriginalBorderThickness = target.BorderThickness;
            DeviceCard deviceCardTarget = target as DeviceCard;
            if (deviceCardTarget != null) deviceCardTarget.SetControllerNavigationSelected(true);
            else
            {
                target.BorderBrush = Palette.BlueBrush;
                target.BorderThickness = new Thickness(2);
            }
            target.Focus();
            Keyboard.Focus(target);
            if (footerStatus != null) footerStatus.Text = "手柄导航 · 已选择：" + ControllerNavigationTargetLabel(target) + " · A 确认";
        }

        private void ClearControllerNavigationHighlight()
        {
            if (controllerNavigationHighlightedTarget != null)
            {
                DeviceCard deviceCardTarget = controllerNavigationHighlightedTarget as DeviceCard;
                if (deviceCardTarget != null) deviceCardTarget.SetControllerNavigationSelected(false);
                else
                {
                    controllerNavigationHighlightedTarget.BorderBrush = controllerNavigationOriginalBorderBrush;
                    controllerNavigationHighlightedTarget.BorderThickness = controllerNavigationOriginalBorderThickness;
                }
            }
            controllerNavigationHighlightedTarget = null;
            controllerNavigationOriginalBorderBrush = null;
            controllerNavigationOriginalBorderThickness = new Thickness(0);
        }

        private void ClearControllerNavigationSelection()
        {
            ClearControllerNavigationHighlight();
            controllerNavigationTargets.Clear();
            controllerNavigationTargetIndex = -1;
        }

        private static string ControllerNavigationTargetLabel(Control target)
        {
            string name = AutomationProperties.GetName(target);
            if (!string.IsNullOrWhiteSpace(name)) return name;
            ContentControl content = target as ContentControl;
            string label = content == null ? null : content.Content as string;
            return string.IsNullOrWhiteSpace(label) ? "当前操作项" : label;
        }

        private void InvokeControllerFocusedAction()
        {
            if (controllerNavigationTargets.Count == 0) RebuildControllerNavigationTargets(true);
            if (controllerNavigationTargetIndex < 0 || controllerNavigationTargetIndex >= controllerNavigationTargets.Count) return;
            Control target = controllerNavigationTargets[controllerNavigationTargetIndex];
            Button button = target as Button;
            if (button != null && button.IsEnabled && button.IsVisible)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RebuildControllerNavigationTargets(true);
                return;
            }
            CheckBox checkBox = target as CheckBox;
            if (checkBox != null && checkBox.IsEnabled && checkBox.IsVisible)
                checkBox.IsChecked = !(checkBox.IsChecked ?? false);
        }

        private void ScrollControllerPage(double delta)
        {
            ScrollViewer viewer = FindAncestorScrollViewer(controllerNavigationHighlightedTarget) ?? FindFirstScrollViewer(CurrentPageRoot);
            if (viewer == null) return;
            viewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(viewer.ScrollableHeight, viewer.VerticalOffset + delta)));
            viewer.UpdateLayout();
        }

        private static ScrollViewer FindAncestorScrollViewer(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                ScrollViewer viewer = current as ScrollViewer;
                if (viewer != null) return viewer;
                try { current = VisualTreeHelper.GetParent(current); }
                catch (InvalidOperationException) { return null; }
            }
            return null;
        }

        private static ScrollViewer FindFirstScrollViewer(DependencyObject root)
        {
            if (root == null) return null;
            ScrollViewer direct = root as ScrollViewer;
            if (direct != null) return direct;
            int childCount;
            try { childCount = VisualTreeHelper.GetChildrenCount(root); }
            catch (InvalidOperationException) { return null; }
            for (int i = 0; i < childCount; i++)
            {
                ScrollViewer found = FindFirstScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }

        // Development-only exercise of the real controller-navigation state
        // machine. It never feeds a formal device report, drift test, or button
        // test; its snapshots are confined to the --controller-navigation-selftest
        // process started from Program.Main.
        internal string RunControllerNavigationSelfTest()
        {
            UpdateLayout();
            InputSnapshot input = new InputSnapshot { Connected = true, DeviceName = "Navigation self-test" };
            controllerNavigationEnabled = false;
            ClearControllerNavigationSelection();
            ResetControllerNavigationInput();

            // View + Menu enables the mode and selects a visible page action.
            input.Buttons = 0x0030;
            HandleControllerNavigation(input);
            if (!controllerNavigationEnabled || controllerNavigationTargets.Count == 0 || controllerNavigationTargetIndex < 0)
                throw new InvalidOperationException("Controller navigation did not create a selectable target.");
            input.Buttons = 0;
            HandleControllerNavigation(input);

            // D-pad must move our explicit selection without relying on WPF's
            // keyboard focus traversal.
            int initialIndex = controllerNavigationTargetIndex;
            input.Buttons = 0x0002;
            HandleControllerNavigation(input);
            if (controllerNavigationTargets.Count > 1 && controllerNavigationTargetIndex == initialIndex)
                throw new InvalidOperationException("D-pad did not advance the controller selection.");
            input.Buttons = 0;
            HandleControllerNavigation(input);

            // A toggles a selected checkbox through the same invocation path
            // that regular page actions use, without starting a test session.
            ShowPage(3);
            UpdateLayout();
            int checkIndex = controllerNavigationTargets.IndexOf(stickTestThreeRunsCheck);
            if (checkIndex < 0) throw new InvalidOperationException("Stick-test checkbox is not discoverable by controller navigation.");
            bool originalCheck = stickTestThreeRunsCheck.IsChecked ?? false;
            controllerNavigationTargetIndex = checkIndex;
            HighlightControllerNavigationTarget();
            input.Buttons = 0x1000;
            HandleControllerNavigation(input);
            if ((stickTestThreeRunsCheck.IsChecked ?? false) == originalCheck)
                throw new InvalidOperationException("A did not invoke the selected controller action.");
            stickTestThreeRunsCheck.IsChecked = originalCheck;
            input.Buttons = 0;
            HandleControllerNavigation(input);

            // Trigger scrolling is page-based, not focus-based. Assert it when
            // the page exposes a scrollable range; compact window sizes may not.
            ScrollViewer viewer = FindFirstScrollViewer(CurrentPageRoot);
            bool scrollVerified = viewer != null && viewer.ScrollableHeight > 1;
            if (scrollVerified)
            {
                viewer.ScrollToTop();
                input.RightTrigger = 255;
                HandleControllerNavigation(input);
                if (viewer.VerticalOffset <= 0.01) throw new InvalidOperationException("RT did not scroll the current page.");
                input.RightTrigger = 0;
                HandleControllerNavigation(input);
            }

            // Page routing is a direct mapping and must be independent of the
            // currently selected page action.
            ShowPage(1);
            input.Buttons = 0;
            HandleControllerNavigation(input);
            input.Buttons = 0x0200;
            HandleControllerNavigation(input);
            if (currentPage != 6) throw new InvalidOperationException("RB did not advance through the primary product navigation.");
            input.Buttons = 0;
            HandleControllerNavigation(input);
            input.Buttons = 0x0100;
            HandleControllerNavigation(input);
            if (currentPage != 1) throw new InvalidOperationException("LB did not return through the primary product navigation.");
            input.Buttons = 0;
            HandleControllerNavigation(input);
            ShowPage(2);
            input.Buttons = 0x2000;
            HandleControllerNavigation(input);
            if (currentPage != 1) throw new InvalidOperationException("B did not return to the live monitor.");

            controllerNavigationEnabled = false;
            ClearControllerNavigationSelection();
            return "Controller navigation self-test passed: opt-in, D-pad selection, A action, page routing, and " + (scrollVerified ? "RT scrolling" : "scroll fallback") + ".";
        }

        internal string RenderProductUiAudit(string directory)
        {
            string root = System.IO.Path.GetFullPath(directory);
            Directory.CreateDirectory(root);
            RenderProductUiPage(System.IO.Path.Combine(root, "live-1920x1080-at-150.png"), 1, 1280, 720);
            RenderProductUiPage(System.IO.Path.Combine(root, "joystick-1920x1080-at-125.png"), 3, 1536, 864);
            RenderProductUiPage(System.IO.Path.Combine(root, "rumble-1920x1080-at-125.png"), 5, 1536, 864);
            RenderProductUiPage(System.IO.Path.Combine(root, "health-1920x1080-at-125.png"), 6, 1536, 864);
            RenderProductUiPage(System.IO.Path.Combine(root, "settings-1920x1080-at-125.png"), 8, 1536, 864);
            RenderProductUiPage(System.IO.Path.Combine(root, "history-2560x1600-at-125.png"), 7, 2048, 1280);
            return "Product UI render audit created: " + root;
        }

        private void RenderProductUiPage(string path, int page, int width, int height)
        {
            Width = width;
            Height = height;
            bool savedReducedMotion = reducedMotion;
            reducedMotion = true;
            ShowPage(page);
            FrameworkElement root = Content as FrameworkElement;
            if (root == null) throw new InvalidOperationException("Window content is not renderable.");
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) encoder.Save(stream);
            reducedMotion = savedReducedMotion;
        }

        private static void UpdatePageButton(Button button, bool selected)
        {
            if (button == null) return;
            button.Background = selected ? new SolidColorBrush(Color.FromArgb(62, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)) : Brushes.Transparent;
            button.BorderBrush = selected ? new SolidColorBrush(Color.FromArgb(120, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)) : Brushes.Transparent;
            button.Foreground = selected ? Palette.TextBrush : Palette.MutedBrush;
        }

        private void UpdateInputTestPage(ControllerState state)
        {
            inputTestEngine.Update(state);
            bool formalInput = state != null && state.IsConnected && state.HasRealInput;
            if (formalInput) stickTriggerTestEngine.Update(state);
            ControllerTestReport report = inputTestEngine.BuildReport(state, stickTriggerTestEngine);
            if (inputTestProgressText != null)
            {
                int passed = 0;
                IList<ControllerButtonTestResult> results = inputTestEngine.Results;
                List<ControllerButtonTestResult> orderedResults = new List<ControllerButtonTestResult>();
                for (int i = 0; i < results.Count; i++) if (!results[i].Passed) orderedResults.Add(results[i]);
                for (int i = 0; i < results.Count; i++) if (results[i].Passed) orderedResults.Add(results[i]);
                results = orderedResults;
                for (int i = 0; i < results.Count; i++) if (results[i].Passed) passed++;
                inputTestProgressText.Text = formalInput ? string.Format(CultureInfo.InvariantCulture, "{0} · {1}/{2} 已通过", state.DeviceName, passed, results.Count) : "正式按键检测不可用：" + (state == null ? "未连接" : state.InputSourceLabel);
                inputTestProgressText.Foreground = formalInput && results.Count > 0 && passed == results.Count ? Palette.GreenBrush : (formalInput ? Palette.BlueBrush : Palette.WarningBrush);
                if (inputTestEmptyText != null)
                {
                    inputTestEmptyText.Visibility = formalInput && results.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                    inputTestEmptyText.Text = formalInput
                        ? "等待第一个按键输入。\n已按过的按键会显示为绿色勾选。"
                        : "连接真实 Xbox XInput 或 DualSense HID 手柄后，按键网格会显示在这里。\n动态演示不会生成正式检测结果。";
                }
                if (inputTestProgressBar != null)
                {
                    inputTestProgressBar.Value = formalInput && results.Count > 0 ? (double)passed / results.Count : 0;
                    inputTestProgressBar.Foreground = formalInput && results.Count > 0 && passed == results.Count ? Palette.GreenBrush : Palette.BlueBrush;
                }
                string signature = (state == null ? string.Empty : state.DeviceId) + ":" + report.OverallStatus + ":" + (state == null ? 0 : state.Buttons) + ":" + (state == null ? 0 : state.LeftTrigger) + ":" + (state == null ? 0 : state.RightTrigger);
                for (int i = 0; i < results.Count; i++) signature += results[i].Id + results[i].Passed;
                if (!string.Equals(signature, renderedInputTestSignature, StringComparison.Ordinal))
                {
                    renderedInputTestSignature = signature;
                    inputTestChipPanel.Children.Clear();
                    for (int i = 0; i < results.Count; i++)
                    {
                        ControllerButtonTestResult item = results[i];
                        bool active = formalInput && ControllerInputTestEngine.IsCurrentlyPressed(item.Id, state);
                        Brush background = item.Passed
                            ? new SolidColorBrush(Color.FromArgb(38, Palette.Green.R, Palette.Green.G, Palette.Green.B))
                            : active
                                ? new SolidColorBrush(Color.FromArgb(58, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B))
                                : Palette.Surface2Brush;
                        Brush border = item.Passed ? Palette.GreenBrush : active ? Palette.BlueBrush : Palette.BorderSubtleBrush;
                        Brush foreground = item.Passed ? Palette.GreenBrush : active ? Palette.BlueBrush : Palette.TextBrush;
                        Border chip = new Border
                        {
                            Style = LabVisualStyles.MetricCardStyle,
                            Width = 118,
                            Height = 42,
                            CornerRadius = LabVisualStyles.ControlRadius,
                            Background = background,
                            BorderBrush = border,
                            BorderThickness = new Thickness(1),
                            Margin = new Thickness(0, 0, 8, 8),
                            Padding = new Thickness(10, 7, 10, 7),
                            Child = new TextBlock { Text = (item.Passed ? "✓  " : active ? "●  " : "·  ") + item.Label, Foreground = foreground, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }
                        };
                        inputTestChipPanel.Children.Add(chip);
                    }
                }
            }
            if (inputTestReportText != null)
            {
                inputTestReportText.Text = BuildInputTestReportText(state, report);
            }
        }

        private static string BuildInputTestReportText(ControllerState state, ControllerTestReport report)
        {
            if (state == null || report == null || !report.IsFormalInput)
            {
                return BuildDeviceInputIdentity(state) + "\n\n当前数据不能用于正式按键检测。\n请连接真实 Xbox XInput 或 DualSense HID 手柄。";
            }
            string unpassed = report.UnpassedButtons == null || report.UnpassedButtons.Count == 0 ? "全部通过" : string.Join("、", report.UnpassedButtons.ToArray());
            bool complete = report.ButtonTestPassedCount == report.ButtonTestTotalCount && report.ButtonTestTotalCount > 0;
            return string.Format(CultureInfo.InvariantCulture,
                "{0}\n\n{1}\n\n未通过\n{2}\n\n扳机峰值\n左 {3:0}% · 右 {4:0}%\n回零：{5}",
                BuildDeviceInputIdentity(state),
                complete ? "全部按键已通过" : string.Format(CultureInfo.InvariantCulture, "已完成 {0}/{1}", report.ButtonTestPassedCount, report.ButtonTestTotalCount),
                unpassed, report.LeftTriggerMaximum * 100.0, report.RightTriggerMaximum * 100.0,
                report.TriggerReturnToZero ? "已回零" : "待检查");
        }

        private UIElement BuildTitleBar()
        {
            Grid title = new Grid { Background = Brushes.Transparent };
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                if (e.ClickCount == 2) ToggleMaximize();
                else DragMove();
            };

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Border mark = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = LabVisualStyles.ControlRadius,
                BorderBrush = Palette.BorderSubtleBrush,
                BorderThickness = new Thickness(1),
                Background = Palette.Surface2Brush,
                Child = new TextBlock { Text = "CL", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Palette.BlueBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            brand.Children.Add(mark);
            brand.Children.Add(new TextBlock { Text = "手柄实验室", FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = Palette.TextBrush, Margin = new Thickness(10, 0, 0, 1), VerticalAlignment = VerticalAlignment.Center });
            title.Children.Add(brand);

            StackPanel navigation = primaryNavigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            homePageButton = MakeButton("设备选择", false);
            visualizerPageButton = MakeButton("实时监视", false);
            inputTestPageButton = MakeButton("按键检测", false);
            healthCheckPageButton = MakeButton("完整检测", false);
            stickDriftPageButton = MakeButton("摇杆检测", false);
            rumblePageButton = MakeButton("震动测试", false);
            motionPageButton = MakeButton("DualSense 高级", false);
            motionPageButton.Visibility = sonyDemoMode || selectedControllerFamily == ControllerFamily.PlayStation ? Visibility.Visible : Visibility.Collapsed;
            historyPageButton = MakeButton("历史报告", false);
            settingsPageButton = MakeButton("设置", false);
            Button[] pages = { visualizerPageButton, healthCheckPageButton, stickDriftPageButton, rumblePageButton, motionPageButton, historyPageButton, settingsPageButton };
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].MinWidth = 72;
                pages[i].Height = 34;
                pages[i].FontSize = 12;
                pages[i].Padding = new Thickness(8, 2, 8, 3);
                pages[i].Margin = new Thickness(3, 0, 3, 0);
                navigation.Children.Add(pages[i]);
            }
            visualizerPageButton.ToolTip = "实时监视：查看手柄、摇杆和扳机的实时反馈";
            healthCheckPageButton.ToolTip = "完整检测：按步骤完成一键手柄健康检测";
            stickDriftPageButton.ToolTip = "摇杆检测：静止漂移、圆周和回中测试";
            rumblePageButton.ToolTip = "震动测试：安全预设、校准与时间线";
            motionPageButton.ToolTip = "DualSense 高级功能：触摸板、陀螺仪与能力状态";
            historyPageButton.ToolTip = "历史报告：查看、导出和比较检测结果";
            settingsPageButton.ToolTip = "设置：显示、检测、震动和本地数据";
            homePageButton.Click += delegate { ShowPage(0); };
            visualizerPageButton.Click += delegate { ShowPage(1); };
            inputTestPageButton.Click += delegate { ShowPage(2); };
            stickDriftPageButton.Click += delegate { ShowPage(3); };
            motionPageButton.Click += delegate { ShowPage(4); };
            rumblePageButton.Click += delegate { ShowPage(5); };
            healthCheckPageButton.Click += delegate { ShowPage(6); };
            historyPageButton.Click += delegate { ShowPage(7); };
            settingsPageButton.Click += delegate { ShowPage(8); };
            Grid.SetColumn(navigation, 1);
            title.Children.Add(navigation);
            UpdatePageButton(visualizerPageButton, true);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button min = MakeWindowButton("\uE921", "最小化");
            min.Click += delegate { WindowState = WindowState.Minimized; };
            Button max = MakeWindowButton("\uE922", "最大化或还原");
            max.Click += delegate { ToggleMaximize(); };
            Button close = MakeWindowButton("\uE8BB", "关闭");
            close.Click += delegate { Close(); };
            buttons.Children.Add(min);
            buttons.Children.Add(max);
            buttons.Children.Add(close);
            Grid.SetColumn(buttons, 2);
            title.Children.Add(buttons);
            return title;
        }

        private Button MakeWindowButton(string glyph, string accessibleName)
        {
            Button button = new Button
            {
                Content = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = Palette.TextBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Template = CreateButtonTemplate(),
                Width = 54,
                Height = 58,
                Focusable = true,
                ToolTip = accessibleName
            };
            AutomationProperties.SetName(button, accessibleName);
            AutomationProperties.SetHelpText(button, "使用 Enter 或空格键执行。");
            button.MouseEnter += delegate { button.Background = new SolidColorBrush(Color.FromRgb(31, 43, 52)); };
            button.MouseLeave += delegate { button.Background = Brushes.Transparent; };
            return button;
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool compact = ActualWidth < 1240;
            if (footerRightPanel != null) footerRightPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            UpdatePrimaryNavigationLayout();
            UpdateDeviceCardResponsiveLayout();
        }

        private void UpdatePrimaryNavigationLayout()
        {
            if (visualizerPageButton == null) return;
            bool compact = ActualWidth > 0 && ActualWidth < 1360;
            SetNavigationLabel(visualizerPageButton, compact ? "监视" : "实时监视", compact ? 62 : 82);
            SetNavigationLabel(healthCheckPageButton, "完整检测", compact ? 76 : 88);
            SetNavigationLabel(stickDriftPageButton, compact ? "摇杆" : "摇杆检测", compact ? 62 : 84);
            SetNavigationLabel(rumblePageButton, compact ? "震动" : "震动测试", compact ? 62 : 84);
            SetNavigationLabel(motionPageButton, compact ? "DualSense" : "DualSense 高级", compact ? 82 : 112);
            SetNavigationLabel(historyPageButton, compact ? "报告" : "历史报告", compact ? 62 : 84);
            SetNavigationLabel(settingsPageButton, "设置", 62);
        }

        private static void SetNavigationLabel(Button button, string text, double width)
        {
            if (button == null) return;
            button.Content = text;
            button.Width = width;
        }

        private void UpdateDeviceCardResponsiveLayout()
        {
            if (deviceCard == null || deviceCard.ActualWidth < 1) return;

            // The left content column is deliberately narrower than the whole window.
            // Use its measured card width rather than the window width; otherwise a
            // 1440px window still lets the header's fixed metadata push the PS badge
            // and action buttons beyond the card's viewport.
            double cardWidth = deviceCard.ActualWidth;
            bool showMetadata = cardWidth >= 1240;
            bool showRefreshBadge = cardWidth >= 900;
            // Xbox's short status (for example "未检测到手柄") can still keep
            // its family selector at the minimum width.  The selector only yields
            // space when the DS touch-status sentence is genuinely long.
            bool hasLongConnectionStatus = connectionText != null && !string.IsNullOrEmpty(connectionText.Text) && connectionText.Text.Length > 14;
            bool showFamilySelector = cardWidth >= 620 && !hasLongConnectionStatus;

            if (deviceMetadataPanel != null)
            {
                deviceMetadataPanel.Visibility = showMetadata ? Visibility.Visible : Visibility.Collapsed;
            }
            if (refreshRateBadge != null) refreshRateBadge.Visibility = showRefreshBadge ? Visibility.Visible : Visibility.Collapsed;
            if (controllerFamilySelectorButton != null) controllerFamilySelectorButton.Visibility = showFamilySelector ? Visibility.Visible : Visibility.Collapsed;
            if (connectionMethodBadge != null) connectionMethodBadge.Visibility = Visibility.Visible;

            // Keep the important connection sentence inside the second line at the
            // absolute minimum window size instead of allowing a StackPanel child to
            // extend past the card. Buttons are removed before text is truncated.
            if (connectionText != null)
            {
                double reserved = 24;
                if (connectionMethodBadge != null) reserved += Math.Max(122, connectionMethodBadge.ActualWidth + 12);
                if (showRefreshBadge) reserved += 156;
                if (showFamilySelector) reserved += 132;
                if (demoModeButton != null && demoModeButton.Visibility == Visibility.Visible) reserved += 96;
                double available = Math.Max(156, cardWidth - 44 - 57 - reserved);
                connectionText.MaxWidth = available;
                connectionText.TextTrimming = TextTrimming.CharacterEllipsis;
                connectionText.TextWrapping = TextWrapping.NoWrap;
            }
        }

        private UIElement BuildFooter()
        {
            Grid footer = new Grid { Margin = new Thickness(26, 0, 26, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Border info = new Border { Width = 17, Height = 17, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1), BorderBrush = Palette.MutedBrush, Child = new TextBlock { Text = "i", FontFamily = new FontFamily("Georgia"), FontSize = 11, Foreground = Palette.MutedBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            footerStatus = new TextBlock { Text = demoMode ? "动态演示模式：手柄反馈会自动变化。" : "移动摇杆、扣动扳机或按下按键，查看实时动态反馈。", Foreground = Palette.MutedBrush, FontSize = 12, Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetLiveSetting(footerStatus, AutomationLiveSetting.Polite);
            left.Children.Add(info);
            left.Children.Add(footerStatus);
            footer.Children.Add(left);
            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            footerRightPanel = right;
            right.Children.Add(new TextBlock { Text = "显示参考死区仅影响诊断显示", Foreground = Palette.MutedBrush, FontSize = 12 });
            right.Children.Add(new Border { Width = 1, Height = 17, Background = Palette.MutedBrush, Margin = new Thickness(25, 0, 25, 0), Opacity = 0.65 });
            right.Children.Add(new TextBlock { Text = "范围：-32768 至 32767", Foreground = Palette.MutedBrush, FontSize = 12 });
            Grid.SetColumn(right, 1);
            footer.Children.Add(right);
            return footer;
        }

        private Border Card(UIElement child)
        {
            return LabVisualStyles.CreateSectionCard(child);
        }

        private Button MakeButton(string text, bool primary)
        {
            Button button = new Button
            {
                Content = text,
                Style = primary ? LabVisualStyles.PrimaryButtonStyle : LabVisualStyles.SecondaryButtonStyle,
                Tag = primary
            };
            AutomationProperties.SetName(button, text);
            AutomationProperties.SetHelpText(button, "按 Enter 或空格键执行");
            return button;
        }

        private void SetButtonPrimary(Button button, bool primary)
        {
            if (button == null) return;
            button.Tag = primary;
            button.Style = primary ? LabVisualStyles.PrimaryButtonStyle : LabVisualStyles.SecondaryButtonStyle;
        }

        private static void ApplyButtonVisual(Button button, bool hover)
        {
            if (button == null) return;
            bool primary = button.Tag is bool && (bool)button.Tag;
            button.Style = primary ? LabVisualStyles.PrimaryButtonStyle : LabVisualStyles.SecondaryButtonStyle;
        }

        private ControlTemplate CreateButtonTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        private ContextMenu CreateDarkContextMenu(double minWidth)
        {
            return new ContextMenu
            {
                MinWidth = minWidth,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                VerticalOffset = 4,
                Padding = new Thickness(4),
                Background = new SolidColorBrush(Color.FromRgb(18, 30, 40)),
                Foreground = Palette.TextBrush,
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 82, 96)),
                BorderThickness = new Thickness(1),
                Template = CreateDarkContextMenuTemplate()
            };
        }

        private void OpenContextMenu(FrameworkElement owner)
        {
            ContextMenu menu = owner == null ? null : owner.ContextMenu;
            if (menu == null) return;
            menu.PlacementTarget = owner;
            menu.HorizontalOffset = 0;
            menu.IsOpen = true;
            menu.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (!menu.IsOpen) return;
                menu.HorizontalOffset = Math.Min(0, owner.ActualWidth - menu.ActualWidth);
            }), DispatcherPriority.Loaded);
        }

        private ControlTemplate CreateDarkContextMenuTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            presenter.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
            border.AppendChild(presenter);
            return new ControlTemplate(typeof(ContextMenu)) { VisualTree = border };
        }

        private MenuItem MakeDarkMenuItem(string header)
        {
            if (darkMenuItemStyle == null) darkMenuItemStyle = CreateDarkMenuItemStyle();
            return new MenuItem { Header = header, Style = darkMenuItemStyle, Foreground = Palette.TextBrush };
        }

        private Style CreateDarkMenuItemStyle()
        {
            Style style = new Style(typeof(MenuItem));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(13, 7, 16, 7)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(3, 0, 0, 0)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Palette.TextBrush));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, new FontFamily("Microsoft YaHei UI")));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 32.0));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "ItemBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Header") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(typeof(MenuItem)) { VisualTree = border };
            Trigger highlighted = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            highlighted.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(31, 49, 62)), "ItemBorder"));
            template.Triggers.Add(highlighted);
            Trigger isChecked = new Trigger { Property = MenuItem.IsCheckedProperty, Value = true };
            isChecked.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(45, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), "ItemBorder"));
            isChecked.Setters.Add(new Setter(Border.BorderBrushProperty, Palette.BlueBrush, "ItemBorder"));
            template.Triggers.Add(isChecked);
            Trigger disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45, "ItemBorder"));
            template.Triggers.Add(disabled);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Separator MakeDarkMenuSeparator()
        {
            FrameworkElementFactory line = new FrameworkElementFactory(typeof(Border));
            line.SetValue(Border.HeightProperty, 1.0);
            line.SetValue(Border.BackgroundProperty, Palette.BorderBrush);
            line.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
            return new Separator
            {
                Height = 9,
                Margin = new Thickness(9, 0, 9, 0),
                Focusable = false,
                IsHitTestVisible = false,
                Template = new ControlTemplate(typeof(Separator)) { VisualTree = line }
            };
        }

        private void OnDeadzoneChanged(object sender, EventArgs e)
        {
            SetTextIfChanged(leftDeadzoneText, string.Format(CultureInfo.InvariantCulture, "{0:0}%", leftDeadzone.Value * 100.0));
            SetTextIfChanged(rightDeadzoneText, string.Format(CultureInfo.InvariantCulture, "{0:0}%", rightDeadzone.Value * 100.0));
            leftPlot.Deadzone = leftDeadzone.Value;
            rightPlot.Deadzone = rightDeadzone.Value;
        }

        private void StartCalibration(object sender, RoutedEventArgs e)
        {
            if (demoMode)
            {
                footerStatus.Text = "动态演示期间不能校准；退出演示并连接真实手柄后再试。";
                return;
            }
            if (calibrating) return;
            ClearStickTestVisualState();
            if (calibrationSuggestionPending)
            {
                leftDeadzone.Value = recommendedLeftDeadzone;
                rightDeadzone.Value = recommendedRightDeadzone;
                calibrationSuggestionPending = false;
                calibrationStatusVisible = false;
                calibrationMessageUntil = DateTime.MinValue;
                calibrateButton.Content = "中心校准";
                footerStatus.Text = string.Format(CultureInfo.InvariantCulture, "已应用中心校准建议显示参考死区：左 {0:0}% · 右 {1:0}% 。", recommendedLeftDeadzone * 100.0, recommendedRightDeadzone * 100.0);
                if (!demoMode) SaveSettings();
                UpdateDiagnostics(currentState);
                return;
            }
            if (!lastConnected)
            {
                footerStatus.Text = "未检测到手柄，连接后才能开始中心校准。";
                return;
            }
            calibrating = true;
            calibrationStatusVisible = true;
            calibrationSuggestionPending = false;
            calibrationMessageUntil = DateTime.MinValue;
            calibrationStarted = DateTime.UtcNow;
            sumLX = sumLY = sumRX = sumRY = 0;
            calibrationSamples = 0;
            minLX = minLY = minRX = minRY = int.MaxValue;
            maxLX = maxLY = maxRX = maxRY = int.MinValue;
            calibrateButton.Content = "校准中 0.0 秒";
            calibrateButton.IsEnabled = false;
            diagnosticScoreText.Foreground = Palette.BlueBrush;
            SetTextIfChanged(diagnosticScoreText, "中心校准 · 准备采样");
            SetTextIfChanged(diagnosticDetailText, "保持两个摇杆居中，进度结束前请勿触碰");
            calibrationProgress.Value = 0;
            calibrationProgress.Visibility = Visibility.Visible;
            footerStatus.Text = "正在采样 2 秒，请保持两个摇杆居中。";
        }

        private void CompleteCalibration()
        {
            calibrating = false;
            calibrateButton.IsEnabled = true;
            calibrationProgress.Visibility = Visibility.Collapsed;
            if (calibrationSamples < 20)
            {
                calibrationMessageUntil = DateTime.MinValue;
                calibrateButton.Content = "重新校准";
                diagnosticScoreText.Foreground = Palette.WarningBrush;
                SetTextIfChanged(diagnosticScoreText, "校准未保存 · 采样不足");
                SetTextIfChanged(diagnosticDetailText, "确认手柄保持连接后点击重新校准");
                footerStatus.Text = "校准失败：有效样本不足，请确认手柄保持连接。";
                return;
            }

            int leftRange = Math.Max(maxLX - minLX, maxLY - minLY);
            int rightRange = Math.Max(maxRX - minRX, maxRY - minRY);
            if (leftRange > 3500 || rightRange > 3500)
            {
                calibrationMessageUntil = DateTime.MinValue;
                calibrateButton.Content = "重新校准";
                diagnosticScoreText.Foreground = Palette.WarningBrush;
                SetTextIfChanged(diagnosticScoreText, "校准未保存 · 检测到移动");
                SetTextIfChanged(diagnosticDetailText, "松开两个摇杆后点击重新校准");
                footerStatus.Text = "校准未保存：采样期间检测到摇杆移动，请松开摇杆后重试。";
                return;
            }

            offsetLX = (double)sumLX / calibrationSamples;
            offsetLY = (double)sumLY / calibrationSamples;
            offsetRX = (double)sumRX / calibrationSamples;
            offsetRY = (double)sumRY / calibrationSamples;
            double leftNoise = leftRange / 65535.0;
            double rightNoise = rightRange / 65535.0;
            recommendedLeftDeadzone = Math.Max(0.04, Math.Min(0.18, leftNoise * 1.5 + 0.02));
            recommendedRightDeadzone = Math.Max(0.04, Math.Min(0.18, rightNoise * 1.5 + 0.02));
            calibrationSuggestionPending = true;
            calibrationMessageUntil = DateTime.UtcNow.AddSeconds(4.0);
            diagnosticScoreText.Foreground = Palette.GreenBrush;
            SetTextIfChanged(diagnosticScoreText, "校准完成 · 中心偏移已保存");
            SetTextIfChanged(diagnosticDetailText, string.Format(CultureInfo.InvariantCulture, "中心校准建议显示参考死区：左 {0:0}% · 右 {1:0}%", recommendedLeftDeadzone * 100.0, recommendedRightDeadzone * 100.0));
            calibrateButton.Content = "应用死区";
            footerStatus.Text = string.Format(CultureInfo.InvariantCulture, "中心偏移已保存；采样噪声：左 {0:0.00}% · 右 {1:0.00}% 。", leftNoise * 100.0, rightNoise * 100.0);
            if (!demoMode) SaveSettings();
        }

    }
}
