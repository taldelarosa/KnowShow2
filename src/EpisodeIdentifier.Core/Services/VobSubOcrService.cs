using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Performs OCR on VobSub subtitle files using vobsub2srt.
/// Similar to PgsRipService, this uses an external tool (vobsub2srt) for better accuracy.
/// </summary>
public class VobSubOcrService : IVobSubOcrService
{
    private readonly ILogger<VobSubOcrService> _logger;
    private readonly IFileSystem _fileSystem;
    private bool? _isAvailable;

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
        if (idxFilePath == null) throw new ArgumentNullException(nameof(idxFilePath));
        if (subFilePath == null) throw new ArgumentNullException(nameof(subFilePath));
        if (language == null) throw new ArgumentNullException(nameof(language));
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty or whitespace", nameof(language));
        if (!_fileSystem.File.Exists(idxFilePath))
            throw new ArgumentException($".idx file does not exist: {idxFilePath}", nameof(idxFilePath));
        if (!_fileSystem.File.Exists(subFilePath))
            throw new ArgumentException($".sub file does not exist: {subFilePath}", nameof(subFilePath));

        var stopwatch = Stopwatch.StartNew();
        var languageCode = GetOcrLanguageCode(language);

        _logger.LogInformation("Starting VobSub OCR for {IdxFile} with language {Language}",
            idxFilePath, languageCode);

        try
        {
            // Check if vobsub2srt is available
            if (!await IsVobSub2SrtAvailableAsync(cancellationToken))
            {
                _logger.LogWarning("vobsub2srt is not available. Install with: sudo apt-get install vobsub2srt");
                return new VobSubOcrResult
                {
                    Success = false,
                    ErrorMessage = "vobsub2srt tool is not installed",
                    OcrDuration = stopwatch.Elapsed
                };
            }

            // vobsub2srt expects the base name without extension
            // e.g., if file is "/tmp/subtitle.idx", pass "/tmp/subtitle"
            var basePath = _fileSystem.Path.Combine(
                _fileSystem.Path.GetDirectoryName(idxFilePath) ?? string.Empty,
                _fileSystem.Path.GetFileNameWithoutExtension(idxFilePath));

            // Create output directory for SRT file
            var outputDir = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), $"vobsub_srt_{Guid.NewGuid()}");
            _fileSystem.Directory.CreateDirectory(outputDir);
            var outputSrt = _fileSystem.Path.Combine(outputDir, "output.srt");

            // Run vobsub2srt: vobsub2srt --lang <lang> <input-base> <output-srt>
            var arguments = $"--lang {languageCode} \"{basePath}\" \"{outputSrt}\"";
            
            _logger.LogDebug("Executing vobsub2srt with arguments: {Arguments}", arguments);

            var result = await RunProcessAsync("vobsub2srt", arguments, cancellationToken);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("vobsub2srt failed with exit code {ExitCode}: {Error}",
                    result.ExitCode, result.StandardError);
                
                return new VobSubOcrResult
                {
                    Success = false,
                    ErrorMessage = $"vobsub2srt exited with code {result.ExitCode}",
                    OcrDuration = stopwatch.Elapsed
                };
            }

            // Read the generated SRT file
            if (!_fileSystem.File.Exists(outputSrt))
            {
                _logger.LogWarning("vobsub2srt completed but output file not found: {OutputFile}", outputSrt);
                return new VobSubOcrResult
                {
                    Success = false,
                    ErrorMessage = "Output SRT file not generated",
                    OcrDuration = stopwatch.Elapsed
                };
            }

            var srtContent = await _fileSystem.File.ReadAllTextAsync(outputSrt, cancellationToken);
            
            // Extract just the text content from SRT (remove timestamps and sequence numbers)
            var extractedText = ExtractTextFromSrt(srtContent);
            var characterCount = extractedText.Length;

            // Clean up temp directory
            try
            {
                _fileSystem.Directory.Delete(outputDir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to cleanup temp directory: {TempDir}", outputDir);
            }

            stopwatch.Stop();

            _logger.LogInformation("VobSub OCR complete. Extracted {CharCount} characters. Duration: {Duration}ms",
                characterCount, stopwatch.ElapsedMilliseconds);

            return new VobSubOcrResult
            {
                Success = true,
                ExtractedText = extractedText,
                ConfidenceScore = 0.85, // vobsub2srt doesn't provide confidence, use reasonable default
                CharacterCount = characterCount,
                OcrDuration = stopwatch.Elapsed,
                ImageCount = 0, // vobsub2srt handles this internally
                Language = languageCode
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "VobSub OCR failed");

            return new VobSubOcrResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                OcrDuration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Check if vobsub2srt is available on the system
    /// </summary>
    public async Task<bool> IsVobSub2SrtAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_isAvailable.HasValue)
            return _isAvailable.Value;

        try
        {
            var result = await RunProcessAsync("vobsub2srt", "--version", cancellationToken);
            _isAvailable = result.ExitCode == 0;

            if (_isAvailable.Value)
            {
                _logger.LogInformation("vobsub2srt is available and ready to use");
            }
            else
            {
                _logger.LogWarning("vobsub2srt is not available - install with: sudo apt-get install vobsub2srt");
            }

            return _isAvailable.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check vobsub2srt availability");
            _isAvailable = false;
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsTesseractAvailableAsync()
    {
        // vobsub2srt uses Tesseract internally, so we check for vobsub2srt instead
        return await IsVobSub2SrtAvailableAsync();
    }

    /// <inheritdoc/>
    public string GetOcrLanguageCode(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "eng";
        }

        if (LanguageCodeMap.TryGetValue(language, out var mappedCode))
        {
            return mappedCode;
        }

        // If it's a 3-letter code, return as-is
        if (language.Length == 3 && language.All(char.IsLetter))
        {
            return language.ToLowerInvariant();
        }

        // Default to English
        _logger.LogWarning("Unknown language '{Language}', defaulting to 'eng'", language);
        return "eng";
    }

    /// <summary>
    /// Extract plain text content from SRT format
    /// </summary>
    private string ExtractTextFromSrt(string srtContent)
    {
        if (string.IsNullOrWhiteSpace(srtContent))
            return string.Empty;

        var lines = srtContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var textBuilder = new StringBuilder();
        var isTextLine = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip sequence numbers (just digits)
            if (int.TryParse(trimmed, out _))
            {
                isTextLine = false;
                continue;
            }

            // Skip timestamp lines (contains -->)
            if (trimmed.Contains("-->"))
            {
                isTextLine = true;
                continue;
            }

            // Empty line resets
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                isTextLine = false;
                if (textBuilder.Length > 0 && textBuilder[textBuilder.Length - 1] != '\n')
                {
                    textBuilder.AppendLine();
                }
                continue;
            }

            // This is subtitle text
            if (isTextLine)
            {
                textBuilder.AppendLine(trimmed);
            }
        }

        return textBuilder.ToString().Trim();
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
        const int maxOutputLength = 100_000; // Reasonable limit for vobsub2srt output

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
