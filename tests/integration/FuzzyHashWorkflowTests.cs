using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Services.Hashing;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Interfaces;
using System.IO;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for fuzzy hash comparison workflow functionality.
/// These tests verify end-to-end CTPH hashing and comparison behavior.
/// All tests MUST FAIL until the full integration is implemented.
/// </summary>
public class FuzzyHashWorkflowTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<FuzzyHashWorkflowTests> _logger;
    private readonly List<string> _testFilesToCleanup = new();
    private readonly string _testFilesDirectory;

    public FuzzyHashWorkflowTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Register services - This will fail until services are implemented
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<ICTPhHashingService, CTPhHashingService>();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<FuzzyHashWorkflowTests>>();

        _testFilesDirectory = Path.Combine(Path.GetTempPath(), "episodeidentifier_hash_tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testFilesDirectory);
    }

    [Fact]
    public async Task ComputeAndCompareFuzzyHashes_WithIdenticalFiles_ReturnsHighSimilarity()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var testFile1 = CreateTestMediaFile("identical_file.mkv", 1024 * 1024); // 1MB
        var testFile2 = CreateTestMediaFile("identical_copy.mkv", 1024 * 1024, sameContent: true);
        var hashingService = _serviceProvider.GetRequiredService<ICTPhHashingService>();

        // Act
        var result = await hashingService.CompareFiles(testFile1, testFile2);

        // Assert
        result.Should().NotBeNull();
        result.Hash1.Should().NotBeNull().And.NotBeEmpty();
        result.Hash2.Should().NotBeNull().And.NotBeEmpty();
        result.SimilarityScore.Should().BeGreaterThan(90, "Identical files should have very high similarity");
        result.IsMatch.Should().BeTrue();
        result.ComparisonTime.Should().BePositive();

        _logger.LogInformation("Fuzzy hash comparison of identical files: {Score}% similarity in {Time}ms",
            result.SimilarityScore, result.ComparisonTime.TotalMilliseconds);
    }

    [Fact]
    public async Task ComputeAndCompareFuzzyHashes_WithSimilarFiles_ReturnsModerateScore()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var testFile1 = CreateTestMediaFile("original.mkv", 1024 * 1024);
        var testFile2 = CreateTestMediaFile("slightly_different.mkv", 1024 * 1024 + 1024); // Slightly larger
        var hashingService = _serviceProvider.GetRequiredService<ICTPhHashingService>();

        // Act
        var result = await hashingService.CompareFiles(testFile1, testFile2);

        // Assert
        result.Should().NotBeNull();
        result.SimilarityScore.Should().BeInRange(30, 95, "Similar files should have moderate similarity");
        result.ComparisonTime.Should().BePositive();

        _logger.LogInformation("Fuzzy hash comparison of similar files: {Score}% similarity in {Time}ms",
            result.SimilarityScore, result.ComparisonTime.TotalMilliseconds);
    }

    [Fact]
    public async Task ComputeAndCompareFuzzyHashes_WithCompletelyDifferentFiles_ReturnsLowScore()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var testFile1 = CreateTestMediaFile("file1.mkv", 512 * 1024, pattern: 0xAA);
        var testFile2 = CreateTestMediaFile("file2.mkv", 2048 * 1024, pattern: 0x55);
        var hashingService = _serviceProvider.GetRequiredService<ICTPhHashingService>();

        // Act
        var result = await hashingService.CompareFiles(testFile1, testFile2);

        // Assert
        result.Should().NotBeNull();
        result.SimilarityScore.Should().BeLessThan(30, "Completely different files should have low similarity");
        result.ComparisonTime.Should().BePositive();

        _logger.LogInformation("Fuzzy hash comparison of different files: {Score}% similarity in {Time}ms",
            result.SimilarityScore, result.ComparisonTime.TotalMilliseconds);
    }

    [Fact]
    public async Task FuzzyHashWorkflow_WithConfiguredThreshold_AppliesCorrectMatching()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var hashingService = _serviceProvider.GetRequiredService<ICTPhHashingService>();

        // Load configuration to get threshold
        var configResult = await configService.LoadConfiguration();
        configResult.IsValid.Should().BeTrue("Configuration should be valid for this test");

        var testFile1 = CreateTestMediaFile("threshold_test1.mkv", 1024 * 1024);
        var testFile2 = CreateTestMediaFile("threshold_test2.mkv", 1024 * 1024 + 512);

        // Act
        var comparisonResult = await hashingService.CompareFiles(testFile1, testFile2);
        var configuredThreshold = hashingService.GetSimilarityThreshold();

        // Assert
        comparisonResult.Should().NotBeNull();
        configuredThreshold.Should().BeInRange(0, 100);
        comparisonResult.IsMatch.Should().Be(comparisonResult.SimilarityScore >= configuredThreshold);

        _logger.LogInformation("Applied threshold {Threshold}% to similarity {Score}%: Match = {IsMatch}",
            configuredThreshold, comparisonResult.SimilarityScore, comparisonResult.IsMatch);
    }

    [Fact]
    public async Task FuzzyHashWorkflow_WithMultipleFiles_ProcessesEfficiently()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var hashingService = _serviceProvider.GetRequiredService<ICTPhHashingService>();
        var testFiles = new List<string>();

        // Create multiple test files
        for (int i = 0; i < 5; i++)
        {
            testFiles.Add(CreateTestMediaFile($"batch_file_{i}.mkv", (i + 1) * 256 * 1024));
        }

        var comparisons = new List<FuzzyHashResult>();
        var startTime = DateTime.UtcNow;

        // Act - Compare each file with the first one
        foreach (var file in testFiles.Skip(1))
        {
            var result = await hashingService.CompareFiles(testFiles[0], file);
            comparisons.Add(result);
        }

        var totalTime = DateTime.UtcNow - startTime;

        // Assert
        comparisons.Should().HaveCount(4);
        comparisons.Should().AllSatisfy(result =>
        {
            result.Should().NotBeNull();
            result.ComparisonTime.Should().BeLessThan(TimeSpan.FromSeconds(5), "Each comparison should be reasonably fast");
            result.SimilarityScore.Should().BeInRange(0, 100);
        });

        totalTime.Should().BeLessThan(TimeSpan.FromSeconds(30), "Batch processing should be efficient");

        _logger.LogInformation("Processed {Count} comparisons in {TotalTime}ms (avg {AvgTime}ms per comparison)",
            comparisons.Count, totalTime.TotalMilliseconds,
            comparisons.Average(c => c.ComparisonTime.TotalMilliseconds));
    }

    [Fact]
    public async Task FuzzyHashWorkflow_WithLargeFiles_HandlesMemoryEfficiently()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var hashingService = _serviceProvider.GetRequiredService<ICTPhHashingService>();
        var largeFile1 = CreateTestMediaFile("large1.mkv", 10 * 1024 * 1024); // 10MB
        var largeFile2 = CreateTestMediaFile("large2.mkv", 12 * 1024 * 1024); // 12MB

        var initialMemory = GC.GetTotalMemory(true);

        // Act
        var result = await hashingService.CompareFiles(largeFile1, largeFile2);

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        result.Should().NotBeNull();
        result.ComparisonTime.Should().BeLessThan(TimeSpan.FromSeconds(10), "Large file comparison should complete reasonably quickly");
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory usage should not grow excessively for large files");

        _logger.LogInformation("Large file comparison: {Score}% similarity, {Time}ms, memory increase: {Memory} bytes",
            result.SimilarityScore, result.ComparisonTime.TotalMilliseconds, memoryIncrease);
    }

    [Fact]
    public async Task FuzzyHashWorkflow_WithInvalidFile_HandlesErrorsGracefully()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var hashingService = _serviceProvider.GetRequiredService<ICTPhHashingService>();
        var validFile = CreateTestMediaFile("valid.mkv", 1024 * 1024);
        var nonExistentFile = Path.Combine(_testFilesDirectory, "nonexistent.mkv");

        // Act & Assert
        await hashingService.Invoking(s => s.CompareFiles(validFile, nonExistentFile))
            .Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*nonexistent.mkv*");

        _logger.LogInformation("Properly handled comparison with non-existent file");
    }

    private string CreateTestMediaFile(string fileName, int sizeBytes, bool sameContent = false, byte pattern = 0x00)
    {
        var filePath = Path.Combine(_testFilesDirectory, fileName);

        using var stream = File.Create(filePath);
        var buffer = new byte[4096];

        if (sameContent && _testFilesToCleanup.Any())
        {
            // Copy content from first file for identical comparison
            var firstFile = _testFilesToCleanup.First();
            if (File.Exists(firstFile))
            {
                File.Copy(firstFile, filePath, true);
                _testFilesToCleanup.Add(filePath);
                return filePath;
            }
        }

        // Fill with pattern
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)((pattern + i) % 256);
        }

        var bytesWritten = 0;
        while (bytesWritten < sizeBytes)
        {
            var bytesToWrite = Math.Min(buffer.Length, sizeBytes - bytesWritten);
            stream.Write(buffer, 0, bytesToWrite);
            bytesWritten += bytesToWrite;
        }

        _testFilesToCleanup.Add(filePath);
        return filePath;
    }

    public void Dispose()
    {
        foreach (var file in _testFilesToCleanup)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup test file: {FilePath}", file);
            }
        }

        if (Directory.Exists(_testFilesDirectory))
        {
            try
            {
                Directory.Delete(_testFilesDirectory, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup test directory: {DirectoryPath}", _testFilesDirectory);
            }
        }

        _serviceProvider.Dispose();
    }
}