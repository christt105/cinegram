using System.Reflection;
using Bot.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bot.Data;

public enum DatabaseOrigin { Created, AdoptedFromV2, Existing }

public record DatabaseSchemaResult(DatabaseOrigin Origin, IReadOnlyList<string> Applied, int Movies, int Series);

/// <summary>
/// Brings the database to the EF model at startup: creates it when the file is missing, adopts a
/// database from v2 (whose schema the Python backend's Alembic migrations owned) by recording
/// <see cref="Baseline"/> as applied, and then runs any pending migration.
/// </summary>
public static class DatabaseSchema
{
    public static string BaselineId { get; } = typeof(Baseline).GetCustomAttribute<MigrationAttribute>()!.Id;

    public static async Task<DatabaseSchemaResult> MigrateAsync(string path, CancellationToken ct = default)
    {
        var existed = System.IO.File.Exists(path);
        if (!existed)
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await using var db = new CinegramDb(CinegramDb.Options(path));
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL", ct);

        var origin = !existed ? DatabaseOrigin.Created
            : await AdoptV2DatabaseAsync(db, ct) ? DatabaseOrigin.AdoptedFromV2
            : DatabaseOrigin.Existing;

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        await db.Database.MigrateAsync(ct);

        return new DatabaseSchemaResult(origin, pending, await db.Movies.CountAsync(ct), await db.Series.CountAsync(ct));
    }

    public static async Task<bool> RunAsync(string path, CancellationToken ct)
    {
        try
        {
            var result = await MigrateAsync(path, ct);
            var applied = result.Applied.Count == 0 ? "up to date" : $"applied {string.Join(", ", result.Applied)}";
            var origin = result.Origin switch
            {
                DatabaseOrigin.Created => "created, ",
                DatabaseOrigin.AdoptedFromV2 => "adopted from the v2 backend, ",
                _ => "",
            };
            Log.Info($"[Database] {path} {origin}{applied}: {result.Movies} movies, {result.Series} series.");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[Database] Could not prepare {path}", ex);
            return false;
        }
    }

    private static async Task<bool> AdoptV2DatabaseAsync(CinegramDb db, CancellationToken ct)
    {
        var history = db.GetService<IHistoryRepository>();
        if (await history.ExistsAsync(ct) || !await TableExistsAsync(db, "alembic_version", ct))
            return false;

        var revision = await db.Database
            .SqlQueryRaw<string>("SELECT version_num AS Value FROM alembic_version")
            .FirstOrDefaultAsync(ct);
        if (revision != Baseline.LegacyAlembicRevision)
            throw new InvalidOperationException(
                $"The database is at backend revision {revision ?? "unknown"}, expected {Baseline.LegacyAlembicRevision}. " +
                "Upgrade to Cinegram v2.3.0 and start it once before moving to v3.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync(history.GetCreateScript(), ct);
        await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(BaselineId, ProductInfo.GetVersion())), ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private static async Task<bool> TableExistsAsync(CinegramDb db, string table, CancellationToken ct) =>
        await db.Database
            .SqlQuery<int>($"SELECT count(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {table}")
            .SingleAsync(ct) > 0;
}
