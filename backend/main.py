from datetime import datetime
from typing import List, Optional
from fastapi import FastAPI, Depends, BackgroundTasks, HTTPException
from pydantic import BaseModel
from sqlmodel import Session, select
from database import init_db, get_session
from crud import create_file, identify_collection, prune_orphaned_media
from logger import logger
from tmdb import TMDB
from models import Movie, File, Collection, Series, Season, Episode, DownloadTask, UploadTask
from fastapi.middleware.cors import CORSMiddleware
import os

app = FastAPI(title="jellygram-backend")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
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
    technical_metadata: str | None = None
class ItemOut(BaseModel):
    id: int
    message_id: int | None
    filename: str | None
    collection_id: int | None
    movie_id: int | None
    season_id: int | None
    episode_id: int | None

@app.get("/")
def root():
    return {"status": "ok", "message": "Backend is running"}

@app.post("/upload", response_model=ItemOut)
def upload_endpoint(payload: UploadIn, session: Session = Depends(get_session)):
    # create DB record with status pending
    file, collection = create_file(
        session,
        message_id=payload.message_id,
        filename=payload.filename,
        filesize=payload.filesize,
        mime_type=payload.mime_type,
        created_at=payload.created_at,
        tmdb_id=payload.tmdb_id,
        technical_metadata=payload.technical_metadata
    )

    if collection.movie_id is None and collection.episode_id is None and collection.season_id is None:
        identify_collection(
            session, collection.id, tmdb,
            forced_tmdb_id=payload.tmdb_id
        )
        session.refresh(collection)

    return {
        "id": file.id,
        "message_id": file.message_id,
        "filename": file.filename,
        "collection_id": collection.id,
        "movie_id": collection.movie_id,
        "season_id": collection.season_id,
        "episode_id": collection.episode_id
    }


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

class EpisodeOut(BaseModel):
    id: int
    episode_number: int
    title: Optional[str]
    collections: List[CollectionOut] = []

    class Config:
        from_attributes = True

class SeasonOut(BaseModel):
    id: int
    season_number: int
    episodes: List[EpisodeOut] = []
    collections: List[CollectionOut] = []

    class Config:
        from_attributes = True

class SeriesOut(BaseModel):
    id: int
    tmdb_id: Optional[int]
    manual_title: Optional[str]
    poster_path: Optional[str]
    overview: Optional[str]
    release_year: Optional[int]
    seasons: List[SeasonOut] = []

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
    """Return all movies stored in DB with collections and files, filtering out empty ones"""
    movies = session.exec(select(Movie)).all()
    return [m for m in movies if len(m.collections) > 0]

@app.get("/series", response_model=list[SeriesOut])
def list_series(session: Session = Depends(get_session)):
    """Return all series stored in DB with seasons, episodes, and collections, filtering out empty ones"""
    series = session.exec(select(Series)).all()
    result = []
    for s in series:
        has_files = False
        for season in s.seasons:
            if len(season.collections) > 0:
                has_files = True
                break
            for ep in season.episodes:
                if len(ep.collections) > 0:
                    has_files = True
                    break
        if has_files:
            result.append(s)
    return result

@app.delete("/movies/{movie_id}")
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

@app.delete("/series/{series_id}")
def delete_series(series_id: int, session: Session = Depends(get_session)):
    """Delete a series and its seasons/episodes structure (unlinks collections)"""
    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")
    for season in series.seasons:
        for episode in season.episodes:
            for collection in episode.collections:
                collection.episode_id = None
                session.add(collection)
            session.delete(episode)
        for collection in season.collections:
            collection.season_id = None
            session.add(collection)
        session.delete(season)
    session.delete(series)
    session.commit()
    prune_orphaned_media(session)
    return {"status": "ok", "message": f"Series {series_id} deleted"}

@app.delete("/collections/{collection_id}")
def delete_collection(collection_id: int, session: Session = Depends(get_session)):
    """Delete a collection and all associated files from DB"""
    collection = session.get(Collection, collection_id)
    if not collection:
        raise HTTPException(status_code=404, detail="Collection not found")
    for file in collection.files:
        session.delete(file)
    session.delete(collection)
    session.commit()
    prune_orphaned_media(session)
    return {"status": "ok", "message": f"Collection {collection_id} deleted"}

@app.get("/movies/{movie_id}", response_model=MovieOut)
def get_movie(movie_id: int, session: Session = Depends(get_session)):
    movie = session.get(Movie, movie_id)
    if not movie:
        raise HTTPException(status_code=404, detail="Movie not found")
    return movie

@app.get("/series/{series_id}", response_model=SeriesOut)
def get_series(series_id: int, session: Session = Depends(get_session)):
    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")
    return series
    
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

class IdentifyRequest(BaseModel):
    tmdb_id: int

