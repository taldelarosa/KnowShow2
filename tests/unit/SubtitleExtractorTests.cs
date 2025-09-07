using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Unit;

[TestClass]
public class SubtitleExtractorTests
{
    private SubtitleExtractor GetExtractor()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var validator = new VideoFormatValidator(loggerFactory.CreateLogger<VideoFormatValidator>());
        return new SubtitleExtractor(loggerFactory.CreateLogger<SubtitleExtractor>(), validator);
    }

    [TestMethod]
    public async Task ExtractPgsSubtitles_WithNonexistentFile_ReturnsEmptyArray()
    {
        // Arrange
        var extractor = GetExtractor();
        var nonexistentPath = "/path/to/nonexistent/file.mkv";

        // Act
        var result = await extractor.ExtractPgsSubtitles(nonexistentPath);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public async Task ExtractPgsSubtitles_WithLanguagePreference_CallsWithCorrectParameters()
    {
        // Arrange
        var extractor = GetExtractor();
        var nonexistentPath = "/path/to/nonexistent/file.mkv";
        var preferredLanguage = "eng";

        // Act
        var result = await extractor.ExtractPgsSubtitles(nonexistentPath, preferredLanguage);

        // Assert
        Assert.IsNotNull(result);
        // Since file doesn't exist, we expect empty result but method should not throw
    }
}
