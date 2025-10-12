using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Complete video file processing service that handles the entire workflow from subtitle extraction to episode identification.
/// Encapsulates all the logic needed for processing a single video file.
/// </summary>
public class VideoFileProcessingService : IVideoFileProcessingService
{
    private readonly ILogger<VideoFileProcessingService> _logger;
    private readonly VideoFormatValidator _videoFormatValidator;
    private readonly SubtitleExtractor _subtitleExtractor;
    private readonly EnhancedPgsToTextConverter _pgsConverter;
    private readonly VideoTextSubtitleExtractor _textExtractor;
    private readonly EpisodeIdentificationService _episodeIdentificationService;
    private readonly FilenameService _filenameService;
    private readonly FileRenameService _fileRenameService;
    private readonly IAppConfigService _configService;

    public VideoFileProcessingService(
        ILogger<VideoFileProcessingService> logger,
        VideoFormatValidator videoFormatValidator,
        SubtitleExtractor subtitleExtractor,
        EnhancedPgsToTextConverter pgsConverter,
        VideoTextSubtitleExtractor textExtractor,
        EpisodeIdentificationService episodeIdentificationService,
        FilenameService filenameService,
        FileRenameService fileRenameService,
        IAppConfigService configService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _videoFormatValidator = videoFormatValidator ?? throw new ArgumentNullException(nameof(videoFormatValidator));
        _subtitleExtractor = subtitleExtractor ?? throw new ArgumentNullException(nameof(subtitleExtractor));
        _pgsConverter = pgsConverter ?? throw new ArgumentNullException(nameof(pgsConverter));
        _textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
        _episodeIdentificationService = episodeIdentificationService ?? throw new ArgumentNullException(nameof(episodeIdentificationService));
        _filenameService = filenameService ?? throw new ArgumentNullException(nameof(filenameService));
        _fileRenameService = fileRenameService ?? throw new ArgumentNullException(nameof(fileRenameService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    /// <summary>
    /// Processes a video file for episode identification and optional renaming.
    /// </summary>
    /// <param name="filePath">Path to the video file to process</param>
    /// <param name="shouldRename">Whether to rename the file if identification is successful</param>
    /// <param name="language">Preferred subtitle language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Video file processing result</returns>
    public async Task<VideoFileProcessingResult> ProcessVideoFileAsync(
        string filePath,
        bool shouldRename = false,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var result = new VideoFileProcessingResult
        {
            FilePath = filePath,
            ProcessingStarted = DateTime.UtcNow
        };

        try
        {
            _logger.LogDebug("Starting video file processing: {FilePath}", filePath);

            // Step 1: Validate file format
            if (!await _videoFormatValidator.IsValidForProcessing(filePath))
            {
                result.Error = IdentificationError.UnsupportedFileType;
                result.ProcessingCompleted = DateTime.UtcNow;
                return result;
            }

            // Step 2: Check if OCR tools are available
            if (!_pgsConverter.IsOcrAvailable())
            {
                result.Error = new IdentificationError
                {
                    Code = "MISSING_DEPENDENCY",
                    Message = "Tesseract OCR is required but not available. Please install tesseract-ocr."
                };
                result.ProcessingCompleted = DateTime.UtcNow;
                return result;
            }

            // Step 3: Get subtitle track information
            var subtitleTracks = await _videoFormatValidator.GetSubtitleTracks(filePath);
            if (!subtitleTracks.Any())
            {
                result.Error = IdentificationError.NoSubtitlesFound;
                result.ProcessingCompleted = DateTime.UtcNow;
                return result;
            }

            // Step 4: Try to extract subtitle text
            string? subtitleText = null;

            // Check if there are any PGS tracks first
            var pgsTracks = subtitleTracks.Where(t =>
                t.CodecName == "hdmv_pgs_subtitle" ||
                t.CodecName == "dvd_subtitle").ToList();

            if (pgsTracks.Any())
            {
                // Try PGS extraction first
                var pgsTrack = PgsTrackSelector.SelectBestTrack(pgsTracks, language);
                var ocrLanguage = GetOcrLanguageCode(language);
                subtitleText = await _pgsConverter.ConvertPgsFromVideoToText(filePath, pgsTrack.Index, ocrLanguage);
            }

            // If PGS extraction failed or no PGS tracks, try text subtitles
            if (string.IsNullOrWhiteSpace(subtitleText))
            {
                var textSubtitleResult = await TryExtractTextSubtitle(filePath, language);
                if (textSubtitleResult.Success)
                {
                    subtitleText = textSubtitleResult.SubtitleText;
                }
                else if (pgsTracks.Any())
                {
                    // PGS tracks existed but OCR failed and no text fallback
                    result.Error = new IdentificationError
                    {
                        Code = "OCR_FAILED",
                        Message = "Failed to extract readable text from PGS subtitles using OCR and no text subtitle fallback available"
                    };
                    result.ProcessingCompleted = DateTime.UtcNow;
                    return result;
                }
                else
                {
                    // No PGS tracks and text extraction failed
                    result.Error = IdentificationError.NoSubtitlesFound;
                    result.ProcessingCompleted = DateTime.UtcNow;
                    return result;
                }
            }

            // Step 5: Identify episode
            var identificationResult = await _episodeIdentificationService.IdentifyEpisodeAsync(subtitleText ?? "", filePath);
            result.IdentificationResult = identificationResult;

            // Log configuration info for debugging
            _logger.LogInformation("Configuration debug: RenameThreshold={Threshold}, Confidence={Confidence}, ShouldRename={ShouldRename}",
                _configService.Config.RenameConfidenceThreshold, identificationResult.MatchConfidence, shouldRename);

            // Step 6: Handle renaming if requested and confidence is high enough
            if (shouldRename && !identificationResult.HasError &&
                identificationResult.MatchConfidence >= _configService.Config.RenameConfidenceThreshold)
            {
                _logger.LogInformation("Attempting file rename: Confidence={Confidence:P1}, Threshold={Threshold:P1}",
                    identificationResult.MatchConfidence, _configService.Config.RenameConfidenceThreshold);
                await AttemptFileRename(filePath, identificationResult, result);
            }
            else
            {
                _logger.LogInformation("Skipping file rename: ShouldRename={ShouldRename}, HasError={HasError}, Confidence={Confidence:P1}, Threshold={Threshold:P1}",
                    shouldRename, identificationResult.HasError, identificationResult.MatchConfidence, _configService.Config.RenameConfidenceThreshold);
            }

            result.ProcessingCompleted = DateTime.UtcNow;
            _logger.LogDebug("Video file processing completed: {FilePath}, Success: {Success}",
                filePath, !result.HasError);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during video file processing: {FilePath}", filePath);
            result.Error = new IdentificationError
            {
                Code = "PROCESSING_ERROR",
                Message = $"Unexpected error during file processing: {ex.Message}"
            };
            result.ProcessingCompleted = DateTime.UtcNow;
            return result;
        }
    }

    private async Task<(bool Success, string? SubtitleText)> TryExtractTextSubtitle(string filePath, string? language)
    {
        try
        {
            // Get all subtitle tracks from the video
            var subtitleTracks = await _videoFormatValidator.GetSubtitleTracks(filePath);

            // Look for text-based subtitle tracks (non-PGS)
            var textTracks = subtitleTracks.Where(t =>
                t.CodecName != "hdmv_pgs_subtitle" &&
                (t.CodecName == "subrip" || t.CodecName == "ass" || t.CodecName == "webvtt" || t.CodecName == "mov_text" || t.CodecName == "srt"))
                .ToList();

            if (!textTracks.Any())
            {
                return (false, null); // No text subtitle tracks found
            }

            // Select the best text track based on language preference
            var selectedTrack = textTracks.FirstOrDefault(t =>
                string.IsNullOrEmpty(language) ||
                (t.Language?.Contains(language, StringComparison.OrdinalIgnoreCase) == true)) ?? textTracks.First();

            // Extract text subtitle content
            var subtitleText = await _textExtractor.ExtractTextSubtitleFromVideo(filePath, selectedTrack.Index, language);

            if (!string.IsNullOrWhiteSpace(subtitleText))
            {
                return (true, subtitleText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Text subtitle extraction failed for {FilePath}", filePath);
        }

        return (false, null);
    }

    private async Task AttemptFileRename(string filePath, IdentificationResult identificationResult, VideoFileProcessingResult processingResult)
    {
        try
        {
            _logger.LogInformation("Starting filename generation for {FilePath}: Series={Series}, Season={Season}, Episode={Episode}",
                filePath, identificationResult.Series, identificationResult.Season, identificationResult.Episode);

            // Generate filename using FilenameService
            var filenameRequest = new FilenameGenerationRequest
            {
                Series = identificationResult.Series ?? "",
                Season = identificationResult.Season ?? "",
                Episode = identificationResult.Episode ?? "",
                EpisodeName = identificationResult.EpisodeName ?? "",
                FileExtension = Path.GetExtension(filePath),
                MatchConfidence = identificationResult.MatchConfidence
            };

            _logger.LogInformation("Filename generation request: Series='{Series}', Season='{Season}', Episode='{Episode}', EpisodeName='{EpisodeName}', Extension='{Extension}'",
                filenameRequest.Series, filenameRequest.Season, filenameRequest.Episode, filenameRequest.EpisodeName, filenameRequest.FileExtension);

            var filenameResult = _filenameService.GenerateFilename(filenameRequest);

            _logger.LogInformation("Filename generation result: IsValid={IsValid}, SuggestedFilename='{SuggestedFilename}', ValidationError='{ValidationError}'",
                filenameResult.IsValid, filenameResult.SuggestedFilename, filenameResult.ValidationError);

            if (filenameResult.IsValid && !string.IsNullOrEmpty(filenameResult.SuggestedFilename))
            {
                // Prepare file rename request
                var renameRequest = new FileRenameRequest
                {
                    OriginalPath = filePath,
                    SuggestedFilename = filenameResult.SuggestedFilename
                };

                // Attempt to rename the file
                var renameResult = await _fileRenameService.RenameFileAsync(renameRequest);

                if (renameResult.Success)
                {
                    // Update processing result with rename success
                    processingResult.SuggestedFilename = filenameResult.SuggestedFilename;
                    processingResult.FileRenamed = true;
                    processingResult.OriginalFilename = Path.GetFileName(filePath);
                    processingResult.NewFilePath = renameResult.NewPath;

                    // Update identification result as well
                    identificationResult.SuggestedFilename = filenameResult.SuggestedFilename;
                    identificationResult.FileRenamed = true;
                    identificationResult.OriginalFilename = Path.GetFileName(filePath);
                }
                else
                {
                    // Include filename suggestion but note rename failure
                    processingResult.SuggestedFilename = filenameResult.SuggestedFilename;
                    processingResult.FileRenamed = false;

                    // Set appropriate error based on rename failure type
                    if (renameResult.ErrorType.HasValue)
                    {
                        identificationResult.Error = IdentificationError.FromFileRenameError(renameResult.ErrorType.Value, renameResult.ErrorMessage);
                    }
                    else
                    {
                        identificationResult.Error = IdentificationError.RenameFailedUnknown(renameResult.ErrorMessage ?? "Unknown rename error");
                    }
                }
            }
            else
            {
                _logger.LogWarning("File identification successful but filename generation failed for {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file rename operation for {FilePath}", filePath);
            identificationResult.Error = IdentificationError.RenameFailedUnknown($"Unexpected error during file rename: {ex.Message}");
        }
    }

    private static string GetOcrLanguageCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "eng"; // Default to English

        return language.ToLowerInvariant() switch
        {
            "english" => "eng",
            "spanish" => "spa",
            "french" => "fra",
            "german" => "deu",
            "italian" => "ita",
            "portuguese" => "por",
            "russian" => "rus",
            "japanese" => "jpn",
            "korean" => "kor",
            "chinese" => "chi_sim",
            _ => "eng" // Default fallback
        };
    }
}
