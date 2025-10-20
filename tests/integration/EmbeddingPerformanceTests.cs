using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using System.Diagnostics;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Performance tests for embedding generation and vector search operations.
/// Validates that operations meet target performance metrics:
/// - Embedding generation: <5s per subtitle
/// - Vector search: <2s per query
/// </summary>
public class EmbeddingPerformanceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly ILogger<EmbeddingService> _embeddingLogger;
    private readonly ILogger<ModelManager> _modelLogger;
    private readonly ILogger<VectorSearchService> _vectorLogger;

    public EmbeddingPerformanceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"perf_test_{Guid.NewGuid()}.db");

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _embeddingLogger = loggerFactory.CreateLogger<EmbeddingService>();
        _modelLogger = loggerFactory.CreateLogger<ModelManager>();
        _vectorLogger = loggerFactory.CreateLogger<VectorSearchService>();
    }

    [Fact(Skip = "Performance test - requires model download (~90MB), run manually")]
    public void GenerateEmbedding_WithRealSubtitle_CompletesWithin5Seconds()
    {
        // Arrange: Criminal Minds S06E19 VobSub OCR sample (typical length)
        var subtitleText = @"Previously on Criminal Minds...
We're looking for someone who's trying to recreate his childhood.
He's collecting blonde women as replacements for the mother he lost.
The UnSub is Brian Matloff, white male, 40s.
He was institutionalized as a child.
His mother died when he was 10.
We believe he's holding the victims in some kind of industrial space.
We need to find them before he kills again.";

        var config = TestHelpers.LoadConfiguration();
        var modelManager = new ModelManager(_modelLogger, config.EmbeddingModel);
        var embeddingService = new EmbeddingService(_embeddingLogger, modelManager);

        // Act: Measure embedding generation time
        var stopwatch = Stopwatch.StartNew();
        var embedding = embeddingService.GenerateEmbedding(subtitleText);
        stopwatch.Stop();

        // Assert: Performance meets target
        Assert.True(stopwatch.ElapsedMilliseconds < 5000,
            $"Embedding generation took {stopwatch.ElapsedMilliseconds}ms, expected <5000ms");

        // Validate embedding properties
        Assert.Equal(384, embedding.Length);
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        Assert.InRange(magnitude, 0.95f, 1.05f); // L2-normalized
    }

    [Fact(Skip = "Performance test - requires model download and database setup, run manually")]
    public void BatchGenerateEmbeddings_With100Entries_AveragesUnder5SecondsEach()
    {
        // Arrange: 100 subtitle samples
        var subtitles = Enumerable.Range(1, 100)
            .Select(i => $"Test subtitle {i}: This is sample dialog for performance testing.")
            .ToList();

        var config = TestHelpers.LoadConfiguration();
        var modelManager = new ModelManager(_modelLogger, config.EmbeddingModel);
        var embeddingService = new EmbeddingService(_embeddingLogger, modelManager);

        // Act: Measure batch processing time
        var stopwatch = Stopwatch.StartNew();
        var embeddings = embeddingService.BatchGenerateEmbeddings(subtitles);
        stopwatch.Stop();

        // Assert: Average time per embedding meets target
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)embeddings.Count;
        Assert.True(avgTimeMs < 5000,
            $"Average embedding time was {avgTimeMs:F1}ms, expected <5000ms");

        Assert.Equal(100, embeddings.Count);
        Assert.All(embeddings, emb => Assert.Equal(384, emb.Length));
    }

    [Fact(Skip = "Performance test - requires vectorlite and populated database, run manually")]
    public void VectorSearch_With834Entries_CompletesWithin2Seconds()
    {
        // Arrange: Create test database with 834 entries (matching bones.db)
        TestHelpers.CreateTestDatabase(_testDbPath, entryCount: 834);

        var config = TestHelpers.LoadConfiguration();
        var modelManager = new ModelManager(_modelLogger, config.EmbeddingModel);
        var embeddingService = new EmbeddingService(_embeddingLogger, modelManager);
        var vectorSearchService = new VectorSearchService(_vectorLogger, _testDbPath);

        // Generate query embedding
        var queryText = "Previously on Criminal Minds...";
        var queryEmbedding = embeddingService.GenerateEmbedding(queryText);

        // Act: Measure vector search time
        var stopwatch = Stopwatch.StartNew();
        var results = vectorSearchService.SearchBySimilarity(
            queryEmbedding,
            topK: 10,
            minSimilarity: 0.5f);
        stopwatch.Stop();

        // Assert: Search time meets target
        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"Vector search took {stopwatch.ElapsedMilliseconds}ms, expected <2000ms");

        Assert.NotEmpty(results);
        Assert.True(results.Count <= 10);
        Assert.All(results, r => Assert.InRange(r.Similarity, 0.5f, 1.0f));
    }

    [Fact(Skip = "Performance test - end-to-end benchmark, run manually")]
    public void EndToEnd_EmbeddingPlusSearch_CompletesWithin7Seconds()
    {
        // Arrange: Test complete workflow (embedding + search)
        TestHelpers.CreateTestDatabase(_testDbPath, entryCount: 834);

        var config = TestHelpers.LoadConfiguration();
        var modelManager = new ModelManager(_modelLogger, config.EmbeddingModel);
        var embeddingService = new EmbeddingService(_embeddingLogger, modelManager);
        var vectorSearchService = new VectorSearchService(_vectorLogger, _testDbPath);

        var queryText = "Previously on Criminal Minds...";

        // Act: Measure total time
        var stopwatch = Stopwatch.StartNew();
        var queryEmbedding = embeddingService.GenerateEmbedding(queryText);
        var results = vectorSearchService.SearchBySimilarity(queryEmbedding, topK: 10, minSimilarity: 0.5f);
        stopwatch.Stop();

        // Assert: Total time meets target (5s embedding + 2s search = 7s)
        Assert.True(stopwatch.ElapsedMilliseconds < 7000,
            $"End-to-end took {stopwatch.ElapsedMilliseconds}ms, expected <7000ms");

        Assert.NotEmpty(results);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }
}
