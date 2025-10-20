using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for migrating existing SubtitleHashes entries to include ML embeddings.
/// Batch-generates embeddings for entries that don't have them yet.
/// </summary>
public class DatabaseMigrationService
{
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly IEmbeddingService _embeddingService;
    private readonly string _databasePath;

    public DatabaseMigrationService(
        ILogger<DatabaseMigrationService> logger,
        IEmbeddingService embeddingService,
        string databasePath)
    {
        _logger = logger;
        _embeddingService = embeddingService;
        _databasePath = databasePath;
    }

    /// <summary>
    /// Migrate all SubtitleHashes entries to include embeddings.
    /// Only processes entries where Embedding IS NULL.
    /// </summary>
    /// <param name="batchSize">Number of entries to process in each batch (default: 100)</param>
    /// <param name="cancellationToken">Token for cancelling the operation</param>
    /// <returns>Number of entries successfully migrated</returns>
    public async Task<MigrationResult> MigrateAllEntriesAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new MigrationResult();

        _logger.LogInformation("Starting database migration to add embeddings - Database: {Database}, BatchSize: {BatchSize}",
            _databasePath, batchSize);

        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            // Check if Embedding column exists
            if (!await CheckEmbeddingColumnExists(connection, cancellationToken))
            {
                _logger.LogError("Embedding column does not exist in SubtitleHashes table. Run database migration script first.");
                result.ErrorMessage = "Embedding column does not exist. Run migration script 013_add_embedding_columns.sql first.";
                return result;
            }

            // Get count of entries needing migration
            result.TotalEntries = await GetEntriesNeedingMigrationCount(connection, cancellationToken);

            if (result.TotalEntries == 0)
            {
                stopwatch.Stop();
                _logger.LogInformation("No entries need migration - all entries already have embeddings");
                result.Success = true;
                result.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
                return result;
            }

            _logger.LogInformation("Found {Count} entries needing embedding generation", result.TotalEntries);

            // Process in batches
            var processedCount = 0;
            while (processedCount < result.TotalEntries && !cancellationToken.IsCancellationRequested)
            {
                var batchResult = await ProcessBatchAsync(connection, batchSize, cancellationToken);

                result.EntriesProcessed += batchResult.Processed;
                result.EntriesFailed += batchResult.Failed;
                processedCount += batchResult.Processed + batchResult.Failed;

                _logger.LogInformation("Migration progress: {Processed}/{Total} entries ({Percentage:P1})",
                    processedCount, result.TotalEntries, (double)processedCount / result.TotalEntries);

                if (batchResult.Processed == 0)
                {
                    // No more entries to process
                    break;
                }
            }

            stopwatch.Stop();
            result.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
            result.Success = result.EntriesFailed == 0;

            _logger.LogInformation("Database migration completed - Total: {Total}, Processed: {Processed}, Failed: {Failed}, Duration: {Duration:F2}s",
                result.TotalEntries, result.EntriesProcessed, result.EntriesFailed, result.DurationSeconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
            result.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Database migration failed - Error: {Error}, Duration: {Duration:F2}s",
                ex.Message, result.DurationSeconds);

            return result;
        }
    }

    private async Task<bool> CheckEmbeddingColumnExists(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(SubtitleHashes)";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(1); // Column name is at index 1
            if (columnName == "Embedding")
            {
                return true;
            }
        }

        return false;
    }

    private async Task<int> GetEntriesNeedingMigrationCount(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SubtitleHashes WHERE Embedding IS NULL AND CleanText IS NOT NULL";

        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count);
    }

    private async Task<BatchResult> ProcessBatchAsync(
        SqliteConnection connection,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var result = new BatchResult();

        try
        {
            // Fetch batch of entries
            var entries = await FetchBatchAsync(connection, batchSize, cancellationToken);

            if (entries.Count == 0)
            {
                return result;
            }

            // Generate embeddings
            var cleanTexts = entries.Select(e => e.CleanText).ToList();
            var embeddings = _embeddingService.BatchGenerateEmbeddings(cleanTexts);

            // Update database
            using var transaction = connection.BeginTransaction();

            for (int i = 0; i < entries.Count; i++)
            {
                try
                {
                    await UpdateEntryEmbedding(connection, entries[i].Id, embeddings[i], cancellationToken);
                    result.Processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update embedding for entry {Id}: {Error}", entries[i].Id, ex.Message);
                    result.Failed++;
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch processing failed: {Error}", ex.Message);
            result.Failed += batchSize; // Assume entire batch failed
        }

        return result;
    }

    private async Task<List<SubtitleHashEntry>> FetchBatchAsync(
        SqliteConnection connection,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var entries = new List<SubtitleHashEntry>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, CleanText, SubtitleSourceFormat
            FROM SubtitleHashes
            WHERE Embedding IS NULL AND CleanText IS NOT NULL
            LIMIT @batchSize";
        command.Parameters.AddWithValue("@batchSize", batchSize);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new SubtitleHashEntry
            {
                Id = reader.GetInt32(0),
                CleanText = reader.GetString(1),
                SourceFormat = reader.IsDBNull(2) ? "Text" : reader.GetString(2)
            });
        }

        return entries;
    }

    private async Task UpdateEntryEmbedding(
        SqliteConnection connection,
        int id,
        float[] embedding,
        CancellationToken cancellationToken)
    {
        // Serialize embedding to bytes
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE SubtitleHashes
            SET Embedding = @embedding
            WHERE Id = @id";
        command.Parameters.AddWithValue("@embedding", bytes);
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private class SubtitleHashEntry
    {
        public int Id { get; set; }
        public string CleanText { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = "Text";
    }

    private class BatchResult
    {
        public int Processed { get; set; }
        public int Failed { get; set; }
    }
}

/// <summary>
/// Result of a database migration operation.
/// </summary>
public class MigrationResult
{
    /// <summary>
    /// Total number of entries that needed migration.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Number of entries successfully processed.
    /// </summary>
    public int EntriesProcessed { get; set; }

    /// <summary>
    /// Number of entries that failed to process.
    /// </summary>
    public int EntriesFailed { get; set; }

    /// <summary>
    /// Duration of the migration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Whether the migration completed successfully (all entries processed).
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if migration failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
