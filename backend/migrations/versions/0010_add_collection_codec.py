"""add codec to collection

Revision ID: 0010_collection_codec
Revises: 0009_forward_origin
Create Date: 2026-09-16

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
import sqlmodel


revision: str = '0010_collection_codec'
down_revision: Union[str, Sequence[str], None] = '0009_forward_origin'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    with op.batch_alter_table('collection', schema=None) as batch_op:
        batch_op.add_column(sa.Column('codec', sqlmodel.sql.sqltypes.AutoString(), nullable=True))


def downgrade() -> None:
    with op.batch_alter_table('collection', schema=None) as batch_op:
        batch_op.drop_column('codec')
