using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Integration;

public class SrtWorkflowTests
{
    private readonly ITextSubtitleExtractor _extractor;
    private readonly ISubtitleMatcher _matcher;

    public SrtWorkflowTests()
    {
        // These will fail until the services are implemented
        _extractor = new TextSubtitleExtractor();
        _matcher = new SubtitleMatcher(); // Existing service, may need enhancement
    }

    [Fact]
    public async Task SrtProcessingWorkflow_WithMatchingEpisode_IdentifiesCorrectEpisode()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_srt.mkv";

        // Act - Complete SRT processing workflow
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var srtTrack = tracks.First(t => t.Format == SubtitleFormat.SRT);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, srtTrack);
        
        // This would use the existing fuzzy hash matching workflow
        var matchResult = await _matcher.FindMatchingEpisodeAsync(content.ExtractedText);

        // Assert
        tracks.Should().NotBeEmpty();
        srtTrack.Should().NotBeNull();
        srtTrack.Format.Should().Be(SubtitleFormat.SRT);
        
        content.Should().NotBeNull();
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        content.LineCount.Should().BeGreaterThan(0);
        content.SourceTrack.Should().Be(srtTrack);
        
        matchResult.Should().NotBeNull();
        matchResult.IsMatch.Should().BeTrue();
        matchResult.SeriesName.Should().NotBeNullOrEmpty();
        matchResult.Season.Should().BeGreaterThan(0);
        matchResult.Episode.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SrtProcessingWorkflow_WithEncodingVariations_HandlesUtf8AndLatin1()
    {
        // Arrange
        var utf8VideoPath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/srt_utf8_episode.mkv";
        var latin1VideoPath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/srt_latin1_episode.mkv";

        // Act - Process both encoding types
        var utf8Tracks = await _extractor.DetectTextSubtitleTracksAsync(utf8VideoPath);
        var latin1Tracks = await _extractor.DetectTextSubtitleTracksAsync(latin1VideoPath);
        
        var utf8Content = await _extractor.ExtractTextSubtitleContentAsync(utf8VideoPath, utf8Tracks.First());
        var latin1Content = await _extractor.ExtractTextSubtitleContentAsync(latin1VideoPath, latin1Tracks.First());

        // Assert
        utf8Content.Encoding.Should().Be("UTF-8");
        latin1Content.Encoding.Should().Be("ISO-8859-1");
        
        utf8Content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        latin1Content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Both should produce readable text despite different encodings
        utf8Content.ExtractedText.Should().NotContain("�"); // No replacement characters
        latin1Content.ExtractedText.Should().NotContain("�");
    }

    [Fact]
    public async Task SrtProcessingWorkflow_WithHtmlTags_StripsStylingAndPreservesContent()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/srt_with_html_tags.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotContain("<i>");
        content.ExtractedText.Should().NotContain("</i>");
        content.ExtractedText.Should().NotContain("<b>");
        content.ExtractedText.Should().NotContain("</b>");
        content.ExtractedText.Should().NotContain("<u>");
        content.ExtractedText.Should().NotContain("</u>");
        
        // But should preserve the actual dialogue content
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        content.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SrtProcessingWorkflow_WithMalformedTimestamps_SkipsInvalidEntriesAndContinuesProcessing()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/srt_malformed_timestamps.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.Should().NotBeNull();
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should extract valid entries even if some are malformed
        content.LineCount.Should().BeGreaterThan(0);
        
        // Should not contain timestamp formatting
        content.ExtractedText.Should().NotContain("-->");
        content.ExtractedText.Should().NotMatch(@"\d{2}:\d{2}:\d{2},\d{3}");
    }

    [Fact]
    public async Task SrtProcessingWorkflow_WithLargeSubtitleFile_CompletesWithinPerformanceGoals()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/large_srt_episode.mkv";
        var startTime = DateTime.UtcNow;

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());
        var endTime = DateTime.UtcNow;

        // Assert
        var processingTime = endTime - startTime;
        processingTime.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10)); // Performance goal
        
        content.Should().NotBeNull();
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        content.LineCount.Should().BeGreaterThan(100); // Verify it was actually a large file
    }
}
