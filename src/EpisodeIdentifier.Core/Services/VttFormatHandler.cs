using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.Text.RegularExpressions;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Handler for WebVTT (.vtt) format.
/// Implements parsing and validation for VTT subtitle files.
/// </summary>
public class VttFormatHandler : ISubtitleFormatHandler
{
    public SubtitleFormat SupportedFormat => SubtitleFormat.VTT;

    /// <summary>
    /// Regular expression pattern for VTT time format: HH:MM:SS.mmm --> HH:MM:SS.mmm
    /// </summary>
    private static readonly Regex VttTimePattern = new(
        @"^(\d{2}):(\d{2}):(\d{2})\.(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})\.(\d{3}).*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Pattern to detect VTT format signature
    /// </summary>
    private static readonly Regex VttHeaderPattern = new(
        @"^WEBVTT\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool CanHandle(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // Look for WEBVTT header or VTT-style timestamps
        return VttHeaderPattern.IsMatch(content) || VttTimePattern.IsMatch(content);
    }

    public async Task<SubtitleParsingResult> ParseContentAsync(string content, CancellationToken cancellationToken = default)
    {
        var result = new SubtitleParsingResult
        {
            Format = SubtitleFormat.VTT,
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
            bool pastHeader = false;

            for (int i = 0; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = lines[i];

                // Skip WEBVTT header and metadata
                if (!pastHeader)
                {
                    if (VttHeaderPattern.IsMatch(line))
                    {
                        pastHeader = true;
                        // Store header metadata
                        if (line.Length > 6) // "WEBVTT" + space + metadata
                        {
                            result.FormatMetadata["Header"] = line[6..].Trim();
                        }
                        continue;
                    }
                    if (line.StartsWith("NOTE") || line.StartsWith("STYLE") || line.StartsWith("REGION"))
                    {
                        continue;
                    }
                }

                // Look for timestamp line
                var timeMatch = VttTimePattern.Match(line);
                if (!timeMatch.Success)
                    continue;

                // Parse start and end times
                var startMs = ParseVttTimestamp(
                    timeMatch.Groups[1].Value, timeMatch.Groups[2].Value,
                    timeMatch.Groups[3].Value, timeMatch.Groups[4].Value);
                var endMs = ParseVttTimestamp(
                    timeMatch.Groups[5].Value, timeMatch.Groups[6].Value,
                    timeMatch.Groups[7].Value, timeMatch.Groups[8].Value);

                // Collect text lines until next timestamp or end
                var textLines = new List<string>();
                for (int j = i + 1; j < lines.Length; j++)
                {
                    if (VttTimePattern.IsMatch(lines[j]))
                        break;
                    
                    // Skip cue identifier lines (lines that don't contain text)
                    if (lines[j].Contains("-->") || 
                        lines[j].StartsWith("NOTE") || 
                        lines[j].StartsWith("STYLE") ||
                        lines[j].StartsWith("REGION"))
                        break;
                        
                    textLines.Add(lines[j]);
                }

                if (textLines.Count > 0)
                {
                    var text = string.Join("\n", textLines);
                    
                    // Remove VTT formatting tags
                    text = RemoveVttFormatting(text);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var entry = new SubtitleEntry
                        {
                            Index = entryIndex++,
                            StartTimeMs = startMs,
                            EndTimeMs = endMs,
                            Text = text.Trim()
                        };

                        // Parse and store positioning/styling info from the timestamp line
                        var timestampLine = line;
                        if (timestampLine.Contains(" "))
                        {
                            var stylePart = timestampLine[(timestampLine.IndexOf("-->") + 3)..].Trim();
                            if (!string.IsNullOrEmpty(stylePart) && stylePart.Contains(' '))
                            {
                                entry.Styling["Position"] = stylePart;
                            }
                        }

                        entries.Add(entry);
                    }
                }

                // Move index past the text lines we just processed
                i += textLines.Count;
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
            result.ErrorMessage = $"Failed to parse VTT content: {ex.Message}";
        }
        finally
        {
            result.ParsingTimeMs = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
        }

        return result;
    }

    private static long ParseVttTimestamp(string hours, string minutes, string seconds, string milliseconds)
    {
        return (long.Parse(hours) * 3600 + long.Parse(minutes) * 60 + long.Parse(seconds)) * 1000 + long.Parse(milliseconds);
    }

    private static string RemoveVttFormatting(string text)
    {
        // Remove VTT formatting tags like <c>, <i>, <b>, <u>, etc.
        var cleanText = Regex.Replace(text, @"<[^>]*>", string.Empty);
        
        // Remove VTT cue settings that might leak into text
        cleanText = Regex.Replace(cleanText, @"\b(align|line|position|size|vertical):[^\s]+", string.Empty);
        
        return cleanText.Trim();
    }
}
