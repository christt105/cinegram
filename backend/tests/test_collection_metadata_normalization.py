import json

import pytest
from fastapi.testclient import TestClient
from sqlalchemy.pool import StaticPool
from sqlmodel import SQLModel, Session, create_engine

import crud
from database import get_session
from main import app


@pytest.fixture(name="client")
def client_fixture():
    import models
    _ = models
    engine = create_engine(
        "sqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool
    )
    SQLModel.metadata.create_all(engine)

    def get_session_override():
        with Session(engine) as session:
            yield session

    app.dependency_overrides[get_session] = get_session_override
    with TestClient(app) as client:
        yield client
    app.dependency_overrides.clear()


def upload_collection(client, message_id, filename):
    response = client.post("/upload", json={
        "message_id": message_id,
        "filename": filename,
        "filesize": 1048576,
        "mime_type": "video/x-matroska"
    })
    assert response.status_code == 200, response.text
    return response.json()["collection_id"]


def probe_metadata_with_video(codec_name):
    return json.dumps({
        "streams": [{
            "codec_type": "video",
            "codec_name": codec_name,
            "width": 1920,
            "height": 1080
        }]
    })


# ========================
# CODEC_MAP / extract_codec_from_metadata
# ========================

def test_extract_codec_from_metadata_maps_known_codecs():
    assert crud.extract_codec_from_metadata(probe_metadata_with_video("h264")) == "H.264/AVC"
    assert crud.extract_codec_from_metadata(probe_metadata_with_video("hevc")) == "H.265/HEVC"
    assert crud.extract_codec_from_metadata(probe_metadata_with_video("av1")) == "AV1"


def test_extract_codec_from_metadata_returns_none_for_unknown_codec():
    assert crud.extract_codec_from_metadata(probe_metadata_with_video("mjpeg")) is None


def test_extract_codec_from_metadata_returns_none_without_a_video_stream():
    metadata = json.dumps({"streams": [{"codec_type": "audio", "codec_name": "aac"}]})
    assert crud.extract_codec_from_metadata(metadata) is None


def test_extract_codec_from_metadata_returns_none_for_garbage_input():
    assert crud.extract_codec_from_metadata("not json") is None


# ========================
# Language vocabulary validation
# ========================

def test_validate_language_list_accepts_known_codes():
    assert crud.validate_language_list("spa,eng") == "spa,eng"


def test_validate_language_list_normalizes_case_and_whitespace():
    assert crud.validate_language_list(" SPA , Eng ") == "spa,eng"


def test_validate_language_list_deduplicates():
    assert crud.validate_language_list("spa,spa,eng") == "spa,eng"


def test_validate_language_list_resolves_terminology_aliases():
    # "fra" is the ISO 639-2/T code for French; the vocabulary is keyed by "fre" (639-2/B)
    assert crud.validate_language_list("fra") == "fre"


def test_validate_language_list_discards_und_silently():
    assert crud.validate_language_list("spa,und") == "spa"
    assert crud.validate_language_list("und") is None


def test_validate_language_list_rejects_unknown_codes():
    with pytest.raises(crud.InvalidLanguageCodeError) as exc_info:
        crud.validate_language_list("spa,klingon")
    assert exc_info.value.codes == ["klingon"]


def test_validate_language_list_handles_none_and_empty():
    assert crud.validate_language_list(None) is None
    assert crud.validate_language_list("") is None
    assert crud.validate_language_list("   ") is None


# ========================
# API integration
# ========================

def test_technical_metadata_patch_fills_in_the_codec(client):
    collection_id = upload_collection(client, 6001, "Mugen.Train.2020.mkv")

    response = client.patch(
        f"/collections/{collection_id}",
        json={"technical_metadata": probe_metadata_with_video("hevc")}
    )
    assert response.status_code == 200
    assert response.json()["codec"] == "H.265/HEVC"


def test_explicit_codec_wins_over_the_probed_one(client):
    collection_id = upload_collection(client, 6002, "Mugen.Train.2020.mkv")

    response = client.patch(f"/collections/{collection_id}", json={
        "technical_metadata": probe_metadata_with_video("hevc"),
        "codec": "Custom Remux"
    })
    assert response.status_code == 200
    assert response.json()["codec"] == "Custom Remux"


def test_patch_accepts_valid_language_codes(client):
    collection_id = upload_collection(client, 6003, "Mugen.Train.2020.mkv")

    response = client.patch(f"/collections/{collection_id}", json={
        "audio_languages": "spa,eng",
        "subtitle_languages": "eng"
    })
    assert response.status_code == 200
    body = response.json()
    assert body["audio_languages"] == "spa,eng"
    assert body["subtitle_languages"] == "eng"


def test_patch_rejects_an_unknown_language_code(client):
    collection_id = upload_collection(client, 6004, "Mugen.Train.2020.mkv")

    response = client.patch(f"/collections/{collection_id}", json={
        "audio_languages": "spa,not-a-language"
    })
    assert response.status_code == 400


def test_create_collection_rejects_an_unknown_language_code(client):
    response = client.post("/collections", json={
        "name": "Some Collection",
        "audio_languages": "not-a-language"
    })
    assert response.status_code == 400


def test_metadata_vocabulary_endpoint(client):
    response = client.get("/collections/vocabulary")
    assert response.status_code == 200
    body = response.json()

    assert "1080p" in body["qualities"]
    assert "4K HDR10+" in body["qualities"]
    assert "H.264/AVC" in body["codecs"]
    assert body["languages"]["spa"] == "Español"
    assert "und" not in body["languages"]
