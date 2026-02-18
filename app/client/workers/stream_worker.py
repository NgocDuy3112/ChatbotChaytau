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
    chunk_received = pyqtSignal(str)
    success = pyqtSignal(object)
    failed = pyqtSignal(str)

    def __init__(self, client: ChatApiClient, request: ChatRequest):
        super().__init__()
        self.client = client
        self.request = request

    def run(self) -> None:
        full_text = ""
        received_any_chunk = False

        try:
            for chunk in self.client.stream(self.request):
                if self.isInterruptionRequested():
                    return
                received_any_chunk = True
                full_text += chunk
                self.chunk_received.emit(chunk)

        except ApiError as stream_error:
            if not received_any_chunk:
                try:
                    response = self.client.generate(self.request)
                    result = StreamResult(
                        conversation_id=response.conversation_id,
                        text=response.output.text(),
                        status=response.status,
                    )
                    self.success.emit(result)
                    return
                except ApiError as fallback_error:
                    self.failed.emit(str(fallback_error))
                    return
            self.failed.emit(str(stream_error))
            return

        result = StreamResult(
            conversation_id=self.request.conversation_id or "",
            text=full_text,
            status="completed",
        )
        self.success.emit(result)
