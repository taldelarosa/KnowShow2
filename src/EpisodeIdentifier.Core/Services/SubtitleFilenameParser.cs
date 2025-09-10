using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Services;

public class SubtitleFilenameParser
{
    private readonly ILogger<SubtitleFilenameParser> _logger;

    public SubtitleFilenameParser(ILogger<SubtitleFilenameParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses TV show information from subtitle filename
    /// Supports formats like:
    /// - "SeriesName - 1x1 - NameOfEpisode.srt" -> Series: SeriesName, Season: 1, Episode: 1
    /// - "SeriesName - S01E01.srt" -> Series: SeriesName, Season: 1, Episode: 1
    /// - "SeriesName.S01E01.srt" -> Series: SeriesName, Season: 1, Episode: 1
    /// - "SeriesName S1E1.srt" -> Series: SeriesName, Season: 1, Episode: 1
    /// </summary>
    public SubtitleFileInfo? ParseFilename(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Only process subtitle files
        var subtitleExtensions = new[] { ".srt", ".vtt", ".ass", ".ssa", ".sub", ".sbv" };
        if (!subtitleExtensions.Contains(extension))
        {
            _logger.LogDebug("Skipping non-subtitle file: {FilePath}", filePath);
            return null;
        }

        // Pattern 1: "SeriesName - 1x1 - NameOfEpisode" (season x episode format)
        var pattern1 = @"^(.+?)\s*-\s*(\d+)x(\d+)(?:\s*-\s*.+)?$";
        var match1 = Regex.Match(fileName, pattern1, RegexOptions.IgnoreCase);
        if (match1.Success)
        {
            return new SubtitleFileInfo
            {
                FilePath = filePath,
                Series = CleanSeriesName(match1.Groups[1].Value),
                Season = match1.Groups[2].Value.TrimStart('0') ?? "0", // Remove leading zeros
                Episode = match1.Groups[3].Value.TrimStart('0') ?? "0"
            };
        }

        // Pattern 2: "SeriesName - S01E01" or "SeriesName.S01E01" or "SeriesName S01E01"
        var pattern2 = @"^(.+?)[\s\.\-]+S(\d+)E(\d+).*?$";
        var match2 = Regex.Match(fileName, pattern2, RegexOptions.IgnoreCase);
        if (match2.Success)
        {
            return new SubtitleFileInfo
            {
                FilePath = filePath,
                Series = CleanSeriesName(match2.Groups[1].Value),
                Season = match2.Groups[2].Value.TrimStart('0') ?? "0",
                Episode = match2.Groups[3].Value.TrimStart('0') ?? "0"
            };
        }

        // Pattern 3: "SeriesName S1E1" (without leading zeros)
        var pattern3 = @"^(.+?)\s+S(\d+)E(\d+).*?$";
        var match3 = Regex.Match(fileName, pattern3, RegexOptions.IgnoreCase);
        if (match3.Success)
        {
            return new SubtitleFileInfo
            {
                FilePath = filePath,
                Series = CleanSeriesName(match3.Groups[1].Value),
                Season = match3.Groups[2].Value,
                Episode = match3.Groups[3].Value
            };
        }

        _logger.LogWarning("Could not parse filename: {FileName}. Supported formats: 'Series - 1x1 - Title', 'Series - S01E01', 'Series.S01E01', 'Series S1E1'", fileName);
        return null;
    }

    /// <summary>
    /// Cleans up series name by removing common artifacts
    /// </summary>
    private string CleanSeriesName(string seriesName)
    {
        // Remove common quality indicators and release group tags
        var cleaned = seriesName.Trim();

        // Remove patterns like [1080p], (2020), etc.
        cleaned = Regex.Replace(cleaned, @"\[.*?\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\(.*?\)", "", RegexOptions.IgnoreCase);

        // Remove common resolution/quality tags
        var qualityPatterns = new[] { "1080p", "720p", "480p", "4K", "HDR", "x264", "x265", "HEVC", "BluRay", "WEB-DL", "WEBRip" };
        foreach (var pattern in qualityPatterns)
        {
            cleaned = Regex.Replace(cleaned, @"\b" + Regex.Escape(pattern) + @"\b", "", RegexOptions.IgnoreCase);
        }

        // Clean up extra spaces and punctuation
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim(' ', '.', '-', '_');

        return cleaned;
    }

    /// <summary>
    /// Scans a directory for subtitle files and parses their information
    /// </summary>
    public Task<List<SubtitleFileInfo>> ScanDirectory(string directoryPath, bool recursive = true)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var subtitleExtensions = new[] { "*.srt", "*.vtt", "*.ass", "*.ssa", "*.sub", "*.sbv" };

        var allFiles = new List<string>();
        foreach (var extension in subtitleExtensions)
        {
            allFiles.AddRange(Directory.GetFiles(directoryPath, extension, searchOption));
        }

        var results = new List<SubtitleFileInfo>();
        foreach (var file in allFiles)
        {
            var parsed = ParseFilename(file);
            if (parsed != null)
            {
                results.Add(parsed);
            }
        }

        _logger.LogInformation("Scanned {TotalFiles} subtitle files, parsed {ParsedFiles} successfully",
            allFiles.Count, results.Count);

        return Task.FromResult(results);
    }
}

/// <summary>
/// Information parsed from a subtitle filename
/// </summary>
public class SubtitleFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Episode { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Series} S{Season}E{Episode} ({Path.GetFileName(FilePath)})";
    }
}
