using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for discovering files in directories for bulk processing.
/// Provides streaming file enumeration with filtering and error handling.
/// </summary>
public class FileDiscoveryService : IFileDiscoveryService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<FileDiscoveryService> _logger;

    /// <summary>
    /// Initializes a new instance of the FileDiscoveryService class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="logger">The logger for this service.</param>
    public FileDiscoveryService(IFileSystem fileSystem, ILogger<FileDiscoveryService> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> DiscoverFilesAsync(
        IEnumerable<string> paths,
        BulkProcessingOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (paths == null) throw new ArgumentNullException(nameof(paths));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Starting file discovery for {PathCount} paths", paths.Count());

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.LogWarning("Skipping null or empty path");
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Check if path is a file or directory
            if (_fileSystem.File.Exists(path))
            {
                if (ShouldIncludeFile(path, options))
                {
                    _logger.LogDebug("Discovered file: {FilePath}", path);
                    yield return path;
                }
            }
            else if (_fileSystem.Directory.Exists(path))
            {
                await foreach (var file in DiscoverFilesInDirectoryAsync(path, options, 0, cancellationToken))
                {
                    yield return file;
                }
            }
            else
            {
                _logger.LogWarning("Path does not exist: {Path}", path);
                throw new DirectoryNotFoundException($"Path does not exist: {path}");
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FileDiscoveryResult> DiscoverFilesWithInfoAsync(
        IEnumerable<string> paths,
        BulkProcessingOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (paths == null) throw new ArgumentNullException(nameof(paths));
        if (options == null) throw new ArgumentNullException(nameof(options));

        await foreach (var filePath in DiscoverFilesAsync(paths, options, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileDiscoveryResult? result = null;
            try
            {
                var fileInfo = _fileSystem.FileInfo.New(filePath);
                var directoryInfo = fileInfo.Directory;

                result = new FileDiscoveryResult
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    Extension = fileInfo.Extension,
                    FileSizeBytes = fileInfo.Length,
                    CreatedTime = fileInfo.CreationTimeUtc,
                    ModifiedTime = fileInfo.LastWriteTimeUtc,
                    Directory = directoryInfo?.FullName ?? string.Empty,
                    IsReadOnly = fileInfo.IsReadOnly,
                    Depth = CalculateDepth(filePath, paths)
                };

                _logger.LogDebug("Collected file info for: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect file info for: {FilePath}", filePath);
                // Create a minimal result with just the path
                result = new FileDiscoveryResult
                {
                    FilePath = filePath,
                    FileName = _fileSystem.Path.GetFileName(filePath),
                    Extension = _fileSystem.Path.GetExtension(filePath)
                };
            }

            if (result != null)
            {
                yield return result;
            }
        }
    }

    /// <inheritdoc />
    public async Task<int> EstimateFileCountAsync(
        IEnumerable<string> paths,
        BulkProcessingOptions options,
        CancellationToken cancellationToken = default)
    {
        if (paths == null) throw new ArgumentNullException(nameof(paths));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("Estimating file count for {PathCount} paths", paths.Count());

        var count = 0;

        await foreach (var _ in DiscoverFilesAsync(paths, options, cancellationToken))
        {
            count++;

            // Periodically check cancellation
            if (count % 1000 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        _logger.LogInformation("Estimated {FileCount} files", count);
        return count;
    }

    /// <inheritdoc />
    public async Task<FileDiscoveryValidationResult> ValidatePathsAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        if (paths == null) throw new ArgumentNullException(nameof(paths));

        var result = new FileDiscoveryValidationResult();
        var pathList = paths.ToList();

        _logger.LogInformation("Validating {PathCount} paths", pathList.Count);

        foreach (var path in pathList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(path))
            {
                result.PathErrors[path] = new List<string> { "Path is null or empty" };
                result.InaccessiblePaths++;
                continue;
            }

            var errors = new List<string>();

            try
            {
                if (_fileSystem.File.Exists(path))
                {
                    // Check if file is accessible
                    try
                    {
                        var fileInfo = _fileSystem.FileInfo.New(path);
                        _ = fileInfo.Length; // Try to access file properties
                        result.AccessiblePaths++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        errors.Add("Access denied to file");
                        result.InaccessiblePaths++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"File access error: {ex.Message}");
                        result.InaccessiblePaths++;
                    }
                }
                else if (_fileSystem.Directory.Exists(path))
                {
                    // Check if directory is accessible
                    try
                    {
                        _ = _fileSystem.Directory.EnumerateFileSystemEntries(path).Take(1).ToList();
                        result.AccessiblePaths++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        errors.Add("Access denied to directory");
                        result.InaccessiblePaths++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Directory access error: {ex.Message}");
                        result.InaccessiblePaths++;
                    }
                }
                else
                {
                    errors.Add("Path does not exist");
                    result.InaccessiblePaths++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Path validation error: {ex.Message}");
                result.InaccessiblePaths++;
            }

            if (errors.Any())
            {
                result.PathErrors[path] = errors;
            }
        }

        result.IsValid = result.InaccessiblePaths == 0;

        _logger.LogInformation("Path validation completed: {AccessiblePaths} accessible, {InaccessiblePaths} inaccessible",
            result.AccessiblePaths, result.InaccessiblePaths);

        return await Task.FromResult(result);
    }

    /// <inheritdoc />
    public bool ShouldIncludeFile(string filePath, BulkProcessingOptions options)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var extension = _fileSystem.Path.GetExtension(filePath);

        // Check exclude list first (takes precedence)
        if (options.ExcludeExtensions.Any() &&
            options.ExcludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // If include list is specified, file must be in it
        if (options.IncludeExtensions.Any())
        {
            return options.IncludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        // If no filters specified, include all files
        return true;
    }

    /// <summary>
    /// Discovers files in a specific directory with depth control.
    /// </summary>
    private async IAsyncEnumerable<string> DiscoverFilesInDirectoryAsync(
        string directoryPath,
        BulkProcessingOptions options,
        int currentDepth,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IEnumerable<string> files;
        try
        {
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = false, // We handle recursion manually for depth control
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
                MatchType = MatchType.Simple,
                ReturnSpecialDirectories = false
            };

            files = _fileSystem.Directory.EnumerateFiles(directoryPath, "*", enumerationOptions);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to directory: {DirectoryPath}", directoryPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enumerating files in directory: {DirectoryPath}", directoryPath);
            throw;
        }

        // Process files in current directory
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldIncludeFile(filePath, options))
            {
                _logger.LogDebug("Discovered file: {FilePath}", filePath);
                yield return filePath;
            }
        }

        // Process subdirectories if recursive is enabled and we haven't exceeded max depth
        if (options.Recursive && (options.MaxDepth == 0 || currentDepth < options.MaxDepth))
        {
            IEnumerable<string> directories;
            try
            {
                directories = _fileSystem.Directory.EnumerateDirectories(directoryPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to subdirectories in: {DirectoryPath}", directoryPath);
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating directories in: {DirectoryPath}", directoryPath);
                yield break;
            }

            foreach (var subDirectory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await foreach (var file in DiscoverFilesInDirectoryAsync(subDirectory, options, currentDepth + 1, cancellationToken))
                {
                    yield return file;
                }
            }
        }
        else if (options.MaxDepth > 0 && currentDepth >= options.MaxDepth)
        {
            _logger.LogDebug("Skipping subdirectories of {DirectoryPath} - maximum depth {MaxDepth} reached",
                directoryPath, options.MaxDepth);
        }
    }

    /// <summary>
    /// Calculates the depth of a file path relative to the search roots.
    /// </summary>
    private int CalculateDepth(string filePath, IEnumerable<string> searchPaths)
    {
        var minDepth = int.MaxValue;

        foreach (var searchPath in searchPaths)
        {
            try
            {
                var searchFullPath = _fileSystem.Path.GetFullPath(searchPath);
                var fileFullPath = _fileSystem.Path.GetFullPath(filePath);

                if (fileFullPath.StartsWith(searchFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = _fileSystem.Path.GetRelativePath(searchFullPath, fileFullPath);
                    var depth = relativePath.Split(_fileSystem.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length - 1;
                    minDepth = Math.Min(minDepth, Math.Max(0, depth));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to calculate depth for {FilePath} relative to {SearchPath}", filePath, searchPath);
            }
        }

        return minDepth == int.MaxValue ? 0 : minDepth;
    }
}
