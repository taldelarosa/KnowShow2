using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using Xunit;

namespace EpisodeIdentifier.Tests.Unit;

public class PgsTrackSelectorTests
{
    [Fact]
    public void SelectBestTrack_WithNoTracks_ThrowsArgumentException()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PgsTrackSelector.SelectBestTrack(tracks));
    }

    [Fact]
    public void SelectBestTrack_WithNullTracks_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PgsTrackSelector.SelectBestTrack(null!));
    }

    [Fact]
    public void SelectBestTrack_WithPreferredLanguageMatch_ReturnsMatchingTrack()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 0, Language = "spa" },
            new() { Index = 1, Language = "eng" },
            new() { Index = 2, Language = "fra" }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks, "fra");

        // Assert
        Assert.Equal(2, result.Index);
        Assert.Equal("fra", result.Language);
    }

    [Fact]
    public void SelectBestTrack_WithPreferredLanguageCaseInsensitive_ReturnsMatchingTrack()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 0, Language = "SPA" },
            new() { Index = 1, Language = "ENG" },
            new() { Index = 2, Language = "FRA" }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks, "eng");

        // Assert
        Assert.Equal(1, result.Index);
        Assert.Equal("ENG", result.Language);
    }

    [Fact]
    public void SelectBestTrack_WithPreferredLanguageNotFound_FallsBackToEnglish()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 0, Language = "spa" },
            new() { Index = 1, Language = "eng" },
            new() { Index = 2, Language = "fra" }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks, "deu");

        // Assert
        Assert.Equal(1, result.Index);
        Assert.Equal("eng", result.Language);
    }

    [Fact]
    public void SelectBestTrack_WithNoPreferredLanguage_DefaultsToEnglish()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 0, Language = "spa" },
            new() { Index = 1, Language = "eng" },
            new() { Index = 2, Language = "fra" }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks);

        // Assert
        Assert.Equal(1, result.Index);
        Assert.Equal("eng", result.Language);
    }

    [Fact]
    public void SelectBestTrack_WithEnglishVariants_ReturnsEnglishTrack()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 0, Language = "spa" },
            new() { Index = 1, Language = "en" },
            new() { Index = 2, Language = "fra" }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks);

        // Assert
        Assert.Equal(1, result.Index);
        Assert.Equal("en", result.Language);
    }

    [Fact]
    public void SelectBestTrack_WithEnglishFullName_ReturnsEnglishTrack()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 0, Language = "spa" },
            new() { Index = 1, Language = "english" },
            new() { Index = 2, Language = "fra" }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks);

        // Assert
        Assert.Equal(1, result.Index);
        Assert.Equal("english", result.Language);
    }

    [Fact]
    public void SelectBestTrack_WithNoEnglish_ReturnsFirstTrack()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 0, Language = "spa" },
            new() { Index = 1, Language = "fra" },
            new() { Index = 2, Language = "deu" }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks);

        // Assert
        Assert.Equal(0, result.Index);
        Assert.Equal("spa", result.Language);
    }

    [Fact]
    public void SelectBestTrack_WithSingleTrack_ReturnsThatTrack()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 5, Language = "spa" }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks);

        // Assert
        Assert.Equal(5, result.Index);
        Assert.Equal("spa", result.Language);
    }

    [Fact]
    public void SelectBestTrack_WithNullLanguages_ReturnsFirstTrack()
    {
        // Arrange
        var tracks = new List<SubtitleTrackInfo>
        {
            new() { Index = 0, Language = null },
            new() { Index = 1, Language = null }
        };

        // Act
        var result = PgsTrackSelector.SelectBestTrack(tracks);

        // Assert
        Assert.Equal(0, result.Index);
        Assert.Null(result.Language);
    }
}