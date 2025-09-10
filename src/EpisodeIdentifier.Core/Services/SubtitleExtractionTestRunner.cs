using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Test class to verify text subtitle extraction functionality.
/// </summary>
public static class SubtitleExtractionTestRunner
{
    public static async Task RunTest(string videoPath)
    {
        Console.WriteLine("=== Text Subtitle Extraction Test ===");
        Console.WriteLine($"Testing with video: {videoPath}");

        // Set up format handlers
        var formatHandlers = new List<ISubtitleFormatHandler>
        {
            new SrtFormatHandler(),
            new AssFormatHandler(),
            new VttFormatHandler()
        };

        // Create the text subtitle extractor
        var extractor = new TextSubtitleExtractor(formatHandlers);

        try
        {
            Console.WriteLine("\n1. Detecting text subtitle tracks...");
            var tracks = await extractor.DetectTextSubtitleTracksAsync(videoPath);

            Console.WriteLine($"Found {tracks.Count} subtitle tracks:");
            foreach (var track in tracks)
            {
                Console.WriteLine($"  - Track {track.Index}: {track.Language} ({track.Format}) - {track.FilePath}");
                Console.WriteLine($"    Source: {track.SourceType}, Default: {track.IsDefault}, Forced: {track.IsForced}");
            }

            if (!tracks.Any())
            {
                Console.WriteLine("No subtitle tracks found. Let's check the directory...");
                var videoDir = Path.GetDirectoryName(videoPath);
                if (!string.IsNullOrEmpty(videoDir))
                {
                    var files = Directory.GetFiles(videoDir, "*.*", SearchOption.TopDirectoryOnly);
                    Console.WriteLine("Files in directory:");
                    foreach (var file in files)
                    {
                        Console.WriteLine($"  - {Path.GetFileName(file)}");
                    }
                }
                return;
            }

            Console.WriteLine("\n2. Extracting content from first track...");
            var firstTrack = tracks.First();
            var extractionResult = await extractor.ExtractTextSubtitleContentAsync(videoPath, firstTrack);

            Console.WriteLine($"Extraction status: {extractionResult.Status}");
            Console.WriteLine($"Successful extractions: {extractionResult.SuccessfulExtractions}");
            Console.WriteLine($"Failed extractions: {extractionResult.FailedExtractions}");

            if (extractionResult.ExtractedTracks.Any())
            {
                var extractedTrack = extractionResult.ExtractedTracks.First();
                Console.WriteLine($"Extracted {extractedTrack.SubtitleCount} subtitle entries");
                Console.WriteLine($"Track status: {extractedTrack.Status}");

                // Show first few lines of content
                var lines = extractedTrack.Content.Split('\n').Take(5);
                Console.WriteLine("\nFirst few lines of content:");
                foreach (var line in lines)
                {
                    Console.WriteLine($"  {line}");
                }
            }

            Console.WriteLine("\n3. Extracting all available tracks...");
            var allResults = await extractor.TryExtractAllTextSubtitlesAsync(videoPath);

            Console.WriteLine($"Overall extraction status: {allResults.Status}");
            Console.WriteLine($"Total tracks found: {allResults.TotalTrackCount}");
            Console.WriteLine($"Successful extractions: {allResults.SuccessfulExtractions}");
            Console.WriteLine($"Failed extractions: {allResults.FailedExtractions}");
            Console.WriteLine($"Processing time: {allResults.ProcessingTimeMs}ms");

            foreach (var track in allResults.ExtractedTracks)
            {
                Console.WriteLine($"\nTrack {track.Index} ({track.Language}):");
                Console.WriteLine($"  Format: {track.Format}");
                Console.WriteLine($"  Entries: {track.SubtitleCount}");
                Console.WriteLine($"  Status: {track.Status}");
                if (!string.IsNullOrEmpty(track.ErrorMessage))
                {
                    Console.WriteLine($"  Error: {track.ErrorMessage}");
                }
            }

            Console.WriteLine("\n=== Test completed successfully! ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
