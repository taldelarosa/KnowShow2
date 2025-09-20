using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Tests.Contract;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for database connection pooling efficiency with concurrent operations.
/// Validates that FuzzyHashService handles concurrent operations efficiently without connection bottlenecks.
/// </summary>
public class DatabaseConnectionPoolingTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly ILogger<FuzzyHashService>_logger;
    private readonly SubtitleNormalizationService _normalizationService;

    public DatabaseConnectionPoolingTests()
    {
        _testDbPath = TestDatabaseConfig.GetTempDatabasePath();
        _logger = TestDatabaseConfig.CreateTestLogger<FuzzyHashService>();
        var normalizationLogger = TestDatabaseConfig.CreateTestLogger<SubtitleNormalizationService>();
        _normalizationService = new SubtitleNormalizationService(normalizationLogger);
    }

    [Fact]
    public async Task ConcurrentDatabaseOperations_ShouldHandleHighConcurrency()
    {
        // Arrange
        using var fuzzyHashService = new FuzzyHashService(_testDbPath, _logger, _normalizationService);
        
        // Pre-populate with some test data
        var testEpisodes = new List<LabelledSubtitle>
        {
            new() { Series = "TestShow", Season = "01", Episode = "01", SubtitleText = "Hello world test episode one" },
            new() { Series = "TestShow", Season = "01", Episode = "02", SubtitleText = "This is episode two content" },
            new() { Series = "TestShow", Season = "01", Episode = "03", SubtitleText = "Episode three has different text" }
        };

        foreach (var episode in testEpisodes)
        {
            await fuzzyHashService.StoreHash(episode);
        }

        const int concurrentOperations = 20;
        const int operationsPerType = concurrentOperations / 2; // Half stores, half searches

        var stopwatch = Stopwatch.StartNew();

        // Act - Run concurrent store and search operations
        var storeTasks = Enumerable.Range(0, operationsPerType)
            .Select(i => fuzzyHashService.StoreHash(new LabelledSubtitle
            {
                Series = "ConcurrentTest",
                Season = "01",
                Episode = i.ToString("D2"),
                SubtitleText = $"Concurrent test episode {i} with unique content"
            }))
            .ToList();

        var searchTasks = Enumerable.Range(0, operationsPerType)
            .Select(async i =>
            {
                var searchText = (i % 3) switch
                {
                    0 => "Hello world test episode",
                    1 => "This is episode two",
                    _ => "Episode three has different"
                };
                return await fuzzyHashService.FindMatches(searchText, 0.5);
            })
            .ToList();

        var allTasks = storeTasks.Cast<Task>().Concat(searchTasks).ToArray();
        await Task.WhenAll(allTasks);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, 
            "Concurrent operations should complete within 10 seconds even with connection overhead");

        // Verify all search operations returned some results
        var searchResults = await Task.WhenAll(searchTasks);
        searchResults.Should().NotBeEmpty();
        searchResults.Should().AllSatisfy(results => results.Should().NotBeEmpty());

        Console.WriteLine($"Completed {concurrentOperations} concurrent database operations in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task SequentialVsConcurrent_DatabaseOperations_PerformanceComparison()
    {
        // Arrange
        using var fuzzyHashService = new FuzzyHashService(_testDbPath, _logger, _normalizationService);
        
        // Pre-populate database
        await fuzzyHashService.StoreHash(new LabelledSubtitle 
        { 
            Series = "TestShow", 
            Season = "01", 
            Episode = "01", 
            SubtitleText = "Sample episode content for testing" 
        });

        const int operationCount = 10;
        var searchText = "Sample episode content";

        // Act 1: Sequential operations
        var sequentialStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < operationCount; i++)
        {
            await fuzzyHashService.FindMatches(searchText, 0.5);
        }
        sequentialStopwatch.Stop();

        // Act 2: Concurrent operations
        var concurrentStopwatch = Stopwatch.StartNew();
        var concurrentTasks = Enumerable.Range(0, operationCount)
            .Select(_ => fuzzyHashService.FindMatches(searchText, 0.5))
            .ToArray();
        await Task.WhenAll(concurrentTasks);
        concurrentStopwatch.Stop();

        // Assert
        Console.WriteLine($"Sequential: {sequentialStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Concurrent: {concurrentStopwatch.ElapsedMilliseconds}ms");

        // Concurrent should not be significantly slower than sequential due to connection overhead
        // This test documents current performance for comparison after optimization
        concurrentStopwatch.ElapsedMilliseconds.Should().BeLessThan(sequentialStopwatch.ElapsedMilliseconds * 3,
            "Concurrent operations should not be more than 3x slower than sequential");
    }

    [Fact]
    public async Task DatabaseOperations_UnderMemoryPressure_ShouldMaintainPerformance()
    {
        // Arrange
        using var fuzzyHashService = new FuzzyHashService(_testDbPath, _logger, _normalizationService);
        
        // Pre-populate with substantial data to create memory pressure
        var largeBatch = Enumerable.Range(0, 50)
            .Select(i => new LabelledSubtitle
            {
                Series = "LargeDataset",
                Season = "01",
                Episode = i.ToString("D2"),
                SubtitleText = $"This is a longer subtitle text for episode {i} " +
                             $"with more content to simulate real-world subtitle data " +
                             $"that might be several sentences long and contain various details " +
                             $"about the episode plot, character dialogue, and scene descriptions."
            })
            .ToList();

        foreach (var episode in largeBatch)
        {
            await fuzzyHashService.StoreHash(episode);
        }

        const int concurrentSearches = 15;
        var stopwatch = Stopwatch.StartNew();

        // Act - Perform concurrent searches on large dataset
        var searchTasks = Enumerable.Range(0, concurrentSearches)
            .Select(i => fuzzyHashService.FindMatches($"longer subtitle text episode {i % 10}", 0.14))
            .ToArray();

        var results = await Task.WhenAll(searchTasks);
        stopwatch.Stop();

        // Assert
        results.Should().AllSatisfy(result => result.Should().NotBeEmpty());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000,
            "Concurrent searches on large dataset should complete within 15 seconds");

        Console.WriteLine($"Concurrent searches on 50-record dataset completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    public void Dispose()
    {
        TestDatabaseConfig.CleanupTempDatabase(_testDbPath);
    }
}
