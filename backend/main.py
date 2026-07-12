from datetime import datetime
from typing import List, Optional
from fastapi import FastAPI, Depends, BackgroundTasks, HTTPException
from pydantic import BaseModel
from sqlmodel import Session, select
from database import init_db, get_session
from crud import create_file, identify_collection
from logger import logger
from tmdb import TMDB
from models import Movie, File, Collection
import os

app = FastAPI(title="jellygram-backend")
tmdb = TMDB()

# Initialize DB on startup
@app.on_event("startup")
def on_startup():
    os.makedirs(os.getenv("DATA_DIR", "/data"), exist_ok=True)
    init_db()

# Pydantic input model for /upload
class UploadIn(BaseModel):
    message_id: int
    filename: str
    filesize: int | None = None
    mime_type: str | None = None
    created_at: str | None = None  # ISO format date string
    tmdb_id: int | None = None     # Pre-identified TMDB ID (skips async search when provided)

# Response model (simple)
class ItemOut(BaseModel):
    id: int
    message_id: int | None
    filename: str | None
    collection_id: int | None

@app.get("/")
def root():
    return {"status": "ok", "message": "Backend is running"}

@app.post("/upload", response_model=ItemOut)
async def upload_endpoint(payload: UploadIn, background_tasks: BackgroundTasks, session: Session = Depends(get_session)):
    # create DB record with status pending
    file, collection = create_file(
        session,
        message_id=payload.message_id,
        filename=payload.filename,
        filesize=payload.filesize,
        mime_type=payload.mime_type,
        created_at=payload.created_at,
    )

    if collection.movie_id is None and collection.episode_id is None:
        background_tasks.add_task(
            identify_collection, collection.id, tmdb,
            forced_tmdb_id=payload.tmdb_id
        )

    return file


class FileOut(BaseModel):
    id: int
    message_id: int
    filename: str
    filesize: int
    mime_type: Optional[str]
    created_at: datetime
    collection_id: int

    class Config:
        from_attributes = True

class CollectionOut(BaseModel):
    id: int
    name: Optional[str]
    movie_id: Optional[int]
    episode_id: Optional[int]
    season_id: Optional[int]
    files: List[FileOut] = []
    quality: Optional[str]
    audio_languages: Optional[str]
    subtitle_languages: Optional[str]
    tags: Optional[str]
    notes: Optional[str]

    class Config:
        from_attributes = True

class MovieOut(BaseModel):
    id: int
    tmdb_id: Optional[int]
    title: Optional[str]
    poster_path: Optional[str]
    collections: List[CollectionOut] = []
    release_year: Optional[int]
    overview: Optional[str]
    tags: Optional[str]
    notes: Optional[str]

    class Config:
        from_attributes = True
    

@app.get("/movies", response_model=list[MovieOut])
def list_movies(session: Session = Depends(get_session)):
    """Return all movies stored in DB with collections and files"""
    movies = session.exec(select(Movie)).all()
    return movies

@app.get("/movies/{movie_id}", response_model=MovieOut)
def get_movie(movie_id: int, session: Session = Depends(get_session)):
    """Return a single movie by ID with its collections and files"""
    movie = session.get(Movie, movie_id)
    if not movie:
        raise HTTPException(status_code=404, detail="Movie not found")
    return movie
    
@app.get("/movies/tmdb/{tmdb_id}", response_model=Optional[MovieOut])
def get_movie_by_tmdb(tmdb_id: int, session: Session = Depends(get_session)):
    """Return a single movie by TMDB ID with its collections and files"""
    movie = session.exec(select(Movie).where(Movie.tmdb_id == tmdb_id)).first()
    if not movie:
        return None
    return movie

@app.get("/movies/{movie_id}/collections", response_model=List[CollectionOut])
def get_collections(movie_id: int, session: Session = Depends(get_session)):
    """Return all collections for a given movie ID with their files"""
    movie = session.get(Movie, movie_id)
    if not movie:
        raise HTTPException(status_code=404, detail="Movie not found")
    return movie.collections

