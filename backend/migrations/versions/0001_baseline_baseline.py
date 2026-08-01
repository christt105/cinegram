"""baseline

Revision ID: 0001_baseline
Revises: 
Create Date: 2026-07-29 19:38:40.948027

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
import sqlmodel


# revision identifiers, used by Alembic.
revision: str = '0001_baseline'
down_revision: Union[str, Sequence[str], None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    op.create_table('movie',
    sa.Column('id', sa.Integer(), nullable=False),
    sa.Column('tmdb_id', sa.Integer(), nullable=True),
    sa.Column('title', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('poster_path', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('release_year', sa.Integer(), nullable=True),
    sa.Column('overview', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('tags', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('notes', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('manually_added', sa.Boolean(), nullable=False),
    sa.Column('created_at', sa.DateTime(), nullable=False),
    sa.PrimaryKeyConstraint('id')
    )
    with op.batch_alter_table('movie', schema=None) as batch_op:
        batch_op.create_index(batch_op.f('ix_movie_tmdb_id'), ['tmdb_id'], unique=True)

    op.create_table('series',
    sa.Column('id', sa.Integer(), nullable=False),
    sa.Column('tmdb_id', sa.Integer(), nullable=True),
    sa.Column('tvdb_id', sa.Integer(), nullable=True),
    sa.Column('manual_title', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('poster_path', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('overview', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('release_year', sa.Integer(), nullable=True),
    sa.Column('tags', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('notes', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('manually_added', sa.Boolean(), nullable=False),
    sa.Column('created_at', sa.DateTime(), nullable=False),
    sa.PrimaryKeyConstraint('id')
    )
    with op.batch_alter_table('series', schema=None) as batch_op:
        batch_op.create_index(batch_op.f('ix_series_tmdb_id'), ['tmdb_id'], unique=True)

    op.create_table('uploadtask',
    sa.Column('id', sa.Integer(), nullable=False),
    sa.Column('jellyfin_id', sqlmodel.sql.sqltypes.AutoString(), nullable=False),
    sa.Column('tmdb_id', sa.Integer(), nullable=True),
    sa.Column('media_type', sqlmodel.sql.sqltypes.AutoString(), nullable=False),
    sa.Column('path', sqlmodel.sql.sqltypes.AutoString(), nullable=False),
    sa.Column('title', sqlmodel.sql.sqltypes.AutoString(), nullable=False),
    sa.Column('year', sa.Integer(), nullable=True),
    sa.Column('status', sqlmodel.sql.sqltypes.AutoString(), nullable=False),
    sa.Column('progress', sa.Integer(), nullable=False),
    sa.Column('error_message', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('created_at', sa.DateTime(), nullable=False),
    sa.Column('completed_at', sa.DateTime(), nullable=True),
    sa.PrimaryKeyConstraint('id')
    )
    op.create_table('season',
    sa.Column('id', sa.Integer(), nullable=False),
    sa.Column('series_id', sa.Integer(), nullable=False),
    sa.Column('season_number', sa.Integer(), nullable=False),
    sa.Column('tags', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('notes', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.ForeignKeyConstraint(['series_id'], ['series.id'], ),
    sa.PrimaryKeyConstraint('id')
    )
    op.create_table('episode',
    sa.Column('id', sa.Integer(), nullable=False),
    sa.Column('season_id', sa.Integer(), nullable=False),
    sa.Column('episode_number', sa.Integer(), nullable=False),
    sa.Column('title', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('tags', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('notes', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.ForeignKeyConstraint(['season_id'], ['season.id'], ),
    sa.PrimaryKeyConstraint('id')
    )
    op.create_table('collection',
    sa.Column('id', sa.Integer(), nullable=False),
    sa.Column('name', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('quality', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('audio_languages', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('subtitle_languages', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('tags', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('notes', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('technical_metadata', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('movie_id', sa.Integer(), nullable=True),
    sa.Column('season_id', sa.Integer(), nullable=True),
    sa.Column('episode_id', sa.Integer(), nullable=True),
    sa.ForeignKeyConstraint(['episode_id'], ['episode.id'], ),
    sa.ForeignKeyConstraint(['movie_id'], ['movie.id'], ),
    sa.ForeignKeyConstraint(['season_id'], ['season.id'], ),
    sa.PrimaryKeyConstraint('id')
    )
    op.create_table('downloadtask',
    sa.Column('id', sa.Integer(), nullable=False),
    sa.Column('collection_id', sa.Integer(), nullable=False),
    sa.Column('status', sqlmodel.sql.sqltypes.AutoString(), nullable=False),
    sa.Column('progress', sa.Integer(), nullable=False),
    sa.Column('error_message', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('created_at', sa.DateTime(), nullable=False),
    sa.Column('completed_at', sa.DateTime(), nullable=True),
    sa.ForeignKeyConstraint(['collection_id'], ['collection.id'], ),
    sa.PrimaryKeyConstraint('id')
    )
    op.create_table('file',
    sa.Column('id', sa.Integer(), nullable=False),
    sa.Column('message_id', sa.Integer(), nullable=False),
    sa.Column('filename', sqlmodel.sql.sqltypes.AutoString(), nullable=False),
    sa.Column('filesize', sa.Integer(), nullable=False),
    sa.Column('mime_type', sqlmodel.sql.sqltypes.AutoString(), nullable=True),
    sa.Column('created_at', sa.DateTime(), nullable=False),
    sa.Column('collection_id', sa.Integer(), nullable=False),
    sa.ForeignKeyConstraint(['collection_id'], ['collection.id'], ),
    sa.PrimaryKeyConstraint('id')
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_table('file')
    op.drop_table('downloadtask')
    op.drop_table('collection')
    op.drop_table('episode')
    op.drop_table('season')
    op.drop_table('uploadtask')
    with op.batch_alter_table('series', schema=None) as batch_op:
        batch_op.drop_index(batch_op.f('ix_series_tmdb_id'))

    op.drop_table('series')
    with op.batch_alter_table('movie', schema=None) as batch_op:
        batch_op.drop_index(batch_op.f('ix_movie_tmdb_id'))

    op.drop_table('movie')
