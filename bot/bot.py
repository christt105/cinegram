import asyncio
import logging
import os
from telethon import TelegramClient, events, __version__ as telethon_version
from uploader import Uploader
from worker import FileWorker
import commands
from config import API_ID, API_HASH, BOT_TOKEN, AUTH_USER_ID

INCOMING_DIR = "/data/incoming"

class TelegramBot:
    def __init__(self):
        self.client = TelegramClient('bot_session', API_ID, API_HASH).start(bot_token=BOT_TOKEN)
        self.client.add_event_handler(self.handle_new_message, events.NewMessage)

        os.makedirs(INCOMING_DIR, exist_ok=True)

        self.queue = asyncio.Queue()
        self.uploader = Uploader()
        self.worker = FileWorker(self.queue, self.uploader)

    async def start(self):
        await self.send_startup_message(AUTH_USER_ID)

        self.client.loop.create_task(self.worker.run())
        await self.client.run_until_disconnected()

    def run(self):
        with self.client:
            self.client.loop.run_until_complete(self.start())
    
    async def send_startup_message(self, user_id):
        message = (
            f"Bot started successfully!\n"
            f"Telethon version: {telethon_version}"
        )
        await self.client.send_message(int(user_id), message)

    async def handle_new_message(self, event):
        if event.sender_id != int(AUTH_USER_ID):
            return
        
        if event.message.file:
            message_id = event.message.id
            extension = event.message.file.ext
            file_name = event.message.file.name
            file_mime_type = event.message.file.mime_type
            file_size = event.message.file.size
            
            await self.queue.put({"message_id": message_id})
            await event.reply(f"✅ File reference saved")

        if event.message.text and event.message.text.startswith("/"):
            parts = event.message.text.strip().split()
            command = parts[0].lower()
            args = parts[1:]

            handler = commands.COMMANDS.get(command)
            if handler:
                await handler(self, event, args)
            else:
                await event.reply("❓ Unknown command. Use /help to see available commands.")

