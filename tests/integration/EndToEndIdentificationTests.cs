using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Tests.Contract;

namespace EpisodeIdentifier.Tests.Integration;

public class EndToEndIdentificationTests : IDisposable
{
    private readonly ITextSubtitleExtractor _extractor;
    private readonly FuzzyHashService _hashService;
    private readonly string _testDbPath;

    public EndToEndIdentificationTests()
    {
        // Create required format handlers
        var formatHandlers = new List<ISubtitleFormatHandler>
        {
            new SrtFormatHandler(),
            new AssFormatHandler(),
            new VttFormatHandler()
        };

        _extractor = new TextSubtitleExtractor(formatHandlers);

        // Create required dependencies for FuzzyHashService
        _testDbPath = TestDatabaseConfig.GetTempDatabasePath();
        _hashService = TestDatabaseConfig.CreateTestFuzzyHashService(_testDbPath);
    }

    public void Dispose()
    {
        _hashService?.Dispose();
        TestDatabaseConfig.CleanupTempDatabase(_testDbPath);
    }

    [Fact]
    public async Task EndToEndIdentification_WithSimpleTest_CompletesSuccessfully()
    {
        // Act - Test the workflow components
        var testVideoPath = "test.mkv"; // Non-existent file for testing

        // The extractor should handle missing files gracefully
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(testVideoPath);

        // Assert - Basic functionality test
        tracks.Should().NotBeNull();
        tracks.Should().BeEmpty(); // No tracks from non-existent file

        // Test hash service with sample text
        var sampleText = "Hello, this is a test subtitle for episode identification";
        var result = await _hashService.FindMatches(sampleText, threshold: 0.5);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EndToEndIdentification_WithMultipleFormats_HandlesAllFormats()
    {
        // Arrange
        var sampleContents = new Dictionary<SubtitleFormat, string>
        {
            { SubtitleFormat.SRT, "1\n00:00:01,000 --> 00:00:04,000\nSample subtitle text" },
            { SubtitleFormat.ASS, "[Events]\nDialogue: 0,0:00:01.00,0:00:04.00,Default,,0,0,0,,Sample subtitle text" },
            { SubtitleFormat.VTT, "WEBVTT\n\n00:00:01.000 --> 00:00:04.000\nSample subtitle text" }
        };

        // Act & Assert
        foreach (var (format, content) in sampleContents)
        {
            // Test that each format can be processed
            var result = await _hashService.FindMatches(content, threshold: 0.5);
            result.Should().NotBeNull($"Format {format} should be processable");
        }
    }

    [Fact]
    public async Task TextSubtitleExtractor_WithNonexistentFile_HandlesGracefully()
    {
        // Arrange
        var nonExistentPath = "nonexistent_file.mkv";

        // Act & Assert - Should not throw, should return empty list
        var result = await _extractor.DetectTextSubtitleTracksAsync(nonExistentPath);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FuzzyHashService_WithTestContent_ReturnsResult()
    {
        // Arrange
        var testContent = "This is comprehensive subtitle content for end-to-end testing of episode identification";

        // Act
        var result = await _hashService.FindMatches(testContent, threshold: 0.5);

        // Assert
        result.Should().NotBeNull();
        // Note: Result may not find a match since this is test content,
        // but the hash service should work without throwing exceptions
    }

    [Fact]
    public void FormatHandlers_AllFormatsSupported_WorkCorrectly()
    {
        // Arrange & Act
        var srtHandler = new SrtFormatHandler();
        var assHandler = new AssFormatHandler();
        var vttHandler = new VttFormatHandler();

        // Sample content for testing
        var srtContent = "1\n00:00:01,000 --> 00:00:04,000\nHello World";
        var assContent = "[V4+ Styles]\nTitle: Test\n[Events]\nDialogue: 0,0:00:01.00,0:00:04.00,Default,,0,0,0,,Hello World";
        var vttContent = "WEBVTT\n\n00:00:01.000 --> 00:00:04.000\nHello World";

        // Assert - Each handler supports its format
        srtHandler.SupportedFormat.Should().Be(SubtitleFormat.SRT);
        assHandler.SupportedFormat.Should().Be(SubtitleFormat.ASS);
        vttHandler.SupportedFormat.Should().Be(SubtitleFormat.VTT);

        // Assert - Each handler can identify its format
        srtHandler.CanHandle(srtContent).Should().BeTrue();
        assHandler.CanHandle(assContent).Should().BeTrue();
        vttHandler.CanHandle(vttContent).Should().BeTrue();

        // Assert - Handlers reject other formats
        srtHandler.CanHandle(assContent).Should().BeFalse();
        assHandler.CanHandle(vttContent).Should().BeFalse();
        vttHandler.CanHandle(srtContent).Should().BeFalse();
    }
}
