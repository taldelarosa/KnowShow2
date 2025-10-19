namespace EpisodeIdentifier.Core.Models;

/// <summary>
/// Subtitle source type for tracking origin and applying format-specific thresholds.
/// Different sources have varying OCR accuracy which affects matching confidence.
/// This is distinct from SubtitleFormat (SRT/ASS/VTT) which tracks file formats.
/// </summary>
public enum SubtitleSourceFormat
{
    /// <summary>
    /// Text-based subtitles (SRT, ASS, WebVTT, etc.)
    /// Highest accuracy, direct text extraction.
    /// Default matching confidence: 0.85
    /// </summary>
    Text,

    /// <summary>
    /// PGS (Presentation Graphic Stream) subtitles from Blu-ray.
    /// Requires OCR via pgsrip + Tesseract.
    /// Medium accuracy due to OCR errors.
    /// Default matching confidence: 0.80
    /// </summary>
    PGS,

    /// <summary>
    /// VobSub (DVD) subtitles (idx/sub format).
    /// Requires OCR via mkvextract + Tesseract.
    /// Lower accuracy due to OCR quality and compression artifacts.
    /// Default matching confidence: 0.75
    /// </summary>
    VobSub
}

/// <summary>
/// Extension methods for SubtitleSourceFormat enum.
/// </summary>
public static class SubtitleSourceFormatExtensions
{
    /// <summary>
    /// Convert enum to database string representation.
    /// </summary>
    public static string ToDbString(this SubtitleSourceFormat format)
    {
        return format.ToString();
    }

    /// <summary>
    /// Parse database string to SubtitleSourceFormat enum.
    /// Throws ArgumentException if format string is invalid.
    /// </summary>
    public static SubtitleSourceFormat FromDbString(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("Format string cannot be null or empty", nameof(format));
        }

        return Enum.Parse<SubtitleSourceFormat>(format, ignoreCase: true);
    }

    /// <summary>
    /// Get default embedding similarity threshold for format.
    /// These thresholds account for OCR quality differences.
    /// </summary>
    public static double GetDefaultEmbedSimilarity(this SubtitleSourceFormat format)
    {
        return format switch
        {
            SubtitleSourceFormat.Text => 0.85,
            SubtitleSourceFormat.PGS => 0.80,
            SubtitleSourceFormat.VobSub => 0.75,
            _ => 0.85
        };
    }

    /// <summary>
    /// Get default match confidence threshold for format.
    /// </summary>
    public static double GetDefaultMatchConfidence(this SubtitleSourceFormat format)
    {
        return format switch
        {
            SubtitleSourceFormat.Text => 0.70,
            SubtitleSourceFormat.PGS => 0.60,
            SubtitleSourceFormat.VobSub => 0.50,
            _ => 0.70
        };
    }

    /// <summary>
    /// Get default rename confidence threshold for format.
    /// </summary>
    public static double GetDefaultRenameConfidence(this SubtitleSourceFormat format)
    {
        return format switch
        {
            SubtitleSourceFormat.Text => 0.80,
            SubtitleSourceFormat.PGS => 0.70,
            SubtitleSourceFormat.VobSub => 0.60,
            _ => 0.80
        };
    }
}
