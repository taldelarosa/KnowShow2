using System;
using System.IO;

// Simple test to debug the filename validation issue
class Program
{
    static void Main()
    {
        var filename = DebugFilenameGeneration();
        Console.WriteLine($"Generated filename: '{filename}'");
        Console.WriteLine($"Length: {filename.Length}");

        // Test basic validation rules
        Console.WriteLine($"Not null/empty: {!string.IsNullOrWhiteSpace(filename)}");
        Console.WriteLine($"Has extension: {!string.IsNullOrEmpty(Path.GetExtension(filename))}");
        Console.WriteLine($"Not ending with dot: {!filename.EndsWith('.')}");
        Console.WriteLine($"Not ending with space: {!filename.EndsWith(' ')}");
        
        // Check for invalid chars
        var invalidChars = Path.GetInvalidFileNameChars();
        var hasInvalidChars = filename.IndexOfAny(invalidChars) >= 0;
        Console.WriteLine($"Has invalid chars: {hasInvalidChars}");
        
        if (hasInvalidChars)
        {
            foreach (var c in filename)
            {
                if (Array.IndexOf(invalidChars, c) >= 0)
                {
                    Console.WriteLine($"Invalid char found: '{c}' (code: {(int)c})");
                }
            }
        }
    }
    
    static string DebugFilenameGeneration()
    {
        var seriesName = new string('B', 150);
        var episodeName = new string('C', 150);
        var maxLength = 100;
        
        // Simulate the filename generation logic
        var filename = $"{seriesName} - S01E01 - {episodeName}.mkv";
        Console.WriteLine($"Original filename length: {filename.Length}");
        
        if (filename.Length <= maxLength)
            return filename;

        var extension = Path.GetExtension(filename);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
        
        // Reserve space for extension
        var availableLength = maxLength - extension.Length;
        
        // Try to preserve the season/episode pattern
        var seasonEpisodeIndex = nameWithoutExtension.IndexOf(" - S01E01 - ");
        if (seasonEpisodeIndex >= 0)
        {
            var beforeSeasonEpisode = nameWithoutExtension.Substring(0, seasonEpisodeIndex);
            var afterSeasonEpisodeIndex = seasonEpisodeIndex + " - S01E01 - ".Length;
            var afterSeasonEpisode = nameWithoutExtension.Substring(afterSeasonEpisodeIndex);
            
            // Essential parts: " - " + "S01E01" + " - " = 10 characters minimum
            var essentialLength = 10;
            var remainingLength = availableLength - essentialLength;
            
            if (remainingLength > 0)
            {
                // Split remaining space between series and episode names
                var seriesMaxLength = remainingLength / 2;
                var episodeMaxLength = remainingLength - seriesMaxLength;
                
                var truncatedSeries = beforeSeasonEpisode.Length > seriesMaxLength ? 
                    beforeSeasonEpisode.Substring(0, seriesMaxLength) : beforeSeasonEpisode;
                var truncatedEpisode = afterSeasonEpisode.Length > episodeMaxLength ? 
                    afterSeasonEpisode.Substring(0, episodeMaxLength) : afterSeasonEpisode;
                
                return $"{truncatedSeries} - S01E01 - {truncatedEpisode}{extension}";
            }
        }
        
        // Fallback
        return filename.Substring(0, maxLength);
    }
}
