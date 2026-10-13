using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bot.Data.Migrations
{
    /// <summary>
    /// The schema the Python backend's Alembic migrations left at <see cref="LegacyAlembicRevision"/>,
    /// verbatim, so a new database and one upgraded from v2 have the same tables. Databases from v2
    /// already have it: <see cref="DatabaseSchema"/> records this migration as applied instead of
    /// running it.
    /// </summary>
    public partial class Baseline : Migration
    {
        public const string LegacyAlembicRevision = "0010_collection_codec";

        private const string Schema = """
            CREATE TABLE collection (
            	id INTEGER NOT NULL, 
            	name VARCHAR, 
            	quality VARCHAR, 
            	audio_languages VARCHAR, 
            	subtitle_languages VARCHAR, 
            	tags VARCHAR, 
            	notes VARCHAR, 
            	technical_metadata VARCHAR, 
            	movie_id INTEGER, 
            	season_id INTEGER, 
            	episode_id INTEGER, local_path VARCHAR, codec VARCHAR, 
            	PRIMARY KEY (id), 
            	FOREIGN KEY(episode_id) REFERENCES episode (id), 
            	FOREIGN KEY(movie_id) REFERENCES movie (id), 
            	FOREIGN KEY(season_id) REFERENCES season (id)
            );
            CREATE TABLE downloadtask (
            	id INTEGER NOT NULL, 
            	collection_id INTEGER NOT NULL, 
            	status VARCHAR NOT NULL, 
            	progress INTEGER NOT NULL, 
            	error_message VARCHAR, 
            	created_at DATETIME NOT NULL, 
            	completed_at DATETIME, name_suffix VARCHAR, 
            	PRIMARY KEY (id), 
            	FOREIGN KEY(collection_id) REFERENCES collection (id)
            );
            CREATE TABLE episode (
            	id INTEGER NOT NULL, 
            	season_id INTEGER NOT NULL, 
            	episode_number INTEGER NOT NULL, 
            	title VARCHAR, 
            	tags VARCHAR, 
            	notes VARCHAR, 
            	PRIMARY KEY (id), 
            	FOREIGN KEY(season_id) REFERENCES season (id)
            );
            CREATE TABLE "file" (
            	id INTEGER NOT NULL, 
            	message_id INTEGER NOT NULL, 
            	filename VARCHAR NOT NULL, 
            	filesize INTEGER NOT NULL, 
            	mime_type VARCHAR, 
            	created_at DATETIME NOT NULL, 
            	collection_id INTEGER NOT NULL, 
            	storage_peer VARCHAR DEFAULT 'bot' NOT NULL, 
            	user_message_id INTEGER, 
            	document_id INTEGER, 
            	fwd_from_type VARCHAR, 
            	fwd_from_id VARCHAR, 
            	fwd_from_name VARCHAR, 
            	fwd_from_hidden BOOLEAN DEFAULT 0 NOT NULL, 
            	PRIMARY KEY (id), 
            	FOREIGN KEY(collection_id) REFERENCES collection (id)
            );
            CREATE INDEX ix_file_document_id ON file (document_id);
            CREATE TABLE movie (
            	id INTEGER NOT NULL, 
            	tmdb_id INTEGER, 
            	title VARCHAR, 
            	poster_path VARCHAR, 
            	release_year INTEGER, 
            	overview VARCHAR, 
            	tags VARCHAR, 
            	notes VARCHAR, 
            	manually_added BOOLEAN NOT NULL, 
            	created_at DATETIME NOT NULL, 
            	PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX ix_movie_tmdb_id ON movie (tmdb_id);
            CREATE TABLE "season" (
            	id INTEGER NOT NULL, 
            	series_id INTEGER NOT NULL, 
            	season_number INTEGER NOT NULL, 
            	tags VARCHAR, 
            	notes VARCHAR, 
            	local_metadata BOOLEAN DEFAULT 0 NOT NULL, 
            	PRIMARY KEY (id), 
            	FOREIGN KEY(series_id) REFERENCES series (id)
            );
            CREATE TABLE series (
            	id INTEGER NOT NULL, 
            	tmdb_id INTEGER, 
            	tvdb_id INTEGER, 
            	manual_title VARCHAR, 
            	poster_path VARCHAR, 
            	overview VARCHAR, 
            	release_year INTEGER, 
            	tags VARCHAR, 
            	notes VARCHAR, 
            	manually_added BOOLEAN NOT NULL, 
            	created_at DATETIME NOT NULL, 
            	PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX ix_series_tmdb_id ON series (tmdb_id);
            CREATE TABLE uploadtask (
            	id INTEGER NOT NULL, 
            	jellyfin_id VARCHAR NOT NULL, 
            	tmdb_id INTEGER, 
            	media_type VARCHAR NOT NULL, 
            	path VARCHAR NOT NULL, 
            	title VARCHAR NOT NULL, 
            	year INTEGER, 
            	status VARCHAR NOT NULL, 
            	progress INTEGER NOT NULL, 
            	error_message VARCHAR, 
            	created_at DATETIME NOT NULL, 
            	completed_at DATETIME, 
            	PRIMARY KEY (id)
            );
            CREATE TABLE watchedfile (
            	id INTEGER NOT NULL, 
            	path VARCHAR NOT NULL, 
            	filename VARCHAR NOT NULL, 
            	filesize INTEGER NOT NULL, 
            	first_seen_at DATETIME NOT NULL, 
            	guess_media_type VARCHAR, 
            	guess_tmdb_id INTEGER, 
            	guess_title VARCHAR, 
            	guess_season INTEGER, 
            	guess_episode INTEGER, 
            	confidence FLOAT NOT NULL, 
            	guess_source VARCHAR, 
            	status VARCHAR NOT NULL, 
            	notified_at DATETIME, 
            	moved_path VARCHAR, 
            	error_message VARCHAR, guess_year INTEGER, guess_tvdb_id INTEGER, 
            	PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX ix_watchedfile_path ON watchedfile (path);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Schema);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "downloadtask", "file", "collection", "episode", "season", "series", "movie", "uploadtask", "watchedfile" })
                migrationBuilder.DropTable(table);
        }
    }
}
