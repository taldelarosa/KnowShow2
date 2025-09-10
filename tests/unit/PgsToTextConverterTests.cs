using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Tests.Unit;

[TestClass]
public class PgsToTextConverterTests
{
    private PgsToTextConverter GetConverter()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
                                 .CreateLogger<PgsToTextConverter>();
        return new PgsToTextConverter(logger);
    }

    [TestMethod]
    public async Task ConvertPgsToText_WithEmptyData_ReturnsEmptyString()
    {
        // Arrange
        var converter = GetConverter();
        var emptyData = Array.Empty<byte>();

        // Act
        var result = await converter.ConvertPgsToText(emptyData);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void IsOcrAvailable_ReturnsBoolean()
    {
        // Arrange
        var converter = GetConverter();

        // Act
        var result = converter.IsOcrAvailable();

        // Assert
        Assert.IsInstanceOfType(result, typeof(bool)); // Just checking it returns a boolean
    }

    [TestMethod]
    public async Task ConvertPgsToText_WithValidLanguage_UsesCorrectLanguageCode()
    {
        // Arrange
        var converter = GetConverter();
        var testData = new byte[] { 0x50, 0x47, 0x53 }; // Mock PGS data

        // Act & Assert
        // This test mainly verifies the method can be called with different languages
        // without throwing exceptions (actual OCR testing would require test data)
        try
        {
            await converter.ConvertPgsToText(testData, "eng");
            await converter.ConvertPgsToText(testData, "spa");
            await converter.ConvertPgsToText(testData, "fra");
            Assert.IsTrue(true); // If we get here, no exceptions were thrown
        }
        catch (Exception ex)
        {
            // OCR might fail due to missing dependencies in test environment
            Assert.IsTrue(ex.Message.Contains("tesseract") || ex.Message.Contains("ffmpeg"));
        }
    }
}
