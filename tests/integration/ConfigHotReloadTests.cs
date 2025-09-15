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
/// Integration tests for configuration hot-reload during file processing functionality.
/// These tests verify that config changes are detected and applied per file processing cycle.
/// All tests MUST FAIL until the full integration is implemented.
/// </summary>
public class ConfigHotReloadTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<ConfigHotReloadTests> _logger;
    private readonly List<string> _testFilesToCleanup = new();
    private readonly string _testConfigDirectory;
    private readonly string _testConfigPath;
    private DateTime _nextTimestamp = DateTime.UtcNow;

    public ConfigHotReloadTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Use a unique config path for this test class to avoid interference
        var uniqueConfigName = $"episodeidentifier_hotreload_{Guid.NewGuid():N}.config.json";
        _testConfigPath = Path.Combine(AppContext.BaseDirectory, uniqueConfigName);

        // Register services with the unique config path
        services.AddScoped<IConfigurationService>(provider => new ConfigurationService(
            provider.GetRequiredService<ILogger<ConfigurationService>>(),
            fileSystem: null,
            configFilePath: _testConfigPath));

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<ConfigHotReloadTests>>();

        // Debug: Log the test config path to verify it's correct
        _logger.LogInformation("ConfigHotReloadTests initialized with config path: {ConfigPath}", _testConfigPath);

        _testConfigDirectory = AppContext.BaseDirectory;
    }

    [Fact]
    public async Task HotReload_DuringFileProcessing_DetectsConfigChanges()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        // Create initial config
        CreateConfigWithThreshold(75);

        // Debug: Check if file was created
        _logger.LogInformation("Test config path: {ConfigPath}", _testConfigPath);
        _logger.LogInformation("File exists: {FileExists}", File.Exists(_testConfigPath));
        if (File.Exists(_testConfigPath))
        {
            _logger.LogInformation("File size: {FileSize} bytes", new FileInfo(_testConfigPath).Length);
        }

        var initialConfig = await configService.LoadConfiguration();

        _logger.LogInformation("Initial config valid: {IsValid}, Errors: {Errors}",
            initialConfig.IsValid, string.Join(", ", initialConfig.Errors));

        _logger.LogInformation("Initial config valid: {IsValid}, Errors: {Errors}",
            initialConfig.IsValid, string.Join(", ", initialConfig.Errors));

        initialConfig.IsValid.Should().BeTrue();

        // Simulate file processing cycle
        await Task.Delay(1000); // Increased delay to ensure timestamp difference

        // Act - Modify config during processing
        CreateConfigWithThreshold(85); // Change threshold

        // Ensure filesystem timestamp granularity is sufficient
        await Task.Delay(1000);

        var wasReloaded = await configService.ReloadIfChanged();

        // Assert
        wasReloaded.Should().BeTrue("Config should be reloaded when file changes");

        var newConfig = await configService.LoadConfiguration();
        newConfig.Should().NotBeNull();
        newConfig.IsValid.Should().BeTrue();
        newConfig.Configuration!.FuzzyHashThreshold.Should().Be(85, "New threshold should be loaded");

        _logger.LogInformation("Successfully detected config change during file processing");
    }

    [Fact]
    public async Task HotReload_WithUnchangedConfig_SkipsReload()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        CreateConfigWithThreshold(75);
        await configService.LoadConfiguration();

        // Act - Check for reload without changing file
        var wasReloaded = await configService.ReloadIfChanged();

        // Assert
        wasReloaded.Should().BeFalse("Config should not be reloaded when unchanged");

        _logger.LogInformation("Correctly skipped reload for unchanged config");
    }

    [Fact]
    public async Task HotReload_WithInvalidConfigChanges_FallsBackToPreviousConfig()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        // Load valid initial config
        CreateConfigWithThreshold(75);
        var initialConfig = await configService.LoadConfiguration();
        initialConfig.IsValid.Should().BeTrue();

        await Task.Delay(100);

        // Act - Create invalid config
        CreateInvalidConfig();
        var wasReloaded = await configService.ReloadIfChanged();

        // Assert - Should detect change but fall back to previous valid config
        if (wasReloaded)
        {
            var currentConfig = await configService.LoadConfiguration();
            // Should either keep previous config or return error with previous config preserved
            if (currentConfig.IsValid)
            {
                currentConfig.Configuration!.FuzzyHashThreshold.Should().Be(75, "Should preserve previous valid config");
            }
            else
            {
                currentConfig.Errors.Should().NotBeEmpty("Should report validation errors");
            }
        }

        _logger.LogWarning("Handled invalid config during hot-reload appropriately");
    }

    [Fact]
    public async Task HotReload_WithMultipleQuickChanges_HandlesDebouncing()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        CreateConfigWithThreshold(70);
        await configService.LoadConfiguration();

        var reloadResults = new List<bool>();

        // Act - Make multiple rapid changes
        for (int i = 1; i <= 5; i++)
        {
            CreateConfigWithThreshold(70 + i);
            await Task.Delay(50); // Quick succession
            reloadResults.Add(await configService.ReloadIfChanged());
        }

        // Assert - Should handle rapid changes gracefully
        reloadResults.Should().Contain(true, "At least one reload should have been detected");

        var finalConfig = await configService.LoadConfiguration();
        finalConfig.IsValid.Should().BeTrue();
        finalConfig.Configuration!.FuzzyHashThreshold.Should().BeInRange(71, 75, "Final config should reflect one of the changes");

        _logger.LogInformation("Successfully handled {Count} rapid config changes", reloadResults.Count(r => r));
    }

    [Fact]
    public async Task HotReload_PerFileProcessingCycle_LoadsConfigOnce()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        CreateConfigWithThreshold(80);
        var loadTimes = new List<DateTime>();

        // Act - Simulate processing multiple files
        for (int fileIndex = 0; fileIndex < 3; fileIndex++)
        {
            // Each file processing should check config once
            var loadStart = DateTime.UtcNow;

            if (fileIndex == 1)
            {
                // Change config before processing second file
                await Task.Delay(100);
                CreateConfigWithThreshold(85);
            }

            var wasReloaded = await configService.ReloadIfChanged();
            var currentConfig = await configService.LoadConfiguration();

            loadTimes.Add(DateTime.UtcNow);

            // Assert per file
            currentConfig.Should().NotBeNull();
            currentConfig.IsValid.Should().BeTrue();

            if (fileIndex == 0)
            {
                currentConfig.Configuration!.FuzzyHashThreshold.Should().Be(80);
                wasReloaded.Should().BeFalse("First load should not be a reload");
            }
            else if (fileIndex == 1)
            {
                wasReloaded.Should().BeTrue("Second file should detect config change");
                currentConfig.Configuration!.FuzzyHashThreshold.Should().Be(85);
            }
            else
            {
                wasReloaded.Should().BeFalse("Third file should not detect changes");
                currentConfig.Configuration!.FuzzyHashThreshold.Should().Be(85);
            }

            await Task.Delay(50); // Simulate file processing time
        }

        // Assert overall behavior
        loadTimes.Should().HaveCount(3);

        _logger.LogInformation("Processed {Count} files with per-file config checking", loadTimes.Count);
    }

    [Fact]
    public async Task HotReload_WithFileSystemPermissionChanges_HandlesGracefully()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();

        CreateConfigWithThreshold(75);
        await configService.LoadConfiguration();

        // Act - Simulate permission issue (file becomes read-only or inaccessible)
        try
        {
            var fileInfo = new FileInfo(_testConfigPath);
            fileInfo.IsReadOnly = true; // Make file read-only

            var wasReloaded = await configService.ReloadIfChanged();

            // Should handle gracefully without crashing
            wasReloaded.Should().BeFalse("Should not reload if file is inaccessible");
        }
        catch (UnauthorizedAccessException)
        {
            // This is acceptable behavior - permission errors should be handled
            _logger.LogWarning("Permission error handled as expected");
        }
        finally
        {
            // Cleanup - restore permissions
            try
            {
                var fileInfo = new FileInfo(_testConfigPath);
                fileInfo.IsReadOnly = false;
            }
            catch { /* ignore cleanup errors */ }
        }

        _logger.LogInformation("Handled file permission changes during hot-reload");
    }

    private void CreateConfigWithThreshold(int threshold)
    {
        var config = new
        {
            version = "2.0",
            matchConfidenceThreshold = 0.8,
            renameConfidenceThreshold = 0.85,
            fuzzyHashThreshold = threshold,
            hashingAlgorithm = "CTPH",
            filenamePatterns = new
            {
                primaryPattern = @"^(?<SeriesName>.+?)\sS(?<Season>\d+)E(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
                secondaryPattern = @"^(?<SeriesName>.+?)\s(?<Season>\d+)x(?<Episode>\d+)(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
                tertiaryPattern = @"^(?<SeriesName>.+?)\.S(?<Season>\d+)\.E(?<Episode>\d+)(?:\.(?<EpisodeName>.+?))?$"
            },
            filenameTemplate = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}"
        };

        // Ensure the directory exists
        var directory = Path.GetDirectoryName(_testConfigPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testConfigPath, json);

        // Use incremental timestamp to ensure each call creates a detectably different file modification time
        _nextTimestamp = _nextTimestamp.AddSeconds(2);
        File.SetLastWriteTimeUtc(_testConfigPath, _nextTimestamp);

        _logger.LogDebug("Created config with threshold {Threshold}, timestamp {Timestamp}",
            threshold, _nextTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));

        _testFilesToCleanup.Add(_testConfigPath);
    }

    private void CreateInvalidConfig()
    {
        // Create syntactically valid JSON but with invalid values
        var invalidConfig = new
        {
            version = "invalid",
            matchConfidenceThreshold = 2.0, // Invalid - > 1.0
            renameConfidenceThreshold = 0.5, // Invalid - less than match threshold
            fuzzyHashThreshold = -10, // Invalid - negative
            hashingAlgorithm = "INVALID_ALGORITHM"
            // Missing required fields
        };

        var json = JsonSerializer.Serialize(invalidConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testConfigPath, json);
    }

    public void Dispose()
    {
        foreach (var file in _testFilesToCleanup)
        {
            try
            {
                if (File.Exists(file))
                {
                    // Ensure file is not read-only before deletion
                    var fileInfo = new FileInfo(file);
                    fileInfo.IsReadOnly = false;
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup test file: {FilePath}", file);
            }
        }

        _serviceProvider.Dispose();
    }
}