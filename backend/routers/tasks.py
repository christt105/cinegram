from typing import List
from fastapi import APIRouter, Depends, HTTPException
from sqlmodel import Session, select
from database import get_session
from models import Series, Collection, DownloadTask, UploadTask

router = APIRouter(tags=["tasks"])

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

@router.get("/tasks/downloads", response_model=List[DownloadTask])
def list_download_tasks(session: Session = Depends(get_session)):
    """List all download tasks"""
    return session.exec(select(DownloadTask)).all()

@router.get("/tasks/uploads", response_model=List[UploadTask])
def list_upload_tasks(session: Session = Depends(get_session)):
    """List all upload tasks"""
    return session.exec(select(UploadTask)).all()

@router.delete("/tasks/completed")
def clear_completed_tasks(session: Session = Depends(get_session)):
    """Clear completed download and upload tasks from the queue"""
    completed_downloads = session.exec(
        select(DownloadTask).where(DownloadTask.status == "completed")
    ).all()
    completed_uploads = session.exec(
        select(UploadTask).where(UploadTask.status == "completed")
    ).all()

    for dt in completed_downloads:
        session.delete(dt)
    for ut in completed_uploads:
        session.delete(ut)

    session.commit()
    return {
        "status": "ok",
        "cleared_downloads": len(completed_downloads),
        "cleared_uploads": len(completed_uploads)
    }

@router.post("/downloads/enqueue/collection/{collection_id}")
def enqueue_collection_endpoint(collection_id: int, session: Session = Depends(get_session)):
    success = enqueue_collection_id(session, collection_id)
    if not success:
        return {"status": "ignored", "message": "Collection already in queue or empty"}
    return {"status": "ok", "message": "Enqueued collection"}

@router.post("/downloads/enqueue/series/{series_id}")
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

@router.post("/downloads/enqueue/series/{series_id}/season/{season_number}")
def enqueue_season_endpoint(series_id: int, season_number: int, session: Session = Depends(get_session)):
    series = session.get(Series, series_id)
    if not series:
        raise HTTPException(status_code=404, detail="Series not found")
    count = 0
    for season in series.seasons:
        if season.season_number == season_number:
            for ep in season.episodes:
                for coll in ep.collections:
                    if enqueue_collection_id(session, coll.id):
                        count += 1
            for coll in season.collections:
                if enqueue_collection_id(session, coll.id):
                    count += 1
    return {"status": "ok", "message": f"Enqueued {count} collections for season {season_number}"}
