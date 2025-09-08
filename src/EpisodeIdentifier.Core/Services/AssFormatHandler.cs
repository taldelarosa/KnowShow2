using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Handler for Advanced SubStation Alpha (.ass) format.
/// Implements parsing and validation for ASS subtitle files.
/// </summary>
public class AssFormatHandler : ISubtitleFormatHandler
{
    public SubtitleFormat SupportedFormat => SubtitleFormat.ASS;

    /// <summary>
    /// Regular expression pattern for ASS dialogue format
    /// </summary>
    private static readonly Regex AssDialoguePattern = new(
        @"^Dialogue:\s*(\d+),(\d+:\d+:\d+\.\d+),(\d+:\d+:\d+\.\d+),([^,]*),([^,]*),([^,]*),([^,]*),([^,]*),([^,]*),(.*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Pattern to detect ASS format headers
    /// </summary>
    private static readonly Regex AssHeaderPattern = new(
        @"^\[Script Info\]|\[V4\+ Styles\]|\[Events\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool CanHandle(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // Look for ASS-style headers or dialogue lines
        return AssHeaderPattern.IsMatch(content) || AssDialoguePattern.IsMatch(content);
    }

    public async Task<SubtitleParsingResult> ParseContentAsync(string content, CancellationToken cancellationToken = default)
    {
        var result = new SubtitleParsingResult
        {
            Format = SubtitleFormat.ASS,
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

            int entryIndex = 1;
            bool inEventsSection = false;

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if we're entering the Events section
                if (line.Equals("[Events]", StringComparison.OrdinalIgnoreCase))
                {
                    inEventsSection = true;
                    continue;
                }

                // Check if we're leaving the Events section
                if (line.StartsWith('[') && line.EndsWith(']') && inEventsSection)
                {
                    inEventsSection = false;
                    continue;
                }

                // Only process dialogue lines when in Events section
                if (!inEventsSection || !line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = AssDialoguePattern.Match(line);
                if (!match.Success)
                    continue;

                try
                {
                    var startTimeMs = ParseAssTimestamp(match.Groups[2].Value);
                    var endTimeMs = ParseAssTimestamp(match.Groups[3].Value);
                    var text = match.Groups[10].Value;

                    // Remove ASS formatting codes
                    text = RemoveAssFormatting(text);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var entry = new SubtitleEntry
                        {
                            Index = entryIndex++,
                            StartTimeMs = startTimeMs,
                            EndTimeMs = endTimeMs,
                            Text = text.Trim()
                        };

                        // Store style information
                        if (!string.IsNullOrWhiteSpace(match.Groups[4].Value))
                        {
                            entry.Styling["Style"] = match.Groups[4].Value;
                        }

                        entries.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    // Skip invalid dialogue lines but continue processing
                    result.FormatMetadata[$"ParseError_{entryIndex}"] = ex.Message;
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
            result.ErrorMessage = $"Failed to parse ASS content: {ex.Message}";
        }
        finally
        {
            result.ParsingTimeMs = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
        }

        return result;
    }

    private static long ParseAssTimestamp(string timestamp)
    {
        // ASS format: H:MM:SS.SS
        var parts = timestamp.Split(':');
        if (parts.Length != 3)
            throw new FormatException($"Invalid ASS timestamp format: {timestamp}");

        var hours = int.Parse(parts[0]);
        var minutes = int.Parse(parts[1]);
        var secondsParts = parts[2].Split('.');
        var seconds = int.Parse(secondsParts[0]);
        var centiseconds = secondsParts.Length > 1 ? int.Parse(secondsParts[1].PadRight(2, '0')[..2]) : 0;

        return (hours * 3600 + minutes * 60 + seconds) * 1000L + centiseconds * 10L;
    }

    private static string RemoveAssFormatting(string text)
    {
        // Remove ASS override codes like {\b1}, {\i1}, etc.
        var cleanText = Regex.Replace(text, @"\{[^}]*\}", string.Empty);
        
        // Remove line breaks (\\N)
        cleanText = cleanText.Replace("\\N", "\n");
        
        return cleanText;
    }
}
