import os
import httpx
from typing import Optional, Dict, Any

TMDB_API_KEY = os.getenv("TMDB_API_KEY")

async def identify_by_filename(filename: str) -> Optional[Dict[str, Any]]:
    """
    Very naive identification: call TMDb search/movie and search/tv by filename text.
    This is a stub/example — you should refine parsing and queries.
    """
    if not TMDB_API_KEY:
        return None

    q = filename.rsplit(".", 1)[0]
    async with httpx.AsyncClient(timeout=10) as client:
        # try movies
        resp = await client.get(
            "https://api.themoviedb.org/3/search/movie",
            params={"api_key": TMDB_API_KEY, "query": q},
        )
        if resp.status_code == 200:
            data = resp.json()
            results = data.get("results") or []
            if results:
                top = results[0]
                return {
                    "media_type": "movie",
                    "title": top.get("title"),
                    "year": (top.get("release_date") or "").split("-")[0] or None,
                    "tmdb_id": top.get("id"),
                }
        # try tv
        resp = await client.get(
            "https://api.themoviedb.org/3/search/tv",
            params={"api_key": TMDB_API_KEY, "query": q},
        )
        if resp.status_code == 200:
            data = resp.json()
            results = data.get("results") or []
            if results:
                top = results[0]
                return {
                    "media_type": "tv",
                    "title": top.get("name"),
                    "year": (top.get("first_air_date") or "").split("-")[0] or None,
                    "tmdb_id": top.get("id"),
                }
    return None
