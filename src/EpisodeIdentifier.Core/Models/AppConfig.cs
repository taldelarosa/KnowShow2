using System.Text.Json.Serialization;
using EpisodeIdentifier.Core.Models.Configuration;

namespace EpisodeIdentifier.Core.Models;

public class AppConfig
{
    /// <summary>
    /// Matching thresholds for different subtitle types.
    /// </summary>
    [JsonPropertyName("matchingThresholds")]
    public MatchingThresholds? MatchingThresholds { get; set; } = new();

    /// <summary>
    /// Matching strategy selection: "embedding", "fuzzy", or "hybrid".
    /// </summary>
    [JsonPropertyName("matchingStrategy")]
    public string MatchingStrategy { get; set; } = "embedding";

    /// <summary>
    /// Embedding-based match thresholds for different subtitle formats.
    /// </summary>
    [JsonPropertyName("embeddingThresholds")]
    public EmbeddingMatchThresholds? EmbeddingThresholds { get; set; } = new();

    /// <summary>
    /// [DEPRECATED] Use MatchingThresholds.TextBased.MatchConfidence instead.
    /// Minimum confidence threshold for episode matching (default: 0.8)
    /// </summary>
    [Obsolete("Use MatchingThresholds.TextBased.MatchConfidence instead. This property will be removed in a future version.")]
    [JsonPropertyName("matchConfidenceThreshold")]
    public double MatchConfidenceThreshold { get; set; } = 0.8;

    /// <summary>
    /// [DEPRECATED] Use MatchingThresholds.TextBased.RenameConfidence instead.
    /// Minimum confidence threshold required for automatic file renaming (default: 0.50)
    /// </summary>
    [Obsolete("Use MatchingThresholds.TextBased.RenameConfidence instead. This property will be removed in a future version.")]
    [JsonPropertyName("renameConfidenceThreshold")]
    public double RenameConfidenceThreshold { get; set; } = 0.50;

    /// <summary>
    /// Regex patterns for parsing series/season/episode/episode name from filenames
    /// </summary>
    [JsonPropertyName("filenamePatterns")]
    public FilenamePatterns FilenamePatterns { get; set; } = new();

    /// <summary>
    /// Template for generating standardized filenames
    /// Available placeholders: {SeriesName}, {Season}, {Episode}, {EpisodeName}, {FileExtension}
    /// </summary>
    [JsonPropertyName("filenameTemplate")]
    public string FilenameTemplate { get; set; } = "{SeriesName} - S{Season:D2}E{Episode:D2} - {EpisodeName}{FileExtension}";

    /// <summary>
    /// Maximum number of concurrent operations allowed for bulk processing (default: 1, range: 1-100).
    /// Controls the level of parallelism when processing multiple files simultaneously.
    /// </summary>
    [JsonPropertyName("maxConcurrency")]
    public int MaxConcurrency { get; set; } = 1;
}

public class FilenamePatterns
{
    /// <summary>
    /// Primary pattern for "Series S##E## EpisodeName" format
    /// </summary>
    [JsonPropertyName("primaryPattern")]
    public string PrimaryPattern { get; set; } = @"^(.+?)\s+S(\d+)E(\d+)(?:[\s\.\-]+(.+?))?$";

    /// <summary>
    /// Secondary pattern for "Series Season Episode EpisodeName" format (space-separated)
    /// </summary>
    [JsonPropertyName("secondaryPattern")]
    public string SecondaryPattern { get; set; } = @"^(.+?)\s+(\d+)x(\d+)(?:[\s\.\-]+(.+?))?$";

    /// <summary>
    /// Tertiary pattern for "Series.S##.E##.EpisodeName" format (dot-separated)
    /// </summary>
    [JsonPropertyName("tertiaryPattern")]
    public string TertiaryPattern { get; set; } = @"^(.+?)\.S(\d+)\.E(\d+)(?:\.(.+?))?$";
}
