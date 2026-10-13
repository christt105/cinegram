using Cinegram.Utils;
using Xunit;

namespace Cinegram.Tests;

public class MediaProbeTests
{
    private const string SampleJson = """
    {
      "streams": [
        { "codec_type": "video", "width": 1920, "height": 1080 },
        { "codec_type": "audio", "tags": { "language": "jpn" } },
        { "codec_type": "audio", "tags": { "language": "eng" } },
        { "codec_type": "audio", "tags": { "language": "eng" } },
        { "codec_type": "subtitle", "tags": { "LANGUAGE": "spa" } },
        { "codec_type": "subtitle" }
      ]
    }
    """;

    [Fact]
    public void Summarize_JoinsDistinctAudioLanguages()
    {
        var summary = MediaProbe.Summarize(SampleJson);

        Assert.Equal("jpn,eng", summary.AudioLanguages);
    }

    [Fact]
    public void Summarize_ReadsUppercaseLanguageTagAndSkipsStreamsWithNone()
    {
        var summary = MediaProbe.Summarize(SampleJson);

        Assert.Equal("spa", summary.SubtitleLanguages);
    }

    [Fact]
    public void Summarize_ReturnsAllNullsWhenThereAreNoStreams()
    {
        var summary = MediaProbe.Summarize("{ \"format\": {} }");

        Assert.Null(summary.AudioLanguages);
        Assert.Null(summary.SubtitleLanguages);
    }

    [Fact]
    public void Summarize_NormalizesAliasesAndDropsLanguagesOutsideTheVocabulary()
    {
        var summary = MediaProbe.Summarize("""
        {
          "streams": [
            { "codec_type": "audio", "tags": { "language": "spa" } },
            { "codec_type": "subtitle", "tags": { "language": "nob" } },
            { "codec_type": "subtitle", "tags": { "language": "fra" } },
            { "codec_type": "subtitle", "tags": { "language": "klingon" } }
          ]
        }
        """);

        Assert.Equal("spa", summary.AudioLanguages);
        Assert.Equal("nor,fre", summary.SubtitleLanguages);
    }
}
