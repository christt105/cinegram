import pytest
from sqlmodel import SQLModel, create_engine, Session
from models import Series, Season, Episode, Collection, File
from crud import get_or_create_series, get_or_create_season, get_or_create_episode, identify_collection

@pytest.fixture(name="session")
def session_fixture():
    engine = create_engine("sqlite:///:memory:", connect_args={"check_same_thread": False})
    SQLModel.metadata.create_all(engine)
    with Session(engine) as session:
        yield session

class MockTMDB:
    def identify_by_tmdbid(self, tmdbid, content_type):
        return {
            "id": 37854,
            "name": "One Piece",
            "media_type": "tv"
        }
    def identify_by_filename(self, filename):
        return {
            "id": 37854,
            "name": "One Piece",
            "media_type": "tv"
        }

class MockTMDBSeries:
    def __init__(self, tmdb_id, name):
        self._result = {"id": tmdb_id, "name": name, "media_type": "tv"}

    def identify_by_tmdbid(self, tmdbid, content_type):
        return self._result

    def identify_by_filename(self, filename):
        return self._result

def test_tv_linking_helpers(session):
    tmdb_series = {
        "id": 31910,
        "name": "Naruto Shippuden",
        "original_name": "ナルト 疾風伝"
    }

    # 1. Create series
    series = get_or_create_series(session, tmdb_series)
    assert series.id is not None
    assert series.tmdb_id == 31910
    assert series.manual_title == "Naruto Shippuden"

    # 2. Get existing series (should return the same)
    series2 = get_or_create_series(session, tmdb_series)
    assert series2.id == series.id

    # 3. Create season
    season = get_or_create_season(session, series.id, 7)
    assert season.id is not None
    assert season.series_id == series.id
    assert season.season_number == 7

    # 4. Create episode
    episode = get_or_create_episode(session, season.id, 2)
    assert episode.id is not None
    assert episode.season_id == season.id
    assert episode.episode_number == 2
    assert episode.title == "Episode 2"

def test_identify_collection_tv(session):
    # Setup test DB entries
    collection = Collection(name="One Piece 1x125.mkv")
    session.add(collection)
    session.commit()
    session.refresh(collection)

    mock_tmdb = MockTMDB()
    success = identify_collection(session, collection.id, mock_tmdb)

    assert success is True
    session.refresh(collection)
    assert collection.episode_id is not None

    episode = session.get(Episode, collection.episode_id)
    assert episode.episode_number == 125
    
    season = session.get(Season, episode.season_id)
    assert season.season_number == 1

    series = session.get(Series, season.series_id)
    assert series.tmdb_id == 37854
    assert series.manual_title == "One Piece"


def test_identify_collection_forced_episode(session):
    """OVA with no SxxExx pattern can be explicitly placed in a season and episode."""
    collection = Collection(name="Mob Psycho 100 II - OVA")
    session.add(collection)
    session.commit()
    session.refresh(collection)

    mock_tmdb = MockTMDBSeries(67075, "Mob Psycho 100")
    success = identify_collection(
        session, collection.id, mock_tmdb,
        forced_tmdb_id=67075,
        forced_media_type="tv",
        forced_season=0,
        forced_episode=1,
    )

    assert success is True
    session.refresh(collection)
    assert collection.movie_id is None
    assert collection.season_id is None
    assert collection.episode_id is not None

    episode = session.get(Episode, collection.episode_id)
    assert episode.episode_number == 1

    season = session.get(Season, episode.season_id)
    assert season.season_number == 0

    series = session.get(Series, season.series_id)
    assert series.tmdb_id == 67075


def test_identify_collection_forced_season_pack(session):
    """Forced season without episode number creates a season pack."""
    collection = Collection(name="Mob Psycho 100 II - OVA")
    session.add(collection)
    session.commit()
    session.refresh(collection)

    mock_tmdb = MockTMDBSeries(67075, "Mob Psycho 100")
    success = identify_collection(
        session, collection.id, mock_tmdb,
        forced_tmdb_id=67075,
        forced_media_type="tv",
        forced_season=0,
    )

    assert success is True
    session.refresh(collection)
    assert collection.movie_id is None
    assert collection.episode_id is None
    assert collection.season_id is not None

    season = session.get(Season, collection.season_id)
    assert season.season_number == 0
