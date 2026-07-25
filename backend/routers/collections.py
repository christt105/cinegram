from typing import Optional
from fastapi import APIRouter, Depends, HTTPException
from sqlmodel import Session, select
from database import get_session
from models import Collection, Season, Episode
from schemas import (
    CollectionOut, CollectionUpdate, CollectionCreate,
    IdentifyRequest, ReidentifyCollectionRequest
)
from crud import prune_orphaned_media, identify_collection
from tmdb import TMDB

router = APIRouter(prefix="/collections", tags=["collections"])
tmdb = TMDB()

@router.get("/{collection_id}", response_model=Optional[CollectionOut])
def get_collection(collection_id: int, session: Session = Depends(get_session)):
    """Return a single collection by ID with its files"""
    collection = session.get(Collection, collection_id)
    if not collection:
        return None
    return collection

@router.delete("/{collection_id}")
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

@router.post("", response_model=CollectionOut)
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

@router.patch("/{collection_id}", response_model=Collection)
def update_collection(
    collection_id: int,
    collection_update: CollectionUpdate,
    session: Session = Depends(get_session),
):
    db_collection = session.get(Collection, collection_id)
    if not db_collection:
        raise HTTPException(status_code=404, detail="Collection not found")

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
        target_season = session.exec(select(Season).where(
            Season.series_id == series_id,
            Season.season_number == collection_update.season_number
        )).first()
        if not target_season:
            target_season = Season(series_id=series_id, season_number=collection_update.season_number)
            session.add(target_season)
            session.commit()
            session.refresh(target_season)
            
        if collection_update.clear_episode:
            db_collection.season_id = target_season.id
            db_collection.episode_id = None
        elif collection_update.episode_number is not None:
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
            db_collection.season_id = None
        else:
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
                    db_collection.season_id = None
            else:
                db_collection.season_id = target_season.id

    update_data = collection_update.dict(exclude_unset=True)
    for virtual_field in ["season_number", "episode_number", "clear_episode"]:
        update_data.pop(virtual_field, None)

    for key, value in update_data.items():
        setattr(db_collection, key, value)

    session.add(db_collection)
    session.commit()
    prune_orphaned_media(session)
    session.refresh(db_collection)
    return db_collection

@router.post("/{collection_id}/identify", response_model=CollectionOut)
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

@router.post("/{collection_id}/reidentify", response_model=CollectionOut)
def reidentify_collection_endpoint(
    collection_id: int,
    request: ReidentifyCollectionRequest,
    session: Session = Depends(get_session)
):
    db_collection = session.get(Collection, collection_id)
    if not db_collection:
        raise HTTPException(status_code=404, detail="Collection not found")

    db_collection.movie_id = None
    db_collection.season_id = None
    db_collection.episode_id = None
    session.add(db_collection)
    session.commit()

    identify_collection(
        session,
        collection_id,
        tmdb,
        forced_tmdb_id=request.tmdb_id,
        forced_media_type=request.media_type,
        forced_season=request.season_number,
        forced_episode=request.episode_number,
    )
    session.refresh(db_collection)
    return db_collection
