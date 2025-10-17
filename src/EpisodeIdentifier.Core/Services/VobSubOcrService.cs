using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Performs OCR on VobSub subtitle files using Tesseract.
/// </summary>
public class VobSubOcrService : IVobSubOcrService
{
    private readonly ILogger<VobSubOcrService> _logger;
    private readonly IFileSystem _fileSystem;
    private const string TesseractTool = "tesseract";

    // Language code mappings
    private static readonly Dictionary<string, string> LanguageCodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "eng", "eng" },
        { "english", "eng" },
        { "spa", "spa" },
        { "spanish", "spa" },
        { "fra", "fra" },
        { "french", "fra" },
        { "deu", "deu" },
        { "german", "deu" },
        { "ita", "ita" },
        { "italian", "ita" }
    };

    public VobSubOcrService(ILogger<VobSubOcrService> logger, IFileSystem fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    public VobSubOcrService(ILogger<VobSubOcrService> logger)
        : this(logger, new FileSystem())
    {
    }

    /// <inheritdoc/>
    public async Task<VobSubOcrResult> PerformOcrAsync(
        string idxFilePath,
        string subFilePath,
        string language,
        CancellationToken cancellationToken)
    {
        if (idxFilePath == null)
        {
            throw new ArgumentNullException(nameof(idxFilePath));
        }

        if (subFilePath == null)
        {
            throw new ArgumentNullException(nameof(subFilePath));
        }

        if (language == null)
        {
            throw new ArgumentNullException(nameof(language));
        }

        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language cannot be empty or whitespace", nameof(language));
        }

        if (!_fileSystem.File.Exists(idxFilePath))
        {
            throw new ArgumentException($".idx file does not exist: {idxFilePath}", nameof(idxFilePath));
        }

        if (!_fileSystem.File.Exists(subFilePath))
        {
            throw new ArgumentException($".sub file does not exist: {subFilePath}", nameof(subFilePath));
        }

        var stopwatch = Stopwatch.StartNew();
        var languageCode = GetOcrLanguageCode(language);

        _logger.LogInformation("Starting VobSub OCR for {IdxFile} with language {Language}",
            idxFilePath, languageCode);

        string? tempImageDir = null;

        try
        {
            // Step 1: Extract images from VobSub using ffmpeg
            tempImageDir = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), $"vobsub_ocr_{Guid.NewGuid()}");
            _fileSystem.Directory.CreateDirectory(tempImageDir);

            _logger.LogDebug("Extracting images to temporary directory: {TempDir}", tempImageDir);

            var imageFiles = await ExtractImagesFromVobSubAsync(idxFilePath, tempImageDir, cancellationToken);

            if (imageFiles.Count == 0)
            {
                stopwatch.Stop();
                _logger.LogInformation("No images extracted from VobSub. OCR complete with empty result.");

                return new VobSubOcrResult
                {
                    Success = true,
                    ExtractedText = string.Empty,
                    ConfidenceScore = 0.0,
                    CharacterCount = 0,
                    OcrDuration = stopwatch.Elapsed,
                    ImageCount = 0,
                    Language = languageCode
                };
            }

            _logger.LogInformation("Extracted {ImageCount} images from VobSub", imageFiles.Count);

            // Step 2: Perform OCR on each image
            var allText = new StringBuilder();
            double totalConfidence = 0.0;
            int totalCharacters = 0;

            foreach (var imageFile in imageFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ocrResult = await PerformTesseractOcrOnImageAsync(imageFile, languageCode, cancellationToken);
                if (!string.IsNullOrWhiteSpace(ocrResult.Text))
                {
                    allText.AppendLine(ocrResult.Text);
                    totalCharacters += ocrResult.Text.Length;
                    totalConfidence += ocrResult.Confidence * ocrResult.Text.Length; // Weight by text length
                }
            }

            stopwatch.Stop();

            var extractedText = NormalizeText(allText.ToString());
            var averageConfidence = totalCharacters > 0 ? totalConfidence / totalCharacters : 0.0;

            _logger.LogInformation("VobSub OCR complete. Extracted {CharCount} characters with {Confidence:P} confidence. Duration: {Duration}ms",
                totalCharacters, averageConfidence, stopwatch.ElapsedMilliseconds);

            return new VobSubOcrResult
            {
                Success = true,
                ExtractedText = extractedText,
                ConfidenceScore = averageConfidence,
                CharacterCount = totalCharacters,
                OcrDuration = stopwatch.Elapsed,
                ImageCount = imageFiles.Count,
                Language = languageCode
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("VobSub OCR cancelled after {Duration}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorMessage = $"Unexpected error during VobSub OCR: {ex.Message}";
            _logger.LogError(ex, "VobSub OCR failed");

            return new VobSubOcrResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                OcrDuration = stopwatch.Elapsed,
                ImageCount = 0,
                Language = languageCode
            };
        }
        finally
        {
            // Clean up temporary directory
            if (tempImageDir != null && _fileSystem.Directory.Exists(tempImageDir))
            {
                try
                {
                    _fileSystem.Directory.Delete(tempImageDir, recursive: true);
                    _logger.LogDebug("Cleaned up temporary directory: {TempDir}", tempImageDir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary directory: {TempDir}", tempImageDir);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsTesseractAvailableAsync()
    {
        try
        {
            var result = await RunProcessAsync(TesseractTool, "--version", CancellationToken.None);
            var isAvailable = result.ExitCode == 0;

            _logger.LogDebug("Tesseract availability check: {IsAvailable}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Tesseract not available");
            return false;
        }
    }

    /// <inheritdoc/>
    public string GetOcrLanguageCode(string language)
    {
        if (LanguageCodeMap.TryGetValue(language, out var code))
        {
            return code;
        }

        // Default to the input if not in mapping (assume it's already a Tesseract code)
        return language.ToLowerInvariant();
    }

    private async Task<List<string>> ExtractImagesFromVobSubAsync(string idxFilePath, string outputDirectory, CancellationToken cancellationToken)
    {
        // Use ffmpeg to extract images from VobSub
        // Command: ffmpeg -i "{idxFilePath}" "{outputDir}/frame_%04d.png"
        var outputPattern = _fileSystem.Path.Combine(outputDirectory, "frame_%04d.png");
        var arguments = $"-i \"{idxFilePath}\" \"{outputPattern}\"";

        _logger.LogDebug("Executing ffmpeg with arguments: {Arguments}", arguments);

        var result = await RunProcessAsync("ffmpeg", arguments, cancellationToken);

        // ffmpeg returns 0 on success
        if (result.ExitCode != 0)
        {
            _logger.LogWarning("ffmpeg extraction failed with exit code {ExitCode}: {Error}",
                result.ExitCode, result.StandardError);
        }

        // Find all extracted PNG files
        var imageFiles = _fileSystem.Directory.GetFiles(outputDirectory, "frame_*.png")
            .OrderBy(f => f)
            .ToList();

        return imageFiles;
    }

    private async Task<TesseractOcrResult> PerformTesseractOcrOnImageAsync(string imagePath, string language, CancellationToken cancellationToken)
    {
        // Command: tesseract "{imagePath}" stdout -l {language}
        var arguments = $"\"{imagePath}\" stdout -l {language}";

        _logger.LogDebug("Performing OCR on image: {ImagePath}", imagePath);

        var result = await RunProcessAsync(TesseractTool, arguments, cancellationToken);

        if (result.ExitCode == 0)
        {
            // Extract text and confidence from output
            var text = result.StandardOutput.Trim();
            // Tesseract doesn't provide per-character confidence in stdout mode
            // We'll assume a default confidence of 0.85 for successful OCR
            var confidence = string.IsNullOrWhiteSpace(text) ? 0.0 : 0.85;

            return new TesseractOcrResult
            {
                Text = text,
                Confidence = confidence
            };
        }
        else
        {
            _logger.LogWarning("Tesseract OCR failed for image {ImagePath} with exit code {ExitCode}",
                imagePath, result.ExitCode);

            return new TesseractOcrResult
            {
                Text = string.Empty,
                Confidence = 0.0
            };
        }
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Remove HTML tags
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);

        // Remove timecodes (e.g., 00:00:01,000)
        text = Regex.Replace(text, @"\d{2}:\d{2}:\d{2}[,\.]\d{3}", string.Empty);

        // Collapse multiple whitespace to single space
        text = Regex.Replace(text, @"\s+", " ");

        // Reduce multiple newlines to single newline
        text = Regex.Replace(text, @"(\r?\n){2,}", "\n");

        return text.Trim();
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

    private class TesseractOcrResult
    {
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}
