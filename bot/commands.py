from telethon.tl.custom.message import Message
from backend_client import BackendClient

async def cmd_start(bot, event: Message, args: list[str], backend: BackendClient):
    await event.reply("👋 Hello! I'm your Media Library bot. Send me a file or use /help for commands.")

async def cmd_help(bot, event: Message, args: list[str], backend: BackendClient):
    await event.reply(
        "Available commands:\n"
        "/start - Welcome message\n"
        "/help - Show this help\n"
        "/ping - Check if bot is alive\n"
        "/download <id> - Download a saved file\n"
    )

async def cmd_health(bot, event: Message, args: list[str], backend: BackendClient):
    health = await backend.health()
    await event.reply(f"{health['status']}")

async def cmd_download(bot, event: Message, args: list[str], backend: BackendClient):
    if not args:
        await event.reply("⚠️ Usage: /download <message_id>")
        return

    try:
        msg_id = int(args[0])
        msg = await bot.client.get_messages(event.chat_id, ids=msg_id)
        if msg and msg.file:
            await msg.download_media("downloads/")
            await event.reply(f"✅ File {msg.file.name} downloaded.")
        else:
            await event.reply("❌ No file found for that ID.")
    except ValueError:
        await event.reply("⚠️ Message ID must be a number.")


# Command registry
COMMANDS = {
    "/start": cmd_start,
    "/help": cmd_help,
    "/health": cmd_health,
    "/download": cmd_download,
}
