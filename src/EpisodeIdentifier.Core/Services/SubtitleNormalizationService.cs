using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

public class SubtitleNormalizationService
{
    private readonly ILogger<SubtitleNormalizationService> _logger;

    public SubtitleNormalizationService(ILogger<SubtitleNormalizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates multiple normalized versions of subtitle text for better fuzzy matching
    /// </summary>
    public SubtitleNormalizedVersions CreateNormalizedVersions(string subtitleText)
    {
        if (string.IsNullOrWhiteSpace(subtitleText))
        {
            return new SubtitleNormalizedVersions
            {
                Original = string.Empty,
                NoTimecodes = string.Empty,
                NoHtml = string.Empty,
                NoHtmlAndTimecodes = string.Empty
            };
        }

        var original = subtitleText.Trim();
        var noTimecodes = RemoveTimecodes(original);
        var noHtml = RemoveHtml(original);
        var noHtmlAndTimecodes = RemoveHtmlAndTimecodes(original);

        _logger.LogDebug("Created normalized versions - Original: {OriginalLength}, NoTimecodes: {NoTimecodesLength}, NoHtml: {NoHtmlLength}, Clean: {CleanLength}",
            original.Length, noTimecodes.Length, noHtml.Length, noHtmlAndTimecodes.Length);

        return new SubtitleNormalizedVersions
        {
            Original = original,
            NoTimecodes = noTimecodes,
            NoHtml = noHtml,
            NoHtmlAndTimecodes = noHtmlAndTimecodes
        };
    }

    /// <summary>
    /// Removes SRT timecode lines (e.g., "00:01:23,456 --> 00:01:25,789")
    /// </summary>
    public string RemoveTimecodes(string subtitleText)
    {
        if (string.IsNullOrWhiteSpace(subtitleText))
            return string.Empty;

        // Pattern to match SRT timecode lines: "00:01:23,456 --> 00:01:25,789"
        var timecodePattern = @"^\d{2}:\d{2}:\d{2},\d{3}\s*-->\s*\d{2}:\d{2}:\d{2},\d{3}$";

        // Also remove sequence numbers (standalone digits on their own line)
        var sequencePattern = @"^\d+$";

        var lines = subtitleText.Split('\n', StringSplitOptions.None);
        var filteredLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip timecode lines and sequence numbers
            if (Regex.IsMatch(trimmedLine, timecodePattern) ||
                Regex.IsMatch(trimmedLine, sequencePattern))
            {
                continue;
            }

            // Keep the line (including empty lines for subtitle separation)
            filteredLines.Add(line);
        }

        // Clean up excessive empty lines while preserving subtitle boundaries
        var result = string.Join('\n', filteredLines);
        result = Regex.Replace(result, @"\n{3,}", "\n\n"); // Replace 3+ newlines with 2

        return result.Trim();
    }

    /// <summary>
    /// Removes HTML/XML tags from subtitle text (e.g., &lt;i&gt;, &lt;b&gt;, &lt;font&gt;, etc.)
    /// </summary>
    public string RemoveHtml(string subtitleText)
    {
        if (string.IsNullOrWhiteSpace(subtitleText))
            return string.Empty;

        // Remove HTML/XML tags
        var noTags = Regex.Replace(subtitleText, @"<[^>]+>", "");

        // Decode common HTML entities
        noTags = noTags.Replace("&lt;", "<");
        noTags = noTags.Replace("&gt;", ">");
        noTags = noTags.Replace("&amp;", "&");
        noTags = noTags.Replace("&quot;", "\"");
        noTags = noTags.Replace("&#39;", "'");
        noTags = noTags.Replace("&nbsp;", " ");

        // Clean up any double spaces that might have been created
        noTags = Regex.Replace(noTags, @"\s{2,}", " ");

        return noTags.Trim();
    }

    /// <summary>
    /// Removes both HTML tags and timecodes for the cleanest comparison text
    /// </summary>
    public string RemoveHtmlAndTimecodes(string subtitleText)
    {
        // Apply both transformations
        var noTimecodes = RemoveTimecodes(subtitleText);
        var clean = RemoveHtml(noTimecodes);

        return clean;
    }
}

/// <summary>
/// Container for all normalized versions of subtitle text
/// </summary>
public class SubtitleNormalizedVersions
{
    public string Original { get; set; } = string.Empty;
    public string NoTimecodes { get; set; } = string.Empty;
    public string NoHtml { get; set; } = string.Empty;
    public string NoHtmlAndTimecodes { get; set; } = string.Empty;
}
