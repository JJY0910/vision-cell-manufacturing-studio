using System.Windows;
using System.Windows.Controls;

namespace VisionCell.App.Shared.Controls;

public partial class CommandBar : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(object),
        typeof(CommandBar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(object),
        typeof(CommandBar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty AlertMessageProperty = DependencyProperty.Register(
        nameof(AlertMessage),
        typeof(object),
        typeof(CommandBar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HasAlertProperty = DependencyProperty.Register(
        nameof(HasAlert),
        typeof(bool),
        typeof(CommandBar),
        new PropertyMetadata(false));

    public static readonly DependencyProperty CommandContentProperty = DependencyProperty.Register(
        nameof(CommandContent),
        typeof(object),
        typeof(CommandBar),
        new PropertyMetadata(null));

    public CommandBar()
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

    public object? AlertMessage
    {
        get => GetValue(AlertMessageProperty);
        set => SetValue(AlertMessageProperty, value);
    }

    public bool HasAlert
    {
        get => (bool)GetValue(HasAlertProperty);
        set => SetValue(HasAlertProperty, value);
    }

    public object? CommandContent
    {
        get => GetValue(CommandContentProperty);
        set => SetValue(CommandContentProperty, value);
    }
}
