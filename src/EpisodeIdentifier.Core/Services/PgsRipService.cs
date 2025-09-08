using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Extension service for PGS conversion using pgsrip when available
/// This provides better accuracy and timing than the built-in method
/// </summary>
public class PgsRipService
{
    private readonly ILogger<PgsRipService> _logger;
    private bool? _isAvailable;

    public PgsRipService(ILogger<PgsRipService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if pgsrip is available on the system
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        if (_isAvailable.HasValue)
            return _isAvailable.Value;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pgsrip",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            
            _isAvailable = process.ExitCode == 0;
            
            if (_isAvailable.Value)
            {
                _logger.LogInformation("pgsrip is available and ready to use");
            }
            else
            {
                _logger.LogWarning("pgsrip is not available - install with: pip install pgsrip");
            }
            
            return _isAvailable.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check pgsrip availability");
            _isAvailable = false;
            return false;
        }
    }

    /// <summary>
    /// Convert PGS subtitle data to SRT format using pgsrip
    /// </summary>
    /// <param name="pgsData">PGS/SUP data as byte array</param>
    /// <param name="language">Language code (e.g., "eng", "deu", "fra")</param>
    /// <returns>SRT format text with accurate timing</returns>
    public async Task<string> ConvertPgsToSrtAsync(byte[] pgsData, string language = "eng")
    {
        if (!await IsAvailableAsync())
        {
            throw new InvalidOperationException("pgsrip is not available on this system");
        }

        if (pgsData.Length == 0)
        {
            return string.Empty;
        }

        // Create temporary files
        var tempSupFile = Path.GetTempFileName() + ".sup";
        var expectedSrtFile = Path.ChangeExtension(tempSupFile, ".srt");
        
        try
        {
            // Write PGS data to SUP file
            await File.WriteAllBytesAsync(tempSupFile, pgsData);
            _logger.LogDebug("Created temporary SUP file: {SupFile}", tempSupFile);

            // Run pgsrip
            var success = await RunPgsRipAsync(tempSupFile, language);
            
            if (success && File.Exists(expectedSrtFile))
            {
                var srtContent = await File.ReadAllTextAsync(expectedSrtFile);
                _logger.LogInformation("pgsrip converted {DataSize} bytes to {SrtLength} characters", 
                    pgsData.Length, srtContent.Length);
                return srtContent;
            }
            else
            {
                _logger.LogWarning("pgsrip failed to generate SRT file");
                return string.Empty;
            }
        }
        finally
        {
            // Cleanup temporary files
            try
            {
                if (File.Exists(tempSupFile))
                    File.Delete(tempSupFile);
                if (File.Exists(expectedSrtFile))
                    File.Delete(expectedSrtFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary files");
            }
        }
    }

    /// <summary>
    /// Convert PGS subtitles directly from video file using pgsrip
    /// </summary>
    /// <param name="videoPath">Path to video file (.mkv, .mks)</param>
    /// <param name="language">Language code for OCR</param>
    /// <returns>SRT content from extracted subtitles</returns>
    public async Task<string> ConvertVideoSubtitlesAsync(string videoPath, string language = "eng")
    {
        if (!await IsAvailableAsync())
        {
            throw new InvalidOperationException("pgsrip is not available on this system");
        }

        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException($"Video file not found: {videoPath}");
        }

        var videoDir = Path.GetDirectoryName(videoPath)!;
        var videoName = Path.GetFileNameWithoutExtension(videoPath);
        
        try
        {
            // Run pgsrip on the video file
            var success = await RunPgsRipAsync(videoPath, language);
            
            if (success)
            {
                // Look for generated SRT files
                var srtFiles = Directory.GetFiles(videoDir, $"{videoName}*.srt");
                
                if (srtFiles.Length > 0)
                {
                    // Combine all SRT files or return the first one
                    var srtContent = await File.ReadAllTextAsync(srtFiles[0]);
                    _logger.LogInformation("pgsrip extracted subtitles from {VideoPath}: {SrtFiles} files, {ContentLength} characters", 
                        videoPath, srtFiles.Length, srtContent.Length);
                    return srtContent;
                }
                else
                {
                    _logger.LogWarning("pgsrip completed but no SRT files were generated");
                    return string.Empty;
                }
            }
            else
            {
                _logger.LogWarning("pgsrip failed to process video file");
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video with pgsrip");
            return string.Empty;
        }
    }

    /// <summary>
    /// Run pgsrip command on a file
    /// </summary>
    private async Task<bool> RunPgsRipAsync(string inputFile, string language)
    {
        try
        {
            var arguments = new List<string>
            {
                "--language", language,
                "--force", // Overwrite existing files
                "--max-workers", "1", // Single threaded for stability
                $"\"{inputFile}\""
            };

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pgsrip",
                    Arguments = string.Join(" ", arguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(inputFile)
                }
            };

            _logger.LogDebug("Running pgsrip: {Command} {Arguments}", process.StartInfo.FileName, process.StartInfo.Arguments);

            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogDebug("pgsrip completed successfully: {Output}", output);
                return true;
            }
            else
            {
                _logger.LogWarning("pgsrip failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run pgsrip command");
            return false;
        }
    }

    /// <summary>
    /// Get information about pgsrip installation
    /// </summary>
    public async Task<string> GetVersionInfoAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pgsrip",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output.Trim() : "pgsrip not available";
        }
        catch
        {
            return "pgsrip not found";
        }
    }

    /// <summary>
    /// Install pgsrip using pip (requires Python)
    /// </summary>
    public async Task<bool> TryInstallAsync()
    {
        try
        {
            _logger.LogInformation("Attempting to install pgsrip via pip...");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pip",
                    Arguments = "install pgsrip",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("pgsrip installed successfully");
                _isAvailable = null; // Reset cache
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to install pgsrip: {Error}", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing pgsrip");
            return false;
        }
    }
}
