namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Represents the result of performing OCR on VobSub subtitle images.
/// </summary>
public class VobSubOcrResult
{
    /// <summary>
    /// Whether OCR succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// OCR output text (normalized). Must be non-empty if Success is true.
    /// Text is normalized with whitespace collapsed, timecodes removed, HTML tags removed,
    /// multiple newlines reduced to single newlines, and leading/trailing whitespace trimmed.
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    /// Aggregate OCR confidence (0.0-1.0). Calculated as the sum of all character confidences divided by total characters.
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Total characters recognized. Must be >= 0.
    /// </summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// Error details if OCR failed. Required if Success is false.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken for OCR processing.
    /// </summary>
    public TimeSpan OcrDuration { get; set; }

    /// <summary>
    /// Number of subtitle images processed. Must be >= 0.
    /// </summary>
    public int ImageCount { get; set; }

    /// <summary>
    /// OCR language code used (e.g., 'eng', 'spa'). Must be non-empty.
    /// </summary>
    public string Language { get; set; } = string.Empty;
}
