# Cinegram

Cinegram is a self-hosted platform that bridges a [Jellyfin](https://jellyfin.org/) media library with [Telegram](https://telegram.org/). It automates downloading movies and series from Telegram, processes and renames them to Jellyfin's naming convention, and can also back up existing Jellyfin files to Telegram.

![Preview of the web](./docs/preview.png)

It ships as three services orchestrated with Docker Compose, so a full deployment is a single `docker compose up`.

## Key Features

- **Bidirectional Media Transfer**: Download files from Telegram directly into your Jellyfin library, or back up existing Jellyfin media to Telegram.
- **Automatic Large File Handling**: Circumvents Telegram's 2 GB bot upload limit by splitting larger files into 1.95 GB multi-part archives using store-only `7z` compression, rejoining them automatically on download.
- **Metadata & Naming Standardization**: Integrates with TMDB to fetch metadata, posters, and standardize filenames/folder structures for Jellyfin.
- **Multi-User Access Control**: Restricts bot commands and storage privileges to authorized Telegram user IDs.
- **Web UI & Bot Interface**: Manage imports, search your library, and monitor transfer queues via the Vue web dashboard or Telegram chat.

## Architecture

Cinegram is a decoupled set of three services:

| Service    | Stack                              | Role                                                                                     |
| ---------- | ---------------------------------- | ---------------------------------------------------------------------------------------- |
| `web`      | Vue 3 + Vite + TypeScript          | Admin panel: browse the library, manage download/upload queues, re-identify collections. |
| `backend`  | Python 3.12 + FastAPI + SQLModel   | Source of truth: REST API, filename parsing, TMDB metadata, task queues (SQLite).        |
| `bot-net`  | C# / .NET 8 + WTelegramClient      | Worker: polls tasks, transfers files over Telegram (MTProto), splits/joins with `7z`, reads metadata with `ffprobe`. |

```
User ─▶ web (Vue) ─▶ backend (FastAPI) ─▶ SQLite / TMDB
                          ▲
                          │ polls tasks & reports status
                       bot-net (.NET) ─▶ Telegram (MTProto)
                                       ─▶ Host disk (Jellyfin media)
```

The `bot-net` worker authenticates as a Telegram **bot**, which caps file transfers at 2 GB; files larger than that are split into **1.95 GB** parts with `7z` (store-only) on upload and rejoined on download.

## Prerequisites

- Docker and Docker Compose.
- A running Jellyfin server and an API token.
- Telegram API credentials (`api_id` / `api_hash` from <https://my.telegram.org>) and a bot token from [@BotFather](https://t.me/BotFather).
- A [TMDB](https://www.themoviedb.org/settings/api) API key.

## Quick start

Cinegram runs from prebuilt images on the GitHub Container Registry, so a deployment needs only two files — no clone, no build.

1. Create a directory and fetch the compose file and the environment template:
   ```bash
   mkdir cinegram && cd cinegram
   curl -O https://raw.githubusercontent.com/christt105/cinegram/main/docker-compose.yml
   curl -o .env https://raw.githubusercontent.com/christt105/cinegram/main/.env.example
   ```
2. Edit `.env` with your own values. In particular, point `IMPORT_MOVIES_DIR` and `IMPORT_SHOWS_DIR` at the host directories where Jellyfin expects movies and shows (these are bind-mounted into `bot-net`).
3. Start the stack:
   ```bash
   docker compose up -d
   ```
4. Open the web panel at `http://<host>:5173`.

Docker pulls the `web`, `backend`, and `bot-net` images from `ghcr.io/christt105/cinegram-*`. To update later, run `docker compose pull && docker compose up -d`. Pin a specific release by setting `CINEGRAM_TAG=v1.0.0` in `.env` (defaults to `latest`).

### Build from source

Contributors who want to run their own changes can build the images locally instead of pulling them:

```bash
git clone https://github.com/christt105/cinegram.git
cd cinegram
cp .env.example .env   # then edit
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
```

## Environment variables

All configuration lives in `.env` (see `.env.example` for the template).

| Variable                | Description                                                                                     |
| ----------------------- | ----------------------------------------------------------------------------------------------- |
| `JELLYFIN_URL`          | Base URL of your Jellyfin server (e.g. `http://your-jellyfin-host:8096`). Read by the web container at start; if left empty the web falls back to the browser host on port 8096. |
| `JELLYFIN_TOKEN`        | Jellyfin API token used by the web client.                                                      |
| `TELEGRAM_API_ID`       | Telegram `api_id` from <https://my.telegram.org>.                                               |
| `TELEGRAM_API_HASH`     | Telegram `api_hash` from <https://my.telegram.org>.                                             |
| `TELEGRAM_BOT_TOKEN`    | Bot token from [@BotFather](https://t.me/BotFather).                                             |
| `TELEGRAM_AUTH_USER_ID` | Telegram user ID allowed to command the bot. Accepts a comma-separated list to authorize several users (e.g. `123,456`); the first ID is the owner whose chat stores the media. |
| `TMDB_API_KEY`          | TMDB API key used for metadata lookups.                                                          |
| `TMDB_CONTENT_LANGUAGE` | Language for titles and overviews (e.g. `en-US`, `es-ES`, `fr-FR`).                              |
| `IMPORT_MOVIES_DIR`     | Host path for the movies library, bind-mounted into `bot-net` at `/data/import/movies`.         |
| `IMPORT_SHOWS_DIR`      | Host path for the shows library, bind-mounted into `bot-net` at `/data/import/shows`.            |
| `PUID` / `PGID`         | User/group IDs the `backend` and `bot-net` containers run as, so they can write to the host media directories (defaults `1000:1000`). |
| `WEB_PORT`              | Host port for the web panel (defaults `5173`). Change it if the port is already in use.          |
| `BACKEND_PORT`          | Host port for the backend API (defaults `8005`). The web container reads it at start.           |
| `BOT_NET_PORT`          | Host port for the `bot-net` worker (defaults `8088`). The web container reads it at start.       |
| `CINEGRAM_TAG`          | Image tag to deploy (defaults `latest`; pin to a release like `v1.0.0`).                         |

## Service ports

| Service   | Host port               | Container port |
| --------- | ----------------------- | -------------- |
| `web`     | `${WEB_PORT:-5173}`     | `80`           |
| `backend` | `${BACKEND_PORT:-8005}` | `8000`         |
| `bot-net` | `${BOT_NET_PORT:-8088}` | `8080`         |

All three host ports are configurable in `.env`, so you can move any of them if it clashes with something else already running on the host. At container start the web injects `JELLYFIN_URL` / `JELLYFIN_TOKEN` / `BACKEND_PORT` / `BOT_NET_PORT` into the browser (via `/config.js`), so the app talks to the services on whatever ports you choose. After changing them, `docker compose up -d` is enough — no rebuild.

The `web` service is a static bundle served by nginx, with the runtime config written on startup by `web/docker-entrypoint.sh`. A `web/Dockerfile.dev` (Vite dev server with hot reload) is kept for local development.

## Security

Cinegram has **no built-in authentication** on the web panel or the backend API: anyone who can reach those ports can browse and modify the library. Only the Telegram bot is access-controlled (via `TELEGRAM_AUTH_USER_ID`). Do **not** expose the `web` or `backend` ports directly to the internet. Keep them on your LAN and reach them through a VPN, or put them behind a reverse proxy that adds authentication and TLS.

## Bot commands

Once the containers are up, control the worker from Telegram (as the user in `TELEGRAM_AUTH_USER_ID`):

| Command                    | Description                                                 |
| -------------------------- | ----------------------------------------------------------- |
| `/start`, `/help`          | Welcome message and list of available commands.             |
| `/health`                  | Report the bot's health.                                    |
| `/add [search query]`      | Search TMDB and add a movie or series to the database.      |
| `/import`                  | Import local Jellyfin media into Telegram and the database. |
| `/search <query>`          | Search the local library (accent-insensitive).              |
| `/movies`, `/series`       | List all registered movies / series.                        |
| `/movie <id\|tmdbid-id>`   | Show a movie's details.                                      |
| `/serie <series-id>`       | Show a series' details.                                      |
| `/orphans`                 | List collections still missing TMDB identification.         |
| `/queue`                   | Show active upload and download transfers.                  |

## Data & Persistence

All persistent application state is stored on the host under `./appdata`:
- `./appdata/backend`: SQLite database storing library metadata, task queues, and media indexes.
- `./appdata/bot-net`: Telegram session state and worker runtime cache.

Make sure to back up the `./appdata` directory when migrating servers or updating containers.

## System dependencies

`bot-net` shells out to `7z` (from `p7zip-full`) for multipart split/join and to `ffprobe` (from `ffmpeg`) for technical metadata. Both are installed inside the `bot-net` Docker image, so no extra host setup is required when running with Docker Compose. If you run the worker outside Docker, make sure `7z` and `ffprobe` are on your `PATH`.

## Troubleshooting

- **Permission errors writing to media folders**: Ensure `PUID` and `PGID` in `.env` match the Linux user/group that owns `IMPORT_MOVIES_DIR` and `IMPORT_SHOWS_DIR`.
- **Web UI settings or backend port updates not taking effect**: the web reads them at container start. Recreate the web container after changing `.env`:
  ```bash
  docker compose up -d web
  ```

## Disclaimer

Cinegram is an open-source tool built for personal media management and self-hosted server administration. It does not host, stream, or distribute copyright-protected material. Users are solely responsible for ensuring that their deployment and file transfers comply with applicable local laws, copyright regulations, and the Terms of Service of external services (such as Telegram and TMDB).

## Attribution

This product uses the TMDB API but is not endorsed or certified by TMDB.

## Support

If you find Cinegram useful and want to support its development, you can buy me a coffee!

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/christt105)

## Contributing

Contributions, bug reports, and feature requests are welcome. Feel free to open an issue or submit a pull request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
