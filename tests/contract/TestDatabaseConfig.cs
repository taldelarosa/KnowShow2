using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.IO;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Centralized configuration for test database paths and service creation.
/// Improves maintainability and environment independence.
/// </summary>
public static class TestDatabaseConfig
{
    /// <summary>
    /// Generates a unique temporary database file path for testing.
    /// </summary>
    /// <returns>Full path to a temporary database file</returns>
    public static string GetTempDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
    }

    /// <summary>
    /// Gets the path to test data directory relative to the test project.
    /// </summary>
    /// <param name="fileName">Optional filename to append to the test data path</param>
    /// <returns>Path to test data file or directory</returns>
    public static string GetTestDataPath(string fileName = "")
    {
        var testDataDir = "TestData";
        return string.IsNullOrEmpty(fileName) ? testDataDir : Path.Combine(testDataDir, fileName);
    }

    /// <summary>
    /// Gets the standard test hash database path.
    /// For integration tests, uses a temporary database if the standard test file doesn't exist.
    /// </summary>
    /// <returns>Path to the test hash database</returns>
    public static string GetTestHashDatabasePath()
    {
        var standardPath = GetTestDataPath("hashes.sqlite");
        
        // For integration tests: if the test file doesn't exist, use a temp database
        if (!File.Exists(standardPath))
        {
            return GetTempDatabasePath();
        }
        
        return standardPath;
    }

    /// <summary>
    /// Creates a logger for test services.
    /// </summary>
    /// <typeparam name="T">The type to create a logger for</typeparam>
    /// <returns>A configured logger instance</returns>
    public static ILogger<T> CreateTestLogger<T>()
    {
        return LoggerFactory.Create(builder => builder.AddConsole())
                          .CreateLogger<T>();
    }

    /// <summary>
    /// Creates a test FuzzyHashService with all required dependencies.
    /// </summary>
    /// <param name="databasePath">Optional custom database path. If null, uses a temporary path.</param>
    /// <returns>Configured FuzzyHashService for testing</returns>
    public static FuzzyHashService CreateTestFuzzyHashService(string? databasePath = null)
    {
        var dbPath = databasePath ?? GetTempDatabasePath();
        var logger = CreateTestLogger<FuzzyHashService>();
        var normalizationLogger = CreateTestLogger<SubtitleNormalizationService>();
        var normalizationService = new SubtitleNormalizationService(normalizationLogger);
        
        return new FuzzyHashService(dbPath, logger, normalizationService);
    }

    /// <summary>
    /// Cleans up a temporary database file if it exists.
    /// </summary>
    /// <param name="databasePath">Path to the database file to delete</param>
    public static void CleanupTempDatabase(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            try
            {
                File.Delete(databasePath);
            }
            catch (Exception)
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}