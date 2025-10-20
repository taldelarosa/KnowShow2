using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for ML embedding-based subtitle matching.
/// Tests the complete flow from subtitle extraction through embedding generation to matching.
/// </summary>
public class EmbeddingMatchingIntegrationTests : IDisposable
{
    private readonly string _testDatabasePath;
    private readonly ILogger<ModelManager> _modelManagerLogger;
    private readonly ILogger<EmbeddingService> _embeddingLogger;
    private readonly ILogger<VectorSearchService> _vectorSearchLogger;

    public EmbeddingMatchingIntegrationTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_embedding_{Guid.NewGuid()}.db");

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _modelManagerLogger = loggerFactory.CreateLogger<ModelManager>();
        _embeddingLogger = loggerFactory.CreateLogger<EmbeddingService>();
        _vectorSearchLogger = loggerFactory.CreateLogger<VectorSearchService>();
    }

    [Fact(Skip = "Integration test - requires ONNX model download (~45MB) and vectorlite extension")]
    public async Task VobSubOcrMatching_AchievesHighSimilarity_WithTextSubtitles()
    {
        // Arrange: Simulate VobSub OCR output (lower quality) vs clean text subtitle
        var textSubtitle = @"You're under arrest for the murder of Jennifer Jareau.
            What? I didn't kill anyone!
            We have evidence linking you to the crime scene.";

        var vobSubOcrOutput = @"Youre under arrest for te murder of Jennifer Jareau.
            Wht? I didnt kill anyone!
            We hav evidence linking you to th crime scene.";  // Typical OCR errors

        // Create embedding service
        var modelManager = new ModelManager(_modelManagerLogger);
        await modelManager.EnsureModelAvailable();

        using var embeddingService = new EmbeddingService(_embeddingLogger, modelManager);

        // Act: Generate embeddings
        var textEmbedding = embeddingService.GenerateEmbedding(textSubtitle);
        var vobSubEmbedding = embeddingService.GenerateEmbedding(vobSubOcrOutput);

        var textEmbeddingModel = new SubtitleEmbedding(textEmbedding, textSubtitle);
        var vobSubEmbeddingModel = new SubtitleEmbedding(vobSubEmbedding, vobSubOcrOutput);

        // Calculate similarity
        var similarity = SubtitleEmbedding.CosineSimilarity(textEmbedding, vobSubEmbedding);

        // Assert: Should achieve >85% similarity despite OCR errors
        Assert.True(similarity > 0.85, $"Expected similarity >0.85, got {similarity:F4}");
    }

    [Fact(Skip = "Integration test - requires vectorlite extension and database setup")]
    public void VectorSearch_FindsSimilarSubtitles_WithCorrectRanking()
    {
        // Arrange: Setup test database with sample embeddings
        InitializeTestDatabase();

        var vectorSearchService = new VectorSearchService(_vectorSearchLogger, _testDatabasePath);

        // Create a query embedding (in real scenario, this would be from OCR subtitle)
        var queryEmbedding = CreateTestEmbedding();

        // Act: Search for similar subtitles
        var results = vectorSearchService.SearchBySimilarity(
            queryEmbedding,
            topK: 5,
            minSimilarity: 0.70);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 5);

        // Results should be ordered by similarity descending
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Similarity >= results[i].Similarity);
            Assert.Equal(i, results[i - 1].Rank);
        }

        // All results should meet minimum similarity
        Assert.All(results, r => Assert.True(r.Similarity >= 0.70));
    }

    [Fact(Skip = "Integration test - requires database and configuration")]
    public async Task DatabaseMigration_CompletesQuickly_ForSmallDataset()
    {
        // Arrange: Create test database with 5 entries
        InitializeTestDatabase();
        var sampleSubtitles = CreateSampleSubtitles(5);

        var modelManager = new ModelManager(_modelManagerLogger);
        await modelManager.EnsureModelAvailable();

        using var embeddingService = new EmbeddingService(_embeddingLogger, modelManager);

        // Act: Generate embeddings for all entries
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var embeddings = embeddingService.BatchGenerateEmbeddings(sampleSubtitles);
        stopwatch.Stop();

        // Assert: Should complete in <30 seconds for 5 entries
        Assert.Equal(5, embeddings.Count);
        Assert.True(stopwatch.Elapsed.TotalSeconds < 30,
            $"Migration took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <30s)");
    }

    [Fact(Skip = "Integration test - requires configuration file")]
    public void ThresholdConfiguration_LoadsCorrectly_ForEachFormat()
    {
        // Arrange: Create test configuration
        var config = new EmbeddingMatchThresholds
        {
            TextBased = new EmbeddingMatchThresholds.FormatThreshold
            {
                EmbedSimilarity = 0.85,
                MatchConfidence = 0.70,
                RenameConfidence = 0.80
            },
            Pgs = new EmbeddingMatchThresholds.FormatThreshold
            {
                EmbedSimilarity = 0.80,
                MatchConfidence = 0.60,
                RenameConfidence = 0.70
            },
            VobSub = new EmbeddingMatchThresholds.FormatThreshold
            {
                EmbedSimilarity = 0.75,
                MatchConfidence = 0.50,
                RenameConfidence = 0.60
            }
        };

        // Act & Assert: Validate configuration
        Assert.True(config.TextBased.IsValid(out var textError), $"TextBased should be valid: {textError}");
        Assert.True(config.Pgs.IsValid(out var pgsError), $"PGS should be valid: {pgsError}");
        Assert.True(config.VobSub.IsValid(out var vobSubError), $"VobSub should be valid: {vobSubError}");

        // Verify thresholds are in logical order
        Assert.True(config.TextBased.EmbedSimilarity > config.Pgs.EmbedSimilarity);
        Assert.True(config.Pgs.EmbedSimilarity > config.VobSub.EmbedSimilarity);

        // Verify each format has rename > match > embed thresholds
        Assert.True(config.TextBased.RenameConfidence > config.TextBased.MatchConfidence);
        Assert.True(config.Pgs.RenameConfidence > config.Pgs.MatchConfidence);
        Assert.True(config.VobSub.RenameConfidence > config.VobSub.MatchConfidence);
    }

    [Fact(Skip = "Integration test - requires full CLI setup")]
    public async Task CliIdentifyCommand_WorksWithEmbeddingMatching_WhenEnabled()
    {
        // This test would execute the full CLI command with embedding matching enabled
        // Example: episodeidentifier --identify test.mkv --matching-strategy embedding

        // This will be implemented when CLI integration is complete (T025)
        await Task.CompletedTask;
    }

    private void InitializeTestDatabase()
    {
        // Create SQLite database with SubtitleHashes table
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_testDatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS SubtitleHashes (
                Id INTEGER PRIMARY KEY,
                Series TEXT NOT NULL,
                Season TEXT NOT NULL,
                Episode TEXT NOT NULL,
                EpisodeName TEXT,
                CleanText TEXT NOT NULL,
                CtphHash TEXT,
                Embedding BLOB,
                SubtitleSourceFormat TEXT DEFAULT 'Text'
            )";
        command.ExecuteNonQuery();
    }

    private float[] CreateTestEmbedding()
    {
        // Create a dummy 384-dimensional embedding for testing
        var embedding = new float[384];
        var random = new Random(42); // Seeded for reproducibility

        for (int i = 0; i < 384; i++)
        {
            embedding[i] = (float)(random.NextDouble() - 0.5);
        }

        // Normalize to unit length
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < 384; i++)
        {
            embedding[i] /= (float)magnitude;
        }

        return embedding;
    }

    private List<string> CreateSampleSubtitles(int count)
    {
        return Enumerable.Range(1, count).Select(i =>
            $"This is sample subtitle number {i} for testing embedding generation."
        ).ToList();
    }

    public void Dispose()
    {
        // Cleanup test database
        if (File.Exists(_testDatabasePath))
        {
            try
            {
                File.Delete(_testDatabasePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
