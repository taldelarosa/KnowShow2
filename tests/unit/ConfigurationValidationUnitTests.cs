using System;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models.Configuration;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Unit;

public class ConfigurationValidationUnitTests : IDisposable
{
    private readonly ConfigurationService _configService;
    private readonly string _tempConfigPath;

    public ConfigurationValidationUnitTests()
    {
        _tempConfigPath = Path.GetTempFileName();
        _configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, null, _tempConfigPath);
    }

    public void Dispose()
    {
        if (File.Exists(_tempConfigPath))
            File.Delete(_tempConfigPath);
    }

    private static Configuration CreateValidMinimalConfiguration()
    {
        return new Configuration
        {
            Version = "2.0",
            MatchConfidenceThreshold = 0.6m,
            RenameConfidenceThreshold = 0.7m,
            FuzzyHashThreshold = 75,
            HashingAlgorithm = HashingAlgorithm.CTPH,
            FilenamePatterns = new EpisodeIdentifier.Core.Models.Configuration.FilenamePatterns
            {
                PrimaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$"
            },
            FilenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}",
            MaxConcurrency = 1,
            BulkProcessing = null // Explicitly set to null to avoid default value validation issues
        };
    }

    [Theory]
    [InlineData(0, 1)] // Below minimum should default to 1
    [InlineData(-1, 1)] // Negative should default to 1
    [InlineData(-100, 1)] // Very negative should default to 1
    [InlineData(101, 1)] // Above maximum should default to 1 (per spec)
    [InlineData(1000, 1)] // Very high should default to 1 (per spec)
    [InlineData(int.MaxValue, 1)] // Extreme value should default to 1 (per spec)
    public async Task LoadConfiguration_InvalidMaxConcurrency_ShouldClampToValidRange(int invalidValue, int expectedValue)
    {
        // Arrange
        var config = CreateValidMinimalConfiguration();
        config.MaxConcurrency = invalidValue;
        
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_tempConfigPath, json);

        // Act
        var result = await _configService.LoadConfiguration();
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.True(result.IsValid, $"Configuration should be valid. Errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(expectedValue, maxConcurrency);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task LoadConfiguration_ValidMaxConcurrency_ShouldRetainValue(int validValue)
    {
        // Arrange
        var config = CreateValidMinimalConfiguration();
        config.MaxConcurrency = validValue;
        
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_tempConfigPath, json);

        // Act
        var result = await _configService.LoadConfiguration();
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.True(result.IsValid, $"Configuration should be valid. Errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(validValue, maxConcurrency);
    }

    [Fact]
    public async Task LoadConfiguration_MissingMaxConcurrencyProperty_ShouldDefaultToOne()
    {
        // Arrange - JSON without maxConcurrency property
        var json = "{}";
        await File.WriteAllTextAsync(_tempConfigPath, json);

        // Act
        await _configService.LoadConfigurationAsync(_tempConfigPath);
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.Equal(1, maxConcurrency);
    }

    [Fact]
    public async Task LoadConfiguration_NullMaxConcurrencyProperty_ShouldDefaultToOne()
    {
        // Arrange - JSON with null maxConcurrency
        var json = @"{ ""maxConcurrency"": null }";
        await File.WriteAllTextAsync(_tempConfigPath, json);

        // Act
        await _configService.LoadConfigurationAsync(_tempConfigPath);
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.Equal(1, maxConcurrency);
    }

    [Fact]
    public async Task LoadConfiguration_EmptyFile_ShouldDefaultToOne()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempConfigPath, "");

        // Act
        await _configService.LoadConfigurationAsync(_tempConfigPath);
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.Equal(1, maxConcurrency);
    }

    [Fact]
    public async Task LoadConfiguration_InvalidJson_ShouldDefaultToOne()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempConfigPath, "{ invalid json }");

        // Act
        await _configService.LoadConfigurationAsync(_tempConfigPath);
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.Equal(1, maxConcurrency);
    }

    [Fact]
    public async Task LoadConfiguration_NonExistentFile_ShouldDefaultToOne()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non-existent-config.json");

        // Act
        await _configService.LoadConfigurationAsync(nonExistentPath);
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.Equal(1, maxConcurrency);
    }

    [Theory]
    [InlineData("string_value")]
    [InlineData("true")]
    [InlineData("false")]
    public async Task LoadConfiguration_InvalidMaxConcurrencyType_ShouldDefaultToOne(string invalidValue)
    {
        // Arrange - maxConcurrency as wrong type
        var json = $@"{{ ""maxConcurrency"": ""{invalidValue}"" }}";
        await File.WriteAllTextAsync(_tempConfigPath, json);

        // Act
        await _configService.LoadConfigurationAsync(_tempConfigPath);
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.Equal(1, maxConcurrency);
    }

    [Fact]
    public async Task LoadConfiguration_MaxConcurrencyAsDecimal_ShouldFailValidation()
    {
        // Arrange
        var json = @"{ ""maxConcurrency"": 5.7 }";
        await File.WriteAllTextAsync(_tempConfigPath, json);

        // Act
        var result = await _configService.LoadConfiguration();
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.False(result.IsValid, "Configuration should be invalid due to decimal value for integer property");
        Assert.Equal(1, maxConcurrency); // Should fall back to default
    }

    [Fact]
    public async Task LoadConfiguration_CorruptedFileWithValidMaxConcurrency_ShouldDefaultToOne()
    {
        // Arrange - Partially valid JSON that becomes corrupted
        var json = @"{ ""maxConcurrency"": 5, ""invalid"": ";
        await File.WriteAllTextAsync(_tempConfigPath, json);

        // Act
        await _configService.LoadConfigurationAsync(_tempConfigPath);
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.Equal(1, maxConcurrency);
    }

    [Fact]
    public async Task LoadConfiguration_VeryLargeFile_ShouldHandleGracefully()
    {
        // Arrange - Create a large corrupted JSON file  
        var largeConfig = CreateValidMinimalConfiguration();
        largeConfig.MaxConcurrency = 10;
        
        // Create valid JSON then corrupt it to test error handling with large files
        var json = JsonSerializer.Serialize(largeConfig, new JsonSerializerOptions { WriteIndented = true });
        var corruptedJson = json + new string(' ', 10000) + "invalid_json_content"; // Add corruption
        await File.WriteAllTextAsync(_tempConfigPath, corruptedJson);

        // Act
        var result = await _configService.LoadConfiguration();
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.False(result.IsValid, "Configuration should be invalid due to corrupted JSON");
        Assert.Equal(1, maxConcurrency); // Should default due to invalid JSON format
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    public async Task LoadConfiguration_BoundaryValues_ShouldValidateCorrectly(int inputValue, int expectedValue)
    {
        // Arrange
        var config = CreateValidMinimalConfiguration();
        config.MaxConcurrency = inputValue;
        
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_tempConfigPath, json);

        // Act
        await _configService.LoadConfigurationAsync(_tempConfigPath);
        var maxConcurrency = _configService.MaxConcurrency;

        // Assert
        Assert.Equal(expectedValue, maxConcurrency);
    }
}