using System.Text.Json;
using Bot.Utils;
using Xunit;

namespace Bot.Tests;

public class CollectionMetadataTests
{
    internal static string Probe(int width = 1920, int height = 1080, string? codec = null, string colorTransfer = "bt709") =>
        JsonSerializer.Serialize(new
        {
            streams = new object[]
            {
                new { codec_type = "audio", codec_name = "aac" },
                new { codec_type = "video", codec_name = codec, width, height, color_transfer = colorTransfer },
            },
        });

    [Theory]
    [InlineData("h264", "H.264/AVC")]
    [InlineData("hevc", "H.265/HEVC")]
    [InlineData("av1", "AV1")]
    [InlineData("HEVC", "H.265/HEVC")]
    public void Codec_maps_known_ffprobe_names(string codecName, string expected) =>
        Assert.Equal(expected, CollectionMetadata.CodecFromProbe(Probe(codec: codecName)));

    [Fact]
    public void Codec_is_null_for_an_unknown_codec() =>
        Assert.Null(CollectionMetadata.CodecFromProbe(Probe(codec: "mjpeg")));

    [Fact]
    public void Codec_is_null_without_a_video_stream() =>
        Assert.Null(CollectionMetadata.CodecFromProbe("""{"streams": [{"codec_type": "audio", "codec_name": "aac"}]}"""));

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"streams": "nope"}""")]
    public void Probes_that_cannot_be_read_give_nothing(string metadata)
    {
        Assert.Null(CollectionMetadata.CodecFromProbe(metadata));
        Assert.Null(CollectionMetadata.QualityFromProbe(metadata));
    }

    [Theory]
    [InlineData(3840, 2160, "bt709", "4K")]
    [InlineData(3840, 1600, "smpte2084", "4K HDR")]
    [InlineData(1920, 800, "arib-std-b67", "1080p HDR")]
    [InlineData(2560, 1440, "bt709", "2K")]
    [InlineData(1280, 534, "bt709", "720p")]
    [InlineData(720, 480, "bt709", "480p")]
    public void Quality_comes_from_the_video_size_and_transfer(int width, int height, string transfer, string expected) =>
        Assert.Equal(expected, CollectionMetadata.QualityFromProbe(Probe(width, height, colorTransfer: transfer)));

    [Fact]
    public void Quality_is_null_below_480p() =>
        Assert.Null(CollectionMetadata.QualityFromProbe(Probe(640, 360)));

    [Theory]
    [InlineData("Dune.Part.Two.2024.2160p.UHD.HDR10.mkv", "4K HDR10")]
    [InlineData("Dune.Part.Two.2024.1080p.WEB-DL.mkv", "1080p")]
    [InlineData("Dune Part Two (FullHD).zip.001", "1080p")]
    [InlineData("Dune.Part.Two.2024.4K.DV.mkv", "4K DolbyVision")]
    [InlineData("Dune.Part.Two.2024.HDR.mkv", null)]
    public void Quality_from_a_filename_needs_a_resolution(string filename, string? expected) =>
        Assert.Equal(expected, CollectionMetadata.QualityFromFilename(filename));

    [Theory]
    [InlineData("spa,eng", "spa,eng")]
    [InlineData(" SPA , Eng ", "spa,eng")]
    [InlineData("spa,spa,eng", "spa,eng")]
    [InlineData("fra", "fre")]
    [InlineData("spa,und", "spa")]
    [InlineData("und", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Languages_are_normalized(string? value, string? expected) =>
        Assert.Equal(expected, CollectionMetadata.ValidateLanguages(value));

    [Fact]
    public void Unknown_languages_are_rejected()
    {
        var ex = Assert.Throws<InvalidLanguageCodeException>(() => CollectionMetadata.ValidateLanguages("spa,klingon"));
        Assert.Equal(["klingon"], ex.Codes);
        Assert.Equal("Unknown language code(s): klingon", ex.Message);
    }

    [Fact]
    public void The_vocabulary_offers_every_detectable_quality()
    {
        Assert.Contains("1080p", CollectionMetadata.Qualities);
        Assert.Contains("4K HDR10+", CollectionMetadata.Qualities);
        Assert.Contains("H.264/AVC", CollectionMetadata.Codecs);
        Assert.Equal("Español", CollectionMetadata.Languages["spa"]);
        Assert.DoesNotContain("und", CollectionMetadata.Languages.Keys);
    }
}
