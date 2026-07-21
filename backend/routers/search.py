from typing import List
from fastapi import APIRouter, Query
from tmdb import TMDB

router = APIRouter(prefix="/search", tags=["search"])
tmdb = TMDB()

@router.get("", response_model=List[dict])
def search_media(
    q: str = Query(..., min_length=1, description="Search query string or TMDB ID"),
    type: str = Query("multi", enum=["multi", "movie", "tv"], description="Media type to search")
):
    """Search movies or series on TMDB by title or numeric TMDB ID."""
    return tmdb.search(q, media_type=type)
