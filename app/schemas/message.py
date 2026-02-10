from __future__ import annotations
from pydantic import BaseModel, Field
from typing import Any
from datetime import datetime
from enum import Enum
import uuid


class Role(str, Enum):
    USER = "user"
    ASSISTANT = "assistant"
    SYSTEM = "system"


class Status(str, Enum):
    PENDING = "pending"
    COMPLETED = "completed"
    FAILED = "failed"


class BaseMessage(BaseModel):
    id: str = Field(default_factory=lambda: "msg_" + str(uuid.uuid4()), description="Unique identifier for the message")
    role: Role
    content: dict[str, Any]
    created_at: datetime = Field(default_factory=datetime.now)