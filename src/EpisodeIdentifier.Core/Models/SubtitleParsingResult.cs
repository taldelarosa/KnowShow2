namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the result of parsing and processing text subtitle content.
/// Contains structured subtitle data and processing statistics.
/// </summary>
public class SubtitleParsingResult
{
    /// <summary>
    /// The format of the parsed subtitle content.
    /// </summary>
    public SubtitleFormat Format { get; set; }

    /// <summary>
    /// List of individual subtitle entries/cues.
    /// </summary>
    public List<SubtitleEntry> Entries { get; set; } = new();

    /// <summary>
    /// Processing status of the parsing operation.
    /// </summary>
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken to parse the content in milliseconds.
    /// </summary>
    public long ParsingTimeMs { get; set; }

    /// <summary>
    /// Total duration covered by all subtitle entries in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Metadata extracted from the subtitle format (e.g., style information).
    /// </summary>
    public Dictionary<string, string> FormatMetadata { get; set; } = new();

    /// <summary>
    /// Indicates whether the parsing was successful.
    /// </summary>
    public bool IsSuccessful => Status == ProcessingStatus.Completed && Entries.Count > 0;

    /// <summary>
    /// Gets the total character count across all subtitle entries.
    /// </summary>
    public int TotalCharacterCount => Entries.Sum(entry => entry.Text?.Length ?? 0);

    /// <summary>
    /// Gets subtitle entries within a specific time range.
    /// </summary>
    /// <param name="startMs">Start time in milliseconds.</param>
    /// <param name="endMs">End time in milliseconds.</param>
    /// <returns>Collection of entries within the specified time range.</returns>
    public IEnumerable<SubtitleEntry> GetEntriesInTimeRange(long startMs, long endMs)
    {
        return Entries.Where(entry => 
            entry.StartTimeMs >= startMs && entry.EndTimeMs <= endMs);
    }
}

/// <summary>
/// Represents a single subtitle entry/cue with timing and text content.
/// </summary>
public class SubtitleEntry
{
    /// <summary>
    /// Sequential index of this entry in the subtitle file.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Start time of the subtitle in milliseconds.
    /// </summary>
    public long StartTimeMs { get; set; }

    /// <summary>
    /// End time of the subtitle in milliseconds.
    /// </summary>
    public long EndTimeMs { get; set; }

    /// <summary>
    /// Text content of the subtitle.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Duration of this subtitle entry in milliseconds.
    /// </summary>
    public long DurationMs => EndTimeMs - StartTimeMs;

    /// <summary>
    /// Format-specific styling or metadata for this entry.
    /// </summary>
    public Dictionary<string, string> Styling { get; set; } = new();
}
