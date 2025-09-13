using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Interfaces;
using EpisodeIdentifier.Core.Models.Configuration;

namespace EpisodeIdentifier.Tests.Contract;

/// <summary>
/// Contract tests for ICTPhHashingService interface.
/// These tests verify the interface contract without testing implementation details.
/// All tests MUST FAIL until the service is implemented.
/// </summary>
public class CTPhHashingServiceContractTests
{
    private readonly ICTPhHashingService _hashingService;
    private readonly string _testFilePath = "/path/to/test/file.mkv";
    private readonly string _validHashExample = "3:AXGBicFlgVNhBGcL6wCrFQEv:AXGHsNhxLsr2C";

    public CTPhHashingServiceContractTests()
    {
        // This will fail until ICTPhHashingService is implemented
        _hashingService = null!; // Will be injected when service exists
    }

    [Fact]
    public async Task ComputeFuzzyHash_WithValidFile_ReturnsHashString()
    {
        // Arrange - This test MUST FAIL until implementation exists

        // Act
        var hash = await _hashingService.ComputeFuzzyHash(_testFilePath);

        // Assert
        hash.Should().NotBeNull();
        hash.Should().NotBeEmpty();
        hash.Should().MatchRegex(@"^\d+:[A-Za-z0-9+/]+:[A-Za-z0-9+/]+$",
            "CTPH hash should match expected format");
    }

    [Fact]
    public async Task ComputeFuzzyHash_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var nonExistentFile = "/path/to/nonexistent/file.mkv";

        // Act & Assert
        await _hashingService.Invoking(s => s.ComputeFuzzyHash(nonExistentFile))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ComputeFuzzyHash_WithUnauthorizedFile_ThrowsUnauthorizedAccessException()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var unauthorizedFile = "/root/restricted/file.mkv";

        // Act & Assert
        await _hashingService.Invoking(s => s.ComputeFuzzyHash(unauthorizedFile))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void CompareFuzzyHashes_WithIdenticalHashes_Returns100()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var hash1 = _validHashExample;
        var hash2 = _validHashExample;

        // Act
        var similarity = _hashingService.CompareFuzzyHashes(hash1, hash2);

        // Assert
        similarity.Should().Be(100);
    }

    [Fact]
    public void CompareFuzzyHashes_WithSimilarHashes_ReturnsHighScore()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var hash1 = "3:AXGBicFlgVNhBGcL6wCrFQEv:AXGHsNhxLsr2C";
        var hash2 = "3:AXGBicFlgVNhBGcL6wCrFQEw:AXGHsNhxLsr2D";

        // Act
        var similarity = _hashingService.CompareFuzzyHashes(hash1, hash2);

        // Assert
        similarity.Should().BeInRange(0, 100);
        similarity.Should().BeGreaterThan(50, "Similar hashes should have high similarity");
    }

    [Fact]
    public void CompareFuzzyHashes_WithCompletelyDifferentHashes_ReturnsLowScore()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var hash1 = "3:AXGBicFlgVNhBGcL6wCrFQEv:AXGHsNhxLsr2C";
        var hash2 = "12:ZZZBicFlgVNhBGcL6wCrFQEv:ZZZHsNhxLsr2C";

        // Act
        var similarity = _hashingService.CompareFuzzyHashes(hash1, hash2);

        // Assert
        similarity.Should().BeInRange(0, 100);
        similarity.Should().BeLessThan(25, "Different hashes should have low similarity");
    }

    [Fact]
    public void CompareFuzzyHashes_WithInvalidHashFormat_ThrowsArgumentException()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var validHash = _validHashExample;
        var invalidHash = "invalid-hash-format";

        // Act & Assert
        _hashingService.Invoking(s => s.CompareFuzzyHashes(validHash, invalidHash))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Invalid hash format*");
    }

    [Fact]
    public async Task CompareFiles_WithValidFiles_ReturnsFuzzyHashResult()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var file1 = "/path/to/test/file1.mkv";
        var file2 = "/path/to/test/file2.mkv";

        // Act
        var result = await _hashingService.CompareFiles(file1, file2);

        // Assert
        result.Should().NotBeNull();
        result.Hash1.Should().NotBeNull().And.NotBeEmpty();
        result.Hash2.Should().NotBeNull().And.NotBeEmpty();
        result.SimilarityScore.Should().BeInRange(0, 100);
        result.IsMatch.Should().Be(result.SimilarityScore >= _hashingService.GetSimilarityThreshold());
        result.ComparisonTime.Should().BePositive();
    }

    [Fact]
    public async Task CompareFiles_WithIdenticalFiles_ReturnsHighSimilarity()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var file1 = "/path/to/test/identical.mkv";
        var file2 = "/path/to/test/identical.mkv";

        // Act
        var result = await _hashingService.CompareFiles(file1, file2);

        // Assert
        result.Should().NotBeNull();
        result.SimilarityScore.Should().Be(100);
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void GetSimilarityThreshold_ReturnsConfiguredThreshold()
    {
        // Arrange - This test MUST FAIL until implementation exists

        // Act
        var threshold = _hashingService.GetSimilarityThreshold();

        // Assert
        threshold.Should().BeInRange(0, 100);
        threshold.Should().BeGreaterThan(50, "Threshold should be reasonable for fuzzy matching");
    }

    [Fact]
    public async Task ComputeFuzzyHash_WithCorruptedFile_ThrowsHashingException()
    {
        // Arrange - This test MUST FAIL until implementation exists
        var corruptedFile = "/path/to/corrupted/file.mkv";

        // Act & Assert
        await _hashingService.Invoking(s => s.ComputeFuzzyHash(corruptedFile))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CTPH computation failed*");
    }
}