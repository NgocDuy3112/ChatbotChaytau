from sqlmodel import SQLModel, Field, Relationship
from sqlalchemy.orm import relationship
from datetime import datetime
from typing import TYPE_CHECKING
import uuid


if TYPE_CHECKING:
    from .message import Message

class Conversation(SQLModel, table=True):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()), primary_key=True)
    created_at: datetime = Field(default_factory=datetime.now)

    messages: list["Message"] = Relationship(
        sa_relationship=relationship("Message", back_populates="conversation")
    )
