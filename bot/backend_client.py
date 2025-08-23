import aiohttp
from config import BACKEND_URL
from tasks.file_meta import FileMeta


class BackendClient:
    def __init__(self, base_url="http://backend:8000"):
        self.base_url = base_url
        self.session = None
    
    async def get_session(self):
        if self.session is None or self.session.closed:
            self.session = aiohttp.ClientSession()
        return self.session        

    async def health(self):
        url = f"{self.base_url}/"
        try:
            session = await self.get_session()
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
        session = await self.get_session()
        async with session.post(url, json=data) as resp:
            if resp.status != 200:
                text = await resp.text()
                raise RuntimeError(f"Upload failed: {resp.status} {text}")
            return await resp.json()

    async def get_movies(self):
        session = await self.get_session()
        async with session.get(f"{self.base_url}/movies") as resp:
            return await resp.json()
        
    async def get_movie(self, local_id: int):
        session = await self.get_session()
        async with session.get(f"{self.base_url}/movies/{local_id}") as resp:
            return await resp.json()
    
    async def search_movies(self, query: str):
        session = await self.get_session()
        async with session.get(f"{self.base_url}/movies/search", params={"q": query}) as resp:
            return await resp.json()
    
    async def get_movie_by_tmdb(self, tmdb_id: int):
        session = await self.get_session()
        async with session.get(f"{self.base_url}/movies/tmdb/{tmdb_id}") as resp:
            if resp.status == 404:
                return None
            return await resp.json()
    
    async def get_collections(self, movie_id: int):
        session = await self.get_session()
        async with session.get(f"{self.base_url}/movies/{movie_id}/collections") as resp:
            if resp.status == 404:
                return []
            return await resp.json()
        
    async def get_collection(self, collection_id: int):
        session = await self.get_session()
        async with session.get(f"{self.base_url}/collections/{collection_id}") as resp:
            if resp.status == 404:
                return None
            return await resp.json()
        
    async def get_file(self, file_id: int):
        session = await self.get_session()
        async with session.get(f"{self.base_url}/files/{file_id}") as resp:
            if resp.status == 404:
                return None
            return await resp.json()
    
    async def delete_file(self, file_id: int):
        session = await self.get_session()
        async with session.delete(f"{self.base_url}/files/{file_id}") as resp:
            if resp.status == 404:
                return False
            return resp.status == 200
    
    async def delete_collection(self, collection_id: int):
        session = await self.get_session()
        async with session.delete(f"{self.base_url}/collections/{collection_id}") as resp:
            if resp.status == 404:
                return False
            return resp.status == 200
