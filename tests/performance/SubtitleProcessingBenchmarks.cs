using System;
using System.IO;
using System.Threading.Tasks;

namespace EpisodeIdentifier.Tests.Performance;

[MemoryDiagnoser]
[SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net80)]
public class SubtitleProcessingBenchmarks
{
    private ServiceProvider _serviceProvider = null!;
    private SubtitleWorkflowCoordinator _coordinator = null!;
    private VideoFormatValidator _validator = null!;
    private ITextSubtitleExtractor _textExtractor = null!;
    private string _testVideoPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        
        // Add logging with minimal output for benchmarks
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        
        // Add core services
        services.AddTransient<VideoFormatValidator>();
        services.AddTransient<SubtitleExtractor>();
        services.AddTransient<ITextSubtitleExtractor, TextSubtitleExtractor>();
        services.AddTransient<PgsRipService>();
        services.AddTransient<EnhancedPgsToTextConverter>(provider =>
            new EnhancedPgsToTextConverter(
                provider.GetRequiredService<ILogger<EnhancedPgsToTextConverter>>(),
                provider.GetRequiredService<PgsRipService>(),
                provider.GetRequiredService<PgsToTextConverter>(),
                true)); // Enable OCR fallback for benchmarks
        services.AddTransient<PgsToTextConverter>();
        services.AddTransient<SubtitleMatcher>();
        services.AddTransient<SubtitleNormalizationService>();
        services.AddTransient<FuzzyHashService>(provider => 
            new FuzzyHashService(
                ":memory:", // Use in-memory SQLite database for tests
                provider.GetRequiredService<ILogger<FuzzyHashService>>(),
                provider.GetRequiredService<SubtitleNormalizationService>()));
        services.AddTransient<SubtitleWorkflowCoordinator>();
        
        _serviceProvider = services.BuildServiceProvider();
        _coordinator = _serviceProvider.GetRequiredService<SubtitleWorkflowCoordinator>();
        _validator = _serviceProvider.GetRequiredService<VideoFormatValidator>();
        _textExtractor = _serviceProvider.GetRequiredService<ITextSubtitleExtractor>();
        
        _testVideoPath = "/mnt/c/src/KnowShow/TestData/media/Episode S02E01.mkv";
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    [Benchmark]
    public async Task<IdentificationResult> ProcessVideo_FullWorkflow()
    {
        if (!File.Exists(_testVideoPath))
        {
            return new IdentificationResult
            {
                Error = new IdentificationError { Code = "FILE_NOT_FOUND", Message = "Test file not available" }
            };
        }

        return await _coordinator.ProcessVideoAsync(_testVideoPath);
    }

    [Benchmark]
    public async Task<IList<SubtitleTrackInfo>> DetectSubtitleTracks()
    {
        if (!File.Exists(_testVideoPath))
        {
            return new List<SubtitleTrackInfo>();
        }

        return await _validator.GetSubtitleTracks(_testVideoPath);
    }

    [Benchmark]
    public async Task<IReadOnlyList<TextSubtitleTrack>> DetectTextTracks()
    {
        if (!File.Exists(_testVideoPath))
        {
            return new List<TextSubtitleTrack>().AsReadOnly();
        }

        return await _textExtractor.DetectTextSubtitleTracksAsync(_testVideoPath);
    }

    [Benchmark]
    public async Task<TextSubtitleExtractionResult> ExtractTextSubtitle()
    {
        if (!File.Exists(_testVideoPath))
        {
            return new TextSubtitleExtractionResult
            {
                Status = ProcessingStatus.Failed,
                ErrorMessage = "Test file not available"
            };
        }

        var tracks = await _textExtractor.DetectTextSubtitleTracksAsync(_testVideoPath);
        if (!tracks.Any())
        {
            return new TextSubtitleExtractionResult
            {
                Status = ProcessingStatus.Failed,
                ErrorMessage = "No text tracks found"
            };
        }

        return await _textExtractor.ExtractTextSubtitleContentAsync(_testVideoPath, tracks.First());
    }

    /// <summary>
    /// Benchmarks workflow coordination overhead
    /// </summary>
    [Benchmark]
    public async Task<IdentificationResult> WorkflowCoordination_PgsFirst()
    {
        if (!File.Exists(_testVideoPath))
        {
            return new IdentificationResult
            {
                Error = new IdentificationError { Code = "FILE_NOT_FOUND", Message = "Test file not available" }
            };
        }

        // This will test the coordination logic without language preference
        return await _coordinator.ProcessVideoAsync(_testVideoPath);
    }

    /// <summary>
    /// Benchmarks workflow coordination with language preference
    /// </summary>
    [Benchmark]
    public async Task<IdentificationResult> WorkflowCoordination_WithLanguage()
    {
        if (!File.Exists(_testVideoPath))
        {
            return new IdentificationResult
            {
                Error = new IdentificationError { Code = "FILE_NOT_FOUND", Message = "Test file not available" }
            };
        }

        return await _coordinator.ProcessVideoAsync(_testVideoPath, "eng");
    }
}
