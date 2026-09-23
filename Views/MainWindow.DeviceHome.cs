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
        private Grid BuildLeftColumn()
        {
            Grid left = new Grid();
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(172) });

            deviceCard = Card(BuildDeviceCard());
            deviceCard.SizeChanged += delegate { UpdateDeviceCardResponsiveLayout(); };
            Grid.SetRow(deviceCard, 0);
            left.Children.Add(deviceCard);

            Border controllerCard = LabVisualStyles.CreateSectionCard(new Grid
            {
                ClipToBounds = true,
                Children = { controllerVisualHost }
            });
            controllerCard.Padding = new Thickness(6, 4, 6, 4);
            controllerCard.ClipToBounds = true;
            Grid.SetRow(controllerCard, 2);
            left.Children.Add(controllerCard);

            Grid triggerStrip = new Grid { ClipToBounds = true };
            triggerStrip.ColumnDefinitions.Add(new ColumnDefinition());
            triggerStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            triggerStrip.ColumnDefinitions.Add(new ColumnDefinition());
            Border leftTrigger = LabVisualStyles.CreateSectionCard(BuildTriggerCard("LT", leftTriggerChart, Palette.GreenBrush, true));
            Border rightTrigger = LabVisualStyles.CreateSectionCard(BuildTriggerCard("RT", rightTriggerChart, Palette.BlueBrush, false));
            triggerStrip.Children.Add(leftTrigger);
            Grid.SetColumn(rightTrigger, 2);
            triggerStrip.Children.Add(rightTrigger);
            Grid.SetRow(triggerStrip, 4);
            left.Children.Add(triggerStrip);
            return left;
        }

        private UIElement BuildDeviceCard()
        {
            Grid grid = new Grid { Margin = new Thickness(22, 14, 22, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel device = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Border logo = new Border
            {
                Width = 43,
                Height = 43,
                CornerRadius = new CornerRadius(22),
                Background = new SolidColorBrush(Color.FromRgb(239, 243, 246)),
                Child = deviceLogoText = new TextBlock { Text = "X", Foreground = new SolidColorBrush(Color.FromRgb(28, 39, 48)), FontSize = 26, FontWeight = FontWeights.Light, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            device.Children.Add(logo);
            StackPanel name = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            deviceNameText = new TextBlock { Text = "手柄自动识别", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Palette.TextBrush };
            name.Children.Add(deviceNameText);
            StackPanel state = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            connectionDot = new Ellipse { Width = 10, Height = 10, Fill = Palette.BlueBrush, VerticalAlignment = VerticalAlignment.Center };
            connectionText = new TextBlock { Text = demoMode ? "动态演示" : "正在扫描…", Foreground = Palette.BlueBrush, FontSize = 13, Margin = new Thickness(8, -1, 0, 0) };
            AutomationProperties.SetLiveSetting(connectionText, AutomationLiveSetting.Polite);
            refreshRateText = new TextBlock { Text = "显示计算中", Foreground = Palette.BlueBrush, FontSize = 13 };
            refreshRateBadge = new Border { Background = new SolidColorBrush(Color.FromRgb(29, 49, 64)), CornerRadius = new CornerRadius(14), Margin = new Thickness(20, -4, 0, -4), Padding = new Thickness(12, 4, 12, 4), Child = refreshRateText };
            state.Children.Add(connectionDot);
            state.Children.Add(connectionText);
            connectionMethodBadge = BuildCompactConnectionMethodBadge();
            state.Children.Add(connectionMethodBadge);
            state.Children.Add(refreshRateBadge);
            controllerFamilySelectorButton = MakeButton(DeviceSelectionLabel(), false);
            controllerFamilySelectorButton.Width = 156;
            controllerFamilySelectorButton.Height = 26;
            controllerFamilySelectorButton.FontSize = 11;
            controllerFamilySelectorButton.Padding = new Thickness(10, 2, 10, 3);
            controllerFamilySelectorButton.VerticalContentAlignment = VerticalAlignment.Center;
            controllerFamilySelectorButton.Margin = new Thickness(16, -3, 0, -3);
            controllerFamilySelectorButton.ToolTip = "选择当前在线的 Xbox 或索尼 DS 手柄；断开后自动切换到其他在线设备";
            controllerFamilySelectorButton.ContextMenu = CreateDarkContextMenu(300);
            controllerFamilySelectorButton.Click += delegate
            {
                RefreshDeviceSelectorMenu();
                OpenContextMenu(controllerFamilySelectorButton);
            };
            state.Children.Add(controllerFamilySelectorButton);
            demoModeButton = MakeButton(demoMode ? "退出演示" : "动态演示", demoMode);
            demoModeButton.Width = 88;
            demoModeButton.Height = 26;
            demoModeButton.FontSize = 11;
            demoModeButton.Padding = new Thickness(10, 2, 10, 3);
            demoModeButton.VerticalContentAlignment = VerticalAlignment.Center;
            demoModeButton.Margin = new Thickness(8, -3, 0, -3);
            demoModeButton.ToolTip = "在实时手柄监测与自动动态演示之间切换（F9）";
            AutomationProperties.SetName(demoModeButton, demoMode ? "退出动态演示" : "启动动态演示");
            demoModeButton.Click += delegate { ToggleDemoMode(); };
            state.Children.Add(demoModeButton);
            name.Children.Add(state);
            device.Children.Add(name);
            grid.Children.Add(device);

            // Start collapsed so the header cannot force the visualizer's left star
            // column wider than the available window before responsive layout runs.
            StackPanel metadata = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed };
            deviceMetadataPanel = metadata;
            deviceMetaText = AddMetadata(metadata, "驱动", input.LibraryName);
            samplingRateText = AddMetadata(metadata, "采样", demoMode ? "演示" : "计算中");
            Grid.SetColumn(metadata, 1);
            grid.Children.Add(metadata);
            return grid;
        }

        private Border BuildCompactConnectionMethodBadge()
        {
            StackPanel value = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "点击选择自动识别或手动显示连接方式" };
            value.Children.Add(new TextBlock { Text = "连接方式", Foreground = Palette.MutedBrush, FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
            connectionMethodDot = new Ellipse { Width = 7, Height = 7, Fill = Palette.MutedBrush, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            value.Children.Add(connectionMethodDot);
            connectionMethodText = new TextBlock { Text = "检测中", Foreground = Palette.TextBrush, FontSize = 11, Margin = new Thickness(5, -1, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 94, ToolTip = "根据当前 XInput 与 Windows 设备路径自动识别" };
            value.Children.Add(connectionMethodText);
            connectionMethodMenu = CreateDarkContextMenu(186);
            AddConnectionMethodMenuItem("自动识别（推荐）", "自动");
            AddConnectionMethodMenuItem("手动显示：有线", "有线");
            AddConnectionMethodMenuItem("手动显示：蓝牙", "蓝牙");
            AddConnectionMethodMenuItem("手动显示：USB 2.4G", "USB 2.4G");
            connectionMethodMenu.Items.Add(MakeDarkMenuSeparator());
            AddConnectionRouteMenuItem("将当前 USB 状态设为有线", "有线");
            AddConnectionRouteMenuItem("将当前 USB 状态设为 2.4G 接收器", "USB 2.4G");
            value.ContextMenu = connectionMethodMenu;
            value.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                OpenContextMenu(value);
                e.Handled = true;
            };
            Border badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 42, 54)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 78, 94)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(12, -3, 0, -3),
                Padding = new Thickness(8, 3, 9, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = value,
                ToolTip = "手柄连接方式；点击可切换自动识别或手动显示"
            };
            AutomationProperties.SetName(badge, "手柄连接方式");
            return badge;
        }

        private TextBlock AddMetadata(StackPanel parent, string label, string value)
        {
            Border divider = new Border { Width = 1, Height = 45, Background = Palette.BorderBrush, Margin = new Thickness(12, 0, 12, 0) };
            parent.Children.Add(divider);
            StackPanel block = new StackPanel { MinWidth = 78, VerticalAlignment = VerticalAlignment.Center };
            block.Children.Add(new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = 12 });
            TextBlock text = new TextBlock { Text = value, Foreground = Palette.TextBrush, FontSize = 13, Margin = new Thickness(0, 8, 0, 0) };
            block.Children.Add(text);
            parent.Children.Add(block);
            return text;
        }

        private void AddConnectionMethodMetadata(StackPanel parent)
        {
            parent.Children.Add(new Border { Width = 1, Height = 45, Background = Palette.BorderBrush, Margin = new Thickness(12, 0, 12, 0) });
            StackPanel block = new StackPanel { MinWidth = 152, VerticalAlignment = VerticalAlignment.Center };
            block.Children.Add(new TextBlock { Text = "手柄连接方式", Foreground = Palette.MutedBrush, FontSize = 12 });
            StackPanel value = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "点击选择自动识别或手动显示连接方式" };
            connectionMethodDot = new Ellipse { Width = 9, Height = 9, Fill = Palette.MutedBrush, VerticalAlignment = VerticalAlignment.Center };
            value.Children.Add(connectionMethodDot);
            connectionMethodText = new TextBlock { Text = "检测中", Foreground = Palette.TextBrush, FontSize = 13, Margin = new Thickness(8, -1, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "根据当前 XInput 与 Windows 设备路径自动识别" };
            value.Children.Add(connectionMethodText);
            connectionMethodMenu = CreateDarkContextMenu(186);
            AddConnectionMethodMenuItem("自动识别（推荐）", "自动");
            AddConnectionMethodMenuItem("手动显示：有线", "有线");
            AddConnectionMethodMenuItem("手动显示：蓝牙", "蓝牙");
            AddConnectionMethodMenuItem("手动显示：USB 2.4G", "USB 2.4G");
            connectionMethodMenu.Items.Add(MakeDarkMenuSeparator());
            AddConnectionRouteMenuItem("将当前 USB 状态设为有线", "有线");
            AddConnectionRouteMenuItem("将当前 USB 状态设为 2.4G 接收器", "USB 2.4G");
            value.ContextMenu = connectionMethodMenu;
            value.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                OpenContextMenu(value);
                e.Handled = true;
            };
            block.Children.Add(value);
            parent.Children.Add(block);
        }

        private void AddConnectionMethodMenuItem(string label, string mode)
        {
            MenuItem item = MakeDarkMenuItem(label);
            item.IsCheckable = true;
            item.IsChecked = connectionMethodOverride == mode;
            item.Click += delegate { SelectConnectionMethodOverride(mode); };
            connectionMethodMenu.Items.Add(item);
        }

        private void AddConnectionRouteMenuItem(string label, string mode)
        {
            MenuItem item = MakeDarkMenuItem(label);
            item.Click += delegate { MarkCurrentUsbRoute(mode); };
            connectionMethodMenu.Items.Add(item);
        }

        private static string NormalizeConnectionMethodOverride(string value)
        {
            return value == "有线" || value == "蓝牙" || value == "USB 2.4G" ? value : "自动";
        }

        private static ControllerFamily NormalizeControllerFamily(string value)
        {
            if (string.Equals(value, "Xbox", StringComparison.OrdinalIgnoreCase)) return ControllerFamily.Xbox;
            if (string.Equals(value, "PlayStation", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "索尼 DS", StringComparison.OrdinalIgnoreCase)) return ControllerFamily.PlayStation;
            return ControllerFamily.Auto;
        }

        private string ControllerFamilySelectionLabel()
        {
            if (selectedControllerFamily == ControllerFamily.Xbox) return "手柄：Xbox";
            if (selectedControllerFamily == ControllerFamily.PlayStation) return "手柄：索尼 DS";
            return "手柄：自动";
        }

        private string DeviceSelectionLabel()
        {
            ControllerState[] devices = latestControllerStates ?? new ControllerState[0];
            for (int i = 0; i < devices.Length; i++)
            {
                if (string.Equals(devices[i].DeviceId, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return "设备：" + ShortDeviceName(devices[i]);
                }
            }
            if (demoMode && multiDemoMode) return devices.Length == 0 ? "设备：多设备演示" : "设备：自动 · " + devices.Length.ToString(CultureInfo.InvariantCulture) + " 台";
            if (demoMode) return "设备：动态演示";
            return devices.Length == 0 ? "设备：自动" : "设备：自动 · " + devices.Length.ToString(CultureInfo.InvariantCulture) + " 台";
        }

        private static string ShortDeviceName(ControllerState state)
        {
            if (state == null) return "自动";
            string prefix = state.ControllerType == ControllerType.Xbox ? "Xbox" : "DualSense";
            if (state.PlayerIndex >= 0 && state.ControllerType == ControllerType.Xbox) return prefix + " " + (state.PlayerIndex + 1).ToString(CultureInfo.InvariantCulture);
            return prefix;
        }

        private void RefreshDeviceSelectorMenu()
        {
            if (controllerFamilySelectorButton == null) return;
            ContextMenu menu = controllerFamilySelectorButton.ContextMenu;
            if (menu == null) return;
            menu.Items.Clear();
            ControllerState[] devices = latestControllerStates ?? new ControllerState[0];
            MenuItem auto = MakeDarkMenuItem("自动选择在线设备");
            auto.IsCheckable = true;
            auto.IsChecked = string.IsNullOrEmpty(selectedDeviceId);
            auto.Click += delegate { SelectDevice(null); };
            menu.Items.Add(auto);
            if (devices.Length > 0) menu.Items.Add(MakeDarkMenuSeparator());
            for (int i = 0; i < devices.Length; i++)
            {
                ControllerState candidate = devices[i];
                MenuItem item = MakeDarkMenuItem(ShortDeviceName(candidate) + " · " + candidate.ConnectionTypeLabel + " · " + candidate.InputBackend);
                item.IsCheckable = true;
                item.IsChecked = string.Equals(selectedDeviceId, candidate.DeviceId, StringComparison.OrdinalIgnoreCase);
                string id = candidate.DeviceId;
                item.Click += delegate { SelectDevice(id); };
                menu.Items.Add(item);
            }
            if (devices.Length == 0)
            {
                MenuItem empty = MakeDarkMenuItem("未发现在线手柄");
                empty.IsEnabled = false;
                menu.Items.Add(empty);
            }
        }

        private void SelectDevice(string deviceId)
        {
            if (rumbleStudioPage != null) rumbleStudioPage.ResetForDeviceChange();
            if (healthCheckView != null) healthCheckView.ResetForDeviceChange();
            rumbleController.Stop("设备已切换，震动已停止");
            selectedDeviceId = deviceId;
            if (string.IsNullOrEmpty(deviceId)) productSettings.AutoConnect = true;
            ClearTriggerHistory();
            diagnostics.Reset();
            inputTestEngine.Reset(currentControllerState);
            stickTriggerTestEngine.Reset(currentControllerState);
            stickDriftTestEngine.Reset(null);
            if (joystickTestPage != null) joystickTestPage.ResetForDeviceChange("设备已切换，摇杆检测已取消");
            ClearStickTestVisualState();
            if (motionPoseView != null) motionPoseView.SetState(null);
            nextMotionUiRefresh = DateTime.MinValue;
            renderedInputTestSignature = null;
            if (controllerFamilySelectorButton != null) controllerFamilySelectorButton.Content = DeviceSelectionLabel();
            if (footerStatus != null) footerStatus.Text = string.IsNullOrEmpty(deviceId) ? "已启用自动设备选择。" : "已切换到 " + DeviceSelectionLabel() + "。";
            if (!demoMode) SaveSettings();
        }

        private void AddControllerFamilyMenuItem(ContextMenu menu, string label, ControllerFamily family)
        {
            MenuItem item = MakeDarkMenuItem(label);
            item.IsCheckable = true;
            item.IsChecked = selectedControllerFamily == family;
            item.Click += delegate { SelectControllerFamily(family); };
            menu.Items.Add(item);
        }

        private void SelectControllerFamily(ControllerFamily family)
        {
            if (rumbleStudioPage != null) rumbleStudioPage.ResetForDeviceChange();
            if (healthCheckView != null) healthCheckView.ResetForDeviceChange();
            rumbleController.Stop("手柄类型已切换，震动已停止");
            selectedControllerFamily = family;
            ClearTriggerHistory();
            stickDriftTestEngine.Reset(null);
            if (joystickTestPage != null) joystickTestPage.ResetForDeviceChange("手柄类型已切换，摇杆检测已取消");
            ClearStickTestVisualState();
            if (demoMode)
            {
                if (family == ControllerFamily.PlayStation) sonyDemoMode = true;
                else if (family == ControllerFamily.Xbox) sonyDemoMode = false;
            }
            if (controllerFamilySelectorButton != null) controllerFamilySelectorButton.Content = ControllerFamilySelectionLabel();
            ContextMenu menu = controllerFamilySelectorButton == null ? null : controllerFamilySelectorButton.ContextMenu;
            if (menu != null)
            {
                for (int i = 0; i < menu.Items.Count; i++)
                {
                    MenuItem item = menu.Items[i] as MenuItem;
                    if (item == null) continue;
                    item.IsChecked = (family == ControllerFamily.Auto && string.Equals(item.Header as string, "手柄：自动", StringComparison.Ordinal)) ||
                        (family == ControllerFamily.Xbox && string.Equals(item.Header as string, "手柄：Xbox", StringComparison.Ordinal)) ||
                        (family == ControllerFamily.PlayStation && string.Equals(item.Header as string, "手柄：索尼 DS", StringComparison.Ordinal));
                }
            }
            diagnostics.Reset();
            if (demoMode) diagnostics.UseDemoBaseline();
            UpdateFamilyPresentation(currentState);
            if (!demoMode) SaveSettings();
            if (footerStatus != null)
            {
                footerStatus.Text = demoMode
                    ? "动态演示模式：已切换到" + (CurrentDemoFamily() == ControllerFamily.PlayStation ? "索尼 DS" : "Xbox") + "演示。"
                    : family == ControllerFamily.Auto
                        ? "已启用自动识别：Xbox 使用 XInput，索尼 DS 使用原生 HID。"
                        : family == ControllerFamily.Xbox
                            ? "已固定为 Xbox 监测；可继续选择玩家槽位。"
                            : "已固定为索尼 DS 监测；操作 DualSense 或 DualShock 4 任意按键开始读取。";
            }
        }

        private ControllerFamily CurrentDemoFamily()
        {
            if (selectedControllerFamily == ControllerFamily.PlayStation) return ControllerFamily.PlayStation;
            if (selectedControllerFamily == ControllerFamily.Xbox) return ControllerFamily.Xbox;
            if (sonyDemoMode) return ControllerFamily.PlayStation;
            return renderedControllerFamily == ControllerFamily.PlayStation ? ControllerFamily.PlayStation : ControllerFamily.Xbox;
        }

        private InputSnapshot CreateCurrentDemoSnapshot()
        {
            return CurrentDemoFamily() == ControllerFamily.PlayStation ? InputSnapshot.CreateSonyDemo() : InputSnapshot.CreateDemo();
        }

        private ControllerState[] CreateMultiDemoStates()
        {
            return new ControllerState[]
            {
                ControllerStateAdapter.FromSnapshot(InputSnapshot.CreateDemo()),
                ControllerStateAdapter.FromSnapshot(InputSnapshot.CreateSonyDemo())
            };
        }

        private void ToggleDemoMode()
        {
            SetDemoMode(!demoMode);
        }

        private void SetDemoMode(bool enabled)
        {
            if (demoMode == enabled) return;
            if (rumbleStudioPage != null) rumbleStudioPage.ResetForDeviceChange();
            if (healthCheckView != null) healthCheckView.ResetForDeviceChange();
            if (enabled)
            {
                SaveSettings();
                sonyDemoMode = selectedControllerFamily == ControllerFamily.PlayStation ||
                    (selectedControllerFamily == ControllerFamily.Auto && renderedControllerFamily == ControllerFamily.PlayStation);
                StopSampling();
                demoMode = true;
                diagnostics.Reset();
                diagnostics.UseDemoBaseline();
            }
            else
            {
                demoMode = false;
                sonyDemoMode = false;
                diagnostics.Reset();
                latestInput = new InputSnapshot();
                StartSampling();
            }

            if (calibrating)
            {
                calibrating = false;
                calibrationStatusVisible = false;
                calibrationProgress.Visibility = Visibility.Collapsed;
            }
            lastConnected = false;
            refreshTicks = 0;
            Interlocked.Exchange(ref samplingTicks, 0);
            actualSamplingHz = 0;
            rateWindowStarted = DateTime.UtcNow;

            if (demoModeButton != null)
            {
                demoModeButton.Content = demoMode ? "退出演示" : "动态演示";
                demoModeButton.ToolTip = demoMode
                    ? "退出自动动画并恢复实时手柄监测（F9）"
                    : "在实时手柄监测与自动动态演示之间切换（F9）";
                AutomationProperties.SetName(demoModeButton, demoMode ? "退出动态演示" : "启动动态演示");
                SetButtonPrimary(demoModeButton, demoMode);
            }
            if (controllerSelectorButton != null)
            {
                controllerSelectorButton.IsEnabled = !demoMode;
                controllerSelectorButton.Content = demoMode ? "设备：演示" : ControllerSelectionLabel();
            }
            if (reducedMotionCheck != null) reducedMotionCheck.IsEnabled = !demoMode;
            if (calibrateButton != null)
            {
                calibrateButton.IsEnabled = !demoMode;
                calibrateButton.Content = demoMode ? "演示中" : calibrationSuggestionPending ? "应用死区" : "中心校准";
            }
            if (samplingRateText != null) samplingRateText.Text = demoMode ? "演示" : "计算中";
            if (footerStatus != null)
            {
                footerStatus.Text = demoMode
                    ? "动态演示模式：摇杆、扳机、方向键和按键会自动变化；点击“退出演示”恢复实时监测。"
                    : "已恢复实时监测：连接或操作手柄后会立即显示输入。";
            }
        }

        private void SelectConnectionMethodOverride(string mode)
        {
            connectionMethodOverride = NormalizeConnectionMethodOverride(mode);
            if (connectionMethodMenu != null)
            {
                for (int i = 0; i < connectionMethodMenu.Items.Count; i++)
                {
                    MenuItem item = connectionMethodMenu.Items[i] as MenuItem;
                    if (item == null) continue;
                    string header = item.Header as string;
                    item.IsChecked = (connectionMethodOverride == "自动" && header == "自动识别（推荐）") ||
                        (connectionMethodOverride == "有线" && header == "手动显示：有线") ||
                        (connectionMethodOverride == "蓝牙" && header == "手动显示：蓝牙") ||
                        (connectionMethodOverride == "USB 2.4G" && header == "手动显示：USB 2.4G");
                }
            }
            UpdateConnectionMethod(currentState);
            if (!demoMode) SaveSettings();
            if (footerStatus != null)
            {
                footerStatus.Text = connectionMethodOverride == "自动"
                    ? "连接方式已恢复自动识别；切换模式后请按任意手柄按键一次以刷新路径。"
                    : "连接方式已设为“" + connectionMethodOverride + "”（仅覆盖显示，不影响输入）。";
            }
        }

        private void MarkCurrentUsbRoute(string mode)
        {
            if (!input.MarkCurrentUsbRoute(mode))
            {
                if (footerStatus != null) footerStatus.Text = "当前没有可标记的 USB 手柄路径；请先连接手柄并操作任意按键。";
                return;
            }
            connectionMethodOverride = "自动";
            UpdateConnectionMethod(currentState);
            if (!demoMode) SaveSettings();
            if (footerStatus != null) footerStatus.Text = "当前 USB 状态已设为“" + mode + "”。此手柄的有线与接收器可复用同一 Windows 路径，切换后请在此处同步一次状态。";
        }

    }
}
