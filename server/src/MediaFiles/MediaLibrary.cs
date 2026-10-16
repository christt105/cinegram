namespace Cinegram.MediaFiles;

/// <summary>
/// Recognises video files and walks them in a directory.
/// </summary>
public static class MediaLibrary
{
    public static readonly string[] VideoExtensions =
        [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm"];

    public static bool IsVideo(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> EnumerateVideos(string dir)
    {
        if (!Directory.Exists(dir)) return [];

        return Directory
            .EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(IsVideo);
    }
}
