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
            "Path to AV1 video file");
        rootCommand.Add(inputOption);

        var subDbOption = new Option<DirectoryInfo>(
            "--sub-db",
            "Path to root of known subtitles (Subtitles=>Series=>Season)");
        rootCommand.Add(subDbOption);

        var hashDbOption = new Option<FileInfo>(
            "--hash-db",
            "Path to SQLite database for fuzzy hashes");
        rootCommand.Add(hashDbOption);

        rootCommand.SetHandler(HandleCommand, inputOption, subDbOption, hashDbOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> HandleCommand(FileInfo input, DirectoryInfo subDb, FileInfo hashDb)
    {
        if (!input.Exists)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "INVALID_INPUT", message = "Input video file not found" } }));
            return 1;
        }

        if (!subDb.Exists)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "INVALID_PATH", message = "Subtitle database path not found" } }));
            return 1;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        var extractor = new SubtitleExtractor(loggerFactory.CreateLogger<SubtitleExtractor>());
        var hashService = new FuzzyHashService(hashDb.FullName, loggerFactory.CreateLogger<FuzzyHashService>());
        var matcher = new SubtitleMatcher(hashService, loggerFactory.CreateLogger<SubtitleMatcher>());

        try
        {
            var subtitleBytes = await extractor.ExtractPgsSubtitles(input.FullName);
            if (subtitleBytes.Length == 0)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "NO_SUBTITLES_FOUND", message = "No PGS subtitles could be extracted from the video file" } }));
                return 1;
            }

            // TODO: Convert PGS subtitle to text (requires additional tooling/library)
            var subtitleText = Convert.ToBase64String(subtitleBytes); // Temporary placeholder

            var result = await matcher.IdentifyEpisode(subtitleText);
            Console.WriteLine(JsonSerializer.Serialize(result));

            return result.HasError ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { error = new { code = "PROCESSING_ERROR", message = ex.Message } }));
            return 1;
        }
    }
}