using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Interfaces;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;
using System.Text.Json;
using System.IO;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for configuration hot-reload functionality with maxConcurrency changes.
/// These tests verify that MaxConcurrency changes are properly detected and applied during hot-reload.
/// </summary>
public class ConfigurationHotReloadContractTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IServiceProvider_serviceProvider;

    public ConfigurationHotReloadContractTests()
    {
        _fileSystem = new MockFileSystem();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<System.IO.Abstractions.IFileSystem>(_fileSystem);
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ConfigurationService_WhenMaxConcurrencyChanged_ShouldDetectHotReload()
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var initialConfig = new Configuration
        {
            MaxConcurrency = 1,
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        _fileSystem.AddFile(configPath, new MockFileData(JsonSerializer.Serialize(initialConfig)));

        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var originalResult = await configService.LoadConfiguration();
        
        // Create updated config with different MaxConcurrency
        var updatedConfig = new Configuration
        {
            MaxConcurrency = 10,
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        // Act - Update the config file
        _fileSystem.File.WriteAllText(configPath, JsonSerializer.Serialize(updatedConfig));
        var reloadedResult = await configService.LoadConfiguration();

        // Assert
        originalResult.IsValid.Should().BeTrue("original config should load successfully");
        originalResult.Configuration!.MaxConcurrency.Should().Be(1);
        
        reloadedResult.IsValid.Should().BeTrue("reloaded config should load successfully");
        reloadedResult.Configuration!.MaxConcurrency.Should().Be(10);
        reloadedResult.Configuration.MaxConcurrency.Should().NotBe(originalResult.Configuration.MaxConcurrency);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(10, 1)]
    [InlineData(50, 100)]
    [InlineData(100, 25)]
    public async Task ConfigurationService_MaxConcurrencyHotReload_HandlesValidRangeChanges(int initialConcurrency, int newConcurrency)
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var initialConfig = CreateTestConfiguration(initialConcurrency);
        var updatedConfig = CreateTestConfiguration(newConcurrency);

        _fileSystem.AddFile(configPath, new MockFileData(JsonSerializer.Serialize(initialConfig)));

        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var originalResult = await configService.LoadConfiguration();

        // Act
        _fileSystem.File.WriteAllText(configPath, JsonSerializer.Serialize(updatedConfig));
        var reloadedResult = await configService.LoadConfiguration();

        // Assert
        originalResult.IsValid.Should().BeTrue("original config should load successfully");
        originalResult.Configuration!.MaxConcurrency.Should().Be(initialConcurrency);
        
        reloadedResult.IsValid.Should().BeTrue("reloaded config should load successfully");
        reloadedResult.Configuration!.MaxConcurrency.Should().Be(newConcurrency);
    }

    [Theory]
    [InlineData(5, 0, 1)]     // Invalid new value should fallback to default
    [InlineData(10, 101, 1)]  // Invalid new value should fallback to default  
    [InlineData(25, -5, 1)]   // Invalid new value should fallback to default
    public async Task ConfigurationService_MaxConcurrencyHotReload_HandlesInvalidValues(int initialConcurrency, int invalidNewConcurrency, int expectedFallback)
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var initialConfig = CreateTestConfiguration(initialConcurrency);
        var invalidConfig = CreateTestConfiguration(invalidNewConcurrency);

        _fileSystem.AddFile(configPath, new MockFileData(JsonSerializer.Serialize(initialConfig)));

        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var originalResult = await configService.LoadConfiguration();

        // Act
        _fileSystem.File.WriteAllText(configPath, JsonSerializer.Serialize(invalidConfig));
        var reloadedResult = await configService.LoadConfiguration();

        // Assert
        originalResult.IsValid.Should().BeTrue("original config should load successfully");
        originalResult.Configuration!.MaxConcurrency.Should().Be(initialConcurrency);
        
        // Invalid config should either fail validation or fallback gracefully
        if (reloadedResult.IsValid)
        {
            reloadedResult.Configuration!.MaxConcurrency.Should().Be(expectedFallback, 
                $"invalid value {invalidNewConcurrency} should fallback to {expectedFallback}");
        }
        else
        {
            reloadedResult.Errors.Should().NotBeEmpty("invalid config should have validation errors");
        }
    }

    [Fact]
    public async Task ConfigurationService_MaxConcurrencyHotReload_PreservesOtherSettings()
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var initialConfig = new Configuration
        {
            MaxConcurrency = 5,
            Version = "2.0",
            MatchConfidenceThreshold = 0.75m,
            RenameConfidenceThreshold = 0.80m,
            FuzzyHashThreshold = 80,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        var updatedConfig = new Configuration
        {
            MaxConcurrency = 15, // Only change this
            Version = "2.0",
            MatchConfidenceThreshold = 0.75m,
            RenameConfidenceThreshold = 0.80m,
            FuzzyHashThreshold = 80,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        _fileSystem.AddFile(configPath, new MockFileData(JsonSerializer.Serialize(initialConfig)));

        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var originalResult = await configService.LoadConfiguration();

        // Act
        _fileSystem.File.WriteAllText(configPath, JsonSerializer.Serialize(updatedConfig));
        var reloadedResult = await configService.LoadConfiguration();

        // Assert
        originalResult.IsValid.Should().BeTrue("original config should load successfully");
        reloadedResult.IsValid.Should().BeTrue("reloaded config should load successfully");
        
        var originalConfig = originalResult.Configuration!;
        var reloadedConfig = reloadedResult.Configuration!;
        
        reloadedConfig.MaxConcurrency.Should().Be(15);
        reloadedConfig.Version.Should().Be(originalConfig.Version);
        reloadedConfig.MatchConfidenceThreshold.Should().Be(originalConfig.MatchConfidenceThreshold);
        reloadedConfig.RenameConfidenceThreshold.Should().Be(originalConfig.RenameConfidenceThreshold);
        reloadedConfig.FuzzyHashThreshold.Should().Be(originalConfig.FuzzyHashThreshold);
        reloadedConfig.HashingAlgorithm.Should().Be(originalConfig.HashingAlgorithm);
        reloadedConfig.FilenamePatterns.PrimaryPattern.Should().Be(originalConfig.FilenamePatterns.PrimaryPattern);
        reloadedConfig.FilenameTemplate.Should().Be(originalConfig.FilenameTemplate);
    }

    [Fact] 
    public async Task ConfigurationService_CorruptedConfigDuringHotReload_ShouldHandleGracefully()
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var initialConfig = CreateTestConfiguration(5);
        var corruptedJson = "{\"MaxConcurrency\": invalid_json";

        _fileSystem.AddFile(configPath, new MockFileData(JsonSerializer.Serialize(initialConfig)));

        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var originalResult = await configService.LoadConfiguration();

        // Act & Assert - This should not crash and should handle the corruption
        var action = async () =>
        {
            _fileSystem.File.WriteAllText(configPath, corruptedJson);
            return await configService.LoadConfiguration();
        };

        await action.Should().NotThrowAsync("corrupted config file should be handled gracefully");
        
        var result = await action();
        // Should either return the original config or a default config, not null
        result.Should().NotBeNull("should return a valid configuration result even when file is corrupted");
        
        // The result should either be valid with a fallback config, or invalid with errors
        if (result.IsValid)
        {
            result.Configuration.Should().NotBeNull("valid result should have configuration");
            result.Configuration!.MaxConcurrency.Should().BeInRange(1, 100, "should have a valid MaxConcurrency value");
        }
        else
        {
            result.Errors.Should().NotBeEmpty("invalid result should contain error details");
        }
    }

    [Fact]
    public async Task ConfigurationService_MissingMaxConcurrencyAfterHotReload_ShouldUseDefault()
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var initialConfig = CreateTestConfiguration(10);
        
        // Config without MaxConcurrency property
        var configWithoutMaxConcurrency = new
        {
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = "CTPH",
            FilenamePatterns = new { PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$" },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        _fileSystem.AddFile(configPath, new MockFileData(JsonSerializer.Serialize(initialConfig)));

        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var originalResult = await configService.LoadConfiguration();

        // Act
        _fileSystem.File.WriteAllText(configPath, JsonSerializer.Serialize(configWithoutMaxConcurrency));
        var reloadedResult = await configService.LoadConfiguration();

        // Assert
        originalResult.IsValid.Should().BeTrue("original config should load successfully");
        originalResult.Configuration!.MaxConcurrency.Should().Be(10);
        
        reloadedResult.IsValid.Should().BeTrue("reloaded config should load successfully");
        reloadedResult.Configuration!.MaxConcurrency.Should().Be(1, "missing MaxConcurrency should default to 1");
    }

    private Configuration CreateTestConfiguration(int maxConcurrency)
    {
        return new Configuration
        {
            MaxConcurrency = maxConcurrency,
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };
    }
}