@app.post("/collections/{collection_id}/identify", response_model=CollectionOut)
def identify_collection_endpoint(
    collection_id: int,
    request: IdentifyRequest,
    session: Session = Depends(get_session)
):
    db_collection = session.get(Collection, collection_id)
    if not db_collection:
        raise HTTPException(status_code=404, detail="Collection not found")
        
    identify_collection(session, collection_id, tmdb, forced_tmdb_id=request.tmdb_id)
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

def enqueue_collection_id(session: Session, collection_id: int):
    coll = session.get(Collection, collection_id)
    if not coll or len(coll.files) == 0:
        return False
    existing = session.exec(
        select(DownloadTask)
        .where(DownloadTask.collection_id == collection_id)
        .where(DownloadTask.status.in_(["pending", "downloading"]))
    ).first()
    if not existing:
        task = DownloadTask(collection_id=collection_id, status="pending", progress=0)
        session.add(task)
        session.commit()
        return True
    return False

@app.post("/downloads/enqueue/collection/{collection_id}")
def enqueue_collection_endpoint(collection_id: int, session: Session = Depends(get_session)):
    success = enqueue_collection_id(session, collection_id)
    if not success:
        return {"status": "ignored", "message": "Collection already in queue or empty"}
    return {"status": "ok", "message": "Enqueued collection"}

@app.post("/downloads/enqueue/series/{series_id}")
def enqueue_series_endpoint(series_id: int, session: Session = Depends(get_session)):
    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")
    count = 0
    for season in series.seasons:
        for ep in season.episodes:
            for coll in ep.collections:
                if enqueue_collection_id(session, coll.id):
                    count += 1
        for coll in season.collections:
            if enqueue_collection_id(session, coll.id):
                count += 1
    return {"status": "ok", "message": f"Enqueued {count} collections for series"}

@app.post("/downloads/enqueue/series/{series_id}/season/{season_number}")
def enqueue_season_endpoint(series_id: int, season_number: int, session: Session = Depends(get_session)):
    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")
    season = session.exec(
        select(Season)
        .where(Season.series_id == series_id)
        .where(Season.season_number == season_number)
    ).first()
    if not season:
        raise HTTPException(status_code=404, detail="Season not found")
    count = 0
    for ep in season.episodes:
        for coll in ep.collections:
            if enqueue_collection_id(session, coll.id):
                count += 1
    for coll in season.collections:
        if enqueue_collection_id(session, coll.id):
            count += 1
    return {"status": "ok", "message": f"Enqueued {count} collections for season"}

@app.post("/downloads/enqueue/series/{series_id}/season/{season_number}/episode/{episode_number}")
def enqueue_episode_endpoint(series_id: int, season_number: int, episode_number: int, session: Session = Depends(get_session)):
    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")
    season = session.exec(
        select(Season)
        .where(Season.series_id == series_id)
        .where(Season.season_number == season_number)
    ).first()
    if not season:
        raise HTTPException(status_code=404, detail="Season not found")
    episode = session.exec(
        select(Episode)
        .where(Episode.season_id == season.id)
        .where(Episode.episode_number == episode_number)
    ).first()
    if not episode:
        raise HTTPException(status_code=404, detail="Episode not found")
    count = 0
    for coll in episode.collections:
        if enqueue_collection_id(session, coll.id):
            count += 1
    if count == 0:
        return {"status": "ignored", "message": "Episode collection already enqueued or not found"}
    return {"status": "ok", "message": "Enqueued episode"}

@app.get("/downloads/pending")
def list_pending_downloads(session: Session = Depends(get_session)):
    tasks = session.exec(select(DownloadTask).where(DownloadTask.status == "pending")).all()
    result = []
    for t in tasks:
        coll = session.get(Collection, t.collection_id)
        if not coll:
            continue
        media_type = "movie"
        title = "Unknown"
        year = None
        season_num = None
        episode_num = None
        tmdb_id = None
        tvdb_id = None
        if coll.movie_id:
            movie = session.get(Movie, coll.movie_id)
            if movie:
                title = movie.title
                year = movie.release_year
                tmdb_id = movie.tmdb_id
        elif coll.episode_id:
            episode = session.get(Episode, coll.episode_id)
            if episode:
                media_type = "tv"
                episode_num = episode.episode_number
                season = session.get(Season, episode.season_id)
                if season:
                    season_num = season.season_number
                    series = session.get(Series, season.series_id)
                    if series:
                        title = series.manual_title
                        year = series.release_year
                        tmdb_id = series.tmdb_id
                        tvdb_id = series.tvdb_id
        elif coll.season_id:
            season = session.get(Season, coll.season_id)
            if season:
                media_type = "tv"
                season_num = season.season_number
                series = session.get(Series, season.series_id)
                if series:
                    title = series.manual_title
                    year = series.release_year
                    tmdb_id = series.tmdb_id
                    tvdb_id = series.tvdb_id
        result.append({
            "task_id": t.id,
            "collection_id": t.collection_id,
            "media_type": media_type,
            "title": title,
            "year": year,
            "season_number": season_num,
            "episode_number": episode_num,
            "quality": coll.quality or "1080p",
            "tmdb_id": tmdb_id,
            "tvdb_id": tvdb_id,
            "files": [
                {
                    "id": f.id,
                    "message_id": f.message_id,
                    "filename": f.filename,
                    "filesize": f.filesize
                }
                for f in coll.files
            ]
        })
    return result

