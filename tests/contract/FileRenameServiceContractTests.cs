using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using System.Runtime.InteropServices;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for IFileRenameService interface.
/// These tests verify the interface contract without testing implementation details.
/// All tests MUST FAIL until the service is implemented.
/// </summary>
public class FileRenameServiceContractTests
{
    private readonly IFileRenameService _fileRenameService;

    public FileRenameServiceContractTests()
    {
        // This will fail until FileRenameService is implemented
        _fileRenameService = new FileRenameService();
    }

    [Fact]
    public async Task RenameFileAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var tempFile = CreateTemporaryTestFile();
        var request = new FileRenameRequest
        {
            OriginalPath = tempFile,
            SuggestedFilename = "renamed_file.mkv",
            ForceOverwrite = false
        };

        try
        {
            // Act
            var result = await _fileRenameService.RenameFileAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.NewPath.Should().NotBeNullOrEmpty();
            result.NewPath.Should().EndWith("renamed_file.mkv");
            result.ErrorMessage.Should().BeNull();
            result.ErrorType.Should().BeNull();

            // Verify file was actually renamed
            File.Exists(result.NewPath).Should().BeTrue();
            File.Exists(tempFile).Should().BeFalse();
        }
        finally
        {
            // Cleanup
            CleanupTestFile(tempFile);
            CleanupTestFile(Path.Combine(Path.GetDirectoryName(tempFile)!, "renamed_file.mkv"));
        }
    }

    [Fact]
    public async Task RenameFileAsync_WithNonExistentFile_ReturnsFileNotFoundError()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent/file.mkv";
        var request = new FileRenameRequest
        {
            OriginalPath = nonExistentPath,
            SuggestedFilename = "renamed_file.mkv",
            ForceOverwrite = false
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.NewPath.Should().BeNull();
        result.ErrorType.Should().Be(FileRenameError.FileNotFound);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task RenameFileAsync_WithExistingTargetAndNoForceOverwrite_ReturnsTargetExistsError()
    {
        // Arrange
        var sourceFile = CreateTemporaryTestFile();
        var targetFile = CreateTemporaryTestFile("existing_target.mkv");
        var targetFilename = Path.GetFileName(targetFile);

        var request = new FileRenameRequest
        {
            OriginalPath = sourceFile,
            SuggestedFilename = targetFilename,
            ForceOverwrite = false
        };

        try
        {
            // Act
            var result = await _fileRenameService.RenameFileAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.NewPath.Should().BeNull();
            result.ErrorType.Should().Be(FileRenameError.TargetExists);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.ErrorMessage.Should().Contain("already exists");

            // Verify original files still exist
            File.Exists(sourceFile).Should().BeTrue();
            File.Exists(targetFile).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            CleanupTestFile(sourceFile);
            CleanupTestFile(targetFile);
        }
    }

    [Fact]
    public async Task RenameFileAsync_WithExistingTargetAndForceOverwrite_ReturnsSuccessResult()
    {
        // Arrange
        var sourceFile = CreateTemporaryTestFile("source content");
        var targetFile = CreateTemporaryTestFile("target content");
        var targetFilename = Path.GetFileName(targetFile);

        // Delete target file to simulate the rename overwriting it
        File.Delete(targetFile);

        var request = new FileRenameRequest
        {
            OriginalPath = sourceFile,
            SuggestedFilename = targetFilename,
            ForceOverwrite = true
        };

        try
        {
            // Act
            var result = await _fileRenameService.RenameFileAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.NewPath.Should().NotBeNullOrEmpty();
            result.ErrorMessage.Should().BeNull();
            result.ErrorType.Should().BeNull();

            // Verify rename succeeded
            File.Exists(result.NewPath).Should().BeTrue();
            File.Exists(sourceFile).Should().BeFalse();
        }
        finally
        {
            // Cleanup
            CleanupTestFile(sourceFile);
            CleanupTestFile(targetFile);
        }
    }

    [Fact]
    public async Task RenameFileAsync_WithEmptyOriginalPath_ReturnsInvalidPathError()
    {
        // Arrange
        var request = new FileRenameRequest
        {
            OriginalPath = "",
            SuggestedFilename = "renamed_file.mkv",
            ForceOverwrite = false
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.NewPath.Should().BeNull();
        result.ErrorType.Should().Be(FileRenameError.InvalidPath);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ErrorMessage.Should().Contain("path");
    }

    [Fact]
    public async Task RenameFileAsync_WithEmptySuggestedFilename_ReturnsInvalidPathError()
    {
        // Arrange
        var tempFile = CreateTemporaryTestFile();
        var request = new FileRenameRequest
        {
            OriginalPath = tempFile,
            SuggestedFilename = "",
            ForceOverwrite = false
        };

        try
        {
            // Act
            var result = await _fileRenameService.RenameFileAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.NewPath.Should().BeNull();
            result.ErrorType.Should().Be(FileRenameError.InvalidPath);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.ErrorMessage.Should().Contain("filename");
        }
        finally
        {
            // Cleanup
            CleanupTestFile(tempFile);
        }
    }

    [Fact]
    public async Task RenameFileAsync_WithTooLongPath_ReturnsPathTooLongError()
    {
        // Arrange
        var tempFile = CreateTemporaryTestFile();
        var longFilename = new string('A', 300) + ".mkv"; // Exceeds Windows 260 char limit

        var request = new FileRenameRequest
        {
            OriginalPath = tempFile,
            SuggestedFilename = longFilename,
            ForceOverwrite = false
        };

        try
        {
            // Act
            var result = await _fileRenameService.RenameFileAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.NewPath.Should().BeNull();
            result.ErrorType.Should().Be(FileRenameError.PathTooLong);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.ErrorMessage.Should().Contain("length");
        }
        finally
        {
            // Cleanup
            CleanupTestFile(tempFile);
        }
    }

    [Fact]
    public void CanRenameFile_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var tempFile = CreateTemporaryTestFile();

        try
        {
            // Act
            var result = _fileRenameService.CanRenameFile(tempFile);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            CleanupTestFile(tempFile);
        }
    }

    [Fact]
    public void CanRenameFile_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent/file.mkv";

        // Act
        var result = _fileRenameService.CanRenameFile(nonExistentPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanRenameFile_WithEmptyPath_ReturnsFalse()
    {
        // Arrange
        var emptyPath = "";

        // Act
        var result = _fileRenameService.CanRenameFile(emptyPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetTargetPath_WithValidInputs_ReturnsCorrectPath()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var originalPath = Path.Combine(tempDir, "videos", "original_file.mkv");
        var suggestedFilename = "new_filename.mkv";

        // Act
        var result = _fileRenameService.GetTargetPath(originalPath, suggestedFilename);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("new_filename.mkv");
        result.Should().StartWith(Path.Combine(tempDir, "videos"));
        result.Should().Be(Path.Combine(tempDir, "videos", "new_filename.mkv"));
    }

    [Fact]
    public void GetTargetPath_WithValidPaths_ReturnsCorrectPath()
    {
        // Arrange - Use temporary directory
        var tempDir = Path.GetTempPath();
        var originalPath = Path.Combine(tempDir, "test_videos", "original_file.mkv");
        var suggestedFilename = "new_filename.mkv";
        var expectedPrefix = Path.Combine(tempDir, "test_videos");

        // Act
        var result = _fileRenameService.GetTargetPath(originalPath, suggestedFilename);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("new_filename.mkv");
        result.Should().StartWith(expectedPrefix);
    }

    [Fact]
    public void GetTargetPath_WithEmptyOriginalPath_ThrowsArgumentException()
    {
        // Arrange
        var originalPath = "";
        var suggestedFilename = "new_filename.mkv";

        // Act & Assert
        var act = () => _fileRenameService.GetTargetPath(originalPath, suggestedFilename);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetTargetPath_WithEmptySuggestedFilename_ThrowsArgumentException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var originalPath = Path.Combine(tempDir, "videos", "original_file.mkv");
        var suggestedFilename = "";

        // Act & Assert
        var act = () => _fileRenameService.GetTargetPath(originalPath, suggestedFilename);
        act.Should().Throw<ArgumentException>();
    }

    // Helper methods for test file management
    private string CreateTemporaryTestFile(string content = "test content")
    {
        var tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, content);
        return tempPath;
    }

    private string CreateTemporaryTestFile(string filename, string content = "test content")
    {
        var tempDir = Path.GetTempPath();
        var tempPath = Path.Combine(tempDir, filename);
        File.WriteAllText(tempPath, content);
        return tempPath;
    }

    private void CleanupTestFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }
}
