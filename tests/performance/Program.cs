using System;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Performance;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "benchmark")
        {
            // Run BenchmarkDotNet benchmarks
            Console.WriteLine("Running performance benchmarks...");
            BenchmarkRunner.Run<SubtitleProcessingBenchmarks>();
        }
        else
        {
            // Run basic performance tests
            Console.WriteLine("Running performance tests...");
            await RunPerformanceTests();
        }
    }

    private static async Task RunPerformanceTests()
    {
        using var performanceTests = new SubtitleWorkflowPerformanceTests();
        
        Console.WriteLine("=== Subtitle Workflow Performance Tests ===\n");

        try
        {
            Console.WriteLine("1. Testing video processing performance...");
            await performanceTests.ProcessVideo_Performance_CompletesWithinTimeLimit();
            Console.WriteLine("✅ Video processing performance test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Video processing performance test failed: {ex.Message}\n");
        }

        try
        {
            Console.WriteLine("2. Testing subtitle detection performance...");
            await performanceTests.SubtitleDetection_Performance_FastDetection();
            Console.WriteLine("✅ Subtitle detection performance test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Subtitle detection performance test failed: {ex.Message}\n");
        }

        try
        {
            Console.WriteLine("3. Testing text subtitle extraction performance...");
            await performanceTests.TextSubtitleExtraction_Performance_EfficientExtraction();
            Console.WriteLine("✅ Text subtitle extraction performance test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Text subtitle extraction performance test failed: {ex.Message}\n");
        }

        try
        {
            Console.WriteLine("4. Testing multiple processing consistency...");
            await performanceTests.MultipleProcessing_Performance_ConsistentTiming();
            Console.WriteLine("✅ Multiple processing consistency test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Multiple processing consistency test failed: {ex.Message}\n");
        }

        try
        {
            Console.WriteLine("5. Testing memory usage...");
            await performanceTests.MemoryUsage_Performance_NoMemoryLeaks();
            Console.WriteLine("✅ Memory usage test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Memory usage test failed: {ex.Message}\n");
        }

        try
        {
            Console.WriteLine("6. Testing concurrent processing...");
            await performanceTests.ConcurrentProcessing_Performance_HandlesMultipleRequests();
            Console.WriteLine("✅ Concurrent processing test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Concurrent processing test failed: {ex.Message}\n");
        }

        Console.WriteLine("=== Performance Tests Complete ===");
    }
}