class DownloadStatusIn(BaseModel):
    status: str
    progress: int
    error_message: Optional[str] = None

@app.post("/downloads/{task_id}/status")
def update_download_status(task_id: int, payload: DownloadStatusIn, session: Session = Depends(get_session)):
    task = session.get(DownloadTask, task_id)
    if not task:
        raise HTTPException(status_code=404, detail="Task not found")
    task.status = payload.status
    task.progress = payload.progress
    if payload.error_message:
        task.error_message = payload.error_message
    if payload.status in ["completed", "failed"]:
        task.completed_at = datetime.utcnow()
    session.add(task)
    session.commit()
    return {"status": "ok"}

class UploadEnqueueIn(BaseModel):
    jellyfin_id: str
    tmdb_id: Optional[int] = None
    media_type: str
    path: str
    title: str
    year: Optional[int] = None

@app.post("/uploads/enqueue")
def enqueue_upload(payload: UploadEnqueueIn, session: Session = Depends(get_session)):
    existing = session.exec(
        select(UploadTask)
        .where(UploadTask.jellyfin_id == payload.jellyfin_id)
        .where(UploadTask.status.in_(["pending", "uploading"]))
    ).first()
    if existing:
        return {"status": "ignored", "message": "Already in upload queue"}
    task = UploadTask(
        jellyfin_id=payload.jellyfin_id,
        tmdb_id=payload.tmdb_id,
        media_type=payload.media_type,
        path=payload.path,
        title=payload.title,
        year=payload.year,
        status="pending"
    )
    session.add(task)
    session.commit()
    return {"status": "ok"}

@app.get("/uploads/pending")
def list_pending_uploads(session: Session = Depends(get_session)):
    tasks = session.exec(select(UploadTask).where(UploadTask.status == "pending")).all()
    return tasks

@app.get("/uploads/queue")
def list_queue_uploads(session: Session = Depends(get_session)):
    tasks = session.exec(select(UploadTask).where(UploadTask.status.in_(["pending", "uploading", "failed"]))).all()
    return tasks

@app.get("/downloads/queue")
def list_queue_downloads(session: Session = Depends(get_session)):
    tasks = session.exec(select(DownloadTask).where(DownloadTask.status.in_(["pending", "downloading", "failed"]))).all()
    result = []
    for t in tasks:
        coll = session.get(Collection, t.collection_id)
        if not coll:
            continue
        title = coll.name or "Unknown"
        if coll.movie_id:
            movie = session.get(Movie, coll.movie_id)
            if movie:
                title = movie.title or movie.manual_title or title
        elif coll.episode_id:
            episode = session.get(Episode, coll.episode_id)
            if episode and episode.season_id:
                season = session.get(Season, episode.season_id)
                if season and season.series_id:
                    series = session.get(Series, season.series_id)
                    if series:
                        title = f"{series.manual_title or 'Unknown Series'} - S{season.season_number}E{episode.episode_number}"
        elif coll.season_id:
            season = session.get(Season, coll.season_id)
            if season and season.series_id:
                series = session.get(Series, season.series_id)
                if series:
                    title = f"{series.manual_title or 'Unknown Series'} - S{season.season_number}"

        result.append({
            "id": t.id,
            "collection_id": t.collection_id,
            "title": title,
            "status": t.status,
            "progress": t.progress,
            "error_message": t.error_message
        })
    return result

class UploadStatusIn(BaseModel):
    status: str
    progress: int
    error_message: Optional[str] = None

@app.post("/uploads/{task_id}/status")
def update_upload_status(task_id: int, payload: UploadStatusIn, session: Session = Depends(get_session)):
    task = session.get(UploadTask, task_id)
    if not task:
        raise HTTPException(status_code=404, detail="Upload task not found")
    task.status = payload.status
    task.progress = payload.progress
    if payload.error_message:
        task.error_message = payload.error_message
    if payload.status in ["completed", "failed"]:
        task.completed_at = datetime.utcnow()
    session.add(task)
    session.commit()
    return {"status": "ok"}

