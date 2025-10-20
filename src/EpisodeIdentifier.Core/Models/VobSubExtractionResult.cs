namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the result of extracting VobSub files from an MKV container.
/// </summary>
public class VobSubExtractionResult
{
    /// <summary>
    /// Whether extraction succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Absolute path to extracted .idx file. Must exist if Success is true.
    /// </summary>
    public string? IdxFilePath { get; set; }

    /// <summary>
    /// Absolute path to extracted .sub file. Must exist if Success is true.
    /// </summary>
    public string? SubFilePath { get; set; }

    /// <summary>
    /// Error details if extraction failed. Required if Success is false.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken for extraction.
    /// </summary>
    public TimeSpan ExtractionDuration { get; set; }

    /// <summary>
    /// Source subtitle track index. Must be >= 0.
    /// </summary>
    public int TrackIndex { get; set; }

    /// <summary>
    /// Source video file path. Must be non-empty.
    /// </summary>
    public string SourceVideoPath { get; set; } = string.Empty;
}
