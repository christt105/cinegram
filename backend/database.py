from sqlmodel import SQLModel, create_engine, Session
import os

DB_PATH = os.getenv("DATABASE_PATH", "/data/database.db")
DATABASE_URL = f"sqlite:///{DB_PATH}"

engine = create_engine(DATABASE_URL, connect_args={"check_same_thread": False})

def init_db():
    SQLModel.metadata.create_all(engine)
    # Auto-migration fallback for SQLite
    from sqlmodel import text
    with Session(engine) as session:
        for column, col_type in [("poster_path", "VARCHAR"), ("overview", "VARCHAR"), ("release_year", "INTEGER"), ("tvdb_id", "INTEGER")]:
            try:
                session.execute(text(f"ALTER TABLE series ADD COLUMN {column} {col_type};"))
                session.commit()
            except Exception:
                pass

def get_session():
    with Session(engine) as session:
        yield session
