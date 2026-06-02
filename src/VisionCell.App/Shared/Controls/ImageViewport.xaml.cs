using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VisionCell.App.Shared.ViewModels;

namespace VisionCell.App.Shared.Controls;

public partial class ImageViewport : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(object),
        typeof(ImageViewport),
        new PropertyMetadata(null));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(object),
        typeof(ImageViewport),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource),
        typeof(ImageSource),
        typeof(ImageViewport),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ViewportMinHeightProperty = DependencyProperty.Register(
        nameof(ViewportMinHeight),
        typeof(double),
        typeof(ImageViewport),
        new PropertyMetadata(160.0));

    public static readonly DependencyProperty OverlayItemsProperty = DependencyProperty.Register(
        nameof(OverlayItems),
        typeof(IEnumerable<RoiOverlayItemViewModel>),
        typeof(ImageViewport),
        new PropertyMetadata(Array.Empty<RoiOverlayItemViewModel>()));

    public static readonly DependencyProperty ImagePixelWidthProperty = DependencyProperty.Register(
        nameof(ImagePixelWidth),
        typeof(double),
        typeof(ImageViewport),
        new PropertyMetadata(0.0));

    public static readonly DependencyProperty ImagePixelHeightProperty = DependencyProperty.Register(
        nameof(ImagePixelHeight),
        typeof(double),
        typeof(ImageViewport),
        new PropertyMetadata(0.0));

    public static readonly DependencyProperty ShowOverlayLabelsProperty = DependencyProperty.Register(
        nameof(ShowOverlayLabels),
        typeof(bool),
        typeof(ImageViewport),
        new PropertyMetadata(true));

    public ImageViewport()
    {
        InitializeComponent();
    }

    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public double ViewportMinHeight
    {
        get => (double)GetValue(ViewportMinHeightProperty);
        set => SetValue(ViewportMinHeightProperty, value);
    }

    public IEnumerable<RoiOverlayItemViewModel> OverlayItems
    {
        get => (IEnumerable<RoiOverlayItemViewModel>)GetValue(OverlayItemsProperty);
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

    public bool ShowOverlayLabels
    {
        get => (bool)GetValue(ShowOverlayLabelsProperty);
        set => SetValue(ShowOverlayLabelsProperty, value);
    }
}
