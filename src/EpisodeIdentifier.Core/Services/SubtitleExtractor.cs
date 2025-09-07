using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EpisodeIdentifier.Core.Services;

public class SubtitleExtractor
{
    private readonly ILogger<SubtitleExtractor> _logger;

    public SubtitleExtractor(ILogger<SubtitleExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> ExtractPgsSubtitles(string videoPath)
    {
        _logger.LogInformation("Extracting PGS subtitles from {VideoPath}", videoPath);

        // First try mkvextract
        try
        {
            var result = await ExtractWithMkvextract(videoPath);
            if (result.Length > 0)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract subtitles with mkvextract, falling back to ffmpeg");
        }

        // Fallback to ffmpeg
        return await ExtractWithFfmpeg(videoPath);
    }

    private async Task<byte[]> ExtractWithMkvextract(string videoPath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "mkvextract",
                Arguments = $"tracks \"{videoPath}\" 0:subtitles.sup",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0 && File.Exists("subtitles.sup"))
        {
            var content = await File.ReadAllBytesAsync("subtitles.sup");
            File.Delete("subtitles.sup");
            return content;
        }

        throw new Exception($"mkvextract failed with exit code {process.ExitCode}");
    }

    private async Task<byte[]> ExtractWithFfmpeg(string videoPath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{videoPath}\" -map 0:s:0 subtitles.sup",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0 && File.Exists("subtitles.sup"))
        {
            var content = await File.ReadAllBytesAsync("subtitles.sup");
            File.Delete("subtitles.sup");
            return content;
        }

        throw new Exception($"ffmpeg failed with exit code {process.ExitCode}");
    }
}
