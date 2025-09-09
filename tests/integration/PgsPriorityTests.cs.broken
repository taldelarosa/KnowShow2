using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Integration;

public class PgsPriorityTests
{
    private readonly ISubtitleExtractor _subtitleExtractor;
    private readonly ITextSubtitleExtractor _textExtractor;

    public PgsPriorityTests()
    {
        _subtitleExtractor = new SubtitleExtractor(); // Existing service
        _textExtractor = new TextSubtitleExtractor(); // New service
    }

    [Fact]
    public async Task PgsPriority_WithBothPgsAndTextSubtitles_PrioritizesPgsProcessing()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_mixed_subs.mkv";

        // Act - Use the enhanced SubtitleExtractor that should check PGS first
        var result = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath, enableTextSubtitles: true);

        // Assert
        result.Should().NotBeNull();
        
        // Should have used PGS subtitles (existing workflow)
        result.SubtitleSource.Should().Be(SubtitleSourceType.PGS);
        
        // Should not have processed text subtitles
        if (result is SubtitleProcessingResult processingResult)
        {
            processingResult.ProcessedTracks.Should().BeEmpty("text tracks should not be processed when PGS available");
        }
        
        // Should have successful identification from PGS
        result.IsMatch.Should().BeTrue();
        result.SeriesName.Should().NotBeNullOrEmpty();
        result.Season.Should().BeGreaterThan(0);
        result.Episode.Should().BeGreaterThan(0);
        result.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task PgsPriority_WithOnlyTextSubtitles_FallsBackToTextProcessing()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_srt.mkv";

        // Act
        var result = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath, enableTextSubtitles: true);

        // Assert
        result.Should().NotBeNull();
        
        // Should have used text subtitle fallback
        result.SubtitleSource.Should().Be(SubtitleSourceType.TextBased);
        
        // Should have processed text subtitles
        if (result is SubtitleProcessingResult processingResult)
        {
            processingResult.ProcessedTracks.Should().NotBeEmpty();
            processingResult.SuccessfulTrack.Should().NotBeNull();
        }
        
        // Should indicate text subtitle source in metadata
        result.SubtitleMetadata.Should().NotBeNull();
        result.SubtitleMetadata.Should().ContainKey("source_type");
        result.SubtitleMetadata!["source_type"].Should().Be("text_based");
    }

    [Fact]
    public async Task PgsPriority_WithTextSubtitlesDisabled_OnlyProcessesPgs()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_srt.mkv";

        // Act
        var result = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath, enableTextSubtitles: false);

        // Assert
        result.Should().NotBeNull();
        
        // Should not have found any subtitles (no PGS available, text disabled)
        result.IsMatch.Should().BeFalse();
        
        // Should not have processed text subtitles
        if (result is SubtitleProcessingResult processingResult)
        {
            processingResult.ProcessedTracks.Should().BeEmpty();
            processingResult.SuccessfulTrack.Should().BeNull();
        }
        
        // Should maintain existing behavior when text subtitles disabled
        result.SubtitleSource.Should().Be(SubtitleSourceType.PGS); // Attempted PGS first
    }

    [Fact]
    public async Task PgsPriority_WithCorruptedPgs_FallsBackToTextSubtitles()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/corrupted_pgs_valid_text.mkv";

        // Act
        var result = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath, enableTextSubtitles: true);

        // Assert
        result.Should().NotBeNull();
        
        // Should fall back to text subtitles when PGS fails
        result.SubtitleSource.Should().Be(SubtitleSourceType.TextBased);
        
        // Should have successful text subtitle processing
        if (result is SubtitleProcessingResult processingResult)
        {
            processingResult.ProcessedTracks.Should().NotBeEmpty();
            processingResult.SuccessfulTrack.Should().NotBeNull();
        }
        
        // Should indicate fallback in metadata
        result.SubtitleMetadata.Should().NotBeNull();
        result.SubtitleMetadata.Should().ContainKey("pgs_attempted");
        result.SubtitleMetadata!["pgs_attempted"].Should().Be(true);
    }

    [Fact]
    public async Task PgsPriority_ExistingWorkflow_RemainsUnchanged()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/existing_pgs_episode.mkv";

        // Act - Use existing method signature (should still work)
        var result = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath);

        // Assert
        result.Should().NotBeNull();
        
        // Should use PGS workflow by default (existing behavior)
        result.SubtitleSource.Should().Be(SubtitleSourceType.PGS);
        
        // Should not have processed text subtitles (existing behavior preserved)
        if (result is SubtitleProcessingResult processingResult)
        {
            processingResult.ProcessedTracks.Should().BeEmpty();
        }
        
        // Should maintain all existing functionality
        result.IsMatch.Should().BeTrue();
        result.SeriesName.Should().NotBeNullOrEmpty();
        result.Season.Should().BeGreaterThan(0);
        result.Episode.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PgsPriority_WithPgsMatchAndTextMatch_PrefersPgsResult()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/both_pgs_and_text_match.mkv";

        // Act
        var result = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath, enableTextSubtitles: true);

        // Assert
        result.Should().NotBeNull();
        
        // Should prefer PGS result even if text would also match
        result.SubtitleSource.Should().Be(SubtitleSourceType.PGS);
        
        // Should not have processed text subtitles at all
        if (result is SubtitleProcessingResult processingResult)
        {
            processingResult.ProcessedTracks.Should().BeEmpty();
        }
        
        // Should use PGS confidence and match data
        result.Confidence.Should().BeGreaterThan(0.5);
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task PgsPriority_PerformanceImpact_MinimalOverheadWhenPgsAvailable()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_mixed_subs.mkv";

        // Act - Measure processing time with text subtitles enabled
        var startTime = DateTime.UtcNow;
        var result = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath, enableTextSubtitles: true);
        var endTime = DateTime.UtcNow;

        // Act - Measure baseline PGS-only processing time
        var baselineStart = DateTime.UtcNow;
        var baselineResult = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath, enableTextSubtitles: false);
        var baselineEnd = DateTime.UtcNow;

        // Assert
        var enhancedTime = endTime - startTime;
        var baselineTime = baselineEnd - baselineStart;
        
        // Should have minimal overhead when PGS is available and used
        var overhead = enhancedTime - baselineTime;
        overhead.Should().BeLessThan(TimeSpan.FromSeconds(2), 
            "text subtitle capability should add minimal overhead when PGS is used");
        
        // Both should have same result (PGS)
        result.SubtitleSource.Should().Be(baselineResult.SubtitleSource);
        result.IsMatch.Should().Be(baselineResult.IsMatch);
    }

    [Fact]
    public async Task PgsPriority_LoggingBehavior_IndicatesProcessingPath()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_mixed_subs.mkv";

        // Act
        var result = await _subtitleExtractor.ExtractSubtitlesAsync(videoFilePath, enableTextSubtitles: true);

        // Assert
        result.Should().NotBeNull();
        
        // Should have metadata indicating processing path
        result.SubtitleMetadata.Should().NotBeNull();
        result.SubtitleMetadata.Should().ContainKey("processing_path");
        result.SubtitleMetadata!["processing_path"].Should().Be("pgs_primary");
        
        // Should not indicate text subtitle processing was attempted
        result.SubtitleMetadata.Should().ContainKey("text_subtitles_attempted");
        result.SubtitleMetadata!["text_subtitles_attempted"].Should().Be(false);
    }
}
