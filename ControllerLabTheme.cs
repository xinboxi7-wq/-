using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ControllerLab
{
    // Shared product tokens for the code-only WPF interface. Keeping these in one
    // place prevents test pages from inventing their own spacing and state colors.
    public static class LabSpacing
    {
        public const double Xs = 4;
        public const double Small = 8;
        public const double Medium = 12;
        public const double Large = 16;
        public const double Xl = 24;
        public const double Xxl = 32;
    }

    public static class LabFontSizes
    {
        public const double Caption = 11;
        public const double Body = 13;
        public const double BodyLarge = 15;
        public const double Section = 18;
        public const double Title = 28;
        public const double Hero = 36;
    }

    public static class LabMotion
    {
        public static readonly Duration Fast = new Duration(TimeSpan.FromMilliseconds(150));
        public static readonly Duration Standard = new Duration(TimeSpan.FromMilliseconds(200));
        public static readonly Duration Slow = new Duration(TimeSpan.FromMilliseconds(280));
        public const double GlowOpacity = 0.32;
    }

    public enum LabButtonVariant { Primary, Secondary, Ghost, Danger, Icon }
    public enum LabCardVariant { Default, Test, Result, Device }
    public enum LabStatusKind { Neutral, Info, Success, Warning, Error }

    public static class LabVisualStyles
    {
        public static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");
        public static readonly CornerRadius CardRadius = new CornerRadius(16);
        public static readonly CornerRadius ControlRadius = new CornerRadius(10);
        public static readonly CornerRadius CompactRadius = new CornerRadius(8);
        public static readonly Duration MotionDuration = LabMotion.Standard;

        public static readonly Style PrimaryButtonStyle = CreateButtonStyle(LabButtonVariant.Primary);
        public static readonly Style SecondaryButtonStyle = CreateButtonStyle(LabButtonVariant.Secondary);
        public static readonly Style GhostButtonStyle = CreateButtonStyle(LabButtonVariant.Ghost);
        public static readonly Style DangerButtonStyle = CreateButtonStyle(LabButtonVariant.Danger);
        public static readonly Style IconButtonStyle = CreateButtonStyle(LabButtonVariant.Icon);

        public static readonly Style StatusBadgeStyle = CreateBadgeStyle();
        public static readonly Style MetricCardStyle = CreateCardStyle(LabCardVariant.Result);
        public static readonly Style SectionCardStyle = CreateCardStyle(LabCardVariant.Default);
        public static readonly Style TestCardStyle = CreateCardStyle(LabCardVariant.Test);
        public static readonly Style ResultCardStyle = CreateCardStyle(LabCardVariant.Result);
        public static readonly Style DeviceCardStyle = CreateCardStyle(LabCardVariant.Device);
        public static readonly Style PageTitleStyle = CreatePageTitleStyle();
        public static readonly Style SecondaryTextStyle = CreateSecondaryTextStyle();

        public static Border CreateSectionCard(UIElement child) { return new Border { Style = SectionCardStyle, Child = child }; }
        public static Border CreateMetricCard(UIElement child) { return new Border { Style = MetricCardStyle, Child = child }; }
        public static Border CreateTestCard(UIElement child) { return new Border { Style = TestCardStyle, Child = child }; }
        public static Border CreateResultCard(UIElement child) { return new Border { Style = ResultCardStyle, Child = child }; }
        public static Border CreateDeviceCard(UIElement child) { return new Border { Style = DeviceCardStyle, Child = child }; }
        public static Border CreateStatusBadge(UIElement child) { return new Border { Style = StatusBadgeStyle, Child = child }; }

        public static Border CreateStatusBadge(string text, LabStatusKind kind)
        {
            TextBlock label = new TextBlock { Text = text, FontFamily = UiFont, FontSize = LabFontSizes.Caption, FontWeight = FontWeights.SemiBold };
            Border badge = CreateStatusBadge(label);
            ApplyStatus(badge, label, kind);
            AutomationProperties.SetName(badge, "状态：" + text);
            return badge;
        }

        public static Border CreateInstructionCard(string title, string body, string step)
        {
            Grid layout = new Grid { Margin = new Thickness(18, 15, 18, 15) };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border number = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(42, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)),
                Child = new TextBlock { Text = step, Foreground = Palette.BlueBrush, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            layout.Children.Add(number);
            StackPanel copy = new StackPanel();
            copy.Children.Add(new TextBlock { Text = title, Foreground = Palette.TextBrush, FontSize = LabFontSizes.BodyLarge, FontWeight = FontWeights.SemiBold });
            copy.Children.Add(new TextBlock { Text = body, Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Body, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetColumn(copy, 1);
            layout.Children.Add(copy);
            return CreateTestCard(layout);
        }

        public static TextBlock CreatePageTitle(string text) { return new TextBlock { Text = text, Style = PageTitleStyle }; }
        public static TextBlock CreateSecondaryText(string text) { return new TextBlock { Text = text, Style = SecondaryTextStyle }; }

        public static Button CreateButton(string text, LabButtonVariant variant)
        {
            Button button = new Button { Content = text, Style = StyleForButton(variant), MinHeight = 36 };
            AutomationProperties.SetName(button, text);
            AutomationProperties.SetHelpText(button, "按 Enter 或空格键执行");
            return button;
        }

        public static Style StyleForButton(LabButtonVariant variant)
        {
            if (variant == LabButtonVariant.Primary) return PrimaryButtonStyle;
            if (variant == LabButtonVariant.Ghost) return GhostButtonStyle;
            if (variant == LabButtonVariant.Danger) return DangerButtonStyle;
            if (variant == LabButtonVariant.Icon) return IconButtonStyle;
            return SecondaryButtonStyle;
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
            DoubleAnimation animation = new DoubleAnimation(0, 1, LabMotion.Standard)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        public static void ApplyStatus(Border badge, TextBlock label, LabStatusKind kind)
        {
            if (badge == null || label == null) return;
            Color color = Palette.Muted;
            if (kind == LabStatusKind.Info) color = Palette.Blue;
            else if (kind == LabStatusKind.Success) color = Palette.Green;
            else if (kind == LabStatusKind.Warning) color = Palette.Warning;
            else if (kind == LabStatusKind.Error) color = Palette.Red;
            label.Foreground = new SolidColorBrush(color);
            badge.BorderBrush = new SolidColorBrush(Color.FromArgb(105, color.R, color.G, color.B));
            badge.Background = new SolidColorBrush(Color.FromArgb(26, color.R, color.G, color.B));
        }

        private static Style CreateCardStyle(LabCardVariant variant)
        {
            Brush background = variant == LabCardVariant.Default ? Palette.SurfaceBrush : Palette.Surface2Brush;
            if (variant == LabCardVariant.Device) background = Palette.SurfaceRaisedBrush;
            Style style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.BackgroundProperty, background));
            style.Setters.Add(new Setter(Border.BorderBrushProperty, variant == LabCardVariant.Test ? Palette.BorderBrush : Palette.BorderSubtleBrush));
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
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, CompactRadius));
            style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(9, 4, 9, 4)));
            return style;
        }

        private static Style CreatePageTitleStyle()
        {
            Style style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Palette.TextBrush));
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, UiFont));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, LabFontSizes.Title));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            return style;
        }

        private static Style CreateSecondaryTextStyle()
        {
            Style style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Palette.MutedBrush));
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, UiFont));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, LabFontSizes.Body));
            return style;
        }

        private static Style CreateButtonStyle(LabButtonVariant variant)
        {
            bool primary = variant == LabButtonVariant.Primary;
            bool danger = variant == LabButtonVariant.Danger;
            bool ghost = variant == LabButtonVariant.Ghost || variant == LabButtonVariant.Icon;
            Brush foreground = primary ? Palette.WindowBrush : danger ? Palette.RedBrush : Palette.TextBrush;
            Brush background = primary ? Palette.BlueBrush : ghost ? Brushes.Transparent : Palette.SurfaceHoverBrush;
            Brush border = primary ? Palette.BlueBrush : danger ? Palette.RedBrush : ghost ? Brushes.Transparent : Palette.BorderSubtleBrush;
            Style style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, UiFont));
            style.Setters.Add(new Setter(Control.FontSizeProperty, LabFontSizes.Body));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            style.Setters.Add(new Setter(Control.BackgroundProperty, background));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, variant == LabButtonVariant.Icon ? new Thickness(9) : new Thickness(13, 7, 13, 7)));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 34.0));
            style.Setters.Add(new Setter(Control.TemplateProperty, CreateButtonTemplate(variant)));
            return style;
        }

        private static ControlTemplate CreateButtonTemplate(LabButtonVariant variant)
        {
            bool primary = variant == LabButtonVariant.Primary;
            bool danger = variant == LabButtonVariant.Danger;
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Root";
            border.SetValue(Border.CornerRadiusProperty, variant == LabButtonVariant.Icon ? CompactRadius : ControlRadius);
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
            hover.Setters.Add(new Setter(Border.BackgroundProperty, primary ? Palette.AccentHoverBrush : danger ? new SolidColorBrush(Color.FromArgb(28, Palette.Red.R, Palette.Red.G, Palette.Red.B)) : Palette.SurfaceRaisedBrush, "Root"));
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, danger ? Palette.RedBrush : primary ? Palette.AccentHoverBrush : Palette.BlueBrush, "Root"));
            template.Triggers.Add(hover);
            Trigger focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(new Setter(Border.BorderBrushProperty, Palette.BlueBrush, "Root"));
            focused.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "Root"));
            template.Triggers.Add(focused);
            Trigger pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.84, "Root"));
            template.Triggers.Add(pressed);
            Trigger disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.38, "Root"));
            disabled.Setters.Add(new Setter(Control.CursorProperty, Cursors.Arrow));
            template.Triggers.Add(disabled);
            return template;
        }
    }
}
