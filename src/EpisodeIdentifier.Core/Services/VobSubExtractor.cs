using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Extracts VobSub (.idx/.sub) files from MKV containers using mkvextract.
/// </summary>
public class VobSubExtractor : IVobSubExtractor
{
    private readonly ILogger<VobSubExtractor> _logger;
    private readonly IFileSystem _fileSystem;
    private const string MkvExtractTool = "mkvextract";

    public VobSubExtractor(ILogger<VobSubExtractor> logger, IFileSystem fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    public VobSubExtractor(ILogger<VobSubExtractor> logger)
        : this(logger, new FileSystem())
    {
    }

    /// <inheritdoc/>
    public async Task<VobSubExtractionResult> ExtractAsync(
        string videoPath,
        int trackIndex,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (videoPath == null)
        {
            throw new ArgumentNullException(nameof(videoPath));
        }

        if (outputDirectory == null)
        {
            throw new ArgumentNullException(nameof(outputDirectory));
        }

        if (trackIndex < 0)
        {
            throw new ArgumentException("Track index must be >= 0", nameof(trackIndex));
        }

        if (!_fileSystem.File.Exists(videoPath))
        {
            throw new ArgumentException($"Video file does not exist: {videoPath}", nameof(videoPath));
        }

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting VobSub extraction from {VideoPath}, track {TrackIndex}",
            videoPath, trackIndex);

        try
        {
            // Ensure output directory exists
            if (!_fileSystem.Directory.Exists(outputDirectory))
            {
                _fileSystem.Directory.CreateDirectory(outputDirectory);
            }

            // Generate output file paths (mkvextract will create .idx and .sub)
            var baseOutputPath = _fileSystem.Path.Combine(outputDirectory, "subtitle");
            var idxFilePath = $"{baseOutputPath}.idx";
            var subFilePath = $"{baseOutputPath}.sub";

            // Build mkvextract command
            // Format: mkvextract tracks "{videoPath}" {trackIndex}:{outputPath}
            var arguments = $"tracks \"{videoPath}\" {trackIndex}:\"{baseOutputPath}\"";

            _logger.LogDebug("Executing mkvextract with arguments: {Arguments}", arguments);

            var processResult = await RunProcessAsync(MkvExtractTool, arguments, cancellationToken);

            if (processResult.ExitCode == 0)
            {
                // Verify both files were created
                if (_fileSystem.File.Exists(idxFilePath) && _fileSystem.File.Exists(subFilePath))
                {
                    stopwatch.Stop();
                    _logger.LogInformation("VobSub extraction successful. Duration: {Duration}ms",
                        stopwatch.ElapsedMilliseconds);

                    return new VobSubExtractionResult
                    {
                        Success = true,
                        IdxFilePath = idxFilePath,
                        SubFilePath = subFilePath,
                        ExtractionDuration = stopwatch.Elapsed,
                        TrackIndex = trackIndex,
                        SourceVideoPath = videoPath
                    };
                }
                else
                {
                    stopwatch.Stop();
                    var errorMessage = $"mkvextract completed but output files not found. Expected: {idxFilePath} and {subFilePath}";
                    _logger.LogWarning(errorMessage);

                    return new VobSubExtractionResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage,
                        ExtractionDuration = stopwatch.Elapsed,
                        TrackIndex = trackIndex,
                        SourceVideoPath = videoPath
                    };
                }
            }
            else
            {
                stopwatch.Stop();
                var errorMessage = $"mkvextract exited with code {processResult.ExitCode}: {processResult.StandardError}";
                _logger.LogWarning("VobSub extraction failed: {ErrorMessage}", errorMessage);

                return new VobSubExtractionResult
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    ExtractionDuration = stopwatch.Elapsed,
                    TrackIndex = trackIndex,
                    SourceVideoPath = videoPath
                };
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("VobSub extraction cancelled after {Duration}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorMessage = $"Unexpected error during VobSub extraction: {ex.Message}";
            _logger.LogError(ex, "VobSub extraction failed");

            return new VobSubExtractionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ExtractionDuration = stopwatch.Elapsed,
                TrackIndex = trackIndex,
                SourceVideoPath = videoPath
            };
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsMkvExtractAvailableAsync()
    {
        try
        {
            var result = await RunProcessAsync(MkvExtractTool, "--version", CancellationToken.None);
            var isAvailable = result.ExitCode == 0;

            _logger.LogDebug("mkvextract availability check: {IsAvailable}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "mkvextract not available");
            return false;
        }
    }

    private async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                outputBuilder.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                errorBuilder.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for process to complete or cancellation
        await Task.Run(() =>
        {
            while (!process.WaitForExit(100))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }, cancellationToken);

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = outputBuilder.ToString(),
            StandardError = errorBuilder.ToString()
        };
    }

    private class ProcessResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
    }
}
