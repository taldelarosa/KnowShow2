namespace EpisodeIdentifier.Core.Models.Configuration;

/// <summary>
/// Per-format embedding match thresholds for semantic similarity matching.
/// Allows different confidence levels for Text, PGS, and VobSub sources
/// to account for OCR quality differences.
/// </summary>
public class EmbeddingMatchThresholds
{
    /// <summary>
    /// Thresholds for text-based subtitles (highest accuracy).
    /// </summary>
    public FormatThreshold TextBased { get; set; } = new()
    {
        EmbedSimilarity = 0.85,
        MatchConfidence = 0.70,
        RenameConfidence = 0.80
    };

    /// <summary>
    /// Thresholds for PGS subtitles (medium accuracy, OCR-based).
    /// </summary>
    public FormatThreshold Pgs { get; set; } = new()
    {
        EmbedSimilarity = 0.80,
        MatchConfidence = 0.60,
        RenameConfidence = 0.70
    };

    /// <summary>
    /// Thresholds for VobSub subtitles (lower accuracy, OCR-based with compression artifacts).
    /// </summary>
    public FormatThreshold VobSub { get; set; } = new()
    {
        EmbedSimilarity = 0.75,
        MatchConfidence = 0.50,
        RenameConfidence = 0.60
    };

    /// <summary>
    /// Get threshold configuration for a specific source format.
    /// </summary>
    public FormatThreshold GetThreshold(SubtitleSourceFormat format)
    {
        return format switch
        {
            SubtitleSourceFormat.Text => TextBased,
            SubtitleSourceFormat.PGS => Pgs,
            SubtitleSourceFormat.VobSub => VobSub,
            _ => TextBased
        };
    }

    /// <summary>
    /// Thresholds for a specific subtitle source format.
    /// </summary>
    public class FormatThreshold
    {
        /// <summary>
        /// Minimum embedding cosine similarity (0.0-1.0) to consider as a match candidate.
        /// Results below this threshold are filtered out entirely.
        /// </summary>
        public double EmbedSimilarity { get; set; }

        /// <summary>
        /// Minimum confidence (0.0-1.0) to report a match to the user.
        /// Should be lower than RenameConfidence.
        /// </summary>
        public double MatchConfidence { get; set; }

        /// <summary>
        /// Minimum confidence (0.0-1.0) for automatic file renaming.
        /// Should be higher than MatchConfidence to avoid false positives.
        /// </summary>
        public double RenameConfidence { get; set; }

        /// <summary>
        /// Validate that thresholds are in valid ranges and logical order.
        /// </summary>
        public bool IsValid(out string? error)
        {
            if (EmbedSimilarity < 0.0 || EmbedSimilarity > 1.0)
            {
                error = "EmbedSimilarity must be between 0.0 and 1.0";
                return false;
            }

            if (MatchConfidence < 0.0 || MatchConfidence > 1.0)
            {
                error = "MatchConfidence must be between 0.0 and 1.0";
                return false;
            }

            if (RenameConfidence < 0.0 || RenameConfidence > 1.0)
            {
                error = "RenameConfidence must be between 0.0 and 1.0";
                return false;
            }

            if (RenameConfidence < MatchConfidence)
            {
                error = "RenameConfidence should be >= MatchConfidence";
                return false;
            }

            error = null;
            return true;
        }
    }
}
