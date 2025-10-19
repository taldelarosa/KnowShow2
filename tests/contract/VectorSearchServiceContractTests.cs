using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IVectorSearchService interface.
/// These tests define the expected behavior of any IVectorSearchService implementation.
/// Tests are marked as Skip until implementation exists (TDD RED phase).
/// </summary>
public class VectorSearchServiceContractTests
{
    private IVectorSearchService CreateVectorSearchService()
    {
        // TODO: Replace with actual implementation once VectorSearchService exists
        throw new NotImplementedException("VectorSearchService not yet implemented - this is expected in TDD RED phase");
    }

    private float[] CreateTestEmbedding()
    {
        // Create a dummy 384-dimensional embedding for testing
        var embedding = new float[384];
        var random = new Random(42); // Deterministic for reproducibility
        for (int i = 0; i < 384; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range [-1, 1]
        }
        return embedding;
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_WithValidEmbedding_ReturnsTopResults()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var queryEmbedding = CreateTestEmbedding();
        var topK = 10;

        // Act
        var results = searchService.SearchBySimilarity(queryEmbedding, topK);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCountLessOrEqualTo(topK, "should not exceed requested top K");
        results.Should().BeInDescendingOrder(r => r.Similarity, 
            "results should be ordered by similarity (highest first)");
        
        foreach (var result in results)
        {
            result.Series.Should().NotBeNullOrEmpty();
            result.Season.Should().NotBeNullOrEmpty();
            result.Episode.Should().NotBeNullOrEmpty();
            result.Similarity.Should().BeInRange(0.0, 1.0);
            result.Confidence.Should().BeInRange(0.0, 1.0);
            result.Distance.Should().BeGreaterOrEqualTo(0.0);
            result.Rank.Should().BeGreaterThan(0);
        }
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_WithMinSimilarityFilter_FiltersResults()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var queryEmbedding = CreateTestEmbedding();
        var topK = 10;
        var minSimilarity = 0.75;

        // Act
        var results = searchService.SearchBySimilarity(queryEmbedding, topK, minSimilarity);

        // Assert
        results.Should().NotBeNull();
        results.Should().OnlyContain(r => r.Similarity >= minSimilarity,
            "all results should meet minimum similarity threshold");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var queryEmbedding = CreateTestEmbedding();

        // Act
        var results = searchService.SearchBySimilarity(queryEmbedding, topK: 10);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty("no results expected when database is empty");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_WithNullEmbedding_ThrowsArgumentNullException()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        float[]? queryEmbedding = null;

        // Act & Assert
        var act = () => searchService.SearchBySimilarity(queryEmbedding!, topK: 10);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("queryEmbedding");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_WithInvalidDimension_ThrowsArgumentException()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var invalidEmbedding = new float[256]; // Wrong dimension (should be 384)

        // Act & Assert
        var act = () => searchService.SearchBySimilarity(invalidEmbedding, topK: 10);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*384*dimension*");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_WithInvalidTopK_ThrowsArgumentException()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var queryEmbedding = CreateTestEmbedding();

        // Act & Assert - topK = 0
        var act1 = () => searchService.SearchBySimilarity(queryEmbedding, topK: 0);
        act1.Should().Throw<ArgumentException>()
            .WithMessage("*topK*greater*zero*");

        // Act & Assert - topK negative
        var act2 = () => searchService.SearchBySimilarity(queryEmbedding, topK: -5);
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*topK*greater*zero*");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void IsVectorliteLoaded_WhenExtensionLoaded_ReturnsTrue()
    {
        // Arrange
        var searchService = CreateVectorSearchService();

        // Act
        var isLoaded = searchService.IsVectorliteLoaded();

        // Assert
        isLoaded.Should().BeTrue("vectorlite extension should be loaded during service initialization");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void GetIndexStats_ReturnsValidStatistics()
    {
        // Arrange
        var searchService = CreateVectorSearchService();

        // Act
        var stats = searchService.GetIndexStats();

        // Assert
        stats.Should().NotBeNull();
        stats.Dimension.Should().Be(384, "index should use 384-dimensional embeddings");
        stats.TotalVectors.Should().BeGreaterOrEqualTo(0);
        stats.IndexSizeBytes.Should().BeGreaterOrEqualTo(0);
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void RebuildIndex_AfterDatabaseChanges_UpdatesIndex()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var statsBefore = searchService.GetIndexStats();

        // Act
        searchService.RebuildIndex();
        var statsAfter = searchService.GetIndexStats();

        // Assert
        statsAfter.Should().NotBeNull();
        statsAfter.LastRebuild.Should().BeAfter(statsBefore.LastRebuild ?? DateTime.MinValue,
            "rebuild timestamp should be updated");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_CompletesWithinPerformanceTarget()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var queryEmbedding = CreateTestEmbedding();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = searchService.SearchBySimilarity(queryEmbedding, topK: 10);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "search should complete within 2 seconds per contract (for ~1000 entries)");
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_WithMultipleCalls_ReturnsConsistentResults()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var queryEmbedding = CreateTestEmbedding();

        // Act
        var results1 = searchService.SearchBySimilarity(queryEmbedding, topK: 5);
        var results2 = searchService.SearchBySimilarity(queryEmbedding, topK: 5);

        // Assert
        results1.Should().HaveCount(results2.Count);
        for (int i = 0; i < results1.Count; i++)
        {
            results1[i].Id.Should().Be(results2[i].Id, 
                "same query should return same results in same order");
            results1[i].Similarity.Should().BeApproximately(results2[i].Similarity, 0.0001,
                "similarity scores should be consistent");
        }
    }

    [Fact(Skip = "Implementation not yet created - TDD RED phase")]
    public void SearchBySimilarity_ResultsIncludeAllMetadata()
    {
        // Arrange
        var searchService = CreateVectorSearchService();
        var queryEmbedding = CreateTestEmbedding();

        // Act
        var results = searchService.SearchBySimilarity(queryEmbedding, topK: 1);

        // Assert
        if (results.Any())
        {
            var result = results.First();
            result.Id.Should().BeGreaterThan(0);
            result.Series.Should().NotBeNullOrEmpty();
            result.Season.Should().NotBeNullOrEmpty();
            result.Episode.Should().NotBeNullOrEmpty();
            result.SourceFormat.Should().BeOneOf(
                SubtitleSourceFormat.Text, 
                SubtitleSourceFormat.PGS, 
                SubtitleSourceFormat.VobSub);
            result.Similarity.Should().BeInRange(0.0, 1.0);
            result.Confidence.Should().BeInRange(0.0, 1.0);
            result.Distance.Should().BeApproximately(1.0 - result.Similarity, 0.0001,
                "distance should be (1 - similarity) for cosine distance");
            result.Rank.Should().Be(1, "first result should have rank 1");
        }
    }
}
