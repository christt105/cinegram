from telethon import TelegramClient, events
from config import API_ID, API_HASH, BOT_TOKEN, AUTH_USER_ID

async def handle_new_message(event):
    await event.reply("Hola, soy un bot de Telegram. ¿En qué puedo ayudarte?")

async def main():
    await client.send_message(int(AUTH_USER_ID), "HOLA")

    await client.run_until_disconnected()

client = TelegramClient('anon', API_ID, API_HASH).start(bot_token=BOT_TOKEN)
client.add_event_handler(handle_new_message, events.NewMessage)
    
with client:
    client.loop.run_until_complete(main())