using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models.Configuration;

namespace EpisodeIdentifier.Tests.Integration;

/// <summary>
/// Integration tests for TextRank-based filtering in the episode identification pipeline.
/// Tests the end-to-end integration with real subtitle processing.
/// </summary>
public class TextRankIntegrationTests
{
    /// <summary>
    /// T022: Integration Test - Verbose Matching
    /// Tests that TextRank filtering successfully processes verbose subtitles.
    /// </summary>
    [Fact]
    public void TextRankFiltering_VerboseSubtitle_ProcessesSuccessfully()
    {
        // Arrange - Create verbose subtitle text (simulating conversational filler + plot content)
        var verboseSubtitle = GenerateVerboseSubtitle(300); // 300 sentences with mixed content

        var textRankService = new TextRankService(NullLogger<TextRankService>.Instance);

        // Act - Process with TextRank
        var result = textRankService.ExtractPlotRelevantSentences(
            verboseSubtitle,
            sentencePercentage: 25,
            minSentences: 15,
            minPercentage: 10);

        // Assert - Should successfully filter without fallback
        Assert.NotNull(result);
        Assert.False(result.FallbackTriggered);
        Assert.Equal(300, result.TotalSentenceCount);
        Assert.InRange(result.SelectedSentenceCount, 70, 80); // ~75 sentences (25% of 300)
        Assert.InRange(result.SelectionPercentage, 23.0, 27.0); // ~25% ± 2%
        Assert.True(result.FilteredText.Length < verboseSubtitle.Length / 2); // Significantly shorter
        Assert.InRange(result.ProcessingTimeMs, 0, 3000); // Should complete within 3 seconds
    }

    /// <summary>
    /// T023: Integration Test - Fallback Validation
    /// Tests that TextRank correctly falls back to full text for short subtitles.
    /// </summary>
    [Fact]
    public void TextRankFiltering_ShortSubtitle_UsesFullTextFallback()
    {
        // Arrange - Create short subtitle (below minSentences threshold)
        var shortSubtitle = "This is sentence one. This is sentence two. Short content here."; // Only 3 sentences

        var textRankService = new TextRankService(NullLogger<TextRankService>.Instance);

        // Act - Test TextRank extraction with threshold of 15 sentences
        var result = textRankService.ExtractPlotRelevantSentences(
            shortSubtitle,
            sentencePercentage: 25,
            minSentences: 15, // Threshold is 15, but we only have 3
            minPercentage: 10);

        // Assert - Should trigger fallback
        Assert.True(result.FallbackTriggered);
        Assert.Contains("15 sentence", result.FallbackReason);
        Assert.Equal(3, result.TotalSentenceCount);
        Assert.Equal(3, result.SelectedSentenceCount); // All sentences retained
        Assert.Equal(100.0, result.SelectionPercentage);
        Assert.Equal(shortSubtitle.Trim(), result.FilteredText.Trim());
    }

    /// <summary>
    /// T024: Integration Test - Hot-Reload
    /// Tests that TextRank configuration changes are respected in subsequent operations.
    /// </summary>
    [Fact]
    public void TextRankFiltering_ConfigurationChange_AppliesNewSettings()
    {
        // Arrange
        var subtitle = GenerateVerboseSubtitle(100);
        var textRankService = new TextRankService(NullLogger<TextRankService>.Instance);

        // Act - First extraction with 25%
        var result25 = textRankService.ExtractPlotRelevantSentences(
            subtitle,
            sentencePercentage: 25,
            minSentences: 5,
            minPercentage: 5);

        // Act - Second extraction with 50%
        var result50 = textRankService.ExtractPlotRelevantSentences(
            subtitle,
            sentencePercentage: 50,
            minSentences: 5,
            minPercentage: 5);

        // Assert - Different percentages should select different amounts
        Assert.True(result25.SelectedSentenceCount < result50.SelectedSentenceCount,
            $"25% selection ({result25.SelectedSentenceCount}) should be less than 50% selection ({result50.SelectedSentenceCount})");
        
        Assert.InRange(result25.SelectionPercentage, 23.0, 27.0); // ~25% ± 2%
        Assert.InRange(result50.SelectionPercentage, 48.0, 52.0); // ~50% ± 2%
    }

    /// <summary>
    /// T025: Integration Test - Performance
    /// Tests that TextRank processing completes within acceptable time limits.
    /// </summary>
    [Fact]
    public void TextRankFiltering_LargeSubtitle_CompletesWithinTimeLimit()
    {
        // Arrange - Create large subtitle (1000 sentences)
        var largeSubtitle = GenerateVerboseSubtitle(1000);
        var textRankService = new TextRankService(NullLogger<TextRankService>.Instance);

        // Act
        var result = textRankService.ExtractPlotRelevantSentences(
            largeSubtitle,
            sentencePercentage: 25,
            minSentences: 15,
            minPercentage: 10);

        // Assert - Performance
        Assert.InRange(result.ProcessingTimeMs, 0, 8000); // Should complete within 8 seconds for 1000 sentences
        
        // Assert - Correctness
        Assert.Equal(1000, result.TotalSentenceCount);
        Assert.InRange(result.SelectedSentenceCount, 240, 260); // ~250 ± 10
        Assert.False(result.FallbackTriggered);
    }

    /// <summary>
    /// T026: Integration Test - Backward Compatibility
    /// Tests that disabling TextRank has no effect on processing.
    /// </summary>
    [Fact]
    public void TextRankFiltering_NullConfiguration_HandlesGracefully()
    {
        // Arrange
        var subtitle = GenerateVerboseSubtitle(100);
        var textRankService = new TextRankService(NullLogger<TextRankService>.Instance);

        // Act - Process without any filtering (simulating disabled TextRank)
        var result = textRankService.ExtractPlotRelevantSentences(
            subtitle,
            sentencePercentage: 100, // Select all sentences (no filtering)
            minSentences: 0,
            minPercentage: 0);

        // Assert - Should work normally, selecting all sentences
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalSentenceCount);
        Assert.Equal(100, result.SelectedSentenceCount);
        Assert.Equal(100.0, result.SelectionPercentage);
        Assert.False(result.FallbackTriggered);
    }

    /// <summary>
    /// Helper: Generates verbose subtitle text with mixed plot and filler content.
    /// </summary>
    private string GenerateVerboseSubtitle(int sentenceCount, int seed = 123)
    {
        var random = new Random(seed);
        var plotSentences = new[]
        {
            "The detective investigated the crime scene thoroughly.",
            "Evidence was collected from multiple locations.",
            "The suspect's alibi didn't hold up under scrutiny.",
            "Forensic analysis revealed crucial DNA evidence.",
            "The investigation led to an unexpected breakthrough.",
            "Witnesses provided conflicting testimonies."
        };
        var fillerSentences = new[]
        {
            "How are you doing today?",
            "I'm fine, thanks for asking.",
            "What would you like for dinner?",
            "The weather is nice this morning.",
            "Did you see that movie last night?",
            "I need to go shopping later."
        };

        var sentences = new System.Collections.Generic.List<string>();
        for (int i = 0; i < sentenceCount; i++)
        {
            var isPlot = random.Next(100) < 70; // 70% plot, 30% filler
            var sourceArray = isPlot ? plotSentences : fillerSentences;
            sentences.Add(sourceArray[random.Next(sourceArray.Length)]);
        }

        return string.Join(" ", sentences);
    }
}
