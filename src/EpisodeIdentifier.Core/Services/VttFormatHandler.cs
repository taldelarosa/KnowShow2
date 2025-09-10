using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Handles parsing of WebVTT (.vtt) subtitle format.
/// </summary>
public class VttFormatHandler : ISubtitleFormatHandler
{
    public SubtitleFormat SupportedFormat => SubtitleFormat.VTT;

    /// <summary>
    /// Regular expression for parsing VTT cue blocks.
    /// </summary>
    private static readonly Regex VttCueRegex = new(
        @"(\d{2}:\d{2}:\d{2}\.\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}\.\d{3})(?:[^\r\n]*)?[\r\n]+((?:[^\r\n]+(?:[\r\n]+)?)*?)(?=\r?\n\r?\n|\r?\n\d{2}:|\Z)",
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

            return ParseVttContent(content);
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

        // VTT files must start with "WEBVTT"
        return content.TrimStart().StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase);
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

    private static SubtitleParsingResult ParseVttContent(string content)
    {
        var entries = new List<SubtitleEntry>();

        // Remove WEBVTT header and any metadata
        var contentWithoutHeader = Regex.Replace(content, @"^WEBVTT[^\r\n]*[\r\n]*", "", RegexOptions.Multiline);

        var matches = VttCueRegex.Matches(contentWithoutHeader);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 4 && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
            {
                var startTime = ParseVttTimestamp(match.Groups[1].Value);
                var endTime = ParseVttTimestamp(match.Groups[2].Value);
                var text = CleanVttText(match.Groups[3].Value.Trim());

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
            Format = SubtitleFormat.VTT,
            Status = ProcessingStatus.Completed
        };
    }

    private static long ParseVttTimestamp(string timestamp)
    {
        // Parse VTT timestamp format: "HH:MM:SS.mmm" or "MM:SS.mmm"
        var parts = timestamp.Split(':');

        if (parts.Length == 2)
        {
            // MM:SS.mmm format
            var minutes = int.Parse(parts[0]);
            var secondsParts = parts[1].Split('.');
            var seconds = int.Parse(secondsParts[0]);
            var milliseconds = int.Parse(secondsParts[1]);

            return (minutes * 60 + seconds) * 1000 + milliseconds;
        }
        else if (parts.Length == 3)
        {
            // HH:MM:SS.mmm format
            var hours = int.Parse(parts[0]);
            var minutes = int.Parse(parts[1]);
            var secondsParts = parts[2].Split('.');
            var seconds = int.Parse(secondsParts[0]);
            var milliseconds = int.Parse(secondsParts[1]);

            return (hours * 3600 + minutes * 60 + seconds) * 1000 + milliseconds;
        }

        return 0;
    }

    private static string CleanVttText(string text)
    {
        // Remove VTT formatting tags like <c>, <i>, etc.
        var cleaned = Regex.Replace(text, @"<[^>]*>", string.Empty);
        return cleaned.Replace("\n", " ").Trim();
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
