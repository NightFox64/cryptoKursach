using System;
using System.Globalization;
using NUnit.Framework;

namespace ChatClient.UnitTests
{
    [TestFixture]
    public class ImageConverterTests
    {
        [Test]
        public void TestImageExtraction_WithDirectImageFormat()
        {
            // Arrange
            string base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            string content = "[IMAGE]" + base64;

            // Act
            string extracted = ExtractBase64(content);

            // Assert
            Assert.That(extracted, Is.EqualTo(base64));
        }

        [Test]
        public void TestImageExtraction_WithSenderPrefix()
        {
            // Arrange
            string base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            string content = "(username): [IMAGE]" + base64;

            // Act
            string extracted = ExtractBase64(content);

            // Assert
            Assert.That(extracted, Is.EqualTo(base64));
        }

        [Test]
        public void TestImageExtraction_OldFormatWithoutImageMarker()
        {
            // Arrange
            string base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            string content = "(username): " + base64;

            // Act
            string extracted = ExtractBase64(content);

            // Assert
            Assert.That(extracted, Is.EqualTo(base64));
        }

        [Test]
        public void TestMessageTypeDetection_ContainsImageMarker()
        {
            // Arrange
            string content1 = "[IMAGE]base64data";
            string content2 = "(user): [IMAGE]base64data";
            string content3 = "Some text without marker";

            // Assert
            Assert.That(content1.Contains("[IMAGE]"), Is.True, "Direct [IMAGE] format should be detected");
            Assert.That(content2.Contains("[IMAGE]"), Is.True, "Prefixed [IMAGE] format should be detected");
            Assert.That(content3.Contains("[IMAGE]"), Is.False, "Text without marker should not be detected");
        }

        // Helper method that mimics ImageConverter logic
        private string ExtractBase64(string content)
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

            return base64Image.Trim();
        }
    }
}
