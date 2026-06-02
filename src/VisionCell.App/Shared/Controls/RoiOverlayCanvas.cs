using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using VisionCell.App.Shared.ViewModels;

namespace VisionCell.App.Shared.Controls;

public sealed class RoiOverlayCanvas : FrameworkElement
{
    public static readonly DependencyProperty OverlayItemsProperty = DependencyProperty.Register(
        nameof(OverlayItems),
        typeof(IEnumerable),
        typeof(RoiOverlayCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ImagePixelWidthProperty = DependencyProperty.Register(
        nameof(ImagePixelWidth),
        typeof(double),
        typeof(RoiOverlayCanvas),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ImagePixelHeightProperty = DependencyProperty.Register(
        nameof(ImagePixelHeight),
        typeof(double),
        typeof(RoiOverlayCanvas),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowLabelsProperty = DependencyProperty.Register(
        nameof(ShowLabels),
        typeof(bool),
        typeof(RoiOverlayCanvas),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface LabelTypeface = new("Segoe UI");
    private static readonly Brush DefectBrush = CreateFrozenBrush(Color.FromRgb(239, 68, 68));
    private static readonly Brush WarningBrush = CreateFrozenBrush(Color.FromRgb(245, 158, 11));
    private static readonly Brush ReadyBrush = CreateFrozenBrush(Color.FromRgb(34, 197, 94));
    private static readonly Brush DefaultBrush = CreateFrozenBrush(Color.FromRgb(56, 189, 248));
    private static readonly Brush LabelBackgroundBrush = CreateFrozenBrush(Color.FromArgb(210, 11, 18, 32));
    private static readonly Brush LabelTextBrush = CreateFrozenBrush(Colors.White);

    public IEnumerable? OverlayItems
    {
        get => (IEnumerable?)GetValue(OverlayItemsProperty);
        set => SetValue(OverlayItemsProperty, value);
    }

    public double ImagePixelWidth
    {
        get => (double)GetValue(ImagePixelWidthProperty);
        set => SetValue(ImagePixelWidthProperty, value);
    }

    public double ImagePixelHeight
    {
        get => (double)GetValue(ImagePixelHeightProperty);
        set => SetValue(ImagePixelHeightProperty, value);
    }

    public bool ShowLabels
    {
        get => (bool)GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (OverlayItems is null ||
            ImagePixelWidth <= 0 ||
            ImagePixelHeight <= 0 ||
            ActualWidth <= 0 ||
            ActualHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(ActualWidth / ImagePixelWidth, ActualHeight / ImagePixelHeight);
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return;
        }

        var renderedWidth = ImagePixelWidth * scale;
        var renderedHeight = ImagePixelHeight * scale;
        var originX = (ActualWidth - renderedWidth) / 2.0;
        var originY = (ActualHeight - renderedHeight) / 2.0;
        var dpi = VisualTreeHelper.GetDpi(this);

        foreach (var item in OverlayItems.OfType<RoiOverlayItemViewModel>())
        {
            var rect = ProjectToViewport(item, scale, originX, originY);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            var brush = ResolveBrush(item.State);
            var pen = new Pen(brush, 2.0);
            pen.Freeze();
            drawingContext.DrawRectangle(null, pen, rect);

            if (ShowLabels && item.HasLabel)
            {
                DrawLabel(drawingContext, item, rect, brush, dpi.PixelsPerDip);
            }
        }
    }

    private Rect ProjectToViewport(
        RoiOverlayItemViewModel item,
        double scale,
        double originX,
        double originY)
    {
        var left = Math.Clamp(item.X, 0, ImagePixelWidth);
        var top = Math.Clamp(item.Y, 0, ImagePixelHeight);
        var right = Math.Clamp(item.X + Math.Max(0, item.Width), 0, ImagePixelWidth);
        var bottom = Math.Clamp(item.Y + Math.Max(0, item.Height), 0, ImagePixelHeight);

        return new Rect(
            originX + left * scale,
            originY + top * scale,
            Math.Max(0, right - left) * scale,
            Math.Max(0, bottom - top) * scale);
    }

    private static void DrawLabel(
        DrawingContext drawingContext,
        RoiOverlayItemViewModel item,
        Rect rect,
        Brush accentBrush,
        double pixelsPerDip)
    {
        var label = TrimLabel(item.DisplayText);
        var text = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            11,
            LabelTextBrush,
            pixelsPerDip);

        var textOrigin = new Point(rect.Left + 4, Math.Max(0, rect.Top - text.Height - 5));
        var background = new Rect(
            textOrigin.X - 3,
            textOrigin.Y - 2,
            text.Width + 6,
            text.Height + 4);

        drawingContext.DrawRoundedRectangle(LabelBackgroundBrush, null, background, 3, 3);
        drawingContext.DrawRectangle(accentBrush, null, new Rect(background.Left, background.Top, 3, background.Height));
        drawingContext.DrawText(text, textOrigin);
    }

    private static string TrimLabel(string value)
    {
        const int MaxLength = 32;
        return value.Length <= MaxLength
            ? value
            : string.Concat(value.AsSpan(0, MaxLength - 1), "...");
    }

    private static Brush ResolveBrush(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return DefaultBrush;
        }

        if (state.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("defect", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("scratch", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("ng", StringComparison.OrdinalIgnoreCase))
        {
            return DefectBrush;
        }

        if (state.Contains("warn", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("offset", StringComparison.OrdinalIgnoreCase))
        {
            return WarningBrush;
        }

        if (state.Contains("pass", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
            state.Contains("ok", StringComparison.OrdinalIgnoreCase))
        {
            return ReadyBrush;
        }

        return DefaultBrush;
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
