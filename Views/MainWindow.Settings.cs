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
