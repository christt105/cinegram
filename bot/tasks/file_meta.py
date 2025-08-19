from dataclasses import dataclass
from datetime import datetime

@dataclass
class FileMeta:
    message_id: int
    filename: str
    filesize: int
    mime_type: str
    created_at: datetime
