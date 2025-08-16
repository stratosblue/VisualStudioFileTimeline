using System.Globalization;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;

namespace VisualStudioFileTimeline.Converters;

public class Int32ToVisibilityConverter : ValueConverter<int, Visibility>
{
    #region Protected 方法

    protected override Visibility Convert(int value, object parameter, CultureInfo culture)
    {
        if (parameter is string target
            && int.TryParse(target, out var targetValue))
        {
            return value == targetValue
                   ? Visibility.Visible
                   : Visibility.Collapsed;
        }

        return value != 0
               ? Visibility.Visible
               : Visibility.Collapsed;
    }

    protected override int ConvertBack(Visibility value, object parameter, CultureInfo culture)
    {
        if (!int.TryParse(parameter as string, out var targetValue))
        {
            targetValue = 1;
        }

        return value == Visibility.Visible
               ? targetValue
               : 0;
    }

    #endregion Protected 方法
}
