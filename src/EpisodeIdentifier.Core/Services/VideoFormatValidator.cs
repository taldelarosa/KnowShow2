using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using EpisodeIdentifier.Core.Models;
using System.IO.Abstractions;

namespace EpisodeIdentifier.Core.Services;

public class VideoFormatValidator
{
    private readonly ILogger<VideoFormatValidator> _logger;
    private readonly IFileSystem _fileSystem;

    public VideoFormatValidator(ILogger<VideoFormatValidator> logger, IFileSystem fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    // Backward-compatible constructor for callers not using DI or IFileSystem
    public VideoFormatValidator(ILogger<VideoFormatValidator> logger)
        : this(logger, new FileSystem())
    {
    }

    public async Task<bool> IsAV1Encoded(string videoPath)
    {
        _logger.LogInformation("Validating AV1 encoding for {VideoPath}", videoPath);

        // Check if file exists first
        if (!_fileSystem.File.Exists(videoPath))
        {
            _logger.LogWarning("Video file not found: {VideoPath}", videoPath);
            return false;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -print_format json -show_streams \"{videoPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe failed with exit code {ExitCode}", process.ExitCode);
                return false;
            }

            using var document = JsonDocument.Parse(output);
            var streams = document.RootElement.GetProperty("streams");

            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.TryGetProperty("codec_type", out var codecType) &&
                    codecType.GetString() == "video" &&
                    stream.TryGetProperty("codec_name", out var codecName))
                {
                    var codec = codecName.GetString();
                    _logger.LogInformation("Found video codec: {Codec}", codec);
                    return codec == "av01" || codec == "libaom-av1" || codec == "av1";
                }
            }

            return false;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogWarning("ffprobe not found. Please install FFmpeg to enable video format validation.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating AV1 encoding for {VideoPath}", videoPath);
            return false;
        }
    }

    public async Task<List<SubtitleTrackInfo>> GetSubtitleTracks(string videoPath)
    {
        _logger.LogInformation("Getting subtitle tracks for {VideoPath}", videoPath);

        // Check if file exists first
        if (!_fileSystem.File.Exists(videoPath))
        {
            _logger.LogWarning("Video file not found: {VideoPath}", videoPath);
            return new List<SubtitleTrackInfo>();
        }

        var tracks = new List<SubtitleTrackInfo>();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -print_format json -show_streams -select_streams s \"{videoPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe failed with exit code {ExitCode}", process.ExitCode);
                return tracks;
            }

            using var document = JsonDocument.Parse(output);
            var streams = document.RootElement.GetProperty("streams");

            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.TryGetProperty("codec_name", out var codecName))
                {
                    var codecNameStr = codecName.GetString();

                    // Check for both PGS and text-based subtitle codecs
                    if (codecNameStr == "hdmv_pgs_subtitle" ||
                        codecNameStr == "subrip" ||
                        codecNameStr == "ass" ||
                        codecNameStr == "webvtt" ||
                        codecNameStr == "mov_text" ||
                        codecNameStr == "srt")
                    {
                        var track = new SubtitleTrackInfo
                        {
                            Index = stream.GetProperty("index").GetInt32(),
                            CodecName = codecNameStr!
                        };

                        if (stream.TryGetProperty("tags", out var tags))
                        {
                            if (tags.TryGetProperty("language", out var language))
                            {
                                track.Language = language.GetString();
                            }
                            if (tags.TryGetProperty("title", out var title))
                            {
                                track.Title = title.GetString();
                            }
                        }

                        tracks.Add(track);
                        _logger.LogInformation("Found subtitle track: Index={Index}, Codec={Codec}, Language={Language}, Title={Title}",
                            track.Index, track.CodecName, track.Language ?? "unknown", track.Title ?? "untitled");
                    }
                }
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogWarning("ffprobe not found. Please install FFmpeg to enable subtitle track detection.");
            return new List<SubtitleTrackInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subtitle tracks for {VideoPath}", videoPath);
        }

        return tracks;
    }
}
