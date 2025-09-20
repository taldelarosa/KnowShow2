using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Performance;

public class SubtitleWorkflowPerformanceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly SubtitleWorkflowCoordinator_coordinator;
    private readonly VideoFormatValidator _validator;
    private readonly ITextSubtitleExtractor_textExtractor;
    private readonly string _testVideoPath;

    public SubtitleWorkflowPerformanceTests()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add core services
        services.AddTransient<VideoFormatValidator>();
        services.AddTransient<SubtitleExtractor>();
        services.AddTransient<ITextSubtitleExtractor, TextSubtitleExtractor>();
        services.AddTransient<PgsRipService>();
        services.AddTransient<EnhancedPgsToTextConverter>();
        services.AddTransient<PgsToTextConverter>();
        services.AddTransient<SubtitleMatcher>();
        services.AddTransient<SubtitleNormalizationService>();
        services.AddTransient<FuzzyHashService>(provider => 
            new FuzzyHashService(
                ":memory:", // Use in-memory SQLite database for tests
                provider.GetRequiredService<ILogger<FuzzyHashService>>(),
                provider.GetRequiredService<SubtitleNormalizationService>()));
        services.AddTransient<SubtitleWorkflowCoordinator>();
        
        _serviceProvider = services.BuildServiceProvider();
        _coordinator = _serviceProvider.GetRequiredService<SubtitleWorkflowCoordinator>();
        _validator = _serviceProvider.GetRequiredService<VideoFormatValidator>();
        _textExtractor = _serviceProvider.GetRequiredService<ITextSubtitleExtractor>();
        
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

        // Act
        var result = await _coordinator.ProcessVideoAsync(_testVideoPath);

        // Assert
        stopwatch.Stop();
        
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000); // Should complete within 30 seconds
        
        Console.WriteLine($"Video processing took: {stopwatch.ElapsedMilliseconds}ms");
        
        if (!result.HasError)
        {
            Console.WriteLine($"Successfully identified: {result.Series} S{result.Season}E{result.Episode} " +
                            $"with {result.MatchConfidence:P1} confidence");
        }
    }

    [Fact]
    public async Task SubtitleDetection_Performance_FastDetection()
    {
        // Skip if test file not available
        if (!File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available");
            return;
        }

        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tracks = await _validator.GetSubtitleTracks(_testVideoPath);

        // Assert
        stopwatch.Stop();
        
        tracks.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should detect within 5 seconds
        
        Console.WriteLine($"Subtitle track detection took: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Found {tracks.Count} subtitle tracks");
    }

    [Fact]
    public async Task TextSubtitleExtraction_Performance_EfficientExtraction()
    {
        // Skip if test file not available
        if (!File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available");
            return;
        }

        // Arrange
        var tracks = await _textExtractor.DetectTextSubtitleTracksAsync(_testVideoPath);
        
        if (!tracks.Any())
        {
            Assert.True(true, "No text tracks available for performance test");
            return;
        }

        var track = tracks.First();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _textExtractor.ExtractTextSubtitleContentAsync(_testVideoPath, track);

        // Assert
        stopwatch.Stop();
        
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Should extract within 10 seconds
        
        Console.WriteLine($"Text subtitle extraction took: {stopwatch.ElapsedMilliseconds}ms");
        
        if (result.Status == ProcessingStatus.Completed && result.ExtractedTracks.Any())
        {
            var extractedTrack = result.ExtractedTracks.First();
            Console.WriteLine($"Extracted {extractedTrack.Content?.Length ?? 0} characters");
        }
    }

    [Fact]
    public async Task MultipleProcessing_Performance_ConsistentTiming()
    {
        // Skip if test file not available
        if (!File.Exists(_testVideoPath))
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
            var result = await _coordinator.ProcessVideoAsync(_testVideoPath);
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
        if (!File.Exists(_testVideoPath))
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
            var result = await _coordinator.ProcessVideoAsync(_testVideoPath);
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
        if (!File.Exists(_testVideoPath))
        {
            Assert.True(true, "Test video file not available");
            return;
        }

        // Arrange
        const int concurrentRequests = 3;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => _coordinator.ProcessVideoAsync(_testVideoPath))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        
        results.Should().AllSatisfy(result => result.Should().NotBeNull());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(60000); // Should complete within 60 seconds
        
        Console.WriteLine($"Concurrent processing ({concurrentRequests} requests) took: {stopwatch.ElapsedMilliseconds}ms");
        
        var successfulResults = results.Where(r => !r.HasError).ToArray();
        Console.WriteLine($"{successfulResults.Length}/{results.Length} requests succeeded");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
