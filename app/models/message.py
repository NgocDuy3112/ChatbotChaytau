from sqlmodel import SQLModel, Field, Relationship
from sqlalchemy import Column, JSON
from typing import Literal, Any
from datetime import datetime
import uuid

from ..models.conversation import Conversation



class Message(SQLModel, table=True):
    id: str = Field(default_factory=lambda: "msg_" + str(uuid.uuid4()), primary_key=True)
    conversation_id: str = Field(foreign_key="conversation.id")
    role: Literal["user", "assistant", "system"]
    content: dict[str, Any] = Field(sa_column=Column(JSON))
    created_at: datetime = Field(default_factory=datetime.now)

    conversation: Conversation = Relationship(back_populates="messages")