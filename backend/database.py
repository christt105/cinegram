from sqlmodel import SQLModel, create_engine, Session
import os

from sqlalchemy import event

DB_PATH = os.getenv("DATABASE_PATH", "/data/database.db")
DATABASE_URL = f"sqlite:///{DB_PATH}"

engine = create_engine(
    DATABASE_URL,
    connect_args={"check_same_thread": False, "timeout": 15}
)

@event.listens_for(engine, "connect")
def set_sqlite_pragma(dbapi_connection, connection_record):
    cursor = dbapi_connection.cursor()
    cursor.execute("PRAGMA journal_mode=WAL;")
    cursor.execute("PRAGMA busy_timeout=5000;")
    cursor.close()

def init_db():
    """
    Initializes the database schema.
    Creates all tables defined in models if they do not exist.
    Future schema changes should be managed using a migration tool (e.g. Alembic).
    """
    import models
    _ = models  # Access module to register table schemas and satisfy static analysis
    SQLModel.metadata.create_all(engine)

def get_session():
    with Session(engine) as session:
        yield session

