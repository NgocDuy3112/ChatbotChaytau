from sqlmodel import SQLModel, Field, Relationship
from datetime import datetime
import uuid

from ..models.message import Message



class Conversation(SQLModel, table=True):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()), primary_key=True)
    created_at: datetime = Field(default_factory=datetime.now)

    messages: list[Message] = Relationship(back_populates="conversation")