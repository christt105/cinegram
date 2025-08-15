import asyncio

class FileWorker:
    def __init__(self, queue, uploader):
        self.queue = queue
        self.uploader = uploader

    async def run(self):
        while True:
            file_path, file_name = await self.queue.get()
            try:
                await self.uploader.send(file_path, file_name)
            except Exception as e:
                print(f"❌ Error sending {file_name}: {e}")
            finally:
                self.queue.task_done()
