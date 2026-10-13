using Bot.Data;
using Microsoft.EntityFrameworkCore;

namespace Bot.Services;

/// <summary>Consistent snapshots of the Cinegram database, safe to take while it is in use.</summary>
public class DatabaseBackup(IDbContextFactory<CinegramDb> dbFactory)
{
    public static string FileName(DateTime utc) => $"cinegram-backup-{utc:yyyyMMdd-HHmmss}.db";

    /// <summary>Writes a snapshot with <c>VACUUM INTO</c> and opens it. The temporary file is
    /// deleted when the stream is closed.</summary>
    public async Task<Stream> OpenSnapshotAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cinegram-backup-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            await db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", [path], ct);
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Delete, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }
}
