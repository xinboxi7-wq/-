// DeadzoneSlider
//
// Extracted verbatim from ControllerLab.cs (lines 10189-10254) on 2026-09-22
// as part of the ControllerLab structural split (batch 2).
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
    public sealed class DeadzoneSlider : FrameworkElement
    {
        private readonly Color accent;
        private double value;
        private bool dragging;
        public event EventHandler ValueChanged;

        public double Value
        {
            get { return value; }
            set
            {
                double next = Math.Max(0, Math.Min(0.25, value));
                if (Math.Abs(next - this.value) < 0.0001) return;
                this.value = next;
                InvalidateVisual();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public DeadzoneSlider(Color color, double initial)
        {
            accent = color;
            value = initial;
            Focusable = true;
            Cursor = Cursors.Hand;
            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { dragging = true; CaptureMouse(); SetFromMouse(e.GetPosition(this).X); Focus(); };
            MouseMove += delegate(object sender, MouseEventArgs e) { if (dragging) SetFromMouse(e.GetPosition(this).X); };
            MouseLeftButtonUp += delegate { dragging = false; ReleaseMouseCapture(); };
            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Down) { Value -= 0.01; e.Handled = true; }
            if (e.Key == Key.Right || e.Key == Key.Up) { Value += 0.01; e.Handled = true; }
            if (e.Key == Key.Home) { Value = 0; e.Handled = true; }
            if (e.Key == Key.End) { Value = 0.25; e.Handled = true; }
        }

        private void SetFromMouse(double mouseX)
        {
            double usable = Math.Max(1, ActualWidth - 14);
            Value = Math.Max(0, Math.Min(1, (mouseX - 7) / usable)) * 0.25;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DeadzoneSliderAutomationPeer(this);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double y = ActualHeight / 2.0;
            double start = 7;
            double end = Math.Max(start, ActualWidth - 7);
            Pen track = new Pen(new SolidColorBrush(Color.FromRgb(104, 119, 129)), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(track, new Point(start, y), new Point(end, y));
            double thumbX = start + (end - start) * value / 0.25;
            Pen filled = new Pen(new SolidColorBrush(accent), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(filled, new Point(start, y), new Point(thumbX, y));
            dc.DrawEllipse(new SolidColorBrush(accent), new Pen(new SolidColorBrush(Color.FromArgb(200, 226, 234, 240)), 0.7), new Point(thumbX, y), 6.5, 6.5);
            if (IsKeyboardFocused) dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(170, accent.R, accent.G, accent.B)), 1), new Rect(1, 1, Math.Max(0, ActualWidth - 2), Math.Max(0, ActualHeight - 2)));
        }
    }
}
