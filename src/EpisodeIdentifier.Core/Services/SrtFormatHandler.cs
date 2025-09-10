using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Handles parsing of SubRip (.srt) subtitle format.
/// </summary>
public class SrtFormatHandler : ISubtitleFormatHandler
{
    public SubtitleFormat SupportedFormat => SubtitleFormat.SRT;

    /// <summary>
    /// Regular expression for parsing SRT subtitle entries.
    /// Matches: sequence number, timestamp, and subtitle text.
    /// </summary>
    private static readonly Regex SrtEntryRegex = new(
        @"^(\d+)\s*\n(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})\s*\n(.*?)(?=\n\d+\s*\n|\n*$)",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Regular expression for stripping HTML tags from subtitle text.
    /// </summary>
    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]*>",
        RegexOptions.Compiled);

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

            return ParseSrtContent(content);
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

        // Look for SRT-style sequence numbers and timestamps
        return Regex.IsMatch(content, @"^\d+\s*\n\d{2}:\d{2}:\d{2},\d{3}\s*-->\s*\d{2}:\d{2}:\d{2},\d{3}", RegexOptions.Multiline);
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

    private static SubtitleParsingResult ParseSrtContent(string content)
    {
        var entries = new List<SubtitleEntry>();
        var matches = SrtEntryRegex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 5)
            {
                var sequenceNumber = int.Parse(match.Groups[1].Value);
                var startTime = ParseSrtTimestamp(match.Groups[2].Value);
                var endTime = ParseSrtTimestamp(match.Groups[3].Value);
                var rawText = match.Groups[4].Value.Trim().Replace("\n", " ");

                // Strip HTML tags from the text
                var cleanText = HtmlTagRegex.Replace(rawText, "").Trim();

                entries.Add(new SubtitleEntry
                {
                    Index = sequenceNumber,
                    StartTimeMs = startTime,
                    EndTimeMs = endTime,
                    Text = cleanText
                });
            }
        }

        return new SubtitleParsingResult
        {
            Entries = entries,
            Format = SubtitleFormat.SRT,
            Status = ProcessingStatus.Completed
        };
    }

    private static long ParseSrtTimestamp(string timestamp)
    {
        // Parse SRT timestamp format: "HH:MM:SS,mmm"
        var parts = timestamp.Split(':');
        var hours = int.Parse(parts[0]);
        var minutes = int.Parse(parts[1]);
        var secondsParts = parts[2].Split(',');
        var seconds = int.Parse(secondsParts[0]);
        var milliseconds = int.Parse(secondsParts[1]);

        return (hours * 3600 + minutes * 60 + seconds) * 1000 + milliseconds;
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
