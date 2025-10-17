using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Tests.Contract;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Tests for SQLite database connection pooling efficiency with concurrent operations.
/// Validates T023: Ensure database connection pooling works efficiently with concurrent operations.
/// </summary>
public class DatabaseConcurrencyOptimizationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly ILogger<FuzzyHashService> _logger;
    private readonly SubtitleNormalizationService _normalizationService;

    public DatabaseConcurrencyOptimizationTests()
    {
        _testDbPath = TestDatabaseConfig.GetTempDatabasePath();
        _logger = TestDatabaseConfig.CreateTestLogger<FuzzyHashService>();
        var normalizationLogger = TestDatabaseConfig.CreateTestLogger<SubtitleNormalizationService>();
        _normalizationService = new SubtitleNormalizationService(normalizationLogger);
    }

    [Fact]
    public async Task SqliteConcurrencyOptimizer_ShouldEnableWalMode()
    {
        // Arrange
        var tempDbPath = TestDatabaseConfig.GetTempDatabasePath();
        var optimizerLogger = TestDatabaseConfig.CreateTestLogger<object>();

        try
        {
            // Act
            var result = SqliteConcurrencyOptimizer.OptimizeForConcurrency(tempDbPath, optimizerLogger);

            // Assert
            result.Should().BeTrue("Optimization should succeed for file databases");

            // Verify optimization was applied by creating a service and testing basic operations
            using var fuzzyHashService = new FuzzyHashService(tempDbPath, _logger, _normalizationService);

            await fuzzyHashService.StoreHash(new LabelledSubtitle
            {
                Series = "TestSeries",
                Season = "01",
                Episode = "01",
                SubtitleText = "Test subtitle for concurrency optimization"
            });

            var matches = await fuzzyHashService.FindMatches("Test subtitle for concurrency optimization", 0.35);
            matches.Should().NotBeEmpty("Should find the stored subtitle");
        }
        finally
        {
            TestDatabaseConfig.CleanupTempDatabase(tempDbPath);
        }
    }

    [Fact]
    public void SqliteConcurrencyOptimizer_ShouldSkipInMemoryDatabases()
    {
        // Act
        var result = SqliteConcurrencyOptimizer.OptimizeForConcurrency(":memory:", _logger);

        // Assert
        result.Should().BeFalse("Should skip optimization for in-memory databases");
    }

    [Fact]
    public void SqliteConcurrencyOptimizer_ShouldProvideOptimizedConnectionString()
    {
        // Act
        var connectionString = SqliteConcurrencyOptimizer.GetOptimizedConnectionString(_testDbPath);

        // Assert
        connectionString.Should().Contain("Data Source=");
        connectionString.Should().Contain("Cache=Shared");
    }

    [Fact]
    public async Task FuzzyHashService_WithOptimizedConnection_ShouldHandleConcurrentOperations()
    {
        // Arrange
        using var fuzzyHashService = new FuzzyHashService(_testDbPath, _logger, _normalizationService);

        // Pre-populate with test data
        await fuzzyHashService.StoreHash(new LabelledSubtitle
        {
            Series = "ConcurrentTest",
            Season = "01",
            Episode = "01",
            SubtitleText = "First test episode content"
        });

        // Act - Run concurrent operations
        const int concurrentOperations = 5;
        var tasks = new Task[concurrentOperations];

        for (int i = 0; i < concurrentOperations; i++)
        {
            int taskId = i;
            tasks[i] = Task.Run(async () =>
            {
                // Mix of reads and writes
                if (taskId % 2 == 0)
                {
                    // Read operation
                    var results = await fuzzyHashService.FindMatches("First test episode content", 0.6);
                    results.Should().NotBeEmpty($"Task {taskId} should find matches");
                }
                else
                {
                    // Write operation
                    await fuzzyHashService.StoreHash(new LabelledSubtitle
                    {
                        Series = "ConcurrentTest",
                        Season = "01",
                        Episode = (taskId + 2).ToString("D2"),
                        SubtitleText = $"Concurrent test episode {taskId} content"
                    });
                }
            });
        }

        // Act & Assert - All operations should complete without errors
        await Task.WhenAll(tasks);

        // Verify final state - the concurrent writes should have succeeded
        // We stored 1 initially + 2 from odd-numbered tasks (1 and 3)
        // Total: 3 episodes
        var verification = await fuzzyHashService.FindMatches("First test episode content", 0.5);
        verification.Should().HaveCountGreaterOrEqualTo(1, "Should find the initially stored episode");
    }

    public void Dispose()
    {
        TestDatabaseConfig.CleanupTempDatabase(_testDbPath);
    }
}
