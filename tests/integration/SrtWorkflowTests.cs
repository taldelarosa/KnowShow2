using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Tests.Contract;

namespace EpisodeIdentifier.Tests.Integration;

public class SrtWorkflowTests : IDisposable
{
    private readonly TextSubtitleExtractor _textExtractor;
    private readonly FuzzyHashService _hashService;
    private readonly string _testDbPath;

    public SrtWorkflowTests()
    {
        // Create required dependencies manually (following the pattern from other working tests)
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Create format handlers
        var formatHandlers = new List<ISubtitleFormatHandler>
        {
            new SrtFormatHandler(),
            new AssFormatHandler(),
            new VttFormatHandler()
        };

        // Create text extractor
        _textExtractor = new TextSubtitleExtractor(formatHandlers);

        // Create matching services
        _testDbPath = TestDatabaseConfig.GetTempDatabasePath();
        _hashService = TestDatabaseConfig.CreateTestFuzzyHashService(_testDbPath);
    }

    public void Dispose()
    {
        _hashService?.Dispose();
        TestDatabaseConfig.CleanupTempDatabase(_testDbPath);
    }

    [Fact]
    public async Task TextSubtitleExtractor_WithNonexistentFile_HandlesGracefully()
    {
        // Arrange
        var nonExistentFile = "nonexistent.mkv";

        // Act - Should handle non-existent file gracefully without throwing
        var result = await _textExtractor.DetectTextSubtitleTracksAsync(nonExistentFile);

        // Assert - Should return empty list for non-existent file
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SrtProcessingWorkflow_WithSimpleTest_CompletesSuccessfully()
    {
        // Arrange
        var testSubtitleText = @"
1
00:00:01,000 --> 00:00:03,000
This is the first subtitle.

2
00:00:04,000 --> 00:00:06,000
This is the second subtitle.

3
00:00:07,000 --> 00:00:09,000
This is the third subtitle.
";

        // Act - Use hash service directly with text
        var result = await _hashService.FindMatches(testSubtitleText, threshold: 0.5);

        // Assert
        result.Should().NotBeNull();
        // For test data that doesn't match, we expect no matches
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FuzzyHashService_WithTestContent_ReturnsResult()
    {
        // Arrange  
        var testContent = "This is a test subtitle that should not match anything in the database.";

        // Act
        var result = await _hashService.FindMatches(testContent, threshold: 0.5);

        // Assert
        result.Should().NotBeNull();
        // Test content should not match anything in database
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SrtProcessingWorkflow_WithFormatHandlers_ProcessesCorrectly()
    {
        // Arrange
        var srtHandler = new SrtFormatHandler();
        var testSrtContent = @"1
00:00:01,000 --> 00:00:03,000
Test subtitle content for SRT format.

2
00:00:04,000 --> 00:00:06,000
Another line of subtitle text.";

        // Act
        var canHandle = srtHandler.SupportedFormat == SubtitleFormat.SRT;

        // Create a test stream for parsing
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testSrtContent));
        var parseResult = await srtHandler.ParseSubtitleTextAsync(stream, null);

        // Assert
        canHandle.Should().BeTrue();
        parseResult.Should().NotBeNull();
        parseResult.Entries.Should().HaveCount(2);
        parseResult.Entries[0].Text.Should().Be("Test subtitle content for SRT format.");
        parseResult.Entries[1].Text.Should().Be("Another line of subtitle text.");
    }

    [Fact]
    public void FormatHandlers_CreatedCorrectly_SupportExpectedFormats()
    {
        // Arrange & Act
        var srtHandler = new SrtFormatHandler();
        var assHandler = new AssFormatHandler();
        var vttHandler = new VttFormatHandler();

        // Assert
        srtHandler.SupportedFormat.Should().Be(SubtitleFormat.SRT);
        assHandler.SupportedFormat.Should().Be(SubtitleFormat.ASS);
        vttHandler.SupportedFormat.Should().Be(SubtitleFormat.VTT);
    }

    [Fact]
    public async Task TextSubtitleExtractor_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        var action = async () => await _textExtractor.DetectTextSubtitleTracksAsync("");
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TextSubtitleExtractor_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var action = async () => await _textExtractor.DetectTextSubtitleTracksAsync(null!);
        await action.Should().ThrowAsync<ArgumentException>();
    }
}
