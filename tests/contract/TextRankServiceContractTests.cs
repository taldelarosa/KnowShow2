using Xunit;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for ITextRankService interface.
/// These tests define the expected behavior of TextRank-based sentence extraction.
/// Tests MUST FAIL during RED phase before implementation.
/// </summary>
public class TextRankServiceContractTests
{
    private readonly ILogger<TextRankService> _logger;

    public TextRankServiceContractTests()
    {
        _logger = NullLogger<TextRankService>.Instance;
    }

    /// <summary>
    /// Contract Test 1: Verbose Subtitle Extraction
    /// Given a verbose subtitle text with 600 sentences
    /// When extracting plot-relevant sentences with 25% selection
    /// Then should return ~150 sentences (25% of 600)
    /// And filtered text should be significantly shorter than original
    /// And should maintain chronological order
    /// </summary>
    [Fact]
    public void ExtractPlotRelevantSentences_VerboseSubtitle_SelectsTop25Percent()
    {
        // Arrange
        var service = new TextRankService(_logger);
        var verboseText = GenerateVerboseSubtitleText(600);
        int sentencePercentage = 25;
        int minSentences = 15;
        int minPercentage = 10;

        // Act
        var result = service.ExtractPlotRelevantSentences(
            verboseText,
            sentencePercentage,
            minSentences,
            minPercentage);

        // Assert - Basic structure
        Assert.NotNull(result);
        Assert.NotNull(result.FilteredText);
        Assert.NotEmpty(result.FilteredText);

        // Assert - Sentence counts
        Assert.Equal(600, result.TotalSentenceCount);
        Assert.InRange(result.SelectedSentenceCount, 140, 160); // ~150 ± 10 for tolerance
        Assert.InRange(result.SelectionPercentage, 23.0, 27.0); // ~25% ± 2% for tolerance

        // Assert - Filtering occurred
        Assert.False(result.FallbackTriggered);
        Assert.Null(result.FallbackReason);
        Assert.True(result.FilteredText.Length < verboseText.Length / 2); // Should be significantly shorter

        // Assert - Performance
        Assert.InRange(result.ProcessingTimeMs, 0, 2000); // Should complete in <2s for 600 sentences

        // Assert - Quality metrics
        Assert.InRange(result.AverageScore, 0.0, 1.0); // Valid score range
        Assert.True(result.AverageScore > 0.0); // Should have meaningful scores
    }

