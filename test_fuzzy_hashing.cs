using System;
using System.Threading.Tasks;
using EpisodeIdentifier.Core.Services;

class Program
{
    static async Task Main(string[] args)
    {
        var fuzzyService = new FuzzyHashService("/mnt/c/Users/Ragma/KnowShow_Specd/test_constraint.db", null);
        
        // Test with similar content that should match much better than 0%
        string text1 = "HODGINS: We're not dealing with amateur psychopath";
        string text2 = "We're not dealing with amateur psychopath";
        
        var hash1 = await fuzzyService.GenerateFuzzyHashAsync(text1);
        var hash2 = await fuzzyService.GenerateFuzzyHashAsync(text2);
        
        var similarity = fuzzyService.CompareFuzzyHashes(hash1, hash2);
        
        Console.WriteLine($"Text 1: {text1}");
        Console.WriteLine($"Text 2: {text2}");
        Console.WriteLine($"Hash 1: {hash1}");
        Console.WriteLine($"Hash 2: {hash2}");
        Console.WriteLine($"Similarity: {similarity:P2}");
        
        // Test with completely different content
        string text3 = "This is completely different content about something else entirely";
        var hash3 = await fuzzyService.GenerateFuzzyHashAsync(text3);
        var similarity2 = fuzzyService.CompareFuzzyHashes(hash1, hash3);
        
        Console.WriteLine($"\nComparing with different text:");
        Console.WriteLine($"Text 3: {text3}");
        Console.WriteLine($"Similarity to text 1: {similarity2:P2}");
    }
}