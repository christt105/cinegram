using System.Text.Json;

namespace Cinegram.Utils;

/// <summary>Thrown for audio or subtitle languages outside <see cref="CollectionMetadata.Languages"/>.</summary>
public class InvalidLanguageCodeException(IReadOnlyList<string> codes)
    : ArgumentException($"Unknown language code(s): {string.Join(", ", codes)}")
{
    public IReadOnlyList<string> Codes { get; } = codes;
}

/// <summary>
/// The fixed vocabulary a collection's quality, codec and languages are restricted to, and how
/// they are read from a filename or from ffprobe's JSON.
/// </summary>
public static class CollectionMetadata
{
    private static readonly (string Label, string[] Tokens)[] Resolutions =
    [
        ("4K", ["2160p", "4k", "uhd"]),
        ("2K", ["1440p", "2k"]),
        ("1080p", ["1080p", "fullhd", "fhd"]),
        ("720p", ["720p"]),
        ("480p", ["480p"]),
        ("360p", ["360p"]),
        ("240p", ["240p"]),
    ];

    private static readonly (string Label, string[] Tokens)[] HdrFormats =
    [
        ("HDR10+", ["hdr10+"]),
        ("HDR10", ["hdr10"]),
        ("DolbyVision", ["dolbyvision", "dv"]),
        ("HLG", ["hlg"]),
        ("HDR", ["hdr"]),
    ];

    /// <summary>ffprobe <c>codec_name</c> to display label.</summary>
    private static readonly Dictionary<string, string> CodecLabels = new()
    {
        ["h264"] = "H.264/AVC",
        ["hevc"] = "H.265/HEVC",
        ["av1"] = "AV1",
        ["vp9"] = "VP9",
        ["vp8"] = "VP8",
        ["mpeg4"] = "MPEG-4",
        ["mpeg2video"] = "MPEG-2",
        ["vc1"] = "VC-1",
        ["theora"] = "Theora",
    };

    /// <summary>ISO 639-2/B code to display name.</summary>
    public static readonly IReadOnlyDictionary<string, string> Languages = new Dictionary<string, string>
    {
        ["spa"] = "Español",
        ["eng"] = "English",
        ["jpn"] = "Japonés",
        ["kor"] = "Coreano",
        ["chi"] = "Chino",
        ["fre"] = "Francés",
        ["ger"] = "Alemán",
        ["ita"] = "Italiano",
        ["por"] = "Portugués",
        ["rus"] = "Ruso",
        ["ara"] = "Árabe",
        ["cat"] = "Catalán",
        ["lat"] = "Latín",
        ["pol"] = "Polaco",
        ["tur"] = "Turco",
        ["hin"] = "Hindi",
        ["vie"] = "Vietnamita",
        ["tha"] = "Tailandés",
        ["ind"] = "Indonesio",
        ["dut"] = "Neerlandés",
        ["swe"] = "Sueco",
        ["nor"] = "Noruego",
        ["dan"] = "Danés",
        ["fin"] = "Finlandés",
        ["gre"] = "Griego",
        ["heb"] = "Hebreo",
        ["ukr"] = "Ucraniano",
        ["hun"] = "Húngaro",
        ["cze"] = "Checo",
        ["rum"] = "Rumano",
        ["glg"] = "Gallego",
        ["baq"] = "Euskera",
        ["hrv"] = "Croata",
        ["srp"] = "Serbio",
        ["slv"] = "Esloveno",
        ["slo"] = "Eslovaco",
        ["bul"] = "Búlgaro",
        ["est"] = "Estonio",
        ["lav"] = "Letón",
        ["lit"] = "Lituano",
        ["ice"] = "Islandés",
        ["per"] = "Persa",
        ["may"] = "Malayo",
        ["fil"] = "Filipino",
        ["tam"] = "Tamil",
        ["tel"] = "Telugu",
    };

    /// <summary>ISO 639-2/T codes ffprobe and mkvmerge sometimes tag instead of the /B ones.</summary>
    private static readonly Dictionary<string, string> LanguageAliases = new()
    {
        ["zho"] = "chi",
        ["fra"] = "fre",
        ["deu"] = "ger",
        ["nld"] = "dut",
        ["ell"] = "gre",
        ["ces"] = "cze",
        ["ron"] = "rum",
        ["eus"] = "baq",
        ["slk"] = "slo",
        ["isl"] = "ice",
        ["fas"] = "per",
        ["msa"] = "may",
        ["nob"] = "nor",
        ["nno"] = "nor",
        ["tgl"] = "fil",
    };

