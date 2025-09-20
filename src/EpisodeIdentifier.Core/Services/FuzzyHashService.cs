using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using EpisodeIdentifier.Core.Models;
using FuzzySharp;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;

namespace EpisodeIdentifier.Core.Services;

public class FuzzyHashService : IDisposable
{
    private readonly string _dbPath;
    private readonly ILogger<FuzzyHashService> _logger;
    private readonly SubtitleNormalizationService _normalizationService;
    private readonly SqliteConnection? _sharedConnection; // For in-memory databases
    private readonly string _connectionString = string.Empty; // Cached for file-based dbs
    private readonly ConcurrentBag<SqliteConnection> _readConnections = new();
    private readonly SemaphoreSlim? _readConnSemaphore; // gate pooled read connections
    private readonly int _maxReadConnections = 0;

    public FuzzyHashService(string dbPath, ILogger<FuzzyHashService> logger, SubtitleNormalizationService normalizationService)
    {
        _dbPath = dbPath;
        _logger = logger;
        _normalizationService = normalizationService;

        // For in-memory databases, keep a shared connection alive
        if (dbPath == ":memory:")
        {
            _sharedConnection = new SqliteConnection($"Data Source={_dbPath}");
            _sharedConnection.Open();
            InitializeDatabaseWithConnection(_sharedConnection);
        }
        else
        {
            // Optimize database for concurrent operations
            SqliteConcurrencyOptimizer.OptimizeForConcurrency(_dbPath, _logger);
            _connectionString = SqliteConcurrencyOptimizer.GetOptimizedConnectionString(_dbPath);

            // Initialize a small pool of read-only connections to reduce open/close overhead under concurrency
            _maxReadConnections = Math.Min(8, Math.Max(2, Environment.ProcessorCount));
            _readConnSemaphore = new SemaphoreSlim(_maxReadConnections, _maxReadConnections);

            InitializeDatabase();
        }
    }

    private void InitializeDatabase()
    {
        var connectionString = string.IsNullOrEmpty(_connectionString)
            ? SqliteConcurrencyOptimizer.GetOptimizedConnectionString(_dbPath)
            : _connectionString;
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        ApplyConnectionPragmas(connection);
        InitializeDatabaseWithConnection(connection);
    }

    private void InitializeDatabaseWithConnection(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        // New optimized schema using actual fuzzy hashes
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS SubtitleHashes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Series TEXT NOT NULL,
                Season TEXT NOT NULL,
                Episode TEXT NOT NULL,
                OriginalHash TEXT NOT NULL,
                NoTimecodesHash TEXT NOT NULL,
                NoHtmlHash TEXT NOT NULL,
                CleanHash TEXT NOT NULL,
                EpisodeName TEXT NULL,
                -- Keep original columns for migration compatibility
                OriginalText TEXT DEFAULT '',
                NoTimecodesText TEXT DEFAULT '',
                NoHtmlText TEXT DEFAULT '',
                CleanText TEXT DEFAULT '',
                UNIQUE(Series, Season, Episode)
            );";
        command.ExecuteNonQuery();

        // Create indexes for better performance - do this after migrations
        CreateIndexesIfNeeded(connection);

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

        // Migration 1: Add hash columns if they don't exist
        if (!columns.Contains("OriginalHash"))
        {
            _logger.LogInformation("Adding fuzzy hash columns for performance optimization");

            try
            {
                using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = @"
                    ALTER TABLE SubtitleHashes ADD COLUMN OriginalHash TEXT DEFAULT '';
                    ALTER TABLE SubtitleHashes ADD COLUMN NoTimecodesHash TEXT DEFAULT '';
                    ALTER TABLE SubtitleHashes ADD COLUMN NoHtmlHash TEXT DEFAULT '';
                    ALTER TABLE SubtitleHashes ADD COLUMN CleanHash TEXT DEFAULT '';";
                alterCommand.ExecuteNonQuery();

                _logger.LogInformation("Hash columns added successfully");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                _logger.LogDebug("Hash columns already exist, skipping migration");
            }
        }

