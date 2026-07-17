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
import tmdbsimple as tmdb_simple

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
    technical_metadata: Optional[str] = None

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
    created_at: Optional[datetime] = None

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
    created_at: Optional[datetime] = None

    class Config:
        from_attributes = True
    

@app.get("/movies", response_model=list[MovieOut])
def list_movies(session: Session = Depends(get_session)):
    """Return all movies stored in DB"""
    return session.exec(select(Movie)).all()

@app.get("/series", response_model=list[SeriesOut])
def list_series(session: Session = Depends(get_session)):
    """Return all series stored in DB"""
    return session.exec(select(Series)).all()

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

@app.post("/series/{series_id}/reidentify")
def reidentify_series(series_id: int, new_tmdb_id: int, session: Session = Depends(get_session)):
    import tmdbsimple as tmdb_simple
    from crud import get_or_create_season, get_or_create_episode, propagate_identification
    
    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")
        
    tmdb_result = tmdb.identify_by_tmdbid(new_tmdb_id, "tv")
    if not tmdb_result:
        raise HTTPException(status_code=404, detail="New TMDB ID not found on TMDB")
        
    # Check if a series with this TMDB ID already exists
    existing_series = session.exec(select(Series).where(Series.tmdb_id == new_tmdb_id)).first()
    
    collections_to_remap = []
    for season in series.seasons:
        for collection in season.collections:
            collections_to_remap.append({
                "collection_id": collection.id,
                "season_num": season.season_number,
                "episode_num": None
            })
        for episode in season.episodes:
            for collection in episode.collections:
                collections_to_remap.append({
                    "collection_id": collection.id,
                    "season_num": season.season_number,
                    "episode_num": episode.episode_number
                })
                
    if existing_series:
        target_series_id = existing_series.id
    else:
        first_air = tmdb_result.get("first_air_date")
        release_year = int(first_air.split("-")[0]) if first_air else None
        
        tvdb_id = None
        try:
            external_ids = tmdb_simple.TV(new_tmdb_id).external_ids()
            tvdb_id = external_ids.get("tvdb_id")
        except Exception as e:
            logger.error(f"Error fetching TVDB ID: {e}")
            
        series.tmdb_id = new_tmdb_id
        series.manual_title = tmdb_result.get("name") or tmdb_result.get("original_name")
        series.poster_path = tmdb_result.get("poster_path")
        series.overview = tmdb_result.get("overview")
        series.release_year = release_year
        if tvdb_id:
            series.tvdb_id = tvdb_id
            
        session.add(series)
        session.commit()
        session.refresh(series)
        
        target_series_id = series.id

    if existing_series:
        for season in series.seasons:
            for episode in season.episodes:
                session.delete(episode)
            session.delete(season)
        session.delete(series)
    else:
        for season in series.seasons:
            for episode in season.episodes:
                session.delete(episode)
            session.delete(season)
            
    session.commit()
    
    for item in collections_to_remap:
        collection = session.get(Collection, item["collection_id"])
        if not collection:
            continue
        season_num = item["season_num"]
        episode_num = item["episode_num"]
        
        season_obj = get_or_create_season(session, target_series_id, season_num)
        if episode_num is not None:
            episode_obj = get_or_create_episode(session, season_obj.id, episode_num)
            collection.episode_id = episode_obj.id
            collection.season_id = None
        else:
            collection.season_id = season_obj.id
            collection.episode_id = None
        session.add(collection)
        
    session.commit()
    
    for item in collections_to_remap:
        collection = session.get(Collection, item["collection_id"])
        if collection:
            parsed = TMDB.clean_filename(collection.name)
            propagate_identification(session, parsed["clean_name"], tmdb)
            
    prune_orphaned_media(session)
    series_name = existing_series.manual_title if existing_series else series.manual_title
    return {"status": "ok", "message": f"Series re-identified to {series_name}"}

@app.post("/movies/{movie_id}/reidentify")
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

@app.get("/maintenance/orphans")
def get_orphans(session: Session = Depends(get_session)):
    orphaned_cols = session.exec(select(Collection).where(
        Collection.movie_id == None,
        Collection.season_id == None,
        Collection.episode_id == None
    )).all()
    
    result = []
    for col in orphaned_cols:
        result.append({
            "id": col.id,
            "name": col.name,
            "quality": col.quality,
            "files_count": len(col.files),
            "files": [{"id": f.id, "filename": f.filename, "filesize": f.filesize} for f in col.files]
        })
    return result

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

