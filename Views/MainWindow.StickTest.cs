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
    public sealed partial class MainWindow
    {
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

    }
}
