namespace Cinegram.Data;

public class Movie
{
    public int Id { get; set; }
    public int? TmdbId { get; set; }
    public string? Title { get; set; }
    public string? PosterPath { get; set; }
    public int? ReleaseYear { get; set; }
    public string? Overview { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public bool ManuallyAdded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Collection> Collections { get; set; } = [];
}

public class Series
{
    public int Id { get; set; }
    public int? TmdbId { get; set; }
    public int? TvdbId { get; set; }
    public string? ManualTitle { get; set; }
    public string? PosterPath { get; set; }
    public string? Overview { get; set; }
    public int? ReleaseYear { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public bool ManuallyAdded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Season> Seasons { get; set; } = [];
}

public class Season
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int SeasonNumber { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public bool LocalMetadata { get; set; }

    public Series? Series { get; set; }
    public List<Episode> Episodes { get; set; } = [];
    public List<Collection> Collections { get; set; } = [];
}

public class Episode
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int EpisodeNumber { get; set; }
    public string? Title { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }

    public Season? Season { get; set; }
    public List<Collection> Collections { get; set; } = [];
}

public class Collection
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Quality { get; set; }
    public string? Codec { get; set; }
    public string? AudioLanguages { get; set; }
    public string? SubtitleLanguages { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public string? TechnicalMetadata { get; set; }
    public string? LocalPath { get; set; }

    public int? MovieId { get; set; }
    public int? SeasonId { get; set; }
    public int? EpisodeId { get; set; }

    public Movie? Movie { get; set; }
    public Season? Season { get; set; }
    public Episode? Episode { get; set; }
    public List<TelegramFile> Files { get; set; } = [];
}

/// <summary>
/// A file stored in Telegram (table <c>file</c>). <see cref="DocumentId"/> identifies the
/// underlying blob, which forwards share, so most rows point at blobs uploaded by someone else.
/// </summary>
public class TelegramFile
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public int? UserMessageId { get; set; }
    public string Filename { get; set; } = "";
    public long Filesize { get; set; }
    public string? MimeType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string StoragePeer { get; set; } = "bot";

    public long? DocumentId { get; set; }
    public string? FwdFromType { get; set; }
    public string? FwdFromId { get; set; }
    public string? FwdFromName { get; set; }
    public bool FwdFromHidden { get; set; }

    public int CollectionId { get; set; }
    public Collection? Collection { get; set; }
}

public class DownloadTask
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public string? NameSuffix { get; set; }
    public string Status { get; set; } = "pending";
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Collection? Collection { get; set; }
}

public class UploadTask
{
    public int Id { get; set; }
    public string JellyfinId { get; set; } = "";
    public int? TmdbId { get; set; }
    public string MediaType { get; set; } = "";
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public int? Year { get; set; }
    public string Status { get; set; } = "pending";
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// A file seen in the downloads folder. Status flow: pending, notified, confirmed or
/// corrected, moved; removed and error end it off the happy path.
/// </summary>
public class WatchedFile
{
    public int Id { get; set; }
    public string Path { get; set; } = "";
    public string Filename { get; set; } = "";
    public long Filesize { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    public string? GuessMediaType { get; set; }
    public int? GuessTmdbId { get; set; }
    public int? GuessTvdbId { get; set; }
    public string? GuessTitle { get; set; }
    public int? GuessYear { get; set; }
    public int? GuessSeason { get; set; }
    public int? GuessEpisode { get; set; }
    public double Confidence { get; set; }
    public string? GuessSource { get; set; }

    public string Status { get; set; } = "pending";
    public DateTime? NotifiedAt { get; set; }
    public string? MovedPath { get; set; }
    public string? ErrorMessage { get; set; }
}
