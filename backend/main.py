import os
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from config import settings
from database import init_db

from routers.maintenance import router as maintenance_router

@asynccontextmanager
async def lifespan(app: FastAPI):
    os.makedirs(settings.DATA_DIR, exist_ok=True)
    init_db()
    yield

app = FastAPI(title=settings.PROJECT_NAME, version=settings.APP_VERSION, lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
def root():
    return {"status": "ok", "message": "Backend is running"}

@app.get("/version")
def version():
    return {"name": settings.PROJECT_NAME, "version": settings.APP_VERSION}

app.include_router(maintenance_router)
