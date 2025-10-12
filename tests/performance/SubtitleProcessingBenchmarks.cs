using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Services.Hashing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Tests.Performance;

/// <summary>
/// BenchmarkDotNet performance benchmarks for subtitle processing operations.
/// Measures real-world performance of core subtitle extraction and identification workflows.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class SubtitleProcessingBenchmarks
{
    private ServiceProvider _serviceProvider = null!;
    private IEpisodeIdentificationService _episodeIdentificationService = null!;
    private VideoFormatValidator _validator = null!;
    private VideoTextSubtitleExtractor _textExtractor = null!;
    private SubtitleExtractor _subtitleExtractor = null!;
    private string _testVideoPath = null!;
    private string _testSubtitleText = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        
        // Add logging with minimal output for benchmarks
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        
        // Add file system abstraction
        services.AddSingleton<IFileSystem, FileSystem>();
        
        // Add core services
        services.AddTransient<VideoFormatValidator>();
        services.AddTransient<SubtitleExtractor>();
        services.AddTransient<VideoTextSubtitleExtractor>();
        services.AddTransient<PgsRipService>();
        services.AddTransient<PgsToTextConverter>();
        services.AddTransient<EnhancedPgsToTextConverter>();
        services.AddTransient<SubtitleNormalizationService>();
        
        // Add CTPH hashing services
        services.AddTransient<CTPhHashingService>();
        services.AddTransient<EnhancedCTPhHashingService>();
        
        // Add configuration service (mock for benchmarks)
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
        
        _testVideoPath = "/mnt/c/src/KnowShow/TestData/media/Episode S02E01.mkv";
        
        // Sample subtitle text for identification benchmarks
        _testSubtitleText = @"1
00:00:01,000 --> 00:00:04,000
Welcome to the show

2
00:00:05,000 --> 00:00:08,000
This is episode one of season two";
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    [Benchmark]
    public async Task<IdentificationResult> IdentifyEpisode_FromSubtitleText()
    {
        return await _episodeIdentificationService.IdentifyEpisodeAsync(_testSubtitleText);
    }

    [Benchmark]
    public async Task<bool> ValidateVideoFormat()
    {
        if (!File.Exists(_testVideoPath))
        {
            return false;
        }

        return await _validator.IsValidForProcessing(_testVideoPath);
    }

    [Benchmark]
    public async Task<string?> ExtractTextSubtitle()
    {
        if (!File.Exists(_testVideoPath))
        {
            return null;
        }

        return await _textExtractor.ExtractTextSubtitleFromVideo(_testVideoPath, 0, "eng");
    }

    /// <summary>
    /// Benchmarks PGS subtitle extraction from video file
    /// </summary>
    [Benchmark]
    public async Task<byte[]> ExtractPgsSubtitles()
    {
        if (!File.Exists(_testVideoPath))
        {
            return Array.Empty<byte>();
        }

        return await _subtitleExtractor.ExtractPgsSubtitles(_testVideoPath, "eng");
    }

    /// <summary>
    /// Benchmarks subtitle extraction and conversion
    /// </summary>
    [Benchmark]
    public async Task<string> ExtractAndConvertSubtitles()
    {
        if (!File.Exists(_testVideoPath))
        {
            return string.Empty;
        }

        return await _subtitleExtractor.ExtractAndConvertSubtitles(_testVideoPath, "eng");
    }

    /// <summary>
    /// Benchmarks episode identification with source file path (for CTPH hashing)
    /// </summary>
    [Benchmark]
    public async Task<IdentificationResult> IdentifyEpisode_WithSourcePath()
    {
        return await _episodeIdentificationService.IdentifyEpisodeAsync(_testSubtitleText, _testVideoPath);
    }

    /// <summary>
    /// Benchmarks episode identification with minimum confidence threshold
    /// </summary>
    [Benchmark]
    public async Task<IdentificationResult> IdentifyEpisode_WithConfidenceThreshold()
    {
        return await _episodeIdentificationService.IdentifyEpisodeAsync(_testSubtitleText, null, 0.75);
    }
}


