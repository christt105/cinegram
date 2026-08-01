"""collection local path and download name suffix

Revision ID: 0002_versions
Revises: 0001_baseline
Create Date: 2026-07-29 19:42:09.374857

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
import sqlmodel


# revision identifiers, used by Alembic.
revision: str = '0002_versions'
down_revision: Union[str, Sequence[str], None] = '0001_baseline'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    with op.batch_alter_table('collection', schema=None) as batch_op:
        batch_op.add_column(sa.Column('local_path', sqlmodel.sql.sqltypes.AutoString(), nullable=True))

    with op.batch_alter_table('downloadtask', schema=None) as batch_op:
        batch_op.add_column(sa.Column('name_suffix', sqlmodel.sql.sqltypes.AutoString(), nullable=True))



def downgrade() -> None:
    """Downgrade schema."""
    with op.batch_alter_table('downloadtask', schema=None) as batch_op:
        batch_op.drop_column('name_suffix')

    with op.batch_alter_table('collection', schema=None) as batch_op:
        batch_op.drop_column('local_path')

