using Bot.Models;
using Bot.Utils;

namespace Bot.Services;

/// <summary>
/// Works with the media files already on disk in the mounted library directories: lists
/// them, reads their technical metadata into a collection and deletes local copies.
/// Only bot-net has those directories mounted, so this cannot live in the backend.
/// </summary>
public class LocalMediaService
{
    private const int DefaultLimit = 200;

    private readonly BotHolder _holder;

    public LocalMediaService(BotHolder holder)
    {
        _holder = holder;
    }

    /// <summary>
    /// Lists video files in the library, optionally filtered by an accent-insensitive
    /// substring of their path and by the ids of the title they are filed under. Filtering
    /// by id is what lets a collection be shown only the copies of its own title.
    /// </summary>
    public List<LocalFile> ListFiles(string? query = null, int? tmdbId = null, int? tvdbId = null, int limit = DefaultLimit)
    {
        var normalizedQuery = TextNormalizer.Normalize(query);

        return MediaLibrary.Roots()
            .SelectMany(MediaLibrary.EnumerateVideos)
            .Where(path => normalizedQuery.Length == 0
                           || TextNormalizer.Normalize(path).Contains(normalizedQuery))
            .Where(path => MediaNameParser.MatchesIds(path, tmdbId, tvdbId))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Take(limit)
            .Select(Describe)
            .ToList();
    }

    private static LocalFile Describe(string path)
    {
        var info = new FileInfo(path);

        return new LocalFile
        {
            Path = path,
            Filename = info.Name,
            Filesize = info.Length,
            ModifiedAt = info.LastWriteTimeUtc,
            VersionTag = MediaNameParser.ParseVersionTag(path),
            Quality = MediaNameParser.ParseQuality(path),
            TmdbId = MediaNameParser.ParseTmdbId(path),
            TvdbId = MediaNameParser.ParseTvdbId(path)
        };
    }

    /// <summary>
    /// Reads a local file with ffprobe and stores the result as the collection's technical
    /// metadata and audio/subtitle languages, pointing the collection at that file. The
    /// backend derives "quality" from the technical metadata itself on patch, so it isn't
    /// set here. Lets an already downloaded file be described without re-uploading it to
    /// Telegram.
    /// </summary>
    public async Task<(bool ok, string error)> ProbeIntoCollectionAsync(int collectionId, string path)
    {
        if (!_holder.IsReady)
            return (false, "Bot not yet initialised.");

        if (!MediaLibrary.TryResolveInsideLibrary(path, out var fullPath))
            return (false, "Path is outside the mounted media library.");

        if (!System.IO.File.Exists(fullPath))
            return (false, $"File not found: {fullPath}");

        return await ProbeFileIntoCollectionAsync(collectionId, fullPath);
    }

    /// <summary>
    /// Matches a series' episodes against the video files already on disk under its
    /// library folder, and probes the unambiguous ones straight into their collection: one
    /// file for that season/episode, one collection tracking the episode, and that
    /// collection not already pointing at a local file. Anything else comes back as a skip
    /// with the reason, left for the manual probe modal in the web UI.
    /// </summary>
    public async Task<(bool ok, string error, SeriesMatcher.Plan? plan)> MatchSeriesAsync(int seriesId)
    {
        if (!_holder.IsReady)
            return (false, "Bot not yet initialised.", null);

        var series = await _holder.ApiClient.GetSeriesAsync(seriesId);
        if (series is null)
            return (false, $"Series {seriesId} not found.", null);

        var localFiles = MediaLibrary.Roots()
            .SelectMany(MediaLibrary.EnumerateVideos)
            .Where(path => MediaNameParser.MatchesIds(path, series.TmdbId, series.TvdbId))
            .Select(path => (path, seasonEpisode: MediaNameParser.ParseSeasonEpisode(path)))
            .Where(f => f.seasonEpisode is not null)
            .Select(f => new SeriesMatcher.LocalEpisodeFile(f.path, f.seasonEpisode!.Value.Season, f.seasonEpisode.Value.Episode));

        var plan = SeriesMatcher.Build(series, localFiles);

        foreach (var match in plan.Matches)
        {
            var (ok, error) = await ProbeFileIntoCollectionAsync(match.CollectionId, match.Path);
            if (!ok)
                Log.Error($"[LocalMedia] Series {seriesId} match S{match.Season:D2}E{match.Episode:D2}: probe failed for collection {match.CollectionId}: {error}");
        }

        Log.Info($"[LocalMedia] Matched series {seriesId} against Jellyfin: {plan.Matches.Count} probed, {plan.Skips.Count} skipped.");
        return (true, "", plan);
    }

    private async Task<(bool ok, string error)> ProbeFileIntoCollectionAsync(int collectionId, string fullPath)
    {
        try
        {
            var metadata = await MediaProbe.ReadMetadataAsync(fullPath);
            var summary = MediaProbe.Summarize(metadata);
            var updated = await _holder.ApiClient.PatchCollectionAsync(collectionId, new UpdateCollectionRequest
            {
                TechnicalMetadata = metadata,
                LocalPath = fullPath,
                AudioLanguages = summary.AudioLanguages,
                SubtitleLanguages = summary.SubtitleLanguages
            });

            if (updated is null)
                return (false, $"Collection {collectionId} not found.");

            Log.Info($"[LocalMedia] Stored technical metadata of {fullPath} in collection {collectionId}.");
            return (true, "");
        }
        catch (Exception ex)
        {
            Log.Error($"[LocalMedia] Failed to probe {fullPath} into collection {collectionId}", ex);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Deletes the collection's downloaded file from disk and clears its local path. The
    /// collection and its Telegram files are left untouched.
    /// </summary>
    public async Task<(bool ok, string error)> DeleteLocalCopyAsync(int collectionId)
    {
        if (!_holder.IsReady)
            return (false, "Bot not yet initialised.");

        var collection = await _holder.ApiClient.GetCollectionAsync(collectionId);
        if (collection is null)
            return (false, $"Collection {collectionId} not found.");

        if (string.IsNullOrWhiteSpace(collection.LocalPath))
            return (false, "Collection has no local copy.");

        if (!MediaLibrary.TryResolveInsideLibrary(collection.LocalPath, out var fullPath))
            return (false, "Local path is outside the mounted media library.");

        try
        {
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
            else
                Log.Info($"[LocalMedia] {fullPath} was already gone, clearing the local path anyway.");

            await _holder.ApiClient.ClearCollectionLocalPathAsync(collectionId);
            Log.Info($"[LocalMedia] Deleted local copy of collection {collectionId}: {fullPath}");
            return (true, "");
        }
        catch (Exception ex)
        {
            Log.Error($"[LocalMedia] Failed to delete {fullPath}", ex);
            return (false, ex.Message);
        }
    }
}
