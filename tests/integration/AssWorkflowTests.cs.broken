using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Integration;

public class AssWorkflowTests
{
    private readonly ITextSubtitleExtractor _extractor;
    private readonly ISubtitleMatcher _matcher;

    public AssWorkflowTests()
    {
        _extractor = new TextSubtitleExtractor();
        _matcher = new SubtitleMatcher();
    }

    [Fact]
    public async Task AssProcessingWorkflow_WithAnimeEpisode_IdentifiesCorrectEpisode()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_ass.mkv";

        // Act - Complete ASS processing workflow
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var assTrack = tracks.First(t => t.Format == SubtitleFormat.ASS);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, assTrack);
        
        var matchResult = await _matcher.FindMatchingEpisodeAsync(content.ExtractedText);

        // Assert
        tracks.Should().NotBeEmpty();
        assTrack.Should().NotBeNull();
        assTrack.Format.Should().Be(SubtitleFormat.ASS);
        
        content.Should().NotBeNull();
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        content.LineCount.Should().BeGreaterThan(0);
        content.SourceTrack.Should().Be(assTrack);
        
        // ASS content should be clean of formatting
        content.ExtractedText.Should().NotContain("Dialogue:");
        content.ExtractedText.Should().NotContain("Format:");
        content.ExtractedText.Should().NotContain("{\\");
        
        matchResult.Should().NotBeNull();
        matchResult.IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task AssProcessingWorkflow_WithAdvancedStyling_ExtractsDialogueTextOnly()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/ass_with_advanced_styling.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should remove ASS override codes
        content.ExtractedText.Should().NotContain("{\\pos(");
        content.ExtractedText.Should().NotContain("{\\move(");
        content.ExtractedText.Should().NotContain("{\\fade(");
        content.ExtractedText.Should().NotContain("{\\fad(");
        content.ExtractedText.Should().NotContain("{\\t(");
        content.ExtractedText.Should().NotContain("{\\clip(");
        content.ExtractedText.Should().NotContain("{\\c&H");
        content.ExtractedText.Should().NotContain("{\\alpha&H");
        
        // Should remove font styling
        content.ExtractedText.Should().NotContain("{\\fn");
        content.ExtractedText.Should().NotContain("{\\fs");
        content.ExtractedText.Should().NotContain("{\\fscx");
        content.ExtractedText.Should().NotContain("{\\fscy");
        content.ExtractedText.Should().NotContain("{\\fsp");
        
        // Should remove text styling
        content.ExtractedText.Should().NotContain("{\\b1}");
        content.ExtractedText.Should().NotContain("{\\i1}");
        content.ExtractedText.Should().NotContain("{\\u1}");
        content.ExtractedText.Should().NotContain("{\\s1}");
        
        // Should preserve actual dialogue content
        content.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AssProcessingWorkflow_WithCommentLines_IgnoresCommentsAndProcessesDialogueOnly()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/ass_with_comments.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should not contain comment lines
        content.ExtractedText.Should().NotContain("Comment:");
        content.ExtractedText.Should().NotContain("Movie:");
        content.ExtractedText.Should().NotContain("Picture:");
        content.ExtractedText.Should().NotContain("Sound:");
        content.ExtractedText.Should().NotContain("Command:");
        
        // Should contain dialogue content
        content.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AssProcessingWorkflow_WithMultipleStyles_ProcessesAllDialogueRegardlessOfStyle()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/ass_multiple_styles.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        content.LineCount.Should().BeGreaterThan(0);
        
        // Should process dialogue from all style types
        // (This assumes test file has dialogue with Default, Narrator, Character styles)
        content.ExtractedText.Length.Should().BeGreaterThan(100); // Significant content
        
        // Should not contain style names or section headers
        content.ExtractedText.Should().NotContain("[V4+ Styles]");
        content.ExtractedText.Should().NotContain("[Events]");
        content.ExtractedText.Should().NotContain("Style:");
        content.ExtractedText.Should().NotContain("Format:");
    }

    [Fact]
    public async Task AssProcessingWorkflow_WithSsaLegacyFormat_HandlesCompatibility()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/ssa_legacy_format.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.Should().NotBeNull();
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        content.LineCount.Should().BeGreaterThan(0);
        
        // Should handle SSA v4.00 format differences
        content.ExtractedText.Should().NotContain("[Script Info]");
        content.ExtractedText.Should().NotContain("ScriptType: v4.00");
        
        // Should still extract dialogue text properly
        content.SourceTrack.Format.Should().Be(SubtitleFormat.ASS); // ASS handler covers SSA too
    }

    [Fact]
    public async Task AssProcessingWorkflow_WithKaraokeEffects_ExtractsLyricsWithoutTiming()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/ass_karaoke_effects.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should remove karaoke timing codes
        content.ExtractedText.Should().NotContain("{\\k");
        content.ExtractedText.Should().NotContain("{\\kf");
        content.ExtractedText.Should().NotContain("{\\ko");
        content.ExtractedText.Should().NotContain("{\\kt");
        
        // Should preserve lyric content
        content.LineCount.Should().BeGreaterThan(0);
    }
}
