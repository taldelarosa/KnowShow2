using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Interfaces;
using System.Text.Json;
using System.IO;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for configuration loading and validation functionality.
/// These tests verify end-to-end configuration behavior with real file system operations.
/// All tests MUST FAIL until the full integration is implemented.
/// </summary>
public class ConfigurationLoadingTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<ConfigurationLoadingTests>_logger;
    private readonly List<string> _testFilesToCleanup = new();
    private readonly string_testConfigDirectory;

    public ConfigurationLoadingTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Register services - This will fail until services are implemented
        services.AddScoped<IConfigurationService, ConfigurationService>();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<ConfigurationLoadingTests>>();

        _testConfigDirectory = AppContext.BaseDirectory;
    }

    [Fact]
    public async Task LoadConfiguration_WithValidConfigFile_LoadsSuccessfully()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configPath = CreateValidTestConfig();
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        // Act
        var result = await configService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Version.Should().Be("2.0");
        result.Configuration.MatchConfidenceThreshold.Should().Be(0.8m);
        result.Configuration.RenameConfidenceThreshold.Should().Be(0.85m);
        result.Configuration.FuzzyHashThreshold.Should().Be(75);
        result.Configuration.HashingAlgorithm.Should().Be(HashingAlgorithm.CTPH);
        result.Errors.Should().BeEmpty();

        _logger.LogInformation("Successfully loaded configuration from {ConfigPath}", configPath);
    }

    [Fact]
    public async Task LoadConfiguration_WithMissingConfigFile_UsesDefaults()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        // No config file created - should use defaults from current config

        // Act
        var result = await configService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        if (result.IsValid)
        {
            // Should use defaults from existing config
            result.Configuration.Should().NotBeNull();
            result.Configuration!.MatchConfidenceThreshold.Should().BeInRange(0.0m, 1.0m);
        }
        else
        {
            // Should indicate missing file
            result.Errors.Should().Contain(error => error.Contains("Configuration file not found"));
        }

        _logger.LogInformation("Handled missing configuration file appropriately");
    }

    [Fact]
    public async Task LoadConfiguration_WithInvalidJsonSyntax_ReturnsValidationErrors()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configPath = CreateInvalidJsonConfig();
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        // Act
        var result = await configService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(error => error.Contains("JSON") || error.Contains("syntax"));

        _logger.LogWarning("Properly handled invalid JSON syntax in config file");
    }

    [Fact]
    public async Task LoadConfiguration_WithInvalidThresholdValues_ReturnsValidationErrors()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configPath = CreateConfigWithInvalidThresholds();
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        // Act
        var result = await configService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(error =>
            error.Contains("MatchConfidenceThreshold") ||
            error.Contains("RenameConfidenceThreshold") ||
            error.Contains("FuzzyHashThreshold"));

        _logger.LogWarning("Properly validated threshold constraints");
    }

    [Fact]
    public async Task LoadConfiguration_WithMissingRequiredFields_ReturnsValidationErrors()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configPath = CreateConfigWithMissingFields();
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        // Act
        var result = await configService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(error =>
            error.Contains("filenameTemplate") || error.Contains("required"));

        _logger.LogWarning("Properly validated required fields");
    }

    [Fact]
    public async Task ReloadConfiguration_WhenFileChanges_DetectsChanges()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configPath = CreateValidTestConfig();
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        // Load initial config
        await configService.LoadConfiguration();

        // Modify the config file
        await Task.Delay(100); // Ensure different timestamp
        ModifyConfigFile(configPath);

        // Act
        var wasReloaded = await configService.ReloadIfChanged();

        // Assert
        wasReloaded.Should().BeTrue();

        _logger.LogInformation("Successfully detected and reloaded configuration changes");
    }

    private string CreateValidTestConfig()
    {
        var config = new
        {
            version = "2.0",
            matchConfidenceThreshold = 0.8,
            renameConfidenceThreshold = 0.85,
            fuzzyHashThreshold = 75,
            hashingAlgorithm = "CTPH",
            filenamePatterns = new
            {
                primaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
                secondaryPattern = @"^(?<SeriesName>.+?)\s(?<Season>\d+)x(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
                tertiaryPattern = @"^(?<SeriesName>.+?)\.S(?<Season>\d+)\.E(?<Episode>\d+)(?:\.(?<EpisodeName>.+?))?$"
            },
            filenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
        };

        var configPath = Path.Combine(_testConfigDirectory, "episodeidentifier.config.json");
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        // Ensure directory exists
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(configPath, json);
        _testFilesToCleanup.Add(configPath);

        return configPath;
    }

    private string CreateInvalidJsonConfig()
    {
        var configPath = Path.Combine(_testConfigDirectory, "episodeidentifier.config.json");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(configPath, "{ invalid json syntax missing quotes and commas }");
        _testFilesToCleanup.Add(configPath);

        return configPath;
    }

    private string CreateConfigWithInvalidThresholds()
    {
        var config = new
        {
            version = "2.0",
            matchConfidenceThreshold = 1.5, // Invalid - > 1.0
            renameConfidenceThreshold = 0.7, // Invalid - less than match threshold
            fuzzyHashThreshold = 150, // Invalid - > 100
            hashingAlgorithm = "CTPH"
        };

        var configPath = Path.Combine(_testConfigDirectory, "episodeidentifier.config.json");
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        // Ensure directory exists
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(configPath, json);
        _testFilesToCleanup.Add(configPath);

        return configPath;
    }

    private string CreateConfigWithMissingFields()
    {
        var config = new
        {
            version = "2.0",
            matchConfidenceThreshold = 0.8
            // Missing required fields: filenameTemplate, filenamePatterns
        };

        var configPath = Path.Combine(_testConfigDirectory, "episodeidentifier.config.json");
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        // Ensure directory exists
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(configPath, json);
        _testFilesToCleanup.Add(configPath);

        return configPath;
    }

    private void ModifyConfigFile(string configPath)
    {
        var config = new
        {
            version = "2.1", // Changed version to trigger reload
            matchConfidenceThreshold = 0.85, // Changed threshold
            renameConfidenceThreshold = 0.9,
            fuzzyHashThreshold = 80,
            hashingAlgorithm = "CTPH",
            filenamePatterns = new
            {
                primaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
                secondaryPattern = @"^(?<SeriesName>.+?)\s(?<Season>\d+)x(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
                tertiaryPattern = @"^(?<SeriesName>.+?)\.S(?<Season>\d+)\.E(?<Episode>\d+)(?:\.(?<EpisodeName>.+?))?$"
            },
            filenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
        };

        // Ensure directory exists
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
    }

    public void Dispose()
    {
        foreach (var file in _testFilesToCleanup)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup test file: {FilePath}", file);
            }
        }

        _serviceProvider.Dispose();
    }
}
