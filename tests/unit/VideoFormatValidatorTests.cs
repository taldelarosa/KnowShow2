using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Unit;

[TestClass]
public class VideoFormatValidatorTests
{
    private VideoFormatValidator GetValidator()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
                                 .CreateLogger<VideoFormatValidator>();
        return new VideoFormatValidator(logger);
    }

    [TestMethod]
    public async Task IsAV1Encoded_WithNonexistentFile_ReturnsFalse()
    {
        // Arrange
        var validator = GetValidator();
        var nonexistentPath = "/path/to/nonexistent/file.mkv";

        // Act
        var result = await validator.IsAV1Encoded(nonexistentPath);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetSubtitleTracks_WithNonexistentFile_ReturnsEmptyList()
    {
        // Arrange
        var validator = GetValidator();
        var nonexistentPath = "/path/to/nonexistent/file.mkv";

        // Act
        var tracks = await validator.GetSubtitleTracks(nonexistentPath);

        // Assert
        Assert.IsNotNull(tracks);
        Assert.AreEqual(0, tracks.Count);
    }
}
