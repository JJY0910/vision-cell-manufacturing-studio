using System.Windows;
using System.Windows.Controls;

namespace VisionCell.App.Shared.Controls;

public partial class EmptyState : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(EmptyState),
        new PropertyMetadata("No records"));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail),
        typeof(string),
        typeof(EmptyState),
        new PropertyMetadata("Refresh or select a record to continue."));

    public static readonly DependencyProperty HasItemsProperty = DependencyProperty.Register(
        nameof(HasItems),
        typeof(bool),
        typeof(EmptyState),
        new PropertyMetadata(false));

    public EmptyState()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public bool HasItems
    {
        get => (bool)GetValue(HasItemsProperty);
        set => SetValue(HasItemsProperty, value);
    }
}
