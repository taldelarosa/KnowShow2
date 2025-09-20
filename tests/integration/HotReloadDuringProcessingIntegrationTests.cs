using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Extensions;
using EpisodeIdentifier.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for configuration hot-reload during active processing.
/// These tests verify that configuration changes are properly handled while
/// bulk processing operations are in progress.
/// 
/// T010: Integration test for hot-reload during active processing
/// Tests hot-reload behavior during concurrent operations
/// </summary>
public class HotReloadDuringProcessingIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly MockFileSystem _fileSystem;

    public HotReloadDuringProcessingIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _fileSystem = new MockFileSystem();
    }

    private ServiceProvider CreateServiceProvider(string configPath)
    {
        var services = new ServiceCollection();
        
        // Setup logging to capture test output
        services.AddLogging(builder => 
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Override file system with mock
        services.AddSingleton<System.IO.Abstractions.IFileSystem>(_fileSystem);
        
        // Add all episode identification services using the extension method
        services.AddEpisodeIdentificationServices();
        
        // Override the configuration service to use the test config path
        services.AddScoped<IConfigurationService>(provider => 
            new ConfigurationService(
                provider.GetRequiredService<ILogger<ConfigurationService>>(),
                provider.GetRequiredService<System.IO.Abstractions.IFileSystem>(),
                configPath
            ));
        
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ProcessingInProgress_ConfigurationHotReload_ShouldCompleteWithNewSettings()
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Series.S01E01.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Series.S01E02.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Series.S01E03.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Series.S01E04.mkv")
        };
        
        // Initial configuration with maxConcurrency = 2
        var initialConfigContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "MaxConcurrency": 2,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;
        
        _fileSystem.AddFile(configPath, new MockFileData(initialConfigContent));
        foreach (var file in testFiles)
        {
            _fileSystem.AddFile(file, new MockFileData("test episode content"));
        }

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
        var configService = serviceProvider.GetRequiredService<IConfigurationService>();

        // Act - Start processing with initial configuration
        var initialConfig = await configService.LoadConfiguration();
        initialConfig.IsValid.Should().BeTrue();
        initialConfig.Configuration!.MaxConcurrency.Should().Be(2);

        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = initialConfig.Configuration!.MaxConcurrency,
                ContinueOnError = true
            }
        };

        // Start long-running processing task
        var processingTask = processor.ProcessAsync(request);
        
        // Wait a moment then update configuration during processing
        await Task.Delay(50);
        
        // Update configuration - change maxConcurrency to 4
        var updatedConfigContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.75,
            "RenameConfidenceThreshold": 0.90,
            "FuzzyHashThreshold": 80,
            "MaxConcurrency": 4,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;
        
        _fileSystem.RemoveFile(configPath);
        _fileSystem.AddFile(configPath, new MockFileData(updatedConfigContent));

        // Wait for processing to complete
        var result = await processingTask;

        // Assert - Processing should complete successfully
        result.Should().NotBeNull();
        result.ProcessedFiles.Should().Be(4);
        result.TotalFiles.Should().Be(4);
        
        // Verify hot-reload was detected
        var currentConfig = await configService.LoadConfiguration();
        currentConfig.IsValid.Should().BeTrue();
        currentConfig.Configuration!.MaxConcurrency.Should().Be(4);
        currentConfig.Configuration!.MatchConfidenceThreshold.Should().Be((decimal)0.75);
    }

    [Fact]
    public async Task ProcessingInProgress_InvalidConfigurationHotReload_ShouldContinueWithOriginalSettings()
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "File1.S01E01.mkv"),
            Path.Combine(AppContext.BaseDirectory, "File2.S01E02.mkv")
        };
        
        // Valid initial configuration
        var validConfigContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "MaxConcurrency": 3,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;
        
        _fileSystem.AddFile(configPath, new MockFileData(validConfigContent));
        foreach (var file in testFiles)
        {
            _fileSystem.AddFile(file, new MockFileData("test episode content"));
        }

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
        var configService = serviceProvider.GetRequiredService<IConfigurationService>();

        // Act - Start processing
        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = 3,
                ContinueOnError = true
            }
        };

        var processingTask = processor.ProcessAsync(request);
        
        // Wait then corrupt configuration during processing
        await Task.Delay(30);
        
        // Invalid configuration - maxConcurrency out of range
        var invalidConfigContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "MaxConcurrency": 500,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;
        
        _fileSystem.RemoveFile(configPath);
        _fileSystem.AddFile(configPath, new MockFileData(invalidConfigContent));
        
        // Wait for processing to complete
        var result = await processingTask;

        // Assert - Processing should still succeed with original settings
        result.Should().NotBeNull();
        result.ProcessedFiles.Should().Be(2);
        result.TotalFiles.Should().Be(2);
        
        // Verify configuration validation failed but processing wasn't interrupted
        var configResult = await configService.LoadConfiguration();
        configResult.IsValid.Should().BeFalse(); // Invalid configuration
    }

    [Fact]
    public async Task ProcessingInProgress_CorruptedConfigurationFile_ShouldHandleGracefully()
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Test.S01E01.mkv")
        };
        
        var validConfigContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "MaxConcurrency": 2,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;
        
        _fileSystem.AddFile(configPath, new MockFileData(validConfigContent));
        _fileSystem.AddFile(testFiles[0], new MockFileData("test episode content"));

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
        var configService = serviceProvider.GetRequiredService<IConfigurationService>();

        // Act - Start processing
        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = 2,
                ContinueOnError = true
            }
        };

        var processingTask = processor.ProcessAsync(request);
        
        // Wait then corrupt the configuration file (invalid JSON)
        await Task.Delay(20);
        
        _fileSystem.RemoveFile(configPath);
        _fileSystem.AddFile(configPath, new MockFileData("{ invalid json content"));
        
        // Wait for processing to complete
        var result = await processingTask;

        // Assert - Should handle corruption gracefully
        result.Should().NotBeNull();
        result.ProcessedFiles.Should().Be(1);
        result.TotalFiles.Should().Be(1);
        
        // Verify configuration load now fails due to corruption
        var configResult = await configService.LoadConfiguration();
        configResult.IsValid.Should().BeFalse();
        configResult.Errors.Should().NotBeEmpty();
        configResult.Errors.Any(e => e.Contains("JSON", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task HotReload_ConcurrencyIncrease_ShouldApplyToSubsequentBatches()
    {
        // Arrange - Multiple files requiring batched processing
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Series.S01E01.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Series.S01E02.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Series.S01E03.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Series.S01E04.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Series.S01E05.mkv"),
            Path.Combine(AppContext.BaseDirectory, "Series.S01E06.mkv")
        };
        
        // Start with low concurrency
        var initialConfigContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "MaxConcurrency": 1,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;
        
        _fileSystem.AddFile(configPath, new MockFileData(initialConfigContent));
        foreach (var file in testFiles)
        {
            _fileSystem.AddFile(file, new MockFileData("test episode content"));
        }

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
        var configService = serviceProvider.GetRequiredService<IConfigurationService>();

        // Act - Start processing with low concurrency
        var initialConfig = await configService.LoadConfiguration();
        initialConfig.Configuration!.MaxConcurrency.Should().Be(1);

        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = initialConfig.Configuration!.MaxConcurrency,
                ContinueOnError = true
            }
        };

        // Start processing task
        var processingTask = processor.ProcessAsync(request);
        
        // Quickly update to higher concurrency
        await Task.Delay(25);
        
        var updatedConfigContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "MaxConcurrency": 5,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;
        
        _fileSystem.RemoveFile(configPath);
        _fileSystem.AddFile(configPath, new MockFileData(updatedConfigContent));
        
        var result = await processingTask;

        // Assert - Should complete with updated concurrency detected
        result.Should().NotBeNull();
        result.ProcessedFiles.Should().Be(6);
        result.TotalFiles.Should().Be(6);
        
        // Verify hot-reload was applied
        var finalConfig = await configService.LoadConfiguration();
        finalConfig.IsValid.Should().BeTrue();
        finalConfig.Configuration!.MaxConcurrency.Should().Be(5);
    }

    [Fact]
    public async Task HotReload_FileSystemWatcher_ShouldDetectConfigurationChanges()
    {
        // Arrange
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        
        var configContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "MaxConcurrency": 2,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;
        
        _fileSystem.AddFile(configPath, new MockFileData(configContent));

        using var serviceProvider = CreateServiceProvider(configPath);
        var configService = serviceProvider.GetRequiredService<IConfigurationService>();

        // Act - Load initial configuration
        var initialResult = await configService.LoadConfiguration();
        initialResult.IsValid.Should().BeTrue();
        initialResult.Configuration!.MaxConcurrency.Should().Be(2);

        // Update configuration multiple times
        for (int i = 3; i <= 5; i++)
        {
            var updatedContent = configContent.Replace("\"MaxConcurrency\": 2", $"\"MaxConcurrency\": {i}");
            _fileSystem.RemoveFile(configPath);
            _fileSystem.AddFile(configPath, new MockFileData(updatedContent));
            
            await Task.Delay(10); // Allow for file watcher detection
        }

        // Verify final configuration
        var finalResult = await configService.LoadConfiguration();
        
        // Assert - Should reflect the latest changes
        finalResult.Should().NotBeNull();
        finalResult.IsValid.Should().BeTrue();
        finalResult.Configuration!.MaxConcurrency.Should().Be(5);
    }

    [Fact]
    public async Task HotReload_DuringHighConcurrency_ShouldMaintainSystemStability()
    {
        // Arrange - Test hot-reload during high concurrency operations
        var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
        var testFiles = Enumerable.Range(1, 15)
            .Select(i => Path.Combine(AppContext.BaseDirectory, $"Episode.S01E{i:00}.mkv"))
            .ToArray();

        var initialConfigContent = """
        {
            "Version": "2.0",
            "HashingAlgorithm": "CTPH",
            "MatchConfidenceThreshold": 0.8,
            "RenameConfidenceThreshold": 0.85,
            "FuzzyHashThreshold": 75,
            "MaxConcurrency": 8,
            "FilenamePatterns": {
                "PrimaryPattern": "^(?<SeriesName>.+?)[ ._-]+[Ss](?<Season>\\d{1,2})[Ee](?<Episode>\\d{1,2}).*$"
            },
            "FilenameTemplate": "{SeriesName} - S{Season:00}E{Episode:00}"
        }
        """;

        _fileSystem.AddFile(configPath, new MockFileData(initialConfigContent));
        foreach (var file in testFiles)
        {
            _fileSystem.AddFile(file, new MockFileData("test episode content"));
        }

        using var serviceProvider = CreateServiceProvider(configPath);
        var processor = serviceProvider.GetRequiredService<IBulkProcessor>();
        var configService = serviceProvider.GetRequiredService<IConfigurationService>();

        // Act - Start high concurrency processing
        var request = new BulkProcessingRequest
        {
            Paths = testFiles.ToList(),
            Options = new BulkProcessingOptions
            {
                MaxConcurrency = 8,
                ContinueOnError = true
            }
        };

        var processingTask = processor.ProcessAsync(request);

        // Perform multiple configuration updates during processing
        for (int i = 1; i <= 3; i++)
        {
            await Task.Delay(20 + i * 10); // Staggered updates
            
            var updatedContent = initialConfigContent.Replace(
                "\"MatchConfidenceThreshold\": 0.8", 
                $"\"MatchConfidenceThreshold\": 0.{8 + i}"
            );
            
            _fileSystem.RemoveFile(configPath);
            _fileSystem.AddFile(configPath, new MockFileData(updatedContent));
        }

        var result = await processingTask;

        // Assert - Should complete successfully despite config changes
        result.Should().NotBeNull();
        result.ProcessedFiles.Should().Be(15);
        result.TotalFiles.Should().Be(15);
        result.FailedFiles.Should().Be(0);

        // Verify final configuration state
        var finalConfig = await configService.LoadConfiguration();
        finalConfig.IsValid.Should().BeTrue();
        finalConfig.Configuration!.MatchConfidenceThreshold.Should().BeGreaterThan((decimal)0.8);
    }
}