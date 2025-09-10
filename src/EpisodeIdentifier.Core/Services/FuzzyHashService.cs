using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using FuzzySharp;

namespace EpisodeIdentifier.Core.Services;

public class FuzzyHashService
{
    private readonly string _dbPath;
    private readonly ILogger<FuzzyHashService> _logger;
    private readonly SubtitleNormalizationService _normalizationService;

    public FuzzyHashService(string dbPath, ILogger<FuzzyHashService> logger, SubtitleNormalizationService normalizationService)
    {
        _dbPath = dbPath;
        _logger = logger;
        _normalizationService = normalizationService;
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
                OriginalText TEXT NOT NULL,
                NoTimecodesText TEXT NOT NULL,
                NoHtmlText TEXT NOT NULL,
                CleanText TEXT NOT NULL,
                UNIQUE(Series, Season, Episode)
            );";
        command.ExecuteNonQuery();

        // Migrate existing data if needed
        MigrateExistingData(connection);

        // Add unique constraint if table already exists without it
        AddUniqueConstraintIfNeeded(connection);
    }

    private void MigrateExistingData(SqliteConnection connection)
    {
        // Check if we have old schema
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info(SubtitleHashes);";

        var columns = new List<string>();
        using var reader = checkCommand.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1)); // Column name is at index 1
        }
        reader.Close();

        // If old schema (has SubtitleText but not the new columns), migrate
        if (columns.Contains("SubtitleText") && !columns.Contains("OriginalText"))
        {
            _logger.LogInformation("Migrating database schema to support normalized versions");

            // Add new columns
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = @"
                ALTER TABLE SubtitleHashes ADD COLUMN OriginalText TEXT DEFAULT '';
                ALTER TABLE SubtitleHashes ADD COLUMN NoTimecodesText TEXT DEFAULT '';
                ALTER TABLE SubtitleHashes ADD COLUMN NoHtmlText TEXT DEFAULT '';
                ALTER TABLE SubtitleHashes ADD COLUMN CleanText TEXT DEFAULT '';";
            alterCommand.ExecuteNonQuery();

            // Migrate existing data
            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = @"
                UPDATE SubtitleHashes 
                SET OriginalText = SubtitleText,
                    NoTimecodesText = SubtitleText,
                    NoHtmlText = SubtitleText,
                    CleanText = SubtitleText
                WHERE OriginalText = '';";
            var updated = updateCommand.ExecuteNonQuery();

            _logger.LogInformation("Migrated {Count} existing records to new schema", updated);
        }
    }

    private void AddUniqueConstraintIfNeeded(SqliteConnection connection)
    {
        try
        {
            // Check if the unique constraint already exists by trying to create it
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_episode 
                ON SubtitleHashes(Series, Season, Episode);";
            command.ExecuteNonQuery();

            _logger.LogDebug("Unique constraint ensured on (Series, Season, Episode)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not add unique constraint: {Message}", ex.Message);
        }
    }

    public async Task StoreHash(LabelledSubtitle subtitle)
    {
        if (!subtitle.IsValid)
        {
            throw new ArgumentException("Subtitle text is required");
        }

        // Create normalized versions
        var normalized = _normalizationService.CreateNormalizedVersions(subtitle.SubtitleText);

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO SubtitleHashes (Series, Season, Episode, OriginalText, NoTimecodesText, NoHtmlText, CleanText)
            VALUES (@series, @season, @episode, @original, @noTimecodes, @noHtml, @clean);";

        command.Parameters.AddWithValue("@series", subtitle.Series);
        command.Parameters.AddWithValue("@season", subtitle.Season);
        command.Parameters.AddWithValue("@episode", subtitle.Episode);
        command.Parameters.AddWithValue("@original", normalized.Original);
        command.Parameters.AddWithValue("@noTimecodes", normalized.NoTimecodes);
        command.Parameters.AddWithValue("@noHtml", normalized.NoHtml);
        command.Parameters.AddWithValue("@clean", normalized.NoHtmlAndTimecodes);

        try
        {
            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Stored subtitle with {VersionCount} normalized versions: {Series} S{Season}E{Episode}",
                4, subtitle.Series, subtitle.Season, subtitle.Episode);
        }
        catch (SqliteException ex) when (ex.Message.Contains("UNIQUE constraint failed"))
        {
            _logger.LogWarning("Episode {Series} S{Season}E{Episode} already exists in database, skipping duplicate entry",
                subtitle.Series, subtitle.Season, subtitle.Episode);
        }
    }

    public async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatches(string subtitleText, double threshold = 0.8)
    {
        var results = new List<(LabelledSubtitle, double)>();

        // Create normalized versions of the input text
        var inputNormalized = _normalizationService.CreateNormalizedVersions(subtitleText);

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Series, Season, Episode, OriginalText, NoTimecodesText, NoHtmlText, CleanText
            FROM SubtitleHashes;";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var subtitle = new LabelledSubtitle
            {
                Series = reader.GetString(0),
                Season = reader.GetString(1),
                Episode = reader.GetString(2),
                SubtitleText = reader.GetString(3) // Use original text for the result
            };

            // Try matching against all normalized versions and take the best score
            var storedOriginal = reader.GetString(3);
            var storedNoTimecodes = reader.GetString(4);
            var storedNoHtml = reader.GetString(5);
            var storedClean = reader.GetString(6);

            // Calculate confidence using ALL possible comparison strategies (4x4 = 16 combinations)
            var inputVersions = new[]
            {
                ("input-original", inputNormalized.Original),
                ("input-no-timecodes", inputNormalized.NoTimecodes),
                ("input-no-html", inputNormalized.NoHtml),
                ("input-clean", inputNormalized.NoHtmlAndTimecodes)
            };

            var storedVersions = new[]
            {
                ("stored-original", storedOriginal),
                ("stored-no-timecodes", storedNoTimecodes),
                ("stored-no-html", storedNoHtml),
                ("stored-clean", storedClean)
            };

            var confidences = new List<(string strategy, double confidence)>();

            // Try all 16 combinations
            foreach (var (inputName, inputText) in inputVersions)
            {
                foreach (var (storedName, storedText) in storedVersions)
                {
                    var comparisonScore = Fuzz.TokenSetRatio(inputText, storedText) / 100.0;
                    var strategyName = $"{inputName} vs {storedName}";
                    confidences.Add((strategyName, comparisonScore));
                }
            }

            // Use the best confidence score from any strategy
            var bestMatch = confidences.OrderByDescending(c => c.confidence).First();
            var confidence = bestMatch.confidence;

            if (confidence >= threshold)
            {
                results.Add((subtitle, confidence));
                _logger.LogDebug("Match found: {Series} S{Season}E{Episode} with confidence {Confidence:P2} (strategy: {Strategy})",
                    subtitle.Series, subtitle.Season, subtitle.Episode, confidence, bestMatch.strategy);
            }
        }

        var sortedResults = results.OrderByDescending(x => x.Item2).ToList();
        _logger.LogInformation("Found {Count} matches above threshold {Threshold:P2}",
            sortedResults.Count, threshold);

        return sortedResults;
    }

    /// <summary>
    /// Gets the best match regardless of confidence threshold, useful for error reporting
    /// </summary>
    public async Task<(LabelledSubtitle Subtitle, double Confidence)?> GetBestMatch(string subtitleText)
    {
        // Create normalized versions of the input text
        var inputNormalized = _normalizationService.CreateNormalizedVersions(subtitleText);

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Series, Season, Episode, OriginalText, NoTimecodesText, NoHtmlText, CleanText
            FROM SubtitleHashes;";

        double bestConfidence = 0;
        LabelledSubtitle? bestSubtitle = null;
        string bestStrategy = "";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var subtitle = new LabelledSubtitle
            {
                Series = reader.GetString(0),
                Season = reader.GetString(1),
                Episode = reader.GetString(2),
                SubtitleText = reader.GetString(3) // Use original text for the result
            };

            // Try matching against all normalized versions and take the best score
            var storedOriginal = reader.GetString(3);
            var storedNoTimecodes = reader.GetString(4);
            var storedNoHtml = reader.GetString(5);
            var storedClean = reader.GetString(6);

            // Calculate confidence using ALL possible comparison strategies (4x4 = 16 combinations)
            var inputVersions = new[]
            {
                ("input-original", inputNormalized.Original),
                ("input-no-timecodes", inputNormalized.NoTimecodes),
                ("input-no-html", inputNormalized.NoHtml),
                ("input-clean", inputNormalized.NoHtmlAndTimecodes)
            };

            var storedVersions = new[]
            {
                ("stored-original", storedOriginal),
                ("stored-no-timecodes", storedNoTimecodes),
                ("stored-no-html", storedNoHtml),
                ("stored-clean", storedClean)
            };

            var confidences = new List<(string strategy, double confidence)>();

            // Try all 16 combinations
            foreach (var (inputName, inputText) in inputVersions)
            {
                foreach (var (storedName, storedText) in storedVersions)
                {
                    var comparisonScore = Fuzz.TokenSetRatio(inputText, storedText) / 100.0;
                    var strategyName = $"{inputName} vs {storedName}";
                    confidences.Add((strategyName, comparisonScore));
                }
            }

            // Find the best confidence score from any strategy
            var bestMatchForThisSubtitle = confidences.OrderByDescending(c => c.confidence).First();

            if (bestMatchForThisSubtitle.confidence > bestConfidence)
            {
                bestConfidence = bestMatchForThisSubtitle.confidence;
                bestSubtitle = subtitle;
                bestStrategy = bestMatchForThisSubtitle.strategy;
            }
        }

        if (bestSubtitle == null)
        {
            _logger.LogInformation("No subtitle entries found in database");
            return null;
        }

        _logger.LogDebug("Best overall match: {Series} S{Season}E{Episode} with confidence {Confidence:P2} (strategy: {Strategy})",
            bestSubtitle.Series, bestSubtitle.Season, bestSubtitle.Episode, bestConfidence, bestStrategy);

        return (bestSubtitle, bestConfidence);
    }
}
