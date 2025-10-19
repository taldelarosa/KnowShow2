using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Services.Hashing;
using EpisodeIdentifier.Core.Interfaces;
using System.IO.Abstractions;

namespace EpisodeIdentifier.Core;

public class Program
{
    // Static properties to make configuration services available throughout the application
    public static IConfigurationService? FuzzyHashConfigurationService { get; private set; }
    public static IAppConfigService? LegacyConfigurationService { get; private set; }

    /// <summary>
    /// Gets the fuzzy hash configuration if available and valid, otherwise returns null.
    /// Services can use this to check if fuzzy hashing is enabled and get configuration values.
    /// </summary>
    public static async Task<Models.Configuration.Configuration?> GetFuzzyHashConfigurationAsync()
    {
        if (FuzzyHashConfigurationService == null) return null;

        var result = await FuzzyHashConfigurationService.LoadConfiguration();
        return result.IsValid ? result.Configuration : null;
    }

    /// <summary>
    /// Checks if fuzzy hashing is available and properly configured.
    /// </summary>
    public static async Task<bool> IsFuzzyHashingEnabledAsync()
    {
        var config = await GetFuzzyHashConfigurationAsync();
        return config?.HashingAlgorithm == Models.Configuration.HashingAlgorithm.CTPH;
    }

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Identify Season and Episode from AV1 video via PGS subtitle comparison. Optionally rename files with standardized naming.");

        var inputOption = new Option<FileInfo>(
            "--input",
            "Path to AV1 video file for identification, or subtitle file for storage (when using --store)");
        rootCommand.Add(inputOption);

        var hashDbOption = new Option<FileInfo>(
            "--hash-db",
            "Path to SQLite database for fuzzy hashes")
        {
            IsRequired = false
        };
        // Use HASH_DB_PATH environment variable if set, otherwise default to production_hashes.db
        var defaultHashDbPath = Environment.GetEnvironmentVariable("HASH_DB_PATH") ?? "production_hashes.db";
        hashDbOption.SetDefaultValue(new FileInfo(defaultHashDbPath));
        rootCommand.Add(hashDbOption);

        var storeOption = new Option<bool>(
            "--store",
            "Store subtitle information in the hash database instead of identifying a video");
        rootCommand.Add(storeOption);

        var bulkStoreOption = new Option<DirectoryInfo>(
            "--bulk-store",
            "Store all subtitle files from a directory, parsing series/season/episode from filenames");
        rootCommand.Add(bulkStoreOption);

        var bulkIdentifyOption = new Option<DirectoryInfo>(
            "--bulk-identify",
            "Process all video files from a directory for episode identification. " +
            "Recursively searches for video files (.mkv, .mp4, .avi, etc.) and identifies each one. " +
            "Provides progress feedback and summary results.");
        rootCommand.Add(bulkIdentifyOption);

        var seriesOption = new Option<string>(
            "--series",
            "Series name when storing subtitle information, or filter by series during identification")
        { IsRequired = false };
        rootCommand.Add(seriesOption);

        var seasonOption = new Option<int?>(
            "--season",
            "Season number when storing subtitle information, or filter by season during identification (requires --series)")
        { IsRequired = false };

        // Add validator to ensure --season is only used with --series
        seasonOption.AddValidator(result =>
        {
            var seasonValue = result.GetValueOrDefault<int?>();
            if (seasonValue.HasValue)
            {
                // Check if --series was provided
                var seriesValue = result.FindResultFor(seriesOption)?.GetValueOrDefault<string>();
                if (string.IsNullOrWhiteSpace(seriesValue))
                {
                    result.ErrorMessage = "--season requires --series to be specified. Use --series <name> when providing --season <number>.";
                }
            }
        });
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

        // Add configuration management commands
        //         rootCommand.AddCommand(EpisodeIdentifier.Core.Commands.ConfigurationCommands.CreateConfigCommands());
        rootCommand.AddCommand(EpisodeIdentifier.Core.Commands.ConfigurationCommands.CreateConfigCommands());

