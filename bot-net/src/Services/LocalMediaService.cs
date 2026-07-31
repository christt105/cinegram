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
    /// metadata, pointing the collection at that file. Lets an already downloaded file be
    /// described without re-uploading it to Telegram.
    /// </summary>
    public async Task<(bool ok, string error)> ProbeIntoCollectionAsync(int collectionId, string path)
    {
        if (!_holder.IsReady)
            return (false, "Bot not yet initialised.");

        if (!MediaLibrary.TryResolveInsideLibrary(path, out var fullPath))
            return (false, "Path is outside the mounted media library.");

        if (!System.IO.File.Exists(fullPath))
            return (false, $"File not found: {fullPath}");

        try
        {
            var metadata = await MediaProbe.ReadMetadataAsync(fullPath);
            var updated = await _holder.ApiClient.PatchCollectionAsync(collectionId, new UpdateCollectionRequest
            {
                TechnicalMetadata = metadata,
                LocalPath = fullPath
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
