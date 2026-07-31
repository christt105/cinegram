using Bot.Utils;
using Xunit;

namespace Bot.Tests;

public class ArchiveDetectorTests
{
    [Fact]
    public void FindEntry_PicksTheFirstRarVolume()
    {
        string[] files =
        [
            "/tmp/Movie.part3.rar",
            "/tmp/Movie.part1.rar",
            "/tmp/Movie.part2.rar"
        ];

        Assert.Equal("/tmp/Movie.part1.rar", ArchiveDetector.FindEntry(files));
    }

    [Fact]
    public void FindEntry_PicksTheFirstSplitVolume()
    {
        string[] files =
        [
            "/tmp/Movie.zip.002",
            "/tmp/Movie.zip.001",
            "/tmp/Movie.zip.003"
        ];

        Assert.Equal("/tmp/Movie.zip.001", ArchiveDetector.FindEntry(files));
    }

    [Fact]
    public void FindEntry_AcceptsVolumesWhoseExtensionWasSanitised()
    {
        string[] files =
        [
            "/tmp/Kimetsu_no_Yaiba_2025_imdbid_tt32820897_7z.001",
            "/tmp/Kimetsu_no_Yaiba_2025_imdbid_tt32820897_7z.002",
            "/tmp/Kimetsu_no_Yaiba_2025_imdbid_tt32820897_7z.003"
        ];

        Assert.Equal("/tmp/Kimetsu_no_Yaiba_2025_imdbid_tt32820897_7z.001", ArchiveDetector.FindEntry(files));
    }

    [Theory]
    [InlineData("/tmp/Movie.rar")]
    [InlineData("/tmp/Movie.zip")]
    [InlineData("/tmp/Movie.7z")]
    [InlineData("/tmp/Movie.ZIP")]
    public void FindEntry_AcceptsSelfContainedArchives(string path)
    {
        Assert.Equal(path, ArchiveDetector.FindEntry([path]));
    }

    [Fact]
    public void FindEntry_PrefersTheRarHeadOverItsTrailingVolumes()
    {
        string[] files = ["/tmp/Movie.r00", "/tmp/Movie.r01", "/tmp/Movie.rar"];

        Assert.Equal("/tmp/Movie.rar", ArchiveDetector.FindEntry(files));
    }

    [Fact]
    public void FindEntry_IgnoresSetsThatDoNotStartAtTheFirstVolume()
    {
        string[] files = ["/tmp/Movie.zip.002", "/tmp/Movie.zip.003"];

        Assert.Null(ArchiveDetector.FindEntry(files));
    }

    [Fact]
    public void FindEntry_IgnoresPlainVideoFiles()
    {
        Assert.Null(ArchiveDetector.FindEntry(["/tmp/Movie (2025) - [1080p].mkv"]));
    }

    [Fact]
    public void CanProduceVideo_AcceptsAVideoOnItsOwn()
    {
        Assert.True(ArchiveDetector.CanProduceVideo(["/tmp/Movie (2025).mkv"]));
    }

    [Fact]
    public void CanProduceVideo_AcceptsARecognisedArchive()
    {
        Assert.True(ArchiveDetector.CanProduceVideo(["/tmp/Movie_7z.001", "/tmp/Movie_7z.002"]));
    }

    [Fact]
    public void CanProduceVideo_RejectsAPayloadWeCouldNotUnpack()
    {
        Assert.False(ArchiveDetector.CanProduceVideo(["/tmp/cover.jpg", "/tmp/notes.txt"]));
    }
}
