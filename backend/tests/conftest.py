import os
import shutil
import tempfile

_TMP_DIR = tempfile.mkdtemp(prefix="cinegram-tests-")
os.environ["DATA_DIR"] = _TMP_DIR
os.environ["DATABASE_PATH"] = os.path.join(_TMP_DIR, "test.db")


def pytest_sessionfinish(session, exitstatus):
    shutil.rmtree(_TMP_DIR, ignore_errors=True)
