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
        _logger.LogInformation("Converting PGS subtitle data to text, size: {Size} bytes", pgsData.Length);

        if (pgsData.Length == 0)
        {
            return string.Empty;
        }

        // Save PGS data to temporary file
        var tempPgsFile = Path.GetTempFileName() + ".sup";
        var tempImagesDir = Path.GetTempPath() + Path.DirectorySeparatorChar + Guid.NewGuid().ToString();
        
        try
        {
            await File.WriteAllBytesAsync(tempPgsFile, pgsData);
            Directory.CreateDirectory(tempImagesDir);

            // Step 1: Extract images from PGS using BDSup2Sub or similar tool
            var imageFiles = await ExtractImagesFromPgs(tempPgsFile, tempImagesDir);
            
            if (!imageFiles.Any())
            {
                _logger.LogWarning("No images extracted from PGS subtitle data");
                return string.Empty;
            }

            // Step 2: OCR each image and combine text
            var extractedTexts = new List<string>();
            foreach (var imageFile in imageFiles)
            {
                var text = await ExtractTextFromImage(imageFile, language);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    extractedTexts.Add(text.Trim());
                }
            }

            var combinedText = string.Join(" ", extractedTexts);
            _logger.LogInformation("Extracted text from {ImageCount} images, total length: {TextLength}", 
                imageFiles.Count, combinedText.Length);

            return combinedText;
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

    private async Task<List<string>> ExtractImagesFromPgs(string pgsFile, string outputDir)
    {
        var imageFiles = new List<string>();

        try
        {
            // Try using ffmpeg to extract subtitle images
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{pgsFile}\" -y \"{outputDir}/subtitle_%04d.png\"",
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
                _logger.LogInformation("Extracted {Count} images using ffmpeg", pngFiles.Count);
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("ffmpeg failed to extract images: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract images from PGS file");
        }

        return imageFiles;
    }

    private async Task<string> ExtractTextFromImage(string imagePath, string language)
    {
        try
        {
            // Use tesseract for OCR
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tesseract",
                    Arguments = $"\"{imagePath}\" stdout -l {language} --psm 6",
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
                return text;
            }
            else
            {
                _logger.LogWarning("Tesseract failed for {ImagePath}: {Error}", imagePath, error);
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from image {ImagePath}", imagePath);
            return string.Empty;
        }
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