        // Migration 2: If old schema (has SubtitleText but not the new columns), migrate
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

        // Migration 3: Generate fuzzy hashes for existing records that don't have them
        MigrateLegacyDataToHashes(connection);

        // Migration 4: Add EpisodeName column if it doesn't exist (T023)
        if (!columns.Contains("EpisodeName"))
        {
            _logger.LogInformation("Adding EpisodeName column to support file renaming feature");

            try
            {
                using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE SubtitleHashes ADD COLUMN EpisodeName TEXT NULL;";
                alterCommand.ExecuteNonQuery();

                _logger.LogInformation("EpisodeName column added successfully");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // Column already exists - this can happen with concurrent access in tests
                _logger.LogDebug("EpisodeName column already exists, skipping migration");
            }
        }
    }

    private void MigrateLegacyDataToHashes(SqliteConnection connection)
    {
        // Check if we have records with text but no hashes
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = @"
            SELECT COUNT(*) FROM SubtitleHashes 
            WHERE (OriginalHash IS NULL OR OriginalHash = '') AND OriginalText != '';";

        var recordsToMigrate = (long)checkCommand.ExecuteScalar()!;

        if (recordsToMigrate > 0)
        {
            _logger.LogInformation("Migrating {Count} existing text records to fuzzy hashes", recordsToMigrate);

            // Get all records that need hash generation
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = @"
                SELECT Id, OriginalText, NoTimecodesText, NoHtmlText, CleanText 
                FROM SubtitleHashes 
                WHERE (OriginalHash IS NULL OR OriginalHash = '') AND OriginalText != '';";

            var recordsToUpdate = new List<(long id, string originalHash, string noTimecodesHash, string noHtmlHash, string cleanHash)>();

            using var selectReader = selectCommand.ExecuteReader();
            while (selectReader.Read())
            {
                var id = selectReader.GetInt64(0);
                var originalText = selectReader.GetString(1);
                var noTimecodesText = selectReader.GetString(2);
                var noHtmlText = selectReader.GetString(3);
                var cleanText = selectReader.GetString(4);

                // Generate fuzzy hashes for each version using our custom algorithm
                var originalHash = GenerateFuzzyHash(originalText);
                var noTimecodesHash = GenerateFuzzyHash(noTimecodesText);
                var noHtmlHash = GenerateFuzzyHash(noHtmlText);
                var cleanHash = GenerateFuzzyHash(cleanText);

                recordsToUpdate.Add((id, originalHash, noTimecodesHash, noHtmlHash, cleanHash));
            }
            selectReader.Close();

            // Update records with generated hashes
            foreach (var (id, originalHash, noTimecodesHash, noHtmlHash, cleanHash) in recordsToUpdate)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = @"
                    UPDATE SubtitleHashes 
                    SET OriginalHash = @originalHash,
                        NoTimecodesHash = @noTimecodesHash,
                        NoHtmlHash = @noHtmlHash,
                        CleanHash = @cleanHash
                    WHERE Id = @id;";

                updateCommand.Parameters.AddWithValue("@originalHash", originalHash);
                updateCommand.Parameters.AddWithValue("@noTimecodesHash", noTimecodesHash);
                updateCommand.Parameters.AddWithValue("@noHtmlHash", noHtmlHash);
                updateCommand.Parameters.AddWithValue("@cleanHash", cleanHash);
                updateCommand.Parameters.AddWithValue("@id", id);

                updateCommand.ExecuteNonQuery();
            }

            _logger.LogInformation("Successfully generated fuzzy hashes for {Count} existing records", recordsToUpdate.Count);
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

    private void CreateIndexesIfNeeded(SqliteConnection connection)
    {
        try
        {
            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_series_season ON SubtitleHashes(Series, Season);";
            indexCommand.ExecuteNonQuery();

            // Only create hash indexes if the columns exist
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "PRAGMA table_info(SubtitleHashes);";
            var columns = new List<string>();
            using var reader = checkCommand.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
            reader.Close();

            if (columns.Contains("CleanHash"))
            {
                using var hashIndexCommand = connection.CreateCommand();
                hashIndexCommand.CommandText = @"
                    CREATE INDEX IF NOT EXISTS idx_clean_hash ON SubtitleHashes(CleanHash);
                    CREATE INDEX IF NOT EXISTS idx_original_hash ON SubtitleHashes(OriginalHash);";
                hashIndexCommand.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not create indexes: {Message}", ex.Message);
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

        // Use shared connection for in-memory databases, or optimized connection for file databases
        if (_sharedConnection != null)
        {
            await StoreHashWithConnection(_sharedConnection, subtitle, normalized);
        }
        else
        {
            var connectionString = string.IsNullOrEmpty(_connectionString)
                ? SqliteConcurrencyOptimizer.GetOptimizedConnectionString(_dbPath)
                : _connectionString;
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            ApplyConnectionPragmas(connection);
            await StoreHashWithConnection(connection, subtitle, normalized);
        }
    }

    private async Task StoreHashWithConnection(SqliteConnection connection, LabelledSubtitle subtitle,
        SubtitleNormalizedVersions normalized)
    {
        // Generate fuzzy hashes for all normalized versions
        var originalHash = GenerateFuzzyHash(normalized.Original);
        var noTimecodesHash = GenerateFuzzyHash(normalized.NoTimecodes);
        var noHtmlHash = GenerateFuzzyHash(normalized.NoHtml);
        var cleanHash = GenerateFuzzyHash(normalized.NoHtmlAndTimecodes);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO SubtitleHashes (Series, Season, Episode, OriginalHash, NoTimecodesHash, NoHtmlHash, CleanHash, 
                                      OriginalText, NoTimecodesText, NoHtmlText, CleanText, EpisodeName)
            VALUES (@series, @season, @episode, @originalHash, @noTimecodesHash, @noHtmlHash, @cleanHash,
                    @original, @noTimecodes, @noHtml, @clean, @episodeName);";

        command.Parameters.AddWithValue("@series", subtitle.Series);
        command.Parameters.AddWithValue("@season", subtitle.Season);
        command.Parameters.AddWithValue("@episode", subtitle.Episode);
        command.Parameters.AddWithValue("@originalHash", originalHash);
        command.Parameters.AddWithValue("@noTimecodesHash", noTimecodesHash);
        command.Parameters.AddWithValue("@noHtmlHash", noHtmlHash);
        command.Parameters.AddWithValue("@cleanHash", cleanHash);
        command.Parameters.AddWithValue("@original", normalized.Original);
        command.Parameters.AddWithValue("@noTimecodes", normalized.NoTimecodes);
        command.Parameters.AddWithValue("@noHtml", normalized.NoHtml);
        command.Parameters.AddWithValue("@clean", normalized.NoHtmlAndTimecodes);
        command.Parameters.AddWithValue("@episodeName", subtitle.EpisodeName ?? (object)DBNull.Value);

        try
        {
            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Stored subtitle with fuzzy hashes: {Series} S{Season}E{Episode}",
                subtitle.Series, subtitle.Season, subtitle.Episode);
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

        // Create normalized versions and hashes of the input text
        var inputNormalized = _normalizationService.CreateNormalizedVersions(subtitleText);
        var inputHashes = new
        {
            OriginalHash = GenerateFuzzyHash(inputNormalized.Original),
            NoTimecodesHash = GenerateFuzzyHash(inputNormalized.NoTimecodes),
            NoHtmlHash = GenerateFuzzyHash(inputNormalized.NoHtml),
            CleanHash = GenerateFuzzyHash(inputNormalized.NoHtmlAndTimecodes)
        };

        // Use shared connection for in-memory databases, or optimized connection for file databases
        if (_sharedConnection != null)
        {
            return await FindMatchesWithConnection(_sharedConnection, inputHashes, threshold);
        }
        else
        {
            // Rent from connection pool to minimize open/close overhead under concurrency
            var connection = await RentReadConnectionAsync();
            try
            {
                return await FindMatchesWithConnection(connection, inputHashes, threshold);
            }
            finally
            {
                ReturnReadConnection(connection);
            }
        }
    }

    private async Task<List<(LabelledSubtitle Subtitle, double Confidence)>> FindMatchesWithConnection(
        SqliteConnection connection, 
        dynamic inputHashes, 
        double threshold)
    {
        var results = new List<(LabelledSubtitle, double)>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Series, Season, Episode, OriginalText, OriginalHash, NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName
            FROM SubtitleHashes;";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var subtitle = new LabelledSubtitle
            {
                Series = reader.GetString(0),
                Season = reader.GetString(1),
                Episode = reader.GetString(2),
                SubtitleText = reader.GetString(3), // Use original text for the result
                EpisodeName = reader.IsDBNull(8) ? null : reader.GetString(8)
            };

            // Get stored hashes
            var storedHashes = new
            {
                OriginalHash = reader.IsDBNull(4) ? "" : reader.GetString(4),
                NoTimecodesHash = reader.IsDBNull(5) ? "" : reader.GetString(5),
                NoHtmlHash = reader.IsDBNull(6) ? "" : reader.GetString(6),
                CleanHash = reader.IsDBNull(7) ? "" : reader.GetString(7)
            };

            // Fast hash-based comparison - try the most important combinations first
            var confidence = 0.0;

            // Primary comparison: clean vs clean (most accurate for subtitle matching)
            if (!string.IsNullOrEmpty(storedHashes.CleanHash))
            {
                confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.CleanHash, storedHashes.CleanHash));

                // Early check: if clean comparison is already above threshold, we have a good match
                if (confidence >= threshold)
                {
                    results.Add((subtitle, confidence));
                    _logger.LogDebug("Fast match found: {Series} S{Season}E{Episode} with confidence {Confidence:P2} (clean hash)",
                        subtitle.Series, subtitle.Season, subtitle.Episode, confidence);

                    // Early termination for excellent matches
                    if (confidence >= 0.95)
                    {
                        break;
                    }
                    continue; // Skip additional comparisons for this entry
                }
            }

            // Secondary comparisons: only if clean comparison wasn't conclusive
            if (confidence < threshold * 0.9) // Only try additional comparisons if we're not close
            {
                // Try other hash combinations
                if (!string.IsNullOrEmpty(storedHashes.NoTimecodesHash))
                {
                    confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.NoTimecodesHash, storedHashes.NoTimecodesHash));
                    confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.CleanHash, storedHashes.NoTimecodesHash));
                }

                if (!string.IsNullOrEmpty(storedHashes.NoHtmlHash))
                {
                    confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.NoHtmlHash, storedHashes.NoHtmlHash));
                }

                if (!string.IsNullOrEmpty(storedHashes.OriginalHash))
                {
                    confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.OriginalHash, storedHashes.OriginalHash));
                }
            }

            // Only add to results if above threshold
            if (confidence >= threshold)
            {
                results.Add((subtitle, confidence));
                _logger.LogDebug("Hash match found: {Series} S{Season}E{Episode} with confidence {Confidence:P2}",
                    subtitle.Series, subtitle.Season, subtitle.Episode, confidence);
            }
        }

        var sortedResults = results.OrderByDescending(x => x.Item2).ToList();
        _logger.LogInformation("Found {Count} matches above threshold {Threshold:P2} using fast hash comparison",
            sortedResults.Count, threshold);

        return sortedResults;
    }

    private async Task<SqliteConnection> RentReadConnectionAsync()
    {
        if (_readConnSemaphore == null)
        {
            // Fallback if semaphore not initialized (shouldn't happen for file-based DB)
            var conn = new SqliteConnection(string.IsNullOrEmpty(_connectionString)
                ? SqliteConcurrencyOptimizer.GetOptimizedConnectionString(_dbPath)
                : _connectionString);
            await conn.OpenAsync();
            ApplyConnectionPragmas(conn);
            return conn;
        }

        await _readConnSemaphore.WaitAsync();
        if (_readConnections.TryTake(out var pooled) && pooled.State == System.Data.ConnectionState.Open)
        {
            return pooled;
        }

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        ApplyConnectionPragmas(connection);
        return connection;
    }

    private void ReturnReadConnection(SqliteConnection connection)
    {
        // If connection is broken/closed, dispose it and don't return to pool
        if (connection.State != System.Data.ConnectionState.Open || _readConnSemaphore == null)
        {
            try { connection.Dispose(); } catch { /* ignore */ }
            return;
        }
        _readConnections.Add(connection);
        _readConnSemaphore.Release();
    }

    private void ApplyConnectionPragmas(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA busy_timeout=5000;";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Best-effort; ignore if PRAGMA not applied
        }
    }

    /// <summary>
    /// Gets the best match regardless of confidence threshold, useful for error reporting
    /// </summary>
    public async Task<(LabelledSubtitle Subtitle, double Confidence)?> GetBestMatch(string subtitleText)
    {
        // Create normalized versions of the input text
        var inputNormalized = _normalizationService.CreateNormalizedVersions(subtitleText);

        // Use shared connection for in-memory databases, or create new connection for file databases
        if (_sharedConnection != null)
        {
            return await GetBestMatchWithConnection(_sharedConnection, subtitleText, inputNormalized);
        }
        else
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            return await GetBestMatchWithConnection(connection, subtitleText, inputNormalized);
        }
    }

    private async Task<(LabelledSubtitle Subtitle, double Confidence)?> GetBestMatchWithConnection(
        SqliteConnection connection, string subtitleText,
        SubtitleNormalizedVersions inputNormalized)
    {
        // Generate hashes for input
        var inputHashes = new
        {
            OriginalHash = GenerateFuzzyHash(inputNormalized.Original),
            NoTimecodesHash = GenerateFuzzyHash(inputNormalized.NoTimecodes),
            NoHtmlHash = GenerateFuzzyHash(inputNormalized.NoHtml),
            CleanHash = GenerateFuzzyHash(inputNormalized.NoHtmlAndTimecodes)
        };

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Series, Season, Episode, OriginalText, OriginalHash, NoTimecodesHash, NoHtmlHash, CleanHash, EpisodeName
            FROM SubtitleHashes;";

        double bestConfidence = 0;
        LabelledSubtitle? bestSubtitle = null;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var subtitle = new LabelledSubtitle
            {
                Series = reader.GetString(0),
                Season = reader.GetString(1),
                Episode = reader.GetString(2),
                SubtitleText = reader.GetString(3), // Use original text for the result
                EpisodeName = reader.IsDBNull(8) ? null : reader.GetString(8)
            };

            // Get stored hashes
            var storedHashes = new
            {
                OriginalHash = reader.IsDBNull(4) ? "" : reader.GetString(4),
                NoTimecodesHash = reader.IsDBNull(5) ? "" : reader.GetString(5),
                NoHtmlHash = reader.IsDBNull(6) ? "" : reader.GetString(6),
                CleanHash = reader.IsDBNull(7) ? "" : reader.GetString(7)
            };

            // Try all hash combinations and find the best
            var confidence = 0.0;

            if (!string.IsNullOrEmpty(storedHashes.CleanHash))
                confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.CleanHash, storedHashes.CleanHash));

            if (!string.IsNullOrEmpty(storedHashes.NoTimecodesHash))
            {
                confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.NoTimecodesHash, storedHashes.NoTimecodesHash));
                confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.CleanHash, storedHashes.NoTimecodesHash));
            }

            if (!string.IsNullOrEmpty(storedHashes.NoHtmlHash))
                confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.NoHtmlHash, storedHashes.NoHtmlHash));

            if (!string.IsNullOrEmpty(storedHashes.OriginalHash))
                confidence = Math.Max(confidence, CompareFuzzyHashes(inputHashes.OriginalHash, storedHashes.OriginalHash));

            // Debug output for top matches
            if (confidence > 0.05) // Only log matches above 5%
            {
                _logger.LogInformation("INFO: {Series} S{Season}E{Episode} confidence: {Confidence:P2}",
                    subtitle.Series, subtitle.Season, subtitle.Episode, confidence);
            }

            if (confidence > bestConfidence)
            {
                bestConfidence = confidence;
                bestSubtitle = subtitle;

                // Early termination for near-perfect matches
                if (confidence >= 0.98)
                {
                    _logger.LogDebug("Near-perfect hash match found, skipping remaining entries");
                    break;
                }
            }
        }

        if (bestSubtitle == null)
        {
            _logger.LogInformation("No subtitle entries found in database");
            return null;
        }

        _logger.LogDebug("Best overall hash match: {Series} S{Season}E{Episode} with confidence {Confidence:P2}",
            bestSubtitle.Series, bestSubtitle.Season, bestSubtitle.Episode, bestConfidence);

        return (bestSubtitle, bestConfidence);
    }

    /// <summary>
    /// Generates a custom fuzzy hash that creates a compact representation suitable for fast comparison
    /// This creates a hash similar to ssdeep but optimized for subtitle content
    /// </summary>
    private string GenerateFuzzyHash(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        var normalizedInput = input.ToLowerInvariant();

        // Instead of cryptographic hashes, use word-based and n-gram approaches for fuzzy matching
        var hashes = new List<string>();

        // Approach 1: Word-based hash (most important for subtitle content)
        var words = normalizedInput.Split(new char[] { ' ', '\n', '\r', '\t', '.', ',', ':', ';', '!', '?' },
            StringSplitOptions.RemoveEmptyEntries);
        var wordHash = GenerateWordBasedHash(words);
        hashes.Add($"words:{wordHash}");

        // Approach 2: Character n-gram hash (handles minor variations)
        var trigramHash = GenerateNGramHash(normalizedInput, 3);
        hashes.Add($"trigrams:{trigramHash}");

        // Approach 3: Shingle hash (overlapping chunks for similarity)
        var shingleHash = GenerateShingleHash(words, 5); // 5-word shingles
        hashes.Add($"shingles:{shingleHash}");

        return string.Join(",", hashes);
    }

    private string GenerateWordBasedHash(string[] words)
    {
        // Create a hash based on word frequency and presence
        var wordCounts = new Dictionary<string, int>();

        foreach (var word in words.Take(200)) // Limit to first 200 words for performance
        {
            if (word.Length > 2) // Skip very short words
            {
                wordCounts[word] = wordCounts.GetValueOrDefault(word, 0) + 1;
            }
        }

        // Sort by frequency and create signature
        var topWords = wordCounts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .Take(50) // Top 50 most frequent words
            .Select(kvp => $"{kvp.Key}:{kvp.Value}")
            .ToArray();

        return string.Join("|", topWords);
    }

    private string GenerateNGramHash(string input, int n)
    {
        var ngrams = new HashSet<string>();

        // Create overlapping n-grams
        for (int i = 0; i <= input.Length - n; i++)
        {
            var ngram = input.Substring(i, n);
            if (ngram.Any(char.IsLetter)) // Only include n-grams with letters
            {
                ngrams.Add(ngram);
            }

            if (ngrams.Count >= 100) // Limit for performance
                break;
        }

        return string.Join("|", ngrams.OrderBy(x => x).Take(50));
    }

    private string GenerateShingleHash(string[] words, int shingleSize)
    {
        var shingles = new HashSet<string>();

        // Create overlapping word shingles
        for (int i = 0; i <= words.Length - shingleSize; i++)
        {
            var shingle = string.Join(" ", words.Skip(i).Take(shingleSize));
            shingles.Add(shingle);

            if (shingles.Count >= 50) // Limit for performance
                break;
        }

        return string.Join("|", shingles.OrderBy(x => x));
    }

    /// <summary>
    /// Compares two fuzzy hashes and returns a similarity score (0.0 to 1.0)
    /// </summary>
    private double CompareFuzzyHashes(string hash1, string hash2)
    {
        if (string.IsNullOrWhiteSpace(hash1) || string.IsNullOrWhiteSpace(hash2))
            return 0.0;

        if (hash1 == hash2)
            return 1.0;

        try
        {
            // Parse both hashes
            var parts1 = ParseNewFuzzyHash(hash1);
            var parts2 = ParseNewFuzzyHash(hash2);

            double maxSimilarity = 0.0;

            // Compare words (most important for subtitle matching)
            if (parts1.ContainsKey("words") && parts2.ContainsKey("words"))
            {
                var wordSimilarity = CompareWordHashes(parts1["words"], parts2["words"]);
                maxSimilarity = Math.Max(maxSimilarity, wordSimilarity * 1.0); // Full weight
            }

            // Compare trigrams (handles minor variations)
            if (parts1.ContainsKey("trigrams") && parts2.ContainsKey("trigrams"))
            {
                var trigramSimilarity = CompareSetHashes(parts1["trigrams"], parts2["trigrams"]);
                maxSimilarity = Math.Max(maxSimilarity, trigramSimilarity * 0.8); // Lower weight
            }

            // Compare shingles (handles order and context)
            if (parts1.ContainsKey("shingles") && parts2.ContainsKey("shingles"))
            {
                var shingleSimilarity = CompareSetHashes(parts1["shingles"], parts2["shingles"]);
                maxSimilarity = Math.Max(maxSimilarity, shingleSimilarity * 0.9); // High weight
            }

            return maxSimilarity;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error comparing fuzzy hashes: {Error}", ex.Message);
            return 0.0;
        }
    }

    private Dictionary<string, string> ParseNewFuzzyHash(string hash)
    {
        var result = new Dictionary<string, string>();
        var parts = hash.Split(',');

        foreach (var part in parts)
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex > 0 && colonIndex < part.Length - 1)
            {
                var key = part.Substring(0, colonIndex);
                var value = part.Substring(colonIndex + 1);
                result[key] = value;
            }
        }

        return result;
    }

    private double CompareWordHashes(string wordHash1, string wordHash2)
    {
        if (wordHash1 == wordHash2) return 1.0;

        var words1 = ParseWordHash(wordHash1);
        var words2 = ParseWordHash(wordHash2);

        if (words1.Count == 0 || words2.Count == 0) return 0.0;

        // Calculate weighted Jaccard similarity based on word frequencies
        var commonWords = words1.Keys.Intersect(words2.Keys).ToList();
        var allWords = words1.Keys.Union(words2.Keys).ToList();

        if (allWords.Count == 0) return 0.0;

        // Weight by frequency - common high-frequency words count more
        double intersection = commonWords.Sum(word => Math.Min(words1[word], words2[word]));
        double union = allWords.Sum(word => Math.Max(words1.GetValueOrDefault(word, 0), words2.GetValueOrDefault(word, 0)));

        return union > 0 ? intersection / union : 0.0;
    }

    private Dictionary<string, int> ParseWordHash(string wordHash)
    {
        var result = new Dictionary<string, int>();
        var parts = wordHash.Split('|', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var colonIndex = part.LastIndexOf(':');
            if (colonIndex > 0)
            {
                var word = part.Substring(0, colonIndex);
                if (int.TryParse(part.Substring(colonIndex + 1), out var count))
                {
                    result[word] = count;
                }
            }
        }

        return result;
    }

    private double CompareSetHashes(string setHash1, string setHash2)
    {
        if (setHash1 == setHash2) return 1.0;

        var set1 = new HashSet<string>(setHash1.Split('|', StringSplitOptions.RemoveEmptyEntries));
        var set2 = new HashSet<string>(setHash2.Split('|', StringSplitOptions.RemoveEmptyEntries));

        if (set1.Count == 0 || set2.Count == 0) return 0.0;

        // Jaccard similarity
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }



    public void Dispose()
    {
        _sharedConnection?.Dispose();

        while (_readConnections.TryTake(out var conn))
        {
            try { conn.Dispose(); } catch { /* ignore */ }
        }

        _readConnSemaphore?.Dispose();
    }
}
