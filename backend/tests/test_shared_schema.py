"""
bot-net maps this database with EF Core (bot-net/src/Data/CinegramDb.cs) while both services
share it. These tests fail when a migration changes the schema without updating the .NET side:
the fixture its tests build databases from, and the revision it expects at startup.

Regenerate the fixture after a schema change with UPDATE_SHARED_SCHEMA=1 python -m pytest
tests/test_shared_schema.py, then update CinegramDb.ExpectedAlembicRevision and the entities.
"""
import os
import re
import sqlite3
from pathlib import Path

from alembic import command
from alembic.config import Config
from alembic.script import ScriptDirectory
from sqlalchemy import create_engine

from database import ALEMBIC_INI

REPO = Path(__file__).resolve().parents[2]
FIXTURE = REPO / "bot-net" / "tests" / "Bot.Tests" / "Fixtures" / "backend-schema.sql"
DB_CONTEXT = REPO / "bot-net" / "src" / "Data" / "CinegramDb.cs"


def head_schema(tmp_path):
    path = tmp_path / "head.db"
    engine = create_engine(f"sqlite:///{path}")
    config = Config(ALEMBIC_INI)
    with engine.begin() as connection:
        config.attributes["connection"] = connection
        command.upgrade(config, "head")
    engine.dispose()
    with sqlite3.connect(path) as conn:
        rows = conn.execute(
            "SELECT sql FROM sqlite_master WHERE sql IS NOT NULL ORDER BY tbl_name, type DESC, name"
        ).fetchall()
    return "".join(f"{sql};\n" for (sql,) in rows)


def test_fixture_matches_alembic_head(tmp_path):
    schema = head_schema(tmp_path)
    if os.getenv("UPDATE_SHARED_SCHEMA"):
        FIXTURE.write_text(schema)
    assert FIXTURE.read_text() == schema


def test_dotnet_expects_alembic_head():
    head = ScriptDirectory.from_config(Config(ALEMBIC_INI)).get_current_head()
    expected = re.search(r'ExpectedAlembicRevision = "([^"]+)"', DB_CONTEXT.read_text()).group(1)
    assert expected == head
