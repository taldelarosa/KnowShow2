using System;
using System.IO;
using System.Threading.Tasks;
using EpisodeIdentifier.Core.Services;

namespace PgsComparisonTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string videoPath = "/mnt/c/src/KnowShow/TestData/media/video.mkv";
            string outputDir = "/tmp/comparison_test";
            
            Console.WriteLine("=== PGS Subtitle Extraction Comparison Test ===");
            Console.WriteLine($"Video: {videoPath}");
            Console.WriteLine($"Output: {outputDir}");
            Console.WriteLine();
            
            // Test new enhanced method
            Console.WriteLine("Testing Enhanced PgsToTextConverter (with pgsrip)...");
            var enhancedConverter = new EnhancedPgsToTextConverter(new PgsRipService(), new PgsToTextConverter());
            
            try
            {
                var result = await enhancedConverter.ConvertPgsFromVideoToText(videoPath, outputDir, 5);
                
                Console.WriteLine($"✓ Enhanced method succeeded!");
                Console.WriteLine($"  Method used: {result.Method}");
                Console.WriteLine($"  Processing time: {result.ProcessingTime}");
                Console.WriteLine($"  Output file: {result.OutputPath}");
                Console.WriteLine($"  File size: {(File.Exists(result.OutputPath) ? new FileInfo(result.OutputPath).Length : 0)} bytes");
                
                if (File.Exists(result.OutputPath))
                {
                    var lines = File.ReadAllLines(result.OutputPath);
                    var subtitleCount = 0;
                    foreach (var line in lines)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(line.Trim(), @"^\d+$"))
                            subtitleCount++;
                    }
                    Console.WriteLine($"  Subtitle entries: {subtitleCount}");
                    
                    // Show first few entries
                    Console.WriteLine("\n  First few subtitle entries:");
                    for (int i = 0; i < Math.Min(15, lines.Length); i++)
                    {
                        Console.WriteLine($"    {lines[i]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Enhanced method failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("Test completed!");
        }
    }
}
