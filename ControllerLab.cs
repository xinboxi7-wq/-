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






    public sealed class MainWindow : Window
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

        private UIElement BuildStickDriftTestPage()
        {
            Grid page = new Grid { Margin = new Thickness(32, 24, 32, 0) };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel();
            title.Children.Add(LabVisualStyles.CreatePageTitle("摇杆检测"));
            stickTestHintText = LabVisualStyles.CreateSecondaryText("松开摇杆后开始。系统会等待 1 秒，再连续采样 5 秒。");
            stickTestHintText.FontSize = 14;
            stickTestHintText.Margin = new Thickness(0, 7, 0, 0);
            title.Children.Add(stickTestHintText);
            stickTestStatusText = new TextBlock { Text = "连接手柄后可开始检测", Foreground = Palette.MutedBrush, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
            title.Children.Add(stickTestStatusText);
            stickTestDeviceText = new TextBlock { Text = "设备：未连接", Foreground = Palette.MutedBrush, FontFamily = new FontFamily("Consolas"), FontSize = 10.5, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            title.Children.Add(stickTestDeviceText);
            heading.Children.Add(title);

            WrapPanel actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            stickTestThreeRunsCheck = new CheckBox { Content = "连续检测 3 次", Foreground = Palette.TextBrush, FontSize = 11, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 6, 8, 2) };
            actions.Children.Add(stickTestThreeRunsCheck);
            stickTestStartButton = MakeButton("开始检测", true);
            stickTestStartButton.Click += delegate { StartStickDriftTest(); };
            stickTestRestartButton = MakeButton("重新检测", false);
            stickTestRestartButton.Click += delegate { StartStickDriftTest(); };
            stickTestStopButton = MakeButton("结束检测", false);
            stickTestStopButton.Click += delegate { EndStickDriftTest(); };
            stickRangeStartButton = MakeButton("范围测试", false);
            stickRangeStartButton.Click += delegate { StartStickRangeTest(); };
            stickRangeStopButton = MakeButton("结束范围", false);
            stickRangeStopButton.Click += delegate { EndStickRangeTest(); };
            stickTestCopyButton = MakeButton("复制结果", false);
            stickTestCopyButton.Click += delegate { CopyStickDriftResult(); };
            stickTestSaveButton = MakeButton("保存实测记录", false);
            stickTestSaveButton.Click += delegate { SaveStickTestEvidence(); };
            Button[] actionsList = { stickTestStartButton, stickTestRestartButton, stickTestStopButton, stickRangeStartButton, stickRangeStopButton, stickTestCopyButton, stickTestSaveButton };
            for (int i = 0; i < actionsList.Length; i++)
            {
                actionsList[i].Height = 34;
                actionsList[i].FontSize = 11;
                actionsList[i].Padding = new Thickness(10, 4, 10, 4);
                actionsList[i].Margin = new Thickness(4, 2, 0, 2);
                actions.Children.Add(actionsList[i]);
            }
            Grid.SetColumn(actions, 1);
            heading.Children.Add(actions);
            page.Children.Add(heading);

            Grid body = new Grid { Height = 528, VerticalAlignment = VerticalAlignment.Top };
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(382) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(132) });
            Grid sticks = new Grid();
            sticks.ColumnDefinitions.Add(new ColumnDefinition());
            sticks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            sticks.ColumnDefinitions.Add(new ColumnDefinition());
            sticks.Children.Add(BuildStickDriftCard(true));
            Border right = BuildStickDriftCard(false);
            Grid.SetColumn(right, 2);
            sticks.Children.Add(right);
            body.Children.Add(sticks);
            Border rangeCard = Card(BuildStickRangeSummary());
            Grid.SetRow(rangeCard, 2);
            body.Children.Add(rangeCard);
            Grid.SetRow(body, 2);
            page.Children.Add(body);
            return page;
        }

        private Border BuildStickDriftCard(bool left)
        {
            StickPlot plot = left ? stickTestLeftPlot : stickTestRightPlot;
            Grid card = new Grid { Margin = new Thickness(18, 16, 18, 14) };
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = new GridLength(206) });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock title = new TextBlock { Text = left ? "左摇杆" : "右摇杆", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            card.Children.Add(title);
            plot.Width = 210;
            plot.Height = 196;
            plot.RecordTrace = false;
            plot.HorizontalAlignment = HorizontalAlignment.Center;
            plot.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(plot, 1);
            card.Children.Add(plot);

            TextBlock summary = new TextBlock { Text = "等待检测", Foreground = Palette.MutedBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetRow(summary, 2);
            card.Children.Add(summary);
            if (left) stickTestLeftSummary = summary; else stickTestRightSummary = summary;

            StackPanel detailsPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            TextBlock details = new TextBlock { Text = "等待检测", Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
            if (left) stickTestLeftInfo = details; else stickTestRightInfo = details;
            detailsPanel.Children.Add(details);
            Border divider = new Border { Height = 1, Background = Palette.BorderSubtleBrush, Margin = new Thickness(0, 9, 0, 8) };
            detailsPanel.Children.Add(divider);
            Grid reference = new Grid();
            reference.ColumnDefinitions.Add(new ColumnDefinition());
            reference.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            reference.Children.Add(new TextBlock { Text = "显示参考死区", Foreground = Palette.MutedBrush, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            TextBlock deadzone = new TextBlock { Text = "8%", Foreground = Palette.BlueBrush, FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(deadzone, 1);
            reference.Children.Add(deadzone);
            detailsPanel.Children.Add(reference);
            DeadzoneSlider slider = left ? leftDeadzone : rightDeadzone;
            slider.Height = 18;
            slider.Margin = new Thickness(0, 4, 0, 0);
            detailsPanel.Children.Add(slider);
            if (left) leftDeadzoneText = deadzone; else rightDeadzoneText = deadzone;
            Expander expander = new Expander { Header = "详细信息", Foreground = Palette.MutedBrush, FontSize = 12, Content = detailsPanel, Margin = new Thickness(0, 6, 0, 0) };
            Grid.SetRow(expander, 3);
            card.Children.Add(expander);
            return LabVisualStyles.CreateSectionCard(card);
        }

        private UIElement BuildStickRangeSummary()
        {
            Grid grid = new Grid { Margin = new Thickness(20, 16, 20, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(154) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new TextBlock { Text = "综合结论", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            stickRangeSummaryText = new TextBlock { Text = "范围测试未开始。将两个摇杆沿外圈各旋转一整圈后，可在这里查看结论。", Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 19, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(stickRangeSummaryText, 1);
            grid.Children.Add(stickRangeSummaryText);
            return grid;
        }

        private UIElement BuildStickTriggerTestPage()
        {
            Grid page = new Grid { Margin = new Thickness(18, 10, 18, 0) };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.Children.Add(new TextBlock { Text = "摇杆与扳机测试", Foreground = Palette.TextBrush, FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, 4, 12, 0) });

            Grid body = new Grid();
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
            Grid sticks = new Grid();
            sticks.ColumnDefinitions.Add(new ColumnDefinition());
            sticks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            sticks.ColumnDefinitions.Add(new ColumnDefinition());
            sticks.Children.Add(BuildStickTestCard(true));
            Border right = BuildStickTestCard(false);
            Grid.SetColumn(right, 2);
            sticks.Children.Add(right);
            body.Children.Add(sticks);
            Border triggerCard = Card(BuildTriggerTestSummary());
            Grid.SetRow(triggerCard, 2);
            body.Children.Add(triggerCard);
            Grid.SetRow(body, 2);
            page.Children.Add(body);
            return page;
        }

        private Border BuildStickTestCard(bool left)
        {
            Color accent = Palette.Blue;
            StickPlot plot = left ? stickTestLeftPlot : stickTestRightPlot;
            Grid card = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(184) });
            StackPanel visual = new StackPanel();
            visual.Children.Add(new TextBlock { Text = left ? "左摇杆" : "右摇杆", Foreground = new SolidColorBrush(accent), FontSize = 16, FontWeight = FontWeights.SemiBold });
            plot.Height = 250;
            plot.Margin = new Thickness(0, 10, 8, 0);
            visual.Children.Add(plot);
            card.Children.Add(visual);
            TextBlock details = new TextBlock { Text = "等待采样", Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 20, VerticalAlignment = VerticalAlignment.Center };
            if (left) stickTestLeftInfo = details; else stickTestRightInfo = details;
            Grid.SetColumn(details, 1);
            card.Children.Add(details);
            return Card(card);
        }

        private UIElement BuildTriggerTestSummary()
        {
            Grid grid = new Grid { Margin = new Thickness(20, 15, 20, 15) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new TextBlock { Text = "扳机行程", Foreground = Palette.TextBrush, FontSize = 17, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            triggerTestInfo = new TextBlock { Text = "等待采样", Foreground = Palette.MutedBrush, FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(triggerTestInfo, 1);
            grid.Children.Add(triggerTestInfo);
            return grid;
        }

        private UIElement BuildMotionPage()
        {
            Grid page = new Grid { Margin = new Thickness(32, 24, 32, 0) };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel heading = new StackPanel();
            heading.Children.Add(LabVisualStyles.CreatePageTitle("体感"));
            TextBlock subtitle = LabVisualStyles.CreateSecondaryText("仅显示真实 DualSense 原生 HID 运动传感器数据；Yaw 没有磁力计参考，长时间使用可能缓慢漂移。");
            subtitle.FontSize = 14;
            subtitle.Margin = new Thickness(0, 7, 0, 0);
            heading.Children.Add(subtitle);
            page.Children.Add(heading);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(352) });

            Grid poseHost = new Grid { Margin = new Thickness(24, 20, 24, 22) };
            motionPoseView = new DualSenseMotionPoseView { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            poseHost.Children.Add(motionPoseView);
            StackPanel unavailable = new StackPanel { MaxWidth = 390, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            unavailable.Children.Add(new TextBlock { Text = "体感数据不可用", Foreground = Palette.TextBrush, FontSize = 22, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center });
            motionUnavailableText = LabVisualStyles.CreateSecondaryText("当前设备或输入模式未提供运动传感器数据。");
            motionUnavailableText.FontSize = 14;
            motionUnavailableText.TextAlignment = TextAlignment.Center;
            motionUnavailableText.TextWrapping = TextWrapping.Wrap;
            motionUnavailableText.Margin = new Thickness(0, 10, 0, 0);
            unavailable.Children.Add(motionUnavailableText);
            motionUnavailablePanel = new Border { Child = unavailable, Background = Brushes.Transparent };
            poseHost.Children.Add(motionUnavailablePanel);
            body.Children.Add(LabVisualStyles.CreateSectionCard(poseHost));

            StackPanel diagnostics = new StackPanel();
            Border angles = BuildMotionAnglesCard();
            angles.Margin = new Thickness(0, 0, 0, 12);
            diagnostics.Children.Add(angles);
            Border status = BuildMotionStatusCard();
            status.Margin = new Thickness(0, 0, 0, 12);
            diagnostics.Children.Add(status);
            Border actions = BuildMotionActionsCard();
            actions.Margin = new Thickness(0, 0, 0, 12);
            diagnostics.Children.Add(actions);
            Border details = BuildMotionDetailsCard();
            diagnostics.Children.Add(details);
            Grid.SetColumn(diagnostics, 2);
            body.Children.Add(diagnostics);
            Grid.SetRow(body, 2);
            page.Children.Add(body);
            return page;
        }

        private Border BuildMotionAnglesCard()
        {
            Grid card = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.Children.Add(new TextBlock { Text = "姿态", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            Grid values = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            values.ColumnDefinitions.Add(new ColumnDefinition());
            values.ColumnDefinitions.Add(new ColumnDefinition());
            values.ColumnDefinitions.Add(new ColumnDefinition());
            AddMotionAngle(values, 0, "Pitch", out motionPitchText);
            AddMotionAngle(values, 1, "Roll", out motionRollText);
            AddMotionAngle(values, 2, "Yaw", out motionYawText);
            Grid.SetRow(values, 1);
            card.Children.Add(values);
            return LabVisualStyles.CreateMetricCard(card);
        }

        private static void AddMotionAngle(Grid grid, int column, string label, out TextBlock value)
        {
            StackPanel item = new StackPanel();
            item.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 12 });
            value = new TextBlock { Text = "—", Foreground = Palette.BlueBrush, FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0) };
            item.Children.Add(value);
            Grid.SetColumn(item, column);
            grid.Children.Add(item);
        }

        private Border BuildMotionStatusCard()
        {
            Grid card = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.Children.Add(new TextBlock { Text = "状态", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            AddMotionStatusRow(card, 2, "连接方式", out motionConnectionText);
            AddMotionStatusRow(card, 3, "更新率", out motionRateText);
            AddMotionStatusRow(card, 4, "静止校准", out motionCalibrationText);
            AddMotionStatusRow(card, 5, "跟踪质量", out motionQualityText);
            return LabVisualStyles.CreateMetricCard(card);
        }

        private static void AddMotionStatusRow(Grid card, int row, string label, out TextBlock value)
        {
            Grid line = new Grid { Margin = new Thickness(0, row == 2 ? 0 : 7, 0, 0) };
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 12 });
            value = new TextBlock { Text = "—", Foreground = Palette.TextBrush, FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 190 };
            Grid.SetColumn(value, 1);
            line.Children.Add(value);
            Grid.SetRow(line, row);
            card.Children.Add(line);
        }

        private Border BuildMotionActionsCard()
        {
            StackPanel card = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
            card.Children.Add(new TextBlock { Text = "操作", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            motionSmoothingCheck = new CheckBox { Content = "模型平滑", Foreground = Palette.MutedBrush, FontSize = 12, IsChecked = true, Margin = new Thickness(0, 10, 0, 7) };
            motionSmoothingCheck.Checked += delegate { if (motionPoseView != null) motionPoseView.SmoothingEnabled = true; };
            motionSmoothingCheck.Unchecked += delegate { if (motionPoseView != null) motionPoseView.SmoothingEnabled = false; };
            card.Children.Add(motionSmoothingCheck);
            Grid actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            motionCalibrateButton = MakeButton("静止校准", true);
            motionCalibrateButton.Height = 34;
            motionCalibrateButton.Click += delegate { StartMotionCalibration(); };
            actions.Children.Add(motionCalibrateButton);
            motionRecenterButton = MakeButton("重新居中", false);
            motionRecenterButton.Height = 34;
            motionRecenterButton.Click += delegate { RecenterMotion(); };
            Grid.SetColumn(motionRecenterButton, 2);
            actions.Children.Add(motionRecenterButton);
            card.Children.Add(actions);
            motionResetButton = MakeButton("重置姿态", false);
            motionResetButton.Height = 32;
            motionResetButton.Margin = new Thickness(0, 8, 0, 0);
            motionResetButton.Click += delegate { ResetMotion(); };
            card.Children.Add(motionResetButton);
            return LabVisualStyles.CreateSectionCard(card);
        }

        private Border BuildMotionDetailsCard()
        {
            Expander expander = new Expander { Header = "详细数据", Foreground = Palette.MutedBrush, FontSize = 12, Margin = new Thickness(18, 14, 18, 14) };
            StackPanel detailContent = new StackPanel();
            motionRawLoggingCheck = new CheckBox { Content = "原始运动数据日志", Foreground = Palette.MutedBrush, FontSize = 11, IsChecked = false, Margin = new Thickness(0, 8, 0, 2) };
            motionRawLoggingCheck.Checked += delegate { sonyInput.EnableRawMotionLogging = true; };
            motionRawLoggingCheck.Unchecked += delegate { sonyInput.EnableRawMotionLogging = false; };
            detailContent.Children.Add(motionRawLoggingCheck);
            motionDetailText = new TextBlock { Text = "等待运动传感器数据。", Foreground = Palette.MutedBrush, FontFamily = new FontFamily("Consolas"), FontSize = 10.5, TextWrapping = TextWrapping.Wrap, LineHeight = 17, Margin = new Thickness(0, 10, 0, 0) };
            detailContent.Children.Add(motionDetailText);
            expander.Content = detailContent;
            return LabVisualStyles.CreateSectionCard(expander);
        }

        private UIElement BuildRumbleTestPage()
        {
            Grid page = new Grid { Margin = new Thickness(32, 24, 32, 0) };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel heading = new StackPanel();
            heading.Children.Add(LabVisualStyles.CreatePageTitle("震动测试"));
            TextBlock subtitle = LabVisualStyles.CreateSecondaryText("Xbox 使用 XInput 双电机；DualSense 使用独立 USB / 蓝牙 HID 输出。离开页面、切换设备、断开或异常时都会自动停止。");
            subtitle.FontSize = 14;
            subtitle.Margin = new Thickness(0, 7, 0, 0);
            heading.Children.Add(subtitle);
            rumbleDeviceText = new TextBlock { Text = "设备：未连接", Foreground = Palette.MutedBrush, FontFamily = new FontFamily("Consolas"), FontSize = 10.5, Margin = new Thickness(0, 5, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            heading.Children.Add(rumbleDeviceText);
            page.Children.Add(heading);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(410) });

            Grid visualCard = new Grid { Margin = new Thickness(28, 22, 28, 22) };
            visualCard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            visualCard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            visualCard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            visualCard.Children.Add(new TextBlock { Text = "双通道实时反馈", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });

            Grid grips = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            grips.ColumnDefinitions.Add(new ColumnDefinition());
            grips.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            grips.ColumnDefinitions.Add(new ColumnDefinition());
            rumbleLeftVisual = BuildRumbleGrip("左侧低频", Palette.Blue, -9);
            grips.Children.Add(rumbleLeftVisual);
            rumbleRightVisual = BuildRumbleGrip("右侧高频", Palette.Blue, 9);
            Grid.SetColumn(rumbleRightVisual, 2);
            grips.Children.Add(rumbleRightVisual);
            Grid.SetRow(grips, 1);
            visualCard.Children.Add(grips);

            Grid liveStatus = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            liveStatus.ColumnDefinitions.Add(new ColumnDefinition());
            liveStatus.ColumnDefinitions.Add(new ColumnDefinition());
            liveStatus.ColumnDefinitions.Add(new ColumnDefinition());
            rumblePatternText = BuildRumbleMetric(liveStatus, 0, "当前预设", "未运行");
            rumbleRemainingText = BuildRumbleMetric(liveStatus, 1, "剩余时间", "0.0 秒");
            rumbleStatusText = BuildRumbleMetric(liveStatus, 2, "输出状态", "等待开始");
            Grid.SetRow(liveStatus, 2);
            visualCard.Children.Add(liveStatus);
            body.Children.Add(LabVisualStyles.CreateSectionCard(visualCard));

            StackPanel controls = new StackPanel();
            Border supportCard = BuildRumbleSupportCard();
            supportCard.Margin = new Thickness(0, 0, 0, 12);
            controls.Children.Add(supportCard);
            Border motorCard = BuildRumbleMotorControls();
            motorCard.Margin = new Thickness(0, 0, 0, 12);
            controls.Children.Add(motorCard);
            Border modeCard = BuildRumbleModeControls();
            controls.Children.Add(modeCard);
            ScrollViewer controlScroller = new ScrollViewer
            {
                Content = controls,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetColumn(controlScroller, 2);
            body.Children.Add(controlScroller);

            Grid.SetRow(body, 2);
            page.Children.Add(body);
            return page;
        }

        private Border BuildRumbleGrip(string label, Color accent, double angle)
        {
            Grid content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.Children.Add(new TextBlock
            {
                Text = "≈",
                Foreground = new SolidColorBrush(Color.FromArgb(190, accent.R, accent.G, accent.B)),
                FontSize = 72,
                FontWeight = FontWeights.Light,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            TextBlock text = new TextBlock { Text = label, Foreground = Palette.TextBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 18) };
            Grid.SetRow(text, 1);
            content.Children.Add(text);
            Border grip = new Border
            {
                Width = 190,
                Height = 300,
                CornerRadius = new CornerRadius(88, 88, 70, 70),
                Background = new SolidColorBrush(Color.FromArgb(90, accent.R, accent.G, accent.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = content,
                Opacity = 0.14,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(angle)
            };
            return grip;
        }

        private static TextBlock BuildRumbleMetric(Grid host, int column, string label, string initial)
        {
            StackPanel item = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            item.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
            TextBlock value = new TextBlock { Text = initial, Foreground = Palette.TextBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 180, Margin = new Thickness(0, 3, 0, 0) };
            item.Children.Add(value);
            Grid.SetColumn(item, column);
            host.Children.Add(item);
            return value;
        }

        private Border BuildRumbleSupportCard()
        {
            StackPanel card = new StackPanel { Margin = new Thickness(18, 15, 18, 15) };
            card.Children.Add(new TextBlock { Text = "设备能力", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            rumbleSupportText = new TextBlock { Text = "连接真实手柄后检查震动支持。", Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 19, Margin = new Thickness(0, 9, 0, 0) };
            card.Children.Add(rumbleSupportText);
            return LabVisualStyles.CreateMetricCard(card);
        }

        private Border BuildRumbleMotorControls()
        {
            StackPanel card = new StackPanel { Margin = new Thickness(18, 15, 18, 16) };
            card.Children.Add(new TextBlock { Text = "强度与时长", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            rumbleLeftSlider = CreateRumbleSlider(0, 100, 40, out rumbleLeftValueText);
            card.Children.Add(BuildRumbleSliderRow("左侧震动强度", rumbleLeftSlider, rumbleLeftValueText, "%"));
            rumbleRightSlider = CreateRumbleSlider(0, 100, 40, out rumbleRightValueText);
            card.Children.Add(BuildRumbleSliderRow("右侧震动强度", rumbleRightSlider, rumbleRightValueText, "%"));
            rumbleOverallSlider = CreateRumbleSlider(0, 100, 100, out rumbleOverallValueText);
            card.Children.Add(BuildRumbleSliderRow("总体强度", rumbleOverallSlider, rumbleOverallValueText, "%"));
            rumbleDurationSlider = CreateRumbleSlider(1, 30, 5, out rumbleDurationValueText);
            rumbleDurationSlider.TickFrequency = 1;
            rumbleDurationSlider.IsSnapToTickEnabled = true;
            card.Children.Add(BuildRumbleSliderRow("持续时间（最大 30 秒）", rumbleDurationSlider, rumbleDurationValueText, " 秒"));
            return LabVisualStyles.CreateSectionCard(card);
        }

        private Slider CreateRumbleSlider(double minimum, double maximum, double value, out TextBlock valueText)
        {
            Slider slider = new Slider
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                Height = 24,
                Foreground = Palette.BlueBrush,
                IsMoveToPointEnabled = true
            };
            valueText = new TextBlock { Text = value.ToString("0", CultureInfo.InvariantCulture), Foreground = Palette.BlueBrush, FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            TextBlock captured = valueText;
            slider.ValueChanged += delegate { captured.Text = slider.Value.ToString("0", CultureInfo.InvariantCulture); };
            return slider;
        }

        private static UIElement BuildRumbleSliderRow(string label, Slider slider, TextBlock value, string suffix)
        {
            StackPanel row = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            StackPanel valueHost = new StackPanel { Orientation = Orientation.Horizontal };
            valueHost.Children.Add(value);
            valueHost.Children.Add(new TextBlock { Text = suffix, Foreground = Palette.MutedBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) });
            Grid.SetColumn(valueHost, 1);
            heading.Children.Add(valueHost);
            row.Children.Add(heading);
            row.Children.Add(slider);
            return row;
        }

        private Border BuildRumbleModeControls()
        {
            StackPanel card = new StackPanel { Margin = new Thickness(18, 15, 18, 16) };
            card.Children.Add(new TextBlock { Text = "测试模式", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            WrapPanel presets = new WrapPanel { Margin = new Thickness(0, 9, 0, 0) };
            AddRumblePatternButton(presets, "左侧单独", ControllerRumblePattern.LeftOnly);
            AddRumblePatternButton(presets, "右侧单独", ControllerRumblePattern.RightOnly);
            AddRumblePatternButton(presets, "均衡震动", ControllerRumblePattern.Balanced);
            AddRumblePatternButton(presets, "左右交替", ControllerRumblePattern.Alternating);
            AddRumblePatternButton(presets, "渐强测试", ControllerRumblePattern.Ramp);
            AddRumblePatternButton(presets, "脉冲测试", ControllerRumblePattern.Pulse);
            card.Children.Add(presets);

            Grid primary = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            primary.ColumnDefinitions.Add(new ColumnDefinition());
            primary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            primary.ColumnDefinitions.Add(new ColumnDefinition());
            rumbleStartButton = MakeButton("开始震动", true);
            rumbleStartButton.Height = 36;
            rumbleStartButton.Click += delegate { StartRumblePattern(ControllerRumblePattern.Manual); };
            primary.Children.Add(rumbleStartButton);
            rumbleStopButton = MakeButton("停止震动", false);
            rumbleStopButton.Height = 36;
            rumbleStopButton.Click += delegate { StopRumble("用户已停止震动"); };
            Grid.SetColumn(rumbleStopButton, 2);
            primary.Children.Add(rumbleStopButton);
            card.Children.Add(primary);
            Button reset = MakeButton("恢复默认（40% · 5 秒）", false);
            reset.Height = 32;
            reset.Margin = new Thickness(0, 8, 0, 0);
            reset.Click += delegate { ResetRumbleControls(); };
            card.Children.Add(reset);
            return LabVisualStyles.CreateSectionCard(card);
        }

        private void AddRumblePatternButton(Panel host, string label, ControllerRumblePattern pattern)
        {
            Button button = MakeButton(label, false);
            button.Height = 32;
            button.FontSize = 11;
            button.Padding = new Thickness(9, 4, 9, 4);
            button.Margin = new Thickness(0, 0, 7, 7);
            button.Click += delegate { StartRumblePattern(pattern); };
            rumblePatternButtons.Add(button);
            host.Children.Add(button);
        }

        private void StartRumblePattern(ControllerRumblePattern pattern)
        {
            if (stickDriftTestEngine.IsActive || joystickTestViewModel.IsTestActive || healthCheckViewModel.IsQuietSamplingActive || IsMotionCalibrationActive(currentControllerState))
            {
                if (rumbleStatusText != null)
                {
                    rumbleStatusText.Text = "漂移检测期间不能进行震动测试";
                    rumbleStatusText.Foreground = Palette.WarningBrush;
                }
                if (footerStatus != null) footerStatus.Text = "漂移检测期间不能进行震动测试，避免物理抖动污染采样结果。";
                return;
            }
            RumbleStatusSnapshot snapshot = rumbleController.GetSnapshot();
            if (!snapshot.IsSupported)
            {
                if (rumbleStatusText != null)
                {
                    rumbleStatusText.Text = snapshot.SupportDetails;
                    rumbleStatusText.Foreground = Palette.WarningBrush;
                }
                if (footerStatus != null) footerStatus.Text = snapshot.SupportDetails;
                return;
            }
            double left = rumbleLeftSlider == null ? ControllerRumbleController.DefaultStrength : rumbleLeftSlider.Value / 100.0;
            double right = rumbleRightSlider == null ? ControllerRumbleController.DefaultStrength : rumbleRightSlider.Value / 100.0;
            double overall = rumbleOverallSlider == null ? 1.0 : rumbleOverallSlider.Value / 100.0;
            double duration = rumbleDurationSlider == null ? ControllerRumbleController.DefaultDurationSeconds : rumbleDurationSlider.Value;
            string error;
            if (!rumbleController.Start(pattern, left, right, overall, duration, out error))
            {
                if (rumbleStatusText != null)
                {
                    rumbleStatusText.Text = error;
                    rumbleStatusText.Foreground = Palette.RedBrush;
                }
                if (footerStatus != null) footerStatus.Text = error;
                return;
            }
            if (footerStatus != null) footerStatus.Text = "震动测试已启动；可随时点击“停止震动”。";
        }

        private void StopRumble(string reason)
        {
            rumbleController.Stop(reason);
            if (footerStatus != null) footerStatus.Text = reason;
        }

        private void ResetRumbleControls()
        {
            StopRumble("已恢复默认并停止震动");
            if (rumbleLeftSlider != null) rumbleLeftSlider.Value = 40;
            if (rumbleRightSlider != null) rumbleRightSlider.Value = 40;
            if (rumbleOverallSlider != null) rumbleOverallSlider.Value = 100;
            if (rumbleDurationSlider != null) rumbleDurationSlider.Value = 5;
        }

        private void UpdateRumblePage(ControllerState controller)
        {
            if (rumblePage == null || rumblePage.Visibility != Visibility.Visible) return;
            if (rumbleStudioPage != null)
            {
                string blockReason = string.Empty;
                bool blocked = false;
                if (stickDriftTestEngine.IsActive || joystickTestViewModel.IsTestActive)
                {
                    blocked = true;
                    blockReason = "摇杆检测期间禁止震动，避免物理抖动污染采样。";
                }
                else if (IsMotionCalibrationActive(controller))
                {
                    blocked = true;
                    blockReason = "陀螺仪静止校准期间禁止震动。";
                }
                else if (healthCheckViewModel.IsQuietSamplingActive)
                {
                    blocked = true;
                    blockReason = "完整检测正在进行静止采样，禁止震动。";
                }
                rumbleStudioPage.Update(controller, blocked, blockReason);
                return;
            }
            RumbleStatusSnapshot snapshot = rumbleController.GetSnapshot();
            if (rumbleDeviceText != null) rumbleDeviceText.Text = BuildDeviceInputIdentity(controller);
            if (rumbleSupportText != null)
            {
                rumbleSupportText.Text = snapshot.SupportDetails;
                rumbleSupportText.Foreground = snapshot.IsSupported ? Palette.TextBrush : Palette.WarningBrush;
            }
            if (rumblePatternText != null) rumblePatternText.Text = snapshot.IsRunning ? snapshot.PatternLabel : "未运行";
            if (rumbleRemainingText != null) rumbleRemainingText.Text = snapshot.RemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒";
            if (rumbleStatusText != null)
            {
                rumbleStatusText.Text = snapshot.Status;
                rumbleStatusText.Foreground = snapshot.IsRunning
                    ? Palette.BlueBrush
                    : snapshot.LastOutputSucceeded ? Palette.GreenBrush : snapshot.IsSupported ? Palette.MutedBrush : Palette.WarningBrush;
            }
            if (rumbleLeftVisual != null) rumbleLeftVisual.Opacity = 0.14 + snapshot.LeftStrength * 0.86;
            if (rumbleRightVisual != null) rumbleRightVisual.Opacity = 0.14 + snapshot.RightStrength * 0.86;
            bool driftActive = stickDriftTestEngine.IsActive || joystickTestViewModel.IsTestActive || healthCheckViewModel.IsRunning;
            bool enabled = snapshot.IsSupported && controller != null && controller.IsConnected && controller.HasRealInput && !driftActive;
            if (rumbleStartButton != null) rumbleStartButton.IsEnabled = enabled && !snapshot.IsRunning;
            if (rumbleStopButton != null) rumbleStopButton.IsEnabled = snapshot.IsRunning;
            for (int i = 0; i < rumblePatternButtons.Count; i++) rumblePatternButtons[i].IsEnabled = enabled && !snapshot.IsRunning;
            if (driftActive && rumbleStatusText != null)
            {
                rumbleStatusText.Text = "漂移检测期间不能进行震动测试";
                rumbleStatusText.Foreground = Palette.WarningBrush;
            }
        }

        private bool IsMotionCalibrationActive(ControllerState controller)
        {
            if (controller == null || string.IsNullOrEmpty(controller.DeviceId)) return false;
            MotionViewState motion = motionManager.Get(controller.DeviceId);
            return motion != null && (motion.CalibrationState == MotionCalibrationState.Settling || motion.CalibrationState == MotionCalibrationState.Sampling);
        }

        private void StartMotionCalibration()
        {
            if (rumbleController.IsRunning) rumbleController.Stop("开始陀螺仪静止校准前，震动已自动停止");
            string reason;
            if (!motionManager.StartCalibration(currentControllerState == null ? string.Empty : currentControllerState.DeviceId, out reason))
            {
                if (footerStatus != null) footerStatus.Text = reason;
                return;
            }
            if (footerStatus != null) footerStatus.Text = "静止校准已开始：请将 DualSense 平放，等待 1 秒后保持静止 3 秒。";
        }

        private void RecenterMotion()
        {
            string reason;
            if (!motionManager.Recenter(currentControllerState == null ? string.Empty : currentControllerState.DeviceId, out reason))
            {
                if (footerStatus != null) footerStatus.Text = reason;
                return;
            }
            if (footerStatus != null) footerStatus.Text = "当前姿态已设为显示零点。";
        }

        private void ResetMotion()
        {
            if (currentControllerState != null) motionManager.Reset(currentControllerState.DeviceId);
            if (motionPoseView != null) motionPoseView.SetState(null);
            if (footerStatus != null) footerStatus.Text = "体感姿态、校准与本机会话轨迹已重置。";
        }

        private void UpdateMotionPage(ControllerState controller)
        {
            if (motionPage == null || motionPage.Visibility != Visibility.Visible) return;
            DateTime now = DateTime.UtcNow;
            if (now < nextMotionUiRefresh) return;
            nextMotionUiRefresh = now.AddMilliseconds(33.3);
            if (dualSenseAdvancedPage != null)
            {
                dualSenseAdvancedPage.Update(controller);
                return;
            }
            bool nativeDualSense = controller != null && controller.IsConnected && controller.ControllerType == ControllerType.DualSense && controller.InputSource == ControllerInputSource.DualSenseHid;
            MotionViewState view = nativeDualSense ? motionManager.Get(controller.DeviceId) : new MotionViewState
            {
                AvailabilityMessage = controller != null && controller.InputSource == ControllerInputSource.DynamicDemo
                    ? "动态演示不会伪造姿态；请连接真实 DualSense 原生 HID 设备。"
                    : "当前设备或输入模式未提供运动传感器数据。",
                CalibrationState = MotionCalibrationState.Unsupported,
                TrackingQuality = MotionTrackingQuality.Unsupported
            };
            bool available = nativeDualSense && view != null && view.IsAvailable && view.Sample != null && view.Sample.IsValid;
            if (motionUnavailablePanel != null) motionUnavailablePanel.Visibility = available ? Visibility.Collapsed : Visibility.Visible;
            if (motionPoseView != null)
            {
                motionPoseView.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
                motionPoseView.SetState(available ? view : null);
            }
            SetTextIfChanged(motionPitchText, available ? FormatDegrees(view.Pose.Pitch) : "—");
            SetTextIfChanged(motionRollText, available ? FormatDegrees(view.Pose.Roll) : "—");
            SetTextIfChanged(motionYawText, available ? FormatDegrees(view.Pose.Yaw) : "—");
            SetTextIfChanged(motionConnectionText, available ? view.Sample.ConnectionLabel + " · 0x" + view.Sample.SourceReportId.ToString("X2", CultureInfo.InvariantCulture) : "不支持");
            SetTextIfChanged(motionRateText, available ? string.Format(CultureInfo.InvariantCulture, "{0:0} Hz", view.UpdatesPerSecond) : "—");
            SetTextIfChanged(motionCalibrationText, MotionCalibrationLabel(view.CalibrationState));
            SetTextIfChanged(motionQualityText, MotionQualityLabel(view.TrackingQuality));
            if (motionQualityText != null) motionQualityText.Foreground = MotionQualityBrush(view.TrackingQuality);
            bool canOperate = available && currentControllerState != null && currentControllerState.HasRealInput;
            bool calibrateEnabled = canOperate && view.CalibrationState != MotionCalibrationState.Settling && view.CalibrationState != MotionCalibrationState.Sampling;
            if (motionCalibrateButton != null)
            {
                motionCalibrateButton.IsEnabled = calibrateEnabled;
                SetButtonPrimary(motionCalibrateButton, calibrateEnabled);
            }
            if (motionRecenterButton != null) motionRecenterButton.IsEnabled = canOperate && view.Pose != null && view.Pose.HasPose;
            if (motionResetButton != null) motionResetButton.IsEnabled = nativeDualSense;
            if (motionDetailText != null) motionDetailText.Text = BuildMotionDetail(view, available, now);
            if (motionUnavailableText != null) motionUnavailableText.Text = view == null || string.IsNullOrEmpty(view.AvailabilityMessage) ? "当前设备或输入模式未提供运动传感器数据。" : view.AvailabilityMessage;
        }

        private static string BuildMotionDetail(MotionViewState view, bool available, DateTime now)
        {
            if (!available || view == null || view.Sample == null) return "未收到可用于姿态融合的真实 DualSense 运动样本。";
            MotionSample sample = view.Sample;
            double age = Math.Max(0, (now - sample.TimestampUtc).TotalMilliseconds);
            return string.Format(CultureInfo.InvariantCulture,
                "Raw gyro: ({0}, {1}, {2})\nRaw accel: ({3}, {4}, {5})\nGyro: ({6:0.000}, {7:0.000}, {8:0.000}) °/s\nAccel: ({9:0.000}, {10:0.000}, {11:0.000}) g\nReport: 0x{12:X2} · seq {13} · {14} bytes\nAge: {15:0} ms · CRC: {16}\nBias: ({17:0.000}, {18:0.000}, {19:0.000}) °/s · samples {20}",
                sample.RawGyroX, sample.RawGyroY, sample.RawGyroZ, sample.RawAccelX, sample.RawAccelY, sample.RawAccelZ,
                sample.GyroX, sample.GyroY, sample.GyroZ, sample.AccelX, sample.AccelY, sample.AccelZ,
                sample.SourceReportId, sample.Sequence, sample.ReportLength, age, sample.CrcValidated ? "通过" : "失败",
                view.Calibration == null ? 0 : view.Calibration.BiasX, view.Calibration == null ? 0 : view.Calibration.BiasY, view.Calibration == null ? 0 : view.Calibration.BiasZ,
                view.Calibration == null ? 0 : view.Calibration.SampleCount);
        }

        private static string FormatDegrees(double value)
        {
            return value.ToString(value >= 0 ? "+0.0°" : "0.0°", CultureInfo.InvariantCulture);
        }

        private static string MotionCalibrationLabel(MotionCalibrationState state)
        {
            switch (state)
            {
                case MotionCalibrationState.Settling: return "准备静止";
                case MotionCalibrationState.Sampling: return "采样中";
                case MotionCalibrationState.Calibrated: return "校准成功";
                case MotionCalibrationState.Failed: return "校准失败";
                case MotionCalibrationState.NotCalibrated: return "未校准";
                default: return "当前模式不支持";
            }
        }

        private static string MotionQualityLabel(MotionTrackingQuality quality)
        {
            switch (quality)
            {
                case MotionTrackingQuality.Good: return "良好";
                case MotionTrackingQuality.DataJitter: return "数据抖动";
                case MotionTrackingQuality.DataInterrupted: return "数据中断";
                case MotionTrackingQuality.Uncalibrated: return "未校准";
                default: return "当前模式不支持";
            }
        }

        private static Brush MotionQualityBrush(MotionTrackingQuality quality)
        {
            if (quality == MotionTrackingQuality.Good) return Palette.GreenBrush;
            if (quality == MotionTrackingQuality.DataJitter || quality == MotionTrackingQuality.DataInterrupted) return Palette.WarningBrush;
            if (quality == MotionTrackingQuality.Uncalibrated) return Palette.MutedBrush;
            return Palette.MutedBrush;
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

        private void StartStickDriftTest()
        {
            if (!CanStartStickTestAfterRumble()) return;
            if (currentControllerState == null || !currentControllerState.IsConnected || !currentControllerState.HasRealInput)
            {
                if (stickTestStatusText != null) stickTestStatusText.Text = "正式检测需要真实 Xbox XInput 或 DualSense HID 输入";
                if (footerStatus != null) footerStatus.Text = "动态演示、构造自检数据和未连接设备不能生成正式摇杆检测结果。";
                return;
            }
            ClearStickTestVisualState();
            showStickRangeVisuals = false;
            stickTestLeftPlot.BeginTrace(StickPlotTraceMode.Drift);
            stickTestRightPlot.BeginTrace(StickPlotTraceMode.Drift);
            stickDriftTestEngine.Start(currentControllerState, stickTestThreeRunsCheck != null && stickTestThreeRunsCheck.IsChecked == true);
        }

        private void EndStickDriftTest()
        {
            if (stickDriftTestEngine.Stage == StickTestStage.RangeRecording)
            {
                stickDriftTestEngine.FinishRangeTest();
                stickTestLeftPlot.EndTrace();
                stickTestRightPlot.EndTrace();
                return;
            }
            stickDriftTestEngine.Cancel("检测已由用户结束");
            stickTestLeftPlot.EndTrace();
            stickTestRightPlot.EndTrace();
        }

        private void StartStickRangeTest()
        {
            if (!CanStartStickTestAfterRumble()) return;
            if (currentControllerState == null || !currentControllerState.IsConnected || !currentControllerState.HasRealInput)
            {
                if (footerStatus != null) footerStatus.Text = "范围测试需要真实 Xbox XInput 或 DualSense HID 输入。";
                return;
            }
            ClearStickTestVisualState();
            showStickRangeVisuals = true;
            stickTestLeftPlot.BeginTrace(StickPlotTraceMode.Range);
            stickTestRightPlot.BeginTrace(StickPlotTraceMode.Range);
            stickDriftTestEngine.StartRangeTest(currentControllerState);
        }

        private bool CanStartStickTestAfterRumble()
        {
            if (rumbleController.IsRunning)
            {
                if (stickTestStatusText != null) stickTestStatusText.Text = "震动正在运行，不能开始摇杆检测";
                if (footerStatus != null) footerStatus.Text = "请先停止震动，并等待至少 1 秒后再开始漂移采样。";
                return false;
            }
            DateTime stopped = rumbleController.LastStoppedUtc;
            if (stopped != DateTime.MinValue)
            {
                double wait = 1.0 - (DateTime.UtcNow - stopped).TotalSeconds;
                if (wait > 0)
                {
                    if (stickTestStatusText != null) stickTestStatusText.Text = "等待震动影响消退";
                    if (footerStatus != null) footerStatus.Text = string.Format(CultureInfo.InvariantCulture, "震动刚刚停止，请等待 {0:0.0} 秒后再开始漂移采样。", wait);
                    return false;
                }
            }
            return true;
        }

        private void EndStickRangeTest()
        {
            stickDriftTestEngine.FinishRangeTest();
            stickTestLeftPlot.EndTrace();
            stickTestRightPlot.EndTrace();
        }

        private void CopyStickDriftResult()
        {
            try
            {
                Clipboard.SetText(BuildFormalDetectionReport());
                if (footerStatus != null) footerStatus.Text = "摇杆检测结果已复制到剪贴板。";
            }
            catch (Exception)
            {
                if (footerStatus != null) footerStatus.Text = "无法访问剪贴板，请稍后重试。";
            }
        }

        private void SaveStickTestEvidence()
        {
            ControllerStickTestResult result = stickDriftTestEngine.LastResult;
            if (result == null || !result.IsFormalInput)
            {
                if (footerStatus != null) footerStatus.Text = "请先完成一次真实手柄的静止摇杆检测，再保存实测记录。";
                return;
            }
            try
            {
                StickTestEvidenceSaveResult saved = StickTestEvidenceStore.Save(result);
                if (stickTestStatusText != null) stickTestStatusText.Text = "实测记录已保存，可在本地复查或附到问题反馈。";
                if (footerStatus != null) footerStatus.Text = "中文报告已保存到本地数据目录。";
            }
            catch (Exception ex)
            {
                if (footerStatus != null) footerStatus.Text = "保存实测记录失败：" + ex.Message;
            }
        }

        private void ClearStickTestVisualState()
        {
            leftPlot.ClearHistory();
            rightPlot.ClearHistory();
            leftPlot.RecordTrace = true;
            rightPlot.RecordTrace = true;
            stickTestLeftPlot.ClearHistory();
            stickTestRightPlot.ClearHistory();
            showStickRangeVisuals = false;
        }

        private string BuildFormalDetectionReport()
        {
            ControllerState state = currentControllerState;
            if (state == null || !state.IsConnected || !state.HasRealInput)
            {
                return "ControllerLab " + ControllerLabVersion.Display + " 检测报告\n当前未连接可用于正式检测的真实设备。\n动态演示和构造自检数据不会进入正式报告。";
            }
            ControllerTestReport buttons = inputTestEngine.BuildReport(state, stickTriggerTestEngine);
            ControllerStickTestResult sticks = stickDriftTestEngine.LastResult;
            StickDriftResult left = sticks == null ? null : sticks.LeftStickDrift;
            StickDriftResult right = sticks == null ? null : sticks.RightStickDrift;
            StickRangeResult leftRange = stickDriftTestEngine.LeftRange;
            StickRangeResult rightRange = stickDriftTestEngine.RightRange;
            string unpassed = buttons.UnpassedButtons == null || buttons.UnpassedButtons.Count == 0 ? "无（全部通过）" : string.Join("、", buttons.UnpassedButtons.ToArray());
            bool valid = left != null && right != null && left.IsValid && right.IsValid;
            return string.Format(CultureInfo.InvariantCulture,
                "ControllerLab " + ControllerLabVersion.Display + " 检测报告\n检测时间：{0:yyyy-MM-dd HH:mm:ss}\n设备：{1}\n设备 ID：{2}\n手柄类型：{3}\n连接方式：{4}\n输入来源：{5}\n\n按键：{6}/{7} 通过\n未通过按钮：{8}\n左/右扳机峰值：{9:0}% / {10:0}%\n\n左摇杆：P95 {11}，建议死区 {12}\n连续检测：{13}\n范围：{14}\n右摇杆：P95 {15}，建议死区 {16}\n连续检测：{17}\n范围：{18}\n检测有效：{19}",
                sticks == null ? DateTime.Now : sticks.TestTime,
                state.DeviceName, string.IsNullOrEmpty(state.DeviceId) ? "—" : state.DeviceId, state.ControllerType, state.ConnectionTypeLabel, state.InputSourceLabel,
                buttons.ButtonTestPassedCount, buttons.ButtonTestTotalCount, unpassed, buttons.LeftTriggerMaximum * 100.0, buttons.RightTriggerMaximum * 100.0,
                left == null ? "未完成" : left.P95DriftPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%", left == null ? "—" : left.SuggestedDeadzonePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%", FormatStabilityForReport(sticks == null ? null : sticks.LeftStickStability), FormatRangeForReport(leftRange),
                right == null ? "未完成" : right.P95DriftPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%", right == null ? "—" : right.SuggestedDeadzonePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%", FormatStabilityForReport(sticks == null ? null : sticks.RightStickStability), FormatRangeForReport(rightRange),
                valid ? "是" : "否");
        }

        private static string FormatRangeForReport(StickRangeResult result)
        {
            if (result == null || result.SampleCount == 0) return "未测试";
            return string.Format(CultureInfo.InvariantCulture, "{0}（上/下/左/右 {1:0}%/{2:0}%/{3:0}%/{4:0}%，最大半径 {5:0}%，最小外圈 {6:0}%，覆盖 {7:0}%，缺失 {8}）",
                result.Status, result.MaxUp * 100.0, result.MaxDown * 100.0, result.MaxLeft * 100.0, result.MaxRight * 100.0, result.MaxRadius * 100.0, result.MinimumOuterRadius * 100.0, result.CoveragePercent, result.MissingDirections);
        }

        private static string FormatStabilityForReport(StickStabilityResult result)
        {
            if (result == null || result.CompletedRuns == 0) return "未进行连续检测";
            return string.Format(CultureInfo.InvariantCulture, "{0}（P95 {1}；平均 {2:0.0}%；差异 {3:0.0}%）", result.Status, FormatP95Runs(result.P95DriftPercent), result.AverageP95DriftPercent, result.MaximumDifferencePercent);
        }

        private void UpdateStickDriftTestPage(ControllerState state)
        {
            if (state != null && !string.Equals(stickTestRenderedDeviceId, state.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                ClearStickTestVisualState();
                stickTestRenderedDeviceId = state.DeviceId;
            }
            stickDriftTestEngine.Update(state);
            if (state == null) return;
            bool formalInput = state.IsConnected && state.HasRealInput;
            if (!formalInput && !stickTestVisualsClearedForUnavailableState)
            {
                ClearStickTestVisualState();
                stickTestVisualsClearedForUnavailableState = true;
            }
            if (formalInput) stickTestVisualsClearedForUnavailableState = false;
            StickDriftResult left = formalInput && stickDriftTestEngine.LastResult != null ? stickDriftTestEngine.LastResult.LeftStickDrift : null;
            StickDriftResult right = formalInput && stickDriftTestEngine.LastResult != null ? stickDriftTestEngine.LastResult.RightStickDrift : null;
            StickRangeResult leftRange = formalInput ? stickDriftTestEngine.LeftRange : null;
            StickRangeResult rightRange = formalInput ? stickDriftTestEngine.RightRange : null;
            StickRangeResult leftRangeForVisual = showStickRangeVisuals ? leftRange : null;
            StickRangeResult rightRangeForVisual = showStickRangeVisuals ? rightRange : null;
            bool recordingDrift = stickDriftTestEngine.Stage == StickTestStage.Sampling;
            bool recordingRange = stickDriftTestEngine.Stage == StickTestStage.RangeRecording;
            stickTestLeftPlot.RecordTrace = recordingDrift || recordingRange;
            stickTestRightPlot.RecordTrace = recordingDrift || recordingRange;

            stickTestLeftPlot.UpdateValue(state.LeftStickX, state.LeftStickY);
            stickTestRightPlot.UpdateValue(state.RightStickX, state.RightStickY);
            stickTestLeftPlot.Deadzone = left == null ? leftDeadzone.Value : left.SuggestedDeadzonePercent / 100.0;
            stickTestRightPlot.Deadzone = right == null ? rightDeadzone.Value : right.SuggestedDeadzonePercent / 100.0;
            stickTestLeftPlot.MaximumReach = leftRangeForVisual == null ? 0 : leftRangeForVisual.MaxRadius;
            stickTestRightPlot.MaximumReach = rightRangeForVisual == null ? 0 : rightRangeForVisual.MaxRadius;

            if (stickTestLeftInfo != null) stickTestLeftInfo.Text = formalInput ? BuildStickDriftDetails(state.LeftStickX, state.LeftStickY, left, leftRangeForVisual, stickDriftTestEngine.LeftStability, leftDeadzone.Value) : BuildUnavailableStickDetails(state);
            if (stickTestRightInfo != null) stickTestRightInfo.Text = formalInput ? BuildStickDriftDetails(state.RightStickX, state.RightStickY, right, rightRangeForVisual, stickDriftTestEngine.RightStability, rightDeadzone.Value) : BuildUnavailableStickDetails(state);
            if (stickTestLeftSummary != null) stickTestLeftSummary.Text = BuildStickDriftSummary(state.LeftStickX, state.LeftStickY, left, formalInput, stickDriftTestEngine.Stage);
            if (stickTestRightSummary != null) stickTestRightSummary.Text = BuildStickDriftSummary(state.RightStickX, state.RightStickY, right, formalInput, stickDriftTestEngine.Stage);
            if (stickRangeSummaryText != null) stickRangeSummaryText.Text = formalInput ? BuildRangeSummary(leftRangeForVisual, rightRangeForVisual, recordingRange) : "范围检测仅接受真实 Xbox XInput 或 DualSense HID 输入；当前不会显示或保存演示结果。";
            if (stickTestDeviceText != null) stickTestDeviceText.Text = BuildDeviceInputIdentity(state);
            if (stickTestStatusText != null)
            {
                stickTestStatusText.Text = stickDriftTestEngine.StatusMessage;
                stickTestStatusText.Foreground = StickDriftStatusBrush(stickDriftTestEngine);
            }
            if (stickTestHintText != null && !formalInput)
            {
                stickTestHintText.Text = "当前为 " + state.InputSourceLabel + "；正式检测页面不会使用演示或构造数据。";
            }
            else if (stickTestHintText != null && stickDriftTestEngine.Stage == StickTestStage.Sampling)
            {
                stickTestHintText.Text = string.Format(CultureInfo.InvariantCulture, "正在采样 {0} 个数据点；请继续不要触碰两个摇杆。", stickDriftTestEngine.DriftSampleCount);
            }
            else if (stickTestHintText != null && stickDriftTestEngine.Stage != StickTestStage.RangeRecording)
            {
                stickTestHintText.Text = "松开两个摇杆后开始：等待 1 秒，再连续采样 5 秒。Xbox 与 DualSense 使用同一套检测规则。";
            }

            bool connected = formalInput;
            bool driftActive = stickDriftTestEngine.Stage == StickTestStage.Settling || stickDriftTestEngine.Stage == StickTestStage.Sampling;
            bool rangeActive = stickDriftTestEngine.Stage == StickTestStage.RangeRecording;
            if (stickTestStartButton != null) stickTestStartButton.IsEnabled = connected && !stickDriftTestEngine.IsActive;
            if (stickTestRestartButton != null) stickTestRestartButton.IsEnabled = connected;
            if (stickTestStopButton != null) stickTestStopButton.IsEnabled = driftActive || rangeActive;
            if (stickRangeStartButton != null) stickRangeStartButton.IsEnabled = connected && !stickDriftTestEngine.IsActive;
            if (stickRangeStopButton != null) stickRangeStopButton.IsEnabled = rangeActive;
            if (stickTestCopyButton != null) stickTestCopyButton.IsEnabled = formalInput && stickDriftTestEngine.LastResult != null;
            if (stickTestSaveButton != null) stickTestSaveButton.IsEnabled = stickDriftTestEngine.LastResult != null && stickDriftTestEngine.LastResult.IsFormalInput;
        }

        private static Brush StickDriftStatusBrush(StickDriftTestEngine engine)
        {
            if (engine == null) return Palette.MutedBrush;
            if (engine.Stage == StickTestStage.Settling || engine.Stage == StickTestStage.Sampling || engine.Stage == StickTestStage.RangeRecording) return Palette.BlueBrush;
            ControllerStickTestResult result = engine.LastResult;
            if (result != null && result.LeftStickDrift != null && result.RightStickDrift != null && result.LeftStickDrift.IsValid && result.RightStickDrift.IsValid) return Palette.GreenBrush;
            if (engine.StatusMessage != null && engine.StatusMessage.IndexOf("断开", StringComparison.OrdinalIgnoreCase) >= 0) return Palette.RedBrush;
            return Palette.WarningBrush;
        }

        private static string BuildStickDriftDetails(double x, double y, StickDriftResult result, StickRangeResult range, StickStabilityResult stability, double userVisualDeadzone)
        {
            StringBuilder text = new StringBuilder();
            text.AppendFormat(CultureInfo.InvariantCulture, "当前 X {0:0.000}\n当前 Y {1:0.000}\n当前偏移 {2:0.0}%", x, y, Math.Sqrt(x * x + y * y) * 100.0);
            if (result == null)
            {
                text.Append("\n\n检测结果\n等待开始检测");
                text.AppendFormat(CultureInfo.InvariantCulture, "\n\n显示参考死区\n{0:0.0}%", userVisualDeadzone * 100.0);
                return text.ToString();
            }
            text.AppendFormat(CultureInfo.InvariantCulture,
                "\n\n漂移结果\n平均 X {0:0.000}\n平均 Y {1:0.000}\n平均漂移 {2:0.0}%\nP95 漂移 {3:0.0}%\n最大漂移 {4:0.0}%\n标准差 {5:0.0}%\n尖峰 {6}\n\n检测建议死区\n{7:0.0}%\n状态：{8}",
                result.AverageX, result.AverageY, result.AverageDriftPercent, result.P95DriftPercent, result.MaximumDriftPercent, result.StandardDeviation * 100.0, result.AnomalySpikeCount,
                result.SuggestedDeadzonePercent, StickDriftTestEngine.RatingLabel(result));
            if (!result.IsValid && !string.IsNullOrEmpty(result.InvalidReason)) text.Append("\n" + result.InvalidReason);
            if (result.Health != null)
            {
                text.AppendFormat(CultureInfo.InvariantCulture,
                    "\n\n摇杆健康\n{0} · {1}/100\n中心稳定性 {2:0.0}%\n噪声水平 {3:0.0}%\n所需死区 {4:0.0}%",
                    JoystickHealthAnalyzer.Label(result.Health), result.Health.Score,
                    result.Health.CenterOffsetPercent, result.Health.NoisePercent, result.Health.RequiredDeadzonePercent);
            }
            text.AppendFormat(CultureInfo.InvariantCulture, "\n显示参考死区（用户）：{0:0.0}%", userVisualDeadzone * 100.0);
            if (stability != null)
            {
                text.Append("\n\n连续检测");
                if (stability.P95DriftPercent.Length > 0) text.Append("\nP95：" + FormatP95Runs(stability.P95DriftPercent));
                text.AppendFormat(CultureInfo.InvariantCulture, "\n平均 {0:0.0}% · 差异 {1:0.0}%\n{2}", stability.AverageP95DriftPercent, stability.MaximumDifferencePercent, stability.Status);
            }
            return text.ToString();
        }

        private static string BuildUnavailableStickDetails(ControllerState state)
        {
            return "正式检测不可用\n\n当前数据来源：" + (state == null ? "未知" : state.InputSourceLabel) + "\n\n请连接真实 Xbox XInput 或 DualSense HID 手柄。\n动态演示与构造自检数据不会产生漂移、范围或按键结果。";
        }

        private static string BuildStickDriftSummary(double x, double y, StickDriftResult result, bool formalInput, StickTestStage stage)
        {
            if (!formalInput) return "正式检测不可用";
            if (stage == StickTestStage.Settling) return "准备采样，请保持摇杆静止";
            if (stage == StickTestStage.Sampling) return "正在采样，请勿触碰摇杆";
            if (stage == StickTestStage.RangeRecording) return "正在记录外圈范围";
            if (result == null)
            {
                double current = Math.Sqrt(x * x + y * y) * 100.0;
                return string.Format(CultureInfo.InvariantCulture, "当前偏移 {0:0.0}% · 等待检测", current);
            }
            string health = result.Health == null ? "Pending" : JoystickHealthAnalyzer.Label(result.Health) + " " + result.Health.Score.ToString(CultureInfo.InvariantCulture) + "/100";
            return string.Format(CultureInfo.InvariantCulture, "{0} · {1} · P95 {2:0.0}% · 建议死区 {3:0.0}%", StickDriftTestEngine.RatingLabel(result), health, result.P95DriftPercent, result.SuggestedDeadzonePercent);
        }

        private static string FormatP95Runs(double[] values)
        {
            if (values == null || values.Length == 0) return "—";
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) text.Append(" / ");
                text.Append(values[i].ToString("0.0", CultureInfo.InvariantCulture));
                text.Append('%');
            }
            return text.ToString();
        }

        private static string BuildDeviceInputIdentity(ControllerState state)
        {
            if (state == null) return "设备：未连接";
            return string.Format(CultureInfo.InvariantCulture, "设备：{0}  |  ID：{1}  |  来源：{2}  |  连接：{3}", state.DeviceName, string.IsNullOrEmpty(state.DeviceId) ? "—" : state.DeviceId, state.InputSourceLabel, string.IsNullOrEmpty(state.ConnectionTypeLabel) ? "未知" : state.ConnectionTypeLabel);
        }

        private static string BuildRangeSummary(StickRangeResult left, StickRangeResult right, bool active)
        {
            if (active) return "正在记录范围：请沿两个摇杆边缘各旋转一圈，完成后点击“结束范围”。";
            return string.Format(CultureInfo.InvariantCulture,
                "左摇杆：{0}，覆盖 {1:0}%{2}\n右摇杆：{3}，覆盖 {4:0}%{5}",
                left == null ? "尚未测试" : left.Status, left == null ? 0 : left.CoveragePercent,
                left == null || string.IsNullOrEmpty(left.MissingDirections) || left.MissingDirections == "无" ? string.Empty : "，缺失 " + left.MissingDirections,
                right == null ? "尚未测试" : right.Status, right == null ? 0 : right.CoveragePercent,
                right == null || string.IsNullOrEmpty(right.MissingDirections) || right.MissingDirections == "无" ? string.Empty : "，缺失 " + right.MissingDirections);
        }

        private void UpdateStickTriggerTestPage(ControllerState state)
        {
            if (state == null) return;
            stickTriggerTestEngine.Update(state);
            stickTestLeftPlot.UpdateValue(state.LeftStickX, state.LeftStickY);
            stickTestRightPlot.UpdateValue(state.RightStickX, state.RightStickY);
            stickTestLeftPlot.Deadzone = stickTriggerTestEngine.SuggestedDeadzone;
            stickTestRightPlot.Deadzone = stickTriggerTestEngine.SuggestedDeadzone;
            if (stickTestLeftInfo != null) stickTestLeftInfo.Text = string.Format(CultureInfo.InvariantCulture, "当前位置\nX {0:0.000}\nY {1:0.000}\n\n中心漂移\n{2:0.0}% · {3}\n\n建议死区\n{4:0.0}%", state.LeftStickX, state.LeftStickY, stickTriggerTestEngine.LeftDriftPercent, stickTriggerTestEngine.LeftRating, stickTriggerTestEngine.SuggestedDeadzone * 100.0);
            if (stickTestRightInfo != null) stickTestRightInfo.Text = string.Format(CultureInfo.InvariantCulture, "当前位置\nX {0:0.000}\nY {1:0.000}\n\n中心漂移\n{2:0.0}% · {3}\n\n建议死区\n{4:0.0}%", state.RightStickX, state.RightStickY, stickTriggerTestEngine.RightDriftPercent, stickTriggerTestEngine.RightRating, stickTriggerTestEngine.SuggestedDeadzone * 100.0);
            if (triggerTestInfo != null) triggerTestInfo.Text = string.Format(CultureInfo.InvariantCulture, "当前：L {0:0}% / R {1:0}%    峰值：L {2:0}% / R {3:0}%    回零：{4}    满行程：{5}", state.LeftTrigger * 100.0, state.RightTrigger * 100.0, stickTriggerTestEngine.LeftTriggerMaximum * 100.0, stickTriggerTestEngine.RightTriggerMaximum * 100.0, stickTriggerTestEngine.TriggersReturnToZero ? "已回零" : "未回零", stickTriggerTestEngine.LeftTriggerMaximum >= 0.95 && stickTriggerTestEngine.RightTriggerMaximum >= 0.95 ? "已达到" : "未达到");
        }

        private Grid BuildGuidedOverlay()
        {
            Grid overlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(218, 5, 10, 15)),
                Visibility = Visibility.Collapsed
            };
            Border card = new Border
            {
                Width = 820,
                Height = 620,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush(Color.FromRgb(17, 29, 39), Color.FromRgb(24, 39, 50), 120),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 82, 96)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };
            Grid layout = new Grid { Margin = new Thickness(28, 22, 28, 22) };
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
            layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel();
            title.Children.Add(new TextBlock { Text = "自动体检", Foreground = Palette.TextBrush, FontSize = 23, FontWeight = FontWeights.SemiBold });
            title.Children.Add(new TextBlock { Text = "按步骤完成动作，最后生成可复测的分项结果", Foreground = Palette.MutedBrush, FontSize = 11, Margin = new Thickness(0, 5, 0, 0) });
            header.Children.Add(title);
            guidedCloseButton = MakeButton("关闭", false);
            guidedCloseButton.Width = 76;
            guidedCloseButton.Height = 34;
            guidedCloseButton.Click += delegate { CloseGuidedTest(); };
            Grid.SetColumn(guidedCloseButton, 1);
            header.Children.Add(guidedCloseButton);
            layout.Children.Add(header);

            Grid progressArea = new Grid { Margin = new Thickness(0, 6, 0, 8) };
            progressArea.ColumnDefinitions.Add(new ColumnDefinition());
            progressArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            guidedProgress = new ProgressBar
            {
                Height = 7,
                Minimum = 0,
                Maximum = 1,
                Foreground = Palette.BlueBrush,
                Background = new SolidColorBrush(Color.FromRgb(36, 50, 61)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            guidedProgressText = new TextBlock { Text = "步骤 1 / 5", Foreground = Palette.MutedBrush, FontSize = 12, Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            progressArea.Children.Add(guidedProgress);
            Grid.SetColumn(guidedProgressText, 1);
            progressArea.Children.Add(guidedProgressText);
            Grid.SetRow(progressArea, 1);
            layout.Children.Add(progressArea);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.12, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            body.ColumnDefinitions.Add(new ColumnDefinition());

            Border instructionCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 23, 31)),
                BorderBrush = Palette.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(22, 20, 22, 18)
            };
            StackPanel instructions = new StackPanel();
            guidedStageText = new TextBlock { Text = "第 1 步 · 中心基线", Foreground = Palette.BlueBrush, FontSize = 13, FontWeight = FontWeights.SemiBold };
            guidedInstructionText = new TextBlock { Text = "松开所有按键，并保持两个摇杆居中", Foreground = Palette.TextBrush, FontSize = 21, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 0) };
            guidedDetailText = new TextBlock { Text = "稳定保持 2 秒；检测到移动时计时会自动重新开始。", Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Margin = new Thickness(0, 12, 0, 0) };
            instructions.Children.Add(guidedStageText);
            instructions.Children.Add(guidedInstructionText);
            instructions.Children.Add(guidedDetailText);
            guidedChecklistTitle = new TextBlock { Text = "本步检测点", Foreground = Palette.TextBrush, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 24, 0, 8) };
            guidedChecklistPanel = new WrapPanel();
            instructions.Children.Add(guidedChecklistTitle);
            instructions.Children.Add(guidedChecklistPanel);
            instructionCard.Child = instructions;
            body.Children.Add(instructionCard);

            Border resultsCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 23, 31)),
                BorderBrush = Palette.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(20, 17, 20, 16)
            };
            StackPanel results = new StackPanel();
            results.Children.Add(new TextBlock { Text = "分项状态", Foreground = Palette.TextBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 9) });
            string[] resultNames = { "连接与采样", "中心基线", "左摇杆行程", "右摇杆行程", "LT / RT 扳机", "14 个按键" };
            for (int i = 0; i < resultNames.Length; i++) results.Children.Add(BuildGuidedResultRow(i, resultNames[i]));
            resultsCard.Child = results;
            Grid.SetColumn(resultsCard, 2);
            body.Children.Add(resultsCard);
            Grid.SetRow(body, 2);
            layout.Children.Add(body);

            Grid footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Children.Add(new TextBlock { Text = "可跳过暂时无法完成的步骤；报告会标记为未完成。", Foreground = Palette.MutedBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
            guidedRestartButton = MakeButton("重新测试", false);
            guidedRestartButton.Width = 104;
            guidedRestartButton.Visibility = Visibility.Collapsed;
            guidedRestartButton.Click += delegate { BeginGuidedTest(); };
            guidedActionButton = MakeButton("跳过此项", false);
            guidedActionButton.Width = 118;
            guidedActionButton.Margin = new Thickness(10, 0, 0, 0);
            guidedActionButton.Click += OnGuidedAction;
            actions.Children.Add(guidedRestartButton);
            actions.Children.Add(guidedActionButton);
            Grid.SetColumn(actions, 1);
            footer.Children.Add(actions);
            Grid.SetRow(footer, 3);
            layout.Children.Add(footer);

            card.Child = layout;
            overlay.Children.Add(card);
            return overlay;
        }

        private UIElement BuildGuidedResultRow(int index, string label)
        {
            Grid row = new Grid { Height = 43 };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            TextBlock status = new TextBlock { Text = "待测试", Foreground = Palette.MutedBrush, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            guidedResultTexts[index] = status;
            Grid.SetColumn(status, 1);
            row.Children.Add(status);
            row.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(100, 43, 57, 68)), VerticalAlignment = VerticalAlignment.Bottom });
            return row;
        }

        private void BeginGuidedTest()
        {
            guidedTest.Begin();
            renderedGuidedStage = GuidedStage.Idle;
            SetShellEnabled(false);
            guidedOverlay.Visibility = Visibility.Visible;
            UpdateGuidedUI();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate
            {
                if (guidedCloseButton != null) guidedCloseButton.Focus();
            }));
            if (footerStatus != null) footerStatus.Text = "自动体检已开始；按屏幕提示完成 5 个步骤。";
        }

        private void CloseGuidedTest()
        {
            bool completed = guidedTest.IsComplete;
            bool cancelled = guidedTest.Active;
            if (cancelled) guidedTest.Cancel();
            if (guidedOverlay != null) guidedOverlay.Visibility = Visibility.Collapsed;
            SetShellEnabled(true);
            if (guidedLaunchButton != null) guidedLaunchButton.Focus();
            if (footerStatus != null && completed)
            {
                footerStatus.Text = guidedTest.HasSkipped ? "自动体检已结束：部分项目未完成。" : "自动体检已完成：全部分项通过。";
            }
            else if (footerStatus != null && cancelled)
            {
                footerStatus.Text = "自动体检已取消，可随时重新开始。";
            }
        }

        private void SetShellEnabled(bool enabled)
        {
            if (shellTitle != null) shellTitle.IsEnabled = enabled;
            if (shellContent != null) shellContent.IsEnabled = enabled;
            if (shellFooter != null) shellFooter.IsEnabled = enabled;
        }

        private void OnGuidedAction(object sender, RoutedEventArgs e)
        {
            if (guidedTest.IsComplete)
            {
                ExportCurrentReport();
                return;
            }
            guidedTest.SkipCurrent();
            UpdateGuidedUI();
        }

        private void UpdateGuidedUI()
        {
            if (guidedStageText == null) return;
            SetTextIfChanged(guidedStageText, guidedTest.StageTitle);
            SetTextIfChanged(guidedInstructionText, guidedTest.Instruction);
            SetTextIfChanged(guidedDetailText, guidedTest.Detail);
            double overall = guidedTest.IsComplete ? 1.0 : ((guidedTest.StepNumber - 1) + guidedTest.Progress) / 5.0;
            guidedProgress.Value = Math.Max(0, Math.Min(1, overall));
            SetTextIfChanged(guidedProgressText, guidedTest.IsComplete
                ? (guidedTest.HasSkipped ? "完成 · 部分项目待复测" : "完成 · 全部通过")
                : string.Format(CultureInfo.InvariantCulture, "步骤 {0} / 5 · {1:0}%", guidedTest.StepNumber, guidedTest.Progress * 100.0));

            RebuildGuidedChecklistIfNeeded();
            UpdateGuidedChecklistState();

            for (int i = 0; i < guidedResultTexts.Length; i++)
            {
                string value = guidedTest.ResultText(i);
                if (i == 0 && !currentState.Connected) value = guidedTest.IsComplete ? "未完成" : "未连接";
                SetTextIfChanged(guidedResultTexts[i], value);
                guidedResultTexts[i].Foreground = GuidedStatusBrush(value);
            }

            guidedRestartButton.Visibility = guidedTest.IsComplete ? Visibility.Visible : Visibility.Collapsed;
            guidedActionButton.Content = guidedTest.IsComplete ? "导出结果" : "跳过此项";
            SetButtonPrimary(guidedActionButton, guidedTest.IsComplete);
            AutomationProperties.SetName(guidedActionButton, guidedTest.IsComplete ? "导出体检结果" : "跳过当前体检项目");
        }

        private void RebuildGuidedChecklistIfNeeded()
        {
            if (guidedChecklistPanel == null || renderedGuidedStage == guidedTest.Stage) return;
            renderedGuidedStage = guidedTest.Stage;
            guidedChecklistPanel.Children.Clear();
            guidedButtonChips.Clear();

            if (guidedTest.Stage == GuidedStage.Center)
            {
                guidedChecklistTitle.Text = "本步检测点";
                AddGuidedChip(101, "左摇杆居中", 92);
                AddGuidedChip(102, "右摇杆居中", 92);
                AddGuidedChip(103, "LT 已松开", 92);
                AddGuidedChip(104, "RT 已松开", 92);
                AddGuidedChip(105, "按键已松开", 92);
            }
            else if (guidedTest.Stage == GuidedStage.LeftStick || guidedTest.Stage == GuidedStage.RightStick)
            {
                guidedChecklistTitle.Text = guidedTest.Stage == GuidedStage.LeftStick ? "左摇杆方向" : "右摇杆方向";
                AddGuidedChip(1, "向右", 72);
                AddGuidedChip(2, "向左", 72);
                AddGuidedChip(4, "向上", 72);
                AddGuidedChip(8, "向下", 72);
            }
            else if (guidedTest.Stage == GuidedStage.Triggers)
            {
                guidedChecklistTitle.Text = "扳机行程";
                AddGuidedChip(1, "LT 达到 90%", 118);
                AddGuidedChip(2, "RT 达到 90%", 118);
            }
            else
            {
                guidedChecklistTitle.Text = guidedTest.IsComplete ? "按键验证结果" : "按键清单";
                for (int i = 0; i < GuidedTestEngine.ButtonMasks.Length; i++)
                {
                    AddGuidedChip(GuidedTestEngine.ButtonMasks[i], GuidedTestEngine.ButtonNames[i], 52);
                }
            }
        }

        private void AddGuidedChip(int key, string text, double width)
        {
            TextBlock chipText = new TextBlock { Text = text, Foreground = Palette.MutedBrush, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Border chip = new Border { Width = width, Height = 29, Margin = new Thickness(0, 0, 7, 7), CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromRgb(24, 37, 47)), BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Child = chipText };
            guidedButtonChips[key] = chip;
            guidedChecklistPanel.Children.Add(chip);
        }

        private void UpdateGuidedChecklistState()
        {
            if (guidedTest.Stage == GuidedStage.Center)
            {
                double leftMagnitude = Math.Sqrt(currentState.LeftNormalizedX * currentState.LeftNormalizedX + currentState.LeftNormalizedY * currentState.LeftNormalizedY);
                double rightMagnitude = Math.Sqrt(currentState.RightNormalizedX * currentState.RightNormalizedX + currentState.RightNormalizedY * currentState.RightNormalizedY);
                SetGuidedChipState(101, currentState.Connected && leftMagnitude < 0.12);
                SetGuidedChipState(102, currentState.Connected && rightMagnitude < 0.12);
                SetGuidedChipState(103, currentState.Connected && currentState.LeftTrigger < 14);
                SetGuidedChipState(104, currentState.Connected && currentState.RightTrigger < 14);
                SetGuidedChipState(105, currentState.Connected && currentState.Buttons == 0);
                return;
            }

            if (guidedTest.Stage == GuidedStage.LeftStick || guidedTest.Stage == GuidedStage.RightStick)
            {
                int directions = guidedTest.Stage == GuidedStage.LeftStick ? guidedTest.LeftDirections : guidedTest.RightDirections;
                int[] bits = { 1, 2, 4, 8 };
                for (int i = 0; i < bits.Length; i++) SetGuidedChipState(bits[i], (directions & bits[i]) != 0);
                return;
            }

            if (guidedTest.Stage == GuidedStage.Triggers)
            {
                SetGuidedChipState(1, (guidedTest.TriggerMask & 1) != 0);
                SetGuidedChipState(2, (guidedTest.TriggerMask & 2) != 0);
                return;
            }

            for (int i = 0; i < GuidedTestEngine.ButtonMasks.Length; i++)
            {
                int mask = GuidedTestEngine.ButtonMasks[i];
                SetGuidedChipState(mask, (guidedTest.SeenButtons & mask) != 0);
            }
        }

        private void SetGuidedChipState(int key, bool complete)
        {
            Border chip;
            if (!guidedButtonChips.TryGetValue(key, out chip)) return;
            chip.Background = complete ? new SolidColorBrush(Color.FromArgb(55, Palette.Green.R, Palette.Green.G, Palette.Green.B)) : new SolidColorBrush(Color.FromRgb(24, 37, 47));
            chip.BorderBrush = complete ? Palette.GreenBrush : Palette.BorderBrush;
            TextBlock label = chip.Child as TextBlock;
            if (label != null) label.Foreground = complete ? Palette.GreenBrush : Palette.MutedBrush;
        }

        private static Brush GuidedStatusBrush(string status)
        {
            if (status == "通过") return Palette.GreenBrush;
            if (status == "测量中") return Palette.BlueBrush;
            if (status == "已跳过") return Palette.WarningBrush;
            if (status == "未连接" || status == "未完成") return Palette.RedBrush;
            return Palette.MutedBrush;
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

        private Grid BuildLeftColumn()
        {
            Grid left = new Grid();
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(172) });

            deviceCard = Card(BuildDeviceCard());
            deviceCard.SizeChanged += delegate { UpdateDeviceCardResponsiveLayout(); };
            Grid.SetRow(deviceCard, 0);
            left.Children.Add(deviceCard);

            Border controllerCard = LabVisualStyles.CreateSectionCard(new Grid
            {
                ClipToBounds = true,
                Children = { controllerVisualHost }
            });
            controllerCard.Padding = new Thickness(6, 4, 6, 4);
            controllerCard.ClipToBounds = true;
            Grid.SetRow(controllerCard, 2);
            left.Children.Add(controllerCard);

            Grid triggerStrip = new Grid { ClipToBounds = true };
            triggerStrip.ColumnDefinitions.Add(new ColumnDefinition());
            triggerStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            triggerStrip.ColumnDefinitions.Add(new ColumnDefinition());
            Border leftTrigger = LabVisualStyles.CreateSectionCard(BuildTriggerCard("LT", leftTriggerChart, Palette.GreenBrush, true));
            Border rightTrigger = LabVisualStyles.CreateSectionCard(BuildTriggerCard("RT", rightTriggerChart, Palette.BlueBrush, false));
            triggerStrip.Children.Add(leftTrigger);
            Grid.SetColumn(rightTrigger, 2);
            triggerStrip.Children.Add(rightTrigger);
            Grid.SetRow(triggerStrip, 4);
            left.Children.Add(triggerStrip);
            return left;
        }

        private UIElement BuildDeviceCard()
        {
            Grid grid = new Grid { Margin = new Thickness(22, 14, 22, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel device = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Border logo = new Border
            {
                Width = 43,
                Height = 43,
                CornerRadius = new CornerRadius(22),
                Background = new SolidColorBrush(Color.FromRgb(239, 243, 246)),
                Child = deviceLogoText = new TextBlock { Text = "X", Foreground = new SolidColorBrush(Color.FromRgb(28, 39, 48)), FontSize = 26, FontWeight = FontWeights.Light, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            device.Children.Add(logo);
            StackPanel name = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            deviceNameText = new TextBlock { Text = "手柄自动识别", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Palette.TextBrush };
            name.Children.Add(deviceNameText);
            StackPanel state = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            connectionDot = new Ellipse { Width = 10, Height = 10, Fill = Palette.BlueBrush, VerticalAlignment = VerticalAlignment.Center };
            connectionText = new TextBlock { Text = demoMode ? "动态演示" : "正在扫描…", Foreground = Palette.BlueBrush, FontSize = 13, Margin = new Thickness(8, -1, 0, 0) };
            AutomationProperties.SetLiveSetting(connectionText, AutomationLiveSetting.Polite);
            refreshRateText = new TextBlock { Text = "显示计算中", Foreground = Palette.BlueBrush, FontSize = 13 };
            refreshRateBadge = new Border { Background = new SolidColorBrush(Color.FromRgb(29, 49, 64)), CornerRadius = new CornerRadius(14), Margin = new Thickness(20, -4, 0, -4), Padding = new Thickness(12, 4, 12, 4), Child = refreshRateText };
            state.Children.Add(connectionDot);
            state.Children.Add(connectionText);
            connectionMethodBadge = BuildCompactConnectionMethodBadge();
            state.Children.Add(connectionMethodBadge);
            state.Children.Add(refreshRateBadge);
            controllerFamilySelectorButton = MakeButton(DeviceSelectionLabel(), false);
            controllerFamilySelectorButton.Width = 156;
            controllerFamilySelectorButton.Height = 26;
            controllerFamilySelectorButton.FontSize = 11;
            controllerFamilySelectorButton.Padding = new Thickness(10, 2, 10, 3);
            controllerFamilySelectorButton.VerticalContentAlignment = VerticalAlignment.Center;
            controllerFamilySelectorButton.Margin = new Thickness(16, -3, 0, -3);
            controllerFamilySelectorButton.ToolTip = "选择当前在线的 Xbox 或索尼 DS 手柄；断开后自动切换到其他在线设备";
            controllerFamilySelectorButton.ContextMenu = CreateDarkContextMenu(300);
            controllerFamilySelectorButton.Click += delegate
            {
                RefreshDeviceSelectorMenu();
                OpenContextMenu(controllerFamilySelectorButton);
            };
            state.Children.Add(controllerFamilySelectorButton);
            demoModeButton = MakeButton(demoMode ? "退出演示" : "动态演示", demoMode);
            demoModeButton.Width = 88;
            demoModeButton.Height = 26;
            demoModeButton.FontSize = 11;
            demoModeButton.Padding = new Thickness(10, 2, 10, 3);
            demoModeButton.VerticalContentAlignment = VerticalAlignment.Center;
            demoModeButton.Margin = new Thickness(8, -3, 0, -3);
            demoModeButton.ToolTip = "在实时手柄监测与自动动态演示之间切换（F9）";
            AutomationProperties.SetName(demoModeButton, demoMode ? "退出动态演示" : "启动动态演示");
            demoModeButton.Click += delegate { ToggleDemoMode(); };
            state.Children.Add(demoModeButton);
            name.Children.Add(state);
            device.Children.Add(name);
            grid.Children.Add(device);

            // Start collapsed so the header cannot force the visualizer's left star
            // column wider than the available window before responsive layout runs.
            StackPanel metadata = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed };
            deviceMetadataPanel = metadata;
            deviceMetaText = AddMetadata(metadata, "驱动", input.LibraryName);
            samplingRateText = AddMetadata(metadata, "采样", demoMode ? "演示" : "计算中");
            Grid.SetColumn(metadata, 1);
            grid.Children.Add(metadata);
            return grid;
        }

        private Border BuildCompactConnectionMethodBadge()
        {
            StackPanel value = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "点击选择自动识别或手动显示连接方式" };
            value.Children.Add(new TextBlock { Text = "连接方式", Foreground = Palette.MutedBrush, FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
            connectionMethodDot = new Ellipse { Width = 7, Height = 7, Fill = Palette.MutedBrush, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            value.Children.Add(connectionMethodDot);
            connectionMethodText = new TextBlock { Text = "检测中", Foreground = Palette.TextBrush, FontSize = 11, Margin = new Thickness(5, -1, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 94, ToolTip = "根据当前 XInput 与 Windows 设备路径自动识别" };
            value.Children.Add(connectionMethodText);
            connectionMethodMenu = CreateDarkContextMenu(186);
            AddConnectionMethodMenuItem("自动识别（推荐）", "自动");
            AddConnectionMethodMenuItem("手动显示：有线", "有线");
            AddConnectionMethodMenuItem("手动显示：蓝牙", "蓝牙");
            AddConnectionMethodMenuItem("手动显示：USB 2.4G", "USB 2.4G");
            connectionMethodMenu.Items.Add(MakeDarkMenuSeparator());
            AddConnectionRouteMenuItem("将当前 USB 状态设为有线", "有线");
            AddConnectionRouteMenuItem("将当前 USB 状态设为 2.4G 接收器", "USB 2.4G");
            value.ContextMenu = connectionMethodMenu;
            value.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                OpenContextMenu(value);
                e.Handled = true;
            };
            Border badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 42, 54)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 78, 94)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(12, -3, 0, -3),
                Padding = new Thickness(8, 3, 9, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = value,
                ToolTip = "手柄连接方式；点击可切换自动识别或手动显示"
            };
            AutomationProperties.SetName(badge, "手柄连接方式");
            return badge;
        }

        private TextBlock AddMetadata(StackPanel parent, string label, string value)
        {
            Border divider = new Border { Width = 1, Height = 45, Background = Palette.BorderBrush, Margin = new Thickness(12, 0, 12, 0) };
            parent.Children.Add(divider);
            StackPanel block = new StackPanel { MinWidth = 78, VerticalAlignment = VerticalAlignment.Center };
            block.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 12 });
            TextBlock text = new TextBlock { Text = value, Foreground = Palette.TextBrush, FontSize = 13, Margin = new Thickness(0, 8, 0, 0) };
            block.Children.Add(text);
            parent.Children.Add(block);
            return text;
        }

        private void AddConnectionMethodMetadata(StackPanel parent)
        {
            parent.Children.Add(new Border { Width = 1, Height = 45, Background = Palette.BorderBrush, Margin = new Thickness(12, 0, 12, 0) });
            StackPanel block = new StackPanel { MinWidth = 152, VerticalAlignment = VerticalAlignment.Center };
            block.Children.Add(new TextBlock { Text = "手柄连接方式", Foreground = Palette.MutedBrush, FontSize = 12 });
            StackPanel value = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "点击选择自动识别或手动显示连接方式" };
            connectionMethodDot = new Ellipse { Width = 9, Height = 9, Fill = Palette.MutedBrush, VerticalAlignment = VerticalAlignment.Center };
            value.Children.Add(connectionMethodDot);
            connectionMethodText = new TextBlock { Text = "检测中", Foreground = Palette.TextBrush, FontSize = 13, Margin = new Thickness(8, -1, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "根据当前 XInput 与 Windows 设备路径自动识别" };
            value.Children.Add(connectionMethodText);
            connectionMethodMenu = CreateDarkContextMenu(186);
            AddConnectionMethodMenuItem("自动识别（推荐）", "自动");
            AddConnectionMethodMenuItem("手动显示：有线", "有线");
            AddConnectionMethodMenuItem("手动显示：蓝牙", "蓝牙");
            AddConnectionMethodMenuItem("手动显示：USB 2.4G", "USB 2.4G");
            connectionMethodMenu.Items.Add(MakeDarkMenuSeparator());
            AddConnectionRouteMenuItem("将当前 USB 状态设为有线", "有线");
            AddConnectionRouteMenuItem("将当前 USB 状态设为 2.4G 接收器", "USB 2.4G");
            value.ContextMenu = connectionMethodMenu;
            value.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                OpenContextMenu(value);
                e.Handled = true;
            };
            block.Children.Add(value);
            parent.Children.Add(block);
        }

        private void AddConnectionMethodMenuItem(string label, string mode)
        {
            MenuItem item = MakeDarkMenuItem(label);
            item.IsCheckable = true;
            item.IsChecked = connectionMethodOverride == mode;
            item.Click += delegate { SelectConnectionMethodOverride(mode); };
            connectionMethodMenu.Items.Add(item);
        }

        private void AddConnectionRouteMenuItem(string label, string mode)
        {
            MenuItem item = MakeDarkMenuItem(label);
            item.Click += delegate { MarkCurrentUsbRoute(mode); };
            connectionMethodMenu.Items.Add(item);
        }

        private static string NormalizeConnectionMethodOverride(string value)
        {
            return value == "有线" || value == "蓝牙" || value == "USB 2.4G" ? value : "自动";
        }

        private static ControllerFamily NormalizeControllerFamily(string value)
        {
            if (string.Equals(value, "Xbox", StringComparison.OrdinalIgnoreCase)) return ControllerFamily.Xbox;
            if (string.Equals(value, "PlayStation", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "索尼 DS", StringComparison.OrdinalIgnoreCase)) return ControllerFamily.PlayStation;
            return ControllerFamily.Auto;
        }

        private string ControllerFamilySelectionLabel()
        {
            if (selectedControllerFamily == ControllerFamily.Xbox) return "手柄：Xbox";
            if (selectedControllerFamily == ControllerFamily.PlayStation) return "手柄：索尼 DS";
            return "手柄：自动";
        }

        private string DeviceSelectionLabel()
        {
            ControllerState[] devices = latestControllerStates ?? new ControllerState[0];
            for (int i = 0; i < devices.Length; i++)
            {
                if (string.Equals(devices[i].DeviceId, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return "设备：" + ShortDeviceName(devices[i]);
                }
            }
            if (demoMode && multiDemoMode) return devices.Length == 0 ? "设备：多设备演示" : "设备：自动 · " + devices.Length.ToString(CultureInfo.InvariantCulture) + " 台";
            if (demoMode) return "设备：动态演示";
            return devices.Length == 0 ? "设备：自动" : "设备：自动 · " + devices.Length.ToString(CultureInfo.InvariantCulture) + " 台";
        }

        private static string ShortDeviceName(ControllerState state)
        {
            if (state == null) return "自动";
            string prefix = state.ControllerType == ControllerType.Xbox ? "Xbox" : "DualSense";
            if (state.PlayerIndex >= 0 && state.ControllerType == ControllerType.Xbox) return prefix + " " + (state.PlayerIndex + 1).ToString(CultureInfo.InvariantCulture);
            return prefix;
        }

        private void RefreshDeviceSelectorMenu()
        {
            if (controllerFamilySelectorButton == null) return;
            ContextMenu menu = controllerFamilySelectorButton.ContextMenu;
            if (menu == null) return;
            menu.Items.Clear();
            ControllerState[] devices = latestControllerStates ?? new ControllerState[0];
            MenuItem auto = MakeDarkMenuItem("自动选择在线设备");
            auto.IsCheckable = true;
            auto.IsChecked = string.IsNullOrEmpty(selectedDeviceId);
            auto.Click += delegate { SelectDevice(null); };
            menu.Items.Add(auto);
            if (devices.Length > 0) menu.Items.Add(MakeDarkMenuSeparator());
            for (int i = 0; i < devices.Length; i++)
            {
                ControllerState candidate = devices[i];
                MenuItem item = MakeDarkMenuItem(ShortDeviceName(candidate) + " · " + candidate.ConnectionTypeLabel + " · " + candidate.InputBackend);
                item.IsCheckable = true;
                item.IsChecked = string.Equals(selectedDeviceId, candidate.DeviceId, StringComparison.OrdinalIgnoreCase);
                string id = candidate.DeviceId;
                item.Click += delegate { SelectDevice(id); };
                menu.Items.Add(item);
            }
            if (devices.Length == 0)
            {
                MenuItem empty = MakeDarkMenuItem("未发现在线手柄");
                empty.IsEnabled = false;
                menu.Items.Add(empty);
            }
        }

        private void SelectDevice(string deviceId)
        {
            if (rumbleStudioPage != null) rumbleStudioPage.ResetForDeviceChange();
            if (healthCheckView != null) healthCheckView.ResetForDeviceChange();
            rumbleController.Stop("设备已切换，震动已停止");
            selectedDeviceId = deviceId;
            if (string.IsNullOrEmpty(deviceId)) productSettings.AutoConnect = true;
            ClearTriggerHistory();
            diagnostics.Reset();
            inputTestEngine.Reset(currentControllerState);
            stickTriggerTestEngine.Reset(currentControllerState);
            stickDriftTestEngine.Reset(null);
            if (joystickTestPage != null) joystickTestPage.ResetForDeviceChange("设备已切换，摇杆检测已取消");
            ClearStickTestVisualState();
            if (motionPoseView != null) motionPoseView.SetState(null);
            nextMotionUiRefresh = DateTime.MinValue;
            renderedInputTestSignature = null;
            if (controllerFamilySelectorButton != null) controllerFamilySelectorButton.Content = DeviceSelectionLabel();
            if (footerStatus != null) footerStatus.Text = string.IsNullOrEmpty(deviceId) ? "已启用自动设备选择。" : "已切换到 " + DeviceSelectionLabel() + "。";
            if (!demoMode) SaveSettings();
        }

        private void AddControllerFamilyMenuItem(ContextMenu menu, string label, ControllerFamily family)
        {
            MenuItem item = MakeDarkMenuItem(label);
            item.IsCheckable = true;
            item.IsChecked = selectedControllerFamily == family;
            item.Click += delegate { SelectControllerFamily(family); };
            menu.Items.Add(item);
        }

        private void SelectControllerFamily(ControllerFamily family)
        {
            if (rumbleStudioPage != null) rumbleStudioPage.ResetForDeviceChange();
            if (healthCheckView != null) healthCheckView.ResetForDeviceChange();
            rumbleController.Stop("手柄类型已切换，震动已停止");
            selectedControllerFamily = family;
            ClearTriggerHistory();
            stickDriftTestEngine.Reset(null);
            if (joystickTestPage != null) joystickTestPage.ResetForDeviceChange("手柄类型已切换，摇杆检测已取消");
            ClearStickTestVisualState();
            if (demoMode)
            {
                if (family == ControllerFamily.PlayStation) sonyDemoMode = true;
                else if (family == ControllerFamily.Xbox) sonyDemoMode = false;
            }
            if (controllerFamilySelectorButton != null) controllerFamilySelectorButton.Content = ControllerFamilySelectionLabel();
            ContextMenu menu = controllerFamilySelectorButton == null ? null : controllerFamilySelectorButton.ContextMenu;
            if (menu != null)
            {
                for (int i = 0; i < menu.Items.Count; i++)
                {
                    MenuItem item = menu.Items[i] as MenuItem;
                    if (item == null) continue;
                    item.IsChecked = (family == ControllerFamily.Auto && string.Equals(item.Header as string, "手柄：自动", StringComparison.Ordinal)) ||
                        (family == ControllerFamily.Xbox && string.Equals(item.Header as string, "手柄：Xbox", StringComparison.Ordinal)) ||
                        (family == ControllerFamily.PlayStation && string.Equals(item.Header as string, "手柄：索尼 DS", StringComparison.Ordinal));
                }
            }
            diagnostics.Reset();
            if (demoMode) diagnostics.UseDemoBaseline();
            UpdateFamilyPresentation(currentState);
            if (!demoMode) SaveSettings();
            if (footerStatus != null)
            {
                footerStatus.Text = demoMode
                    ? "动态演示模式：已切换到" + (CurrentDemoFamily() == ControllerFamily.PlayStation ? "索尼 DS" : "Xbox") + "演示。"
                    : family == ControllerFamily.Auto
                        ? "已启用自动识别：Xbox 使用 XInput，索尼 DS 使用原生 HID。"
                        : family == ControllerFamily.Xbox
                            ? "已固定为 Xbox 监测；可继续选择玩家槽位。"
                            : "已固定为索尼 DS 监测；操作 DualSense 或 DualShock 4 任意按键开始读取。";
            }
        }

        private ControllerFamily CurrentDemoFamily()
        {
            if (selectedControllerFamily == ControllerFamily.PlayStation) return ControllerFamily.PlayStation;
            if (selectedControllerFamily == ControllerFamily.Xbox) return ControllerFamily.Xbox;
            if (sonyDemoMode) return ControllerFamily.PlayStation;
            return renderedControllerFamily == ControllerFamily.PlayStation ? ControllerFamily.PlayStation : ControllerFamily.Xbox;
        }

        private InputSnapshot CreateCurrentDemoSnapshot()
        {
            return CurrentDemoFamily() == ControllerFamily.PlayStation ? InputSnapshot.CreateSonyDemo() : InputSnapshot.CreateDemo();
        }

        private ControllerState[] CreateMultiDemoStates()
        {
            return new ControllerState[]
            {
                ControllerStateAdapter.FromSnapshot(InputSnapshot.CreateDemo()),
                ControllerStateAdapter.FromSnapshot(InputSnapshot.CreateSonyDemo())
            };
        }

        private void ToggleDemoMode()
        {
            SetDemoMode(!demoMode);
        }

        private void SetDemoMode(bool enabled)
        {
            if (demoMode == enabled) return;
            if (rumbleStudioPage != null) rumbleStudioPage.ResetForDeviceChange();
            if (healthCheckView != null) healthCheckView.ResetForDeviceChange();
            if (enabled)
            {
                SaveSettings();
                sonyDemoMode = selectedControllerFamily == ControllerFamily.PlayStation ||
                    (selectedControllerFamily == ControllerFamily.Auto && renderedControllerFamily == ControllerFamily.PlayStation);
                StopSampling();
                demoMode = true;
                diagnostics.Reset();
                diagnostics.UseDemoBaseline();
            }
            else
            {
                demoMode = false;
                sonyDemoMode = false;
                diagnostics.Reset();
                latestInput = new InputSnapshot();
                StartSampling();
            }

            if (calibrating)
            {
                calibrating = false;
                calibrationStatusVisible = false;
                calibrationProgress.Visibility = Visibility.Collapsed;
            }
            lastConnected = false;
            refreshTicks = 0;
            Interlocked.Exchange(ref samplingTicks, 0);
            actualSamplingHz = 0;
            rateWindowStarted = DateTime.UtcNow;

            if (demoModeButton != null)
            {
                demoModeButton.Content = demoMode ? "退出演示" : "动态演示";
                demoModeButton.ToolTip = demoMode
                    ? "退出自动动画并恢复实时手柄监测（F9）"
                    : "在实时手柄监测与自动动态演示之间切换（F9）";
                AutomationProperties.SetName(demoModeButton, demoMode ? "退出动态演示" : "启动动态演示");
                SetButtonPrimary(demoModeButton, demoMode);
            }
            if (controllerSelectorButton != null)
            {
                controllerSelectorButton.IsEnabled = !demoMode;
                controllerSelectorButton.Content = demoMode ? "设备：演示" : ControllerSelectionLabel();
            }
            if (reducedMotionCheck != null) reducedMotionCheck.IsEnabled = !demoMode;
            if (calibrateButton != null)
            {
                calibrateButton.IsEnabled = !demoMode;
                calibrateButton.Content = demoMode ? "演示中" : calibrationSuggestionPending ? "应用死区" : "中心校准";
            }
            if (samplingRateText != null) samplingRateText.Text = demoMode ? "演示" : "计算中";
            if (footerStatus != null)
            {
                footerStatus.Text = demoMode
                    ? "动态演示模式：摇杆、扳机、方向键和按键会自动变化；点击“退出演示”恢复实时监测。"
                    : "已恢复实时监测：连接或操作手柄后会立即显示输入。";
            }
        }

        private void SelectConnectionMethodOverride(string mode)
        {
            connectionMethodOverride = NormalizeConnectionMethodOverride(mode);
            if (connectionMethodMenu != null)
            {
                for (int i = 0; i < connectionMethodMenu.Items.Count; i++)
                {
                    MenuItem item = connectionMethodMenu.Items[i] as MenuItem;
                    if (item == null) continue;
                    string header = item.Header as string;
                    item.IsChecked = (connectionMethodOverride == "自动" && header == "自动识别（推荐）") ||
                        (connectionMethodOverride == "有线" && header == "手动显示：有线") ||
                        (connectionMethodOverride == "蓝牙" && header == "手动显示：蓝牙") ||
                        (connectionMethodOverride == "USB 2.4G" && header == "手动显示：USB 2.4G");
                }
            }
            UpdateConnectionMethod(currentState);
            if (!demoMode) SaveSettings();
            if (footerStatus != null)
            {
                footerStatus.Text = connectionMethodOverride == "自动"
                    ? "连接方式已恢复自动识别；切换模式后请按任意手柄按键一次以刷新路径。"
                    : "连接方式已设为“" + connectionMethodOverride + "”（仅覆盖显示，不影响输入）。";
            }
        }

        private void MarkCurrentUsbRoute(string mode)
        {
            if (!input.MarkCurrentUsbRoute(mode))
            {
                if (footerStatus != null) footerStatus.Text = "当前没有可标记的 USB 手柄路径；请先连接手柄并操作任意按键。";
                return;
            }
            connectionMethodOverride = "自动";
            UpdateConnectionMethod(currentState);
            if (!demoMode) SaveSettings();
            if (footerStatus != null) footerStatus.Text = "当前 USB 状态已设为“" + mode + "”。此手柄的有线与接收器可复用同一 Windows 路径，切换后请在此处同步一次状态。";
        }

        private UIElement BuildTriggerCard(string name, TriggerChart chart, Brush accent, bool left)
        {
            Grid grid = new Grid { Margin = new Thickness(18, 12, 16, 10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(31) });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            TextBlock title = new TextBlock { Text = name, FontSize = 16, Foreground = accent, FontWeight = FontWeights.SemiBold };
            if (left) leftTriggerTitle = title;
            else rightTriggerTitle = title;
            StackPanel stats = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            stats.Children.Add(new TextBlock { Text = "当前", Foreground = Palette.MutedBrush, FontSize = 10, Margin = new Thickness(0, 7, 5, 0) });
            TextBlock percent = new TextBlock { Text = "0%", FontSize = 22, Foreground = accent, FontWeight = FontWeights.SemiBold };
            stats.Children.Add(percent);
            stats.Children.Add(new Border { Width = 1, Height = 18, Background = Palette.BorderBrush, Margin = new Thickness(9, 5, 9, 0) });
            stats.Children.Add(new TextBlock { Text = "峰值", Foreground = Palette.MutedBrush, FontSize = 10, Margin = new Thickness(0, 7, 5, 0) });
            TextBlock peak = new TextBlock { Text = "0%", FontSize = 15, Foreground = Palette.TextBrush, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) };
            stats.Children.Add(peak);
            chart.PercentText = percent;
            chart.PeakText = peak;
            chart.Label = name;
            AutomationProperties.SetName(chart, name + " 近 5 秒历史曲线");
            grid.Children.Add(title);
            grid.Children.Add(stats);
            Grid.SetRow(chart, 1);
            grid.Children.Add(chart);
            TextBlock detail = new TextBlock { FontSize = 10.5, Foreground = Palette.MutedBrush, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            chart.DetailText = detail;
            Grid.SetRow(detail, 2);
            grid.Children.Add(detail);
            return grid;
        }

        private Grid BuildRightColumn()
        {
            Grid right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(134) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(134) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(124) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(218) });
            right.VerticalAlignment = VerticalAlignment.Top;

            right.Children.Add(new TextBlock { Text = "实时诊断", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            Border leftStick = BuildRealtimeStickCard(true);
            Grid.SetRow(leftStick, 2);
            right.Children.Add(leftStick);
            Border rightStick = BuildRealtimeStickCard(false);
            Grid.SetRow(rightStick, 4);
            right.Children.Add(rightStick);
            Border triggers = BuildRealtimeTriggerCard();
            Grid.SetRow(triggers, 6);
            right.Children.Add(triggers);
            Border health = BuildRealtimeHealthCard();
            Grid.SetRow(health, 8);
            right.Children.Add(health);
            return right;
        }

        private Border BuildRealtimeStickCard(bool left)
        {
            Grid card = new Grid { Margin = new Thickness(18, 15, 18, 14) };
            card.ColumnDefinitions.Add(new ColumnDefinition());
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            card.Children.Add(new TextBlock { Text = left ? "左摇杆" : "右摇杆", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            TextBlock status = new TextBlock { Text = "等待输入", Foreground = Palette.MutedBrush, FontSize = 12, FontWeight = FontWeights.SemiBold };
            Border badge = LabVisualStyles.CreateStatusBadge(status);
            Grid.SetColumn(badge, 1);
            card.Children.Add(badge);

            TextBlock metric = new TextBlock { Text = "0.0%", Foreground = Palette.BlueBrush, FontSize = 32, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(metric, 1);
            card.Children.Add(metric);
            TextBlock context = LabVisualStyles.CreateSecondaryText("X 0.000 · Y 0.000");
            context.Margin = new Thickness(0, 2, 0, 0);
            Grid.SetRow(context, 2);
            card.Children.Add(context);

            TextBlock advice = LabVisualStyles.CreateSecondaryText("轻推摇杆可查看实时位置");
            advice.TextAlignment = TextAlignment.Right;
            advice.VerticalAlignment = VerticalAlignment.Bottom;
            advice.TextWrapping = TextWrapping.Wrap;
            advice.MaxWidth = 142;
            Grid.SetColumn(advice, 1);
            Grid.SetRow(advice, 1);
            Grid.SetRowSpan(advice, 2);
            card.Children.Add(advice);

            if (left)
            {
                leftDriftX = metric;
                leftDriftY = context;
                leftStickStatusText = status;
                leftStickAdviceText = advice;
            }
            else
            {
                rightDriftX = metric;
                rightDriftY = context;
                rightStickStatusText = status;
                rightStickAdviceText = advice;
            }
            return LabVisualStyles.CreateMetricCard(card);
        }

        private Border BuildRealtimeTriggerCard()
        {
            Grid card = new Grid { Margin = new Thickness(18, 14, 18, 14) };
            card.ColumnDefinitions.Add(new ColumnDefinition());
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            card.ColumnDefinitions.Add(new ColumnDefinition());
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            card.Children.Add(new TextBlock { Text = "扳机", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            triggerStatusText = new TextBlock { Text = "等待输入", Foreground = Palette.MutedBrush, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(triggerStatusText, 2);
            card.Children.Add(triggerStatusText);
            Border divider = new Border { Background = Palette.BorderSubtleBrush, Margin = new Thickness(12, 4, 12, 3) };
            Grid.SetColumn(divider, 1);
            Grid.SetRowSpan(divider, 3);
            card.Children.Add(divider);

            StackPanel left = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            leftRealtimeTriggerLabel = new TextBlock { Text = "LT", Foreground = Palette.MutedBrush, FontSize = 12 };
            left.Children.Add(leftRealtimeTriggerLabel);
            leftTriggerCurrentText = new TextBlock { Text = "0%", Foreground = Palette.BlueBrush, FontSize = 28, FontWeight = FontWeights.SemiBold };
            left.Children.Add(leftTriggerCurrentText);
            Grid.SetRow(left, 1);
            card.Children.Add(left);
            StackPanel right = new StackPanel { Margin = new Thickness(18, 8, 0, 0) };
            rightRealtimeTriggerLabel = new TextBlock { Text = "RT", Foreground = Palette.MutedBrush, FontSize = 12 };
            right.Children.Add(rightRealtimeTriggerLabel);
            rightTriggerCurrentText = new TextBlock { Text = "0%", Foreground = Palette.BlueBrush, FontSize = 28, FontWeight = FontWeights.SemiBold };
            right.Children.Add(rightTriggerCurrentText);
            Grid.SetColumn(right, 2);
            Grid.SetRow(right, 1);
            card.Children.Add(right);
            return LabVisualStyles.CreateMetricCard(card);
        }

        private Border BuildRealtimeHealthCard()
        {
            Grid card = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.Children.Add(new TextBlock { Text = "综合健康", Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold });
            diagnosticScoreText = new TextBlock { Text = demoMode ? "评估中" : "等待手柄", Foreground = Palette.MutedBrush, FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0) };
            Grid.SetRow(diagnosticScoreText, 1);
            card.Children.Add(diagnosticScoreText);
            diagnosticDetailText = LabVisualStyles.CreateSecondaryText("连接后会给出简短的健康建议。");
            diagnosticDetailText.TextWrapping = TextWrapping.Wrap;
            diagnosticDetailText.Margin = new Thickness(0, 3, 0, 0);
            Grid.SetRow(diagnosticDetailText, 2);
            card.Children.Add(diagnosticDetailText);

            Grid preferences = new Grid { Margin = new Thickness(0, 9, 0, 0) };
            preferences.ColumnDefinitions.Add(new ColumnDefinition());
            preferences.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(98) });
            reducedMotionCheck = new CheckBox
            {
                Content = "减少动态效果",
                IsChecked = reducedMotion,
                IsEnabled = !demoMode,
                Foreground = Palette.MutedBrush,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            reducedMotionCheck.Checked += OnReducedMotionChanged;
            reducedMotionCheck.Unchecked += OnReducedMotionChanged;
            preferences.Children.Add(reducedMotionCheck);
            calibrationProgress = new ProgressBar
            {
                Height = 3,
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                Foreground = Palette.BlueBrush,
                Background = Palette.SurfaceHoverBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(calibrationProgress, 1);
            preferences.Children.Add(calibrationProgress);
            Grid.SetRow(preferences, 3);
            card.Children.Add(preferences);

            Grid actions = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            guidedLaunchButton = MakeButton("快速体检", true);
            guidedLaunchButton.Click += delegate { BeginGuidedTest(); };
            actions.Children.Add(guidedLaunchButton);
            calibrateButton = MakeButton(demoMode ? "演示中" : "中心校准", false);
            calibrateButton.IsEnabled = !demoMode;
            calibrateButton.Click += StartCalibration;
            Grid.SetColumn(calibrateButton, 2);
            actions.Children.Add(calibrateButton);
            Button inputTest = MakeButton("按键", false);
            inputTest.ToolTip = "打开独立按键检测";
            inputTest.Click += delegate { ShowPage(2); };
            Grid.SetColumn(inputTest, 4);
            actions.Children.Add(inputTest);
            Button more = realtimeAdvancedButton = MakeButton("高级", false);
            more.MinWidth = 58;
            ContextMenu moreMenu = CreateDarkContextMenu(174);
            pauseHistoryMenuItem = MakeDarkMenuItem("暂停扳机曲线");
            pauseHistoryMenuItem.Click += delegate { ToggleHistoryPause(); };
            MenuItem clearHistory = MakeDarkMenuItem("清空扳机曲线");
            clearHistory.Click += delegate { ClearTriggerHistory(); };
            MenuItem export = MakeDarkMenuItem("导出报告");
            export.Click += delegate { ExportCurrentReport(); };
            MenuItem exportTriggers = MakeDarkMenuItem("导出 LT / RT 曲线");
            exportTriggers.Click += delegate { ExportTriggerHistory(); };
            MenuItem xboxCalibration = MakeDarkMenuItem("Xbox Controller Calibration");
            xboxCalibration.Click += delegate { OpenXboxCalibration(); };
            MenuItem xboxFaceCalibration = MakeDarkMenuItem("校准 Xbox A/B/X/Y");
            xboxFaceCalibration.Click += delegate { OpenXboxFaceButtonCalibration(); };
            MenuItem ds5Calibration = MakeDarkMenuItem("DS5 轮廓校准");
            ds5Calibration.Click += delegate { OpenDualSenseCalibration(); };
            MenuItem ds5TouchDebug = MakeDarkMenuItem("DS5 触摸调试");
            ds5TouchDebug.Click += delegate { OpenDualSenseTouchDebug(); };
            MenuItem resetAll = MakeDarkMenuItem("恢复默认设置");
            resetAll.Foreground = Palette.WarningBrush;
            resetAll.Click += delegate { ResetAllSettings(); };
            moreMenu.Items.Add(pauseHistoryMenuItem);
            moreMenu.Items.Add(clearHistory);
            moreMenu.Items.Add(exportTriggers);
            moreMenu.Items.Add(export);
            moreMenu.Items.Add(MakeDarkMenuSeparator());
            moreMenu.Items.Add(xboxCalibration);
            moreMenu.Items.Add(xboxFaceCalibration);
            moreMenu.Items.Add(ds5Calibration);
            moreMenu.Items.Add(ds5TouchDebug);
            moreMenu.Items.Add(resetAll);
            more.ContextMenu = moreMenu;
            more.Click += delegate { OpenContextMenu(more); };
            Grid.SetColumn(more, 6);
            actions.Children.Add(more);
            Grid.SetRow(actions, 4);
            card.Children.Add(actions);
            return LabVisualStyles.CreateSectionCard(card);
        }

        private UIElement BuildStickSection(bool left)
        {
            Color accentColor = Palette.Blue;
            Brush accent = Palette.BlueBrush;
            StickPlot plot = left ? leftPlot : rightPlot;
            DeadzoneSlider slider = left ? leftDeadzone : rightDeadzone;
            AutomationProperties.SetName(slider, left ? "左摇杆显示参考死区" : "右摇杆显示参考死区");
            AutomationProperties.SetHelpText(slider, "使用左右方向键在 0% 到 25% 之间调整；仅影响诊断参考线。 ");
            slider.ToolTip = left ? "左摇杆显示参考死区（用户手动值，只用于诊断显示）" : "右摇杆显示参考死区（用户手动值，只用于诊断显示）";

            Grid section = new Grid { Margin = new Thickness(20, 16, 18, 14) };
            section.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) });
            section.RowDefinitions.Add(new RowDefinition());
            TextBlock heading = new TextBlock { Text = left ? "左摇杆" : "右摇杆", Foreground = accent, FontSize = 15, FontWeight = FontWeights.SemiBold };
            section.Children.Add(heading);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(156) });
            plot.Margin = new Thickness(0, 0, 12, 0);
            body.Children.Add(plot);

            FontFamily metricFont = new FontFamily("Consolas");
            StackPanel info = new StackPanel { Margin = new Thickness(8, 1, 0, 0) };
            info.Children.Add(new TextBlock { Text = "实时位置", Foreground = Palette.TextBrush, FontSize = 12.5, FontWeight = FontWeights.SemiBold });
            Grid drift = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            drift.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            drift.ColumnDefinitions.Add(new ColumnDefinition());
            drift.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            drift.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            drift.Children.Add(new TextBlock { Text = "X", Foreground = Palette.MutedBrush, FontSize = 11.5, FontWeight = FontWeights.Medium });
            TextBlock dx = new TextBlock { Text = "0.000", Foreground = accent, FontFamily = metricFont, FontSize = 12.5, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(dx, 1);
            drift.Children.Add(dx);
            TextBlock yl = new TextBlock { Text = "Y", Foreground = Palette.MutedBrush, FontSize = 11.5, FontWeight = FontWeights.Medium };
            Grid.SetRow(yl, 1);
            drift.Children.Add(yl);
            TextBlock dy = new TextBlock { Text = "0.000", Foreground = accent, FontFamily = metricFont, FontSize = 12.5, FontWeight = FontWeights.SemiBold };
            Grid.SetRow(dy, 1);
            Grid.SetColumn(dy, 1);
            drift.Children.Add(dy);
            info.Children.Add(drift);
            info.Children.Add(new TextBlock { Text = "显示参考死区", Foreground = Palette.TextBrush, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
            TextBlock dz = new TextBlock { Text = string.Format(CultureInfo.InvariantCulture, "{0:0}%", slider.Value * 100.0), Foreground = accent, FontFamily = metricFont, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0) };
            info.Children.Add(dz);
            slider.Height = 18;
            slider.Margin = new Thickness(0, 1, 0, 0);
            info.Children.Add(slider);
            Grid limits = new Grid();
            limits.ColumnDefinitions.Add(new ColumnDefinition());
            limits.ColumnDefinitions.Add(new ColumnDefinition());
            limits.Children.Add(new TextBlock { Text = "0%", Foreground = Palette.MutedBrush, FontFamily = metricFont, FontSize = 10.5 });
            TextBlock max = new TextBlock { Text = "25%", Foreground = Palette.MutedBrush, FontFamily = metricFont, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(max, 1);
            limits.Children.Add(max);
            info.Children.Add(limits);
            Button reset = MakeButton("重置参考线", false);
            reset.Margin = new Thickness(0, 3, 0, 0);
            reset.MinWidth = 132;
            reset.Height = 32;
            reset.Padding = new Thickness(8, 3, 8, 3);
            reset.FontSize = 11.5;
            reset.FontWeight = FontWeights.SemiBold;
            reset.ToolTip = "将用户显示参考死区恢复为 8%";
            reset.Click += delegate { slider.Value = 0.08; ClearStickTestVisualState(); };
            info.Children.Add(reset);
            Grid.SetColumn(info, 1);
            body.Children.Add(info);
            Grid.SetRow(body, 1);
            section.Children.Add(body);

            if (left)
            {
                leftDriftX = dx;
                leftDriftY = dy;
                leftDeadzoneText = dz;
            }
            else
            {
                rightDriftX = dx;
                rightDriftY = dy;
                rightDeadzoneText = dz;
            }
            return section;
        }

        private UIElement BuildCalibrationControls()
        {
            Grid grid = new Grid { Margin = new Thickness(16, 10, 16, 10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(7) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(31) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(43) });

            StackPanel diagnosis = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            diagnosticScoreText = new TextBlock { Text = demoMode ? "基础健康 · 评估中" : "基础健康 · 等待手柄", Foreground = Palette.MutedBrush, FontSize = 14, FontWeight = FontWeights.SemiBold };
            diagnosticDetailText = new TextBlock { Text = "连接后建立中心基线并测量实际采样率", Foreground = Palette.MutedBrush, FontSize = 10, Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            diagnosticScoreText.ToolTip = "基础健康分只评估连接、采样率和摇杆中心稳定性；完整按键与行程请运行自动体检。";
            AutomationProperties.SetHelpText(diagnosticScoreText, "基础健康分评估连接、采样率和摇杆中心稳定性。完整按键与行程请运行自动体检。 ");
            AutomationProperties.SetLiveSetting(diagnosticScoreText, AutomationLiveSetting.Polite);
            diagnosis.Children.Add(diagnosticScoreText);
            diagnosis.Children.Add(diagnosticDetailText);
            calibrationProgress = new ProgressBar
            {
                Height = 3,
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                Foreground = Palette.BlueBrush,
                Background = new SolidColorBrush(Color.FromRgb(35, 49, 60)),
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed
            };
            diagnosis.Children.Add(calibrationProgress);
            grid.Children.Add(diagnosis);

            Grid settings = new Grid();
            settings.ColumnDefinitions.Add(new ColumnDefinition());
            settings.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            reducedMotionCheck = new CheckBox
            {
                Content = "减少动态效果",
                IsChecked = reducedMotion,
                IsEnabled = !demoMode,
                Foreground = Palette.TextBrush,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            reducedMotionCheck.Checked += OnReducedMotionChanged;
            reducedMotionCheck.Unchecked += OnReducedMotionChanged;
            AutomationProperties.SetName(reducedMotionCheck, "减少动态效果");
            settings.Children.Add(reducedMotionCheck);

            controllerSelectorButton = MakeButton(demoMode ? "设备：演示" : ControllerSelectionLabel(), false);
            controllerSelectorButton.Width = 130;
            controllerSelectorButton.Height = 31;
            controllerSelectorButton.FontSize = 11;
            controllerSelectorButton.IsEnabled = !demoMode;
            controllerSelectorButton.ToolTip = "自动选择第一只已连接手柄，或固定监测玩家 1 到玩家 4";
            AutomationProperties.SetName(controllerSelectorButton, "选择监测手柄");
            ContextMenu deviceMenu = CreateDarkContextMenu(150);
            string[] controllerNames = { "自动选择", "玩家 1", "玩家 2", "玩家 3", "玩家 4" };
            for (int i = 0; i < controllerNames.Length; i++)
            {
                int controllerIndex = i - 1;
                MenuItem item = MakeDarkMenuItem(controllerNames[i]);
                item.IsCheckable = true;
                item.IsChecked = selectedControllerIndex == controllerIndex;
                item.Click += delegate { SelectController(controllerIndex); };
                controllerMenuItems[i] = item;
                deviceMenu.Items.Add(item);
            }
            controllerSelectorButton.ContextMenu = deviceMenu;
            controllerSelectorButton.Click += delegate
            {
                OpenContextMenu(controllerSelectorButton);
            };
            Grid.SetColumn(controllerSelectorButton, 1);
            settings.Children.Add(controllerSelectorButton);
            Grid.SetRow(settings, 2);
            grid.Children.Add(settings);

            Grid buttons = new Grid();
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition());

            guidedLaunchButton = MakeButton("自动体检", true);
            guidedLaunchButton.FontSize = 12;
            guidedLaunchButton.Click += delegate { BeginGuidedTest(); };
            buttons.Children.Add(guidedLaunchButton);

            calibrateButton = MakeButton(demoMode ? "演示中" : "中心校准", false);
            calibrateButton.FontSize = 12;
            calibrateButton.IsEnabled = !demoMode;
            calibrateButton.Click += StartCalibration;
            Grid.SetColumn(calibrateButton, 2);
            buttons.Children.Add(calibrateButton);

            Button export = MakeButton("导出报告", false);
            export.FontSize = 12;
            export.Click += delegate { ExportCurrentReport(); };
            Grid.SetColumn(export, 4);
            buttons.Children.Add(export);

            Button more = MakeButton("更多 ···", false);
            more.FontSize = 12;
            ContextMenu moreMenu = CreateDarkContextMenu(166);
            pauseHistoryMenuItem = MakeDarkMenuItem("暂停扳机曲线");
            pauseHistoryMenuItem.Click += delegate { ToggleHistoryPause(); };
            MenuItem clearHistory = MakeDarkMenuItem("清空扳机曲线");
            clearHistory.Click += delegate { ClearTriggerHistory(); };
            MenuItem xboxCalibration = MakeDarkMenuItem("Xbox Controller Calibration");
            xboxCalibration.Click += delegate { OpenXboxCalibration(); };
            MenuItem xboxFaceCalibration = MakeDarkMenuItem("校准 Xbox A/B/X/Y");
            xboxFaceCalibration.Click += delegate { OpenXboxFaceButtonCalibration(); };
            MenuItem resetAll = MakeDarkMenuItem("恢复默认设置");
            resetAll.Foreground = Palette.WarningBrush;
            resetAll.Click += delegate { ResetAllSettings(); };
            MenuItem ds5Calibration = MakeDarkMenuItem("DS5 轮廓校准");
            ds5Calibration.Click += delegate { OpenDualSenseCalibration(); };
            MenuItem ds5TouchDebug = MakeDarkMenuItem("DS5 触摸调试");
            ds5TouchDebug.Click += delegate { OpenDualSenseTouchDebug(); };
            moreMenu.Items.Add(pauseHistoryMenuItem);
            moreMenu.Items.Add(clearHistory);
            moreMenu.Items.Add(MakeDarkMenuSeparator());
            moreMenu.Items.Add(xboxCalibration);
            moreMenu.Items.Add(xboxFaceCalibration);
            moreMenu.Items.Add(ds5Calibration);
            moreMenu.Items.Add(ds5TouchDebug);
            moreMenu.Items.Add(resetAll);
            more.ContextMenu = moreMenu;
            more.Click += delegate
            {
                OpenContextMenu(more);
            };
            Grid.SetColumn(more, 6);
            buttons.Children.Add(more);
            Grid.SetRow(buttons, 4);
            grid.Children.Add(buttons);
            return grid;
        }

        private void OpenDualSenseCalibration()
        {
            try
            {
                DualSenseCalibrationWindow window = new DualSenseCalibrationWindow(dualSenseVisual.Regions, dualSenseVisual.ControllerPhoto);
                window.Owner = this;
                bool? result = window.ShowDialog();
                dualSenseVisual.InvalidateVisual();
                if (footerStatus != null) footerStatus.Text = window.StatusMessage ?? (result == true ? "DS5 Geometry 校准已保存。" : "已关闭 DS5 Geometry 校准。");
            }
            catch (Exception ex)
            {
                if (footerStatus != null) footerStatus.Text = "无法打开 DS5 轮廓校准：" + ex.Message;
            }
        }

        private void OpenXboxCalibration()
        {
            try
            {
                XboxCalibrationWindow window = new XboxCalibrationWindow(controllerVisual.Regions, controllerVisual.ControllerPhoto);
                window.Owner = this;
                window.ShowDialog();
                controllerVisual.InvalidateVisual();
                if (footerStatus != null) footerStatus.Text = window.StatusMessage ?? "已关闭 Xbox Controller Calibration。";
            }
            catch (Exception ex)
            {
                if (footerStatus != null) footerStatus.Text = "无法打开 Xbox Controller Calibration：" + ex.Message;
            }
        }

        private void OpenXboxFaceButtonCalibration()
        {
            try
            {
                XboxCalibrationWindow window = new XboxCalibrationWindow(
                    controllerVisual.Regions, controllerVisual.ControllerPhoto,
                    new[] { "a", "b", "x", "y" }, "Xbox A/B/X/Y 手动校准");
                window.Owner = this;
                window.ShowDialog();
                controllerVisual.InvalidateVisual();
                if (footerStatus != null) footerStatus.Text = window.StatusMessage ?? "已关闭 Xbox A/B/X/Y 手动校准。";
            }
            catch (Exception ex)
            {
                if (footerStatus != null) footerStatus.Text = "无法打开 Xbox A/B/X/Y 手动校准：" + ex.Message;
            }
        }

        private void OpenXboxDPadUpCalibration()
        {
            try
            {
                XboxDPadCalibrationWindow window = new XboxDPadCalibrationWindow(controllerVisual.Regions, controllerVisual.ControllerPhoto, "dpad-up");
                window.Owner = this;
                window.ShowDialog();
                controllerVisual.InvalidateVisual();
                if (footerStatus != null) footerStatus.Text = window.StatusMessage ?? "已关闭 Xbox DPadUp 精密校准。";
            }
            catch (Exception ex)
            {
                if (footerStatus != null) footerStatus.Text = "无法打开 Xbox DPadUp 精密校准：" + ex.Message;
            }
        }

        private void OpenDualSenseTouchDebug()
        {
            DualSenseTouchDebugWindow window = new DualSenseTouchDebugWindow(
                delegate { return currentState; },
                delegate(DualSenseTouchPoint point) { return dualSenseVisual.Regions.MapTouchPoint(point); },
                delegate(bool enabled) { sonyInput.EnableRawTouchLogging = enabled; },
                delegate { return sonyInput.EnableRawTouchLogging; });
            window.Owner = this;
            window.Show();
        }

        private string ControllerSelectionLabel()
        {
            return selectedControllerIndex < 0 ? "设备：自动" : string.Format(CultureInfo.InvariantCulture, "设备：玩家 {0}", selectedControllerIndex + 1);
        }

        private void SelectController(int index)
        {
            if (demoMode) return;
            selectedControllerIndex = Math.Max(-1, Math.Min(3, index));
            if (controllerSelectorButton != null) controllerSelectorButton.Content = ControllerSelectionLabel();
            for (int i = 0; i < controllerMenuItems.Length; i++)
            {
                if (controllerMenuItems[i] != null) controllerMenuItems[i].IsChecked = i - 1 == selectedControllerIndex;
            }
            diagnostics.Reset();
            latestInput = new InputSnapshot { Index = Math.Max(0, selectedControllerIndex) };
            if (footerStatus != null)
            {
                footerStatus.Text = selectedControllerIndex < 0
                    ? "已启用自动选择：监测第一只已连接的 Xbox 手柄。"
                    : string.Format(CultureInfo.InvariantCulture, "已固定监测玩家 {0}。", selectedControllerIndex + 1);
            }
            SaveSettings();
        }

        private void OnReducedMotionChanged(object sender, RoutedEventArgs e)
        {
            reducedMotion = reducedMotionCheck != null && reducedMotionCheck.IsChecked == true;
            ApplyReducedMotion();
            if (footerStatus != null) footerStatus.Text = reducedMotion ? "已减少动态效果：关闭拖尾、发光扩散与弹性过渡。" : "已恢复标准动态反馈。";
            if (!demoMode) SaveSettings();
        }

        private void ApplyReducedMotion()
        {
            controllerVisual.ReducedMotion = reducedMotion;
            dualSenseVisual.ReducedMotion = reducedMotion;
            leftPlot.ReducedMotion = reducedMotion;
            rightPlot.ReducedMotion = reducedMotion;
            leftTriggerChart.ReducedMotion = reducedMotion;
            rightTriggerChart.ReducedMotion = reducedMotion;
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

        private void OnTick(object sender, EventArgs e)
        {
            if (demoMode)
            {
                latestControllerStates = multiDemoMode
                    ? CreateMultiDemoStates()
                    : new ControllerState[] { ControllerStateAdapter.FromSnapshot(CreateCurrentDemoSnapshot()) };
                // Dynamic demo never supplies a MotionSample. Synchronizing here clears
                // any prior real-device pose instead of carrying it into demonstration mode.
                motionManager.Synchronize(latestControllerStates);
                dualSenseAdvancedManager.Synchronize(latestControllerStates, motionManager);
            }
            // This is the only point where the WPF-bound device collection changes.
            // Sampling continues on its background thread and never touches the UI.
            deviceManager.Synchronize(latestControllerStates);
            ControllerState selected = ResolveActiveControllerState();
            currentControllerState = selected;
            rumbleController.Synchronize(selected);
            InputSnapshot raw = selected.ToInputSnapshot();
            if (demoMode)
            {
                DateTime demoTimestamp = DateTime.UtcNow;
                leftTriggerTelemetry.Record(raw.LeftTrigger / 255.0, demoTimestamp);
                rightTriggerTelemetry.Record(raw.RightTrigger / 255.0, demoTimestamp);
            }
            if (controllerFamilySelectorButton != null)
            {
                string label = DeviceSelectionLabel();
                if (!string.Equals(controllerFamilySelectorButton.Content as string, label, StringComparison.Ordinal)) controllerFamilySelectorButton.Content = label;
            }
            if (calibrating)
            {
                double elapsed = (DateTime.UtcNow - calibrationStarted).TotalSeconds;
                calibrationProgress.Value = Math.Min(1.0, elapsed / 2.0);
                calibrateButton.Content = string.Format(CultureInfo.InvariantCulture, "校准中 {0:0.0} 秒", Math.Min(2.0, elapsed));
                SetTextIfChanged(diagnosticScoreText, string.Format(CultureInfo.InvariantCulture, "中心校准 · {0:0}%", Math.Min(1.0, elapsed / 2.0) * 100.0));
                if (raw.Connected)
                {
                    sumLX += raw.LeftX;
                    sumLY += raw.LeftY;
                    sumRX += raw.RightX;
                    sumRY += raw.RightY;
                    minLX = Math.Min(minLX, raw.LeftX);
                    minLY = Math.Min(minLY, raw.LeftY);
                    minRX = Math.Min(minRX, raw.RightX);
                    minRY = Math.Min(minRY, raw.RightY);
                    maxLX = Math.Max(maxLX, raw.LeftX);
                    maxLY = Math.Max(maxLY, raw.LeftY);
                    maxRX = Math.Max(maxRX, raw.RightX);
                    maxRY = Math.Max(maxRY, raw.RightY);
                    calibrationSamples++;
                }
                if (elapsed >= 2.0) CompleteCalibration();
            }

            if (!calibrating && calibrationStatusVisible && calibrationSuggestionPending && calibrationMessageUntil != DateTime.MinValue && DateTime.UtcNow >= calibrationMessageUntil)
            {
                calibrationStatusVisible = false;
                calibrationMessageUntil = DateTime.MinValue;
            }

            InputSnapshot state = raw.WithOffsets(offsetLX, offsetLY, offsetRX, offsetRY);
            currentState = state;
            UpdateFamilyPresentation(state);
            UpdateRates(state);
            UpdateConnection(state);
            if (state.Connected) UpdateVisuals(state);
            diagnostics.Update(state, demoMode ? 220.0 : actualSamplingHz, leftDeadzone.Value, rightDeadzone.Value);
            UpdateInputTestPage(selected);
            UpdateStickDriftTestPage(selected);
            if (joystickTestPage != null && (currentPage == 3 || joystickTestViewModel.IsTestActive))
                joystickTestPage.Update(selected, demoMode ? 220.0 : actualSamplingHz, rumbleController.IsRunning, rumbleController.LastStoppedUtc);
            UpdateMotionPage(selected);
            UpdateRumblePage(selected);
            if (healthCheckView != null && (currentPage == 6 || healthCheckViewModel.IsRunning))
                healthCheckView.Update(selected);
            if (guidedOverlay != null && guidedOverlay.Visibility == Visibility.Visible)
            {
                guidedTest.Update(state, demoMode ? 220.0 : actualSamplingHz);
                UpdateGuidedUI();
            }
            UpdateDiagnostics(state);
            try { HandleControllerNavigation(state); }
            catch (Exception ex)
            {
                // Navigation is optional. A visual-tree transition must never
                // take down the live monitor or the input sampling thread.
                controllerNavigationEnabled = false;
                ResetControllerNavigationInput();
                ClearControllerNavigationSelection();
                App.RecordUnhandledException("Controller navigation", ex);
                if (footerStatus != null) footerStatus.Text = "手柄导航遇到异常，已安全关闭；请重新开启。详细信息已写入日志。";
            }
        }

        private ControllerState ResolveActiveControllerState()
        {
            ControllerState[] devices = latestControllerStates ?? new ControllerState[0];
            if (!string.IsNullOrEmpty(selectedDeviceId))
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (string.Equals(devices[i].DeviceId, selectedDeviceId, StringComparison.OrdinalIgnoreCase)) return devices[i];
                }
                // A manually selected device disappeared. Fall back immediately to the
                // first online device instead of leaving a stale visual on screen.
                selectedDeviceId = null;
                if (footerStatus != null && devices.Length > 0) footerStatus.Text = productSettings.AutoConnect ? "选中的手柄已断开，已自动切换到其他在线设备。" : "选中的手柄已断开，请在设备选择页重新选择。";
            }
            if (!demoMode && !productSettings.AutoConnect)
            {
                ControllerState waitingForSelection = ControllerStateAdapter.CreateDisconnected();
                waitingForSelection.ControllerType = selectedControllerFamily == ControllerFamily.PlayStation ? ControllerType.DualSense : ControllerType.Xbox;
                waitingForSelection.DeviceName = "请选择设备";
                waitingForSelection.InputBackend = waitingForSelection.ControllerType == ControllerType.DualSense ? "Sony Native HID" : input.LibraryName;
                return waitingForSelection;
            }
            if (devices.Length > 0) return devices[0];
            ControllerState disconnected = ControllerStateAdapter.CreateDisconnected();
            disconnected.ControllerType = selectedControllerFamily == ControllerFamily.PlayStation ? ControllerType.DualSense : ControllerType.Xbox;
            disconnected.DeviceName = disconnected.ControllerType == ControllerType.DualSense ? "索尼 DS 手柄" : "Xbox 手柄";
            disconnected.InputBackend = disconnected.ControllerType == ControllerType.DualSense ? "Sony Native HID" : input.LibraryName;
            return disconnected;
        }

        private InputSnapshot ResolveActiveInput()
        {
            return ResolveActiveControllerState().ToInputSnapshot();
        }

        private void UpdateRates(InputSnapshot state)
        {
            refreshTicks++;
            double seconds = (DateTime.UtcNow - rateWindowStarted).TotalSeconds;
            if (seconds >= 1.0)
            {
                if (refreshRateText != null)
                {
                    actualDisplayHz = refreshTicks / seconds;
                    refreshRateText.Text = string.Format(CultureInfo.InvariantCulture, "显示 {0:0} Hz", actualDisplayHz);
                }
                if (samplingRateText != null)
                {
                    int samples = demoMode ? refreshTicks : Interlocked.Exchange(ref samplingTicks, 0);
                    actualSamplingHz = demoMode ? refreshTicks / seconds : samples / seconds;
                    samplingRateText.Text = demoMode ? "演示" : string.Format(CultureInfo.InvariantCulture, "{0:0} Hz", actualSamplingHz);
                }
                refreshTicks = 0;
                rateWindowStarted = DateTime.UtcNow;
            }
        }

        private void StartSampling()
        {
            if (demoMode || sampling) return;
            sampling = true;
            samplingThread = new Thread(SamplingLoop) { IsBackground = true, Name = "ControllerLab XInput sampler" };
            samplingThread.Start();
        }

        private void StopSampling()
        {
            sampling = false;
            if (samplingThread != null && samplingThread.IsAlive) samplingThread.Join(600);
            samplingThread = null;
        }

        private void SamplingLoop()
        {
            const uint CreateWaitableTimerHighResolution = 0x00000002;
            const uint TimerAllAccess = 0x001F0003;
            IntPtr highResolutionTimer = CreateWaitableTimerEx(IntPtr.Zero, null, CreateWaitableTimerHighResolution, TimerAllAccess);
            bool timerReady = false;
            if (highResolutionTimer != IntPtr.Zero)
            {
                long firstDueTime = -40000;
                timerReady = SetWaitableTimer(highResolutionTimer, ref firstDueTime, 4, IntPtr.Zero, IntPtr.Zero, false);
            }
            timeBeginPeriod(1);
            try
            {
                while (sampling)
                {
                    ControllerState[] states = deviceManager.Scan();
                    latestControllerStates = states;
                    motionManager.Synchronize(states);
                    dualSenseAdvancedManager.Synchronize(states, motionManager);
                    RecordTriggerTelemetry(states);
                    latestInput = states.Length > 0 ? states[0].ToInputSnapshot() : new InputSnapshot();
                    Interlocked.Increment(ref samplingTicks);
                    if (timerReady) WaitForSingleObject(highResolutionTimer, 20);
                    else Thread.Sleep(4);
                }
            }
            finally
            {
                timeEndPeriod(1);
                if (highResolutionTimer != IntPtr.Zero)
                {
                    if (timerReady) CancelWaitableTimer(highResolutionTimer);
                    CloseHandle(highResolutionTimer);
                }
            }
        }

        private void RecordTriggerTelemetry(ControllerState[] states)
        {
            if (states == null || states.Length == 0) return;
            ControllerState selected = null;
            string desired = selectedDeviceId;
            for (int i = 0; i < states.Length; i++)
            {
                ControllerState candidate = states[i];
                if (candidate != null && candidate.IsConnected && (string.IsNullOrEmpty(desired) || string.Equals(candidate.DeviceId, desired, StringComparison.OrdinalIgnoreCase))) { selected = candidate; break; }
            }
            if (selected == null) return;
            DateTime timestamp = selected.TimestampUtc == DateTime.MinValue ? DateTime.UtcNow : selected.TimestampUtc;
            leftTriggerTelemetry.Record(selected.LeftTrigger, timestamp);
            rightTriggerTelemetry.Record(selected.RightTrigger, timestamp);
        }

        private void UpdateConnection(InputSnapshot state)
        {
            if (state.Connected)
            {
                if (!lastConnected)
                {
                    capabilitiesReadyUtc = DateTime.UtcNow.AddMilliseconds(650);
                    if (noticeBanner != null) noticeBanner.Hide();
                    LabLogger.Info("Device", "Controller connected; capability detection started.");
                }
                connectionDot.Fill = Palette.BlueBrush;
                connectionText.Foreground = Palette.BlueBrush;
                string touchStatus = state.Family == ControllerFamily.PlayStation
                    ? (state.TouchCoordinatesAvailable ? " · 触摸坐标可用" : " · 触摸坐标不可用（仅按压）")
                    : string.Empty;
                bool detectingCapabilities = !demoMode && DateTime.UtcNow < capabilitiesReadyUtc;
                string status = demoMode
                    ? "动态演示" + (sonyDemoMode ? " · 触摸坐标不可用（仅按压）" : string.Empty)
                    : detectingCapabilities
                        ? "能力检测中 · " + (state.Family == ControllerFamily.PlayStation ? "原生 HID" : "XInput")
                    : state.Family == ControllerFamily.PlayStation
                        ? "已就绪 · 原生 HID" + touchStatus
                        : string.Format(CultureInfo.InvariantCulture, "已就绪 · 玩家 {0}", state.Index + 1);
                SetTextIfChanged(connectionText, status);
                UpdateConnectionMethod(state);
                SetTextIfChanged(deviceMetaText, string.IsNullOrEmpty(state.InputBackend) ? input.LibraryName : state.InputBackend);
            }
            else
            {
                connectionDot.Fill = Palette.RedBrush;
                connectionText.Foreground = Palette.RedBrush;
                SetTextIfChanged(connectionText, selectedControllerIndex < 0
                    ? (DateTime.UtcNow - applicationStartedUtc).TotalSeconds < 2.0 ? "正在识别手柄" : lastConnected ? "已断开" : "未连接"
                    : string.Format(CultureInfo.InvariantCulture, "玩家 {0} 未连接", selectedControllerIndex + 1));
                UpdateConnectionMethod(state);
                SetTextIfChanged(deviceMetaText, renderedControllerFamily == ControllerFamily.PlayStation ? "Sony 原生 HID" : input.LibraryName);
                if (lastConnected)
                {
                    rumbleController.Stop("设备已断开，震动输出已归零");
                    if (stickDriftTestEngine.IsActive) stickDriftTestEngine.Cancel("设备已断开，检测已取消");
                    if (joystickTestPage != null) joystickTestPage.Cancel("设备已断开，摇杆检测已取消");
                    if (dualSenseAdvancedPage != null) dualSenseAdvancedPage.CancelForPageLeave();
                    footerStatus.Text = "设备已断开。已停止震动和当前检测，最近一次报告仍保留。";
                    if (noticeBanner != null) noticeBanner.Show(ProductNoticeKind.Warning, "设备已断开", "震动与当前检测已安全停止；重新连接后可从当前项目重新开始。", "设备选择", delegate { ShowPage(0); });
                    LabLogger.Warning("Device", "Active controller disconnected; output and tests stopped.");
                }
                UpdateRealtimeStickCard(0, false, leftStickStatusText, leftStickAdviceText);
                UpdateRealtimeStickCard(0, false, rightStickStatusText, rightStickAdviceText);
                if (triggerStatusText != null) { triggerStatusText.Text = "未连接"; triggerStatusText.Foreground = Palette.RedBrush; }
            }
            if (controllerVisualHost != null) controllerVisualHost.Opacity = state.Connected ? 1.0 : 0.38;
            lastConnected = state.Connected;
        }

        private void UpdateConnectionMethod(InputSnapshot state)
        {
            if (connectionMethodText == null || connectionMethodDot == null) return;
            Color color = Palette.Muted;
            string text = "未连接";
            if (!state.Connected)
            {
                text = "未连接";
            }
            else if (demoMode)
            {
                color = Palette.Blue;
                text = "动态演示";
            }
            else
            {
                bool manualOverride = connectionMethodOverride != "自动";
                text = manualOverride ? connectionMethodOverride : state.ConnectionMethod;
                color = text == "蓝牙" ? Palette.Blue : text.StartsWith("USB 2.4G", StringComparison.Ordinal) ? Palette.Blue : text.StartsWith("USB 通道", StringComparison.Ordinal) ? Palette.Muted : Palette.Text;
            }
            Brush brush = new SolidColorBrush(color);
            connectionMethodDot.Fill = brush;
            connectionMethodText.Foreground = brush;
            connectionMethodText.ToolTip = connectionMethodOverride == "自动"
                ? "自动识别：蓝牙依据当前 Raw Input 的设备父链。此手柄的 USB 有线与接收器可能复用同一 Windows 路径，无法自动区分；点击可同步当前 USB 状态。"
                : "当前为手动显示“" + connectionMethodOverride + "”。点击可恢复自动识别。";
            SetTextIfChanged(connectionMethodText, text);
            UpdateDeviceCardResponsiveLayout();
        }

        private void UpdateVisuals(InputSnapshot state)
        {
            if (renderedControllerFamily == ControllerFamily.PlayStation) dualSenseVisual.UpdateState(state);
            else controllerVisual.UpdateState(state);
            leftPlot.UpdateValue(state.LeftNormalizedX, state.LeftNormalizedY);
            rightPlot.UpdateValue(state.RightNormalizedX, state.RightNormalizedY);
            leftPlot.Deadzone = leftDeadzone.Value;
            rightPlot.Deadzone = rightDeadzone.Value;
            leftTriggerChart.Value = state.LeftTrigger / 255.0;
            rightTriggerChart.Value = state.RightTrigger / 255.0;
            double leftMagnitude = Math.Min(1.0, Math.Sqrt(state.LeftNormalizedX * state.LeftNormalizedX + state.LeftNormalizedY * state.LeftNormalizedY));
            double rightMagnitude = Math.Min(1.0, Math.Sqrt(state.RightNormalizedX * state.RightNormalizedX + state.RightNormalizedY * state.RightNormalizedY));
            SetTextIfChanged(leftDriftX, string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", leftMagnitude * 100.0));
            string coordinateFormat = "0." + new string('0', Math.Max(1, Math.Min(3, productSettings.DecimalPlaces)));
            SetTextIfChanged(leftDriftY, "X " + state.LeftNormalizedX.ToString(coordinateFormat, CultureInfo.InvariantCulture) + " · Y " + state.LeftNormalizedY.ToString(coordinateFormat, CultureInfo.InvariantCulture));
            SetTextIfChanged(rightDriftX, string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", rightMagnitude * 100.0));
            SetTextIfChanged(rightDriftY, "X " + state.RightNormalizedX.ToString(coordinateFormat, CultureInfo.InvariantCulture) + " · Y " + state.RightNormalizedY.ToString(coordinateFormat, CultureInfo.InvariantCulture));
            UpdateRealtimeStickCard(leftMagnitude, state.Connected, leftStickStatusText, leftStickAdviceText);
            UpdateRealtimeStickCard(rightMagnitude, state.Connected, rightStickStatusText, rightStickAdviceText);

            double leftTrigger = state.LeftTrigger / 255.0;
            double rightTrigger = state.RightTrigger / 255.0;
            SetTextIfChanged(leftTriggerCurrentText, string.Format(CultureInfo.InvariantCulture, "{0:0}%", leftTrigger * 100.0));
            SetTextIfChanged(rightTriggerCurrentText, string.Format(CultureInfo.InvariantCulture, "{0:0}%", rightTrigger * 100.0));
            if (triggerStatusText != null)
            {
                if (!state.Connected)
                {
                    triggerStatusText.Text = "未连接";
                    triggerStatusText.Foreground = Palette.RedBrush;
                }
                else if (leftTrigger > 0.03 || rightTrigger > 0.03)
                {
                    triggerStatusText.Text = "正在输入";
                    triggerStatusText.Foreground = Palette.BlueBrush;
                }
                else
                {
                    triggerStatusText.Text = "已回零";
                    triggerStatusText.Foreground = Palette.GreenBrush;
                }
            }
        }

        private static void UpdateRealtimeStickCard(double magnitude, bool connected, TextBlock status, TextBlock advice)
        {
            if (status == null || advice == null) return;
            if (!connected)
            {
                status.Text = "未连接";
                status.Foreground = Palette.RedBrush;
                advice.Text = "连接手柄后开始监测";
            }
            else if (magnitude > 0.025)
            {
                status.Text = "正在输入";
                status.Foreground = Palette.BlueBrush;
                advice.Text = "实时位置已更新";
            }
            else
            {
                status.Text = "稳定";
                status.Foreground = Palette.GreenBrush;
                advice.Text = "运行检测可确认漂移";
            }
        }

        private void UpdateFamilyPresentation(InputSnapshot state)
        {
            ControllerFamily family = state.Family == ControllerFamily.PlayStation ? ControllerFamily.PlayStation : ControllerFamily.Xbox;
            // A connected entry owns its visual family. The old family preference is
            // retained only as an offline/demo fallback and can no longer override a
            // selected device from the unified catalog.
            if (!state.Connected && selectedControllerFamily == ControllerFamily.PlayStation) family = ControllerFamily.PlayStation;
            if (!state.Connected && selectedControllerFamily == ControllerFamily.Xbox) family = ControllerFamily.Xbox;
            bool changed = family != renderedControllerFamily;
            renderedControllerFamily = family;
            if (controllerVisual != null) controllerVisual.Visibility = family == ControllerFamily.Xbox ? Visibility.Visible : Visibility.Collapsed;
            if (dualSenseVisual != null) dualSenseVisual.Visibility = family == ControllerFamily.PlayStation ? Visibility.Visible : Visibility.Collapsed;
            if (motionPageButton != null)
            {
                bool showDualSense = family == ControllerFamily.PlayStation;
                motionPageButton.Visibility = showDualSense ? Visibility.Visible : Visibility.Collapsed;
                if (!showDualSense && state.Connected && currentPage == 4) ShowPage(1);
            }
            if (deviceNameText != null) SetTextIfChanged(deviceNameText, state.Connected ? state.DeviceName : (family == ControllerFamily.PlayStation ? "索尼 DS 手柄实验室" : "Xbox 手柄实验室"));
            if (deviceLogoText != null)
            {
                deviceLogoText.Text = family == ControllerFamily.PlayStation ? "PS" : "X";
                deviceLogoText.FontSize = family == ControllerFamily.PlayStation ? 16 : 26;
                deviceLogoText.FontWeight = family == ControllerFamily.PlayStation ? FontWeights.SemiBold : FontWeights.Light;
            }
            if (changed)
            {
                Title = family == ControllerFamily.PlayStation ? "手柄实验室 · 索尼 DS" : "手柄实验室 · Xbox";
                diagnostics.Reset();
                if (demoMode) diagnostics.UseDemoBaseline();
                leftTriggerChart.Label = family == ControllerFamily.PlayStation ? "L2" : "LT";
                rightTriggerChart.Label = family == ControllerFamily.PlayStation ? "R2" : "RT";
                SetTextIfChanged(leftTriggerTitle, family == ControllerFamily.PlayStation ? "L2" : "LT");
                SetTextIfChanged(rightTriggerTitle, family == ControllerFamily.PlayStation ? "R2" : "RT");
                SetTextIfChanged(leftRealtimeTriggerLabel, family == ControllerFamily.PlayStation ? "L2" : "LT");
                SetTextIfChanged(rightRealtimeTriggerLabel, family == ControllerFamily.PlayStation ? "R2" : "RT");
            }
        }

        private void ToggleHistoryPause()
        {
            historyPaused = !historyPaused;
            leftTriggerTelemetry.SetPaused(historyPaused);
            rightTriggerTelemetry.SetPaused(historyPaused);
            leftTriggerChart.Paused = historyPaused;
            rightTriggerChart.Paused = historyPaused;
            if (pauseHistoryMenuItem != null) pauseHistoryMenuItem.Header = historyPaused ? "继续扳机曲线" : "暂停扳机曲线";
            if (footerStatus != null) footerStatus.Text = historyPaused ? "扳机历史曲线已暂停；当前值仍会实时更新。" : "扳机历史曲线已继续记录。";
        }

        private void ClearTriggerHistory()
        {
            leftTriggerChart.ClearHistory();
            rightTriggerChart.ClearHistory();
            leftTriggerTelemetry.Clear();
            rightTriggerTelemetry.Clear();
            if (footerStatus != null) footerStatus.Text = "LT 与 RT 的近 5 秒历史曲线已清空。";
        }

        private void ResetAllSettings()
        {
            MessageBoxResult result = MessageBox.Show(this, "将清除中心校准偏移，并恢复参考死区、设备选择和动态效果。是否继续？", "恢复默认设置", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            leftDeadzone.Value = 0.08;
            rightDeadzone.Value = 0.08;
            offsetLX = offsetLY = offsetRX = offsetRY = 0;
            selectedControllerIndex = -1;
            selectedControllerFamily = ControllerFamily.Auto;
            connectionMethodOverride = "自动";
            input.SetUsbRouteProfiles(null, null);
            if (controllerSelectorButton != null) controllerSelectorButton.Content = demoMode ? "设备：演示" : ControllerSelectionLabel();
            if (controllerFamilySelectorButton != null) controllerFamilySelectorButton.Content = ControllerFamilySelectionLabel();
            reducedMotion = false;
            if (reducedMotionCheck != null) reducedMotionCheck.IsChecked = false;
            ApplyReducedMotion();
            calibrationSuggestionPending = false;
            calibrationStatusVisible = false;
            calibrationMessageUntil = DateTime.MinValue;
            if (calibrationProgress != null) calibrationProgress.Visibility = Visibility.Collapsed;
            if (calibrateButton != null)
            {
                calibrateButton.IsEnabled = true;
                calibrateButton.Content = "中心校准";
            }
            diagnostics.Reset();
            if (demoMode) diagnostics.UseDemoBaseline();
            stickDriftTestEngine.Reset(null);
            if (joystickTestPage != null) joystickTestPage.ResetForDeviceChange("输入模式已切换，摇杆检测已取消");
            ClearStickTestVisualState();
            ClearTriggerHistory();
            if (!demoMode) SaveSettings();
            UpdateDiagnostics(currentState);
            if (footerStatus != null) footerStatus.Text = "中心偏移、参考死区、设备选择与动态效果已恢复默认。";
        }

        private ControllerReport BuildCurrentReport()
        {
            string guidedStatus = "未运行";
            if (guidedTest.IsComplete) guidedStatus = guidedTest.HasSkipped ? "部分完成" : "全部通过";
            else if (guidedTest.Active) guidedStatus = "进行中";
            string[] guidedResults = new string[6];
            for (int i = 0; i < guidedResults.Length; i++) guidedResults[i] = guidedTest.ResultText(i);
            return new ControllerReport
            {
                GeneratedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                Controller = demoMode ? "动态演示" : (currentState.Connected ? (currentState.Family == ControllerFamily.PlayStation ? currentState.DeviceName : string.Format(CultureInfo.InvariantCulture, "Xbox 玩家 {0}", currentState.Index + 1)) : ControllerFamilySelectionLabel()),
                Connected = currentState.Connected,
                DisplayHz = actualDisplayHz,
                SamplingHz = demoMode ? actualDisplayHz : actualSamplingHz,
                DiagnosticReady = diagnostics.IsReady,
                DiagnosticScore = diagnostics.Score,
                DiagnosticStatus = diagnostics.Status,
                DiagnosticDetail = diagnostics.Detail,
                DiagnosticCoverage = diagnostics.CoverageCount,
                CenterLeft = diagnostics.CenterLeft,
                CenterRight = diagnostics.CenterRight,
                GuidedStatus = guidedStatus,
                GuidedStage = guidedTest.StageTitle,
                GuidedResults = guidedResults,
                LeftX = currentState.LeftNormalizedX,
                LeftY = currentState.LeftNormalizedY,
                RightX = currentState.RightNormalizedX,
                RightY = currentState.RightNormalizedY,
                LeftTrigger = currentState.LeftTrigger / 255.0,
                RightTrigger = currentState.RightTrigger / 255.0,
                LeftTriggerPeak = leftTriggerChart.PeakValue,
                RightTriggerPeak = rightTriggerChart.PeakValue,
                ButtonsHex = "0x" + currentState.Buttons.ToString("X4", CultureInfo.InvariantCulture),
                LeftDeadzone = leftDeadzone.Value,
                RightDeadzone = rightDeadzone.Value,
                OffsetLX = offsetLX,
                OffsetLY = offsetLY,
                OffsetRX = offsetRX,
                OffsetRY = offsetRY,
                ReducedMotion = reducedMotion,
                HistoryPaused = historyPaused,
                LeftTriggerHistory = leftTriggerChart.GetHistorySnapshot(),
                RightTriggerHistory = rightTriggerChart.GetHistorySnapshot()
            };
        }

        private void ExportCurrentReport()
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "导出手柄检测报告",
                Filter = "JSON 报告 (*.json)|*.json|CSV 报告 (*.csv)|*.csv",
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = (currentState.Family == ControllerFamily.PlayStation ? "索尼DS手柄报告_" : "Xbox手柄报告_") + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                ControllerReport report = BuildCurrentReport();
                string extension = System.IO.Path.GetExtension(dialog.FileName);
                string content = string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
                    ? ReportExporter.BuildCsv(report)
                    : ReportExporter.BuildJson(report);
                File.WriteAllText(dialog.FileName, content, new UTF8Encoding(true));
                footerStatus.Text = "检测报告已导出：" + System.IO.Path.GetFileName(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message, "导出报告", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportTriggerHistory()
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "导出 LT / RT 曲线",
                Filter = "CSV 文件 (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = "ControllerLab_Trigger_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                double[] left = leftTriggerChart.GetHistorySnapshot();
                double[] right = rightTriggerChart.GetHistorySnapshot();
                int count = Math.Max(left.Length, right.Length);
                StringBuilder csv = new StringBuilder();
                csv.AppendLine("sample,secondsAgo,LT,RT");
                for (int i = 0; i < count; i++)
                {
                    double secondsAgo = (count - 1 - i) * TriggerChart.SampleIntervalSeconds;
                    string lt = i < left.Length ? (left[i] * 100.0).ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
                    string rt = i < right.Length ? (right[i] * 100.0).ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
                    csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:0.000},{2},{3}\r\n", i, secondsAgo, lt, rt);
                }
                File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
                if (footerStatus != null) footerStatus.Text = "LT / RT 曲线已导出：" + System.IO.Path.GetFileName(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出 LT / RT 曲线失败：" + ex.Message, "导出曲线", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDiagnostics(InputSnapshot state)
        {
            if (diagnosticScoreText == null || diagnosticDetailText == null) return;
            if (calibrationStatusVisible) return;
            if (!state.Connected)
            {
                diagnosticScoreText.Foreground = Palette.MutedBrush;
                SetTextIfChanged(diagnosticScoreText, "基础健康 · 等待手柄");
                SetTextIfChanged(diagnosticDetailText, "连接后建立中心基线并测量实际采样率");
                return;
            }
            if (!diagnostics.IsReady)
            {
                diagnosticScoreText.Foreground = Palette.BlueBrush;
                SetTextIfChanged(diagnosticScoreText, "基础健康 · " + diagnostics.Status);
                SetTextIfChanged(diagnosticDetailText, diagnostics.Detail);
                return;
            }
            Brush color = diagnostics.Score >= 90 ? Palette.GreenBrush : diagnostics.Score >= 75 ? Palette.WarningBrush : Palette.RedBrush;
            diagnosticScoreText.Foreground = color;
            SetTextIfChanged(diagnosticScoreText, string.Format(CultureInfo.InvariantCulture, "基础健康 {0} · {1}", diagnostics.Score, diagnostics.Status));
            string detail = diagnostics.Detail.Replace("操作覆盖", "覆盖");
            if (guidedTest.IsComplete) detail += guidedTest.HasSkipped ? " · 体检部分完成" : " · 体检通过";
            if (calibrationSuggestionPending) detail += " · 死区待应用";
            diagnosticDetailText.ToolTip = detail;
            SetTextIfChanged(diagnosticDetailText, detail);
        }

        private static void SetTextIfChanged(TextBlock target, string value)
        {
            if (target != null && target.Text != value) target.Text = value;
        }

        private void OpenApplicationDataDirectory()
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.ApplicationDataDirectory);
                Process.Start("explorer.exe", "\"" + SettingsStore.ApplicationDataDirectory + "\"");
            }
            catch (Exception ex)
            {
                LabLogger.Error("Settings", "Unable to open application data directory.", ex);
                if (noticeBanner != null) noticeBanner.Show(ProductNoticeKind.Error, "无法打开数据目录", "请检查 Windows 文件资源管理器是否可用，然后重试。", "重试", delegate { OpenApplicationDataDirectory(); });
            }
        }

        private void ClearAllHistoryReports()
        {
            List<ControllerHealthReport> reports = healthReportStore.LoadAll();
            if (reports.Count == 0)
            {
                if (noticeBanner != null) noticeBanner.Show(ProductNoticeKind.Info, "没有历史报告", "当前没有需要清除的本地检测报告。", null, null);
                return;
            }
            if (MessageBox.Show("确定清除全部 " + reports.Count.ToString(CultureInfo.InvariantCulture) + " 份历史报告吗？此操作无法撤销。", "清除历史报告", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                for (int i = 0; i < reports.Count; i++) healthReportStore.Delete(reports[i].ReportId);
                if (historyReportsView != null) historyReportsView.Refresh();
                if (noticeBanner != null) noticeBanner.Show(ProductNoticeKind.Success, "历史报告已清除", "已删除本机保存的检测报告。", null, null);
                LabLogger.Info("History", "All local reports cleared.");
            }
            catch (Exception ex)
            {
                LabLogger.Error("History", "Unable to clear all reports.", ex);
                if (noticeBanner != null) noticeBanner.Show(ProductNoticeKind.Error, "清除失败", "部分报告可能仍被其他程序占用，请关闭相关文件后重试。", "重试", delegate { ClearAllHistoryReports(); });
            }
        }

        private void ApplyProductSettings(ControllerSettings settings, bool refreshVisuals)
        {
            if (settings == null) return;
            settings.Normalize();
            reducedMotion = settings.ReducedMotion;
            uiRefreshInterval = TimeSpan.FromSeconds(1.0 / settings.UiRefreshRate);
            joystickTestViewModel.StationarySampleDurationSeconds = settings.StationarySampleDuration;
            joystickTestViewModel.DeadzoneSafetyMarginPercent = settings.DeadzoneSafetyMarginPercent;
            healthReportStore.SaveEnabled = settings.SaveHistory;
            dualSenseAdvancedManager.SetTrailCapacity(settings.TrailLength);
            rumbleSettingsStore.SetGlobalDefaults(settings.DefaultRumbleStrengthPercent / 100.0, settings.DefaultRumbleDurationSeconds, settings.RumbleSafetyMaximumPercent / 100.0);
            if (!refreshVisuals) return;
            if (reducedMotionCheck != null) reducedMotionCheck.IsChecked = reducedMotion;
            if (leftDriftY != null) leftDriftY.Visibility = settings.ShowAdvancedData ? Visibility.Visible : Visibility.Collapsed;
            if (rightDriftY != null) rightDriftY.Visibility = settings.ShowAdvancedData ? Visibility.Visible : Visibility.Collapsed;
            if (realtimeAdvancedButton != null) realtimeAdvancedButton.Visibility = settings.ShowAdvancedData ? Visibility.Visible : Visibility.Collapsed;
            if (demoModeButton != null && !demoMode) demoModeButton.Visibility = settings.ShowAdvancedData ? Visibility.Visible : Visibility.Collapsed;
            ApplyReducedMotion();
            UpdateDeviceCardResponsiveLayout();
            if (noticeBanner != null && IsLoaded) noticeBanner.Show(ProductNoticeKind.Success, "设置已应用", "界面显示与测试默认值已经更新。", null, null);
        }

        private void SaveSettings()
        {
            productSettings.OffsetLX = offsetLX;
            productSettings.OffsetLY = offsetLY;
            productSettings.OffsetRX = offsetRX;
            productSettings.OffsetRY = offsetRY;
            productSettings.LeftDeadzone = leftDeadzone.Value;
            productSettings.RightDeadzone = rightDeadzone.Value;
            productSettings.ControllerIndex = selectedControllerIndex;
            productSettings.ReducedMotion = reducedMotion;
            productSettings.AnimationsEnabled = !reducedMotion;
            productSettings.ConnectionMethodOverride = connectionMethodOverride;
            productSettings.WiredUsbRoute = input.WiredUsbRoute;
            productSettings.ReceiverUsbRoute = input.ReceiverUsbRoute;
            productSettings.ControllerFamily = selectedControllerFamily.ToString();
            if (productSettings.RememberWindowPosition)
            {
                Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
                if (bounds.Width >= MinWidth && bounds.Height >= MinHeight)
                {
                    productSettings.HasWindowPlacement = true;
                    productSettings.WindowLeft = bounds.Left;
                    productSettings.WindowTop = bounds.Top;
                    productSettings.WindowWidth = bounds.Width;
                    productSettings.WindowHeight = bounds.Height;
                }
            }
            SettingsStore.Save(productSettings);
        }
    }





































}
