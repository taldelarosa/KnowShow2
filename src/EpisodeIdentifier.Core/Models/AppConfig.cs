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

    /// <summary>
    /// TextRank-based filtering configuration for plot-relevant sentence extraction.
    /// When enabled, filters subtitle text before embedding generation to improve matching accuracy.
    /// </summary>
    [JsonPropertyName("textRankFiltering")]
    public TextRankConfiguration? TextRankFiltering { get; set; } = new();
}

public class FilenamePatterns
{
    /// <summary>
    /// List of regex patterns for parsing episode information from filenames.
    /// Each pattern must contain named capture groups: SeriesName, Season, Episode (EpisodeName is optional).
    /// Patterns are tried in order until a match is found.
    /// If not specified, default patterns are used for common formats.
    /// </summary>
    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = new()
    {
        // Space or dot before S##E## (handles: "Show Name S01E01", "Show.Name.S01E01.WEB.x264-GROUP")
        @"^(?<SeriesName>.+?)[\s\.]S(?<Season>\d+)E(?<Episode>\d+)[A-Za-z]?(?:E\d+[A-Za-z]?)*(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
        // Space before season x episode (handles: "Show Name 1x01", "Show Name 1x01 Episode Title")
        @"^(?<SeriesName>.+?)\s(?<Season>\d+)x(?<Episode>\d+)[A-Za-z]?(?:x\d+[A-Za-z]?)*(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
        // Dot-separated with S##.E## (handles: "Show.Name.S01.E01.Episode.Title")
        @"^(?<SeriesName>.+?)\.S(?<Season>\d+)\.E(?<Episode>\d+)[A-Za-z]?(?:\.E\d+[A-Za-z]?)*(?:\.(?<EpisodeName>.+?))?$",
        // Hyphen-separated S##E## (handles: "Show-Name-S01E01")
        @"^(?<SeriesName>.+?)-S(?<Season>\d+)E(?<Episode>\d+)[A-Za-z]?(?:E\d+[A-Za-z]?)*(?:[\s\.\-]+(?<EpisodeName>.+?))?$",
        // Underscore-separated S##E## (handles: "Show_Name_S01E01")
        @"^(?<SeriesName>.+?)_S(?<Season>\d+)E(?<Episode>\d+)[A-Za-z]?(?:E\d+[A-Za-z]?)*(?:[\s\.\-_]+(?<EpisodeName>.+?))?$"
    };

    // Legacy properties for backward compatibility - deprecated but maintained for config migration
    [JsonPropertyName("primaryPattern")]
    [Obsolete("Use Patterns list instead. This property is maintained for backward compatibility only.")]
    public string? PrimaryPattern { get; set; }

    [JsonPropertyName("secondaryPattern")]
    [Obsolete("Use Patterns list instead. This property is maintained for backward compatibility only.")]
    public string? SecondaryPattern { get; set; }

    [JsonPropertyName("tertiaryPattern")]
    [Obsolete("Use Patterns list instead. This property is maintained for backward compatibility only.")]
    public string? TertiaryPattern { get; set; }
}
