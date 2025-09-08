using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Integration;

public class VttWorkflowTests
{
    private readonly ITextSubtitleExtractor _extractor;
    private readonly ISubtitleMatcher _matcher;

    public VttWorkflowTests()
    {
        _extractor = new TextSubtitleExtractor();
        _matcher = new SubtitleMatcher();
    }

    [Fact]
    public async Task VttProcessingWorkflow_WithWebVideoEpisode_IdentifiesCorrectEpisode()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/sample_episode_with_vtt.mkv";

        // Act - Complete VTT processing workflow
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var vttTrack = tracks.First(t => t.Format == SubtitleFormat.VTT);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, vttTrack);
        
        var matchResult = await _matcher.FindMatchingEpisodeAsync(content.ExtractedText);

        // Assert
        tracks.Should().NotBeEmpty();
        vttTrack.Should().NotBeNull();
        vttTrack.Format.Should().Be(SubtitleFormat.VTT);
        
        content.Should().NotBeNull();
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        content.LineCount.Should().BeGreaterThan(0);
        content.SourceTrack.Should().Be(vttTrack);
        
        // VTT content should be clean of formatting
        content.ExtractedText.Should().NotContain("WEBVTT");
        content.ExtractedText.Should().NotContain("NOTE");
        content.ExtractedText.Should().NotMatch(@"\d{2}:\d{2}:\d{2}\.\d{3} --> \d{2}:\d{2}:\d{2}\.\d{3}");
        
        matchResult.Should().NotBeNull();
        matchResult.IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task VttProcessingWorkflow_WithCueSettings_ExtractsCueTextWithoutSettings()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/vtt_with_cue_settings.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should remove cue settings
        content.ExtractedText.Should().NotContain("align:");
        content.ExtractedText.Should().NotContain("line:");
        content.ExtractedText.Should().NotContain("position:");
        content.ExtractedText.Should().NotContain("size:");
        content.ExtractedText.Should().NotContain("vertical:");
        content.ExtractedText.Should().NotContain("region:");
        
        // Should preserve cue text content
        content.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task VttProcessingWorkflow_WithVoiceSpanTags_ExtractsTextWithoutSpeakerMarkup()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/vtt_with_voice_spans.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should remove voice span markup
        content.ExtractedText.Should().NotContain("<v ");
        content.ExtractedText.Should().NotContain("</v>");
        content.ExtractedText.Should().NotContain("<v.speaker>");
        content.ExtractedText.Should().NotContain("<v John>");
        content.ExtractedText.Should().NotContain("<v.narrator>");
        
        // Should preserve the actual spoken content
        content.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task VttProcessingWorkflow_WithClassSpanTags_ExtractsTextWithoutStylingMarkup()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/vtt_with_class_spans.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should remove class span markup
        content.ExtractedText.Should().NotContain("<c>");
        content.ExtractedText.Should().NotContain("</c>");
        content.ExtractedText.Should().NotContain("<c.className>");
        content.ExtractedText.Should().NotContain("<c.highlight>");
        content.ExtractedText.Should().NotContain("<c.emphasis>");
        
        // Should remove other WebVTT tags
        content.ExtractedText.Should().NotContain("<i>");
        content.ExtractedText.Should().NotContain("</i>");
        content.ExtractedText.Should().NotContain("<b>");
        content.ExtractedText.Should().NotContain("</b>");
        content.ExtractedText.Should().NotContain("<u>");
        content.ExtractedText.Should().NotContain("</u>");
        content.ExtractedText.Should().NotContain("<ruby>");
        content.ExtractedText.Should().NotContain("</ruby>");
        
        // Should preserve text content
        content.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task VttProcessingWorkflow_WithNestedTimestamps_ExtractsTextWithoutTimingMarkup()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/vtt_with_nested_timestamps.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should remove nested timestamp markup
        content.ExtractedText.Should().NotContain("<00:00:01.000>");
        content.ExtractedText.Should().NotContain("</00:00:02.000>");
        content.ExtractedText.Should().NotMatch(@"<\d{2}:\d{2}:\d{2}\.\d{3}>");
        content.ExtractedText.Should().NotMatch(@"</\d{2}:\d{2}:\d{2}\.\d{3}>");
        
        // Should preserve text content between timestamps
        content.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task VttProcessingWorkflow_WithNoteSections_IgnoresNotesAndProcessesCuesOnly()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/vtt_with_extensive_notes.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should not contain NOTE content
        content.ExtractedText.Should().NotContain("NOTE");
        content.ExtractedText.Should().NotContain("This is a note");
        content.ExtractedText.Should().NotContain("Author:");
        content.ExtractedText.Should().NotContain("Description:");
        
        // Should not contain WEBVTT header
        content.ExtractedText.Should().NotContain("WEBVTT");
        content.ExtractedText.Should().NotContain("Kind:");
        content.ExtractedText.Should().NotContain("Language:");
        
        // Should contain actual cue text
        content.LineCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task VttProcessingWorkflow_WithRegionDefinitions_ExtractsTextWithoutRegionMarkup()
    {
        // Arrange
        var videoFilePath = "/mnt/c/Users/Ragma/KnowShow_Specd/tests/data/nonpgs-workflow/vtt_with_regions.mkv";

        // Act
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(videoFilePath);
        var content = await _extractor.ExtractTextSubtitleContentAsync(videoFilePath, tracks.First());

        // Assert
        content.ExtractedText.Should().NotBeNullOrWhiteSpace();
        
        // Should not contain region definitions
        content.ExtractedText.Should().NotContain("REGION");
        content.ExtractedText.Should().NotContain("id:");
        content.ExtractedText.Should().NotContain("width:");
        content.ExtractedText.Should().NotContain("regionanchor:");
        content.ExtractedText.Should().NotContain("viewportanchor:");
        content.ExtractedText.Should().NotContain("scroll:");
        
        // Should contain cue text content
        content.LineCount.Should().BeGreaterThan(0);
    }
}
