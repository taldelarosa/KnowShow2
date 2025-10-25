namespace EpisodeIdentifier.Core.Models.Configuration;

/// <summary>
/// Configuration for TextRank-based plot-relevant sentence extraction.
/// Controls how subtitles are filtered before embedding generation.
/// </summary>
public class TextRankConfiguration
{
    /// <summary>
    /// Enable or disable TextRank filtering.
    /// When false, full subtitle text is used for embeddings.
    /// Default: false (feature is opt-in).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Percentage of sentences to select based on TextRank scores.
    /// Range: 10-50 (10% to 50% of sentences).
    /// Default: 25 (top 25% of sentences).
    /// </summary>
    public int SentencePercentage { get; set; } = 25;

    /// <summary>
    /// Absolute minimum number of sentences required to apply filtering.
    /// If total sentences are below this, fallback to full text.
    /// Range: 5-100.
    /// Default: 15.
    /// </summary>
    public int MinSentences { get; set; } = 15;

    /// <summary>
    /// Minimum percentage of original text that must remain after filtering.
    /// If selection would result in less than this percentage, fallback to full text.
    /// Range: 5-50.
    /// Default: 10 (at least 10% of original must remain).
    /// </summary>
    public int MinPercentage { get; set; } = 10;

    /// <summary>
    /// PageRank damping factor.
    /// Controls the probability of following graph edges vs. random jumps.
    /// Range: 0.5-0.95.
    /// Default: 0.85 (standard PageRank value).
    /// </summary>
    public double DampingFactor { get; set; } = 0.85;

    /// <summary>
    /// Convergence threshold for PageRank iteration.
    /// Algorithm stops when score changes are below this value.
    /// Range: 0.00001-0.01.
    /// Default: 0.0001.
    /// </summary>
    public double ConvergenceThreshold { get; set; } = 0.0001;

    /// <summary>
    /// Maximum number of PageRank iterations.
    /// Prevents infinite loops if convergence is not reached.
    /// Range: 10-500.
    /// Default: 100.
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Minimum cosine similarity threshold for sentence graph edges.
    /// Sentences with similarity below this are not connected in the graph.
    /// Range: 0.0-0.5.
    /// Default: 0.1 (connect sentences with >10% similarity).
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.1;

    /// <summary>
    /// Validates the configuration values are within acceptable ranges.
    /// </summary>
    /// <returns>Tuple of (isValid, errorMessage). errorMessage is null if valid.</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (SentencePercentage < 10 || SentencePercentage > 50)
        {
            return (false, $"SentencePercentage must be between 10 and 50, got {SentencePercentage}");
        }

        if (MinSentences < 5 || MinSentences > 100)
        {
            return (false, $"MinSentences must be between 5 and 100, got {MinSentences}");
        }

        if (MinPercentage < 5 || MinPercentage > 50)
        {
            return (false, $"MinPercentage must be between 5 and 50, got {MinPercentage}");
        }

        if (DampingFactor < 0.5 || DampingFactor > 0.95)
        {
            return (false, $"DampingFactor must be between 0.5 and 0.95, got {DampingFactor}");
        }

        if (ConvergenceThreshold < 0.00001 || ConvergenceThreshold > 0.01)
        {
            return (false, $"ConvergenceThreshold must be between 0.00001 and 0.01, got {ConvergenceThreshold}");
        }

        if (MaxIterations < 10 || MaxIterations > 500)
        {
            return (false, $"MaxIterations must be between 10 and 500, got {MaxIterations}");
        }

        if (SimilarityThreshold < 0.0 || SimilarityThreshold > 0.5)
        {
            return (false, $"SimilarityThreshold must be between 0.0 and 0.5, got {SimilarityThreshold}");
        }

        return (true, null);
    }
}