    /// <summary>Codes that carry no usable language, dropped rather than rejected.</summary>
    private static readonly HashSet<string> NoLanguageCodes = ["und", "mis", "mul", "zxx"];

    /// <summary>Every quality the edit form offers, generated so it never drifts from detection.</summary>
    public static readonly IReadOnlyList<string> Qualities = Resolutions
        .SelectMany(r => HdrFormats.Select(h => $"{r.Label} {h.Label}").Prepend(r.Label))
        .ToList();

    public static readonly IReadOnlyList<string> Codecs = CodecLabels.Values.Distinct().ToList();

    /// <summary>Resolution plus HDR format when the name mentions both, e.g. "4K HDR10"; null
    /// without a recognisable resolution.</summary>
    public static string? QualityFromFilename(string filename)
    {
        var name = filename.ToLowerInvariant();
        var resolution = Resolutions.FirstOrDefault(r => r.Tokens.Any(name.Contains)).Label;
        if (resolution is null) return null;

        var hdr = HdrFormats.FirstOrDefault(h => h.Tokens.Any(name.Contains)).Label;
        return hdr is null ? resolution : $"{resolution} {hdr}";
    }

    /// <summary>From the first video stream's size and transfer function; null when the JSON
    /// can't be read or the stream is below 480p.</summary>
    public static string? QualityFromProbe(string technicalMetadata)
    {
        var video = VideoStream(technicalMetadata);
        if (video is not { } stream) return null;

        var width = Number(stream, "width");
        var height = Number(stream, "height");
        if (width is not > 0 || height is not > 0) return null;

        var resolution =
            width >= 3840 || height >= 2160 ? "4K" :
            width >= 2560 || height >= 1440 ? "2K" :
            width >= 1920 || height >= 1080 ? "1080p" :
            width >= 1280 || height >= 720 ? "720p" :
            width >= 848 || height >= 480 ? "480p" :
            null;
        if (resolution is null) return null;

        var transfer = stream.TryGetProperty("color_transfer", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;
        return transfer is "smpte2084" or "arib-std-b67" ? $"{resolution} HDR" : resolution;
    }

    public static string? CodecFromProbe(string technicalMetadata)
    {
        if (VideoStream(technicalMetadata) is not { } stream) return null;
        return stream.TryGetProperty("codec_name", out var codec) && codec.ValueKind == JsonValueKind.String
            && codec.GetString() is { Length: > 0 } name
            && CodecLabels.TryGetValue(name.ToLowerInvariant(), out var label)
                ? label
                : null;
    }

    /// <summary>
    /// Cleans a comma separated language list: trims, lowercases, resolves aliases, drops
    /// no-language codes and duplicates. Null when nothing is left.
    /// </summary>
    /// <exception cref="InvalidLanguageCodeException">Some code is outside the vocabulary.</exception>
    public static string? ValidateLanguages(string? value)
    {
        var (normalized, invalid) = NormalizeLanguages(value);
        if (invalid.Count > 0) throw new InvalidLanguageCodeException(invalid);
        return normalized;
    }

    /// <summary>Like <see cref="ValidateLanguages"/>, but drops codes outside the vocabulary
    /// instead of rejecting the list. For languages read from a file's own tags.</summary>
    public static string? KnownLanguages(string? value) => NormalizeLanguages(value).Normalized;

    private static (string? Normalized, List<string> Invalid) NormalizeLanguages(string? value)
    {
        var normalized = new List<string>();
        var invalid = new List<string>();
        if (value is null) return (null, invalid);

        foreach (var raw in value.Split(','))
        {
            var code = raw.Trim().ToLowerInvariant();
            if (code.Length == 0 || NoLanguageCodes.Contains(code)) continue;

            var resolved = LanguageAliases.GetValueOrDefault(code, code);
            if (!Languages.ContainsKey(resolved))
                invalid.Add(code);
            else if (!normalized.Contains(resolved))
                normalized.Add(resolved);
        }

        return (normalized.Count > 0 ? string.Join(",", normalized) : null, invalid);
    }

    private static JsonElement? VideoStream(string technicalMetadata)
    {
        try
        {
            using var doc = JsonDocument.Parse(technicalMetadata);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var stream in streams.EnumerateArray())
                if (stream.ValueKind == JsonValueKind.Object
                    && stream.TryGetProperty("codec_type", out var type) && type.ValueKind == JsonValueKind.String
                    && type.GetString() == "video")
                    return stream.Clone();
            return null;
        }
        catch (JsonException ex)
        {
            Log.Error("Failed to parse technical metadata", ex);
            return null;
        }
    }

    private static double? Number(JsonElement e, string property) =>
        e.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
