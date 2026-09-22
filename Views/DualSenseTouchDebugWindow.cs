// DualSenseTouchDebugWindow
//
// Extracted verbatim from ControllerLab.cs (lines 5547-5630) on 2026-09-22
// as part of the ControllerLab structural split (batch 4).
// No logic was changed.
// See docs/redesign/ControllerLab-结构拆分施工图.md
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
    public sealed class DualSenseTouchDebugWindow : Window
    {
        private readonly Func<InputSnapshot> readSnapshot;
        private readonly Func<DualSenseTouchPoint, Point> mapPoint;
        private readonly Action<bool> setRawLogging;
        private readonly Func<bool> getRawLogging;
        private readonly TextBlock details;
        private readonly DispatcherTimer timer;
        private readonly CheckBox rawLogging;

        public DualSenseTouchDebugWindow(Func<InputSnapshot> readSnapshot, Func<DualSenseTouchPoint, Point> mapPoint, Action<bool> setRawLogging, Func<bool> getRawLogging)
        {
            this.readSnapshot = readSnapshot;
            this.mapPoint = mapPoint;
            this.setRawLogging = setRawLogging;
            this.getRawLogging = getRawLogging;
            Title = "DS5 触摸调试";
            Width = 590;
            Height = 500;
            MinWidth = 520;
            MinHeight = 430;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Palette.WindowBrush;
            Foreground = Palette.TextBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            Grid root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            root.RowDefinitions.Add(new RowDefinition());
            TextBlock title = new TextBlock { Text = "DualSense 原生 HID 触点", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Palette.TextBrush };
            root.Children.Add(title);
            rawLogging = new CheckBox { Content = "启用原始触摸数据日志（Debug 输出）", IsChecked = getRawLogging != null && getRawLogging(), Foreground = Palette.MutedBrush, FontSize = 12, Margin = new Thickness(0, 28, 0, 0) };
            rawLogging.Checked += delegate { if (setRawLogging != null) setRawLogging(true); };
            rawLogging.Unchecked += delegate { if (setRawLogging != null) setRawLogging(false); };
            root.Children.Add(rawLogging);
            Border card = new Border { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(14) };
            details = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = Palette.TextBrush, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
            card.Child = details;
            Grid.SetRow(card, 2);
            root.Children.Add(card);
            Content = root;

            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += delegate { Refresh(); };
            Loaded += delegate { Refresh(); timer.Start(); };
            Closed += delegate { timer.Stop(); };
        }

        private void Refresh()
        {
            InputSnapshot state = readSnapshot == null ? null : readSnapshot();
            if (state == null || state.Family != ControllerFamily.PlayStation)
            {
                details.Text = "等待 DualSense 原生 HID 输入。\n\n未检测到 DS5 时，程序不会使用鼠标、XInput 或动态演示伪造触点。";
                return;
            }
            DualSenseTouchDebugInfo info = state.TouchDebug;
            StringBuilder text = new StringBuilder();
            text.AppendLine("HID 连接方式: " + (info == null ? state.ConnectionMethod : info.ConnectionMethod));
            text.AppendLine("设备身份: " + (info == null ? "-" : info.DeviceIdentity));
            text.AppendLine("报告: " + (info == null ? "等待原始 HID 报文" : string.Format(CultureInfo.InvariantCulture, "0x{0:X2}, {1} bytes, {2}", info.ReportId, info.ReportLength, info.Layout)));
            text.AppendLine("触点偏移: " + (info == null ? "-" : info.TouchOffset.ToString(CultureInfo.InvariantCulture)) + "    蓝牙 CRC: " + (info == null ? "-" : (info.CrcValidated ? "通过/不适用" : "失败")));
            text.AppendLine("触摸坐标: " + (state.TouchCoordinatesAvailable ? "可用（真实 HID）" : (info == null ? "不可用" : info.AvailabilityMessage)));
            text.AppendLine("更新率: " + (info == null ? "0" : info.UpdatesPerSecond.ToString("0.0", CultureInfo.InvariantCulture)) + " Hz");
            text.AppendLine("原始触点字节: " + (info == null || info.RawTouchBytes == null ? "-" : BitConverter.ToString(info.RawTouchBytes)));
            AppendPoint(text, "触点 1", state.TouchPoint1);
            AppendPoint(text, "触点 2", state.TouchPoint2);
            details.Text = text.ToString();
        }

        private void AppendPoint(StringBuilder text, string label, DualSenseTouchPoint point)
        {
            if (point == null)
            {
                text.AppendLine(label + ": 无");
                return;
            }
            Point mapped = mapPoint == null ? new Point(double.NaN, double.NaN) : mapPoint(point);
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: active={1}, id={2}, raw=({3},{4}), normalized=({5:0.000},{6:0.000}), stage=({7:0.0},{8:0.0})", label, point.IsActive, point.Id, point.RawX, point.RawY, point.X, point.Y, mapped.X, mapped.Y));
        }
    }
}
