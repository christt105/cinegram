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
        self.pending_action = None

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
                caption=f"{PREFIX_MOVIE}{movie['title']} ({movie.get("release_year")})",
                parse_mode="html",
            )

        await event.respond(caption, parse_mode="html", buttons=buttons)
    
    def get_movie_caption(self, movie):
        caption = (
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
        
        return caption, buttons
    
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
                msg_text += f"{i}. {c['name']} (ID {c['id']}) - Quality: {c.get('quality','N/A')} - Files: {len(c.get('files', []))}\n"

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

            movie_id = collection["movie_id"]
            season_id = collection["season_id"]
            episode_id = collection["episode_id"]

            linked_to = "None"
            if movie_id:
                movie = await self.backend.get_movie(movie_id)
                linked_to = f"Movie: [{movie["id"]}] {movie["title"]}"
            elif season_id:
                season = await self.backend.get_season(season_id)
                linked_to = f"Season: [{season["id"]}] S{season["season_number"]}"
            elif episode_id:
                episode = await self.backend.get_episode(episode_id)
                linked_to = f"Episode: [{episode["id"]}] S{episode["season_id"]}E{episode["epidose_number"]}"


            tags = ", ".join(
                f"#{t.strip()}"
                for t in (collection.get("tags") or "").split(",")
                if t.strip()
            ) or "N/A"

            caption = (
                f"ID: {collection['id']}\n"
                f"Quality: {collection.get('quality','N/A')}\n"
                f"Audio Languaje: {collection.get("audio_languages","N/A")}\n"
                f"Subtitle Languaje: {collection.get("subtitle_languages","N/A")}\n"
                f"Tags: {tags}\n"
                f"Notes: <blockquote>{collection.get("notes","N/A")}</blockquote>\n"
                f"<b>Files:</b>\n{files_text}"
            )

            buttons = [
                [Button.inline("✏️ Edit Collection", data=f"edit_collection:{collection_id}")],
                [Button.inline("🗑️ Delete Collection", data=f"delete_collection:{collection_id}")],
            ]

            # Botones para cada archivo de la página
            for i, f in enumerate(files):
                buttons.append([Button.inline(f"{i+1}", data=f"file:{f['id']}")])

            # Back button
            buttons.append([Button.inline("⬅️ Back to collections", data=f"movie_collections:{collection.get('movie_id', 0)}")])

            await event.edit(caption, parse_mode="html", buttons=buttons)
        
        elif action == "edit_collection":
            collection_id = obj_id
            collection = await self.backend.get_collection(collection_id)
            
            if not collection:
                await event.edit("❌ Collection not found.")
                return

            movie_id = collection["movie_id"]
            season_id = collection["season_id"]
            episode_id = collection["episode_id"]

            linked_to = "None"
            if movie_id:
                movie = await self.backend.get_movie(movie_id)
                linked_to = f"Movie: [{movie["id"]}] {movie["title"]}"
            elif season_id:
                season = await self.backend.get_season(season_id)
                linked_to = f"Season: [{season["id"]}] S{season["season_number"]}"
            elif episode_id:
                episode = await self.backend.get_episode(episode_id)
                linked_to = f"Episode: [{episode["id"]}] S{episode["season_id"]}E{episode["epidose_number"]}"

            tags = ", ".join(
                f"#{t.strip()}"
                for t in (collection.get("tags") or "").split(",")
                if t.strip()
            ) or "N/A"
            
            details = [
                f"{collection["name"]}",
                f"ID: {collection['id']}",
                f"Quality: {collection.get('quality','N/A')}",
                f"Audio Language: {collection.get('audio_languages','N/A')}",
                f"Subtitle Language: {collection.get('subtitle_languages','N/A')}",
                f"Tags: {tags}",
                f"Linked to:\n   {linked_to}\n"
                f"Notes: {collection.get('notes','N/A')}",
            ]

            caption = "\n".join(details)

            buttons = [
                [Button.inline("Edit Name", data=f"edit_collection_name:{collection_id}")],
                [Button.inline("Edit Quality", data=f"edit_collection_quality:{collection_id}")],
                [Button.inline("Edit Audio Language", data=f"edit_collection_audio:{collection_id}")],
                [Button.inline("Edit Subtitle Language", data=f"edit_collection_sub:{collection_id}")],
                [Button.inline("Edit Tags", data=f"edit_collection_tags:{collection_id}")],
                [Button.inline("Edit Notes", data=f"edit_collection_notes:{collection_id}")],
            ]

            buttons.append([Button.inline("⬅️ Back to collection", data=f"collection:{collection_id}")])

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

            if self.pending_action and self.pending_action["action"] == "change_collection_in_file":
                message_id = self.pending_action.get("message_id")
                message = await self.client.get_messages(event.chat_id, ids=message_id)
                await message.delete()
                self.pending_action = None

            collection = await self.backend.get_collection(file.get("collection_id", 0))
            caption = f"Collection: {collection['name'] if collection else 'N/A'}"
            
            if file:
                caption += f"\n\n<b>File Details:</b>\nID: {file['id']}\nFilename: {file['filename']}\nSize: {self.human_readable_size(file['filesize'])}\nCreated At: {file['created_at']}"
                buttons.append([Button.inline("🗑️ Delete File", data=f"delete_file:{file_id}")])
            else:
                caption += "\n\n❌ File not found."

            buttons.append([Button.inline("Change Collection", data=f"file_change:{file_id}")])
            buttons.append([Button.inline("⬅️ Back to collection", data=f"collection:{file.get('collection_id', 0)}")])

            await event.edit(caption, parse_mode="html", buttons=buttons)
        
        elif action == "file_change":
            file_id = obj_id

            file = await self.backend.get_file(file_id)

            msg = await event.get_message()
            await event.edit(msg.text)

            buttons = [[Button.inline("Cancel", data=f"file:{file_id}")]]
            new_message = await event.respond(
                f"📂 Change collection for file ID {file_id}.\n\n"
                "➡️ Send me the ID of the target collection, or type `/newcol <name>` to create a new one.",
                buttons=buttons
            )
            
            self.pending_action = {
                "action": "change_collection_in_file", 
                "file_id": file_id,
                "original_collection": file.get("collection_id") if file else None,
                "message_id": new_message.id
                }

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
                await event.edit(f"✅ File ID {file_id} deleted successfully.", buttons=buttons)
            else:
                await event.edit(f"❌ Failed to delete file ID {file_id}.", buttons=buttons)
        
    def set_action(self, action_key: str, data: dict, callback_success, callback_cancel):
        if self.pending_action != None:
            callback_cancel()

        self.pending_action = {
                "action": action_key, 
                "data": data
                }

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

        elif event.message.text:
            if event.message.text.startswith("/"):
                parts = event.message.text.strip().split()
                command_name = parts[0].lower()
                args = parts[1:]

                cmd = COMMANDS.get(command_name)
                if cmd:
                    await cmd.handler(self, event, args, self.backend)
                else:
                    await event.reply("❌ Unknown command. Use /help to see available commands.")
            else:
                if self.pending_action != None:
                    action = self.pending_action.get("action")
                    if action == "change_collection_in_file":
                        text = event.message.text
                        file_id = self.pending_action.get("file_id")
                        if text.isdigit():
                            collection_id = int(text)

                            collection = await self.backend.get_collection(collection_id)

                            if not collection:
                                await event.respond(f"Collection with ID {collection_id} does not exists.")
                            else:
                                success, status = await self.backend.patch_file(file_id, {"collection_id": collection_id})
                                if status == 200:
                                    await event.respond(f"✅ File {file_id} moved to collection ID {collection_id}.")
                                    self.pending_action = None
                                else:
                                    await event.respond(f"❌ Failed to move file {file_id} to collection {collection_id}. Error: {success}")
                                
                        else:
                            await event.respond("❌ Invalid input. Send a numeric collection ID or `/newcol <name>`. Type `/cancel action` to cancel the action.")
    
    async def cancel_current_action(self, event):
        if self.pending_action != None:
            await event.reply("No active action to cancel")
            return
        
        key_action = self.pending_action["key"]
        self.pending_action = None
        await event.reply(f"Current action with key: {key_action} cancelled")

                        