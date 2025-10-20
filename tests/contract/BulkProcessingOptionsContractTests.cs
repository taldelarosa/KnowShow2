using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models.Configuration;
using EpisodeIdentifier.Core.Interfaces;
using System.IO.Abstractions.TestingHelpers;

namespace EpisodeIdentifier.Tests.Contract
{
    /// <summary>
    /// Contract tests for BulkProcessingOptions configuration-based concurrency behavior.
    /// Tests that BulkProcessingOptions correctly reads MaxConcurrency from configuration service
    /// instead of using hardcoded Environment.ProcessorCount default.
    /// </summary>
    public class BulkProcessingOptionsContractTests
    {
        private readonly ServiceProvider _serviceProvider;

        public BulkProcessingOptionsContractTests()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            // Create a test configuration file
            var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
            var mockFileSystem = new MockFileSystem();
            services.AddSingleton<System.IO.Abstractions.IFileSystem>(mockFileSystem);

            // Register ConfigurationService for both interfaces it implements
            // This test specifically needs IAppConfigService for BulkProcessingOptions.CreateFromConfigurationAsync
            services.AddSingleton<ConfigurationService>(provider => new ConfigurationService(
                provider.GetRequiredService<ILogger<ConfigurationService>>(),
                mockFileSystem,
                configPath));
            services.AddSingleton<IAppConfigService>(provider => provider.GetRequiredService<ConfigurationService>());
            services.AddSingleton<IConfigurationService>(provider => provider.GetRequiredService<ConfigurationService>());

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task CreateFromConfiguration_WithDefaultConfig_ShouldUseConfiguredMaxConcurrency()
        {
            // Arrange
            var configService = _serviceProvider.GetRequiredService<IAppConfigService>();

            // Create config with MaxConcurrency = 1 (default from our T001 implementation)
            await CreateValidConfigurationFile(maxConcurrency: 1);

            // Act
            var options = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);

            // Assert
            options.Should().NotBeNull();
            options.MaxConcurrency.Should().Be(1, "should use configured MaxConcurrency instead of Environment.ProcessorCount");
        }

