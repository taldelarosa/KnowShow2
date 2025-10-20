using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Performs OCR on VobSub subtitle files using vobsub2srt.
/// </summary>
public class VobSubOcrService : IVobSubOcrService
{
    private readonly ILogger<VobSubOcrService> _logger;
    private readonly IFileSystem _fileSystem;
    private const string VobSub2SrtTool = "vobsub2srt";

    // Language code mappings for vobsub2srt
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

        _logger.LogInformation("Starting VobSub OCR using vobsub2srt for {IdxFile} with language {Language}",
            idxFilePath, languageCode);

        string? outputSrtPath = null;

        try
        {
            // vobsub2srt creates output based on input filename
            // It takes the basename without extension and creates <basename>.srt
            var idxBasePath = idxFilePath.EndsWith(".idx", StringComparison.OrdinalIgnoreCase)
                ? idxFilePath[..^4]
                : idxFilePath;

            outputSrtPath = $"{idxBasePath}.srt";

            // Run vobsub2srt: vobsub2srt --lang {language} --tesseract-lang {language} {idxFilePathWithoutExtension}
            // The tool expects the path without .idx extension and will find both .idx and .sub
            // Output will be written to {idxBasePath}.srt automatically
            // Note: --tesseract-lang is required for snap-installed vobsub2srt to avoid language detection issues
            var arguments = $"--lang {languageCode} --tesseract-lang {languageCode} \"{idxBasePath}\"";

            _logger.LogDebug("Executing vobsub2srt with arguments: {Arguments}", arguments);

            var result = await RunProcessAsync(VobSub2SrtTool, arguments, cancellationToken);

            if (result.ExitCode != 0)
            {
                var errorMessage = $"vobsub2srt failed with exit code {result.ExitCode}: {result.StandardError}";
                _logger.LogError("VobSub OCR failed: {Error}", errorMessage);

                stopwatch.Stop();
                return new VobSubOcrResult
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    OcrDuration = stopwatch.Elapsed,
                    ImageCount = 0,
                    Language = languageCode
                };
            }

            // Read the generated SRT file
            if (!_fileSystem.File.Exists(outputSrtPath))
            {
                var errorMessage = $"vobsub2srt completed but output SRT file not found: {outputSrtPath}";
                _logger.LogError("VobSub OCR failed: {Error}", errorMessage);

                stopwatch.Stop();
                return new VobSubOcrResult
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    OcrDuration = stopwatch.Elapsed,
                    ImageCount = 0,
                    Language = languageCode
                };
            }

            var srtContent = await _fileSystem.File.ReadAllTextAsync(outputSrtPath, cancellationToken);
            var extractedText = ExtractTextFromSrt(srtContent);
            var normalizedText = NormalizeText(extractedText);
            var characterCount = normalizedText.Length;

            // Count subtitle entries to approximate image count
            var imageCount = CountSrtEntries(srtContent);

            // vobsub2srt doesn't provide confidence scores, so we'll use a fixed high confidence
            // since it processes all frames unlike our previous sampling approach
            const double defaultConfidence = 0.90;

            stopwatch.Stop();

            _logger.LogInformation(
                "VobSub OCR complete using vobsub2srt. Extracted {CharCount} characters from {ImageCount} subtitle frames. Duration: {Duration}ms",
                characterCount, imageCount, stopwatch.ElapsedMilliseconds);

            return new VobSubOcrResult
            {
                Success = true,
                ExtractedText = normalizedText,
                ConfidenceScore = defaultConfidence,
                CharacterCount = characterCount,
                OcrDuration = stopwatch.Elapsed,
                ImageCount = imageCount,
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
            // Clean up temporary SRT file
            if (outputSrtPath != null && _fileSystem.File.Exists(outputSrtPath))
            {
                try
                {
                    _fileSystem.File.Delete(outputSrtPath);
                    _logger.LogDebug("Cleaned up temporary SRT file: {SrtPath}", outputSrtPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary SRT file: {SrtPath}", outputSrtPath);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsTesseractAvailableAsync()
    {
        try
        {
            // vobsub2srt doesn't support --version, use --help instead (exits with 1 but that's OK)
            var result = await RunProcessAsync(VobSub2SrtTool, "--help", CancellationToken.None);
            // vobsub2srt --help returns exit code 1 but still outputs help text
            // Check if the tool is available by looking for help output
            var isAvailable = !string.IsNullOrEmpty(result.StandardOutput) || !string.IsNullOrEmpty(result.StandardError);

            _logger.LogDebug("vobsub2srt availability check: {IsAvailable}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "vobsub2srt not available");
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

        // Default to the input if not in mapping (assume it's already a vobsub2srt language code)
        return language.ToLowerInvariant();
    }

    private string ExtractTextFromSrt(string srtContent)
    {
        if (string.IsNullOrWhiteSpace(srtContent))
        {
            return string.Empty;
        }

        var textBuilder = new StringBuilder();
        var lines = srtContent.Split('\n');

        bool isTextLine = false;
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                isTextLine = false;
                continue;
            }

            // Skip subtitle index numbers (e.g., "1", "2", "3")
            if (int.TryParse(trimmedLine, out _))
            {
                isTextLine = false;
                continue;
            }

            // Skip timecode lines (e.g., "00:00:01,000 --> 00:00:03,000")
            if (trimmedLine.Contains("-->"))
            {
                isTextLine = true;
                continue;
            }

            // This is subtitle text
            if (isTextLine)
            {
                textBuilder.AppendLine(trimmedLine);
            }
        }

        return textBuilder.ToString();
    }

    private int CountSrtEntries(string srtContent)
    {
        if (string.IsNullOrWhiteSpace(srtContent))
        {
            return 0;
        }

        // Count lines that contain "-->" (timecode separator)
        return srtContent.Split('\n').Count(line => line.Contains("-->"));
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
        const int maxOutputLength = 1_000_000; // 1MB limit to prevent overflow

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null && outputBuilder.Length < maxOutputLength)
            {
                outputBuilder.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null && errorBuilder.Length < maxOutputLength)
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
