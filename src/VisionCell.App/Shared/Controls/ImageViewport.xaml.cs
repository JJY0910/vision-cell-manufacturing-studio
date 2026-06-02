using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
}
