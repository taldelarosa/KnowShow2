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

        try
        {
            // Read bytes first to check for malformed data
            var buffer = new byte[stream.Length];
            await stream.ReadAsync(buffer, 0, (int)stream.Length, cancellationToken);

            // Check for invalid UTF-8 sequences
            if (IsInvalidUtf8(buffer))
            {
                throw new InvalidDataException("The subtitle file contains malformed data or invalid encoding.");
            }

            // Reset stream position and read as text
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var content = await reader.ReadToEndAsync(cancellationToken);

            return ParseAssContent(content);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("The subtitle file contains malformed data or invalid encoding.", ex);
        }
    }

    public bool CanHandle(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // ASS files contain sections like [Script Info], [V4+ Styles], [Events] and Dialogue lines
        return content.Contains("[Script Info]", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("[V4+ Styles]", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("[V4 Styles]", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("[Events]", StringComparison.OrdinalIgnoreCase) ||
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
            throw new NotSupportedException($"Encoding '{encoding}' is not supported", new ArgumentException($"Invalid encoding: {encoding}", nameof(encoding)));
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

    private static bool IsInvalidUtf8(byte[] bytes)
    {
        // Check for invalid UTF-8 byte sequences
        // Bytes 0xFF and 0xFE are never valid in UTF-8
        foreach (var b in bytes)
        {
            if (b == 0xFF || b == 0xFE)
                return true;
        }

        // Additional check: try to convert to string and see if it contains replacement characters
        try
        {
            var text = Encoding.UTF8.GetString(bytes);
            return text.Contains('\uFFFD');
        }
        catch
        {
            return true;
        }
    }
}