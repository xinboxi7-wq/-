using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerLab
{
    public sealed class ControllerHealthCheckPage : Grid, IDisposable
    {
        private readonly ControllerHealthCheckViewModel viewModel;
        private readonly ControllerHealthReportStore store;
        private readonly Grid overviewPanel;
        private readonly Grid wizardPanel;
        private readonly Grid reportPanel;
        private readonly Grid historyPanel;
        private TextBlock overviewDevice;
        private TextBlock overviewMeta;
        private TextBlock supportedText;
        private TextBlock unsupportedText;
        private TextBlock estimateText;
        private Button startButton;
        private Button historyButton;
        private TextBlock progressText;
        private ProgressBar progressBar;
        private ListBox stepList;
        private TextBlock wizardTitle;
        private TextBlock wizardInstruction;
        private TextBlock wizardCondition;
        private TextBlock wizardDetail;
        private WrapPanel buttonChecklist;
        private WrapPanel rumbleFeedbackPanel;
        private Button previousButton;
        private Button nextButton;
        private Button retestButton;
        private Button skipButton;
        private Button cancelButton;
        private TextBlock reportScore;
        private TextBlock reportStatus;
        private TextBlock reportMeta;
        private StackPanel reportCards;
        private ListBox historyList;
        private TextBlock historyDetail;
        private ControllerState lastState;
        private string preparedDeviceId = string.Empty;
        private string stepSignature = string.Empty;
        private string checklistSignature = string.Empty;
        private string renderedReportId = string.Empty;
        private List<ControllerHealthReport> historyReports = new List<ControllerHealthReport>();

        public bool IsActive { get { return viewModel.IsRunning; } }

        public ControllerHealthCheckPage(ControllerHealthCheckViewModel viewModel, ControllerHealthReportStore store)
        {
            if (viewModel == null) throw new ArgumentNullException("viewModel");
            this.viewModel = viewModel;
            this.store = store ?? new ControllerHealthReportStore();
            Background = Palette.WindowBrush;

            overviewPanel = BuildOverview();
            wizardPanel = BuildWizard();
            reportPanel = BuildReport();
            historyPanel = BuildHistory();
            Children.Add(overviewPanel);
            Children.Add(wizardPanel);
            Children.Add(reportPanel);
            Children.Add(historyPanel);
            ShowPanel(overviewPanel);
        }

        public void Update(ControllerState state)
        {
            lastState = state;
            string id = state == null || !state.IsConnected ? string.Empty : state.DeviceId;
            if (!viewModel.IsRunning && !viewModel.IsReportReady && !string.Equals(preparedDeviceId, id, StringComparison.OrdinalIgnoreCase))
            {
                string ignored;
                viewModel.Prepare(state, out ignored);
                preparedDeviceId = id;
                RefreshOverview();
            }
            if (viewModel.IsRunning) viewModel.Update(state, DateTime.UtcNow);
            if (viewModel.IsReportReady)
            {
                ShowPanel(reportPanel);
                RefreshReport();
            }
            else if (viewModel.IsRunning)
            {
                ShowPanel(wizardPanel);
                RefreshWizard();
            }
            else if (historyPanel.Visibility != Visibility.Visible)
            {
                ShowPanel(overviewPanel);
                RefreshOverview();
            }
        }

        public void CancelForPageLeave()
        {
            if (!viewModel.IsRunning) return;
            viewModel.CancelAndDiscard();
            ShowPanel(overviewPanel);
            RefreshOverview();
        }

        public void ResetForDeviceChange()
        {
            viewModel.CancelAndDiscard();
            preparedDeviceId = string.Empty;
            renderedReportId = string.Empty;
            ShowPanel(overviewPanel);
        }

        public void Dispose()
        {
            viewModel.Dispose();
        }

        private Grid BuildOverview()
        {
            Grid panel = PageRoot();
            StackPanel body = new StackPanel { Margin = new Thickness(12, 4, 12, 24) };
            ScrollViewer scroll = WrapScroll(body);
            panel.Children.Add(scroll);
            body.Children.Add(new TextBlock { Text = "Controller Health Check", Foreground = Palette.TextBrush, FontSize = 27, FontWeight = FontWeights.SemiBold });
            body.Children.Add(new TextBlock { Text = "一键手柄健康检测", Foreground = Palette.BlueBrush, FontSize = 15, Margin = new Thickness(0, 4, 0, 0) });
            body.Children.Add(new TextBlock { Text = "按设备实际能力生成步骤；不支持和跳过项目不会参与评分。", Foreground = Palette.MutedBrush, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) });

            Border deviceCard = Card(new Thickness(18, 15, 18, 15));
            deviceCard.Margin = new Thickness(0, 16, 0, 0);
            StackPanel device = new StackPanel();
            overviewDevice = new TextBlock { Text = "当前设备：未连接", Foreground = Palette.TextBrush, FontSize = 19, FontWeight = FontWeights.SemiBold };
            overviewMeta = new TextBlock { Text = "请连接真实手柄", Foreground = Palette.MutedBrush, FontSize = 12, Margin = new Thickness(0, 5, 0, 0) };
            estimateText = new TextBlock { Text = "预计时间：—", Foreground = Palette.BlueBrush, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };
            device.Children.Add(overviewDevice);
            device.Children.Add(overviewMeta);
            device.Children.Add(estimateText);
            deviceCard.Child = device;
            body.Children.Add(deviceCard);

            Grid capabilities = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            capabilities.ColumnDefinitions.Add(new ColumnDefinition());
            capabilities.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            capabilities.ColumnDefinitions.Add(new ColumnDefinition());
            supportedText = new TextBlock { Foreground = Palette.TextBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
            unsupportedText = new TextBlock { Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
            capabilities.Children.Add(CapabilityCard("支持的检测项目", supportedText, Palette.Green, 0));
            capabilities.Children.Add(CapabilityCard("不支持的检测项目", unsupportedText, Palette.Warning, 2));
            body.Children.Add(capabilities);

            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            startButton = MakeButton("开始完整检测", true);
            historyButton = MakeButton("查看历史报告", false);
            startButton.Click += delegate { StartHealthCheck(); };
            historyButton.Click += delegate { OpenHistory(); };
            actions.Children.Add(startButton);
            actions.Children.Add(historyButton);
            body.Children.Add(actions);
            return panel;
        }

        private Grid BuildWizard()
        {
            Grid panel = PageRoot();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid header = new Grid { Margin = new Thickness(12, 4, 12, 10) };
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid headerLine = new Grid();
            headerLine.ColumnDefinitions.Add(new ColumnDefinition());
            headerLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerLine.Children.Add(new TextBlock { Text = "完整检测向导", Foreground = Palette.TextBrush, FontSize = 23, FontWeight = FontWeights.SemiBold });
            progressText = new TextBlock { Text = "0 / 0", Foreground = Palette.BlueBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(progressText, 1);
            headerLine.Children.Add(progressText);
            header.Children.Add(headerLine);
            progressBar = new ProgressBar { Height = 5, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 9, 0, 0), Foreground = Palette.BlueBrush, Background = Palette.SurfaceRaisedBrush, BorderThickness = new Thickness(0) };
            Grid.SetRow(progressBar, 1);
            header.Children.Add(progressBar);
            panel.Children.Add(header);

            Grid content = new Grid { Margin = new Thickness(12, 0, 12, 18) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(content, 1);
            panel.Children.Add(content);

            Border stepCard = Card(new Thickness(8));
            stepList = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Palette.TextBrush, IsHitTestVisible = false, Focusable = false };
            stepCard.Child = stepList;
            content.Children.Add(stepCard);

            Border workCard = Card(new Thickness(22, 18, 22, 18));
            Grid.SetColumn(workCard, 2);
            ScrollViewer workScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            StackPanel work = new StackPanel();
            workScroll.Content = work;
            workCard.Child = workScroll;
            wizardTitle = new TextBlock { Text = "当前步骤", Foreground = Palette.TextBrush, FontSize = 24, FontWeight = FontWeights.SemiBold };
            wizardInstruction = new TextBlock { Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0), LineHeight = 29 };
            wizardCondition = new TextBlock { Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 0) };
            wizardDetail = new TextBlock { Foreground = Palette.BlueBrush, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), LineHeight = 21 };
            work.Children.Add(wizardTitle);
            work.Children.Add(wizardInstruction);
            work.Children.Add(wizardCondition);
            work.Children.Add(wizardDetail);

            buttonChecklist = new WrapPanel { Margin = new Thickness(0, 15, 0, 0), Visibility = Visibility.Collapsed };
            work.Children.Add(buttonChecklist);
            rumbleFeedbackPanel = BuildRumbleFeedbackPanel();
            work.Children.Add(rumbleFeedbackPanel);

            Expander details = new Expander { Header = "查看详细数据", Foreground = Palette.TextBrush, Margin = new Thickness(0, 14, 0, 0) };
            TextBlock detailHelp = new TextBlock { Text = "详细采样结果会在完成步骤后写入最终报告；界面仅显示与当前操作有关的摘要。", Foreground = Palette.MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
            details.Content = detailHelp;
            work.Children.Add(details);

            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 22, 0, 0) };
            previousButton = MakeButton("上一步", false);
            nextButton = MakeButton("下一步", true);
            retestButton = MakeButton("重新测试", false);
            skipButton = MakeButton("跳过", false);
            cancelButton = MakeButton("取消检测", false);
            previousButton.Click += delegate { RunAction(viewModel.Previous); };
            nextButton.Click += delegate { RunAction(viewModel.Next); };
            retestButton.Click += delegate { RunAction(viewModel.RetestCurrent); };
            skipButton.Click += delegate { RunAction(viewModel.SkipCurrent); };
            cancelButton.Click += delegate { ConfirmCancel(); };
            actions.Children.Add(previousButton);
            actions.Children.Add(nextButton);
            actions.Children.Add(retestButton);
            actions.Children.Add(skipButton);
            actions.Children.Add(cancelButton);
            work.Children.Add(actions);
            content.Children.Add(workCard);
            return panel;
        }

        private Grid BuildReport()
        {
            Grid panel = PageRoot();
            StackPanel body = new StackPanel { Margin = new Thickness(12, 4, 12, 24) };
            panel.Children.Add(WrapScroll(body));
            Grid hero = new Grid();
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border scoreCard = Card(new Thickness(18));
            reportScore = new TextBlock { Text = "—", Foreground = Palette.BlueBrush, FontSize = 38, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            scoreCard.Child = reportScore;
            hero.Children.Add(scoreCard);
            StackPanel summary = new StackPanel { Margin = new Thickness(18, 4, 0, 4), VerticalAlignment = VerticalAlignment.Center };
            summary.Children.Add(new TextBlock { Text = "手柄健康报告", Foreground = Palette.TextBrush, FontSize = 25, FontWeight = FontWeights.SemiBold });
            reportStatus = new TextBlock { Foreground = Palette.TextBrush, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) };
            reportMeta = new TextBlock { Foreground = Palette.MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0) };
            summary.Children.Add(reportStatus);
            summary.Children.Add(reportMeta);
            Grid.SetColumn(summary, 1);
            hero.Children.Add(summary);
            body.Children.Add(hero);
            reportCards = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            body.Children.Add(reportCards);
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            Button rerun = MakeButton("重新检测", true);
            Button exportJson = MakeButton("导出 JSON", false);
            Button exportMarkdown = MakeButton("导出 Markdown", false);
            Button history = MakeButton("查看历史报告", false);
            rerun.Click += delegate { BeginNewTest(); };
            exportJson.Click += delegate { ExportReport(viewModel.Report, false); };
            exportMarkdown.Click += delegate { ExportReport(viewModel.Report, true); };
            history.Click += delegate { OpenHistory(); };
            actions.Children.Add(rerun);
            actions.Children.Add(exportJson);
            actions.Children.Add(exportMarkdown);
            actions.Children.Add(history);
            body.Children.Add(actions);
            return panel;
        }

        private Grid BuildHistory()
        {
            Grid panel = PageRoot();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid heading = new Grid { Margin = new Thickness(12, 4, 12, 10) };
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(new TextBlock { Text = "历史健康报告", Foreground = Palette.TextBrush, FontSize = 24, FontWeight = FontWeights.SemiBold });
            Button back = MakeButton("返回", false);
            back.Click += delegate { ShowPanel(viewModel.IsReportReady ? reportPanel : overviewPanel); };
            Grid.SetColumn(back, 1);
            heading.Children.Add(back);
            panel.Children.Add(heading);
            Grid content = new Grid { Margin = new Thickness(12, 0, 12, 18) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            content.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(content, 1);
            panel.Children.Add(content);
            Border listCard = Card(new Thickness(8));
            historyList = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Palette.TextBrush };
            historyList.SelectionChanged += delegate { RefreshHistoryDetail(); };
            listCard.Child = historyList;
            content.Children.Add(listCard);
            Border detailCard = Card(new Thickness(18));
            Grid.SetColumn(detailCard, 2);
            StackPanel detail = new StackPanel();
            historyDetail = new TextBlock { Text = "选择一份历史报告。", Foreground = Palette.MutedBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
            detail.Children.Add(historyDetail);
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            Button exportJson = MakeButton("导出 JSON", false);
            Button exportMarkdown = MakeButton("导出 Markdown", false);
            Button delete = MakeButton("删除报告", false);
            exportJson.Click += delegate { ExportSelectedHistory(false); };
            exportMarkdown.Click += delegate { ExportSelectedHistory(true); };
            delete.Click += delegate { DeleteSelectedHistory(); };
            actions.Children.Add(exportJson);
            actions.Children.Add(exportMarkdown);
            actions.Children.Add(delete);
            detail.Children.Add(actions);
            detailCard.Child = new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            content.Children.Add(detailCard);
            return panel;
        }

        private WrapPanel BuildRumbleFeedbackPanel()
        {
            WrapPanel panel = new WrapPanel { Margin = new Thickness(0, 16, 0, 0), Visibility = Visibility.Collapsed };
            AddRumbleFeedbackButton(panel, "正常", RumbleUserFeedback.Normal, true);
            AddRumbleFeedbackButton(panel, "很弱", RumbleUserFeedback.Weak, false);
            AddRumbleFeedbackButton(panel, "无震动", RumbleUserFeedback.NoRumble, false);
            AddRumbleFeedbackButton(panel, "位置不正确", RumbleUserFeedback.WrongPosition, false);
            AddRumbleFeedbackButton(panel, "跳过", RumbleUserFeedback.Skipped, false);
            return panel;
        }

        private void AddRumbleFeedbackButton(Panel panel, string label, RumbleUserFeedback feedback, bool primary)
        {
            Button button = MakeButton(label, primary);
            button.Click += delegate
            {
                string reason;
                if (!viewModel.SubmitRumbleFeedback(feedback, out reason) && !string.IsNullOrEmpty(reason)) wizardDetail.Text = reason;
                RefreshWizard();
            };
            panel.Children.Add(button);
        }

        private void StartHealthCheck()
        {
            string reason;
            if (!viewModel.IsPrepared) viewModel.Prepare(lastState, out reason);
            if (!viewModel.Start(out reason))
            {
                overviewMeta.Text = reason;
                return;
            }
            ShowPanel(wizardPanel);
            RefreshWizard();
        }

        private void BeginNewTest()
        {
            viewModel.CancelAndDiscard();
            renderedReportId = string.Empty;
            preparedDeviceId = string.Empty;
            string reason;
            viewModel.Prepare(lastState, out reason);
            preparedDeviceId = lastState == null ? string.Empty : lastState.DeviceId;
            ShowPanel(overviewPanel);
            RefreshOverview();
        }

        private delegate bool WizardAction(out string reason);

        private void RunAction(WizardAction action)
        {
            string reason;
            if (!action(out reason) && !string.IsNullOrEmpty(reason)) wizardDetail.Text = reason;
            if (viewModel.IsReportReady) { ShowPanel(reportPanel); RefreshReport(); }
            else RefreshWizard();
        }

        private void ConfirmCancel()
        {
            MessageBoxResult result = MessageBox.Show("是否退出并放弃当前检测进度？\n\n选择“是”：退出并放弃\n选择“否”：继续检测", "取消完整检测", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            viewModel.CancelAndDiscard();
            ShowPanel(overviewPanel);
            RefreshOverview();
        }

        private void RefreshOverview()
        {
            bool connected = lastState != null && lastState.IsConnected && lastState.HasRealInput;
            overviewDevice.Text = "当前设备：" + (connected ? lastState.DeviceName : "未连接");
            overviewMeta.Text = connected ? "设备类型：" + lastState.ControllerType + " · 连接方式：" + lastState.ConnectionTypeLabel + " · 输入：" + lastState.InputSourceLabel : "请连接真实 Xbox XInput 或 DualSense HID 手柄。";
            supportedText.Text = viewModel.SupportedItems.Count == 0 ? "—" : "• " + string.Join("\n• ", viewModel.SupportedItems.ToArray());
            unsupportedText.Text = viewModel.UnsupportedItems.Count == 0 ? "无" : "• " + string.Join("\n• ", viewModel.UnsupportedItems.ToArray());
            estimateText.Text = viewModel.EstimatedSeconds <= 0 ? "预计时间：—" : "预计时间：约 " + (viewModel.EstimatedSeconds / 60) + " 分 " + (viewModel.EstimatedSeconds % 60) + " 秒 · 共 " + viewModel.Steps.Count + " 步";
            startButton.IsEnabled = connected && viewModel.IsPrepared;
        }

        private void RefreshWizard()
        {
            HealthCheckStep step = viewModel.CurrentStep;
            if (step == null) return;
            progressText.Text = viewModel.CurrentStepNumber + " / " + viewModel.Steps.Count;
            progressBar.Value = viewModel.Steps.Count == 0 ? 0 : viewModel.CurrentStepNumber / (double)viewModel.Steps.Count;
            wizardTitle.Text = step.Title + " · " + ControllerHealthCheckViewModel.StatusChinese(step.Status);
            wizardInstruction.Text = viewModel.DynamicInstruction;
            wizardCondition.Text = "完成条件：" + step.CompletionCondition;
            wizardDetail.Text = viewModel.IsDisconnected ? "设备已断开。已完成结果保留在内存中；重新连接原设备后点击“重新测试”。" : viewModel.CurrentDetail;
            previousButton.IsEnabled = viewModel.CurrentStepNumber > 1 && !viewModel.IsDisconnected;
            retestButton.IsEnabled = !viewModel.IsDeviceChanged;
            skipButton.IsEnabled = step.Kind != HealthCheckStepKind.DeviceInfo && step.Kind != HealthCheckStepKind.GenerateReport && !viewModel.IsDisconnected;
            nextButton.IsEnabled = !viewModel.IsDisconnected && (step.Status == HealthCheckStepStatus.Passed || step.Status == HealthCheckStepStatus.Attention || step.Status == HealthCheckStepStatus.Abnormal || step.Status == HealthCheckStepStatus.Skipped || step.Kind == HealthCheckStepKind.LeftStickCircularity || step.Kind == HealthCheckStepKind.RightStickCircularity || step.Kind == HealthCheckStepKind.LeftTrigger || step.Kind == HealthCheckStepKind.RightTrigger || step.Kind == HealthCheckStepKind.Touchpad || step.Kind == HealthCheckStepKind.Gyroscope);
            nextButton.Content = step.Status == HealthCheckStepStatus.Testing ? "完成并下一步" : "下一步";
            rumbleFeedbackPanel.Visibility = step.Kind == HealthCheckStepKind.Rumble && viewModel.AwaitingRumbleFeedback ? Visibility.Visible : Visibility.Collapsed;
            buttonChecklist.Visibility = step.Kind == HealthCheckStepKind.Buttons || step.Kind == HealthCheckStepKind.DPad ? Visibility.Visible : Visibility.Collapsed;
            RefreshStepList();
            RefreshButtonChecklist();
        }

        private void RefreshStepList()
        {
            StringBuilder signature = new StringBuilder();
            for (int i = 0; i < viewModel.Steps.Count; i++) signature.Append((int)viewModel.Steps[i].Status).Append(',');
            signature.Append(viewModel.CurrentStepNumber);
            if (signature.ToString() == stepSignature) return;
            stepSignature = signature.ToString();
            stepList.Items.Clear();
            for (int i = 0; i < viewModel.Steps.Count; i++)
            {
                HealthCheckStep step = viewModel.Steps[i];
                Border row = new Border { Padding = new Thickness(9, 7, 9, 7), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 3), Background = i == viewModel.CurrentStepNumber - 1 ? new SolidColorBrush(Color.FromArgb(34, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)) : Brushes.Transparent };
                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.Children.Add(new TextBlock { Text = (i + 1).ToString(CultureInfo.InvariantCulture), Foreground = Palette.MutedBrush, VerticalAlignment = VerticalAlignment.Center });
                TextBlock title = new TextBlock { Text = step.Title, Foreground = Palette.TextBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(title, 1);
                grid.Children.Add(title);
                TextBlock status = new TextBlock { Text = ControllerHealthCheckViewModel.StatusChinese(step.Status), Foreground = StepBrush(step.Status), FontSize = 10, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(status, 2);
                grid.Children.Add(status);
                row.Child = grid;
                stepList.Items.Add(row);
            }
        }

        private void RefreshButtonChecklist()
        {
            if (buttonChecklist.Visibility != Visibility.Visible) return;
            IList<HealthButtonProgress> progress = viewModel.GetButtonProgress();
            StringBuilder signature = new StringBuilder();
            for (int i = 0; i < progress.Count; i++) signature.Append(progress[i].Id).Append(progress[i].Complete ? '1' : progress[i].Pressed ? 'p' : '0');
            if (signature.ToString() == checklistSignature) return;
            checklistSignature = signature.ToString();
            buttonChecklist.Children.Clear();
            for (int i = 0; i < progress.Count; i++)
            {
                HealthButtonProgress item = progress[i];
                Border chip = new Border
                {
                    Background = item.Complete ? new SolidColorBrush(Color.FromArgb(42, Palette.Green.R, Palette.Green.G, Palette.Green.B)) : Palette.SurfaceRaisedBrush,
                    BorderBrush = item.Complete ? Palette.GreenBrush : item.Pressed ? Palette.BlueBrush : Palette.BorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 6, 6),
                    Child = new TextBlock { Text = item.Label + (item.Complete ? " ✓" : item.Pressed ? " · 请释放" : string.Empty), Foreground = item.Complete ? Palette.GreenBrush : Palette.TextBrush, FontSize = 11 }
                };
                buttonChecklist.Children.Add(chip);
            }
        }

        private void RefreshReport()
        {
            ControllerHealthReport report = viewModel.Report;
            if (report == null || renderedReportId == report.ReportId) return;
            renderedReportId = report.ReportId;
            reportScore.Text = report.OverallScore.ToString("0", CultureInfo.InvariantCulture);
            reportStatus.Text = report.OverallStatus + " / " + report.OverallStatusChinese;
            reportMeta.Text = report.DeviceName + " · " + report.ConnectionType + "\n" + report.TestDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " · " + report.TestDurationSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒 · " + (report.IsComplete ? "完整完成" : "检测不完整") + "\n保存位置：" + viewModel.SavedReportPath;
            reportCards.Children.Clear();
            Grid categories = new Grid();
            for (int i = 0; i < report.Categories.Count; i++) categories.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < report.Categories.Count; i++)
            {
                HealthCategoryScore category = report.Categories[i];
                Border card = Card(new Thickness(12));
                card.Margin = new Thickness(i == 0 ? 0 : 4, 0, i == report.Categories.Count - 1 ? 0 : 4, 0);
                StackPanel copy = new StackPanel();
                copy.Children.Add(new TextBlock { Text = category.DisplayName, Foreground = Palette.MutedBrush, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
                copy.Children.Add(new TextBlock { Text = category.Tested ? category.Score.ToString("0", CultureInfo.InvariantCulture) : "—", Foreground = category.Tested ? Palette.TextBrush : Palette.MutedBrush, FontSize = 21, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) });
                copy.Children.Add(new TextBlock { Text = category.Status, Foreground = CategoryBrush(category), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
                card.Child = copy;
                Grid.SetColumn(card, i);
                categories.Children.Add(card);
            }
            reportCards.Children.Add(categories);
            for (int i = 0; i < report.Steps.Count; i++)
            {
                HealthCheckStepRecord item = report.Steps[i];
                if (item.Kind == HealthCheckStepKind.DeviceInfo.ToString() || item.Kind == HealthCheckStepKind.GenerateReport.ToString()) continue;
                reportCards.Children.Add(ReportItemCard(item));
            }
        }

        private Border ReportItemCard(HealthCheckStepRecord item)
        {
            Border card = Card(new Thickness(15, 12, 15, 12));
            card.Margin = new Thickness(0, 8, 0, 0);
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            StackPanel title = new StackPanel();
            title.Children.Add(new TextBlock { Text = item.Title, Foreground = Palette.TextBrush, FontSize = 14, FontWeight = FontWeights.SemiBold });
            title.Children.Add(new TextBlock { Text = item.Status + (item.ParticipatesInScore ? " · " + item.Score.ToString("0", CultureInfo.InvariantCulture) + "/100" : string.Empty), Foreground = RecordBrush(item.Status), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
            grid.Children.Add(title);
            StackPanel detail = new StackPanel();
            detail.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(item.Summary) ? "无摘要" : item.Summary, Foreground = Palette.TextBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(item.Issue)) detail.Children.Add(new TextBlock { Text = "问题：" + item.Issue, Foreground = Palette.WarningBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            if (!string.IsNullOrEmpty(item.Advice)) detail.Children.Add(new TextBlock { Text = "建议：" + item.Advice, Foreground = Palette.MutedBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            Grid.SetColumn(detail, 1);
            grid.Children.Add(detail);
            card.Child = grid;
            return card;
        }

        private void OpenHistory()
        {
            historyReports = store.LoadAll();
            historyList.Items.Clear();
            for (int i = 0; i < historyReports.Count; i++)
            {
                ControllerHealthReport report = historyReports[i];
                historyList.Items.Add(report.TestDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " · " + report.DeviceName + " · " + report.OverallScore.ToString("0", CultureInfo.InvariantCulture) + " · " + report.OverallStatusChinese);
            }
            historyDetail.Text = historyReports.Count == 0 ? "尚无历史报告。\n保存目录：" + store.DirectoryPath : "选择一份历史报告。";
            ShowPanel(historyPanel);
        }

        private void RefreshHistoryDetail()
        {
            ControllerHealthReport report = SelectedHistory();
            if (report == null) { historyDetail.Text = "选择一份历史报告。"; return; }
            StringBuilder text = new StringBuilder();
            text.AppendLine(report.DeviceName + " · " + report.DeviceType);
            text.AppendLine(report.ConnectionType + " · " + report.TestDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            text.AppendLine();
            text.AppendLine(report.OverallStatus + " / " + report.OverallStatusChinese + " · " + report.OverallScore.ToString("0", CultureInfo.InvariantCulture) + "/100");
            text.AppendLine(report.IsComplete ? "完整完成" : "检测不完整");
            text.AppendLine();
            for (int i = 0; i < report.Categories.Count; i++) text.AppendLine(report.Categories[i].DisplayName + "：" + report.Categories[i].Status + (report.Categories[i].Tested ? " · " + report.Categories[i].Score.ToString("0", CultureInfo.InvariantCulture) : string.Empty));
            historyDetail.Text = text.ToString();
        }

        private ControllerHealthReport SelectedHistory()
        {
            int index = historyList.SelectedIndex;
            return index >= 0 && index < historyReports.Count ? historyReports[index] : null;
        }

        private void ExportSelectedHistory(bool markdown)
        {
            ControllerHealthReport report = SelectedHistory();
            if (report == null) { historyDetail.Text = "请先选择历史报告。"; return; }
            ExportReport(report, markdown);
        }

        private void DeleteSelectedHistory()
        {
            ControllerHealthReport report = SelectedHistory();
            if (report == null) { historyDetail.Text = "请先选择历史报告。"; return; }
            if (MessageBox.Show("确定删除所选报告及其 Markdown 副本吗？", "删除健康报告", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                store.Delete(report.ReportId);
                OpenHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show("删除失败：" + ex.Message, "ControllerLab", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportReport(ControllerHealthReport report, bool markdown)
        {
            if (report == null) return;
            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = "ControllerLab-health-" + report.ReportId,
                DefaultExt = markdown ? ".md" : ".json",
                Filter = markdown ? "Markdown 报告 (*.md)|*.md|纯文本 (*.txt)|*.txt" : "JSON 报告 (*.json)|*.json"
            };
            if (dialog.ShowDialog() != true) return;
            try { store.Export(report, dialog.FileName, markdown); }
            catch (Exception ex) { MessageBox.Show("导出失败：" + ex.Message, "ControllerLab", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void ShowPanel(UIElement visible)
        {
            overviewPanel.Visibility = visible == overviewPanel ? Visibility.Visible : Visibility.Collapsed;
            wizardPanel.Visibility = visible == wizardPanel ? Visibility.Visible : Visibility.Collapsed;
            reportPanel.Visibility = visible == reportPanel ? Visibility.Visible : Visibility.Collapsed;
            historyPanel.Visibility = visible == historyPanel ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Grid PageRoot() { return new Grid { Background = Palette.WindowBrush, Visibility = Visibility.Collapsed }; }

        private static ScrollViewer WrapScroll(object content)
        {
            return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(2, 0, 8, 0) };
        }

        private static Border Card(Thickness padding)
        {
            return new Border { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = padding };
        }

        private static Border CapabilityCard(string title, TextBlock content, Color accent, int column)
        {
            Border card = Card(new Thickness(16, 13, 16, 15));
            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, Foreground = new SolidColorBrush(accent), FontSize = 14, FontWeight = FontWeights.SemiBold });
            content.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(content);
            card.Child = stack;
            Grid.SetColumn(card, column);
            return card;
        }

        private static Button MakeButton(string label, bool primary)
        {
            Button button = new Button
            {
                Content = label,
                Height = 36,
                MinWidth = 96,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(14, 4, 14, 4),
                Foreground = primary ? Brushes.White : Palette.TextBrush,
                Background = primary ? Palette.BlueBrush : Palette.SurfaceRaisedBrush,
                BorderBrush = primary ? Palette.BlueBrush : Palette.BorderBrush,
                BorderThickness = new Thickness(1),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            AutomationProperties.SetName(button, label);
            return button;
        }

        private static Brush StepBrush(HealthCheckStepStatus status)
        {
            if (status == HealthCheckStepStatus.Passed) return Palette.GreenBrush;
            if (status == HealthCheckStepStatus.Attention || status == HealthCheckStepStatus.Skipped) return Palette.WarningBrush;
            if (status == HealthCheckStepStatus.Abnormal) return Palette.RedBrush;
            if (status == HealthCheckStepStatus.Testing) return Palette.BlueBrush;
            return Palette.MutedBrush;
        }

        private static Brush RecordBrush(string status)
        {
            if (status == HealthCheckStepStatus.Passed.ToString()) return Palette.GreenBrush;
            if (status == HealthCheckStepStatus.Abnormal.ToString()) return Palette.RedBrush;
            if (status == HealthCheckStepStatus.Attention.ToString() || status == HealthCheckStepStatus.Skipped.ToString()) return Palette.WarningBrush;
            return Palette.MutedBrush;
        }

        private static Brush CategoryBrush(HealthCategoryScore category)
        {
            if (!category.Tested) return Palette.MutedBrush;
            if (category.Score >= 90) return Palette.GreenBrush;
            if (category.Score >= 55) return Palette.WarningBrush;
            return Palette.RedBrush;
        }
    }
}
