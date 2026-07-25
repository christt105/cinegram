from typing import List
from fastapi import APIRouter, Query
from tmdb import TMDB

router = APIRouter(prefix="/tmdb", tags=["tmdb"])
tmdb = TMDB()

@router.get("/search", response_model=List[dict])
def search_media(
    query: str = Query(..., min_length=1, description="Search query string or TMDB ID"),
    media_type: str = Query("multi", enum=["multi", "movie", "tv"], description="Media type to search")
):
    """Search movies or series on TMDB by title or numeric TMDB ID."""
    return tmdb.search(query, media_type=media_type)
