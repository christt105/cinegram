from typing import List
from pydantic import BaseModel


class BatchIdentifyRequest(BaseModel):
    collection_ids: List[int]
    tmdb_id: int

class BatchDeleteRequest(BaseModel):
    collection_ids: List[int]
