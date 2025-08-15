import os

API_ID = int(os.getenv("TELEGRAM_API_ID", "123456"))
API_HASH = os.getenv("TELEGRAM_API_HASH", "your_api_hash")
BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "your_bot_token")
AUTH_USER_ID = os.getenv("TELEGRAM_AUTH_USER_ID", "your_user_id")

BACKEND_URL = os.getenv("BACKEND_URL", "http://backend:8000")
