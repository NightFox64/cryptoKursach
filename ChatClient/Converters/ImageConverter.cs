using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ChatClient.Converters
{
    public class ImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string content && !string.IsNullOrEmpty(content))
            {
                string base64Image = content;
                
                // Find and extract [IMAGE] block
                int imageIndex = base64Image.IndexOf("[IMAGE]");
                if (imageIndex != -1)
                {
                    // Extract everything after [IMAGE]
                    base64Image = base64Image.Substring(imageIndex + "[IMAGE]".Length);
                }
                else
                {
                    // Fallback: Remove sender prefix like "(user): " if [IMAGE] not found
                    if (base64Image.StartsWith("("))
                    {
                        int prefixEndIndex = base64Image.IndexOf("): ");
                        if (prefixEndIndex != -1)
                        {
                            base64Image = base64Image.Substring(prefixEndIndex + 3);
                        }
                    }
                }

                try
                {
                    byte[] imageBytes = System.Convert.FromBase64String(base64Image.Trim());
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = ms;
                        image.EndInit();
                        return image;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to convert image: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
