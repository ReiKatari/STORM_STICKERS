using System;
using Microsoft.UI.Xaml.Data;

namespace STORM_STICKERS
{
    public class StringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is string format)
            {
                // Unescape brackets if they were escaped in XAML
                string cleanFormat = format.Replace("\\{", "{").Replace("\\}", "}");
                return string.Format(cleanFormat, value);
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
