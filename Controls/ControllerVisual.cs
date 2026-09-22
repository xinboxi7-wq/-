// ControllerVisual
//
// Extracted verbatim from ControllerLab.cs (lines 9636-9974) on 2026-09-22
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
    public sealed class ControllerVisual : FrameworkElement
    {
        private readonly ImageSource image;
        private readonly XboxRegionManager regions;
        private readonly BitmapSource leftStickCap;
        private readonly BitmapSource rightStickCap;
        private readonly Dictionary<int, double> buttonLevels = new Dictionary<int, double>();
        private readonly int[] animatedMasks = { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080, 0x0100, 0x0200, 0x0400, 0x1000, 0x2000, 0x4000, 0x8000 };
        private InputSnapshot state = new InputSnapshot();
        private double smoothLX;
        private double smoothLY;
        private double smoothRX;
        private double smoothRY;
        private double smoothLT;
        private double smoothRT;
        private bool reducedMotion;
        private readonly Typeface regular = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private readonly Typeface semi = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

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

        public XboxRegionManager Regions { get { return regions; } }
        public ImageSource ControllerPhoto { get { return image; } }

        public ControllerVisual(ImageSource source)
        {
            image = source;
            regions = XboxRegionManager.Load(false);
            BitmapSource bitmap = source as BitmapSource;
            if (bitmap != null && bitmap.PixelWidth >= 1100 && bitmap.PixelHeight >= 700)
            {
                // The old source crops included the recessed black socket, which made the moving layer look like a dark halo.
                // This is an alpha-isolated cap with only the top dish and knurled grip, shared by both sticks.
                BitmapSource cleanCap = LoadBitmapResource("ControllerLab.Assets.stick-cap.png");
                leftStickCap = cleanCap ?? CreateCrop(bitmap, 404, 236, 144, 144);
                rightStickCap = cleanCap ?? CreateCrop(bitmap, 875, 417, 150, 150);
            }
            ClipToBounds = false;
            IsHitTestVisible = false;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        public void UpdateState(InputSnapshot value)
        {
            bool inputChanged = state.Connected != value.Connected ||
                state.Buttons != value.Buttons ||
                state.LeftTrigger != value.LeftTrigger || state.RightTrigger != value.RightTrigger ||
                state.LeftX != value.LeftX || state.LeftY != value.LeftY ||
                state.RightX != value.RightX || state.RightY != value.RightY;
            state = value;
            bool animationChanged = false;
            double stickSpeed = reducedMotion ? 1.0 : 0.28;
            double triggerSpeed = reducedMotion ? 1.0 : 0.32;
            double before = smoothLX;
            smoothLX += (value.LeftNormalizedX - smoothLX) * stickSpeed;
            animationChanged |= Math.Abs(smoothLX - before) > 0.00005;
            before = smoothLY;
            smoothLY += (value.LeftNormalizedY - smoothLY) * stickSpeed;
            animationChanged |= Math.Abs(smoothLY - before) > 0.00005;
            before = smoothRX;
            smoothRX += (value.RightNormalizedX - smoothRX) * stickSpeed;
            animationChanged |= Math.Abs(smoothRX - before) > 0.00005;
            before = smoothRY;
            smoothRY += (value.RightNormalizedY - smoothRY) * stickSpeed;
            animationChanged |= Math.Abs(smoothRY - before) > 0.00005;
            before = smoothLT;
            smoothLT += (value.LeftTrigger / 255.0 - smoothLT) * triggerSpeed;
            animationChanged |= Math.Abs(smoothLT - before) > 0.00005;
            before = smoothRT;
            smoothRT += (value.RightTrigger / 255.0 - smoothRT) * triggerSpeed;
            animationChanged |= Math.Abs(smoothRT - before) > 0.00005;
            for (int i = 0; i < animatedMasks.Length; i++)
            {
                int mask = animatedMasks[i];
                double current;
                if (!buttonLevels.TryGetValue(mask, out current)) current = 0;
                double target = (value.Buttons & mask) != 0 ? 1.0 : 0.0;
                double speed = target > current ? 0.48 : 0.22;
                double next = reducedMotion ? target : current + (target - current) * speed;
                if (Math.Abs(next - current) > 0.0005) animationChanged = true;
                buttonLevels[mask] = next;
            }
            if (inputChanged || animationChanged) InvalidateVisual();
        }

        private static BitmapSource CreateCrop(BitmapSource source, int x, int y, int width, int height)
        {
            CroppedBitmap crop = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            crop.Freeze();
            return crop;
        }

        private static BitmapSource LoadBitmapResource(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                stream.Dispose();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 10 || h < 10) return;
            // The photo and every overlay use this single 1536x1024 logical-stage matrix.
            // No region has a local Canvas position, margin, scale, or render transform.
            Matrix stage = XboxRegionManager.CreateStageMatrix(w, h);
            dc.PushTransform(new MatrixTransform(stage));
            RadialGradientBrush shadow = new RadialGradientBrush(Color.FromArgb(78, 28, 51, 65), Color.FromArgb(0, 12, 20, 27));
            dc.DrawEllipse(shadow, null, new Point(XboxRegionManager.LogicalWidth / 2.0, 750), 690, 270);
            regions.DrawPhoto(dc, image);
            regions.DrawStickSockets(dc);
            // Layer 2: fixed socket rings stay behind the moving caps. They
            // describe the recess and must never occlude the real thumb top.
            regions.DrawStickFeedback(dc, state, GetVisualLevel, reducedMotion);
            // Layer 3: the only movable pixels are the alpha-isolated thumb caps.
            // They intentionally render over the fixed rings and controller shell.
            DrawMovingStickOnSharedStage(dc, "l3", leftStickCap, smoothLX, smoothLY, GetVisualLevel(0x0040));
            DrawMovingStickOnSharedStage(dc, "r3", rightStickCap, smoothRX, smoothRY, GetVisualLevel(0x0080));
            // Layer 4: all press/trigger feedback is above the photo but clipped
            // to the corresponding shared-stage geometry.
            // Top trigger masks map directly from the latest input report. The
            // smoothed values remain available for non-critical visual motion,
            // but must not delay LT/RT pressure feedback on the controller.
            regions.DrawActiveFeedback(dc, state, GetVisualLevel, state.LeftTrigger / 255.0, state.RightTrigger / 255.0, reducedMotion);
            dc.Pop();
        }

        private void DrawMovingStickOnSharedStage(DrawingContext dc, string id, BitmapSource cap, double inputX, double inputY, double pressed)
        {
            Point center = regions.GetStickCenter(id);
            Size size = regions.GetStickSize(id);
            Vector travel = regions.GetStickTravel(id);
            if (reducedMotion) travel *= 0.72;
            Point moved = new Point(center.X + inputX * travel.X, center.Y - inputY * travel.Y + pressed * 2.0);
            if (cap == null)
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(29, 31, 34)), null, moved, size.Width * 0.40, size.Height * 0.40);
                return;
            }
            // The cap is a clean alpha-isolated bitmap. It is intentionally not clipped
            // by the fixed L3/R3 hit geometry: its whole silhouette stays above the shell.
            dc.DrawImage(cap, new Rect(moved.X - size.Width / 2.0, moved.Y - size.Height / 2.0, size.Width, size.Height));
            if (pressed > 0.01) dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(150, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 1.5), regions.GetGeometry(id));
        }

        private double GetLevel(int mask)
        {
            double level;
            return buttonLevels.TryGetValue(mask, out level) ? level : 0;
        }

        private double GetVisualLevel(int mask)
        {
            return (state.Buttons & mask) != 0 ? 1.0 : GetLevel(mask);
        }

        private void DrawStickSocket(DrawingContext dc, Rect rect, BitmapSource socket, double nx, double ny, double cavityRadiusSource, double cavityOffsetXSource, double cavityOffsetYSource)
        {
            Point center = new Point(rect.X + nx * rect.Width, rect.Y + ny * rect.Height);
            double sourceScale = rect.Width / 1586.0;
            double cavityRadius = cavityRadiusSource * sourceScale;
            Point cavityCenter = new Point(center.X + cavityOffsetXSource * sourceScale, center.Y + cavityOffsetYSource * sourceScale);
            // Cover the photo's fixed green/blue ring with one neutral recessed socket. The moving cap is rendered after this layer.
            double socketRadius = cavityRadius * 1.30;
            RadialGradientBrush cavity = new RadialGradientBrush(Color.FromRgb(30, 38, 45), Color.FromRgb(5, 8, 10));
            cavity.GradientOrigin = new Point(0.43, 0.38);
            dc.DrawEllipse(cavity, new Pen(new SolidColorBrush(Color.FromArgb(118, Palette.Border.R, Palette.Border.G, Palette.Border.B)), Math.Max(1.0, sourceScale * 1.6)), cavityCenter, socketRadius, socketRadius);
            Pen accentRing = new Pen(new SolidColorBrush(Color.FromArgb(152, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), Math.Max(1.15, sourceScale * 1.9));
            dc.DrawEllipse(null, accentRing, cavityCenter, socketRadius * 0.90, socketRadius * 0.90);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(72, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), Math.Max(0.9, sourceScale * 1.1)), cavityCenter, socketRadius * 1.05, socketRadius * 1.05);
        }

        private void DrawMovingStick(DrawingContext dc, Rect rect, BitmapSource cap, double nx, double ny, double inputX, double inputY, double pressed, Color color, double capRadiusSource, double capScale, double shellRadiusSource, double capDiameterSource)
        {
            Point center = new Point(rect.X + nx * rect.Width, rect.Y + ny * rect.Height);
            double magnitude = Math.Min(1.0, Math.Sqrt(inputX * inputX + inputY * inputY));
            if (cap == null)
            {
                double fallbackRadius = rect.Width * 0.032;
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(29, 31, 34)), null, center, fallbackRadius, fallbackRadius);
                return;
            }
            bool moving = magnitude >= 0.001 || pressed >= 0.001;

            double travel = rect.Width * (reducedMotion ? 0.010 : 0.014);
            Point moved = new Point(center.X + inputX * travel, center.Y - inputY * travel + pressed * Math.Max(0.8, rect.Width * 0.0014));
            double sourceScale = rect.Width / 1586.0;
            double capRadius = capRadiusSource * sourceScale;
            double shellRadius = shellRadiusSource * sourceScale;
            double protrudeRadius = capRadius * capScale;
            double protrudeSize = capDiameterSource * sourceScale * capScale;
            double protrudeImageRadius = protrudeSize / 2.0;

            if (!reducedMotion && magnitude >= 0.001)
            {
                Pen vector = new Pen(new SolidColorBrush(Color.FromArgb((byte)(58 + magnitude * 88), color.R, color.G, color.B)), 1.2);
                dc.DrawLine(vector, center, moved);
            }

            if (cap != null)
            {
                // The cap itself has a feathered alpha silhouette, so it remains a separate protruding object without a circular black crop edge.
                Rect capRect = new Rect(moved.X - protrudeImageRadius, moved.Y - protrudeImageRadius, protrudeSize, protrudeSize);
                dc.PushClip(new EllipseGeometry(center, shellRadius, shellRadius));
                dc.DrawImage(cap, capRect);
                dc.Pop();
            }
            else
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(29, 31, 34)), null, moved, protrudeRadius, protrudeRadius);
            }

        }

        private void DrawButtonFeedback(DrawingContext dc, Rect rect, double nx, double ny, double level, Color color, double sizeScale)
        {
            if (level < 0.01) return;
            Point p = new Point(rect.X + nx * rect.Width, rect.Y + ny * rect.Height);
            double radius = Math.Max(9, rect.Width * 0.0235 * sizeScale) * (1.0 - level * 0.045);
            byte alpha = (byte)(50 + level * 145);
            if (!reducedMotion)
            {
                RadialGradientBrush glow = new RadialGradientBrush(Color.FromArgb(alpha, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
                dc.DrawEllipse(glow, null, p, radius * 1.45, radius * 1.45);
            }
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(28 + level * 42), color.R, color.G, color.B)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(120 + level * 120), color.R, color.G, color.B)), 1.6), p, radius, radius);
        }

        private void DrawShoulderFeedback(DrawingContext dc, Rect rect, double nx, double ny, double level, Color color)
        {
            if (level < 0.01) return;
            Rect r = new Rect(rect.X + nx * rect.Width - rect.Width * 0.060, rect.Y + ny * rect.Height - 7 + level * 2, rect.Width * 0.12, 15);
            byte alpha = (byte)(35 + level * 115);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), new Pen(new SolidColorBrush(Color.FromArgb((byte)(100 + level * 150), color.R, color.G, color.B)), 1.4), r, 7, 7);
        }

        private void DrawTriggerFeedback(DrawingContext dc, Rect rect, double nx, double value, Color color)
        {
            if (value < 0.005) return;
            double width = rect.Width * 0.12;
            double height = 12 + value * 7;
            Rect r = new Rect(rect.X + nx * rect.Width - width / 2.0, rect.Y + rect.Height * 0.064 - height / 2.0 + value * 2.0, width, height);
            byte alpha = (byte)(30 + value * 130);
            Brush fill = reducedMotion
                ? (Brush)new SolidColorBrush(Color.FromArgb((byte)Math.Min(120, (int)alpha), color.R, color.G, color.B))
                : new RadialGradientBrush(Color.FromArgb(alpha, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
            dc.DrawRoundedRectangle(fill, new Pen(new SolidColorBrush(Color.FromArgb((byte)(70 + value * 170), color.R, color.G, color.B)), 1.4), r, 8, 8);
        }

        private void DrawCallouts(DrawingContext dc, Rect rect)
        {
            double leftX = 12;
            double rightX = ActualWidth - 105;
            Point lp = new Point(rect.X + rect.Width * (476.0 / 1586.0), rect.Y + rect.Height * (308.0 / 992.0));
            Point rp = new Point(rect.X + rect.Width * (950.0 / 1586.0), rect.Y + rect.Height * (492.0 / 992.0));
            Pen greenPen = new Pen(Palette.GreenBrush, 1.3);
            Pen bluePen = new Pen(Palette.BlueBrush, 1.3);
            double leftMagnitude = Magnitude(state.LeftNormalizedX, state.LeftNormalizedY);
            double rightMagnitude = Magnitude(state.RightNormalizedX, state.RightNormalizedY);
            string leftAngleText = leftMagnitude < 0.02 ? "—" : Angle(state.LeftNormalizedX, state.LeftNormalizedY).ToString("0", CultureInfo.InvariantCulture) + "°";
            string rightAngleText = rightMagnitude < 0.02 ? "—" : Angle(state.RightNormalizedX, state.RightNormalizedY).ToString("0", CultureInfo.InvariantCulture) + "°";

            dc.DrawLine(greenPen, new Point(leftX + 126, lp.Y + 60), new Point(rect.X - 8, lp.Y + 60));
            dc.DrawLine(greenPen, new Point(rect.X - 8, lp.Y + 60), lp);
            DrawText(dc, "左摇杆", leftX, lp.Y + 23, 13, Palette.GreenBrush, true);
            DrawText(dc, "X", leftX, lp.Y + 50, 12, Palette.MutedBrush, false);
            DrawText(dc, state.LeftX.ToString(CultureInfo.InvariantCulture), leftX + 38, lp.Y + 50, 12, Palette.GreenBrush, false);
            DrawText(dc, "Y", leftX, lp.Y + 72, 12, Palette.MutedBrush, false);
            DrawText(dc, state.LeftY.ToString(CultureInfo.InvariantCulture), leftX + 38, lp.Y + 72, 12, Palette.GreenBrush, false);
            DrawText(dc, "幅度", leftX, lp.Y + 94, 12, Palette.MutedBrush, false);
            DrawText(dc, leftMagnitude.ToString("0.00", CultureInfo.InvariantCulture), leftX + 38, lp.Y + 94, 12, Palette.GreenBrush, false);
            DrawText(dc, "角度", leftX, lp.Y + 116, 12, Palette.MutedBrush, false);
            DrawText(dc, leftAngleText, leftX + 38, lp.Y + 116, 12, Palette.GreenBrush, false);

            dc.DrawLine(bluePen, rp, new Point(rightX - 12, rp.Y - 28));
            DrawText(dc, "右摇杆", rightX, rp.Y - 66, 13, Palette.BlueBrush, true);
            DrawText(dc, "X", rightX, rp.Y - 39, 12, Palette.MutedBrush, false);
            DrawText(dc, state.RightX.ToString(CultureInfo.InvariantCulture), rightX + 38, rp.Y - 39, 12, Palette.BlueBrush, false);
            DrawText(dc, "Y", rightX, rp.Y - 17, 12, Palette.MutedBrush, false);
            DrawText(dc, state.RightY.ToString(CultureInfo.InvariantCulture), rightX + 38, rp.Y - 17, 12, Palette.BlueBrush, false);
            DrawText(dc, "幅度", rightX, rp.Y + 5, 12, Palette.MutedBrush, false);
            DrawText(dc, rightMagnitude.ToString("0.00", CultureInfo.InvariantCulture), rightX + 38, rp.Y + 5, 12, Palette.BlueBrush, false);
            DrawText(dc, "角度", rightX, rp.Y + 27, 12, Palette.MutedBrush, false);
            DrawText(dc, rightAngleText, rightX + 38, rp.Y + 27, 12, Palette.BlueBrush, false);

            double ltX = rect.X + rect.Width * 0.20;
            double rtX = rect.X + rect.Width * 0.78;
            DrawText(dc, "LT", ltX - 40, rect.Y - 26, 13, Palette.TextBrush, false);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "{0:0}%", state.LeftTrigger / 2.55), ltX - 40, rect.Y - 5, 12, Palette.MutedBrush, false);
            dc.DrawLine(new Pen(Palette.MutedBrush, 1), new Point(ltX - 9, rect.Y - 2), new Point(ltX + 42, rect.Y - 2));
            dc.DrawLine(new Pen(Palette.MutedBrush, 1), new Point(ltX + 42, rect.Y - 2), new Point(ltX + 52, rect.Y + 22));
            DrawText(dc, "RT", rtX + 14, rect.Y - 26, 13, Palette.TextBrush, false);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "{0:0}%", state.RightTrigger / 2.55), rtX + 14, rect.Y - 5, 12, Palette.BlueBrush, false);
            dc.DrawLine(bluePen, new Point(rtX + 4, rect.Y - 2), new Point(rtX - 49, rect.Y - 2));
            dc.DrawLine(bluePen, new Point(rtX - 49, rect.Y - 2), new Point(rtX - 58, rect.Y + 23));
        }

        private void DrawText(DrawingContext dc, string text, double x, double y, double size, Brush brush, bool bold)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, bold ? semi : regular, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x, y));
        }

        private static double Magnitude(double x, double y)
        {
            return Math.Min(1.0, Math.Sqrt(x * x + y * y));
        }

        private static double Angle(double x, double y)
        {
            if (Math.Abs(x) < 0.0001 && Math.Abs(y) < 0.0001) return 0;
            double angle = Math.Atan2(y, x) * 180.0 / Math.PI;
            return angle < 0 ? angle + 360 : angle;
        }
    }
}
