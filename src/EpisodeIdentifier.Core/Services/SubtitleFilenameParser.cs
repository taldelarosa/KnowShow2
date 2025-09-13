using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;

namespace EpisodeIdentifier.Core.Services;

public class SubtitleFilenameParser
{
    private readonly ILogger<SubtitleFilenameParser> _logger;
    private readonly IConfigurationService _configService;

    public SubtitleFilenameParser(ILogger<SubtitleFilenameParser> logger, IConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
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

        // Use configurable patterns
        var patterns = _configService.Config.FilenamePatterns;

        // Pattern 1: Primary pattern (default: SeriesName S##E## EpisodeName)
        var match1 = Regex.Match(fileName, patterns.PrimaryPattern, RegexOptions.IgnoreCase);
        if (match1.Success && match1.Groups.Count >= 4)
        {
            return new SubtitleFileInfo
            {
                FilePath = filePath,
                Series = CleanSeriesName(match1.Groups[1].Value),
                Season = match1.Groups[2].Value.TrimStart('0') ?? "0", // Remove leading zeros
                Episode = match1.Groups[3].Value.TrimStart('0') ?? "0",
                EpisodeName = CleanEpisodeName(match1.Groups.Count > 4 ? match1.Groups[4].Value : null)
            };
        }

        // Pattern 2: Secondary pattern (default: SeriesName ##x## EpisodeName)
        var match2 = Regex.Match(fileName, patterns.SecondaryPattern, RegexOptions.IgnoreCase);
        if (match2.Success && match2.Groups.Count >= 4)
        {
            return new SubtitleFileInfo
            {
                FilePath = filePath,
                Series = CleanSeriesName(match2.Groups[1].Value),
                Season = match2.Groups[2].Value.TrimStart('0') ?? "0",
                Episode = match2.Groups[3].Value.TrimStart('0') ?? "0",
                EpisodeName = CleanEpisodeName(match2.Groups.Count > 4 ? match2.Groups[4].Value : null)
            };
        }

        // Pattern 3: Tertiary pattern (default: SeriesName.S##.E##.EpisodeName)
        var match3 = Regex.Match(fileName, patterns.TertiaryPattern, RegexOptions.IgnoreCase);
        if (match3.Success && match3.Groups.Count >= 4)
        {
            return new SubtitleFileInfo
            {
                FilePath = filePath,
                Series = CleanSeriesName(match3.Groups[1].Value),
                Season = match3.Groups[2].Value,
                Episode = match3.Groups[3].Value,
                EpisodeName = CleanEpisodeName(match3.Groups.Count > 4 ? match3.Groups[4].Value : null)
            };
        }

        _logger.LogWarning("Could not parse filename: {FileName}. Current patterns: Primary='{Primary}', Secondary='{Secondary}', Tertiary='{Tertiary}'", 
            fileName, patterns.PrimaryPattern, patterns.SecondaryPattern, patterns.TertiaryPattern);
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
    /// Cleans up episode name by removing common artifacts and formatting
    /// </summary>
    private string CleanEpisodeName(string? episodeName)
    {
        if (string.IsNullOrWhiteSpace(episodeName))
            return string.Empty;

        var cleaned = episodeName.Trim();

        // Remove common quality indicators and release group tags
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
    public string EpisodeName { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Series} S{Season}E{Episode} - {EpisodeName} ({Path.GetFileName(FilePath)})";
    }
}
