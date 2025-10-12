using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Coordinates between PGS and text subtitle workflows, automatically determining
/// which workflow to use based on the video file content.
/// </summary>
public class SubtitleWorkflowCoordinator
{
    private readonly ILogger<SubtitleWorkflowCoordinator> _logger;
    private readonly VideoFormatValidator _validator;
    private readonly SubtitleExtractor _pgsExtractor;
    private readonly ITextSubtitleExtractor _textExtractor;
    private readonly EnhancedPgsToTextConverter _pgsConverter;
    private readonly IEpisodeIdentificationService _identificationService;

    public SubtitleWorkflowCoordinator(
        ILogger<SubtitleWorkflowCoordinator> logger,
        VideoFormatValidator validator,
        SubtitleExtractor pgsExtractor,
        ITextSubtitleExtractor textExtractor,
        EnhancedPgsToTextConverter pgsConverter,
        IEpisodeIdentificationService identificationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _pgsExtractor = pgsExtractor ?? throw new ArgumentNullException(nameof(pgsExtractor));
        _textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
        _pgsConverter = pgsConverter ?? throw new ArgumentNullException(nameof(pgsConverter));
        _identificationService = identificationService ?? throw new ArgumentNullException(nameof(identificationService));
    }

    /// <summary>
    /// Automatically determines and executes the appropriate subtitle workflow
    /// based on the video file content.
    /// </summary>
    /// <param name="videoFilePath">Path to the video file to process</param>
    /// <param name="language">Preferred subtitle language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Episode identification result</returns>
    public async Task<IdentificationResult> ProcessVideoAsync(
        string videoFilePath,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath))
            throw new ArgumentException("Video file path cannot be null or empty.", nameof(videoFilePath));

        if (!File.Exists(videoFilePath))
        {
            return new IdentificationResult
            {
                Error = new IdentificationError
                {
                    Code = "FILE_NOT_FOUND",
                    Message = $"Video file not found: {videoFilePath}"
                }
            };
        }

        _logger.LogInformation("Starting subtitle workflow coordination for {VideoFile}", videoFilePath);

        try
        {
            // First, check for PGS subtitles (preferred workflow)
            var pgsResult = await TryPgsWorkflowAsync(videoFilePath, language, cancellationToken);
            if (!pgsResult.HasError)
            {
                _logger.LogInformation("Successfully processed video using PGS workflow");
                return pgsResult;
            }

            _logger.LogInformation("PGS workflow failed or no PGS subtitles found, trying text subtitle workflow");

            // Fallback to text subtitle workflow
            var textResult = await TryTextSubtitleWorkflowAsync(videoFilePath, language, cancellationToken);
            if (!textResult.HasError)
            {
                _logger.LogInformation("Successfully processed video using text subtitle workflow");
                return textResult;
            }

            _logger.LogWarning("Both PGS and text subtitle workflows failed for {VideoFile}", videoFilePath);

            // If both workflows fail, return the more informative error
            return textResult.HasError ? textResult : pgsResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during subtitle workflow coordination for {VideoFile}", videoFilePath);
            return new IdentificationResult
            {
                Error = new IdentificationError
                {
                    Code = "PROCESSING_ERROR",
                    Message = $"Unexpected error: {ex.Message}"
                }
            };
        }
    }

    /// <summary>
    /// Attempts to process the video using the PGS subtitle workflow.
    /// </summary>
    private async Task<IdentificationResult> TryPgsWorkflowAsync(
        string videoFilePath,
        string? language,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Checking for PGS subtitles in {VideoFile}", videoFilePath);

            // Check if video has PGS subtitle tracks
            var subtitleTracks = await _validator.GetSubtitleTracks(videoFilePath);
            var pgsTrack = subtitleTracks
                .Where(t => string.Equals(t.CodecName, "pgs", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.CodecName, "hdmv_pgs_subtitle", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(t =>
                    string.IsNullOrEmpty(language) ||
                    (t.Language?.Contains(language, StringComparison.OrdinalIgnoreCase) ?? false));

            if (pgsTrack == null)
            {
                _logger.LogDebug("No suitable PGS subtitles found in {VideoFile}", videoFilePath);
                return new IdentificationResult
                {
                    Error = new IdentificationError
                    {
                        Code = "NO_SUBTITLES_FOUND",
                        Message = "No PGS subtitles found in the video file"
                    }
                };
            }

            _logger.LogInformation("Found PGS subtitle track (index: {Index}, language: {Language})",
                pgsTrack.Index, pgsTrack.Language ?? "unknown");

            // Check if OCR tools are available
            if (!_pgsConverter.IsOcrAvailable())
            {
                return new IdentificationResult
                {
                    Error = new IdentificationError
                    {
                        Code = "MISSING_DEPENDENCY",
                        Message = "Tesseract OCR is required but not available. Please install tesseract-ocr."
                    }
                };
            }

            // Extract and OCR subtitle images from video file
            var ocrLanguage = GetOcrLanguageCode(language);
            var subtitleText = await _pgsConverter.ConvertPgsFromVideoToText(
                videoFilePath,
                pgsTrack.Index,
                ocrLanguage);

            if (string.IsNullOrWhiteSpace(subtitleText))
            {
                return new IdentificationResult
                {
                    Error = new IdentificationError
                    {
                        Code = "OCR_FAILED",
                        Message = "Failed to extract readable text from PGS subtitles using OCR"
                    }
                };
            }

            _logger.LogDebug("Successfully extracted {Length} characters from PGS subtitles", subtitleText.Length);

            // Match against database
            var result = await _identificationService.IdentifyEpisodeAsync(subtitleText, videoFilePath);

            // Add workflow metadata
            if (!result.HasError)
            {
                _logger.LogInformation("PGS workflow successfully identified: {Series} S{Season}E{Episode}",
                    result.Series, result.Season, result.Episode);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PGS workflow failed for {VideoFile}", videoFilePath);
            return new IdentificationResult
            {
                Error = new IdentificationError
                {
                    Code = "PROCESSING_ERROR",
                    Message = $"PGS workflow error: {ex.Message}"
                }
            };
        }
    }

    /// <summary>
    /// Attempts to process the video using the text subtitle workflow.
    /// </summary>
    private async Task<IdentificationResult> TryTextSubtitleWorkflowAsync(
        string videoFilePath,
        string? language,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Checking for text subtitles for {VideoFile}", videoFilePath);

            // Detect available text subtitle tracks
            var textTracks = await _textExtractor.DetectTextSubtitleTracksAsync(videoFilePath, cancellationToken);

            if (!textTracks.Any())
            {
                _logger.LogDebug("No text subtitle tracks found for {VideoFile}", videoFilePath);
                return new IdentificationResult
                {
                    Error = new IdentificationError
                    {
                        Code = "NO_SUBTITLES_FOUND",
                        Message = "No text-based subtitles found (SRT, VTT, ASS, etc.)"
                    }
                };
            }

            _logger.LogInformation("Found {Count} text subtitle track(s) for {VideoFile}", textTracks.Count, videoFilePath);

            // Select the best track based on language preference
            var selectedTrack = SelectBestTextTrack(textTracks, language);

            _logger.LogInformation("Selected text subtitle track: {Format} (index: {Index}, language: {Language})",
                selectedTrack.Format, selectedTrack.Index, selectedTrack.Language ?? "unknown");

            // Extract subtitle content
            var extractionResult = await _textExtractor.ExtractTextSubtitleContentAsync(
                videoFilePath,
                selectedTrack,
                cancellationToken);

            if (extractionResult.Status != ProcessingStatus.Completed ||
                extractionResult.ExtractedTracks.Count == 0)
            {
                return new IdentificationResult
                {
                    Error = new IdentificationError
                    {
                        Code = "EXTRACTION_FAILED",
                        Message = $"Failed to extract text from subtitle track: {extractionResult.ErrorMessage}"
                    }
                };
            }

            var extractedTrack = extractionResult.ExtractedTracks.First();
            if (string.IsNullOrWhiteSpace(extractedTrack.Content))
            {
                return new IdentificationResult
                {
                    Error = new IdentificationError
                    {
                        Code = "EXTRACTION_FAILED",
                        Message = "Extracted subtitle content is empty"
                    }
                };
            }

            _logger.LogDebug("Successfully extracted {Length} characters from text subtitles", extractedTrack.Content.Length);

            // Match against database
            var result = await _identificationService.IdentifyEpisodeAsync(extractedTrack.Content, videoFilePath);

            // Add workflow metadata
            if (!result.HasError)
            {
                _logger.LogInformation("Text subtitle workflow successfully identified: {Series} S{Season}E{Episode} (using {Format})",
                    result.Series, result.Season, result.Episode, selectedTrack.Format);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Text subtitle workflow failed for {VideoFile}", videoFilePath);
            return new IdentificationResult
            {
                Error = new IdentificationError
                {
                    Code = "PROCESSING_ERROR",
                    Message = $"Text subtitle workflow error: {ex.Message}"
                }
            };
        }
    }

    /// <summary>
    /// Selects the best text subtitle track based on language preference.
    /// </summary>
    private TextSubtitleTrack SelectBestTextTrack(IReadOnlyList<TextSubtitleTrack> tracks, string? preferredLanguage)
    {
        // If preferred language specified, try to find it
        if (!string.IsNullOrEmpty(preferredLanguage))
        {
            var langTrack = tracks.FirstOrDefault(t =>
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (langTrack != null)
            {
                return langTrack;
            }
        }

        // Default preferences: English first, then default track, then first available
        var englishTrack = tracks.FirstOrDefault(t =>
            string.Equals(t.Language, "eng", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Language, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Language, "english", StringComparison.OrdinalIgnoreCase));

        if (englishTrack != null)
            return englishTrack;

        var defaultTrack = tracks.FirstOrDefault(t => t.IsDefault);
        if (defaultTrack != null)
            return defaultTrack;

        return tracks.First();
    }

    /// <summary>
    /// Maps language preferences to Tesseract OCR language codes.
    /// </summary>
    private static string GetOcrLanguageCode(string? language)
    {
        if (string.IsNullOrEmpty(language))
            return "eng"; // Default to English

        // Map common language names/codes to Tesseract language codes
        return language.ToLowerInvariant() switch
        {
            "en" or "eng" or "english" => "eng",
            "es" or "spa" or "spanish" => "spa",
            "fr" or "fra" or "french" => "fra",
            "de" or "deu" or "german" => "deu",
            "it" or "ita" or "italian" => "ita",
            "pt" or "por" or "portuguese" => "por",
            "ru" or "rus" or "russian" => "rus",
            "ja" or "jpn" or "japanese" => "jpn",
            "ko" or "kor" or "korean" => "kor",
            "zh" or "chi" or "chinese" => "chi_sim",
            "ar" or "ara" or "arabic" => "ara",
            _ => "eng" // Default to English for unknown languages
        };
    }
}
