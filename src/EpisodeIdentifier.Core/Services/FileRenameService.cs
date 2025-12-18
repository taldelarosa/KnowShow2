using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using System.Linq;

namespace EpisodeIdentifier.Core.Services;

/// <summary>
/// Service for performing safe file rename operations with comprehensive error handling.
/// </summary>
public class FileRenameService : IFileRenameService
{
    /// <inheritdoc/>
    public async Task<FileRenameResult> RenameFileAsync(FileRenameRequest request)
    {
        // Input validation
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return validationError;
        }

        // Check source file exists
        if (!File.Exists(request.OriginalPath))
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.FileNotFound,
                ErrorMessage = $"Source file not found: {request.OriginalPath}"
            };
        }

        // Generate target path
        string targetPath;
        try
        {
            targetPath = GetTargetPath(request.OriginalPath, request.SuggestedFilename);
        }
        catch (ArgumentException ex)
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.InvalidPath,
                ErrorMessage = ex.Message
            };
        }

        // Path length validation
        if (targetPath.Length > 260)
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.PathTooLong,
                ErrorMessage = $"Target path is too long (exceeds maximum length of 260 characters): {targetPath.Length}"
            };
        }

        // Check for target collision and find unique name if needed
        if (File.Exists(targetPath) && !request.ForceOverwrite)
        {
            // Try to find a unique filename by adding underscores
            string? uniqueTargetPath = FindUniqueFilename(targetPath);
            
            if (uniqueTargetPath != null)
            {
                // Use the unique path instead
                targetPath = uniqueTargetPath;
            }
            else
            {
                // Could not find a unique name (unlikely but possible after many attempts)
                return new FileRenameResult
                {
                    Success = false,
                    ErrorType = FileRenameError.TargetExists,
                    ErrorMessage = $"Target file already exists and could not generate unique name: {targetPath}"
                };
            }
        }

        // Check if we can rename the file
        if (!CanRenameFile(request.OriginalPath))
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.PermissionDenied,
                ErrorMessage = "Cannot rename file: insufficient permissions or file is in use"
            };
        }

        // Perform the rename operation
        try
        {
            // Use async file operations to avoid blocking
            await Task.Run(() =>
            {
                // If target exists and force overwrite is enabled, delete it first
                if (File.Exists(targetPath) && request.ForceOverwrite)
                {
                    File.Delete(targetPath);
                }

                // Perform the rename
                File.Move(request.OriginalPath, targetPath);
            });

            return new FileRenameResult
            {
                Success = true,
                NewPath = targetPath
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.PermissionDenied,
                ErrorMessage = $"Permission denied: {ex.Message}"
            };
        }
        catch (DirectoryNotFoundException ex)
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.InvalidPath,
                ErrorMessage = $"Directory not found: {ex.Message}"
            };
        }
        catch (IOException ex) when (ex.Message.Contains("not enough space") || ex.Message.Contains("disk full"))
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.DiskFull,
                ErrorMessage = $"Insufficient disk space: {ex.Message}"
            };
        }
        catch (IOException ex)
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.PermissionDenied,
                ErrorMessage = $"File operation failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.InvalidPath,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public bool CanRenameFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        try
        {
            // Check if file exists
            if (!File.Exists(filePath))
                return false;

            // Check if path format is valid
            var fullPath = Path.GetFullPath(filePath);
            if (string.IsNullOrEmpty(fullPath))
                return false;

            // Check directory write permissions by attempting to create a temp file
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                return false;

            var tempFile = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                File.WriteAllText(tempFile, "");
                File.Delete(tempFile);
            }
            catch
            {
                return false; // No write permission
            }

            // Check if file is not locked by trying to open it
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                // If we can open it for reading, it should be renamable
            }
            catch (IOException)
            {
                return false; // File is locked/in use
            }
            catch (UnauthorizedAccessException)
            {
                return false; // No permission
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public string GetTargetPath(string originalPath, string suggestedFilename)
    {
        if (string.IsNullOrWhiteSpace(originalPath))
            throw new ArgumentException("Original path cannot be null or empty", nameof(originalPath));

        if (string.IsNullOrWhiteSpace(suggestedFilename))
            throw new ArgumentException("Suggested filename cannot be null or empty", nameof(suggestedFilename));

        // Validate filename for invalid characters (Windows-specific) - allow path separators for cross-directory renames
        var invalidChars = new char[] { '<', '>', ':', '"', '|', '?', '*' };
        if (suggestedFilename.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException($"Suggested filename contains invalid characters: {suggestedFilename}", nameof(suggestedFilename));
        }

        try
        {
            var directory = Path.GetDirectoryName(originalPath);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("Cannot determine directory from original path", nameof(originalPath));

            // If suggested filename contains directory path, handle it properly
            string targetPath;
            if (Path.IsPathRooted(suggestedFilename))
            {
                // If it's an absolute path, use it directly
                targetPath = suggestedFilename;
            }
            else
            {
                // If it's relative, combine with the original directory
                // This handles cases like "subdirectory/filename.mkv"
                targetPath = Path.Combine(directory, suggestedFilename);
            }

            // Ensure the target directory exists
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Validate the resulting path
            var fullPath = Path.GetFullPath(targetPath);
            return fullPath;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new ArgumentException($"Invalid path format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Finds a unique filename by adding underscores before the extension when the target already exists.
    /// For example: "file.mkv" becomes "file_.mkv", then "file__.mkv", etc.
    /// </summary>
    /// <param name="targetPath">The desired target path that already exists</param>
    /// <returns>A unique path with underscores added, or null if unable to find unique name after 100 attempts</returns>
    private string? FindUniqueFilename(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        
        // Try up to 100 times to find a unique name
        for (int i = 1; i <= 100; i++)
        {
            // Add underscores before the extension
            var underscores = new string('_', i);
            var newFileName = $"{fileNameWithoutExtension}{underscores}{extension}";
            var newPath = Path.Combine(directory, newFileName);
            
            if (!File.Exists(newPath))
            {
                return newPath;
            }
        }
        
        // Could not find a unique name after 100 attempts
        return null;
    }

    private FileRenameResult? ValidateRequest(FileRenameRequest request)
    {
        if (request == null)
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.InvalidPath,
                ErrorMessage = "Request cannot be null"
            };
        }

        if (string.IsNullOrWhiteSpace(request.OriginalPath))
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.InvalidPath,
                ErrorMessage = "Original path cannot be empty"
            };
        }

        if (string.IsNullOrWhiteSpace(request.SuggestedFilename))
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.InvalidPath,
                ErrorMessage = "Suggested filename cannot be empty"
            };
        }

        // Check for invalid characters in filename - but allow path separators for cross-directory renames
        var invalidChars = Path.GetInvalidFileNameChars().Where(c => c != '/' && c != '\\').ToArray();
        if (request.SuggestedFilename.IndexOfAny(invalidChars) >= 0)
        {
            return new FileRenameResult
            {
                Success = false,
                ErrorType = FileRenameError.InvalidPath,
                ErrorMessage = "Suggested filename contains invalid characters"
            };
        }

        return null; // Validation passed
    }
}
