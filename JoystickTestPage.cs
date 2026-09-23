using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerLab
{
    public sealed class JoystickTestPage : Grid, IDisposable
    {
        private readonly JoystickTestViewModel viewModel;
        private readonly JoystickTrajectoryPlot leftPlot;
        private readonly JoystickTrajectoryPlot rightPlot;
        private readonly TextBlock deviceText;
        private readonly TextBlock connectionText;
        private readonly TextBlock leftStatusText;
        private readonly TextBlock rightStatusText;
        private readonly TextBlock statusText;
        private readonly TextBlock instructionText;
        private readonly TextBlock countdownText;
        private readonly TextBlock sampleText;
        private readonly TextBlock leftDetails;
        private readonly TextBlock rightDetails;
        private readonly Button stationaryButton;
        private readonly Button circularityButton;
        private readonly Button returnButton;
        private readonly Button completeCircularityButton;
        private readonly Button cancelButton;
        private ControllerState lastState;
        private bool lastRumbleRunning;
        private DateTime lastRumbleStoppedUtc;

        public JoystickTestViewModel ViewModel { get { return viewModel; } }
        public CheckBox NavigationCheckBox { get; private set; }

        public JoystickTestPage(JoystickTestViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException("viewModel");
            this.viewModel = viewModel;
            Background = Palette.WindowBrush;

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(2, 0, 8, 18)
            };
            StackPanel root = new StackPanel { Margin = new Thickness(10, 4, 10, 16) };
            scroll.Content = root;
            Children.Add(scroll);

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel();
            title.Children.Add(new TextBlock { Text = "摇杆专业检测", Foreground = Palette.TextBrush, FontSize = 24, FontWeight = FontWeights.SemiBold });
            title.Children.Add(new TextBlock { Text = "原始归一化输入 · 静止漂移 · 72 区间圆周 · 四方向回中", Foreground = Palette.MutedBrush, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
            heading.Children.Add(title);
            StackPanel metadata = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            deviceText = new TextBlock { Text = "当前设备：未连接", Foreground = Palette.TextBrush, FontSize = 12, TextAlignment = TextAlignment.Right };
            connectionText = new TextBlock { Text = "连接方式：—", Foreground = Palette.MutedBrush, FontSize = 11, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 3, 0, 0) };
            metadata.Children.Add(deviceText);
            metadata.Children.Add(connectionText);
            Grid.SetColumn(metadata, 1);
            heading.Children.Add(metadata);
            root.Children.Add(heading);

            Grid stateCards = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            stateCards.ColumnDefinitions.Add(new ColumnDefinition());
            stateCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            stateCards.ColumnDefinitions.Add(new ColumnDefinition());
            leftStatusText = CreateStatusCard(stateCards, 0, "左摇杆");
            rightStatusText = CreateStatusCard(stateCards, 2, "右摇杆");
            root.Children.Add(stateCards);

            Border guide = CreateCard(new Thickness(16, 13, 16, 13));
            guide.Margin = new Thickness(0, 10, 0, 0);
            Grid guideGrid = new Grid();
            guideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            guideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            countdownText = new TextBlock { Text = "—", Foreground = Palette.BlueBrush, FontSize = 34, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            guideGrid.Children.Add(countdownText);
            StackPanel guideCopy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            statusText = new TextBlock { Text = "等待开始检测", Foreground = Palette.TextBrush, FontSize = 16, FontWeight = FontWeights.SemiBold };
            instructionText = new TextBlock { Text = "请选择静止漂移、圆周测试或回中测试。", Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            sampleText = new TextBlock { Text = "有效采样 0 · 0.0 Hz", Foreground = Palette.BlueBrush, FontSize = 11, Margin = new Thickness(0, 5, 0, 0) };
            TextBlock criteria = new TextBlock { Text = "判定依据：静止样本与噪声、72 个方向外圈覆盖、首次回中与最终稳定时间。采样不足不会生成正常结论。", Foreground = Palette.MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) };
            NavigationCheckBox = new CheckBox
            {
                Content = "显示历史轨迹",
                IsChecked = true,
                Foreground = Palette.MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(4)
            };
            guideCopy.Children.Add(statusText);
            guideCopy.Children.Add(instructionText);
            guideCopy.Children.Add(sampleText);
            guideCopy.Children.Add(criteria);
            guideCopy.Children.Add(NavigationCheckBox);
            Grid.SetColumn(guideCopy, 1);
            guideGrid.Children.Add(guideCopy);
            guide.Child = guideGrid;
            root.Children.Add(guide);

            Grid plots = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            plots.ColumnDefinitions.Add(new ColumnDefinition());
            plots.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            plots.ColumnDefinitions.Add(new ColumnDefinition());
            leftPlot = new JoystickTrajectoryPlot(Palette.Green);
            rightPlot = new JoystickTrajectoryPlot(Palette.Blue);
            plots.Children.Add(BuildPlotCard("左摇杆轨迹", leftPlot, 0));
            Border rightCard = BuildPlotCard("右摇杆轨迹", rightPlot, 2);
            plots.Children.Add(rightCard);
            root.Children.Add(plots);

            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            stationaryButton = MakeButton("静止漂移", true);
            circularityButton = MakeButton("圆周测试", false);
            returnButton = MakeButton("回中测试", false);
            completeCircularityButton = MakeButton("完成当前摇杆", true);
            completeCircularityButton.Visibility = Visibility.Collapsed;
            cancelButton = MakeButton("取消当前检测", false);
            Button clearButton = MakeButton("清除数据", false);
            Button restartButton = MakeButton("重新检测", false);
            stationaryButton.Click += delegate { StartStationary(false); };
            circularityButton.Click += delegate { StartCircularity(); };
            returnButton.Click += delegate { StartReturn(); };
            completeCircularityButton.Click += delegate { CompleteCircularity(); };
            cancelButton.Click += delegate { viewModel.Cancel("检测已由用户取消"); Refresh(); };
            clearButton.Click += delegate { viewModel.ClearAll(); Refresh(); };
            restartButton.Click += delegate { StartStationary(true); };
            NavigationCheckBox.Checked += delegate { leftPlot.ShowTrace = rightPlot.ShowTrace = true; };
            NavigationCheckBox.Unchecked += delegate { leftPlot.ShowTrace = rightPlot.ShowTrace = false; };
            actions.Children.Add(stationaryButton);
            actions.Children.Add(circularityButton);
            actions.Children.Add(returnButton);
            actions.Children.Add(completeCircularityButton);
            actions.Children.Add(cancelButton);
            actions.Children.Add(clearButton);
            actions.Children.Add(restartButton);
            root.Children.Add(actions);

            Grid detailGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            detailGrid.ColumnDefinitions.Add(new ColumnDefinition());
            detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            detailGrid.ColumnDefinitions.Add(new ColumnDefinition());
            leftDetails = new TextBlock { Foreground = Palette.MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
            rightDetails = new TextBlock { Foreground = Palette.MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
            detailGrid.Children.Add(BuildDetails("左摇杆详细数据", leftDetails, 0));
            detailGrid.Children.Add(BuildDetails("右摇杆详细数据", rightDetails, 2));
            Expander detailsExpander = new Expander
            {
                Header = "查看详细数据",
                Foreground = Palette.TextBrush,
                Margin = new Thickness(0, 8, 0, 0),
                IsExpanded = false,
                Content = detailGrid
            };
            AutomationProperties.SetName(detailsExpander, "摇杆检测详细数据");
            root.Children.Add(detailsExpander);
            Refresh();
        }

        public void Update(ControllerState state, double sourceSamplingHz, bool rumbleRunning, DateTime rumbleStoppedUtc)
        {
            lastState = state;
            lastRumbleRunning = rumbleRunning;
            lastRumbleStoppedUtc = rumbleStoppedUtc;
            viewModel.Update(state, DateTime.UtcNow);
            deviceText.Text = "当前设备：" + (state == null || !state.IsConnected ? "未连接" : state.DeviceName);
            connectionText.Text = state == null || !state.IsConnected
                ? "连接方式：—"
                : "连接方式：" + state.ConnectionTypeLabel + " · " + state.InputSourceLabel + " · 输入轮询 " + Math.Max(0, sourceSamplingHz).ToString("0", CultureInfo.InvariantCulture) + " Hz";
            Refresh();
        }

        public void Cancel(string reason)
        {
            if (viewModel.IsTestActive) viewModel.Cancel(reason);
            Refresh();
        }

        public void ResetForDeviceChange(string reason)
        {
            viewModel.ResetForDeviceChange(reason);
            Refresh();
        }

        public void Dispose()
        {
            viewModel.Dispose();
        }

        private void StartStationary(bool clearFirst)
        {
            if (clearFirst) viewModel.ClearAll();
            string reason;
            if (!viewModel.StartStationary(lastState, lastRumbleRunning, lastRumbleStoppedUtc, out reason)) viewModel.ReportBlocked(reason);
            Refresh();
        }

        private void StartCircularity()
        {
            string reason;
            if (!viewModel.StartCircularity(lastState, lastRumbleRunning, lastRumbleStoppedUtc, out reason)) viewModel.ReportBlocked(reason);
            Refresh();
        }

        private void StartReturn()
        {
            string reason;
            if (!viewModel.StartReturnTest(lastState, lastRumbleRunning, lastRumbleStoppedUtc, out reason)) viewModel.ReportBlocked(reason);
            Refresh();
        }

        private void CompleteCircularity()
        {
            string reason;
            if (!viewModel.CompleteCircularityStep(out reason)) viewModel.ReportBlocked(reason);
            Refresh();
        }

        private void Refresh()
        {
            bool connected = lastState != null && lastState.IsConnected && lastState.HasRealInput;
            bool active = viewModel.IsTestActive;
            statusText.Text = viewModel.StatusMessage;
            instructionText.Text = viewModel.Instruction;
            countdownText.Text = viewModel.CountdownValue > 0 ? viewModel.CountdownValue.ToString(CultureInfo.InvariantCulture) : "●";
            sampleText.Text = string.Format(CultureInfo.InvariantCulture, "有效采样 {0} · {1:0.0} Hz · 分析输入 -1.0～1.0", viewModel.CurrentSampleCount, viewModel.CurrentSamplingFrequencyHz);
            stationaryButton.IsEnabled = connected && !active;
            circularityButton.IsEnabled = connected && !active;
            returnButton.IsEnabled = connected && !active;
            completeCircularityButton.Visibility = viewModel.IsCircularityActive ? Visibility.Visible : Visibility.Collapsed;
            completeCircularityButton.IsEnabled = viewModel.IsCircularityActive;
            cancelButton.IsEnabled = active;

            leftStatusText.Text = HealthSummary(viewModel.LeftResult);
            rightStatusText.Text = HealthSummary(viewModel.RightResult);
            leftDetails.Text = BuildDetailsText(viewModel.LeftResult);
            rightDetails.Text = BuildDetailsText(viewModel.RightResult);

            double leftX = lastState == null ? 0 : lastState.LeftStickX;
            double leftY = lastState == null ? 0 : lastState.LeftStickY;
            double rightX = lastState == null ? 0 : lastState.RightStickX;
            double rightY = lastState == null ? 0 : lastState.RightStickY;
            leftPlot.SetData(leftX, leftY, viewModel.LeftTrace, viewModel.LeftResult);
            rightPlot.SetData(rightX, rightY, viewModel.RightTrace, viewModel.RightResult);
        }

        private static string HealthSummary(StickTestResult result)
        {
            if (result == null || result.Health == null || !result.Health.IsComplete) return "Not Tested · 未检测";
            return result.Health.EnglishStatus + " · " + result.Health.ChineseStatus + " · " + result.Health.Score.ToString(CultureInfo.InvariantCulture) + "/100";
        }

        private static string BuildDetailsText(StickTestResult result)
        {
            if (result == null) return "暂无检测数据。";
            StringBuilder text = new StringBuilder();
            JoystickStationaryResult stationary = result.Stationary;
            text.AppendLine("静止漂移");
            if (stationary == null) text.AppendLine("未检测");
            else if (!stationary.IsValid) text.AppendLine(stationary.InvalidReason);
            else
            {
                text.AppendFormat(CultureInfo.InvariantCulture, "平均 X/Y {0:0.00}% / {1:0.00}%\n中心偏移 {2:0.00}% · 最大偏移 {3:0.00}%\n标准差 X/Y {4:0.00}% / {5:0.00}%\n噪声 {6:0.00}% · 方向 {7}\n稳定性 {8}/100 · {9} 点 · {10:0.0} Hz\n",
                    stationary.AverageXPercent, stationary.AverageYPercent, stationary.CenterOffsetPercent, stationary.MaximumOffsetPercent,
                    stationary.StandardDeviationXPercent, stationary.StandardDeviationYPercent, stationary.NoiseLevelPercent,
                    stationary.DriftDirection, stationary.StabilityScore, stationary.SampleCount, stationary.SamplingFrequencyHz);
            }

            text.AppendLine();
            text.AppendLine("自动死区建议");
            DeadzoneRecommendation deadzone = result.Deadzone;
            if (deadzone == null) text.AppendLine("完成静止漂移后生成");
            else
            {
                text.AppendFormat(CultureInfo.InvariantCulture, "最大偏移 {0:0.0}% · 噪声余量 {1:0.0}%\n最低 {2:0.0}% · 推荐 {3:0.0}% · 稳定 {4:0.0}%\n圆形 {5:0.0}% · 轴向 X/Y {6:0.0}% / {7:0.0}%\n",
                    deadzone.DetectedMaximumOffsetPercent, deadzone.NoiseMarginPercent, deadzone.MinimumDeadzonePercent,
                    deadzone.RecommendedDeadzonePercent, deadzone.StableDeadzonePercent, deadzone.CircularDeadzonePercent,
                    deadzone.AxialDeadzoneXPercent, deadzone.AxialDeadzoneYPercent);
                if (!string.IsNullOrEmpty(deadzone.Warning)) text.AppendLine(deadzone.Warning);
            }

            text.AppendLine();
            text.AppendLine("圆周测试");
            StickCircularityResult circular = result.Circularity;
            if (circular == null) text.AppendLine("未检测");
            else if (!circular.IsValid) text.AppendLine(circular.InvalidReason);
            else
            {
                text.AppendFormat(CultureInfo.InvariantCulture, "X+ / X- {0:0.0}% / {1:0.0}% · Y+ / Y- {2:0.0}% / {3:0.0}%\n最大/最小有效/平均半径 {4:0.0}% / {5:0.0}% / {6:0.0}%\n外圈覆盖 {7:0.0}% · 象限覆盖 {8:0.0}%\n圆度误差 {9:0.0}% · 不对称 {10:0.0}% · {11}\n缺失方向 {12} · 方形限制 {13} · 削角 {14}\n{15} 点 · 72 区间 · {16:0.0} Hz\n",
                    circular.MaximumXPositivePercent, circular.MaximumXNegativePercent, circular.MaximumYPositivePercent, circular.MaximumYNegativePercent,
                    circular.MaximumRadiusPercent, circular.MinimumEffectiveRadiusPercent, circular.AverageRadiusPercent,
                    circular.OuterCoveragePercent, circular.QuadrantCoveragePercent, circular.CircularityErrorPercent,
                    circular.DirectionalAsymmetryPercent, circular.DirectionConsistency, circular.MissingDirections,
                    circular.IsSquareLimited ? "是" : "否", circular.HasCornerCutting ? "是" : "否", circular.SampleCount, circular.SamplingFrequencyHz);
            }

            text.AppendLine();
            text.AppendLine("回中测试");
            if (result.ReturnResults.Count == 0) text.AppendLine("未检测");
            for (int i = 0; i < result.ReturnResults.Count; i++)
            {
                StickReturnResult item = result.ReturnResults[i];
                if (item == null) continue;
                if (!item.IsValid)
                {
                    text.AppendLine(JoystickAnalyzer.DirectionLabel(item.Direction) + "：" + item.InvalidReason);
                    continue;
                }
                List<string> flags = new List<string>();
                if (item.IsSlowReturn) flags.Add("回中缓慢");
                if (item.HasOvershoot) flags.Add("存在过冲");
                if (item.BounceCount > 1) flags.Add("多次反弹");
                if (item.HasPersistentJitter) flags.Add("持续抖动");
                if (item.FailedToStabilize) flags.Add("无法稳定");
                text.AppendFormat(CultureInfo.InvariantCulture, "{0}：开始 {1:0} ms · 首次中心 {2:0} ms · 稳定 {3:0} ms\n  回中 {4:0} ms · 过冲 {5:0.0}% · 反弹 {6} · 收敛 {7:0} ms · 最终 {8:0.0}%{9}\n",
                    JoystickAnalyzer.DirectionLabel(item.Direction), item.ReturnStartTimeMilliseconds, item.FirstCenterEntryTimeMilliseconds,
                    item.StableCenterTimeMilliseconds, item.ReturnDurationMilliseconds, item.OvershootPercent, item.BounceCount,
                    item.SettlingTimeMilliseconds, item.FinalOffsetPercent, flags.Count == 0 ? string.Empty : " · " + string.Join("、", flags.ToArray()));
            }

            text.AppendLine();
            text.AppendLine("综合健康评分");
            StickHealthScoreResult health = result.Health;
            if (health == null || !health.IsComplete) text.Append("Not Tested · 未检测（需完成静止、圆周与四方向回中）");
            else
            {
                text.AppendFormat(CultureInfo.InvariantCulture, "{0} · {1} · {2}/100\n中心 {3:0} · 噪声 {4:0} · 覆盖 {5:0} · 圆度 {6:0}\n行程一致性 {7:0} · 回中速度 {8:0} · 反弹 {9:0} · 死区 {10:0}",
                    health.EnglishStatus, health.ChineseStatus, health.Score, health.CenterStabilityScore, health.NoiseScore,
                    health.CircularCoverageScore, health.CircularityScore, health.DirectionConsistencyScore,
                    health.ReturnSpeedScore, health.BounceScore, health.DeadzoneScore);
            }
            return text.ToString();
        }

        private static TextBlock CreateStatusCard(Grid host, int column, string label)
        {
            StackPanel panel = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };
            panel.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 11 });
            TextBlock value = new TextBlock { Text = "Not Tested · 未检测", Foreground = Palette.TextBrush, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0) };
            panel.Children.Add(value);
            Border card = CreateCard(new Thickness(0));
            card.Child = panel;
            Grid.SetColumn(card, column);
            host.Children.Add(card);
            return value;
        }

        private static Border BuildPlotCard(string title, JoystickTrajectoryPlot plot, int column)
        {
            Grid panel = new Grid { Margin = new Thickness(12, 10, 12, 12) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(270) });
            panel.Children.Add(new TextBlock { Text = title, Foreground = Palette.TextBrush, FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            Grid.SetRow(plot, 1);
            panel.Children.Add(plot);
            Border card = CreateCard(new Thickness(0));
            card.Child = panel;
            Grid.SetColumn(card, column);
            return card;
        }

        private static Expander BuildDetails(string header, TextBlock content, int column)
        {
            Border body = CreateCard(new Thickness(14));
            body.Child = content;
            Expander expander = new Expander
            {
                Header = header,
                Foreground = Palette.TextBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsExpanded = false,
                Content = body,
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetColumn(expander, column);
            return expander;
        }

        private static Border CreateCard(Thickness padding)
        {
            return new Border
            {
                Background = Palette.SurfaceBrush,
                BorderBrush = Palette.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = padding
            };
        }

        private static Button MakeButton(string label, bool primary)
        {
            Button button = new Button
            {
                Content = label,
                Height = 34,
                MinWidth = 96,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(13, 4, 13, 4),
                Foreground = primary ? Brushes.White : Palette.TextBrush,
                Background = primary ? Palette.BlueBrush : Palette.SurfaceRaisedBrush,
                BorderBrush = primary ? Palette.BlueBrush : Palette.BorderBrush,
                BorderThickness = new Thickness(1),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            AutomationProperties.SetName(button, label);
            AutomationProperties.SetHelpText(button, "按 Enter 或空格键执行");
            return button;
        }
    }

    public sealed class JoystickTrajectoryPlot : FrameworkElement
    {
        private readonly Color accent;
        private IList<StickSample> trace = new List<StickSample>();
        private double currentX;
        private double currentY;
        private double deadzone = JoystickAnalysisConfiguration.CenterThreshold;
        private double driftX;
        private double driftY;
        private bool showTrace = true;

        public bool ShowTrace
        {
            get { return showTrace; }
            set { showTrace = value; InvalidateVisual(); }
        }

        public JoystickTrajectoryPlot(Color accent)
        {
            this.accent = accent;
            ClipToBounds = true;
            SnapsToDevicePixels = true;
        }

        public void SetData(double x, double y, IList<StickSample> trace, StickTestResult result)
        {
            currentX = Math.Max(-1.0, Math.Min(1.0, x));
            currentY = Math.Max(-1.0, Math.Min(1.0, y));
            this.trace = trace ?? new List<StickSample>();
            deadzone = result != null && result.Deadzone != null ? result.Deadzone.RecommendedDeadzonePercent / 100.0 : JoystickAnalysisConfiguration.CenterThreshold;
            driftX = result != null && result.Stationary != null ? result.Stationary.AverageXPercent / 100.0 : 0;
            driftY = result != null && result.Stationary != null ? result.Stationary.AverageYPercent / 100.0 : 0;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 4 || height <= 4) return;
            Point center = new Point(width / 2.0, height / 2.0);
            double radius = Math.Max(8, Math.Min(width, height) / 2.0 - 18);
            Brush subtle = new SolidColorBrush(Color.FromArgb(22, accent.R, accent.G, accent.B));
            Brush grid = new SolidColorBrush(Color.FromArgb(86, Palette.Muted.R, Palette.Muted.G, Palette.Muted.B));
            Pen gridPen = new Pen(grid, 1);
            Pen outerPen = new Pen(new SolidColorBrush(Color.FromArgb(150, accent.R, accent.G, accent.B)), 1.5);
            Pen tracePen = new Pen(new SolidColorBrush(Color.FromArgb(165, accent.R, accent.G, accent.B)), 1.35);
            drawingContext.DrawEllipse(subtle, outerPen, center, radius, radius);
            drawingContext.DrawLine(gridPen, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
            drawingContext.DrawLine(gridPen, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));
            double deadzoneRadius = Math.Max(2, radius * Math.Max(0, Math.Min(0.30, deadzone)));
            drawingContext.DrawEllipse(null, new Pen(Palette.WarningBrush, 1.2), center, deadzoneRadius, deadzoneRadius);

            int start = Math.Max(0, trace.Count - 900);
            Point? previous = null;
            for (int i = start; showTrace && i < trace.Count; i++)
            {
                StickSample sample = trace[i];
                if (sample == null) continue;
                Point point = Map(center, radius, sample.X, sample.Y);
                if (previous.HasValue) drawingContext.DrawLine(tracePen, previous.Value, point);
                previous = point;
            }

            Point drift = Map(center, radius, driftX, driftY);
            drawingContext.DrawEllipse(Palette.WarningBrush, null, drift, 3.5, 3.5);
            Point current = Map(center, radius, currentX, currentY);
            drawingContext.DrawEllipse(Palette.TextBrush, new Pen(new SolidColorBrush(accent), 2), current, 6, 6);
        }

        private static Point Map(Point center, double radius, double x, double y)
        {
            return new Point(center.X + x * radius, center.Y - y * radius);
        }
    }
}