class MovieUpdate(BaseModel):
    title: Optional[str] = None
    poster_path: Optional[str] = None
    overview: Optional[str] = None
    release_year: Optional[int] = None

class SeriesUpdate(BaseModel):
    manual_title: Optional[str] = None
    poster_path: Optional[str] = None
    overview: Optional[str] = None
    release_year: Optional[int] = None

@app.patch("/movies/{movie_id}")
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

@app.patch("/series/{series_id}")
def update_series(series_id: int, request: SeriesUpdate, session: Session = Depends(get_session)):
    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")
        
    update_data = request.dict(exclude_unset=True)
    for key, value in update_data.items():
        setattr(series, key, value)
        
    session.add(series)
    session.commit()
    session.refresh(series)
    return series

@app.get("/movies/{movie_id}/posters")
def get_movie_posters(movie_id: int, session: Session = Depends(get_session)):
    movie = session.get(Movie, movie_id)
    if not movie or not movie.tmdb_id:
        return []
    try:
        images = tmdb_simple.Movies(movie.tmdb_id).images()
        posters = [p["file_path"] for p in images.get("posters", []) if "file_path" in p]
        return posters
    except Exception as e:
        print(f"Error fetching movie posters: {e}")
        return []

@app.get("/series/{series_id}/posters")
def get_series_posters(series_id: int, session: Session = Depends(get_session)):
    series = session.get(Series, series_id)
    if not series or not series.tmdb_id:
        return []
    try:
        images = tmdb_simple.TV(series.tmdb_id).images()
        posters = [p["file_path"] for p in images.get("posters", []) if "file_path" in p]
        return posters
    except Exception as e:
        print(f"Error fetching series posters: {e}")
        return []
@app.get("/movies/tmdb/{tmdb_id}", response_model=Optional[MovieOut])
def get_movie_by_tmdb(tmdb_id: int, session: Session = Depends(get_session)):
    """Return a single movie by TMDB ID with its collections and files"""
    movie = session.exec(select(Movie).where(Movie.tmdb_id == tmdb_id)).first()
    if not movie:
        return None
    return movie

@app.get("/series/tmdb/{tmdb_id}", response_model=Optional[SeriesOut])
def get_series_by_tmdb(tmdb_id: int, session: Session = Depends(get_session)):
    """Return a single series by TMDB ID with its seasons and episodes"""
    series = session.exec(select(Series).where(Series.tmdb_id == tmdb_id)).first()
    if not series:
        return None
    return series

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
    
    # New fields to change season/episode association within the series
    season_number: Optional[int] = None
    episode_number: Optional[int] = None
    clear_episode: Optional[bool] = None

@app.patch("/collections/{collection_id}", response_model=Collection)
def update_collection(
    collection_id: int,
    collection_update: CollectionUpdate,
    session: Session = Depends(get_session),
):
    db_collection = session.get(Collection, collection_id)
    if not db_collection:
        raise HTTPException(status_code=404, detail="Collection not found")

    # Handle season_number / episode_number changes if the collection is part of a series
    series_id = None
    if db_collection.season_id:
        season = session.get(Season, db_collection.season_id)
        if season:
            series_id = season.series_id
    elif db_collection.episode_id:
        episode = session.get(Episode, db_collection.episode_id)
        if episode and episode.season:
            series_id = episode.season.series_id

    if series_id is not None and collection_update.season_number is not None:
        # Resolve target season
        target_season = session.exec(select(Season).where(
            Season.series_id == series_id,
            Season.season_number == collection_update.season_number
        )).first()
        if not target_season:
            target_season = Season(series_id=series_id, season_number=collection_update.season_number)
            session.add(target_season)
            session.commit()
            session.refresh(target_season)
            
        # Decide episode and season association
        if collection_update.clear_episode:
            db_collection.season_id = target_season.id
            db_collection.episode_id = None
        elif collection_update.episode_number is not None:
            # Find or create episode
            target_ep = session.exec(select(Episode).where(
                Episode.season_id == target_season.id,
                Episode.episode_number == collection_update.episode_number
            )).first()
            if not target_ep:
                target_ep = Episode(season_id=target_season.id, episode_number=collection_update.episode_number)
                session.add(target_ep)
                session.commit()
                session.refresh(target_ep)
            db_collection.episode_id = target_ep.id
            db_collection.season_id = None  # Clear season pack association since it's an episode collection
        else:
            # If they just changed the season, we can keep the same episode number if they had one
            if db_collection.episode_id:
                old_ep = session.get(Episode, db_collection.episode_id)
                if old_ep:
                    target_ep = session.exec(select(Episode).where(
                        Episode.season_id == target_season.id,
                        Episode.episode_number == old_ep.episode_number
                    )).first()
                    if not target_ep:
                        target_ep = Episode(season_id=target_season.id, episode_number=old_ep.episode_number)
                        session.add(target_ep)
                        session.commit()
                        session.refresh(target_ep)
                    db_collection.episode_id = target_ep.id
                    db_collection.season_id = None  # Clear season pack association since it's an episode collection
            else:
                db_collection.season_id = target_season.id

    update_data = collection_update.dict(exclude_unset=True)
    # Exclude the virtual parameters from model fields assignment
    for virtual_field in ["season_number", "episode_number", "clear_episode"]:
        update_data.pop(virtual_field, None)

    for key, value in update_data.items():
        setattr(db_collection, key, value)

    session.add(db_collection)
    session.commit()
    prune_orphaned_media(session)
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

