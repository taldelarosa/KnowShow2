using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Configuration;
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
    private readonly IVobSubExtractor _vobSubExtractor;
    private readonly IVobSubOcrService _vobSubOcrService;
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
        IVobSubExtractor vobSubExtractor,
        IVobSubOcrService vobSubOcrService,
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
        _vobSubExtractor = vobSubExtractor ?? throw new ArgumentNullException(nameof(vobSubExtractor));
        _vobSubOcrService = vobSubOcrService ?? throw new ArgumentNullException(nameof(vobSubOcrService));
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

            // Step 4: Try to extract subtitle text (priority: PGS > VobSub > Text)
            string? subtitleText = null;
            SubtitleType actualSubtitleType = SubtitleType.TextBased;

            // Check for PGS tracks (highest image quality)
            var pgsTracks = subtitleTracks.Where(t =>
                t.CodecName == "hdmv_pgs_subtitle").ToList();

            if (pgsTracks.Any())
            {
                // Try PGS extraction
                var pgsTrack = PgsTrackSelector.SelectBestTrack(pgsTracks, language);
                var ocrLanguage = GetOcrLanguageCode(language);
                subtitleText = await _pgsConverter.ConvertPgsFromVideoToText(filePath, pgsTrack.Index, ocrLanguage);
                if (!string.IsNullOrWhiteSpace(subtitleText))
                {
                    actualSubtitleType = SubtitleType.PGS;
                }
            }

            // If PGS failed or not available, try VobSub (DVD subtitles)
            if (string.IsNullOrWhiteSpace(subtitleText))
            {
                var vobSubResult = await TryExtractVobSubSubtitle(filePath, language);
                if (vobSubResult.Success)
                {
                    subtitleText = vobSubResult.SubtitleText;
                    actualSubtitleType = SubtitleType.VobSub;
                }
            }

            // If VobSub failed or not available, try text subtitles
            if (string.IsNullOrWhiteSpace(subtitleText))
            {
                var textSubtitleResult = await TryExtractTextSubtitle(filePath, language);
                if (textSubtitleResult.Success)
                {
                    subtitleText = textSubtitleResult.SubtitleText;
                    actualSubtitleType = SubtitleType.TextBased;
                }
                else if (pgsTracks.Any())
                {
                    // PGS tracks existed but OCR failed and no other fallback
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
                    // No supported subtitle tracks or all extraction methods failed
                    result.Error = IdentificationError.NoSubtitlesFound;
                    result.ProcessingCompleted = DateTime.UtcNow;
                    return result;
                }
            }

            // Step 5: Identify episode using the extracted subtitle text
            var identificationResult = await _episodeIdentificationService.IdentifyEpisodeAsync(
                subtitleText ?? "", 
                actualSubtitleType, 
                filePath);
            result.IdentificationResult = identificationResult;

            // Get the appropriate rename threshold based on actual subtitle type
            var renameThreshold = actualSubtitleType switch
            {
                SubtitleType.TextBased => _configService.Config.MatchingThresholds?.TextBased.RenameConfidence,
                SubtitleType.PGS => _configService.Config.MatchingThresholds?.PGS.RenameConfidence,
                SubtitleType.VobSub => _configService.Config.MatchingThresholds?.VobSub.RenameConfidence,
                _ => _configService.Config.MatchingThresholds?.TextBased.RenameConfidence
            }
#pragma warning disable CS0618 // Type or member is obsolete
            ?? (decimal)_configService.Config.RenameConfidenceThreshold;
