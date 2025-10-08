using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using Xunit;
using System.Diagnostics;

namespace EpisodeIdentifier.Tests.Performance;

public class FilenamePerformanceTests
{
    private readonly FilenameService _filenameService;

    public FilenamePerformanceTests()
    {
        _filenameService = new FilenameService();
    }

    [Fact]
    public void GenerateFilename_SingleCall_CompletesUnder10Milliseconds()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "Test Series",
            Season = "1",
            Episode = "1",
            EpisodeName = "Test Episode Name",
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var result = _filenameService.GenerateFilename(request);
        stopwatch.Stop();

        // Assert
        Assert.True(result.IsValid);
        Assert.True(stopwatch.ElapsedMilliseconds < 10, 
            $"Filename generation took {stopwatch.ElapsedMilliseconds}ms, expected < 10ms");
    }

    [Fact]
    public void GenerateFilename_MultipleCalls_AverageUnder10Milliseconds()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "Test Series",
            Season = "1",
            Episode = "1", 
            EpisodeName = "Test Episode Name",
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        var iterations = 1000;
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        for (int i = 0; i < iterations; i++)
        {
            var result = _filenameService.GenerateFilename(request);
            Assert.True(result.IsValid);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = (double)stopwatch.ElapsedMilliseconds / iterations;
        Assert.True(averageMs < 10, 
            $"Average filename generation took {averageMs:F2}ms, expected < 10ms");
    }

    [Fact]
    public void GenerateFilename_LongInputs_CompletesUnder10Milliseconds()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "Very Long Series Name That Might Take More Time To Process Due To Length",
            Season = "1",
            Episode = "1",
            EpisodeName = "Very Long Episode Name That Should Test Performance With Extended Content That Might Require Sanitization And Truncation Operations",
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var result = _filenameService.GenerateFilename(request);
        stopwatch.Stop();

        // Assert
        Assert.True(result.IsValid);
        Assert.True(stopwatch.ElapsedMilliseconds < 10,
            $"Long input filename generation took {stopwatch.ElapsedMilliseconds}ms, expected < 10ms");
    }

    [Fact]
    public void GenerateFilename_InvalidCharacters_CompletesUnder10Milliseconds()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "Test<>Series|With*Invalid?Characters\"",
            Season = "1",
            Episode = "1",
            EpisodeName = "Episode\\With/More:Invalid<Characters>",
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var result = _filenameService.GenerateFilename(request);
        stopwatch.Stop();

        // Assert
        Assert.True(result.IsValid);
        Assert.True(stopwatch.ElapsedMilliseconds < 10,
            $"Invalid character filename generation took {stopwatch.ElapsedMilliseconds}ms, expected < 10ms");
    }

    [Fact]
    public void SanitizeForWindows_LongString_CompletesUnder1Millisecond()
    {
        // Arrange
        var longString = new string('a', 1000) + "<>|?*\":\\/" + new string('b', 1000);
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var result = _filenameService.SanitizeForWindows(longString);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(stopwatch.ElapsedMilliseconds < 1,
            $"String sanitization took {stopwatch.ElapsedMilliseconds}ms, expected < 1ms");
    }

    [Fact]
    public void TruncateToLimit_LongString_CompletesUnder1Millisecond()
    {
        // Arrange
        var longString = new string('a', 10000) + ".mkv";
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var result = _filenameService.TruncateToLimit(longString, 255);
        stopwatch.Stop();

        // Assert
        Assert.True(result.Length <= 255);
        Assert.True(stopwatch.ElapsedMilliseconds < 1,
            $"String truncation took {stopwatch.ElapsedMilliseconds}ms, expected < 1ms");
    }

    [Fact]
    public void IsValidWindowsFilename_ComplexValidation_CompletesUnder1Millisecond()
    {
        // Arrange
        var complexFilename = "Complex-Filename_With.Multiple-Parts.And.Extensions.mkv";
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var result = _filenameService.IsValidWindowsFilename(complexFilename);
        stopwatch.Stop();

        // Assert
        Assert.True(result);
        Assert.True(stopwatch.ElapsedMilliseconds < 1,
            $"Filename validation took {stopwatch.ElapsedMilliseconds}ms, expected < 1ms");
    }

    [Fact]
    public void GenerateFilename_ConcurrentCalls_MaintainsPerformance()
    {
        // Arrange
        var request = new FilenameGenerationRequest
        {
            Series = "Test Series",
            Season = "1", 
            Episode = "1",
            EpisodeName = "Test Episode",
            FileExtension = ".mkv",
            MatchConfidence = 0.95
        };

        var tasks = new List<Task>();
        var results = new List<long>();
        var lockObject = new object();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                
                for (int j = 0; j < 100; j++)
                {
                    var result = _filenameService.GenerateFilename(request);
                    Assert.True(result.IsValid);
                }
                
                stopwatch.Stop();
                
                lock (lockObject)
                {
                    results.Add(stopwatch.ElapsedMilliseconds);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var averageMs = results.Average() / 100.0; // Average per call
        Assert.True(averageMs < 10,
            $"Concurrent filename generation averaged {averageMs:F2}ms per call, expected < 10ms");
    }
}