        rootCommand.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption);
            var hashDb = context.ParseResult.GetValueForOption(hashDbOption);
            var store = context.ParseResult.GetValueForOption(storeOption);
            var bulkStoreDirectory = context.ParseResult.GetValueForOption(bulkStoreOption);
            var bulkIdentifyDirectory = context.ParseResult.GetValueForOption(bulkIdentifyOption);
            var series = context.ParseResult.GetValueForOption(seriesOption);
            var season = context.ParseResult.GetValueForOption(seasonOption);
            var episode = context.ParseResult.GetValueForOption(episodeOption);
            var language = context.ParseResult.GetValueForOption(languageOption);
            var rename = context.ParseResult.GetValueForOption(renameOption);

            Environment.Exit(await HandleCommand(input, hashDb!, store, bulkStoreDirectory, bulkIdentifyDirectory, series, season, episode, language, rename));
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> HandleCommand(
        FileInfo? input,
        FileInfo hashDb,
        bool store,
        DirectoryInfo? bulkStoreDirectory,
        DirectoryInfo? bulkIdentifyDirectory,
        string? series,
        int? season,
        string? episode,
        string? language,
        bool rename)
    {
        // Configure JSON serialization with camelCase for consistent API
        var jsonSerializationOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // Additional validation: --season requires --series
        if (season.HasValue && string.IsNullOrWhiteSpace(series))
        {
            Console.Error.WriteLine("--season requires --series to be specified.");
            return 1;
        }

        // Validate input parameters
        var bulkOptions = new[] { bulkStoreDirectory != null, bulkIdentifyDirectory != null }.Count(x => x);
        var hasInput = input != null;
        var totalInputOptions = bulkOptions + (hasInput ? 1 : 0);

        if (totalInputOptions > 1)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "CONFLICTING_OPTIONS", message = "Cannot specify multiple input options. Use either --input, --bulk-store, or --bulk-identify." } }, jsonSerializationOptions));
            return 1;
        }

        if (totalInputOptions == 0)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "MISSING_INPUT", message = "Must specify either --input, --bulk-store, or --bulk-identify option" } }, jsonSerializationOptions));
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

        // Load legacy configuration (backward compatibility)
        var legacyConfigService = new AppConfigService(loggerFactory.CreateLogger<AppConfigService>());
        await legacyConfigService.LoadConfigurationAsync();
        LegacyConfigurationService = legacyConfigService; // Make available to other services

        // Load new fuzzy hashing configuration
        // Use CONFIG_PATH environment variable if set, otherwise default to application directory
        var configPath = Environment.GetEnvironmentVariable("CONFIG_PATH");
        var fuzzyHashConfigService = new ConfigurationService(loggerFactory.CreateLogger<ConfigurationService>(), configFilePath: configPath);
        var fuzzyConfigResult = await fuzzyHashConfigService.LoadConfiguration();
        FuzzyHashConfigurationService = fuzzyHashConfigService; // Make available to other services

        // Log configuration loading results
        if (fuzzyConfigResult.IsValid)
        {
            loggerFactory.CreateLogger<Program>().LogInformation("Fuzzy hashing configuration loaded successfully");
        }
        else
        {
            loggerFactory.CreateLogger<Program>().LogWarning("Fuzzy hashing configuration failed to load: {Errors}. Falling back to legacy configuration.",
                string.Join(", ", fuzzyConfigResult.Errors));
        }

        var validator = new VideoFormatValidator(loggerFactory.CreateLogger<VideoFormatValidator>());
        var extractor = new SubtitleExtractor(loggerFactory.CreateLogger<SubtitleExtractor>(), validator);
        var pgsRipService = new PgsRipService(loggerFactory.CreateLogger<PgsRipService>());
        var fallbackConverter = new PgsToTextConverter(loggerFactory.CreateLogger<PgsToTextConverter>());
        var pgsConverter = new EnhancedPgsToTextConverter(loggerFactory.CreateLogger<EnhancedPgsToTextConverter>(), pgsRipService, fallbackConverter);
        var normalizationService = new SubtitleNormalizationService(loggerFactory.CreateLogger<SubtitleNormalizationService>());
        
        // ML embedding services for semantic similarity matching
        var modelManager = new ModelManager(loggerFactory.CreateLogger<ModelManager>());
        var embeddingService = new EmbeddingService(loggerFactory.CreateLogger<EmbeddingService>(), modelManager);
        var vectorSearchService = new VectorSearchService(loggerFactory.CreateLogger<VectorSearchService>(), hashDb.FullName);
        
        var hashService = new FuzzyHashService(hashDb.FullName, loggerFactory.CreateLogger<FuzzyHashService>(), normalizationService);
        var filenameParser = new SubtitleFilenameParser(loggerFactory.CreateLogger<SubtitleFilenameParser>(), legacyConfigService);
        var textExtractor = new VideoTextSubtitleExtractor(loggerFactory.CreateLogger<VideoTextSubtitleExtractor>());
        var filenameService = new FilenameService(legacyConfigService);
        var fileRenameService = new FileRenameService();

        // Create enhanced services for modern CTPH + text fallback functionality
        var fileSystem = new System.IO.Abstractions.FileSystem();
        var ctphHashingService = new CTPhHashingService(loggerFactory.CreateLogger<CTPhHashingService>(), fileSystem);
        var enhancedCtphService = new EnhancedCTPhHashingService(
            ctphHashingService,
            hashService,
            loggerFactory.CreateLogger<EnhancedCTPhHashingService>(),
            fuzzyHashConfigService);

        // Create episode identification service that uses CTPH fuzzy hashing with text fallback
        var episodeIdentificationService = new EpisodeIdentificationService(
            loggerFactory.CreateLogger<EpisodeIdentificationService>(),
            fileSystem,
            enhancedCtphService);

        try
        {
            if (store)
            {
                if (string.IsNullOrEmpty(series) || !season.HasValue || string.IsNullOrEmpty(episode))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "MISSING_METADATA", message = "Series, season, and episode are required when storing subtitles" } }));
                    return 1;
                }

                // Convert season number to zero-padded string format (e.g., 1 -> "01")
                var seasonString = season.Value.ToString("D2");

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
                    Season = seasonString,
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
                        season = seasonString,
                        episode,
                        file = input!.Name
                    }
                }));
                return 0;
            }
            else if (bulkStoreDirectory != null)
            {
                if (!bulkStoreDirectory.Exists)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "DIRECTORY_NOT_FOUND", message = $"Directory not found: {bulkStoreDirectory.FullName}" } }));
                    return 1;
                }

                // Scan directory for subtitle files and parse their information
                var subtitleFiles = await filenameParser.ScanDirectory(bulkStoreDirectory.FullName);

                if (!subtitleFiles.Any())
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "NO_SUBTITLE_FILES", message = "No parseable subtitle files found in the directory" } }));
                    return 1;
                }

                // Log initial scan results
                Console.Error.WriteLine($"Found {subtitleFiles.Count} subtitle files to import");
                Console.Error.WriteLine($"Starting bulk import from: {bulkStoreDirectory.FullName}");
                Console.Error.WriteLine();
                Console.Error.Flush();

                var successCount = 0;
                var failureCount = 0;
                var results = new List<object>();
                var totalFiles = subtitleFiles.Count;
                var overallTimer = System.Diagnostics.Stopwatch.StartNew();

                foreach (var subtitleFile in subtitleFiles)
                {
                    var currentIndex = successCount + failureCount + 1;
                    var fileTimer = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        // Log progress for each file
                        Console.Error.WriteLine($"[{currentIndex}/{totalFiles}] Processing: {Path.GetFileName(subtitleFile.FilePath)}");
                        Console.Error.WriteLine($"           Series: {subtitleFile.Series}, S{subtitleFile.Season:D2}E{subtitleFile.Episode:D2}");
                        Console.Error.Flush();

                        var readTimer = System.Diagnostics.Stopwatch.StartNew();
                        var subtitleText = await File.ReadAllTextAsync(subtitleFile.FilePath);
                        readTimer.Stop();

                        var subtitle = new LabelledSubtitle
                        {
                            Series = subtitleFile.Series,
                            Season = subtitleFile.Season,
                            Episode = subtitleFile.Episode,
                            EpisodeName = subtitleFile.EpisodeName,
                            SubtitleText = subtitleText
                        };

                        var storeTimer = System.Diagnostics.Stopwatch.StartNew();
                        await hashService.StoreHash(subtitle);
                        storeTimer.Stop();

                        successCount++;
                        fileTimer.Stop();
                        Console.Error.WriteLine($"           ✓ Successfully stored (read: {readTimer.ElapsedMilliseconds}ms, store: {storeTimer.ElapsedMilliseconds}ms, total: {fileTimer.ElapsedMilliseconds}ms)");
                        Console.Error.WriteLine();
                        Console.Error.Flush();

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
                        fileTimer.Stop();
                        Console.Error.WriteLine($"           ✗ Failed after {fileTimer.ElapsedMilliseconds}ms: {ex.Message}");
                        Console.Error.WriteLine();
                        Console.Error.Flush();

                        results.Add(new
                        {
                            status = "failed",
                            file = Path.GetFileName(subtitleFile.FilePath),
                            error = ex.Message
                        });
                    }
                }

                overallTimer.Stop();

                // Log final summary to stderr
                Console.Error.WriteLine("=".PadRight(60, '='));
                Console.Error.WriteLine($"Bulk Import Summary:");
                Console.Error.WriteLine($"  Total Files:  {totalFiles}");
                Console.Error.WriteLine($"  Successful:   {successCount}");
                Console.Error.WriteLine($"  Failed:       {failureCount}");
                Console.Error.WriteLine($"  Total Time:   {overallTimer.Elapsed.TotalSeconds:F2}s");
                if (successCount > 0)
                {
                    Console.Error.WriteLine($"  Avg per file: {overallTimer.Elapsed.TotalMilliseconds / successCount:F0}ms");
                }
                Console.Error.WriteLine("=".PadRight(60, '='));
                Console.Error.WriteLine();
                Console.Error.Flush();

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
            else if (bulkIdentifyDirectory != null)
            {
                if (!bulkIdentifyDirectory.Exists)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "DIRECTORY_NOT_FOUND", message = $"Directory not found: {bulkIdentifyDirectory.FullName}" } }));
                    return 1;
                }

                // Use bulk processor service for video file identification
                var localFileSystem = new FileSystem();
                var fileDiscoveryService = new FileDiscoveryService(localFileSystem, loggerFactory.CreateLogger<FileDiscoveryService>());
                var progressTracker = new ProgressTracker(loggerFactory.CreateLogger<ProgressTracker>());

                // Create the complete video file processing service
                var videoFileProcessingService = new VideoFileProcessingService(
                    loggerFactory.CreateLogger<VideoFileProcessingService>(),
                    validator,
                    extractor,
                    pgsConverter,
                    textExtractor,
                    episodeIdentificationService,
                    filenameService,
                    fileRenameService,
                    legacyConfigService);

                var bulkProcessor = new BulkProcessorService(
                    loggerFactory.CreateLogger<BulkProcessorService>(),
                    fileDiscoveryService,
                    progressTracker,
                    videoFileProcessingService,
                    localFileSystem);

                // Create BulkProcessingOptions with config-based concurrency
                var bulkProcessingOptions = await BulkProcessingOptions.CreateFromConfigurationAsync(legacyConfigService);
                bulkProcessingOptions.Recursive = true;
                bulkProcessingOptions.IncludeExtensions = new List<string> { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
                bulkProcessingOptions.ContinueOnError = true;

                var request = new BulkProcessingRequest
                {
                    Paths = new List<string> { bulkIdentifyDirectory.FullName },
                    Options = bulkProcessingOptions
                };

                // Set up progress reporting
                var progressReporter = new Progress<BulkProcessingProgress>(progress =>
                {
                    Console.Error.WriteLine($"Progress: {progress.ProcessedFiles}/{progress.TotalFiles} files " +
                                          $"({progress.PercentComplete:F1}%) - {progress.CurrentPhase}");
                    if (!string.IsNullOrEmpty(progress.CurrentFile))
                    {
                        Console.Error.WriteLine($"  Processing: {Path.GetFileName(progress.CurrentFile)}");
                    }
                });

                try
                {
                    var result = await bulkProcessor.ProcessAsync(request, progressReporter);

                    // Output JSON results
                    var jsonResult = new
                    {
                        message = "Bulk identification completed",
                        status = result.Status.ToString(),
                        summary = new
                        {
                            totalFiles = result.TotalFiles,
                            processedFiles = result.ProcessedFiles,
                            failedFiles = result.FailedFiles,
                            skippedFiles = result.SkippedFiles,
                            processingTime = result.Duration.ToString(@"mm\:ss")
                        },
                        results = result.GetFileResultsAsList().Select(fr => new
                        {
                            file = fr.FilePath,  // Use full path for bulk imports from multiple folders
                            fileName = Path.GetFileName(fr.FilePath),  // Also include just the filename for convenience
                            status = fr.Status.ToString(),
                            error = fr.Error?.Message,
                            processingTime = fr.ProcessingDuration.ToString(@"ss\.fff"),
                            // For now, we'll handle identification results as generic objects
                            // until the actual identification service integration is complete
                            identificationResults = fr.IdentificationResults.Count > 0 ? fr.IdentificationResults : null
                        }).ToList()
                    };

                    Console.WriteLine(JsonSerializer.Serialize(jsonResult, jsonSerializationOptions));
                    return result.FailedFiles > 0 ? 1 : 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "BULK_PROCESSING_FAILED", message = ex.Message } }, jsonSerializationOptions));
                    return 1;
                }
            }
            else
            {
                // Validate file format first
                var validationResult = await validator.ValidateForProcessing(input!.FullName);
                if (!validationResult.IsValid)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        error = new
                        {
                            code = validationResult.ErrorCode,
                            message = validationResult.ErrorMessage
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
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "NO_SUBTITLES_FOUND", message = "No subtitles could be found in the video file" } }, jsonSerializationOptions));
                    return 1;
                }

                // Priority 1: Try text subtitle processing first (fastest and most reliable)
                var textSubtitleResult = await TryExtractTextSubtitle(input.FullName, language, validator, textExtractor, episodeIdentificationService, rename, filenameService, fileRenameService, legacyConfigService, series, season);
                if (textSubtitleResult != null)
                {
                    Console.WriteLine(JsonSerializer.Serialize(textSubtitleResult, jsonSerializationOptions));
                    return textSubtitleResult.HasError ? 1 : 0;
                }

                // Priority 2: Try PGS subtitle processing
                var pgsTracks = subtitleTracks.Where(t =>
                    t.CodecName == "hdmv_pgs_subtitle").ToList();

                if (pgsTracks.Any())
                {
                    var pgsTrack = PgsTrackSelector.SelectBestTrack(pgsTracks, language);

                    // Extract and OCR subtitle images directly from video file
                    var ocrLanguage = GetOcrLanguageCode(language);
                    var subtitleText = await pgsConverter.ConvertPgsFromVideoToText(input.FullName, pgsTrack.Index, ocrLanguage);

                    if (!string.IsNullOrWhiteSpace(subtitleText))
                    {
                        // PGS extraction successful - proceed with identification
                        goto ProcessIdentification;
                    }
                }

                // Priority 3: Try DVD subtitle processing
                var dvdSubtitleResult = await TryExtractDvdSubtitle(input.FullName, language, validator, episodeIdentificationService, rename, filenameService, fileRenameService, legacyConfigService, series, season, loggerFactory);
                if (dvdSubtitleResult != null)
                {
                    Console.WriteLine(JsonSerializer.Serialize(dvdSubtitleResult, jsonSerializationOptions));
                    return dvdSubtitleResult.HasError ? 1 : 0;
                }

                // No subtitle format worked
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    error = new
                    {
                        code = "NO_SUPPORTED_SUBTITLES",
                        message = "Failed to extract text from any available subtitle format (text, PGS, or DVD)"
                    }
                }));
                return 1;

            ProcessIdentification:
                // PGS subtitle text is available - proceed with identification
                var ocrLang = GetOcrLanguageCode(language);
                var pgsSubtitleText = await pgsConverter.ConvertPgsFromVideoToText(input.FullName, pgsTracks.First().Index, ocrLang);

                if (string.IsNullOrWhiteSpace(pgsSubtitleText))
                {
                    // Shouldn't happen since we checked above, but handle it
                    var dvdFallback = await TryExtractDvdSubtitle(input.FullName, language, validator, episodeIdentificationService, rename, filenameService, fileRenameService, legacyConfigService, series, season, loggerFactory);
                    if (dvdFallback != null)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(dvdFallback, jsonSerializationOptions));
                        return dvdFallback.HasError ? 1 : 0;
                    }

                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        error = new
                        {
                            code = "OCR_FAILED",
                            message = "Failed to extract readable text from subtitles"
                        }
                    }));
                    return 1;
                }

                // Use the episode identification service that supports both legacy and CTPH fuzzy hashing
                IdentificationResult result;
                try
                {
                    result = await episodeIdentificationService.IdentifyEpisodeAsync(
                        pgsSubtitleText, 
                        SubtitleType.PGS, 
                        input.FullName, 
                        null, 
                        series, 
                        season);
                }
                catch (ArgumentException ex)
                {
                    // Handle validation errors from FindMatches (e.g., season without series)
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }

                // Handle empty results when series filter doesn't match (return empty JSON array)
                if (!result.HasError && result.MatchConfidence == 0 && !string.IsNullOrWhiteSpace(series))
                {
                    Console.WriteLine("[]");
                    return 0;
                }

                // Get the appropriate rename threshold based on subtitle type
                var pgsRenameThreshold = legacyConfigService.Config.MatchingThresholds?.PGS.RenameConfidence 
