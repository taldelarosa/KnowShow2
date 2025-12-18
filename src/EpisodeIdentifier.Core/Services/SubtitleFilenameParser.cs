using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;

namespace EpisodeIdentifier.Core.Services;

public class SubtitleFilenameParser
{
    private readonly ILogger<SubtitleFilenameParser> _logger;
    private readonly IAppConfigService _configService;

    public SubtitleFilenameParser(ILogger<SubtitleFilenameParser> logger, IAppConfigService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// Parses TV show information from subtitle filename
    /// Supports various formats with configurable patterns:
    /// - "SeriesName S01E01 EpisodeName" 
    /// - "SeriesName.S01E01.WEB.x264-GROUP"
    /// - "SeriesName 1x01 EpisodeName"
    /// - "SeriesName.S01.E01.EpisodeName"
    /// And many more - fully configurable via patterns in config
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

        var patterns = _configService.Config.FilenamePatterns;
        
        // For backward compatibility, try legacy properties first if Patterns list is empty
        var patternList = patterns.Patterns?.Any() == true 
            ? patterns.Patterns 
            : GetLegacyPatterns(patterns);

        // Try each pattern in order until we find a match
        for (int i = 0; i < patternList.Count; i++)
        {
            var pattern = patternList[i];
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            try
            {
                var match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    // Extract using named capture groups
                    var seriesName = match.Groups["SeriesName"]?.Value;
                    var season = match.Groups["Season"]?.Value;
                    var episode = match.Groups["Episode"]?.Value;
                    var episodeName = match.Groups["EpisodeName"]?.Value;

                    // Validate we got the required fields
                    if (!string.IsNullOrEmpty(seriesName) && 
                        !string.IsNullOrEmpty(season) && 
                        !string.IsNullOrEmpty(episode))
                    {
                        _logger.LogDebug("Matched filename '{FileName}' using pattern #{PatternIndex}", fileName, i + 1);
                        
                        return new SubtitleFileInfo
                        {
                            FilePath = filePath,
                            Series = CleanSeriesName(seriesName),
                            Season = season.TrimStart('0') != "" ? season.TrimStart('0') : "0", // Remove leading zeros
                            Episode = episode.TrimStart('0') != "" ? episode.TrimStart('0') : "0",
                            EpisodeName = CleanEpisodeName(episodeName)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error applying pattern #{PatternIndex}: {Pattern}", i + 1, pattern);
            }
        }

        _logger.LogWarning("Could not parse filename: {FileName}. Tried {PatternCount} pattern(s)", 
            fileName, patternList.Count);
        return null;
    }

    /// <summary>
    /// Gets patterns from legacy properties for backward compatibility
    /// </summary>
    private List<string> GetLegacyPatterns(FilenamePatterns patterns)
    {
        var legacyPatterns = new List<string>();
        
        #pragma warning disable CS0618 // Type or member is obsolete
        if (!string.IsNullOrWhiteSpace(patterns.PrimaryPattern))
            legacyPatterns.Add(patterns.PrimaryPattern);
        if (!string.IsNullOrWhiteSpace(patterns.SecondaryPattern))
            legacyPatterns.Add(patterns.SecondaryPattern);
        if (!string.IsNullOrWhiteSpace(patterns.TertiaryPattern))
            legacyPatterns.Add(patterns.TertiaryPattern);
        #pragma warning restore CS0618
        
        return legacyPatterns;
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

        _logger.LogInformation("Scanning directory for subtitle files: {DirectoryPath}", directoryPath);
        Console.Error.WriteLine($"Scanning directory: {directoryPath}");
        Console.Error.Flush();

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var subtitleExtensions = new[] { "*.srt", "*.vtt", "*.ass", "*.ssa", "*.sub", "*.sbv" };

        // Quick scan to get total file count for progress feedback
        Console.Error.Write("Counting files... ");
        Console.Error.Flush();
        var allFiles = new List<string>();
        foreach (var extension in subtitleExtensions)
        {
            var files = Directory.GetFiles(directoryPath, extension, searchOption);
            allFiles.AddRange(files);
        }
        Console.Error.WriteLine($"found {allFiles.Count} subtitle files");
        Console.Error.WriteLine();
        Console.Error.Flush();

        Console.Error.WriteLine($"Parsing {allFiles.Count} filenames...");
        Console.Error.Flush();

        var results = new List<SubtitleFileInfo>();
        var parseFailures = 0;
        foreach (var file in allFiles)
        {
            var parsed = ParseFilename(file);
            if (parsed != null)
            {
                results.Add(parsed);
            }
            else
            {
                parseFailures++;
            }
        }

        _logger.LogInformation("Scanned {TotalFiles} subtitle files, parsed {ParsedFiles} successfully",
            allFiles.Count, results.Count);

        Console.Error.WriteLine($"✓ Parsing complete: {results.Count} parseable files found");
        if (parseFailures > 0)
        {
            Console.Error.WriteLine($"  Note: {parseFailures} files could not be parsed (see log for details)");
        }
        Console.Error.WriteLine();
        Console.Error.Flush();

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
