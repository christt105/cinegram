using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Cinegram.Watch;

namespace Cinegram.Data;

/// <summary>
/// EF Core view of the Cinegram database (<c>database.db</c>). Column names and timestamp format
/// follow the SQLAlchemy ones the Python backend used, so databases from v2 keep working unchanged.
/// </summary>
public class CinegramDb(DbContextOptions<CinegramDb> options) : DbContext(options)
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffff";

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<TelegramFile> Files => Set<TelegramFile>();
    public DbSet<DownloadTask> DownloadTasks => Set<DownloadTask>();
    public DbSet<UploadTask> UploadTasks => Set<UploadTask>();
    public DbSet<WatchedFile> WatchedFiles => Set<WatchedFile>();

    public static string ConnectionString(string path) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        DefaultTimeout = 15,
    }.ToString();

    public static DbContextOptions<CinegramDb> Options(string path) =>
        new DbContextOptionsBuilder<CinegramDb>().UseSqlite(ConnectionString(path)).Options;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<TimestampConverter>();
        builder.Properties<DateTime?>().HaveConversion<TimestampConverter>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Movie>().ToTable("movie").HasIndex(m => m.TmdbId).IsUnique().HasDatabaseName("ix_movie_tmdb_id");
        model.Entity<Series>().ToTable("series").HasIndex(s => s.TmdbId).IsUnique().HasDatabaseName("ix_series_tmdb_id");
        model.Entity<Season>().ToTable("season");
        model.Entity<Episode>().ToTable("episode");
        model.Entity<Collection>().ToTable("collection");
        model.Entity<TelegramFile>().ToTable("file").HasIndex(f => f.DocumentId).HasDatabaseName("ix_file_document_id");
        model.Entity<DownloadTask>().ToTable("downloadtask");
        model.Entity<UploadTask>().ToTable("uploadtask");
        model.Entity<WatchedFile>().ToTable("watchedfile").HasIndex(w => w.Path).IsUnique().HasDatabaseName("ix_watchedfile_path");

        model.Entity<Collection>().HasOne(c => c.Movie).WithMany(m => m.Collections).HasForeignKey(c => c.MovieId);
        model.Entity<Collection>().HasOne(c => c.Season).WithMany(s => s.Collections).HasForeignKey(c => c.SeasonId);
        model.Entity<Collection>().HasOne(c => c.Episode).WithMany(e => e.Collections).HasForeignKey(c => c.EpisodeId);
        model.Entity<DownloadTask>().HasOne(d => d.Collection).WithMany().HasForeignKey(d => d.CollectionId);

        foreach (var entity in model.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
            property.SetColumnName(SnakeCase(property.Name));
    }

    private static string SnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Stores timestamps as naive UTC text with microseconds, the format SQLAlchemy wrote, so
    /// ordering and comparisons in SQL keep working across rows from before v3.
    /// </summary>
    private class TimestampConverter() : ValueConverter<DateTime, string>(
        v => (v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v).ToString(TimestampFormat, CultureInfo.InvariantCulture),
        v => DateTime.SpecifyKind(DateTime.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.None), DateTimeKind.Utc));
}
