from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from typing import Any


class Role(str, Enum):
    USER = "user"
    ASSISTANT = "assistant"
    SYSTEM = "system"


@dataclass(slots=True)
class BaseMessage:
    role: str
    content: dict[str, Any]
    id: str | None = None
    created_at: datetime = field(default_factory=datetime.now)

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "BaseMessage":
        return cls(
            id=str(payload.get("id")) if payload.get("id") else None,
            role=str(payload.get("role", Role.ASSISTANT.value)),
            content=payload.get("content") if isinstance(payload.get("content"), dict) else {"text": ""},
            created_at=parse_datetime(payload.get("created_at")),
        )

    def text(self) -> str:
        value = self.content.get("text", "")
        if isinstance(value, str):
            return value
        return str(value)


@dataclass(slots=True)
class Conversation:
    id: str
    title: str | None = None
    created_at: datetime = field(default_factory=datetime.now)

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "Conversation":
        raw_title = payload.get("title")
        return cls(
            id=str(payload.get("id", "")),
            title=raw_title.strip() if isinstance(raw_title, str) and raw_title.strip() else None,
            created_at=parse_datetime(payload.get("created_at")),
        )


@dataclass(slots=True)
class ChatRequest:
    input: str
    model: str
    conversation_id: str | None = None
    instructions: str | None = None
    file_paths: list[str] = field(default_factory=list)

    def to_payload(self) -> dict[str, Any]:
        payload: dict[str, Any] = {
            "conversation_id": self.conversation_id,
            "instructions": self.instructions,
            "input": self.input,
            "model": self.model,
            "file_paths": self.file_paths,
        }
        return payload


@dataclass(slots=True)
class ChatResponse:
    conversation_id: str
    output: BaseMessage
    status: str = "completed"
    created_at: datetime = field(default_factory=datetime.now)

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "ChatResponse":
        output_payload = payload.get("output")
        output = BaseMessage.from_dict(output_payload) if isinstance(output_payload, dict) else BaseMessage(role=Role.ASSISTANT.value, content={"text": ""})
        return cls(
            conversation_id=str(payload.get("conversation_id", "")),
            output=output,
            status=str(payload.get("status", "completed")),
            created_at=parse_datetime(payload.get("created_at")),
        )


def parse_datetime(value: Any) -> datetime:
    if isinstance(value, datetime):
        return value
    if isinstance(value, str) and value:
        normalized = value.replace("Z", "+00:00")
        try:
            return datetime.fromisoformat(normalized)
        except ValueError:
            return datetime.now()
    return datetime.now()
