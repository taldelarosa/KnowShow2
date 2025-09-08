using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Tests.Contract;

public class TextSubtitleExtractorContractTests
{
    private readonly ITextSubtitleExtractor _extractor;

    public TextSubtitleExtractorContractTests()
    {
        // This will fail until ITextSubtitleExtractor is implemented
        _extractor = new TextSubtitleExtractor();
    }

    [Fact]
    public async Task DetectTextSubtitleTracksAsync_WithValidVideoContainingSrtTracks_ReturnsNonEmptyList()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_srt.mkv";

        // Act
        var result = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(track => 
        {
            track.Index.Should().BeGreaterOrEqualTo(0);
            track.Format.Should().Be(SubtitleFormat.SRT);
        });
    }

    [Fact]
    public async Task DetectTextSubtitleTracksAsync_WithValidVideoNoTextTracks_ReturnsEmptyList()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/video_with_only_pgs.mkv";

        // Act
        var result = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectTextSubtitleTracksAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var videoFilePath = "/path/to/nonexistent/file.mkv";

        // Act & Assert
        await FluentActions.Invoking(() => _extractor.DetectTextSubtitleTracksAsync(videoFilePath))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DetectTextSubtitleTracksAsync_WithInvalidPath_ThrowsArgumentException(string invalidPath)
    {
        // Act & Assert
        await FluentActions.Invoking(() => _extractor.DetectTextSubtitleTracksAsync(invalidPath))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExtractTextSubtitleContentAsync_WithValidSrtTrack_ReturnsContentWithTextAndMetadata()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_srt.mkv";
        var track = new TextSubtitleTrack
        {
            Index = 0,
            Format = SubtitleFormat.SRT,
            Language = "en",
            IsDefault = true,
            IsForced = false
        };

        // Act
        var result = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, track);

        // Assert
        result.Should().NotBeNull();
        result.SourceTrack.Should().Be(track);
        result.ExtractedText.Should().NotBeNullOrWhiteSpace();
        result.LineCount.Should().BeGreaterThan(0);
        result.Encoding.Should().NotBeNullOrEmpty();
        result.ExtractedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ExtractTextSubtitleContentAsync_WithValidAssTrack_ReturnsContentWithDialogueTextOnly()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_ass.mkv";
        var track = new TextSubtitleTrack
        {
            Index = 0,
            Format = SubtitleFormat.ASS,
            Language = "ja",
            IsDefault = true,
            IsForced = false
        };

        // Act
        var result = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, track);

        // Assert
        result.Should().NotBeNull();
        result.SourceTrack.Should().Be(track);
        result.ExtractedText.Should().NotBeNullOrWhiteSpace();
        result.ExtractedText.Should().NotContain("Dialogue:");
        result.ExtractedText.Should().NotContain("Format:");
        result.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExtractTextSubtitleContentAsync_WithValidVttTrack_ReturnsContentWithCueText()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_vtt.mkv";
        var track = new TextSubtitleTrack
        {
            Index = 0,
            Format = SubtitleFormat.VTT,
            Language = "es",
            IsDefault = false,
            IsForced = false
        };

        // Act
        var result = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, track);

        // Assert
        result.Should().NotBeNull();
        result.SourceTrack.Should().Be(track);
        result.ExtractedText.Should().NotBeNullOrWhiteSpace();
        result.ExtractedText.Should().NotContain("WEBVTT");
        result.ExtractedText.Should().NotContain("NOTE");
        result.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExtractTextSubtitleContentAsync_WithInvalidTrackIndex_ThrowsArgumentException()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_srt.mkv";
        var invalidTrack = new TextSubtitleTrack
        {
            Index = 999,
            Format = SubtitleFormat.SRT,
            Language = "en",
            IsDefault = false,
            IsForced = false
        };

        // Act & Assert
        await FluentActions.Invoking(() => _extractor.ExtractTextSubtitleContentAsync(videoFilePath, invalidTrack))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExtractTextSubtitleContentAsync_WithCorruptedSubtitleData_ThrowsInvalidDataException()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/corrupted_subtitles.mkv";
        var track = new TextSubtitleTrack
        {
            Index = 0,
            Format = SubtitleFormat.SRT,
            Language = "en",
            IsDefault = true,
            IsForced = false
        };

        // Act & Assert
        await FluentActions.Invoking(() => _extractor.ExtractTextSubtitleContentAsync(videoFilePath, track))
            .Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task TryExtractAllTextSubtitlesAsync_WithVideoContainingMatchingSubtitles_ReturnsSuccessfulResult()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_srt.mkv";
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.SubtitleSource.Should().Be(SubtitleSourceType.TextBased);
        result.ProcessedTracks.Should().NotBeEmpty();
        result.SuccessfulTrack.Should().NotBeNull();
        result.SuccessfulTrack.Should().BeOneOf(result.ProcessedTracks);
        result.ExtractionErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task TryExtractAllTextSubtitlesAsync_WithVideoContainingNonMatchingSubtitles_ReturnsUnsuccessfulResult()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/unknown_episode.mkv";
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.SubtitleSource.Should().Be(SubtitleSourceType.TextBased);
        result.ProcessedTracks.Should().NotBeEmpty();
        result.SuccessfulTrack.Should().BeNull();
        result.ExtractionErrors.Should().BeEmpty(); // No extraction errors, just no matches
    }

    [Fact]
    public async Task TryExtractAllTextSubtitlesAsync_WithVideoWithNoTextTracks_ReturnsResultIndicatingNoTracksProcessed()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/video_with_only_pgs.mkv";
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.SubtitleSource.Should().Be(SubtitleSourceType.TextBased);
        result.ProcessedTracks.Should().BeEmpty();
        result.SuccessfulTrack.Should().BeNull();
        result.ExtractionErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task TryExtractAllTextSubtitlesAsync_WithCancellationToken_CancelsOperationAppropriately()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/large_subtitle_file.mkv";
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await FluentActions.Invoking(() => _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath, cancellationTokenSource.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TryExtractAllTextSubtitlesAsync_WithVeryLargeSubtitleFiles_CompletesWithinTimeout()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/large_subtitle_file.mkv";
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(2)); // Reasonable timeout

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath, cancellationTokenSource.Token);

        // Assert
        result.Should().NotBeNull();
        cancellationTokenSource.Token.IsCancellationRequested.Should().BeFalse();
    }
}
