import os
from dotenv import load_dotenv

load_dotenv()

class Settings:
    PROJECT_NAME: str = "cinegram-backend"
    DATA_DIR: str = os.getenv("DATA_DIR", "/data")
    DATABASE_PATH: str = os.getenv("DATABASE_PATH", "/data/database.db")
    TMDB_API_KEY: str = os.getenv("TMDB_API_KEY", "")
    TMDB_CONTENT_LANGUAGE: str = os.getenv("TMDB_CONTENT_LANGUAGE", "en-US")

settings = Settings()

# Backward compatibility accessors
TMDB_API_KEY = settings.TMDB_API_KEY
TMDB_CONTENT_LANGUAGE = settings.TMDB_CONTENT_LANGUAGE