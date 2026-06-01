using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VisionCell.Core.Events;

namespace VisionCell.App.Shared.Converters;

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var resourceKey = value switch
        {
            SystemEventSeverity.Alarm => "Brush.Alarm",
            SystemEventSeverity.Error => "Brush.Alarm",
            SystemEventSeverity.Warning => "Brush.Warning",
            SystemEventSeverity.Trace => "Brush.Moving",
            _ => "Brush.Ready"
        };

        return System.Windows.Application.Current.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
