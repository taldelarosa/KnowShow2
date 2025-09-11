using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using Xunit;
using System.IO;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Unit;

public class FileRenameServiceTests
{
    private readonly FileRenameService _fileRenameService;
    private readonly string _testDirectory;

    public FileRenameServiceTests()
    {
        _fileRenameService = new FileRenameService();
        _testDirectory = Path.Combine(Path.GetTempPath(), "EpisodeIdentifierTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RenameFileAsync_FileNotFound_ReturnsFileNotFoundError()
    {
        // Arrange
        var request = new FileRenameRequest
        {
            OriginalPath = Path.Combine(_testDirectory, "nonexistent.mkv"),
            SuggestedFilename = "Test Series - S01E01 - Episode.mkv"
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(FileRenameError.FileNotFound, result.ErrorType);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenameFileAsync_TargetExists_ReturnsTargetExistsError()
    {
        // Arrange
        var originalFile = Path.Combine(_testDirectory, "original.mkv");
        var targetFile = Path.Combine(_testDirectory, "Test Series - S01E01 - Episode.mkv");
        
        await File.WriteAllTextAsync(originalFile, "test content");
        await File.WriteAllTextAsync(targetFile, "existing content");

        var request = new FileRenameRequest
        {
            OriginalPath = originalFile,
            SuggestedFilename = "Test Series - S01E01 - Episode.mkv"
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(FileRenameError.TargetExists, result.ErrorType);
        Assert.Contains("already exists", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenameFileAsync_InvalidPath_ReturnsInvalidPathError()
    {
        // Arrange
        var originalFile = Path.Combine(_testDirectory, "original.mkv");
        await File.WriteAllTextAsync(originalFile, "test content");

        var request = new FileRenameRequest
        {
            OriginalPath = originalFile,
            SuggestedFilename = "Invalid<>Filename?.mkv"  // Invalid characters
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(FileRenameError.InvalidPath, result.ErrorType);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenameFileAsync_PathTooLong_ReturnsPathTooLongError()
    {
        // Arrange
        var originalFile = Path.Combine(_testDirectory, "original.mkv");
        await File.WriteAllTextAsync(originalFile, "test content");

        // Create a filename that would result in a path longer than Windows limit
        var longFilename = new string('a', 300) + ".mkv";

        var request = new FileRenameRequest
        {
            OriginalPath = originalFile,
            SuggestedFilename = longFilename
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(FileRenameError.PathTooLong, result.ErrorType);
        Assert.Contains("too long", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenameFileAsync_EmptyOriginalPath_ReturnsInvalidPathError()
    {
        // Arrange
        var request = new FileRenameRequest
        {
            OriginalPath = "",
            SuggestedFilename = "Test Series - S01E01 - Episode.mkv"
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(FileRenameError.InvalidPath, result.ErrorType);
    }

    [Fact]
    public async Task RenameFileAsync_EmptySuggestedFilename_ReturnsInvalidPathError()
    {
        // Arrange
        var originalFile = Path.Combine(_testDirectory, "original.mkv");
        await File.WriteAllTextAsync(originalFile, "test content");

        var request = new FileRenameRequest
        {
            OriginalPath = originalFile,
            SuggestedFilename = ""
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(FileRenameError.InvalidPath, result.ErrorType);
    }

    [Fact]
    public async Task RenameFileAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var originalFile = Path.Combine(_testDirectory, "original.mkv");
        await File.WriteAllTextAsync(originalFile, "test content");

        var request = new FileRenameRequest
        {
            OriginalPath = originalFile,
            SuggestedFilename = "Test Series - S01E01 - Episode.mkv"
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorType);
        Assert.NotNull(result.NewPath);
        Assert.True(File.Exists(result.NewPath));
        Assert.False(File.Exists(originalFile));
    }

    [Fact]
    public void CanRenameFile_FileExists_ReturnsTrue()
    {
        // Arrange
        var originalFile = Path.Combine(_testDirectory, "original.mkv");
        File.WriteAllText(originalFile, "test content");

        // Act
        var result = _fileRenameService.CanRenameFile(originalFile);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanRenameFile_FileNotExists_ReturnsFalse()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.mkv");

        // Act
        var result = _fileRenameService.CanRenameFile(nonExistentFile);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanRenameFile_EmptyPath_ReturnsFalse()
    {
        // Act
        var result = _fileRenameService.CanRenameFile("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanRenameFile_NullPath_ReturnsFalse()
    {
        // Act
        var result = _fileRenameService.CanRenameFile(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RenameFileAsync_ForceOverwrite_OverwritesExistingFile()
    {
        // Arrange
        var originalFile = Path.Combine(_testDirectory, "original.mkv");
        var targetFile = Path.Combine(_testDirectory, "Test Series - S01E01 - Episode.mkv");
        
        await File.WriteAllTextAsync(originalFile, "original content");
        await File.WriteAllTextAsync(targetFile, "existing content");

        var request = new FileRenameRequest
        {
            OriginalPath = originalFile,
            SuggestedFilename = "Test Series - S01E01 - Episode.mkv",
            ForceOverwrite = true
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(result.NewPath));
        Assert.False(File.Exists(originalFile));
        
        var newContent = await File.ReadAllTextAsync(result.NewPath);
        Assert.Equal("original content", newContent);
    }

    [Fact]
    public async Task RenameFileAsync_CrossDirectoryRename_HandlesCorrectly()
    {
        // Arrange
        var subDirectory = Path.Combine(_testDirectory, "subdirectory");
        Directory.CreateDirectory(subDirectory);
        
        var originalFile = Path.Combine(_testDirectory, "original.mkv");
        await File.WriteAllTextAsync(originalFile, "test content");

        var request = new FileRenameRequest
        {
            OriginalPath = originalFile,
            SuggestedFilename = Path.Combine("subdirectory", "Test Series - S01E01 - Episode.mkv")
        };

        // Act
        var result = await _fileRenameService.RenameFileAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(result.NewPath));
        Assert.False(File.Exists(originalFile));
        Assert.Contains("subdirectory", result.NewPath);
    }
}
