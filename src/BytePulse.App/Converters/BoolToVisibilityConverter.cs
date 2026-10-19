using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace BytePulse.Converters;

/// <summary>Shows an element when a boolean is true.</summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool flag = value is bool boolean && boolean;

        // A parameter of "invert" flips the result.
        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}
