using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IFilenameService interface.
/// These tests verify the interface contract without testing implementation details.
/// All tests MUST FAIL until the service is implemented.
/// </summary>
public class FilenameServiceContractTests
{
    private readonly IFilenameService _filenameService;

    public FilenameServiceContractTests()
    {
        var mockConfigService = new TestAppConfigService();
        _filenameService = new FilenameService(mockConfigService);
    }

    private class TestAppConfigService : IAppConfigService
    {
        public AppConfig Config => new AppConfig
        {
            RenameConfidenceThreshold = 0.1
        };

        public Task LoadConfigurationAsync(string? configPath = null) => Task.CompletedTask;
        public Task SaveConfigurationAsync(string? configPath = null) => Task.CompletedTask;
    }

    [Fact]
    public void GenerateFilename_WithValidHighConfidenceRequest_ReturnsValidFilename()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = "Pilot",
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        // Act
        var result = _filenameService.GenerateFilename(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.SuggestedFilename.Should().Be("The Office - S01E01 - Pilot.mkv");
        result.ValidationError.Should().BeNull();
        result.TotalLength.Should().BeGreaterThan(0);
        result.WasTruncated.Should().BeFalse();
        result.SanitizedCharacters.Should().NotBeNull();
    }

    [Fact]
    public void GenerateFilename_WithLowConfidence_ReturnsValidationError()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            FileExtension = ".mkv",
            MatchConfidence = 0.75 // Below 0.9 threshold
        };

        // Act
        var result = _filenameService.GenerateFilename(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ValidationError.Should().NotBeNullOrEmpty();
        result.ValidationError.Should().Contain("confidence");
    }

    [Fact]
    public void GenerateFilename_WithNullSeries_ReturnsValidationError()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = null!,
            Season = "01",
            Episode = "01",
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        // Act
        var result = _filenameService.GenerateFilename(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ValidationError.Should().NotBeNullOrEmpty();
        result.ValidationError.Should().Contain("Series");
    }

    [Fact]
    public void GenerateFilename_WithEmptySeason_ReturnsValidationError()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "The Office",
            Season = "",
            Episode = "01",
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        // Act
        var result = _filenameService.GenerateFilename(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ValidationError.Should().NotBeNullOrEmpty();
        result.ValidationError.Should().Contain("Season");
    }

    [Fact]
    public void GenerateFilename_WithInvalidFileExtension_ReturnsValidationError()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            FileExtension = "mkv", // Missing dot
            MatchConfidence = 0.95
        };

        // Act
        var result = _filenameService.GenerateFilename(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ValidationError.Should().NotBeNullOrEmpty();
        result.ValidationError.Should().Contain("extension");
    }

    [Fact]
    public void GenerateFilename_WithoutEpisodeName_GeneratesFilenameWithoutEpisodeName()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = null,
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        // Act
        var result = _filenameService.GenerateFilename(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.SuggestedFilename.Should().Be("The Office - S01E01.mkv");
        result.ValidationError.Should().BeNull();
    }

    [Fact]
    public void GenerateFilename_WithLongNames_TruncatesAndReportsCorrectly()
    {
        // Arrange
        var longSeriesName = new string('A', 200);
        var longEpisodeName = new string('B', 100);
        var request = new FilenameGenerationRequest
        {
            Series = longSeriesName,
            Season = "01",
            Episode = "01",
            EpisodeName = longEpisodeName,
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        // Act
        var result = _filenameService.GenerateFilename(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.TotalLength.Should().BeLessOrEqualTo(260);
        result.WasTruncated.Should().BeTrue();
        result.SuggestedFilename.Should().EndWith(".mkv");
    }

    [Fact]
    public void SanitizeForWindows_WithInvalidCharacters_ReplacesWithSpaces()
    {
        // Arrange
        var input = "Show: The \"Best\" <Episode> |Part 1| ?Question? *Star* \\Path\\";

        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotContain("<");
        result.Should().NotContain(">");
        result.Should().NotContain(":");
        result.Should().NotContain("\"");
        result.Should().NotContain("|");
        result.Should().NotContain("?");
        result.Should().NotContain("*");
        result.Should().NotContain("\\");
        result.Should().Be("Show The Best Episode Part 1 Question Star Path");
    }

    [Fact]
    public void SanitizeForWindows_WithMultipleSpaces_CollapsesToSingleSpace()
    {
        // Arrange
        var input = "Show    with     many      spaces";

        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        result.Should().Be("Show with many spaces");
    }

    [Fact]
    public void SanitizeForWindows_WithLeadingTrailingSpaces_TrimsSpaces()
    {
        // Arrange
        var input = "   Show with spaces   ";

        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        result.Should().Be("Show with spaces");
    }

    [Fact]
    public void SanitizeForWindows_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var input = "";

        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void SanitizeForWindows_WithOnlyInvalidCharacters_ReturnsEmptyAfterTrim()
    {
        // Arrange
        var input = "<>:\"|?*\\";

        // Act
        var result = _filenameService.SanitizeForWindows(input);

        // Assert
        result.Should().Be("");
    }

    [Theory]
    [InlineData("Valid filename.txt", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("filename<invalid>.txt", false)]
    [InlineData("filename>invalid.txt", false)]
    [InlineData("filename:invalid.txt", false)]
    [InlineData("filename\"invalid.txt", false)]
    [InlineData("filename|invalid.txt", false)]
    [InlineData("filename?invalid.txt", false)]
    [InlineData("filename*invalid.txt", false)]
    [InlineData("filename\\invalid.txt", false)]
    public void IsValidWindowsFilename_ValidatesCorrectly(string filename, bool expectedValid)
    {
        // Act
        var result = _filenameService.IsValidWindowsFilename(filename);

        // Assert
        result.Should().Be(expectedValid);
    }

    [Fact]
    public void IsValidWindowsFilename_WithLongPath_ReturnsFalse()
    {
        // Arrange
        var longFilename = new string('A', 300) + ".txt";

        // Act
        var result = _filenameService.IsValidWindowsFilename(longFilename);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TruncateToLimit_PreservesFileExtension()
    {
        // Arrange
        var longFilename = new string('A', 300) + ".mkv";

        // Act
        var result = _filenameService.TruncateToLimit(longFilename, 260);

        // Assert
        result.Should().EndWith(".mkv");
        result.Length.Should().BeLessOrEqualTo(260);
    }

    [Fact]
    public void TruncateToLimit_WithCustomMaxLength_RespectsLimit()
    {
        // Arrange
        var longFilename = "Very long filename that exceeds the custom limit.txt";
        var customLimit = 20;

        // Act
        var result = _filenameService.TruncateToLimit(longFilename, customLimit);

        // Assert
        result.Length.Should().BeLessOrEqualTo(customLimit);
        result.Should().EndWith(".txt");
    }

    [Fact]
    public void TruncateToLimit_WithShortFilename_ReturnsUnchanged()
    {
        // Arrange
        var shortFilename = "short.txt";

        // Act
        var result = _filenameService.TruncateToLimit(shortFilename, 260);

        // Assert
        result.Should().Be(shortFilename);
    }
}
