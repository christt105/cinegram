# Cinegram

Cinegram is a self-hosted platform that bridges a [Jellyfin](https://jellyfin.org/)
media library with [Telegram](https://telegram.org/). It automates downloading
movies and series from Telegram chats/channels, processes and renames them to
Jellyfin's naming convention, and can also back up existing Jellyfin files the
other way around — up to Telegram.

It ships as three services orchestrated with Docker Compose, so a full deployment
is a single `docker compose up`.

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

The `bot-net` worker authenticates as a Telegram **bot**, which caps file
transfers at 2 GB; files larger than that are split into **1.95 GB** parts with
`7z` (store-only) on upload and rejoined on download.

## Prerequisites

- Docker and Docker Compose.
- A running Jellyfin server and an API token.
- Telegram API credentials (`api_id` / `api_hash` from <https://my.telegram.org>)
  and a bot token from [@BotFather](https://t.me/BotFather).
- A [TMDB](https://www.themoviedb.org/settings/api) API key.

## Quick start

1. Clone the repository.
2. Copy the environment template and fill in your own values:
   ```bash
   cp .env.example .env
   # edit .env
   ```
3. Point `IMPORT_MOVIES_DIR` and `IMPORT_SHOWS_DIR` at the host directories where
   Jellyfin expects movies and shows (these are bind-mounted into `bot-net`).
4. Start the stack:
   ```bash
   docker compose up -d --build
   ```
5. Open the web panel at `http://<host>:5173`.

## Environment variables

All configuration lives in `.env` (see `.env.example` for the template).

| Variable                | Description                                                                                     |
| ----------------------- | ----------------------------------------------------------------------------------------------- |
| `JELLYFIN_URL`          | Base URL of your Jellyfin server (e.g. `http://your-jellyfin-host:8096`). Consumed by the web build as `VITE_JELLYFIN_URL`; if left empty the web falls back to the browser host on port 8096. |
| `JELLYFIN_TOKEN`        | Jellyfin API token used by the web client.                                                      |
| `TELEGRAM_API_ID`       | Telegram `api_id` from <https://my.telegram.org>.                                               |
| `TELEGRAM_API_HASH`     | Telegram `api_hash` from <https://my.telegram.org>.                                             |
| `TELEGRAM_BOT_TOKEN`    | Bot token from [@BotFather](https://t.me/BotFather).                                             |
| `TELEGRAM_AUTH_USER_ID` | Telegram user ID allowed to command the bot.                                                     |
| `TMDB_API_KEY`          | TMDB API key used for metadata lookups.                                                          |
| `TMDB_CONTENT_LANGUAGE` | Language for titles and overviews (e.g. `en-US`, `es-ES`, `fr-FR`).                              |
| `IMPORT_MOVIES_DIR`     | Host path for the movies library, bind-mounted into `bot-net` at `/data/import/movies`.         |
| `IMPORT_SHOWS_DIR`      | Host path for the shows library, bind-mounted into `bot-net` at `/data/import/shows`.            |
| `PUID` / `PGID`         | User/group IDs the `backend` and `bot-net` containers run as, so they can write to the host media directories (defaults `1000:1000`). |
| `WEB_PORT`              | Host port for the web panel (defaults `5173`). Change it if the port is already in use.          |
| `BACKEND_PORT`          | Host port for the backend API (defaults `8005`). The web reads it as `VITE_BACKEND_PORT`.        |
| `BOT_NET_PORT`          | Host port for the `bot-net` worker (defaults `8088`). The web reads it as `VITE_BOT_NET_PORT`.   |

## Service ports

| Service   | Host port               | Container port |
| --------- | ----------------------- | -------------- |
| `web`     | `${WEB_PORT:-5173}`     | `5173`         |
| `backend` | `${BACKEND_PORT:-8005}` | `8000`         |
| `bot-net` | `${BOT_NET_PORT:-8088}` | `8080`         |

All three host ports are configurable in `.env`, so you can move any of them if
it clashes with something else already running on the host. The web panel picks
up `BACKEND_PORT` / `BOT_NET_PORT` automatically (injected as `VITE_BACKEND_PORT`
/ `VITE_BOT_NET_PORT`), so the browser talks to the services on whatever ports
you choose.

## Bot commands

Once the containers are up, control the worker from Telegram (as the user in
`TELEGRAM_AUTH_USER_ID`):

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

## System dependencies

`bot-net` shells out to `7z` (from `p7zip-full`) for multipart split/join and to
`ffprobe` (from `ffmpeg`) for technical metadata. Both are installed inside the
`bot-net` Docker image, so no extra host setup is required when running with
Docker Compose. If you run the worker outside Docker, make sure `7z` and
`ffprobe` are on your `PATH`.
