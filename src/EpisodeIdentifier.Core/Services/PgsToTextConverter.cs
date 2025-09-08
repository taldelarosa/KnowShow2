using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace EpisodeIdentifier.Core.Services;

public class PgsToTextConverter
{
    private readonly ILogger<PgsToTextConverter> _logger;

    public PgsToTextConverter(ILogger<PgsToTextConverter> logger)
    {
        _logger = logger;
    }

    public async Task<string> ConvertPgsToText(byte[] pgsData, string language = "eng")
    {
        _logger.LogInformation("Converting PGS subtitle data to text using OCR, size: {Size} bytes", pgsData.Length);

        if (pgsData.Length == 0)
        {
            return string.Empty;
        }

        // Save PGS data to temporary .sup file for processing
        var tempPgsFile = Path.GetTempFileName() + ".sup";
        var tempImagesDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        
        try
        {
            await File.WriteAllBytesAsync(tempPgsFile, pgsData);
            Directory.CreateDirectory(tempImagesDir);

            // Extract images from PGS data using ffmpeg
            var imageFiles = await ExtractImagesFromPgsFile(tempPgsFile, tempImagesDir);
            
            if (!imageFiles.Any())
            {
                _logger.LogWarning("No images could be extracted from PGS subtitle data for OCR");
                return string.Empty;
            }

            // OCR each image and build SRT format text
            var srtBuilder = new StringBuilder();
            var subtitleIndex = 1;
            
            foreach (var imageFile in imageFiles.OrderBy(f => f))
            {
                var text = await ExtractTextFromImage(imageFile, language);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Create basic timestamps (in production, these would come from PGS timing data)
                    var startTime = TimeSpan.FromSeconds((subtitleIndex - 1) * 3);
                    var endTime = TimeSpan.FromSeconds(subtitleIndex * 3);
                    
                    srtBuilder.AppendLine(subtitleIndex.ToString());
                    srtBuilder.AppendLine($"{FormatSrtTime(startTime)} --> {FormatSrtTime(endTime)}");
                    srtBuilder.AppendLine(text.Trim());
                    srtBuilder.AppendLine();
                    
                    subtitleIndex++;
                }
            }

            var result = srtBuilder.ToString().Trim();
            _logger.LogInformation("OCR extracted text from {ImageCount} PGS images, total SRT length: {TextLength}", 
                imageFiles.Count, result.Length);

            return result;
        }
        finally
        {
            // Cleanup temporary files
            try
            {
                if (File.Exists(tempPgsFile))
                    File.Delete(tempPgsFile);
                
                if (Directory.Exists(tempImagesDir))
                    Directory.Delete(tempImagesDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary files");
            }
        }
    }

    public async Task<string> ConvertPgsFromVideoToText(string videoPath, int subtitleTrackIndex, string language = "eng")
    {
        _logger.LogInformation("Converting PGS subtitles directly from video: {VideoPath}, track {TrackIndex}", videoPath, subtitleTrackIndex);
        
        var tempImagesDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempImagesDir);

            // Extract images directly from video file using ffmpeg
            var imageFiles = await ExtractImagesFromVideoSubtitleTrack(videoPath, subtitleTrackIndex, tempImagesDir);
            
            if (!imageFiles.Any())
            {
                _logger.LogWarning("No images extracted from video PGS subtitles");
                return string.Empty;
            }

            // OCR each image and build SRT format text
            var srtBuilder = new StringBuilder();
            var subtitleIndex = 1;
            
