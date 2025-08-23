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
SESSION_FILE = "/data/bot_session.session"

PREFIX_MOVIE = "🎬" # emoji movie 
PREFIX_TV = "📺"    # emoji tv 

class TelegramBot:
    def __init__(self):
        os.makedirs(INCOMING_DIR, exist_ok=True)
        self.queue = asyncio.Queue()
        self.backend = BackendClient()
        self.worker = Worker(self.queue)

        self.client = TelegramClient(SESSION_FILE, API_ID, API_HASH)
        self.client.add_event_handler(self.handle_new_message, events.NewMessage)
        self.client.add_event_handler(self.menu_handler, events.CallbackQuery)

    async def start(self):
        """Start the bot and the worker"""
        await self.client.start(bot_token=BOT_TOKEN)
        logger.info(f"Bot started. Telethon version: {telethon_version}")

        await self.send_startup_message(AUTH_USER_ID)

        asyncio.create_task(self.worker.run())

        await self.client.run_until_disconnected()
    
    async def send_startup_message(self, user_id):
        message = (
            f"Bot started successfully!\n"
            f"Telethon version: {telethon_version}"
        )
        await self.client.send_message(int(user_id), message)
    
    async def send_movie_menu(self, event, movie):
        """Shows a movie with its menu."""
        caption, buttons = self.get_movie_caption(movie)

        if movie.get("poster_path"):
            poster_url = f"https://image.tmdb.org/t/p/w500{movie['poster_path']}"
            await self.client.send_file(
                event.chat_id,
                poster_url,
                caption=PREFIX_MOVIE + movie['title'],
            )
            await event.respond(caption, parse_mode="html", buttons=buttons)
        else:
            await event.edit(caption, parse_mode="html", buttons=buttons)
    
    def get_movie_caption(self, movie):
        caption = (
                f"<b>{movie['title']}</b>\n\n"
                f"Local ID: {movie['id']}\n"
                f"TMDB ID: " + (
                    f"<a href='https://www.themoviedb.org/movie/{movie['tmdb_id']}'>{movie['tmdb_id']}</a>\n"
                    if movie.get("tmdb_id") else "N/A\n"
                ) + 
                f"Collections: {len(movie.get('collections', []))}"
            )

        buttons = [
                [Button.inline("📂 Collections", data=f"movie_collections:{movie['id']}")],
                [Button.inline("⬇️ Download", data=f"download_movie:{movie['id']}")],
                [Button.inline("✏️ Edit Movie", data=f"edit_movie:{movie['id']}")]
            ]
        
        return caption,buttons
    
    # TODO: Reestructurar toda esta mierda, que asco de python
    def human_readable_size(self, size: int) -> str:
        """Convert bytes to human-readable format"""
        for unit in ['B', 'KB', 'MB', 'GB', 'TB']:
            if size < 1024:
                return f"{size:.1f}{unit}"
            size /= 1024
        return f"{size:.1f}PB"

    async def menu_handler(self, event):
        ITEMS_PER_PAGE = 10

        data = event.data.decode()
        action, *params = data.split(":")

        page = 0
        if len(params) > 1:
            obj_id, page = int(params[0]), int(params[1])
        else:
            obj_id = int(params[0])
        
        logger.info(f"Callback data received: {data} (action: {action}, obj_id: {obj_id}, page: {page})")

        if action == "movie":
            movie = await self.backend.get_movie(obj_id)
            caption, buttons = self.get_movie_caption(movie)

            await event.edit(caption, parse_mode="html", buttons=buttons)

        # -------------------------
        # Mostrar colecciones de una película
        # -------------------------
        elif action.startswith("movie_collections"):
            parts = data.split(":")
            obj_id = int(parts[1])
            page = int(parts[2]) if len(parts) > 2 else 0

            collections = await self.backend.get_collections(obj_id)

            start = page * ITEMS_PER_PAGE
            end = start + ITEMS_PER_PAGE
            page_collections = collections[start:end]

            # Mensaje con lista numerada
            msg_text = f"Select a collection:\n\n"
            for i, c in enumerate(page_collections, start=1 + start):
                msg_text += f"{i}. {c['name']} (ID {c['id']})\n"

            # Botones solo con números
            buttons = [[Button.inline(str(i), data=f"collection:{collections[i-1]["id"]}")] for i in range(start+1, min(end, len(collections))+1)]

            # Back to movie
            buttons.append([Button.inline("Create new Collection"), Button.inline("⬅️ Back to movie", data=f"movie:{obj_id}")])

            await event.edit(msg_text, buttons=buttons)

        # -------------------------
        # Mostrar archivos dentro de una colección
        # -------------------------
        elif action == "collection":
            collection_id = obj_id
            collection = await self.backend.get_collection(collection_id)
            if not collection:
                await event.edit("❌ Collection not found.")
                return

            files = collection.get("files", [])
            files_text = f"\n".join([
                f"{i+1}. {f['filename']} ({self.human_readable_size(f['filesize'])})"
                for i, f in enumerate(files)
            ]) or "No files"

            caption = f"<b>{collection['name']}</b>\nFiles:\n{files_text}"

            buttons = [
                [Button.inline("✏️ Edit Collection", data=f"edit_collection:{collection_id}")],
                # Remove collection button
                [Button.inline("🗑️ Delete Collection", data=f"delete_collection:{collection_id}")],
            ]

            # Botones para cada archivo de la página
            for i, f in enumerate(files):
                buttons.append([Button.inline(f"{i+1}", data=f"file:{f['id']}")])

            # Back button
            buttons.append([Button.inline("⬅️ Back to collections", data=f"movie_collections:{collection.get('movie_id', 0)}")])

            await event.edit(caption, parse_mode="html", buttons=buttons)
        
        elif action == "delete_collection":
            collection_id = obj_id
            collection = await self.backend.get_collection(collection_id)
            if not collection:
                await event.edit("❌ Collection not found.")
                return
            
            await event.edit(f"⚠️ Are you sure you want to delete collection '{collection['name']}' (ID {collection_id})?", buttons=[
                [Button.inline("✅ Yes, delete", data=f"confirm_delete_collection:{collection_id}")],
                [Button.inline("❌ Cancel", data=f"collection:{collection_id}")]
            ])
        
        elif action == "confirm_delete_collection":
            collection_id = obj_id
            collection = await self.backend.get_collection(collection_id)
            if not collection:
                await event.edit("❌ Collection not found.")
                return
            
            result = await self.backend.delete_collection(collection_id)
            buttons = []
            if collection.get("movie_id"):
                buttons.append([Button.inline("⬅️ Back to collections", data=f"movie_collections:{collection['movie_id']}")])

            if result:
                await event.edit(f"✅ Collection '{collection['name']}' (ID {collection_id}) deleted successfully.", buttons=buttons)
            else:
                await event.edit(f"❌ Failed to delete collection '{collection['name']}' (ID {collection_id}).", buttons=buttons)

        # -------------------------
        # Selección de archivo individual
        # -------------------------
        elif action == "file":
            file_id = obj_id
            file = await self.backend.get_file(file_id)

            buttons = [
            ]

            collection = await self.backend.get_collection(file.get("collection_id", 0))
            movie = await self.backend.get_movie(collection.get("movie_id", 0)) if collection else None
            caption = f"{movie['title'] if movie else 'N/A'}\nCollection: {collection['name'] if collection else 'N/A'}"
            
            if file:
                caption += f"\n\n<b>File Details:</b>\nID: {file['id']}\nFilename: {file['filename']}\nSize: {self.human_readable_size(file['filesize'])}\nCreated At: {file['created_at']}"
                buttons.append([Button.inline("🗑️ Delete File", data=f"delete_file:{file_id}")])
            else:
                caption += "\n\n❌ File not found."

            buttons.append([Button.inline("⬅️ Back to collection", data=f"collection:{file.get('collection_id', 0)}")])

            await event.edit(caption, parse_mode="html", buttons=buttons)
        
        elif action == "delete_file":
            file_id = obj_id

            #ask for confirmation
            await event.edit(f"⚠️ Are you sure you want to delete file ID {file_id}?", buttons=[
                [Button.inline("✅ Yes, delete", data=f"confirm_delete_file:{file_id}")],
                [Button.inline("❌ Cancel", data=f"file:{file_id}")]
            ])

        elif action == "confirm_delete_file":
            file_id = obj_id
            file = await self.backend.get_file(file_id)
            result = await self.backend.delete_file(file_id)

            buttons = [
                [Button.inline("⬅️ Back to collection", data=f"collection:{file.get('collection_id', 0)}")]
            ] if file else []

            if result:
                await event.edit(f"✅ File ID {file_id} deleted successfully.")
            else:
                await event.edit(f"❌ Failed to delete file ID {file_id}.")

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
