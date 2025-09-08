using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Core.Tests;

/// <summary>
/// Demo program to compare PGS conversion methods
/// </summary>
public class PgsConversionDemo
{
    public static async Task RunDemo(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddScoped<PgsToTextConverter>();
        services.AddScoped<PgsRipService>();
        services.AddScoped<EnhancedPgsToTextConverter>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<PgsConversionDemo>>();
        var enhancedConverter = serviceProvider.GetRequiredService<EnhancedPgsToTextConverter>();

        logger.LogInformation("PGS Conversion Quality Comparison Demo");
        logger.LogInformation("=====================================");

        // Show quality information
        var qualityInfo = await enhancedConverter.GetQualityInfoAsync();
        logger.LogInformation(qualityInfo.ToString());

        if (args.Length == 0)
        {
            logger.LogInformation("Usage: PgsConversionDemo <video_file_or_sup_file>");
            logger.LogInformation("Example: PgsConversionDemo movie.mkv");
            logger.LogInformation("Example: PgsConversionDemo subtitles.sup");
            return;
        }

        var inputFile = args[0];
        var language = args.Length > 1 ? args[1] : "eng";

        if (!File.Exists(inputFile))
        {
            logger.LogError("File not found: {InputFile}", inputFile);
            return;
        }

        logger.LogInformation("Processing file: {InputFile}", inputFile);
        logger.LogInformation("Language: {Language}", language);

        var extension = Path.GetExtension(inputFile).ToLowerInvariant();
        
        try
        {
            string result;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (extension == ".sup")
            {
                // Process SUP file
                var supData = await File.ReadAllBytesAsync(inputFile);
                result = await enhancedConverter.ConvertPgsToText(supData, language);
            }
            else if (extension == ".mkv" || extension == ".mks")
            {
                // Process video file (assuming subtitle track 0)
                result = await enhancedConverter.ConvertPgsFromVideoToText(inputFile, 0, language);
            }
            else
            {
                logger.LogError("Unsupported file type. Supported: .sup, .mkv, .mks");
                return;
            }

            stopwatch.Stop();

            logger.LogInformation("Conversion completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            logger.LogInformation("Result length: {Length} characters", result.Length);

            if (!string.IsNullOrEmpty(result))
            {
                // Save result to file
                var outputFile = Path.ChangeExtension(inputFile, ".extracted.srt");
                await File.WriteAllTextAsync(outputFile, result);
                logger.LogInformation("Saved result to: {OutputFile}", outputFile);

                // Show preview
                var lines = result.Split('\n');
                var previewLines = lines.Take(Math.Min(20, lines.Length));
                
                logger.LogInformation("Preview (first 20 lines):");
                logger.LogInformation("========================");
                foreach (var line in previewLines)
                {
                    Console.WriteLine(line);
                }

                if (lines.Length > 20)
                {
                    logger.LogInformation("... ({MoreLines} more lines)", lines.Length - 20);
                }
            }
            else
            {
                logger.LogWarning("No text was extracted from the file");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing file");
        }
    }
}
