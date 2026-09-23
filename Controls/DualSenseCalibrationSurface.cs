// DualSenseCalibrationSurface
//
// Extracted verbatim from ControllerLab.cs (lines 9152-9400) on 2026-09-22
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
    public sealed class DualSenseCalibrationSurface : FrameworkElement
    {
        private readonly DualSenseRegionManager manager;
        private readonly ImageSource photo;
        private Matrix stageMatrix = Matrix.Identity;
        private string selectedId;
        private DualSenseCalibrationHandle selectedHandle;
        private Point previousSource;
        private Point pointerSource;
        private bool dragging;
        private bool draggingHandle;

        public event Action<string> RegionSelected;
        public event Action EditStarted;
        public event Action<string> CoordinatesChanged;
        public double BackgroundOpacity { get; set; }
        public double OverlayOpacity { get; set; }
        public bool ImageLocked { get; set; }
        // Calibration-only rendering: no fill, no halo and exactly one screen pixel of stroke.
        public bool OutlineCalibrationView { get; set; }
        public string SelectedId { get { return selectedId; } }
        public DualSenseCalibrationHandle SelectedHandle { get { return selectedHandle; } }

        public DualSenseCalibrationSurface(DualSenseRegionManager value, ImageSource image)
        {
            manager = value;
            photo = image;
            BackgroundOpacity = 1.0;
            OverlayOpacity = 0.72;
            ImageLocked = true;
            pointerSource = new Point(DualSenseRegionManager.LogicalWidth * 0.5, DualSenseRegionManager.LogicalHeight * 0.5);
            Focusable = true;
            Cursor = Cursors.Cross;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            KeyDown += OnKeyDown;
        }

        public void Select(string id)
        {
            selectedId = id;
            selectedHandle = null;
            if (RegionSelected != null) RegionSelected(id);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth < 2 || ActualHeight < 2 || manager == null || photo == null) return;
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            stageMatrix = manager.GetStageMatrix(ActualWidth, ActualHeight, dpi);
            double scale = Math.Max(0.001, stageMatrix.M11);
            dc.DrawRectangle(Palette.WindowBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));
            dc.PushTransform(new MatrixTransform(stageMatrix));
            dc.PushOpacity(BackgroundOpacity);
            dc.DrawImage(photo, new Rect(0, 0, DualSenseRegionManager.LogicalWidth, DualSenseRegionManager.LogicalHeight));
            dc.Pop();

            if (manager.Document.Regions != null)
            {
                for (int i = 0; i < manager.Document.Regions.Count; i++)
                {
                    DualSenseRegionDefinition region = manager.Document.Regions[i];
                    if (region == null || string.Equals(region.Kind, "shared", StringComparison.OrdinalIgnoreCase)) continue;
                    Geometry geometry = manager.GetGeometry(region.Id);
                    if (geometry == null) continue;
                    bool selected = string.Equals(region.Id, selectedId, StringComparison.OrdinalIgnoreCase);
                    Color color = selected ? Palette.Green : (OutlineCalibrationView && region.Id.StartsWith("dpad-", StringComparison.OrdinalIgnoreCase) ? Palette.Warning : Palette.Blue);
                    byte fillAlpha = OutlineCalibrationView ? (byte)0 : (byte)(selected ? 42 : 18);
                    double strokePixels = OutlineCalibrationView ? 1.0 : (selected ? 1.7 : 1.0);
                    byte strokeAlpha = OutlineCalibrationView ? (byte)255 : (byte)(selected ? 240 : 135);
                    Pen stroke = new Pen(new SolidColorBrush(Color.FromArgb(strokeAlpha, color.R, color.G, color.B)), strokePixels / scale);
                    stroke.LineJoin = PenLineJoin.Round;
                    dc.PushOpacity(OverlayOpacity);
                    dc.DrawGeometry(fillAlpha == 0 ? null : new SolidColorBrush(Color.FromArgb(fillAlpha, color.R, color.G, color.B)), stroke, geometry);
                    dc.Pop();
                    if (!OutlineCalibrationView)
                    {
                        Rect b = geometry.Bounds;
                        DrawSourceText(dc, region.Id, b.X, Math.Max(12, b.Y - 7), 11 / scale, selected ? Palette.GreenBrush : Palette.BlueBrush);
                    }
                }
            }
            if (!string.IsNullOrEmpty(selectedId)) DrawHandles(dc, scale);
            DrawCrosshair(dc, scale);
            dc.Pop();
            string coordinates = string.Format(CultureInfo.InvariantCulture, "原图坐标  X {0:0.0}   Y {1:0.0}", pointerSource.X, pointerSource.Y);
            DrawScreenText(dc, coordinates, 12, ActualHeight - 24, 11, Palette.TextBrush);
            if (OutlineCalibrationView) DrawScreenText(dc, "实体轮廓校准视图 · 无 Glow · 透明填充 · 1px 描边", 12, 14, 11, Palette.WarningBrush);
            DrawMagnifier(dc);
        }

        private void DrawMagnifier(DrawingContext dc)
        {
            if (pointerSource.X < 0 || pointerSource.Y < 0 || pointerSource.X > DualSenseRegionManager.LogicalWidth || pointerSource.Y > DualSenseRegionManager.LogicalHeight) return;
            const double zoom = 3.0;
            const double radius = 64.0;
            Point center = new Point(Math.Max(radius + 12, ActualWidth - radius - 14), radius + 14);
            EllipseGeometry lens = new EllipseGeometry(center, radius, radius);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 8, 16, 23)), null, center, radius + 3, radius + 3);
            dc.PushClip(lens);
            dc.PushTransform(new MatrixTransform(new Matrix(zoom, 0, 0, zoom, center.X - pointerSource.X * zoom, center.Y - pointerSource.Y * zoom)));
            dc.PushOpacity(BackgroundOpacity);
            dc.DrawImage(photo, new Rect(0, 0, DualSenseRegionManager.LogicalWidth, DualSenseRegionManager.LogicalHeight));
            dc.Pop();
            if (!string.IsNullOrEmpty(selectedId))
            {
                Geometry selected = manager.GetGeometry(selectedId);
                if (selected != null) dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(42, Palette.Green.R, Palette.Green.G, Palette.Green.B)), new Pen(Palette.GreenBrush, 1.15 / zoom), selected);
            }
            dc.Pop();
            dc.Pop();
            dc.DrawEllipse(null, new Pen(Palette.WarningBrush, 1.4), center, radius, radius);
            dc.DrawLine(new Pen(Palette.WarningBrush, 1.0), new Point(center.X - 10, center.Y), new Point(center.X + 10, center.Y));
            dc.DrawLine(new Pen(Palette.WarningBrush, 1.0), new Point(center.X, center.Y - 10), new Point(center.X, center.Y + 10));
            DrawScreenText(dc, "局部放大 3×", center.X - 31, center.Y + radius + 8, 10, Palette.WarningBrush);
        }

        private void DrawHandles(DrawingContext dc, double scale)
        {
            List<DualSenseCalibrationHandle> handles = manager.GetHandles(selectedId);
            for (int i = 0; i < handles.Count; i++)
            {
                DualSenseCalibrationHandle handle = handles[i];
                bool active = handle == selectedHandle;
                Color color = active ? Palette.Warning : Palette.Green;
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)), new Pen(Palette.WindowBrush, 1.0 / scale), handle.Point, 4.5 / scale, 4.5 / scale);
                if (active) DrawSourceText(dc, handle.Key, handle.Point.X + 7 / scale, handle.Point.Y - 8 / scale, 10 / scale, Palette.WarningBrush);
            }
        }

        private void DrawCrosshair(DrawingContext dc, double scale)
        {
            if (pointerSource.X < 0 || pointerSource.Y < 0 || pointerSource.X > DualSenseRegionManager.LogicalWidth || pointerSource.Y > DualSenseRegionManager.LogicalHeight) return;
            Pen pen = new Pen(new SolidColorBrush(Color.FromArgb(150, Palette.Warning.R, Palette.Warning.G, Palette.Warning.B)), 0.8 / scale);
            dc.DrawLine(pen, new Point(pointerSource.X - 18 / scale, pointerSource.Y), new Point(pointerSource.X + 18 / scale, pointerSource.Y));
            dc.DrawLine(pen, new Point(pointerSource.X, pointerSource.Y - 18 / scale), new Point(pointerSource.X, pointerSource.Y + 18 / scale));
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            pointerSource = ToSource(e.GetPosition(this));
            if (e.ChangedButton != MouseButton.Left) return;
            selectedHandle = FindHandle(pointerSource);
            if (selectedHandle != null && !string.IsNullOrEmpty(selectedId))
            {
                BeginEdit();
                dragging = draggingHandle = true;
                previousSource = pointerSource;
                CaptureMouse();
                e.Handled = true;
                return;
            }
            string hit = manager.HitTest(pointerSource);
            if (!string.IsNullOrEmpty(hit))
            {
                Select(hit);
                BeginEdit();
                dragging = true;
                draggingHandle = false;
                previousSource = pointerSource;
                CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            pointerSource = ToSource(e.GetPosition(this));
            if (CoordinatesChanged != null) CoordinatesChanged(string.Format(CultureInfo.InvariantCulture, "X {0:0.0}, Y {1:0.0}", pointerSource.X, pointerSource.Y));
            if (dragging && !string.IsNullOrEmpty(selectedId))
            {
                if (draggingHandle && selectedHandle != null) manager.MoveHandle(selectedId, selectedHandle, pointerSource.X, pointerSource.Y);
                else manager.MoveRegion(selectedId, pointerSource.X - previousSource.X, pointerSource.Y - previousSource.Y);
                previousSource = pointerSource;
            }
            InvalidateVisual();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (dragging) ReleaseMouseCapture();
            dragging = false;
            draggingHandle = false;
            InvalidateVisual();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedId)) return;
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5.0 : 1.0;
            double dx = 0;
            double dy = 0;
            if (e.Key == Key.Left) dx = -step;
            else if (e.Key == Key.Right) dx = step;
            else if (e.Key == Key.Up) dy = -step;
            else if (e.Key == Key.Down) dy = step;
            else return;
            BeginEdit();
            if (selectedHandle != null) manager.MoveHandle(selectedId, selectedHandle, selectedHandle.Point.X + dx, selectedHandle.Point.Y + dy);
            else manager.MoveRegion(selectedId, dx, dy);
            e.Handled = true;
            InvalidateVisual();
        }

        private void BeginEdit()
        {
            if (EditStarted != null) EditStarted();
        }

        private DualSenseCalibrationHandle FindHandle(Point point)
        {
            if (string.IsNullOrEmpty(selectedId)) return null;
            double threshold = 12.0 / Math.Max(0.001, stageMatrix.M11);
            List<DualSenseCalibrationHandle> handles = manager.GetHandles(selectedId);
            for (int i = 0; i < handles.Count; i++)
            {
                double dx = handles[i].Point.X - point.X;
                double dy = handles[i].Point.Y - point.Y;
                if (dx * dx + dy * dy <= threshold * threshold) return handles[i];
            }
            return null;
        }

        private Point ToSource(Point screen)
        {
            Matrix inverse = stageMatrix;
            if (!inverse.HasInverse) return new Point(-1, -1);
            inverse.Invert();
            return inverse.Transform(screen);
        }

        private void DrawSourceText(DrawingContext dc, string text, double x, double y, double size, Brush brush)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), size, brush, 1.0);
            dc.DrawText(ft, new Point(x, y));
        }

        private void DrawScreenText(DrawingContext dc, string text, double x, double y, double size, Brush brush)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
