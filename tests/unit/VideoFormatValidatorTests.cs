using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using Xunit;

namespace EpisodeIdentifier.Tests.Unit;

public class VideoFormatValidatorTests
{
    private VideoFormatValidator GetValidator()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
                                 .CreateLogger<VideoFormatValidator>();
        return new VideoFormatValidator(logger);
    }

    [Fact]
    public async Task IsAV1Encoded_WithNonexistentFile_ReturnsFalse()
    {
        // Arrange
        var validator = GetValidator();
        var nonexistentPath = "/path/to/nonexistent/file.mkv";

        // Act
        var result = await validator.IsAV1Encoded(nonexistentPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetSubtitleTracks_WithNonexistentFile_ReturnsEmptyList()
    {
        // Arrange
        var validator = GetValidator();
        var nonexistentPath = "/path/to/nonexistent/file.mkv";

        // Act
        var tracks = await validator.GetSubtitleTracks(nonexistentPath);

        // Assert
        Assert.NotNull(tracks);
        Assert.Equal(0, tracks.Count);
    }
}
