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
    public static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [STAThread]
        public static void Main()
        {
            if (HasArgument("--startup-selftest"))
            {
                try
                {
                    App startupApp = new App();
                    MainWindow startupWindow = new MainWindow();
                    startupWindow.Close();
                    startupApp.Shutdown();
                    Console.WriteLine("ControllerLab startup construction self-test passed.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (HasArgument("--runtime-selftest"))
            {
                Exception runtimeFailure = null;
                App runtimeApp = new App();
                runtimeApp.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                {
                    runtimeFailure = e.Exception;
                    e.Handled = true;
                    runtimeApp.Shutdown(-1);
                };
                MainWindow runtimeWindow = new MainWindow();
                runtimeWindow.Loaded += delegate
                {
                    DispatcherTimer shutdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                    shutdownTimer.Tick += delegate
                    {
                        shutdownTimer.Stop();
                        runtimeWindow.Close();
                    };
                    shutdownTimer.Start();
                };
                runtimeApp.Run(runtimeWindow);
                if (runtimeFailure != null)
                {
                    Console.Error.WriteLine(runtimeFailure.ToString());
                    Environment.ExitCode = 1;
                }
                else Console.WriteLine("ControllerLab displayed-runtime self-test passed.");
                return;
            }
            if (HasArgument("--controller-navigation-selftest"))
            {
                Exception navigationFailure = null;
                string navigationResult = null;
                App navigationApp = new App();
                MainWindow navigationWindow = new MainWindow();
                navigationWindow.Width = 1100;
                navigationWindow.Height = 720;
                navigationWindow.Loaded += delegate
                {
                    DispatcherTimer navigationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
                    navigationTimer.Tick += delegate
                    {
                        navigationTimer.Stop();
                        try { navigationResult = navigationWindow.RunControllerNavigationSelfTest(); }
                        catch (Exception ex) { navigationFailure = ex; }
                        finally { navigationWindow.Close(); }
                    };
                    navigationTimer.Start();
                };
                navigationApp.Run(navigationWindow);
                if (navigationFailure != null)
                {
                    Console.Error.WriteLine(navigationFailure.ToString());
                    Environment.ExitCode = 1;
                }
                else Console.WriteLine(navigationResult);
                return;
            }
            if (HasArgument("--xbox-trigger-calibrate"))
            {
                App calibrationApp = new App();
                XboxTriggerCalibrationWindow calibrationWindow = new XboxTriggerCalibrationWindow(
                    XboxRegionManager.Load(false), XboxRegionManager.LoadControllerPhotoForCalibration());
                calibrationApp.Run(calibrationWindow);
                return;
            }
            if (HasArgument("--xbox-face-calibrate"))
            {
                App calibrationApp = new App();
                XboxCalibrationWindow calibrationWindow = new XboxCalibrationWindow(
                    XboxRegionManager.Load(false), XboxRegionManager.LoadControllerPhotoForCalibration(),
                    new[] { "a", "b", "x", "y" }, "Xbox A/B/X/Y 手动校准");
                calibrationApp.Run(calibrationWindow);
                return;
            }
            if (HasArgument("--xbox-dpad-up-calibrate") || HasArgument("--xbox-dpad-calibrate"))
            {
                App calibrationApp = new App();
                XboxDPadCalibrationWindow calibrationWindow = new XboxDPadCalibrationWindow(XboxRegionManager.Load(false), XboxRegionManager.LoadControllerPhotoForCalibration(), "dpad-up");
                calibrationApp.Run(calibrationWindow);
                return;
            }
            if (HasArgument("--ds5-touch-parser-selftest"))
            {
                Console.WriteLine(SonyInputManager.RunTouchParserSelfTest());
                return;
            }
            if (HasArgument("--ds5-motion-selftest"))
            {
                Console.WriteLine(DualSenseMotionSelfTest.Run());
                return;
            }
            if (HasArgument("--ds5-overlay-selftest"))
            {
                Console.WriteLine(DualSenseRegionManager.RunOverlayGeometrySelfTest());
                return;
            }
            if (HasArgument("--xbox-overlay-selftest"))
            {
                Console.WriteLine(XboxRegionManager.RunOverlayGeometrySelfTest());
                return;
            }
            if (HasArgument("--xbox-top-controls-autocal-selftest"))
            {
                Console.WriteLine(XboxRegionManager.RunTopControlAutoCalibrationSelfTest());
                return;
            }
            if (HasArgument("--xbox-dpad-autocal-selftest"))
            {
                Console.WriteLine(XboxRegionManager.RunDPadAutoCalibrationSelfTest());
                return;
            }
            if (HasArgument("--xbox-dpad-autocal-report"))
            {
                Console.WriteLine(XboxRegionManager.GetDPadAutoCalibrationReport());
                return;
            }
            if (HasArgument("--xbox-dpad-up-render-audit"))
            {
                string auditDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "xbox-dpadup-2026-07-23");
                Console.WriteLine(XboxRegionManager.RenderDPadUpAudit(auditDirectory));
                return;
            }
            if (HasArgument("--xbox-overlay-render-audit"))
            {
                string auditDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "xbox-polish-2026-07-23", "overlays");
                Console.WriteLine(XboxRegionManager.RenderDefaultOverlayAudit(auditDirectory));
                return;
            }
            if (HasArgument("--xbox-polish-render-audit"))
            {
                string auditDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "xbox-polish-2026-07-23", "polish-final");
                Console.WriteLine(XboxRegionManager.RenderPolishAudit(auditDirectory));
                return;
            }
            if (HasArgument("--xbox-trigger-geometry-render-audit"))
            {
                string auditDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "xbox-trigger-geometry");
                Console.WriteLine(XboxRegionManager.RenderTriggerGeometryAudit(auditDirectory));
                return;
            }
            if (HasArgument("--trigger-chart-selftest"))
            {
                Console.WriteLine(TriggerTelemetryBuffer.RunSelfTest());
                return;
            }
            if (HasArgument("--controller-core-selftest"))
            {
                Console.WriteLine(ControllerCoreSelfTest.Run());
                return;
            }
            if (HasArgument("--stick-drift-selftest"))
            {
                Console.WriteLine(ControllerCoreSelfTest.RunStickDriftSelfTest());
                return;
            }
            if (HasArgument("--device-manager-selftest"))
            {
                Console.WriteLine(ControllerCoreSelfTest.RunDeviceManagerSelfTest());
                return;
            }
            bool demo = HasArgument("--demo") || HasArgument("--sony-demo") || HasArgument("--multi-demo");
            string mutexName = HasArgument("--multi-demo") ? "Local\\ControllerLab.MultiDemo" : (demo ? "Local\\ControllerLab.Demo" : "Local\\ControllerLab");
            bool created;
            using (Mutex mutex = new Mutex(true, mutexName, out created))
            {
                if (!created)
                {
                    IntPtr existing = FindWindow(null, "手柄实验室");
                    if (existing == IntPtr.Zero) existing = FindWindow(null, "Xbox 手柄实验室");
                    if (existing != IntPtr.Zero)
                    {
                        ShowWindow(existing, 9);
                        SetForegroundWindow(existing);
                    }
                    return;
                }
                App app = new App();
                app.Run(new MainWindow());
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
    }

    public sealed class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
            DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs exception)
            {
                RecordUnhandledException("WPF Dispatcher", exception.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs exception)
            {
                RecordUnhandledException("AppDomain", exception.ExceptionObject as Exception);
            };
        }

        // Keep the native exception behavior for unexpected faults, but leave a
        // useful managed stack trace beside the executable's normal user data.
        // This is especially important for faults reported by Windows only as
        // 0xe0434352, which otherwise have no actionable call stack.
        internal static void RecordUnhandledException(string source, Exception exception)
        {
            try
            {
                string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerLab", "logs");
                Directory.CreateDirectory(directory);
                string message = string.Format(CultureInfo.InvariantCulture, "[{0:O}] {1}\r\n{2}\r\n\r\n", DateTime.UtcNow, source, exception == null ? "Unknown managed exception" : exception.ToString());
                File.AppendAllText(System.IO.Path.Combine(directory, "crash.log"), message, Encoding.UTF8);
            }
            catch { }
        }
    }

    public delegate void ControllerDeviceSelectedEventHandler(object sender, IControllerDevice device);

    public sealed class ControllerVisualizerView : Grid
    {
        public ControllerVisualizerView(UIElement content)
        {
            ClipToBounds = true;
            if (content != null) Children.Add(content);
        }
    }

    public sealed class DeviceCard : Button
    {
        private readonly Border surface;
        private bool controllerNavigationSelected;
        public event ControllerDeviceSelectedEventHandler DeviceSelected;

        public DeviceCard()
        {
            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Margin = new Thickness(0, 0, 14, 14);
            Cursor = Cursors.Hand;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            surface = new Border
            {
                Width = 300,
                MinHeight = 156,
                Style = LabVisualStyles.MetricCardStyle,
                CornerRadius = LabVisualStyles.CardRadius,
                Background = Palette.Surface2Brush,
                BorderBrush = Palette.BorderSubtleBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 16, 18, 16)
            };
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock name = new TextBlock { Foreground = Palette.TextBrush, FontSize = 17, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            name.SetBinding(TextBlock.TextProperty, new Binding("DisplayName"));
            layout.Children.Add(name);
            StackPanel typeAndStatus = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock type = new TextBlock { Foreground = Palette.BlueBrush, FontSize = 12, FontWeight = FontWeights.SemiBold };
            type.SetBinding(TextBlock.TextProperty, new Binding("ControllerTypeLabel"));
            typeAndStatus.Children.Add(type);
            typeAndStatus.Children.Add(new TextBlock { Text = " · ", Foreground = Palette.MutedBrush, FontSize = 12 });
            TextBlock status = new TextBlock { Foreground = Palette.BlueBrush, FontSize = 12 };
            status.SetBinding(TextBlock.TextProperty, new Binding("ConnectionStatusLabel"));
            typeAndStatus.Children.Add(status);
            Grid.SetRow(typeAndStatus, 2);
            layout.Children.Add(typeAndStatus);
            StackPanel connection = MakeRow("连接方式");
            ((TextBlock)connection.Children[1]).SetBinding(TextBlock.TextProperty, new Binding("ConnectionLabel"));
            Grid.SetRow(connection, 4);
            layout.Children.Add(connection);
            StackPanel battery = MakeRow("电量");
            ((TextBlock)battery.Children[1]).SetBinding(TextBlock.TextProperty, new Binding("BatteryLabel"));
            Grid.SetRow(battery, 6);
            layout.Children.Add(battery);
            surface.Child = layout;
            Content = surface;
            MouseEnter += delegate { surface.BorderBrush = Palette.BlueBrush; surface.Background = Palette.SurfaceRaisedBrush; };
            MouseLeave += delegate { if (!controllerNavigationSelected) { surface.BorderBrush = Palette.BorderSubtleBrush; surface.Background = Palette.Surface2Brush; } };
            Click += delegate
            {
                ControllerDeviceSelectedEventHandler handler = DeviceSelected;
                if (handler != null) handler(this, DataContext as IControllerDevice);
            };
            DataContextChanged += delegate
            {
                IControllerDevice device = DataContext as IControllerDevice;
                if (device != null) AutomationProperties.SetName(this, device.DisplayName + " · " + device.ControllerType + " · " + device.ConnectionType);
            };
        }

        public void SetControllerNavigationSelected(bool selected)
        {
            controllerNavigationSelected = selected;
            surface.BorderBrush = selected ? Palette.BlueBrush : Palette.BorderSubtleBrush;
            surface.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            surface.Background = selected ? Palette.SurfaceRaisedBrush : Palette.Surface2Brush;
        }

        private static StackPanel MakeRow(string label)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = label + "：", Foreground = Palette.MutedBrush, FontSize = 11 });
            row.Children.Add(new TextBlock { Foreground = Palette.TextBrush, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            return row;
        }
    }

    public sealed class DeviceHomeView : Grid, IDisposable
    {
        private readonly ObservableCollection<IControllerDevice> devices;
        private readonly WrapPanel cards;
        private bool disposed;
        public event ControllerDeviceSelectedEventHandler DeviceSelected;

        public DeviceHomeView(ObservableCollection<IControllerDevice> devices)
        {
            this.devices = devices;
            Margin = new Thickness(32, 28, 32, 0);
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            StackPanel header = new StackPanel();
            header.Children.Add(LabVisualStyles.CreatePageTitle("设备首页"));
            TextBlock subtitle = LabVisualStyles.CreateSecondaryText("已连接的 Xbox 与 DualSense 手柄会自动出现在这里。");
            subtitle.FontSize = 14;
            subtitle.Margin = new Thickness(0, 7, 0, 0);
            header.Children.Add(subtitle);
            Children.Add(header);
            cards = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            ScrollViewer scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = cards };
            Grid.SetRow(scroller, 2);
            Children.Add(scroller);
            if (devices != null) devices.CollectionChanged += OnDevicesChanged;
            RebuildCards();
        }

        private void OnDevicesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildCards();
        }

        private void RebuildCards()
        {
            cards.Children.Clear();
            if (devices == null || devices.Count == 0)
            {
                cards.Children.Add(new TextBlock
                {
                    Text = "未检测到兼容手柄。连接 Xbox 或 DualSense 后无需重启程序，列表会自动刷新。",
                    Foreground = Palette.MutedBrush,
                    FontSize = 14,
                    Margin = new Thickness(2, 8, 0, 0)
                });
                return;
            }
            for (int i = 0; i < devices.Count; i++)
            {
                DeviceCard card = new DeviceCard { DataContext = devices[i] };
                card.DeviceSelected += delegate(object sender, IControllerDevice device)
                {
                    ControllerDeviceSelectedEventHandler handler = DeviceSelected;
                    if (handler != null) handler(this, device);
                };
                cards.Children.Add(card);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (devices != null) devices.CollectionChanged -= OnDevicesChanged;
            cards.Children.Clear();
        }
    }

    public static class Palette
    {
        public static readonly Color Window = Color.FromRgb(10, 17, 23);
        public static readonly Color Surface = Color.FromRgb(17, 29, 39);
        public static readonly Color Surface2 = Color.FromRgb(23, 38, 50);
        public static readonly Color SurfaceHover = Color.FromRgb(29, 47, 60);
        public static readonly Color SurfaceRaised = Color.FromRgb(35, 56, 70);
        public static readonly Color Border = Color.FromRgb(81, 101, 114);
        public static readonly Color Muted = Color.FromRgb(142, 161, 173);
        public static readonly Color Text = Color.FromRgb(235, 242, 247);
        public static readonly Color Green = Color.FromRgb(88, 201, 133);
        public static readonly Color Blue = Color.FromRgb(64, 186, 227);
        public static readonly Color AccentHover = Color.FromRgb(103, 205, 238);
        public static readonly Color TopLeftShoulder = Color.FromRgb(45, 218, 191);
        public static readonly Color TopRightShoulder = Color.FromRgb(151, 112, 245);
        public static readonly Color Red = Color.FromRgb(239, 107, 98);
        public static readonly Color Warning = Color.FromRgb(235, 184, 79);
        public static readonly SolidColorBrush WindowBrush = Freeze(new SolidColorBrush(Window));
        public static readonly SolidColorBrush SurfaceBrush = Freeze(new SolidColorBrush(Surface));
        public static readonly SolidColorBrush Surface2Brush = Freeze(new SolidColorBrush(Surface2));
        public static readonly SolidColorBrush SurfaceHoverBrush = Freeze(new SolidColorBrush(SurfaceHover));
        public static readonly SolidColorBrush SurfaceRaisedBrush = Freeze(new SolidColorBrush(SurfaceRaised));
        public static readonly SolidColorBrush BorderBrush = Freeze(new SolidColorBrush(Border));
        public static readonly SolidColorBrush BorderSubtleBrush = Freeze(new SolidColorBrush(Color.FromArgb(128, Border.R, Border.G, Border.B)));
        public static readonly SolidColorBrush MutedBrush = Freeze(new SolidColorBrush(Muted));
        public static readonly SolidColorBrush TextBrush = Freeze(new SolidColorBrush(Text));
        public static readonly SolidColorBrush GreenBrush = Freeze(new SolidColorBrush(Green));
        public static readonly SolidColorBrush BlueBrush = Freeze(new SolidColorBrush(Blue));
        public static readonly SolidColorBrush AccentHoverBrush = Freeze(new SolidColorBrush(AccentHover));
        public static readonly SolidColorBrush RedBrush = Freeze(new SolidColorBrush(Red));
        public static readonly SolidColorBrush WarningBrush = Freeze(new SolidColorBrush(Warning));

        private static T Freeze<T>(T value) where T : Freezable
        {
            value.Freeze();
            return value;
        }
    }

    public enum ControllerFamily
    {
        Auto,
        Xbox,
        PlayStation
    }

    public sealed class MainWindow : Window
    {
        private bool demoMode;
        private bool sonyDemoMode;
        private readonly bool multiDemoMode;
        private readonly InputManager input;
        private readonly SonyInputManager sonyInput;
        private readonly ControllerDeviceManager deviceManager;
        private readonly DualSenseMotionManager motionManager;
        private readonly ControllerInputTestEngine inputTestEngine = new ControllerInputTestEngine();
        private readonly StickTriggerTestEngine stickTriggerTestEngine = new StickTriggerTestEngine();
        private readonly StickDriftTestEngine stickDriftTestEngine = new StickDriftTestEngine();
        private readonly DispatcherTimer timer;
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
        private Button inputTestPageButton;
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
            demoMode = HasArgument("--demo") || sonyDemoMode || multiDemoMode;
            input = new InputManager();
            sonyInput = new SonyInputManager();
            deviceManager = new ControllerDeviceManager(input, sonyInput);
            motionManager = new DualSenseMotionManager();
            Title = "手柄实验室";
            MinWidth = 1120;
            MinHeight = 760;
            Rect workArea = SystemParameters.WorkArea;
            if (demoMode)
            {
                Width = Math.Min(1440, Math.Max(MinWidth, workArea.Width - 24));
                Height = Math.Min(1024, Math.Max(MinHeight, workArea.Height - 24));
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                Width = Math.Min(1440, Math.Max(MinWidth, workArea.Width - 24));
                Height = Math.Min(1024, Math.Max(MinHeight, workArea.Height - 24));
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = workArea.Left + (workArea.Width - Width) / 2.0;
                Top = workArea.Top + (workArea.Height - Height) / 2.0;
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
            ControllerSettings saved = demoMode ? new ControllerSettings() : SettingsStore.Load();
            offsetLX = saved.OffsetLX;
            offsetLY = saved.OffsetLY;
            offsetRX = saved.OffsetRX;
            offsetRY = saved.OffsetRY;
            selectedControllerIndex = Math.Max(-1, Math.Min(3, saved.ControllerIndex));
            reducedMotion = saved.ReducedMotion;
            connectionMethodOverride = NormalizeConnectionMethodOverride(saved.ConnectionMethodOverride);
            selectedControllerFamily = NormalizeControllerFamily(saved.ControllerFamily);
            if (sonyDemoMode) selectedControllerFamily = ControllerFamily.PlayStation;
            input.SetUsbRouteProfiles(saved.WiredUsbRoute, saved.ReceiverUsbRoute);
            if (demoMode) diagnostics.UseDemoBaseline();
            leftDeadzone = new DeadzoneSlider(Palette.Blue, saved.LeftDeadzone);
            rightDeadzone = new DeadzoneSlider(Palette.Blue, saved.RightDeadzone);
            ApplyReducedMotion();

            Content = BuildRoot();
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

            timer = new DispatcherTimer(DispatcherPriority.Render);
            timer.Interval = TimeSpan.FromMilliseconds(8);
            timer.Tick += OnTick;
            Loaded += delegate
            {
                StartSampling();
                timer.Start();
                UpdateDeviceCardResponsiveLayout();
                if (HasArgument("--ds5-calibrate")) Dispatcher.BeginInvoke(new Action(OpenDualSenseCalibration), DispatcherPriority.Background);
                if (HasArgument("--xbox-calibrate")) Dispatcher.BeginInvoke(new Action(OpenXboxCalibration), DispatcherPriority.Background);
                if (HasArgument("--visualizer")) Dispatcher.BeginInvoke(new Action(delegate { ShowPage(1); }), DispatcherPriority.Background);
            };
            Closed += delegate
            {
                timer.Stop();
                StopSampling();
                stickDriftTestEngine.Dispose();
                if (!demoMode) SaveSettings();
                if (deviceHomeView != null) deviceHomeView.Dispose();
                deviceManager.Dispose();
                sonyInput.Dispose();
                input.Dispose();
            };
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
            Grid.SetColumn(right, 2);
            content.Children.Add(right);
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
            stickDriftTestPage = BuildStickDriftTestPage();
            stickDriftTestPage.Visibility = Visibility.Collapsed;
            motionPage = BuildMotionPage();
            motionPage.Visibility = Visibility.Collapsed;
            pageHost = new Grid();
            pageHost.Children.Add(homePage);
            pageHost.Children.Add(visualizerPage);
            pageHost.Children.Add(inputTestPage);
            pageHost.Children.Add(stickDriftTestPage);
            pageHost.Children.Add(motionPage);
            Grid.SetRow(pageHost, 1);
            root.Children.Add(pageHost);
            shellContent = pageHost;

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
            Button[] actionsList = { stickTestStartButton, stickTestRestartButton, stickTestStopButton, stickRangeStartButton, stickRangeStopButton, stickTestCopyButton };
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

        private void StartMotionCalibration()
        {
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
            nextMotionUiRefresh = now.AddMilliseconds(16.7);
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
            if (homePage == null || visualizerPage == null || inputTestPage == null || stickDriftTestPage == null || motionPage == null) return;
            currentPage = Math.Max(0, Math.Min(4, page));
            page = currentPage;
            if (page != 3)
            {
                if (stickDriftTestEngine.IsActive) stickDriftTestEngine.Cancel("已离开摇杆检测页面，检测未完成");
                ClearStickTestVisualState();
            }
            homePage.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
            visualizerPage.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
            inputTestPage.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;
            stickDriftTestPage.Visibility = page == 3 ? Visibility.Visible : Visibility.Collapsed;
            motionPage.Visibility = page == 4 ? Visibility.Visible : Visibility.Collapsed;
            UpdatePageButton(homePageButton, page == 0);
            UpdatePageButton(visualizerPageButton, page == 1);
            UpdatePageButton(inputTestPageButton, page == 2);
            UpdatePageButton(stickDriftPageButton, page == 3);
            UpdatePageButton(motionPageButton, page == 4);
            LabVisualStyles.FadeIn(page == 0 ? homePage : page == 1 ? visualizerPage : page == 2 ? inputTestPage : page == 3 ? stickDriftTestPage : motionPage, reducedMotion);
            if (controllerNavigationEnabled) RebuildControllerNavigationTargets(true);
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
            if ((pressed & 0x0100) != 0) ShowControllerPage(currentPage == 0 ? 4 : currentPage - 1);
            if ((pressed & 0x0200) != 0) ShowControllerPage(currentPage == 4 ? 0 : currentPage + 1);
            if ((pressed & 0x1000) != 0) InvokeControllerFocusedAction();
            if ((pressed & 0x2000) != 0) ShowControllerPage(0);

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

        private UIElement CurrentPageRoot
        {
            get
            {
                return currentPage == 0 ? homePage : currentPage == 1 ? visualizerPage : currentPage == 2 ? inputTestPage : currentPage == 3 ? stickDriftTestPage : motionPage;
            }
        }

        private void RebuildControllerNavigationTargets(bool selectFirst)
        {
            Control previous = controllerNavigationHighlightedTarget;
            ClearControllerNavigationHighlight();
            controllerNavigationTargets.Clear();
            CollectControllerNavigationTargets(CurrentPageRoot);
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
            ShowPage(0);
            input.Buttons = 0;
            HandleControllerNavigation(input);
            input.Buttons = 0x0200;
            HandleControllerNavigation(input);
            if (currentPage != 1) throw new InvalidOperationException("RB did not advance to the next page.");
            input.Buttons = 0;
            HandleControllerNavigation(input);
            input.Buttons = 0x0100;
            HandleControllerNavigation(input);
            if (currentPage != 0) throw new InvalidOperationException("LB did not return to the previous page.");
            input.Buttons = 0;
            HandleControllerNavigation(input);
            ShowPage(2);
            input.Buttons = 0x2000;
            HandleControllerNavigation(input);
            if (currentPage != 0) throw new InvalidOperationException("B did not return to the device home page.");

            controllerNavigationEnabled = false;
            ClearControllerNavigationSelection();
            return "Controller navigation self-test passed: opt-in, D-pad selection, A action, page routing, and " + (scrollVerified ? "RT scrolling" : "scroll fallback") + ".";
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
                return "ControllerLab v1.2.0-test 检测报告\n当前未连接可用于正式检测的真实设备。\n动态演示和构造自检数据不会进入正式报告。";
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
                "ControllerLab v1.2.0-test 检测报告\n检测时间：{0:yyyy-MM-dd HH:mm:ss}\n设备：{1}\n设备 ID：{2}\n手柄类型：{3}\n连接方式：{4}\n输入来源：{5}\n\n按键：{6}/{7} 通过\n未通过按钮：{8}\n左/右扳机峰值：{9:0}% / {10:0}%\n\n左摇杆：P95 {11}，建议死区 {12}\n连续检测：{13}\n范围：{14}\n右摇杆：P95 {15}，建议死区 {16}\n连续检测：{17}\n范围：{18}\n检测有效：{19}",
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

            StackPanel navigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            homePageButton = MakeButton("设备首页", false);
            visualizerPageButton = MakeButton("实时可视化", false);
            inputTestPageButton = MakeButton("按键检测", false);
            stickDriftPageButton = MakeButton("摇杆检测", false);
            motionPageButton = MakeButton("体感", false);
            Button[] pages = { homePageButton, visualizerPageButton, inputTestPageButton, stickDriftPageButton, motionPageButton };
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Width = 92;
                pages[i].Height = 34;
                pages[i].FontSize = 12;
                pages[i].Padding = new Thickness(8, 2, 8, 3);
                pages[i].Margin = new Thickness(3, 0, 3, 0);
                navigation.Children.Add(pages[i]);
            }
            homePageButton.Click += delegate { ShowPage(0); };
            visualizerPageButton.Click += delegate { ShowPage(1); };
            inputTestPageButton.Click += delegate { ShowPage(2); };
            stickDriftPageButton.Click += delegate { ShowPage(3); };
            motionPageButton.Click += delegate { ShowPage(4); };
            Grid.SetColumn(navigation, 1);
            title.Children.Add(navigation);
            UpdatePageButton(homePageButton, true);

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
            UpdateDeviceCardResponsiveLayout();
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
            selectedDeviceId = deviceId;
            ClearTriggerHistory();
            diagnostics.Reset();
            inputTestEngine.Reset(currentControllerState);
            stickTriggerTestEngine.Reset(currentControllerState);
            stickDriftTestEngine.Reset(null);
            ClearStickTestVisualState();
            if (motionPoseView != null) motionPoseView.SetState(null);
            nextMotionUiRefresh = DateTime.MinValue;
            renderedInputTestSignature = null;
            if (controllerFamilySelectorButton != null) controllerFamilySelectorButton.Content = DeviceSelectionLabel();
            if (footerStatus != null) footerStatus.Text = string.IsNullOrEmpty(deviceId) ? "已启用自动设备选择。" : "已切换到 " + DeviceSelectionLabel() + "。";
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
            selectedControllerFamily = family;
            ClearTriggerHistory();
            stickDriftTestEngine.Reset(null);
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
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            guidedLaunchButton = MakeButton("自动体检", true);
            guidedLaunchButton.Click += delegate { BeginGuidedTest(); };
            actions.Children.Add(guidedLaunchButton);
            calibrateButton = MakeButton(demoMode ? "演示中" : "中心校准", false);
            calibrateButton.IsEnabled = !demoMode;
            calibrateButton.Click += StartCalibration;
            Grid.SetColumn(calibrateButton, 2);
            actions.Children.Add(calibrateButton);
            Button more = MakeButton("更多", false);
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
            Grid.SetColumn(more, 4);
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
            }
            // This is the only point where the WPF-bound device collection changes.
            // Sampling continues on its background thread and never touches the UI.
            deviceManager.Synchronize(latestControllerStates);
            ControllerState selected = ResolveActiveControllerState();
            currentControllerState = selected;
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
            UpdateVisuals(state);
            diagnostics.Update(state, demoMode ? 220.0 : actualSamplingHz, leftDeadzone.Value, rightDeadzone.Value);
            UpdateInputTestPage(selected);
            UpdateStickDriftTestPage(selected);
            UpdateMotionPage(selected);
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
                if (footerStatus != null && devices.Length > 0) footerStatus.Text = "选中的手柄已断开，已自动切换到其他在线设备。";
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
                connectionDot.Fill = Palette.BlueBrush;
                connectionText.Foreground = Palette.BlueBrush;
                string touchStatus = state.Family == ControllerFamily.PlayStation
                    ? (state.TouchCoordinatesAvailable ? " · 触摸坐标可用" : " · 触摸坐标不可用（仅按压）")
                    : string.Empty;
                string status = demoMode
                    ? "动态演示" + (sonyDemoMode ? " · 触摸坐标不可用（仅按压）" : string.Empty)
                    : state.Family == ControllerFamily.PlayStation
                        ? "已连接 · 原生 HID" + touchStatus
                        : string.Format(CultureInfo.InvariantCulture, "已连接 · 玩家 {0}", state.Index + 1);
                SetTextIfChanged(connectionText, status);
                UpdateConnectionMethod(state);
                SetTextIfChanged(deviceMetaText, string.IsNullOrEmpty(state.InputBackend) ? input.LibraryName : state.InputBackend);
            }
            else
            {
                connectionDot.Fill = Palette.RedBrush;
                connectionText.Foreground = Palette.RedBrush;
                SetTextIfChanged(connectionText, selectedControllerIndex < 0
                    ? "未检测到手柄"
                    : string.Format(CultureInfo.InvariantCulture, "玩家 {0} 未连接", selectedControllerIndex + 1));
                UpdateConnectionMethod(state);
                SetTextIfChanged(deviceMetaText, renderedControllerFamily == ControllerFamily.PlayStation ? "Sony 原生 HID" : input.LibraryName);
                if (lastConnected) footerStatus.Text = "请通过 USB 或蓝牙连接 Xbox 或索尼 DS 手柄，连接后会自动开始监测。";
            }
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
            SetTextIfChanged(leftDriftY, string.Format(CultureInfo.InvariantCulture, "X {0:0.000} · Y {1:0.000}", state.LeftNormalizedX, state.LeftNormalizedY));
            SetTextIfChanged(rightDriftX, string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", rightMagnitude * 100.0));
            SetTextIfChanged(rightDriftY, string.Format(CultureInfo.InvariantCulture, "X {0:0.000} · Y {1:0.000}", state.RightNormalizedX, state.RightNormalizedY));
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

        private void SaveSettings()
        {
            SettingsStore.Save(new ControllerSettings
            {
                OffsetLX = offsetLX,
                OffsetLY = offsetLY,
                OffsetRX = offsetRX,
                OffsetRY = offsetRY,
                LeftDeadzone = leftDeadzone.Value,
                RightDeadzone = rightDeadzone.Value,
                ControllerIndex = selectedControllerIndex,
                ReducedMotion = reducedMotion,
                ConnectionMethodOverride = connectionMethodOverride,
                WiredUsbRoute = input.WiredUsbRoute,
                ReceiverUsbRoute = input.ReceiverUsbRoute
                ,ControllerFamily = selectedControllerFamily.ToString()
            });
        }
    }

    public sealed class ControllerReport
    {
        public string GeneratedAt;
        public string Controller;
        public bool Connected;
        public double DisplayHz;
        public double SamplingHz;
        public bool DiagnosticReady;
        public int DiagnosticScore;
        public string DiagnosticStatus;
        public string DiagnosticDetail;
        public int DiagnosticCoverage;
        public double CenterLeft;
        public double CenterRight;
        public string GuidedStatus;
        public string GuidedStage;
        public string[] GuidedResults;
        public double LeftX;
        public double LeftY;
        public double RightX;
        public double RightY;
        public double LeftTrigger;
        public double RightTrigger;
        public double LeftTriggerPeak;
        public double RightTriggerPeak;
        public string ButtonsHex;
        public double LeftDeadzone;
        public double RightDeadzone;
        public double OffsetLX;
        public double OffsetLY;
        public double OffsetRX;
        public double OffsetRY;
        public bool ReducedMotion;
        public bool HistoryPaused;
        public double[] LeftTriggerHistory;
        public double[] RightTriggerHistory;
    }

    public static class ReportExporter
    {
        public static string BuildJson(ControllerReport report)
        {
            if (report == null) throw new ArgumentNullException("report");
            StringBuilder builder = new StringBuilder();
            builder.Append("{\n");
            AppendString(builder, "generatedAt", report.GeneratedAt, true);
            AppendString(builder, "controller", report.Controller, true);
            AppendBoolean(builder, "connected", report.Connected, true);
            AppendNumber(builder, "displayHz", report.DisplayHz, true);
            AppendNumber(builder, "samplingHz", report.SamplingHz, true);
            AppendBoolean(builder, "diagnosticReady", report.DiagnosticReady, true);
            AppendInteger(builder, "diagnosticScore", report.DiagnosticScore, true);
            AppendString(builder, "diagnosticStatus", report.DiagnosticStatus, true);
            AppendString(builder, "diagnosticDetail", report.DiagnosticDetail, true);
            AppendInteger(builder, "diagnosticCoverage", report.DiagnosticCoverage, true);
            AppendNumber(builder, "centerLeft", report.CenterLeft, true);
            AppendNumber(builder, "centerRight", report.CenterRight, true);
            AppendString(builder, "guidedStatus", report.GuidedStatus, true);
            AppendString(builder, "guidedStage", report.GuidedStage, true);
            AppendStringArray(builder, "guidedResults", report.GuidedResults, true);
            AppendNumber(builder, "leftX", report.LeftX, true);
            AppendNumber(builder, "leftY", report.LeftY, true);
            AppendNumber(builder, "rightX", report.RightX, true);
            AppendNumber(builder, "rightY", report.RightY, true);
            AppendNumber(builder, "leftTrigger", report.LeftTrigger, true);
            AppendNumber(builder, "rightTrigger", report.RightTrigger, true);
            AppendNumber(builder, "leftTriggerPeak", report.LeftTriggerPeak, true);
            AppendNumber(builder, "rightTriggerPeak", report.RightTriggerPeak, true);
            AppendString(builder, "buttons", report.ButtonsHex, true);
            AppendNumber(builder, "leftDeadzone", report.LeftDeadzone, true);
            AppendNumber(builder, "rightDeadzone", report.RightDeadzone, true);
            AppendNumber(builder, "offsetLX", report.OffsetLX, true);
            AppendNumber(builder, "offsetLY", report.OffsetLY, true);
            AppendNumber(builder, "offsetRX", report.OffsetRX, true);
            AppendNumber(builder, "offsetRY", report.OffsetRY, true);
            AppendBoolean(builder, "reducedMotion", report.ReducedMotion, true);
            AppendBoolean(builder, "historyPaused", report.HistoryPaused, true);
            AppendNumberArray(builder, "leftTriggerHistory", report.LeftTriggerHistory, true);
            AppendNumberArray(builder, "rightTriggerHistory", report.RightTriggerHistory, false);
            builder.Append("}\n");
            return builder.ToString();
        }

        public static string BuildCsv(ControllerReport report)
        {
            if (report == null) throw new ArgumentNullException("report");
            StringBuilder builder = new StringBuilder();
            builder.Append("项目,值\r\n");
            CsvRow(builder, "生成时间", report.GeneratedAt);
            CsvRow(builder, "设备", report.Controller);
            CsvRow(builder, "连接状态", report.Connected ? "已连接" : "未连接");
            CsvRow(builder, "显示刷新率 Hz", Number(report.DisplayHz));
            CsvRow(builder, "输入采样率 Hz", Number(report.SamplingHz));
            CsvRow(builder, "基础健康已就绪", report.DiagnosticReady ? "是" : "否");
            CsvRow(builder, "基础健康分", report.DiagnosticScore.ToString(CultureInfo.InvariantCulture));
            CsvRow(builder, "基础健康状态", report.DiagnosticStatus);
            CsvRow(builder, "基础健康详情", report.DiagnosticDetail);
            CsvRow(builder, "操作覆盖", report.DiagnosticCoverage.ToString(CultureInfo.InvariantCulture) + "/6");
            CsvRow(builder, "左摇杆中心幅度", Number(report.CenterLeft));
            CsvRow(builder, "右摇杆中心幅度", Number(report.CenterRight));
            CsvRow(builder, "自动体检", report.GuidedStatus);
            CsvRow(builder, "自动体检阶段", report.GuidedStage);
            string[] resultNames = { "连接与采样", "中心基线", "左摇杆行程", "右摇杆行程", "LT / RT 扳机", "14 个按键" };
            for (int i = 0; i < resultNames.Length; i++)
            {
                string value = report.GuidedResults != null && i < report.GuidedResults.Length ? report.GuidedResults[i] : "未运行";
                CsvRow(builder, "体检 - " + resultNames[i], value);
            }
            CsvRow(builder, "左摇杆 X", Number(report.LeftX));
            CsvRow(builder, "左摇杆 Y", Number(report.LeftY));
            CsvRow(builder, "右摇杆 X", Number(report.RightX));
            CsvRow(builder, "右摇杆 Y", Number(report.RightY));
            CsvRow(builder, "LT 当前", Number(report.LeftTrigger));
            CsvRow(builder, "RT 当前", Number(report.RightTrigger));
            CsvRow(builder, "LT 近 5 秒峰值", Number(report.LeftTriggerPeak));
            CsvRow(builder, "RT 近 5 秒峰值", Number(report.RightTriggerPeak));
            CsvRow(builder, "按键位掩码", report.ButtonsHex);
            CsvRow(builder, "左参考死区", Number(report.LeftDeadzone));
            CsvRow(builder, "右参考死区", Number(report.RightDeadzone));
            CsvRow(builder, "中心偏移 LX", Number(report.OffsetLX));
            CsvRow(builder, "中心偏移 LY", Number(report.OffsetLY));
            CsvRow(builder, "中心偏移 RX", Number(report.OffsetRX));
            CsvRow(builder, "中心偏移 RY", Number(report.OffsetRY));
            CsvRow(builder, "减少动态效果", report.ReducedMotion ? "是" : "否");
            CsvRow(builder, "历史曲线暂停", report.HistoryPaused ? "是" : "否");
            CsvRow(builder, "LT 历史", JoinNumbers(report.LeftTriggerHistory));
            CsvRow(builder, "RT 历史", JoinNumbers(report.RightTriggerHistory));
            return builder.ToString();
        }

        private static void AppendName(StringBuilder builder, string name)
        {
            builder.Append("  \"").Append(EscapeJson(name)).Append("\": ");
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            AppendName(builder, name);
            builder.Append('"').Append(EscapeJson(value ?? string.Empty)).Append('"');
            EndProperty(builder, comma);
        }

        private static void AppendBoolean(StringBuilder builder, string name, bool value, bool comma)
        {
            AppendName(builder, name);
            builder.Append(value ? "true" : "false");
            EndProperty(builder, comma);
        }

        private static void AppendInteger(StringBuilder builder, string name, int value, bool comma)
        {
            AppendName(builder, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            EndProperty(builder, comma);
        }

        private static void AppendNumber(StringBuilder builder, string name, double value, bool comma)
        {
            AppendName(builder, name);
            builder.Append(Number(value));
            EndProperty(builder, comma);
        }

        private static void AppendStringArray(StringBuilder builder, string name, string[] values, bool comma)
        {
            AppendName(builder, name);
            builder.Append('[');
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    builder.Append('"').Append(EscapeJson(values[i] ?? string.Empty)).Append('"');
                }
            }
            builder.Append(']');
            EndProperty(builder, comma);
        }

        private static void AppendNumberArray(StringBuilder builder, string name, double[] values, bool comma)
        {
            AppendName(builder, name);
            builder.Append('[');
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    builder.Append(Number(values[i]));
                }
            }
            builder.Append(']');
            EndProperty(builder, comma);
        }

        private static void EndProperty(StringBuilder builder, bool comma)
        {
            if (comma) builder.Append(',');
            builder.Append('\n');
        }

        private static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string JoinNumbers(double[] values)
        {
            if (values == null || values.Length == 0) return string.Empty;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) builder.Append(';');
                builder.Append(Number(values[i]));
            }
            return builder.ToString();
        }

        private static void CsvRow(StringBuilder builder, string name, string value)
        {
            builder.Append(EscapeCsv(name)).Append(',').Append(EscapeCsv(value ?? string.Empty)).Append("\r\n");
        }

        private static string EscapeCsv(string value)
        {
            bool quote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            string escaped = value.Replace("\"", "\"\"");
            return quote ? "\"" + escaped + "\"" : escaped;
        }

        private static string EscapeJson(string value)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\') builder.Append("\\\\");
                else if (c == '"') builder.Append("\\\"");
                else if (c == '\n') builder.Append("\\n");
                else if (c == '\r') builder.Append("\\r");
                else if (c == '\t') builder.Append("\\t");
                else if (c < 32) builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else builder.Append(c);
            }
            return builder.ToString();
        }
    }

    public sealed class ControllerSettings
    {
        public double OffsetLX;
        public double OffsetLY;
        public double OffsetRX;
        public double OffsetRY;
        public double LeftDeadzone = 0.08;
        public double RightDeadzone = 0.08;
        public int ControllerIndex = -1;
        public bool ReducedMotion;
        public string ConnectionMethodOverride = "自动";
        public string WiredUsbRoute;
        public string ReceiverUsbRoute;
        public string ControllerFamily = "Auto";
    }

    public static class SettingsStore
    {
        private static readonly string DirectoryPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxControllerLab");
        private static readonly string FilePath = System.IO.Path.Combine(DirectoryPath, "settings.ini");

        public static ControllerSettings Load()
        {
            ControllerSettings settings = new ControllerSettings();
            try
            {
                if (!File.Exists(FilePath)) return settings;
                string[] lines = File.ReadAllLines(FilePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    int split = lines[i].IndexOf('=');
                    if (split <= 0) continue;
                    string key = lines[i].Substring(0, split).Trim();
                    string raw = lines[i].Substring(split + 1).Trim();
                    if (key == "controllerIndex")
                    {
                        int controllerIndex;
                        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out controllerIndex)) settings.ControllerIndex = Math.Max(-1, Math.Min(3, controllerIndex));
                        continue;
                    }
                    if (key == "reducedMotion")
                    {
                        bool reduced;
                        if (bool.TryParse(raw, out reduced)) settings.ReducedMotion = reduced;
                        continue;
                    }
                    if (key == "connectionMethodOverride")
                    {
                        settings.ConnectionMethodOverride = raw;
                        continue;
                    }
                    if (key == "wiredUsbRoute")
                    {
                        settings.WiredUsbRoute = raw;
                        continue;
                    }
                    if (key == "receiverUsbRoute")
                    {
                        settings.ReceiverUsbRoute = raw;
                        continue;
                    }
                    if (key == "controllerFamily")
                    {
                        settings.ControllerFamily = raw;
                        continue;
                    }
                    double value;
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
                    if (key == "offsetLX") settings.OffsetLX = ClampOffset(value);
                    else if (key == "offsetLY") settings.OffsetLY = ClampOffset(value);
                    else if (key == "offsetRX") settings.OffsetRX = ClampOffset(value);
                    else if (key == "offsetRY") settings.OffsetRY = ClampOffset(value);
                    else if (key == "leftDeadzone") settings.LeftDeadzone = ClampDeadzone(value);
                    else if (key == "rightDeadzone") settings.RightDeadzone = ClampDeadzone(value);
                }
            }
            catch
            {
                return new ControllerSettings();
            }
            return settings;
        }

        public static void Save(ControllerSettings settings)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string[] lines =
                {
                    "version=4",
                    "offsetLX=" + ClampOffset(settings.OffsetLX).ToString("0.###", CultureInfo.InvariantCulture),
                    "offsetLY=" + ClampOffset(settings.OffsetLY).ToString("0.###", CultureInfo.InvariantCulture),
                    "offsetRX=" + ClampOffset(settings.OffsetRX).ToString("0.###", CultureInfo.InvariantCulture),
                    "offsetRY=" + ClampOffset(settings.OffsetRY).ToString("0.###", CultureInfo.InvariantCulture),
                    "leftDeadzone=" + ClampDeadzone(settings.LeftDeadzone).ToString("0.###", CultureInfo.InvariantCulture),
                    "rightDeadzone=" + ClampDeadzone(settings.RightDeadzone).ToString("0.###", CultureInfo.InvariantCulture),
                    "controllerIndex=" + Math.Max(-1, Math.Min(3, settings.ControllerIndex)).ToString(CultureInfo.InvariantCulture),
                    "reducedMotion=" + settings.ReducedMotion.ToString(CultureInfo.InvariantCulture),
                    "connectionMethodOverride=" + (settings.ConnectionMethodOverride ?? "自动"),
                    "wiredUsbRoute=" + (settings.WiredUsbRoute ?? string.Empty),
                    "receiverUsbRoute=" + (settings.ReceiverUsbRoute ?? string.Empty),
                    "controllerFamily=" + (settings.ControllerFamily ?? "Auto")
                };
                File.WriteAllLines(FilePath, lines);
            }
            catch
            {
                // Settings failure must not prevent live monitoring.
            }
        }

        private static double ClampOffset(double value)
        {
            return Math.Max(-32768, Math.Min(32767, value));
        }

        private static double ClampDeadzone(double value)
        {
            return Math.Max(0, Math.Min(0.25, value));
        }
    }

    public sealed class DiagnosticEngine
    {
        private const int RequiredBaselineSamples = 45;
        private double centerLeft;
        private double centerRight;
        private bool hasCenterBaseline;
        private int baselineSamples;
        private double baselineLeftSum;
        private double baselineRightSum;
        private int coverageMask;
        private int score;

        public int Score { get { return score; } }
        public bool IsReady { get { return score >= 0 && hasCenterBaseline; } }
        public bool HasBaseline { get { return hasCenterBaseline; } }
        public double BaselineProgress { get { return Math.Min(1.0, baselineSamples / (double)RequiredBaselineSamples); } }
        public double CenterLeft { get { return centerLeft; } }
        public double CenterRight { get { return centerRight; } }
        public int CoverageMask { get { return coverageMask; } }
        public int CoverageCount { get { return CountBits(coverageMask); } }
        public string Status { get; private set; }
        public string Detail { get; private set; }

        public DiagnosticEngine()
        {
            Reset();
        }

        public void Reset()
        {
            centerLeft = 0;
            centerRight = 0;
            hasCenterBaseline = false;
            baselineSamples = 0;
            baselineLeftSum = 0;
            baselineRightSum = 0;
            coverageMask = 0;
            score = -1;
            Status = "等待手柄";
            Detail = "连接后自动检查中心、采样与操作覆盖";
        }

        public void UseDemoBaseline()
        {
            centerLeft = 0.004;
            centerRight = 0.004;
            hasCenterBaseline = true;
            baselineSamples = RequiredBaselineSamples;
            baselineLeftSum = centerLeft * RequiredBaselineSamples;
            baselineRightSum = centerRight * RequiredBaselineSamples;
            score = -1;
            Status = "评估中";
            Detail = "演示中心基线已准备 · 正在测量刷新率";
        }

        public void Update(InputSnapshot state, double samplingHz, double leftDeadzone, double rightDeadzone)
        {
            if (!state.Connected)
            {
                centerLeft = 0;
                centerRight = 0;
                hasCenterBaseline = false;
                baselineSamples = 0;
                baselineLeftSum = 0;
                baselineRightSum = 0;
                coverageMask = 0;
                score = -1;
                Status = "等待手柄";
                Detail = "连接后自动检查中心、采样与操作覆盖";
                return;
            }

            double leftMagnitude = Math.Min(1.0, Math.Sqrt(state.LeftNormalizedX * state.LeftNormalizedX + state.LeftNormalizedY * state.LeftNormalizedY));
            double rightMagnitude = Math.Min(1.0, Math.Sqrt(state.RightNormalizedX * state.RightNormalizedX + state.RightNormalizedY * state.RightNormalizedY));

            if (leftMagnitude > 0.82) coverageMask |= 1;
            if (rightMagnitude > 0.82) coverageMask |= 2;
            if (state.LeftTrigger > 229) coverageMask |= 4;
            if (state.RightTrigger > 229) coverageMask |= 8;
            if ((state.Buttons & 0xF000) != 0) coverageMask |= 16;
            if ((state.Buttons & 0x000F) != 0) coverageMask |= 32;

            bool stableCenter = leftMagnitude < 0.12 && rightMagnitude < 0.12 && state.LeftTrigger < 14 && state.RightTrigger < 14;
            if (!hasCenterBaseline)
            {
                if (stableCenter)
                {
                    baselineSamples++;
                    baselineLeftSum += leftMagnitude;
                    baselineRightSum += rightMagnitude;
                    if (baselineSamples >= RequiredBaselineSamples)
                    {
                        centerLeft = baselineLeftSum / baselineSamples;
                        centerRight = baselineRightSum / baselineSamples;
                        hasCenterBaseline = true;
                    }
                }
                else
                {
                    baselineSamples = 0;
                    baselineLeftSum = 0;
                    baselineRightSum = 0;
                }

                if (!hasCenterBaseline)
                {
                    score = -1;
                    Status = "评估中";
                    Detail = stableCenter
                        ? string.Format(CultureInfo.InvariantCulture, "正在建立中心基线 {0:0}% · 请保持摇杆居中", BaselineProgress * 100.0)
                        : "请松开摇杆与扳机，以建立中心基线";
                    return;
                }
            }
            else if (leftMagnitude < 0.25 && rightMagnitude < 0.25)
            {
                centerLeft = centerLeft * 0.965 + leftMagnitude * 0.035;
                centerRight = centerRight * 0.965 + rightMagnitude * 0.035;
            }

            if (samplingHz <= 0)
            {
                score = -1;
                Status = "评估中";
                Detail = "中心基线已建立 · 正在测量实际采样率";
                return;
            }

            int penalty = 0;
            if (samplingHz < 120) penalty += (int)Math.Min(20, Math.Round((120 - samplingHz) / 6.0));
            penalty += DriftPenalty(centerLeft, Math.Max(0.02, leftDeadzone));
            penalty += DriftPenalty(centerRight, Math.Max(0.02, rightDeadzone));
            score = Math.Max(0, Math.Min(100, 100 - penalty));
            Status = score >= 90 ? "状态良好" : score >= 75 ? "建议观察" : "需要检查";
            int coverage = CountBits(coverageMask);
            Detail = string.Format(CultureInfo.InvariantCulture, "操作覆盖 {0}/6 · 中心 L {1:0.000} / R {2:0.000}", coverage, centerLeft, centerRight);
        }

        private static int DriftPenalty(double magnitude, double reference)
        {
            if (magnitude <= reference) return 0;
            return (int)Math.Min(25, Math.Round((magnitude - reference) * 180.0));
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }
    }

    public enum GuidedStage
    {
        Idle,
        Center,
        LeftStick,
        RightStick,
        Triggers,
        Buttons,
        Complete
    }

    public sealed class GuidedTestEngine
    {
        public static readonly int[] ButtonMasks =
        {
            0x0001, 0x0002, 0x0004, 0x0008,
            0x0010, 0x0020, 0x0040, 0x0080,
            0x0100, 0x0200,
            0x1000, 0x2000, 0x4000, 0x8000
        };

        public static readonly string[] ButtonNames =
        {
            "上", "下", "左", "右",
            "菜单", "视图", "LS", "RS",
            "LB", "RB", "A", "B", "X", "Y"
        };

        private const int AllButtonsMask = 0xF3FF;
        private DateTime lastUpdate;
        private double centerStableSeconds;
        private int leftDirections;
        private int rightDirections;
        private int triggerMask;
        private int seenButtons;
        private bool connectionPassed;
        private bool centerPassed;
        private bool leftPassed;
        private bool rightPassed;
        private bool triggersPassed;
        private bool buttonsPassed;
        private bool centerSkipped;
        private bool leftSkipped;
        private bool rightSkipped;
        private bool triggersSkipped;
        private bool buttonsSkipped;

        public GuidedStage Stage { get; private set; }
        public bool Active { get { return Stage != GuidedStage.Idle && Stage != GuidedStage.Complete; } }
        public bool IsComplete { get { return Stage == GuidedStage.Complete; } }
        public bool ConnectionPassed { get { return connectionPassed; } }
        public bool CenterPassed { get { return centerPassed; } }
        public bool LeftPassed { get { return leftPassed; } }
        public bool RightPassed { get { return rightPassed; } }
        public bool TriggersPassed { get { return triggersPassed; } }
        public bool ButtonsPassed { get { return buttonsPassed; } }
        public double CenterStableSeconds { get { return centerStableSeconds; } }
        public int LeftDirections { get { return leftDirections; } }
        public int RightDirections { get { return rightDirections; } }
        public int TriggerMask { get { return triggerMask; } }
        public int SeenButtons { get { return seenButtons; } }
        public int ButtonCount { get { return CountBits(seenButtons & AllButtonsMask); } }
        public bool HasSkipped { get { return centerSkipped || leftSkipped || rightSkipped || triggersSkipped || buttonsSkipped; } }

        public GuidedTestEngine()
        {
            Stage = GuidedStage.Idle;
        }

        public void Begin()
        {
            lastUpdate = DateTime.UtcNow;
            centerStableSeconds = 0;
            leftDirections = 0;
            rightDirections = 0;
            triggerMask = 0;
            seenButtons = 0;
            connectionPassed = false;
            centerPassed = false;
            leftPassed = false;
            rightPassed = false;
            triggersPassed = false;
            buttonsPassed = false;
            centerSkipped = false;
            leftSkipped = false;
            rightSkipped = false;
            triggersSkipped = false;
            buttonsSkipped = false;
            Stage = GuidedStage.Center;
        }

        public void Update(InputSnapshot state, double samplingHz)
        {
            if (!Active) return;
            DateTime now = DateTime.UtcNow;
            double elapsed = Math.Max(0, Math.Min(0.1, (now - lastUpdate).TotalSeconds));
            lastUpdate = now;
            if (!state.Connected)
            {
                centerStableSeconds = 0;
                return;
            }

            if (samplingHz >= 60) connectionPassed = true;
            double lx = state.LeftNormalizedX;
            double ly = state.LeftNormalizedY;
            double rx = state.RightNormalizedX;
            double ry = state.RightNormalizedY;
            if (Stage == GuidedStage.LeftStick)
            {
                if (lx > 0.75) leftDirections |= 1;
                if (lx < -0.75) leftDirections |= 2;
                if (ly > 0.75) leftDirections |= 4;
                if (ly < -0.75) leftDirections |= 8;
            }
            if (Stage == GuidedStage.RightStick)
            {
                if (rx > 0.75) rightDirections |= 1;
                if (rx < -0.75) rightDirections |= 2;
                if (ry > 0.75) rightDirections |= 4;
                if (ry < -0.75) rightDirections |= 8;
            }
            if (Stage == GuidedStage.Triggers)
            {
                if (state.LeftTrigger >= 230) triggerMask |= 1;
                if (state.RightTrigger >= 230) triggerMask |= 2;
            }
            if (Stage == GuidedStage.Buttons) seenButtons |= state.Buttons & AllButtonsMask;

            if (Stage == GuidedStage.Center)
            {
                double leftMagnitude = Math.Sqrt(lx * lx + ly * ly);
                double rightMagnitude = Math.Sqrt(rx * rx + ry * ry);
                bool stable = leftMagnitude < 0.12 && rightMagnitude < 0.12 && state.LeftTrigger < 14 && state.RightTrigger < 14 && state.Buttons == 0;
                centerStableSeconds = stable ? centerStableSeconds + elapsed : 0;
                if (centerStableSeconds >= 2.0)
                {
                    centerPassed = true;
                    Stage = GuidedStage.LeftStick;
                }
            }
            else if (Stage == GuidedStage.LeftStick && leftDirections == 15)
            {
                leftPassed = true;
                Stage = GuidedStage.RightStick;
            }
            else if (Stage == GuidedStage.RightStick && rightDirections == 15)
            {
                rightPassed = true;
                Stage = GuidedStage.Triggers;
            }
            else if (Stage == GuidedStage.Triggers && triggerMask == 3)
            {
                triggersPassed = true;
                Stage = GuidedStage.Buttons;
            }
            else if (Stage == GuidedStage.Buttons && (seenButtons & AllButtonsMask) == AllButtonsMask)
            {
                buttonsPassed = true;
                Stage = GuidedStage.Complete;
            }
        }

        public void SkipCurrent()
        {
            if (Stage == GuidedStage.Center)
            {
                centerSkipped = true;
                Stage = GuidedStage.LeftStick;
            }
            else if (Stage == GuidedStage.LeftStick)
            {
                leftSkipped = true;
                Stage = GuidedStage.RightStick;
            }
            else if (Stage == GuidedStage.RightStick)
            {
                rightSkipped = true;
                Stage = GuidedStage.Triggers;
            }
            else if (Stage == GuidedStage.Triggers)
            {
                triggersSkipped = true;
                Stage = GuidedStage.Buttons;
            }
            else if (Stage == GuidedStage.Buttons)
            {
                buttonsSkipped = true;
                Stage = GuidedStage.Complete;
            }
        }

        public void Cancel()
        {
            Stage = GuidedStage.Idle;
            lastUpdate = DateTime.UtcNow;
        }

        public int StepNumber
        {
            get
            {
                if (Stage == GuidedStage.Center) return 1;
                if (Stage == GuidedStage.LeftStick) return 2;
                if (Stage == GuidedStage.RightStick) return 3;
                if (Stage == GuidedStage.Triggers) return 4;
                return 5;
            }
        }

        public double Progress
        {
            get
            {
                if (Stage == GuidedStage.Center) return Math.Min(1.0, centerStableSeconds / 2.0);
                if (Stage == GuidedStage.LeftStick) return CountBits(leftDirections) / 4.0;
                if (Stage == GuidedStage.RightStick) return CountBits(rightDirections) / 4.0;
                if (Stage == GuidedStage.Triggers) return CountBits(triggerMask) / 2.0;
                if (Stage == GuidedStage.Buttons) return ButtonCount / 14.0;
                if (Stage == GuidedStage.Complete) return 1.0;
                return 0;
            }
        }

        public string StageTitle
        {
            get
            {
                if (Stage == GuidedStage.Center) return "第 1 步 · 中心基线";
                if (Stage == GuidedStage.LeftStick) return "第 2 步 · 左摇杆行程";
                if (Stage == GuidedStage.RightStick) return "第 3 步 · 右摇杆行程";
                if (Stage == GuidedStage.Triggers) return "第 4 步 · 扳机行程";
                if (Stage == GuidedStage.Buttons) return "第 5 步 · 按键覆盖";
                return "体检完成";
            }
        }

        public string Instruction
        {
            get
            {
                if (Stage == GuidedStage.Center) return "松开所有按键，并保持两个摇杆居中";
                if (Stage == GuidedStage.LeftStick) return "将左摇杆依次推到上、下、左、右边缘";
                if (Stage == GuidedStage.RightStick) return "将右摇杆依次推到上、下、左、右边缘";
                if (Stage == GuidedStage.Triggers) return "分别将 LT 与 RT 扣到底";
                if (Stage == GuidedStage.Buttons) return "按下清单中的每一个按键";
                return HasSkipped ? "体检已完成，部分项目尚未验证" : "全部项目均已验证通过";
            }
        }

        public string Detail
        {
            get
            {
                if (Stage == GuidedStage.Center) return string.Format(CultureInfo.InvariantCulture, "稳定保持 2 秒；当前 {0:0.0} 秒，检测到移动会重新计时。", centerStableSeconds);
                if (Stage == GuidedStage.LeftStick) return string.Format(CultureInfo.InvariantCulture, "已识别 {0}/4 个方向；越过 75% 行程即记录。", CountBits(leftDirections));
                if (Stage == GuidedStage.RightStick) return string.Format(CultureInfo.InvariantCulture, "已识别 {0}/4 个方向；越过 75% 行程即记录。", CountBits(rightDirections));
                if (Stage == GuidedStage.Triggers) return string.Format(CultureInfo.InvariantCulture, "已验证 {0}/2 个扳机；需达到 90% 行程。", CountBits(triggerMask));
                if (Stage == GuidedStage.Buttons) return string.Format(CultureInfo.InvariantCulture, "已识别 {0}/14 个按键；Xbox Guide 键不纳入测试。", ButtonCount);
                return HasSkipped ? "可重新测试未完成项目，或导出当前结果。" : "结果可导出为 JSON 或 CSV，便于留档和复测。";
            }
        }

        public string ResultText(int index)
        {
            if (index == 0)
            {
                if (connectionPassed) return "通过";
                return Stage == GuidedStage.Complete ? "未完成" : "测量中";
            }
            bool passed = index == 1 ? centerPassed : index == 2 ? leftPassed : index == 3 ? rightPassed : index == 4 ? triggersPassed : buttonsPassed;
            bool skipped = index == 1 ? centerSkipped : index == 2 ? leftSkipped : index == 3 ? rightSkipped : index == 4 ? triggersSkipped : buttonsSkipped;
            if (passed) return "通过";
            if (skipped) return "已跳过";
            return Stage == GuidedStage.Complete ? "未完成" : "待测试";
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }
    }

    public sealed class DualSenseTouchDebugWindow : Window
    {
        private readonly Func<InputSnapshot> readSnapshot;
        private readonly Func<DualSenseTouchPoint, Point> mapPoint;
        private readonly Action<bool> setRawLogging;
        private readonly Func<bool> getRawLogging;
        private readonly TextBlock details;
        private readonly DispatcherTimer timer;
        private readonly CheckBox rawLogging;

        public DualSenseTouchDebugWindow(Func<InputSnapshot> readSnapshot, Func<DualSenseTouchPoint, Point> mapPoint, Action<bool> setRawLogging, Func<bool> getRawLogging)
        {
            this.readSnapshot = readSnapshot;
            this.mapPoint = mapPoint;
            this.setRawLogging = setRawLogging;
            this.getRawLogging = getRawLogging;
            Title = "DS5 触摸调试";
            Width = 590;
            Height = 500;
            MinWidth = 520;
            MinHeight = 430;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Palette.WindowBrush;
            Foreground = Palette.TextBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            Grid root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            root.RowDefinitions.Add(new RowDefinition());
            TextBlock title = new TextBlock { Text = "DualSense 原生 HID 触点", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Palette.TextBrush };
            root.Children.Add(title);
            rawLogging = new CheckBox { Content = "启用原始触摸数据日志（Debug 输出）", IsChecked = getRawLogging != null && getRawLogging(), Foreground = Palette.MutedBrush, FontSize = 12, Margin = new Thickness(0, 28, 0, 0) };
            rawLogging.Checked += delegate { if (setRawLogging != null) setRawLogging(true); };
            rawLogging.Unchecked += delegate { if (setRawLogging != null) setRawLogging(false); };
            root.Children.Add(rawLogging);
            Border card = new Border { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(14) };
            details = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = Palette.TextBrush, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
            card.Child = details;
            Grid.SetRow(card, 2);
            root.Children.Add(card);
            Content = root;

            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += delegate { Refresh(); };
            Loaded += delegate { Refresh(); timer.Start(); };
            Closed += delegate { timer.Stop(); };
        }

        private void Refresh()
        {
            InputSnapshot state = readSnapshot == null ? null : readSnapshot();
            if (state == null || state.Family != ControllerFamily.PlayStation)
            {
                details.Text = "等待 DualSense 原生 HID 输入。\n\n未检测到 DS5 时，程序不会使用鼠标、XInput 或动态演示伪造触点。";
                return;
            }
            DualSenseTouchDebugInfo info = state.TouchDebug;
            StringBuilder text = new StringBuilder();
            text.AppendLine("HID 连接方式: " + (info == null ? state.ConnectionMethod : info.ConnectionMethod));
            text.AppendLine("设备身份: " + (info == null ? "-" : info.DeviceIdentity));
            text.AppendLine("报告: " + (info == null ? "等待原始 HID 报文" : string.Format(CultureInfo.InvariantCulture, "0x{0:X2}, {1} bytes, {2}", info.ReportId, info.ReportLength, info.Layout)));
            text.AppendLine("触点偏移: " + (info == null ? "-" : info.TouchOffset.ToString(CultureInfo.InvariantCulture)) + "    蓝牙 CRC: " + (info == null ? "-" : (info.CrcValidated ? "通过/不适用" : "失败")));
            text.AppendLine("触摸坐标: " + (state.TouchCoordinatesAvailable ? "可用（真实 HID）" : (info == null ? "不可用" : info.AvailabilityMessage)));
            text.AppendLine("更新率: " + (info == null ? "0" : info.UpdatesPerSecond.ToString("0.0", CultureInfo.InvariantCulture)) + " Hz");
            text.AppendLine("原始触点字节: " + (info == null || info.RawTouchBytes == null ? "-" : BitConverter.ToString(info.RawTouchBytes)));
            AppendPoint(text, "触点 1", state.TouchPoint1);
            AppendPoint(text, "触点 2", state.TouchPoint2);
            details.Text = text.ToString();
        }

        private void AppendPoint(StringBuilder text, string label, DualSenseTouchPoint point)
        {
            if (point == null)
            {
                text.AppendLine(label + ": 无");
                return;
            }
            Point mapped = mapPoint == null ? new Point(double.NaN, double.NaN) : mapPoint(point);
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: active={1}, id={2}, raw=({3},{4}), normalized=({5:0.000},{6:0.000}), stage=({7:0.0},{8:0.0})", label, point.IsActive, point.Id, point.RawX, point.RawY, point.X, point.Y, mapped.X, mapped.Y));
        }
    }

    public sealed class DualSenseTouchPoint
    {
        // The controller-assigned contact id is stable across report ordering. Raw coordinates are
        // preserved for diagnostics while X/Y remain normalized for every visual consumer.
        public int Id;
        public bool IsActive;
        public double X;
        public double Y;
        public byte RawId;
        public int RawX;
        public int RawY;

        public DualSenseTouchPoint Copy()
        {
            return new DualSenseTouchPoint { Id = Id, IsActive = IsActive, X = X, Y = Y, RawId = RawId, RawX = RawX, RawY = RawY };
        }
    }

    public sealed class DualSenseTouchDebugInfo
    {
        public string DeviceIdentity;
        public string ConnectionMethod;
        public byte ReportId;
        public int ReportLength;
        public int TouchOffset;
        public string Layout;
        public bool CrcValidated;
        public bool CoordinatesAvailable;
        public string AvailabilityMessage;
        public byte[] RawTouchBytes;
        public double UpdatesPerSecond;
    }

    public sealed class InputSnapshot
    {
        // Stable identity and timestamp make raw input interchangeable at the UI boundary.
        public string DeviceId;
        public DateTime TimestampUtc;
        public bool Connected;
        public ControllerFamily Family = ControllerFamily.Xbox;
        public string DeviceName = "Xbox 无线手柄";
        public string InputBackend = "XInput";
        public int Index;
        public uint Packet;
        public ushort Buttons;
        public int LeftTrigger;
        public int RightTrigger;
        public int LeftX;
        public int LeftY;
        public int RightX;
        public int RightY;
        public string Battery = "—";
        public int BatteryPercent = -1;
        public bool BatteryTelemetryUnavailable;
        public string ConnectionMethod = "检测中";
        public bool ConnectionIsWireless;
        public bool TouchpadPressed;
        public bool MicrophoneMuted;
        public double GyroscopeX;
        public double GyroscopeY;
        public double GyroscopeZ;
        public double AccelerometerX;
        public double AccelerometerY;
        public double AccelerometerZ;
        public MotionSample Motion;
        public string LightbarState;
        // True DualSense touch data is supplied only by a validated native HID report. XInput and demo
        // paths intentionally leave these fields empty instead of inventing input.
        public bool TouchCoordinatesAvailable;
        public bool HasTouchCoordinates;
        public DualSenseTouchPoint TouchPoint1;
        public DualSenseTouchPoint TouchPoint2;
        public long TouchReportSequence;
        public DateTime TouchReportUtc;
        public DualSenseTouchDebugInfo TouchDebug;

        public double LeftNormalizedX { get { return Normalize(LeftX); } }
        public double LeftNormalizedY { get { return Normalize(LeftY); } }
        public double RightNormalizedX { get { return Normalize(RightX); } }
        public double RightNormalizedY { get { return Normalize(RightY); } }

        public InputSnapshot WithOffsets(double lx, double ly, double rx, double ry)
        {
            return new InputSnapshot
            {
                DeviceId = DeviceId,
                TimestampUtc = TimestampUtc,
                Connected = Connected,
                Family = Family,
                DeviceName = DeviceName,
                InputBackend = InputBackend,
                Index = Index,
                Packet = Packet,
                Buttons = Buttons,
                LeftTrigger = LeftTrigger,
                RightTrigger = RightTrigger,
                LeftX = ClampShort(LeftX - lx),
                LeftY = ClampShort(LeftY - ly),
                RightX = ClampShort(RightX - rx),
                RightY = ClampShort(RightY - ry),
                Battery = Battery,
                BatteryPercent = BatteryPercent,
                BatteryTelemetryUnavailable = BatteryTelemetryUnavailable,
                ConnectionMethod = ConnectionMethod,
                ConnectionIsWireless = ConnectionIsWireless,
                TouchpadPressed = TouchpadPressed,
                MicrophoneMuted = MicrophoneMuted,
                GyroscopeX = GyroscopeX,
                GyroscopeY = GyroscopeY,
                GyroscopeZ = GyroscopeZ,
                AccelerometerX = AccelerometerX,
                AccelerometerY = AccelerometerY,
                AccelerometerZ = AccelerometerZ,
                Motion = Motion == null ? null : Motion.Copy(),
                LightbarState = LightbarState,
                TouchCoordinatesAvailable = TouchCoordinatesAvailable,
                HasTouchCoordinates = HasTouchCoordinates,
                TouchPoint1 = TouchPoint1 == null ? null : TouchPoint1.Copy(),
                TouchPoint2 = TouchPoint2 == null ? null : TouchPoint2.Copy(),
                TouchReportSequence = TouchReportSequence,
                TouchReportUtc = TouchReportUtc,
                TouchDebug = TouchDebug
            };
        }

        private static int ClampShort(double value)
        {
            return (int)Math.Max(-32768, Math.Min(32767, Math.Round(value)));
        }

        public static double Normalize(int value)
        {
            return Math.Max(-1.0, Math.Min(1.0, value < 0 ? value / 32768.0 : value / 32767.0));
        }

        public static InputSnapshot CreateDemo()
        {
            double t = (DateTime.UtcNow.Ticks % TimeSpan.TicksPerMinute) / (double)TimeSpan.TicksPerSecond;
            double lx = Math.Cos(t * 0.72 + 2.2) * 0.58;
            double ly = Math.Sin(t * 0.72 + 2.2) * 0.58;
            double rx = Math.Cos(t * 0.94 - 0.45) * 0.31;
            double ry = Math.Sin(t * 0.94 - 0.45) * 0.22;
            int lt = (int)((Math.Sin(t * 0.43 + 2.1) * 0.5 + 0.5) * 175);
            int rt = (int)((Math.Sin(t * 0.56) * 0.5 + 0.5) * 205);
            int phase = ((int)(t * 1.7)) % 18;
            ushort buttons = phase == 1 ? (ushort)0x1000 :
                phase == 3 ? (ushort)0x4000 :
                phase == 5 ? (ushort)0x0200 :
                phase == 7 ? (ushort)0x0001 :
                phase == 8 ? (ushort)0x0008 :
                phase == 9 ? (ushort)0x0002 :
                phase == 10 ? (ushort)0x0004 :
                phase == 11 ? (ushort)0x0009 :
                phase == 12 ? (ushort)0x0006 :
                phase == 14 ? (ushort)0x0040 :
                phase == 16 ? (ushort)0x2000 :
                phase == 17 ? (ushort)0x8000 : (ushort)0;
            return new InputSnapshot
            {
                DeviceId = "demo:xbox:0",
                TimestampUtc = DateTime.UtcNow,
                Connected = true,
                Family = ControllerFamily.Xbox,
                DeviceName = "Xbox 无线手柄",
                InputBackend = "动态演示",
                Index = 0,
                Packet = (uint)(t * 125),
                Buttons = buttons,
                LeftTrigger = lt,
                RightTrigger = rt,
                LeftX = (int)(lx * 32767),
                LeftY = (int)(ly * 32767),
                RightX = (int)(rx * 32767),
                RightY = (int)(ry * 32767),
                Battery = "满电",
                BatteryPercent = 100,
                ConnectionMethod = "动态演示",
                ConnectionIsWireless = true
            };
        }

        public static InputSnapshot CreateSonyDemo()
        {
            double t = (DateTime.UtcNow.Ticks % TimeSpan.TicksPerMinute) / (double)TimeSpan.TicksPerSecond;
            double lx = Math.Cos(t * 0.70 + 2.1) * 0.72;
            double ly = Math.Sin(t * 0.70 + 2.1) * 0.68;
            double rx = Math.Cos(t * 0.98 - 0.35) * 0.58;
            double ry = Math.Sin(t * 0.98 - 0.35) * 0.54;
            int phase = ((int)(t * 1.8)) % 24;
            ushort buttons = phase == 1 ? (ushort)0x1000 :
                phase == 2 ? (ushort)0x2000 :
                phase == 3 ? (ushort)0x4000 :
                phase == 4 ? (ushort)0x8000 :
                phase == 5 ? (ushort)0x0001 :
                phase == 6 ? (ushort)0x0008 :
                phase == 7 ? (ushort)0x0002 :
                phase == 8 ? (ushort)0x0004 :
                phase == 9 ? (ushort)0x0009 :
                phase == 10 ? (ushort)0x0006 :
                phase == 11 ? (ushort)0x0040 :
                phase == 12 ? (ushort)0x0080 :
                phase == 13 ? (ushort)0x0800 :
                phase == 14 ? (ushort)0x0400 :
                phase == 16 ? (ushort)0x0100 :
                phase == 17 ? (ushort)0x0200 :
                phase == 18 ? (ushort)0x0020 :
                phase == 19 ? (ushort)0x0010 : (ushort)0;
            return new InputSnapshot
            {
                DeviceId = "demo:dualsense:0",
                TimestampUtc = DateTime.UtcNow,
                Connected = true,
                Family = ControllerFamily.PlayStation,
                DeviceName = "DualSense 无线控制器",
                InputBackend = "动态演示",
                Index = 0,
                Packet = (uint)(t * 125),
                Buttons = buttons,
                LeftTrigger = (int)((Math.Sin(t * 0.49 + 1.8) * 0.5 + 0.5) * 255),
                RightTrigger = (int)((Math.Sin(t * 0.61) * 0.5 + 0.5) * 255),
                LeftX = (int)(lx * 32767),
                LeftY = (int)(ly * 32767),
                RightX = (int)(rx * 32767),
                RightY = (int)(ry * 32767),
                Battery = "满电",
                BatteryPercent = 100,
                ConnectionMethod = "动态演示",
                ConnectionIsWireless = true,
                TouchpadPressed = phase == 13,
                MicrophoneMuted = phase == 15
            };
        }
    }

    public sealed class InputManager : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short LeftX;
            public short LeftY;
            public short RightX;
            public short RightY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint PacketNumber;
            public XInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputBattery
        {
            public byte Type;
            public byte Level;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputVibration
        {
            public ushort LeftMotorSpeed;
            public ushort RightMotorSpeed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputCapabilities
        {
            public byte Type;
            public byte SubType;
            public ushort Flags;
            public XInputGamepad Gamepad;
            public XInputVibration Vibration;
        }

        private struct BatteryReading
        {
            public string Label;
            public int ApproxPercent;
            public bool TelemetryUnavailable;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetStateDelegate(uint index, out XInputState state);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetBatteryDelegate(uint index, byte deviceType, out XInputBattery battery);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetCapabilitiesDelegate(uint index, uint flags, out XInputCapabilities capabilities);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr module);

        private IntPtr module;
        private GetStateDelegate getState;
        private GetBatteryDelegate getBattery;
        private GetCapabilitiesDelegate getCapabilities;
        private readonly BatteryReading[] cachedBattery =
        {
            new BatteryReading { Label = "—", ApproxPercent = -1 },
            new BatteryReading { Label = "—", ApproxPercent = -1 },
            new BatteryReading { Label = "—", ApproxPercent = -1 },
            new BatteryReading { Label = "—", ApproxPercent = -1 }
        };
        private readonly DateTime[] batteryCheckedAt = { DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue };
        private readonly bool[] cachedWireless = { false, false, false, false };
        private readonly DateTime[] wirelessCheckedAt = { DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue };
        private string cachedConnectionMethod = "检测中";
        private DateTime connectionMethodCheckedAt = DateTime.MinValue;
        private readonly object rawInputLock = new object();
        private string lastActiveRawDevicePath;
        private DateTime lastActiveRawDeviceAt = DateTime.MinValue;
        private string wiredUsbRoute;
        private string receiverUsbRoute;
        private string currentUsbRoute;
        public string LibraryName { get; private set; }

        public string WiredUsbRoute
        {
            get { lock (rawInputLock) return wiredUsbRoute; }
        }

        public string ReceiverUsbRoute
        {
            get { lock (rawInputLock) return receiverUsbRoute; }
        }

        public InputManager()
        {
            string[] libraries = { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" };
            for (int i = 0; i < libraries.Length; i++)
            {
                module = LoadLibrary(libraries[i]);
                if (module == IntPtr.Zero) continue;
                IntPtr statePtr = GetProcAddress(module, "XInputGetState");
                if (statePtr != IntPtr.Zero)
                {
                    getState = (GetStateDelegate)Marshal.GetDelegateForFunctionPointer(statePtr, typeof(GetStateDelegate));
                    IntPtr batteryPtr = GetProcAddress(module, "XInputGetBatteryInformation");
                    if (batteryPtr != IntPtr.Zero) getBattery = (GetBatteryDelegate)Marshal.GetDelegateForFunctionPointer(batteryPtr, typeof(GetBatteryDelegate));
                    IntPtr capabilitiesPtr = GetProcAddress(module, "XInputGetCapabilities");
                    if (capabilitiesPtr != IntPtr.Zero) getCapabilities = (GetCapabilitiesDelegate)Marshal.GetDelegateForFunctionPointer(capabilitiesPtr, typeof(GetCapabilitiesDelegate));
                    LibraryName = libraries[i].Replace(".dll", "");
                    break;
                }
                FreeLibrary(module);
                module = IntPtr.Zero;
            }
            if (getState == null) LibraryName = "XInput 不可用";
        }

        public InputSnapshot Read(int preferredIndex)
        {
            if (getState == null) return new InputSnapshot { Index = Math.Max(0, preferredIndex) };
            if (preferredIndex >= 0 && preferredIndex < 4)
            {
                InputSnapshot selected = ReadIndex((uint)preferredIndex);
                return selected ?? new InputSnapshot { Index = preferredIndex };
            }
            for (uint index = 0; index < 4; index++)
            {
                InputSnapshot found = ReadIndex(index);
                if (found != null) return found;
            }
            return new InputSnapshot();
        }

        public InputSnapshot ReadFirst()
        {
            return Read(-1);
        }

        // A catalog consumer needs every online XInput slot, whereas the legacy Read
        // method intentionally returns only the current preferred controller.
        public IList<InputSnapshot> ReadAll()
        {
            List<InputSnapshot> result = new List<InputSnapshot>();
            if (getState == null) return result;
            for (uint index = 0; index < 4; index++)
            {
                InputSnapshot state = ReadIndex(index);
                if (state != null) result.Add(state);
            }
            return result;
        }

        private InputSnapshot ReadIndex(uint index)
        {
            XInputState state;
            if (getState(index, out state) != 0) return null;
            BatteryReading battery = ReadBatteryCached(index);
            bool wireless = ReadWirelessCapability(index);
            return new InputSnapshot
            {
                DeviceId = "xinput:" + index.ToString(CultureInfo.InvariantCulture),
                TimestampUtc = DateTime.UtcNow,
                Connected = true,
                Family = ControllerFamily.Xbox,
                DeviceName = "Xbox 无线手柄",
                InputBackend = LibraryName,
                Index = (int)index,
                Packet = state.PacketNumber,
                Buttons = state.Gamepad.Buttons,
                LeftTrigger = state.Gamepad.LeftTrigger,
                RightTrigger = state.Gamepad.RightTrigger,
                LeftX = state.Gamepad.LeftX,
                LeftY = state.Gamepad.LeftY,
                RightX = state.Gamepad.RightX,
                RightY = state.Gamepad.RightY,
                Battery = battery.Label,
                BatteryPercent = battery.ApproxPercent,
                BatteryTelemetryUnavailable = battery.TelemetryUnavailable,
                ConnectionMethod = ReadConnectionMethod(wireless),
                ConnectionIsWireless = wireless
            };
        }

        private bool ReadWirelessCapability(uint index)
        {
            int slot = (int)index;
            if ((DateTime.UtcNow - wirelessCheckedAt[slot]).TotalSeconds < 1.0) return cachedWireless[slot];
            wirelessCheckedAt[slot] = DateTime.UtcNow;
            if (getCapabilities == null) return cachedWireless[slot];
            XInputCapabilities capabilities;
            if (getCapabilities(index, 0, out capabilities) != 0) return cachedWireless[slot];
            cachedWireless[slot] = (capabilities.Flags & 0x0002) != 0; // XINPUT_CAPS_WIRELESS
            return cachedWireless[slot];
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDeviceList
        {
            public IntPtr Device;
            public uint Type;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList([In, Out] RawInputDeviceList[] list, ref uint count, uint size);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder data, ref uint size);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNode(out uint devInst, string deviceId, int flags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Parent(out uint parentDevInst, uint devInst, int flags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Device_ID_Size(out uint length, uint devInst, int flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_ID(uint devInst, StringBuilder buffer, uint bufferLength, int flags);

        public void SetUsbRouteProfiles(string wiredRoute, string receiverRoute)
        {
            lock (rawInputLock)
            {
                wiredUsbRoute = NormalizeRouteKey(wiredRoute);
                receiverUsbRoute = NormalizeRouteKey(receiverRoute);
                connectionMethodCheckedAt = DateTime.MinValue;
            }
        }

        public bool MarkCurrentUsbRoute(string mode)
        {
            lock (rawInputLock)
            {
                if (string.IsNullOrEmpty(currentUsbRoute)) return false;
                if (mode == "有线")
                {
                    wiredUsbRoute = currentUsbRoute;
                    if (string.Equals(receiverUsbRoute, currentUsbRoute, StringComparison.OrdinalIgnoreCase)) receiverUsbRoute = null;
                }
                else if (mode == "USB 2.4G")
                {
                    receiverUsbRoute = currentUsbRoute;
                    if (string.Equals(wiredUsbRoute, currentUsbRoute, StringComparison.OrdinalIgnoreCase)) wiredUsbRoute = null;
                }
                else return false;
                connectionMethodCheckedAt = DateTime.MinValue;
                return true;
            }
        }

        private string ReadConnectionMethod(bool isWireless)
        {
            if ((DateTime.UtcNow - connectionMethodCheckedAt).TotalSeconds < 1.0) return cachedConnectionMethod;
            connectionMethodCheckedAt = DateTime.UtcNow;
            try
            {
                // Bluetooth XInput HID stays enumerable even when the most recent WM_INPUT packet belongs to an
                // older USB route.  Give a live Bluetooth XInput endpoint priority over that short-lived cache.
                if (isWireless && FindBluetoothXInputRawDevice())
                {
                    cachedConnectionMethod = "蓝牙";
                    return cachedConnectionMethod;
                }
                string rawPath = GetRecentActiveRawDevice();
                if (string.IsNullOrEmpty(rawPath)) rawPath = FindActiveXInputRawDevice();
                if (string.IsNullOrEmpty(rawPath))
                {
                    cachedConnectionMethod = "检测中";
                    return cachedConnectionMethod;
                }
                string deviceId = ToDeviceInstanceId(rawPath);
                string current = deviceId;
                bool foundUsbTransport = false;
                for (int i = 0; i < 6 && !string.IsNullOrEmpty(current); i++)
                {
                    string upper = current.ToUpperInvariant();
                    if (IsBluetoothTransportNode(current))
                    {
                        cachedConnectionMethod = "蓝牙";
                        return cachedConnectionMethod;
                    }
                    if (upper.StartsWith("USB\\")) foundUsbTransport = true;
                    current = GetParentDeviceInstanceId(current);
                }
                string routeKey = foundUsbTransport ? NormalizeRouteKey(deviceId) : null;
                string wiredRoute;
                string receiverRoute;
                lock (rawInputLock)
                {
                    currentUsbRoute = routeKey;
                    wiredRoute = wiredUsbRoute;
                    receiverRoute = receiverUsbRoute;
                }
                if (!string.IsNullOrEmpty(routeKey) && string.Equals(routeKey, wiredRoute, StringComparison.OrdinalIgnoreCase))
                {
                    cachedConnectionMethod = "有线";
                    return cachedConnectionMethod;
                }
                if (!string.IsNullOrEmpty(routeKey) && string.Equals(routeKey, receiverRoute, StringComparison.OrdinalIgnoreCase))
                {
                    cachedConnectionMethod = "USB 2.4G 接收器";
                    return cachedConnectionMethod;
                }
                // XInput wireless capability is not a reliable transport discriminator for this controller's
                // emulated XInput endpoint.  An unknown USB path remains explicitly unclassified.
                cachedConnectionMethod = foundUsbTransport ? "USB 通道（待标记）" : "检测中";
                return cachedConnectionMethod;
            }
            catch
            {
                cachedConnectionMethod = "检测中";
                return cachedConnectionMethod;
            }
        }

        public void ObserveRawInputDevicePath(string rawPath)
        {
            // WM_INPUT is registered only for generic-desktop Gamepad/Joystick usages.  Bluetooth HID paths do not
            // consistently carry the XInput "&IG_" marker, so keeping that old filter made us reuse a stale USB path.
            if (string.IsNullOrEmpty(rawPath)) return;
            lock (rawInputLock)
            {
                lastActiveRawDevicePath = rawPath;
                lastActiveRawDeviceAt = DateTime.UtcNow;
                connectionMethodCheckedAt = DateTime.MinValue;
            }
        }

        private string GetRecentActiveRawDevice()
        {
            lock (rawInputLock)
            {
                return (DateTime.UtcNow - lastActiveRawDeviceAt).TotalSeconds < 12.0 ? lastActiveRawDevicePath : null;
            }
        }

        private static string FindActiveXInputRawDevice()
        {
            uint count = 0;
            uint size = (uint)Marshal.SizeOf(typeof(RawInputDeviceList));
            if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue || count == 0) return null;
            RawInputDeviceList[] devices = new RawInputDeviceList[count];
            if (GetRawInputDeviceList(devices, ref count, size) == uint.MaxValue) return null;
            for (int i = 0; i < count; i++)
            {
                // RIM_TYPEHID = 2; XInput HID interfaces carry the &IG_ marker in their device path.
                if (devices[i].Type != 2) continue;
                string value = GetRawDevicePath(devices[i].Device);
                if (string.IsNullOrEmpty(value)) continue;
                if (value.IndexOf("&IG_", StringComparison.OrdinalIgnoreCase) >= 0) return value;
            }
            return null;
        }

        private static bool FindBluetoothXInputRawDevice()
        {
            uint count = 0;
            uint size = (uint)Marshal.SizeOf(typeof(RawInputDeviceList));
            if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue || count == 0) return false;
            RawInputDeviceList[] devices = new RawInputDeviceList[count];
            if (GetRawInputDeviceList(devices, ref count, size) == uint.MaxValue) return false;
            for (int i = 0; i < count; i++)
            {
                if (devices[i].Type != 2) continue;
                string rawPath = GetRawDevicePath(devices[i].Device);
                if (string.IsNullOrEmpty(rawPath) || rawPath.IndexOf("&IG_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string current = ToDeviceInstanceId(rawPath);
                for (int depth = 0; depth < 6 && !string.IsNullOrEmpty(current); depth++)
                {
                    if (IsBluetoothTransportNode(current)) return true;
                    current = GetParentDeviceInstanceId(current);
                }
            }
            return false;
        }

        public static string GetRawDevicePath(IntPtr device)
        {
            if (device == IntPtr.Zero) return null;
            uint chars = 0;
            GetRawInputDeviceInfo(device, 0x20000007, null, ref chars);
            if (chars == 0) return null;
            StringBuilder path = new StringBuilder((int)chars + 1);
            if (GetRawInputDeviceInfo(device, 0x20000007, path, ref chars) == uint.MaxValue) return null;
            return path.ToString();
        }

        // Discovery uses the same Raw Input device table as live reports, but does
        // not require a button press. This keeps a connected DualSense visible on
        // the home page while it is idle.
        public static IList<string> EnumerateRawHidDevicePaths()
        {
            List<string> result = new List<string>();
            uint count = 0;
            uint size = (uint)Marshal.SizeOf(typeof(RawInputDeviceList));
            if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue || count == 0) return result;
            RawInputDeviceList[] devices = new RawInputDeviceList[count];
            if (GetRawInputDeviceList(devices, ref count, size) == uint.MaxValue) return result;
            for (int i = 0; i < count; i++)
            {
                if (devices[i].Type != 2) continue;
                string path = GetRawDevicePath(devices[i].Device);
                if (!string.IsNullOrEmpty(path)) result.Add(path);
            }
            return result;
        }

        private static string ToDeviceInstanceId(string rawPath)
        {
            string value = rawPath;
            if (value.StartsWith("\\\\?\\", StringComparison.Ordinal)) value = value.Substring(4);
            // Bluetooth HID instance IDs can begin with a service GUID (for example HID#{00001124...}).
            // The Raw Input interface GUID is the final "#{...}" component, so only remove that last component.
            int guidStart = value.LastIndexOf("#{", StringComparison.Ordinal);
            if (guidStart >= 0) value = value.Substring(0, guidStart);
            return value.Replace('#', '\\');
        }

        private static string NormalizeRouteKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        private static string GetParentDeviceInstanceId(string deviceId)
        {
            uint devInst;
            if (CM_Locate_DevNode(out devInst, deviceId, 0) != 0) return null;
            uint parent;
            if (CM_Get_Parent(out parent, devInst, 0) != 0) return null;
            uint length;
            if (CM_Get_Device_ID_Size(out length, parent, 0) != 0) return null;
            StringBuilder buffer = new StringBuilder((int)length + 1);
            if (CM_Get_Device_ID(parent, buffer, length + 1, 0) != 0) return null;
            return buffer.ToString();
        }

        private static bool IsBluetoothTransportNode(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            string upper = deviceId.ToUpperInvariant();
            if (upper.StartsWith("BTHLE\\") || upper.StartsWith("BTHENUM\\") || upper.StartsWith("BTH\\")) return true;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + deviceId))
                {
                    if (key == null) return false;
                    string[] names = { "Service", "Class", "ClassGUID", "DeviceDesc", "FriendlyName", "Mfg" };
                    for (int i = 0; i < names.Length; i++)
                    {
                        string value = key.GetValue(names[i]) as string;
                        if (string.IsNullOrEmpty(value)) continue;
                        if (value.IndexOf("BLUETOOTH", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            value.IndexOf("BTH", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    }
                }
            }
            catch
            {
                // Device metadata is an optional signal.  A missing registry value must not interrupt live input.
            }
            return false;
        }

        public static string DescribeRawInputConnection(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath)) return "检测中";
            try
            {
                string current = ToDeviceInstanceId(rawPath);
                bool usb = false;
                for (int depth = 0; depth < 6 && !string.IsNullOrEmpty(current); depth++)
                {
                    if (IsBluetoothTransportNode(current)) return "蓝牙";
                    if (current.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)) usb = true;
                    current = GetParentDeviceInstanceId(current);
                }
                return usb ? "有线" : "原生 HID";
            }
            catch
            {
                return rawPath.IndexOf("BTH", StringComparison.OrdinalIgnoreCase) >= 0 ? "蓝牙" : "原生 HID";
            }
        }

        private BatteryReading ReadBatteryCached(uint index)
        {
            int slot = (int)index;
            if ((DateTime.UtcNow - batteryCheckedAt[slot]).TotalSeconds < 2.0) return cachedBattery[slot];
            batteryCheckedAt[slot] = DateTime.UtcNow;
            cachedBattery[slot] = ReadBattery(index);
            return cachedBattery[slot];
        }

        private BatteryReading ReadBattery(uint index)
        {
            if (getBattery == null) return new BatteryReading { Label = "状态未知", ApproxPercent = -1 };
            XInputBattery battery;
            if (getBattery(index, 0, out battery) != 0) return new BatteryReading { Label = "状态未知", ApproxPercent = -1 };
            if (battery.Type == 0) return new BatteryReading { Label = "未连接", ApproxPercent = -1 };
            // Bluetooth and 2.4 GHz receivers can expose an XInput USB-style path even while the controller is physically wireless.
            // BATTERY_TYPE_WIRED therefore means this API did not provide a usable battery reading, not proof of a cable connection.
            if (battery.Type == 1) return new BatteryReading { Label = "电量未上报", ApproxPercent = -1, TelemetryUnavailable = true };
            if (battery.Type == 0xFF) return new BatteryReading { Label = "类型未知", ApproxPercent = -1 };
            if (battery.Level == 3) return new BatteryReading { Label = "满电", ApproxPercent = 100 };
            if (battery.Level == 2) return new BatteryReading { Label = "中等", ApproxPercent = 65 };
            if (battery.Level == 1) return new BatteryReading { Label = "低电量", ApproxPercent = 25 };
            return new BatteryReading { Label = "电量耗尽", ApproxPercent = 5 };
        }

        public void Dispose()
        {
            if (module != IntPtr.Zero) FreeLibrary(module);
            module = IntPtr.Zero;
        }
    }

    public sealed class SonyInputManager
    {
        private sealed class SonyDeviceRecord
        {
            public InputSnapshot Latest;
            public DateTime LastPacketAt = DateTime.MinValue;
            public DateTime LastSeenAt = DateTime.MinValue;
            public bool Present;
        }

        private sealed class DualSenseReportLayout
        {
            public string Name;
            public byte ReportId;
            public int MinimumLength;
            public int BodyStart;
            public int AxisStart;
            public int ButtonStart;
            public int LeftTriggerIndex;
            public int RightTriggerIndex;
            public int TouchOffset;
            public bool RequiresCrc;
            public bool HasTouchCoordinates;
            public bool HasMotionSamples;
        }

        private const int DualSenseTouchRawWidth = 1920;
        private const int DualSenseTouchRawHeight = 1080;
        private readonly object sync = new object();
        private InputSnapshot latest = new InputSnapshot { Family = ControllerFamily.PlayStation, DeviceName = "索尼 DS 手柄", InputBackend = "Sony 原生 HID" };
        private DateTime lastPacketAt = DateTime.MinValue;
        private readonly Dictionary<string, SonyDeviceRecord> devices = new Dictionary<string, SonyDeviceRecord>(StringComparer.OrdinalIgnoreCase);
        private uint packet;
        private long touchReportSequence;
        private long motionReportSequence;
        private DateTime lastDiscoveryAt = DateTime.MinValue;
        private DateTime touchRateWindowStarted = DateTime.UtcNow;
        private int touchReportsInWindow;
        private double touchUpdatesPerSecond;
        private DateTime motionRateWindowStarted = DateTime.UtcNow;
        private int motionReportsInWindow;
        private double motionUpdatesPerSecond;

        // Debug output is deliberately opt-in: a connected controller can report at several hundred Hz.
        public bool EnableRawTouchLogging { get; set; }
        public bool EnableRawMotionLogging { get; set; }

        public bool WasRecentlyActive
        {
            get
            {
                lock (sync)
                {
                    foreach (SonyDeviceRecord record in devices.Values)
                    {
                        if ((DateTime.UtcNow - record.LastPacketAt).TotalSeconds < 0.9) return true;
                    }
                    return false;
                }
            }
        }

        // Polls the Raw Input device table at a low rate so an idle DS5 is visible
        // before it emits its first input report. Report parsing remains unchanged.
        public void DiscoverConnectedDevices()
        {
            DateTime now = DateTime.UtcNow;
            lock (sync)
            {
                if ((now - lastDiscoveryAt).TotalMilliseconds < 750) return;
                lastDiscoveryAt = now;
            }

            IList<string> paths = InputManager.EnumerateRawHidDevicePaths();
            Dictionary<string, string> present = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (path.IndexOf("VID_054C", StringComparison.OrdinalIgnoreCase) < 0) continue;
                present[BuildDeviceIdentity(path)] = path;
            }

            lock (sync)
            {
                foreach (SonyDeviceRecord record in devices.Values) record.Present = false;
                foreach (KeyValuePair<string, string> item in present)
                {
                    SonyDeviceRecord record;
                    if (!devices.TryGetValue(item.Key, out record))
                    {
                        bool edge = item.Value.IndexOf("PID_0DF2", StringComparison.OrdinalIgnoreCase) >= 0;
                        record = new SonyDeviceRecord
                        {
                            Latest = new InputSnapshot
                            {
                                DeviceId = "sony:" + item.Key,
                                TimestampUtc = now,
                                Connected = true,
                                Family = ControllerFamily.PlayStation,
                                DeviceName = edge ? "DualSense Edge" : "DualSense",
                                InputBackend = "Sony Native HID",
                                Battery = "Unknown",
                                BatteryPercent = -1,
                                ConnectionMethod = InputManager.DescribeRawInputConnection(item.Value),
                                ConnectionIsWireless = InputManager.DescribeRawInputConnection(item.Value).IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) >= 0 || InputManager.DescribeRawInputConnection(item.Value).IndexOf("蓝牙", StringComparison.OrdinalIgnoreCase) >= 0
                            }
                        };
                        devices[item.Key] = record;
                    }
                    record.Present = true;
                    record.LastSeenAt = now;
                }
            }
        }

        public void ObserveRawInput(string rawPath, byte[] report)
        {
            if (string.IsNullOrEmpty(rawPath) || report == null || report.Length < 10) return;
            if (rawPath.IndexOf("VID_054C", StringComparison.OrdinalIgnoreCase) < 0) return;
            bool dualSense = rawPath.IndexOf("PID_0CE6", StringComparison.OrdinalIgnoreCase) >= 0 || rawPath.IndexOf("PID_0DF2", StringComparison.OrdinalIgnoreCase) >= 0;
            string deviceIdentity = BuildDeviceIdentity(rawPath);
            DateTime now = DateTime.UtcNow;
            InputSnapshot parsed;
            string connectionMethod = InputManager.DescribeRawInputConnection(rawPath);
            if (!TryParse(report, dualSense, deviceIdentity, connectionMethod, out parsed))
            {
                if (EnableRawTouchLogging && dualSense) Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "DS5 HID report ignored: connection={0}, report=0x{1:X2}, length={2}, device={3}", connectionMethod, report[0], report.Length, deviceIdentity));
                return;
            }
            parsed.Connected = true;
            parsed.Family = ControllerFamily.PlayStation;
            parsed.DeviceName = dualSense
                ? (rawPath.IndexOf("PID_0DF2", StringComparison.OrdinalIgnoreCase) >= 0 ? "DualSense Edge" : "DualSense 无线控制器")
                : "DUALSHOCK 4 无线控制器";
            parsed.InputBackend = "Sony 原生 HID";
            parsed.ConnectionMethod = connectionMethod;
            parsed.ConnectionIsWireless = parsed.ConnectionMethod == "蓝牙";
            parsed.Packet = unchecked(++packet);
            parsed.DeviceId = "sony:" + deviceIdentity;
            parsed.TimestampUtc = now;
            if (parsed.Motion != null) parsed.Motion.TimestampUtc = now;
            lock (sync)
            {
                SonyDeviceRecord record;
                if (!devices.TryGetValue(deviceIdentity, out record))
                {
                    record = new SonyDeviceRecord();
                    devices[deviceIdentity] = record;
                }
                record.Latest = parsed;
                record.LastPacketAt = now;
                record.LastSeenAt = now;
                record.Present = true;
                latest = parsed;
                lastPacketAt = now;
            }
        }

        public InputSnapshot Read()
        {
            lock (sync)
            {
                if ((DateTime.UtcNow - lastPacketAt).TotalSeconds < 3.0) return latest;
                return new InputSnapshot
                {
                    Family = ControllerFamily.PlayStation,
                    DeviceName = latest.DeviceName ?? "索尼 DS 手柄",
                    InputBackend = "Sony 原生 HID",
                    ConnectionMethod = "未连接"
                };
            }
        }

        public IList<InputSnapshot> ReadAll()
        {
            List<InputSnapshot> result = new List<InputSnapshot>();
            lock (sync)
            {
                DateTime now = DateTime.UtcNow;
                foreach (SonyDeviceRecord record in devices.Values)
                {
                    if (record.Present && (now - record.LastSeenAt).TotalSeconds < 2.0 && record.Latest != null && record.Latest.Connected) result.Add(record.Latest);
                }
            }
            return result;
        }

        public void Dispose()
        {
            lock (sync)
            {
                devices.Clear();
                latest = new InputSnapshot { Family = ControllerFamily.PlayStation };
                lastPacketAt = DateTime.MinValue;
            }
        }

        public static string RunTouchParserSelfTest()
        {
            SonyInputManager manager = new SonyInputManager();
            List<string> passed = new List<string>();
            InputSnapshot state;

            byte[] usb = new byte[64];
            usb[0] = 0x01;
            WriteSyntheticTouch(usb, 33, 3, true, 960, 540);
            WriteSyntheticTouch(usb, 37, 4, false, 120, 890);
            if (!manager.TryParseDualSense(usb, "SELFTEST#USB", "有线", out state) || !state.TouchCoordinatesAvailable || state.TouchPoint1 == null || !state.TouchPoint1.IsActive || state.TouchPoint1.Id != 3 || state.TouchPoint1.RawX != 960 || state.TouchPoint1.RawY != 540 || state.TouchPoint2 == null || state.TouchPoint2.IsActive) throw new InvalidOperationException("USB touch layout self-test failed.");
            passed.Add("usb-0x01-64");

            byte[] bluetooth = new byte[78];
            bluetooth[0] = 0x31;
            WriteSyntheticTouch(bluetooth, 34, 12, true, 1800, 1010);
            WriteSyntheticTouch(bluetooth, 38, 13, true, 120, 50);
            WriteBluetoothCrc(bluetooth);
            if (!manager.TryParseDualSense(bluetooth, "SELFTEST#BT", "蓝牙", out state) || !state.TouchCoordinatesAvailable || state.TouchPoint1 == null || state.TouchPoint1.Id != 12 || state.TouchPoint1.RawX != 1800 || state.TouchPoint1.RawY != 1010 || state.TouchPoint2 == null || state.TouchPoint2.Id != 13) throw new InvalidOperationException("Bluetooth touch layout self-test failed.");
            passed.Add("bluetooth-0x31-78-crc");

            bluetooth[77] ^= 0x01;
            if (!manager.TryParseDualSense(bluetooth, "SELFTEST#BT", "蓝牙", out state) || state.TouchCoordinatesAvailable) throw new InvalidOperationException("Bluetooth CRC rejection self-test failed.");
            passed.Add("bluetooth-invalid-crc-rejected");

            byte[] compact = new byte[10];
            compact[0] = 0x01;
            if (!manager.TryParseDualSense(compact, "SELFTEST#BT", "蓝牙", out state) || state.TouchCoordinatesAvailable || state.HasTouchCoordinates) throw new InvalidOperationException("Bluetooth compact input self-test failed.");
            passed.Add("bluetooth-compact-no-coordinates");

            DualSenseTouchSensorDefinition mapping = new DualSenseTouchSensorDefinition
            {
                TopLeft = new DualSenseLogicalPoint { X = 10, Y = 20 },
                TopRight = new DualSenseLogicalPoint { X = 110, Y = 30 },
                BottomLeft = new DualSenseLogicalPoint { X = 20, Y = 220 },
                BottomRight = new DualSenseLogicalPoint { X = 120, Y = 230 }
            };
            Point center = DualSenseTouchVisualizer.Map(mapping, 0.5, 0.5);
            if (Math.Abs(center.X - 65) > 0.001 || Math.Abs(center.Y - 125) > 0.001) throw new InvalidOperationException("Touchpad bilinear mapping self-test failed.");
            passed.Add("bilinear-touchpad-mapping");

            DualSenseTouchVisualizer visualizer = new DualSenseTouchVisualizer();
            DateTime touchTime = DateTime.UtcNow;
            InputSnapshot touchDown = new InputSnapshot { Family = ControllerFamily.PlayStation, TouchCoordinatesAvailable = true, TouchReportSequence = 1, TouchReportUtc = touchTime, TouchPoint1 = new DualSenseTouchPoint { Id = 7, IsActive = true, X = 0.4, Y = 0.6 } };
            visualizer.Update(touchDown, false);
            if (!visualizer.HasVisibleContacts) throw new InvalidOperationException("Touch visualizer activation self-test failed.");
            InputSnapshot touchUp = new InputSnapshot { Family = ControllerFamily.PlayStation, TouchCoordinatesAvailable = true, TouchReportSequence = 2, TouchReportUtc = touchTime.AddMilliseconds(10), TouchPoint1 = new DualSenseTouchPoint { Id = 7, IsActive = false }, TouchPoint2 = new DualSenseTouchPoint { Id = 8, IsActive = false } };
            visualizer.Update(touchUp, false);
            visualizer.Advance(touchTime.AddMilliseconds(300), false);
            if (visualizer.HasVisibleContacts) throw new InvalidOperationException("Touch visualizer fade self-test failed.");
            passed.Add("touch-animation-lifecycle");
            return "DS5 touch parser self-test passed: " + string.Join(", ", passed.ToArray());
        }

        public static string RunMotionParserSelfTest()
        {
            SonyInputManager manager = new SonyInputManager();
            List<string> passed = new List<string>();
            InputSnapshot state;

            byte[] usb = new byte[64];
            usb[0] = 0x01;
            WriteSyntheticMotion(usb, 16, 1024, -2048, 512, 8192, -4096, 16384);
            if (!manager.TryParseDualSense(usb, "SELFTEST#USB", "wired", out state) || state.Motion == null || !state.Motion.IsValid || state.Motion.SourceReportId != 0x01 || state.Motion.RawGyroX != 1024 || Math.Abs(state.Motion.GyroY + 2.0) > 0.0001 || Math.Abs(state.Motion.AccelZ - 2.0) > 0.0001) throw new InvalidOperationException("USB motion layout self-test failed.");
            passed.Add("usb-0x01-64-body-gyro15-accel21");

            byte[] bluetooth = new byte[78];
            bluetooth[0] = 0x31;
            WriteSyntheticMotion(bluetooth, 17, -3072, 2048, -1024, -8192, 4096, 8192);
            WriteBluetoothCrc(bluetooth);
            if (!manager.TryParseDualSense(bluetooth, "SELFTEST#BT", "Bluetooth", out state) || state.Motion == null || !state.Motion.IsValid || state.Motion.SourceReportId != 0x31 || !state.Motion.CrcValidated || state.Motion.RawGyroX != -3072 || Math.Abs(state.Motion.AccelX + 1.0) > 0.0001) throw new InvalidOperationException("Bluetooth motion layout self-test failed.");
            passed.Add("bluetooth-0x31-78-common-body-and-crc");

            bluetooth[77] ^= 0x01;
            if (!manager.TryParseDualSense(bluetooth, "SELFTEST#BT", "Bluetooth", out state) || state.Motion == null || state.Motion.IsValid || state.Motion.CrcValidated) throw new InvalidOperationException("Bluetooth motion CRC rejection self-test failed.");
            passed.Add("bluetooth-crc-failure-rejects-motion-only");

            byte[] compact = new byte[10];
            compact[0] = 0x01;
            if (!manager.TryParseDualSense(compact, "SELFTEST#BT", "Bluetooth", out state) || state.Motion == null || state.Motion.IsValid) throw new InvalidOperationException("Compact motion compatibility self-test failed.");
            passed.Add("compact-report-motion-unsupported");
            return string.Join(", ", passed.ToArray());
        }

        private static void WriteSyntheticMotion(byte[] data, int gyroOffset, int gyroX, int gyroY, int gyroZ, int accelX, int accelY, int accelZ)
        {
            WriteInt16(data, gyroOffset, gyroX);
            WriteInt16(data, gyroOffset + 2, gyroY);
            WriteInt16(data, gyroOffset + 4, gyroZ);
            WriteInt16(data, gyroOffset + 6, accelX);
            WriteInt16(data, gyroOffset + 8, accelY);
            WriteInt16(data, gyroOffset + 10, accelZ);
        }

        private static void WriteInt16(byte[] data, int offset, int value)
        {
            short signed = (short)value;
            data[offset] = (byte)(signed & 0xFF);
            data[offset + 1] = (byte)((signed >> 8) & 0xFF);
        }

        private static void WriteSyntheticTouch(byte[] data, int offset, int id, bool active, int rawX, int rawY)
        {
            data[offset] = (byte)((active ? 0 : 0x80) | (id & 0x7F));
            data[offset + 1] = (byte)(rawX & 0xFF);
            data[offset + 2] = (byte)(((rawX >> 8) & 0x0F) | ((rawY & 0x0F) << 4));
            data[offset + 3] = (byte)((rawY >> 4) & 0xFF);
        }

        private static void WriteBluetoothCrc(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            crc = Crc32Le(crc, 0xA1);
            for (int i = 0; i < data.Length - 4; i++) crc = Crc32Le(crc, data[i]);
            uint final = ~crc;
            data[data.Length - 4] = (byte)(final & 0xFF);
            data[data.Length - 3] = (byte)((final >> 8) & 0xFF);
            data[data.Length - 2] = (byte)((final >> 16) & 0xFF);
            data[data.Length - 1] = (byte)((final >> 24) & 0xFF);
        }

        private bool TryParse(byte[] report, bool dualSense, string deviceIdentity, string connectionMethod, out InputSnapshot state)
        {
            state = null;
            if (dualSense) return TryParseDualSense(report, deviceIdentity, connectionMethod, out state);
            return TryParseDualShock4(report, out state);
        }

        private bool TryParseDualSense(byte[] data, string deviceIdentity, string connectionMethod, out InputSnapshot state)
        {
            state = null;
            DualSenseReportLayout layout;
            if (!TryGetDualSenseLayout(data, connectionMethod, out layout)) return false;
            if (!HasIndices(data, layout.AxisStart + 3, layout.ButtonStart + 2, layout.LeftTriggerIndex, layout.RightTriggerIndex)) return false;
            bool crcValidated = !layout.RequiresCrc || HasValidDualSenseBluetoothCrc(data);
            state = BuildState(data, layout.AxisStart, layout.ButtonStart, layout.LeftTriggerIndex, layout.RightTriggerIndex);
            state.TouchpadPressed = (data[layout.ButtonStart + 2] & 0x02) != 0;
            state.MicrophoneMuted = (data[layout.ButtonStart + 2] & 0x04) != 0;
            state.Motion = ParseDualSenseMotion(data, layout, connectionMethod, crcValidated);
            if (state.Motion != null && state.Motion.IsValid)
            {
                state.GyroscopeX = state.Motion.GyroX;
                state.GyroscopeY = state.Motion.GyroY;
                state.GyroscopeZ = state.Motion.GyroZ;
                state.AccelerometerX = state.Motion.AccelX;
                state.AccelerometerY = state.Motion.AccelY;
                state.AccelerometerZ = state.Motion.AccelZ;
                UpdateMotionRate(state.Motion.TimestampUtc);
            }
            state.LightbarState = "available";
            if (layout.HasTouchCoordinates && crcValidated && HasIndices(data, layout.TouchOffset, layout.TouchOffset + 7))
            {
                state.TouchPoint1 = ParseDualSenseTouchPoint(data, layout.TouchOffset);
                state.TouchPoint2 = ParseDualSenseTouchPoint(data, layout.TouchOffset + 4);
                state.TouchCoordinatesAvailable = true;
                state.HasTouchCoordinates = true;
                state.TouchReportSequence = Interlocked.Increment(ref touchReportSequence);
                state.TouchReportUtc = DateTime.UtcNow;
                UpdateTouchRate(state.TouchReportUtc);
            }
            if (layout.HasTouchCoordinates && HasIndices(data, layout.BodyStart + 52)) ApplyDualSenseBattery(state, data[layout.BodyStart + 52]);
            state.TouchDebug = CreateTouchDebugInfo(data, layout, deviceIdentity, connectionMethod, crcValidated, state);
            if (EnableRawTouchLogging && layout.HasTouchCoordinates)
            {
                Debug.WriteLine(FormatTouchDebug(state.TouchDebug, state.TouchPoint1, state.TouchPoint2));
            }
            if (EnableRawMotionLogging && state.Motion != null)
            {
                Debug.WriteLine(FormatMotionDebug(deviceIdentity, state.Motion));
            }
            return true;
        }

        private static bool TryGetDualSenseLayout(byte[] data, string connectionMethod, out DualSenseReportLayout layout)
        {
            layout = null;
            if (data == null || data.Length == 0) return false;
            bool bluetoothPath = string.Equals(connectionMethod, "蓝牙", StringComparison.OrdinalIgnoreCase);
            if (data[0] == 0x01 && !bluetoothPath && data.Length == 64)
            {
                // USB full input: Report ID 0x01, then the 63-byte shared DS5 body.
                layout = new DualSenseReportLayout { Name = "USB full input", ReportId = 0x01, MinimumLength = 64, BodyStart = 1, AxisStart = 1, ButtonStart = 8, LeftTriggerIndex = 5, RightTriggerIndex = 6, TouchOffset = 33, HasTouchCoordinates = true, HasMotionSamples = true };
                return true;
            }
            if (data[0] == 0x31 && data.Length == 78)
            {
                // Bluetooth full input: ID + sequence/tag precede the same body; final four bytes are CRC32.
                layout = new DualSenseReportLayout { Name = "Bluetooth full input", ReportId = 0x31, MinimumLength = 78, BodyStart = 2, AxisStart = 2, ButtonStart = 9, LeftTriggerIndex = 6, RightTriggerIndex = 7, TouchOffset = 34, RequiresCrc = true, HasTouchCoordinates = true, HasMotionSamples = true };
                return true;
            }
            if (data[0] == 0x01 && data.Length >= 10)
            {
                // Bluetooth can expose a compact ID 0x01 compatibility report. It contains normal buttons
                // but no native touch records, so keep the click path and explicitly expose coordinates as unavailable.
                layout = new DualSenseReportLayout { Name = "Bluetooth compact compatibility input", ReportId = 0x01, MinimumLength = 10, BodyStart = 1, AxisStart = 1, ButtonStart = 5, LeftTriggerIndex = 8, RightTriggerIndex = 9, TouchOffset = -1, HasTouchCoordinates = false, HasMotionSamples = false };
                return true;
            }
            return false;
        }

        private static DualSenseTouchPoint ParseDualSenseTouchPoint(byte[] data, int offset)
        {
            byte contact = data[offset];
            int rawX = data[offset + 1] | ((data[offset + 2] & 0x0F) << 8);
            int rawY = ((data[offset + 2] >> 4) & 0x0F) | (data[offset + 3] << 4);
            bool active = (contact & 0x80) == 0;
            return new DualSenseTouchPoint
            {
                Id = contact & 0x7F,
                RawId = (byte)(contact & 0x7F),
                IsActive = active && rawX >= 0 && rawX < DualSenseTouchRawWidth && rawY >= 0 && rawY < DualSenseTouchRawHeight,
                RawX = rawX,
                RawY = rawY,
                X = Math.Max(0, Math.Min(1, rawX / (double)(DualSenseTouchRawWidth - 1))),
                Y = Math.Max(0, Math.Min(1, rawY / (double)(DualSenseTouchRawHeight - 1)))
            };
        }

        private static short ReadInt16(byte[] data, int offset)
        {
            return (short)(data[offset] | (data[offset + 1] << 8));
        }

        private MotionSample ParseDualSenseMotion(byte[] data, DualSenseReportLayout layout, string connectionMethod, bool crcValidated)
        {
            MotionSample sample = new MotionSample
            {
                TimestampUtc = DateTime.UtcNow,
                Sequence = Interlocked.Increment(ref motionReportSequence),
                SourceReportId = layout == null ? (byte)0 : layout.ReportId,
                ConnectionType = ControllerStateAdapter.ParseConnectionType(connectionMethod),
                ConnectionLabel = connectionMethod ?? string.Empty,
                ReportLength = data == null ? 0 : data.Length,
                CrcValidated = crcValidated,
                Layout = layout == null ? string.Empty : layout.Name
            };
            if (layout == null || !layout.HasMotionSamples)
            {
                sample.AvailabilityMessage = "当前兼容输入报告未包含运动传感器字段。";
                return sample;
            }
            if (!crcValidated)
            {
                sample.AvailabilityMessage = "蓝牙 HID CRC 校验失败，已拒绝运动传感器数据。";
                return sample;
            }
            if (!HasIndices(data, layout.BodyStart + 26))
            {
                sample.AvailabilityMessage = "运动传感器报告长度不足，无法安全读取字段。";
                return sample;
            }
            // Linux hid-playstation dualsense_input_report: gyro[3] starts at shared body +15,
            // accel[3] at +21. Both USB and BT layouts above point BodyStart to that same body.
            sample.RawGyroX = ReadInt16(data, layout.BodyStart + 15);
            sample.RawGyroY = ReadInt16(data, layout.BodyStart + 17);
            sample.RawGyroZ = ReadInt16(data, layout.BodyStart + 19);
            sample.RawAccelX = ReadInt16(data, layout.BodyStart + 21);
            sample.RawAccelY = ReadInt16(data, layout.BodyStart + 23);
            sample.RawAccelZ = ReadInt16(data, layout.BodyStart + 25);
            sample.GyroX = DualSenseMotionUnits.GyroToDegreesPerSecond(sample.RawGyroX);
            sample.GyroY = DualSenseMotionUnits.GyroToDegreesPerSecond(sample.RawGyroY);
            sample.GyroZ = DualSenseMotionUnits.GyroToDegreesPerSecond(sample.RawGyroZ);
            sample.AccelX = DualSenseMotionUnits.AccelToG(sample.RawAccelX);
            sample.AccelY = DualSenseMotionUnits.AccelToG(sample.RawAccelY);
            sample.AccelZ = DualSenseMotionUnits.AccelToG(sample.RawAccelZ);
            sample.IsValid = IsFiniteMotion(sample);
            sample.AvailabilityMessage = sample.IsValid ? string.Empty : "运动传感器换算结果无效。";
            return sample;
        }

        private static bool IsFiniteMotion(MotionSample sample)
        {
            if (sample == null) return false;
            return !double.IsNaN(sample.GyroX) && !double.IsInfinity(sample.GyroX)
                && !double.IsNaN(sample.GyroY) && !double.IsInfinity(sample.GyroY)
                && !double.IsNaN(sample.GyroZ) && !double.IsInfinity(sample.GyroZ)
                && !double.IsNaN(sample.AccelX) && !double.IsInfinity(sample.AccelX)
                && !double.IsNaN(sample.AccelY) && !double.IsInfinity(sample.AccelY)
                && !double.IsNaN(sample.AccelZ) && !double.IsInfinity(sample.AccelZ);
        }

        private void UpdateMotionRate(DateTime now)
        {
            motionReportsInWindow++;
            double elapsed = (now - motionRateWindowStarted).TotalSeconds;
            if (elapsed >= 0.5)
            {
                motionUpdatesPerSecond = motionReportsInWindow / elapsed;
                motionReportsInWindow = 0;
                motionRateWindowStarted = now;
            }
        }

        private static string FormatMotionDebug(string deviceIdentity, MotionSample sample)
        {
            if (sample == null) return "DS5 motion: unavailable";
            return string.Format(CultureInfo.InvariantCulture,
                "DS5 motion: device={0}, connection={1}, report=0x{2:X2}, length={3}, crc={4}, rawGyro=({5},{6},{7}), rawAccel=({8},{9},{10}), gyro=({11:0.000},{12:0.000},{13:0.000}) deg/s, accel=({14:0.000},{15:0.000},{16:0.000}) g, valid={17}",
                deviceIdentity, sample.ConnectionLabel, sample.SourceReportId, sample.ReportLength, sample.CrcValidated,
                sample.RawGyroX, sample.RawGyroY, sample.RawGyroZ, sample.RawAccelX, sample.RawAccelY, sample.RawAccelZ,
                sample.GyroX, sample.GyroY, sample.GyroZ, sample.AccelX, sample.AccelY, sample.AccelZ, sample.IsValid);
        }

        private void UpdateTouchRate(DateTime now)
        {
            touchReportsInWindow++;
            double elapsed = (now - touchRateWindowStarted).TotalSeconds;
            if (elapsed >= 0.5)
            {
                touchUpdatesPerSecond = touchReportsInWindow / elapsed;
                touchReportsInWindow = 0;
                touchRateWindowStarted = now;
            }
        }

        private DualSenseTouchDebugInfo CreateTouchDebugInfo(byte[] data, DualSenseReportLayout layout, string deviceIdentity, string connectionMethod, bool crcValidated, InputSnapshot state)
        {
            DualSenseTouchDebugInfo info = new DualSenseTouchDebugInfo
            {
                DeviceIdentity = deviceIdentity,
                ConnectionMethod = connectionMethod,
                ReportId = layout.ReportId,
                ReportLength = data == null ? 0 : data.Length,
                TouchOffset = layout.TouchOffset,
                Layout = layout.Name,
                CrcValidated = crcValidated,
                CoordinatesAvailable = state != null && state.TouchCoordinatesAvailable,
                UpdatesPerSecond = touchUpdatesPerSecond,
                AvailabilityMessage = layout.HasTouchCoordinates
                    ? (crcValidated ? "原生 HID 触摸坐标可用" : "蓝牙 HID CRC 校验失败，已禁用触摸坐标")
                    : "当前连接模式不支持触摸坐标（仅触摸板按压）"
            };
            if (layout.HasTouchCoordinates && HasIndices(data, layout.TouchOffset, layout.TouchOffset + 7))
            {
                info.RawTouchBytes = new byte[8];
                Buffer.BlockCopy(data, layout.TouchOffset, info.RawTouchBytes, 0, 8);
            }
            return info;
        }

        private static bool HasValidDualSenseBluetoothCrc(byte[] data)
        {
            if (data == null || data.Length < 78) return false;
            uint crc = 0xFFFFFFFF;
            crc = Crc32Le(crc, 0xA1);
            for (int i = 0; i < data.Length - 4; i++) crc = Crc32Le(crc, data[i]);
            uint expected = (uint)(data[data.Length - 4] | (data[data.Length - 3] << 8) | (data[data.Length - 2] << 16) | (data[data.Length - 1] << 24));
            return ~crc == expected;
        }

        private static uint Crc32Le(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320U : crc >> 1;
            return crc;
        }

        private static string BuildDeviceIdentity(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath)) return string.Empty;
            string[] segments = rawPath.ToUpperInvariant().Split('#');
            if (segments.Length < 3) return rawPath.ToUpperInvariant();
            string instance = segments[2];
            int collection = instance.IndexOf("&COL", StringComparison.Ordinal);
            if (collection >= 0) instance = instance.Substring(0, collection);
            return segments[1] + "#" + instance;
        }

        private static string FormatTouchDebug(DualSenseTouchDebugInfo info, DualSenseTouchPoint first, DualSenseTouchPoint second)
        {
            string bytes = info == null || info.RawTouchBytes == null ? "-" : BitConverter.ToString(info.RawTouchBytes);
            return string.Format(CultureInfo.InvariantCulture,
                "DS5 touch: connection={0}, report=0x{1:X2}, length={2}, offset={3}, bytes={4}, p1={5}, p2={6}",
                info == null ? "-" : info.ConnectionMethod, info == null ? 0 : info.ReportId, info == null ? 0 : info.ReportLength, info == null ? -1 : info.TouchOffset, bytes, DescribeTouch(first), DescribeTouch(second));
        }

        private static string DescribeTouch(DualSenseTouchPoint point)
        {
            if (point == null) return "none";
            return string.Format(CultureInfo.InvariantCulture, "id={0},active={1},raw=({2},{3}),norm=({4:0.000},{5:0.000})", point.Id, point.IsActive, point.RawX, point.RawY, point.X, point.Y);
        }

        private static bool TryParseDualShock4(byte[] data, out InputSnapshot state)
        {
            state = null;
            int axisStart;
            if (data[0] == 0x01) axisStart = 1;
            else if (data[0] == 0x11) axisStart = 3;
            else return false;
            int buttonStart = axisStart + 4;
            int leftTrigger = axisStart + 7;
            int rightTrigger = axisStart + 8;
            if (!HasIndices(data, axisStart + 3, buttonStart + 2, leftTrigger, rightTrigger)) return false;
            state = BuildState(data, axisStart, buttonStart, leftTrigger, rightTrigger);
            state.TouchpadPressed = (data[buttonStart + 2] & 0x02) != 0;
            int statusIndex = axisStart + 29;
            if (statusIndex < data.Length) ApplyDualShock4Battery(state, data[statusIndex]);
            return true;
        }

        private static InputSnapshot BuildState(byte[] data, int axisStart, int buttonStart, int leftTriggerIndex, int rightTriggerIndex)
        {
            byte faceDpad = data[buttonStart];
            byte shoulders = data[buttonStart + 1];
            byte system = data[buttonStart + 2];
            ushort buttons = MapButtons(faceDpad, shoulders, system);
            return new InputSnapshot
            {
                LeftX = Axis(data[axisStart]),
                LeftY = AxisY(data[axisStart + 1]),
                RightX = Axis(data[axisStart + 2]),
                RightY = AxisY(data[axisStart + 3]),
                LeftTrigger = data[leftTriggerIndex],
                RightTrigger = data[rightTriggerIndex],
                Buttons = buttons,
                Battery = "电量读取中",
                BatteryPercent = -1
            };
        }

        private static bool HasIndices(byte[] data, params int[] indices)
        {
            for (int i = 0; i < indices.Length; i++) if (indices[i] < 0 || indices[i] >= data.Length) return false;
            return true;
        }

        private static int Axis(byte value)
        {
            return Math.Max(-32768, Math.Min(32767, (value - 128) * 257));
        }

        private static int AxisY(byte value)
        {
            return Math.Max(-32768, Math.Min(32767, (128 - value) * 257));
        }

        private static ushort MapButtons(byte faceDpad, byte shoulders, byte system)
        {
            ushort value = MapDpad((byte)(faceDpad & 0x0F));
            if ((faceDpad & 0x10) != 0) value |= 0x4000; // Square -> X slot
            if ((faceDpad & 0x20) != 0) value |= 0x1000; // Cross -> A slot
            if ((faceDpad & 0x40) != 0) value |= 0x2000; // Circle -> B slot
            if ((faceDpad & 0x80) != 0) value |= 0x8000; // Triangle -> Y slot
            if ((shoulders & 0x01) != 0) value |= 0x0100;
            if ((shoulders & 0x02) != 0) value |= 0x0200;
            if ((shoulders & 0x10) != 0) value |= 0x0020; // Create / Share
            if ((shoulders & 0x20) != 0) value |= 0x0010; // Options
            if ((shoulders & 0x40) != 0) value |= 0x0040;
            if ((shoulders & 0x80) != 0) value |= 0x0080;
            if ((system & 0x01) != 0) value |= 0x0400; // PS
            if ((system & 0x02) != 0) value |= 0x0800; // Touchpad click
            return value;
        }

        private static ushort MapDpad(byte hat)
        {
            if (hat == 0) return 0x0001;
            if (hat == 1) return 0x0001 | 0x0008;
            if (hat == 2) return 0x0008;
            if (hat == 3) return 0x0008 | 0x0002;
            if (hat == 4) return 0x0002;
            if (hat == 5) return 0x0002 | 0x0004;
            if (hat == 6) return 0x0004;
            if (hat == 7) return 0x0004 | 0x0001;
            return 0;
        }

        private static void ApplyDualSenseBattery(InputSnapshot state, byte status)
        {
            int capacity = status & 0x0F;
            if (capacity > 10) return;
            state.BatteryPercent = Math.Min(100, capacity * 10);
            state.Battery = (status & 0xF0) != 0 ? "充电中" : state.BatteryPercent >= 90 ? "满电" : state.BatteryPercent >= 40 ? "使用中" : "低电量";
        }

        private static void ApplyDualShock4Battery(InputSnapshot state, byte status)
        {
            int capacity = status & 0x0F;
            if (capacity > 11) return;
            state.BatteryPercent = Math.Min(100, capacity * 10);
            state.Battery = capacity >= 10 ? "满电" : capacity >= 4 ? "使用中" : "低电量";
        }
    }

    public sealed class DualSenseVisual : FrameworkElement
    {
        private readonly Dictionary<int, double> levels = new Dictionary<int, double>();
        private readonly int[] animatedMasks = { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080, 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000 };
        private readonly Typeface regular = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private readonly Typeface semi = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private readonly ImageSource controllerPhoto;
        private readonly BitmapSource leftPhotoStickCap;
        private readonly BitmapSource rightPhotoStickCap;
        private readonly DualSenseRegionManager regions;
        private InputSnapshot state = new InputSnapshot { Family = ControllerFamily.PlayStation };
        private bool reducedMotion;
        private double smoothLX;
        private double smoothLY;
        private double smoothRX;
        private double smoothRY;
        private double smoothL2;
        private double smoothR2;
        private string renderedDeviceId;

        public bool ReducedMotion
        {
            get { return reducedMotion; }
            set
            {
                if (reducedMotion == value) return;
                reducedMotion = value;
                InvalidateVisual();
            }
        }

        public DualSenseVisual()
        {
            controllerPhoto = LoadPhotoResource();
            regions = DualSenseRegionManager.Load(HasCommandLineArgument("--ds5-default-geometry"));
            BitmapSource photo = controllerPhoto as BitmapSource;
            if (photo != null && photo.PixelWidth >= 1200 && photo.PixelHeight >= 800)
            {
                // The photographed sockets and rubber caps do not share exactly the same center.
                // Crop each complete cap from its measured visual center instead of reusing the
                // older 136 px assets, which were 6-11 px off-axis and trimmed the rubber rim.
                leftPhotoStickCap = CreatePhotoStickCap(photo, 568, 484, 74);
                rightPhotoStickCap = CreatePhotoStickCap(photo, 969, 484, 74);
            }
            else
            {
                BitmapSource cleanCap = LoadSharedStickCap();
                if (cleanCap != null)
                {
                    leftPhotoStickCap = cleanCap;
                    rightPhotoStickCap = cleanCap;
                }
            }
            ClipToBounds = false;
            IsHitTestVisible = false;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        public void UpdateState(InputSnapshot value)
        {
            bool changed = state.Connected != value.Connected || state.Buttons != value.Buttons || state.LeftX != value.LeftX || state.LeftY != value.LeftY || state.RightX != value.RightX || state.RightY != value.RightY || state.LeftTrigger != value.LeftTrigger || state.RightTrigger != value.RightTrigger || state.TouchpadPressed != value.TouchpadPressed || state.MicrophoneMuted != value.MicrophoneMuted;
            if (!string.Equals(renderedDeviceId, value.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                renderedDeviceId = value.DeviceId;
                regions.ResetTouchVisualizer();
                changed = true;
            }
            state = value;
            changed |= regions.UpdateTouchVisualizer(value, reducedMotion);
            changed |= Smooth(ref smoothLX, value.LeftNormalizedX, 0.31);
            changed |= Smooth(ref smoothLY, value.LeftNormalizedY, 0.31);
            changed |= Smooth(ref smoothRX, value.RightNormalizedX, 0.31);
            changed |= Smooth(ref smoothRY, value.RightNormalizedY, 0.31);
            changed |= Smooth(ref smoothL2, value.LeftTrigger / 255.0, 0.34);
            changed |= Smooth(ref smoothR2, value.RightTrigger / 255.0, 0.34);
            for (int i = 0; i < animatedMasks.Length; i++)
            {
                int mask = animatedMasks[i];
                double before;
                if (!levels.TryGetValue(mask, out before)) before = 0;
                double target = (value.Buttons & mask) != 0 ? 1 : 0;
                double next = reducedMotion ? target : before + (target - before) * (target > before ? 0.5 : 0.22);
                if (Math.Abs(next - before) > 0.0005) changed = true;
                levels[mask] = next;
            }
            if (changed) InvalidateVisual();
        }

        public DualSenseRegionManager Regions
        {
            get { return regions; }
        }

        public ImageSource ControllerPhoto
        {
            get { return controllerPhoto; }
        }

        private bool Smooth(ref double current, double target, double speed)
        {
            double before = current;
            current += (target - current) * (reducedMotion ? 1 : speed);
            return Math.Abs(current - before) > 0.00005;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth < 10 || ActualHeight < 10) return;
            if (controllerPhoto != null)
            {
                DrawPhotographicController(dc);
                return;
            }
            const double baseW = 1000;
            const double baseH = 590;
            double scale = Math.Min(ActualWidth * 0.92 / baseW, ActualHeight * 0.89 / baseH);
            double x = (ActualWidth - baseW * scale) / 2.0;
            double y = (ActualHeight - baseH * scale) / 2.0 + 12;

            RadialGradientBrush floor = new RadialGradientBrush(Color.FromArgb(95, 25, 128, 220), Color.FromArgb(0, 7, 18, 29));
            dc.DrawEllipse(floor, null, new Point(ActualWidth / 2.0, y + 440 * scale), 390 * scale, 122 * scale);
            dc.PushTransform(new MatrixTransform(new Matrix(scale, 0, 0, scale, x, y)));

            DrawTrigger(dc, new Rect(230, 86, 140, 24), smoothL2, true);
            DrawTrigger(dc, new Rect(630, 86, 140, 24), smoothR2, false);
            DrawControllerShell(dc);
            DrawLightBar(dc);
            DrawTouchpad(dc);
            DrawDpad(dc, new Point(315, 292));
            DrawFaceButtons(dc);
            DrawSmallButton(dc, new Point(431, 296), "创建", 0x0020);
            DrawSmallButton(dc, new Point(569, 296), "选项", 0x0010);
            DrawPsButton(dc, new Point(500, 338));
            DrawStick(dc, new Point(372, 391), smoothLX, smoothLY, 0x0040, Palette.Blue);
            DrawStick(dc, new Point(628, 391), smoothRX, smoothRY, 0x0080, Palette.Blue);
            DrawShoulders(dc);
            dc.Pop();
        }

        private static bool HasCommandLineArgument(string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static ImageSource LoadPhotoResource()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ControllerLab.Assets.dualsense.png");
            if (stream == null) return null;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static BitmapSource LoadSharedStickCap()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ControllerLab.Assets.stick-cap.png");
            if (stream == null) return null;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static BitmapSource LoadDualSenseStickCap(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static BitmapSource CreatePhotoStickCap(BitmapSource source, int centerX, int centerY, int radius)
        {
            int size = radius * 2;
            CroppedBitmap crop = new CroppedBitmap(source, new Int32Rect(centerX - radius, centerY - radius, size, size));
            FormatConvertedBitmap converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = crop;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            int stride = size * 4;
            byte[] pixels = new byte[stride * size];
            converted.CopyPixels(pixels, stride, 0);
            double inner = radius - 4.0;
            double outer = radius - 0.4;
            for (int row = 0; row < size; row++)
            {
                double y = row - radius + 0.5;
                for (int column = 0; column < size; column++)
                {
                    double x = column - radius + 0.5;
                    double distance = Math.Sqrt(x * x + y * y);
                    if (distance <= inner) continue;
                    int pixel = row * stride + column * 4;
                    if (distance >= outer)
                    {
                        pixels[pixel + 3] = 0;
                    }
                    else
                    {
                        double factor = Math.Max(0, Math.Min(1, (outer - distance) / (outer - inner)));
                        pixels[pixel + 3] = (byte)(pixels[pixel + 3] * factor);
                    }
                }
            }
            WriteableBitmap cap = new WriteableBitmap(size, size, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null);
            cap.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
            cap.Freeze();
            return cap;
        }

        private void DrawPhotographicController(DrawingContext dc)
        {
            DualSenseOverlayState overlay = new DualSenseOverlayState
            {
                Connected = state.Connected,
                ReducedMotion = reducedMotion,
                DpadUp = Level(0x0001),
                DpadDown = Level(0x0002),
                DpadLeft = Level(0x0004),
                DpadRight = Level(0x0008),
                Create = Level(0x0020),
                Options = Level(0x0010),
                L3 = Level(0x0040),
                R3 = Level(0x0080),
                L1 = Level(0x0100),
                R1 = Level(0x0200),
                Ps = Level(0x0400),
                TouchpadButton = Math.Max(Level(0x0800), state.TouchpadPressed ? 1.0 : 0.0),
                Cross = Level(0x1000),
                Circle = Level(0x2000),
                Square = Level(0x4000),
                Triangle = Level(0x8000),
                Microphone = state.MicrophoneMuted ? 1.0 : 0.0,
                L2 = smoothL2,
                R2 = smoothR2,
                LeftX = smoothLX,
                LeftY = smoothLY,
                RightX = smoothRX,
                RightY = smoothRY,
                TouchCoordinatesAvailable = state.TouchCoordinatesAvailable,
                HasTouchCoordinates = state.HasTouchCoordinates,
                TouchpadSurface = regions.HasVisibleTouchContacts ? 1.0 : 0.0
            };
            regions.Draw(dc, controllerPhoto, leftPhotoStickCap, rightPhotoStickCap, overlay, ActualWidth, ActualHeight, VisualTreeHelper.GetDpi(this).DpiScaleX);
        }

        private void DrawControllerShell(DrawingContext dc)
        {
            StreamGeometry outline = new StreamGeometry();
            using (StreamGeometryContext c = outline.Open())
            {
                c.BeginFigure(new Point(140, 190), true, true);
                c.BezierTo(new Point(160, 126), new Point(234, 100), new Point(318, 106), true, false);
                c.BezierTo(new Point(397, 114), new Point(432, 139), new Point(500, 139), true, false);
                c.BezierTo(new Point(568, 139), new Point(603, 114), new Point(682, 106), true, false);
                c.BezierTo(new Point(766, 100), new Point(840, 126), new Point(860, 190), true, false);
                c.BezierTo(new Point(886, 270), new Point(876, 418), new Point(823, 499), true, false);
                c.BezierTo(new Point(786, 553), new Point(720, 547), new Point(672, 487), true, false);
                c.BezierTo(new Point(641, 452), new Point(607, 445), new Point(500, 445), true, false);
                c.BezierTo(new Point(393, 445), new Point(359, 452), new Point(328, 487), true, false);
                c.BezierTo(new Point(280, 547), new Point(214, 553), new Point(177, 499), true, false);
                c.BezierTo(new Point(124, 418), new Point(114, 270), new Point(140, 190), true, false);
            }
            LinearGradientBrush shell = new LinearGradientBrush(Color.FromRgb(236, 241, 245), Color.FromRgb(136, 150, 163), new Point(0.5, 0), new Point(0.5, 1));
            dc.DrawGeometry(shell, new Pen(new SolidColorBrush(Color.FromRgb(210, 224, 235)), 2), outline);

            StreamGeometry inner = new StreamGeometry();
            using (StreamGeometryContext c = inner.Open())
            {
                c.BeginFigure(new Point(194, 201), true, true);
                c.BezierTo(new Point(218, 153), new Point(279, 139), new Point(348, 144), true, false);
                c.BezierTo(new Point(414, 149), new Point(442, 168), new Point(500, 168), true, false);
                c.BezierTo(new Point(558, 168), new Point(586, 149), new Point(652, 144), true, false);
                c.BezierTo(new Point(721, 139), new Point(782, 153), new Point(806, 201), true, false);
                c.BezierTo(new Point(824, 250), new Point(815, 394), new Point(774, 465), true, false);
                c.BezierTo(new Point(740, 505), new Point(699, 487), new Point(658, 437), true, false);
                c.BezierTo(new Point(625, 397), new Point(590, 389), new Point(500, 389), true, false);
                c.BezierTo(new Point(410, 389), new Point(375, 397), new Point(342, 437), true, false);
                c.BezierTo(new Point(301, 487), new Point(260, 505), new Point(226, 465), true, false);
                c.BezierTo(new Point(185, 394), new Point(176, 250), new Point(194, 201), true, false);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(35, 46, 58)), new Pen(new SolidColorBrush(Color.FromRgb(55, 71, 86)), 1), inner);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(95, 255, 255, 255)), 1.2), new Point(232, 158), new Point(768, 158));
        }

        private void DrawLightBar(DrawingContext dc)
        {
            double ps = Level(0x0400);
            Color glowColor = Palette.Blue;
            RadialGradientBrush halo = new RadialGradientBrush(Color.FromArgb((byte)(48 + ps * 100), glowColor.R, glowColor.G, glowColor.B), Color.FromArgb(0, glowColor.R, glowColor.G, glowColor.B));
            dc.DrawEllipse(halo, null, new Point(500, 170), 152, 56);
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(45, 154, 255), Color.FromRgb(170, 228, 255), new Point(0, 0), new Point(1, 0)), null, new Rect(410, 160, 180, 8), 4, 4);
        }

        private void DrawTouchpad(DrawingContext dc)
        {
            double level = Math.Max(Level(0x0800), state.TouchpadPressed ? 1 : 0);
            Rect r = new Rect(420, 198, 160, 76);
            if (!reducedMotion && level > 0.01)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(55 + level * 105), Palette.Blue.R, Palette.Blue.G, Palette.Blue.B), Color.FromArgb(0, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B));
                dc.DrawRoundedRectangle(glow, null, new Rect(r.X - 18, r.Y - 12, r.Width + 36, r.Height + 24), 19, 19);
            }
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(52, 66, 78), Color.FromRgb(19, 30, 40), new Point(0.5, 0), new Point(0.5, 1)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(112 + level * 115), Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 1.4), r, 12, 12);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, 202, 219, 230)), 0.8), new Point(500, r.Y + 8), new Point(500, r.Bottom - 8));
        }

        private void DrawDpad(DrawingContext dc, Point center)
        {
            DrawDpadArm(dc, new Rect(center.X - 22, center.Y - 66, 44, 48), 0x0001);
            DrawDpadArm(dc, new Rect(center.X - 22, center.Y + 18, 44, 48), 0x0002);
            DrawDpadArm(dc, new Rect(center.X - 66, center.Y - 22, 48, 44), 0x0004);
            DrawDpadArm(dc, new Rect(center.X + 18, center.Y - 22, 48, 44), 0x0008);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(37, 49, 61)), new Pen(new SolidColorBrush(Color.FromRgb(14, 19, 25)), 1), new Rect(center.X - 23, center.Y - 23, 46, 46), 5, 5);
        }

        private void DrawDpadArm(DrawingContext dc, Rect rect, int mask)
        {
            double level = Level(mask);
            Color color = Palette.Blue;
            if (!reducedMotion && level > 0.01)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(42 + level * 96), color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                dc.DrawRoundedRectangle(glow, null, new Rect(rect.X - 12, rect.Y - 12, rect.Width + 24, rect.Height + 24), 9, 9);
            }
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(38, 49, 61)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(84 + level * 160), color.R, color.G, color.B)), 1.4), rect, 7, 7);
        }

        private void DrawFaceButtons(DrawingContext dc)
        {
            DrawFaceButton(dc, new Point(738, 226), "△", 0x8000);
            DrawFaceButton(dc, new Point(690, 274), "□", 0x4000);
            DrawFaceButton(dc, new Point(786, 274), "○", 0x2000);
            DrawFaceButton(dc, new Point(738, 322), "×", 0x1000);
        }

        private void DrawFaceButton(DrawingContext dc, Point point, string symbol, int mask)
        {
            double level = Level(mask);
            Color color = Palette.Blue;
            if (!reducedMotion && level > 0.01)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(52 + level * 118), color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                dc.DrawEllipse(glow, null, point, 34, 34);
            }
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(36, 48, 60)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(104 + level * 151), color.R, color.G, color.B)), 1.5), point, 20 - level, 20 - level);
            DrawText(dc, symbol, point.X - 9, point.Y - 15, 25, level > 0.01 ? new SolidColorBrush(Color.FromRgb(214, 242, 255)) : new SolidColorBrush(Color.FromRgb(188, 207, 220)), true);
        }

        private void DrawSmallButton(DrawingContext dc, Point point, string label, int mask)
        {
            double level = Level(mask);
            Color color = Palette.Blue;
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(40, 52, 63)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(78 + level * 160), color.R, color.G, color.B)), 1.2), point, 15, 15);
            DrawText(dc, label, point.X - 11, point.Y - 5, 8.5, Palette.MutedBrush, false);
        }

        private void DrawPsButton(DrawingContext dc, Point point)
        {
            double level = Level(0x0400);
            Color color = Palette.Blue;
            if (!reducedMotion && level > 0.01)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(45 + level * 135), color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                dc.DrawEllipse(glow, null, point, 32, 32);
            }
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(31, 42, 53)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(90 + level * 155), color.R, color.G, color.B)), 1.3), point, 18, 18);
            DrawText(dc, "PS", point.X - 8, point.Y - 6, 9, level > 0.01 ? Palette.TextBrush : Palette.MutedBrush, true);
        }

        private void DrawStick(DrawingContext dc, Point center, double x, double y, int mask, Color accent)
        {
            double pressed = Level(mask);
            if (!reducedMotion)
            {
                RadialGradientBrush bedGlow = new RadialGradientBrush(Color.FromArgb(82, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B));
                dc.DrawEllipse(bedGlow, null, center, 71, 71);
            }
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(19, 28, 36)), new Pen(new SolidColorBrush(Color.FromRgb(91, 110, 124)), 2), center, 54, 54);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(165, accent.R, accent.G, accent.B)), 1.4), center, 46, 46);
            Point moved = new Point(center.X + x * 22, center.Y - y * 22 + pressed * 2.5);
            if (!reducedMotion && (Math.Abs(x) > 0.01 || Math.Abs(y) > 0.01)) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(155, accent.R, accent.G, accent.B)), 1.1), center, moved);
            RadialGradientBrush cap = new RadialGradientBrush(Color.FromRgb(92, 105, 116), Color.FromRgb(24, 31, 39));
            cap.GradientOrigin = new Point(0.36, 0.3);
            dc.DrawEllipse(cap, new Pen(new SolidColorBrush(Color.FromRgb(10, 14, 18)), 2), moved, 34, 34);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(122, 215, 227, 236)), 1), moved, 27, 27);
        }

        private void DrawShoulders(DrawingContext dc)
        {
            DrawShoulder(dc, new Rect(200, 125, 138, 24), 0x0100, "L1", true);
            DrawShoulder(dc, new Rect(662, 125, 138, 24), 0x0200, "R1", false);
        }

        private void DrawShoulder(DrawingContext dc, Rect rect, int mask, string label, bool left)
        {
            double level = Level(mask);
            Color color = Palette.Blue;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(47, 59, 72)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(100 + level * 145), color.R, color.G, color.B)), 1.4), rect, 8, 8);
            DrawText(dc, label, rect.X + rect.Width / 2 - 8, rect.Y + 5, 11, level > 0.01 ? Palette.TextBrush : Palette.MutedBrush, true);
        }

        private void DrawTrigger(DrawingContext dc, Rect rect, double value, bool left)
        {
            Color color = Palette.Blue;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(23, 34, 44)), new Pen(new SolidColorBrush(Color.FromRgb(86, 104, 120)), 1), rect, 7, 7);
            if (value > 0.001)
            {
                Rect filled = new Rect(rect.X + 2, rect.Y + 2, (rect.Width - 4) * value, rect.Height - 4);
                dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(46, 143, 255), Color.FromRgb(141, 211, 255), new Point(0, 0), new Point(1, 0)), null, filled, 5, 5);
            }
            DrawText(dc, left ? "L2" : "R2", left ? rect.X - 30 : rect.Right + 9, rect.Y + 4, 13, Palette.TextBrush, true);
            string percent = (value * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            DrawText(dc, percent, left ? rect.X - 34 : rect.Right + 9, rect.Y + 22, 11, Palette.BlueBrush, false);
        }

        private void DrawTelemetry(DrawingContext dc)
        {
            DrawText(dc, "左摇杆", 86, 304, 12, Palette.BlueBrush, true);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "X {0:0.000}   Y {1:0.000}", state.LeftNormalizedX, state.LeftNormalizedY), 86, 326, 10.5, Palette.MutedBrush, false);
            DrawText(dc, "右摇杆", 792, 382, 12, Palette.BlueBrush, true);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "X {0:0.000}   Y {1:0.000}", state.RightNormalizedX, state.RightNormalizedY), 792, 404, 10.5, Palette.MutedBrush, false);
            if (state.TouchpadPressed) DrawText(dc, "触控板按下", 456, 238, 10, Palette.TextBrush, true);
        }

        private double Level(int mask)
        {
            double value;
            return levels.TryGetValue(mask, out value) ? value : 0;
        }

        private void DrawText(DrawingContext dc, string text, double x, double y, double size, Brush brush, bool bold)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, bold ? semi : regular, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }

    // A self-contained native WPF renderer for true DualSense touch contacts. It owns temporal
    // smoothing, contact-id tracking, ripples and short-lived trails; the main controller view only
    // supplies validated HID snapshots and asks for a redraw.
    public sealed class DualSenseTouchVisualizer
    {
        private sealed class TrailPoint
        {
            public Point Position;
            public DateTime At;
        }

        private sealed class TouchTrack
        {
            public int Id;
            public bool Active;
            public bool SeenInReport;
            public bool Initialized;
            public Point Current;
            public Point Target;
            public DateTime LastSeen;
            public DateTime ReleasedAt;
            public DateTime RippleStartedAt;
            public double Opacity;
            public readonly List<TrailPoint> Trail = new List<TrailPoint>();
        }

        private readonly Dictionary<int, TouchTrack> tracks = new Dictionary<int, TouchTrack>();
        private long lastReportSequence = -1;

        public bool HasVisibleContacts
        {
            get
            {
                foreach (TouchTrack track in tracks.Values) if (track.Opacity > 0.01) return true;
                return false;
            }
        }

        public void Reset()
        {
            tracks.Clear();
            lastReportSequence = -1;
        }

        public bool Update(InputSnapshot state, bool reducedMotion)
        {
            if (state == null) return false;
            bool changed = false;
            if (state.TouchReportSequence != 0 && state.TouchReportSequence != lastReportSequence)
            {
                lastReportSequence = state.TouchReportSequence;
                DateTime now = state.TouchReportUtc == DateTime.MinValue ? DateTime.UtcNow : state.TouchReportUtc;
                foreach (TouchTrack track in tracks.Values) track.SeenInReport = false;
                changed |= ApplyPoint(state.TouchPoint1, now, reducedMotion);
                changed |= ApplyPoint(state.TouchPoint2, now, reducedMotion);
                foreach (TouchTrack track in tracks.Values)
                {
                    if (!track.SeenInReport && track.Active)
                    {
                        track.Active = false;
                        track.ReleasedAt = now;
                        changed = true;
                    }
                }
            }
            else if (!state.TouchCoordinatesAvailable && state.Family == ControllerFamily.PlayStation)
            {
                DateTime now = DateTime.UtcNow;
                foreach (TouchTrack track in tracks.Values)
                {
                    if (track.Active)
                    {
                        track.Active = false;
                        track.ReleasedAt = now;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private bool ApplyPoint(DualSenseTouchPoint point, DateTime now, bool reducedMotion)
        {
            if (point == null || !point.IsActive) return false;
            TouchTrack track;
            if (!tracks.TryGetValue(point.Id, out track))
            {
                track = new TouchTrack { Id = point.Id };
                tracks[point.Id] = track;
            }
            Point target = new Point(Clamp01(point.X), Clamp01(point.Y));
            bool isNewContact = !track.Active || !track.Initialized;
            track.SeenInReport = true;
            track.Active = true;
            track.LastSeen = now;
            track.Opacity = 1.0;
            track.Target = target;
            if (isNewContact)
            {
                track.Current = target;
                track.Initialized = true;
                track.RippleStartedAt = now;
                track.Trail.Clear();
                track.Trail.Add(new TrailPoint { Position = target, At = now });
                return true;
            }
            TrailPoint last = track.Trail.Count == 0 ? null : track.Trail[track.Trail.Count - 1];
            if (last == null || Distance(last.Position, target) >= 0.0035)
            {
                track.Trail.Add(new TrailPoint { Position = target, At = now });
                while (track.Trail.Count > 12) track.Trail.RemoveAt(0);
            }
            return true;
        }

        public bool Advance(DateTime now, bool reducedMotion)
        {
            bool changed = false;
            List<int> retired = null;
            foreach (KeyValuePair<int, TouchTrack> pair in tracks)
            {
                TouchTrack track = pair.Value;
                if (track.Active && (now - track.LastSeen).TotalMilliseconds > 80)
                {
                    track.Active = false;
                    track.ReleasedAt = track.LastSeen.AddMilliseconds(80);
                    changed = true;
                }
                double beforeX = track.Current.X;
                double beforeY = track.Current.Y;
                double smoothing = reducedMotion ? 1.0 : 0.52;
                track.Current = new Point(track.Current.X + (track.Target.X - track.Current.X) * smoothing, track.Current.Y + (track.Target.Y - track.Current.Y) * smoothing);
                if (Math.Abs(track.Current.X - beforeX) > 0.00005 || Math.Abs(track.Current.Y - beforeY) > 0.00005) changed = true;
                if (!track.Active)
                {
                    double elapsed = (now - track.ReleasedAt).TotalMilliseconds;
                    double next = elapsed <= 0 ? 1.0 : Math.Max(0, 1.0 - elapsed / (reducedMotion ? 120.0 : 200.0));
                    if (Math.Abs(next - track.Opacity) > 0.0005) changed = true;
                    track.Opacity = next;
                    if (track.Opacity <= 0.001 && (now - track.ReleasedAt).TotalMilliseconds > 260)
                    {
                        if (retired == null) retired = new List<int>();
                        retired.Add(pair.Key);
                    }
                }
                for (int i = track.Trail.Count - 1; i >= 0; i--)
                {
                    if ((now - track.Trail[i].At).TotalMilliseconds > 230) track.Trail.RemoveAt(i);
                }
            }
            if (retired != null)
            {
                for (int i = 0; i < retired.Count; i++) tracks.Remove(retired[i]);
                changed = true;
            }
            return changed;
        }

        public void Draw(DrawingContext dc, Geometry touchpadClip, DualSenseTouchSensorDefinition mapping, double scale, bool reducedMotion)
        {
            if (dc == null || touchpadClip == null || mapping == null || !HasVisibleContacts) return;
            double inverseScale = 1.0 / Math.Max(0.001, scale);
            DateTime now = DateTime.UtcNow;
            dc.PushClip(touchpadClip);
            foreach (TouchTrack track in tracks.Values)
            {
                if (track.Opacity <= 0.001) continue;
                DrawTrail(dc, track, mapping, inverseScale);
                Point point = Map(mapping, track.Current.X, track.Current.Y);
                byte alpha = (byte)Math.Max(0, Math.Min(255, 255 * track.Opacity));
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(alpha * 0.42), Palette.Blue.R, Palette.Blue.G, Palette.Blue.B), Color.FromArgb(0, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B));
                dc.DrawEllipse(glow, null, point, 17.0 * inverseScale, 17.0 * inverseScale);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.90), 116, 194, 255)), new Pen(new SolidColorBrush(Color.FromArgb(alpha, 224, 245, 255)), 1.0 * inverseScale), point, 4.7 * inverseScale, 4.7 * inverseScale);
                double rippleAge = (now - track.RippleStartedAt).TotalMilliseconds;
                if (!reducedMotion && rippleAge >= 0 && rippleAge < 310)
                {
                    double progress = rippleAge / 310.0;
                    byte rippleAlpha = (byte)Math.Max(0, Math.Min(150, 150 * (1.0 - progress) * track.Opacity));
                    dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(rippleAlpha, 96, 184, 255)), 1.15 * inverseScale), point, (7 + progress * 20) * inverseScale, (7 + progress * 20) * inverseScale);
                }
            }
            dc.Pop();
        }

        public static Point Map(DualSenseTouchSensorDefinition mapping, double u, double v)
        {
            u = Clamp01(u);
            v = Clamp01(v);
            DualSenseLogicalPoint topLeft = mapping.TopLeft ?? new DualSenseLogicalPoint { X = mapping.X, Y = mapping.Y };
            DualSenseLogicalPoint topRight = mapping.TopRight ?? new DualSenseLogicalPoint { X = mapping.X + mapping.Width, Y = mapping.Y };
            DualSenseLogicalPoint bottomLeft = mapping.BottomLeft ?? new DualSenseLogicalPoint { X = mapping.X, Y = mapping.Y + mapping.Height };
            DualSenseLogicalPoint bottomRight = mapping.BottomRight ?? new DualSenseLogicalPoint { X = mapping.X + mapping.Width, Y = mapping.Y + mapping.Height };
            double topWeight = 1.0 - v;
            double bottomWeight = v;
            return new Point(
                topLeft.X * (1.0 - u) * topWeight + topRight.X * u * topWeight + bottomLeft.X * (1.0 - u) * bottomWeight + bottomRight.X * u * bottomWeight,
                topLeft.Y * (1.0 - u) * topWeight + topRight.Y * u * topWeight + bottomLeft.Y * (1.0 - u) * bottomWeight + bottomRight.Y * u * bottomWeight);
        }

        private static void DrawTrail(DrawingContext dc, TouchTrack track, DualSenseTouchSensorDefinition mapping, double inverseScale)
        {
            if (track.Trail.Count < 2) return;
            for (int i = 1; i < track.Trail.Count; i++)
            {
                double life = i / (double)(track.Trail.Count - 1);
                byte alpha = (byte)Math.Max(0, Math.Min(110, 110 * life * life * track.Opacity));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(alpha, 75, 163, 255)), (1.1 + life * 2.4) * inverseScale), Map(mapping, track.Trail[i - 1].Position.X, track.Trail[i - 1].Position.Y), Map(mapping, track.Trail[i].Position.X, track.Trail[i].Position.Y));
            }
        }

        private static double Clamp01(double value) { return Math.Max(0, Math.Min(1, value)); }
        private static double Distance(Point a, Point b) { double dx = a.X - b.X; double dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy); }
    }

    public sealed class DualSenseOverlayState
    {
        public bool Connected;
        public bool ReducedMotion;
        public double DpadUp;
        public double DpadDown;
        public double DpadLeft;
        public double DpadRight;
        public double Cross;
        public double Circle;
        public double Square;
        public double Triangle;
        public double L3;
        public double R3;
        public double L1;
        public double R1;
        public double L2;
        public double R2;
        public double Create;
        public double Options;
        public double Ps;
        public double Microphone;
        public double TouchpadSurface;
        public double TouchpadButton;
        public double LeftX;
        public double LeftY;
        public double RightX;
        public double RightY;
        public bool TouchCoordinatesAvailable;
        public bool HasTouchCoordinates;

        public double ValueFor(string id)
        {
            if (id == "dpad-up") return DpadUp;
            if (id == "dpad-down") return DpadDown;
            if (id == "dpad-left") return DpadLeft;
            if (id == "dpad-right") return DpadRight;
            if (id == "button-cross") return Cross;
            if (id == "button-circle") return Circle;
            if (id == "button-square") return Square;
            if (id == "button-triangle") return Triangle;
            if (id == "button-l3") return L3;
            if (id == "button-r3") return R3;
            if (id == "button-l1") return L1;
            if (id == "button-r1") return R1;
            if (id == "trigger-l2") return L2;
            if (id == "trigger-r2") return R2;
            if (id == "button-create") return Create;
            if (id == "button-options") return Options;
            if (id == "button-ps") return Ps;
            if (id == "button-mic") return Microphone;
            if (id == "touchpad-surface") return TouchpadSurface;
            if (id == "touchpad-button") return TouchpadButton;
            return 0;
        }
    }

    [DataContract]
    public sealed class DualSenseRegionsDocument
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "sourceImage")] public string SourceImage { get; set; }
        [DataMember(Name = "imageWidth")] public int ImageWidth { get; set; }
        [DataMember(Name = "imageHeight")] public int ImageHeight { get; set; }
        [DataMember(Name = "regions")] public List<DualSenseRegionDefinition> Regions { get; set; }
        [DataMember(Name = "motionRanges")] public List<DualSenseMotionRangeDefinition> MotionRanges { get; set; }
        [DataMember(Name = "visualStyleDefaults")] public Dictionary<string, string> VisualStyleDefaults { get; set; }
        [DataMember(Name = "touchSensor")] public DualSenseTouchSensorDefinition TouchSensor { get; set; }
    }

    [DataContract]
    public sealed class DualSenseRegionDefinition
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "kind")] public string Kind { get; set; }
        [DataMember(Name = "style")] public string Style { get; set; }
        [DataMember(Name = "commands")] public List<DualSensePathCommand> Commands { get; set; }
        [DataMember(Name = "ellipse")] public DualSenseEllipseDefinition Ellipse { get; set; }
        [DataMember(Name = "sharedGeometryId")] public string SharedGeometryId { get; set; }
        [DataMember(Name = "motionId")] public string MotionId { get; set; }
    }

    [DataContract]
    public sealed class DualSensePathCommand
    {
        [DataMember(Name = "op")] public string Op { get; set; }
        [DataMember(Name = "x")] public double X { get; set; }
        [DataMember(Name = "y")] public double Y { get; set; }
        [DataMember(Name = "cx")] public double CX { get; set; }
        [DataMember(Name = "cy")] public double CY { get; set; }
        [DataMember(Name = "c1x")] public double C1X { get; set; }
        [DataMember(Name = "c1y")] public double C1Y { get; set; }
        [DataMember(Name = "c2x")] public double C2X { get; set; }
        [DataMember(Name = "c2y")] public double C2Y { get; set; }
    }

    [DataContract]
    public sealed class DualSenseEllipseDefinition
    {
        [DataMember(Name = "cx")] public double CX { get; set; }
        [DataMember(Name = "cy")] public double CY { get; set; }
        [DataMember(Name = "rx")] public double RX { get; set; }
        [DataMember(Name = "ry")] public double RY { get; set; }
    }

    [DataContract]
    public sealed class DualSenseMotionRangeDefinition
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "accent")] public string Accent { get; set; }
        [DataMember(Name = "travelX")] public double TravelX { get; set; }
        [DataMember(Name = "travelY")] public double TravelY { get; set; }
        [DataMember(Name = "socket")] public DualSenseEllipseDefinition Socket { get; set; }
        [DataMember(Name = "cap")] public DualSenseEllipseDefinition Cap { get; set; }
        [DataMember(Name = "pressRegionId")] public string PressRegionId { get; set; }
    }

    [DataContract]
    public sealed class DualSenseTouchSensorDefinition
    {
        [DataMember(Name = "rawWidth")] public int RawWidth { get; set; }
        [DataMember(Name = "rawHeight")] public int RawHeight { get; set; }
        [DataMember(Name = "topLeft")] public DualSenseLogicalPoint TopLeft { get; set; }
        [DataMember(Name = "topRight")] public DualSenseLogicalPoint TopRight { get; set; }
        [DataMember(Name = "bottomLeft")] public DualSenseLogicalPoint BottomLeft { get; set; }
        [DataMember(Name = "bottomRight")] public DualSenseLogicalPoint BottomRight { get; set; }
        // Legacy rectangular values are retained only to load older user overrides. Runtime mapping
        // prefers the four calibrated corners above and never uses a second layout transform.
        [DataMember(Name = "x")] public double X { get; set; }
        [DataMember(Name = "y")] public double Y { get; set; }
        [DataMember(Name = "width")] public double Width { get; set; }
        [DataMember(Name = "height")] public double Height { get; set; }
    }

    [DataContract]
    public sealed class DualSenseLogicalPoint
    {
        [DataMember(Name = "x")] public double X { get; set; }
        [DataMember(Name = "y")] public double Y { get; set; }
    }

    [DataContract]
    public sealed class DualSenseVisualStylesDocument
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "styles")] public List<DualSenseVisualStyleDefinition> Styles { get; set; }
    }

    [DataContract]
    public sealed class DualSenseVisualStyleDefinition
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "fillOpacity")] public double FillOpacity { get; set; }
        [DataMember(Name = "strokeOpacity")] public double StrokeOpacity { get; set; }
        [DataMember(Name = "strokePixels")] public double StrokePixels { get; set; }
        [DataMember(Name = "glowOpacity")] public double GlowOpacity { get; set; }
        [DataMember(Name = "glowPixels")] public double GlowPixels { get; set; }
    }

    [DataContract]
    public sealed class DualSenseRegionsOverride
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "sourceImage")] public string SourceImage { get; set; }
        [DataMember(Name = "imageWidth")] public int ImageWidth { get; set; }
        [DataMember(Name = "imageHeight")] public int ImageHeight { get; set; }
        [DataMember(Name = "regions")] public List<DualSenseRegionDefinition> Regions { get; set; }
        [DataMember(Name = "motionRanges")] public List<DualSenseMotionRangeDefinition> MotionRanges { get; set; }
        [DataMember(Name = "styles")] public List<DualSenseVisualStyleDefinition> Styles { get; set; }
    }

    public sealed class DualSenseRegionManager
    {
        public const int LogicalWidth = 1536;
        public const int LogicalHeight = 1024;
        private const string RegionsResource = "ControllerLab.Assets.dualSenseRegions.json";
        private const string StylesResource = "ControllerLab.Assets.dualSenseVisualStyles.json";
        private readonly HashSet<string> modifiedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> modifiedMotionRanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool stylesModified;
        private DualSenseRegionsDocument defaults;
        private DualSenseRegionsDocument document;
        private DualSenseVisualStylesDocument styles;
        private Dictionary<string, DualSenseRegionDefinition> regions;
        private Dictionary<string, DualSenseMotionRangeDefinition> motionRanges;
        private bool skipUserOverride;
        private readonly DualSenseTouchVisualizer touchVisualizer = new DualSenseTouchVisualizer();

        public string LastLoadMessage { get; private set; }
        public DualSenseRegionsDocument Document { get { return document; } }
        public DualSenseVisualStylesDocument Styles { get { return styles; } }
        public bool HasVisibleTouchContacts { get { return touchVisualizer.HasVisibleContacts; } }

        public bool UpdateTouchVisualizer(InputSnapshot state, bool reducedMotion)
        {
            return touchVisualizer.Update(state, reducedMotion) | touchVisualizer.Advance(DateTime.UtcNow, reducedMotion);
        }

        public void ResetTouchVisualizer()
        {
            touchVisualizer.Reset();
        }

        public Point MapTouchPoint(DualSenseTouchPoint point)
        {
            if (point == null || document == null || document.TouchSensor == null) return new Point(double.NaN, double.NaN);
            return DualSenseTouchVisualizer.Map(document.TouchSensor, point.X, point.Y);
        }

        public static DualSenseRegionManager Load(bool ignoreUserOverride = false)
        {
            DualSenseRegionManager manager = new DualSenseRegionManager();
            manager.skipUserOverride = ignoreUserOverride;
            manager.Reload();
            return manager;
        }

        // Development verification for the production Geometry pipeline. It validates that all
        // logical DS5 regions resolve in the single 1536×1024 source space without a region-level
        // transform, and that the same uniform stage matrix remains valid at supported DPI scales.
        public static string RunOverlayGeometrySelfTest()
        {
            DualSenseRegionManager manager = Load(true);
            string[] regionIds =
            {
                "dpad-up", "dpad-down", "dpad-left", "dpad-right",
                "button-triangle", "button-circle", "button-cross", "button-square",
                "button-l1", "button-r1", "trigger-l2", "trigger-r2",
                "button-l3", "button-r3", "button-create", "button-options",
                "button-ps", "button-mic", "touchpad-surface", "touchpad-button"
            };
            if (manager.document == null || manager.document.ImageWidth != LogicalWidth || manager.document.ImageHeight != LogicalHeight) throw new InvalidOperationException("DS5 source coordinate system is not 1536×1024.");
            for (int i = 0; i < regionIds.Length; i++)
            {
                Geometry geometry = manager.GetGeometry(regionIds[i]);
                if (geometry == null || geometry.Bounds.IsEmpty || geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0) throw new InvalidOperationException("Missing DS5 hit geometry: " + regionIds[i]);
                Matrix local = geometry.Transform == null ? Matrix.Identity : geometry.Transform.Value;
                if (!local.IsIdentity) throw new InvalidOperationException("Region-level transform is not allowed: " + regionIds[i]);
                Rect bounds = geometry.Bounds;
                if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > LogicalWidth || bounds.Bottom > LogicalHeight) throw new InvalidOperationException("DS5 hit geometry escapes source stage: " + regionIds[i]);
            }
            DualSenseRegionDefinition surface = manager.GetRegion("touchpad-surface");
            DualSenseRegionDefinition button = manager.GetRegion("touchpad-button");
            if (surface == null || button == null || !string.Equals(button.SharedGeometryId, surface.Id, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Touchpad surface/button must share one Geometry.");
            VerifyMotionGeometry(manager, "stick-left", "button-l3");
            VerifyMotionGeometry(manager, "stick-right", "button-r3");

            double[,] sizes = { { 1920, 1080 }, { 1440, 900 }, { 1280, 768 }, { 1024, 768 } };
            double[] dpis = { 1.0, 1.25, 1.5 };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                for (int dpi = 0; dpi < dpis.Length; dpi++)
                {
                    Matrix matrix = manager.GetStageMatrix(sizes[size, 0], sizes[size, 1], dpis[dpi]);
                    double expectedScale = Math.Min(sizes[size, 0] / LogicalWidth, sizes[size, 1] / LogicalHeight);
                    if (Math.Abs(matrix.M11 - expectedScale) > 0.000001 || Math.Abs(matrix.M22 - expectedScale) > 0.000001 || Math.Abs(matrix.M12) > 0.000001 || Math.Abs(matrix.M21) > 0.000001) throw new InvalidOperationException("Non-uniform DS5 stage matrix at DPI " + dpis[dpi].ToString(CultureInfo.InvariantCulture));
                }
            }
            return "DS5 overlay self-test passed: 20 fixed hit regions, 2 motion regions, shared touchpad geometry, no region transforms, 4 client sizes × 3 DPI scales.";
        }

        private static void VerifyMotionGeometry(DualSenseRegionManager manager, string motionId, string regionId)
        {
            DualSenseMotionRangeDefinition motion = manager.GetMotion(motionId);
            Geometry region = manager.GetGeometry(regionId);
            if (motion == null || motion.Cap == null || region == null) throw new InvalidOperationException("Missing motion geometry: " + motionId);
            Rect bounds = region.Bounds;
            if (Math.Abs(bounds.X - (motion.Cap.CX - motion.Cap.RX)) > 0.001 || Math.Abs(bounds.Y - (motion.Cap.CY - motion.Cap.RY)) > 0.001 || Math.Abs(bounds.Width - motion.Cap.RX * 2) > 0.001 || Math.Abs(bounds.Height - motion.Cap.RY * 2) > 0.001) throw new InvalidOperationException("L3/R3 geometry does not reuse the measured stick cap: " + regionId);
        }

        public void Reload()
        {
            defaults = ReadEmbedded<DualSenseRegionsDocument>(RegionsResource);
            document = Clone(defaults);
            styles = ReadEmbedded<DualSenseVisualStylesDocument>(StylesResource);
            LastLoadMessage = "已加载默认 DS5 区域数据";
            BuildIndexes();
            if (!skipUserOverride) LoadUserOverride();
        }

        public void Draw(DrawingContext dc, ImageSource photo, BitmapSource leftCap, BitmapSource rightCap, DualSenseOverlayState state, double availableWidth, double availableHeight, double dpiScale)
        {
            if (dc == null || photo == null || availableWidth < 2 || availableHeight < 2) return;
            Matrix stageMatrix = GetStageMatrix(availableWidth, availableHeight, dpiScale);
            double scale = stageMatrix.M11;
            if (scale <= 0) return;

            dc.PushTransform(new MatrixTransform(stageMatrix));
            RadialGradientBrush floor = new RadialGradientBrush(Color.FromArgb(80, 25, 104, 164), Color.FromArgb(0, 7, 17, 25));
            dc.DrawEllipse(floor, null, new Point(768, 900), 610, 105);
            dc.DrawImage(photo, new Rect(0, 0, LogicalWidth, LogicalHeight));

            if (document.Regions != null)
            {
                for (int i = 0; i < document.Regions.Count; i++)
                {
                    DualSenseRegionDefinition region = document.Regions[i];
                    if (region == null || string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase)) continue;
                    double level = state.ValueFor(region.Id);
                    if (string.Equals(region.Id, "touchpad-surface", StringComparison.OrdinalIgnoreCase) && touchVisualizer.HasVisibleContacts) level = 1.0;
                    if (level <= 0.001) continue;
                    Geometry shape = GetGeometry(region.Id);
                    if (shape == null) continue;
                    Color accent = AccentFor(region.Id);
                    if (string.Equals(region.Style, "analog", StringComparison.OrdinalIgnoreCase)) DrawAnalogRegion(dc, shape, region, level, accent, scale, state.ReducedMotion);
                    else DrawRegion(dc, shape, region.Style, level, accent, scale, state.ReducedMotion);
                }
            }
            touchVisualizer.Draw(dc, GetGeometry("touchpad-surface"), document.TouchSensor, scale, state.ReducedMotion);
            DrawMotionRegion(dc, GetMotion("stick-left"), leftCap, state.LeftX, state.LeftY, state.L3, scale, state.ReducedMotion);
            DrawMotionRegion(dc, GetMotion("stick-right"), rightCap, state.RightX, state.RightY, state.R3, scale, state.ReducedMotion);
            dc.Pop();
        }

        private static double Snap(double value, double dpiScale)
        {
            return dpiScale > 0 ? Math.Round(value * dpiScale) / dpiScale : value;
        }

        private void DrawRegion(DrawingContext dc, Geometry shape, string styleId, double level, Color accent, double scale, bool reducedMotion)
        {
            DualSenseVisualStyleDefinition style = GetStyle(styleId);
            if (style == null) return;
            level = Math.Max(0, Math.Min(1, level));
            double sourceStroke = style.StrokePixels / Math.Max(0.001, scale);
            if (!reducedMotion && style.GlowOpacity > 0)
            {
                Pen glow = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * style.GlowOpacity * level), accent.R, accent.G, accent.B)), style.GlowPixels / Math.Max(0.001, scale));
                glow.LineJoin = PenLineJoin.Round;
                dc.DrawGeometry(null, glow, shape);
            }
            // Keep the color field inside the physical button geometry. Stroke and the intentionally
            // subdued outer halo are separate layers, so the fill itself never leaks past the path.
            Brush fill = new SolidColorBrush(Color.FromArgb((byte)(255 * style.FillOpacity * level), accent.R, accent.G, accent.B));
            dc.PushClip(shape);
            dc.DrawGeometry(fill, null, shape);
            dc.Pop();
            Pen stroke = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * style.StrokeOpacity * level), accent.R, accent.G, accent.B)), sourceStroke);
            stroke.LineJoin = PenLineJoin.Round;
            dc.DrawGeometry(null, stroke, shape);
        }

        private void DrawAnalogRegion(DrawingContext dc, Geometry shape, DualSenseRegionDefinition region, double level, Color accent, double scale, bool reducedMotion)
        {
            DrawRegion(dc, shape, "analog", level, accent, scale, reducedMotion);
            Rect b = shape.Bounds;
            double width = b.Width * Math.Max(0, Math.Min(1, level));
            Rect fill = region.Id == "trigger-r2" ? new Rect(b.Right - width, b.Top, width, b.Height) : new Rect(b.Left, b.Top, width, b.Height);
            dc.PushClip(shape);
            LinearGradientBrush brush = new LinearGradientBrush(Color.FromArgb(36, accent.R, accent.G, accent.B), Color.FromArgb(150, accent.R, accent.G, accent.B), new Point(0, 0), new Point(1, 0));
            dc.DrawRectangle(brush, null, fill);
            dc.Pop();
        }

        private void DrawMotionRegion(DrawingContext dc, DualSenseMotionRangeDefinition motion, BitmapSource capImage, double x, double y, double pressed, double scale, bool reducedMotion)
        {
            if (motion == null || motion.Socket == null || motion.Cap == null) return;
            Color accent = Palette.Blue;
            double magnitude = Math.Min(1.0, Math.Sqrt(x * x + y * y));
            Point center = new Point(motion.Cap.CX, motion.Cap.CY);
            Point moved = new Point(center.X + x * motion.TravelX, center.Y - y * motion.TravelY + pressed * 2.0);
            EllipseGeometry socket = Ellipse(motion.Socket);
            if (magnitude > 0.01 || pressed > 0.01)
            {
                DrawRegion(dc, socket, "active", Math.Max(magnitude, pressed * 0.8), accent, scale, reducedMotion);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, accent.R, accent.G, accent.B)), 1.0 / Math.Max(0.001, scale)), center, moved);
            }
            RadialGradientBrush cavity = new RadialGradientBrush(Color.FromRgb(35, 43, 49), Color.FromRgb(9, 13, 17));
            dc.DrawEllipse(cavity, null, center, motion.Cap.RX + 5, motion.Cap.RY + 5);
            if (capImage != null) dc.DrawImage(capImage, new Rect(moved.X - motion.Cap.RX, moved.Y - motion.Cap.RY, motion.Cap.RX * 2, motion.Cap.RY * 2));
            if (pressed > 0.001)
            {
                EllipseGeometry cap = new EllipseGeometry(moved, motion.Cap.RX * 0.90, motion.Cap.RY * 0.90);
                DrawRegion(dc, cap, "pressed", pressed, accent, scale, reducedMotion);
            }
        }

        public Geometry GetGeometry(string id)
        {
            DualSenseRegionDefinition region;
            if (string.IsNullOrEmpty(id) || !regions.TryGetValue(id, out region)) return null;
            if (string.Equals(region.Kind, "shared", StringComparison.OrdinalIgnoreCase)) return GetGeometry(region.SharedGeometryId);
            if (string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase))
            {
                DualSenseMotionRangeDefinition motion = GetMotion(region.MotionId);
                return motion == null ? null : Ellipse(motion.Cap);
            }
            if (string.Equals(region.Kind, "ellipse", StringComparison.OrdinalIgnoreCase)) return Ellipse(region.Ellipse);
            return BuildPathGeometry(region.Commands);
        }

        private static EllipseGeometry Ellipse(DualSenseEllipseDefinition ellipse)
        {
            return ellipse == null ? null : new EllipseGeometry(new Point(ellipse.CX, ellipse.CY), ellipse.RX, ellipse.RY);
        }

        private static Geometry BuildPathGeometry(List<DualSensePathCommand> commands)
        {
            if (commands == null || commands.Count == 0 || !string.Equals(commands[0].Op, "M", StringComparison.OrdinalIgnoreCase)) return null;
            bool closed = false;
            for (int i = 0; i < commands.Count; i++) if (string.Equals(commands[i].Op, "Z", StringComparison.OrdinalIgnoreCase)) closed = true;
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(new Point(commands[0].X, commands[0].Y), true, closed);
                for (int i = 1; i < commands.Count; i++)
                {
                    DualSensePathCommand command = commands[i];
                    if (string.Equals(command.Op, "L", StringComparison.OrdinalIgnoreCase)) context.LineTo(new Point(command.X, command.Y), true, false);
                    else if (string.Equals(command.Op, "Q", StringComparison.OrdinalIgnoreCase)) context.QuadraticBezierTo(new Point(command.CX, command.CY), new Point(command.X, command.Y), true, false);
                    else if (string.Equals(command.Op, "C", StringComparison.OrdinalIgnoreCase)) context.BezierTo(new Point(command.C1X, command.C1Y), new Point(command.C2X, command.C2Y), new Point(command.X, command.Y), true, false);
                }
            }
            return geometry;
        }

        public DualSenseRegionDefinition GetRegion(string id)
        {
            DualSenseRegionDefinition result;
            return id != null && regions.TryGetValue(id, out result) ? result : null;
        }

        public DualSenseMotionRangeDefinition GetMotion(string id)
        {
            DualSenseMotionRangeDefinition result;
            return id != null && motionRanges.TryGetValue(id, out result) ? result : null;
        }

        public Matrix GetStageMatrix(double availableWidth, double availableHeight, double dpiScale)
        {
            double scale = Math.Min(availableWidth / LogicalWidth, availableHeight / LogicalHeight);
            double width = LogicalWidth * scale;
            double height = LogicalHeight * scale;
            return new Matrix(scale, 0, 0, scale, Snap((availableWidth - width) * 0.5, dpiScale), Snap((availableHeight - height) * 0.5 + 4.0, dpiScale));
        }

        public string HitTest(Point source)
        {
            if (document.Regions == null) return null;
            for (int i = document.Regions.Count - 1; i >= 0; i--)
            {
                DualSenseRegionDefinition region = document.Regions[i];
                if (region == null || string.Equals(region.Kind, "shared", StringComparison.OrdinalIgnoreCase)) continue;
                Geometry shape = GetGeometry(region.Id);
                if (shape != null && shape.FillContains(source)) return region.Id;
            }
            return null;
        }

        public List<DualSenseCalibrationHandle> GetHandles(string id)
        {
            List<DualSenseCalibrationHandle> handles = new List<DualSenseCalibrationHandle>();
            DualSenseRegionDefinition region = GetRegion(id);
            if (region == null) return handles;
            if (string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase))
            {
                DualSenseMotionRangeDefinition motion = GetMotion(region.MotionId);
                if (motion != null && motion.Cap != null)
                {
                    handles.Add(new DualSenseCalibrationHandle { Key = "motion-center", Point = new Point(motion.Cap.CX, motion.Cap.CY) });
                    handles.Add(new DualSenseCalibrationHandle { Key = "motion-x-radius", Point = new Point(motion.Cap.CX + motion.Cap.RX, motion.Cap.CY) });
                    handles.Add(new DualSenseCalibrationHandle { Key = "motion-y-radius", Point = new Point(motion.Cap.CX, motion.Cap.CY + motion.Cap.RY) });
                }
                return handles;
            }
            if (region.Ellipse != null)
            {
                handles.Add(new DualSenseCalibrationHandle { Key = "ellipse-center", Point = new Point(region.Ellipse.CX, region.Ellipse.CY) });
                handles.Add(new DualSenseCalibrationHandle { Key = "ellipse-x-radius", Point = new Point(region.Ellipse.CX + region.Ellipse.RX, region.Ellipse.CY) });
                handles.Add(new DualSenseCalibrationHandle { Key = "ellipse-y-radius", Point = new Point(region.Ellipse.CX, region.Ellipse.CY + region.Ellipse.RY) });
                return handles;
            }
            if (region.Commands == null) return handles;
            for (int i = 0; i < region.Commands.Count; i++)
            {
                DualSensePathCommand command = region.Commands[i];
                if (command == null || string.Equals(command.Op, "Z", StringComparison.OrdinalIgnoreCase)) continue;
                handles.Add(new DualSenseCalibrationHandle { Key = "end", CommandIndex = i, Point = new Point(command.X, command.Y) });
                if (string.Equals(command.Op, "Q", StringComparison.OrdinalIgnoreCase)) handles.Add(new DualSenseCalibrationHandle { Key = "control", CommandIndex = i, Point = new Point(command.CX, command.CY) });
                if (string.Equals(command.Op, "C", StringComparison.OrdinalIgnoreCase))
                {
                    handles.Add(new DualSenseCalibrationHandle { Key = "control1", CommandIndex = i, Point = new Point(command.C1X, command.C1Y) });
                    handles.Add(new DualSenseCalibrationHandle { Key = "control2", CommandIndex = i, Point = new Point(command.C2X, command.C2Y) });
                }
            }
            return handles;
        }

        public void MoveRegion(string id, double dx, double dy)
        {
            DualSenseRegionDefinition region = GetRegion(id);
            if (region == null) return;
            if (string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase))
            {
                DualSenseMotionRangeDefinition motion = GetMotion(region.MotionId);
                if (motion != null && motion.Cap != null) { motion.Cap.CX += dx; motion.Cap.CY += dy; MarkMotionModified(motion.Id); }
                return;
            }
            if (region.Ellipse != null) { region.Ellipse.CX += dx; region.Ellipse.CY += dy; }
            if (region.Commands != null)
            {
                for (int i = 0; i < region.Commands.Count; i++)
                {
                    DualSensePathCommand command = region.Commands[i];
                    if (command == null || string.Equals(command.Op, "Z", StringComparison.OrdinalIgnoreCase)) continue;
                    command.X += dx; command.Y += dy;
                    if (string.Equals(command.Op, "Q", StringComparison.OrdinalIgnoreCase)) { command.CX += dx; command.CY += dy; }
                    if (string.Equals(command.Op, "C", StringComparison.OrdinalIgnoreCase)) { command.C1X += dx; command.C1Y += dy; command.C2X += dx; command.C2Y += dy; }
                }
            }
            MarkRegionModified(id);
        }

        public void MoveHandle(string id, DualSenseCalibrationHandle handle, double x, double y)
        {
            if (handle == null) return;
            DualSenseRegionDefinition region = GetRegion(id);
            if (region == null) return;
            if (string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase))
            {
                DualSenseMotionRangeDefinition motion = GetMotion(region.MotionId);
                if (motion == null || motion.Cap == null) return;
                if (handle.Key == "motion-center") { motion.Cap.CX = x; motion.Cap.CY = y; }
                else if (handle.Key == "motion-x-radius") motion.Cap.RX = Math.Max(2, Math.Abs(x - motion.Cap.CX));
                else if (handle.Key == "motion-y-radius") motion.Cap.RY = Math.Max(2, Math.Abs(y - motion.Cap.CY));
                MarkMotionModified(motion.Id);
                return;
            }
            if (region.Ellipse != null)
            {
                if (handle.Key == "ellipse-center") { region.Ellipse.CX = x; region.Ellipse.CY = y; }
                else if (handle.Key == "ellipse-x-radius") region.Ellipse.RX = Math.Max(2, Math.Abs(x - region.Ellipse.CX));
                else if (handle.Key == "ellipse-y-radius") region.Ellipse.RY = Math.Max(2, Math.Abs(y - region.Ellipse.CY));
                MarkRegionModified(id);
                return;
            }
            if (region.Commands == null || handle.CommandIndex < 0 || handle.CommandIndex >= region.Commands.Count) return;
            DualSensePathCommand command = region.Commands[handle.CommandIndex];
            if (handle.Key == "end") { command.X = x; command.Y = y; }
            else if (handle.Key == "control") { command.CX = x; command.CY = y; }
            else if (handle.Key == "control1") { command.C1X = x; command.C1Y = y; }
            else if (handle.Key == "control2") { command.C2X = x; command.C2Y = y; }
            MarkRegionModified(id);
        }

        public DualSenseCalibrationSnapshot CreateSnapshot()
        {
            DualSenseCalibrationSnapshot result = new DualSenseCalibrationSnapshot();
            result.Document = Clone(document);
            result.Styles = Clone(styles);
            result.ModifiedRegions = new List<string>(modifiedRegions);
            result.ModifiedMotionRanges = new List<string>(modifiedMotionRanges);
            result.StylesModified = stylesModified;
            return result;
        }

        public void RestoreSnapshot(DualSenseCalibrationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Document == null) return;
            document = Clone(snapshot.Document);
            styles = Clone(snapshot.Styles);
            modifiedRegions.Clear();
            modifiedMotionRanges.Clear();
            if (snapshot.ModifiedRegions != null) for (int i = 0; i < snapshot.ModifiedRegions.Count; i++) modifiedRegions.Add(snapshot.ModifiedRegions[i]);
            if (snapshot.ModifiedMotionRanges != null) for (int i = 0; i < snapshot.ModifiedMotionRanges.Count; i++) modifiedMotionRanges.Add(snapshot.ModifiedMotionRanges[i]);
            stylesModified = snapshot.StylesModified;
            BuildIndexes();
        }

        public void MarkRegionModified(string id)
        {
            if (!string.IsNullOrEmpty(id)) modifiedRegions.Add(id);
            BuildIndexes();
        }

        public void MarkMotionModified(string id)
        {
            if (!string.IsNullOrEmpty(id)) modifiedMotionRanges.Add(id);
            BuildIndexes();
        }

        public void MarkStylesModified()
        {
            stylesModified = true;
        }

        public void ResetRegion(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            DualSenseRegionDefinition original = FindRegion(defaults.Regions, id);
            if (original != null) ReplaceRegion(Clone(original));
            else
            {
                DualSenseMotionRangeDefinition originalMotion = FindMotion(defaults.MotionRanges, id);
                if (originalMotion != null) ReplaceMotion(Clone(originalMotion));
            }
            modifiedRegions.Remove(id);
            modifiedMotionRanges.Remove(id);
            BuildIndexes();
        }

        public bool SaveUserOverride(out string message)
        {
            try
            {
                DualSenseRegionsOverride output = new DualSenseRegionsOverride
                {
                    SchemaVersion = document.SchemaVersion,
                    SourceImage = document.SourceImage,
                    ImageWidth = document.ImageWidth,
                    ImageHeight = document.ImageHeight,
                    Regions = new List<DualSenseRegionDefinition>(),
                    MotionRanges = new List<DualSenseMotionRangeDefinition>(),
                    Styles = stylesModified && styles != null ? Clone(styles.Styles) : new List<DualSenseVisualStyleDefinition>()
                };
                foreach (string id in modifiedRegions)
                {
                    DualSenseRegionDefinition region = GetRegion(id);
                    if (region != null) output.Regions.Add(Clone(region));
                }
                foreach (string id in modifiedMotionRanges)
                {
                    DualSenseMotionRangeDefinition motion = GetMotion(id);
                    if (motion != null) output.MotionRanges.Add(Clone(motion));
                }
                string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxControllerLab");
                Directory.CreateDirectory(directory);
                WriteJson(System.IO.Path.Combine(directory, "dualSense-regions.override.json"), output);
                message = "已保存 DS5 用户校准覆盖";
                return true;
            }
            catch (Exception ex)
            {
                message = "保存 DS5 校准失败：" + ex.Message;
                return false;
            }
        }

        public bool ExportDocument(string path, out string message)
        {
            try { WriteJson(path, document); message = "已导出完整 DS5 Geometry 数据"; return true; }
            catch (Exception ex) { message = "导出失败：" + ex.Message; return false; }
        }

        public bool ImportDocument(string path, out string message)
        {
            try
            {
                DualSenseRegionsDocument imported = ReadFile<DualSenseRegionsDocument>(path);
                string reason;
                if (!ValidateDocument(imported, out reason)) { message = "导入已忽略：" + reason; return false; }
                document = imported;
                modifiedRegions.Clear();
                modifiedMotionRanges.Clear();
                for (int i = 0; i < document.Regions.Count; i++) modifiedRegions.Add(document.Regions[i].Id);
                for (int i = 0; i < document.MotionRanges.Count; i++) modifiedMotionRanges.Add(document.MotionRanges[i].Id);
                BuildIndexes();
                message = "已导入 Geometry，等待保存覆盖文件";
                return true;
            }
            catch (Exception ex) { message = "导入失败：" + ex.Message; return false; }
        }

        private void LoadUserOverride()
        {
            string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxControllerLab", "dualSense-regions.override.json");
            if (!File.Exists(path)) return;
            try
            {
                DualSenseRegionsOverride saved = ReadFile<DualSenseRegionsOverride>(path);
                if (saved == null || saved.SchemaVersion != document.SchemaVersion || saved.ImageWidth != document.ImageWidth || saved.ImageHeight != document.ImageHeight || !string.Equals(saved.SourceImage, document.SourceImage, StringComparison.OrdinalIgnoreCase))
                {
                    LastLoadMessage = "已忽略不兼容的 DS5 用户校准覆盖";
                    return;
                }
                if (saved.Regions != null)
                {
                    for (int i = 0; i < saved.Regions.Count; i++)
                    {
                        DualSenseRegionDefinition current = saved.Regions[i];
                        if (current != null && FindRegion(defaults.Regions, current.Id) != null) { ReplaceRegion(current); modifiedRegions.Add(current.Id); }
                    }
                }
                if (saved.MotionRanges != null)
                {
                    for (int i = 0; i < saved.MotionRanges.Count; i++)
                    {
                        DualSenseMotionRangeDefinition current = saved.MotionRanges[i];
                        if (current != null && FindMotion(defaults.MotionRanges, current.Id) != null) { ReplaceMotion(current); modifiedMotionRanges.Add(current.Id); }
                    }
                }
                if (saved.Styles != null && saved.Styles.Count > 0) { styles.Styles = saved.Styles; stylesModified = true; }
                BuildIndexes();
                LastLoadMessage = "已加载 DS5 用户校准覆盖";
            }
            catch
            {
                LastLoadMessage = "DS5 用户校准覆盖无效，已使用默认 Geometry";
            }
        }

        private bool ValidateDocument(DualSenseRegionsDocument candidate, out string reason)
        {
            reason = null;
            if (candidate == null || candidate.SchemaVersion != defaults.SchemaVersion) { reason = "schemaVersion 不匹配"; return false; }
            if (candidate.ImageWidth != LogicalWidth || candidate.ImageHeight != LogicalHeight || !string.Equals(candidate.SourceImage, defaults.SourceImage, StringComparison.OrdinalIgnoreCase)) { reason = "底图尺寸或名称不匹配"; return false; }
            if (candidate.Regions == null || candidate.MotionRanges == null) { reason = "regions 或 motionRanges 缺失"; return false; }
            for (int i = 0; i < defaults.Regions.Count; i++) if (FindRegion(candidate.Regions, defaults.Regions[i].Id) == null) { reason = "缺少区域：" + defaults.Regions[i].Id; return false; }
            for (int i = 0; i < defaults.MotionRanges.Count; i++) if (FindMotion(candidate.MotionRanges, defaults.MotionRanges[i].Id) == null) { reason = "缺少运动范围：" + defaults.MotionRanges[i].Id; return false; }
            return true;
        }

        private void ReplaceRegion(DualSenseRegionDefinition value)
        {
            for (int i = 0; i < document.Regions.Count; i++) if (string.Equals(document.Regions[i].Id, value.Id, StringComparison.OrdinalIgnoreCase)) { document.Regions[i] = Clone(value); return; }
        }

        private void ReplaceMotion(DualSenseMotionRangeDefinition value)
        {
            for (int i = 0; i < document.MotionRanges.Count; i++) if (string.Equals(document.MotionRanges[i].Id, value.Id, StringComparison.OrdinalIgnoreCase)) { document.MotionRanges[i] = Clone(value); return; }
        }

        private void BuildIndexes()
        {
            regions = new Dictionary<string, DualSenseRegionDefinition>(StringComparer.OrdinalIgnoreCase);
            motionRanges = new Dictionary<string, DualSenseMotionRangeDefinition>(StringComparer.OrdinalIgnoreCase);
            if (document.Regions != null) for (int i = 0; i < document.Regions.Count; i++) if (document.Regions[i] != null && !string.IsNullOrEmpty(document.Regions[i].Id)) regions[document.Regions[i].Id] = document.Regions[i];
            if (document.MotionRanges != null) for (int i = 0; i < document.MotionRanges.Count; i++) if (document.MotionRanges[i] != null && !string.IsNullOrEmpty(document.MotionRanges[i].Id)) motionRanges[document.MotionRanges[i].Id] = document.MotionRanges[i];
        }

        private DualSenseVisualStyleDefinition GetStyle(string id)
        {
            if (styles == null || styles.Styles == null) return null;
            for (int i = 0; i < styles.Styles.Count; i++) if (string.Equals(styles.Styles[i].Id, id, StringComparison.OrdinalIgnoreCase)) return styles.Styles[i];
            return styles.Styles.Count > 0 ? styles.Styles[0] : null;
        }

        public DualSenseVisualStyleDefinition FindStyle(string id)
        {
            return GetStyle(id);
        }

        private static Color AccentFor(string id)
        {
            return Palette.Blue;
        }

        private static DualSenseRegionDefinition FindRegion(List<DualSenseRegionDefinition> values, string id)
        {
            if (values == null) return null;
            for (int i = 0; i < values.Count; i++) if (values[i] != null && string.Equals(values[i].Id, id, StringComparison.OrdinalIgnoreCase)) return values[i];
            return null;
        }

        private static DualSenseMotionRangeDefinition FindMotion(List<DualSenseMotionRangeDefinition> values, string id)
        {
            if (values == null) return null;
            for (int i = 0; i < values.Count; i++) if (values[i] != null && string.Equals(values[i].Id, id, StringComparison.OrdinalIgnoreCase)) return values[i];
            return null;
        }

        private static T ReadEmbedded<T>(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) throw new InvalidOperationException("缺少嵌入资源：" + resourceName);
            try { return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
            finally { stream.Dispose(); }
        }

        private static T ReadFile<T>(string path)
        {
            using (FileStream stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        private static void WriteJson<T>(string path, T value)
        {
            using (FileStream stream = File.Create(path)) new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        }

        private static T Clone<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                serializer.WriteObject(stream, value);
                stream.Position = 0;
                return (T)serializer.ReadObject(stream);
            }
        }
    }

    public sealed class DualSenseCalibrationHandle
    {
        public string Key;
        public int CommandIndex = -1;
        public Point Point;
    }

    public sealed class DualSenseCalibrationSnapshot
    {
        public DualSenseRegionsDocument Document;
        public DualSenseVisualStylesDocument Styles;
        public List<string> ModifiedRegions;
        public List<string> ModifiedMotionRanges;
        public bool StylesModified;
    }

    public sealed class DualSenseCalibrationSurface : FrameworkElement
    {
        private readonly DualSenseRegionManager manager;
        private readonly ImageSource photo;
        private Matrix stageMatrix = Matrix.Identity;
        private string selectedId;
        private DualSenseCalibrationHandle selectedHandle;
        private Point previousSource;
        private Point pointerSource;
        private bool dragging;
        private bool draggingHandle;

        public event Action<string> RegionSelected;
        public event Action EditStarted;
        public event Action<string> CoordinatesChanged;
        public double BackgroundOpacity { get; set; }
        public double OverlayOpacity { get; set; }
        public bool ImageLocked { get; set; }
        // Calibration-only rendering: no fill, no halo and exactly one screen pixel of stroke.
        public bool OutlineCalibrationView { get; set; }
        public string SelectedId { get { return selectedId; } }
        public DualSenseCalibrationHandle SelectedHandle { get { return selectedHandle; } }

        public DualSenseCalibrationSurface(DualSenseRegionManager value, ImageSource image)
        {
            manager = value;
            photo = image;
            BackgroundOpacity = 1.0;
            OverlayOpacity = 0.72;
            ImageLocked = true;
            pointerSource = new Point(DualSenseRegionManager.LogicalWidth * 0.5, DualSenseRegionManager.LogicalHeight * 0.5);
            Focusable = true;
            Cursor = Cursors.Cross;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            KeyDown += OnKeyDown;
        }

        public void Select(string id)
        {
            selectedId = id;
            selectedHandle = null;
            if (RegionSelected != null) RegionSelected(id);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth < 2 || ActualHeight < 2 || manager == null || photo == null) return;
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            stageMatrix = manager.GetStageMatrix(ActualWidth, ActualHeight, dpi);
            double scale = Math.Max(0.001, stageMatrix.M11);
            dc.DrawRectangle(Palette.WindowBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));
            dc.PushTransform(new MatrixTransform(stageMatrix));
            dc.PushOpacity(BackgroundOpacity);
            dc.DrawImage(photo, new Rect(0, 0, DualSenseRegionManager.LogicalWidth, DualSenseRegionManager.LogicalHeight));
            dc.Pop();

            if (manager.Document.Regions != null)
            {
                for (int i = 0; i < manager.Document.Regions.Count; i++)
                {
                    DualSenseRegionDefinition region = manager.Document.Regions[i];
                    if (region == null || string.Equals(region.Kind, "shared", StringComparison.OrdinalIgnoreCase)) continue;
                    Geometry geometry = manager.GetGeometry(region.Id);
                    if (geometry == null) continue;
                    bool selected = string.Equals(region.Id, selectedId, StringComparison.OrdinalIgnoreCase);
                    Color color = selected ? Palette.Green : (OutlineCalibrationView && region.Id.StartsWith("dpad-", StringComparison.OrdinalIgnoreCase) ? Palette.Warning : Palette.Blue);
                    byte fillAlpha = OutlineCalibrationView ? (byte)0 : (byte)(selected ? 42 : 18);
                    double strokePixels = OutlineCalibrationView ? 1.0 : (selected ? 1.7 : 1.0);
                    byte strokeAlpha = OutlineCalibrationView ? (byte)255 : (byte)(selected ? 240 : 135);
                    Pen stroke = new Pen(new SolidColorBrush(Color.FromArgb(strokeAlpha, color.R, color.G, color.B)), strokePixels / scale);
                    stroke.LineJoin = PenLineJoin.Round;
                    dc.PushOpacity(OverlayOpacity);
                    dc.DrawGeometry(fillAlpha == 0 ? null : new SolidColorBrush(Color.FromArgb(fillAlpha, color.R, color.G, color.B)), stroke, geometry);
                    dc.Pop();
                    if (!OutlineCalibrationView)
                    {
                        Rect b = geometry.Bounds;
                        DrawSourceText(dc, region.Id, b.X, Math.Max(12, b.Y - 7), 11 / scale, selected ? Palette.GreenBrush : Palette.BlueBrush);
                    }
                }
            }
            if (!string.IsNullOrEmpty(selectedId)) DrawHandles(dc, scale);
            DrawCrosshair(dc, scale);
            dc.Pop();
            string coordinates = string.Format(CultureInfo.InvariantCulture, "原图坐标  X {0:0.0}   Y {1:0.0}", pointerSource.X, pointerSource.Y);
            DrawScreenText(dc, coordinates, 12, ActualHeight - 24, 11, Palette.TextBrush);
            if (OutlineCalibrationView) DrawScreenText(dc, "实体轮廓校准视图 · 无 Glow · 透明填充 · 1px 描边", 12, 14, 11, Palette.WarningBrush);
            DrawMagnifier(dc);
        }

        private void DrawMagnifier(DrawingContext dc)
        {
            if (pointerSource.X < 0 || pointerSource.Y < 0 || pointerSource.X > DualSenseRegionManager.LogicalWidth || pointerSource.Y > DualSenseRegionManager.LogicalHeight) return;
            const double zoom = 3.0;
            const double radius = 64.0;
            Point center = new Point(Math.Max(radius + 12, ActualWidth - radius - 14), radius + 14);
            EllipseGeometry lens = new EllipseGeometry(center, radius, radius);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 8, 16, 23)), null, center, radius + 3, radius + 3);
            dc.PushClip(lens);
            dc.PushTransform(new MatrixTransform(new Matrix(zoom, 0, 0, zoom, center.X - pointerSource.X * zoom, center.Y - pointerSource.Y * zoom)));
            dc.PushOpacity(BackgroundOpacity);
            dc.DrawImage(photo, new Rect(0, 0, DualSenseRegionManager.LogicalWidth, DualSenseRegionManager.LogicalHeight));
            dc.Pop();
            if (!string.IsNullOrEmpty(selectedId))
            {
                Geometry selected = manager.GetGeometry(selectedId);
                if (selected != null) dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(42, Palette.Green.R, Palette.Green.G, Palette.Green.B)), new Pen(Palette.GreenBrush, 1.15 / zoom), selected);
            }
            dc.Pop();
            dc.Pop();
            dc.DrawEllipse(null, new Pen(Palette.WarningBrush, 1.4), center, radius, radius);
            dc.DrawLine(new Pen(Palette.WarningBrush, 1.0), new Point(center.X - 10, center.Y), new Point(center.X + 10, center.Y));
            dc.DrawLine(new Pen(Palette.WarningBrush, 1.0), new Point(center.X, center.Y - 10), new Point(center.X, center.Y + 10));
            DrawScreenText(dc, "局部放大 3×", center.X - 31, center.Y + radius + 8, 10, Palette.WarningBrush);
        }

        private void DrawHandles(DrawingContext dc, double scale)
        {
            List<DualSenseCalibrationHandle> handles = manager.GetHandles(selectedId);
            for (int i = 0; i < handles.Count; i++)
            {
                DualSenseCalibrationHandle handle = handles[i];
                bool active = handle == selectedHandle;
                Color color = active ? Palette.Warning : Palette.Green;
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)), new Pen(Palette.WindowBrush, 1.0 / scale), handle.Point, 4.5 / scale, 4.5 / scale);
                if (active) DrawSourceText(dc, handle.Key, handle.Point.X + 7 / scale, handle.Point.Y - 8 / scale, 10 / scale, Palette.WarningBrush);
            }
        }

        private void DrawCrosshair(DrawingContext dc, double scale)
        {
            if (pointerSource.X < 0 || pointerSource.Y < 0 || pointerSource.X > DualSenseRegionManager.LogicalWidth || pointerSource.Y > DualSenseRegionManager.LogicalHeight) return;
            Pen pen = new Pen(new SolidColorBrush(Color.FromArgb(150, Palette.Warning.R, Palette.Warning.G, Palette.Warning.B)), 0.8 / scale);
            dc.DrawLine(pen, new Point(pointerSource.X - 18 / scale, pointerSource.Y), new Point(pointerSource.X + 18 / scale, pointerSource.Y));
            dc.DrawLine(pen, new Point(pointerSource.X, pointerSource.Y - 18 / scale), new Point(pointerSource.X, pointerSource.Y + 18 / scale));
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            pointerSource = ToSource(e.GetPosition(this));
            if (e.ChangedButton != MouseButton.Left) return;
            selectedHandle = FindHandle(pointerSource);
            if (selectedHandle != null && !string.IsNullOrEmpty(selectedId))
            {
                BeginEdit();
                dragging = draggingHandle = true;
                previousSource = pointerSource;
                CaptureMouse();
                e.Handled = true;
                return;
            }
            string hit = manager.HitTest(pointerSource);
            if (!string.IsNullOrEmpty(hit))
            {
                Select(hit);
                BeginEdit();
                dragging = true;
                draggingHandle = false;
                previousSource = pointerSource;
                CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            pointerSource = ToSource(e.GetPosition(this));
            if (CoordinatesChanged != null) CoordinatesChanged(string.Format(CultureInfo.InvariantCulture, "X {0:0.0}, Y {1:0.0}", pointerSource.X, pointerSource.Y));
            if (dragging && !string.IsNullOrEmpty(selectedId))
            {
                if (draggingHandle && selectedHandle != null) manager.MoveHandle(selectedId, selectedHandle, pointerSource.X, pointerSource.Y);
                else manager.MoveRegion(selectedId, pointerSource.X - previousSource.X, pointerSource.Y - previousSource.Y);
                previousSource = pointerSource;
            }
            InvalidateVisual();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (dragging) ReleaseMouseCapture();
            dragging = false;
            draggingHandle = false;
            InvalidateVisual();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedId)) return;
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5.0 : 1.0;
            double dx = 0;
            double dy = 0;
            if (e.Key == Key.Left) dx = -step;
            else if (e.Key == Key.Right) dx = step;
            else if (e.Key == Key.Up) dy = -step;
            else if (e.Key == Key.Down) dy = step;
            else return;
            BeginEdit();
            if (selectedHandle != null) manager.MoveHandle(selectedId, selectedHandle, selectedHandle.Point.X + dx, selectedHandle.Point.Y + dy);
            else manager.MoveRegion(selectedId, dx, dy);
            e.Handled = true;
            InvalidateVisual();
        }

        private void BeginEdit()
        {
            if (EditStarted != null) EditStarted();
        }

        private DualSenseCalibrationHandle FindHandle(Point point)
        {
            if (string.IsNullOrEmpty(selectedId)) return null;
            double threshold = 12.0 / Math.Max(0.001, stageMatrix.M11);
            List<DualSenseCalibrationHandle> handles = manager.GetHandles(selectedId);
            for (int i = 0; i < handles.Count; i++)
            {
                double dx = handles[i].Point.X - point.X;
                double dy = handles[i].Point.Y - point.Y;
                if (dx * dx + dy * dy <= threshold * threshold) return handles[i];
            }
            return null;
        }

        private Point ToSource(Point screen)
        {
            Matrix inverse = stageMatrix;
            if (!inverse.HasInverse) return new Point(-1, -1);
            inverse.Invert();
            return inverse.Transform(screen);
        }

        private void DrawSourceText(DrawingContext dc, string text, double x, double y, double size, Brush brush)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), size, brush, 1.0);
            dc.DrawText(ft, new Point(x, y));
        }

        private void DrawScreenText(DrawingContext dc, string text, double x, double y, double size, Brush brush)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }

    public sealed class DualSenseCalibrationWindow : Window
    {
        private readonly DualSenseRegionManager manager;
        private readonly DualSenseCalibrationSurface surface;
        private readonly ListBox regionsList;
        private readonly TextBlock selectedText;
        private readonly TextBlock coordinatesText;
        private readonly TextBlock statusText;
        private readonly Stack<DualSenseCalibrationSnapshot> undo = new Stack<DualSenseCalibrationSnapshot>();
        private readonly Stack<DualSenseCalibrationSnapshot> redo = new Stack<DualSenseCalibrationSnapshot>();
        private DualSenseCalibrationSnapshot committedSnapshot;
        private bool snapshotPending;

        public string StatusMessage { get; private set; }

        public DualSenseCalibrationWindow(DualSenseRegionManager value, ImageSource photo)
        {
            manager = value;
            committedSnapshot = manager.CreateSnapshot();
            Title = "DS5 轮廓校准";
            Width = 1480;
            Height = 920;
            MinWidth = 1120;
            MinHeight = 700;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Palette.WindowBrush;
            Foreground = Palette.TextBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            Grid root = new Grid { Margin = new Thickness(14) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(212) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(282) });

            regionsList = new ListBox { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, Foreground = Palette.TextBrush, Margin = new Thickness(0, 0, 10, 0) };
            PopulateRegionList();
            regionsList.SelectionChanged += delegate { surface.Select(regionsList.SelectedItem as string); RefreshSelected(); };
            root.Children.Add(regionsList);

            Border stageBorder = new Border { Background = Palette.WindowBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) };
            surface = new DualSenseCalibrationSurface(manager, photo);
            surface.RegionSelected += OnSurfaceRegionSelected;
            surface.EditStarted += PushUndo;
            surface.CoordinatesChanged += delegate(string valueText) { coordinatesText.Text = valueText; };
            stageBorder.Child = surface;
            Grid.SetColumn(stageBorder, 1);
            root.Children.Add(stageBorder);

            StackPanel panel = new StackPanel { Background = Palette.SurfaceBrush, Margin = new Thickness(0), Orientation = Orientation.Vertical };
            Border panelBorder = new Border { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Padding = new Thickness(14), Child = panel };
            Grid.SetColumn(panelBorder, 2);
            root.Children.Add(panelBorder);
            selectedText = Text("未选择区域", 14, Palette.TextBrush, true);
            coordinatesText = Text("X 0, Y 0", 11, Palette.MutedBrush, false);
            statusText = Text(manager.LastLoadMessage, 11, Palette.MutedBrush, false);
            panel.Children.Add(selectedText);
            panel.Children.Add(coordinatesText);
            panel.Children.Add(statusText);
            panel.Children.Add(Separator());

            AddButton(panel, "撤销", delegate { Undo(); });
            AddButton(panel, "重做", delegate { Redo(); });
            AddButton(panel, "恢复当前区域默认值", delegate { if (surface.SelectedId != null) { PushUndo(); manager.ResetRegion(surface.SelectedId); surface.InvalidateVisual(); RefreshSelected(); } });
            panel.Children.Add(Separator());
            AddSlider(panel, "底图透明度", 0.15, 1.0, surface.BackgroundOpacity, delegate(double v) { surface.BackgroundOpacity = v; surface.InvalidateVisual(); });
            AddSlider(panel, "区域透明度", 0.15, 1.0, surface.OverlayOpacity, delegate(double v) { surface.OverlayOpacity = v; surface.InvalidateVisual(); });
            CheckBox outlineView = new CheckBox { Content = "实体轮廓校准视图（无 Glow / 1px）", Foreground = Palette.WarningBrush, Margin = new Thickness(0, 2, 0, 0), ToolTip = "透明填充、1 个屏幕像素描边；先用此视图贴合实体边缘，再检查正式光效" };
            outlineView.Checked += delegate { surface.OutlineCalibrationView = true; surface.InvalidateVisual(); };
            outlineView.Unchecked += delegate { surface.OutlineCalibrationView = false; surface.InvalidateVisual(); };
            panel.Children.Add(outlineView);
            CheckBox lockImage = new CheckBox { Content = "锁定底图", IsChecked = true, Foreground = Palette.TextBrush, Margin = new Thickness(0, 8, 0, 0) };
            lockImage.Checked += delegate { surface.ImageLocked = true; };
            lockImage.Unchecked += delegate { surface.ImageLocked = false; };
            panel.Children.Add(lockImage);
            panel.Children.Add(Separator());
            AddStyleSliders(panel);
            panel.Children.Add(Separator());
            AddButton(panel, "导出完整 Geometry JSON", ExportDocument);
            AddButton(panel, "导入完整 Geometry JSON", ImportDocument);
            AddButton(panel, "重新加载默认与用户覆盖", delegate { Reload(); });
            AddButton(panel, "保存用户校准覆盖", delegate { Save(); });
            Button close = new Button { Content = "关闭", Height = 34, Margin = new Thickness(0, 7, 0, 0), Background = Palette.Surface2Brush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderBrush };
            close.Click += delegate { Close(); };
            panel.Children.Add(close);

            Content = root;
            // The editor is transactional: a successful save/reload advances the committed snapshot;
            // closing after an unsaved drag restores that last committed state in the live monitor.
            Closed += delegate { if (!ReferenceEquals(committedSnapshot, null)) manager.RestoreSnapshot(committedSnapshot); };
            Loaded += delegate { surface.Focus(); };
        }

        private void PopulateRegionList()
        {
            regionsList.Items.Clear();
            if (manager.Document.Regions != null)
            {
                for (int i = 0; i < manager.Document.Regions.Count; i++) regionsList.Items.Add(manager.Document.Regions[i].Id);
            }
            if (manager.Document.MotionRanges != null)
            {
                for (int i = 0; i < manager.Document.MotionRanges.Count; i++) regionsList.Items.Add(manager.Document.MotionRanges[i].Id);
            }
        }

        private void OnSurfaceRegionSelected(string id)
        {
            regionsList.SelectedItem = id;
            RefreshSelected();
        }

        private void RefreshSelected()
        {
            string id = surface.SelectedId;
            if (string.IsNullOrEmpty(id)) { selectedText.Text = "未选择区域"; return; }
            List<DualSenseCalibrationHandle> handles = manager.GetHandles(id);
            selectedText.Text = id + " · " + handles.Count.ToString(CultureInfo.InvariantCulture) + " 个可编辑锚点";
        }

        private void PushUndo()
        {
            if (snapshotPending) return;
            undo.Push(manager.CreateSnapshot());
            redo.Clear();
            snapshotPending = true;
            Dispatcher.BeginInvoke(new Action(delegate { snapshotPending = false; }), DispatcherPriority.Background);
        }

        private void Undo()
        {
            if (undo.Count == 0) return;
            redo.Push(manager.CreateSnapshot());
            manager.RestoreSnapshot(undo.Pop());
            PopulateRegionList();
            surface.InvalidateVisual();
            RefreshSelected();
            SetStatus("已撤销上一步 Geometry 编辑。");
        }

        private void Redo()
        {
            if (redo.Count == 0) return;
            undo.Push(manager.CreateSnapshot());
            manager.RestoreSnapshot(redo.Pop());
            PopulateRegionList();
            surface.InvalidateVisual();
            RefreshSelected();
            SetStatus("已重做 Geometry 编辑。");
        }

        private void Save()
        {
            string message;
            if (manager.SaveUserOverride(out message)) committedSnapshot = manager.CreateSnapshot();
            SetStatus(message);
        }

        private void Reload()
        {
            manager.Reload();
            committedSnapshot = manager.CreateSnapshot();
            undo.Clear();
            redo.Clear();
            PopulateRegionList();
            surface.Select(null);
            surface.InvalidateVisual();
            SetStatus(manager.LastLoadMessage);
        }

        private void ExportDocument(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog { Filter = "JSON 文件|*.json", FileName = "dualSenseRegions-export.json" };
            if (dialog.ShowDialog(this) != true) return;
            string message;
            manager.ExportDocument(dialog.FileName, out message);
            SetStatus(message);
        }

        private void ImportDocument(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "JSON 文件|*.json" };
            if (dialog.ShowDialog(this) != true) return;
            PushUndo();
            string message;
            manager.ImportDocument(dialog.FileName, out message);
            PopulateRegionList();
            surface.InvalidateVisual();
            RefreshSelected();
            SetStatus(message);
        }

        private void AddStyleSliders(StackPanel panel)
        {
            DualSenseVisualStyleDefinition active = manager.FindStyle("active");
            if (active == null) return;
            AddSlider(panel, "描边宽度", 0.5, 3.0, active.StrokePixels, delegate(double v) { PushUndo(); active.StrokePixels = v; manager.MarkStylesModified(); surface.InvalidateVisual(); });
            AddSlider(panel, "发光强度", 0.0, 1.0, active.GlowOpacity, delegate(double v) { PushUndo(); active.GlowOpacity = v; manager.MarkStylesModified(); surface.InvalidateVisual(); });
        }

        private void AddSlider(StackPanel panel, string label, double min, double max, double value, Action<double> changed)
        {
            panel.Children.Add(Text(label, 11, Palette.MutedBrush, false));
            Slider slider = new Slider { Minimum = min, Maximum = max, Value = value, Margin = new Thickness(0, 2, 0, 6) };
            slider.ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e) { changed(e.NewValue); };
            panel.Children.Add(slider);
        }

        private void AddButton(StackPanel panel, string content, RoutedEventHandler click)
        {
            Button button = new Button { Content = content, Height = 30, Margin = new Thickness(0, 4, 0, 0), Background = Palette.Surface2Brush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderBrush };
            button.Click += click;
            panel.Children.Add(button);
        }

        private static TextBlock Text(string value, double size, Brush brush, bool bold)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = brush, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5) };
        }

        private static Border Separator()
        {
            return new Border { Height = 1, Background = Palette.BorderBrush, Margin = new Thickness(0, 10, 0, 8) };
        }

        private void SetStatus(string value)
        {
            StatusMessage = value;
            statusText.Text = value;
        }

    }

    public sealed class ControllerVisual : FrameworkElement
    {
        private readonly ImageSource image;
        private readonly XboxRegionManager regions;
        private readonly BitmapSource leftStickCap;
        private readonly BitmapSource rightStickCap;
        private readonly Dictionary<int, double> buttonLevels = new Dictionary<int, double>();
        private readonly int[] animatedMasks = { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080, 0x0100, 0x0200, 0x0400, 0x1000, 0x2000, 0x4000, 0x8000 };
        private InputSnapshot state = new InputSnapshot();
        private double smoothLX;
        private double smoothLY;
        private double smoothRX;
        private double smoothRY;
        private double smoothLT;
        private double smoothRT;
        private bool reducedMotion;
        private readonly Typeface regular = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private readonly Typeface semi = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        public bool ReducedMotion
        {
            get { return reducedMotion; }
            set
            {
                if (reducedMotion == value) return;
                reducedMotion = value;
                InvalidateVisual();
            }
        }

        public XboxRegionManager Regions { get { return regions; } }
        public ImageSource ControllerPhoto { get { return image; } }

        public ControllerVisual(ImageSource source)
        {
            image = source;
            regions = XboxRegionManager.Load(false);
            BitmapSource bitmap = source as BitmapSource;
            if (bitmap != null && bitmap.PixelWidth >= 1100 && bitmap.PixelHeight >= 700)
            {
                // The old source crops included the recessed black socket, which made the moving layer look like a dark halo.
                // This is an alpha-isolated cap with only the top dish and knurled grip, shared by both sticks.
                BitmapSource cleanCap = LoadBitmapResource("ControllerLab.Assets.stick-cap.png");
                leftStickCap = cleanCap ?? CreateCrop(bitmap, 404, 236, 144, 144);
                rightStickCap = cleanCap ?? CreateCrop(bitmap, 875, 417, 150, 150);
            }
            ClipToBounds = false;
            IsHitTestVisible = false;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        public void UpdateState(InputSnapshot value)
        {
            bool inputChanged = state.Connected != value.Connected ||
                state.Buttons != value.Buttons ||
                state.LeftTrigger != value.LeftTrigger || state.RightTrigger != value.RightTrigger ||
                state.LeftX != value.LeftX || state.LeftY != value.LeftY ||
                state.RightX != value.RightX || state.RightY != value.RightY;
            state = value;
            bool animationChanged = false;
            double stickSpeed = reducedMotion ? 1.0 : 0.28;
            double triggerSpeed = reducedMotion ? 1.0 : 0.32;
            double before = smoothLX;
            smoothLX += (value.LeftNormalizedX - smoothLX) * stickSpeed;
            animationChanged |= Math.Abs(smoothLX - before) > 0.00005;
            before = smoothLY;
            smoothLY += (value.LeftNormalizedY - smoothLY) * stickSpeed;
            animationChanged |= Math.Abs(smoothLY - before) > 0.00005;
            before = smoothRX;
            smoothRX += (value.RightNormalizedX - smoothRX) * stickSpeed;
            animationChanged |= Math.Abs(smoothRX - before) > 0.00005;
            before = smoothRY;
            smoothRY += (value.RightNormalizedY - smoothRY) * stickSpeed;
            animationChanged |= Math.Abs(smoothRY - before) > 0.00005;
            before = smoothLT;
            smoothLT += (value.LeftTrigger / 255.0 - smoothLT) * triggerSpeed;
            animationChanged |= Math.Abs(smoothLT - before) > 0.00005;
            before = smoothRT;
            smoothRT += (value.RightTrigger / 255.0 - smoothRT) * triggerSpeed;
            animationChanged |= Math.Abs(smoothRT - before) > 0.00005;
            for (int i = 0; i < animatedMasks.Length; i++)
            {
                int mask = animatedMasks[i];
                double current;
                if (!buttonLevels.TryGetValue(mask, out current)) current = 0;
                double target = (value.Buttons & mask) != 0 ? 1.0 : 0.0;
                double speed = target > current ? 0.48 : 0.22;
                double next = reducedMotion ? target : current + (target - current) * speed;
                if (Math.Abs(next - current) > 0.0005) animationChanged = true;
                buttonLevels[mask] = next;
            }
            if (inputChanged || animationChanged) InvalidateVisual();
        }

        private static BitmapSource CreateCrop(BitmapSource source, int x, int y, int width, int height)
        {
            CroppedBitmap crop = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            crop.Freeze();
            return crop;
        }

        private static BitmapSource LoadBitmapResource(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                stream.Dispose();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 10 || h < 10) return;
            // The photo and every overlay use this single 1536x1024 logical-stage matrix.
            // No region has a local Canvas position, margin, scale, or render transform.
            Matrix stage = XboxRegionManager.CreateStageMatrix(w, h);
            dc.PushTransform(new MatrixTransform(stage));
            RadialGradientBrush shadow = new RadialGradientBrush(Color.FromArgb(78, 28, 51, 65), Color.FromArgb(0, 12, 20, 27));
            dc.DrawEllipse(shadow, null, new Point(XboxRegionManager.LogicalWidth / 2.0, 750), 690, 270);
            regions.DrawPhoto(dc, image);
            regions.DrawStickSockets(dc);
            // Layer 2: fixed socket rings stay behind the moving caps. They
            // describe the recess and must never occlude the real thumb top.
            regions.DrawStickFeedback(dc, state, GetVisualLevel, reducedMotion);
            // Layer 3: the only movable pixels are the alpha-isolated thumb caps.
            // They intentionally render over the fixed rings and controller shell.
            DrawMovingStickOnSharedStage(dc, "l3", leftStickCap, smoothLX, smoothLY, GetVisualLevel(0x0040));
            DrawMovingStickOnSharedStage(dc, "r3", rightStickCap, smoothRX, smoothRY, GetVisualLevel(0x0080));
            // Layer 4: all press/trigger feedback is above the photo but clipped
            // to the corresponding shared-stage geometry.
            // Top trigger masks map directly from the latest input report. The
            // smoothed values remain available for non-critical visual motion,
            // but must not delay LT/RT pressure feedback on the controller.
            regions.DrawActiveFeedback(dc, state, GetVisualLevel, state.LeftTrigger / 255.0, state.RightTrigger / 255.0, reducedMotion);
            dc.Pop();
        }

        private void DrawMovingStickOnSharedStage(DrawingContext dc, string id, BitmapSource cap, double inputX, double inputY, double pressed)
        {
            Point center = regions.GetStickCenter(id);
            Size size = regions.GetStickSize(id);
            Vector travel = regions.GetStickTravel(id);
            if (reducedMotion) travel *= 0.72;
            Point moved = new Point(center.X + inputX * travel.X, center.Y - inputY * travel.Y + pressed * 2.0);
            if (cap == null)
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(29, 31, 34)), null, moved, size.Width * 0.40, size.Height * 0.40);
                return;
            }
            // The cap is a clean alpha-isolated bitmap. It is intentionally not clipped
            // by the fixed L3/R3 hit geometry: its whole silhouette stays above the shell.
            dc.DrawImage(cap, new Rect(moved.X - size.Width / 2.0, moved.Y - size.Height / 2.0, size.Width, size.Height));
            if (pressed > 0.01) dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(150, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 1.5), regions.GetGeometry(id));
        }

        private double GetLevel(int mask)
        {
            double level;
            return buttonLevels.TryGetValue(mask, out level) ? level : 0;
        }

        private double GetVisualLevel(int mask)
        {
            return (state.Buttons & mask) != 0 ? 1.0 : GetLevel(mask);
        }

        private void DrawStickSocket(DrawingContext dc, Rect rect, BitmapSource socket, double nx, double ny, double cavityRadiusSource, double cavityOffsetXSource, double cavityOffsetYSource)
        {
            Point center = new Point(rect.X + nx * rect.Width, rect.Y + ny * rect.Height);
            double sourceScale = rect.Width / 1586.0;
            double cavityRadius = cavityRadiusSource * sourceScale;
            Point cavityCenter = new Point(center.X + cavityOffsetXSource * sourceScale, center.Y + cavityOffsetYSource * sourceScale);
            // Cover the photo's fixed green/blue ring with one neutral recessed socket. The moving cap is rendered after this layer.
            double socketRadius = cavityRadius * 1.30;
            RadialGradientBrush cavity = new RadialGradientBrush(Color.FromRgb(30, 38, 45), Color.FromRgb(5, 8, 10));
            cavity.GradientOrigin = new Point(0.43, 0.38);
            dc.DrawEllipse(cavity, new Pen(new SolidColorBrush(Color.FromArgb(118, Palette.Border.R, Palette.Border.G, Palette.Border.B)), Math.Max(1.0, sourceScale * 1.6)), cavityCenter, socketRadius, socketRadius);
            Pen accentRing = new Pen(new SolidColorBrush(Color.FromArgb(152, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), Math.Max(1.15, sourceScale * 1.9));
            dc.DrawEllipse(null, accentRing, cavityCenter, socketRadius * 0.90, socketRadius * 0.90);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(72, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), Math.Max(0.9, sourceScale * 1.1)), cavityCenter, socketRadius * 1.05, socketRadius * 1.05);
        }

        private void DrawMovingStick(DrawingContext dc, Rect rect, BitmapSource cap, double nx, double ny, double inputX, double inputY, double pressed, Color color, double capRadiusSource, double capScale, double shellRadiusSource, double capDiameterSource)
        {
            Point center = new Point(rect.X + nx * rect.Width, rect.Y + ny * rect.Height);
            double magnitude = Math.Min(1.0, Math.Sqrt(inputX * inputX + inputY * inputY));
            if (cap == null)
            {
                double fallbackRadius = rect.Width * 0.032;
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(29, 31, 34)), null, center, fallbackRadius, fallbackRadius);
                return;
            }
            bool moving = magnitude >= 0.001 || pressed >= 0.001;

            double travel = rect.Width * (reducedMotion ? 0.010 : 0.014);
            Point moved = new Point(center.X + inputX * travel, center.Y - inputY * travel + pressed * Math.Max(0.8, rect.Width * 0.0014));
            double sourceScale = rect.Width / 1586.0;
            double capRadius = capRadiusSource * sourceScale;
            double shellRadius = shellRadiusSource * sourceScale;
            double protrudeRadius = capRadius * capScale;
            double protrudeSize = capDiameterSource * sourceScale * capScale;
            double protrudeImageRadius = protrudeSize / 2.0;

            if (!reducedMotion && magnitude >= 0.001)
            {
                Pen vector = new Pen(new SolidColorBrush(Color.FromArgb((byte)(58 + magnitude * 88), color.R, color.G, color.B)), 1.2);
                dc.DrawLine(vector, center, moved);
            }

            if (cap != null)
            {
                // The cap itself has a feathered alpha silhouette, so it remains a separate protruding object without a circular black crop edge.
                Rect capRect = new Rect(moved.X - protrudeImageRadius, moved.Y - protrudeImageRadius, protrudeSize, protrudeSize);
                dc.PushClip(new EllipseGeometry(center, shellRadius, shellRadius));
                dc.DrawImage(cap, capRect);
                dc.Pop();
            }
            else
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(29, 31, 34)), null, moved, protrudeRadius, protrudeRadius);
            }

        }

        private void DrawButtonFeedback(DrawingContext dc, Rect rect, double nx, double ny, double level, Color color, double sizeScale)
        {
            if (level < 0.01) return;
            Point p = new Point(rect.X + nx * rect.Width, rect.Y + ny * rect.Height);
            double radius = Math.Max(9, rect.Width * 0.0235 * sizeScale) * (1.0 - level * 0.045);
            byte alpha = (byte)(50 + level * 145);
            if (!reducedMotion)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb(alpha, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                dc.DrawEllipse(glow, null, p, radius * 1.45, radius * 1.45);
            }
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(28 + level * 42), color.R, color.G, color.B)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(120 + level * 120), color.R, color.G, color.B)), 1.6), p, radius, radius);
        }

        private void DrawShoulderFeedback(DrawingContext dc, Rect rect, double nx, double ny, double level, Color color)
        {
            if (level < 0.01) return;
            Rect r = new Rect(rect.X + nx * rect.Width - rect.Width * 0.060, rect.Y + ny * rect.Height - 7 + level * 2, rect.Width * 0.12, 15);
            byte alpha = (byte)(35 + level * 115);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(100 + level * 150), color.R, color.G, color.B)), 1.4), r, 7, 7);
        }

        private void DrawTriggerFeedback(DrawingContext dc, Rect rect, double nx, double value, Color color)
        {
            if (value < 0.005) return;
            double width = rect.Width * 0.12;
            double height = 12 + value * 7;
            Rect r = new Rect(rect.X + nx * rect.Width - width / 2.0, rect.Y + rect.Height * 0.064 - height / 2.0 + value * 2.0, width, height);
            byte alpha = (byte)(30 + value * 130);
            Brush fill = reducedMotion
                ? (Brush)new SolidColorBrush(Color.FromArgb((byte)Math.Min(120, (int)alpha), color.R, color.G, color.B))
                : new RadialGradientBrush(Color.FromArgb(alpha, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
            dc.DrawRoundedRectangle(fill, new Pen(new SolidColorBrush(Color.FromArgb((byte)(70 + value * 170), color.R, color.G, color.B)), 1.4), r, 8, 8);
        }

        private void DrawCallouts(DrawingContext dc, Rect rect)
        {
            double leftX = 12;
            double rightX = ActualWidth - 105;
            Point lp = new Point(rect.X + rect.Width * (476.0 / 1586.0), rect.Y + rect.Height * (308.0 / 992.0));
            Point rp = new Point(rect.X + rect.Width * (950.0 / 1586.0), rect.Y + rect.Height * (492.0 / 992.0));
            Pen greenPen = new Pen(Palette.GreenBrush, 1.3);
            Pen bluePen = new Pen(Palette.BlueBrush, 1.3);
            double leftMagnitude = Magnitude(state.LeftNormalizedX, state.LeftNormalizedY);
            double rightMagnitude = Magnitude(state.RightNormalizedX, state.RightNormalizedY);
            string leftAngleText = leftMagnitude < 0.02 ? "—" : Angle(state.LeftNormalizedX, state.LeftNormalizedY).ToString("0", CultureInfo.InvariantCulture) + "°";
            string rightAngleText = rightMagnitude < 0.02 ? "—" : Angle(state.RightNormalizedX, state.RightNormalizedY).ToString("0", CultureInfo.InvariantCulture) + "°";

            dc.DrawLine(greenPen, new Point(leftX + 126, lp.Y + 60), new Point(rect.X - 8, lp.Y + 60));
            dc.DrawLine(greenPen, new Point(rect.X - 8, lp.Y + 60), lp);
            DrawText(dc, "左摇杆", leftX, lp.Y + 23, 13, Palette.GreenBrush, true);
            DrawText(dc, "X", leftX, lp.Y + 50, 12, Palette.MutedBrush, false);
            DrawText(dc, state.LeftX.ToString(CultureInfo.InvariantCulture), leftX + 38, lp.Y + 50, 12, Palette.GreenBrush, false);
            DrawText(dc, "Y", leftX, lp.Y + 72, 12, Palette.MutedBrush, false);
            DrawText(dc, state.LeftY.ToString(CultureInfo.InvariantCulture), leftX + 38, lp.Y + 72, 12, Palette.GreenBrush, false);
            DrawText(dc, "幅度", leftX, lp.Y + 94, 12, Palette.MutedBrush, false);
            DrawText(dc, leftMagnitude.ToString("0.00", CultureInfo.InvariantCulture), leftX + 38, lp.Y + 94, 12, Palette.GreenBrush, false);
            DrawText(dc, "角度", leftX, lp.Y + 116, 12, Palette.MutedBrush, false);
            DrawText(dc, leftAngleText, leftX + 38, lp.Y + 116, 12, Palette.GreenBrush, false);

            dc.DrawLine(bluePen, rp, new Point(rightX - 12, rp.Y - 28));
            DrawText(dc, "右摇杆", rightX, rp.Y - 66, 13, Palette.BlueBrush, true);
            DrawText(dc, "X", rightX, rp.Y - 39, 12, Palette.MutedBrush, false);
            DrawText(dc, state.RightX.ToString(CultureInfo.InvariantCulture), rightX + 38, rp.Y - 39, 12, Palette.BlueBrush, false);
            DrawText(dc, "Y", rightX, rp.Y - 17, 12, Palette.MutedBrush, false);
            DrawText(dc, state.RightY.ToString(CultureInfo.InvariantCulture), rightX + 38, rp.Y - 17, 12, Palette.BlueBrush, false);
            DrawText(dc, "幅度", rightX, rp.Y + 5, 12, Palette.MutedBrush, false);
            DrawText(dc, rightMagnitude.ToString("0.00", CultureInfo.InvariantCulture), rightX + 38, rp.Y + 5, 12, Palette.BlueBrush, false);
            DrawText(dc, "角度", rightX, rp.Y + 27, 12, Palette.MutedBrush, false);
            DrawText(dc, rightAngleText, rightX + 38, rp.Y + 27, 12, Palette.BlueBrush, false);

            double ltX = rect.X + rect.Width * 0.20;
            double rtX = rect.X + rect.Width * 0.78;
            DrawText(dc, "LT", ltX - 40, rect.Y - 26, 13, Palette.TextBrush, false);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "{0:0}%", state.LeftTrigger / 2.55), ltX - 40, rect.Y - 5, 12, Palette.MutedBrush, false);
            dc.DrawLine(new Pen(Palette.MutedBrush, 1), new Point(ltX - 9, rect.Y - 2), new Point(ltX + 42, rect.Y - 2));
            dc.DrawLine(new Pen(Palette.MutedBrush, 1), new Point(ltX + 42, rect.Y - 2), new Point(ltX + 52, rect.Y + 22));
            DrawText(dc, "RT", rtX + 14, rect.Y - 26, 13, Palette.TextBrush, false);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "{0:0}%", state.RightTrigger / 2.55), rtX + 14, rect.Y - 5, 12, Palette.BlueBrush, false);
            dc.DrawLine(bluePen, new Point(rtX + 4, rect.Y - 2), new Point(rtX - 49, rect.Y - 2));
            dc.DrawLine(bluePen, new Point(rtX - 49, rect.Y - 2), new Point(rtX - 58, rect.Y + 23));
        }

        private void DrawText(DrawingContext dc, string text, double x, double y, double size, Brush brush, bool bold)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, bold ? semi : regular, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }

        private static double Magnitude(double x, double y)
        {
            return Math.Min(1.0, Math.Sqrt(x * x + y * y));
        }

        private static double Angle(double x, double y)
        {
            if (Math.Abs(x) < 0.0001 && Math.Abs(y) < 0.0001) return 0;
            double angle = Math.Atan2(y, x) * 180.0 / Math.PI;
            return angle < 0 ? angle + 360 : angle;
        }
    }

    public enum StickPlotTraceMode
    {
        Passive,
        Drift,
        Range
    }

    public sealed class StickPlot : FrameworkElement
    {
        private readonly Color accent;
        private double x;
        private double y;
        private double deadzone = 0.08;
        private double maximumReach;
        private bool reducedMotion;
        private bool recordTrace = true;
        private StickPlotTraceMode traceMode;
        private readonly List<TimedStickPoint> passiveTrail = new List<TimedStickPoint>();
        private readonly List<TimedStickPoint> driftTrail = new List<TimedStickPoint>();
        private readonly List<TimedStickPoint> rangeTrail = new List<TimedStickPoint>();
        private readonly Typeface typeface = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private const int MaximumTracePoints = 64;
        private const int MaximumDriftTracePoints = 384;
        private static readonly TimeSpan TraceLifetime = TimeSpan.FromSeconds(1.0);
        private static readonly TimeSpan DriftTraceLifetime = TimeSpan.FromSeconds(5.0);

        private sealed class TimedStickPoint
        {
            public Point Value;
            public DateTime Timestamp;
        }

        public double Deadzone
        {
            get { return deadzone; }
            set
            {
                double next = Math.Max(0, Math.Min(0.25, value));
                if (Math.Abs(next - deadzone) < 0.0001) return;
                deadzone = next;
                InvalidateVisual();
            }
        }

        public bool ReducedMotion
        {
            get { return reducedMotion; }
            set
            {
                if (reducedMotion == value) return;
                reducedMotion = value;
                if (reducedMotion) ClearAllTrails();
                InvalidateVisual();
            }
        }

        public bool RecordTrace
        {
            get { return recordTrace; }
            set { recordTrace = value; }
        }

        public StickPlotTraceMode TraceMode
        {
            get { return traceMode; }
        }

        public double MaximumReach
        {
            get { return maximumReach; }
            set
            {
                double next = Math.Max(0, Math.Min(1, value));
                if (Math.Abs(next - maximumReach) < 0.0001) return;
                maximumReach = next;
                InvalidateVisual();
            }
        }

        public StickPlot(Color color)
        {
            accent = color;
            MinWidth = 180;
            MinHeight = 150;
            IsHitTestVisible = false;
        }

        public void UpdateValue(double nx, double ny)
        {
            DateTime now = DateTime.UtcNow;
            bool removedExpired = PurgeExpired(now);
            if (Math.Abs(nx - x) < 0.00005 && Math.Abs(ny - y) < 0.00005)
            {
                if (removedExpired) InvalidateVisual();
                return;
            }
            x = nx;
            y = ny;
            if (!reducedMotion && recordTrace)
            {
                List<TimedStickPoint> trail = ActiveTrail();
                trail.Add(new TimedStickPoint { Value = new Point(x, y), Timestamp = now });
                int maximum = traceMode == StickPlotTraceMode.Drift ? MaximumDriftTracePoints : MaximumTracePoints;
                while (trail.Count > maximum) trail.RemoveAt(0);
            }
            InvalidateVisual();
        }

        public void BeginTrace(StickPlotTraceMode mode)
        {
            ClearAllTrails();
            traceMode = mode;
            recordTrace = true;
            maximumReach = 0;
            InvalidateVisual();
        }

        public void EndTrace()
        {
            recordTrace = false;
        }

        public void ClearHistory()
        {
            ClearAllTrails();
            traceMode = StickPlotTraceMode.Passive;
            recordTrace = false;
            maximumReach = 0;
            InvalidateVisual();
        }

        private List<TimedStickPoint> ActiveTrail()
        {
            if (traceMode == StickPlotTraceMode.Drift) return driftTrail;
            if (traceMode == StickPlotTraceMode.Range) return rangeTrail;
            return passiveTrail;
        }

        private bool PurgeExpired(DateTime now)
        {
            bool changed = false;
            changed |= PurgeTrail(passiveTrail, now, TraceLifetime);
            changed |= PurgeTrail(driftTrail, now, DriftTraceLifetime);
            changed |= PurgeTrail(rangeTrail, now, TraceLifetime);
            return changed;
        }

        private static bool PurgeTrail(List<TimedStickPoint> trail, DateTime now, TimeSpan lifetime)
        {
            bool changed = false;
            while (trail.Count > 0 && now - trail[0].Timestamp > lifetime)
            {
                trail.RemoveAt(0);
                changed = true;
            }
            return changed;
        }

        private void ClearAllTrails()
        {
            passiveTrail.Clear();
            driftTrail.Clear();
            rangeTrail.Clear();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double radius = Math.Max(20, Math.Min(ActualWidth, ActualHeight) / 2.0 - 18);
            Point c = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            Pen grid = new Pen(new SolidColorBrush(Color.FromArgb(64, Palette.Border.R, Palette.Border.G, Palette.Border.B)), 1);
            Pen axis = new Pen(new SolidColorBrush(Color.FromArgb(92, Palette.Muted.R, Palette.Muted.G, Palette.Muted.B)), 1);
            Pen outer = new Pen(new SolidColorBrush(Color.FromArgb(150, Palette.Muted.R, Palette.Muted.G, Palette.Muted.B)), 1.0);
            for (int i = 1; i <= 3; i++) dc.DrawEllipse(null, grid, c, radius * i / 3.0, radius * i / 3.0);
            dc.DrawEllipse(null, outer, c, radius, radius);
            dc.DrawLine(axis, new Point(c.X - radius, c.Y), new Point(c.X + radius, c.Y));
            dc.DrawLine(axis, new Point(c.X, c.Y - radius), new Point(c.X, c.Y + radius));
            if (maximumReach > 0.01)
            {
                Pen reach = new Pen(new SolidColorBrush(Color.FromArgb(120, Palette.Warning.R, Palette.Warning.G, Palette.Warning.B)), 1.0);
                dc.DrawEllipse(null, reach, c, radius * maximumReach, radius * maximumReach);
            }
            double deadzoneRadius = Math.Max(radius * deadzone, 12);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(32, accent.R, accent.G, accent.B)), new Pen(new SolidColorBrush(Color.FromArgb(78, accent.R, accent.G, accent.B)), 0.8), c, deadzoneRadius, deadzoneRadius);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(35, 49, 60)), outer, c, 5, 5);

            SolidColorBrush accentBrush = new SolidColorBrush(accent);
            List<TimedStickPoint> trail = ActiveTrail();
            for (int i = 0; !reducedMotion && i < trail.Count; i++)
            {
                Point n = trail[i].Value;
                Point p = new Point(c.X + n.X * radius, c.Y - n.Y * radius);
                byte a = (byte)(6 + i * 42 / Math.Max(1, trail.Count));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(a, accent.R, accent.G, accent.B)), null, p, 1.8, 1.8);
            }
            Point value = new Point(c.X + x * radius, c.Y - y * radius);
            Pen vector = new Pen(new SolidColorBrush(Color.FromArgb(105, accent.R, accent.G, accent.B)), 1.2);
            dc.DrawLine(vector, c, value);
            if (!reducedMotion)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb(82, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B));
                dc.DrawEllipse(glow, null, value, 12, 12);
            }
            dc.DrawEllipse(accentBrush, new Pen(new SolidColorBrush(Color.FromArgb(190, Palette.Text.R, Palette.Text.G, Palette.Text.B)), 0.7), value, 5, 5);

            DrawLabel(dc, "-1", c.X - radius - 19, c.Y - 7);
            DrawLabel(dc, "0", c.X - 3, c.Y + 8);
            DrawLabel(dc, "1", c.X + radius + 8, c.Y - 7);
            DrawLabel(dc, "1", c.X - 3, c.Y - radius - 17);
            DrawLabel(dc, "-1", c.X - 6, c.Y + radius + 5);
        }

        private void DrawLabel(DrawingContext dc, string text, double x, double y)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10, Palette.MutedBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }

    public sealed class DeadzoneSlider : FrameworkElement
    {
        private readonly Color accent;
        private double value;
        private bool dragging;
        public event EventHandler ValueChanged;

        public double Value
        {
            get { return value; }
            set
            {
                double next = Math.Max(0, Math.Min(0.25, value));
                if (Math.Abs(next - this.value) < 0.0001) return;
                this.value = next;
                InvalidateVisual();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public DeadzoneSlider(Color color, double initial)
        {
            accent = color;
            value = initial;
            Focusable = true;
            Cursor = Cursors.Hand;
            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { dragging = true; CaptureMouse(); SetFromMouse(e.GetPosition(this).X); Focus(); };
            MouseMove += delegate(object sender, MouseEventArgs e) { if (dragging) SetFromMouse(e.GetPosition(this).X); };
            MouseLeftButtonUp += delegate { dragging = false; ReleaseMouseCapture(); };
            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Down) { Value -= 0.01; e.Handled = true; }
            if (e.Key == Key.Right || e.Key == Key.Up) { Value += 0.01; e.Handled = true; }
            if (e.Key == Key.Home) { Value = 0; e.Handled = true; }
            if (e.Key == Key.End) { Value = 0.25; e.Handled = true; }
        }

        private void SetFromMouse(double mouseX)
        {
            double usable = Math.Max(1, ActualWidth - 14);
            Value = Math.Max(0, Math.Min(1, (mouseX - 7) / usable)) * 0.25;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DeadzoneSliderAutomationPeer(this);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double y = ActualHeight / 2.0;
            double start = 7;
            double end = Math.Max(start, ActualWidth - 7);
            Pen track = new Pen(new SolidColorBrush(Color.FromRgb(104, 119, 129)), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(track, new Point(start, y), new Point(end, y));
            double thumbX = start + (end - start) * value / 0.25;
            Pen filled = new Pen(new SolidColorBrush(accent), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(filled, new Point(start, y), new Point(thumbX, y));
            dc.DrawEllipse(new SolidColorBrush(accent), new Pen(new SolidColorBrush(Color.FromArgb(200, 226, 234, 240)), 0.7), new Point(thumbX, y), 6.5, 6.5);
            if (IsKeyboardFocused) dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(170, accent.R, accent.G, accent.B)), 1), new Rect(1, 1, Math.Max(0, ActualWidth - 2), Math.Max(0, ActualHeight - 2)));
        }
    }

    public sealed class DeadzoneSliderAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider
    {
        private readonly DeadzoneSlider slider;

        public DeadzoneSliderAutomationPeer(DeadzoneSlider owner) : base(owner)
        {
            slider = owner;
        }

        protected override string GetClassNameCore()
        {
            return "Slider";
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Slider;
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.RangeValue) return this;
            return base.GetPattern(patternInterface);
        }

        public bool IsReadOnly { get { return false; } }
        public double LargeChange { get { return 0.05; } }
        public double Maximum { get { return 0.25; } }
        public double Minimum { get { return 0.0; } }
        public double SmallChange { get { return 0.01; } }
        public double Value { get { return slider.Value; } }

        public void SetValue(double value)
        {
            if (!slider.IsEnabled) throw new ElementNotEnabledException();
            slider.Dispatcher.Invoke(new Action(delegate { slider.Value = value; }));
        }
    }

    public sealed class TriggerChart : FrameworkElement
    {
        public const double SampleIntervalSeconds = 0.030;
        private readonly Color accent;
        private readonly TriggerTelemetryBuffer telemetry;
        private double value;
        private bool reducedMotion;
        private bool paused;
        private string label = "扳机";
        private TriggerTelemetryStats stats = new TriggerTelemetryStats();
        private readonly Typeface typeface = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        public TextBlock PercentText { get; set; }
        public TextBlock PeakText { get; set; }
        public TextBlock DetailText { get; set; }
        public string Label
        {
            get { return label; }
            set { label = string.IsNullOrEmpty(value) ? "扳机" : value; }
        }
        public bool Paused
        {
            get { return paused; }
            set
            {
                paused = value;
                InvalidateVisual();
            }
        }
        public double PeakValue { get { return stats == null ? 0 : stats.Peak; } }

        public bool ReducedMotion
        {
            get { return reducedMotion; }
            set
            {
                if (reducedMotion == value) return;
                reducedMotion = value;
                InvalidateVisual();
            }
        }

        public double Value
        {
            get { return value; }
            set
            {
                double next = Math.Max(0, Math.Min(1, value));
                bool changed = Math.Abs(next - this.value) >= 0.0005;
                this.value = next;
                stats = telemetry.GetStats();
                string currentText = string.Format(CultureInfo.InvariantCulture, "{0:0}%", this.value * 100.0);
                if (PercentText != null && PercentText.Text != currentText) PercentText.Text = currentText;
                string peakText = string.Format(CultureInfo.InvariantCulture, "{0:0}%", stats.Peak * 100.0);
                if (PeakText != null && PeakText.Text != peakText) PeakText.Text = peakText;
                if (DetailText != null) DetailText.Text = string.Format(CultureInfo.InvariantCulture, "最低 {0:0}% · 平均 {1:0}% · 回弹 {2:0}%/s · 变化 {3} · 噪声 {4:0.0}% · 满行程 {5} · 回零 {6} · {7}", stats.Minimum * 100.0, stats.Average * 100.0, stats.ReleaseSpeedPerSecond * 100.0, stats.ChangeCount, stats.Noise * 100.0, stats.ReachesFullRange ? "✓" : "待测", stats.ReturnsToZero ? "✓" : "待测", stats.HealthText);
                AutomationProperties.SetName(this, string.Format(CultureInfo.InvariantCulture, "{0} 当前 {1:0}% ，近 5 秒峰值 {2:0}%", label, this.value * 100.0, stats.Peak * 100.0));
                if (changed || !paused) InvalidateVisual();
            }
        }

        public void ClearHistory()
        {
            stats = telemetry.GetStats();
            if (PeakText != null) PeakText.Text = "0%";
            InvalidateVisual();
        }

        public double[] GetHistorySnapshot()
        {
            return telemetry.GetSnapshot();
        }

        public TriggerChart(Color color, TriggerTelemetryBuffer source)
        {
            accent = color;
            telemetry = source ?? new TriggerTelemetryBuffer();
            MinHeight = 0;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Rect plot = new Rect(42, 8, Math.Max(90, ActualWidth - 58), Math.Max(72, ActualHeight - 34));
            Brush gridBrush = new SolidColorBrush(Color.FromArgb(46, 86, 109, 125));
            Pen grid = new Pen(gridBrush, 1);
            for (int i = 0; i <= 2; i++)
            {
                double gy = plot.Top + plot.Height * i / 2.0;
                dc.DrawLine(grid, new Point(plot.Left, gy), new Point(plot.Right, gy));
            }
            Pen axis = new Pen(new SolidColorBrush(Color.FromArgb(85, Palette.Border.R, Palette.Border.G, Palette.Border.B)), 1.0);
            dc.DrawLine(axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));

            double[] history = telemetry.GetSnapshot();
            if (history.Length > 0)
            {
                List<Point> points = new List<Point>(history.Length);
                for (int i = 0; i < history.Length; i++)
                {
                    double position = (170 - history.Length + i) / 169.0;
                    points.Add(new Point(plot.Left + plot.Width * position, plot.Bottom - plot.Height * history[i]));
                }

                if (!reducedMotion && points.Count > 1)
                {
                    StreamGeometry area = new StreamGeometry();
                    using (StreamGeometryContext context = area.Open())
                    {
                        context.BeginFigure(new Point(points[0].X, plot.Bottom), true, true);
                        context.LineTo(points[0], true, false);
                        for (int i = 1; i < points.Count; i++) context.LineTo(points[i], true, false);
                        context.LineTo(new Point(points[points.Count - 1].X, plot.Bottom), true, false);
                    }
                    LinearGradientBrush fill = new LinearGradientBrush(
                        Color.FromArgb(92, accent.R, accent.G, accent.B),
                        Color.FromArgb(5, accent.R, accent.G, accent.B),
                        new Point(0.5, 0), new Point(0.5, 1));
                    dc.DrawGeometry(fill, null, area);
                }

                StreamGeometry line = new StreamGeometry();
                using (StreamGeometryContext context = line.Open())
                {
                    context.BeginFigure(points[0], false, false);
                    for (int i = 1; i < points.Count - 1; i++)
                    {
                        Point mid = new Point((points[i].X + points[i + 1].X) * 0.5, (points[i].Y + points[i + 1].Y) * 0.5);
                        context.QuadraticBezierTo(points[i], mid, true, false);
                    }
                    if (points.Count > 1) context.LineTo(points[points.Count - 1], true, false);
                }
                Pen curve = new Pen(new SolidColorBrush(accent), reducedMotion ? 1.5 : 2.0)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                dc.DrawGeometry(null, curve, line);
            }

            Point marker = new Point(plot.Right, plot.Bottom - plot.Height * value);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(110, accent.R, accent.G, accent.B)), 1), new Point(plot.Right, plot.Top), new Point(plot.Right, plot.Bottom));
            if (!reducedMotion)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb(110, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B));
                dc.DrawEllipse(glow, null, marker, 13, 13);
            }
            dc.DrawEllipse(new SolidColorBrush(Palette.Surface), new Pen(new SolidColorBrush(accent), 1.7), marker, 5, 5);

            DrawText(dc, "100%", 2, plot.Top - 5, 10);
            DrawText(dc, "50%", 10, plot.Top + plot.Height / 2 - 6, 10);
            DrawText(dc, "0%", 20, plot.Bottom - 7, 10);
            DrawText(dc, "5 秒前", plot.Left, plot.Bottom + 5, 10);
            DrawText(dc, "现在", plot.Right - 22, plot.Bottom + 5, 10);
        }

        private void DrawText(DrawingContext dc, string text, double x, double y, double size)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, Palette.MutedBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
