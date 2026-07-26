from typing import Optional
from fastapi import APIRouter, Depends, HTTPException
from sqlmodel import Session, select
from database import get_session
from models import File, Collection
from schemas import FileOut, FileUpdate, BatchFileDeleteRequest, BatchFileMoveRequest
from crud import prune_orphaned_media

router = APIRouter(prefix="/files", tags=["files"])

@router.get("/{file_id}", response_model=Optional[FileOut])
def get_file(file_id: int, session: Session = Depends(get_session)):
    """Return a single file by ID"""
    file = session.get(File, file_id)
    if not file:
        return None
    return file

@router.delete("/{file_id}", response_model=dict)
def delete_file(file_id: int, session: Session = Depends(get_session)):
    """Delete a file record by ID"""
    file = session.get(File, file_id)
    if not file:
        raise HTTPException(status_code=404, detail="File not found")
    session.delete(file)
    session.commit()
    return {"status": "deleted", "file_id": file_id}

@router.post("/batch-delete")
def delete_files_batch(request: BatchFileDeleteRequest, session: Session = Depends(get_session)):
    affected_collection_ids: set[int] = set()
    for file_id in request.file_ids:
        file = session.get(File, file_id)
        if file:
            affected_collection_ids.add(file.collection_id)
            session.delete(file)
    session.commit()

    for col_id in affected_collection_ids:
        remaining = session.exec(select(File).where(File.collection_id == col_id)).all()
        if not remaining:
            col = session.get(Collection, col_id)
            if col:
                session.delete(col)
    session.commit()
    prune_orphaned_media(session)

    return {"status": "deleted", "count": len(request.file_ids)}

@router.post("/batch-move")
def move_files_batch(request: BatchFileMoveRequest, session: Session = Depends(get_session)):
    target_col = session.get(Collection, request.collection_id)
    if not target_col:
        raise HTTPException(status_code=404, detail="Target collection not found")

    affected_source_ids: set[int] = set()
    for file_id in request.file_ids:
        file = session.get(File, file_id)
        if file:
            affected_source_ids.add(file.collection_id)
            file.collection_id = request.collection_id
    session.commit()

    for col_id in affected_source_ids:
        if col_id == request.collection_id:
            continue
        remaining = session.exec(select(File).where(File.collection_id == col_id)).all()
        if not remaining:
            col = session.get(Collection, col_id)
            if col:
                session.delete(col)
    session.commit()
    prune_orphaned_media(session)

    return {"status": "moved", "count": len(request.file_ids)}

@router.patch("/{file_id}", response_model=File)
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
