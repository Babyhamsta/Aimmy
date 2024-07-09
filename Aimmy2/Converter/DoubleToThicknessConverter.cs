using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Aimmy2.Converter;

public class DoubleToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return new Thickness(doubleValue);
        }
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Thickness thickness)
        {
            return thickness.Left; // or another side, assuming all sides are equal
        }
        return 0.0;
    }
}