import asyncio
from logger import logger
import os
from telethon import Button, TelegramClient, events, __version__ as telethon_version
from tasks.file_meta import FileMeta
from backend_client import BackendClient
from tasks.upload_file_task import UploadFileTask
from worker import Worker
from commands import COMMANDS
from config import API_ID, API_HASH, BOT_TOKEN, AUTH_USER_ID

INCOMING_DIR = "/data/incoming"

class TelegramBot:
    def __init__(self):
        self.client = TelegramClient('bot_session', API_ID, API_HASH).start(bot_token=BOT_TOKEN)
        self.client.add_event_handler(self.handle_new_message, events.NewMessage)
        self.client.add_event_handler(self.menu_handler, events.CallbackQuery)

        logger.info("Starting Telegram Bot...")

        os.makedirs(INCOMING_DIR, exist_ok=True)

        self.queue = asyncio.Queue()
        self.backend = BackendClient()
        self.worker = Worker(self.queue)

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
    
    async def menu_handler(self, event):
        data = event.data.decode()
        action, obj_id = data.split(":")
        obj_id = int(obj_id)
    
        if action == "movie_collections":
            collections = await self.backend.get_collections(obj_id)
            if not collections:
                await event.edit("📭 No collections found.")
                return
    
            buttons = [
                [Button.inline(f"{c['name']} (ID {c['id']})", data=f"collection:{c['id']}")]
                for c in collections
            ]
            buttons.append([Button.inline("⬅️ Back", data=f"back_to_movie:{obj_id}")])
    
            await event.edit("📂 Select a collection:", buttons=buttons)
    
        elif action == "collection":
            collection_id = obj_id
            collection = await self.backend.get_collection(collection_id)
            if not collection:
                await event.edit("❌ Collection not found.")
                return
        
            # Listamos los archivos
            files = collection.get("files", [])
            files_text = "\n".join([f"{i+1}. {f['filename']} ({f['filesize']} bytes)" for i, f in enumerate(files)]) or "No files"
        
            caption = f"<b>{collection['name']}</b>\nFiles:\n{files_text}"
        
            buttons = [
                [Button.inline("✏️ Edit Collection", data=f"edit_collection:{collection_id}")],
                [Button.inline("⬅️ Back to movie", data=f"movie_collections:{collection.get('movie_id', 0)}")]
            ]
        
            await event.edit(caption, parse_mode="html", buttons=buttons)

        elif action.startswith("edit"):
            await event.edit("✏️ Editing feature not implemented yet")

        elif action.startswith("back_to_movie"):
            movie_id = obj_id
            movie = await self.backend.get_movie(movie_id)
            #await send_movie(bot, event, movie, bot.backend)


    async def handle_new_message(self, event):
        if event.sender_id != int(AUTH_USER_ID):
            return
        
        if event.message.file:
            message_id = event.message.id
            extension = event.message.file.ext
            file_name = event.message.file.name
            file_mime_type = event.message.file.mime_type
            file_size = event.message.file.size
            date = event.message.date
            
            message = (
                f"📥 New file received:\n"
                f"ID: {message_id}\n"
                f"Name: {file_name}\n"
                f"Size: {file_size} bytes\n"
                f"Extension: {extension}\n"
                f"Type: {file_mime_type}\n"
                f"Date: {date.strftime('%Y-%m-%d %H:%M:%S')}\n"
            )
            
            logger.info(message)
            
            async def reply_callback(result):
                await event.reply(
                    f"File processed successfully:\nID: {result['id']}\nName: {result['filename']}\nCollection ID: {result.get('collection_id', 'N/A')}"
                    if result else "No result returned."
                )
            
            await self.queue.put(UploadFileTask(
                FileMeta(message_id, file_name, file_size, file_mime_type, date),
                self.backend,
                reply_callback
                ))

        elif event.message.text and event.message.text.startswith("/"):
            parts = event.message.text.strip().split()
            command_name = parts[0].lower()
            args = parts[1:]

            cmd = COMMANDS.get(command_name)
            if cmd:
                await cmd.handler(self, event, args, self.backend)
            else:
                await event.reply("❌ Unknown command. Use /help to see available commands.")
