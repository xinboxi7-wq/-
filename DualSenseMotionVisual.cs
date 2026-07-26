using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ControllerLab
{
    // Lightweight two-dimensional pose representation. It intentionally consumes only
    // MotionViewState from the motion service; no HID or Raw Input types enter this view.
    public sealed class DualSenseMotionPoseView : FrameworkElement
    {
        private static readonly StreamGeometry ControllerGeometry = CreateControllerGeometry();
        private static readonly StreamGeometry ArrowGeometry = CreateArrowGeometry(new Point(0, -119), 8);
        private readonly Pen outerPen = new Pen(new SolidColorBrush(Color.FromArgb(150, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 2.0);
        private readonly Pen innerPen = new Pen(new SolidColorBrush(Color.FromArgb(120, Palette.Border.R, Palette.Border.G, Palette.Border.B)), 1.0);
        private readonly Pen dialPen = new Pen(new SolidColorBrush(Color.FromArgb(60, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 1.0);
        private readonly Brush bodyBrush = new SolidColorBrush(Color.FromRgb(28, 44, 56));
        private readonly Brush panelBrush = new SolidColorBrush(Color.FromRgb(16, 27, 36));
        private readonly Brush accentBrush = new SolidColorBrush(Color.FromArgb(135, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B));
        private readonly Brush mutedBrush = new SolidColorBrush(Color.FromArgb(130, Palette.Muted.R, Palette.Muted.G, Palette.Muted.B));
        private MotionViewState state;
        private bool smoothingEnabled = true;
        private bool hasDisplayPose;
        private double pitch;
        private double roll;
        private double yaw;

        public DualSenseMotionPoseView()
        {
            MinWidth = 360;
            MinHeight = 320;
            SnapsToDevicePixels = true;
            IsHitTestVisible = false;
        }

        public bool SmoothingEnabled
        {
            get { return smoothingEnabled; }
            set
            {
                if (smoothingEnabled == value) return;
                smoothingEnabled = value;
                hasDisplayPose = false;
            }
        }

        public void SetState(MotionViewState value)
        {
            state = value;
            if (value == null || !value.IsAvailable || value.Pose == null || !value.Pose.HasPose)
            {
                hasDisplayPose = false;
                InvalidateVisual();
                return;
            }
            if (!hasDisplayPose || !smoothingEnabled)
            {
                pitch = value.Pose.Pitch;
                roll = value.Pose.Roll;
                yaw = value.Pose.Yaw;
                hasDisplayPose = true;
            }
            else
            {
                pitch = SmoothAngle(pitch, value.Pose.Pitch, 0.28);
                roll = SmoothAngle(roll, value.Pose.Roll, 0.28);
                yaw = SmoothAngle(yaw, value.Pose.Yaw, 0.24);
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Rect bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width < 1 || bounds.Height < 1) return;
            Point center = new Point(bounds.Width * 0.5, bounds.Height * 0.49);
            if (!hasDisplayPose)
            {
                dc.DrawEllipse(null, innerPen, center, Math.Min(bounds.Width, bounds.Height) * 0.18, Math.Min(bounds.Width, bounds.Height) * 0.18);
                DrawText(dc, "等待真实 DualSense 运动数据", new Point(center.X, center.Y + 8), 13, Palette.Muted, TextAlignment.Center);
                return;
            }

            double limitedPitch = Clamp(pitch, -65, 65);
            double limitedRoll = Clamp(roll, -70, 70);
            double limitedYaw = Clamp(yaw, -90, 90);
            double yawScale = 0.78 + 0.22 * Math.Cos(limitedYaw * Math.PI / 180.0);
            double pitchSkew = limitedPitch * 0.14;
            double yawSkew = limitedYaw * 0.12;

            dc.PushTransform(new TranslateTransform(center.X, center.Y));
            dc.PushTransform(new RotateTransform(-limitedRoll));
            dc.PushTransform(new SkewTransform(yawSkew, pitchSkew));
            dc.PushTransform(new ScaleTransform(yawScale, 1.0));

            dc.DrawGeometry(bodyBrush, outerPen, ControllerGeometry);
            dc.DrawRoundedRectangle(panelBrush, innerPen, new Rect(-98, -43, 196, 62), 16, 16);
            dc.DrawEllipse(panelBrush, outerPen, new Point(-68, 31), 29, 29);
            dc.DrawEllipse(panelBrush, outerPen, new Point(68, 31), 29, 29);
            dc.DrawRoundedRectangle(accentBrush, null, new Rect(-54, -20, 108, 4), 2, 2);
            dc.DrawLine(outerPen, new Point(0, -75), new Point(0, -111));
            dc.DrawGeometry(accentBrush, null, ArrowGeometry);
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();

            double dialRadius = Math.Min(bounds.Width, bounds.Height) * 0.36;
            dc.DrawEllipse(null, dialPen, center, dialRadius, dialRadius);
            DrawText(dc, "P " + FormatSigned(pitch) + "°", new Point(bounds.Width * 0.5, bounds.Height - 32), 12, Palette.Muted, TextAlignment.Center);
            DrawText(dc, "R " + FormatSigned(roll) + "°", new Point(20, 22), 12, Palette.Muted, TextAlignment.Left);
            DrawText(dc, "Y " + FormatSigned(yaw) + "°", new Point(bounds.Width - 20, 22), 12, Palette.Muted, TextAlignment.Right);
        }

        private static StreamGeometry CreateControllerGeometry()
        {
            StreamGeometry shape = new StreamGeometry();
            using (StreamGeometryContext context = shape.Open())
            {
                context.BeginFigure(new Point(-190, -35), true, true);
                context.BezierTo(new Point(-171, -82), new Point(-102, -94), new Point(-52, -74), true, false);
                context.LineTo(new Point(52, -74), true, false);
                context.BezierTo(new Point(102, -94), new Point(171, -82), new Point(190, -35), true, false);
                context.BezierTo(new Point(204, 4), new Point(178, 90), new Point(137, 98), true, false);
                context.BezierTo(new Point(103, 104), new Point(72, 62), new Point(43, 60), true, false);
                context.LineTo(new Point(-43, 60), true, false);
                context.BezierTo(new Point(-72, 62), new Point(-103, 104), new Point(-137, 98), true, false);
                context.BezierTo(new Point(-178, 90), new Point(-204, 4), new Point(-190, -35), true, false);
            }
            shape.Freeze();
            return shape;
        }

        private static StreamGeometry CreateArrowGeometry(Point center, double radius)
        {
            StreamGeometry arrow = new StreamGeometry();
            using (StreamGeometryContext context = arrow.Open())
            {
                context.BeginFigure(new Point(center.X, center.Y - radius), true, true);
                context.LineTo(new Point(center.X + radius * 0.72, center.Y + radius), true, false);
                context.LineTo(new Point(center.X - radius * 0.72, center.Y + radius), true, false);
            }
            arrow.Freeze();
            return arrow;
        }

        private void DrawText(DrawingContext dc, string text, Point point, double size, Color color, TextAlignment alignment)
        {
            FormattedText formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(LabVisualStyles.UiFont, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), size, new SolidColorBrush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            formatted.TextAlignment = alignment;
            dc.DrawText(formatted, point);
        }

        private static double SmoothAngle(double current, double target, double amount)
        {
            double delta = target - current;
            while (delta > 180) delta -= 360;
            while (delta < -180) delta += 360;
            return current + delta * amount;
        }

        private static string FormatSigned(double value)
        {
            return value >= 0 ? "+" + value.ToString("0.0", CultureInfo.InvariantCulture) : value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }

}
