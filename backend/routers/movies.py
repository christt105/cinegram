from typing import List, Optional
from fastapi import APIRouter, Depends, HTTPException
from sqlmodel import Session, select
from database import get_session
from models import Movie
from schemas import MovieOut, MovieUpdate, CollectionOut
from crud import prune_orphaned_media
from tmdb import TMDB
import tmdbsimple as tmdb_simple

router = APIRouter(prefix="/movies", tags=["movies"])
tmdb = TMDB()

@router.get("", response_model=List[MovieOut])
def list_movies(session: Session = Depends(get_session)):
    """Return all movies stored in DB"""
    return session.exec(select(Movie)).all()

@router.get("/{movie_id}", response_model=MovieOut)
def get_movie(movie_id: int, session: Session = Depends(get_session)):
    movie = session.get(Movie, movie_id)
    if not movie:
        raise HTTPException(status_code=404, detail="Movie not found")
    return movie

@router.delete("/{movie_id}")
def delete_movie(movie_id: int, session: Session = Depends(get_session)):
    """Delete a movie and unlink its collections"""
    movie = session.get(Movie, movie_id)
    if not movie:
        raise HTTPException(status_code=404, detail="Movie not found")
    for collection in movie.collections:
        collection.movie_id = None
        session.add(collection)
    session.delete(movie)
    session.commit()
    prune_orphaned_media(session)
    return {"status": "ok", "message": f"Movie {movie_id} deleted"}

@router.patch("/{movie_id}")
def update_movie(movie_id: int, request: MovieUpdate, session: Session = Depends(get_session)):
    movie = session.get(Movie, movie_id)
    if not movie:
        raise HTTPException(status_code=404, detail="Movie not found")
    
    update_data = request.dict(exclude_unset=True)
    for key, value in update_data.items():
        setattr(movie, key, value)
        
    session.add(movie)
    session.commit()
    session.refresh(movie)
    return movie

@router.get("/{movie_id}/posters")
def get_movie_posters(movie_id: int, session: Session = Depends(get_session)):
    movie = session.get(Movie, movie_id)
    if not movie or not movie.tmdb_id:
        return []
    try:
        images = tmdb_simple.Movies(movie.tmdb_id).images()
        posters = [p["file_path"] for p in images.get("posters", []) if "file_path" in p]
        return posters
    except Exception as e:
        return []

@router.get("/tmdb/{tmdb_id}", response_model=Optional[MovieOut])
def get_movie_by_tmdb(tmdb_id: int, session: Session = Depends(get_session)):
    """Return a single movie by TMDB ID with its collections and files"""
    movie = session.exec(select(Movie).where(Movie.tmdb_id == tmdb_id)).first()
    if not movie:
        return None
    return movie

@router.get("/{movie_id}/collections", response_model=List[CollectionOut])
def get_collections(movie_id: int, session: Session = Depends(get_session)):
    """Return all collections for a given movie ID with their files"""
    movie = session.get(Movie, movie_id)
    if not movie:
        raise HTTPException(status_code=404, detail="Movie not found")
    return movie.collections

@router.post("/{movie_id}/reidentify")
def reidentify_movie(movie_id: int, new_tmdb_id: int, session: Session = Depends(get_session)):
    from crud import propagate_identification
    
    movie = session.get(Movie, movie_id)
    if not movie:
        raise HTTPException(status_code=404, detail="Movie not found")
        
    tmdb_result = tmdb.identify_by_tmdbid(new_tmdb_id, "movie")
    if not tmdb_result:
        raise HTTPException(status_code=404, detail="New TMDB ID not found on TMDB")
        
    existing_movie = session.exec(select(Movie).where(Movie.tmdb_id == new_tmdb_id)).first()
    
    if existing_movie:
        for collection in movie.collections:
            collection.movie_id = existing_movie.id
            session.add(collection)
        session.delete(movie)
        session.commit()
        target_movie = existing_movie
    else:
        release_date = tmdb_result.get("release_date")
        release_year = int(release_date.split("-")[0]) if release_date else None
        
        movie.tmdb_id = new_tmdb_id
        movie.title = tmdb_result.get("title") or tmdb_result.get("original_title")
        movie.poster_path = tmdb_result.get("poster_path")
        movie.overview = tmdb_result.get("overview")
        movie.release_year = release_year
        
        session.add(movie)
        session.commit()
        session.refresh(movie)
        target_movie = movie
        
    for collection in target_movie.collections:
        parsed = TMDB.clean_filename(collection.name)
        propagate_identification(session, parsed["clean_name"], tmdb)
        
    prune_orphaned_media(session)
    return {"status": "ok", "message": f"Movie re-identified to {target_movie.title}"}
