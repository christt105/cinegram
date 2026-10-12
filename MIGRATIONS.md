# Migrations

Breaking changes that require manual action when upgrading an existing deployment. Entries are added when a change lands, in descending order (newest first). Non-breaking changes aren't listed here — see the [GitHub Releases](https://github.com/christt105/cinegram/releases) for the full changelog.

## v2.4.0

`bot-net` now mounts the backend's data directory (`./appdata/backend`) at `/data/db`, the first step of moving the backend's work into it. Nothing reads it yet, so an old `docker-compose.yml` keeps working, but later releases will need the mount. The web reaches the backend and `bot-net` through its own `/api` and `/bot` proxy, so the browser no longer needs `BACKEND_PORT` or `BOT_NET_PORT` to be reachable.

**To upgrade:**

1. Pull the new `docker-compose.yml`. If you override the `bot-net` volumes with absolute paths (for example in a `docker-compose.override.yml`), add the backend directory there too:
   ```yaml
   - /absolute/path/to/appdata/backend:/data/db
   ```
2. Run `docker compose up -d`. The `bot-net` log should show `[Database] /data/db/database.db at <revision>`.

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
