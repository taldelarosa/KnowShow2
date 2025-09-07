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
        var rootCommand = new RootCommand("Identify Season and Episode from AV1 video via PGS subtitle comparison");

        var inputOption = new Option<FileInfo>(
            "--input",
            "Path to AV1 video file or subtitle file when using --store");
        rootCommand.Add(inputOption);

        var subDbOption = new Option<DirectoryInfo>(
            "--sub-db",
            "Path to root of known subtitles (Subtitles=>Series=>Season)");
        rootCommand.Add(subDbOption);

        var hashDbOption = new Option<FileInfo>(
            "--hash-db",
            "Path to SQLite database for fuzzy hashes");
        rootCommand.Add(hashDbOption);

        var storeOption = new Option<bool>(
            "--store",
            "Store subtitle information in the hash database instead of identifying a video");
        rootCommand.Add(storeOption);

        var seriesOption = new Option<string>(
            "--series",
            "Series name when storing subtitle information") { IsRequired = false };
        rootCommand.Add(seriesOption);

        var seasonOption = new Option<string>(
            "--season",
            "Season number when storing subtitle information") { IsRequired = false };
        rootCommand.Add(seasonOption);

        var episodeOption = new Option<string>(
            "--episode",
            "Episode number when storing subtitle information") { IsRequired = false };
        rootCommand.Add(episodeOption);

        var languageOption = new Option<string>(
            "--language",
            "Preferred subtitle language (default: English)") { IsRequired = false };
        rootCommand.Add(languageOption);

        rootCommand.SetHandler(HandleCommand, inputOption, subDbOption, hashDbOption, storeOption, seriesOption, seasonOption, episodeOption, languageOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> HandleCommand(
        FileInfo input, 
        DirectoryInfo subDb, 
        FileInfo hashDb, 
        bool store,
        string? series,
        string? season,
        string? episode,
        string? language)
    {
        if (!input.Exists)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "INVALID_INPUT", message = "Input file not found" } }));
            return 1;
        }

        if (!store && !subDb.Exists)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "INVALID_PATH", message = "Subtitle database path not found" } }));
            return 1;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        var validator = new VideoFormatValidator(loggerFactory.CreateLogger<VideoFormatValidator>());
        var extractor = new SubtitleExtractor(loggerFactory.CreateLogger<SubtitleExtractor>(), validator);
        var pgsConverter = new PgsToTextConverter(loggerFactory.CreateLogger<PgsToTextConverter>());
        var hashService = new FuzzyHashService(hashDb.FullName, loggerFactory.CreateLogger<FuzzyHashService>());
        var matcher = new SubtitleMatcher(hashService, loggerFactory.CreateLogger<SubtitleMatcher>());

        try
        {
            if (store)
            {
                if (string.IsNullOrEmpty(series) || string.IsNullOrEmpty(season) || string.IsNullOrEmpty(episode))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "MISSING_METADATA", message = "Series, season, and episode are required when storing subtitles" } }));
                    return 1;
                }

                // For storing, we expect a subtitle file directly
                var subtitleText = await File.ReadAllTextAsync(input.FullName);
                var subtitle = new LabelledSubtitle
                {
                    Series = series,
                    Season = season,
                    Episode = episode,
                    SubtitleText = subtitleText
                };

                await hashService.StoreHash(subtitle);
                Console.WriteLine(JsonSerializer.Serialize(new { 
                    message = "Subtitle stored successfully",
                    details = new {
                        series,
                        season,
                        episode,
                        file = input.Name
                    }
                }));
                return 0;
            }
            else
            {
                // Validate AV1 encoding first
                if (!await validator.IsAV1Encoded(input.FullName))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { 
                        error = new { 
                            code = "UNSUPPORTED_FILE_TYPE", 
                            message = "The provided file is not AV1 encoded. Non-AV1 files will be supported in a later release." 
                        } 
                    }));
                    return 1;
                }

                // Check if OCR tools are available
                if (!pgsConverter.IsOcrAvailable())
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { 
                        error = new { 
                            code = "MISSING_DEPENDENCY", 
                            message = "Tesseract OCR is required but not available. Please install tesseract-ocr." 
                        } 
                    }));
                    return 1;
                }

                var subtitleBytes = await extractor.ExtractPgsSubtitles(input.FullName, language);
                if (subtitleBytes.Length == 0)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "NO_SUBTITLES_FOUND", message = "No PGS subtitles could be extracted from the video file" } }));
                    return 1;
                }

                // Convert PGS subtitle bytes to text using OCR
                var ocrLanguage = GetOcrLanguageCode(language);
                var subtitleText = await pgsConverter.ConvertPgsToText(subtitleBytes, ocrLanguage);
                
                if (string.IsNullOrWhiteSpace(subtitleText))
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { 
                        error = new { 
                            code = "OCR_FAILED", 
                            message = "Failed to extract readable text from PGS subtitles using OCR" 
                        } 
                    }));
                    return 1;
                }

                var result = await matcher.IdentifyEpisode(subtitleText);
                Console.WriteLine(JsonSerializer.Serialize(result));

                return result.HasError ? 1 : 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "PROCESSING_ERROR", message = ex.Message } }));
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
}