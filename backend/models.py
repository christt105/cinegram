from typing import Optional, List
from sqlmodel import SQLModel, Field, Relationship
from datetime import datetime, timezone

# ========================
# Media core
# ========================

class Movie(SQLModel, table=True):
    id: Optional[int] = Field(default=None, primary_key=True)
    tmdb_id: Optional[int] = Field(default=None, unique=True, index=True)
    manual_title: Optional[str] = None

    collections: List["Collection"] = Relationship(back_populates="movie")


class Series(SQLModel, table=True):
    id: Optional[int] = Field(default=None, primary_key=True)
    tmdb_id: Optional[int] = Field(default=None, unique=True, index=True)
    manual_title: Optional[str] = None

    seasons: List["Season"] = Relationship(back_populates="series")


class Season(SQLModel, table=True):
    id: Optional[int] = Field(default=None, primary_key=True)
    series_id: int = Field(foreign_key="series.id")
    season_number: int

    series: "Series" = Relationship(back_populates="seasons")
    episodes: List["Episode"] = Relationship(back_populates="season")


class Episode(SQLModel, table=True):
    id: Optional[int] = Field(default=None, primary_key=True)
    season_id: int = Field(foreign_key="season.id")
    episode_number: int
    title: Optional[str] = None

    season: "Season" = Relationship(back_populates="episodes")
    collections: List["Collection"] = Relationship(back_populates="episode")


# ========================
# Files & Collections
# ========================

class Collection(SQLModel, table=True):
    id: Optional[int] = Field(default=None, primary_key=True)
    name: Optional[str] = None

    movie_id: Optional[int] = Field(default=None, foreign_key="movie.id")
    episode_id: Optional[int] = Field(default=None, foreign_key="episode.id")

    movie: Optional[Movie] = Relationship(back_populates="collections")
    episode: Optional[Episode] = Relationship(back_populates="collections")
    files: List["File"] = Relationship(back_populates="collection")

class File(SQLModel, table=True):
    id: Optional[int] = Field(default=None, primary_key=True)
    message_id: int
    filename: str
    filesize: int
    mime_type: Optional[str] = None

    created_at: datetime = Field(default_factory=datetime.now(timezone.utc), nullable=False)

    collection_id: int = Field(foreign_key="collection.id")
    collection: "Collection" = Relationship(back_populates="files")
