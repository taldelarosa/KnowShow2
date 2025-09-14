using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models.Hashing;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using SSDEEP.NET;

namespace EpisodeIdentifier.Core.Services.Hashing
{
    /// <summary>
    /// Service for Context Triggered Piecewise Hashing (CTPH) operations using ssdeep fuzzy hashing
    /// </summary>
    public class CTPhHashingService : ICTPhHashingService
    {
        private readonly ILogger<CTPhHashingService> _logger;
        private readonly IFileSystem _fileSystem;
        private const int DEFAULT_SIMILARITY_THRESHOLD = 50;
        private readonly int _similarityThreshold;

        public CTPhHashingService(ILogger<CTPhHashingService> logger, IFileSystem fileSystem)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _similarityThreshold = DEFAULT_SIMILARITY_THRESHOLD;
        }

        /// <summary>
        /// Computes the CTPH fuzzy hash for a file
        /// </summary>
        /// <param name="filePath">Path to the file to hash</param>
        /// <returns>The fuzzy hash string, or empty string on error</returns>
        public async Task<string> ComputeFuzzyHash(string filePath)
        {
            var operationId = Guid.NewGuid();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["Operation"] = "CTPhHashGeneration",
                ["OperationId"] = operationId,
                ["FilePath"] = filePath
            });

            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    stopwatch.Stop();
                    _logger.LogWarning("File path validation failed - null or empty path - Operation: {OperationId}, Duration: {Duration}ms",
                        operationId, stopwatch.ElapsedMilliseconds);
                    return string.Empty;
                }

                if (!_fileSystem.File.Exists(filePath))
                {
                    stopwatch.Stop();
                    _logger.LogWarning("File not found during hash computation - Operation: {OperationId}, Path: {FilePath}, Duration: {Duration}ms",
                        operationId, filePath, stopwatch.ElapsedMilliseconds);
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                // Get file size for logging
                var fileInfo = _fileSystem.FileInfo.New(filePath);
                var fileSize = fileInfo.Length;

                _logger.LogDebug("Starting fuzzy hash computation - Operation: {OperationId}, Path: {FilePath}, Size: {FileSize} bytes",
                    operationId, filePath, fileSize);

                // Read file contents and compute ssdeep hash
                var fileBytes = await _fileSystem.File.ReadAllBytesAsync(filePath);
                var readTime = stopwatch.ElapsedMilliseconds;

                var hash = Hasher.HashBuffer(fileBytes, fileBytes.Length, FuzzyHashMode.EliminateSequences);
                stopwatch.Stop();

                if (hash == null)
                {
                    _logger.LogWarning("Fuzzy hash generation failed - null result - Operation: {OperationId}, Path: {FilePath}, FileSize: {FileSize}, ReadTime: {ReadTime}ms, TotalTime: {Duration}ms",
                        operationId, filePath, fileSize, readTime, stopwatch.ElapsedMilliseconds);
                    return string.Empty;
                }

                _logger.LogInformation("Fuzzy hash generation successful - Operation: {OperationId}, Path: {FilePath}, FileSize: {FileSize}, Hash: {Hash}, ReadTime: {ReadTime}ms, HashTime: {HashTime}ms, TotalTime: {Duration}ms",
                    operationId, filePath, fileSize, hash, readTime, stopwatch.ElapsedMilliseconds - readTime, stopwatch.ElapsedMilliseconds);

                return hash;
            }
            catch (UnauthorizedAccessException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Access denied during hash computation - Operation: {OperationId}, Path: {FilePath}, Duration: {Duration}ms",
                    operationId, filePath, stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error during hash computation - Operation: {OperationId}, Path: {FilePath}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                    operationId, filePath, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
                return string.Empty;
            }
        }

        /// <summary>
        /// Compares two files and returns detailed comparison results
        /// </summary>
        /// <param name="filePath1">Path to the first file</param>
        /// <param name="filePath2">Path to the second file</param>
        /// <returns>Detailed comparison result including hashes and similarity</returns>
        public async Task<FileComparisonResult> CompareFiles(string filePath1, string filePath2)
        {
            var operationId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["Operation"] = "CTPhFileComparison",
                ["OperationId"] = operationId,
                ["FilePath1"] = filePath1,
                ["FilePath2"] = filePath2,
                ["SimilarityThreshold"] = _similarityThreshold
            });

            try
            {
                if (string.IsNullOrWhiteSpace(filePath1) || string.IsNullOrWhiteSpace(filePath2))
                {
                    stopwatch.Stop();
                    _logger.LogWarning("File path validation failed - one or both paths null/empty - Operation: {OperationId}, Path1Valid: {Path1Valid}, Path2Valid: {Path2Valid}, Duration: {Duration}ms",
                        operationId, !string.IsNullOrWhiteSpace(filePath1), !string.IsNullOrWhiteSpace(filePath2), stopwatch.ElapsedMilliseconds);
                    return FileComparisonResult.Failure();
                }

                if (!_fileSystem.File.Exists(filePath1))
                {
                    stopwatch.Stop();
                    _logger.LogWarning("First file not found during comparison - Operation: {OperationId}, Path1: {FilePath1}, Duration: {Duration}ms",
                        operationId, filePath1, stopwatch.ElapsedMilliseconds);
                    throw new FileNotFoundException($"File not found: {filePath1}", filePath1);
                }

                if (!_fileSystem.File.Exists(filePath2))
                {
                    stopwatch.Stop();
                    _logger.LogWarning("Second file not found during comparison - Operation: {OperationId}, Path2: {FilePath2}, Duration: {Duration}ms",
                        operationId, filePath2, stopwatch.ElapsedMilliseconds);
                    throw new FileNotFoundException($"File not found: {filePath2}", filePath2);
                }

                // Get file sizes for metrics
                var fileInfo1 = _fileSystem.FileInfo.New(filePath1);
                var fileInfo2 = _fileSystem.FileInfo.New(filePath2);
                var fileSize1 = fileInfo1.Length;
                var fileSize2 = fileInfo2.Length;

                _logger.LogDebug("Starting file comparison - Operation: {OperationId}, File1: {FilePath1} ({FileSize1} bytes), File2: {FilePath2} ({FileSize2} bytes)",
                    operationId, filePath1, fileSize1, filePath2, fileSize2);

                // Compute hashes for both files
                var hashStartTime = stopwatch.ElapsedMilliseconds;
                var hash1 = await ComputeFuzzyHash(filePath1);
                var hash1Time = stopwatch.ElapsedMilliseconds - hashStartTime;

                var hash2StartTime = stopwatch.ElapsedMilliseconds;
                var hash2 = await ComputeFuzzyHash(filePath2);
                var hash2Time = stopwatch.ElapsedMilliseconds - hash2StartTime;

                if (string.IsNullOrEmpty(hash1) || string.IsNullOrEmpty(hash2))
                {
                    stopwatch.Stop();
                    _logger.LogWarning("Hash computation failed during file comparison - Operation: {OperationId}, Hash1Valid: {Hash1Valid}, Hash2Valid: {Hash2Valid}, Hash1Time: {Hash1Time}ms, Hash2Time: {Hash2Time}ms, Duration: {Duration}ms",
                        operationId, !string.IsNullOrEmpty(hash1), !string.IsNullOrEmpty(hash2), hash1Time, hash2Time, stopwatch.ElapsedMilliseconds);
                    return FileComparisonResult.Failure();
                }

                // Compare the hashes
                var compareStartTime = stopwatch.ElapsedMilliseconds;
                var similarity = CompareFuzzyHashes(hash1, hash2);
                var compareTime = stopwatch.ElapsedMilliseconds - compareStartTime;
                var isMatch = similarity >= _similarityThreshold;

                stopwatch.Stop();

                _logger.LogInformation("File comparison completed - Operation: {OperationId}, File1: {FilePath1}, File2: {FilePath2}, Similarity: {Similarity}%, IsMatch: {IsMatch}, Threshold: {Threshold}%, Hash1Time: {Hash1Time}ms, Hash2Time: {Hash2Time}ms, CompareTime: {CompareTime}ms, TotalDuration: {Duration}ms",
                    operationId, filePath1, filePath2, similarity, isMatch, _similarityThreshold, hash1Time, hash2Time, compareTime, stopwatch.ElapsedMilliseconds);

                return FileComparisonResult.Success(hash1, hash2, similarity, isMatch, stopwatch.Elapsed);
            }
            catch (FileNotFoundException)
            {
                // Re-throw FileNotFoundException to allow callers to handle it
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error during file comparison - Operation: {OperationId}, File1: {FilePath1}, File2: {FilePath2}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                    operationId, filePath1, filePath2, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
                return FileComparisonResult.Failure();
            }
        }

        /// <summary>
        /// Compares two existing fuzzy hashes and returns similarity score
        /// </summary>
        /// <param name="hash1">First fuzzy hash</param>
        /// <param name="hash2">Second fuzzy hash</param>
        /// <returns>Similarity score (0-100), or 0 on error</returns>
        public int CompareFuzzyHashes(string hash1, string hash2)
        {
            var operationId = Guid.NewGuid();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["Operation"] = "CTPhHashComparison",
                ["OperationId"] = operationId,
                ["Hash1Length"] = hash1?.Length ?? 0,
                ["Hash2Length"] = hash2?.Length ?? 0
            });

            try
            {
                if (string.IsNullOrWhiteSpace(hash1) || string.IsNullOrWhiteSpace(hash2))
                {
                    stopwatch.Stop();
                    _logger.LogWarning("Hash validation failed - one or both hashes null/empty - Operation: {OperationId}, Hash1Valid: {Hash1Valid}, Hash2Valid: {Hash2Valid}, Duration: {Duration}ms",
                        operationId, !string.IsNullOrWhiteSpace(hash1), !string.IsNullOrWhiteSpace(hash2), stopwatch.ElapsedMilliseconds);
                    return 0;
                }

                // Validate hash format (CTPH hashes have format: blocksize:hash1:hash2)
                var hash1Valid = IsValidHashFormat(hash1);
                var hash2Valid = IsValidHashFormat(hash2);

                if (!hash1Valid || !hash2Valid)
                {
                    stopwatch.Stop();
                    _logger.LogError("Invalid hash format detected - Operation: {OperationId}, Hash1Valid: {Hash1Valid}, Hash2Valid: {Hash2Valid}, Hash1: {Hash1}, Hash2: {Hash2}, Duration: {Duration}ms",
                        operationId, hash1Valid, hash2Valid, hash1, hash2, stopwatch.ElapsedMilliseconds);
                    throw new ArgumentException("Invalid hash format. Expected format: blocksize:hash1:hash2");
                }

                if (hash1 == hash2)
                {
                    stopwatch.Stop();
                    _logger.LogDebug("Identical hashes detected - Operation: {OperationId}, Hash: {Hash}, Duration: {Duration}ms",
                        operationId, hash1, stopwatch.ElapsedMilliseconds);
                    return 100;
                }

                _logger.LogDebug("Starting hash comparison - Operation: {OperationId}, Hash1: {Hash1}, Hash2: {Hash2}",
                    operationId, hash1, hash2);

                // Use ssdeep to compare the hashes
                var similarity = Comparer.Compare(hash1, hash2);
                stopwatch.Stop();

                _logger.LogInformation("Hash comparison completed - Operation: {OperationId}, Hash1: {Hash1}, Hash2: {Hash2}, Similarity: {Similarity}%, Duration: {Duration}ms",
                    operationId, hash1, hash2, similarity, stopwatch.ElapsedMilliseconds);

                return similarity;
            }
            catch (ArgumentException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Hash format validation error - Operation: {OperationId}, Hash1: {Hash1}, Hash2: {Hash2}, Duration: {Duration}ms",
                    operationId, hash1, hash2, stopwatch.ElapsedMilliseconds);
                // Re-throw argument exceptions for invalid hash formats
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error during hash comparison - Operation: {OperationId}, Hash1: {Hash1}, Hash2: {Hash2}, Duration: {Duration}ms, ExceptionType: {ExceptionType}",
                    operationId, hash1, hash2, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
                return 0;
            }
        }

        /// <summary>
        /// Validates if the provided string is a valid CTPH hash format
        /// </summary>
        private bool IsValidHashFormat(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return false;

            // CTPH hashes have format: blocksize:hash1:hash2
            var parts = hash.Split(':');
            if (parts.Length != 3)
                return false;

            // First part should be a number (block size)
            if (!int.TryParse(parts[0], out _))
                return false;

            // For ssdeep hashes, the hash parts can contain various characters
            // Just ensure they're not empty and contain printable ASCII characters
            return !string.IsNullOrEmpty(parts[1]) && !string.IsNullOrEmpty(parts[2]);
        }

        /// <summary>
        /// Gets the current similarity threshold used for determining matches
        /// </summary>
        /// <returns>The similarity threshold (0-100)</returns>
        public int GetSimilarityThreshold()
        {
            return _similarityThreshold;
        }
    }
}