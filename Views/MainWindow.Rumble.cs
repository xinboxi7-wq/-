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

    }
}
