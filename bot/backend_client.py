import aiohttp
from config import BACKEND_URL
from tasks.file_meta import FileMeta


class BackendClient:
    def __init__(self):
        self.base_url = BACKEND_URL

    async def health(self):
        url = f"{self.base_url}/"
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(url) as resp:
                    return await resp.json()
        except aiohttp.ClientError as e:
            return {"status": "unhealthy", "error": str(e)}

    async def upload(self, file_meta: FileMeta):
        url = f"{self.base_url}/upload"
        data = {
            "message_id": file_meta.message_id,
            "filename": file_meta.filename,
            "filesize": file_meta.filesize,
            "mime_type": file_meta.mime_type,
            "created_at": file_meta.created_at.isoformat(),
        }
        async with aiohttp.ClientSession() as session:
            async with session.post(url, json=data) as resp:
                if resp.status != 200:
                    text = await resp.text()
                    raise RuntimeError(f"Upload failed: {resp.status} {text}")
                return await resp.json()

    async def list_items(self, limit: int = 100):
        url = f"{self.base_url}/items?limit={limit}"
        async with aiohttp.ClientSession() as session:
            async with session.get(url) as resp:
                return await resp.json()

    async def get_item(self, item_id: int):
        url = f"{self.base_url}/items/{item_id}"
        async with aiohttp.ClientSession() as session:
            async with session.get(url) as resp:
                if resp.status == 404:
                    return None
                return await resp.json()

    async def patch_item(self, item_id: int, **kwargs):
        url = f"{self.base_url}/items/{item_id}"
        data = {k: v for k, v in kwargs.items() if v is not None}
        async with aiohttp.ClientSession() as session:
            async with session.patch(url, json=data) as resp:
                if resp.status == 404:
                    return None
                return await resp.json()
