// TriggerChart
//
// Extracted verbatim from ControllerLab.cs (lines 10295-10457) on 2026-09-22
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
    public sealed class TriggerChart : FrameworkElement
    {
        public const double SampleIntervalSeconds = 0.030;
        private readonly Color accent;
        private readonly TriggerTelemetryBuffer telemetry;
        private double value;
        private bool reducedMotion;
        private bool paused;
        private string label = "扳机";
        private TriggerTelemetryStats stats = new TriggerTelemetryStats();
        private readonly Typeface typeface = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        public TextBlock PercentText { get; set; }
        public TextBlock PeakText { get; set; }
        public TextBlock DetailText { get; set; }
        public string Label
        {
            get { return label; }
            set { label = string.IsNullOrEmpty(value) ? "扳机" : value; }
        }
        public bool Paused
        {
            get { return paused; }
            set
            {
                paused = value;
                InvalidateVisual();
            }
        }
        public double PeakValue { get { return stats == null ? 0 : stats.Peak; } }

        public bool ReducedMotion
        {
            get { return reducedMotion; }
            set
            {
                if (reducedMotion == value) return;
                reducedMotion = value;
                InvalidateVisual();
            }
        }

        public double Value
        {
            get { return value; }
            set
            {
                double next = Math.Max(0, Math.Min(1, value));
                bool changed = Math.Abs(next - this.value) >= 0.0005;
                this.value = next;
                stats = telemetry.GetStats();
                string currentText = string.Format(CultureInfo.InvariantCulture, "{0:0}%", this.value * 100.0);
                if (PercentText != null && PercentText.Text != currentText) PercentText.Text = currentText;
                string peakText = string.Format(CultureInfo.InvariantCulture, "{0:0}%", stats.Peak * 100.0);
                if (PeakText != null && PeakText.Text != peakText) PeakText.Text = peakText;
                if (DetailText != null) DetailText.Text = string.Format(CultureInfo.InvariantCulture, "最低 {0:0}% · 平均 {1:0}% · 回弹 {2:0}%/s · 变化 {3} · 噪声 {4:0.0}% · 满行程 {5} · 回零 {6} · {7}", stats.Minimum * 100.0, stats.Average * 100.0, stats.ReleaseSpeedPerSecond * 100.0, stats.ChangeCount, stats.Noise * 100.0, stats.ReachesFullRange ? "✓" : "待测", stats.ReturnsToZero ? "✓" : "待测", stats.HealthText);
                AutomationProperties.SetName(this, string.Format(CultureInfo.InvariantCulture, "{0} 当前 {1:0}% ，近 5 秒峰值 {2:0}%", label, this.value * 100.0, stats.Peak * 100.0));
                if (changed || !paused) InvalidateVisual();
            }
        }

        public void ClearHistory()
        {
            stats = telemetry.GetStats();
            if (PeakText != null) PeakText.Text = "0%";
            InvalidateVisual();
        }

        public double[] GetHistorySnapshot()
        {
            return telemetry.GetSnapshot();
        }

        public TriggerChart(Color color, TriggerTelemetryBuffer source)
        {
            accent = color;
            telemetry = source ?? new TriggerTelemetryBuffer();
            MinHeight = 0;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Rect plot = new Rect(42, 8, Math.Max(90, ActualWidth - 58), Math.Max(72, ActualHeight - 34));
            Brush gridBrush = new SolidColorBrush(Color.FromArgb(46, 86, 109, 125));
            Pen grid = new Pen(gridBrush, 1);
            for (int i = 0; i <= 2; i++)
            {
                double gy = plot.Top + plot.Height * i / 2.0;
                dc.DrawLine(grid, new Point(plot.Left, gy), new Point(plot.Right, gy));
            }
            Pen axis = new Pen(new SolidColorBrush(Color.FromArgb(85, Palette.Border.R, Palette.Border.G, Palette.Border.B)), 1.0);
            dc.DrawLine(axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));

            double[] history = telemetry.GetSnapshot();
            if (history.Length > 0)
            {
                List<Point> points = new List<Point>(history.Length);
                for (int i = 0; i < history.Length; i++)
                {
                    double position = (170 - history.Length + i) / 169.0;
                    points.Add(new Point(plot.Left + plot.Width * position, plot.Bottom - plot.Height * history[i]));
                }

                if (!reducedMotion && points.Count > 1)
                {
                    StreamGeometry area = new StreamGeometry();
                    using (StreamGeometryContext context = area.Open())
                    {
                        context.BeginFigure(new Point(points[0].X, plot.Bottom), true, true);
                        context.LineTo(points[0], true, false);
                        for (int i = 1; i < points.Count; i++) context.LineTo(points[i], true, false);
                        context.LineTo(new Point(points[points.Count - 1].X, plot.Bottom), true, false);
                    }
                    LinearGradientBrush fill = new LinearGradientBrush(
                        Color.FromArgb(92, accent.R, accent.G, accent.B),
                        Color.FromArgb(5, accent.R, accent.G, accent.B),
                        new Point(0.5, 0), new Point(0.5, 1));
                    dc.DrawGeometry(fill, null, area);
                }

                StreamGeometry line = new StreamGeometry();
                using (StreamGeometryContext context = line.Open())
                {
                    context.BeginFigure(points[0], false, false);
                    for (int i = 1; i < points.Count - 1; i++)
                    {
                        Point mid = new Point((points[i].X + points[i + 1].X) * 0.5, (points[i].Y + points[i + 1].Y) * 0.5);
                        context.QuadraticBezierTo(points[i], mid, true, false);
                    }
                    if (points.Count > 1) context.LineTo(points[points.Count - 1], true, false);
                }
                Pen curve = new Pen(new SolidColorBrush(accent), reducedMotion ? 1.5 : 2.0)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                dc.DrawGeometry(null, curve, line);
            }

            Point marker = new Point(plot.Right, plot.Bottom - plot.Height * value);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(110, accent.R, accent.G, accent.B)), 1), new Point(plot.Right, plot.Top), new Point(plot.Right, plot.Bottom));
            if (!reducedMotion)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb(110, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B));
                dc.DrawEllipse(glow, null, marker, 13, 13);
            }
            dc.DrawEllipse(new SolidColorBrush(Palette.Surface), new Pen(new SolidColorBrush(accent), 1.7), marker, 5, 5);

            DrawText(dc, "100%", 2, plot.Top - 5, 10);
            DrawText(dc, "50%", 10, plot.Top + plot.Height / 2 - 6, 10);
            DrawText(dc, "0%", 20, plot.Bottom - 7, 10);
            DrawText(dc, "5 秒前", plot.Left, plot.Bottom + 5, 10);
            DrawText(dc, "现在", plot.Right - 22, plot.Bottom + 5, 10);
        }

        private void DrawText(DrawingContext dc, string text, double x, double y, double size)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, Palette.MutedBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
