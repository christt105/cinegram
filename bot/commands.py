from telethon.tl.custom.message import Message

async def cmd_start(bot, event: Message, args: list[str]):
    await event.reply("👋 Hello! I’m your bot. Send me a file or use /help for commands.")

async def cmd_help(bot, event: Message, args: list[str]):
    await event.reply(
        "Available commands:\n"
        "/start - Welcome message\n"
        "/help - Show this help\n"
        "/ping - Check if bot is alive\n"
        "/download <id> - Download a saved file\n"
    )

async def cmd_ping(bot, event: Message, args: list[str]):
    await event.reply("🏓 Pong!")

async def cmd_download(bot, event: Message, args: list[str]):
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
    "/ping": cmd_ping,
    "/download": cmd_download,
}
