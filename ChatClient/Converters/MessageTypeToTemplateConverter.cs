using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using ChatClient.Shared.Models; // Added for Message model

namespace ChatClient.Converters
{
    public class MessageTypeToTemplateConverter : DataTemplateSelector
    {
        public DataTemplate? TextMessageTemplate { get; set; }
        public DataTemplate? ImageMessageTemplate { get; set; }
        public DataTemplate? FileMessageTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is Message message)
            {
                if (message.Content != null)
                {
                    if (message.Content.StartsWith("[IMAGE]") && ImageMessageTemplate != null)
                    {
                        return ImageMessageTemplate;
                    }
                    else if (message.Content.StartsWith("[FILE]") && FileMessageTemplate != null)
                    {
                        return FileMessageTemplate;
                    }
                    else if (TextMessageTemplate != null)
                    {
                        return TextMessageTemplate;
                    }
                }
            }
            return TextMessageTemplate; // Default to text, if it's not null
        }
    }
}
