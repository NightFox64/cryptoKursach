using System;
using System.Globalization;
using System.Windows.Data;

namespace ChatClient.Converters
{
    public class SenderNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string content && !string.IsNullOrEmpty(content))
            {
                // Extract sender name from format "(username): message"
                if (content.StartsWith("("))
                {
                    int endIndex = content.IndexOf("): ");
                    if (endIndex != -1)
                    {
                        return content.Substring(1, endIndex - 1); // Extract username without parentheses
                    }
                }
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
