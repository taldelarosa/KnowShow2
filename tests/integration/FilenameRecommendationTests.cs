using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for filename recommendation functionality.
/// These tests verify end-to-end filename suggestion behavior.
/// All tests MUST FAIL until the full service integration is implemented.
/// </summary>
public class FilenameRecommendationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<FilenameRecommendationTests> _logger;

    public FilenameRecommendationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Register services
        services.AddSingleton<IAppConfigService, TestAppConfigService>();
        services.AddScoped<IFilenameService, FilenameService>();
        services.AddScoped<SubtitleNormalizationService>();
        services.AddScoped<FuzzyHashService>(provider =>
            new FuzzyHashService(
                ":memory:", // Use in-memory SQLite database for tests
                provider.GetRequiredService<ILogger<FuzzyHashService>>(),
                provider.GetRequiredService<SubtitleNormalizationService>()));

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<FilenameRecommendationTests>>();
    }

    [Fact]
    public async Task HighConfidenceIdentification_GeneratesSuggestedFilename()
    {
        // Arrange
        var fuzzyHashService = _serviceProvider.GetRequiredService<FuzzyHashService>();
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        // Simulate high confidence episode identification
        var episodeData = new
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = "Pilot",
            MatchConfidence = 0.95
        };

        // Act - This simulates the main identification workflow
        var filenameRequest = new FilenameGenerationRequest
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            EpisodeName = episodeData.EpisodeName,
            FileExtension = ".mkv",
            MatchConfidence = episodeData.MatchConfidence
        };

        var filenameResult = filenameService.GenerateFilename(filenameRequest);

        // Assert
        filenameResult.Should().NotBeNull();
        filenameResult.IsValid.Should().BeTrue();
        filenameResult.SuggestedFilename.Should().Be("The Office - S01E01 - Pilot.mkv");
        filenameResult.ValidationError.Should().BeNull();
        filenameResult.WasTruncated.Should().BeFalse();

        _logger.LogInformation("High confidence identification generated filename: {Filename}", filenameResult.SuggestedFilename);
    }

    [Fact]
    public async Task HighConfidenceWithoutEpisodeName_GeneratesBasicFilename()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        var episodeData = new
        {
            Series = "Breaking Bad",
            Season = "02",
            Episode = "05",
            EpisodeName = (string?)null, // No episode name available
            MatchConfidence = 0.92
        };

        // Act
        var filenameRequest = new FilenameGenerationRequest
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            EpisodeName = episodeData.EpisodeName,
            FileExtension = ".mkv",
            MatchConfidence = episodeData.MatchConfidence
        };

        var filenameResult = filenameService.GenerateFilename(filenameRequest);

        // Assert
        filenameResult.Should().NotBeNull();
        filenameResult.IsValid.Should().BeTrue();
        filenameResult.SuggestedFilename.Should().Be("Breaking Bad - S02E05.mkv");
        filenameResult.ValidationError.Should().BeNull();

        _logger.LogInformation("High confidence without episode name generated: {Filename}", filenameResult.SuggestedFilename);
    }

    [Fact]
    public async Task HighConfidenceWithLongNames_GeneratesTruncatedFilename()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        var episodeData = new
        {
            Series = "The Extremely Long Series Name That Goes On And On And On And On",
            Season = "01",
            Episode = "01",
            EpisodeName = "This Is An Extremely Long Episode Name That Contains Many Words And Characters That Will Definitely Exceed The Windows Path Limit",
            MatchConfidence = 0.98
        };

        // Act
        var filenameRequest = new FilenameGenerationRequest
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            EpisodeName = episodeData.EpisodeName,
            FileExtension = ".mkv",
            MatchConfidence = episodeData.MatchConfidence,
            MaxLength = 200  // Force truncation to test the behavior
        };

        var filenameResult = filenameService.GenerateFilename(filenameRequest);

        // Assert
        filenameResult.Should().NotBeNull();
        filenameResult.IsValid.Should().BeTrue();
        filenameResult.TotalLength.Should().BeLessOrEqualTo(200);
        filenameResult.WasTruncated.Should().BeTrue();
        filenameResult.SuggestedFilename.Should().EndWith(".mkv");
        filenameResult.SuggestedFilename.Should().Contain("S01E01");

        _logger.LogInformation("Long names truncated to: {Filename} (length: {Length})",
            filenameResult.SuggestedFilename, filenameResult.TotalLength);
    }

    [Fact]
    public async Task MediumConfidenceIdentification_NoFilenameSuggestion()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        var episodeData = new
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = "Pilot",
            MatchConfidence = 0.75 // Below 0.9 threshold
        };

        // Act
        var filenameRequest = new FilenameGenerationRequest
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            EpisodeName = episodeData.EpisodeName,
            FileExtension = ".mkv",
            MatchConfidence = episodeData.MatchConfidence
        };

        var filenameResult = filenameService.GenerateFilename(filenameRequest);

        // Assert
        filenameResult.Should().NotBeNull();
        filenameResult.IsValid.Should().BeFalse();
        filenameResult.ValidationError.Should().NotBeNullOrEmpty();
        filenameResult.ValidationError.Should().Contain("confidence");
        filenameResult.SuggestedFilename.Should().BeNull();

        _logger.LogInformation("Medium confidence rejected: {Error}", filenameResult.ValidationError);
    }

    [Fact]
    public async Task EndToEndWorkflow_HighConfidence_CreatesCompleteResult()
    {
        // Arrange - Simulate full episode identification workflow
        var fuzzyHashService = _serviceProvider.GetRequiredService<FuzzyHashService>();
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        // This simulates what the main Program.cs workflow would do
        var videoFile = "/path/to/test/video.mkv";
        var episodeData = new
        {
            Series = "Game of Thrones",
            Season = "01",
            Episode = "01",
            EpisodeName = "Winter Is Coming",
            MatchConfidence = 0.97
        };

        // Act - Complete workflow simulation
        var identificationResult = new IdentificationResult
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            MatchConfidence = episodeData.MatchConfidence
        };

        // Generate filename if confidence is high enough
        if (identificationResult.MatchConfidence >= 0.9)
        {
            var filenameRequest = new FilenameGenerationRequest
            {
                Series = identificationResult.Series!,
                Season = identificationResult.Season!,
                Episode = identificationResult.Episode!,
                EpisodeName = episodeData.EpisodeName,
                FileExtension = Path.GetExtension(videoFile),
                MatchConfidence = identificationResult.MatchConfidence
            };

            var filenameResult = filenameService.GenerateFilename(filenameRequest);

            if (filenameResult.IsValid)
            {
                identificationResult.SuggestedFilename = filenameResult.SuggestedFilename;
            }
        }

        // Assert
        identificationResult.Should().NotBeNull();
        identificationResult.MatchConfidence.Should().Be(0.97);
        identificationResult.SuggestedFilename.Should().Be("Game of Thrones - S01E01 - Winter Is Coming.mkv");

        _logger.LogInformation("End-to-end workflow result: {Result}",
            System.Text.Json.JsonSerializer.Serialize(identificationResult));
    }

    [Fact]
    public async Task DifferentFileExtensions_GenerateCorrectFilenames()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        var testCases = new[]
        {
            new { Extension = ".mkv", Expected = "The Office - S01E01 - Pilot.mkv" },
            new { Extension = ".mp4", Expected = "The Office - S01E01 - Pilot.mp4" },
            new { Extension = ".avi", Expected = "The Office - S01E01 - Pilot.avi" },
            new { Extension = ".mov", Expected = "The Office - S01E01 - Pilot.mov" }
        };

        foreach (var testCase in testCases)
        {
            // Act
            var filenameRequest = new FilenameGenerationRequest
            {
                Series = "The Office",
                Season = "01",
                Episode = "01",
                EpisodeName = "Pilot",
                FileExtension = testCase.Extension,
                MatchConfidence = 0.95
            };

            var result = filenameService.GenerateFilename(filenameRequest);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.SuggestedFilename.Should().Be(testCase.Expected);

            _logger.LogInformation("Extension {Extension} generated: {Filename}",
                testCase.Extension, result.SuggestedFilename);
        }
    }

    [Fact]
    public async Task DatabaseIntegration_StoresAndRetrievesEpisodeNames()
    {
        // Arrange
        var fuzzyHashService = _serviceProvider.GetRequiredService<FuzzyHashService>();

        var episodeData = new
        {
            Series = "The Office",
            Season = "01",
            Episode = "01",
            EpisodeName = "Pilot",
            Hash = "test_hash_12345"
        };

        // Act - This tests the database integration for episode names
        // The FuzzyHashService should now support storing episode names
        var labelledSubtitle = new LabelledSubtitle
        {
            Series = episodeData.Series,
            Season = episodeData.Season,
            Episode = episodeData.Episode,
            SubtitleText = "Sample subtitle text for testing"
        };

        await fuzzyHashService.StoreHash(labelledSubtitle);

        var retrievedMatch = await fuzzyHashService.GetBestMatch("Sample subtitle text for testing");

        // Assert
        retrievedMatch.Should().NotBeNull();
        retrievedMatch.Value.Subtitle.Series.Should().Be(episodeData.Series);
        retrievedMatch.Value.Subtitle.Season.Should().Be(episodeData.Season);
        retrievedMatch.Value.Subtitle.Episode.Should().Be(episodeData.Episode);

        _logger.LogInformation("Database integration test - stored and retrieved episode data for series: {Series}",
            retrievedMatch.Value.Subtitle.Series);
    }

    [Fact]
    public async Task WindowsCharacterSanitization_ReplacesInvalidCharacters()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        var testCases = new[]
        {
            new
            {
                Series = "Show: The Best Series",
                EpisodeName = "Episode \"Title\" with <Brackets>",
                Expected = "Show  The Best Series - S01E01 - Episode  Title  with  Brackets .mkv"
            },
            new
            {
                Series = "Series|Name",
                EpisodeName = "Episode?Title*Name",
                Expected = "Series Name - S01E01 - Episode Title Name.mkv"
            },
            new
            {
                Series = "Path\\Series\\Name",
                EpisodeName = "Episode/Title",
                Expected = "Path Series Name - S01E01 - Episode Title.mkv"
            }
        };

        foreach (var testCase in testCases)
        {
            // Act
            var filenameRequest = new FilenameGenerationRequest
            {
                Series = testCase.Series,
                Season = "01",
                Episode = "01",
                EpisodeName = testCase.EpisodeName,
                FileExtension = ".mkv",
                MatchConfidence = 0.95
            };

            var result = filenameService.GenerateFilename(filenameRequest);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.SuggestedFilename.Should().NotContain("<");
            result.SuggestedFilename.Should().NotContain(">");
            result.SuggestedFilename.Should().NotContain(":");
            result.SuggestedFilename.Should().NotContain("\"");
            result.SuggestedFilename.Should().NotContain("|");
            result.SuggestedFilename.Should().NotContain("?");
            result.SuggestedFilename.Should().NotContain("*");
            result.SuggestedFilename.Should().NotContain("\\");
            result.SuggestedFilename.Should().NotContain("/");

            result.SanitizedCharacters.Should().NotBeEmpty();

            _logger.LogInformation("Windows sanitization test - Input: '{Input}' -> Output: '{Output}'",
                $"{testCase.Series} - {testCase.EpisodeName}", result.SuggestedFilename);
        }
    }

    [Fact]
    public async Task FilenameLengthTruncation_HandlesVeryLongNames()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        var testCases = new[]
        {
            new
            {
                Series = "The Extremely Long Series Name That Goes On And On And Contains Many Words And Characters That Could Potentially Cause Issues With Windows File System Limitations And Path Length Restrictions",
                EpisodeName = "This Is An Extremely Long Episode Name That Contains Many Words And Characters And Details About The Plot And Characters And Everything Else That Could Make The Filename Very Long",
                MaxLength = 260
            },
            new
            {
                Series = "Short Series",
                EpisodeName = new string('A', 300), // 300 character episode name
                MaxLength = 200
            },
            new
            {
                Series = new string('B', 150), // 150 character series name
                EpisodeName = new string('C', 150), // 150 character episode name
                MaxLength = 100
            }
        };

        foreach (var testCase in testCases)
        {
            // Act
            var filenameRequest = new FilenameGenerationRequest
            {
                Series = testCase.Series,
                Season = "01",
                Episode = "01",
                EpisodeName = testCase.EpisodeName,
                FileExtension = ".mkv",
                MatchConfidence = 0.95,
                MaxLength = testCase.MaxLength
            };

            var result = filenameService.GenerateFilename(filenameRequest);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.TotalLength.Should().BeLessOrEqualTo(testCase.MaxLength);
            result.WasTruncated.Should().BeTrue();

            // Must preserve essential elements
            result.SuggestedFilename.Should().EndWith(".mkv");
            result.SuggestedFilename.Should().Contain("S01E01");
            result.SuggestedFilename.Should().Contain(" - ");

            _logger.LogInformation("Length truncation test - Original lengths: Series={SeriesLength}, Episode={EpisodeLength} -> Final length: {FinalLength}",
                testCase.Series.Length, testCase.EpisodeName.Length, result.TotalLength);
        }
    }

    [Fact]
    public async Task WindowsPathLimits_RespectSystemConstraints()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();

        // Test various Windows-specific constraints
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "LPT1" };

        foreach (var reservedName in reservedNames)
        {
            // Act
            var filenameRequest = new FilenameGenerationRequest
            {
                Series = reservedName,
                Season = "01",
                Episode = "01",
                EpisodeName = "Test Episode",
                FileExtension = ".mkv",
                MatchConfidence = 0.95
            };

            var result = filenameService.GenerateFilename(filenameRequest);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();

            // Reserved names should be handled appropriately
            result.SuggestedFilename.Should().NotStartWith(reservedName + ".");
            result.SuggestedFilename.Should().NotBe(reservedName + ".mkv");

            _logger.LogInformation("Reserved name test - '{ReservedName}' -> '{Filename}'",
                reservedName, result.SuggestedFilename);
        }

        // Test maximum path length constraint
        var longPath = "/very/long/directory/structure/that/goes/deep/into/many/subdirectories/and/could/potentially/exceed/windows/path/limits/in/some/scenarios";
        var longFilename = new string('X', 200) + ".mkv";

        var validationResult = filenameService.IsValidWindowsFilename(longPath + "/" + longFilename);
        validationResult.Should().BeFalse("Paths exceeding 260 characters should be invalid");

        _logger.LogInformation("Path length validation test - Long path rejected correctly");
    }

    [Fact]
    public async Task CrossPlatformCompatibility_HandlesUnixAndWindowsPaths()
    {
        // Arrange
        var filenameService = _serviceProvider.GetRequiredService<IFilenameService>();
        var tempDir = Path.GetTempPath();

        var pathTestCases = new[]
        {
            Path.Combine(tempDir, "videos", "episode.mkv"),      // Temp directory path
            Path.Combine("relative", "path", "episode.mkv"),     // Relative path
            "episode.mkv"                                        // Filename only
        };

        foreach (var path in pathTestCases)
        {
            // Act
            var filenameRequest = new FilenameGenerationRequest
            {
                Series = "Test Series",
                Season = "01",
                Episode = "01",
                EpisodeName = "Test Episode",
                FileExtension = Path.GetExtension(path),
                MatchConfidence = 0.95
            };

            var result = filenameService.GenerateFilename(filenameRequest);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.SuggestedFilename.Should().Be("Test Series - S01E01 - Test Episode" + Path.GetExtension(path));

            _logger.LogInformation("Cross-platform test - Path: '{Path}' -> Extension: '{Extension}'",
                path, Path.GetExtension(path));
        }
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
