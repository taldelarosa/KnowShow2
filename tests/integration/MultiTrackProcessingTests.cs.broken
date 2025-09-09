using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Integration;

public class MultiTrackProcessingTests
{
    private readonly ITextSubtitleExtractor _extractor;

    public MultiTrackProcessingTests()
    {
        _extractor = new TextSubtitleExtractor();
    }

    [Fact]
    public async Task MultiTrackProcessing_WithSequentialTracks_ProcessesUntilMatchFound()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_multi_subs.mkv";

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath);

        // Assert
        result.Should().NotBeNull();
        result.SubtitleSource.Should().Be(SubtitleSourceType.TextBased);
        
        // Should have processed multiple tracks
        result.ProcessedTracks.Should().HaveCountGreaterThan(1);
        
        // Should find match on track 2 (Spanish SRT) as per test data design
        result.SuccessfulTrack.Should().NotBeNull();
        result.SuccessfulTrack!.Index.Should().Be(2);
        result.SuccessfulTrack.Format.Should().Be(SubtitleFormat.SRT);
        result.SuccessfulTrack.Language.Should().Be("es");
        
        // Should have attempted tracks 0 and 1 before finding match
        var processedIndices = result.ProcessedTracks.Select(t => t.Index).OrderBy(i => i).ToList();
        processedIndices.Should().Contain(0);
        processedIndices.Should().Contain(1);
        processedIndices.Should().Contain(2);
        
        // Should not have errors for successful track
        result.ExtractionErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiTrackProcessing_WithDifferentFormats_HandlesAllSupportedFormats()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/mixed_format_subtitles.mkv";

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath);

        // Assert
        result.Should().NotBeNull();
        result.ProcessedTracks.Should().NotBeEmpty();
        
        // Should have attempted different formats
        var processedFormats = result.ProcessedTracks.Select(t => t.Format).Distinct().ToList();
        processedFormats.Should().Contain(SubtitleFormat.SRT);
        processedFormats.Should().Contain(SubtitleFormat.ASS);
        processedFormats.Should().Contain(SubtitleFormat.VTT);
        
        // Each track should be processed sequentially by index
        var trackIndices = result.ProcessedTracks.Select(t => t.Index).OrderBy(i => i).ToList();
        for (int i = 0; i < trackIndices.Count - 1; i++)
        {
            trackIndices[i + 1].Should().Be(trackIndices[i] + 1, "tracks should be processed in sequential order");
        }
    }

    [Fact]
    public async Task MultiTrackProcessing_WithEarlyMatch_StopsProcessingRemainingTracks()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/early_match_episode.mkv";
        
        // This test file should have a match on track 0, with additional tracks available
        var allTracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        allTracks.Should().HaveCountGreaterThan(2, "test requires multiple tracks for proper validation");

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulTrack.Should().NotBeNull();
        result.SuccessfulTrack!.Index.Should().Be(0, "should match on first track");
        
        // Should only have processed tracks up to and including the successful one
        result.ProcessedTracks.Should().HaveCount(1, "should stop after finding match");
        result.ProcessedTracks.First().Index.Should().Be(0);
        
        // Should not have processed remaining tracks
        var processedIndices = result.ProcessedTracks.Select(t => t.Index);
        var allTrackIndices = allTracks.Select(t => t.Index);
        var unprocessedTracks = allTrackIndices.Except(processedIndices);
        unprocessedTracks.Should().NotBeEmpty("should have tracks that weren't processed");
    }

    [Fact]
    public async Task MultiTrackProcessing_WithExtractionErrors_ContinuesToNextTrack()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/corrupted_and_valid_tracks.mkv";

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath);

        // Assert
        result.Should().NotBeNull();
        result.ProcessedTracks.Should().NotBeEmpty();
        
        // Should have extraction errors for corrupted tracks
        result.ExtractionErrors.Should().NotBeEmpty();
        result.ExtractionErrors.Should().OnlyContain(error => 
            error.Contains("corrupted") || 
            error.Contains("invalid") || 
            error.Contains("extraction failed"));
        
        // Should still find successful track despite errors
        if (result.SuccessfulTrack != null)
        {
            result.SuccessfulTrack.Should().BeOneOf(result.ProcessedTracks);
        }
        
        // Should have processed multiple tracks (including failed ones)
        result.ProcessedTracks.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task MultiTrackProcessing_WithNoMatches_ProcessesAllTracksAndReturnsNoMatch()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/unknown_episode.mkv";

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath);

        // Assert
        result.Should().NotBeNull();
        result.SubtitleSource.Should().Be(SubtitleSourceType.TextBased);
        result.ProcessedTracks.Should().NotBeEmpty();
        result.SuccessfulTrack.Should().BeNull("no match should be found");
        
        // Should have processed all available text tracks
        var allTracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        result.ProcessedTracks.Should().HaveCount(allTracks.Count, "should process all tracks when no match found");
        
        // Each track should have been attempted
        var processedIndices = result.ProcessedTracks.Select(t => t.Index).OrderBy(i => i);
        var allIndices = allTracks.Select(t => t.Index).OrderBy(i => i);
        processedIndices.Should().BeEquivalentTo(allIndices);
    }

    [Fact]
    public async Task MultiTrackProcessing_WithLanguagePriority_ProcessesInTrackIndexOrder()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/multilingual_episode.mkv";

        // Act
        var allTracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath);

        // Assert
        allTracks.Should().NotBeEmpty();
        result.ProcessedTracks.Should().NotBeEmpty();
        
        // Should process tracks in index order regardless of language
        var expectedOrder = allTracks.OrderBy(t => t.Index).Select(t => t.Index);
        var actualOrder = result.ProcessedTracks.Select(t => t.Index);
        
        // Take only the processed portion (in case processing stopped early)
        var processedCount = result.ProcessedTracks.Count;
        var expectedProcessedOrder = expectedOrder.Take(processedCount);
        
        actualOrder.Should().BeEquivalentTo(expectedProcessedOrder, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task MultiTrackProcessing_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/large_multitrack_episode.mkv";
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act & Assert
        await FluentActions.Invoking(() => 
            _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath, cancellationTokenSource.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task MultiTrackProcessing_WithPerformanceGoals_CompletesWithinReasonableTime()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_multi_subs.mkv";
        var startTime = DateTime.UtcNow;

        // Act
        var result = await _extractor.TryExtractAllTextSubtitlesAsync(videoFilePath);
        var endTime = DateTime.UtcNow;

        // Assert
        var processingTime = endTime - startTime;
        
        // Should complete within performance goals (10 seconds per track max)
        var maxExpectedTime = TimeSpan.FromSeconds(result.ProcessedTracks.Count * 10);
        processingTime.Should().BeLessOrEqualTo(maxExpectedTime);
        
        // Should be reasonably fast for multiple tracks
        processingTime.Should().BeLessOrEqualTo(TimeSpan.FromMinutes(1), 
            "multi-track processing should complete within reasonable time");
    }
}
