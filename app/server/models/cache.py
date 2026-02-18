from sqlmodel import SQLModel, Field
from sqlalchemy import Column, JSON, Text
from datetime import datetime
import uuid


class CachedResponse(SQLModel, table=True):
    id: str = Field(default_factory=lambda: "cache_" + str(uuid.uuid4()), primary_key=True)
    request_key: str = Field(index=True)
    model: str
    input_text: str
    instructions: str | None = None
    file_hashes: list[str] = Field(sa_column=Column(JSON), default_factory=list)
    response_text: str = Field(sa_column=Column(Text))
    meta_data: dict = Field(sa_column=Column(JSON), default_factory=dict)
    created_at: datetime = Field(default_factory=datetime.now)
    expires_at: datetime | None = None
