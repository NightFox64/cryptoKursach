using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;

namespace ChatClient.Converters
{
    public class MessageTypeToTemplateConverter : IValueConverter
    {
        public DataTemplate? TextMessageTemplate { get; set; }
        public DataTemplate? ImageMessageTemplate { get; set; }
        public DataTemplate? FileMessageTemplate { get; set; }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string messageContent)
            {
                if (messageContent.StartsWith("[IMAGE]") && ImageMessageTemplate != null)
                {
                    return ImageMessageTemplate;
                }
                else if (messageContent.StartsWith("[FILE]") && FileMessageTemplate != null)
                {
                    return FileMessageTemplate;
                }
                else if (TextMessageTemplate != null)
                {
                    return TextMessageTemplate;
                }
            }
            return TextMessageTemplate; // Default to text, if it's not null
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
