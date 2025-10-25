using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Helper service for segmenting text into sentences.
/// Handles subtitle-specific preprocessing (removing timestamps, speaker labels, etc.)
/// before applying sentence boundary detection.
/// </summary>
public class SentenceSegmenter
{
    private static readonly Regex SentenceBoundaryRegex = new(
        @"(?<=[.!?])\s+(?=[A-Z])",
        RegexOptions.Compiled);

    private static readonly Regex TimestampRegex = new(
        @"\d{1,2}:\d{2}:\d{2}[,\.]\d{3}\s*-->\s*\d{1,2}:\d{2}:\d{2}[,\.]\d{3}",
        RegexOptions.Compiled);

    private static readonly Regex SequenceNumberRegex = new(
        @"^\d+\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex SpeakerLabelRegex = new(
        @"^[-\[\(<].*?[>\)\]]\s*",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex StyleTagsRegex = new(
        @"<[^>]+>|\{[^}]+\}",
        RegexOptions.Compiled);

    private static readonly Regex MultipleWhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);

    /// <summary>
    /// Segments subtitle text into individual sentences.
    /// Applies preprocessing to remove subtitle-specific markup before segmentation.
    /// </summary>
    /// <param name="text">Raw subtitle text to segment.</param>
    /// <returns>Array of sentence strings. Empty array if input is null or whitespace.</returns>
    public string[] SegmentIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        // Preprocess: Remove subtitle-specific markup
        text = RemoveSubtitleMarkup(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        // Split on sentence boundaries
        var sentences = SentenceBoundaryRegex.Split(text)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length >= 3) // Filter very short fragments
            .ToArray();

        return sentences;
    }

    /// <summary>
    /// Removes subtitle-specific markup from text.
    /// Includes timestamps, sequence numbers, speaker labels, style tags.
    /// </summary>
    private static string RemoveSubtitleMarkup(string text)
    {
        // Remove timestamps (00:01:23,456 --> 00:01:26,789)
        text = TimestampRegex.Replace(text, string.Empty);

        // Remove sequence numbers (standalone numbers on their own lines)
        text = SequenceNumberRegex.Replace(text, string.Empty);

        // Remove speaker labels (- Speaker: , [Speaker], <Speaker>, (Speaker))
        text = SpeakerLabelRegex.Replace(text, string.Empty);

        // Remove style tags (<i>, <b>, {\\an8}, etc.)
        text = StyleTagsRegex.Replace(text, string.Empty);

        // Normalize whitespace
        text = MultipleWhitespaceRegex.Replace(text, " ");

        return text.Trim();
    }
}
