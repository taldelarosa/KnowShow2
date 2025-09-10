using Microsoft.Extensions.Logging;
using System.Diagnostics;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for extracting text-based subtitles directly from video files.
/// </summary>
public class VideoTextSubtitleExtractor
{
    private readonly ILogger<VideoTextSubtitleExtractor> _logger;

    public VideoTextSubtitleExtractor(ILogger<VideoTextSubtitleExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Extracts text subtitle content directly from a video file using ffmpeg.
    /// </summary>
    /// <param name="videoFilePath">Path to the video file</param>
    /// <param name="subtitleTrackIndex">Index of the subtitle track to extract</param>
    /// <param name="preferredLanguage">Preferred language for subtitle selection</param>
    /// <returns>Raw subtitle text content</returns>
    public async Task<string?> ExtractTextSubtitleFromVideo(
        string videoFilePath,
        int subtitleTrackIndex,
        string? preferredLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
            throw new ArgumentException("Video file path cannot be null or empty.", nameof(videoFilePath));

        if (!File.Exists(videoFilePath))
            throw new FileNotFoundException($"Video file not found: {videoFilePath}");

        _logger.LogInformation("Extracting text subtitle from {VideoFile}, track index: {TrackIndex}",
            videoFilePath, subtitleTrackIndex);

        try
        {
            // Create temporary file for subtitle extraction
            var tempSubtitleFile = Path.GetTempFileName() + ".srt";

            try
            {
                // Use ffmpeg to extract subtitle track to SRT format
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = $"-i \"{videoFilePath}\" -map 0:{subtitleTrackIndex} -c:s srt -y \"{tempSubtitleFile}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                _logger.LogDebug("Running ffmpeg command: {Command}", process.StartInfo.Arguments);

                process.Start();
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("ffmpeg subtitle extraction failed with exit code {ExitCode}: {Error}",
                        process.ExitCode, stderr);
                    return null;
                }

                if (!File.Exists(tempSubtitleFile))
                {
                    _logger.LogWarning("Subtitle file was not created: {TempFile}", tempSubtitleFile);
                    return null;
                }

                // Read the extracted subtitle content
                var subtitleContent = await File.ReadAllTextAsync(tempSubtitleFile);

                if (string.IsNullOrWhiteSpace(subtitleContent))
                {
                    _logger.LogWarning("Extracted subtitle content is empty");
                    return null;
                }

                _logger.LogInformation("Successfully extracted {Length} characters from text subtitle track",
                    subtitleContent.Length);

                // Clean the subtitle text
                return CleanSubtitleText(subtitleContent);
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempSubtitleFile))
                {
                    try
                    {
                        File.Delete(tempSubtitleFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary subtitle file: {TempFile}", tempSubtitleFile);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text subtitle from {VideoFile}", videoFilePath);
            return null;
        }
    }

    /// <summary>
    /// Cleans subtitle text by removing timestamps, sequence numbers, and formatting tags.
    /// </summary>
    private string CleanSubtitleText(string rawSubtitleText)
    {
        if (string.IsNullOrWhiteSpace(rawSubtitleText))
            return string.Empty;

        var lines = rawSubtitleText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var textLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip sequence numbers
            if (int.TryParse(trimmedLine, out _))
                continue;

            // Skip timestamp lines (SRT format: HH:MM:SS,mmm --> HH:MM:SS,mmm)
            if (trimmedLine.Contains("-->"))
                continue;

            // Skip VTT headers
            if (trimmedLine.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            // Remove common subtitle formatting tags
            var cleanedLine = trimmedLine
                .Replace("<i>", "")
                .Replace("</i>", "")
                .Replace("<b>", "")
                .Replace("</b>", "")
                .Replace("<u>", "")
                .Replace("</u>", "")
                .Replace("<font ", "")
                .Replace("</font>", "")
                .Trim();

            // Remove any remaining HTML-like tags
            cleanedLine = System.Text.RegularExpressions.Regex.Replace(cleanedLine, @"<[^>]*>", "");

            if (!string.IsNullOrWhiteSpace(cleanedLine))
            {
                textLines.Add(cleanedLine);
            }
        }

        var result = string.Join(" ", textLines);
        _logger.LogDebug("Cleaned subtitle text: {Length} characters", result.Length);

        return result;
    }
}
