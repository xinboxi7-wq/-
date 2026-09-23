// DeviceHomeView
//
// Extracted verbatim from ControllerLab.cs (lines 426-493) on 2026-09-22
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
    public sealed class DeviceHomeView : Grid, IDisposable
    {
        private readonly ObservableCollection<IControllerDevice> devices;
        private readonly WrapPanel cards;
        private bool disposed;
        public event ControllerDeviceSelectedEventHandler DeviceSelected;

        public DeviceHomeView(ObservableCollection<IControllerDevice> devices)
        {
            this.devices = devices;
            Margin = new Thickness(32, 28, 32, 0);
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            StackPanel header = new StackPanel();
            header.Children.Add(LabVisualStyles.CreatePageTitle("设备首页"));
            TextBlock subtitle = LabVisualStyles.CreateSecondaryText("已连接的 Xbox 与 DualSense 手柄会自动出现在这里。");
            subtitle.FontSize = 14;
            subtitle.Margin = new Thickness(0, 7, 0, 0);
            header.Children.Add(subtitle);
            Children.Add(header);
            cards = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            ScrollViewer scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = cards };
            Grid.SetRow(scroller, 2);
            Children.Add(scroller);
            if (devices != null) devices.CollectionChanged += OnDevicesChanged;
            RebuildCards();
        }

        private void OnDevicesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildCards();
        }

        private void RebuildCards()
        {
            cards.Children.Clear();
            if (devices == null || devices.Count == 0)
            {
                cards.Children.Add(new TextBlock
                {
                    Text = "未检测到兼容手柄。连接 Xbox 或 DualSense 后无需重启程序，列表会自动刷新。",
                    Foreground = Palette.MutedBrush,
                    FontSize = 14,
                    Margin = new Thickness(2, 8, 0, 0)
                });
                return;
            }
            for (int i = 0; i < devices.Count; i++)
            {
                DeviceCard card = new DeviceCard { DataContext = devices[i] };
                card.DeviceSelected += delegate(object sender, IControllerDevice device)
                {
                    ControllerDeviceSelectedEventHandler handler = DeviceSelected;
                    if (handler != null) handler(this, device);
                };
                cards.Children.Add(card);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (devices != null) devices.CollectionChanged -= OnDevicesChanged;
            cards.Children.Clear();
        }
    }
}