class ReidentifyCollectionRequest(BaseModel):
    tmdb_id: int | None = None

@app.post("/collections/{collection_id}/reidentify", response_model=CollectionOut)
def reidentify_collection_endpoint(
    collection_id: int,
    request: ReidentifyCollectionRequest,
    session: Session = Depends(get_session)
):
    db_collection = session.get(Collection, collection_id)
    if not db_collection:
        raise HTTPException(status_code=404, detail="Collection not found")

    # Clear existing identification so the guard in identify_collection is bypassed.
    db_collection.movie_id = None
    db_collection.season_id = None
    db_collection.episode_id = None
    session.add(db_collection)
    session.commit()

    identify_collection(session, collection_id, tmdb, forced_tmdb_id=request.tmdb_id)
    session.refresh(db_collection)

    return db_collection

class BatchIdentifyRequest(BaseModel):
    collection_ids: List[int]
    tmdb_id: int

@app.post("/maintenance/identify-batch")
def identify_batch(request: BatchIdentifyRequest, session: Session = Depends(get_session)):
    from crud import identify_collection
    results = []
    for col_id in request.collection_ids:
        try:
            identify_collection(session, col_id, tmdb, forced_tmdb_id=request.tmdb_id)
            results.append({"collection_id": col_id, "status": "success"})
        except Exception as e:
            logger.error(f"Failed to identify collection {col_id} in batch: {e}")
            results.append({"collection_id": col_id, "status": "error", "error": str(e)})
    session.commit()
    return {"status": "ok", "results": results}

class BatchDeleteRequest(BaseModel):
    collection_ids: List[int]

@app.post("/maintenance/delete-batch")
def delete_batch(request: BatchDeleteRequest, session: Session = Depends(get_session)):
    for col_id in request.collection_ids:
        col = session.get(Collection, col_id)
        if col:
            for file in col.files:
                session.delete(file)
            session.delete(col)
    session.commit()
    prune_orphaned_media(session)
    return {"status": "ok"}

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

@app.delete("/uploads/{task_id}")
def delete_upload_task(task_id: int, session: Session = Depends(get_session)):
    task = session.get(UploadTask, task_id)
    if not task:
        raise HTTPException(404, "Task not found")
    session.delete(task)
    session.commit()
    return {"status": "ok"}


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

@app.delete("/downloads/{task_id}")
def delete_download_task(task_id: int, session: Session = Depends(get_session)):
    task = session.get(DownloadTask, task_id)
    if not task:
        raise HTTPException(404, "Task not found")
    session.delete(task)
    session.commit()
    return {"status": "ok"}

@app.get("/tmdb/search")
def search_tmdb_endpoint(query: str, media_type: str = "multi"):
    if not query:
        raise HTTPException(status_code=400, detail="Query cannot be empty")
    return tmdb.search(query=query, media_type=media_type)

class CreateManualMediaRequest(BaseModel):
    tmdb_id: int
    media_type: str  # "movie" or "tv"