    /// <summary>
    /// Contract Test 2: Insufficient Sentence Fallback
    /// Given subtitle text with only 10 sentences (below 15 sentence threshold)
    /// When extracting plot-relevant sentences
    /// Then should trigger fallback and return full text
    /// And set FallbackTriggered = true with appropriate reason
    /// </summary>
    [Fact]
    public void ExtractPlotRelevantSentences_InsufficientSentences_TriggersAbsoluteFallback()
    {
        // Arrange
        var service = new TextRankService(_logger);
        var shortText = GenerateVerboseSubtitleText(10);
        int sentencePercentage = 25;
        int minSentences = 15;
        int minPercentage = 10;

        // Act
        var result = service.ExtractPlotRelevantSentences(
            shortText,
            sentencePercentage,
            minSentences,
            minPercentage);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.TotalSentenceCount);
        Assert.Equal(10, result.SelectedSentenceCount); // All sentences returned
        Assert.Equal(100.0, result.SelectionPercentage); // 100% retained
        Assert.True(result.FallbackTriggered);
        Assert.Contains("15 sentence", result.FallbackReason); // Mentions threshold
        Assert.Equal(shortText.Trim(), result.FilteredText.Trim()); // Full text returned
    }

    /// <summary>
    /// Contract Test 3: Low Percentage Fallback
    /// Given subtitle text where 25% selection would result in less than 10% retention
    /// When extracting plot-relevant sentences
    /// Then should trigger percentage-based fallback and return full text
    /// </summary>
    [Fact]
    public void ExtractPlotRelevantSentences_LowPercentageSelection_TriggersPercentageFallback()
    {
        // Arrange
        var service = new TextRankService(_logger);
        var text = GenerateVerboseSubtitleText(100);
        int sentencePercentage = 25; // Would select 25 sentences
        int minSentences = 5; // Allow filtering (threshold satisfied)
        int minPercentage = 30; // Require at least 30% retention (25% < 30%, triggers fallback)

        // Act
        var result = service.ExtractPlotRelevantSentences(
            text,
            sentencePercentage,
            minSentences,
            minPercentage);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalSentenceCount);
        Assert.Equal(100, result.SelectedSentenceCount); // All sentences returned
        Assert.Equal(100.0, result.SelectionPercentage);
        Assert.True(result.FallbackTriggered);
        Assert.Contains("30%", result.FallbackReason); // Mentions percentage threshold
        Assert.Equal(text.Trim(), result.FilteredText.Trim());
    }

    /// <summary>
    /// Contract Test 4: Single-Sentence Handling
    /// Given subtitle text with only one sentence
    /// When extracting plot-relevant sentences
    /// Then should trigger fallback (no meaningful extraction possible)
    /// </summary>
    [Fact]
    public void ExtractPlotRelevantSentences_SingleSentence_TriggersFallback()
    {
        // Arrange
        var service = new TextRankService(_logger);
        var singleSentence = "This is a single sentence subtitle.";
        int sentencePercentage = 25;
        int minSentences = 15;
        int minPercentage = 10;

        // Act
        var result = service.ExtractPlotRelevantSentences(
            singleSentence,
            sentencePercentage,
            minSentences,
            minPercentage);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalSentenceCount);
        Assert.Equal(1, result.SelectedSentenceCount);
        Assert.Equal(100.0, result.SelectionPercentage);
        Assert.True(result.FallbackTriggered);
        Assert.NotNull(result.FallbackReason);
        Assert.Equal(singleSentence.Trim(), result.FilteredText.Trim());
    }

    /// <summary>
    /// Contract Test 5: Score Calculation Validation
    /// Given an array of sentences with varying content
    /// When calculating TextRank scores
    /// Then should return scores for all sentences
    /// And scores should be in valid range (0.0-1.0)
    /// And different sentences should have different scores (not all identical)
    /// </summary>
    [Fact]
    public void CalculateTextRankScores_VariedSentences_ReturnsValidScores()
    {
        // Arrange
        var service = new TextRankService(_logger);
        var sentences = new[]
        {
            "The detective investigated the crime scene carefully.",
            "He found a bloody knife under the table.",
            "The suspect claimed to be innocent.",
            "What would you like for dinner tonight?",
            "The weather is nice today.",
            "Evidence pointed to the butler as the murderer.",
            "The investigation continued for weeks.",
            "I need to buy groceries later.",
            "The trial began on Monday morning.",
            "The jury deliberated for hours."
        };

        // Act
        var scores = service.CalculateTextRankScores(sentences);

        // Assert - Basic structure
        Assert.NotNull(scores);
        Assert.Equal(sentences.Length, scores.Count);

        // Assert - All indices present
        for (int i = 0; i < sentences.Length; i++)
        {
            Assert.True(scores.ContainsKey(i), $"Missing score for sentence index {i}");
        }

        // Assert - Valid score range
        foreach (var score in scores.Values)
        {
            Assert.InRange(score, 0.0, 1.0);
        }

        // Assert - Score variance (not all identical)
        var uniqueScores = scores.Values.Distinct().Count();
        Assert.True(uniqueScores > 1, "All scores are identical - ranking failed");

        // Assert - Related sentences should have higher scores
        // Sentences 0, 1, 2, 5, 6, 8, 9 are about investigation/crime (should have higher average score)
        // Sentences 3, 4, 7 are unrelated filler (should have lower average score)
        var investigationIndices = new[] { 0, 1, 2, 5, 6, 8, 9 };
        var fillerIndices = new[] { 3, 4, 7 };

        var avgInvestigationScore = investigationIndices.Average(i => scores[i]);
        var avgFillerScore = fillerIndices.Average(i => scores[i]);

        Assert.True(avgInvestigationScore > avgFillerScore,
            $"Investigation sentences (avg={avgInvestigationScore:F3}) should score higher than filler (avg={avgFillerScore:F3})");
    }

    /// <summary>
    /// Generates verbose subtitle text with specified number of sentences.
    /// Simulates realistic subtitle content with mix of plot-relevant and filler dialogue.
    /// </summary>
    private static string GenerateVerboseSubtitleText(int sentenceCount)
    {
        var sentences = new List<string>();
        var plotSentences = new[]
        {
            "The suspect was seen entering the building at midnight.",
            "Detective Martinez discovered crucial evidence at the scene.",
            "The victim had been dead for approximately three hours.",
            "Forensic analysis revealed traces of poison in the glass.",
            "The killer left no fingerprints behind.",
            "Security footage showed a figure in a dark coat."
        };
        var fillerSentences = new[]
        {
            "How are you doing today?",
            "I'm fine, thanks for asking.",
            "What would you like for dinner?",
            "The weather is nice this morning.",
            "Did you see that movie last night?",
            "I need to go to the store later."
        };

        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 0; i < sentenceCount; i++)
        {
            // Mix 70% plot-relevant, 30% filler
            var isPlot = random.Next(100) < 70;
            var sourceArray = isPlot ? plotSentences : fillerSentences;
            sentences.Add(sourceArray[random.Next(sourceArray.Length)]);
        }

        return string.Join(" ", sentences);
    }

    /// <summary>
    /// Contract Test 6: Empty Input Handling
    /// Given empty or null subtitle text
    /// When extracting plot-relevant sentences
    /// Then should handle gracefully without throwing exceptions
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractPlotRelevantSentences_EmptyInput_HandlesGracefully(string? input)
    {
        // Arrange
        var service = new TextRankService(_logger);
        int sentencePercentage = 25;
        int minSentences = 15;
        int minPercentage = 10;

        // Act
        var result = service.ExtractPlotRelevantSentences(
            input ?? string.Empty,
            sentencePercentage,
            minSentences,
            minPercentage);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.FallbackTriggered);
        Assert.Equal(0, result.TotalSentenceCount);
        Assert.Equal(0, result.SelectedSentenceCount);
    }

    /// <summary>
    /// Contract Test 7: Score Calculation with Empty Input
    /// Given empty sentence array
    /// When calculating TextRank scores
    /// Then should return empty dictionary without throwing
    /// </summary>
    [Fact]
    public void CalculateTextRankScores_EmptyInput_ReturnsEmptyDictionary()
    {
        // Arrange
        var service = new TextRankService(_logger);
        var emptySentences = Array.Empty<string>();

        // Act
        var scores = service.CalculateTextRankScores(emptySentences);

        // Assert
        Assert.NotNull(scores);
        Assert.Empty(scores);
    }
}
