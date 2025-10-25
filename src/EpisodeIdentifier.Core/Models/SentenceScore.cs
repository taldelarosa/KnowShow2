namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents a sentence with its TextRank importance score.
/// Used during the sentence ranking and selection process.
/// </summary>
public class SentenceScore
{
    /// <summary>
    /// The sentence text content.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// TextRank score indicating the sentence's importance/relevance.
    /// Higher scores indicate more central/plot-relevant sentences.
    /// Range: 0.0 to 1.0 (normalized PageRank score).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Original index of the sentence in the source text.
    /// Used to maintain chronological order when reconstructing filtered text.
    /// </summary>
    public required int OriginalIndex { get; init; }

    /// <summary>
    /// Number of words in the sentence.
    /// Used for filtering out very short sentences that may not be meaningful.
    /// </summary>
    public required int WordCount { get; init; }

    /// <summary>
    /// Indicates whether this sentence was selected for the filtered output.
    /// </summary>
    public required bool IsSelected { get; init; }
}