            foreach (var imageFile in imageFiles.OrderBy(f => f))
            {
                var text = await ExtractTextFromImage(imageFile, language);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Create timestamps based on image sequence
                    var startTime = TimeSpan.FromSeconds((subtitleIndex - 1) * 3);
                    var endTime = TimeSpan.FromSeconds(subtitleIndex * 3);
                    
                    srtBuilder.AppendLine(subtitleIndex.ToString());
                    srtBuilder.AppendLine($"{FormatSrtTime(startTime)} --> {FormatSrtTime(endTime)}");
                    srtBuilder.AppendLine(text.Trim());
                    srtBuilder.AppendLine();
                    
                    subtitleIndex++;
                }
            }

            var result = srtBuilder.ToString().Trim();
            _logger.LogInformation("Extracted text from {ImageCount} images, total SRT length: {TextLength}", 
                imageFiles.Count, result.Length);

            return result;
        }
        finally
        {
            // Cleanup temporary files
            try
            {
                if (Directory.Exists(tempImagesDir))
                    Directory.Delete(tempImagesDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary files");
            }
        }
    }

    private async Task<List<string>> ExtractImagesFromPgs(string pgsFile, string outputDir)
    {
        // This method is deprecated - use ExtractImagesFromPgsFile instead
        return await ExtractImagesFromPgsFile(pgsFile, outputDir);
    }

    private async Task<List<string>> ExtractImagesFromPgsFile(string pgsFile, string outputDir)
    {
        var imageFiles = new List<string>();

        try
        {
            // Use ffmpeg to extract subtitle images from SUP file
            // -f image2 forces image output format
            // -vf "scale=iw*2:ih*2" upscales for better OCR results
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{pgsFile}\" -vf \"scale=iw*2:ih*2\" -y \"{outputDir}/subtitle_%06d.png\"",
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
                // Find all generated PNG files
                var pngFiles = Directory.GetFiles(outputDir, "subtitle_*.png")
                    .OrderBy(f => f)
                    .ToList();
                
                imageFiles.AddRange(pngFiles);
                _logger.LogInformation("Extracted {Count} images from PGS file using ffmpeg", pngFiles.Count);
            }
            else
            {
                _logger.LogWarning("ffmpeg failed to extract images from PGS: {Error}", error);
                
                // Alternative approach: Try using ffmpeg with different options
                await TryAlternativePgsExtraction(pgsFile, outputDir, imageFiles);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract images from PGS file: {PgsFile}", pgsFile);
        }

        return imageFiles;
    }

    private async Task TryAlternativePgsExtraction(string pgsFile, string outputDir, List<string> imageFiles)
    {
        try
        {
            // Alternative method: Use simpler ffmpeg approach
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{pgsFile}\" -f image2 \"{outputDir}/sub_%04d.png\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var pngFiles = Directory.GetFiles(outputDir, "sub_*.png")
                    .OrderBy(f => f)
                    .ToList();
                
                imageFiles.AddRange(pngFiles);
                _logger.LogInformation("Extracted {Count} images using alternative method", pngFiles.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alternative PGS extraction also failed");
        }
    }

    private async Task<List<string>> ExtractImagesFromVideo(string videoPath, int trackIndex, string outputDir)
    {
        var imageFiles = new List<string>();

        try
        {
            // Extract subtitle images directly from video file using ffmpeg
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{videoPath}\" -map 0:s:{trackIndex} -c:s copy -f segment -segment_time 0.1 \"{outputDir}/subtitle_%04d.png\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                // Find all generated PNG files
                var pngFiles = Directory.GetFiles(outputDir, "subtitle_*.png")
                    .OrderBy(f => f)
                    .ToList();
                
                imageFiles.AddRange(pngFiles);
                _logger.LogInformation("Extracted {Count} images from video subtitle track", pngFiles.Count);
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("ffmpeg failed to extract images from video: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract images from video subtitle track");
        }

        return imageFiles;
    }

    private async Task<string> ExtractTextFromImage(string imagePath, string language)
    {
        try
        {
            // Use tesseract for OCR with optimized settings for subtitle text
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tesseract",
                    Arguments = $"\"{imagePath}\" stdout -l {language} --psm 8 -c tessedit_char_whitelist=ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?':;-()[] ",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var text = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var cleanText = text.Trim();
                if (!string.IsNullOrWhiteSpace(cleanText))
                {
                    _logger.LogDebug("OCR extracted text from {ImagePath}: {Text}", imagePath, cleanText);
                    return cleanText;
                }
            }
            else
            {
                _logger.LogWarning("Tesseract failed for {ImagePath}: {Error}", imagePath, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from image {ImagePath}", imagePath);
        }
        
        return string.Empty;
    }

    private static string FormatSrtTime(TimeSpan time)
    {
        return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
    }

    private async Task<List<string>> ExtractImagesFromVideoSubtitleTrack(string videoPath, int trackIndex, string outputDir)
    {
        var imageFiles = new List<string>();

        try
        {
            // Extract subtitle images directly from video file using ffmpeg
            // This extracts PGS subtitles as individual images
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{videoPath}\" -map 0:s:{trackIndex} -c:s png -f image2 \"{outputDir}/subtitle_%06d.png\"",
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
                // Find all generated PNG files
                var pngFiles = Directory.GetFiles(outputDir, "subtitle_*.png")
                    .OrderBy(f => f)
                    .ToList();
                
                imageFiles.AddRange(pngFiles);
                _logger.LogInformation("Extracted {Count} images from video subtitle track {TrackIndex}", pngFiles.Count, trackIndex);
            }
            else
            {
                _logger.LogWarning("ffmpeg failed to extract images from video track {TrackIndex}: {Error}", trackIndex, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract images from video subtitle track: {VideoPath}:{TrackIndex}", videoPath, trackIndex);
        }

        return imageFiles;
    }

    public bool IsOcrAvailable()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tesseract",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
