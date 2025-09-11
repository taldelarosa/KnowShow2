using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Core;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Identify Season and Episode from AV1 video via PGS subtitle comparison. Optionally rename files with standardized naming.");

        var inputOption = new Option<FileInfo>(
            "--input",
            "Path to AV1 video file for identification, or subtitle file for storage (when using --store)");
        rootCommand.Add(inputOption);

        var hashDbOption = new Option<FileInfo>(
            "--hash-db",
            "Path to SQLite database for fuzzy hashes");
        rootCommand.Add(hashDbOption);

        var storeOption = new Option<bool>(
            "--store",
            "Store subtitle information in the hash database instead of identifying a video");
        rootCommand.Add(storeOption);

        var bulkOption = new Option<DirectoryInfo>(
            "--bulk-store",
            "Store all subtitle files from a directory, parsing series/season/episode from filenames");
        rootCommand.Add(bulkOption);

        var seriesOption = new Option<string>(
            "--series",
            "Series name when storing subtitle information")
        { IsRequired = false };
        rootCommand.Add(seriesOption);

        var seasonOption = new Option<string>(
            "--season",
            "Season number when storing subtitle information")
        { IsRequired = false };
        rootCommand.Add(seasonOption);

        var episodeOption = new Option<string>(
            "--episode",
            "Episode number when storing subtitle information")
        { IsRequired = false };
        rootCommand.Add(episodeOption);

        var languageOption = new Option<string>(
            "--language",
            "Preferred subtitle language (default: English)")
        { IsRequired = false };
        rootCommand.Add(languageOption);

        var renameOption = new Option<bool>(
            "--rename",
            "Automatically rename the video file when episode identification is successful. " +
            "Requires high confidence match (>= 90%). " +
            "Filename format: 'SeriesName - S01E01 - EpisodeName.ext'. " +
            "If rename fails, identification result will include error details and suggested filename.")
        { IsRequired = false };
        rootCommand.Add(renameOption);

        rootCommand.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption);
            var hashDb = context.ParseResult.GetValueForOption(hashDbOption);
            var store = context.ParseResult.GetValueForOption(storeOption);
            var bulkDirectory = context.ParseResult.GetValueForOption(bulkOption);
            var series = context.ParseResult.GetValueForOption(seriesOption);
            var season = context.ParseResult.GetValueForOption(seasonOption);
            var episode = context.ParseResult.GetValueForOption(episodeOption);
            var language = context.ParseResult.GetValueForOption(languageOption);
            var rename = context.ParseResult.GetValueForOption(renameOption);
            
            Environment.Exit(await HandleCommand(input, hashDb!, store, bulkDirectory, series, season, episode, language, rename));
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> HandleCommand(
        FileInfo? input,
        FileInfo hashDb,
        bool store,
        DirectoryInfo? bulkDirectory,
        string? series,
        string? season,
        string? episode,
        string? language,
        bool rename)
    {
        // Configure JSON serialization with camelCase for consistent API
        var jsonSerializationOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        
        // Validate input parameters
        if (bulkDirectory != null && input != null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "CONFLICTING_OPTIONS", message = "Cannot specify both --input and --bulk-store options" } }, jsonSerializationOptions));
            return 1;
        }

        if (bulkDirectory == null && input == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "MISSING_INPUT", message = "Must specify either --input or --bulk-store option" } }, jsonSerializationOptions));
            return 1;
        }

        if (input != null && !input.Exists)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "INVALID_INPUT", message = "Input file not found" } }, jsonSerializationOptions));
            return 1;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        var validator = new VideoFormatValidator(loggerFactory.CreateLogger<VideoFormatValidator>());
        var extractor = new SubtitleExtractor(loggerFactory.CreateLogger<SubtitleExtractor>(), validator);
        var pgsRipService = new PgsRipService(loggerFactory.CreateLogger<PgsRipService>());
        var fallbackConverter = new PgsToTextConverter(loggerFactory.CreateLogger<PgsToTextConverter>());
        var pgsConverter = new EnhancedPgsToTextConverter(loggerFactory.CreateLogger<EnhancedPgsToTextConverter>(), pgsRipService, fallbackConverter);
        var normalizationService = new SubtitleNormalizationService(loggerFactory.CreateLogger<SubtitleNormalizationService>());
        var hashService = new FuzzyHashService(hashDb.FullName, loggerFactory.CreateLogger<FuzzyHashService>(), normalizationService);
        var matcher = new SubtitleMatcher(hashService, loggerFactory.CreateLogger<SubtitleMatcher>());
        var filenameParser = new SubtitleFilenameParser(loggerFactory.CreateLogger<SubtitleFilenameParser>());
        var textExtractor = new VideoTextSubtitleExtractor(loggerFactory.CreateLogger<VideoTextSubtitleExtractor>());
        var filenameService = new FilenameService();
        var fileRenameService = new FileRenameService();

        try
        {
            if (store)
            {
                if (string.IsNullOrEmpty(series) || string.IsNullOrEmpty(season) || string.IsNullOrEmpty(episode))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "MISSING_METADATA", message = "Series, season, and episode are required when storing subtitles" } }));
                    return 1;
                }

                // Validate that the input file is a subtitle file, not a video file
                var videoExtensions = new[] { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
                var inputExtension = input!.Extension.ToLowerInvariant();

                if (videoExtensions.Contains(inputExtension))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "INVALID_FILE_TYPE", message = $"Cannot store video files in database. Only subtitle files (.srt, .vtt, .ass, etc.) are allowed for storage. Got: {inputExtension}" } }));
                    return 1;
                }

                // For storing, we expect a subtitle file directly
                var subtitleText = await File.ReadAllTextAsync(input!.FullName);
                var subtitle = new LabelledSubtitle
                {
                    Series = series,
                    Season = season,
                    Episode = episode,
                    SubtitleText = subtitleText
                };

                await hashService.StoreHash(subtitle);
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    message = "Subtitle stored successfully",
                    details = new
                    {
                        series,
                        season,
                        episode,
                        file = input!.Name
                    }
                }));
                return 0;
            }
            else if (bulkDirectory != null)
            {
                if (!bulkDirectory.Exists)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "DIRECTORY_NOT_FOUND", message = $"Directory not found: {bulkDirectory.FullName}" } }));
                    return 1;
                }

                // Scan directory for subtitle files and parse their information
                var subtitleFiles = await filenameParser.ScanDirectory(bulkDirectory.FullName);

                if (!subtitleFiles.Any())
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "NO_SUBTITLE_FILES", message = "No parseable subtitle files found in the directory" } }));
                    return 1;
                }

                var successCount = 0;
                var failureCount = 0;
                var results = new List<object>();

                foreach (var subtitleFile in subtitleFiles)
                {
                    try
                    {
                        var subtitleText = await File.ReadAllTextAsync(subtitleFile.FilePath);
                        var subtitle = new LabelledSubtitle
                        {
                            Series = subtitleFile.Series,
                            Season = subtitleFile.Season,
                            Episode = subtitleFile.Episode,
                            SubtitleText = subtitleText
                        };

                        await hashService.StoreHash(subtitle);
                        successCount++;
                        results.Add(new
                        {
                            status = "success",
                            file = Path.GetFileName(subtitleFile.FilePath),
                            series = subtitleFile.Series,
                            season = subtitleFile.Season,
                            episode = subtitleFile.Episode
                        });
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        results.Add(new
                        {
                            status = "failed",
                            file = Path.GetFileName(subtitleFile.FilePath),
                            error = ex.Message
                        });
                    }
                }

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    message = "Bulk ingestion completed",
                    summary = new
                    {
                        totalFiles = subtitleFiles.Count,
                        successful = successCount,
                        failed = failureCount
                    },
                    results
                }));
                return failureCount > 0 ? 1 : 0;
            }
            else
            {
                // Validate AV1 encoding first
                if (!await validator.IsAV1Encoded(input!.FullName))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        error = new
                        {
                            code = "UNSUPPORTED_FILE_TYPE",
                            message = "The provided file is not AV1 encoded. Non-AV1 files will be supported in a later release."
                        }
                    }));
                    return 1;
                }

                // Check if OCR tools are available
                if (!pgsConverter.IsOcrAvailable())
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        error = new
                        {
                            code = "MISSING_DEPENDENCY",
                            message = "Tesseract OCR is required but not available. Please install tesseract-ocr."
                        }
                    }));
                    return 1;
                }

                // Get subtitle track information for direct video processing
                var subtitleTracks = await validator.GetSubtitleTracks(input.FullName);
                
                if (!subtitleTracks.Any())
                {
                    // Try text subtitle fallback
                    var textSubtitleResult = await TryExtractTextSubtitle(input.FullName, language, validator, textExtractor, matcher, rename, filenameService, fileRenameService);
                    if (textSubtitleResult != null)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(textSubtitleResult, jsonSerializationOptions));
                        return textSubtitleResult.HasError ? 1 : 0;
                    }

                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "NO_SUBTITLES_FOUND", message = "No PGS or text subtitles could be found in the video file" } }, jsonSerializationOptions));
                    return 1;
                }

                var pgsTrack = PgsTrackSelector.SelectBestTrack(subtitleTracks, language);

                // Extract and OCR subtitle images directly from video file
                var ocrLanguage = GetOcrLanguageCode(language);
                var subtitleText = await pgsConverter.ConvertPgsFromVideoToText(input.FullName, pgsTrack.Index, ocrLanguage);

                if (string.IsNullOrWhiteSpace(subtitleText))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        error = new
                        {
                            code = "OCR_FAILED",
                            message = "Failed to extract readable text from PGS subtitles using OCR"
                        }
                    }));
                    return 1;
                }

                var result = await matcher.IdentifyEpisode(subtitleText);

                // Handle file renaming if --rename flag is specified
                if (rename && !result.HasError && result.MatchConfidence >= 0.9)
                {
                    try
                    {
                        // Generate filename using FilenameService
                        var filenameRequest = new FilenameGenerationRequest
                        {
                            Series = result.Series ?? "",
                            Season = result.Season ?? "",
                            Episode = result.Episode ?? "",
                            EpisodeName = result.EpisodeName ?? "",
                            FileExtension = Path.GetExtension(input!.FullName),
                            MatchConfidence = result.MatchConfidence
                        };

                        var filenameResult = filenameService.GenerateFilename(filenameRequest);
                        
                        if (filenameResult.IsValid && !string.IsNullOrEmpty(filenameResult.SuggestedFilename))
                        {
                            // Prepare file rename request
                            var renameRequest = new FileRenameRequest
                            {
                                OriginalPath = input.FullName,
                                SuggestedFilename = filenameResult.SuggestedFilename
                            };

                            try
                            {
                                // Attempt to rename the file
                                var renameResult = await fileRenameService.RenameFileAsync(renameRequest);
                                
                                if (renameResult.Success)
                                {
                                    // Update identification result with rename success
                                    result.SuggestedFilename = filenameResult.SuggestedFilename;
                                    result.FileRenamed = true;
                                    result.OriginalFilename = Path.GetFileName(input.FullName);
                                }
                                else
                                {
                                    // Include filename suggestion but set error for rename failure
                                    result.SuggestedFilename = filenameResult.SuggestedFilename;
                                    result.FileRenamed = false;
                                    
                                    // Set appropriate error based on rename failure type
                                    if (renameResult.ErrorType.HasValue)
                                    {
                                        result.Error = IdentificationError.FromFileRenameError(renameResult.ErrorType.Value, renameResult.ErrorMessage);
                                    }
                                    else
                                    {
                                        result.Error = IdentificationError.RenameFailedUnknown(renameResult.ErrorMessage ?? "Unknown rename error");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Handle unexpected exceptions during rename operation
                                result.SuggestedFilename = filenameResult.SuggestedFilename;
                                result.FileRenamed = false;
                                result.Error = IdentificationError.RenameFailedUnknown($"Unexpected error during file rename: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine(JsonSerializer.Serialize(new
                            {
                                warning = new
                                {
                                    code = "FILENAME_GENERATION_FAILED", 
                                    message = "File identification successful but filename generation failed"
                                }
                            }));
                        }
                    }
                    catch (Exception renameEx)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(new
                        {
                            warning = new
                            {
                                code = "RENAME_ERROR",
                                message = $"File identification successful but rename operation encountered an error: {renameEx.Message}"
                            }
                        }));
                    }
                }
                else if (rename && !result.HasError && result.MatchConfidence < 0.9)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        warning = new
                        {
                            code = "LOW_CONFIDENCE_RENAME",
                            message = $"File rename skipped due to low confidence match ({result.MatchConfidence:F2}). Requires confidence >= 0.9 for automatic renaming."
                        }
                    }));
                }

                Console.WriteLine(JsonSerializer.Serialize(result, jsonSerializationOptions));

                return result.HasError ? 1 : 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "PROCESSING_ERROR", message = ex.Message } }, jsonSerializationOptions));
            return 1;
        }
    }

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

    /// <summary>
    /// Attempts to extract text subtitles from the video when PGS subtitles are not available.
    /// </summary>
    private static async Task<IdentificationResult?> TryExtractTextSubtitle(
        string videoFilePath,
        string? language,
        VideoFormatValidator validator,
        VideoTextSubtitleExtractor textExtractor,
        SubtitleMatcher matcher,
        bool rename,
        FilenameService filenameService,
        FileRenameService fileRenameService)
    {
        try
        {
            // Get all subtitle tracks from the video
            var subtitleTracks = await validator.GetSubtitleTracks(videoFilePath);

            // Look for text-based subtitle tracks (non-PGS)
            var textTracks = subtitleTracks.Where(t =>
                t.CodecName != "hdmv_pgs_subtitle" &&
                (t.CodecName == "subrip" || t.CodecName == "ass" || t.CodecName == "webvtt" || t.CodecName == "mov_text" || t.CodecName == "srt"))
                .ToList();

            if (!textTracks.Any())
            {
                return null; // No text subtitle tracks found
            }

            // Select the best text track based on language preference
            var selectedTrack = textTracks.FirstOrDefault(t =>
                string.IsNullOrEmpty(language) ||
                (t.Language?.Contains(language, StringComparison.OrdinalIgnoreCase) == true)) ?? textTracks.First();

            // Extract text subtitle content
            var subtitleText = await textExtractor.ExtractTextSubtitleFromVideo(videoFilePath, selectedTrack.Index, language);

            if (string.IsNullOrWhiteSpace(subtitleText))
            {
                return null; // Failed to extract text content
            }

            // Match against database
            var result = await matcher.IdentifyEpisode(subtitleText);
            
            // Handle file renaming if --rename flag is specified
            if (rename && !result.HasError && result.MatchConfidence >= 0.9)
            {
                // Generate filename using FilenameService
                var filenameRequest = new FilenameGenerationRequest
                {
                    Series = result.Series ?? "",
                    Season = result.Season ?? "",
                    Episode = result.Episode ?? "",
                    EpisodeName = result.EpisodeName ?? "",
                    FileExtension = Path.GetExtension(videoFilePath),
                    MatchConfidence = result.MatchConfidence
                };

                var filenameResult = filenameService.GenerateFilename(filenameRequest);
                
                if (filenameResult.IsValid && !string.IsNullOrEmpty(filenameResult.SuggestedFilename))
                {
                    // Prepare file rename request
                    var renameRequest = new FileRenameRequest
                    {
                        OriginalPath = videoFilePath,
                        SuggestedFilename = filenameResult.SuggestedFilename
                    };

                    try
                    {
                        // Attempt to rename the file
                        var renameResult = await fileRenameService.RenameFileAsync(renameRequest);
                        
                        if (renameResult.Success)
                        {
                            // Update identification result with rename success
                            result.SuggestedFilename = filenameResult.SuggestedFilename;
                            result.FileRenamed = true;
                            result.OriginalFilename = Path.GetFileName(videoFilePath);
                        }
                        else
                        {
                            // Include filename suggestion but set error for rename failure
                            result.SuggestedFilename = filenameResult.SuggestedFilename;
                            result.FileRenamed = false;
                            
                            // Set appropriate error based on rename failure type
                            if (renameResult.ErrorType.HasValue)
                            {
                                result.Error = IdentificationError.FromFileRenameError(renameResult.ErrorType.Value, renameResult.ErrorMessage);
                            }
                            else
                            {
                                result.Error = IdentificationError.RenameFailedUnknown(renameResult.ErrorMessage ?? "Unknown rename error");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle unexpected exceptions during rename operation
                        result.SuggestedFilename = filenameResult.SuggestedFilename;
                        result.FileRenamed = false;
                        result.Error = IdentificationError.RenameFailedUnknown($"Unexpected error during file rename: {ex.Message}");
                    }
                }
            }
            
            return result;
        }
        catch (Exception)
        {
            return null; // Text subtitle extraction failed
        }
    }
}