from logging.config import fileConfig

from alembic import context
from sqlmodel import SQLModel

from database import DATABASE_URL, engine
import models

_ = models  # Access module to register table schemas and satisfy static analysis

config = context.config

if config.config_file_name is not None:
    fileConfig(config.config_file_name)

target_metadata = SQLModel.metadata


def run_migrations_offline() -> None:
    """Emit the migration SQL without connecting to a database."""
    context.configure(
        url=DATABASE_URL,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
    )

    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    """Run the migrations against the application database."""
    connectable = config.attributes.get("connection", None)

    if connectable is not None:
        _configure_and_run(connectable)
        return

    with engine.connect() as connection:
        _configure_and_run(connection)


def _configure_and_run(connection) -> None:
    context.configure(
        connection=connection,
        target_metadata=target_metadata,
        render_as_batch=True,
    )

    with context.begin_transaction():
        context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
