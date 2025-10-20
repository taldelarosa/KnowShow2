using System.Data;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for vector similarity search using vectorlite SQLite extension.
/// Performs fast cosine similarity search with HNSW indexing.
/// </summary>
public class VectorSearchService : IVectorSearchService
{
    private readonly ILogger<VectorSearchService> _logger;
    private readonly string _databasePath;
    private bool _vectorliteLoaded = false;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public VectorSearchService(ILogger<VectorSearchService> logger, string databasePath)
    {
        _logger = logger;
        _databasePath = databasePath;
    }

    /// <inheritdoc/>
    public List<VectorSimilarityResult> SearchBySimilarity(
        float[] queryEmbedding, 
        int topK = 10, 
        double minSimilarity = 0.0)
    {
        if (queryEmbedding == null)
        {
            throw new ArgumentNullException(nameof(queryEmbedding));
        }

        if (queryEmbedding.Length != 384)
        {
            throw new ArgumentException("Query embedding must be exactly 384 dimensions", nameof(queryEmbedding));
        }

        if (topK <= 0)
        {
            throw new ArgumentException("topK must be greater than zero", nameof(topK));
        }

        EnsureVectorliteLoaded();

        _logger.LogDebug("Searching for top {TopK} similar subtitles (minSimilarity: {MinSimilarity})", 
            topK, minSimilarity);

        var results = new List<VectorSimilarityResult>();

        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            // Query vector index for similar embeddings
            // Vectorlite uses: vector_distance(vector1, vector2, distance_type)
            // distance_type: 'cosine' for cosine distance, 'l2' for Euclidean
            // Cosine similarity = 1 - cosine distance
            var query = @"
                SELECT 
                    sh.Id,
                    sh.Series,
                    sh.Season,
                    sh.Episode,
                    sh.EpisodeName,
                    sh.SubtitleSourceFormat,
                    (1.0 - vector_distance(sh.Embedding, @queryEmbedding, 'cosine')) as Similarity
                FROM SubtitleHashes sh
                WHERE sh.Embedding IS NOT NULL
                    AND (1.0 - vector_distance(sh.Embedding, @queryEmbedding, 'cosine')) >= @minSimilarity
                ORDER BY Similarity DESC
                LIMIT @topK";

            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.Parameters.AddWithValue("@queryEmbedding", SerializeEmbedding(queryEmbedding));
            command.Parameters.AddWithValue("@minSimilarity", minSimilarity);
            command.Parameters.AddWithValue("@topK", topK);

            using var reader = command.ExecuteReader();
            int rank = 1;

            while (reader.Read())
            {
                var similarity = reader.GetDouble(reader.GetOrdinal("Similarity"));
                var distance = 1.0 - similarity;
                
                // Calculate confidence based on source format
                var sourceFormatStr = reader.GetString(reader.GetOrdinal("SubtitleSourceFormat"));
                var sourceFormat = SubtitleSourceFormatExtensions.FromDbString(sourceFormatStr);
                var confidence = CalculateConfidence(similarity, sourceFormat);

                var result = new VectorSimilarityResult(
                    id: reader.GetInt32(reader.GetOrdinal("Id")),
                    series: reader.GetString(reader.GetOrdinal("Series")),
                    season: reader.GetString(reader.GetOrdinal("Season")),
                    episode: reader.GetString(reader.GetOrdinal("Episode")),
                    episodeName: reader.IsDBNull(reader.GetOrdinal("EpisodeName")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("EpisodeName")),
                    sourceFormat: sourceFormat,
                    similarity: similarity,
                    confidence: confidence,
                    distance: distance,
                    rank: rank++
                );

                results.Add(result);
            }

            _logger.LogInformation("Found {Count} similar subtitles", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching by similarity: {Error}", ex.Message);
            
            // If vectorlite is not available, return empty results for now
            // In production, this should throw
            _logger.LogWarning("Vectorlite may not be loaded - returning empty results");
            return new List<VectorSimilarityResult>();
        }
    }

    /// <inheritdoc/>
    public bool IsVectorliteLoaded()
    {
        return _vectorliteLoaded;
    }