        [Theory]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(25)]
        [InlineData(50)]
        [InlineData(100)]
        public async Task CreateFromConfiguration_WithValidMaxConcurrency_ShouldUseConfiguredValue(int expectedConcurrency)
        {
            // Arrange
            var configService = _serviceProvider.GetRequiredService<IAppConfigService>();
            await CreateValidConfigurationFile(maxConcurrency: expectedConcurrency);

            // Act
            var options = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);

            // Assert
            options.MaxConcurrency.Should().Be(expectedConcurrency, "should use the exact configured MaxConcurrency value");
        }

        [Fact]
        public async Task CreateFromConfiguration_WhenConfigurationFailsToLoad_ShouldUseFallbackDefault()
        {
            // Arrange
            var configService = _serviceProvider.GetRequiredService<IAppConfigService>();
            // Do not create config file - this should cause load failure

            // Act
            var options = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);

            // Assert
            options.Should().NotBeNull();
            options.MaxConcurrency.Should().Be(1, "should use fallback default when configuration fails to load");
        }

        [Fact]
        public async Task CreateFromConfiguration_WithInvalidMaxConcurrency_ShouldUseFallbackDefault()
        {
            // Arrange
            var configService = _serviceProvider.GetRequiredService<IAppConfigService>();
            await CreateInvalidConfigurationFile(maxConcurrency: 101); // Invalid: exceeds range

            // Act
            var options = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);

            // Assert
            options.Should().NotBeNull();
            options.MaxConcurrency.Should().Be(1, "should use fallback default when MaxConcurrency is invalid");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(101)]
        [InlineData(500)]
        public async Task CreateFromConfiguration_WithOutOfRangeMaxConcurrency_ShouldUseFallbackDefault(int invalidConcurrency)
        {
            // Arrange
            var configService = _serviceProvider.GetRequiredService<IAppConfigService>();
            await CreateInvalidConfigurationFile(maxConcurrency: invalidConcurrency);

            // Act
            var options = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);

            // Assert
            options.MaxConcurrency.Should().Be(1, $"should use fallback default when MaxConcurrency ({invalidConcurrency}) is out of valid range");
        }

        [Fact]
        public async Task CreateFromConfiguration_ShouldPreserveOtherDefaultProperties()
        {
            // Arrange
            var configService = _serviceProvider.GetRequiredService<IAppConfigService>();
            await CreateValidConfigurationFile(maxConcurrency: 10);

            // Act
            var options = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);

            // Assert
            // Verify other properties maintain their expected defaults
            options.BatchSize.Should().Be(100);
            options.Recursive.Should().BeTrue();
            options.MaxDepth.Should().Be(0);
            options.ContinueOnError.Should().BeTrue();
            options.CreateBackups.Should().BeFalse();
            options.ProgressReportingInterval.Should().Be(1000);
            options.ForceGarbageCollection.Should().BeTrue();
        }

        [Fact]
        public async Task CreateFromConfiguration_WithConfigurationHotReload_ShouldReflectNewMaxConcurrency()
        {
            // Arrange
            var configService = _serviceProvider.GetRequiredService<IAppConfigService>();

            // Initial configuration
            await CreateValidConfigurationFile(maxConcurrency: 5);
            var initialOptions = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);
            initialOptions.MaxConcurrency.Should().Be(5);

            // Simulate hot-reload with new MaxConcurrency
            await CreateValidConfigurationFile(maxConcurrency: 15);
            await Task.Delay(100); // Allow time for file change detection

            // Act
            var updatedOptions = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);

            // Assert
            updatedOptions.MaxConcurrency.Should().Be(15, "should reflect hot-reloaded MaxConcurrency value");
        }

        [Fact]
        public void BulkProcessingOptions_MaxConcurrencyProperty_ShouldHaveValidationAttributes()
        {
            // Arrange & Act
            var property = typeof(BulkProcessingOptions).GetProperty(nameof(BulkProcessingOptions.MaxConcurrency));

            // Assert
            property.Should().NotBeNull();
            var rangeAttribute = property!.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
            rangeAttribute.Should().NotBeEmpty("MaxConcurrency should have Range validation attribute");
        }

        [Fact]
        public async Task CreateFromConfiguration_ShouldReturnNewInstanceEachTime()
        {
            // Arrange
            var configService = _serviceProvider.GetRequiredService<IAppConfigService>();
            await CreateValidConfigurationFile(maxConcurrency: 8);

            // Act
            var options1 = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);
            var options2 = await BulkProcessingOptions.CreateFromConfigurationAsync(configService);

            // Assert
            options1.Should().NotBeSameAs(options2, "should create new instance each time");
            options1.MaxConcurrency.Should().Be(options2.MaxConcurrency, "but both should have same configured values");
        }

        private async Task CreateValidConfigurationFile(int maxConcurrency)
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
            var mockFileSystem = (MockFileSystem)_serviceProvider.GetRequiredService<System.IO.Abstractions.IFileSystem>();

            var config = $$"""
            {
                "version": "2.0",
                "hashingAlgorithm": "CTPH",
                "matchConfidenceThreshold": 0.80,
                "renameConfidenceThreshold": 0.85,
                "fuzzyHashThreshold": 75,
                "matchingThresholds": {
                    "textBased": {
                        "matchConfidence": 0.80,
                        "renameConfidence": 0.85,
                        "fuzzyHashSimilarity": 75
                    },
                    "pgs": {
                        "matchConfidence": 0.70,
                        "renameConfidence": 0.75,
                        "fuzzyHashSimilarity": 65
                    },
                    "vobSub": {
                        "matchConfidence": 0.60,
                        "renameConfidence": 0.70,
                        "fuzzyHashSimilarity": 55
                    }
                },
                "maxConcurrency": {{maxConcurrency}},
                "filenamePatterns": {
                    "primaryPattern": "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$"
                },
                "filenameTemplate": "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
            }
            """;

            mockFileSystem.AddFile(configPath, config);
        }

        private async Task CreateInvalidConfigurationFile(int maxConcurrency)
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "episodeidentifier.config.json");
            var mockFileSystem = (MockFileSystem)_serviceProvider.GetRequiredService<System.IO.Abstractions.IFileSystem>();

            var config = $$"""
            {
                "version": "2.0",
                "hashingAlgorithm": "CTPH",
                "matchConfidenceThreshold": 0.80,
                "renameConfidenceThreshold": 0.85,
                "fuzzyHashThreshold": 75,
                "matchingThresholds": {
                    "textBased": {
                        "matchConfidence": 0.80,
                        "renameConfidence": 0.85,
                        "fuzzyHashSimilarity": 75
                    },
                    "pgs": {
                        "matchConfidence": 0.70,
                        "renameConfidence": 0.75,
                        "fuzzyHashSimilarity": 65
                    },
                    "vobSub": {
                        "matchConfidence": 0.60,
                        "renameConfidence": 0.70,
                        "fuzzyHashSimilarity": 55
                    }
                },
                "maxConcurrency": {{maxConcurrency}},
                "filenamePatterns": {
                    "primaryPattern": "^(?<SeriesName>.+?)\\sS(?<Season>\\d+)E(?<Episode>\\d+)(?:[\\s\\.\\-]+(?<EpisodeName>.+?))?$"
                },
                "filenameTemplate": "{SeriesName} - S{Season}E{Episode} - {EpisodeName}{FileExtension}"
            }
            """;

            mockFileSystem.AddFile(configPath, config);
        }
    }
}
