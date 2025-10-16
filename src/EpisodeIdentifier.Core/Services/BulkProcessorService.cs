using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Collections.Concurrent;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for bulk processing of files for episode identification.
/// Provides high-level orchestration of file discovery, processing, and reporting.
/// </summary>
public class BulkProcessorService : IBulkProcessor
{
    private readonly ILogger<BulkProcessorService> _logger;
    private readonly IFileDiscoveryService _fileDiscoveryService;
    private readonly IProgressTracker _progressTracker;
    private readonly IVideoFileProcessingService _videoFileProcessingService;
    private readonly IFileSystem _fileSystem;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();

    // Centralized set of supported video file extensions for validation and discovery logic
    private static readonly HashSet<string> SupportedVideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".webm", ".flv", ".mpg", ".mpeg", ".m2v", ".ts"
    };

    /// <summary>
    /// Initializes a new instance of the BulkProcessorService class.
    /// </summary>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="fileDiscoveryService">The file discovery service.</param>
    /// <param name="progressTracker">The progress tracker.</param>
    /// <param name="videoFileProcessingService">The complete video file processing service.</param>
    public BulkProcessorService(
        ILogger<BulkProcessorService> logger,
        IFileDiscoveryService fileDiscoveryService,
        IProgressTracker progressTracker,
        IVideoFileProcessingService videoFileProcessingService,
        IFileSystem fileSystem)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileDiscoveryService = fileDiscoveryService ?? throw new ArgumentNullException(nameof(fileDiscoveryService));
        _progressTracker = progressTracker ?? throw new ArgumentNullException(nameof(progressTracker));
        _videoFileProcessingService = videoFileProcessingService ?? throw new ArgumentNullException(nameof(videoFileProcessingService));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    // Backward-compatible constructor for existing tests/usages
    public BulkProcessorService(
        ILogger<BulkProcessorService> logger,
        IFileDiscoveryService fileDiscoveryService,
        IProgressTracker progressTracker,
        IVideoFileProcessingService videoFileProcessingService)
        : this(logger, fileDiscoveryService, progressTracker, videoFileProcessingService, new FileSystem())
    {
    }

    /// <inheritdoc />
    public async Task<BulkProcessingResult> ProcessAsync(BulkProcessingRequest request)
    {
        return await ProcessAsync(request, null);
    }

    /// <inheritdoc />
    public async Task<BulkProcessingResult> ProcessAsync(BulkProcessingRequest request, IProgress<BulkProcessingProgress>? progressCallback = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        _logger.LogInformation("Starting bulk processing for request {RequestId} with {PathCount} paths",
            request.RequestId, request.Paths.Count);

        // Create combined cancellation token
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
        _activeCancellationTokens[request.RequestId] = combinedCts;

        var result = new BulkProcessingResult
        {
            RequestId = request.RequestId,
            Status = BulkProcessingStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Initialize progress tracking as early as possible so completion can be marked even if early steps fail
            _progressTracker.Initialize(request.RequestId, request.Paths.Count, request.Options);

            // Phase 1: Validate request
            _logger.LogDebug("Validating request {RequestId}", request.RequestId);
            var validationErrors = await GetValidationErrorsAsync(request);
            if (validationErrors.Any())
            {
                // Allow continuing when only FileNotFound errors are present and ContinueOnError is true
                var onlyMissingFiles = validationErrors.All(e => e.ErrorType == BulkProcessingErrorType.FileNotFound);
                if (!(onlyMissingFiles && request.Options.ContinueOnError))
                {
                    result.Status = BulkProcessingStatus.Failed;
                    foreach (var error in validationErrors)
                    {
                        result.Errors.Add(error);
                    }
                    _logger.LogError("Request validation failed for {RequestId} with {ErrorCount} errors",
                        request.RequestId, validationErrors.Count);
                    return result;
                }

                foreach (var error in validationErrors)
                {
                    result.Errors.Add(error);
                }
                _logger.LogWarning("Proceeding with processing for {RequestId} despite {ErrorCount} non-fatal validation errors (ContinueOnError enabled)",
                    request.RequestId, validationErrors.Count);
            }

            // Phase 2: Optional estimation; swallow errors when ContinueOnError is enabled
            try
            {
                var estimate = await EstimateProcessingAsync(request);
                // We don't update the initialized total here; final totals come from processing results
            }
            catch (Exception ex) when (request.Options.ContinueOnError)
            {
                _logger.LogWarning(ex, "Estimation encountered an error for {RequestId}; continuing due to ContinueOnError", request.RequestId);
            }

            // Subscribe to progress updates if callback provided
            EventHandler<ProgressUpdatedEventArgs>? progressHandler = null;
            if (progressCallback != null)
            {
                progressHandler = (sender, args) =>
                {
                    if (args.RequestId == request.RequestId)
                    {
                        progressCallback.Report(args.Progress);
                    }
                };
                _progressTracker.ProgressUpdated += progressHandler;
            }

            try
            {
                // Phase 3: Process files
                await ProcessFilesAsync(request, result, combinedCts.Token);

                // Phase 4: Finalize
                result.CompletedAt = DateTime.UtcNow;
                result.Status = DetermineCompletionStatus(result);

                _progressTracker.MarkCompleted(request.RequestId, result.Status);

                _logger.LogInformation("Bulk processing completed for request {RequestId}: {Status}, " +
                    "{ProcessedFiles} processed, {FailedFiles} failed, {SkippedFiles} skipped in {Duration}",
                    request.RequestId, result.Status, result.ProcessedFiles, result.FailedFiles,
                    result.SkippedFiles, result.Duration);
            }
            finally
            {
                // Unsubscribe from progress updates
                if (progressHandler != null)
                {
                    _progressTracker.ProgressUpdated -= progressHandler;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bulk processing cancelled for request {RequestId}", request.RequestId);
            result.Status = BulkProcessingStatus.Cancelled;
            result.CompletedAt = DateTime.UtcNow;
            TryMarkCompleted(request.RequestId, result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk processing failed for request {RequestId}", request.RequestId);
            result.Status = BulkProcessingStatus.Failed;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add(BulkProcessingError.FromException(ex, null, BulkProcessingPhase.Processing));
            TryMarkCompleted(request.RequestId, result.Status);
        }
        finally
        {
            // Clean up cancellation token
            _activeCancellationTokens.TryRemove(request.RequestId, out _);
        }

        // Get final progress for the result
        result.FinalProgress = _progressTracker.GetProgress(request.RequestId);

        return result;
    }

    private void TryMarkCompleted(string requestId, BulkProcessingStatus status)
    {
        try
        {
            _progressTracker.MarkCompleted(requestId, status);
        }
        catch (InvalidOperationException)
        {
            // Progress wasn't initialized; ignore to prevent masking the original error
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateRequestAsync(BulkProcessingRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var errors = await GetValidationErrorsAsync(request);
        return !errors.Any();
    }

    /// <inheritdoc />
    public async Task<List<BulkProcessingError>> GetValidationErrorsAsync(BulkProcessingRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var errors = new List<BulkProcessingError>();

        // Phase 1: Basic request validation
        ValidateBasicRequest(request, errors);

        // Phase 2: Path validation
        await ValidatePathsAsync(request, errors);

        // Phase 3: Options validation
        ValidateOptions(request.Options, errors);

        // Phase 4: File extension validation
        ValidateFileExtensions(request, errors);

        return errors;
    }

    /// <summary>
    /// Validates basic request structure and required fields.
    /// </summary>
    private void ValidateBasicRequest(BulkProcessingRequest request, List<BulkProcessingError> errors)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "Request ID cannot be null or empty", null, BulkProcessingPhase.Validating));
        }

        if (!request.Paths.Any())
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "At least one path must be specified", null, BulkProcessingPhase.Validating));
        }

        // Check for duplicate paths
        var duplicatePaths = request.Paths
            .GroupBy(p => _fileSystem.Path.GetFullPath(p).ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatePaths.Any())
        {
            foreach (var duplicatePath in duplicatePaths)
            {
                errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                    $"Duplicate path specified: {duplicatePath}", duplicatePath, BulkProcessingPhase.Validating));
            }
        }
    }

    /// <summary>
    /// Validates that all specified paths exist and are accessible.
    /// </summary>
    private async Task ValidatePathsAsync(BulkProcessingRequest request, List<BulkProcessingError> errors)
    {
        if (!request.Paths.Any()) return; // Already handled in basic validation

        try
        {
            var pathValidation = await _fileDiscoveryService.ValidatePathsAsync(request.Paths, request.CancellationToken);
            if (!pathValidation.IsValid)
            {
                foreach (var pathError in pathValidation.PathErrors)
                {
                    foreach (var error in pathError.Value)
                    {
                        errors.Add(new BulkProcessingError(BulkProcessingErrorType.FileNotFound,
                            error, pathError.Key, BulkProcessingPhase.Validating));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.OperationCancelled,
                "Path validation was cancelled", null, BulkProcessingPhase.Validating));
        }
        catch (Exception ex)
        {
            errors.Add(BulkProcessingError.FromException(ex, null, BulkProcessingPhase.Validating));
        }
    }

    /// <summary>
    /// Validates processing options are within reasonable ranges.
    /// </summary>
    private void ValidateOptions(BulkProcessingOptions options, List<BulkProcessingError> errors)
    {
        // Batch size validation
        if (options.BatchSize <= 0)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "Batch size must be greater than zero", null, BulkProcessingPhase.Validating));
        }
        else if (options.BatchSize > 10000)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "Batch size cannot exceed 10,000 files (memory constraints)", null, BulkProcessingPhase.Validating));
        }

        // Concurrency validation
        if (options.MaxConcurrency <= 0)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "Max concurrency must be greater than zero", null, BulkProcessingPhase.Validating));
        }
        else if (options.MaxConcurrency > Environment.ProcessorCount * 4)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                $"Max concurrency ({options.MaxConcurrency}) should not exceed {Environment.ProcessorCount * 4} " +
                $"(4x processor count) for optimal performance", null, BulkProcessingPhase.Validating));
        }

        // Progress reporting interval validation
        if (options.ProgressReportingInterval < 100)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "Progress reporting interval must be at least 100ms", null, BulkProcessingPhase.Validating));
        }
        else if (options.ProgressReportingInterval > 60000)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "Progress reporting interval should not exceed 60 seconds", null, BulkProcessingPhase.Validating));
        }

        // Error threshold validation
        if (options.MaxErrorsBeforeAbort.HasValue && options.MaxErrorsBeforeAbort.Value < 1)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "Max errors before abort must be at least 1 if specified", null, BulkProcessingPhase.Validating));
        }
        else if (options.MaxErrorsBeforeAbort.HasValue && options.MaxErrorsBeforeAbort.Value > 100000)
        {
            errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                "Max errors before abort should not exceed 100,000", null, BulkProcessingPhase.Validating));
        }

        // Timeout validation
        if (options.FileProcessingTimeout.HasValue)
        {
            if (options.FileProcessingTimeout.Value < TimeSpan.FromSeconds(1))
            {
                errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                    "File processing timeout must be at least 1 second", null, BulkProcessingPhase.Validating));
            }
            else if (options.FileProcessingTimeout.Value > TimeSpan.FromHours(1))
            {
                errors.Add(new BulkProcessingError(BulkProcessingErrorType.InvalidInput,
                    "File processing timeout should not exceed 1 hour", null, BulkProcessingPhase.Validating));
            }
        }
    }

    /// <summary>
    /// Validates that file extensions are supported for processing.
    /// </summary>
    private void ValidateFileExtensions(BulkProcessingRequest request, List<BulkProcessingError> errors)
    {
        var supportedExtensions = SupportedVideoExtensions;

        // Check file extensions in file filter options
        if (request.Options.FileExtensions?.Any() == true)
        {
            foreach (var extension in request.Options.FileExtensions)
            {
                if (!supportedExtensions.Contains(extension))
                {
                    errors.Add(new BulkProcessingError(BulkProcessingErrorType.UnsupportedFileType,
                        $"File extension '{extension}' is not supported for episode identification. " +
                        $"Supported extensions: {string.Join(", ", supportedExtensions.OrderBy(e => e))}",
                        null, BulkProcessingPhase.Validating));
                }
            }
        }

        // Check specific file paths for unsupported extensions
        var specificFiles = request.Paths.Where(p => _fileSystem.File.Exists(p));
        foreach (var filePath in specificFiles)
        {
            var extension = _fileSystem.Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(extension) && !supportedExtensions.Contains(extension))
            {
                errors.Add(new BulkProcessingError(BulkProcessingErrorType.UnsupportedFileType,
                    $"File '{_fileSystem.Path.GetFileName(filePath)}' has unsupported extension '{extension}'. " +
                    $"Supported extensions: {string.Join(", ", supportedExtensions.OrderBy(e => e))}",
                    filePath, BulkProcessingPhase.Validating));
            }
        }
    }

    /// <inheritdoc />
    public async Task<ProcessingEstimate> EstimateProcessingAsync(BulkProcessingRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        _logger.LogDebug("Estimating processing requirements for request {RequestId}", request.RequestId);

        try
        {
            var fileCount = await _fileDiscoveryService.EstimateFileCountAsync(request.Paths, request.Options, request.CancellationToken);

            // Base estimates (these could be made configurable)
            var averageFileProcessingTime = TimeSpan.FromSeconds(2); // Estimated 2 seconds per file
            var averageFileSizeBytes = 50 * 1024 * 1024; // Estimated 50MB per file
            var memoryPerBatch = request.Options.BatchSize * averageFileSizeBytes;

            var estimate = new ProcessingEstimate
            {
                EstimatedFileCount = fileCount,
                EstimatedDuration = TimeSpan.FromTicks(averageFileProcessingTime.Ticks * fileCount),
                EstimatedMemoryUsage = memoryPerBatch,
                EstimatedBackupSpace = request.Options.CreateBackups ? fileCount * averageFileSizeBytes : 0,
                ConfidenceLevel = 60, // Medium confidence for estimates
                Details = new Dictionary<string, object>
                {
                    ["AverageFileProcessingTime"] = averageFileProcessingTime,
                    ["EstimatedAverageFileSize"] = averageFileSizeBytes,
                    ["BatchCount"] = Math.Ceiling((double)fileCount / request.Options.BatchSize),
                    ["ConcurrencyLevel"] = request.Options.MaxConcurrency
                }
            };

            _logger.LogDebug("Processing estimate for request {RequestId}: {FileCount} files, {Duration} duration, {MemoryUsage}MB memory",
                request.RequestId, fileCount, estimate.EstimatedDuration, estimate.EstimatedMemoryUsage / (1024 * 1024));

            return estimate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to estimate processing requirements for request {RequestId}", request.RequestId);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<bool> CancelProcessingAsync(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));

        _logger.LogInformation("Attempting to cancel processing for request {RequestId}", requestId);

        if (_activeCancellationTokens.TryGetValue(requestId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancellation requested for request {RequestId}", requestId);
            return Task.FromResult(true);
        }

        _logger.LogWarning("No active processing found for request {RequestId}", requestId);
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<BulkProcessingProgress?> GetProgressAsync(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));

        return await _progressTracker.GetProgressAsync(requestId);
    }

    /// <summary>
    /// Processes all discovered files in batches.
    /// </summary>
    private async Task ProcessFilesAsync(BulkProcessingRequest request, BulkProcessingResult result, CancellationToken cancellationToken)
    {
        _progressTracker.UpdatePhase(request.RequestId, BulkProcessingPhase.Discovery);

        var discoveredFiles = new List<string>();

        // Pre-handle missing file paths to ensure FileResults count matches requested paths
        var existingPaths = new List<string>();
        var missingFilesCount = 0;
        foreach (var p in request.Paths)
        {
            var existsAsFile = _fileSystem.File.Exists(p);
            var existsAsDirectory = _fileSystem.Directory.Exists(p);

            if (!existsAsFile && !existsAsDirectory)
            {
                // Treat as a file if it appears to be a video file path
                var ext = _fileSystem.Path.GetExtension(p);
                if (!string.IsNullOrEmpty(ext) && SupportedVideoExtensions.Contains(ext))
                {
                    missingFilesCount++;
                    var error = new BulkProcessingError(BulkProcessingErrorType.FileNotFound,
                        $"File not found: {p}", p, BulkProcessingPhase.Validating);
                    var now = DateTime.UtcNow;
                    result.FileResults.Add(new FileProcessingResult
                    {
                        FilePath = p,
                        OriginalFileName = _fileSystem.Path.GetFileName(p),
                        Status = FileProcessingStatus.Failed,
                        ProcessingStarted = now,
                        ProcessingCompleted = now,
                        Error = error
                    });
                    result.IncrementFailedFiles();
                }
                else
                {
                    // Non-existing path that doesn't look like a video file: record a request-level error
                    result.Errors.Add(new BulkProcessingError(BulkProcessingErrorType.FileNotFound,
                        $"Path does not exist: {p}", p, BulkProcessingPhase.Validating));
                }
            }
            else
            {
                existingPaths.Add(p);
            }
        }

        // Collect files from existing paths/directories
        await foreach (var filePath in _fileDiscoveryService.DiscoverFilesAsync(existingPaths, request.Options, cancellationToken))
        {
            discoveredFiles.Add(filePath);
            cancellationToken.ThrowIfCancellationRequested();
        }

        result.TotalFiles = discoveredFiles.Count + missingFilesCount;
        _logger.LogInformation("Discovered {FileCount} files for request {RequestId}", discoveredFiles.Count, request.RequestId);

        // Update the progress tracker with the actual total file count after discovery
        _progressTracker.UpdateTotalFiles(request.RequestId, result.TotalFiles);

        if (discoveredFiles.Count == 0)
        {
            _logger.LogWarning("No files discovered for request {RequestId}", request.RequestId);
            return;
        }

        _progressTracker.UpdatePhase(request.RequestId, BulkProcessingPhase.Processing);

        // Process files in configurable batches with enhanced memory management
        await ProcessFilesInBatchesAsync(request, discoveredFiles, result, cancellationToken);
    }

    /// <summary>
    /// Processes files in configurable batches with enhanced memory management and statistics tracking.
    /// </summary>
    private async Task ProcessFilesInBatchesAsync(BulkProcessingRequest request, List<string> files, BulkProcessingResult result, CancellationToken cancellationToken)
    {
        var totalBatches = (int)Math.Ceiling((double)files.Count / request.Options.BatchSize);
        var currentBatch = 0;
        var batchStatistics = new List<BatchProcessingStats>();

        _logger.LogInformation("Starting batch processing: {TotalFiles} files in {TotalBatches} batches of {BatchSize} files each",
            files.Count, totalBatches, request.Options.BatchSize);

        foreach (var batch in files.Chunk(request.Options.BatchSize))
        {
            currentBatch++;
            var batchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var batchStats = new BatchProcessingStats
            {
                BatchNumber = currentBatch,
                FileCount = batch.Length,
                StartTime = DateTime.UtcNow
            };

            _logger.LogDebug("Processing batch {CurrentBatch}/{TotalBatches} with {FileCount} files for request {RequestId}",
                currentBatch, totalBatches, batch.Length, request.RequestId);

            // Capture memory before batch processing
            var memoryBefore = GC.GetTotalMemory(false);

            try
            {
                await ProcessBatchAsync(request, batch, result, cancellationToken);
                batchStats.Success = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Batch {CurrentBatch} processing failed for request {RequestId}", currentBatch, request.RequestId);
                batchStats.Success = false;
                batchStats.Error = ex.Message;

                // Continue with next batch unless error limit exceeded
                if (_progressTracker.HasExceededErrorLimit(request.RequestId))
                {
                    _logger.LogError("Error limit exceeded during batch {CurrentBatch} for request {RequestId}, stopping processing",
                        currentBatch, request.RequestId);
                    throw new InvalidOperationException($"Maximum error limit exceeded in batch {currentBatch}");
                }
            }

            batchStopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);

            // Complete batch statistics
            batchStats.EndTime = DateTime.UtcNow;
            batchStats.Duration = batchStopwatch.Elapsed;
            batchStats.MemoryUsedBytes = Math.Max(0, memoryAfter - memoryBefore);
            batchStatistics.Add(batchStats);

            _logger.LogDebug("Batch {CurrentBatch} completed in {Duration}ms, memory delta: {MemoryDelta:N0} bytes",
                currentBatch, batchStopwatch.ElapsedMilliseconds, batchStats.MemoryUsedBytes);

            // Memory management between batches
            await ManageBatchMemoryAsync(request, currentBatch, totalBatches, batchStats.MemoryUsedBytes);

            cancellationToken.ThrowIfCancellationRequested();

            // Update progress with batch completion
            _progressTracker.UpdateBatchProgress(request.RequestId, currentBatch, totalBatches);
        }

        // Log final batch statistics
        LogBatchStatistics(request.RequestId, batchStatistics);
    }

    /// <summary>
    /// Manages memory between batch processing with configurable garbage collection.
    /// </summary>
    private async Task ManageBatchMemoryAsync(BulkProcessingRequest request, int currentBatch, int totalBatches, long memoryUsed)
    {
        var shouldForceGC = request.Options.ForceGarbageCollection;
        var memoryThresholdBytes = 500 * 1024 * 1024; // 500MB threshold

        // Force GC if requested or if memory usage is high
        if (shouldForceGC || memoryUsed > memoryThresholdBytes)
        {
            _logger.LogDebug("Performing garbage collection after batch {CurrentBatch} (memory used: {MemoryUsed:N0} bytes)",
                currentBatch, memoryUsed);

            // Use Task.Run to avoid blocking the current thread during GC
            await Task.Run(() =>
            {
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
            });

            // Small delay to allow system to stabilize
            await Task.Delay(50);
        }

        // Additional memory management for large batch operations
        if (currentBatch % 10 == 0 && totalBatches > 20)
        {
            var currentMemory = GC.GetTotalMemory(false);
            var memoryMB = currentMemory / (1024 * 1024);

            _logger.LogDebug("Memory checkpoint at batch {CurrentBatch}: {MemoryUsage:N0} MB",
                currentBatch, memoryMB);

            if (memoryMB > 1000) // > 1GB
            {
                _logger.LogWarning("High memory usage detected ({MemoryUsage:N0} MB) at batch {CurrentBatch}, " +
                    "performing additional garbage collection", memoryMB, currentBatch);

                await Task.Run(() =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                });
            }
        }
    }

    /// <summary>
    /// Logs comprehensive batch processing statistics.
    /// </summary>
    private void LogBatchStatistics(string requestId, List<BatchProcessingStats> statistics)
    {
        if (!statistics.Any()) return;

        var totalDuration = statistics.Sum(s => s.Duration.TotalMilliseconds);
        var totalMemoryUsed = statistics.Sum(s => s.MemoryUsedBytes);
        var successfulBatches = statistics.Count(s => s.Success);
        var averageBatchTime = statistics.Average(s => s.Duration.TotalMilliseconds);
        var totalFiles = statistics.Sum(s => s.FileCount);

        _logger.LogInformation("Batch processing statistics for request {RequestId}: " +
            "{TotalBatches} batches ({SuccessfulBatches} successful), " +
            "{TotalFiles} files processed in {TotalDuration:F1}ms " +
            "(avg {AverageBatchTime:F1}ms per batch), " +
            "total memory used: {TotalMemoryUsed:N0} bytes",
            requestId, statistics.Count, successfulBatches, totalFiles,
            totalDuration, averageBatchTime, totalMemoryUsed);

        if (successfulBatches < statistics.Count)
        {
            var failedBatches = statistics.Where(s => !s.Success).ToList();
            _logger.LogWarning("Failed batches for request {RequestId}: {FailedBatchNumbers}",
                requestId, string.Join(", ", failedBatches.Select(b => $"#{b.BatchNumber}: {b.Error}")));
        }
    }

    /// <summary>
    /// Statistics for a single batch processing operation.
    /// </summary>
    private class BatchProcessingStats
    {
        public int BatchNumber { get; set; }
        public int FileCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long MemoryUsedBytes { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Processes a batch of files concurrently.
    /// </summary>
    private async Task ProcessBatchAsync(BulkProcessingRequest request, string[] batch, BulkProcessingResult result, CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(request.Options.MaxConcurrency, request.Options.MaxConcurrency);
        var tasks = batch.Select(async filePath =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessSingleFileAsync(request.RequestId, filePath, result, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Processes a single file with comprehensive error handling and categorization.
    /// </summary>
    private async Task ProcessSingleFileAsync(string requestId, string filePath, BulkProcessingResult result, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var fileResult = new FileProcessingResult
        {
            FilePath = filePath,
            OriginalFileName = _fileSystem.Path.GetFileName(filePath),
            ProcessingStarted = DateTime.UtcNow,
            Status = FileProcessingStatus.Processing
        };

        var retryAttempt = 0;
        const int maxRetries = 3;

        while (retryAttempt <= maxRetries)
        {
            try
            {
                if (retryAttempt > 0)
                {
                    _logger.LogDebug("Retrying file processing (attempt {RetryAttempt}/{MaxRetries}): {FilePath}",
                        retryAttempt, maxRetries, filePath);

                    // Exponential backoff delay
                    var delayMs = (int)Math.Pow(2, retryAttempt - 1) * 1000; // 1s, 2s, 4s
                    await Task.Delay(delayMs, cancellationToken);
                }

                _logger.LogDebug("Processing file (attempt {Attempt}): {FilePath}", retryAttempt + 1, filePath);
                _progressTracker.UpdatePhase(requestId, BulkProcessingPhase.Processing, filePath);

                // Pre-processing validation
                await ValidateFileForProcessingAsync(filePath, cancellationToken);

                // Main processing logic
                await ProcessFileWithTimeoutAsync(filePath, requestId, fileResult, cancellationToken);

                // Post-processing validation
                await ValidateProcessingResultAsync(filePath, cancellationToken);

                fileResult.Status = FileProcessingStatus.Success;
                fileResult.ProcessingCompleted = DateTime.UtcNow;
                fileResult.RetryCount = retryAttempt;

                result.IncrementProcessedFiles();
                _progressTracker.ReportFileSuccess(requestId, filePath, stopwatch.Elapsed);

                _logger.LogDebug("Successfully processed file: {FilePath} in {Duration}ms (after {RetryCount} retries)",
                    filePath, stopwatch.ElapsedMilliseconds, retryAttempt);

                break; // Success - exit retry loop
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("File processing cancelled: {FilePath}", filePath);
                fileResult.Status = FileProcessingStatus.Cancelled;
                fileResult.ProcessingCompleted = DateTime.UtcNow;
                fileResult.RetryCount = retryAttempt;
                throw; // Re-throw to stop batch processing
            }
            catch (Exception ex)
            {
                var errorCategory = CategorizeError(ex);
                var isRetryable = IsRetryableError(errorCategory);

                _logger.LogError(ex, "File processing failed (attempt {Attempt}/{MaxAttempts}) for {FilePath}: {ErrorCategory}",
                    retryAttempt + 1, maxRetries + 1, filePath, errorCategory);

                if (retryAttempt >= maxRetries || !isRetryable)
                {
                    // Final failure - no more retries
                    var error = CreateCategorizedError(ex, filePath, errorCategory);
                    fileResult.Error = error;
                    fileResult.Status = FileProcessingStatus.Failed;
                    fileResult.ProcessingCompleted = DateTime.UtcNow;
                    fileResult.RetryCount = retryAttempt;

                    result.IncrementFailedFiles();
                    _progressTracker.ReportFileFailure(requestId, filePath, error, stopwatch.Elapsed);

                    // Handle critical system errors
                    if (errorCategory == BulkProcessingErrorType.SystemError)
                    {
                        _logger.LogCritical(ex, "Critical system error processing file {FilePath}, may affect subsequent processing", filePath);

                        // Add system error to result for monitoring
                        result.Errors.Add(error);
                    }

                    break; // Exit retry loop
                }
                else
                {
                    // Log retry attempt
                    _logger.LogWarning(ex, "Retryable error processing file {FilePath} (attempt {Attempt}/{MaxAttempts}): {ErrorMessage}",
                        filePath, retryAttempt + 1, maxRetries + 1, ex.Message);

                    retryAttempt++;
                }
            }
        }

        result.FileResults.Add(fileResult);
    }

    /// <summary>
    /// Validates a file before processing to catch issues early.
    /// </summary>
    private async Task ValidateFileForProcessingAsync(string filePath, CancellationToken cancellationToken)
    {
        // Check file exists
        if (!_fileSystem.File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }

        // Check file is accessible
        try
        {
            using var stream = _fileSystem.File.OpenRead(filePath);
            // Basic readability check
            var buffer = new byte[1024];
            await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access denied to file: {filePath}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"IO error accessing file: {filePath}", ex);
        }

        // Check file size (avoid processing empty files)
        var fileInfo = _fileSystem.FileInfo.New(filePath);
        if (fileInfo.Length == 0)
        {
            throw new InvalidDataException($"File is empty: {filePath}");
        }

        // Check file extension is supported
        var extension = _fileSystem.Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedVideoExtensions.Contains(extension))
        {
            throw new NotSupportedException($"Unsupported file type: {extension} for file {filePath}");
        }
    }

    /// <summary>
    /// Processes a file with configurable timeout support.
    /// </summary>
    private async Task ProcessFileWithTimeoutAsync(string filePath, string requestId, FileProcessingResult fileResult, CancellationToken cancellationToken)
    {
        // Get timeout from progress tracker if available
        var progress = _progressTracker.GetProgress(requestId);
        var timeout = progress?.Details.ContainsKey("FileProcessingTimeout") == true
            ? (TimeSpan?)progress.Details["FileProcessingTimeout"]
            : TimeSpan.FromMinutes(5); // Default 5 minute timeout

        if (timeout.HasValue)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout.Value);

            try
            {
                await ProcessFileWithIdentificationAsync(filePath, fileResult, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"File processing timed out after {timeout.Value.TotalSeconds} seconds for file: {filePath}");
            }
        }
        else
        {
            await ProcessFileWithIdentificationAsync(filePath, fileResult, cancellationToken);
        }
    }

    /// <summary>
    /// Validates the processing result to ensure quality.
    /// </summary>
    private async Task ValidateProcessingResultAsync(string filePath, CancellationToken cancellationToken)
    {
        // This would validate the processing result
        // For now, just a placeholder that occasionally fails for testing
        await Task.Delay(10, cancellationToken);

        // TODO: Implement actual result validation
        // - Check identification confidence scores
        // - Validate generated metadata
        // - Verify file integrity if modified
    }

    /// <summary>
    /// Categorizes an error for appropriate handling.
    /// </summary>
    private static BulkProcessingErrorType CategorizeError(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException => BulkProcessingErrorType.FileNotFound,
            UnauthorizedAccessException => BulkProcessingErrorType.AccessDenied,
            DirectoryNotFoundException => BulkProcessingErrorType.FileNotFound,
            DriveNotFoundException => BulkProcessingErrorType.FileNotFound,
            IOException => BulkProcessingErrorType.FileAccessError,
            NotSupportedException => BulkProcessingErrorType.UnsupportedFileType,
            InvalidDataException => BulkProcessingErrorType.InvalidFileFormat,
            TimeoutException => BulkProcessingErrorType.ProcessingTimeout,
            OutOfMemoryException => BulkProcessingErrorType.SystemError,
            StackOverflowException => BulkProcessingErrorType.SystemError,
            _ => BulkProcessingErrorType.ProcessingError
        };
    }

    /// <summary>
    /// Determines if an error type is retryable.
    /// </summary>
    private static bool IsRetryableError(BulkProcessingErrorType errorType)
    {
        return errorType switch
        {
            BulkProcessingErrorType.FileAccessError => true,
            BulkProcessingErrorType.ProcessingError => true,
            BulkProcessingErrorType.ProcessingTimeout => true,
            BulkProcessingErrorType.SystemError => false,
            BulkProcessingErrorType.FileNotFound => false,
            BulkProcessingErrorType.AccessDenied => false,
            BulkProcessingErrorType.UnsupportedFileType => false,
            BulkProcessingErrorType.InvalidFileFormat => false,
            _ => false
        };
    }

    /// <summary>
    /// Creates a categorized error with enhanced information.
    /// </summary>
    private BulkProcessingError CreateCategorizedError(Exception exception, string filePath, BulkProcessingErrorType errorType)
    {
        var error = BulkProcessingError.FromException(exception, filePath, BulkProcessingPhase.Processing);

        // Enhance error with categorization using Context dictionary
        error.Context["ErrorCategory"] = errorType.ToString();
        error.Context["IsRetryable"] = IsRetryableError(errorType);
        error.Context["Timestamp"] = DateTime.UtcNow;
        error.Context["FileName"] = _fileSystem.Path.GetFileName(filePath);
        error.Context["FileExtension"] = _fileSystem.Path.GetExtension(filePath);

        return error;
    }

    /// <summary>
    /// Processes a file with actual episode identification and optional renaming.
    /// </summary>
    private async Task ProcessFileWithIdentificationAsync(string filePath, FileProcessingResult fileResult, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing file for episode identification: {FilePath}", filePath);

            // Use the complete video processing service to handle the entire workflow
            var processingResult = await _videoFileProcessingService.ProcessVideoFileAsync(
                filePath,
                shouldRename: true, // TODO: Make this configurable based on request options
                language: null, // TODO: Make this configurable based on request options
                cancellationToken: cancellationToken);

            // Convert the processing result to the expected format for bulk processing
            if (processingResult.IdentificationResult != null)
            {
                // Store identification result for the response
                fileResult.IdentificationResults.Add(processingResult.IdentificationResult);

                // If there was an identification error, it doesn't mean the file processing failed
                // The file was successfully processed, but identification might have failed
            }

            if (processingResult.HasError && processingResult.Error != null)
            {
                // In bulk mode, some identification errors are considered non-fatal for processing
                var code = processingResult.Error.Code;
                var nonFatalCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "UNSUPPORTED_FILE_TYPE", // e.g., not AV1 or validation unavailable in test env
                    "NO_SUBTITLES_FOUND"     // identification couldn't proceed, but processing pipeline succeeded
                };

                if (!nonFatalCodes.Contains(code))
                {
                    // This is a true processing error - fail the file
                    throw new InvalidOperationException($"File processing failed: {processingResult.Error.Message}");
                }

                _logger.LogInformation("Non-fatal identification error for {FilePath}: {Code} - treating as processed in bulk mode", filePath, code);
            }

            _logger.LogDebug("File processing completed successfully: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file processing: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Determines the final completion status based on the results.
    /// </summary>
    private static BulkProcessingStatus DetermineCompletionStatus(BulkProcessingResult result)
    {
        if (result.FailedFiles > 0)
        {
            return result.ProcessedFiles > 0 ? BulkProcessingStatus.CompletedWithWarnings : BulkProcessingStatus.Failed;
        }

        return BulkProcessingStatus.Completed;
    }
}