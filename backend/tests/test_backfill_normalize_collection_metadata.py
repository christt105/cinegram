import json

import pytest
from sqlalchemy.pool import StaticPool
from sqlmodel import Session, SQLModel, create_engine, select

import models
from models import Collection
from scripts import backfill_normalize_collection_metadata as backfill


@pytest.fixture(name="db_engine")
def db_engine_fixture(monkeypatch):
    engine = create_engine(
        "sqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool
    )
    SQLModel.metadata.create_all(engine)
    monkeypatch.setattr(backfill, "engine", engine)
    return engine


def probe_metadata(codec_name=None, width=1920, height=1080):
    stream = {"codec_type": "video", "width": width, "height": height}
    if codec_name:
        stream["codec_name"] = codec_name
    return json.dumps({"streams": [stream]})


def seed_collection(engine, **kwargs):
    with Session(engine) as session:
        collection = Collection(name=kwargs.pop("name", "Test"), **kwargs)
        session.add(collection)
        session.commit()
        session.refresh(collection)
        return collection.id


# ========================
# Token-level helpers
# ========================

def test_resolve_language_token_accepts_iso_codes_and_aliases():
    assert backfill.resolve_language_token("spa") == "spa"
    assert backfill.resolve_language_token("es") == "spa"
    assert backfill.resolve_language_token("Spanish") == "spa"
    assert backfill.resolve_language_token("Español") == "spa"


def test_resolve_language_token_rejects_unknown_tokens():
    assert backfill.resolve_language_token("klingon") is None


def test_normalize_language_field_maps_every_token():
    normalized, resolved = backfill.normalize_language_field("Spanish, English")
    assert resolved is True
    assert normalized == "spa,eng"


def test_normalize_language_field_leaves_unresolved_values_untouched():
    normalized, resolved = backfill.normalize_language_field("Spanish, Klingon")
    assert resolved is False
    assert normalized == "Spanish, Klingon"


def test_normalize_language_field_handles_empty_values():
    assert backfill.normalize_language_field(None) == (None, True)
    assert backfill.normalize_language_field("") == ("", True)


# ========================
# run() against a seeded database
# ========================

def test_run_normalizes_quality_from_free_text(db_engine):
    collection_id = seed_collection(db_engine, quality="1080p Remux")

    stats = backfill.run()

    assert stats["quality_updated"] == 1
    with Session(db_engine) as session:
        collection = session.get(Collection, collection_id)
    assert collection.quality == "1080p"


def test_run_prefers_probed_quality_over_free_text(db_engine):
    collection_id = seed_collection(
        db_engine, quality="1080p Remux", technical_metadata=probe_metadata(width=3840, height=2160)
    )

    stats = backfill.run()

    assert stats["quality_updated"] == 1
    with Session(db_engine) as session:
        collection = session.get(Collection, collection_id)
    assert collection.quality == "4K"


def test_run_leaves_unrecognizable_quality_for_manual_review(db_engine):
    collection_id = seed_collection(db_engine, quality="Whatever Cut")

    stats = backfill.run()

    assert stats["quality_updated"] == 0
    assert (collection_id, "quality", "Whatever Cut") in stats["needs_review"]
    with Session(db_engine) as session:
        collection = session.get(Collection, collection_id)
    assert collection.quality == "Whatever Cut"


def test_run_fills_in_codec_from_technical_metadata(db_engine):
    collection_id = seed_collection(
        db_engine, quality="1080p", technical_metadata=probe_metadata(codec_name="hevc")
    )

    stats = backfill.run()

    assert stats["codec_updated"] == 1
    with Session(db_engine) as session:
        collection = session.get(Collection, collection_id)
    assert collection.codec == "H.265/HEVC"


def test_run_does_not_overwrite_an_existing_codec(db_engine):
    collection_id = seed_collection(
        db_engine, quality="1080p", codec="Custom", technical_metadata=probe_metadata(codec_name="hevc")
    )

    backfill.run()

    with Session(db_engine) as session:
        collection = session.get(Collection, collection_id)
    assert collection.codec == "Custom"


def test_run_normalizes_language_free_text(db_engine):
    collection_id = seed_collection(
        db_engine, quality="1080p", audio_languages="Spanish, English", subtitle_languages="es"
    )

    stats = backfill.run()

    assert stats["audio_languages_updated"] == 1
    assert stats["subtitle_languages_updated"] == 1
    with Session(db_engine) as session:
        collection = session.get(Collection, collection_id)
    assert collection.audio_languages == "spa,eng"
    assert collection.subtitle_languages == "spa"


def test_run_leaves_unresolvable_languages_for_manual_review(db_engine):
    collection_id = seed_collection(db_engine, quality="1080p", audio_languages="Spanish, Klingon")

    stats = backfill.run()

    assert stats["audio_languages_updated"] == 0
    assert (collection_id, "audio_languages", "Spanish, Klingon") in stats["needs_review"]
    with Session(db_engine) as session:
        collection = session.get(Collection, collection_id)
    assert collection.audio_languages == "Spanish, Klingon"


def test_dry_run_does_not_write_to_the_database(db_engine):
    collection_id = seed_collection(db_engine, quality="1080p Remux")

    stats = backfill.run(dry_run=True)

    assert stats["quality_updated"] == 1
    with Session(db_engine) as session:
        collection = session.get(Collection, collection_id)
    assert collection.quality == "1080p Remux"
