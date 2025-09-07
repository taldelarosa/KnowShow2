using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using FuzzySharp;

namespace EpisodeIdentifier.Core.Services;

public class FuzzyHashService
{
    private readonly string _dbPath;
    private readonly ILogger<FuzzyHashService> _logger;

    public FuzzyHashService(string dbPath, ILogger<FuzzyHashService> logger)
    {
        _dbPath = dbPath;
        _logger = logger;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS SubtitleHashes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Series TEXT NOT NULL,
                Season TEXT NOT NULL,
                Episode TEXT NOT NULL,
                SubtitleText TEXT NOT NULL
            );";
        command.ExecuteNonQuery();
    }

    public async Task StoreHash(LabelledSubtitle subtitle)
    {
        if (!subtitle.IsValid)
        {
            throw new ArgumentException("Subtitle text is required");
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO SubtitleHashes (Series, Season, Episode, SubtitleText)
            VALUES (@series, @season, @episode, @text);";
        
        command.Parameters.AddWithValue("@series", subtitle.Series);
        command.Parameters.AddWithValue("@season", subtitle.Season);
        command.Parameters.AddWithValue("@episode", subtitle.Episode);
        command.Parameters.AddWithValue("@text", subtitle.SubtitleText);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Stored subtitle: {Series} S{Season}E{Episode}", 
            subtitle.Series, subtitle.Season, subtitle.Episode);
    }

    public async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatches(string subtitleText, double threshold = 0.8)
    {
        var results = new List<(LabelledSubtitle, double)>();
        
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Series, Season, Episode, SubtitleText
            FROM SubtitleHashes;";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var storedText = reader.GetString(3);
            // Convert the FuzzySharp ratio (0-100) to a confidence score (0-1)
            var confidence = Fuzz.TokenSetRatio(subtitleText, storedText) / 100.0;

            if (confidence >= threshold)
            {
                var subtitle = new LabelledSubtitle
                {
                    Series = reader.GetString(0),
                    Season = reader.GetString(1),
                    Episode = reader.GetString(2),
                    SubtitleText = storedText
                };

                results.Add((subtitle, confidence));
                _logger.LogDebug("Match found: {Series} S{Season}E{Episode} with confidence {Confidence:P2}", 
                    subtitle.Series, subtitle.Season, subtitle.Episode, confidence);
            }
        }

        var sortedResults = results.OrderByDescending(x => x.Item2).ToList();
        _logger.LogInformation("Found {Count} matches above threshold {Threshold:P2}", 
            sortedResults.Count, threshold);

        return sortedResults;
    }
}
