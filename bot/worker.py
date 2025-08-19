from logger import logger

class Worker:
    def __init__(self, queue):
        self.queue = queue

    async def run(self):
        self.running = True
        while self.running:
            task = await self.queue.get()
            logger.info(f"Running task: {task}")
            try:
                await task.run()
            except Exception as e:
                logger.error(f"Error running task {task}: {e}")
            finally:
                self.queue.task_done()
