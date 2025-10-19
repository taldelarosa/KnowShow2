namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Result from vector similarity search including subtitle metadata and similarity score.
/// Returned by IVectorSearchService.SearchBySimilarity().
/// </summary>
public class VectorSimilarityResult
{
    /// <summary>
    /// Database ID from SubtitleHashes table.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Series name (e.g., "Criminal Minds").
    /// </summary>
    public string Series { get; init; }

    /// <summary>
    /// Season number (e.g., "06" or "6").
    /// </summary>
    public string Season { get; init; }

    /// <summary>
    /// Episode number (e.g., "19").
    /// </summary>
    public string Episode { get; init; }

    /// <summary>
    /// Episode name if available.
    /// </summary>
    public string? EpisodeName { get; init; }

    /// <summary>
    /// Subtitle source format (Text, PGS, VobSub).
    /// </summary>
    public SubtitleSourceFormat SourceFormat { get; init; }

    /// <summary>
    /// Cosine similarity score (0.0-1.0) between query and this result.
    /// Higher is more similar. 1.0 = identical embeddings.
    /// </summary>
    public double Similarity { get; init; }

    /// <summary>
    /// Overall confidence score (0.0-1.0) combining similarity and format-specific adjustments.
    /// Used for matching decisions and auto-renaming.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Distance metric from vectorlite (1.0 - similarity for cosine distance).
    /// Lower distance = more similar.
    /// </summary>
    public double Distance { get; init; }

    /// <summary>
    /// Rank in search results (1 = best match, 2 = second best, etc.).
    /// </summary>
    public int Rank { get; init; }

    public VectorSimilarityResult(
        int id,
        string series,
        string season,
        string episode,
        string? episodeName,
        SubtitleSourceFormat sourceFormat,
        double similarity,
        double confidence,
        double distance,
        int rank)
    {
        Id = id;
        Series = series ?? throw new ArgumentNullException(nameof(series));
        Season = season ?? throw new ArgumentNullException(nameof(season));
        Episode = episode ?? throw new ArgumentNullException(nameof(episode));
        EpisodeName = episodeName;
        SourceFormat = sourceFormat;
        Similarity = similarity;
        Confidence = confidence;
        Distance = distance;
        Rank = rank;
    }

    /// <summary>
    /// Convert to LabelledSubtitle for compatibility with existing code.
    /// Note: SubtitleText is not available in search results.
    /// </summary>
    public LabelledSubtitle ToLabelledSubtitle()
    {
        return new LabelledSubtitle
        {
            Series = Series,
            Season = Season,
            Episode = Episode,
            EpisodeName = EpisodeName,
            SubtitleText = "", // Not available in vector search results
            FuzzyHash = "" // Not applicable for embedding-based matching
        };
    }
}
