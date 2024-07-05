using System.Globalization;
using System.Windows.Data;

namespace Aimmy2.Converter;

public class AddConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is double && values[1] is double)
        {
            return (double)values[0] + (double)values[1];
        }
        return 0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}