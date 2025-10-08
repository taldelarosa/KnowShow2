using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for reporting bulk processing progress to the console.
/// Provides user-friendly progress updates for command-line interfaces.
/// </summary>
public class ConsoleProgressReporter : IConsoleProgressReporter, IDisposable
{
    private readonly ILogger<ConsoleProgressReporter> _logger;
    private readonly object _consoleLock = new();
    private DateTime _lastUpdate = DateTime.MinValue;
    private readonly TimeSpan _minUpdateInterval = TimeSpan.FromMilliseconds(500);
    private int _lastProgressBarLength = 0;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the ConsoleProgressReporter class.
    /// </summary>
    /// <param name="logger">The logger for this service.</param>
    public ConsoleProgressReporter(ILogger<ConsoleProgressReporter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Store original console settings
        try
        {
            Console.CursorVisible = false;
        }
        catch (IOException)
        {
            // Console may not support cursor visibility (e.g., when redirected)
            // This is not critical, so we continue
        }
    }

    // IConsoleProgressReporter interface implementation

    /// <summary>
    /// Reports progress updates for the specified request.
    /// </summary>
    /// <param name="requestId">The unique request identifier.</param>
    /// <param name="progress">The current progress information.</param>
    public void ReportProgress(string requestId, BulkProcessingProgress progress)
    {
        if (string.IsNullOrEmpty(requestId) || progress == null) return;
        if (_disposed) return;

        // Use the existing ReportProgress method that takes BulkProcessingProgress
        ReportProgress(progress);
    }

    /// <summary>
    /// Reports the completion of a bulk processing request.
    /// </summary>
    /// <param name="requestId">The unique request identifier.</param>
    /// <param name="result">The final processing result.</param>
    public void ReportCompletion(string requestId, BulkProcessingResult result)
    {
        if (string.IsNullOrEmpty(requestId) || result == null) return;
        ReportCompletion(result);
    }

    /// <summary>
    /// Reports an error during bulk processing.
    /// </summary>
    /// <param name="requestId">The unique request identifier.</param>
    /// <param name="error">The error that occurred.</param>
    public void ReportError(string requestId, string error)
    {
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(error)) return;

