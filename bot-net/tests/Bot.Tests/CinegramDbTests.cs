using Bot.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bot.Tests;

public class CinegramDbTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cinegram-db-test-").FullName;
    private readonly string _path;

    public CinegramDbTests()
    {
        _path = Path.Combine(_dir, "database.db");
        CreateBackendDatabase(_path, CinegramDb.ExpectedAlembicRevision);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>Builds a database the way the backend's Alembic head leaves it.</summary>
    private static void CreateBackendDatabase(string path, string revision)
    {
        var schema = System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "backend-schema.sql"));
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        Execute(connection, schema);
        Execute(connection, $"INSERT INTO alembic_version (version_num) VALUES ('{revision}')");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private CinegramDb Open() => new(CinegramDb.Options(_path));

    private record Column(string Name, bool NotNull);

    private List<Column> TableColumns(string table)
    {
        using var connection = new SqliteConnection($"Data Source={_path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var reader = command.ExecuteReader();
        var columns = new List<Column>();
        while (reader.Read()) columns.Add(new Column(reader.GetString(1), reader.GetInt64(3) == 1));
        return columns;
    }

    [Fact]
    public void Model_MapsEveryBackendColumnWithTheSameNullability()
    {
        using var db = Open();
        var entities = db.Model.GetEntityTypes().ToList();

        foreach (var entity in entities)
        {
            var table = entity.GetTableName()!;
            var expected = TableColumns(table).OrderBy(c => c.Name).ToList();
            var mapped = entity.GetProperties()
                .Select(p => new Column(p.GetColumnName(), !p.IsNullable))
                .OrderBy(c => c.Name)
                .ToList();

            Assert.True(expected.Count > 0, $"Table {table} does not exist in the backend schema");
            Assert.Equal(expected, mapped);
        }

        var tables = (string)Scalar(_path,
            "SELECT group_concat(name) FROM (SELECT name FROM sqlite_master WHERE type = 'table' AND name <> 'alembic_version' ORDER BY name)")!;
        Assert.Equal(tables.Split(',').Order(), entities.Select(e => e.GetTableName()!).Order());
    }

    [Fact]
    public async Task Writes_UseTheBackendsColumnsAndTimestampFormat()
    {
        await using (var db = Open())
        {
            var movie = new Movie { TmdbId = 635302, Title = "Mugen Train", CreatedAt = new DateTime(2026, 7, 29, 10, 0, 1, DateTimeKind.Utc) };
            var collection = new Collection { Name = "Mugen Train BDRip", Quality = "1080p", Movie = movie };
            collection.Files.Add(new TelegramFile
            {
                MessageId = 4242,
                Filename = "Mugen Train.mkv",
                Filesize = 4_194_304_000,
                DocumentId = 6051031088048905721,
                CreatedAt = new DateTime(2026, 7, 29, 10, 0, 1, 500, DateTimeKind.Utc),
            });
            db.Collections.Add(collection);
            await db.SaveChangesAsync();
        }

        Assert.Equal("2026-07-29 10:00:01.000000", Scalar(_path, "SELECT created_at FROM movie"));
        Assert.Equal("2026-07-29 10:00:01.500000", Scalar(_path, "SELECT created_at FROM file"));
        Assert.Equal("bot", Scalar(_path, "SELECT storage_peer FROM file"));
        Assert.Equal(6051031088048905721L, Scalar(_path, "SELECT document_id FROM file"));
        Assert.Equal(1L, Scalar(_path, "SELECT count(*) FROM collection c JOIN movie m ON m.id = c.movie_id WHERE m.tmdb_id = 635302"));
    }

    [Fact]
    public async Task Reads_RowsWrittenBySqlAlchemy()
    {
        using (var connection = new SqliteConnection($"Data Source={_path};Pooling=False"))
        {
            connection.Open();
            Execute(connection, """
                INSERT INTO series (id, tmdb_id, manual_title, manually_added, created_at) VALUES (1, 1429, 'Attack on Titan', 1, '2026-10-11 20:34:11.499989');
                INSERT INTO season (id, series_id, season_number, local_metadata) VALUES (1, 1, 2, 0);
                INSERT INTO episode (id, season_id, episode_number, title) VALUES (1, 1, 5, 'Historia');
                INSERT INTO collection (id, name, episode_id) VALUES (1, 'AoT S02E05', 1);
                INSERT INTO watchedfile (path, filename, filesize, first_seen_at, confidence, status, notified_at)
                    VALUES ('/downloads/a.mkv', 'a.mkv', 10, '2026-10-10 21:23:27.128636', 0.87, 'notified', NULL);
                """);
        }

        await using var db = Open();
        var series = await db.Series.Include(s => s.Seasons).ThenInclude(s => s.Episodes).ThenInclude(e => e.Collections).SingleAsync();
        Assert.True(series.ManuallyAdded);
        Assert.Equal(new DateTime(2026, 10, 11, 20, 34, 11, DateTimeKind.Utc).AddTicks(4999890), series.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, series.CreatedAt.Kind);
        Assert.Equal("AoT S02E05", series.Seasons.Single().Episodes.Single().Collections.Single().Name);

        var watched = await db.WatchedFiles.SingleAsync();
        Assert.Equal(0.87, watched.Confidence);
        Assert.Null(watched.NotifiedAt);
    }

    [Fact]
    public async Task Check_ReportsAMissingFile()
    {
        var result = await DatabaseCheck.RunAsync(Path.Combine(_dir, "absent.db"));

        Assert.Equal(DatabaseStatus.Missing, result.Status);
        Assert.False(System.IO.File.Exists(Path.Combine(_dir, "absent.db")));
    }

    [Fact]
    public async Task Check_RejectsAnotherAlembicRevision()
    {
        var path = Path.Combine(_dir, "old.db");
        CreateBackendDatabase(path, "0009_forward_origin");

        var result = await DatabaseCheck.RunAsync(path);

        Assert.Equal(DatabaseStatus.UnexpectedSchema, result.Status);
        Assert.Equal("0009_forward_origin", result.Revision);
    }

    [Fact]
    public async Task Check_ReadsAReadyDatabaseAndSwitchesItToWal()
    {
        await using (var db = Open())
        {
            db.Movies.Add(new Movie { Title = "A" });
            db.Series.AddRange(new Series { ManualTitle = "B" }, new Series { ManualTitle = "C" });
            await db.SaveChangesAsync();
        }

        var result = await DatabaseCheck.RunAsync(_path);

        Assert.Equal(new DatabaseCheckResult(DatabaseStatus.Ready, CinegramDb.ExpectedAlembicRevision, 1, 2), result);
        Assert.Equal("wal", Scalar(_path, "PRAGMA journal_mode"));
    }

    /// <summary>
    /// Loads every row of a real database with the EF model. Runs only when CINEGRAM_REAL_DB points
    /// at a copy of one (never the live file), to check that existing data maps without errors.
    /// </summary>
    [Fact]
    public async Task RealDatabaseCopy_LoadsEveryRow()
    {
        var path = Environment.GetEnvironmentVariable("CINEGRAM_REAL_DB");
        if (string.IsNullOrEmpty(path)) return;

        Assert.Equal(DatabaseStatus.Ready, (await DatabaseCheck.RunAsync(path)).Status);
        await using var db = new CinegramDb(CinegramDb.Options(path));
        var counts = new Dictionary<string, int>
        {
            ["movie"] = (await db.Movies.Include(m => m.Collections).ThenInclude(c => c.Files).ToListAsync()).Count,
            ["series"] = (await db.Series.Include(s => s.Seasons).ThenInclude(s => s.Episodes).ToListAsync()).Count,
            ["collection"] = (await db.Collections.ToListAsync()).Count,
            ["file"] = (await db.Files.ToListAsync()).Count,
            ["downloadtask"] = (await db.DownloadTasks.ToListAsync()).Count,
            ["uploadtask"] = (await db.UploadTasks.ToListAsync()).Count,
            ["watchedfile"] = (await db.WatchedFiles.ToListAsync()).Count,
        };
        foreach (var (table, count) in counts)
            Assert.Equal(Convert.ToInt32(Scalar(path, $"SELECT count(*) FROM \"{table}\"")), count);
    }
}
