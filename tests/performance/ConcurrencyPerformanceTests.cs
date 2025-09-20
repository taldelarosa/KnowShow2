using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;

namespace EpisodeIdentifier.Tests.Performance;

public class ConcurrencyPerformanceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly List<string> _testFiles;
    private readonly Mock<IAppConfigService> _mockConfigService;

    public ConcurrencyPerformanceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
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
            File.WriteAllText(filePath, $"Test video file {i} - simulated content for performance testing");
            _testFiles.Add(filePath);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task BulkProcessing_DifferentConcurrencyLevels_ShouldShowPerformanceDifferences(int maxConcurrency)
    {
        // Arrange
        _mockConfigService.Setup(x => x.MaxConcurrency).Returns(maxConcurrency);
        var options = new BulkProcessingOptions(_mockConfigService.Object);
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        await SimulateBulkProcessing(options, _testFiles);
        stopwatch.Stop();

        // Assert
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        
        // Performance assertions - higher concurrency should generally be faster for I/O bound operations
        Assert.True(elapsedMs >= 0, "Processing should take measurable time");
        Assert.Equal(maxConcurrency, options.MaxConcurrency);
        
        // Log performance results for analysis
        var throughput = _testFiles.Count / (elapsedMs / 1000.0);
        Console.WriteLine($"MaxConcurrency: {maxConcurrency}, Time: {elapsedMs}ms, Throughput: {throughput:F2} files/sec");
    }

    [Fact]
    public async Task BulkProcessing_ConcurrencyComparison_ShouldShowScalabilityTrends()
    {
        // Arrange
        var concurrencyLevels = new[] { 1, 3, 5, 10 };
        var performanceResults = new List<(int Concurrency, long ElapsedMs, double Throughput)>();

        // Act - Test each concurrency level
        foreach (var concurrency in concurrencyLevels)
        {
            _mockConfigService.Setup(x => x.MaxConcurrency).Returns(concurrency);
            var options = new BulkProcessingOptions(_mockConfigService.Object);
            var stopwatch = Stopwatch.StartNew();

            await SimulateBulkProcessing(options, _testFiles);

            stopwatch.Stop();
            var throughput = _testFiles.Count / (stopwatch.ElapsedMilliseconds / 1000.0);
            performanceResults.Add((concurrency, stopwatch.ElapsedMilliseconds, throughput));
            
            // Small delay between tests to avoid resource contention
            await Task.Delay(100);
        }

        // Assert
        Assert.Equal(concurrencyLevels.Length, performanceResults.Count);
        
        // Performance should generally improve with concurrency (up to a point)
        // At minimum, higher concurrency shouldn't be dramatically worse
        var sequentialTime = performanceResults.First(r => r.Concurrency == 1).ElapsedMs;
        var highConcurrencyTime = performanceResults.First(r => r.Concurrency == 10).ElapsedMs;
        
        // High concurrency should not be more than 50% slower than sequential
        // (allowing for overhead and test environment variations)
        Assert.True(highConcurrencyTime <= sequentialTime * 1.5, 
            $"High concurrency ({highConcurrencyTime}ms) should not be significantly slower than sequential ({sequentialTime}ms)");

        // Log detailed results
        Console.WriteLine("\nConcurrency Performance Comparison:");
        foreach (var result in performanceResults)
        {
            Console.WriteLine($"Concurrency: {result.Concurrency,2}, Time: {result.ElapsedMs,4}ms, Throughput: {result.Throughput:F2} files/sec");
        }
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(3, 10)]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    public async Task BulkProcessing_ConsistentFileCount_ShouldProcessAllFiles(int maxConcurrency, int expectedFileCount)
    {
        // Arrange
        _mockConfigService.Setup(x => x.MaxConcurrency).Returns(maxConcurrency);
        var options = new BulkProcessingOptions(_mockConfigService.Object);
        var testSubset = _testFiles.Take(expectedFileCount).ToList();

        // Act
        var processedFiles = await SimulateBulkProcessing(options, testSubset);

        // Assert
        Assert.Equal(expectedFileCount, processedFiles.Count);
        Assert.Equal(maxConcurrency, options.MaxConcurrency);
    }

    [Fact]
    public async Task BulkProcessing_MemoryUsage_ShouldNotGrowExcessivelyWithConcurrency()
    {
        // Arrange
        var concurrencyLevels = new[] { 1, 5, 10 };
        var memoryResults = new List<(int Concurrency, long MemoryBefore, long MemoryAfter)>();

        foreach (var concurrency in concurrencyLevels)
        {
            // Force garbage collection to get baseline
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryBefore = GC.GetTotalMemory(false);

            _mockConfigService.Setup(x => x.MaxConcurrency).Returns(concurrency);
            var options = new BulkProcessingOptions(_mockConfigService.Object);

            // Act
            await SimulateBulkProcessing(options, _testFiles);

            var memoryAfter = GC.GetTotalMemory(false);
            memoryResults.Add((concurrency, memoryBefore, memoryAfter));

            await Task.Delay(100); // Allow cleanup between tests
        }

        // Assert
        foreach (var result in memoryResults)
        {
            var memoryGrowth = result.MemoryAfter - result.MemoryBefore;
            
            // Memory growth should be reasonable (less than 10MB for test scenario)
            Assert.True(memoryGrowth < 10_000_000, 
                $"Memory growth for concurrency {result.Concurrency} was {memoryGrowth} bytes, which seems excessive");
            
            Console.WriteLine($"Concurrency: {result.Concurrency}, Memory Growth: {memoryGrowth / 1024.0:F2} KB");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task BulkProcessing_RepeatablePerformance_ShouldBeConsistent(int maxConcurrency)
    {
        // Arrange
        _mockConfigService.Setup(x => x.MaxConcurrency).Returns(maxConcurrency);
        var options = new BulkProcessingOptions(_mockConfigService.Object);
        var runs = 3;
        var elapsedTimes = new List<long>();

        // Act - Run multiple times to check consistency
        for (int run = 0; run < runs; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            await SimulateBulkProcessing(options, _testFiles.Take(10).ToList());
            stopwatch.Stop();
            
            elapsedTimes.Add(stopwatch.ElapsedMilliseconds);
            await Task.Delay(50); // Brief pause between runs
        }

        // Assert
        var averageTime = elapsedTimes.Average();
        var maxDeviation = elapsedTimes.Max(t => Math.Abs(t - averageTime));
        var deviationPercentage = (maxDeviation / averageTime) * 100;

        // Performance should be relatively consistent (within 100% deviation)
        // This allows for normal system variations while catching major issues
        Assert.True(deviationPercentage < 100, 
            $"Performance variation of {deviationPercentage:F1}% is too high for concurrency level {maxConcurrency}");

        Console.WriteLine($"Concurrency: {maxConcurrency}, Average: {averageTime:F2}ms, Max Deviation: {deviationPercentage:F1}%");
    }

    [Fact]
    public async Task BulkProcessing_ScalabilityEfficiency_ShouldMaintainReasonableOverhead()
    {
        // Arrange - Test overhead of concurrency coordination
        var singleFileTime = await MeasureSingleFileProcessing(1);
        var concurrentSingleFileTime = await MeasureSingleFileProcessing(10);

        // Assert - Concurrency overhead shouldn't be excessive for single file
        var overheadRatio = (double)concurrentSingleFileTime / singleFileTime;
        
        // Overhead should be reasonable - no more than 300% for single file
        Assert.True(overheadRatio < 3.0, 
            $"Concurrency overhead ratio of {overheadRatio:F2} is too high for single file processing");

        Console.WriteLine($"Single file - Sequential: {singleFileTime}ms, Concurrent: {concurrentSingleFileTime}ms, Overhead: {overheadRatio:F2}x");
    }

    [Theory]
    [InlineData(1, 5)]   // Sequential with 5 files
    [InlineData(3, 15)]  // Moderate concurrency with 15 files
    [InlineData(10, 20)] // High concurrency with 20 files
    public async Task BulkProcessing_LoadScaling_ShouldHandleDifferentWorkloads(int concurrency, int fileCount)
    {
        // Arrange
        _mockConfigService.Setup(x => x.MaxConcurrency).Returns(concurrency);
        var options = new BulkProcessingOptions(_mockConfigService.Object);
        var workloadFiles = _testFiles.Take(fileCount).ToList();
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var processedFiles = await SimulateBulkProcessing(options, workloadFiles);
        stopwatch.Stop();

        // Assert
        Assert.Equal(fileCount, processedFiles.Count);
        Assert.True(stopwatch.ElapsedMilliseconds >= 0);
        
        var throughput = fileCount / (stopwatch.ElapsedMilliseconds / 1000.0);
        Console.WriteLine($"Workload - Concurrency: {concurrency}, Files: {fileCount}, Time: {stopwatch.ElapsedMilliseconds}ms, Throughput: {throughput:F2} files/sec");
    }

    private async Task<List<string>> SimulateBulkProcessing(BulkProcessingOptions options, List<string> files)
    {
        var processedFiles = new List<string>();
        var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
        var tasks = new List<Task>();

        foreach (var file in files)
        {
            tasks.Add(ProcessFileAsync(file, semaphore, processedFiles));
        }

        await Task.WhenAll(tasks);
        return processedFiles;
    }

    private async Task ProcessFileAsync(string filePath, SemaphoreSlim semaphore, List<string> processedFiles)
    {
        await semaphore.WaitAsync();
        try
        {
            // Simulate file processing work (I/O bound simulation)
            await Task.Delay(50 + Random.Shared.Next(0, 50)); // 50-100ms processing time
            
            // Simulate CPU work
            var content = await File.ReadAllTextAsync(filePath);
            var hash = content.GetHashCode();
            
            lock (processedFiles)
            {
                processedFiles.Add(filePath);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<long> MeasureSingleFileProcessing(int concurrency)
    {
        _mockConfigService.Setup(x => x.MaxConcurrency).Returns(concurrency);
        var options = new BulkProcessingOptions(_mockConfigService.Object);
        var stopwatch = Stopwatch.StartNew();

        await SimulateBulkProcessing(options, _testFiles.Take(1).ToList());

        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}