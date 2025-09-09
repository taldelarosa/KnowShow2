using Microsoft.Extensions.Logging;
using System.Diagnostics;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Core.Services;

public class SubtitleExtractor : ISubtitleExtractor
{
    private readonly ILogger<SubtitleExtractor> _logger;
    private readonly VideoFormatValidator _validator;

    public SubtitleExtractor(ILogger<SubtitleExtractor> logger, VideoFormatValidator validator)
    {
        _logger = logger;
        _validator = validator;
    }

    public async Task<byte[]> ExtractPgsSubtitles(string videoPath, string? preferredLanguage = null)
    {
        _logger.LogInformation("Extracting PGS subtitles from {VideoPath}, preferred language: {Language}", 
            videoPath, preferredLanguage ?? "any");

        // Get available subtitle tracks
        var subtitleTracks = await _validator.GetSubtitleTracks(videoPath);
        if (!subtitleTracks.Any())
        {
            _logger.LogWarning("No PGS subtitle tracks found in {VideoPath}", videoPath);
            return Array.Empty<byte>();
        }

        // Select the best track based on language preference
        var selectedTrack = SelectBestTrack(subtitleTracks, preferredLanguage);
        _logger.LogInformation("Selected subtitle track: Index={Index}, Language={Language}", 
            selectedTrack.Index, selectedTrack.Language ?? "unknown");

        // First try mkvextract with specific track
        try
        {
            var result = await ExtractWithMkvextract(videoPath, selectedTrack.Index);
            if (result.Length > 0)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract subtitles with mkvextract, falling back to ffmpeg");
        }

        // Fallback to ffmpeg with specific stream
        return await ExtractWithFfmpeg(videoPath, selectedTrack.Index);
    }

    private SubtitleTrackInfo SelectBestTrack(List<SubtitleTrackInfo> tracks, string? preferredLanguage)
    {
        // If preferred language specified, try to find it
        if (!string.IsNullOrEmpty(preferredLanguage))
        {
            var langTrack = tracks.FirstOrDefault(t => 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (langTrack != null)
            {
                return langTrack;
            }
        }

        // Default preferences: English first, then first available
        var englishTrack = tracks.FirstOrDefault(t => 
            string.Equals(t.Language, "eng", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Language, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Language, "english", StringComparison.OrdinalIgnoreCase));
        
        return englishTrack ?? tracks.First();
    }

    private async Task<byte[]> ExtractWithMkvextract(string videoPath, int trackIndex)
    {
        // Get unique temporary filename
        var tempFile = Path.GetTempFileName() + ".sup";
        
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "mkvextract",
                    Arguments = $"tracks \"{videoPath}\" {trackIndex}:\"{tempFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(tempFile))
            {
                var content = await File.ReadAllBytesAsync(tempFile);
                _logger.LogInformation("Successfully extracted {Size} bytes using mkvextract", content.Length);
                return content;
            }
            else
            {
                _logger.LogWarning("mkvextract failed with exit code {ExitCode}: {Error}", process.ExitCode, stderr);
                return Array.Empty<byte>();
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {TempFile}", tempFile);
                }
            }
        }
    }

    private async Task<byte[]> ExtractWithFfmpeg(string videoPath, int streamIndex)
    {
        // Get unique temporary filename
        var tempFile = Path.GetTempFileName() + ".sup";
        
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{videoPath}\" -map 0:{streamIndex} -c copy -y \"{tempFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(tempFile))
            {
                var content = await File.ReadAllBytesAsync(tempFile);
                _logger.LogInformation("Successfully extracted {Size} bytes using ffmpeg", content.Length);
                return content;
            }
            else
            {
                _logger.LogWarning("ffmpeg failed with exit code {ExitCode}: {Error}", process.ExitCode, stderr);
                return Array.Empty<byte>();
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {TempFile}", tempFile);
                }
            }
        }
    }

    public async Task<string> ExtractAndConvertSubtitles(string videoPath, string? preferredLanguage = null)
    {
        _logger.LogInformation("Extracting and converting subtitles from {VideoPath}, preferred language: {Language}", 
            videoPath, preferredLanguage ?? "any");

        // Extract PGS subtitles
        var pgsData = await ExtractPgsSubtitles(videoPath, preferredLanguage);
        
        if (pgsData.Length == 0)
        {
            _logger.LogWarning("No PGS subtitle data extracted from {VideoPath}", videoPath);
            return string.Empty;
        }

        // TODO: Convert PGS data to text using OCR
        // For now, return a placeholder message
        _logger.LogInformation("Extracted {Size} bytes of PGS data from {VideoPath}", pgsData.Length, videoPath);
        return $"[PGS Subtitles extracted: {pgsData.Length} bytes]";
    }
}
