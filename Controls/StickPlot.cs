// StickPlot
//
// Extracted verbatim from ControllerLab.cs (lines 9977-10187) on 2026-09-22
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
    public sealed class StickPlot : FrameworkElement
    {
        private readonly Color accent;
        private double x;
        private double y;
        private double deadzone = 0.08;
        private double maximumReach;
        private bool reducedMotion;
        private bool recordTrace = true;
        private StickPlotTraceMode traceMode;
        private readonly List<TimedStickPoint> passiveTrail = new List<TimedStickPoint>();
        private readonly List<TimedStickPoint> driftTrail = new List<TimedStickPoint>();
        private readonly List<TimedStickPoint> rangeTrail = new List<TimedStickPoint>();
        private readonly Typeface typeface = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private const int MaximumTracePoints = 64;
        private const int MaximumDriftTracePoints = 384;
        private static readonly TimeSpan TraceLifetime = TimeSpan.FromSeconds(1.0);
        private static readonly TimeSpan DriftTraceLifetime = TimeSpan.FromSeconds(5.0);

        private sealed class TimedStickPoint
        {
            public Point Value;
            public DateTime Timestamp;
        }

        public double Deadzone
        {
            get { return deadzone; }
            set
            {
                double next = Math.Max(0, Math.Min(0.25, value));
                if (Math.Abs(next - deadzone) < 0.0001) return;
                deadzone = next;
                InvalidateVisual();
            }
        }

        public bool ReducedMotion
        {
            get { return reducedMotion; }
            set
            {
                if (reducedMotion == value) return;
                reducedMotion = value;
                if (reducedMotion) ClearAllTrails();
                InvalidateVisual();
            }
        }

        public bool RecordTrace
        {
            get { return recordTrace; }
            set { recordTrace = value; }
        }

        public StickPlotTraceMode TraceMode
        {
            get { return traceMode; }
        }

        public double MaximumReach
        {
            get { return maximumReach; }
            set
            {
                double next = Math.Max(0, Math.Min(1, value));
                if (Math.Abs(next - maximumReach) < 0.0001) return;
                maximumReach = next;
                InvalidateVisual();
            }
        }

        public StickPlot(Color color)
        {
            accent = color;
            MinWidth = 180;
            MinHeight = 150;
            IsHitTestVisible = false;
        }

        public void UpdateValue(double nx, double ny)
        {
            DateTime now = DateTime.UtcNow;
            bool removedExpired = PurgeExpired(now);
            if (Math.Abs(nx - x) < 0.00005 && Math.Abs(ny - y) < 0.00005)
            {
                if (removedExpired) InvalidateVisual();
                return;
            }
            x = nx;
            y = ny;
            if (!reducedMotion && recordTrace)
            {
                List<TimedStickPoint> trail = ActiveTrail();
                trail.Add(new TimedStickPoint { Value = new Point(x, y), Timestamp = now });
                int maximum = traceMode == StickPlotTraceMode.Drift ? MaximumDriftTracePoints : MaximumTracePoints;
                while (trail.Count > maximum) trail.RemoveAt(0);
            }
            InvalidateVisual();
        }

        public void BeginTrace(StickPlotTraceMode mode)
        {
            ClearAllTrails();
            traceMode = mode;
            recordTrace = true;
            maximumReach = 0;
            InvalidateVisual();
        }

        public void EndTrace()
        {
            recordTrace = false;
        }

        public void ClearHistory()
        {
            ClearAllTrails();
            traceMode = StickPlotTraceMode.Passive;
            recordTrace = false;
            maximumReach = 0;
            InvalidateVisual();
        }

        private List<TimedStickPoint> ActiveTrail()
        {
            if (traceMode == StickPlotTraceMode.Drift) return driftTrail;
            if (traceMode == StickPlotTraceMode.Range) return rangeTrail;
            return passiveTrail;
        }

        private bool PurgeExpired(DateTime now)
        {
            bool changed = false;
            changed |= PurgeTrail(passiveTrail, now, TraceLifetime);
            changed |= PurgeTrail(driftTrail, now, DriftTraceLifetime);
            changed |= PurgeTrail(rangeTrail, now, TraceLifetime);
            return changed;
        }

        private static bool PurgeTrail(List<TimedStickPoint> trail, DateTime now, TimeSpan lifetime)
        {
            bool changed = false;
            while (trail.Count > 0 && now - trail[0].Timestamp > lifetime)
            {
                trail.RemoveAt(0);
                changed = true;
            }
            return changed;
        }

        private void ClearAllTrails()
        {
            passiveTrail.Clear();
            driftTrail.Clear();
            rangeTrail.Clear();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double radius = Math.Max(20, Math.Min(ActualWidth, ActualHeight) / 2.0 - 18);
            Point c = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            Pen grid = new Pen(new SolidColorBrush(Color.FromArgb(64, Palette.Border.R, Palette.Border.G, Palette.Border.B)), 1);
            Pen axis = new Pen(new SolidColorBrush(Color.FromArgb(92, Palette.Muted.R, Palette.Muted.G, Palette.Muted.B)), 1);
            Pen outer = new Pen(new SolidColorBrush(Color.FromArgb(150, Palette.Muted.R, Palette.Muted.G, Palette.Muted.B)), 1.0);
            for (int i = 1; i <= 3; i++) dc.DrawEllipse(null, grid, c, radius * i / 3.0, radius * i / 3.0);
            dc.DrawEllipse(null, outer, c, radius, radius);
            dc.DrawLine(axis, new Point(c.X - radius, c.Y), new Point(c.X + radius, c.Y));
            dc.DrawLine(axis, new Point(c.X, c.Y - radius), new Point(c.X, c.Y + radius));
            if (maximumReach > 0.01)
            {
                Pen reach = new Pen(new SolidColorBrush(Color.FromArgb(120, Palette.Warning.R, Palette.Warning.G, Palette.Warning.B)), 1.0);
                dc.DrawEllipse(null, reach, c, radius * maximumReach, radius * maximumReach);
            }
            double deadzoneRadius = Math.Max(radius * deadzone, 12);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(32, accent.R, accent.G, accent.B)), new Pen(new SolidColorBrush(Color.FromArgb(78, accent.R, accent.G, accent.B)), 0.8), c, deadzoneRadius, deadzoneRadius);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(35, 49, 60)), outer, c, 5, 5);

            SolidColorBrush accentBrush = new SolidColorBrush(accent);
            List<TimedStickPoint> trail = ActiveTrail();
            for (int i = 0; !reducedMotion && i < trail.Count; i++)
            {
                Point n = trail[i].Value;
                Point p = new Point(c.X + n.X * radius, c.Y - n.Y * radius);
                byte a = (byte)(6 + i * 42 / Math.Max(1, trail.Count));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(a, accent.R, accent.G, accent.B)), null, p, 1.8, 1.8);
            }
            Point value = new Point(c.X + x * radius, c.Y - y * radius);
            Pen vector = new Pen(new SolidColorBrush(Color.FromArgb(105, accent.R, accent.G, accent.B)), 1.2);
            dc.DrawLine(vector, c, value);
            if (!reducedMotion)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb(82, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B));
                dc.DrawEllipse(glow, null, value, 12, 12);
            }
            dc.DrawEllipse(accentBrush, new Pen(new SolidColorBrush(Color.FromArgb(190, Palette.Text.R, Palette.Text.G, Palette.Text.B)), 0.7), value, 5, 5);

            DrawLabel(dc, "-1", c.X - radius - 19, c.Y - 7);
            DrawLabel(dc, "0", c.X - 3, c.Y + 8);
            DrawLabel(dc, "1", c.X + radius + 8, c.Y - 7);
            DrawLabel(dc, "1", c.X - 3, c.Y - radius - 17);
            DrawLabel(dc, "-1", c.X - 6, c.Y + radius + 5);
        }

        private void DrawLabel(DrawingContext dc, string text, double x, double y)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10, Palette.MutedBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
