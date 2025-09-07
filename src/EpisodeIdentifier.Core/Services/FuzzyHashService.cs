using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;

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
                FuzzyHash TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_SubtitleHashes_FuzzyHash ON SubtitleHashes(FuzzyHash);";
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
            INSERT INTO SubtitleHashes (Series, Season, Episode, FuzzyHash)
            VALUES (@series, @season, @episode, @hash);";
        
        command.Parameters.AddWithValue("@series", subtitle.Series);
        command.Parameters.AddWithValue("@season", subtitle.Season);
        command.Parameters.AddWithValue("@episode", subtitle.Episode);
        command.Parameters.AddWithValue("@hash", ComputeFuzzyHash(subtitle.SubtitleText));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatches(string subtitleText, double threshold = 0.8)
    {
        var results = new List<(LabelledSubtitle, double)>();
        var searchHash = ComputeFuzzyHash(subtitleText);

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Series, Season, Episode, FuzzyHash
            FROM SubtitleHashes;";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var storedHash = reader.GetString(3);
            var confidence = ComputeHashSimilarity(searchHash, storedHash);

            if (confidence >= threshold)
            {
                var subtitle = new LabelledSubtitle
                {
                    Series = reader.GetString(0),
                    Season = reader.GetString(1),
                    Episode = reader.GetString(2),
                    FuzzyHash = storedHash
                };

                results.Add((subtitle, confidence));
            }
        }

        return results.OrderByDescending(x => x.Item2).ToList();
    }

    private string ComputeFuzzyHash(string text)
    {
        // This is a simplified implementation using character n-grams
        // A production implementation would use a proper fuzzy hashing algorithm like ssdeep
        const int ngramSize = 3;
        var ngrams = new HashSet<string>();

        for (int i = 0; i <= text.Length - ngramSize; i++)
        {
            var ngram = text.Substring(i, ngramSize).ToLowerInvariant();
            ngrams.Add(ngram);
        }

        return string.Join("|", ngrams.OrderBy(n => n));
    }

    private double ComputeHashSimilarity(string hash1, string hash2)
    {
        var set1 = new HashSet<string>(hash1.Split('|'));
        var set2 = new HashSet<string>(hash2.Split('|'));

        if (set1.Count == 0 || set2.Count == 0) return 0.0;

        // Use Jaccard similarity coefficient
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return (double)intersection / union;
    }
}
