using Xunit;
using FluentAssertions;
using EpisodeIdentifier.Core.Services;
using EpisodeIdentifier.Core.Models;
using EpisodeIdentifier.Core.Interfaces;
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
    [InlineData(typeof(SrtFormatHandler), "1\n00:00:01,000 --> 00:00:04,000\nHello World", true)]
    [InlineData(typeof(SrtFormatHandler), "[V4+ Styles]\nTitle: Test", false)]
    [InlineData(typeof(SrtFormatHandler), "WEBVTT\n\n00:00:01.000 --> 00:00:04.000", false)]
    [InlineData(typeof(AssFormatHandler), "[V4+ Styles]\nTitle: Test", true)]
    [InlineData(typeof(AssFormatHandler), "1\n00:00:01,000 --> 00:00:04,000\nHello World", false)]
    [InlineData(typeof(AssFormatHandler), "WEBVTT\n\n00:00:01.000 --> 00:00:04.000", false)]
    [InlineData(typeof(VttFormatHandler), "WEBVTT\n\n00:00:01.000 --> 00:00:04.000", true)]
    [InlineData(typeof(VttFormatHandler), "1\n00:00:01,000 --> 00:00:04,000\nHello World", false)]
    [InlineData(typeof(VttFormatHandler), "[V4+ Styles]\nTitle: Test", false)]
    public void CanHandle_ReturnsTrueForSupportedFormatOnly(Type handlerType, string content, bool expectedResult)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;

        // Act
        var canHandle = handler.CanHandle(content);

        // Assert
        canHandle.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(typeof(SrtFormatHandler), "1\n00:00:01,000 --> 00:00:04,000\nHello World")]
    [InlineData(typeof(AssFormatHandler), "[V4+ Styles]\nTitle: Test")]
    [InlineData(typeof(VttFormatHandler), "WEBVTT\n\n00:00:01.000 --> 00:00:04.000")]
    public void CanHandle_ConsistentResultsAcrossMultipleCalls(Type handlerType, string content)
    {
        // Arrange
        var handler = (ISubtitleFormatHandler)Activator.CreateInstance(handlerType)!;

        // Act
        var result1 = handler.CanHandle(content);
        var result2 = handler.CanHandle(content);
        var result3 = handler.CanHandle(content);

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
        result.IsSuccessful.Should().BeTrue();
        result.Entries.Should().NotBeEmpty();
        
        var allText = string.Join(" ", result.Entries.Select(e => e.Text));
        allText.Should().Contain("Hello world!");
        allText.Should().Contain("This is italic text");
        allText.Should().Contain("Multiple lines");
        allText.Should().NotContain("00:00:01,000");
        allText.Should().NotContain("<i>");
        allText.Should().NotContain("</i>");
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
        result.IsSuccessful.Should().BeTrue();
        result.Entries.Should().NotBeEmpty();
        
        var allText = string.Join(" ", result.Entries.Select(e => e.Text));
        allText.Should().Contain("Hello from ASS!");
        allText.Should().Contain("Italic text");
        allText.Should().NotContain("This is a comment");
        allText.Should().NotContain("Format:");
        allText.Should().NotContain("{\\i1}");
        allText.Should().NotContain("{\\i0}");
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
        result.IsSuccessful.Should().BeTrue();
        result.Entries.Should().NotBeEmpty();
        
        var allText = string.Join(" ", result.Entries.Select(e => e.Text));
        allText.Should().Contain("Hello from WebVTT!");
        allText.Should().Contain("Multiple speakers");
        allText.Should().Contain("Text with styling");
        allText.Should().NotContain("WEBVTT");
        allText.Should().NotContain("NOTE");
        allText.Should().NotContain("<v Speaker>");
        allText.Should().NotContain("<c.className>");
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
        result.Entries.Should().BeEmpty();
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
