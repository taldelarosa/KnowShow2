using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using System.Text;

namespace EpisodeIdentifier.Tests.Contract;

public class SubtitleFormatHandlerContractTests
{
    [Theory]
    [InlineData(typeof(SrtFormatHandler), SubtitleFormat.SRT)]
    [InlineData(typeof(AssFormatHandler), SubtitleFormat.ASS)]
    [InlineData(typeof(VttFormatHandler), SubtitleFormat.VTT)]
    public void SupportedFormat_ReturnsCorrectFormatForHandler(Type handlerType, SubtitleFormat expectedFormat)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;

        // Act
        var supportedFormat = handler.SupportedFormat;

        // Assert
        supportedFormat.Should().Be(expectedFormat);
    }

    [Theory]
    [InlineData(typeof(SrtFormatHandler), SubtitleFormat.SRT, true)]
    [InlineData(typeof(SrtFormatHandler), SubtitleFormat.ASS, false)]
    [InlineData(typeof(SrtFormatHandler), SubtitleFormat.VTT, false)]
    [InlineData(typeof(AssFormatHandler), SubtitleFormat.ASS, true)]
    [InlineData(typeof(AssFormatHandler), SubtitleFormat.SRT, false)]
    [InlineData(typeof(AssFormatHandler), SubtitleFormat.VTT, false)]
    [InlineData(typeof(VttFormatHandler), SubtitleFormat.VTT, true)]
    [InlineData(typeof(VttFormatHandler), SubtitleFormat.SRT, false)]
    [InlineData(typeof(VttFormatHandler), SubtitleFormat.ASS, false)]
    public void CanHandle_ReturnsTrueForSupportedFormatOnly(Type handlerType, SubtitleFormat format, bool expectedResult)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;

        // Act
        var canHandle = handler.CanHandle(format);

        // Assert
        canHandle.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(typeof(SrtFormatHandler), SubtitleFormat.SRT)]
    [InlineData(typeof(AssFormatHandler), SubtitleFormat.ASS)]
    [InlineData(typeof(VttFormatHandler), SubtitleFormat.VTT)]
    public void CanHandle_ConsistentResultsAcrossMultipleCalls(Type handlerType, SubtitleFormat format)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;

        // Act
        var result1 = handler.CanHandle(format);
        var result2 = handler.CanHandle(format);
        var result3 = handler.CanHandle(format);

        // Assert
        result1.Should().Be(result2);
        result2.Should().Be(result3);
        result1.Should().BeTrue(); // Should handle its own format
    }

    [Fact]
    public async Task SrtFormatHandler_ParseSubtitleTextAsync_WithValidSrtContent_ReturnsCleanDialogueText()
    {
        // Arrange
        var handler = new SrtFormatHandler();
        var srtContent = @"1
00:00:01,000 --> 00:00:03,000
Hello world!

2
00:00:04,000 --> 00:00:06,000
<i>This is italic text</i>

3
00:00:07,000 --> 00:00:09,000
Multiple lines
in one subtitle
";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(srtContent));

        // Act
        var result = await handler.ParseSubtitleTextAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Hello world!");
        result.Should().Contain("This is italic text");
        result.Should().Contain("Multiple lines");
        result.Should().NotContain("00:00:01,000");
        result.Should().NotContain("<i>");
        result.Should().NotContain("</i>");
    }

    [Fact]
    public async Task AssFormatHandler_ParseSubtitleTextAsync_WithValidAssContent_ReturnsDialogueEventsOnly()
    {
        // Arrange
        var handler = new AssFormatHandler();
        var assContent = @"[Script Info]
Title: Test
ScriptType: v4.00+

[V4+ Styles]
Format: Name, Fontname, Fontsize
Style: Default,Arial,20

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:01.00,0:00:03.00,Default,,0,0,0,,Hello from ASS!
Comment: 0,0:00:02.00,0:00:04.00,Default,,0,0,0,,This is a comment
Dialogue: 0,0:00:05.00,0:00:07.00,Default,,0,0,0,,{\i1}Italic text{\i0}
";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(assContent));

        // Act
        var result = await handler.ParseSubtitleTextAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Hello from ASS!");
        result.Should().Contain("Italic text");
        result.Should().NotContain("This is a comment");
        result.Should().NotContain("Format:");
        result.Should().NotContain("{\\i1}");
        result.Should().NotContain("{\\i0}");
    }

    [Fact]
    public async Task VttFormatHandler_ParseSubtitleTextAsync_WithValidVttContent_ReturnsCueTextOnly()
    {
        // Arrange
        var handler = new VttFormatHandler();
        var vttContent = @"WEBVTT

NOTE This is a note

00:00:01.000 --> 00:00:03.000
Hello from WebVTT!

00:00:04.000 --> 00:00:06.000
<v Speaker>Multiple speakers</v>

00:00:07.000 --> 00:00:09.000
Text with <c.className>styling</c>
";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(vttContent));

        // Act
        var result = await handler.ParseSubtitleTextAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Hello from WebVTT!");
        result.Should().Contain("Multiple speakers");
        result.Should().Contain("Text with styling");
        result.Should().NotContain("WEBVTT");
        result.Should().NotContain("NOTE");
        result.Should().NotContain("<v Speaker>");
        result.Should().NotContain("<c.className>");
    }

    [Theory]
    [InlineData(typeof(SrtFormatHandler))]
    [InlineData(typeof(AssFormatHandler))]
    [InlineData(typeof(VttFormatHandler))]
    public async Task ParseSubtitleTextAsync_WithEmptyContent_ReturnsEmptyString(Type handlerType)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;
        var emptyStream = new MemoryStream();

        // Act
        var result = await handler.ParseSubtitleTextAsync(emptyStream);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(SrtFormatHandler))]
    [InlineData(typeof(AssFormatHandler))]
    [InlineData(typeof(VttFormatHandler))]
    public async Task ParseSubtitleTextAsync_WithNullStream_ThrowsArgumentNullException(Type handlerType)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;

        // Act & Assert
        await FluentActions.Invoking(() => handler.ParseSubtitleTextAsync(null!, "UTF-8"))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(typeof(SrtFormatHandler))]
    [InlineData(typeof(AssFormatHandler))]
    [InlineData(typeof(VttFormatHandler))]
    public async Task ParseSubtitleTextAsync_WithUnsupportedEncoding_ThrowsNotSupportedException(Type handlerType)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        // Act & Assert
        await FluentActions.Invoking(() => handler.ParseSubtitleTextAsync(stream, "INVALID-ENCODING"))
            .Should().ThrowAsync<NotSupportedException>();
    }

    [Theory]
    [InlineData(typeof(SrtFormatHandler))]
    [InlineData(typeof(AssFormatHandler))]
    [InlineData(typeof(VttFormatHandler))]
    public async Task ParseSubtitleTextAsync_WithMalformedData_ThrowsInvalidDataException(Type handlerType)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;
        var malformedContent = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC }; // Invalid UTF-8
        var stream = new MemoryStream(malformedContent);

        // Act & Assert
        await FluentActions.Invoking(() => handler.ParseSubtitleTextAsync(stream))
            .Should().ThrowAsync<InvalidDataException>();
    }
}
