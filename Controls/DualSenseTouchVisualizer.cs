// DualSenseTouchVisualizer
//
// Extracted verbatim from ControllerLab.cs (lines 8305-8517) on 2026-09-22
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
    // A self-contained native WPF renderer for true DualSense touch contacts. It owns temporal
    // smoothing, contact-id tracking, ripples and short-lived trails; the main controller view only
    // supplies validated HID snapshots and asks for a redraw.
    public sealed class DualSenseTouchVisualizer
    {
        private sealed class TrailPoint
        {
            public Point Position;
            public DateTime At;
        }

        private sealed class TouchTrack
        {
            public int Id;
            public bool Active;
            public bool SeenInReport;
            public bool Initialized;
            public Point Current;
            public Point Target;
            public DateTime LastSeen;
            public DateTime ReleasedAt;
            public DateTime RippleStartedAt;
            public double Opacity;
            public readonly List<TrailPoint> Trail = new List<TrailPoint>();
        }

        private readonly Dictionary<int, TouchTrack> tracks = new Dictionary<int, TouchTrack>();
        private long lastReportSequence = -1;

        public bool HasVisibleContacts
        {
            get
            {
                foreach (TouchTrack track in tracks.Values) if (track.Opacity > 0.01) return true;
                return false;
            }
        }

        public void Reset()
        {
            tracks.Clear();
            lastReportSequence = -1;
        }

        public bool Update(InputSnapshot state, bool reducedMotion)
        {
            if (state == null) return false;
            bool changed = false;
            if (state.TouchReportSequence != 0 && state.TouchReportSequence != lastReportSequence)
            {
                lastReportSequence = state.TouchReportSequence;
                DateTime now = state.TouchReportUtc == DateTime.MinValue ? DateTime.UtcNow : state.TouchReportUtc;
                foreach (TouchTrack track in tracks.Values) track.SeenInReport = false;
                changed |= ApplyPoint(state.TouchPoint1, now, reducedMotion);
                changed |= ApplyPoint(state.TouchPoint2, now, reducedMotion);
                foreach (TouchTrack track in tracks.Values)
                {
                    if (!track.SeenInReport && track.Active)
                    {
                        track.Active = false;
                        track.ReleasedAt = now;
                        changed = true;
                    }
                }
            }
            else if (!state.TouchCoordinatesAvailable && state.Family == ControllerFamily.PlayStation)
            {
                DateTime now = DateTime.UtcNow;
                foreach (TouchTrack track in tracks.Values)
                {
                    if (track.Active)
                    {
                        track.Active = false;
                        track.ReleasedAt = now;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private bool ApplyPoint(DualSenseTouchPoint point, DateTime now, bool reducedMotion)
        {
            if (point == null || !point.IsActive) return false;
            TouchTrack track;
            if (!tracks.TryGetValue(point.Id, out track))
            {
                track = new TouchTrack { Id = point.Id };
                tracks[point.Id] = track;
            }
            Point target = new Point(Clamp01(point.X), Clamp01(point.Y));
            bool isNewContact = !track.Active || !track.Initialized;
            track.SeenInReport = true;
            track.Active = true;
            track.LastSeen = now;
            track.Opacity = 1.0;
            track.Target = target;
            if (isNewContact)
            {
                track.Current = target;
                track.Initialized = true;
                track.RippleStartedAt = now;
                track.Trail.Clear();
                track.Trail.Add(new TrailPoint { Position = target, At = now });
                return true;
            }
            TrailPoint last = track.Trail.Count == 0 ? null : track.Trail[track.Trail.Count - 1];
            if (last == null || Distance(last.Position, target) >= 0.0035)
            {
                track.Trail.Add(new TrailPoint { Position = target, At = now });
                while (track.Trail.Count > 12) track.Trail.RemoveAt(0);
            }
            return true;
        }

        public bool Advance(DateTime now, bool reducedMotion)
        {
            bool changed = false;
            List<int> retired = null;
            foreach (KeyValuePair<int, TouchTrack> pair in tracks)
            {
                TouchTrack track = pair.Value;
                if (track.Active && (now - track.LastSeen).TotalMilliseconds > 80)
                {
                    track.Active = false;
                    track.ReleasedAt = track.LastSeen.AddMilliseconds(80);
                    changed = true;
                }
                double beforeX = track.Current.X;
                double beforeY = track.Current.Y;
                double smoothing = reducedMotion ? 1.0 : 0.52;
                track.Current = new Point(track.Current.X + (track.Target.X - track.Current.X) * smoothing, track.Current.Y + (track.Target.Y - track.Current.Y) * smoothing);
                if (Math.Abs(track.Current.X - beforeX) > 0.00005 || Math.Abs(track.Current.Y - beforeY) > 0.00005) changed = true;
                if (!track.Active)
                {
                    double elapsed = (now - track.ReleasedAt).TotalMilliseconds;
                    double next = elapsed <= 0 ? 1.0 : Math.Max(0, 1.0 - elapsed / (reducedMotion ? 120.0 : 200.0));
                    if (Math.Abs(next - track.Opacity) > 0.0005) changed = true;
                    track.Opacity = next;
                    if (track.Opacity <= 0.001 && (now - track.ReleasedAt).TotalMilliseconds > 260)
                    {
                        if (retired == null) retired = new List<int>();
                        retired.Add(pair.Key);
                    }
                }
                for (int i = track.Trail.Count - 1; i >= 0; i--)
                {
                    if ((now - track.Trail[i].At).TotalMilliseconds > 230) track.Trail.RemoveAt(i);
                }
            }
            if (retired != null)
            {
                for (int i = 0; i < retired.Count; i++) tracks.Remove(retired[i]);
                changed = true;
            }
            return changed;
        }

        public void Draw(DrawingContext dc, Geometry touchpadClip, DualSenseTouchSensorDefinition mapping, double scale, bool reducedMotion)
        {
            if (dc == null || touchpadClip == null || mapping == null || !HasVisibleContacts) return;
            double inverseScale = 1.0 / Math.Max(0.001, scale);
            DateTime now = DateTime.UtcNow;
            dc.PushClip(touchpadClip);
            foreach (TouchTrack track in tracks.Values)
            {
                if (track.Opacity <= 0.001) continue;
                DrawTrail(dc, track, mapping, inverseScale);
                Point point = Map(mapping, track.Current.X, track.Current.Y);
                byte alpha = (byte)Math.Max(0, Math.Min(255, 255 * track.Opacity));
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(alpha * 0.42), Palette.Blue.R, Palette.Blue.G, Palette.Blue.B), Color.FromArgb(0, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B));
                dc.DrawEllipse(glow, null, point, 17.0 * inverseScale, 17.0 * inverseScale);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.90), 116, 194, 255)), new Pen(new SolidColorBrush(Color.FromArgb(alpha, 224, 245, 255)), 1.0 * inverseScale), point, 4.7 * inverseScale, 4.7 * inverseScale);
                double rippleAge = (now - track.RippleStartedAt).TotalMilliseconds;
                if (!reducedMotion && rippleAge >= 0 && rippleAge < 310)
                {
                    double progress = rippleAge / 310.0;
                    byte rippleAlpha = (byte)Math.Max(0, Math.Min(150, 150 * (1.0 - progress) * track.Opacity));
                    dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(rippleAlpha, 96, 184, 255)), 1.15 * inverseScale), point, (7 + progress * 20) * inverseScale, (7 + progress * 20) * inverseScale);
                }
            }
            dc.Pop();
        }

        public static Point Map(DualSenseTouchSensorDefinition mapping, double u, double v)
        {
            u = Clamp01(u);
            v = Clamp01(v);
            DualSenseLogicalPoint topLeft = mapping.TopLeft ?? new DualSenseLogicalPoint { X = mapping.X, Y = mapping.Y };
            DualSenseLogicalPoint topRight = mapping.TopRight ?? new DualSenseLogicalPoint { X = mapping.X + mapping.Width, Y = mapping.Y };
            DualSenseLogicalPoint bottomLeft = mapping.BottomLeft ?? new DualSenseLogicalPoint { X = mapping.X, Y = mapping.Y + mapping.Height };
            DualSenseLogicalPoint bottomRight = mapping.BottomRight ?? new DualSenseLogicalPoint { X = mapping.X + mapping.Width, Y = mapping.Y + mapping.Height };
            double topWeight = 1.0 - v;
            double bottomWeight = v;
            return new Point(
                topLeft.X * (1.0 - u) * topWeight + topRight.X * u * topWeight + bottomLeft.X * (1.0 - u) * bottomWeight + bottomRight.X * u * bottomWeight,
                topLeft.Y * (1.0 - u) * topWeight + topRight.Y * u * topWeight + bottomLeft.Y * (1.0 - u) * bottomWeight + bottomRight.Y * u * bottomWeight);
        }

        private static void DrawTrail(DrawingContext dc, TouchTrack track, DualSenseTouchSensorDefinition mapping, double inverseScale)
        {
            if (track.Trail.Count < 2) return;
            for (int i = 1; i < track.Trail.Count; i++)
            {
                double life = i / (double)(track.Trail.Count - 1);
                byte alpha = (byte)Math.Max(0, Math.Min(110, 110 * life * life * track.Opacity));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(alpha, 75, 163, 255)), (1.1 + life * 2.4) * inverseScale), Map(mapping, track.Trail[i - 1].Position.X, track.Trail[i - 1].Position.Y), Map(mapping, track.Trail[i].Position.X, track.Trail[i].Position.Y));
            }
        }

        private static double Clamp01(double value) { return Math.Max(0, Math.Min(1, value)); }
        private static double Distance(Point a, Point b) { double dx = a.X - b.X; double dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy); }
    }
}