@app.post("/maintenance/create-media")
def create_manual_media(request: CreateManualMediaRequest, session: Session = Depends(get_session)):
    from crud import get_or_create_movie, get_or_create_series
    
    # Fetch TMDB details
    tmdb_result = tmdb.identify_by_tmdbid(request.tmdb_id, request.media_type)
    if not tmdb_result:
        raise HTTPException(status_code=404, detail="TMDB item not found")
        
    if request.media_type == "movie":
        movie = get_or_create_movie(session, tmdb_result)
        return {"status": "success", "type": "movie", "id": movie.id}
    elif request.media_type == "tv":
        series = get_or_create_series(session, tmdb_result)
        return {"status": "success", "type": "series", "id": series.id}
    else:
        raise HTTPException(status_code=400, detail="Invalid media type")

@app.post("/downloads/{task_id}/retry")
def retry_download_task(task_id: int, session: Session = Depends(get_session)):
    task = session.get(DownloadTask, task_id)
    if not task:
        raise HTTPException(status_code=404, detail="Download task not found")
    task.status = "pending"
    task.progress = 0
    task.error_message = None
    session.add(task)
    session.commit()
    return {"status": "ok"}

@app.post("/uploads/{task_id}/retry")
def retry_upload_task(task_id: int, session: Session = Depends(get_session)):
    task = session.get(UploadTask, task_id)
    if not task:
        raise HTTPException(status_code=404, detail="Upload task not found")
    task.status = "pending"
    task.progress = 0
    task.error_message = None
    session.add(task)
    session.commit()
    return {"status": "ok"}


# ========================
# Telegram preview helpers
# ========================

def _format_size(bytes_val: Optional[int]) -> str:
    if bytes_val is None:
        return "?"
    if bytes_val > 1024 ** 3:
        return f"{bytes_val / 1024**3:.2f} GB"
    if bytes_val > 1024 ** 2:
        return f"{bytes_val / 1024**2:.2f} MB"
    if bytes_val > 1024:
        return f"{bytes_val / 1024:.2f} KB"
    return f"{bytes_val} B"


def _build_collection_preview_text(collection: Collection, media_title: str, tmdb_url: Optional[str] = None) -> str:
    """Build a rich HTML preview message for a collection."""
    title_line = f"<b>{media_title}</b>"
    if tmdb_url:
        title_line += f'  •  <a href="{tmdb_url}">TMDB</a>'

    lines = [title_line, f"<b>Collection:</b> {collection.name or '-'}"]

    quality_parts = []
    if collection.quality:
        quality_parts.append(f"🎞 {collection.quality}")
    if collection.audio_languages:
        quality_parts.append(f"🔊 {collection.audio_languages}")
    if collection.subtitle_languages:
        quality_parts.append(f"💬 {collection.subtitle_languages}")
    if quality_parts:
        lines.append("  |  ".join(quality_parts))

    if collection.tags:
        lines.append(f"<b>Tags:</b> {collection.tags}")
    if collection.notes:
        lines.append(f"<b>Notes:</b> {collection.notes}")
    if collection.technical_metadata:
        lines.append(f"<b>Technical:</b> <code>{collection.technical_metadata}</code>")

    lines.append("")
    lines.append(f"<b>Files: {len(collection.files)}</b>")
    for i, f in enumerate(collection.files, 1):
        lines.append(f"{i}. {f.filename}  <i>({_format_size(f.filesize)})</i>")

    return "\n".join(lines)


def _send_telegram_message(bot_token: str, chat_id: str, text: str, photo_url: Optional[str] = None):
    """Send a message (optionally with photo) via the Telegram Bot API."""
    import urllib.request
    import urllib.parse
    import json

    base = f"https://api.telegram.org/bot{bot_token}"

    if photo_url:
        params = urllib.parse.urlencode({
            "chat_id": chat_id,
            "photo": photo_url,
            "caption": text,
            "parse_mode": "HTML"
        }).encode()
        req = urllib.request.Request(f"{base}/sendPhoto", data=params)
    else:
        params = urllib.parse.urlencode({
            "chat_id": chat_id,
            "text": text,
            "parse_mode": "HTML",
            "disable_web_page_preview": "true"
        }).encode()
        req = urllib.request.Request(f"{base}/sendMessage", data=params)

    with urllib.request.urlopen(req, timeout=10) as resp:
        return json.loads(resp.read())