#pragma warning restore CS0618

            // Log configuration info for debugging
            _logger.LogInformation("Configuration debug: RenameThreshold={Threshold}, Confidence={Confidence}, ShouldRename={ShouldRename}",
                renameThreshold, identificationResult.MatchConfidence, shouldRename);

            // Step 6: Handle renaming if requested and confidence is high enough
            if (shouldRename && !identificationResult.HasError &&
                (decimal)identificationResult.MatchConfidence >= renameThreshold)
            {
                _logger.LogInformation("Attempting file rename: Confidence={Confidence:P1}, Threshold={Threshold:P1}",
                    identificationResult.MatchConfidence, renameThreshold);
                await AttemptFileRename(filePath, identificationResult, result);
            }
            else
            {
                _logger.LogInformation("Skipping file rename: ShouldRename={ShouldRename}, HasError={HasError}, Confidence={Confidence:P1}, Threshold={Threshold:P1}",
                    shouldRename, identificationResult.HasError, identificationResult.MatchConfidence, renameThreshold);
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

            // Look for text-based subtitle tracks (non-PGS, non-DVD)
            var textTracks = subtitleTracks.Where(t =>
                t.CodecName != "hdmv_pgs_subtitle" &&
                t.CodecName != "dvd_subtitle" &&
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

    private async Task<(bool Success, string? SubtitleText)> TryExtractVobSubSubtitle(string filePath, string? language)
    {
        try
        {
            // Get all subtitle tracks from the video
            var subtitleTracks = await _videoFormatValidator.GetSubtitleTracks(filePath);

            // Look for DVD subtitle tracks
            var vobSubTracks = subtitleTracks.Where(t => t.CodecName == "dvd_subtitle").ToList();

            if (!vobSubTracks.Any())
            {
                return (false, null); // No DVD subtitle tracks found
            }

            // Check if required tools are available
            if (!await _vobSubExtractor.IsMkvExtractAvailableAsync())
            {
                _logger.LogDebug("mkvextract not available, skipping VobSub extraction for {FilePath}", filePath);
                return (false, null);
            }

            if (!await _vobSubOcrService.IsTesseractAvailableAsync())
            {
                _logger.LogDebug("Tesseract not available, skipping VobSub OCR for {FilePath}", filePath);
                return (false, null);
            }

            // Select the best VobSub track based on language preference
            var selectedTrack = vobSubTracks.FirstOrDefault(t =>
                string.IsNullOrEmpty(language) ||
                (t.Language?.Contains(language, StringComparison.OrdinalIgnoreCase) == true)) ?? vobSubTracks.First();

            // Create temporary directory for VobSub extraction
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
            {
                baseDir = Directory.GetCurrentDirectory();
            }
            var tempDir = Path.Combine(baseDir, ".episodeidentifier_temp", $"vobsub_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Extract VobSub files from MKV
                var extractionResult = await _vobSubExtractor.ExtractAsync(
                    filePath,
                    selectedTrack.Index,
                    tempDir,
                    CancellationToken.None);

                if (!extractionResult.Success || string.IsNullOrEmpty(extractionResult.IdxFilePath) || 
                    string.IsNullOrEmpty(extractionResult.SubFilePath))
                {
                    _logger.LogDebug("VobSub extraction failed for {FilePath}: {Error}", 
                        filePath, extractionResult.ErrorMessage);
                    return (false, null);
                }

                // Perform OCR on VobSub files
                var ocrLanguage = _vobSubOcrService.GetOcrLanguageCode(language ?? "eng");
                var ocrResult = await _vobSubOcrService.PerformOcrAsync(
                    extractionResult.IdxFilePath,
                    extractionResult.SubFilePath,
                    ocrLanguage,
                    CancellationToken.None);

                if (!ocrResult.Success || string.IsNullOrWhiteSpace(ocrResult.ExtractedText))
                {
                    _logger.LogDebug("VobSub OCR failed for {FilePath}: {Error}", 
                        filePath, ocrResult.ErrorMessage);
                    return (false, null);
                }

                _logger.LogInformation("Successfully extracted {Length} characters from DVD subtitles (confidence: {Confidence:F2})",
                    ocrResult.ExtractedText.Length, ocrResult.ConfidenceScore);

                return (true, ocrResult.ExtractedText);
            }
            finally
            {
                // Clean up temporary files
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to clean up temporary VobSub files in {TempDir}", tempDir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "VobSub subtitle extraction failed for {FilePath}", filePath);
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
