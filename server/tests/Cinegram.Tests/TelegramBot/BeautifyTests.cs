using System.Text.Json;
using Xunit;
using Cinegram.Data;
using Cinegram.TelegramBot;

namespace Cinegram.Tests.TelegramBot;

public class BeautifyTests
{
    private const int TelegramMessageLimit = 4096;

    private static string LongTechnicalMetadata()
    {
        var streams = new List<object>
        {
            new { index = 0, codec_type = "video", codec_name = "hevc", width = 3840, height = 2160 },
        };
        for (var i = 1; i <= 40; i++)
        {
            var type = i <= 10 ? "audio" : "subtitle";
            var language = i % 2 == 0 ? "spa" : "jpn";
            streams.Add(new
            {
                index = i,
                codec_type = type,
                codec_name = type == "audio" ? "flac" : "ass",
                tags = new { language, title = $"Track {i} with a long descriptive title", handler_name = "SubtitleHandler" },
                disposition = new { @default = 0, forced = 0, hearing_impaired = 0, visual_impaired = 0 },
            });
        }

        return JsonSerializer.Serialize(new
        {
            streams,
            format = new { filename = "/data/temp/Akira.1988.2160p.mkv", size = "64424509440", bit_rate = "48000000" },
        });
    }

    [Fact]
    public void A_file_card_shows_the_technical_summary_instead_of_the_raw_metadata()
    {
        var metadata = LongTechnicalMetadata();
        Assert.True(metadata.Length > TelegramMessageLimit);
        var collection = new Collection { Id = 83, Name = "Akira (1988)", Quality = "4K", TechnicalMetadata = metadata };
        var file = new TelegramFile { Id = 7, Filename = "Akira.1988.2160p.mkv", Filesize = 64424509440, CollectionId = 83 };

        var text = Beautify.FormatFile(file, collection);

        Assert.True(text.Length < TelegramMessageLimit);
        Assert.DoesNotContain("{", text);
        Assert.Contains("<b>Technical:</b> 60.00 GB  |  48.0 Mbps  |  HEVC  |  Aud: jpn,spa  |  Sub: jpn,spa", text);
        Assert.Contains("<b>Collection:</b> Akira (1988) (ID 83)", text);
    }

    [Fact]
    public void A_file_card_escapes_the_names_it_shows_as_html()
    {
        var collection = new Collection { Id = 1, Name = "Fast & Furious <Extended>" };
        var file = new TelegramFile { Id = 2, Filename = "Fast & Furious.mkv", CollectionId = 1 };

        var text = Beautify.FormatFile(file, collection);

        Assert.Contains("<code>Fast &amp; Furious.mkv</code>", text);
        Assert.Contains("Fast &amp; Furious &lt;Extended&gt; (ID 1)", text);
    }

    [Fact]
    public void A_file_card_without_metadata_or_collection_leaves_those_lines_out()
    {
        var file = new TelegramFile { Id = 3, Filename = "orphan.mkv", CollectionId = 9 };

        Assert.DoesNotContain("Technical", Beautify.FormatFile(file, new Collection { Id = 9, Name = "Plain" }));
        Assert.DoesNotContain("Collection", Beautify.FormatFile(file, null));
    }
}
