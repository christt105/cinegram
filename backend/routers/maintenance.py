import os
import shutil
import sqlite3
import tempfile
from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import FileResponse
from starlette.background import BackgroundTask
from sqlmodel import Session, select
from database import get_session
from models import Collection
from schemas import BatchIdentifyRequest, BatchDeleteRequest
from crud import identify_collection, prune_orphaned_media
from config import settings
from tmdb import TMDB
from logger import logger

router = APIRouter(prefix="/maintenance", tags=["maintenance"])
tmdb = TMDB()


@router.get("/backup")
def backup_database():
    db_path = settings.DATABASE_PATH
    if not os.path.exists(db_path):
        raise HTTPException(status_code=404, detail="Database not found")

    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    filename = f"cinegram-backup-{timestamp}.db"
    tmp_dir = tempfile.mkdtemp(prefix="cinegram-backup-")
    tmp_path = os.path.join(tmp_dir, filename)

    try:
        source = sqlite3.connect(db_path)
        try:
            source.execute(f"VACUUM INTO '{tmp_path.replace(chr(39), chr(39) * 2)}'")
        finally:
            source.close()
    except sqlite3.Error as e:
        shutil.rmtree(tmp_dir, ignore_errors=True)
        logger.error(f"Database backup failed: {e}")
        raise HTTPException(status_code=500, detail="Backup failed")

    return FileResponse(
        tmp_path,
        media_type="application/octet-stream",
        filename=filename,
        background=BackgroundTask(shutil.rmtree, tmp_dir, ignore_errors=True),
    )

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
