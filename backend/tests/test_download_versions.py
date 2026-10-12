import json

import pytest
from fastapi.testclient import TestClient
from sqlalchemy.pool import StaticPool
from sqlmodel import SQLModel, Session, create_engine

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


def probe_metadata(width, height, color_transfer="bt709"):
    return json.dumps({
        "streams": [{
            "codec_type": "video",
            "width": width,
            "height": height,
            "color_transfer": color_transfer
        }]
    })


def test_technical_metadata_patch_refreshes_the_quality(client):
    collection_id = upload_collection(client, 5003, "Mugen.Train.2020.mkv")

    response = client.patch(
        f"/collections/{collection_id}",
        json={"technical_metadata": probe_metadata(3840, 2160, "smpte2084")}
    )
    assert response.status_code == 200
    assert response.json()["quality"] == "4K HDR"


def test_explicit_quality_wins_over_the_probed_one(client):
    collection_id = upload_collection(client, 5004, "Mugen.Train.2020.mkv")

    response = client.patch(f"/collections/{collection_id}", json={
        "technical_metadata": probe_metadata(1920, 1080),
        "quality": "1080p Remux"
    })
    assert response.status_code == 200
    assert response.json()["quality"] == "1080p Remux"
