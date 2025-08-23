from sqlmodel import select
from sqlalchemy.exc import NoResultFound
from models import File, Collection, Movie
from database import engine
from sqlmodel import Session
from datetime import datetime
from typing import Optional, Dict, Any
from logger import logger
from tmdb import TMDB

import re

QUALITY_MAP = {
    "4K": ["2160p", "4k", "uhd"],
    "2K": ["1440p", "2k"],
    "1080p": ["1080p", "fullhd", "fhd"],
    "720p": ["720p"],
    "480p": ["480p"],
    "360p": ["360p"],
    "240p": ["240p"]
}

HDR_MAP = {
    "HDR10+": ["hdr10+"],
    "HDR10": ["hdr10"],
    "DolbyVision": ["dolbyvision", "dv"],
    "HLG": ["hlg"],
    "HDR": ["hdr"],
    "SDR": []  # default si no se detecta nada
}

def try_subtract_quality(filename: str):
    fname = filename.lower()
    
    # Normalizar resolución
    resolution = None
    for key, values in QUALITY_MAP.items():
        if any(v in fname for v in values):
            resolution = key
            break
    
    # Normalizar HDR
    hdr = "SDR"
    for key, values in HDR_MAP.items():
        if any(v in fname for v in values):
            hdr = key
            break
    
    return resolution, hdr


def get_or_create_collection(session: Session, filename: str, mime_type: str):

    collection_name = filename.split('.')[0] # TODO: Improve collection name extraction logic (?)

    resolution, hdr = try_subtract_quality(filename)
    

    quality = f"{resolution}" if resolution else None

    if quality and hdr and hdr != "SDR":
        quality += f" {hdr}"

    if mime_type.startswith("video/"):
        # if it is a video, we can assume that it is a file on its own
        
        # TODO: Check if there is a collection with the same name and use the same movie_id or episode_id if it exists
        
        collection = Collection(name=collection_name, quality=quality)
        session.add(collection)
        session.commit()
        session.refresh(collection)
        return collection
    # otherwise, it is a collection of files, so we need to check if it already exists and create it if it does not exist
    # WARN: if the filename is the same as other files, it will be added to the same collection
    
    collection = session.exec(
        select(Collection).where(Collection.name == collection_name)
    ).first()

    if collection:
        return collection

    collection = Collection(name=collection_name, quality=quality)
    session.add(collection)
    session.commit()
    session.refresh(collection)
    return collection

def create_file(session: Session, message_id, filename, filesize, mime_type, created_at):
    
    
    collection = get_or_create_collection(session, filename, mime_type)
    
    file = File(
        message_id=message_id,
        filename=filename,
        filesize=filesize,
        mime_type=mime_type,
        created_at=datetime.fromisoformat(created_at) if created_at else None,
        collection_id=collection.id
    )
    session.add(file)
    session.commit()
    session.refresh(file)
    return file, collection

def get_file(session: Session, item_id: int) -> Optional[File]:
    return session.get(File, item_id)

def get_collection(session: Session, item_id: int) -> Optional[File]:
    return session.get(Collection, item_id)

async def identify_collection(session: Session, collection_id: int, tmdb: TMDB):
    logger.info(f"Identifying collection {collection_id}")
    
    collection = get_collection(session, collection_id)
    
    if not collection:
        logger.error(f"Collection {collection_id} not found")
        return
    
    if collection.movie_id is not None or collection.episode_id is not None:
        logger.info(f"Collection {collection_id} already identified")
        return
    
    tmdb_result = tmdb.identify_by_filename(collection.name)
    
    if not tmdb_result:
        logger.warning(f"No TMDB identification result for collection {collection_id}")
        return
    
    if not tmdb_result["media_type"]:
        logger.warning(f"No media type found for collection {collection_id}")
        return
    
    if tmdb_result["media_type"] == "movie":
        movie = get_or_create_movie(session, tmdb_result)
        collection.movie_id = movie.id
        session.add(collection)
        session.commit()
        logger.info(f"Linked collection {collection_id} to movie {movie.id} ({movie.title})")
    
    elif tmdb_result["media_type"] == "tv":
        # TODO: implement tv show linking
        logger.info(f"Collection {collection_id} is a TV show (not yet linked)")
    
    logger.info(f"TMDB identification result: {tmdb_result}")
    

def get_or_create_movie(session: Session, tmdb_movie: dict) -> Movie:
    """Get or create a movie by TMDB ID."""
    tmdb_id = tmdb_movie["id"]
    try:
        movie = session.exec(
            select(Movie).where(Movie.tmdb_id == tmdb_id)
        ).one()
        return movie
    except NoResultFound:
        movie = Movie(
            tmdb_id=tmdb_id, 
            title=tmdb_movie["title"],
            release_year=int(tmdb_movie["release_date"].split("-")[0]) if tmdb_movie.get("release_date") else None,
            poster_path=tmdb_movie.get("poster_path")
        )
        session.add(movie)
        session.commit()
        session.refresh(movie)
        return movie

