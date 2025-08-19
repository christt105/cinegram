from logger import logger
from tasks.task import Task
from backend_client import BackendClient
from tasks.file_meta import FileMeta

class UploadFileTask(Task):
    def __init__(self, file_meta: FileMeta, backend_client: BackendClient):
        self.file_meta = file_meta
        self.backend_client = backend_client

    async def run(self):
        logger.info(f"Uploading metadata for file: {self.file_meta.filename} (ID: {self.file_meta.message_id})")
        try:
            result = await self.backend_client.upload(self.file_meta)
            logger.info(f"✅ Upload successful: {result}")
        except Exception as e:
            logger.error(f"❌ Upload failed: {e}")
        
        