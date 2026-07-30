from sqlmodel import create_engine, Session
import os

from sqlalchemy import event, inspect

DB_PATH = os.getenv("DATABASE_PATH", "/data/database.db")
DATABASE_URL = f"sqlite:///{DB_PATH}"

ALEMBIC_INI = os.path.join(os.path.dirname(os.path.abspath(__file__)), "alembic.ini")
BASELINE_REVISION = "0001_baseline"

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

def needs_baseline_stamp(connection) -> bool:
    """
    True for a database created before Alembic was adopted: it already holds the
    baseline tables but has no version table, so the baseline must be stamped
    instead of executed.
    """
    tables = inspect(connection).get_table_names()
    return "alembic_version" not in tables and "collection" in tables

def init_db():
    """
    Brings the database schema up to date by running the Alembic migrations.
    Creates the schema from scratch on an empty database.
    """
    from alembic import command
    from alembic.config import Config

    config = Config(ALEMBIC_INI)
    with engine.begin() as connection:
        config.attributes["connection"] = connection
        if needs_baseline_stamp(connection):
            command.stamp(config, BASELINE_REVISION)
        command.upgrade(config, "head")

def get_session():
    with Session(engine) as session:
        yield session

