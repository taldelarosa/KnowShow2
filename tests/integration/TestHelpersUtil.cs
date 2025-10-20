using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Services;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Helper methods for integration tests
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Load configuration from episodeidentifier.config.json
    /// </summary>
    public static Configuration LoadConfiguration()
    {
        var configPath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "../../../../episodeidentifier.config.json");
        
        if (!File.Exists(configPath))
        {
            configPath = Path.Combine(
                Directory.GetCurrentDirectory(), 
                "../../../episodeidentifier.config.json");
        }
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found at {configPath}");
        }
        
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<Configuration>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });
        
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize configuration");
        }
        
        return config;
    }

    /// <summary>
    /// Create a test database with sample embeddings for performance testing
    /// </summary>
    public static void CreateTestDatabase(string dbPath, int entryCount)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // Create SubtitleHashes table
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS SubtitleHashes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShowTitle TEXT NOT NULL,
                    Season INTEGER NOT NULL,
                    Episode INTEGER NOT NULL,
                    CleanSubtitleText TEXT NOT NULL,
                    CtphHash TEXT NOT NULL,
                    Embedding BLOB,
                    SubtitleFormat INTEGER NOT NULL,
                    ImportedDate TEXT NOT NULL
                )";
            cmd.ExecuteNonQuery();
        }

        // Insert sample entries with random embeddings
        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 0; i < entryCount; i++)
        {
            // Generate 384-dimensional random embedding (L2-normalized)
            var embedding = new float[384];
            var sumSquares = 0.0;
            for (int j = 0; j < 384; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1); // Range: [-1, 1]
                sumSquares += embedding[j] * embedding[j];
            }
            
            // Normalize to unit length
            var magnitude = Math.Sqrt(sumSquares);
            for (int j = 0; j < 384; j++)
            {
                embedding[j] /= (float)magnitude;
            }

            // Convert to byte array
            var embeddingBytes = new byte[384 * sizeof(float)];
            Buffer.BlockCopy(embedding, 0, embeddingBytes, 0, embeddingBytes.Length);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO SubtitleHashes 
                (ShowTitle, Season, Episode, CleanSubtitleText, CtphHash, Embedding, SubtitleFormat, ImportedDate)
                VALUES (@showTitle, @season, @episode, @text, @hash, @embedding, @format, @date)";
            
            cmd.Parameters.AddWithValue("@showTitle", $"TestShow{i % 10}");
            cmd.Parameters.AddWithValue("@season", (i % 20) + 1);
            cmd.Parameters.AddWithValue("@episode", (i % 25) + 1);
            cmd.Parameters.AddWithValue("@text", $"Test subtitle text entry {i}");
            cmd.Parameters.AddWithValue("@hash", $"test_hash_{i}");
            cmd.Parameters.AddWithValue("@embedding", embeddingBytes);
            cmd.Parameters.AddWithValue("@format", 0); // Text
            cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
            
            cmd.ExecuteNonQuery();
        }
    }
}