#pragma warning disable CS0618 // Type or member is obsolete
                    ?? (decimal)legacyConfigService.Config.RenameConfidenceThreshold;
#pragma warning restore CS0618

                // Handle file renaming if --rename flag is specified
                if (rename && !result.HasError && (decimal)result.MatchConfidence >= pgsRenameThreshold)
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
        EpisodeIdentificationService episodeIdentificationService,
        bool rename,
        FilenameService filenameService,
        FileRenameService fileRenameService,
        IAppConfigService legacyConfigService,
        string? seriesFilter = null,
        int? seasonFilter = null)
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

            // Match against database using the episode identification service
            var result = await episodeIdentificationService.IdentifyEpisodeAsync(
                subtitleText, 
                SubtitleType.TextBased, 
                videoFilePath, 
                null, 
                seriesFilter, 
                seasonFilter);

            // Get the appropriate rename threshold based on subtitle type
            var renameThreshold = legacyConfigService.Config.MatchingThresholds?.TextBased.RenameConfidence 
#pragma warning disable CS0618 // Type or member is obsolete
                ?? (decimal)legacyConfigService.Config.RenameConfidenceThreshold;
#pragma warning restore CS0618

            // Handle file renaming if --rename flag is specified
            if (rename && !result.HasError && (decimal)result.MatchConfidence >= renameThreshold)
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

    /// <summary>
    /// Attempts to extract DVD subtitles (VobSub) from the video when text and PGS subtitles are not available.
    /// </summary>
    private static async Task<IdentificationResult?> TryExtractDvdSubtitle(
        string videoFilePath,
        string? language,
        VideoFormatValidator validator,
        EpisodeIdentificationService episodeIdentificationService,
        bool rename,
        FilenameService filenameService,
        FileRenameService fileRenameService,
        IAppConfigService legacyConfigService,
        string? seriesFilter = null,
        int? seasonFilter = null,
        ILoggerFactory? loggerFactory = null)
    {
        try
        {
            // Get all subtitle tracks from the video
            var subtitleTracks = await validator.GetSubtitleTracks(videoFilePath);

            // Look for DVD subtitle tracks
            var dvdTracks = subtitleTracks.Where(t => t.CodecName == "dvd_subtitle").ToList();

            if (!dvdTracks.Any())
            {
                return null; // No DVD subtitle tracks found
            }

            // Check for required dependencies
            var vobSubExtractor = loggerFactory != null
                ? new VobSubExtractor(loggerFactory.CreateLogger<VobSubExtractor>())
                : new VobSubExtractor(LoggerFactory.Create(builder => { }).CreateLogger<VobSubExtractor>());

            var vobSubOcrService = loggerFactory != null
                ? new VobSubOcrService(loggerFactory.CreateLogger<VobSubOcrService>())
                : new VobSubOcrService(LoggerFactory.Create(builder => { }).CreateLogger<VobSubOcrService>());

            if (!await vobSubExtractor.IsMkvExtractAvailableAsync())
            {
                return null; // mkvextract not available, cannot process DVD subtitles
            }

            if (!await vobSubOcrService.IsTesseractAvailableAsync())
            {
                return null; // Tesseract not available, cannot OCR DVD subtitles
            }

            // Select the best DVD track based on language preference
            var selectedTrack = dvdTracks.FirstOrDefault(t =>
                string.IsNullOrEmpty(language) ||
                (t.Language?.Contains(language, StringComparison.OrdinalIgnoreCase) == true)) ?? dvdTracks.First();

            // Create temporary directory for VobSub extraction
            // Use current directory or home directory instead of /tmp to avoid snap confinement issues
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
            {
                baseDir = Directory.GetCurrentDirectory();
            }
            var tempDir = Path.Combine(baseDir, ".episodeidentifier_temp", $"vobsub_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Extract VobSub files using VobSubExtractor
                var extractionResult = await vobSubExtractor.ExtractAsync(
                    videoFilePath,
                    selectedTrack.Index,
                    tempDir,
                    CancellationToken.None);

                if (!extractionResult.Success || string.IsNullOrEmpty(extractionResult.IdxFilePath) || string.IsNullOrEmpty(extractionResult.SubFilePath))
                {
                    return null; // VobSub extraction failed
                }

                // Perform OCR on extracted VobSub files
                var ocrLanguage = vobSubOcrService.GetOcrLanguageCode(language ?? "eng");
                var ocrResult = await vobSubOcrService.PerformOcrAsync(
                    extractionResult.IdxFilePath,
                    extractionResult.SubFilePath,
                    ocrLanguage,
                    CancellationToken.None);

                if (!ocrResult.Success || string.IsNullOrWhiteSpace(ocrResult.ExtractedText))
                {
                    return null; // OCR failed or no text extracted
                }

                // Match against database using the episode identification service
                var result = await episodeIdentificationService.IdentifyEpisodeAsync(
                    ocrResult.ExtractedText,
                    SubtitleType.VobSub,
                    videoFilePath,
                    null,
                    seriesFilter,
                    seasonFilter);

                // Get the appropriate rename threshold based on subtitle type
                var renameThreshold = legacyConfigService.Config.MatchingThresholds?.VobSub.RenameConfidence 
#pragma warning disable CS0618 // Type or member is obsolete
                    ?? (decimal)legacyConfigService.Config.RenameConfidenceThreshold;
#pragma warning restore CS0618

                // Handle file renaming if --rename flag is specified
                if (rename && !result.HasError && (decimal)result.MatchConfidence >= renameThreshold)
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
            finally
            {
                // Clean up temporary directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch (Exception)
        {
            return null; // DVD subtitle extraction failed
        }
    }
}
