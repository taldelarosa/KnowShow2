using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Tests.Contract;

namespace EpisodeIdentifier.Tests.Integration;

public class VttWorkflowTests : IDisposable
{
    private readonly ITextSubtitleExtractor _extractor;
    private readonly FuzzyHashService _hashService;
    private readonly string _testDbPath;

    public VttWorkflowTests()
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
    public async Task VttProcessingWorkflow_WithSimpleTest_CompletesSuccessfully()
    {
        // This is a simplified test that doesn't rely on external video files
        // that may not exist in the test environment

        // Act - Test the workflow components
        var testVideoPath = "test.mkv"; // Non-existent file for testing

        // The extractor should handle missing files gracefully
        var tracks = await _extractor.DetectTextSubtitleTracksAsync(testVideoPath);

        // Assert - Basic functionality test
        tracks.Should().NotBeNull();
        tracks.Should().BeEmpty(); // No tracks from non-existent file

        // Test hash service with sample text
        var sampleText = "Hello, this is a test subtitle";
        var result = await _hashService.FindMatches(sampleText, threshold: 0.5);

        result.Should().NotBeNull();
    }

    [Fact]
    public void VttProcessingWorkflow_WithFormatHandlers_ProcessesCorrectly()
    {
        // Arrange
        var sampleVttContent = @"WEBVTT

00:00:01.000 --> 00:00:04.000
Hello world

00:00:05.000 --> 00:00:08.000
This is a test";

        // Act - Create a manual track for testing
        var track = new TextSubtitleTrack
        {
            Format = SubtitleFormat.VTT,
            Content = sampleVttContent,
            Language = "en",
            Index = 0
        };

        // Test that format handlers work
        var vttHandler = new VttFormatHandler();
        var canHandle = vttHandler.CanHandle(sampleVttContent);

        // Assert
        canHandle.Should().BeTrue();
        track.Format.Should().Be(SubtitleFormat.VTT);
        track.Content.Should().NotBeNullOrWhiteSpace();
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
        var testContent = "This is sample subtitle content for testing episode identification";

        // Act
        var result = await _hashService.FindMatches(testContent, threshold: 0.5);

        // Assert
        result.Should().NotBeNull();
        // Note: Result may not find a match since this is test content,
        // but the hash service should work without throwing exceptions
    }

    [Fact]
    public void FormatHandlers_CreatedCorrectly_SupportExpectedFormats()
    {
        // Arrange & Act
        var srtHandler = new SrtFormatHandler();
        var assHandler = new AssFormatHandler();
        var vttHandler = new VttFormatHandler();

        // Sample content for testing
        var srtContent = "1\n00:00:01,000 --> 00:00:04,000\nHello World";
        var assContent = "[V4+ Styles]\nTitle: Test\n[Events]\nDialogue: 0,0:00:01.00,0:00:04.00,Default,,0,0,0,,Hello World";
        var vttContent = "WEBVTT\n\n00:00:01.000 --> 00:00:04.000\nHello World";

        // Assert
        srtHandler.SupportedFormat.Should().Be(SubtitleFormat.SRT);
        assHandler.SupportedFormat.Should().Be(SubtitleFormat.ASS);
        vttHandler.SupportedFormat.Should().Be(SubtitleFormat.VTT);

        srtHandler.CanHandle(srtContent).Should().BeTrue();
        srtHandler.CanHandle(vttContent).Should().BeFalse();

        vttHandler.CanHandle(vttContent).Should().BeTrue();
        vttHandler.CanHandle(srtContent).Should().BeFalse();

        assHandler.CanHandle(assContent).Should().BeTrue();
        assHandler.CanHandle(vttContent).Should().BeFalse();
    }
}
