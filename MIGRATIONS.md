# Migrations

Breaking changes that require manual action when upgrading an existing deployment. Entries are added when a change lands, in descending order (newest first). Non-breaking changes aren't listed here — see the [GitHub Releases](https://github.com/christt105/cinegram/releases) for the full changelog.

## v3.1.0: one appdata volume

`./appdata/bot-net` and `./appdata/backend` were v2 names for what is now one service. `docker-compose.yml` mounts `./appdata` at `/data` instead, with the database in `./appdata/db`. Paths inside the container don't change, so this only moves folders on the host.

**To upgrade** (from the folder holding `docker-compose.yml`):

1. Stop the stack and back up `./appdata`:
   ```bash
   docker compose down
   cp -a appdata appdata.bak-pre-single-volume
   ```
2. Move the folders. `appdata/bot-net/db` is the empty mount point the old layout left behind:
   ```bash
   rmdir appdata/bot-net/db
   mv appdata/backend appdata/bot-net/db
   mv appdata/bot-net appdata.new && rmdir appdata && mv appdata.new appdata
   ```
3. Replace `docker-compose.yml` with the new one. If you keep a `docker-compose.override.yml` with absolute paths, replace its two `appdata/bot-net` and `appdata/backend` entries with a single `<absolute path>/appdata:/data`.
4. `docker compose up -d`. The log should say `[Database] /data/db/database.db up to date`.

## v3.0.0

Cinegram is now a single container. The Python `backend` and the nginx `web` container are gone: the .NET service owns the database and its migrations, serves the web panel and the API (under `/api`) on `WEB_PORT`, and proxies the web's Jellyfin reads so `JELLYFIN_TOKEN` no longer reaches the browser. The database file is the same one, so nothing is converted or copied.

**Before upgrading:**

- Run v2.3.0 at least once. v3 adopts a database at v2.3.0's schema and refuses to start on an older one, with a log line saying so.
- Back up `./appdata` (stop the stack first, or copy `database.db` with its `-wal` file).

**To upgrade:**

1. Replace `docker-compose.yml` with the new one: a single `cinegram` service with the same `./appdata/bot-net` and `./appdata/backend` mounts as before. If you keep a `docker-compose.override.yml` with absolute paths, rename its `bot-net` service to `cinegram` and drop the `backend` and `web` entries.
2. In `.env`, delete `BACKEND_PORT` and `BOT_NET_PORT`; they are no longer used. `WEB_PORT` now publishes the whole service. `JELLYFIN_URL` must be reachable from the container (it was already, for the worker), since the browser no longer talks to Jellyfin directly.
3. Pull and start, removing the three old containers:
   ```bash
   docker compose pull
   docker compose up -d --remove-orphans
   ```
4. The log should show `[Database] /data/db/database.db adopted from the v2 backend, up to date`. Later starts say `up to date`, or list the migrations they applied.

**Other changes:**

- Scripts that called `bot-net` on `BOT_NET_PORT` or the backend on `BACKEND_PORT` now use `http://<host>:WEB_PORT/api/...`.
- The `/health` bot command is removed; it only reported the backend's health.
- Images are published as `ghcr.io/christt105/cinegram` instead of `cinegram-web`, `cinegram-backend` and `cinegram-bot-net`.

**Rolling back:** the database keeps the v2 `alembic_version` table, and v2 ignores the `__EFMigrationsHistory` table v3 adds, so restoring the v2.3.0 `docker-compose.yml` and `.env` works as long as no later v3 release has changed the schema. Releases that do will say so here.

## v2.0.0

`IMPORT_MOVIES_DIR` and `IMPORT_SHOWS_DIR` no longer exist. `bot-net` now mounts a single `MEDIA_ROOT` directory (with `movies`/`shows` as subfolders of it) so moves within the library stay on one filesystem instead of silently falling back to a slow copy.

**To upgrade:**

1. If your movies and shows directories don't already share a parent directory on the host, move one under the other first.
2. In `.env`, replace:
   ```
   IMPORT_MOVIES_DIR=/path/to/movies
   IMPORT_SHOWS_DIR=/path/to/shows
   ```
   with:
   ```
   MEDIA_ROOT=/path/to/media
   MOVIES_SUBDIR=movies
   SHOWS_SUBDIR=shows
   ```
   `MOVIES_SUBDIR`/`SHOWS_SUBDIR` default to `movies`/`shows`, so you only need to set them if your subfolder names differ.
3. Pull the new `docker-compose.yml` (the `bot-net` volume mount changed from two binds to one) and run `docker compose up -d`.
