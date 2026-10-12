using Microsoft.EntityFrameworkCore;

namespace Bot.Data;

public enum DatabaseStatus { Missing, UnexpectedSchema, Ready }

public record DatabaseCheckResult(DatabaseStatus Status, string? Revision, int Movies, int Series);

/// <summary>
/// Verifies at startup that the shared database is mounted and at the Alembic revision the EF
/// model maps, so a backend upgrade that changes the schema is caught before .NET writes to it.
/// </summary>
public static class DatabaseCheck
{
    public static async Task<DatabaseCheckResult> RunAsync(string path, CancellationToken ct = default)
    {
        if (!System.IO.File.Exists(path))
            return new DatabaseCheckResult(DatabaseStatus.Missing, null, 0, 0);

        await using var db = new CinegramDb(CinegramDb.Options(path));
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL", ct);
        var revision = await db.Database
            .SqlQueryRaw<string>("SELECT version_num AS Value FROM alembic_version")
            .FirstOrDefaultAsync(ct);
        if (revision != CinegramDb.ExpectedAlembicRevision)
            return new DatabaseCheckResult(DatabaseStatus.UnexpectedSchema, revision, 0, 0);

        return new DatabaseCheckResult(
            DatabaseStatus.Ready, revision, await db.Movies.CountAsync(ct), await db.Series.CountAsync(ct));
    }

    public static async Task LogAsync(string path, CancellationToken ct)
    {
        try
        {
            var result = await RunAsync(path, ct);
            switch (result.Status)
            {
                case DatabaseStatus.Missing:
                    Log.Warning($"[Database] {path} not found. Mount the backend's data directory at {Path.GetDirectoryName(path)}.");
                    break;
                case DatabaseStatus.UnexpectedSchema:
                    Log.Warning($"[Database] Schema revision is {result.Revision ?? "unknown"}, expected {CinegramDb.ExpectedAlembicRevision}. Upgrade bot-net together with the backend.");
                    break;
                default:
                    Log.Info($"[Database] {path} at {result.Revision}: {result.Movies} movies, {result.Series} series.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[Database] Could not open {path}", ex);
        }
    }
}
