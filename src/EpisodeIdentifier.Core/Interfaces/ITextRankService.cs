using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Service interface for TextRank-based plot-relevant sentence extraction.
/// Implements graph-based ranking to filter conversational filler from subtitle text.
/// </summary>
public interface ITextRankService
{
    /// <summary>
    /// Extracts plot-relevant sentences from subtitle text using TextRank algorithm.
    /// Applies PageRank-based scoring to identify the most important sentences,
    /// then returns the top-ranked sentences maintaining chronological order.
    /// </summary>
    /// <param name="subtitleText">Full subtitle text to process.</param>
    /// <param name="sentencePercentage">Percentage of sentences to select (10-50).</param>
    /// <param name="minSentences">Absolute minimum sentence threshold for filtering (5-100).</param>
    /// <param name="minPercentage">Minimum percentage of original text to retain (5-50).</param>
    /// <returns>
    /// TextRankExtractionResult containing filtered text and statistics.
    /// If fallback is triggered (insufficient sentences or percentage), returns full text with fallback flag set.
    /// </returns>
    /// <remarks>
    /// Fallback conditions (returns full text):
    /// - Total sentences &lt; minSentences (absolute threshold)
    /// - Selected sentences &lt; (totalSentences * minPercentage / 100) (percentage threshold)
    /// - Single sentence input (no meaningful extraction possible)
    /// 
    /// Processing steps:
    /// 1. Segment text into sentences
    /// 2. Build similarity graph using bag-of-words cosine similarity
    /// 3. Apply PageRank algorithm to score sentences
    /// 4. Select top sentencePercentage% of sentences by score
    /// 5. Check fallback conditions
    /// 6. Return filtered text maintaining original order
    /// </remarks>
    TextRankExtractionResult ExtractPlotRelevantSentences(
        string subtitleText,
        int sentencePercentage,
        int minSentences,
        int minPercentage);

    /// <summary>
    /// Calculates TextRank scores for a collection of sentences using PageRank algorithm.
    /// Used internally by ExtractPlotRelevantSentences but exposed for testing and validation.
    /// </summary>
    /// <param name="sentences">Array of sentence texts to score.</param>
    /// <returns>
    /// Dictionary mapping sentence index to TextRank score (0.0-1.0).
    /// Higher scores indicate more central/plot-relevant sentences.
    /// Returns empty dictionary if input is null or empty.
    /// </returns>
    /// <remarks>
    /// Algorithm:
    /// 1. Build graph where nodes are sentences and edges represent similarity
    /// 2. Edge weight = cosine similarity of bag-of-words vectors
    /// 3. Apply PageRank with damping factor (typically 0.85)
    /// 4. Iterate until convergence (score changes &lt; threshold)
    /// 5. Normalize scores to 0.0-1.0 range
    /// </remarks>
    Dictionary<int, double> CalculateTextRankScores(string[] sentences);
}
