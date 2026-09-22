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

    }
}
