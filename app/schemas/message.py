from pydantic import BaseModel, Field
from typing import Literal, Any
from datetime import datetime
import uuid


Role = Literal["user", "assistant", "system"]
Status = Literal["pending", "completed", "failed"]


class BaseMessage(BaseModel):
    id: str = Field(default_factory=lambda: "msg_" + str(uuid.uuid4()), description="Unique identifier for the message")
    role: Role
    content: dict[str, Any]
    created_at: datetime = Field(default_factory=datetime.now)