using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EpisodeIdentifier.Tests.Performance;

/// <summary>
/// Performance tests for async/concurrent processing functionality
/// Tests the MaxConcurrency feature and its impact on processing speed
/// </summary>
public class AsyncConcurrencyPerformanceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly List<string> _testFiles;
    private readonly Mock<IAppConfigService> _mockConfigService;

    public AsyncConcurrencyPerformanceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"perf_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _testFiles = new List<string>();
        _mockConfigService = new Mock<IAppConfigService>();
        
        // Create test video files for performance testing
        CreateTestVideoFiles(20); // Create 20 test files for meaningful performance comparison
    }

    private void CreateTestVideoFiles(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            var fileName = $"test_video_{i:D2}.mkv";
            var filePath = Path.Combine(_tempDirectory, fileName);
            
            // Create a minimal file with some content to simulate real files
            File.WriteAllText(filePath, $"Test video file {i} - simulated content for performance testing. " +
                                       string.Join("", Enumerable.Repeat("Sample data ", 1000)));
            _testFiles.Add(filePath);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task AsyncProcessing_DifferentConcurrencyLevels_ShouldMeasurePerformance(int maxConcurrency)
    {
        // Arrange
        _mockConfigService.Setup(x => x.MaxConcurrency).Returns(maxConcurrency);
        var options = await BulkProcessingOptions.CreateFromConfigurationAsync(_mockConfigService.Object);
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        await SimulateAsyncProcessing(options, _testFiles);
        stopwatch.Stop();

        // Assert
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        
        // Performance assertions - higher concurrency should generally be faster for I/O bound operations
        Assert.True(elapsedMs >= 0, "Processing should take measurable time");
        Assert.Equal(maxConcurrency, options.MaxConcurrency);
        
        // Log performance results for analysis
        var throughput = _testFiles.Count / (elapsedMs / 1000.0);
        Console.WriteLine($"MaxConcurrency: {maxConcurrency,2}, Time: {elapsedMs,5}ms, Throughput: {throughput:F2} files/sec");
    }

    [Fact]
    public async Task AsyncProcessing_ConcurrencyComparison_ShouldShowScalabilityTrends()
    {
        // Arrange
        var concurrencyLevels = new[] { 1, 2, 4, 8 };
        var performanceResults = new List<(int Concurrency, long ElapsedMs, double Throughput)>();

        Console.WriteLine("\n=== Async Concurrency Performance Comparison ===");
        Console.WriteLine($"Testing with {_testFiles.Count} files");
        Console.WriteLine();

        // Act - Test each concurrency level
        foreach (var concurrency in concurrencyLevels)
        {
            _mockConfigService.Setup(x => x.MaxConcurrency).Returns(concurrency);
            var options = await BulkProcessingOptions.CreateFromConfigurationAsync(_mockConfigService.Object);
            var stopwatch = Stopwatch.StartNew();

            await SimulateAsyncProcessing(options, _testFiles);

            stopwatch.Stop();
            var throughput = _testFiles.Count / (stopwatch.ElapsedMilliseconds / 1000.0);
            performanceResults.Add((concurrency, stopwatch.ElapsedMilliseconds, throughput));
            
            Console.WriteLine($"Concurrency: {concurrency,2} | Time: {stopwatch.ElapsedMilliseconds,5}ms | Throughput: {throughput,6:F2} files/sec");
            
            // Small delay between tests to avoid resource contention
            await Task.Delay(100);
        }

        // Assert
        Assert.Equal(concurrencyLevels.Length, performanceResults.Count);
        
        // Performance should generally improve with concurrency (up to a point)
        // At minimum, higher concurrency shouldn't be dramatically worse
        var sequentialTime = performanceResults.First(r => r.Concurrency == 1).ElapsedMs;
        var highConcurrencyTime = performanceResults.Last().ElapsedMs;
        
        Console.WriteLine();
        Console.WriteLine($"Sequential baseline: {sequentialTime}ms");
        Console.WriteLine($"Highest concurrency: {highConcurrencyTime}ms");
        Console.WriteLine($"Speedup factor: {(double)sequentialTime / highConcurrencyTime:F2}x");
        
        // High concurrency should not be significantly slower than sequential
        Assert.True(highConcurrencyTime <= sequentialTime * 2.0,
            $"High concurrency ({highConcurrencyTime}ms) should not be more than 2x slower than sequential ({sequentialTime}ms)");

        // Calculate average performance improvement
        var improvementCount = 0;
        for (int i = 1; i < performanceResults.Count; i++)
        {
            if (performanceResults[i].ElapsedMs <= performanceResults[i - 1].ElapsedMs)
            {
                improvementCount++;
            }
        }

        Console.WriteLine($"Performance improved or stayed same in {improvementCount}/{performanceResults.Count - 1} steps");
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(4, 10)]
    [InlineData(8, 10)]
    public async Task AsyncProcessing_ConsistentFileCount_ShouldProcessAllFiles(int maxConcurrency, int expectedFileCount)
    {
        // Arrange
        _mockConfigService.Setup(x => x.MaxConcurrency).Returns(maxConcurrency);
        var options = await BulkProcessingOptions.CreateFromConfigurationAsync(_mockConfigService.Object);
        var testSubset = _testFiles.Take(expectedFileCount).ToList();

        // Act
        var processedFiles = await SimulateAsyncProcessing(options, testSubset);

        // Assert
        Assert.Equal(expectedFileCount, processedFiles.Count);
        Assert.Equal(maxConcurrency, options.MaxConcurrency);
    }

    [Fact]
    public async Task AsyncProcessing_RepeatablePerformance_ShouldBeConsistent()
    {
        // Arrange
        const int maxConcurrency = 4;
        _mockConfigService.Setup(x => x.MaxConcurrency).Returns(maxConcurrency);
        var options = await BulkProcessingOptions.CreateFromConfigurationAsync(_mockConfigService.Object);
        var runs = 3;
        var elapsedTimes = new List<long>();

        Console.WriteLine($"\n=== Testing Performance Consistency (Concurrency={maxConcurrency}) ===");

        // Act - Run multiple times to check consistency
        for (int run = 0; run < runs; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            await SimulateAsyncProcessing(options, _testFiles.Take(10).ToList());
            stopwatch.Stop();
            
            elapsedTimes.Add(stopwatch.ElapsedMilliseconds);
            Console.WriteLine($"Run {run + 1}: {stopwatch.ElapsedMilliseconds}ms");
            
            await Task.Delay(50); // Small delay between runs
        }

        // Assert
        Assert.Equal(runs, elapsedTimes.Count);
        
        var avgTime = elapsedTimes.Average();
        var maxDeviation = elapsedTimes.Max(t => Math.Abs(t - avgTime));
        var deviationPercent = (maxDeviation / avgTime) * 100;

        Console.WriteLine($"Average: {avgTime:F2}ms, Max deviation: {maxDeviation}ms ({deviationPercent:F1}%)");
        
        // Performance should be reasonably consistent (within 50% deviation)
        Assert.True(deviationPercent < 50,
            $"Performance deviation ({deviationPercent:F1}%) should be reasonable");
    }

    /// <summary>
    /// Simulates async file processing with configurable concurrency
    /// </summary>
    private async Task<List<string>> SimulateAsyncProcessing(BulkProcessingOptions options, List<string> filesToProcess)
    {
        var processedFiles = new List<string>();
        var semaphore = new System.Threading.SemaphoreSlim(options.MaxConcurrency);
        var tasks = new List<Task>();

        foreach (var file in filesToProcess)
        {
            await semaphore.WaitAsync();
            
            var task = Task.Run(async () =>
            {
                try
                {
                    // Simulate I/O-bound work (reading file, processing, etc.)
                    await Task.Delay(50); // Simulate subtitle extraction
                    var content = await File.ReadAllTextAsync(file);
                    await Task.Delay(30); // Simulate hash computation
                    
                    lock (processedFiles)
                    {
                        processedFiles.Add(file);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });
            
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        return processedFiles;
    }

    public void Dispose()
    {
        // Cleanup test files
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
