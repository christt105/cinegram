from typing import Optional
from fastapi import APIRouter, Depends, HTTPException
from sqlmodel import Session
from database import get_session
from models import File
from schemas import FileOut, FileUpdate

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
