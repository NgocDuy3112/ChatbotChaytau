from __future__ import annotations
from sqlmodel import SQLModel, Field
from datetime import datetime
import uuid

from ..configs import AcceptMimeTypes



class UploadedFile(SQLModel, table=True):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()), primary_key=True)
    local_path: str = Field(index=True)
    file_hash: str = Field(index=True)  # SHA-256 of the file content
    gemini_uri: str  # The URI returned by Google GenAI API
    mime_type: AcceptMimeTypes
    created_at: datetime = Field(default_factory=datetime.utcnow)
    # Gemini files typically expire after 48 hours
    expires_at: datetime = Field()