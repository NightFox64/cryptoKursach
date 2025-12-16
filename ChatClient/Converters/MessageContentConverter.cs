using System;
using System.Globalization;
using System.Windows.Data;

namespace ChatClient.Converters
{
    public class MessageContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string content && !string.IsNullOrEmpty(content))
            {
                // Extract message content without sender prefix "(username): "
                if (content.StartsWith("("))
                {
                    int endIndex = content.IndexOf("): ");
                    if (endIndex != -1)
                    {
                        return content.Substring(endIndex + 3); // Return content without prefix
                    }
                }
                return content; // Return as-is if no prefix found
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
