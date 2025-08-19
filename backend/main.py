from fastapi import FastAPI, Depends, BackgroundTasks, HTTPException
from pydantic import BaseModel
from sqlmodel import Session
from database import init_db, get_session
from crud import create_file, get_item
from tmdb import identify_by_filename
import os

app = FastAPI(title="jellygram-backend")

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

# Response model (simple)
class ItemOut(BaseModel):
    id: int
    message_id: int | None
    filename: str | None
    collection_id: int | None

@app.get("/")
def root():
    return {"status": "ok", "message": "Backend is running"}

@app.post("/upload", response_model=ItemOut)
async def upload_endpoint(payload: UploadIn, background_tasks: BackgroundTasks, session: Session = Depends(get_session)):
    # create DB record with status pending
    file, collection = create_file(
        session,
        message_id=payload.message_id,
        filename=payload.filename,
        filesize=payload.filesize,
        mime_type=payload.mime_type,
        created_at=payload.created_at,
    )
    
    #background_tasks.add_task(identify_and_update, file.id)
    
    return file

async def identify_and_update(item_id: int):
    # create a ephemeral session
    from database import engine
    from sqlmodel import Session
    from crud import update_item, get_item
    with Session(engine) as session:
        item = get_item(session, item_id)
        if not item:
            return
        # try identification by filename
        result = await identify_by_filename(item.filename or "")
        if result:
            update_item(session, item_id, {
                "media_type": result.get("media_type"),
                "title": result.get("title"),
                "year": int(result.get("year")) if result.get("year") else None,
                "tmdb_id": result.get("tmdb_id"),
                "status": "identified",
            })
        else:
            update_item(session, item_id, {"status": "failed"})

@app.get("/items", response_model=list[ItemOut])
def get_items(limit: int = 100, session: Session = Depends(get_session)):
    return list_items(session, limit=limit)

@app.get("/items/{item_id}", response_model=ItemOut)
def get_item_route(item_id: int, session: Session = Depends(get_session)):
    item = get_item(session, item_id)
    if not item:
        raise HTTPException(status_code=404, detail="Item not found")
    return item

class ItemPatch(BaseModel):
    title: str | None = None
    year: int | None = None
    tmdb_id: int | None = None
    media_type: str | None = None
    status: str | None = None

@app.patch("/items/{item_id}", response_model=ItemOut)
def patch_item(item_id: int, data: ItemPatch, session: Session = Depends(get_session)):
    item = update_item(session, item_id, data.dict(exclude_none=True))
    if not item:
        raise HTTPException(status_code=404, detail="Item not found")
    return item
