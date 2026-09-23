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

    }
}
