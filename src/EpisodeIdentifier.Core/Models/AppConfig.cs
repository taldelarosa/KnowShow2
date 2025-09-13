using System.Text.Json.Serialization;

namespace EpisodeIdentifier.Core.Models;

public class AppConfig
{
    /// <summary>
    /// Minimum confidence threshold for episode matching (default: 0.8)
    /// </summary>
    [JsonPropertyName("matchConfidenceThreshold")]
    public double MatchConfidenceThreshold { get; set; } = 0.8;

    /// <summary>
    /// Minimum confidence threshold required for automatic file renaming (default: 0.85)
    /// </summary>
    [JsonPropertyName("renameConfidenceThreshold")]
    public double RenameConfidenceThreshold { get; set; } = 0.85;

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