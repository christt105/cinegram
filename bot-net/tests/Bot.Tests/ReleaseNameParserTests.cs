using Bot.Utils;
using Xunit;

namespace Bot.Tests;

public class ReleaseNameParserTests
{
    public static TheoryData<string, ParsedReleaseName> Cases => new()
    {
        { @"Show S01E02.mkv", new ParsedReleaseName(null, @"Show", "tv", 1, 2, null) },
        { @"Show 1x08.mkv", new ParsedReleaseName(null, @"Show", "tv", 1, 8, null) },
        { @"Show S01-08.mkv", new ParsedReleaseName(null, @"Show", "tv", 1, 8, null) },
        { @"Show S01_08.mkv", new ParsedReleaseName(null, @"Show", "tv", 1, 8, null) },
        { @"Show - 08.mkv", new ParsedReleaseName(null, @"Show", "tv", 1, 8, null) },
        { @"Show - Season 2.mkv", new ParsedReleaseName(null, @"Show", "tv", 2, null, null) },
        { @"Show S01E01E02.mkv", new ParsedReleaseName(null, @"Show", "tv", 1, 1, null) },
        { @"Show S01E01-E03.mkv", new ParsedReleaseName(null, @"Show", "tv", 1, 1, null) },
        { @"1917 (2019).mkv", new ParsedReleaseName(null, @"1917", "movie", null, null, 2019) },
        { @"2012.mkv", new ParsedReleaseName(null, @"2012", "movie", null, null, null) },
        { @"One Piece 1085.mkv", new ParsedReleaseName(null, @"One Piece 1085", "tv", null, null, null) },
        { @"Pokémon 2: El poder de uno (1999).zip.001", new ParsedReleaseName(null, @"Pokémon 2: El poder de uno", "movie", null, null, 1999) },
        { @"Jurassic World: Dominion [Versión Extendida] (1080p).zip.001", new ParsedReleaseName(null, @"Jurassic World: Dominion", "movie", null, null, null) },
        { @"Hayao Miyazaki and the Heron (2024) [tmdbid-1292585].zip.001", new ParsedReleaseName(1292585, @"Hayao Miyazaki and the Heron", "movie", null, null, 2024) },
        { @"Hijos de la anarquía - S05E10 - Crucificado.mkv.zip.001", new ParsedReleaseName(null, @"Hijos de la anarquía", "tv", 5, 10, null) },
        { @"Yellowstone 5x08 Un cuchillo y ninguna moneda.mkv", new ParsedReleaseName(null, @"Yellowstone", "tv", 5, 8, null) },
        { @"One Piece 1x125.mkv", new ParsedReleaseName(null, @"One Piece", "tv", 1, 125, null) },
        { @"Naruto Shippuden - S07E02 - [tmdbid-31910].avi", new ParsedReleaseName(31910, @"Naruto Shippuden", "tv", 7, 2, null) },
        { @"Vikingos - Temporada 3 (Blu-ray 1080p).zip.006", new ParsedReleaseName(null, @"Vikingos", "tv", 3, null, null) },
        { @"01x01 Mi otra yo.mp4", new ParsedReleaseName(null, @"Mi otra yo", "tv", 1, 1, null) },
        { @"Dorohedoro - 01.mp4", new ParsedReleaseName(null, @"Dorohedoro", "tv", 1, 1, null) },
        { @"Dorohedoro S2 - 01.mp4", new ParsedReleaseName(null, @"Dorohedoro", "tv", 2, 1, null) },
        { @"Show.Name.S01E02.1080p.WEB-DL.x264-GROUP.mkv", new ParsedReleaseName(null, @"Show Name", "tv", 1, 2, null) },
        { @"The.Matrix.1999.1080p.BluRay.x264-GROUP.mkv", new ParsedReleaseName(null, @"The Matrix", "movie", null, null, 1999) },
        { @"Parasite.2019.720p.WEB-DL.x264-GROUP.mkv", new ParsedReleaseName(null, @"Parasite", "movie", null, null, 2019) },
        { @"Spider-Man", new ParsedReleaseName(null, @"Spider-Man", "movie", null, null, null) },
        { @"Arrietty_y_el_mundo_de_los_diminutos_2010_tmdbid_51739_LasCositas.001", new ParsedReleaseName(51739, @"Arrietty y el mundo de los diminutos", "movie", null, null, 2010) },
        { @"El viento se levanta (2013) [tmdbid-149870] - LasCositas.zip.001", new ParsedReleaseName(149870, @"El viento se levanta", "movie", null, null, 2013) },
        { @"La Cena (2025).zip.001", new ParsedReleaseName(null, @"La Cena", "movie", null, null, 2025) },
        { @"Fullmetal Alchemist - Brotherhood 1x01.mkv", new ParsedReleaseName(null, @"Fullmetal Alchemist Brotherhood", "tv", 1, 1, null) },
        { @"Wall•E (1080p).zip.001", new ParsedReleaseName(null, @"Wall E", "movie", null, null, null) },
        { @"La Princesa Mononoke (1997) - Las Cositas.zip.001", new ParsedReleaseName(null, @"La Princesa Mononoke", "movie", null, null, 1997) },
        { @"Spider-Man.(2002).(Spanish.English.Subs).Micro4K.2160p.HDR10.x265-AC3.by.CHINAKO.(hispashare.org).mkv", new ParsedReleaseName(null, @"Spider-Man", "movie", null, null, 2002) },
        { @"Lightyear.(2022).(Spanish.English.Subs).WEB-DL.m1080p.x265-AC3.by.yamil.mkv", new ParsedReleaseName(null, @"Lightyear", "movie", null, null, 2022) },
        { @"Toy.Story.4.(2019).(Spanish.English.Subs).BDRip.2160p.x265.10Bit.HDR-EAC3.AC3.by.enjoy.(hispashare.org).mkv", new ParsedReleaseName(null, @"Toy Story 4", "movie", null, null, 2019) },
        { @"Sterling.Point.1x04.Llevame.a.los.acantilados.(Spanish.English.Subs).WEBRip.1080p.x264-AC3.by.Mony2007.mkv", new ParsedReleaseName(null, @"Sterling Point", "tv", 1, 4, null) },
        { @"Stand.by.Me.1986.1080p.BluRay.x264-GROUP.mkv", new ParsedReleaseName(null, @"Stand by Me", "movie", null, null, 1986) },
        { @"Spider-Man No Way Home Ext. (1080p).001", new ParsedReleaseName(null, @"Spider-Man No Way Home", "movie", null, null, null) },
        { @"The.Surfer.(2024).(Spanish.English.Subs).BDRip.1080p.x264-EAC3.by.aurora45-xusman.(nocturniap2p).mkv", new ParsedReleaseName(null, @"The Surfer", "movie", null, null, 2024) },
        { @"Shelter.El.protector.(2026).(Spanish.English.Subs).BDRip.1080p.x264-EAC3.by.diavliyo-xusman.(nocturniap2p).mkv", new ParsedReleaseName(null, @"Shelter El protector", "movie", null, null, 2026) },
        { @"Altas.capacidades.(2026).(Spanish).WEB-DL.1080p.x264-EAC3.by.xusman.(nocturniap2p).mkv", new ParsedReleaseName(null, @"Altas capacidades", "movie", null, null, 2026) },
        { @"Stand.By.Me.Doraemon.2.(2020).(Spanish).WEBRip.1080p.x265-AC3.by.s1d3sh0w.(hispashare.org).mkv", new ParsedReleaseName(null, @"Stand By Me Doraemon 2", "movie", null, null, 2020) },
        { @"Elysium.(2013).(Spanish.English.Subs).BDrip.1080p.x265-AC3.by.SparroW.mkv", new ParsedReleaseName(null, @"Elysium", "movie", null, null, 2013) },
        { @"Enola.Holmes.(2020).(Spanish.English.Subs).NF.WEB-DL.1080p.x264-EAC3.mkv", new ParsedReleaseName(null, @"Enola Holmes", "movie", null, null, 2020) },
        { @"La.casa.de.la.pradera.1x04 .Apreciemos.la.vida. (Spanish.English .Subs).WEBRip.1080p.x265-EAC3.by .piter332.(hispashare.org).mkv", new ParsedReleaseName(null, @"La casa de la pradera", "tv", 1, 4, null) },
        { @"Spider-Noir.1x01.Pase.a.mi.despacho.BN.(Spanish.English.Subs).WEBRip.1080p.x265-EAC3.EAC3.Atmos.by.Legan.mkv", new ParsedReleaseName(null, @"Spider-Noir", "tv", 1, 1, null) },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Parse_ExtractsTitleYearAndEpisode(string filename, ParsedReleaseName expected) =>
        Assert.Equal(expected, ReleaseNameParser.Parse(filename));

    /// <summary>Absolute anime numbering is only hinted as tv for 4+ digit trailing numbers; a
    /// 3-digit threshold would also turn titles like "Fahrenheit 451" into series.</summary>
    [Fact]
    public void Parse_ShortAbsoluteEpisodeNumberStaysAMovie() =>
        Assert.Equal("movie", ReleaseNameParser.Parse("Naruto 220.mkv").Type);
}
