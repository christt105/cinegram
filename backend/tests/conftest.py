import os
import shutil
import tempfile

# Set before the app modules are imported: database.py resolves the engine URL at
# import time, and the app lifespan runs the migrations against it.
_TMP_DIR = tempfile.mkdtemp(prefix="cinegram-tests-")
os.environ["DATA_DIR"] = _TMP_DIR
os.environ["DATABASE_PATH"] = os.path.join(_TMP_DIR, "test.db")


def pytest_sessionfinish(session, exitstatus):
    shutil.rmtree(_TMP_DIR, ignore_errors=True)
