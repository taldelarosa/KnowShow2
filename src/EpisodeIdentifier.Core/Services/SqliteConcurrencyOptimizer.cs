using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// SQLite database configuration service for optimal concurrent performance.
/// Configures SQLite settings like WAL mode and connection pooling for better concurrent access.
/// </summary>
public static class SqliteConcurrencyOptimizer
{
    /// <summary>
    /// Configures SQLite database for optimal concurrent performance.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>True if optimization was applied, false if database is in-memory or optimization failed</returns>
    public static bool OptimizeForConcurrency(string databasePath, ILogger? logger = null)
    {
        // Skip optimization for in-memory databases
        if (databasePath == ":memory:")
        {
            logger?.LogDebug("Skipping concurrency optimization for in-memory database");
            return false;
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            // Enable WAL mode for better concurrent performance
            // WAL mode allows concurrent readers with a single writer
            using (var walCommand = connection.CreateCommand())
            {
                walCommand.CommandText = "PRAGMA journal_mode=WAL;";
                var walResult = walCommand.ExecuteScalar()?.ToString();
                logger?.LogInformation("SQLite WAL mode enabled: {Result}", walResult);
            }

            // Set busy timeout for better handling of concurrent access
            // This makes SQLite wait up to 5 seconds when database is locked
            using (var timeoutCommand = connection.CreateCommand())
            {
                timeoutCommand.CommandText = "PRAGMA busy_timeout=5000;";
                timeoutCommand.ExecuteNonQuery();
                logger?.LogInformation("SQLite busy timeout set to 5000ms for better concurrent handling");
            }

            // Enable query optimizer for better performance
            using (var optimizeCommand = connection.CreateCommand())
            {
                optimizeCommand.CommandText = "PRAGMA optimize;";
                optimizeCommand.ExecuteNonQuery();
                logger?.LogDebug("SQLite query optimizer enabled");
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning("Could not optimize SQLite database for concurrency: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets the recommended connection string for concurrent operations.
    /// </summary>
    /// <param name="databasePath">Path to the database file</param>
    /// <returns>Optimized connection string</returns>
    public static string GetOptimizedConnectionString(string databasePath)
    {
        if (databasePath == ":memory:")
        {
            return $"Data Source={databasePath}";
        }

        // Add connection pooling and timeout parameters
        // Pooling is enabled by default by Microsoft.Data.Sqlite, but explicitly set for clarity.
        return $"Data Source={databasePath};Cache=Shared;Pooling=True;Default Timeout=5;";
    }
}