@app.post("/collections/{collection_id}/send-preview")
def send_collection_preview(collection_id: int, session: Session = Depends(get_session)):
    """Send a Telegram preview message for a movie collection."""
    bot_token = os.getenv("TELEGRAM_BOT_TOKEN")
    chat_id = os.getenv("TELEGRAM_AUTH_USER_ID")
    if not bot_token or not chat_id:
        raise HTTPException(status_code=500, detail="Telegram credentials not configured")

    collection = session.get(Collection, collection_id)
    if not collection:
        raise HTTPException(status_code=404, detail="Collection not found")

    # Determine media title and poster
    media_title = collection.name or "Unknown"
    poster_url = None
    tmdb_url = None

    if collection.movie_id:
        movie = session.get(Movie, collection.movie_id)
        if movie:
            media_title = f"🎬 {movie.title} ({movie.release_year})"
            if movie.tmdb_id:
                tmdb_url = f"https://www.themoviedb.org/movie/{movie.tmdb_id}"
            if movie.poster_path:
                poster_url = f"https://image.tmdb.org/t/p/w500{movie.poster_path}"
    elif collection.season_id:
        season = session.get(Season, collection.season_id)
        if season:
            series = session.get(Series, season.series_id)
            if series:
                media_title = f"📺 {series.manual_title} ({series.release_year}) — Season {season.season_number}"
                if series.tmdb_id:
                    tmdb_url = f"https://www.themoviedb.org/tv/{series.tmdb_id}"
                if series.poster_path:
                    poster_url = f"https://image.tmdb.org/t/p/w500{series.poster_path}"

    text = _build_collection_preview_text(collection, media_title, tmdb_url)

    try:
        _send_telegram_message(bot_token, chat_id, text, poster_url)
        return {"status": "ok", "message": "Preview sent to Telegram"}
    except Exception as e:
        logger.error(f"Failed to send Telegram preview: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to send: {str(e)}")


@app.post("/series/{series_id}/season/{season_number}/send-preview")
def send_season_preview(series_id: int, season_number: int, session: Session = Depends(get_session)):
    """Send a Telegram preview message for all files in a season."""
    bot_token = os.getenv("TELEGRAM_BOT_TOKEN")
    chat_id = os.getenv("TELEGRAM_AUTH_USER_ID")
    if not bot_token or not chat_id:
        raise HTTPException(status_code=500, detail="Telegram credentials not configured")

    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")

    season = session.exec(
        select(Season).where(Season.series_id == series_id).where(Season.season_number == season_number)
    ).first()
    if not season:
        raise HTTPException(status_code=404, detail="Season not found")

    # Gather all collections for this season
    all_collections = list(season.collections or [])
    for ep in (season.episodes or []):
        all_collections.extend(ep.collections or [])

    if not all_collections:
        raise HTTPException(status_code=404, detail="No collections found for this season")

    title = f"📺 {series.manual_title} ({series.release_year}) — Season {season.season_number}"
    tmdb_url = f"https://www.themoviedb.org/tv/{series.tmdb_id}" if series.tmdb_id else None
    poster_url = f"https://image.tmdb.org/t/p/w500{series.poster_path}" if series.poster_path else None

    title_line = f"<b>{title}</b>"
    if tmdb_url:
        title_line += f'  •  <a href="{tmdb_url}">TMDB</a>'

    # Gather all file counts and unique qualities
    total_files = sum(len(c.files) for c in all_collections)
    quality_parts_set = set()
    for c in all_collections:
        parts = []
        if c.quality: parts.append(f"🎞 {c.quality}")
        if c.audio_languages: parts.append(f"🔊 {c.audio_languages}")
        if c.subtitle_languages: parts.append(f"💬 {c.subtitle_languages}")
        if parts:
            quality_parts_set.add("  |  ".join(parts))

    lines = [title_line, f"<b>Total files:</b> {total_files}"]
    for q in sorted(quality_parts_set):
        lines.append(q)

    for c in all_collections:
        lines.append("")
        lines.append(f"<b>{c.name or 'Collection'}</b>")
        for i, f in enumerate(c.files, 1):
            lines.append(f"  {i}. {f.filename}  <i>({_format_size(f.filesize)})</i>")

    text = "\n".join(lines)

    try:
        _send_telegram_message(bot_token, chat_id, text, poster_url)
        return {"status": "ok", "message": "Season preview sent to Telegram"}
    except Exception as e:
        logger.error(f"Failed to send Telegram season preview: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to send: {str(e)}")
