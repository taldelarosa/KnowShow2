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

        // Create temporary files in a temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"pgsrip_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempSupFile = Path.Combine(tempDir, "subtitle.sup");

        try
        {
            // Write PGS data to SUP file
            await File.WriteAllBytesAsync(tempSupFile, pgsData);
            _logger.LogDebug("Created temporary SUP file: {SupFile}", tempSupFile);

            // Run pgsrip with output to the same temp directory
            var success = await RunPgsRipAsync(tempSupFile, language, tempDir);

            if (success)
            {
                // Look for any SRT file in the temp directory
                var srtFiles = Directory.GetFiles(tempDir, "*.srt");
                if (srtFiles.Length > 0)
                {
                    var srtContent = await File.ReadAllTextAsync(srtFiles[0]);
                    _logger.LogInformation("pgsrip converted {DataSize} bytes to {SrtLength} characters",
                        pgsData.Length, srtContent.Length);
                    return srtContent;
                }
            }
            
            _logger.LogWarning("pgsrip failed to generate SRT file");
            return string.Empty;
        }
        finally
        {
            // Cleanup temporary directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary directory: {TempDir}", tempDir);
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

        // Create a temp directory for pgsrip to avoid permission issues and special character problems
        // pgsrip writes output files in the same directory as the input, so we create a symlink
        var tempDir = Path.Combine(Path.GetTempPath(), $"pgsrip_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        // Create a simple filename for the symlink to avoid special character issues
        var tempVideoLink = Path.Combine(tempDir, "video.mkv");

        try
        {
            // Create a symbolic link to the video file
            // This avoids permission issues and special characters in the path
            try
            {
                // On Linux/Unix, create a symlink
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    var symlinkProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "ln",
                            Arguments = $"-s \"{videoPath}\" \"{tempVideoLink}\"",
                            RedirectStandardError = true,
                            UseShellExecute = false
                        }
                    };
                    symlinkProcess.Start();
                    await symlinkProcess.WaitForExitAsync();
                    
                    if (symlinkProcess.ExitCode != 0)
                    {
                        _logger.LogWarning("Failed to create symlink, will try copying file instead");
                        throw new IOException("Symlink creation failed");
                    }
                }
                else
                {
                    // On Windows or if symlink fails, copy the file (slower but works)
                    throw new IOException("Not Linux, will copy");
                }
            }
            catch
            {
                // If symlink fails, copy the file (slower but more compatible)
                _logger.LogInformation("Creating temporary copy of video file for pgsrip processing");
                File.Copy(videoPath, tempVideoLink, true);
            }

            // Run pgsrip on the temp video link
            var success = await RunPgsRipAsync(tempVideoLink, language, tempDir);

            if (success)
            {
                _logger.LogInformation("pgsrip reported success. Searching for output files in temp dir: {TempDir}", tempDir);
                
                // List all SRT files in the temp directory for debugging
                var allSrtInDir = Directory.GetFiles(tempDir, "*.srt");
                _logger.LogInformation("Found {Count} total SRT files in temp directory: {Files}", 
                    allSrtInDir.Length, 
                    string.Join(", ", allSrtInDir.Select(f => Path.GetFileName(f))));
                
                // Get any SRT file from temp directory
                var srtFiles = allSrtInDir;

                if (srtFiles.Length > 0)
                {
                    // Read the first SRT file
                    var srtContent = await File.ReadAllTextAsync(srtFiles[0]);
                    _logger.LogInformation("pgsrip extracted subtitles from {VideoPath}: {SrtFiles} files, {ContentLength} characters",
                        videoPath, srtFiles.Length, srtContent.Length);
                    return srtContent;
                }
                else
                {
                    _logger.LogWarning("pgsrip completed but no SRT files were generated in temp directory {TempDir}", tempDir);
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
        finally
        {
            // Clean up temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp directory: {TempDir}", tempDir);
            }
        }
    }

    /// <summary>
    /// Run pgsrip command on a file
    /// </summary>
    /// <param name="inputFile">Path to the input video file</param>
    /// <param name="language">Language code for OCR</param>
    /// <param name="workingDir">Directory where pgsrip will write output files (same as input file location)</param>
    private async Task<bool> RunPgsRipAsync(string inputFile, string language, string workingDir)
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

            _logger.LogInformation("About to run pgsrip: InputFile={InputFile}, Language={Language}, WorkingDir={WorkingDir}", 
                inputFile, language, workingDir);

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
                    WorkingDirectory = workingDir // pgsrip writes output in same dir as input
                }
            };

            _logger.LogInformation("Running pgsrip command: {Command} {Arguments}", process.StartInfo.FileName, process.StartInfo.Arguments);

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("pgsrip completed successfully. Output: {Output}, Error: {Error}", output, error);
                return true;
            }
            else
            {
                _logger.LogWarning("pgsrip failed with exit code {ExitCode}. Output: {Output}, Error: {Error}", process.ExitCode, output, error);
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
