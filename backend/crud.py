from sqlmodel import select
from sqlalchemy.exc import NoResultFound
from models import File, Collection
from database import engine
from sqlmodel import Session
from datetime import datetime
from typing import Optional, Dict, Any

def get_or_create_collection(session: Session, collection_name: str, mime_type: str):
    
    if mime_type.startswith("video/"):
        # if it is a video, we can assume that it is a file on its own
        collection = Collection(name=collection_name)
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

    collection = Collection(name=collection_name)
    session.add(collection)
    session.commit()
    session.refresh(collection)
    return collection

def create_file(session: Session, message_id, filename, filesize, mime_type, created_at):
    
    collection_name = filename.split('.')[0] # TODO: Improve collection name extraction logic (?)
    
    collection = get_or_create_collection(session, collection_name, mime_type)
    
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

def get_item(session: Session, item_id: int) -> Optional[File]:
    return session.get(File, item_id)
