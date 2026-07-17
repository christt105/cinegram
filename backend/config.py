import os
from dotenv import load_dotenv

load_dotenv()

TMDB_API_KEY = os.getenv("TMDB_API_KEY", "your_tmdb_api_key")
TMDB_CONTENT_LANGUAGE = os.getenv("TMDB_CONTENT_LANGUAGE", "en-US")