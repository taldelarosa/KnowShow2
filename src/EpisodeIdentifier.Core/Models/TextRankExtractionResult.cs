namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the result of TextRank-based plot-relevant sentence extraction.
/// Contains the filtered text and statistics about the extraction process.
/// </summary>
public class TextRankExtractionResult
{
    /// <summary>
    /// The concatenated text of selected plot-relevant sentences.
    /// This is the text that should be used for embedding generation.
    /// </summary>
    public required string FilteredText { get; init; }

    /// <summary>
    /// Total number of sentences in the original subtitle text.
    /// </summary>
    public required int TotalSentenceCount { get; init; }

    /// <summary>
    /// Number of sentences selected by TextRank algorithm.
    /// </summary>
    public required int SelectedSentenceCount { get; init; }

    /// <summary>
    /// Average TextRank score of selected sentences.
    /// Higher scores indicate more plot-relevant sentences.
    /// </summary>
    public required double AverageScore { get; init; }

    /// <summary>
    /// Percentage of sentences selected (SelectedSentenceCount / TotalSentenceCount * 100).
    /// </summary>
    public required double SelectionPercentage { get; init; }

    /// <summary>
    /// Indicates whether fallback to full text was triggered due to insufficient sentences.
    /// </summary>
    public required bool FallbackTriggered { get; init; }

    /// <summary>
    /// Reason for fallback if triggered (e.g., "Below 15 sentence threshold", "Below 10% minimum").
    /// Null if no fallback occurred.
    /// </summary>
    public string? FallbackReason { get; init; }

    /// <summary>
    /// Time taken to perform TextRank extraction in milliseconds.
    /// </summary>
    public required long ProcessingTimeMs { get; init; }
}
