using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ControllerLab
{
    // Code-only WPF theme.  The application intentionally stays XAML-free, while
    // still exposing the same reusable visual roles a ResourceDictionary would.
    public static class LabVisualStyles
    {
        public static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");
        public static readonly CornerRadius CardRadius = new CornerRadius(16);
        public static readonly CornerRadius ControlRadius = new CornerRadius(10);
        public static readonly Duration MotionDuration = new Duration(TimeSpan.FromMilliseconds(160));
        public static readonly Style PrimaryButtonStyle = CreateButtonStyle(true);
        public static readonly Style SecondaryButtonStyle = CreateButtonStyle(false);
        public static readonly Style StatusBadgeStyle = CreateBadgeStyle();
        public static readonly Style MetricCardStyle = CreateCardStyle(Palette.Surface2Brush);
        public static readonly Style SectionCardStyle = CreateCardStyle(Palette.SurfaceBrush);
        public static readonly Style PageTitleStyle = CreatePageTitleStyle();
        public static readonly Style SecondaryTextStyle = CreateSecondaryTextStyle();

        public static Border CreateSectionCard(UIElement child)
        {
            return new Border { Style = SectionCardStyle, Child = child };
        }

        public static Border CreateMetricCard(UIElement child)
        {
            return new Border { Style = MetricCardStyle, Child = child };
        }

        public static Border CreateStatusBadge(UIElement child)
        {
            return new Border { Style = StatusBadgeStyle, Child = child };
        }

        public static TextBlock CreatePageTitle(string text)
        {
            return new TextBlock { Text = text, Style = PageTitleStyle };
        }

        public static TextBlock CreateSecondaryText(string text)
        {
            return new TextBlock { Text = text, Style = SecondaryTextStyle };
        }

        public static void FadeIn(UIElement element, bool reducedMotion)
        {
            if (element == null) return;
            if (reducedMotion)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                return;
            }
            element.Opacity = 0;
            DoubleAnimation animation = new DoubleAnimation(0, 1, MotionDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static Style CreateCardStyle(Brush background)
        {
            Style style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.BackgroundProperty, background));
            style.Setters.Add(new Setter(Border.BorderBrushProperty, Palette.BorderSubtleBrush));
            style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, CardRadius));
            style.Setters.Add(new Setter(Border.SnapsToDevicePixelsProperty, true));
            return style;
        }

        private static Style CreateBadgeStyle()
        {
            Style style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.BackgroundProperty, Palette.SurfaceHoverBrush));
            style.Setters.Add(new Setter(Border.BorderBrushProperty, Palette.BorderSubtleBrush));
            style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, ControlRadius));
            style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(9, 4, 9, 4)));
            return style;
        }

        private static Style CreatePageTitleStyle()
        {
            Style style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Palette.TextBrush));
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, UiFont));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 28.0));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            return style;
        }

        private static Style CreateSecondaryTextStyle()
        {
            Style style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Palette.MutedBrush));
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, UiFont));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));
            return style;
        }

        private static Style CreateButtonStyle(bool primary)
        {
            Style style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, UiFont));
            style.Setters.Add(new Setter(Control.ForegroundProperty, primary ? Palette.WindowBrush : Palette.TextBrush));
            style.Setters.Add(new Setter(Control.BackgroundProperty, primary ? Palette.BlueBrush : Palette.SurfaceHoverBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, primary ? Palette.BlueBrush : Palette.BorderSubtleBrush));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 7, 12, 7)));
            style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
            style.Setters.Add(new Setter(Control.TemplateProperty, CreateButtonTemplate(primary)));
            return style;
        }

        private static ControlTemplate CreateButtonTemplate(bool primary)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Root";
            border.SetValue(Border.CornerRadiusProperty, ControlRadius);
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            Trigger hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, primary ? Palette.AccentHoverBrush : Palette.SurfaceRaisedBrush, "Root"));
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, primary ? Palette.AccentHoverBrush : Palette.BlueBrush, "Root"));
            template.Triggers.Add(hover);
            Trigger pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.86, "Root"));
            template.Triggers.Add(pressed);
            Trigger disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.42, "Root"));
            template.Triggers.Add(disabled);
            return template;
        }
    }
}
