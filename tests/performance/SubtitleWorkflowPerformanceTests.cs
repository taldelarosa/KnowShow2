using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Services.Hashing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EpisodeIdentifier.Tests.Performance;

/// <summary>
/// Performance tests for the complete subtitle processing workflow.
/// Tests end-to-end performance from video file to episode identification.
/// </summary>
public class SubtitleWorkflowPerformanceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IEpisodeIdentificationService _episodeIdentificationService;
    private readonly VideoFormatValidator _validator;
    private readonly VideoTextSubtitleExtractor _textExtractor;
    private readonly SubtitleExtractor _subtitleExtractor;
    private readonly string? _testVideoPath;

    public SubtitleWorkflowPerformanceTests()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // Add file system abstraction
        services.AddSingleton<IFileSystem, FileSystem>();
        
        // Add core services
        services.AddTransient<VideoFormatValidator>();
        services.AddTransient<SubtitleExtractor>();
        services.AddTransient<VideoTextSubtitleExtractor>();
        services.AddTransient<PgsRipService>();
        services.AddTransient<EnhancedPgsToTextConverter>();
        services.AddTransient<PgsToTextConverter>();
        services.AddTransient<SubtitleNormalizationService>();
        
        // Add CTPH hashing services
        services.AddTransient<CTPhHashingService>();
        services.AddTransient<EnhancedCTPhHashingService>();
        
        // Add configuration service
        services.AddTransient<ConfigurationService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ConfigurationService>>();
            return new ConfigurationService(logger);
        });
        
        // Add fuzzy hash service with in-memory database
        services.AddTransient<FuzzyHashService>(provider => 
            new FuzzyHashService(
                ":memory:", // Use in-memory SQLite database for tests
                provider.GetRequiredService<ILogger<FuzzyHashService>>(),
                provider.GetRequiredService<SubtitleNormalizationService>()));
        
        // Add episode identification service
        services.AddTransient<IEpisodeIdentificationService, EpisodeIdentificationService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _episodeIdentificationService = _serviceProvider.GetRequiredService<IEpisodeIdentificationService>();
        _validator = _serviceProvider.GetRequiredService<VideoFormatValidator>();
        _textExtractor = _serviceProvider.GetRequiredService<VideoTextSubtitleExtractor>();
        _subtitleExtractor = _serviceProvider.GetRequiredService<SubtitleExtractor>();
        
        _testVideoPath = Environment.GetEnvironmentVariable("TEST_VIDEO_PATH");
    }

    [Fact]
    public async Task ProcessVideo_Performance_CompletesWithinTimeLimit()
    {
        // Skip if test file not available
        if (string.IsNullOrEmpty(_testVideoPath) || !File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available or TEST_VIDEO_PATH not set");
            return;
        }

        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act - Extract and convert subtitles, then identify
        var subtitleText = await _subtitleExtractor.ExtractAndConvertSubtitles(_testVideoPath, "eng");
        var result = await _episodeIdentificationService.IdentifyEpisodeAsync(subtitleText, _testVideoPath);

        // Assert
        stopwatch.Stop();
        
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000); // Should complete within 30 seconds
        
        Console.WriteLine($"Video processing took: {stopwatch.ElapsedMilliseconds}ms");
        
        if (!result.IsAmbiguous && result.Series != null)
        {
            Console.WriteLine($"Successfully identified: {result.Series} S{result.Season}E{result.Episode} " +
                            $"with {result.MatchConfidence:P1} confidence");
        }
    }

    [Fact]
    public async Task SubtitleDetection_Performance_FastDetection()
    {
        // Skip if test file not available
        if (string.IsNullOrEmpty(_testVideoPath) || !File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available");
            return;
        }

        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var isValid = await _validator.IsValidForProcessing(_testVideoPath);

        // Assert
        stopwatch.Stop();
        
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should validate within 5 seconds
        
        Console.WriteLine($"Video validation took: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Video is {(isValid ? "valid" : "invalid")} for processing");
    }

    [Fact]
    public async Task TextSubtitleExtraction_Performance_EfficientExtraction()
    {
        // Skip if test file not available
        if (string.IsNullOrEmpty(_testVideoPath) || !File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available");
            return;
        }

        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var subtitleText = await _textExtractor.ExtractTextSubtitleFromVideo(_testVideoPath, 0, "eng");

        // Assert
        stopwatch.Stop();
        
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Should extract within 10 seconds
        
        Console.WriteLine($"Text subtitle extraction took: {stopwatch.ElapsedMilliseconds}ms");
        
        if (subtitleText != null)
        {
            Console.WriteLine($"Extracted {subtitleText.Length} characters");
        }
        else
        {
            Console.WriteLine("No text subtitles found");
        }
    }

    [Fact]
    public async Task MultipleProcessing_Performance_ConsistentTiming()
    {
        // Skip if test file not available
        if (string.IsNullOrEmpty(_testVideoPath) || !File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available");
            return;
        }

        const int iterations = 3;
        var timings = new List<long>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var subtitleText = await _subtitleExtractor.ExtractAndConvertSubtitles(_testVideoPath, "eng");
            var result = await _episodeIdentificationService.IdentifyEpisodeAsync(subtitleText, _testVideoPath);
            stopwatch.Stop();
            
            timings.Add(stopwatch.ElapsedMilliseconds);
            
            result.Should().NotBeNull();
            Console.WriteLine($"Iteration {i + 1}: {stopwatch.ElapsedMilliseconds}ms");
        }

        // Assert
        var averageTime = timings.Average();
        var maxVariation = timings.Max() - timings.Min();
        
        averageTime.Should().BeLessThan(30000); // Average should be under 30 seconds
        maxVariation.Should().BeLessThan(15000); // Variation should be reasonable (under 15 seconds)
        
        Console.WriteLine($"Average processing time: {averageTime:F1}ms");
        Console.WriteLine($"Timing variation: {maxVariation}ms");
    }

    [Fact]
    public async Task MemoryUsage_Performance_NoMemoryLeaks()
    {
        // Skip if test file not available
        if (string.IsNullOrEmpty(_testVideoPath) || !File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available");
            return;
        }

        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);

        // Act - Process video multiple times
        for (int i = 0; i < 5; i++)
        {
            var subtitleText = await _subtitleExtractor.ExtractAndConvertSubtitles(_testVideoPath, "eng");
            var result = await _episodeIdentificationService.IdentifyEpisodeAsync(subtitleText, _testVideoPath);
            result.Should().NotBeNull();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024.0 * 1024.0);
        
        Console.WriteLine($"Initial memory: {initialMemory / (1024.0 * 1024.0):F2} MB");
        Console.WriteLine($"Final memory: {finalMemory / (1024.0 * 1024.0):F2} MB");
        Console.WriteLine($"Memory increase: {memoryIncreaseMB:F2} MB");
        
        // Memory increase should be reasonable (less than 100MB)
        memoryIncreaseMB.Should().BeLessThan(100);
    }

    [Fact]
    public async Task ConcurrentProcessing_Performance_HandlesMultipleRequests()
    {
        // Skip if test file not available
        if (string.IsNullOrEmpty(_testVideoPath) || !File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available");
            return;
        }

        // Arrange
        const int concurrentRequests = 3;
        var stopwatch = Stopwatch.StartNew();

        // Act - Process concurrently
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async _ =>
            {
                var subtitleText = await _subtitleExtractor.ExtractAndConvertSubtitles(_testVideoPath, "eng");
                return await _episodeIdentificationService.IdentifyEpisodeAsync(subtitleText, _testVideoPath);
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        
        results.Should().AllSatisfy(result => result.Should().NotBeNull());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(60000); // Should complete within 60 seconds
        
        Console.WriteLine($"Concurrent processing ({concurrentRequests} requests) took: {stopwatch.ElapsedMilliseconds}ms");
        
        var successfulResults = results.Where(r => !r.IsAmbiguous && r.Series != null).ToArray();
        Console.WriteLine($"{successfulResults.Length}/{results.Length} requests succeeded");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
