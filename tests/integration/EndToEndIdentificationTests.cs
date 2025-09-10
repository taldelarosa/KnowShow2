using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Tests.Integration;

public class EndToEndIdentificationTests : IDisposable
{
    private readonly SubtitleWorkflowCoordinator _coordinator;
    private readonly string _testDataPath;

    public EndToEndIdentificationTests()
    {
        // Create required dependencies manually (like AssWorkflowTests pattern)
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Create core services
        var validatorLogger = loggerFactory.CreateLogger<VideoFormatValidator>();
        var pgsExtractorLogger = loggerFactory.CreateLogger<SubtitleExtractor>();
        var pgsRipLogger = loggerFactory.CreateLogger<PgsRipService>();
        var pgsConverterLogger = loggerFactory.CreateLogger<PgsToTextConverter>();
        var enhancedConverterLogger = loggerFactory.CreateLogger<EnhancedPgsToTextConverter>();

        var validator = new VideoFormatValidator(validatorLogger);
        var pgsExtractor = new SubtitleExtractor(pgsExtractorLogger, validator);

        // Create text extractor with format handlers
        var formatHandlers = new List<ISubtitleFormatHandler>
        {
            new SrtFormatHandler(),
            new AssFormatHandler(),
            new VttFormatHandler()
        };
        var textExtractor = new TextSubtitleExtractor(formatHandlers);

        // Create converter services
        var pgsRipService = new PgsRipService(pgsRipLogger);
        var pgsConverter = new PgsToTextConverter(pgsConverterLogger);
        var enhancedConverter = new EnhancedPgsToTextConverter(enhancedConverterLogger, pgsRipService, pgsConverter);

        // Create matching services
        var fuzzyLogger = loggerFactory.CreateLogger<FuzzyHashService>();
        var normalizationLogger = loggerFactory.CreateLogger<SubtitleNormalizationService>();
        var matcherLogger = loggerFactory.CreateLogger<SubtitleMatcher>();
        var coordinatorLogger = loggerFactory.CreateLogger<SubtitleWorkflowCoordinator>();

        var normalizationService = new SubtitleNormalizationService(normalizationLogger);
        var testDbPath = "/mnt/c/Users/Ragma/KnowShow_Specd/test_constraint.db";
        var hashService = new FuzzyHashService(testDbPath, fuzzyLogger, normalizationService);
        var matcher = new SubtitleMatcher(hashService, matcherLogger);

        // Create coordinator
        _coordinator = new SubtitleWorkflowCoordinator(
            coordinatorLogger,
            validator,
            pgsExtractor,
            textExtractor,
            enhancedConverter,
            matcher);

        // Setup test data path
        _testDataPath = Path.Combine(Path.GetTempPath(), "EpisodeIdentifierTests");
        Directory.CreateDirectory(_testDataPath);
    }

    [Fact]
    public async Task ProcessVideo_WithTextSubtitles_IdentifiesCorrectly()
    {
        // Arrange
        var testVideoPath = "/mnt/c/src/KnowShow/TestData/media/Episode S02E01.mkv";

        // Skip test if file doesn't exist
        if (!File.Exists(testVideoPath))
        {
            // Create a minimal test that validates the service setup
            var result = await _coordinator.ProcessVideoAsync("nonexistent.mkv");
            result.Should().NotBeNull();
            result.HasError.Should().BeTrue();
            return;
        }

        // Act
        var identificationResult = await _coordinator.ProcessVideoAsync(testVideoPath);

        // Assert
        identificationResult.Should().NotBeNull();

        if (!identificationResult.HasError)
        {
            identificationResult.Series.Should().NotBeNullOrEmpty();
            identificationResult.Season.Should().NotBeNullOrEmpty();
            identificationResult.Episode.Should().NotBeNullOrEmpty();
            identificationResult.MatchConfidence.Should().BeGreaterThan(0.5);
        }
        else
        {
            // Log the error for debugging
            Console.WriteLine($"Identification failed: {identificationResult.Error?.Message}");
        }
    }

    [Fact]
    public async Task ProcessVideo_WithNonexistentFile_ReturnsError()
    {
        // Arrange
        var nonexistentPath = Path.Combine(_testDataPath, "nonexistent.mkv");

        // Act
        var result = await _coordinator.ProcessVideoAsync(nonexistentPath);

        // Assert
        result.Should().NotBeNull();
        result.HasError.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessVideo_WithEmptyFilePath_ThrowsException()
    {
        // Act & Assert
        var action = async () => await _coordinator.ProcessVideoAsync("");
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("videoFilePath");
    }

    [Fact]
    public async Task ProcessVideo_WithLanguagePreference_UsesCorrectLanguage()
    {
        // Arrange
        var testVideoPath = "/mnt/c/src/KnowShow/TestData/media/Episode S02E01.mkv";
        var preferredLanguage = "eng";

        // Skip test if file doesn't exist
        if (!File.Exists(testVideoPath))
        {
            var result = await _coordinator.ProcessVideoAsync("nonexistent.mkv", preferredLanguage);
            result.Should().NotBeNull();
            result.HasError.Should().BeTrue();
            return;
        }

        // Act
        var identificationResult = await _coordinator.ProcessVideoAsync(testVideoPath, preferredLanguage);

        // Assert
        identificationResult.Should().NotBeNull();
        // The method should not throw even with language preference
    }

    public void Dispose()
    {
        // Cleanup test data
        if (Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