        var bulkError = new BulkProcessingError(BulkProcessingErrorType.ProcessingError, error);
        ReportError(requestId, bulkError);
    }

    /// <summary>
    /// Reports a warning during bulk processing.
    /// </summary>
    /// <param name="requestId">The unique request identifier.</param>
    /// <param name="warning">The warning message.</param>
    public void ReportWarning(string requestId, string warning)
    {
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(warning)) return;
        if (_disposed) return;

        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] {warning}");
            Console.ResetColor();
        }

        _logger.LogWarning("Bulk processing warning for request {RequestId}: {Warning}", requestId, warning);
    }

    // Existing implementation methods

    /// <summary>
    /// Reports progress for a bulk processing operation.
    /// </summary>
    /// <param name="progress">The current progress information.</param>
    public void ReportProgress(BulkProcessingProgress progress)
    {
        if (progress == null) return;
        if (_disposed) return;

        var now = DateTime.UtcNow;
        if (now - _lastUpdate < _minUpdateInterval && progress.PercentComplete < 100.0)
        {
            return; // Throttle updates to avoid console spam
        }

        _lastUpdate = now;

        lock (_consoleLock)
        {
            try
            {
                DisplayProgress(progress);
            }
            catch (Exception ex)
            {
                // Don't let console errors stop processing
                _logger.LogWarning(ex, "Error displaying console progress for request {RequestId}", progress.RequestId);
            }
        }
    }

    /// <summary>
    /// Reports the completion of a bulk processing operation.
    /// </summary>
    /// <param name="result">The final processing result.</param>
    public void ReportCompletion(BulkProcessingResult result)
    {
        if (result == null) return;
        if (_disposed) return;

        lock (_consoleLock)
        {
            try
            {
                ClearProgressLine();
                DisplayCompletionSummary(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error displaying completion summary for request {RequestId}", result.RequestId);
            }
        }
    }

    /// <summary>
    /// Reports an error during bulk processing.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="error">The error information.</param>
    public void ReportError(string requestId, BulkProcessingError error)
    {
        if (error == null) return;
        if (_disposed) return;

        lock (_consoleLock)
        {
            try
            {
                var errorColor = GetErrorColor(error.ErrorType);
                var timestamp = DateTime.Now.ToString("HH:mm:ss");

                Console.ForegroundColor = errorColor;
                Console.WriteLine($"[{timestamp}] ERROR: {error.Message}");

                if (!string.IsNullOrEmpty(error.FilePath))
                {
                    Console.WriteLine($"  File: {error.FilePath}");
                }

                if (error.ErrorType != BulkProcessingErrorType.Unknown)
                {
                    Console.WriteLine($"  Type: {error.ErrorType}");
                }

                Console.ResetColor();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error displaying error message for request {RequestId}", requestId);
            }
        }
    }

    /// <summary>
    /// Displays the current progress information.
    /// </summary>
    private void DisplayProgress(BulkProcessingProgress progress)
    {
        if (IsConsoleRedirected())
        {
            DisplaySimpleProgress(progress);
        }
        else
        {
            DisplayInteractiveProgress(progress);
        }
    }

    /// <summary>
    /// Displays simple progress for non-interactive environments (pipes, redirects).
    /// </summary>
    private void DisplaySimpleProgress(BulkProcessingProgress progress)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var percent = Math.Round(progress.PercentComplete, 1);
        var files = $"{progress.ProcessedFiles}/{progress.TotalFiles}";

        Console.WriteLine($"[{timestamp}] Progress: {percent}% ({files} files) - {progress.CurrentPhase}");

        if (!string.IsNullOrEmpty(progress.CurrentFile))
        {
            var fileName = Path.GetFileName(progress.CurrentFile);
            Console.WriteLine($"  Processing: {fileName}");
        }
    }

    /// <summary>
    /// Displays interactive progress with progress bar for console environments.
    /// </summary>
    private void DisplayInteractiveProgress(BulkProcessingProgress progress)
    {
        ClearProgressLine();

        // Build progress line
        var progressText = BuildProgressText(progress);
        var progressBar = BuildProgressBar(progress.PercentComplete);

        // Display progress
        Console.Write(progressText);
        Console.WriteLine(progressBar);

        // Display current file (if any)
        if (!string.IsNullOrEmpty(progress.CurrentFile))
        {
            var fileName = Path.GetFileName(progress.CurrentFile);
            var truncatedFileName = TruncateString(fileName, Console.WindowWidth - 15);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"Processing: {truncatedFileName}");
            Console.ResetColor();
            Console.WriteLine();
        }

        _lastProgressBarLength = progressText.Length + progressBar.Length +
                                (string.IsNullOrEmpty(progress.CurrentFile) ? 0 : Console.WindowWidth);
    }

    /// <summary>
    /// Builds the progress text portion of the display.
    /// </summary>
    private string BuildProgressText(BulkProcessingProgress progress)
    {
        var percent = Math.Round(progress.PercentComplete, 1).ToString("F1");
        var files = $"{progress.ProcessedFiles:N0}/{progress.TotalFiles:N0}";
        var rate = progress.ProcessingRate > 0 ? $"{progress.ProcessingRate * 60:F1} files/min" : "calculating...";

        var timeRemaining = progress.EstimatedTimeRemaining.HasValue
            ? FormatTimeSpan(progress.EstimatedTimeRemaining.Value)
            : "unknown";

        return $"{percent}% [{files}] {rate} ETA: {timeRemaining} ";
    }

    /// <summary>
    /// Builds a progress bar visualization.
    /// </summary>
    private string BuildProgressBar(double percentComplete)
    {
        var progressBarWidth = Math.Min(30, Console.WindowWidth - 50);
        if (progressBarWidth <= 0) return "";

        var completed = (int)Math.Round(percentComplete / 100.0 * progressBarWidth);
        var remaining = progressBarWidth - completed;

        var progressBar = $"[{new string('█', completed)}{new string('░', remaining)}]";

        // Color the progress bar
        Console.ForegroundColor = percentComplete >= 100.0 ? ConsoleColor.Green : ConsoleColor.Cyan;
        var result = progressBar;
        Console.ResetColor();

        return result;
    }

    /// <summary>
    /// Displays the completion summary.
    /// </summary>
    private void DisplayCompletionSummary(BulkProcessingResult result)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════");

        var statusColor = GetStatusColor(result.Status);
        Console.ForegroundColor = statusColor;
        Console.WriteLine($"PROCESSING COMPLETE: {result.Status}");
        Console.ResetColor();

        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine($"Request ID: {result.RequestId}");
        Console.WriteLine($"Duration: {FormatTimeSpan(result.Duration)}");
        Console.WriteLine();

        // File statistics
        Console.WriteLine("File Statistics:");
        Console.WriteLine($"  Total files: {result.TotalFiles:N0}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Processed successfully: {result.ProcessedFiles:N0}");
        Console.ResetColor();

        if (result.FailedFiles > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Failed: {result.FailedFiles:N0}");
            Console.ResetColor();
        }

        if (result.SkippedFiles > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Skipped: {result.SkippedFiles:N0}");
            Console.ResetColor();
        }

        // Performance metrics
        if (result.Duration.TotalSeconds > 0)
        {
            var filesPerMinute = result.ProcessedFiles / result.Duration.TotalMinutes;
            Console.WriteLine($"  Processing rate: {filesPerMinute:F1} files/minute");
        }

        // Show recent errors (last 5)
        if (result.FileResults.Any(f => f.HasError))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Recent Errors:");
            Console.ResetColor();

            var recentErrors = result.FileResults
                .Where(f => f.HasError)
                .OrderByDescending(f => f.ProcessingCompleted)
                .Take(5);

            foreach (var errorFile in recentErrors)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  • {Path.GetFileName(errorFile.FilePath)}: {errorFile.Error?.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Clears the current progress line(s).
    /// </summary>
    private void ClearProgressLine()
    {
        if (IsConsoleRedirected()) return;

        try
        {
            if (_lastProgressBarLength > 0)
            {
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, Console.CursorTop);
            }
        }
        catch (IOException)
        {
            // Console doesn't support cursor positioning, continue without clearing
        }
    }

    /// <summary>
    /// Determines if console output is being redirected.
    /// </summary>
    private static bool IsConsoleRedirected()
    {
        try
        {
            return Console.IsOutputRedirected || Console.WindowWidth <= 0;
        }
        catch
        {
            return true; // Assume redirected if we can't determine
        }
    }

    /// <summary>
    /// Gets the appropriate color for an error type.
    /// </summary>
    private static ConsoleColor GetErrorColor(BulkProcessingErrorType errorType)
    {
        return errorType switch
        {
            BulkProcessingErrorType.FileNotFound => ConsoleColor.Yellow,
            BulkProcessingErrorType.AccessDenied => ConsoleColor.Red,
            BulkProcessingErrorType.SystemError => ConsoleColor.Magenta,
            BulkProcessingErrorType.ProcessingError => ConsoleColor.Red,
            BulkProcessingErrorType.Cancelled => ConsoleColor.Blue,
            _ => ConsoleColor.Red
        };
    }

    /// <summary>
    /// Gets the appropriate color for a processing status.
    /// </summary>
    private static ConsoleColor GetStatusColor(BulkProcessingStatus status)
    {
        return status switch
        {
            BulkProcessingStatus.Completed => ConsoleColor.Green,
            BulkProcessingStatus.CompletedWithWarnings => ConsoleColor.Yellow,
            BulkProcessingStatus.Failed => ConsoleColor.Red,
            BulkProcessingStatus.Cancelled => ConsoleColor.Blue,
            _ => ConsoleColor.Gray
        };
    }

    /// <summary>
    /// Formats a TimeSpan for display.
    /// </summary>
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.Days}d {timeSpan.Hours}h";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        return $"{timeSpan.Seconds}s";
    }

    /// <summary>
    /// Truncates a string to fit within the specified width.
    /// </summary>
    private static string TruncateString(string input, int maxWidth)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxWidth) return input;
        return input.Length > maxWidth - 3 ? input.Substring(0, maxWidth - 3) + "..." : input;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                Console.CursorVisible = true;
                Console.ResetColor();
            }
            catch (IOException)
            {
                // Ignore console errors during disposal
            }

            _disposed = true;
        }
    }
}