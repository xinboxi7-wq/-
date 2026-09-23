// DualSenseRegionManager
//
// Extracted verbatim from ControllerLab.cs (lines 7840-8458) on 2026-09-22
// as part of the ControllerLab structural split (batch 3).
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
    public sealed class DualSenseRegionManager
    {
        public const int LogicalWidth = 1536;
        public const int LogicalHeight = 1024;
        private const string RegionsResource = "ControllerLab.Assets.dualSenseRegions.json";
        private const string StylesResource = "ControllerLab.Assets.dualSenseVisualStyles.json";
        private readonly HashSet<string> modifiedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> modifiedMotionRanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool stylesModified;
        private DualSenseRegionsDocument defaults;
        private DualSenseRegionsDocument document;
        private DualSenseVisualStylesDocument styles;
        private Dictionary<string, DualSenseRegionDefinition> regions;
        private Dictionary<string, DualSenseMotionRangeDefinition> motionRanges;
        private bool skipUserOverride;
        private readonly DualSenseTouchVisualizer touchVisualizer = new DualSenseTouchVisualizer();

        public string LastLoadMessage { get; private set; }
        public DualSenseRegionsDocument Document { get { return document; } }
        public DualSenseVisualStylesDocument Styles { get { return styles; } }
        public bool HasVisibleTouchContacts { get { return touchVisualizer.HasVisibleContacts; } }

        public bool UpdateTouchVisualizer(InputSnapshot state, bool reducedMotion)
        {
            return touchVisualizer.Update(state, reducedMotion) | touchVisualizer.Advance(DateTime.UtcNow, reducedMotion);
        }

        public void ResetTouchVisualizer()
        {
            touchVisualizer.Reset();
        }

        public Point MapTouchPoint(DualSenseTouchPoint point)
        {
            if (point == null || document == null || document.TouchSensor == null) return new Point(double.NaN, double.NaN);
            return DualSenseTouchVisualizer.Map(document.TouchSensor, point.X, point.Y);
        }

        public static DualSenseRegionManager Load(bool ignoreUserOverride = false)
        {
            DualSenseRegionManager manager = new DualSenseRegionManager();
            manager.skipUserOverride = ignoreUserOverride;
            manager.Reload();
            return manager;
        }

        // Development verification for the production Geometry pipeline. It validates that all
        // logical DS5 regions resolve in the single 1536×1024 source space without a region-level
        // transform, and that the same uniform stage matrix remains valid at supported DPI scales.
        public static string RunOverlayGeometrySelfTest()
        {
            DualSenseRegionManager manager = Load(true);
            string[] regionIds =
            {
                "dpad-up", "dpad-down", "dpad-left", "dpad-right",
                "button-triangle", "button-circle", "button-cross", "button-square",
                "button-l1", "button-r1", "trigger-l2", "trigger-r2",
                "button-l3", "button-r3", "button-create", "button-options",
                "button-ps", "button-mic", "touchpad-surface", "touchpad-button"
            };
            if (manager.document == null || manager.document.ImageWidth != LogicalWidth || manager.document.ImageHeight != LogicalHeight) throw new InvalidOperationException("DS5 source coordinate system is not 1536×1024.");
            for (int i = 0; i < regionIds.Length; i++)
            {
                Geometry geometry = manager.GetGeometry(regionIds[i]);
                if (geometry == null || geometry.Bounds.IsEmpty || geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0) throw new InvalidOperationException("Missing DS5 hit geometry: " + regionIds[i]);
                Matrix local = geometry.Transform == null ? Matrix.Identity : geometry.Transform.Value;
                if (!local.IsIdentity) throw new InvalidOperationException("Region-level transform is not allowed: " + regionIds[i]);
                Rect bounds = geometry.Bounds;
                if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > LogicalWidth || bounds.Bottom > LogicalHeight) throw new InvalidOperationException("DS5 hit geometry escapes source stage: " + regionIds[i]);
            }
            DualSenseRegionDefinition surface = manager.GetRegion("touchpad-surface");
            DualSenseRegionDefinition button = manager.GetRegion("touchpad-button");
            if (surface == null || button == null || !string.Equals(button.SharedGeometryId, surface.Id, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Touchpad surface/button must share one Geometry.");
            VerifyMotionGeometry(manager, "stick-left", "button-l3");
            VerifyMotionGeometry(manager, "stick-right", "button-r3");

            double[,] sizes = { { 1920, 1080 }, { 1440, 900 }, { 1280, 768 }, { 1024, 768 } };
            double[] dpis = { 1.0, 1.25, 1.5 };
            for (int size = 0; size < sizes.GetLength(0); size++)
            {
                for (int dpi = 0; dpi < dpis.Length; dpi++)
                {
                    Matrix matrix = manager.GetStageMatrix(sizes[size, 0], sizes[size, 1], dpis[dpi]);
                    double expectedScale = Math.Min(sizes[size, 0] / LogicalWidth, sizes[size, 1] / LogicalHeight);
                    if (Math.Abs(matrix.M11 - expectedScale) > 0.000001 || Math.Abs(matrix.M22 - expectedScale) > 0.000001 || Math.Abs(matrix.M12) > 0.000001 || Math.Abs(matrix.M21) > 0.000001) throw new InvalidOperationException("Non-uniform DS5 stage matrix at DPI " + dpis[dpi].ToString(CultureInfo.InvariantCulture));
                }
            }
            return "DS5 overlay self-test passed: 20 fixed hit regions, 2 motion regions, shared touchpad geometry, no region transforms, 4 client sizes × 3 DPI scales.";
        }

        private static void VerifyMotionGeometry(DualSenseRegionManager manager, string motionId, string regionId)
        {
            DualSenseMotionRangeDefinition motion = manager.GetMotion(motionId);
            Geometry region = manager.GetGeometry(regionId);
            if (motion == null || motion.Cap == null || region == null) throw new InvalidOperationException("Missing motion geometry: " + motionId);
            Rect bounds = region.Bounds;
            if (Math.Abs(bounds.X - (motion.Cap.CX - motion.Cap.RX)) > 0.001 || Math.Abs(bounds.Y - (motion.Cap.CY - motion.Cap.RY)) > 0.001 || Math.Abs(bounds.Width - motion.Cap.RX * 2) > 0.001 || Math.Abs(bounds.Height - motion.Cap.RY * 2) > 0.001) throw new InvalidOperationException("L3/R3 geometry does not reuse the measured stick cap: " + regionId);
        }

        public void Reload()
        {
            defaults = ReadEmbedded<DualSenseRegionsDocument>(RegionsResource);
            document = Clone(defaults);
            styles = ReadEmbedded<DualSenseVisualStylesDocument>(StylesResource);
            LastLoadMessage = "已加载默认 DS5 区域数据";
            BuildIndexes();
            if (!skipUserOverride) LoadUserOverride();
        }

        public void Draw(DrawingContext dc, ImageSource photo, BitmapSource leftCap, BitmapSource rightCap, DualSenseOverlayState state, double availableWidth, double availableHeight, double dpiScale)
        {
            if (dc == null || photo == null || availableWidth < 2 || availableHeight < 2) return;
            Matrix stageMatrix = GetStageMatrix(availableWidth, availableHeight, dpiScale);
            double scale = stageMatrix.M11;
            if (scale <= 0) return;

            dc.PushTransform(new MatrixTransform(stageMatrix));
            RadialGradientBrush floor = new RadialGradientBrush(Color.FromArgb(80, 25, 104, 164), Color.FromArgb(0, 7, 17, 25));
            dc.DrawEllipse(floor, null, new Point(768, 900), 610, 105);
            dc.DrawImage(photo, new Rect(0, 0, LogicalWidth, LogicalHeight));

            if (document.Regions != null)
            {
                for (int i = 0; i < document.Regions.Count; i++)
                {
                    DualSenseRegionDefinition region = document.Regions[i];
                    if (region == null || string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase)) continue;
                    double level = state.ValueFor(region.Id);
                    if (string.Equals(region.Id, "touchpad-surface", StringComparison.OrdinalIgnoreCase) && touchVisualizer.HasVisibleContacts) level = 1.0;
                    if (level <= 0.001) continue;
                    Geometry shape = GetGeometry(region.Id);
                    if (shape == null) continue;
                    Color accent = AccentFor(region.Id);
                    if (string.Equals(region.Style, "analog", StringComparison.OrdinalIgnoreCase)) DrawAnalogRegion(dc, shape, region, level, accent, scale, state.ReducedMotion);
                    else DrawRegion(dc, shape, region.Style, level, accent, scale, state.ReducedMotion);
                }
            }
            touchVisualizer.Draw(dc, GetGeometry("touchpad-surface"), document.TouchSensor, scale, state.ReducedMotion);
            DrawMotionRegion(dc, GetMotion("stick-left"), leftCap, state.LeftX, state.LeftY, state.L3, scale, state.ReducedMotion);
            DrawMotionRegion(dc, GetMotion("stick-right"), rightCap, state.RightX, state.RightY, state.R3, scale, state.ReducedMotion);
            dc.Pop();
        }

        private static double Snap(double value, double dpiScale)
        {
            return dpiScale > 0 ? Math.Round(value * dpiScale) / dpiScale : value;
        }

        private void DrawRegion(DrawingContext dc, Geometry shape, string styleId, double level, Color accent, double scale, bool reducedMotion)
        {
            DualSenseVisualStyleDefinition style = GetStyle(styleId);
            if (style == null) return;
            level = Math.Max(0, Math.Min(1, level));
            double sourceStroke = style.StrokePixels / Math.Max(0.001, scale);
            if (!reducedMotion && style.GlowOpacity > 0)
            {
                Pen glow = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * style.GlowOpacity * level), accent.R, accent.G, accent.B)), style.GlowPixels / Math.Max(0.001, scale));
                glow.LineJoin = PenLineJoin.Round;
                dc.DrawGeometry(null, glow, shape);
            }
            // Keep the color field inside the physical button geometry. Stroke and the intentionally
            // subdued outer halo are separate layers, so the fill itself never leaks past the path.
            Brush fill = new SolidColorBrush(Color.FromArgb((byte)(255 * style.FillOpacity * level), accent.R, accent.G, accent.B));
            dc.PushClip(shape);
            dc.DrawGeometry(fill, null, shape);
            dc.Pop();
            Pen stroke = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * style.StrokeOpacity * level), accent.R, accent.G, accent.B)), sourceStroke);
            stroke.LineJoin = PenLineJoin.Round;
            dc.DrawGeometry(null, stroke, shape);
        }

        private void DrawAnalogRegion(DrawingContext dc, Geometry shape, DualSenseRegionDefinition region, double level, Color accent, double scale, bool reducedMotion)
        {
            DrawRegion(dc, shape, "analog", level, accent, scale, reducedMotion);
            Rect b = shape.Bounds;
            double width = b.Width * Math.Max(0, Math.Min(1, level));
            Rect fill = region.Id == "trigger-r2" ? new Rect(b.Right - width, b.Top, width, b.Height) : new Rect(b.Left, b.Top, width, b.Height);
            dc.PushClip(shape);
            LinearGradientBrush brush = new LinearGradientBrush(Color.FromArgb(36, accent.R, accent.G, accent.B), Color.FromArgb(150, accent.R, accent.G, accent.B), new Point(0, 0), new Point(1, 0));
            dc.DrawRectangle(brush, null, fill);
            dc.Pop();
        }

        private void DrawMotionRegion(DrawingContext dc, DualSenseMotionRangeDefinition motion, BitmapSource capImage, double x, double y, double pressed, double scale, bool reducedMotion)
        {
            if (motion == null || motion.Socket == null || motion.Cap == null) return;
            Color accent = Palette.Blue;
            double magnitude = Math.Min(1.0, Math.Sqrt(x * x + y * y));
            Point center = new Point(motion.Cap.CX, motion.Cap.CY);
            Point moved = new Point(center.X + x * motion.TravelX, center.Y - y * motion.TravelY + pressed * 2.0);
            EllipseGeometry socket = Ellipse(motion.Socket);
            if (magnitude > 0.01 || pressed > 0.01)
            {
                DrawRegion(dc, socket, "active", Math.Max(magnitude, pressed * 0.8), accent, scale, reducedMotion);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, accent.R, accent.G, accent.B)), 1.0 / Math.Max(0.001, scale)), center, moved);
            }
            RadialGradientBrush cavity = new RadialGradientBrush(Color.FromRgb(35, 43, 49), Color.FromRgb(9, 13, 17));
            dc.DrawEllipse(cavity, null, center, motion.Cap.RX + 5, motion.Cap.RY + 5);
            if (capImage != null) dc.DrawImage(capImage, new Rect(moved.X - motion.Cap.RX, moved.Y - motion.Cap.RY, motion.Cap.RX * 2, motion.Cap.RY * 2));
            if (pressed > 0.001)
            {
                EllipseGeometry cap = new EllipseGeometry(moved, motion.Cap.RX * 0.90, motion.Cap.RY * 0.90);
                DrawRegion(dc, cap, "pressed", pressed, accent, scale, reducedMotion);
            }
        }

        public Geometry GetGeometry(string id)
        {
            DualSenseRegionDefinition region;
            if (string.IsNullOrEmpty(id) || !regions.TryGetValue(id, out region)) return null;
            if (string.Equals(region.Kind, "shared", StringComparison.OrdinalIgnoreCase)) return GetGeometry(region.SharedGeometryId);
            if (string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase))
            {
                DualSenseMotionRangeDefinition motion = GetMotion(region.MotionId);
                return motion == null ? null : Ellipse(motion.Cap);
            }
            if (string.Equals(region.Kind, "ellipse", StringComparison.OrdinalIgnoreCase)) return Ellipse(region.Ellipse);
            return BuildPathGeometry(region.Commands);
        }

        private static EllipseGeometry Ellipse(DualSenseEllipseDefinition ellipse)
        {
            return ellipse == null ? null : new EllipseGeometry(new Point(ellipse.CX, ellipse.CY), ellipse.RX, ellipse.RY);
        }

        private static Geometry BuildPathGeometry(List<DualSensePathCommand> commands)
        {
            if (commands == null || commands.Count == 0 || !string.Equals(commands[0].Op, "M", StringComparison.OrdinalIgnoreCase)) return null;
            bool closed = false;
            for (int i = 0; i < commands.Count; i++) if (string.Equals(commands[i].Op, "Z", StringComparison.OrdinalIgnoreCase)) closed = true;
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(new Point(commands[0].X, commands[0].Y), true, closed);
                for (int i = 1; i < commands.Count; i++)
                {
                    DualSensePathCommand command = commands[i];
                    if (string.Equals(command.Op, "L", StringComparison.OrdinalIgnoreCase)) context.LineTo(new Point(command.X, command.Y), true, false);
                    else if (string.Equals(command.Op, "Q", StringComparison.OrdinalIgnoreCase)) context.QuadraticBezierTo(new Point(command.CX, command.CY), new Point(command.X, command.Y), true, false);
                    else if (string.Equals(command.Op, "C", StringComparison.OrdinalIgnoreCase)) context.BezierTo(new Point(command.C1X, command.C1Y), new Point(command.C2X, command.C2Y), new Point(command.X, command.Y), true, false);
                }
            }
            return geometry;
        }

        public DualSenseRegionDefinition GetRegion(string id)
        {
            DualSenseRegionDefinition result;
            return id != null && regions.TryGetValue(id, out result) ? result : null;
        }

        public DualSenseMotionRangeDefinition GetMotion(string id)
        {
            DualSenseMotionRangeDefinition result;
            return id != null && motionRanges.TryGetValue(id, out result) ? result : null;
        }

        public Matrix GetStageMatrix(double availableWidth, double availableHeight, double dpiScale)
        {
            double scale = Math.Min(availableWidth / LogicalWidth, availableHeight / LogicalHeight);
            double width = LogicalWidth * scale;
            double height = LogicalHeight * scale;
            return new Matrix(scale, 0, 0, scale, Snap((availableWidth - width) * 0.5, dpiScale), Snap((availableHeight - height) * 0.5 + 4.0, dpiScale));
        }

        public string HitTest(Point source)
        {
            if (document.Regions == null) return null;
            for (int i = document.Regions.Count - 1; i >= 0; i--)
            {
                DualSenseRegionDefinition region = document.Regions[i];
                if (region == null || string.Equals(region.Kind, "shared", StringComparison.OrdinalIgnoreCase)) continue;
                Geometry shape = GetGeometry(region.Id);
                if (shape != null && shape.FillContains(source)) return region.Id;
            }
            return null;
        }

        public List<DualSenseCalibrationHandle> GetHandles(string id)
        {
            List<DualSenseCalibrationHandle> handles = new List<DualSenseCalibrationHandle>();
            DualSenseRegionDefinition region = GetRegion(id);
            if (region == null) return handles;
            if (string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase))
            {
                DualSenseMotionRangeDefinition motion = GetMotion(region.MotionId);
                if (motion != null && motion.Cap != null)
                {
                    handles.Add(new DualSenseCalibrationHandle { Key = "motion-center", Point = new Point(motion.Cap.CX, motion.Cap.CY) });
                    handles.Add(new DualSenseCalibrationHandle { Key = "motion-x-radius", Point = new Point(motion.Cap.CX + motion.Cap.RX, motion.Cap.CY) });
                    handles.Add(new DualSenseCalibrationHandle { Key = "motion-y-radius", Point = new Point(motion.Cap.CX, motion.Cap.CY + motion.Cap.RY) });
                }
                return handles;
            }
            if (region.Ellipse != null)
            {
                handles.Add(new DualSenseCalibrationHandle { Key = "ellipse-center", Point = new Point(region.Ellipse.CX, region.Ellipse.CY) });
                handles.Add(new DualSenseCalibrationHandle { Key = "ellipse-x-radius", Point = new Point(region.Ellipse.CX + region.Ellipse.RX, region.Ellipse.CY) });
                handles.Add(new DualSenseCalibrationHandle { Key = "ellipse-y-radius", Point = new Point(region.Ellipse.CX, region.Ellipse.CY + region.Ellipse.RY) });
                return handles;
            }
            if (region.Commands == null) return handles;
            for (int i = 0; i < region.Commands.Count; i++)
            {
                DualSensePathCommand command = region.Commands[i];
                if (command == null || string.Equals(command.Op, "Z", StringComparison.OrdinalIgnoreCase)) continue;
                handles.Add(new DualSenseCalibrationHandle { Key = "end", CommandIndex = i, Point = new Point(command.X, command.Y) });
                if (string.Equals(command.Op, "Q", StringComparison.OrdinalIgnoreCase)) handles.Add(new DualSenseCalibrationHandle { Key = "control", CommandIndex = i, Point = new Point(command.CX, command.CY) });
                if (string.Equals(command.Op, "C", StringComparison.OrdinalIgnoreCase))
                {
                    handles.Add(new DualSenseCalibrationHandle { Key = "control1", CommandIndex = i, Point = new Point(command.C1X, command.C1Y) });
                    handles.Add(new DualSenseCalibrationHandle { Key = "control2", CommandIndex = i, Point = new Point(command.C2X, command.C2Y) });
                }
            }
            return handles;
        }

        public void MoveRegion(string id, double dx, double dy)
        {
            DualSenseRegionDefinition region = GetRegion(id);
            if (region == null) return;
            if (string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase))
            {
                DualSenseMotionRangeDefinition motion = GetMotion(region.MotionId);
                if (motion != null && motion.Cap != null) { motion.Cap.CX += dx; motion.Cap.CY += dy; MarkMotionModified(motion.Id); }
                return;
            }
            if (region.Ellipse != null) { region.Ellipse.CX += dx; region.Ellipse.CY += dy; }
            if (region.Commands != null)
            {
                for (int i = 0; i < region.Commands.Count; i++)
                {
                    DualSensePathCommand command = region.Commands[i];
                    if (command == null || string.Equals(command.Op, "Z", StringComparison.OrdinalIgnoreCase)) continue;
                    command.X += dx; command.Y += dy;
                    if (string.Equals(command.Op, "Q", StringComparison.OrdinalIgnoreCase)) { command.CX += dx; command.CY += dy; }
                    if (string.Equals(command.Op, "C", StringComparison.OrdinalIgnoreCase)) { command.C1X += dx; command.C1Y += dy; command.C2X += dx; command.C2Y += dy; }
                }
            }
            MarkRegionModified(id);
        }

        public void MoveHandle(string id, DualSenseCalibrationHandle handle, double x, double y)
        {
            if (handle == null) return;
            DualSenseRegionDefinition region = GetRegion(id);
            if (region == null) return;
            if (string.Equals(region.Kind, "motion-cap", StringComparison.OrdinalIgnoreCase))
            {
                DualSenseMotionRangeDefinition motion = GetMotion(region.MotionId);
                if (motion == null || motion.Cap == null) return;
                if (handle.Key == "motion-center") { motion.Cap.CX = x; motion.Cap.CY = y; }
                else if (handle.Key == "motion-x-radius") motion.Cap.RX = Math.Max(2, Math.Abs(x - motion.Cap.CX));
                else if (handle.Key == "motion-y-radius") motion.Cap.RY = Math.Max(2, Math.Abs(y - motion.Cap.CY));
                MarkMotionModified(motion.Id);
                return;
            }
            if (region.Ellipse != null)
            {
                if (handle.Key == "ellipse-center") { region.Ellipse.CX = x; region.Ellipse.CY = y; }
                else if (handle.Key == "ellipse-x-radius") region.Ellipse.RX = Math.Max(2, Math.Abs(x - region.Ellipse.CX));
                else if (handle.Key == "ellipse-y-radius") region.Ellipse.RY = Math.Max(2, Math.Abs(y - region.Ellipse.CY));
                MarkRegionModified(id);
                return;
            }
            if (region.Commands == null || handle.CommandIndex < 0 || handle.CommandIndex >= region.Commands.Count) return;
            DualSensePathCommand command = region.Commands[handle.CommandIndex];
            if (handle.Key == "end") { command.X = x; command.Y = y; }
            else if (handle.Key == "control") { command.CX = x; command.CY = y; }
            else if (handle.Key == "control1") { command.C1X = x; command.C1Y = y; }
            else if (handle.Key == "control2") { command.C2X = x; command.C2Y = y; }
            MarkRegionModified(id);
        }

        public DualSenseCalibrationSnapshot CreateSnapshot()
        {
            DualSenseCalibrationSnapshot result = new DualSenseCalibrationSnapshot();
            result.Document = Clone(document);
            result.Styles = Clone(styles);
            result.ModifiedRegions = new List<string>(modifiedRegions);
            result.ModifiedMotionRanges = new List<string>(modifiedMotionRanges);
            result.StylesModified = stylesModified;
            return result;
        }

        public void RestoreSnapshot(DualSenseCalibrationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Document == null) return;
            document = Clone(snapshot.Document);
            styles = Clone(snapshot.Styles);
            modifiedRegions.Clear();
            modifiedMotionRanges.Clear();
            if (snapshot.ModifiedRegions != null) for (int i = 0; i < snapshot.ModifiedRegions.Count; i++) modifiedRegions.Add(snapshot.ModifiedRegions[i]);
            if (snapshot.ModifiedMotionRanges != null) for (int i = 0; i < snapshot.ModifiedMotionRanges.Count; i++) modifiedMotionRanges.Add(snapshot.ModifiedMotionRanges[i]);
            stylesModified = snapshot.StylesModified;
            BuildIndexes();
        }

        public void MarkRegionModified(string id)
        {
            if (!string.IsNullOrEmpty(id)) modifiedRegions.Add(id);
            BuildIndexes();
        }

        public void MarkMotionModified(string id)
        {
            if (!string.IsNullOrEmpty(id)) modifiedMotionRanges.Add(id);
            BuildIndexes();
        }

        public void MarkStylesModified()
        {
            stylesModified = true;
        }

        public void ResetRegion(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            DualSenseRegionDefinition original = FindRegion(defaults.Regions, id);
            if (original != null) ReplaceRegion(Clone(original));
            else
            {
                DualSenseMotionRangeDefinition originalMotion = FindMotion(defaults.MotionRanges, id);
                if (originalMotion != null) ReplaceMotion(Clone(originalMotion));
            }
            modifiedRegions.Remove(id);
            modifiedMotionRanges.Remove(id);
            BuildIndexes();
        }

        public bool SaveUserOverride(out string message)
        {
            try
            {
                DualSenseRegionsOverride output = new DualSenseRegionsOverride
                {
                    SchemaVersion = document.SchemaVersion,
                    SourceImage = document.SourceImage,
                    ImageWidth = document.ImageWidth,
                    ImageHeight = document.ImageHeight,
                    Regions = new List<DualSenseRegionDefinition>(),
                    MotionRanges = new List<DualSenseMotionRangeDefinition>(),
                    Styles = stylesModified && styles != null ? Clone(styles.Styles) : new List<DualSenseVisualStyleDefinition>()
                };
                foreach (string id in modifiedRegions)
                {
                    DualSenseRegionDefinition region = GetRegion(id);
                    if (region != null) output.Regions.Add(Clone(region));
                }
                foreach (string id in modifiedMotionRanges)
                {
                    DualSenseMotionRangeDefinition motion = GetMotion(id);
                    if (motion != null) output.MotionRanges.Add(Clone(motion));
                }
                string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxControllerLab");
                Directory.CreateDirectory(directory);
                WriteJson(System.IO.Path.Combine(directory, "dualSense-regions.override.json"), output);
                message = "已保存 DS5 用户校准覆盖";
                return true;
            }
            catch (Exception ex)
            {
                message = "保存 DS5 校准失败：" + ex.Message;
                return false;
            }
        }

        public bool ExportDocument(string path, out string message)
        {
            try { WriteJson(path, document); message = "已导出完整 DS5 Geometry 数据"; return true; }
            catch (Exception ex) { message = "导出失败：" + ex.Message; return false; }
        }

        public bool ImportDocument(string path, out string message)
        {
            try
            {
                DualSenseRegionsDocument imported = ReadFile<DualSenseRegionsDocument>(path);
                string reason;
                if (!ValidateDocument(imported, out reason)) { message = "导入已忽略：" + reason; return false; }
                document = imported;
                modifiedRegions.Clear();
                modifiedMotionRanges.Clear();
                for (int i = 0; i < document.Regions.Count; i++) modifiedRegions.Add(document.Regions[i].Id);
                for (int i = 0; i < document.MotionRanges.Count; i++) modifiedMotionRanges.Add(document.MotionRanges[i].Id);
                BuildIndexes();
                message = "已导入 Geometry，等待保存覆盖文件";
                return true;
            }
            catch (Exception ex) { message = "导入失败：" + ex.Message; return false; }
        }

        private void LoadUserOverride()
        {
            string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxControllerLab", "dualSense-regions.override.json");
            if (!File.Exists(path)) return;
            try
            {
                DualSenseRegionsOverride saved = ReadFile<DualSenseRegionsOverride>(path);
                if (saved == null || saved.SchemaVersion != document.SchemaVersion || saved.ImageWidth != document.ImageWidth || saved.ImageHeight != document.ImageHeight || !string.Equals(saved.SourceImage, document.SourceImage, StringComparison.OrdinalIgnoreCase))
                {
                    LastLoadMessage = "已忽略不兼容的 DS5 用户校准覆盖";
                    return;
                }
                if (saved.Regions != null)
                {
                    for (int i = 0; i < saved.Regions.Count; i++)
                    {
                        DualSenseRegionDefinition current = saved.Regions[i];
                        if (current != null && FindRegion(defaults.Regions, current.Id) != null) { ReplaceRegion(current); modifiedRegions.Add(current.Id); }
                    }
                }
                if (saved.MotionRanges != null)
                {
                    for (int i = 0; i < saved.MotionRanges.Count; i++)
                    {
                        DualSenseMotionRangeDefinition current = saved.MotionRanges[i];
                        if (current != null && FindMotion(defaults.MotionRanges, current.Id) != null) { ReplaceMotion(current); modifiedMotionRanges.Add(current.Id); }
                    }
                }
                if (saved.Styles != null && saved.Styles.Count > 0) { styles.Styles = saved.Styles; stylesModified = true; }
                BuildIndexes();
                LastLoadMessage = "已加载 DS5 用户校准覆盖";
            }
            catch
            {
                LastLoadMessage = "DS5 用户校准覆盖无效，已使用默认 Geometry";
            }
        }

        private bool ValidateDocument(DualSenseRegionsDocument candidate, out string reason)
        {
            reason = null;
            if (candidate == null || candidate.SchemaVersion != defaults.SchemaVersion) { reason = "schemaVersion 不匹配"; return false; }
            if (candidate.ImageWidth != LogicalWidth || candidate.ImageHeight != LogicalHeight || !string.Equals(candidate.SourceImage, defaults.SourceImage, StringComparison.OrdinalIgnoreCase)) { reason = "底图尺寸或名称不匹配"; return false; }
            if (candidate.Regions == null || candidate.MotionRanges == null) { reason = "regions 或 motionRanges 缺失"; return false; }
            for (int i = 0; i < defaults.Regions.Count; i++) if (FindRegion(candidate.Regions, defaults.Regions[i].Id) == null) { reason = "缺少区域：" + defaults.Regions[i].Id; return false; }
            for (int i = 0; i < defaults.MotionRanges.Count; i++) if (FindMotion(candidate.MotionRanges, defaults.MotionRanges[i].Id) == null) { reason = "缺少运动范围：" + defaults.MotionRanges[i].Id; return false; }
            return true;
        }

        private void ReplaceRegion(DualSenseRegionDefinition value)
        {
            for (int i = 0; i < document.Regions.Count; i++) if (string.Equals(document.Regions[i].Id, value.Id, StringComparison.OrdinalIgnoreCase)) { document.Regions[i] = Clone(value); return; }
        }

        private void ReplaceMotion(DualSenseMotionRangeDefinition value)
        {
            for (int i = 0; i < document.MotionRanges.Count; i++) if (string.Equals(document.MotionRanges[i].Id, value.Id, StringComparison.OrdinalIgnoreCase)) { document.MotionRanges[i] = Clone(value); return; }
        }

        private void BuildIndexes()
        {
            regions = new Dictionary<string, DualSenseRegionDefinition>(StringComparer.OrdinalIgnoreCase);
            motionRanges = new Dictionary<string, DualSenseMotionRangeDefinition>(StringComparer.OrdinalIgnoreCase);
            if (document.Regions != null) for (int i = 0; i < document.Regions.Count; i++) if (document.Regions[i] != null && !string.IsNullOrEmpty(document.Regions[i].Id)) regions[document.Regions[i].Id] = document.Regions[i];
            if (document.MotionRanges != null) for (int i = 0; i < document.MotionRanges.Count; i++) if (document.MotionRanges[i] != null && !string.IsNullOrEmpty(document.MotionRanges[i].Id)) motionRanges[document.MotionRanges[i].Id] = document.MotionRanges[i];
        }

        private DualSenseVisualStyleDefinition GetStyle(string id)
        {
            if (styles == null || styles.Styles == null) return null;
            for (int i = 0; i < styles.Styles.Count; i++) if (string.Equals(styles.Styles[i].Id, id, StringComparison.OrdinalIgnoreCase)) return styles.Styles[i];
            return styles.Styles.Count > 0 ? styles.Styles[0] : null;
        }

        public DualSenseVisualStyleDefinition FindStyle(string id)
        {
            return GetStyle(id);
        }

        private static Color AccentFor(string id)
        {
            return Palette.Blue;
        }

        private static DualSenseRegionDefinition FindRegion(List<DualSenseRegionDefinition> values, string id)
        {
            if (values == null) return null;
            for (int i = 0; i < values.Count; i++) if (values[i] != null && string.Equals(values[i].Id, id, StringComparison.OrdinalIgnoreCase)) return values[i];
            return null;
        }

        private static DualSenseMotionRangeDefinition FindMotion(List<DualSenseMotionRangeDefinition> values, string id)
        {
            if (values == null) return null;
            for (int i = 0; i < values.Count; i++) if (values[i] != null && string.Equals(values[i].Id, id, StringComparison.OrdinalIgnoreCase)) return values[i];
            return null;
        }

        private static T ReadEmbedded<T>(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) throw new InvalidOperationException("缺少嵌入资源：" + resourceName);
            try { return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
            finally { stream.Dispose(); }
        }

        private static T ReadFile<T>(string path)
        {
            using (FileStream stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        private static void WriteJson<T>(string path, T value)
        {
            using (FileStream stream = File.Create(path)) new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        }

        private static T Clone<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                serializer.WriteObject(stream, value);
                stream.Position = 0;
                return (T)serializer.ReadObject(stream);
            }
        }
    }
}
