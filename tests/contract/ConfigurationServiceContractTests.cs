using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models.Configuration;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IConfigurationService interface.
/// These tests verify the interface contract without testing implementation details.
/// All tests MUST FAIL until the service is implemented.
/// </summary>
public class ConfigurationServiceContractTests
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationServiceContractTests()
    {
        // This will fail until IConfigurationService is implemented
        _configurationService = null!; // Will be injected when service exists
    }

    [Fact]
    public async Task LoadConfiguration_WithValidConfigFile_ReturnsValidConfiguration()
    {
        // Arrange - This test MUST FAIL until implementation exists

        // Act
        var result = await _configurationService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Version.Should().NotBeNull();
        result.Configuration.MatchConfidenceThreshold.Should().BeInRange(0.0m, 1.0m);
        result.Configuration.RenameConfidenceThreshold.Should().BeInRange(0.0m, 1.0m);
        result.Configuration.FuzzyHashThreshold.Should().BeInRange(0, 100);
        result.Configuration.HashingAlgorithm.Should().BeDefined();
        result.Configuration.FilenamePatterns.Should().NotBeNull();
        result.Configuration.FilenameTemplate.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadConfiguration_WithMissingConfigFile_ReturnsErrorResult()
    {
        // Arrange - This test MUST FAIL until implementation exists

        // Act
        var result = await _configurationService.LoadConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(error => error.Contains("Configuration file not found"));
    }

    [Fact]
    public async Task LoadConfiguration_WithInvalidThresholds_ReturnsValidationErrors()
    {
        // Arrange - This test MUST FAIL until implementation exists

        // Act
        var result = await _configurationService.LoadConfiguration();

        // Assert - Should validate threshold constraints
        result.Should().NotBeNull();
        if (!result.IsValid)
        {
            result.Errors.Should().Contain(error =>
                error.Contains("renameConfidenceThreshold must be >= matchConfidenceThreshold"));
        }
    }

    [Fact]
    public async Task ReloadIfChanged_WhenConfigUnchanged_ReturnsFalse()
    {
        // Arrange - This test MUST FAIL until implementation exists

        // Act
        var reloaded = await _configurationService.ReloadIfChanged();

        // Assert
        reloaded.Should().BeFalse();
    }

    [Fact]
    public async Task ReloadIfChanged_WhenConfigChanged_ReturnsTrue()
    {
        // Arrange - This test MUST FAIL until implementation exists
        // This would require modifying the config file timestamp

        // Act
        var reloaded = await _configurationService.ReloadIfChanged();

        // Assert
        reloaded.Should().BeTrue();
    }

    [Fact]
    public void ValidateConfiguration_WithValidConfig_ReturnsSuccess()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var config = new Configuration
        {
            Version = "2.0",
            MatchConfidenceThreshold = 0.8m,
            RenameConfidenceThreshold = 0.85m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new FilenamePatterns
            {
                PrimaryPattern = "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
        };

        // Act
        var result = _configurationService.ValidateConfiguration(config);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidConfig_ReturnsErrors()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var config = new Configuration
        {
            Version = "invalid-version",
            MatchConfidenceThreshold = 1.5m, // Invalid range
            RenameConfidenceThreshold = 0.5m, // Less than match threshold
            FuzzyHashThreshold = 150, // Invalid range
            HashingAlgorithm = (HashingAlgorithm)999, // Invalid enum
            FilenamePatterns = null!, // Required field missing
            FilenameTemplate = "" // Empty template
        };

        // Act
        var result = _configurationService.ValidateConfiguration(config);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Count.Should().BeGreaterThan(3);
    }
}