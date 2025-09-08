using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Handler for SubRip Subtitle (.srt) format.
/// Implements parsing and validation for SRT subtitle files.
/// </summary>
public class SrtFormatHandler : ISubtitleFormatHandler
{
    public SubtitleFormat SupportedFormat => SubtitleFormat.SRT;

    /// <summary>
    /// Regular expression pattern for SRT time format: HH:MM:SS,mmm --> HH:MM:SS,mmm
    /// </summary>
    private static readonly Regex SrtTimePattern = new(
        @"^(\d{2}):(\d{2}):(\d{2}),(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2}),(\d{3})$",
        RegexOptions.Compiled);

    public bool CanHandle(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // Look for SRT-style timestamps in the content
        return SrtTimePattern.IsMatch(content);
    }

    public async Task<SubtitleParsingResult> ParseContentAsync(string content, CancellationToken cancellationToken = default)
    {
        var result = new SubtitleParsingResult
        {
            Format = SubtitleFormat.SRT,
            Status = ProcessingStatus.Processing
        };

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                result.Status = ProcessingStatus.Failed;
                result.ErrorMessage = "Content is empty or null";
                return result;
            }

            var entries = new List<SubtitleEntry>();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                             .Select(line => line.Trim())
                             .Where(line => !string.IsNullOrEmpty(line))
                             .ToArray();

            for (int i = 0; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip non-numeric index lines
                if (!int.TryParse(lines[i], out int index))
                    continue;

                // Look for timestamp on next line
                if (i + 1 >= lines.Length)
                    break;

                var timeLine = lines[i + 1];
                var timeMatch = SrtTimePattern.Match(timeLine);
                if (!timeMatch.Success)
                    continue;

                // Parse start and end times
                var startMs = ParseSrtTimestamp(
                    timeMatch.Groups[1].Value, timeMatch.Groups[2].Value,
                    timeMatch.Groups[3].Value, timeMatch.Groups[4].Value);
                var endMs = ParseSrtTimestamp(
                    timeMatch.Groups[5].Value, timeMatch.Groups[6].Value,
                    timeMatch.Groups[7].Value, timeMatch.Groups[8].Value);

                // Collect text lines until next index or end
                var textLines = new List<string>();
                for (int j = i + 2; j < lines.Length; j++)
                {
                    if (int.TryParse(lines[j], out _))
                        break;
                    textLines.Add(lines[j]);
                }

                if (textLines.Count > 0)
                {
                    var entry = new SubtitleEntry
                    {
                        Index = index,
                        StartTimeMs = startMs,
                        EndTimeMs = endMs,
                        Text = string.Join("\n", textLines)
                    };
                    entries.Add(entry);
                }
            }

            result.Entries = entries;
            result.TotalDurationMs = entries.Count > 0 ? entries.Max(e => e.EndTimeMs) : 0;
            result.Status = ProcessingStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            result.Status = ProcessingStatus.Failed;
            result.ErrorMessage = "Parsing was cancelled";
            throw;
        }
        catch (Exception ex)
        {
            result.Status = ProcessingStatus.Failed;
            result.ErrorMessage = $"Failed to parse SRT content: {ex.Message}";
        }
        finally
        {
            result.ParsingTimeMs = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
        }

        return result;
    }

    private static long ParseSrtTimestamp(string hours, string minutes, string seconds, string milliseconds)
    {
        return (long.Parse(hours) * 3600 + long.Parse(minutes) * 60 + long.Parse(seconds)) * 1000 + long.Parse(milliseconds);
    }
}
