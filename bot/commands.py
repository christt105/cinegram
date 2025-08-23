from dataclasses import dataclass
from typing import Callable, Awaitable
from telethon import Button
from telethon.tl.custom.message import Message
from backend_client import BackendClient
from logger import logger


@dataclass
class Command:
    name: str
    description: str
    usage: str
    handler: Callable[..., Awaitable[None]]


# ======================
# Command Handlers
# ======================

async def cmd_start(bot, event: Message, args: list[str], backend: BackendClient):
    await event.reply("👋 Hello! I'm your Media Library bot. Send me a file or use /help for commands.")


async def cmd_help(bot, event: Message, args: list[str], backend: BackendClient):
    msg = "📖 Available commands:\n\n"
    for cmd in COMMANDS.values():
        msg += f"{cmd.name} - {cmd.description}\nUsage: {cmd.usage}\n\n"
    await event.reply(msg)


async def cmd_health(bot, event: Message, args: list[str], backend: BackendClient):
    health = await backend.health()
    await event.reply(f"✅ Backend status: {health['status']}")


async def cmd_movies(bot, event: Message, args: list[str], backend: BackendClient):
    movies = await backend.get_movies()
    if not movies:
        await event.reply("📭 No movies found.")
        return

    text = "🎬 Movies:\n"
    for m in movies[:10]:
        text += f"- {m['id']}: {m['title']} ({m.get('tmdb_id','?')})\n"
    if len(movies) > 10:
        text += f"\n... and {len(movies) - 10} more"

    await event.reply(text)


async def cmd_movie(bot, event, args, backend):
    if not args:
        await event.reply("⚠️ Usage: /movie <local_id> | tmdbid-<tmdb_id>")
        return

    query = args[0]
    if query.startswith("tmdbid-"):
        tmdb_id = int(query.split("-", 1)[1])
        movie = await backend.get_movie_by_tmdb(tmdb_id)
    else:
        try:
            local_id = int(query)
            movie = await backend.get_movie(local_id)
        except ValueError:
            await event.reply("⚠️ Invalid ID format.")
            return

    if not movie:
        await event.reply("❌ Movie not found.")
        return

    await bot.send_movie_menu(event, movie)

# ======================
# Command Registry
# ======================

COMMANDS: dict[str, Command] = {
    "/start": Command(
        name="/start",
        description="Welcome message",
        usage="/start",
        handler=cmd_start,
    ),
    "/help": Command(
        name="/help",
        description="Show available commands",
        usage="/help",
        handler=cmd_help,
    ),
    "/health": Command(
        name="/health",
        description="Check backend health",
        usage="/health",
        handler=cmd_health,
    ),
    "/movies": Command(
        name="/movies",
        description="List stored movies",
        usage="/movies",
        handler=cmd_movies,
    ),
    "/movie": Command(
        name="/movie",
        description="Get a specific movie by ID",
        usage="/movies <id> | tmdbid-<tmdb_id>",
        handler=cmd_movie,
    ),
}
