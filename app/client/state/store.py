from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime


@dataclass(slots=True)
class ChatMessage:
    role: str
    text: str
    attachment_names: list[str] = field(default_factory=list)
    created_at: datetime = field(default_factory=datetime.now)


@dataclass(slots=True)
class ChatState:
    current_conversation_id: str | None = None
    messages: list[ChatMessage] = field(default_factory=list)
    attached_paths: list[str] = field(default_factory=list)

    def reset_chat(self) -> None:
        self.current_conversation_id = None
        self.messages.clear()
        self.attached_paths.clear()

    def set_messages(self, messages: list[ChatMessage]) -> None:
        self.messages = list(messages)

    def add_message(self, role: str, text: str, attachment_names: list[str] | None = None) -> None:
        self.messages.append(
            ChatMessage(
                role=role,
                text=text,
                attachment_names=list(attachment_names or []),
            )
        )

    def append_or_create_assistant_chunk(self, chunk: str) -> None:
        if self.messages and self.messages[-1].role == "assistant":
            self.messages[-1].text += chunk
            return
        self.messages.append(ChatMessage(role="assistant", text=chunk))
