using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Aimmy2.Converter;

public class DoubleToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            Console.WriteLine(doubleValue);
            return new CornerRadius(doubleValue);
        }
        return new CornerRadius(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CornerRadius cornerRadius)
        {
            return cornerRadius.TopLeft; // or another corner, assuming all corners are equal
        }
        return 0.0;
    }
}