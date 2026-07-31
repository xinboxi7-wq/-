using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerLab
{
    public sealed class RumbleTimelineView : FrameworkElement
    {
        private RumblePatternDefinition pattern;
        private double progress;

        public RumblePatternDefinition Pattern
        {
            get { return pattern; }
            set { pattern = value; InvalidateVisual(); }
        }

        public double Progress
        {
            get { return progress; }
            set { progress = Math.Max(0, Math.Min(1, value)); InvalidateVisual(); }
        }

        public RumbleTimelineView()
        {
            Height = 150;
            MinWidth = 320;
            SnapsToDevicePixels = true;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            Rect bounds = new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
            drawingContext.DrawRoundedRectangle(Palette.Surface2Brush, new Pen(Palette.BorderSubtleBrush, 1), bounds, 10, 10);
            Rect graph = new Rect(42, 18, Math.Max(1, bounds.Width - 58), Math.Max(1, bounds.Height - 42));
            Pen grid = new Pen(new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)), 1);
            for (int i = 0; i <= 4; i++)
            {
                double y = graph.Top + graph.Height * i / 4.0;
                drawingContext.DrawLine(grid, new Point(graph.Left, y), new Point(graph.Right, y));
            }
            for (int i = 0; i <= 5; i++)
            {
                double x = graph.Left + graph.Width * i / 5.0;
                drawingContext.DrawLine(grid, new Point(x, graph.Top), new Point(x, graph.Bottom));
            }
            DrawLabel(drawingContext, "100%", new Point(5, graph.Top - 7));
            DrawLabel(drawingContext, "0%", new Point(13, graph.Bottom - 7));
            double total = pattern == null ? 0 : pattern.TotalDuration;
            if (pattern != null && total > 0)
            {
                StreamGeometry leftGeometry = new StreamGeometry();
                StreamGeometry rightGeometry = new StreamGeometry();
                using (StreamGeometryContext leftContext = leftGeometry.Open())
                using (StreamGeometryContext rightContext = rightGeometry.Open())
                {
                    const int points = 160;
                    for (int i = 0; i <= points; i++)
                    {
                        double t = total * i / points;
                        double left;
                        double right;
                        string ignored;
                        RumblePatternMath.Evaluate(pattern, t, out left, out right, out ignored);
                        Point lp = new Point(graph.Left + graph.Width * i / points, graph.Bottom - graph.Height * left);
                        Point rp = new Point(graph.Left + graph.Width * i / points, graph.Bottom - graph.Height * right);
                        if (i == 0) { leftContext.BeginFigure(lp, false, false); rightContext.BeginFigure(rp, false, false); }
                        else { leftContext.LineTo(lp, true, false); rightContext.LineTo(rp, true, false); }
                    }
                }
                leftGeometry.Freeze();
                rightGeometry.Freeze();
                drawingContext.DrawGeometry(null, new Pen(Palette.BlueBrush, 2.2), leftGeometry);
                drawingContext.DrawGeometry(null, new Pen(Palette.WarningBrush, 2.2), rightGeometry);
                double playX = graph.Left + graph.Width * progress;
                drawingContext.DrawLine(new Pen(Palette.TextBrush, 1.2), new Point(playX, graph.Top), new Point(playX, graph.Bottom));
                DrawLabel(drawingContext, total.ToString("0.00", CultureInfo.InvariantCulture) + " s", new Point(graph.Right - 44, graph.Bottom + 5));
            }
            DrawLabel(drawingContext, "Left Motor", new Point(graph.Left, 1), Palette.BlueBrush);
            DrawLabel(drawingContext, "Right Motor", new Point(graph.Left + 92, 1), Palette.WarningBrush);
        }

        private static void DrawLabel(DrawingContext context, string value, Point location) { DrawLabel(context, value, location, Palette.MutedBrush); }

        private static void DrawLabel(DrawingContext context, string value, Point location, Brush brush)
        {
            FormattedText text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(LabVisualStyles.UiFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 10, brush, 1.0);
            context.DrawText(text, location);
        }
    }

    public sealed class RumbleStudioPage : Grid, IDisposable
    {
        private readonly ControllerRumbleController controller;
        private readonly RumbleSettingsStore store;
        private readonly RumbleCalibrationController calibration;
        private readonly List<RumblePatternDefinition> builtIns;
        private ControllerState currentDevice;
        private string loadedDeviceId = string.Empty;
        private RumbleDeviceProfile profile;
        private RumblePatternDefinition selectedPattern;
        private RumblePatternDefinition editingPattern;
        private bool outputBlocked;
        private string blockedReason = string.Empty;

        private TextBlock deviceText;
        private TextBlock capabilityText;
        private TextBlock advancedText;
        private TextBlock statusText;
        private TextBlock stepText;
        private TextBlock timeText;
        private Border leftGrip;
        private Border rightGrip;
        private RumbleTimelineView liveTimeline;
        private RumbleTimelineView editorTimeline;
        private Slider leftSlider;
        private Slider rightSlider;
        private Slider durationSlider;
        private Slider overallSlider;
        private Button manualPlayButton;
        private Button pauseButton;
        private Button emergencyButton;
        private readonly List<Button> presetButtons = new List<Button>();
        private ComboBox customPatternSelector;
        private TextBox customName;
        private StackPanel nodeHost;
        private Button previewButton;
        private Button saveCustomButton;
        private Button deleteCustomButton;
        private TextBlock editorStatus;
        private TextBlock calibrationInstruction;
        private TextBlock calibrationValues;
        private Button calibrationStartButton;
        private Button calibrationConfirmButton;
        private Button calibrationCancelButton;
        private CheckBox applyCalibrationCheck;
        private CheckBox safetyCheck;

        public RumbleStudioPage(ControllerRumbleController controller, RumbleSettingsStore store)
        {
            if (controller == null) throw new ArgumentNullException("controller");
            this.controller = controller;
            this.store = store ?? new RumbleSettingsStore();
            calibration = new RumbleCalibrationController(controller);
            builtIns = RumblePatternCatalog.CreateBuiltIns();
            selectedPattern = FindPattern("balanced") ?? builtIns[0];
            editingPattern = CreateDefaultCustomPattern();
            Background = Palette.WindowBrush;
            BuildPage();
            RefreshEditorNodes();
            RefreshCustomPatterns();
        }

        public void Update(ControllerState state, bool blocked, string reason)
        {
            currentDevice = state;
            outputBlocked = blocked;
            blockedReason = reason ?? string.Empty;
            string id = state == null || !state.IsConnected ? string.Empty : state.DeviceId ?? string.Empty;
            if (!string.Equals(loadedDeviceId, id, StringComparison.OrdinalIgnoreCase)) LoadDevice(state, id);

            RumbleStatusSnapshot snapshot = controller.GetSnapshot();
            RumbleCapabilities capabilities = snapshot.Capabilities ?? new RumbleCapabilities();
            deviceText.Text = state == null || !state.IsConnected ? "设备：未连接" : "设备：" + state.DeviceName + " · " + state.ConnectionTypeLabel;
            capabilityText.Text = BuildCapabilityText(capabilities, snapshot.SupportDetails);
            capabilityText.Foreground = capabilities.IsSupported ? Palette.TextBrush : Palette.WarningBrush;
            advancedText.Text = state != null && state.ControllerType == ControllerType.DualSense
                ? "高级触觉反馈尚未开放 · 自适应扳机效果尚未开放"
                : "高级触觉与扳机效果：当前设备路径不支持";

            statusText.Text = outputBlocked ? blockedReason : snapshot.Status;
            statusText.Foreground = outputBlocked ? Palette.WarningBrush : snapshot.IsRunning ? Palette.BlueBrush : snapshot.LastOutputSucceeded ? Palette.GreenBrush : Palette.MutedBrush;
            stepText.Text = "当前步骤：" + snapshot.CurrentStepLabel;
            timeText.Text = snapshot.ElapsedSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " / " + snapshot.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " 秒 · 剩余 " + snapshot.RemainingSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " 秒";
            leftGrip.Opacity = 0.16 + snapshot.LeftStrength * 0.84;
            rightGrip.Opacity = 0.16 + snapshot.RightStrength * 0.84;
            liveTimeline.Pattern = selectedPattern;
            liveTimeline.Progress = snapshot.Progress;

            bool usable = capabilities.IsSupported && state != null && state.IsConnected && state.HasRealInput && !outputBlocked;
            manualPlayButton.IsEnabled = usable && !snapshot.IsRunning;
            previewButton.IsEnabled = usable && !snapshot.IsRunning && editingPattern.Steps.Count > 0;
            calibrationStartButton.IsEnabled = usable && !snapshot.IsRunning && calibration.Phase == RumbleCalibrationPhase.Idle;
            pauseButton.IsEnabled = snapshot.IsRunning;
            pauseButton.Content = snapshot.IsPaused ? "继续播放" : "暂停";
            emergencyButton.IsEnabled = snapshot.IsRunning || snapshot.LeftStrength > 0 || snapshot.RightStrength > 0;
            for (int i = 0; i < presetButtons.Count; i++) presetButtons[i].IsEnabled = usable && !snapshot.IsRunning;
            leftSlider.IsEnabled = capabilities.SupportsLeftMotor;
            rightSlider.IsEnabled = capabilities.SupportsRightMotor;
            calibrationConfirmButton.IsEnabled = calibration.Phase != RumbleCalibrationPhase.Idle && calibration.Phase != RumbleCalibrationPhase.Completed && snapshot.IsRunning;
            calibrationCancelButton.IsEnabled = calibration.Phase != RumbleCalibrationPhase.Idle && calibration.Phase != RumbleCalibrationPhase.Completed;
            RefreshCalibrationText();
        }

        public void CancelForPageLeave()
        {
            calibration.Cancel();
            controller.Stop("已离开震动测试页面，所有输出已归零");
        }

        public void ResetForDeviceChange()
        {
            calibration.Cancel();
            controller.Stop("设备已切换，上一台设备的震动已归零");
            loadedDeviceId = string.Empty;
            profile = null;
        }

        public void Dispose() { CancelForPageLeave(); }

        private void BuildPage()
        {
            Margin = new Thickness(32, 20, 32, 0);
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(210) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel headingText = new StackPanel();
            headingText.Children.Add(LabVisualStyles.CreatePageTitle("专业震动实验室"));
            headingText.Children.Add(new TextBlock { Text = "数据化预设、25 Hz 时间线播放、设备校准与严格安全停止。", Foreground = Palette.MutedBrush, FontSize = 13, Margin = new Thickness(0, 5, 0, 0) });
            deviceText = new TextBlock { Text = "设备：未连接", Foreground = Palette.MutedBrush, FontFamily = new FontFamily("Consolas"), FontSize = 10.5, Margin = new Thickness(0, 5, 0, 0) };
            headingText.Children.Add(deviceText);
            heading.Children.Add(headingText);
            emergencyButton = MakeButton("紧急停止", false);
            emergencyButton.Background = Palette.RedBrush;
            emergencyButton.BorderBrush = Palette.RedBrush;
            emergencyButton.Foreground = Brushes.White;
            emergencyButton.FontWeight = FontWeights.Bold;
            emergencyButton.Height = 38;
            emergencyButton.Click += delegate { calibration.Cancel(); controller.Stop("紧急停止：双通道已归零"); };
            Grid.SetColumn(emergencyButton, 1);
            heading.Children.Add(emergencyButton);
            Children.Add(heading);

            Grid live = new Grid { Margin = new Thickness(18, 14, 18, 14) };
            live.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            live.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            live.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel capability = new StackPanel();
            capability.Children.Add(new TextBlock { Text = "设备震动能力", Foreground = Palette.TextBrush, FontSize = 16, FontWeight = FontWeights.SemiBold });
            capabilityText = new TextBlock { Text = "等待设备", Foreground = Palette.MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, LineHeight = 18, Margin = new Thickness(0, 6, 0, 0) };
            advancedText = new TextBlock { Foreground = Palette.WarningBrush, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0) };
            capability.Children.Add(capabilityText);
            capability.Children.Add(advancedText);
            live.Children.Add(capability);

            Grid feedback = new Grid();
            feedback.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            feedback.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid grips = new Grid();
            grips.ColumnDefinitions.Add(new ColumnDefinition());
            grips.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grips.ColumnDefinitions.Add(new ColumnDefinition());
            leftGrip = MakeGrip("左握把", Palette.BlueBrush);
            rightGrip = MakeGrip("右握把", Palette.WarningBrush);
            grips.Children.Add(leftGrip);
            Grid.SetColumn(rightGrip, 2);
            grips.Children.Add(rightGrip);
            feedback.Children.Add(grips);
            StackPanel liveStatus = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
            statusText = new TextBlock { Text = "等待开始", Foreground = Palette.MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap };
            stepText = new TextBlock { Text = "当前步骤：等待", Foreground = Palette.TextBrush, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) };
            timeText = new TextBlock { Text = "0.00 / 0.00 秒", Foreground = Palette.MutedBrush, FontSize = 10, Margin = new Thickness(0, 2, 0, 0) };
            liveStatus.Children.Add(statusText);
            liveStatus.Children.Add(stepText);
            liveStatus.Children.Add(timeText);
            Grid.SetRow(liveStatus, 1);
            feedback.Children.Add(liveStatus);
            Grid liveContent = new Grid();
            liveContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            liveContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            liveContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            liveContent.Children.Add(feedback);
            liveTimeline = new RumbleTimelineView { Pattern = selectedPattern };
            Grid.SetColumn(liveTimeline, 2);
            liveContent.Children.Add(liveTimeline);
            Grid.SetColumn(liveContent, 2);
            live.Children.Add(liveContent);
            Border liveCard = LabVisualStyles.CreateSectionCard(live);
            Grid.SetRow(liveCard, 2);
            Children.Add(liveCard);

            TabControl tabs = new TabControl { Background = Palette.WindowBrush, BorderBrush = Palette.BorderSubtleBrush };
            tabs.Items.Add(MakeTab("预设与播放", BuildPlaybackTab()));
            tabs.Items.Add(MakeTab("时间线编辑", BuildTimelineTab()));
            tabs.Items.Add(MakeTab("设备校准", BuildCalibrationTab()));
            Grid.SetRow(tabs, 4);
            Children.Add(tabs);
        }

        private UIElement BuildPlaybackTab()
        {
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            StackPanel body = new StackPanel { Margin = new Thickness(16, 14, 16, 20) };
            scroll.Content = body;
            Border guide = LabVisualStyles.CreateInstructionCard("先确认设备能力，再从低强度开始", "播放期间观察左右握把通道与时间线；位置、强度或输出异常时立即使用右上角“紧急停止”。离开页面、设备切换和异常都会自动归零。", "1");
            guide.Margin = new Thickness(0, 0, 0, 14);
            body.Children.Add(guide);
            body.Children.Add(SectionTitle("基础强度与手动播放"));
            leftSlider = MakeSlider(0, 100, 40);
            rightSlider = MakeSlider(0, 100, 40);
            durationSlider = MakeSlider(0.1, 30, 5);
            durationSlider.TickFrequency = 0.1;
            overallSlider = MakeSlider(0, 100, 60);
            body.Children.Add(SliderRow("左侧强度", leftSlider, "%"));
            body.Children.Add(SliderRow("右侧强度", rightSlider, "%"));
            body.Children.Add(SliderRow("手动持续时间", durationSlider, " 秒"));
            body.Children.Add(SliderRow("预设总体缩放", overallSlider, "%"));
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            manualPlayButton = MakeButton("播放当前强度", true);
            manualPlayButton.Click += delegate { PlayManual(); };
            pauseButton = MakeButton("暂停", false);
            pauseButton.Click += delegate { TogglePause(); };
            actions.Children.Add(manualPlayButton);
            actions.Children.Add(pauseButton);
            body.Children.Add(actions);
            body.Children.Add(SectionTitle("数据化预设"));
            WrapPanel presets = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            for (int i = 0; i < builtIns.Count; i++)
            {
                RumblePatternDefinition pattern = builtIns[i];
                Button button = MakeButton(pattern.Name, false);
                button.Tag = pattern;
                button.Margin = new Thickness(0, 0, 7, 7);
                button.Click += delegate(object sender, RoutedEventArgs args) { PlayPreset(((Button)sender).Tag as RumblePatternDefinition); };
                presetButtons.Add(button);
                presets.Children.Add(button);
            }
            body.Children.Add(presets);
            body.Children.Add(new TextBlock { Text = "默认输出不超过 40%。用户主动提高强度后，高强度时间线会被自动缩短；单次播放绝不超过 30 秒。", Foreground = Palette.WarningBrush, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
            return scroll;
        }

        private UIElement BuildTimelineTab()
        {
            Grid body = new Grid { Margin = new Thickness(16, 14, 16, 18) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
            StackPanel left = new StackPanel();
            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            customName = new TextBox { Text = editingPattern.Name, Background = Palette.Surface2Brush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderSubtleBrush, Padding = new Thickness(8, 5, 8, 5), MinWidth = 220 };
            customName.TextChanged += delegate { editingPattern.Name = customName.Text; };
            heading.Children.Add(customName);
            Button add = MakeButton("新增节点", false);
            add.Click += delegate { AddNode(); };
            Grid.SetColumn(add, 1);
            heading.Children.Add(add);
            left.Children.Add(heading);
            editorTimeline = new RumbleTimelineView { Pattern = editingPattern, Margin = new Thickness(0, 10, 0, 10), Height = 180 };
            left.Children.Add(editorTimeline);
            WrapPanel actions = new WrapPanel();
            previewButton = MakeButton("预览时间线", true);
            previewButton.Click += delegate { PlayEditorPattern(); };
            saveCustomButton = MakeButton("保存自定义预设", false);
            saveCustomButton.Click += delegate { SaveCustomPattern(); };
            actions.Children.Add(previewButton);
            actions.Children.Add(saveCustomButton);
            left.Children.Add(actions);
            editorStatus = new TextBlock { Foreground = Palette.MutedBrush, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            left.Children.Add(editorStatus);
            body.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Children.Add(SectionTitle("时间节点 · 线性或阶跃"));
            ScrollViewer nodeScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 300 };
            nodeHost = new StackPanel();
            nodeScroll.Content = nodeHost;
            right.Children.Add(nodeScroll);
            right.Children.Add(SectionTitle("已保存自定义预设"));
            customPatternSelector = new ComboBox { Background = Palette.Surface2Brush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderSubtleBrush, Height = 32, Margin = new Thickness(0, 6, 0, 0) };
            customPatternSelector.SelectionChanged += delegate { LoadSelectedCustomPattern(); };
            right.Children.Add(customPatternSelector);
            deleteCustomButton = MakeButton("删除所选自定义预设", false);
            deleteCustomButton.Margin = new Thickness(0, 8, 0, 0);
            deleteCustomButton.Click += delegate { DeleteSelectedCustomPattern(); };
            right.Children.Add(deleteCustomButton);
            Grid.SetColumn(right, 2);
            body.Children.Add(right);
            return body;
        }

        private UIElement BuildCalibrationTab()
        {
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel body = new StackPanel { Margin = new Thickness(18, 16, 18, 20), MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
            scroll.Content = body;
            body.Children.Add(SectionTitle("最小可感知强度与舒适上限"));
            calibrationInstruction = new TextBlock { Text = "先连接真实设备。校准最大只会逐渐增加到 65%，不会自动执行 100% 强度。", Foreground = Palette.TextBrush, FontSize = 13, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Margin = new Thickness(0, 8, 0, 0) };
            body.Children.Add(calibrationInstruction);
            calibrationValues = new TextBlock { Foreground = Palette.MutedBrush, FontFamily = new FontFamily("Consolas"), FontSize = 11, Margin = new Thickness(0, 10, 0, 0) };
            body.Children.Add(calibrationValues);
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            calibrationStartButton = MakeButton("开始校准", true);
            calibrationStartButton.Click += delegate { StartCalibration(); };
            calibrationConfirmButton = MakeButton("确认当前强度", false);
            calibrationConfirmButton.Click += delegate { ConfirmCalibration(); };
            calibrationCancelButton = MakeButton("取消校准", false);
            calibrationCancelButton.Click += delegate { calibration.Cancel(); controller.SetDeviceProfile(profile); RefreshCalibrationText(); };
            actions.Children.Add(calibrationStartButton);
            actions.Children.Add(calibrationConfirmButton);
            actions.Children.Add(calibrationCancelButton);
            body.Children.Add(actions);
            applyCalibrationCheck = new CheckBox { Content = "播放时应用设备校准", Foreground = Palette.TextBrush, Margin = new Thickness(0, 14, 0, 0) };
            safetyCheck = new CheckBox { Content = "启用安全限制（推荐）", Foreground = Palette.TextBrush, Margin = new Thickness(0, 7, 0, 0), IsChecked = true };
            body.Children.Add(applyCalibrationCheck);
            body.Children.Add(safetyCheck);
            Button save = MakeButton("保存设备设置", false);
            save.Margin = new Thickness(0, 10, 0, 0);
            save.HorizontalAlignment = HorizontalAlignment.Left;
            save.Click += delegate { SaveProfile(); };
            body.Children.Add(save);
            body.Children.Add(new TextBlock { Text = "设备键仅保存 SHA-256 截断哈希，不在配置文件名中暴露原始 DeviceId。", Foreground = Palette.MutedBrush, FontSize = 10.5, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
            return scroll;
        }

        private void LoadDevice(ControllerState state, string id)
        {
            calibration.Cancel();
            controller.Stop("震动设备上下文已更新，输出已归零");
            loadedDeviceId = id;
            if (state == null || !state.IsConnected || !state.HasRealInput)
            {
                profile = null;
                controller.SetDeviceProfile(null);
                return;
            }
            profile = store.LoadDeviceProfile(state);
            controller.SetDeviceProfile(profile);
            leftSlider.Value = profile.DefaultLeftStrength * 100;
            rightSlider.Value = profile.DefaultRightStrength * 100;
            durationSlider.Value = profile.DefaultDurationSeconds;
            applyCalibrationCheck.IsChecked = profile.ApplyDeviceCalibration;
            safetyCheck.IsChecked = profile.SafetyLimitsEnabled;
            selectedPattern = FindAnyPattern(profile.LastPatternId) ?? selectedPattern;
            RefreshCalibrationText();
        }

        private void PlayManual()
        {
            if (!CanPlay()) return;
            ApplyProfileControls();
            string error;
            if (!controller.Start(ControllerRumblePattern.Manual, leftSlider.Value / 100.0, rightSlider.Value / 100.0, 1.0, durationSlider.Value, out error)) statusText.Text = error;
            else
            {
                selectedPattern = RumblePatternCatalog.FromLegacy(ControllerRumblePattern.Manual, leftSlider.Value / 100.0, rightSlider.Value / 100.0, durationSlider.Value);
                SaveUsage("manual");
            }
        }

        private void PlayPreset(RumblePatternDefinition pattern)
        {
            if (pattern == null || !CanPlay()) return;
            ApplyProfileControls();
            selectedPattern = pattern;
            string error;
            if (!controller.PlayPattern(pattern, overallSlider.Value / 100.0, out error)) statusText.Text = error;
            else SaveUsage(pattern.Id);
        }

        private void PlayEditorPattern()
        {
            if (!CanPlay()) return;
            ApplyProfileControls();
            editingPattern.Name = string.IsNullOrWhiteSpace(customName.Text) ? "自定义时间线" : customName.Text.Trim();
            selectedPattern = editingPattern;
            string error;
            if (!controller.PlayPattern(editingPattern, overallSlider.Value / 100.0, out error)) editorStatus.Text = error;
            else editorStatus.Text = "正在预览；紧急停止始终可用。";
        }

        private bool CanPlay()
        {
            if (outputBlocked) { statusText.Text = blockedReason; return false; }
            RumbleCapabilities capabilities = controller.GetCapabilities();
            if (!capabilities.IsSupported) { statusText.Text = capabilities.Details; return false; }
            return true;
        }

        private void TogglePause()
        {
            RumbleStatusSnapshot snapshot = controller.GetSnapshot();
            string reason;
            bool success = snapshot.IsPaused ? controller.Resume(out reason) : controller.Pause(out reason);
            if (!success) statusText.Text = reason;
        }

        private void AddNode()
        {
            double start = Math.Min(29.5, editingPattern.TotalDuration + 0.10);
            editingPattern.Steps.Add(new RumblePatternStep { StartTime = start, Duration = 0.25, LeftStrength = 0.20, RightStrength = 0.20, Interpolation = RumbleInterpolation.Linear, Label = "新节点" });
            RefreshEditorNodes();
        }

        private void RefreshEditorNodes()
        {
            if (nodeHost == null) return;
            nodeHost.Children.Clear();
            for (int i = 0; i < editingPattern.Steps.Count; i++) nodeHost.Children.Add(BuildNodeEditor(editingPattern.Steps[i], i));
            editorTimeline.Pattern = editingPattern;
            editorTimeline.InvalidateVisual();
        }

        private UIElement BuildNodeEditor(RumblePatternStep step, int index)
        {
            StackPanel card = new StackPanel { Margin = new Thickness(9, 8, 9, 8) };
            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBox label = new TextBox { Text = step.Label, Background = Palette.SurfaceRaisedBrush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderSubtleBrush, Padding = new Thickness(6, 3, 6, 3) };
            label.TextChanged += delegate { step.Label = label.Text; };
            heading.Children.Add(label);
            Button remove = MakeButton("删除", false);
            remove.Padding = new Thickness(8, 4, 8, 4);
            remove.Click += delegate { editingPattern.Steps.Remove(step); RefreshEditorNodes(); };
            Grid.SetColumn(remove, 1);
            heading.Children.Add(remove);
            card.Children.Add(heading);
            Slider start = MakeSlider(0, 30, step.StartTime);
            Slider duration = MakeSlider(0.01, 10, step.Duration);
            Slider left = MakeSlider(0, 100, step.LeftStrength * 100);
            Slider right = MakeSlider(0, 100, step.RightStrength * 100);
            start.ValueChanged += delegate { step.StartTime = start.Value; editorTimeline.InvalidateVisual(); };
            duration.ValueChanged += delegate { step.Duration = duration.Value; editorTimeline.InvalidateVisual(); };
            left.ValueChanged += delegate { step.LeftStrength = left.Value / 100.0; editorTimeline.InvalidateVisual(); };
            right.ValueChanged += delegate { step.RightStrength = right.Value / 100.0; editorTimeline.InvalidateVisual(); };
            card.Children.Add(SliderRow("开始时间", start, " 秒"));
            card.Children.Add(SliderRow("变化时长", duration, " 秒"));
            card.Children.Add(SliderRow("Left Motor", left, "%"));
            card.Children.Add(SliderRow("Right Motor", right, "%"));
            ComboBox interpolation = new ComboBox { ItemsSource = Enum.GetValues(typeof(RumbleInterpolation)), SelectedItem = step.Interpolation, Height = 28, Background = Palette.SurfaceRaisedBrush, Foreground = Palette.TextBrush, Margin = new Thickness(0, 4, 0, 0) };
            interpolation.SelectionChanged += delegate { if (interpolation.SelectedItem != null) step.Interpolation = (RumbleInterpolation)interpolation.SelectedItem; editorTimeline.InvalidateVisual(); };
            card.Children.Add(interpolation);
            Border border = LabVisualStyles.CreateMetricCard(card);
            border.Margin = new Thickness(0, 0, 0, 7);
            return border;
        }

        private void SaveCustomPattern()
        {
            try
            {
                editingPattern.Name = string.IsNullOrWhiteSpace(customName.Text) ? "自定义时间线" : customName.Text.Trim();
                string path = store.SaveCustomPattern(editingPattern);
                editorStatus.Text = "已保存：" + path;
                RefreshCustomPatterns();
            }
            catch (Exception ex) { editorStatus.Text = "保存失败：" + ex.Message; }
        }

        private void DeleteSelectedCustomPattern()
        {
            RumblePatternDefinition pattern = customPatternSelector.SelectedItem as RumblePatternDefinition;
            if (pattern == null) { editorStatus.Text = "请先选择自定义预设。"; return; }
            if (MessageBox.Show("确定删除自定义预设“" + pattern.Name + "”吗？", "删除震动预设", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                editorStatus.Text = store.DeleteCustomPattern(pattern.Id) ? "自定义预设已删除。" : "没有找到对应预设。";
                RefreshCustomPatterns();
            }
            catch (Exception ex) { editorStatus.Text = "删除失败：" + ex.Message; }
        }

        private void RefreshCustomPatterns()
        {
            if (customPatternSelector == null) return;
            customPatternSelector.Items.Clear();
            List<RumblePatternDefinition> patterns = store.LoadCustomPatterns();
            for (int i = 0; i < patterns.Count; i++) customPatternSelector.Items.Add(patterns[i]);
            deleteCustomButton.IsEnabled = patterns.Count > 0;
        }

        private void LoadSelectedCustomPattern()
        {
            RumblePatternDefinition selected = customPatternSelector.SelectedItem as RumblePatternDefinition;
            if (selected == null) return;
            editingPattern = selected.Copy();
            editingPattern.IsBuiltIn = false;
            customName.Text = editingPattern.Name;
            RefreshEditorNodes();
        }

        private void StartCalibration()
        {
            if (profile == null || !CanPlay()) return;
            string error;
            if (!calibration.Start(profile, out error)) calibrationInstruction.Text = error;
            else RefreshCalibrationText();
        }

        private void ConfirmCalibration()
        {
            if (profile == null) return;
            string error;
            if (!calibration.Confirm(profile, out error)) calibrationInstruction.Text = error;
            else
            {
                if (calibration.Phase == RumbleCalibrationPhase.Completed)
                {
                    profile.ApplyDeviceCalibration = true;
                    applyCalibrationCheck.IsChecked = true;
                    SaveProfile();
                }
                RefreshCalibrationText();
            }
        }

        private void RefreshCalibrationText()
        {
            if (calibrationInstruction == null) return;
            calibrationInstruction.Text = calibration.Instruction;
            calibrationValues.Text = profile == null
                ? "L min -- · R min -- · L comfort -- · R comfort --"
                : string.Format(CultureInfo.InvariantCulture, "L min {0:0.0}% · R min {1:0.0}% · L comfort {2:0.0}% · R comfort {3:0.0}%", profile.LeftMinimumPerceptible * 100, profile.RightMinimumPerceptible * 100, profile.LeftComfortMaximum * 100, profile.RightComfortMaximum * 100);
            calibrationConfirmButton.Content = calibration.Phase == RumbleCalibrationPhase.LeftMinimum || calibration.Phase == RumbleCalibrationPhase.RightMinimum ? "刚刚能感觉到" : "已经足够强";
        }

        private void SaveProfile()
        {
            if (profile == null) return;
            try
            {
                profile.DefaultLeftStrength = leftSlider.Value / 100.0;
                profile.DefaultRightStrength = rightSlider.Value / 100.0;
                profile.DefaultDurationSeconds = durationSlider.Value;
                profile.ApplyDeviceCalibration = applyCalibrationCheck.IsChecked == true;
                profile.SafetyLimitsEnabled = safetyCheck.IsChecked != false;
                profile.OutputConnectionMode = currentDevice == null ? "Unknown" : currentDevice.ConnectionTypeLabel;
                string path = store.SaveDeviceProfile(profile);
                controller.SetDeviceProfile(profile);
                calibrationInstruction.Text = "设备设置已保存：" + path;
            }
            catch (Exception ex) { calibrationInstruction.Text = "设备设置保存失败：" + ex.Message; }
        }

        private void SaveUsage(string patternId)
        {
            if (profile == null) return;
            profile.DefaultLeftStrength = Math.Min(0.40, leftSlider.Value / 100.0);
            profile.DefaultRightStrength = Math.Min(0.40, rightSlider.Value / 100.0);
            profile.DefaultDurationSeconds = Math.Min(5, durationSlider.Value);
            profile.LastPatternId = patternId;
            profile.ApplyDeviceCalibration = applyCalibrationCheck.IsChecked == true;
            profile.SafetyLimitsEnabled = safetyCheck.IsChecked != false;
            try { store.SaveDeviceProfile(profile); controller.SetDeviceProfile(profile); }
            catch { }
        }

        private void ApplyProfileControls()
        {
            if (profile == null) return;
            profile.ApplyDeviceCalibration = applyCalibrationCheck.IsChecked == true;
            profile.SafetyLimitsEnabled = safetyCheck.IsChecked != false;
            controller.SetDeviceProfile(profile);
        }

        private RumblePatternDefinition FindPattern(string id)
        {
            for (int i = 0; i < builtIns.Count; i++) if (string.Equals(builtIns[i].Id, id, StringComparison.OrdinalIgnoreCase)) return builtIns[i];
            return null;
        }

        private RumblePatternDefinition FindAnyPattern(string id)
        {
            RumblePatternDefinition pattern = FindPattern(id);
            if (pattern != null) return pattern;
            List<RumblePatternDefinition> custom = store.LoadCustomPatterns();
            for (int i = 0; i < custom.Count; i++) if (string.Equals(custom[i].Id, id, StringComparison.OrdinalIgnoreCase)) return custom[i];
            return null;
        }

        private static string BuildCapabilityText(RumbleCapabilities capabilities, string details)
        {
            if (capabilities == null || !capabilities.IsSupported) return string.IsNullOrEmpty(details) ? "当前设备不支持震动" : details;
            return string.Format(CultureInfo.InvariantCulture,
                "左电机 {0} · 右电机 {1} · 独立通道 {2}\nUSB {3} · 蓝牙 {4} · 当前连接 {5}\n安全上限 {6:0} 秒 · {7}",
                Mark(capabilities.SupportsLeftMotor), Mark(capabilities.SupportsRightMotor), Mark(capabilities.SupportsIndependentChannels), Mark(capabilities.SupportsUsb), Mark(capabilities.SupportsBluetooth), capabilities.ConnectionMode, capabilities.MaximumSafeDuration, capabilities.VerificationLabel);
        }

        private static string Mark(bool value) { return value ? "支持" : "不支持"; }

        private static RumblePatternDefinition CreateDefaultCustomPattern()
        {
            RumblePatternDefinition pattern = new RumblePatternDefinition { Id = "custom-" + Guid.NewGuid().ToString("N"), Name = "我的震动时间线", IsBuiltIn = false };
            pattern.Steps.Add(new RumblePatternStep { StartTime = 0, Duration = 0.25, LeftStrength = 0.20, RightStrength = 0.10, Interpolation = RumbleInterpolation.Linear, Label = "渐入" });
            pattern.Steps.Add(new RumblePatternStep { StartTime = 0.25, Duration = 0.35, LeftStrength = 0.35, RightStrength = 0.35, Interpolation = RumbleInterpolation.Linear, Label = "均衡" });
            pattern.Steps.Add(new RumblePatternStep { StartTime = 0.60, Duration = 0.25, LeftStrength = 0, RightStrength = 0, Interpolation = RumbleInterpolation.Linear, Label = "结束" });
            return pattern;
        }

        private static TabItem MakeTab(string title, UIElement content)
        {
            return new TabItem { Header = title, Content = content, Foreground = Palette.TextBrush, Background = Palette.SurfaceBrush, Padding = new Thickness(14, 7, 14, 7) };
        }

        private static TextBlock SectionTitle(string value)
        {
            return new TextBlock { Text = value, Foreground = Palette.TextBrush, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };
        }

        private static Button MakeButton(string text, bool primary)
        {
            Button button = new Button { Content = text, Style = primary ? LabVisualStyles.PrimaryButtonStyle : LabVisualStyles.SecondaryButtonStyle, MinWidth = 90, Height = 34, Margin = new Thickness(0, 0, 7, 0) };
            AutomationProperties.SetName(button, text);
            AutomationProperties.SetHelpText(button, "按 Enter 或空格键执行");
            return button;
        }

        private static Slider MakeSlider(double minimum, double maximum, double value)
        {
            return new Slider { Minimum = minimum, Maximum = maximum, Value = value, Height = 23, Foreground = Palette.BlueBrush, IsMoveToPointEnabled = true };
        }

        private static UIElement SliderRow(string label, Slider slider, string suffix)
        {
            StackPanel row = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 11 });
            TextBlock value = new TextBlock { Text = slider.Value.ToString("0.##", CultureInfo.InvariantCulture) + suffix, Foreground = Palette.TextBrush, FontSize = 11 };
            slider.ValueChanged += delegate { value.Text = slider.Value.ToString("0.##", CultureInfo.InvariantCulture) + suffix; };
            Grid.SetColumn(value, 1);
            heading.Children.Add(value);
            row.Children.Add(heading);
            row.Children.Add(slider);
            return row;
        }

        private static Border MakeGrip(string label, Brush color)
        {
            return new Border
            {
                Width = 78,
                Height = 94,
                CornerRadius = new CornerRadius(35, 35, 28, 28),
                Background = color,
                BorderBrush = color,
                BorderThickness = new Thickness(1),
                Opacity = 0.16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
        }
    }
}
