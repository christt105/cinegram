from fastapi import APIRouter, Depends
from sqlmodel import Session, select
from database import get_session
from models import Collection
from schemas import BatchIdentifyRequest, BatchDeleteRequest
from crud import identify_collection, prune_orphaned_media
from tmdb import TMDB
from logger import logger

router = APIRouter(prefix="/maintenance", tags=["maintenance"])
tmdb = TMDB()

@router.get("/orphans")
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

@router.post("/identify-batch")
def identify_batch(request: BatchIdentifyRequest, session: Session = Depends(get_session)):
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

@router.post("/delete-batch")
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
