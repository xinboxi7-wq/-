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
                dualSenseAdvancedManager.Synchronize(latestControllerStates, motionManager);
            }
            // This is the only point where the WPF-bound device collection changes.
            // Sampling continues on its background thread and never touches the UI.
            deviceManager.Synchronize(latestControllerStates);
            ControllerState selected = ResolveActiveControllerState();
            currentControllerState = selected;
            rumbleController.Synchronize(selected);
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
            if (state.Connected) UpdateVisuals(state);
            diagnostics.Update(state, demoMode ? 220.0 : actualSamplingHz, leftDeadzone.Value, rightDeadzone.Value);
            UpdateInputTestPage(selected);
            UpdateStickDriftTestPage(selected);
            if (joystickTestPage != null && (currentPage == 3 || joystickTestViewModel.IsTestActive))
                joystickTestPage.Update(selected, demoMode ? 220.0 : actualSamplingHz, rumbleController.IsRunning, rumbleController.LastStoppedUtc);
            UpdateMotionPage(selected);
            UpdateRumblePage(selected);
            if (healthCheckView != null && (currentPage == 6 || healthCheckViewModel.IsRunning))
                healthCheckView.Update(selected);
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
                if (footerStatus != null && devices.Length > 0) footerStatus.Text = productSettings.AutoConnect ? "选中的手柄已断开，已自动切换到其他在线设备。" : "选中的手柄已断开，请在设备选择页重新选择。";
            }
            if (!demoMode && !productSettings.AutoConnect)
            {
                ControllerState waitingForSelection = ControllerStateAdapter.CreateDisconnected();
                waitingForSelection.ControllerType = selectedControllerFamily == ControllerFamily.PlayStation ? ControllerType.DualSense : ControllerType.Xbox;
                waitingForSelection.DeviceName = "请选择设备";
                waitingForSelection.InputBackend = waitingForSelection.ControllerType == ControllerType.DualSense ? "Sony Native HID" : input.LibraryName;
                return waitingForSelection;
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
                    dualSenseAdvancedManager.Synchronize(states, motionManager);
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
                if (!lastConnected)
                {
                    capabilitiesReadyUtc = DateTime.UtcNow.AddMilliseconds(650);
                    if (noticeBanner != null) noticeBanner.Hide();
                    LabLogger.Info("Device", "Controller connected; capability detection started.");
                }
                connectionDot.Fill = Palette.BlueBrush;
                connectionText.Foreground = Palette.BlueBrush;
                string touchStatus = state.Family == ControllerFamily.PlayStation
                    ? (state.TouchCoordinatesAvailable ? " · 触摸坐标可用" : " · 触摸坐标不可用（仅按压）")
                    : string.Empty;
                bool detectingCapabilities = !demoMode && DateTime.UtcNow < capabilitiesReadyUtc;
                string status = demoMode
                    ? "动态演示" + (sonyDemoMode ? " · 触摸坐标不可用（仅按压）" : string.Empty)
                    : detectingCapabilities
                        ? "能力检测中 · " + (state.Family == ControllerFamily.PlayStation ? "原生 HID" : "XInput")
                    : state.Family == ControllerFamily.PlayStation
                        ? "已就绪 · 原生 HID" + touchStatus
                        : string.Format(CultureInfo.InvariantCulture, "已就绪 · 玩家 {0}", state.Index + 1);
                SetTextIfChanged(connectionText, status);
                UpdateConnectionMethod(state);
                SetTextIfChanged(deviceMetaText, string.IsNullOrEmpty(state.InputBackend) ? input.LibraryName : state.InputBackend);
            }
            else
            {
                connectionDot.Fill = Palette.RedBrush;
                connectionText.Foreground = Palette.RedBrush;
                SetTextIfChanged(connectionText, selectedControllerIndex < 0
                    ? (DateTime.UtcNow - applicationStartedUtc).TotalSeconds < 2.0 ? "正在识别手柄" : lastConnected ? "已断开" : "未连接"
                    : string.Format(CultureInfo.InvariantCulture, "玩家 {0} 未连接", selectedControllerIndex + 1));
                UpdateConnectionMethod(state);
                SetTextIfChanged(deviceMetaText, renderedControllerFamily == ControllerFamily.PlayStation ? "Sony 原生 HID" : input.LibraryName);
                if (lastConnected)
                {
                    rumbleController.Stop("设备已断开，震动输出已归零");
                    if (stickDriftTestEngine.IsActive) stickDriftTestEngine.Cancel("设备已断开，检测已取消");
                    if (joystickTestPage != null) joystickTestPage.Cancel("设备已断开，摇杆检测已取消");
                    if (dualSenseAdvancedPage != null) dualSenseAdvancedPage.CancelForPageLeave();
                    footerStatus.Text = "设备已断开。已停止震动和当前检测，最近一次报告仍保留。";
                    if (noticeBanner != null) noticeBanner.Show(ProductNoticeKind.Warning, "设备已断开", "震动与当前检测已安全停止；重新连接后可从当前项目重新开始。", "设备选择", delegate { ShowPage(0); });
                    LabLogger.Warning("Device", "Active controller disconnected; output and tests stopped.");
                }
                UpdateRealtimeStickCard(0, false, leftStickStatusText, leftStickAdviceText);
                UpdateRealtimeStickCard(0, false, rightStickStatusText, rightStickAdviceText);
                if (triggerStatusText != null) { triggerStatusText.Text = "未连接"; triggerStatusText.Foreground = Palette.RedBrush; }
            }
            if (controllerVisualHost != null) controllerVisualHost.Opacity = state.Connected ? 1.0 : 0.38;
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
            string coordinateFormat = "0." + new string('0', Math.Max(1, Math.Min(3, productSettings.DecimalPlaces)));
            SetTextIfChanged(leftDriftY, "X " + state.LeftNormalizedX.ToString(coordinateFormat, CultureInfo.InvariantCulture) + " · Y " + state.LeftNormalizedY.ToString(coordinateFormat, CultureInfo.InvariantCulture));
            SetTextIfChanged(rightDriftX, string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", rightMagnitude * 100.0));
            SetTextIfChanged(rightDriftY, "X " + state.RightNormalizedX.ToString(coordinateFormat, CultureInfo.InvariantCulture) + " · Y " + state.RightNormalizedY.ToString(coordinateFormat, CultureInfo.InvariantCulture));
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
            if (motionPageButton != null)
            {
                bool showDualSense = family == ControllerFamily.PlayStation;
                motionPageButton.Visibility = showDualSense ? Visibility.Visible : Visibility.Collapsed;
                if (!showDualSense && state.Connected && currentPage == 4) ShowPage(1);
            }
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
            if (joystickTestPage != null) joystickTestPage.ResetForDeviceChange("输入模式已切换，摇杆检测已取消");
            ClearStickTestVisualState();
            ClearTriggerHistory();
            if (!demoMode) SaveSettings();
            UpdateDiagnostics(currentState);
            if (footerStatus != null) footerStatus.Text = "中心偏移、参考死区、设备选择与动态效果已恢复默认。";
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


    }
}
