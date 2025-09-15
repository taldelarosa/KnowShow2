using System.IO.Abstractions.TestingHelpers;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for configuration service hot-reload functionality.
/// Tests configuration changes and their impact on bulk processing operations.
/// </summary>
public class ConfigurationIntegrationTests : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly ConfigurationService _configService;
    private readonly string _configPath;

    public ConfigurationIntegrationTests()
    {
        _fileSystem = new MockFileSystem();
        _logger = NullLogger<ConfigurationService>.Instance;
        _configService = new ConfigurationService(_logger, _fileSystem);
        _configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
    }

    public void Dispose()
    {
        // No cleanup needed for MockFileSystem
    }

    [Fact]
    public async Task LoadConfiguration_WithBulkProcessingSettings_ShouldLoadCorrectly()
    {
        // Arrange
        var configJson = CreateConfigWithBulkProcessing();
        _fileSystem.AddFile(_configPath, new MockFileData(configJson));

        // Act
        var result = await _configService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
        result.Configuration!.BulkProcessing.Should().NotBeNull();

        var bulkConfig = result.Configuration.BulkProcessing!;
        bulkConfig.DefaultBatchSize.Should().Be(200);
        bulkConfig.DefaultMaxConcurrency.Should().Be(8);
        bulkConfig.DefaultProgressReportingInterval.Should().Be(2000);
        bulkConfig.DefaultForceGarbageCollection.Should().BeTrue();
        bulkConfig.DefaultCreateBackups.Should().BeFalse();
        bulkConfig.DefaultContinueOnError.Should().BeTrue();
        bulkConfig.DefaultFileProcessingTimeout.Should().Be(TimeSpan.FromMinutes(10));
        bulkConfig.MaxBatchSize.Should().Be(5000);
        bulkConfig.MaxConcurrency.Should().Be(64);
        bulkConfig.EnableBatchStatistics.Should().BeTrue();
        bulkConfig.EnableMemoryMonitoring.Should().BeTrue();
    }

    [Fact]
    public async Task LoadConfiguration_WithInvalidBulkProcessingSettings_ShouldFailValidation()
    {
        // Arrange
        var configJson = CreateInvalidBulkProcessingConfig();
        _fileSystem.AddFile(_configPath, new MockFileData(configJson));

        // Act
        var result = await _configService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();

        // Should have specific validation errors
        result.Errors.Should().Contain(error => error.Contains("DefaultBatchSize"));
        result.Errors.Should().Contain(error => error.Contains("DefaultMaxConcurrency"));
        result.Errors.Should().Contain(error => error.Contains("DefaultProgressReportingInterval"));
    }

    [Fact]
    public async Task LoadConfiguration_WithConstraintViolations_ShouldFailValidation()
    {
        // Arrange
        var configJson = CreateConfigWithConstraintViolations();
        _fileSystem.AddFile(_configPath, new MockFileData(configJson));

        // Act
        var result = await _configService.LoadConfiguration();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(error =>
            error.Contains("DefaultBatchSize") && error.Contains("MaxBatchSize"));
        result.Errors.Should().Contain(error =>
            error.Contains("DefaultMaxConcurrency") && error.Contains("MaxConcurrency"));
    }

    [Fact]
    public async Task ReloadIfChanged_WithConfigurationChanges_ShouldDetectChanges()
    {
        // Arrange
        var initialConfig = CreateConfigWithBulkProcessing();
        _fileSystem.AddFile(_configPath, new MockFileData(initialConfig));

        // Load initial configuration
        var initialResult = await _configService.LoadConfiguration();
        initialResult.IsValid.Should().BeTrue();

        // Act - Update the configuration file
        var updatedConfig = CreateUpdatedBulkProcessingConfig();
        _fileSystem.AddFile(_configPath, new MockFileData(updatedConfig));

        // Simulate file timestamp change
        await Task.Delay(10);

        var reloaded = await _configService.ReloadIfChanged();

        // Assert
        reloaded.Should().BeTrue();

        // Verify the new configuration is loaded
        var newResult = await _configService.LoadConfiguration();
        newResult.IsValid.Should().BeTrue();
        newResult.Configuration!.BulkProcessing!.DefaultBatchSize.Should().Be(300); // Changed value
    }

    [Fact]
    public async Task ValidateConfiguration_WithComplexBulkProcessingRules_ShouldValidateCorrectly()
    {
        // Arrange
        var config = CreateComplexValidConfiguration();

        // Act
        var result = _configService.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithEdgeCaseValues_ShouldHandleCorrectly()
    {
        // Arrange - Test edge cases
        var config = CreateValidConfiguration();
        config.BulkProcessing = new BulkProcessingConfiguration
        {
            DefaultBatchSize = 1, // Minimum value
            DefaultMaxConcurrency = 1, // Minimum value
            DefaultProgressReportingInterval = 100, // Minimum value
            DefaultFileProcessingTimeout = TimeSpan.FromSeconds(1), // Minimum value
            DefaultMaxErrorsBeforeAbort = 1, // Minimum value
            MaxBatchSize = 50000, // Maximum value
            MaxConcurrency = 500, // Maximum value
            DefaultFileExtensions = new List<string> { ".mkv", ".mp4", ".avi", ".mov", ".wmv" } // Multiple extensions
        };

        // Act
        var result = _configService.ValidateConfiguration(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadConfiguration_WithPartialBulkProcessingConfig_ShouldUseDefaults()
    {
        // Arrange
        var configJson = CreatePartialBulkProcessingConfig();
        _fileSystem.AddFile(_configPath, new MockFileData(configJson));

        // Act
        var result = await _configService.LoadConfiguration();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Configuration!.BulkProcessing.Should().NotBeNull();

        var bulkConfig = result.Configuration.BulkProcessing!;
        bulkConfig.DefaultBatchSize.Should().Be(150); // From JSON
        bulkConfig.DefaultMaxConcurrency.Should().Be(Environment.ProcessorCount); // Default
        bulkConfig.DefaultProgressReportingInterval.Should().Be(1000); // Default
        bulkConfig.EnableMemoryMonitoring.Should().BeTrue(); // Default
    }

    private static string CreateConfigWithBulkProcessing()
    {
        return @"{
            ""version"": ""2.0"",
            ""matchConfidenceThreshold"": 0.8,
            ""renameConfidenceThreshold"": 0.85,
            ""fuzzyHashThreshold"": 75,
            ""hashingAlgorithm"": ""CTPH"",
            ""filenamePatterns"": {
                ""primaryPattern"": ""^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$""
            },
            ""filenameTemplate"": ""{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"",
            ""bulkProcessing"": {
                ""defaultBatchSize"": 200,
                ""defaultMaxConcurrency"": 8,
                ""defaultProgressReportingInterval"": 2000,
                ""defaultForceGarbageCollection"": true,
                ""defaultCreateBackups"": false,
                ""defaultContinueOnError"": true,
                ""defaultFileProcessingTimeout"": ""00:10:00"",
                ""defaultFileExtensions"": ["".mkv"", "".mp4"", "".avi""],
                ""maxBatchSize"": 5000,
                ""maxConcurrency"": 64,
                ""enableBatchStatistics"": true,
                ""enableMemoryMonitoring"": true
            }
        }";
    }

    private static string CreateInvalidBulkProcessingConfig()
    {
        return @"{
            ""version"": ""2.0"",
            ""matchConfidenceThreshold"": 0.8,
            ""renameConfidenceThreshold"": 0.85,
            ""fuzzyHashThreshold"": 75,
            ""hashingAlgorithm"": ""CTPH"",
            ""filenamePatterns"": {
                ""primaryPattern"": ""^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$""
            },
            ""filenameTemplate"": ""{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"",
            ""bulkProcessing"": {
                ""defaultBatchSize"": -1,
                ""defaultMaxConcurrency"": 101,
                ""defaultProgressReportingInterval"": 50,
                ""maxBatchSize"": 50001,
                ""maxConcurrency"": 501
            }
        }";
    }

    private static string CreateConfigWithConstraintViolations()
    {
        return @"{
            ""version"": ""2.0"",
            ""matchConfidenceThreshold"": 0.8,
            ""renameConfidenceThreshold"": 0.85,
            ""fuzzyHashThreshold"": 75,
            ""hashingAlgorithm"": ""CTPH"",
            ""filenamePatterns"": {
                ""primaryPattern"": ""^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$""
            },
            ""filenameTemplate"": ""{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"",
            ""bulkProcessing"": {
                ""defaultBatchSize"": 1000,
                ""maxBatchSize"": 500,
                ""defaultMaxConcurrency"": 50,
                ""maxConcurrency"": 25
            }
        }";
    }

    private static string CreateUpdatedBulkProcessingConfig()
    {
        return @"{
            ""version"": ""2.0"",
            ""matchConfidenceThreshold"": 0.8,
            ""renameConfidenceThreshold"": 0.85,
            ""fuzzyHashThreshold"": 75,
            ""hashingAlgorithm"": ""CTPH"",
            ""filenamePatterns"": {
                ""primaryPattern"": ""^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$""
            },
            ""filenameTemplate"": ""{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"",
            ""bulkProcessing"": {
                ""defaultBatchSize"": 300,
                ""defaultMaxConcurrency"": 12,
                ""enableBatchStatistics"": false
            }
        }";
    }

    private static string CreatePartialBulkProcessingConfig()
    {
        return @"{
            ""version"": ""2.0"",
            ""matchConfidenceThreshold"": 0.8,
            ""renameConfidenceThreshold"": 0.85,
            ""fuzzyHashThreshold"": 75,
            ""hashingAlgorithm"": ""CTPH"",
            ""filenamePatterns"": {
                ""primaryPattern"": ""^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$""
            },
            ""filenameTemplate"": ""{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"",
            ""bulkProcessing"": {
                ""defaultBatchSize"": 150,
                ""maxBatchSize"": 3000
            }
        }";
    }

    private static Configuration CreateValidConfiguration()
    {
        return new Configuration
        {
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new EpisodeIdentifier.Core.Models.Configuration.FilenamePatterns
            {
                PrimaryPattern = "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
        };
    }

    private static Configuration CreateComplexValidConfiguration()
    {
        return new Configuration
        {
            Version = "2.1.3",
            MatchConfidenceThreshold = 0.75m,
            RenameConfidenceThreshold = 0.9m,
            FuzzyHashThreshold = 80,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new EpisodeIdentifier.Core.Models.Configuration.FilenamePatterns
            {
                PrimaryPattern = "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}",
            BulkProcessing = new BulkProcessingConfiguration
            {
                DefaultBatchSize = 500,
                DefaultMaxConcurrency = 16,
                DefaultProgressReportingInterval = 3000,
                DefaultForceGarbageCollection = false,
                DefaultCreateBackups = true,
                DefaultContinueOnError = false,
                DefaultMaxErrorsBeforeAbort = 10,
                DefaultFileProcessingTimeout = TimeSpan.FromMinutes(15),
                DefaultFileExtensions = new List<string> { ".mkv", ".mp4", ".avi", ".mov" },
                MaxBatchSize = 10000,
                MaxConcurrency = 128,
                EnableBatchStatistics = true,
                EnableMemoryMonitoring = true
            }
        };
    }
}