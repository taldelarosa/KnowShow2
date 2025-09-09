using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Handles parsing of Advanced SubStation Alpha (.ass/.ssa) subtitle format.
/// </summary>
public class AssFormatHandler : ISubtitleFormatHandler
{
    public SubtitleFormat SupportedFormat => SubtitleFormat.ASS;

    /// <summary>
    /// Regular expression for parsing ASS dialogue lines.
    /// </summary>
    private static readonly Regex AssDialogueRegex = new(
        @"^Dialogue:\s*\d+,([^,]+),([^,]+),([^,]*),([^,]*),([^,]*),([^,]*),([^,]*),([^,]*),(.*)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public async Task<SubtitleParsingResult> ParseSubtitleTextAsync(
        Stream stream, 
        string? encoding = null,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var textEncoding = GetEncoding(encoding);
        
        using var reader = new StreamReader(stream, textEncoding, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        
        return ParseAssContent(content);
    }

    public bool CanHandle(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // ASS files contain [Script Info] section and Dialogue lines
        return content.Contains("[Script Info]", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("Dialogue:", StringComparison.OrdinalIgnoreCase);
    }

    private static Encoding GetEncoding(string? encoding)
    {
        if (string.IsNullOrWhiteSpace(encoding))
            return Encoding.UTF8;

        try
        {
            return Encoding.GetEncoding(encoding);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException($"Invalid encoding: {encoding}", nameof(encoding));
        }
    }

    private static SubtitleParsingResult ParseAssContent(string content)
    {
        var entries = new List<SubtitleEntry>();
        var matches = AssDialogueRegex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 10)
            {
                var startTime = ParseAssTimestamp(match.Groups[1].Value);
                var endTime = ParseAssTimestamp(match.Groups[2].Value);
                var text = CleanAssText(match.Groups[9].Value);

                entries.Add(new SubtitleEntry
                {
                    Index = entries.Count + 1,
                    StartTimeMs = startTime,
                    EndTimeMs = endTime,
                    Text = text
                });
            }
        }

        return new SubtitleParsingResult
        {
            Entries = entries,
            Format = SubtitleFormat.ASS,
            Status = ProcessingStatus.Completed
        };
    }

    private static long ParseAssTimestamp(string timestamp)
    {
        // Parse ASS timestamp format: "H:MM:SS.cc" (centiseconds)
        var parts = timestamp.Split(':');
        var hours = int.Parse(parts[0]);
        var minutes = int.Parse(parts[1]);
        var secondsParts = parts[2].Split('.');
        var seconds = int.Parse(secondsParts[0]);
        var centiseconds = secondsParts.Length > 1 ? int.Parse(secondsParts[1]) : 0;
        
        return (hours * 3600 + minutes * 60 + seconds) * 1000 + centiseconds * 10;
    }

    private static string CleanAssText(string text)
    {
        // Remove ASS formatting codes like {\b1}, {\i1}, etc.
        var cleaned = Regex.Replace(text, @"\{[^}]*\}", string.Empty);
        return cleaned.Replace("\\N", " ").Replace("\\n", " ").Trim();
    }
}