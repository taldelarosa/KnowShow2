using EpisodeIdentifier.Core.Models;

namespace EpisodeIdentifier.Core.Interfaces;

/// <summary>
/// Interface for discovering files in directories for bulk processing.
/// Provides streaming file enumeration with filtering and error handling.
/// </summary>
public interface IFileDiscoveryService
{
    /// <summary>
    /// Discovers files in the specified paths based on the given options.
    /// </summary>
    /// <param name="paths">The file and directory paths to search.</param>
    /// <param name="options">The options controlling file discovery behavior.</param>
    /// <param name="cancellationToken">Token to cancel the discovery operation.</param>
    /// <returns>An async enumerable of discovered file paths.</returns>
    /// <exception cref="ArgumentNullException">Thrown when paths or options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when paths is empty or contains invalid paths.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a specified directory doesn't exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to a directory is denied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    IAsyncEnumerable<string> DiscoverFilesAsync(IEnumerable<string> paths, BulkProcessingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers files with detailed information including metadata.
    /// </summary>
    /// <param name="paths">The file and directory paths to search.</param>
    /// <param name="options">The options controlling file discovery behavior.</param>
    /// <param name="cancellationToken">Token to cancel the discovery operation.</param>
    /// <returns>An async enumerable of discovered file information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when paths or options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when paths is empty or contains invalid paths.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a specified directory doesn't exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to a directory is denied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    IAsyncEnumerable<FileDiscoveryResult> DiscoverFilesWithInfoAsync(IEnumerable<string> paths, BulkProcessingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of files that would be discovered without enumerating them all.
    /// Useful for progress reporting setup.
    /// </summary>
    /// <param name="paths">The file and directory paths to count.</param>
    /// <param name="options">The options controlling file discovery behavior.</param>
    /// <param name="cancellationToken">Token to cancel the counting operation.</param>
    /// <returns>A task representing the estimated file count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when paths or options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when paths is empty or contains invalid paths.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a specified directory doesn't exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to a directory is denied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<int> EstimateFileCountAsync(IEnumerable<string> paths, BulkProcessingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the specified paths exist and are accessible.
    /// </summary>
    /// <param name="paths">The paths to validate.</param>
    /// <param name="cancellationToken">Token to cancel the validation operation.</param>
    /// <returns>A task representing the validation results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when paths is null.</exception>
    Task<FileDiscoveryValidationResult> ValidatePathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if a file should be included based on extension filters.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <param name="options">The options containing extension filters.</param>
    /// <returns>True if the file should be included, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    bool ShouldIncludeFile(string filePath, BulkProcessingOptions options);
}

/// <summary>
/// Represents the result of file discovery with detailed information.
/// </summary>
public class FileDiscoveryResult
{
    /// <summary>
    /// Gets or sets the full path to the discovered file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name without the path.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file extension.
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets when the file was created.
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// Gets or sets when the file was last modified.
    /// </summary>
    public DateTime ModifiedTime { get; set; }

    /// <summary>
    /// Gets or sets the directory containing the file.
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the depth level of the file relative to the search root.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Gets or sets whether the file is read-only.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets or sets additional file attributes.
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();
}

/// <summary>
/// Represents the result of path validation.
/// </summary>
public class FileDiscoveryValidationResult
{
    /// <summary>
    /// Gets or sets whether all paths are valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors for each path.
    /// </summary>
    public Dictionary<string, List<string>> PathErrors { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of accessible paths.
    /// </summary>
    public int AccessiblePaths { get; set; }

    /// <summary>
    /// Gets or sets the total number of inaccessible paths.
    /// </summary>
    public int InaccessiblePaths { get; set; }
}