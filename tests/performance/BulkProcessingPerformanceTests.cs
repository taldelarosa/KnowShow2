using System.Diagnostics;
using System.IO.Abstractions.TestingHelpers;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EpisodeIdentifier.Tests.Performance;

/// <summary>
/// Performance tests for bulk processing operations.
/// Tests throughput, memory usage, and scalability under various load conditions.
/// </summary>
public class BulkProcessingPerformanceTests : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly ILogger<BulkProcessorService> _logger;
    private readonly BulkProcessorService _bulkProcessor;
    private readonly Stopwatch _stopwatch;

    public BulkProcessingPerformanceTests()
    {
        _fileSystem = new MockFileSystem();
        _logger = Substitute.For<ILogger<BulkProcessorService>>();
        _bulkProcessor = new BulkProcessorService(_logger, _fileSystem);
        _stopwatch = new Stopwatch();
    }

    public void Dispose()
    {
        _stopwatch?.Stop();
    }

    [Fact]
    public async Task ProcessAsync_SmallBatch_ShouldMeetPerformanceTargets()
    {
        // Arrange
        var files = CreateTestFiles(10);
        var request = CreatePerformanceTestRequest(files, batchSize: 5, maxConcurrency: 2);

        // Act
        _stopwatch.Start();
        var result = await _bulkProcessor.ProcessAsync(request);
        _stopwatch.Stop();

        // Assert
        result.Status.Should().Be(BulkProcessingStatus.Completed);
        result.ProcessedFiles.Should().Be(10);
        
        // Performance targets for small batches
        _stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // < 5 seconds
        result.TotalDuration.Should().BeLessThan(TimeSpan.FromSeconds(5));
        
        // Memory should remain reasonable
        GC.Collect();
        var memoryBefore = GC.GetTotalMemory(true);
        memoryBefore.Should().BeLessThan(50 * 1024 * 1024); // < 50MB
    }

    [Fact]
    public async Task ProcessAsync_MediumBatch_ShouldScaleEfficiently()
    {
        // Arrange
        var files = CreateTestFiles(100);
        var request = CreatePerformanceTestRequest(files, batchSize: 20, maxConcurrency: 4);

        var progressReports = new List<BulkProcessingProgress>();
        var progress = new Progress<BulkProcessingProgress>(p => progressReports.Add(p));
        request.Progress = progress;

        // Act
        _stopwatch.Start();
        var result = await _bulkProcessor.ProcessAsync(request);
        _stopwatch.Stop();

        // Assert
        result.Status.Should().Be(BulkProcessingStatus.Completed);
        result.ProcessedFiles.Should().Be(100);
        
        // Performance targets for medium batches
        _stopwatch.ElapsedMilliseconds.Should().BeLessThan(20000); // < 20 seconds
        result.TotalDuration.Should().BeLessThan(TimeSpan.FromSeconds(20));
        
        // Throughput should be reasonable (files per second)
        var throughput = result.ProcessedFiles / result.TotalDuration.TotalSeconds;
        throughput.Should().BeGreaterThan(5); // > 5 files/second

        // Should have efficient batching
        var batchCompletionReports = progressReports.Where(p => p.Details.Contains("batch")).ToList();
        batchCompletionReports.Should().HaveCount(5); // 100 files / 20 batch size = 5 batches
    }

    [Fact]
    public async Task ProcessAsync_LargeBatch_ShouldHandleMemoryEfficiently()
    {
        // Arrange
        var files = CreateTestFiles(500);
        var request = CreatePerformanceTestRequest(files, batchSize: 50, maxConcurrency: 8);
        request.Options.ForceGarbageCollection = true; // Enable memory management

        var memoryBefore = GC.GetTotalMemory(true);

        // Act
        _stopwatch.Start();
        var result = await _bulkProcessor.ProcessAsync(request);
        _stopwatch.Stop();

        // Assert
        result.Status.Should().Be(BulkProcessingStatus.Completed);
        result.ProcessedFiles.Should().Be(500);
        
        // Performance targets for large batches
        _stopwatch.ElapsedMilliseconds.Should().BeLessThan(60000); // < 60 seconds
        result.TotalDuration.Should().BeLessThan(TimeSpan.FromMinutes(1));
        
        // Memory growth should be controlled
        GC.Collect();
        var memoryAfter = GC.GetTotalMemory(true);
        var memoryGrowth = memoryAfter - memoryBefore;
        memoryGrowth.Should().BeLessThan(100 * 1024 * 1024); // < 100MB growth

        // High throughput for large batches
        var throughput = result.ProcessedFiles / result.TotalDuration.TotalSeconds;
        throughput.Should().BeGreaterThan(8); // > 8 files/second
    }

    [Fact]
    public async Task ProcessAsync_ConcurrencyScaling_ShouldImproveWithMoreConcurrency()
    {
        // Arrange
        var files = CreateTestFiles(100);
        
        // Test with low concurrency
        var lowConcurrencyRequest = CreatePerformanceTestRequest(files, batchSize: 10, maxConcurrency: 2);
        var lowConcurrencyResult = await _bulkProcessor.ProcessAsync(lowConcurrencyRequest);

        // Test with high concurrency
        var highConcurrencyRequest = CreatePerformanceTestRequest(files, batchSize: 10, maxConcurrency: 8);
        var highConcurrencyResult = await _bulkProcessor.ProcessAsync(highConcurrencyRequest);

        // Assert
        lowConcurrencyResult.Status.Should().Be(BulkProcessingStatus.Completed);
        highConcurrencyResult.Status.Should().Be(BulkProcessingStatus.Completed);

        // Higher concurrency should generally be faster (accounting for overhead)
        var lowThroughput = lowConcurrencyResult.ProcessedFiles / lowConcurrencyResult.TotalDuration.TotalSeconds;
        var highThroughput = highConcurrencyResult.ProcessedFiles / highConcurrencyResult.TotalDuration.TotalSeconds;

        // Allow some variance due to overhead, but expect improvement
        highThroughput.Should().BeGreaterOrEqualTo(lowThroughput * 0.8); // At least 80% of expected scaling
    }

    [Fact]
    public async Task ProcessAsync_BatchSizeOptimization_ShouldFindOptimalBalance()
    {
        // Arrange
        var files = CreateTestFiles(200);
        var results = new Dictionary<int, TimeSpan>();

        // Test different batch sizes
        var batchSizes = new[] { 10, 25, 50, 100 };

        foreach (var batchSize in batchSizes)
        {
            var request = CreatePerformanceTestRequest(files, batchSize, maxConcurrency: 4);
            var result = await _bulkProcessor.ProcessAsync(request);
            results[batchSize] = result.TotalDuration;
        }

        // Assert
        // All should complete successfully
        foreach (var batchSize in batchSizes)
        {
            results[batchSize].Should().BeLessThan(TimeSpan.FromMinutes(1));
        }

        // There should be an optimal range (not too small, not too large)
        var fastestTime = results.Values.Min();
        var slowestTime = results.Values.Max();
        
        // The difference shouldn't be too extreme (good batch sizing should help)
        (slowestTime.TotalMilliseconds / fastestTime.TotalMilliseconds).Should().BeLessThan(3.0);
    }

    [Fact]
    public async Task ProcessAsync_ProgressReporting_ShouldNotSignificantlyImpactPerformance()
    {
        // Arrange
        var files = CreateTestFiles(100);

        // Test without progress reporting
        var withoutProgressRequest = CreatePerformanceTestRequest(files, batchSize: 20, maxConcurrency: 4);
        var withoutProgressResult = await _bulkProcessor.ProcessAsync(withoutProgressRequest);

        // Test with progress reporting
        var withProgressRequest = CreatePerformanceTestRequest(files, batchSize: 20, maxConcurrency: 4);
        var progressReports = new List<BulkProcessingProgress>();
        var progress = new Progress<BulkProcessingProgress>(p => progressReports.Add(p));
        withProgressRequest.Progress = progress;
        withProgressRequest.Options.ProgressReportingInterval = 100; // Frequent reporting
        
        var withProgressResult = await _bulkProcessor.ProcessAsync(withProgressRequest);

        // Assert
        withoutProgressResult.Status.Should().Be(BulkProcessingStatus.Completed);
        withProgressResult.Status.Should().Be(BulkProcessingStatus.Completed);

        // Progress reporting should not significantly impact performance (< 20% overhead)
        var overhead = (withProgressResult.TotalDuration.TotalMilliseconds - withoutProgressResult.TotalDuration.TotalMilliseconds) 
                      / withoutProgressResult.TotalDuration.TotalMilliseconds;
        overhead.Should().BeLessThan(0.2); // < 20% overhead

        // Should have received progress reports
        progressReports.Should().NotBeEmpty();
        progressReports.Should().HaveCountGreaterThan(10);
    }

    [Fact]
    public async Task ProcessAsync_ErrorHandling_ShouldNotDegradePerformanceSignificantly()
    {
        // Arrange
        var goodFiles = CreateTestFiles(80);
        var mixedFiles = CreateMixedTestFiles(80, 20); // 80 good + 20 problematic

        // Test with all good files
        var goodFilesRequest = CreatePerformanceTestRequest(goodFiles, batchSize: 20, maxConcurrency: 4);
        var goodFilesResult = await _bulkProcessor.ProcessAsync(goodFilesRequest);

        // Test with mixed files (some will fail)
        var mixedFilesRequest = CreatePerformanceTestRequest(mixedFiles, batchSize: 20, maxConcurrency: 4);
        mixedFilesRequest.Options.ContinueOnError = true;
        mixedFilesRequest.Options.RetryAttempts = 1;
        var mixedFilesResult = await _bulkProcessor.ProcessAsync(mixedFilesRequest);

        // Assert
        goodFilesResult.Status.Should().Be(BulkProcessingStatus.Completed);
        mixedFilesResult.Status.Should().Be(BulkProcessingStatus.CompletedWithErrors);

        // Error handling should not cause excessive performance degradation
        var performanceDelta = mixedFilesResult.TotalDuration.TotalMilliseconds / goodFilesResult.TotalDuration.TotalMilliseconds;
        performanceDelta.Should().BeLessThan(2.0); // Should not take more than 2x as long

        // Should have processed successful files efficiently
        mixedFilesResult.SuccessfulFiles.Should().BeGreaterThan(60); // Most should succeed
        mixedFilesResult.FailedFiles.Should().BeGreaterThan(0); // Some should fail
    }

    [Fact]
    public async Task ProcessAsync_MemoryPressure_ShouldHandleGracefully()
    {
        // Arrange
        var files = CreateLargeTestFiles(1000); // Large number of files
        var request = CreatePerformanceTestRequest(files, batchSize: 100, maxConcurrency: 2); // Larger batches
        request.Options.ForceGarbageCollection = true;

        var initialMemory = GC.GetTotalMemory(true);

        // Act
        var result = await _bulkProcessor.ProcessAsync(request);

        // Assert
        result.Status.Should().Be(BulkProcessingStatus.Completed);
        result.ProcessedFiles.Should().Be(1000);

        // Memory should not grow excessively
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = finalMemory - initialMemory;
        
        // Allow for reasonable growth but prevent memory leaks
        memoryGrowth.Should().BeLessThan(200 * 1024 * 1024); // < 200MB growth

        // Should maintain decent throughput even under memory pressure
        var throughput = result.ProcessedFiles / result.TotalDuration.TotalSeconds;
        throughput.Should().BeGreaterThan(5); // > 5 files/second
    }

    private List<string> CreateTestFiles(int count)
    {
        var files = new List<string>();
        for (int i = 1; i <= count; i++)
        {
            var fileName = $"/test/performance/Series S{(i % 10) + 1:D2}E{i:D2} Episode {i}.mkv";
            files.Add(fileName);
            _fileSystem.AddFile(fileName, new MockFileData($"test video content {i}"));
        }
        return files;
    }

    private List<string> CreateLargeTestFiles(int count)
    {
        var files = new List<string>();
        for (int i = 1; i <= count; i++)
        {
            var fileName = $"/test/large/Series{i % 50} S{(i % 20) + 1:D2}E{(i % 25) + 1:D2} Episode {i}.mkv";
            files.Add(fileName);
            _fileSystem.AddFile(fileName, new MockFileData($"large test video content {i} with more data"));
        }
        return files;
    }

    private List<string> CreateMixedTestFiles(int goodCount, int badCount)
    {
        var files = new List<string>();
        
        // Add good files
        for (int i = 1; i <= goodCount; i++)
        {
            var fileName = $"/test/mixed/Good Series S{(i % 10) + 1:D2}E{i:D2} Episode {i}.mkv";
            files.Add(fileName);
            _fileSystem.AddFile(fileName, new MockFileData($"good video content {i}"));
        }

        // Add problematic files (missing files)
        for (int i = 1; i <= badCount; i++)
        {
            var fileName = $"/test/mixed/Missing Series S{i:D2}E01 Episode {i}.mkv";
            files.Add(fileName);
            // Intentionally not adding to file system to simulate missing files
        }

        return files;
    }

    private static BulkProcessingRequest CreatePerformanceTestRequest(
        List<string> files, 
        int batchSize, 
        int maxConcurrency)
    {
        return new BulkProcessingRequest
        {
            InputPaths = files,
            Options = new BulkProcessingOptions
            {
                BatchSize = batchSize,
                MaxConcurrency = maxConcurrency,
                ContinueOnError = true,
                CreateBackups = false,
                ProgressReportingInterval = 1000,
                RetryAttempts = 0, // No retries in performance tests unless specified
                ForceGarbageCollection = false // Only when testing memory
            }
        };
    }
}
