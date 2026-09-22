// DualSenseVisual
//
// Extracted verbatim from ControllerLab.cs (lines 7827-8303) on 2026-09-22
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
    public sealed class DualSenseVisual : FrameworkElement
    {
        private readonly Dictionary<int, double> levels = new Dictionary<int, double>();
        private readonly int[] animatedMasks = { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080, 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000 };
        private readonly Typeface regular = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private readonly Typeface semi = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private readonly ImageSource controllerPhoto;
        private readonly BitmapSource leftPhotoStickCap;
        private readonly BitmapSource rightPhotoStickCap;
        private readonly DualSenseRegionManager regions;
        private InputSnapshot state = new InputSnapshot { Family = ControllerFamily.PlayStation };
        private bool reducedMotion;
        private double smoothLX;
        private double smoothLY;
        private double smoothRX;
        private double smoothRY;
        private double smoothL2;
        private double smoothR2;
        private string renderedDeviceId;

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

        public DualSenseVisual()
        {
            controllerPhoto = LoadPhotoResource();
            regions = DualSenseRegionManager.Load(HasCommandLineArgument("--ds5-default-geometry"));
            BitmapSource photo = controllerPhoto as BitmapSource;
            if (photo != null && photo.PixelWidth >= 1200 && photo.PixelHeight >= 800)
            {
                // The photographed sockets and rubber caps do not share exactly the same center.
                // Crop each complete cap from its measured visual center instead of reusing the
                // older 136 px assets, which were 6-11 px off-axis and trimmed the rubber rim.
                leftPhotoStickCap = CreatePhotoStickCap(photo, 568, 484, 74);
                rightPhotoStickCap = CreatePhotoStickCap(photo, 969, 484, 74);
            }
            else
            {
                BitmapSource cleanCap = LoadSharedStickCap();
                if (cleanCap != null)
                {
                    leftPhotoStickCap = cleanCap;
                    rightPhotoStickCap = cleanCap;
                }
            }
            ClipToBounds = false;
            IsHitTestVisible = false;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        public void UpdateState(InputSnapshot value)
        {
            bool changed = state.Connected != value.Connected || state.Buttons != value.Buttons || state.LeftX != value.LeftX || state.LeftY != value.LeftY || state.RightX != value.RightX || state.RightY != value.RightY || state.LeftTrigger != value.LeftTrigger || state.RightTrigger != value.RightTrigger || state.TouchpadPressed != value.TouchpadPressed || state.MicrophoneMuted != value.MicrophoneMuted;
            if (!string.Equals(renderedDeviceId, value.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                renderedDeviceId = value.DeviceId;
                regions.ResetTouchVisualizer();
                changed = true;
            }
            state = value;
            changed |= regions.UpdateTouchVisualizer(value, reducedMotion);
            changed |= Smooth(ref smoothLX, value.LeftNormalizedX, 0.31);
            changed |= Smooth(ref smoothLY, value.LeftNormalizedY, 0.31);
            changed |= Smooth(ref smoothRX, value.RightNormalizedX, 0.31);
            changed |= Smooth(ref smoothRY, value.RightNormalizedY, 0.31);
            changed |= Smooth(ref smoothL2, value.LeftTrigger / 255.0, 0.34);
            changed |= Smooth(ref smoothR2, value.RightTrigger / 255.0, 0.34);
            for (int i = 0; i < animatedMasks.Length; i++)
            {
                int mask = animatedMasks[i];
                double before;
                if (!levels.TryGetValue(mask, out before)) before = 0;
                double target = (value.Buttons & mask) != 0 ? 1 : 0;
                double next = reducedMotion ? target : before + (target - before) * (target > before ? 0.5 : 0.22);
                if (Math.Abs(next - before) > 0.0005) changed = true;
                levels[mask] = next;
            }
            if (changed) InvalidateVisual();
        }

        public DualSenseRegionManager Regions
        {
            get { return regions; }
        }

        public ImageSource ControllerPhoto
        {
            get { return controllerPhoto; }
        }

        private bool Smooth(ref double current, double target, double speed)
        {
            double before = current;
            current += (target - current) * (reducedMotion ? 1 : speed);
            return Math.Abs(current - before) > 0.00005;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth < 10 || ActualHeight < 10) return;
            if (controllerPhoto != null)
            {
                DrawPhotographicController(dc);
                return;
            }
            const double baseW = 1000;
            const double baseH = 590;
            double scale = Math.Min(ActualWidth * 0.92 / baseW, ActualHeight * 0.89 / baseH);
            double x = (ActualWidth - baseW * scale) / 2.0;
            double y = (ActualHeight - baseH * scale) / 2.0 + 12;

            RadialGradientBrush floor = new RadialGradientBrush(Color.FromArgb(95, 25, 128, 220), Color.FromArgb(0, 7, 18, 29));
            dc.DrawEllipse(floor, null, new Point(ActualWidth / 2.0, y + 440 * scale), 390 * scale, 122 * scale);
            dc.PushTransform(new MatrixTransform(new Matrix(scale, 0, 0, scale, x, y)));

            DrawTrigger(dc, new Rect(230, 86, 140, 24), smoothL2, true);
            DrawTrigger(dc, new Rect(630, 86, 140, 24), smoothR2, false);
            DrawControllerShell(dc);
            DrawLightBar(dc);
            DrawTouchpad(dc);
            DrawDpad(dc, new Point(315, 292));
            DrawFaceButtons(dc);
            DrawSmallButton(dc, new Point(431, 296), "创建", 0x0020);
            DrawSmallButton(dc, new Point(569, 296), "选项", 0x0010);
            DrawPsButton(dc, new Point(500, 338));
            DrawStick(dc, new Point(372, 391), smoothLX, smoothLY, 0x0040, Palette.Blue);
            DrawStick(dc, new Point(628, 391), smoothRX, smoothRY, 0x0080, Palette.Blue);
            DrawShoulders(dc);
            dc.Pop();
        }

        private static bool HasCommandLineArgument(string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static ImageSource LoadPhotoResource()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ControllerLab.Assets.dualsense.png");
            if (stream == null) return null;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static BitmapSource LoadSharedStickCap()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ControllerLab.Assets.stick-cap.png");
            if (stream == null) return null;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static BitmapSource LoadDualSenseStickCap(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static BitmapSource CreatePhotoStickCap(BitmapSource source, int centerX, int centerY, int radius)
        {
            int size = radius * 2;
            CroppedBitmap crop = new CroppedBitmap(source, new Int32Rect(centerX - radius, centerY - radius, size, size));
            FormatConvertedBitmap converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = crop;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            int stride = size * 4;
            byte[] pixels = new byte[stride * size];
            converted.CopyPixels(pixels, stride, 0);
            double inner = radius - 4.0;
            double outer = radius - 0.4;
            for (int row = 0; row < size; row++)
            {
                double y = row - radius + 0.5;
                for (int column = 0; column < size; column++)
                {
                    double x = column - radius + 0.5;
                    double distance = Math.Sqrt(x * x + y * y);
                    if (distance <= inner) continue;
                    int pixel = row * stride + column * 4;
                    if (distance >= outer)
                    {
                        pixels[pixel + 3] = 0;
                    }
                    else
                    {
                        double factor = Math.Max(0, Math.Min(1, (outer - distance) / (outer - inner)));
                        pixels[pixel + 3] = (byte)(pixels[pixel + 3] * factor);
                    }
                }
            }
            WriteableBitmap cap = new WriteableBitmap(size, size, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null);
            cap.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
            cap.Freeze();
            return cap;
        }

        private void DrawPhotographicController(DrawingContext dc)
        {
            DualSenseOverlayState overlay = new DualSenseOverlayState
            {
                Connected = state.Connected,
                ReducedMotion = reducedMotion,
                DpadUp = Level(0x0001),
                DpadDown = Level(0x0002),
                DpadLeft = Level(0x0004),
                DpadRight = Level(0x0008),
                Create = Level(0x0020),
                Options = Level(0x0010),
                L3 = Level(0x0040),
                R3 = Level(0x0080),
                L1 = Level(0x0100),
                R1 = Level(0x0200),
                Ps = Level(0x0400),
                TouchpadButton = Math.Max(Level(0x0800), state.TouchpadPressed ? 1.0 : 0.0),
                Cross = Level(0x1000),
                Circle = Level(0x2000),
                Square = Level(0x4000),
                Triangle = Level(0x8000),
                Microphone = state.MicrophoneMuted ? 1.0 : 0.0,
                L2 = smoothL2,
                R2 = smoothR2,
                LeftX = smoothLX,
                LeftY = smoothLY,
                RightX = smoothRX,
                RightY = smoothRY,
                TouchCoordinatesAvailable = state.TouchCoordinatesAvailable,
                HasTouchCoordinates = state.HasTouchCoordinates,
                TouchpadSurface = regions.HasVisibleTouchContacts ? 1.0 : 0.0
            };
            regions.Draw(dc, controllerPhoto, leftPhotoStickCap, rightPhotoStickCap, overlay, ActualWidth, ActualHeight, VisualTreeHelper.GetDpi(this).DpiScaleX);
        }

        private void DrawControllerShell(DrawingContext dc)
        {
            StreamGeometry outline = new StreamGeometry();
            using (StreamGeometryContext c = outline.Open())
            {
                c.BeginFigure(new Point(140, 190), true, true);
                c.BezierTo(new Point(160, 126), new Point(234, 100), new Point(318, 106), true, false);
                c.BezierTo(new Point(397, 114), new Point(432, 139), new Point(500, 139), true, false);
                c.BezierTo(new Point(568, 139), new Point(603, 114), new Point(682, 106), true, false);
                c.BezierTo(new Point(766, 100), new Point(840, 126), new Point(860, 190), true, false);
                c.BezierTo(new Point(886, 270), new Point(876, 418), new Point(823, 499), true, false);
                c.BezierTo(new Point(786, 553), new Point(720, 547), new Point(672, 487), true, false);
                c.BezierTo(new Point(641, 452), new Point(607, 445), new Point(500, 445), true, false);
                c.BezierTo(new Point(393, 445), new Point(359, 452), new Point(328, 487), true, false);
                c.BezierTo(new Point(280, 547), new Point(214, 553), new Point(177, 499), true, false);
                c.BezierTo(new Point(124, 418), new Point(114, 270), new Point(140, 190), true, false);
            }
            LinearGradientBrush shell = new LinearGradientBrush(Color.FromRgb(236, 241, 245), Color.FromRgb(136, 150, 163), new Point(0.5, 0), new Point(0.5, 1));
            dc.DrawGeometry(shell, new Pen(new SolidColorBrush(Color.FromRgb(210, 224, 235)), 2), outline);

            StreamGeometry inner = new StreamGeometry();
            using (StreamGeometryContext c = inner.Open())
            {
                c.BeginFigure(new Point(194, 201), true, true);
                c.BezierTo(new Point(218, 153), new Point(279, 139), new Point(348, 144), true, false);
                c.BezierTo(new Point(414, 149), new Point(442, 168), new Point(500, 168), true, false);
                c.BezierTo(new Point(558, 168), new Point(586, 149), new Point(652, 144), true, false);
                c.BezierTo(new Point(721, 139), new Point(782, 153), new Point(806, 201), true, false);
                c.BezierTo(new Point(824, 250), new Point(815, 394), new Point(774, 465), true, false);
                c.BezierTo(new Point(740, 505), new Point(699, 487), new Point(658, 437), true, false);
                c.BezierTo(new Point(625, 397), new Point(590, 389), new Point(500, 389), true, false);
                c.BezierTo(new Point(410, 389), new Point(375, 397), new Point(342, 437), true, false);
                c.BezierTo(new Point(301, 487), new Point(260, 505), new Point(226, 465), true, false);
                c.BezierTo(new Point(185, 394), new Point(176, 250), new Point(194, 201), true, false);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(35, 46, 58)), new Pen(new SolidColorBrush(Color.FromRgb(55, 71, 86)), 1), inner);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(95, 255, 255, 255)), 1.2), new Point(232, 158), new Point(768, 158));
        }

        private void DrawLightBar(DrawingContext dc)
        {
            double ps = Level(0x0400);
            Color glowColor = Palette.Blue;
            RadialGradientBrush halo = new RadialGradientBrush(Color.FromArgb((byte)(48 + ps * 100), glowColor.R, glowColor.G, glowColor.B), Color.FromArgb(0, glowColor.R, glowColor.G, glowColor.B));
            dc.DrawEllipse(halo, null, new Point(500, 170), 152, 56);
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(45, 154, 255), Color.FromRgb(170, 228, 255), new Point(0, 0), new Point(1, 0)), null, new Rect(410, 160, 180, 8), 4, 4);
        }

        private void DrawTouchpad(DrawingContext dc)
        {
            double level = Math.Max(Level(0x0800), state.TouchpadPressed ? 1 : 0);
            Rect r = new Rect(420, 198, 160, 76);
            if (!reducedMotion && level > 0.01)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(55 + level * 105), Palette.Blue.R, Palette.Blue.G, Palette.Blue.B), Color.FromArgb(0, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B));
                dc.DrawRoundedRectangle(glow, null, new Rect(r.X - 18, r.Y - 12, r.Width + 36, r.Height + 24), 19, 19);
            }
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(52, 66, 78), Color.FromRgb(19, 30, 40), new Point(0.5, 0), new Point(0.5, 1)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(112 + level * 115), Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 1.4), r, 12, 12);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, 202, 219, 230)), 0.8), new Point(500, r.Y + 8), new Point(500, r.Bottom - 8));
        }

        private void DrawDpad(DrawingContext dc, Point center)
        {
            DrawDpadArm(dc, new Rect(center.X - 22, center.Y - 66, 44, 48), 0x0001);
            DrawDpadArm(dc, new Rect(center.X - 22, center.Y + 18, 44, 48), 0x0002);
            DrawDpadArm(dc, new Rect(center.X - 66, center.Y - 22, 48, 44), 0x0004);
            DrawDpadArm(dc, new Rect(center.X + 18, center.Y - 22, 48, 44), 0x0008);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(37, 49, 61)), new Pen(new SolidColorBrush(Color.FromRgb(14, 19, 25)), 1), new Rect(center.X - 23, center.Y - 23, 46, 46), 5, 5);
        }

        private void DrawDpadArm(DrawingContext dc, Rect rect, int mask)
        {
            double level = Level(mask);
            Color color = Palette.Blue;
            if (!reducedMotion && level > 0.01)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(42 + level * 96), color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                dc.DrawRoundedRectangle(glow, null, new Rect(rect.X - 12, rect.Y - 12, rect.Width + 24, rect.Height + 24), 9, 9);
            }
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(38, 49, 61)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(84 + level * 160), color.R, color.G, color.B)), 1.4), rect, 7, 7);
        }

        private void DrawFaceButtons(DrawingContext dc)
        {
            DrawFaceButton(dc, new Point(738, 226), "△", 0x8000);
            DrawFaceButton(dc, new Point(690, 274), "□", 0x4000);
            DrawFaceButton(dc, new Point(786, 274), "○", 0x2000);
            DrawFaceButton(dc, new Point(738, 322), "×", 0x1000);
        }

        private void DrawFaceButton(DrawingContext dc, Point point, string symbol, int mask)
        {
            double level = Level(mask);
            Color color = Palette.Blue;
            if (!reducedMotion && level > 0.01)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(52 + level * 118), color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                dc.DrawEllipse(glow, null, point, 34, 34);
            }
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(36, 48, 60)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(104 + level * 151), color.R, color.G, color.B)), 1.5), point, 20 - level, 20 - level);
            DrawText(dc, symbol, point.X - 9, point.Y - 15, 25, level > 0.01 ? new SolidColorBrush(Color.FromRgb(214, 242, 255)) : new SolidColorBrush(Color.FromRgb(188, 207, 220)), true);
        }

        private void DrawSmallButton(DrawingContext dc, Point point, string label, int mask)
        {
            double level = Level(mask);
            Color color = Palette.Blue;
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(40, 52, 63)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(78 + level * 160), color.R, color.G, color.B)), 1.2), point, 15, 15);
            DrawText(dc, label, point.X - 11, point.Y - 5, 8.5, Palette.MutedBrush, false);
        }

        private void DrawPsButton(DrawingContext dc, Point point)
        {
            double level = Level(0x0400);
            Color color = Palette.Blue;
            if (!reducedMotion && level > 0.01)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb((byte)(45 + level * 135), color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                dc.DrawEllipse(glow, null, point, 32, 32);
            }
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(31, 42, 53)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(90 + level * 155), color.R, color.G, color.B)), 1.3), point, 18, 18);
            DrawText(dc, "PS", point.X - 8, point.Y - 6, 9, level > 0.01 ? Palette.TextBrush : Palette.MutedBrush, true);
        }

        private void DrawStick(DrawingContext dc, Point center, double x, double y, int mask, Color accent)
        {
            double pressed = Level(mask);
            if (!reducedMotion)
            {
                RadialGradientBrush bedGlow = new RadialGradientBrush(Color.FromArgb(82, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B));
                dc.DrawEllipse(bedGlow, null, center, 71, 71);
            }
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(19, 28, 36)), new Pen(new SolidColorBrush(Color.FromRgb(91, 110, 124)), 2), center, 54, 54);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(165, accent.R, accent.G, accent.B)), 1.4), center, 46, 46);
            Point moved = new Point(center.X + x * 22, center.Y - y * 22 + pressed * 2.5);
            if (!reducedMotion && (Math.Abs(x) > 0.01 || Math.Abs(y) > 0.01)) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(155, accent.R, accent.G, accent.B)), 1.1), center, moved);
            RadialGradientBrush cap = new RadialGradientBrush(Color.FromRgb(92, 105, 116), Color.FromRgb(24, 31, 39));
            cap.GradientOrigin = new Point(0.36, 0.3);
            dc.DrawEllipse(cap, new Pen(new SolidColorBrush(Color.FromRgb(10, 14, 18)), 2), moved, 34, 34);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(122, 215, 227, 236)), 1), moved, 27, 27);
        }

        private void DrawShoulders(DrawingContext dc)
        {
            DrawShoulder(dc, new Rect(200, 125, 138, 24), 0x0100, "L1", true);
            DrawShoulder(dc, new Rect(662, 125, 138, 24), 0x0200, "R1", false);
        }

        private void DrawShoulder(DrawingContext dc, Rect rect, int mask, string label, bool left)
        {
            double level = Level(mask);
            Color color = Palette.Blue;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(47, 59, 72)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(100 + level * 145), color.R, color.G, color.B)), 1.4), rect, 8, 8);
            DrawText(dc, label, rect.X + rect.Width / 2 - 8, rect.Y + 5, 11, level > 0.01 ? Palette.TextBrush : Palette.MutedBrush, true);
        }

        private void DrawTrigger(DrawingContext dc, Rect rect, double value, bool left)
        {
            Color color = Palette.Blue;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(23, 34, 44)), new Pen(new SolidColorBrush(Color.FromRgb(86, 104, 120)), 1), rect, 7, 7);
            if (value > 0.001)
            {
                Rect filled = new Rect(rect.X + 2, rect.Y + 2, (rect.Width - 4) * value, rect.Height - 4);
                dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(46, 143, 255), Color.FromRgb(141, 211, 255), new Point(0, 0), new Point(1, 0)), null, filled, 5, 5);
            }
            DrawText(dc, left ? "L2" : "R2", left ? rect.X - 30 : rect.Right + 9, rect.Y + 4, 13, Palette.TextBrush, true);
            string percent = (value * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            DrawText(dc, percent, left ? rect.X - 34 : rect.Right + 9, rect.Y + 22, 11, Palette.BlueBrush, false);
        }

        private void DrawTelemetry(DrawingContext dc)
        {
            DrawText(dc, "左摇杆", 86, 304, 12, Palette.BlueBrush, true);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "X {0:0.000}   Y {1:0.000}", state.LeftNormalizedX, state.LeftNormalizedY), 86, 326, 10.5, Palette.MutedBrush, false);
            DrawText(dc, "右摇杆", 792, 382, 12, Palette.BlueBrush, true);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "X {0:0.000}   Y {1:0.000}", state.RightNormalizedX, state.RightNormalizedY), 792, 404, 10.5, Palette.MutedBrush, false);
            if (state.TouchpadPressed) DrawText(dc, "触控板按下", 456, 238, 10, Palette.TextBrush, true);
        }

        private double Level(int mask)
        {
            double value;
            return levels.TryGetValue(mask, out value) ? value : 0;
        }

        private void DrawText(DrawingContext dc, string text, double x, double y, double size, Brush brush, bool bold)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, bold ? semi : regular, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }
    }
}