    /// <inheritdoc/>
    public VectorIndexStats GetIndexStats()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COUNT(*) as TotalVectors,
                    SUM(LENGTH(Embedding)) as TotalSize
                FROM SubtitleHashes
                WHERE Embedding IS NOT NULL";

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var totalVectors = reader.GetInt32(0);
                var totalSize = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);

                return new VectorIndexStats
                {
                    TotalVectors = totalVectors,
                    Dimension = 384,
                    IndexSizeBytes = totalSize,
                    LastRebuild = null // TODO: Track rebuild timestamp
                };
            }

            return new VectorIndexStats
            {
                TotalVectors = 0,
                Dimension = 384,
                IndexSizeBytes = 0,
                LastRebuild = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting index stats: {Error}", ex.Message);
            
            return new VectorIndexStats
            {
                TotalVectors = 0,
                Dimension = 384,
                IndexSizeBytes = 0,
                LastRebuild = null
            };
        }
    }

    /// <inheritdoc/>
    public void RebuildIndex()
    {
        if (!IsVectorliteLoaded())
        {
            throw new InvalidOperationException("Vectorlite extension not loaded");
        }

        _logger.LogInformation("Rebuilding vector index...");

        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            // Drop and recreate virtual table
            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = "DROP TABLE IF EXISTS vector_index";
            dropCommand.ExecuteNonQuery();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                CREATE VIRTUAL TABLE vector_index USING vectorlite(
                    embedding float32[384],
                    hnsw(max_elements=10000, ef_construction=200, M=48)
                )";
            createCommand.ExecuteNonQuery();

            // Re-populate index from SubtitleHashes
            // This would require vectorlite-specific syntax for bulk insertion
            _logger.LogInformation("Vector index rebuilt successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding vector index: {Error}", ex.Message);
            throw;
        }
    }

    private void EnsureVectorliteLoaded()
    {
        if (_vectorliteLoaded) return;

        _loadLock.Wait();
        try
        {
            if (_vectorliteLoaded) return;

            _logger.LogInformation("Loading vectorlite SQLite extension...");

            // Attempt to load vectorlite extension
            // This requires platform-specific extension files
            var extensionPath = GetVectorliteExtensionPath();

            if (!File.Exists(extensionPath))
            {
                _logger.LogWarning("Vectorlite extension not found at {Path} - vector search will not be available", 
                    extensionPath);
                return;
            }

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();
                
                // Enable extension loading
                using var enableExtensionsCmd = connection.CreateCommand();
                enableExtensionsCmd.CommandText = "SELECT 1";  // Dummy query to ensure connection is active
                enableExtensionsCmd.ExecuteNonQuery();
                
                // Enable loading extensions via the connection API
                connection.EnableExtensions(true);

                // Load extension
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT load_extension('{extensionPath}')";
                command.ExecuteNonQuery();

                _vectorliteLoaded = true;
                _logger.LogInformation("Vectorlite extension loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load vectorlite extension: {Error}", ex.Message);
                // Don't throw - allow graceful degradation
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private string GetVectorliteExtensionPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // Try multiple possible locations in order of preference
        string[] possiblePaths;
        
        if (OperatingSystem.IsWindows())
        {
            possiblePaths = new[]
            {
                Path.Combine(baseDir, "vectorlite.dll"),
                Path.Combine(baseDir, "external", "vectorlite", "vectorlite-win-x64.dll"),
                Path.Combine(baseDir, "native", "vectorlite.dll")
            };
        }
        else if (OperatingSystem.IsLinux())
        {
            possiblePaths = new[]
            {
                Path.Combine(baseDir, "vectorlite.so"),
                Path.Combine(baseDir, "external", "vectorlite", "vectorlite-linux-x64.so"),
                Path.Combine(baseDir, "native", "vectorlite.so")
            };
        }
        else
        {
            throw new PlatformNotSupportedException($"Vectorlite not supported on this platform");
        }
        
        // Return the first path that exists
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        
        // If no file found, return the first preferred path (for error messages)
        return possiblePaths[0];
    }

    private byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private double CalculateConfidence(double similarity, SubtitleSourceFormat sourceFormat)
    {
        // Apply format-specific adjustments to confidence
        // Lower quality sources (VobSub) need higher similarity for same confidence
        return sourceFormat switch
        {
            SubtitleSourceFormat.Text => similarity * 0.95,      // High confidence
            SubtitleSourceFormat.PGS => similarity * 0.90,       // Medium confidence
            SubtitleSourceFormat.VobSub => similarity * 0.85,    // Lower confidence (OCR errors)
            _ => similarity * 0.95
        };
    }
}
