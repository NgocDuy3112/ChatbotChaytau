from __future__ import annotations

from dataclasses import dataclass

from PyQt6.QtCore import QThread, pyqtSignal

from ..api.client import ApiError, ChatApiClient
from ..models.dto import ChatRequest


@dataclass(slots=True)
class StreamResult:
    conversation_id: str
    text: str
    status: str


class ChatStreamWorker(QThread):
    success = pyqtSignal(object)
    failed = pyqtSignal(str)

    def __init__(self, client: ChatApiClient, request: ChatRequest):
        super().__init__()
        self.client = client
        self.request = request

    def run(self) -> None:
        try:
            response = self.client.generate(self.request)
        except ApiError as exc:
            self.failed.emit(str(exc))
            return

        if self.isInterruptionRequested():
            return

        result = StreamResult(
            conversation_id=response.conversation_id,
            text=response.output.text(),
            status=response.status,
        )
        self.success.emit(result)