@app.get("/collections/{collection_id}", response_model=Optional[CollectionOut])
def get_collection(collection_id: int, session: Session = Depends(get_session)):
    """Return a single collection by ID with its files"""
    collection = session.get(Collection, collection_id)
    if not collection:
        return None
    return collection

@app.get("/files/{file_id}", response_model=Optional[FileOut])
def get_file(file_id: int, session: Session = Depends(get_session)):
    """Return a single file by ID"""
    file = session.get(File, file_id)
    if not file:
        return None
    return file

@app.delete("/files/{file_id}", response_model=dict)
def delete_file(file_id: int, session: Session = Depends(get_session)):
    """Delete a file record by ID"""
    file = session.get(File, file_id)
    if not file:
        raise HTTPException(status_code=404, detail="File not found")
    session.delete(file)
    session.commit()
    return {"status": "deleted", "file_id": file_id}

@app.delete("/collections/{collection_id}", response_model=dict)
def delete_collection(collection_id: int, session: Session = Depends(get_session)):
    """Delete a collection and its associated files by ID"""
    collection = session.get(Collection, collection_id)
    if not collection:
        raise HTTPException(status_code=404, detail="Collection not found")
    # Optionally, delete associated files
    for file in collection.files:
        session.delete(file)
    session.delete(collection)
    session.commit()
    return {"status": "deleted", "collection_id": collection_id}
    
class CollectionUpdate(BaseModel):
    name: Optional[str] = None
    quality: Optional[str] = None
    audio_languages: Optional[str] = None
    subtitle_languages: Optional[str] = None
    tags: Optional[str] = None
    notes: Optional[str] = None

@app.patch("/collections/{collection_id}", response_model=Collection)
def update_collection(
    collection_id: int,
    collection_update: CollectionUpdate,
    session: Session = Depends(get_session),
):
    db_collection = session.get(Collection, collection_id)
    if not db_collection:
        raise HTTPException(status_code=404, detail="Collection not found")

    update_data = collection_update.dict(exclude_unset=True)
    for key, value in update_data.items():
        setattr(db_collection, key, value)

    session.add(db_collection)
    session.commit()
    session.refresh(db_collection)
    return db_collection

class CollectionCreate(BaseModel):
    name: str
    movie_id: Optional[int] = None
    season_id: Optional[int] = None
    episode_id: Optional[int] = None
    quality: Optional[str] = None
    audio_languages: Optional[str] = None
    subtitle_languages: Optional[str] = None
    tags: Optional[str] = None
    notes: Optional[str] = None

@app.post("/collections", response_model=CollectionOut)
def create_collection(
    collection_in: CollectionCreate,
    session: Session = Depends(get_session),
):
    """Create a new collection"""
    new_collection = Collection(
        name=collection_in.name,
        movie_id=collection_in.movie_id,
        season_id=collection_in.season_id,
        episode_id=collection_in.episode_id,
        quality=collection_in.quality,
        audio_languages=collection_in.audio_languages,
        subtitle_languages=collection_in.subtitle_languages,
        tags=collection_in.tags,
        notes=collection_in.notes,
    )

    session.add(new_collection)
    session.commit()
    session.refresh(new_collection)
    return new_collection


class FileUpdate(BaseModel):
    collection_id: Optional[int] = None

@app.patch("/files/{file_id}", response_model=File)
def update_file(
    file_id: int,
    file_update: FileUpdate,
    session: Session = Depends(get_session),
):
    db_file = session.get(File, file_id)
    if not db_file:
        raise HTTPException(status_code=404, detail="File not found")

    update_data = file_update.dict(exclude_unset=True)
    for key, value in update_data.items():
        setattr(db_file, key, value)

    session.add(db_file)
    session.commit()
    session.refresh(db_file)
    return db_file

