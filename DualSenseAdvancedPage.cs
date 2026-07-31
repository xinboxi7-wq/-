using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerLab
{
    public sealed class TouchpadTrailRenderer : FrameworkElement
    {
        private static readonly Brush Surface = new SolidColorBrush(Color.FromRgb(17, 29, 38));
        private static readonly Brush ContactOne = new SolidColorBrush(Color.FromRgb(51, 182, 255));
        private static readonly Brush ContactTwo = new SolidColorBrush(Color.FromRgb(170, 104, 255));
        private static readonly Brush Heat = new SolidColorBrush(Color.FromArgb(52, 51, 182, 255));
        private static readonly Pen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)), 1);
        private static readonly Pen BorderPen = new Pen(Palette.BorderBrush, 1.4);
        private TouchpadAnalysisSnapshot snapshot;

        public TouchpadTrailRenderer()
        {
            MinHeight = 310;
            SnapsToDevicePixels = true;
            ClipToBounds = true;
        }

        public void SetSnapshot(TouchpadAnalysisSnapshot value)
        {
            snapshot = value;
            InvalidateVisual();
        }

        public static Point MapNormalized(double x, double y, Rect bounds)
        {
            return new Point(bounds.Left + Clamp01(x) * bounds.Width, bounds.Top + Clamp01(y) * bounds.Height);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Rect pad = new Rect(12, 12, Math.Max(1, ActualWidth - 24), Math.Max(1, ActualHeight - 24));
            dc.DrawRoundedRectangle(Surface, BorderPen, pad, 26, 26);
            if (snapshot == null || !snapshot.IsAvailable)
            {
                DrawText(dc, "等待真实 DualSense 触摸坐标", new Point(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2), 14, Palette.Muted, TextAlignment.Center);
                return;
            }

            double cellWidth = pad.Width / TouchpadAnalysisSnapshot.GridColumns;
            double cellHeight = pad.Height / TouchpadAnalysisSnapshot.GridRows;
            for (int row = 0; row < TouchpadAnalysisSnapshot.GridRows; row++)
            {
                for (int column = 0; column < TouchpadAnalysisSnapshot.GridColumns; column++)
                {
                    int index = row * TouchpadAnalysisSnapshot.GridColumns + column;
                    Rect cell = new Rect(pad.Left + column * cellWidth, pad.Top + row * cellHeight, cellWidth, cellHeight);
                    if (snapshot.CoveredCells != null && index < snapshot.CoveredCells.Length && snapshot.CoveredCells[index]) dc.DrawRectangle(Heat, null, cell);
                    dc.DrawRectangle(null, GridPen, cell);
                }
            }

            TouchTrailPoint previous = null;
            if (snapshot.Trail != null)
            {
                for (int i = 0; i < snapshot.Trail.Length; i++)
                {
                    TouchTrailPoint point = snapshot.Trail[i];
                    Point mapped = MapNormalized(point.X, point.Y, pad);
                    if (previous != null && !point.StartsStroke && previous.ContactId == point.ContactId)
                    {
                        Brush color = ColorFor(point.ContactId);
                        dc.DrawLine(new Pen(color, 2.2), MapNormalized(previous.X, previous.Y, pad), mapped);
                    }
                    previous = point;
                }
            }
            if (snapshot.Points != null)
            {
                for (int i = 0; i < snapshot.Points.Length; i++)
                {
                    TouchPointState point = snapshot.Points[i];
                    Point mapped = MapNormalized(point.X, point.Y, pad);
                    Brush color = ColorFor(point.ContactId);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(55, ((SolidColorBrush)color).Color.R, ((SolidColorBrush)color).Color.G, ((SolidColorBrush)color).Color.B)), null, mapped, 18, 18);
                    dc.DrawEllipse(color, null, mapped, 7, 7);
                    DrawText(dc, point.ContactId.ToString(CultureInfo.InvariantCulture), new Point(mapped.X + 12, mapped.Y - 16), 11, ((SolidColorBrush)color).Color, TextAlignment.Left);
                }
            }
            if (snapshot.IsPressed) DrawText(dc, "触摸板按下", new Point(pad.Left + pad.Width / 2, pad.Bottom - 18), 12, Palette.Green, TextAlignment.Center);
        }

        private static Brush ColorFor(int contactId)
        {
            return (contactId & 1) == 0 ? ContactTwo : ContactOne;
        }

        private void DrawText(DrawingContext dc, string text, Point point, double size, Color color, TextAlignment alignment)
        {
            FormattedText formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(LabVisualStyles.UiFont, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), size, new SolidColorBrush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            formatted.TextAlignment = alignment;
            dc.DrawText(formatted, point);
        }

        private static double Clamp01(double value) { return Math.Max(0, Math.Min(1, value)); }
    }

    public sealed class DualSenseAdvancedPage : UserControl, IDisposable
    {
        private readonly DualSenseMotionManager motionManager;
        private readonly DualSenseAdvancedManager advancedManager;
        private readonly DualSenseCalibrationStore calibrationStore = new DualSenseCalibrationStore();
        private readonly IDualSenseAdvancedOutputService advancedOutput = new UnverifiedDualSenseAdvancedOutputService();
        private readonly Action startCalibration;
        private readonly Action recenter;
        private readonly Action resetMotion;
        private readonly TouchpadTrailRenderer touchRenderer = new TouchpadTrailRenderer();
        private readonly DualSenseMotionPoseView poseView = new DualSenseMotionPoseView();
        private readonly TextBlock deviceText = Secondary("等待 DualSense");
        private readonly TextBlock capabilityText = Secondary(string.Empty);
        private readonly TextBlock touchGuidance = Body(string.Empty, 16, Palette.TextBrush);
        private readonly TextBlock touchMetrics = Secondary(string.Empty);
        private readonly TextBlock touchPointDetails = Secondary(string.Empty);
        private readonly TextBlock motionValues = Body(string.Empty, 16, Palette.TextBrush);
        private readonly TextBlock motionDiagnostics = Secondary(string.Empty);
        private readonly TextBlock motionRaw = Secondary(string.Empty);
        private readonly TextBlock calibrationText = Secondary(string.Empty);
        private readonly TextBlock batteryText = Body(string.Empty, 17, Palette.TextBrush);
        private readonly TextBlock lightbarText = Secondary(string.Empty);
        private readonly TextBlock triggerText = Secondary(string.Empty);
        private readonly Button touchTestButton;
        private readonly Button touchRecordButton;
        private readonly Button touchPauseButton;
        private readonly Button touchClearButton;
        private readonly Button calibrateButton;
        private readonly Button saveCalibrationButton;
        private readonly Button recenterButton;
        private readonly Button resetButton;
        private readonly CheckBox rawPoseCheck;
        private readonly Slider sensitivitySlider;
        private readonly Slider smoothingSlider;
        private string currentDeviceId = string.Empty;
        private string profileAppliedDeviceId = string.Empty;
        private bool disposed;

        public DualSenseAdvancedPage(DualSenseMotionManager motion, DualSenseAdvancedManager advanced, Action calibrate, Action recenterAction, Action resetAction)
        {
            motionManager = motion;
            advancedManager = advanced;
            startCalibration = calibrate;
            recenter = recenterAction;
            resetMotion = resetAction;
            Background = Palette.WindowBrush;

            Grid root = new Grid { Margin = new Thickness(32, 22, 32, 0) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            StackPanel header = new StackPanel();
            header.Children.Add(LabVisualStyles.CreatePageTitle("DualSense 高级检测"));
            header.Children.Add(deviceText);
            capabilityText.Margin = new Thickness(0, 5, 0, 0);
            header.Children.Add(capabilityText);
            root.Children.Add(header);

            TabControl tabs = new TabControl { Background = Palette.WindowBrush, BorderThickness = new Thickness(0), Foreground = Palette.TextBrush };
            touchTestButton = Button("开始引导检测", true);
            touchRecordButton = Button("录制 5 秒", false);
            touchPauseButton = Button("暂停轨迹", false);
            touchClearButton = Button("清除轨迹", false);
            touchTestButton.Click += delegate { advancedManager.StartTouchTest(currentDeviceId); };
            touchRecordButton.Click += delegate { advancedManager.RecordTouch(currentDeviceId); };
            touchPauseButton.Click += delegate
            {
                DualSenseAdvancedSnapshot snapshot = advancedManager.Get(currentDeviceId, motionManager);
                bool pause = snapshot == null || snapshot.Touchpad == null || !snapshot.Touchpad.IsPaused;
                advancedManager.PauseTouch(currentDeviceId, pause);
            };
            touchClearButton.Click += delegate { advancedManager.ClearTouch(currentDeviceId); };
            tabs.Items.Add(Tab("触摸板", BuildTouchpadTab()));

            calibrateButton = Button("静止校准", true);
            saveCalibrationButton = Button("保存为设备校准", false);
            recenterButton = Button("重置显示零点", false);
            resetButton = Button("重置姿态", false);
            calibrateButton.Click += delegate { if (startCalibration != null) startCalibration(); };
            saveCalibrationButton.Click += delegate { SaveCalibration(); };
            recenterButton.Click += delegate { if (recenter != null) recenter(); };
            resetButton.Click += delegate { if (resetMotion != null) resetMotion(); advancedManager.ResetGyroscope(currentDeviceId); };
            rawPoseCheck = new CheckBox { Content = "显示纯陀螺积分姿态（无重力校正）", Foreground = Palette.TextBrush, Margin = new Thickness(0, 7, 0, 0) };
            rawPoseCheck.Checked += delegate { poseView.UseRawPose = true; };
            rawPoseCheck.Unchecked += delegate { poseView.UseRawPose = false; };
            sensitivitySlider = Slider(0.5, 2.0, 1.0);
            smoothingSlider = Slider(0, 1, 0.72);
            sensitivitySlider.ValueChanged += delegate { poseView.Sensitivity = sensitivitySlider.Value; };
            smoothingSlider.ValueChanged += delegate { poseView.Smoothing = smoothingSlider.Value; };
            tabs.Items.Add(Tab("陀螺仪", BuildMotionTab()));
            tabs.Items.Add(Tab("灯带与电量", BuildLightbarTab()));
            tabs.Items.Add(Tab("自适应扳机", BuildAdaptiveTriggerTab()));
            Grid.SetRow(tabs, 2);
            root.Children.Add(tabs);
            Content = root;
        }

        public void Update(ControllerState controller)
        {
            if (disposed) return;
            bool native = controller != null && controller.IsConnected && controller.ControllerType == ControllerType.DualSense && controller.InputSource == ControllerInputSource.DualSenseHid;
            if (!native) profileAppliedDeviceId = string.Empty;
            currentDeviceId = native ? controller.DeviceId : string.Empty;
            deviceText.Text = native
                ? controller.DeviceName + " · " + controller.ConnectionTypeLabel + " · 真实 HID"
                : controller != null && controller.ControllerType == ControllerType.Xbox
                    ? "当前为 Xbox 设备；DualSense 专属功能未启用，Xbox 输入链路不受影响。"
                    : "等待真实 DualSense 原生 HID 输入；动态演示不会生成检测结论。";
            DualSenseAdvancedCapabilities capabilities = DualSenseAdvancedCapabilities.From(controller);
            capabilityText.Text = BuildCapabilityLine(capabilities);
            SetTouchEnabled(native && capabilities.TouchPoint1Available);
            MotionViewState motion = native ? motionManager.Get(currentDeviceId) : new MotionViewState { AvailabilityMessage = "当前设备不提供 DualSense 运动传感器数据。", CalibrationState = MotionCalibrationState.Unsupported };
            ApplySavedProfileOnce(native, motion);
            DualSenseAdvancedSnapshot advanced = native ? advancedManager.Get(currentDeviceId, motionManager) : new DualSenseAdvancedSnapshot();
            UpdateTouch(advanced.Touchpad);
            UpdateMotion(motion, advanced.Gyroscope, native);
            UpdateBatteryAndOutputs(controller, capabilities, native);
        }

        public void CancelForPageLeave()
        {
            if (!string.IsNullOrEmpty(currentDeviceId))
            {
                advancedManager.CancelTransient(currentDeviceId);
                motionManager.CancelCalibration(currentDeviceId, "已离开 DualSense 高级页，静止校准已取消。");
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancelForPageLeave();
            touchRenderer.SetSnapshot(null);
            poseView.SetState(null);
            advancedOutput.RestoreSafeDefaults();
        }

        private UIElement BuildTouchpadTab()
        {
            Grid grid = new Grid { Margin = new Thickness(0, 12, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border visual = LabVisualStyles.CreateSectionCard(touchRenderer);
            visual.Padding = new Thickness(10);
            grid.Children.Add(visual);

            StackPanel side = new StackPanel();
            touchGuidance.TextWrapping = TextWrapping.Wrap;
            side.Children.Add(touchGuidance);
            touchMetrics.Margin = new Thickness(0, 9, 0, 0);
            touchMetrics.TextWrapping = TextWrapping.Wrap;
            side.Children.Add(touchMetrics);
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            actions.Children.Add(touchTestButton);
            actions.Children.Add(touchRecordButton);
            actions.Children.Add(touchPauseButton);
            actions.Children.Add(touchClearButton);
            side.Children.Add(actions);
            Expander details = new Expander { Header = "查看触点详细数据", Foreground = Palette.TextBrush, Margin = new Thickness(0, 12, 0, 0), Content = touchPointDetails };
            side.Children.Add(details);
            Border card = LabVisualStyles.CreateSectionCard(side);
            card.Padding = new Thickness(18);
            Grid.SetColumn(card, 2);
            grid.Children.Add(card);
            return Scroll(grid);
        }

        private UIElement BuildMotionTab()
        {
            Grid grid = new Grid { Margin = new Thickness(0, 12, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border pose = LabVisualStyles.CreateSectionCard(poseView);
            pose.Padding = new Thickness(10);
            grid.Children.Add(pose);

            StackPanel side = new StackPanel();
            side.Children.Add(Body("实时姿态与专业诊断", 18, Palette.TextBrush));
            Border guide = LabVisualStyles.CreateInstructionCard("先静止校准，再缓慢转动手柄", "将手柄平放且不要触碰，完成 3 秒静止采样；随后检查左右旋转、前后倾斜和侧向倾斜。轴冻结、跳变或采样中断会显示为需要注意。", "1");
            guide.Margin = new Thickness(0, 10, 0, 10);
            side.Children.Add(guide);
            motionValues.Margin = new Thickness(0, 8, 0, 0);
            side.Children.Add(motionValues);
            motionDiagnostics.Margin = new Thickness(0, 8, 0, 0);
            motionDiagnostics.TextWrapping = TextWrapping.Wrap;
            side.Children.Add(motionDiagnostics);
            calibrationText.Margin = new Thickness(0, 8, 0, 0);
            calibrationText.TextWrapping = TextWrapping.Wrap;
            side.Children.Add(calibrationText);
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            actions.Children.Add(calibrateButton);
            actions.Children.Add(saveCalibrationButton);
            actions.Children.Add(recenterButton);
            actions.Children.Add(resetButton);
            side.Children.Add(actions);
            side.Children.Add(rawPoseCheck);
            side.Children.Add(SliderRow("姿态灵敏度", sensitivitySlider, "0.5× — 2.0×"));
            side.Children.Add(SliderRow("显示平滑度", smoothingSlider, "低 — 高"));
            Expander details = new Expander { Header = "查看原始传感器数据", Foreground = Palette.TextBrush, Margin = new Thickness(0, 12, 0, 0), Content = motionRaw };
            side.Children.Add(details);
            Border card = LabVisualStyles.CreateSectionCard(side);
            card.Padding = new Thickness(18);
            Grid.SetColumn(card, 2);
            grid.Children.Add(card);
            return Scroll(grid);
        }

        private UIElement BuildLightbarTab()
        {
            StackPanel panel = new StackPanel { Margin = new Thickness(0, 12, 0, 8) };
            StackPanel battery = new StackPanel { Margin = new Thickness(18) };
            battery.Children.Add(Body("电量与连接", 18, Palette.TextBrush));
            batteryText.Margin = new Thickness(0, 10, 0, 0);
            battery.Children.Add(batteryText);
            panel.Children.Add(LabVisualStyles.CreateSectionCard(battery));
            StackPanel light = new StackPanel { Margin = new Thickness(18) };
            light.Children.Add(Body("灯带控制", 18, Palette.TextBrush));
            lightbarText.Margin = new Thickness(0, 10, 0, 0);
            lightbarText.TextWrapping = TextWrapping.Wrap;
            light.Children.Add(lightbarText);
            WrapPanel controls = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), IsEnabled = false };
            controls.Children.Add(Button("默认蓝色", false));
            controls.Children.Add(Button("自定义颜色", false));
            controls.Children.Add(Button("呼吸效果", false));
            controls.Children.Add(Button("恢复设备默认", false));
            light.Children.Add(controls);
            Border lightCard = LabVisualStyles.CreateSectionCard(light);
            lightCard.Margin = new Thickness(0, 14, 0, 0);
            panel.Children.Add(lightCard);
            return Scroll(panel);
        }

        private UIElement BuildAdaptiveTriggerTab()
        {
            StackPanel panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(Body("自适应扳机 · 高级实验模块", 20, Palette.TextBrush));
            triggerText.Margin = new Thickness(0, 10, 0, 0);
            triggerText.TextWrapping = TextWrapping.Wrap;
            panel.Children.Add(triggerText);
            WrapPanel presets = new WrapPanel { Margin = new Thickness(0, 16, 0, 0), IsEnabled = false };
            string[] names = { "无阻力", "固定阻力", "阻力墙", "分段阻力", "弹簧感", "点击感", "一键恢复" };
            for (int i = 0; i < names.Length; i++) presets.Children.Add(Button(names[i], false));
            panel.Children.Add(presets);
            return Scroll(LabVisualStyles.CreateSectionCard(panel));
        }

        private void UpdateTouch(TouchpadAnalysisSnapshot snapshot)
        {
            snapshot = snapshot ?? new TouchpadAnalysisSnapshot();
            touchRenderer.SetSnapshot(snapshot);
            touchGuidance.Text = snapshot.Guidance;
            touchMetrics.Text = snapshot.IsAvailable
                ? string.Format(CultureInfo.InvariantCulture, "触点 {0} · 最多双触点 {1} · 网格覆盖 {2:0.0}% · 边缘覆盖 {3:0.0}% · 样本 {4}{5}\n{6}", snapshot.ActiveContactCount, snapshot.MaximumSimultaneousContacts, snapshot.CoveragePercent, snapshot.EdgeCoveragePercent, snapshot.SampleCount, snapshot.IsRecording ? " · 录制剩余 " + snapshot.RecordingSecondsRemaining.ToString("0.0", CultureInfo.InvariantCulture) + " 秒" : string.Empty, snapshot.TestSummary)
                : snapshot.AvailabilityMessage;
            touchPauseButton.Content = snapshot.IsPaused ? "继续轨迹" : "暂停轨迹";
            string details = "";
            for (int i = 0; i < snapshot.Points.Length; i++)
            {
                TouchPointState point = snapshot.Points[i];
                details += string.Format(CultureInfo.InvariantCulture, "触点 {0}: ({1:0.000}, {2:0.000}) · {3} · {4:0.00} 触摸板宽度/秒\n", point.ContactId, point.X, point.Y, point.Phase, point.Speed);
            }
            touchPointDetails.Text = string.IsNullOrEmpty(details) ? "当前没有活动触点。触点结束后不会保留为活动状态。" : details;
        }

        private void UpdateMotion(MotionViewState motion, GyroscopeDiagnosticsSnapshot diagnostics, bool native)
        {
            bool available = native && motion != null && motion.IsAvailable && motion.Sample != null && motion.Sample.IsValid;
            poseView.SetState(available ? motion : null);
            diagnostics = diagnostics ?? new GyroscopeDiagnosticsSnapshot();
            MotionFusionSnapshot pose = motion == null ? null : (rawPoseCheck.IsChecked == true && motion.RawPose != null ? motion.RawPose : motion.Pose);
            motionValues.Text = available && pose != null
                ? string.Format(CultureInfo.InvariantCulture, "Pitch {0:+0.0;-0.0;0.0}°   Roll {1:+0.0;-0.0;0.0}°   Yaw {2:+0.0;-0.0;0.0}°\n角速度 ({3:+0.00;-0.00;0.00}, {4:+0.00;-0.00;0.00}, {5:+0.00;-0.00;0.00}) °/s\n加速度 ({6:+0.000;-0.000;0.000}, {7:+0.000;-0.000;0.000}, {8:+0.000;-0.000;0.000}) g · {9:0} Hz", pose.Pitch, pose.Roll, pose.Yaw, motion.Sample.GyroX, motion.Sample.GyroY, motion.Sample.GyroZ, motion.Sample.AccelX, motion.Sample.AccelY, motion.Sample.AccelZ, motion.UpdatesPerSecond)
                : "当前没有可用于姿态融合的真实运动数据。";
            motionDiagnostics.Text = available
                ? string.Format(CultureInfo.InvariantCulture, "诊断 {0} / {1} 分 · 双向轴响应 X {2} / Y {3} / Z {4} · 中断 {5} · 跳变 {6}\n请依次做左右旋转、前后倾斜和侧向倾斜；每个轴需出现正负方向响应。\n{7}", diagnostics.Status, diagnostics.Score, YesNo(diagnostics.AxisXResponded), YesNo(diagnostics.AxisYResponded), YesNo(diagnostics.AxisZResponded), diagnostics.InterruptedSamples, diagnostics.JumpCount, diagnostics.Notes)
                : "静止噪声、零偏、轴响应、冻结、跳变与中断只对真实 HID 样本生成结论。";
            MotionCalibrationResult calibration = motion == null ? null : motion.Calibration;
            calibrationText.Text = calibration != null && calibration.IsValid
                ? string.Format(CultureInfo.InvariantCulture, "校准已应用：Bias ({0:0.000}, {1:0.000}, {2:0.000}) °/s · Noise ({3:0.000}, {4:0.000}, {5:0.000}) °/s · {6} 样本", calibration.BiasX, calibration.BiasY, calibration.BiasZ, calibration.StandardDeviationX, calibration.StandardDeviationY, calibration.StandardDeviationZ, calibration.SampleCount)
                : MotionCalibrationLabel(motion == null ? MotionCalibrationState.Unsupported : motion.CalibrationState, motion == null ? string.Empty : motion.AvailabilityMessage);
            motionRaw.Text = available
                ? string.Format(CultureInfo.InvariantCulture, "Raw gyro: {0}, {1}, {2}\nRaw accel: {3}, {4}, {5}\nReport: 0x{6:X2} · {7} bytes · {8}\nCRC: {9} · seq {10}\n坐标：设备原始 X/Y/Z 轴；界面将融合后的 Pitch/Roll/Yaw 映射到简化手柄平面。Yaw 无磁力计参考，可能随时间漂移。", motion.Sample.RawGyroX, motion.Sample.RawGyroY, motion.Sample.RawGyroZ, motion.Sample.RawAccelX, motion.Sample.RawAccelY, motion.Sample.RawAccelZ, motion.Sample.SourceReportId, motion.Sample.ReportLength, motion.Sample.Layout, motion.Sample.CrcValidated ? "通过/USB 不适用" : "失败", motion.Sample.Sequence)
                : "未收到原始数据。";
            bool calibrating = motion != null && (motion.CalibrationState == MotionCalibrationState.Settling || motion.CalibrationState == MotionCalibrationState.Sampling);
            calibrateButton.IsEnabled = available && !calibrating;
            saveCalibrationButton.IsEnabled = available && calibration != null && calibration.IsValid;
            recenterButton.IsEnabled = available && pose != null && pose.HasPose;
            resetButton.IsEnabled = native;
        }

        private void UpdateBatteryAndOutputs(ControllerState controller, DualSenseAdvancedCapabilities capabilities, bool native)
        {
            batteryText.Text = native
                ? "电量：" + (capabilities.BatteryAvailable ? controller.BatteryLevel.ToString(CultureInfo.InvariantCulture) + "%（" + controller.BatteryLabel + "）" : "未知（当前报告未提供可靠值）") + "\n连接：" + controller.ConnectionTypeLabel + "\n充电状态：" + ChargingLabel(controller.DualSense == null ? "unknown" : controller.DualSense.BatteryChargingState)
                : "电量：未知\n连接：未连接 DualSense\n充电状态：未知";
            lightbarText.Text = "当前输入状态只标记设备具有灯带，并未可靠读取实际灯带颜色。现有输出架构没有完成 USB/蓝牙灯带实机验证，因此默认蓝色、自定义颜色、呼吸效果和恢复默认均保持禁用，不发送未经确认的报告。\n\nUSB：" + capabilities.UsbStatus + "\n蓝牙：" + capabilities.BluetoothStatus;
            triggerText.Text = "当前版本未开放自适应扳机输出。LT/RT 普通模拟量输入已实现，但固定阻力、阻力墙、分段阻力、弹簧感和点击感需要分别验证 USB/蓝牙输出启用位、报告长度与恢复路径。所有控件保持禁用，断开和退出时不会遗留实验性效果。\n\nUSB：待验证，未开放\n蓝牙：待验证，未开放";
        }

        private void ApplySavedProfileOnce(bool native, MotionViewState motion)
        {
            if (!native || string.IsNullOrEmpty(currentDeviceId) || string.Equals(profileAppliedDeviceId, currentDeviceId, StringComparison.OrdinalIgnoreCase)) return;
            DualSenseDeviceCalibrationProfile profile = calibrationStore.Load(currentDeviceId);
            if (profile == null)
            {
                profileAppliedDeviceId = currentDeviceId;
                return;
            }
            sensitivitySlider.Value = profile.Sensitivity;
            smoothingSlider.Value = profile.Smoothing;
            if (motion != null && motion.IsAvailable)
            {
                MotionCalibrationResult saved = new MotionCalibrationResult
                {
                    DeviceId = currentDeviceId,
                    BiasX = profile.GyroBiasX,
                    BiasY = profile.GyroBiasY,
                    BiasZ = profile.GyroBiasZ,
                    StandardDeviationX = profile.GyroNoiseX,
                    StandardDeviationY = profile.GyroNoiseY,
                    StandardDeviationZ = profile.GyroNoiseZ,
                    SampleCount = profile.SampleCount,
                    IsValid = true
                };
                string reason;
                if (motionManager.ApplyCalibration(currentDeviceId, saved, out reason)) profileAppliedDeviceId = currentDeviceId;
            }
        }

        private void SaveCalibration()
        {
            MotionViewState view = motionManager.Get(currentDeviceId);
            try
            {
                string path = calibrationStore.Save(currentDeviceId, view == null ? null : view.Calibration, sensitivitySlider.Value, smoothingSlider.Value);
                calibrationText.Text = "设备校准已保存：" + path;
            }
            catch (Exception ex)
            {
                calibrationText.Text = "保存失败：" + ex.Message;
            }
        }

        private void SetTouchEnabled(bool enabled)
        {
            touchTestButton.IsEnabled = enabled;
            touchRecordButton.IsEnabled = enabled;
            touchPauseButton.IsEnabled = enabled;
            touchClearButton.IsEnabled = enabled;
        }

        private static string BuildCapabilityLine(DualSenseAdvancedCapabilities value)
        {
            return "真实字段：按压 " + YesNo(value.TouchpadPressedAvailable) + " · 双触点 " + YesNo(value.TouchPoint2Available) + " · 六轴 " + YesNo(value.GyroscopeAvailable) + " · 电量 " + YesNo(value.BatteryAvailable) + "   |   高级输出：灯带 未开放 · 自适应扳机 未开放";
        }

        private static string MotionCalibrationLabel(MotionCalibrationState state, string unavailable)
        {
            switch (state)
            {
                case MotionCalibrationState.Settling: return "准备静止：请将手柄平放在稳定桌面上，不要触碰。";
                case MotionCalibrationState.Sampling: return "正在采样 3 秒：校准期间震动已禁用。";
                case MotionCalibrationState.Failed: return "校准失败，请保持静止后重试。";
                case MotionCalibrationState.NotCalibrated: return "尚未校准；姿态可显示，但零偏尚未扣除。";
                default: return string.IsNullOrEmpty(unavailable) ? "当前版本暂不支持检测。" : unavailable;
            }
        }

        private static TabItem Tab(string header, UIElement content)
        {
            return new TabItem { Header = header, Content = content, Foreground = Palette.TextBrush, Background = Palette.SurfaceBrush, Padding = new Thickness(16, 8, 16, 8) };
        }

        private static ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        private static Button Button(string text, bool primary)
        {
            Button button = new Button { Content = text, Style = primary ? LabVisualStyles.PrimaryButtonStyle : LabVisualStyles.SecondaryButtonStyle, Margin = new Thickness(0, 0, 8, 8), MinHeight = 34 };
            AutomationProperties.SetName(button, text);
            AutomationProperties.SetHelpText(button, "按 Enter 或空格键执行");
            return button;
        }

        private static Slider Slider(double min, double max, double value)
        {
            return new Slider { Minimum = min, Maximum = max, Value = value, Width = 190, IsSnapToTickEnabled = false };
        }

        private static UIElement SliderRow(string label, Slider slider, string hint)
        {
            StackPanel row = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            row.Children.Add(Secondary(label + " · " + hint));
            row.Children.Add(slider);
            return row;
        }

        private static TextBlock Body(string text, double size, Brush brush)
        {
            return new TextBlock { Text = text, FontFamily = LabVisualStyles.UiFont, FontSize = size, Foreground = brush };
        }

        private static TextBlock Secondary(string text)
        {
            return new TextBlock { Text = text, Style = LabVisualStyles.SecondaryTextStyle };
        }

        private static string YesNo(bool value) { return value ? "是" : "否"; }

        private static string ChargingLabel(string value)
        {
            if (string.Equals(value, "charging", StringComparison.OrdinalIgnoreCase)) return "充电中";
            if (string.Equals(value, "complete", StringComparison.OrdinalIgnoreCase)) return "已充满";
            if (string.Equals(value, "discharging", StringComparison.OrdinalIgnoreCase)) return "未充电 / 电池供电";
            return "未知（当前报告值不在已确认范围）";
        }
    }
}
