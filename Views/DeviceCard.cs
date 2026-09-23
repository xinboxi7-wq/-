// DeviceCard
//
// Extracted verbatim from ControllerLab.cs (lines 337-424) on 2026-09-22
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
    public sealed class DeviceCard : Button
    {
        private readonly Border surface;
        private bool controllerNavigationSelected;
        public event ControllerDeviceSelectedEventHandler DeviceSelected;

        public DeviceCard()
        {
            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Margin = new Thickness(0, 0, 14, 14);
            Cursor = Cursors.Hand;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            surface = new Border
            {
                Width = 300,
                MinHeight = 156,
                Style = LabVisualStyles.MetricCardStyle,
                CornerRadius = LabVisualStyles.CardRadius,
                Background = Palette.Surface2Brush,
                BorderBrush = Palette.BorderSubtleBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 16, 18, 16)
            };
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock name = new TextBlock { Foreground = Palette.TextBrush, FontSize = 17, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            name.SetBinding(TextBlock.TextProperty, new Binding("DisplayName"));
            layout.Children.Add(name);
            StackPanel typeAndStatus = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock type = new TextBlock { Foreground = Palette.BlueBrush, FontSize = 12, FontWeight = FontWeights.SemiBold };
            type.SetBinding(TextBlock.TextProperty, new Binding("ControllerTypeLabel"));
            typeAndStatus.Children.Add(type);
            typeAndStatus.Children.Add(new TextBlock { Text = " · ", Foreground = Palette.MutedBrush, FontSize = 12 });
            TextBlock status = new TextBlock { Foreground = Palette.BlueBrush, FontSize = 12 };
            status.SetBinding(TextBlock.TextProperty, new Binding("ConnectionStatusLabel"));
            typeAndStatus.Children.Add(status);
            Grid.SetRow(typeAndStatus, 2);
            layout.Children.Add(typeAndStatus);
            StackPanel connection = MakeRow("连接方式");
            ((TextBlock)connection.Children[1]).SetBinding(TextBlock.TextProperty, new Binding("ConnectionLabel"));
            Grid.SetRow(connection, 4);
            layout.Children.Add(connection);
            StackPanel battery = MakeRow("电量");
            ((TextBlock)battery.Children[1]).SetBinding(TextBlock.TextProperty, new Binding("BatteryLabel"));
            Grid.SetRow(battery, 6);
            layout.Children.Add(battery);
            surface.Child = layout;
            Content = surface;
            MouseEnter += delegate { surface.BorderBrush = Palette.BlueBrush; surface.Background = Palette.SurfaceRaisedBrush; };
            MouseLeave += delegate { if (!controllerNavigationSelected) { surface.BorderBrush = Palette.BorderSubtleBrush; surface.Background = Palette.Surface2Brush; } };
            Click += delegate
            {
                ControllerDeviceSelectedEventHandler handler = DeviceSelected;
                if (handler != null) handler(this, DataContext as IControllerDevice);
            };
            DataContextChanged += delegate
            {
                IControllerDevice device = DataContext as IControllerDevice;
                if (device != null) AutomationProperties.SetName(this, device.DisplayName + " · " + device.ControllerType + " · " + device.ConnectionType);
            };
        }

        public void SetControllerNavigationSelected(bool selected)
        {
            controllerNavigationSelected = selected;
            surface.BorderBrush = selected ? Palette.BlueBrush : Palette.BorderSubtleBrush;
            surface.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            surface.Background = selected ? Palette.SurfaceRaisedBrush : Palette.Surface2Brush;
        }

        private static StackPanel MakeRow(string label)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = label + "：", Foreground = Palette.MutedBrush, FontSize = 11 });
            row.Children.Add(new TextBlock { Foreground = Palette.TextBrush, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            return row;
        }
    }
}
