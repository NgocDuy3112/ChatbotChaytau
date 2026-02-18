from __future__ import annotations

from typing import Any, Iterator

import httpx

from ..models.dto import BaseMessage, ChatRequest, ChatResponse, Conversation


class ApiError(RuntimeError):
    """Raised for API/network errors."""


class ChatApiClient:
    def __init__(self, base_url: str = "http://localhost:8000", timeout: float = 30.0):
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout

    def set_base_url(self, base_url: str) -> None:
        self.base_url = base_url.rstrip("/")

    def list_conversations(self) -> list[Conversation]:
        payload = self._request_json("GET", "/conversation/")
        if not isinstance(payload, list):
            raise ApiError("Phản hồi danh sách cuộc trò chuyện không hợp lệ")
        return [Conversation.from_dict(item) for item in payload]

    def get_history(self, conversation_id: str) -> list[BaseMessage]:
        payload = self._request_json("GET", f"/conversation/history/{conversation_id}")
        if not isinstance(payload, list):
            raise ApiError("Phản hồi lịch sử cuộc trò chuyện không hợp lệ")
        return [BaseMessage.from_dict(item) for item in payload]

    def delete_conversation(self, conversation_id: str) -> None:
        payload = self._request_json("DELETE", f"/conversation/{conversation_id}")
        if isinstance(payload, dict):
            status = str(payload.get("status", "")).lower()
            if status in {"deleted", "ok", "success"}:
                return
        raise ApiError("Phản hồi xóa cuộc trò chuyện không hợp lệ")

    def rename_conversation(self, conversation_id: str, title: str) -> Conversation:
        payload = self._request_json(
            "PATCH",
            f"/conversation/{conversation_id}/title",
            json={"title": title},
        )
        if not isinstance(payload, dict):
            raise ApiError("Phản hồi đổi tên cuộc trò chuyện không hợp lệ")
        return Conversation.from_dict(payload)

    def generate(self, request: ChatRequest) -> ChatResponse:
        payload = self._request_json("POST", "/chat/generate", json=request.to_payload())
        if not isinstance(payload, dict):
            raise ApiError("Phản hồi tạo câu trả lời không hợp lệ")
        return ChatResponse.from_dict(payload)

    def stream(self, request: ChatRequest) -> Iterator[str]:
        url = self._url("/chat/stream")
        try:
            with httpx.Client(timeout=None) as client:
                with client.stream(
                    "POST",
                    url,
                    json=request.to_payload(),
                    headers={"Accept": "text/event-stream"},
                ) as response:
                    self._raise_for_status(response)
                    for line in response.iter_lines():
                        if not line:
                            continue
                        text = line.strip()
                        if not text.startswith("data:"):
                            continue
                        chunk = text[5:].lstrip()
                        if chunk:
                            yield chunk
        except httpx.HTTPError as exc:
            raise ApiError(f"Streaming thất bại: {exc}") from exc

    def _request_json(
        self,
        method: str,
        path: str,
        json: dict[str, Any] | None = None,
    ) -> Any:
        url = self._url(path)
        try:
            with httpx.Client(timeout=self.timeout) as client:
                response = client.request(method=method, url=url, json=json)
                self._raise_for_status(response)
                return response.json()
        except httpx.HTTPError as exc:
            raise ApiError(f"Yêu cầu thất bại: {exc}") from exc
        except ValueError as exc:
            raise ApiError(f"Phản hồi JSON không hợp lệ từ {path}") from exc

    def _raise_for_status(self, response: httpx.Response) -> None:
        try:
            response.raise_for_status()
        except httpx.HTTPStatusError as exc:
            detail = ""
            try:
                payload = response.json()
                if isinstance(payload, dict):
                    detail = str(payload.get("detail", "")).strip()
            except ValueError:
                detail = response.text.strip()
            suffix = f" - {detail}" if detail else ""
            raise ApiError(f"Lỗi API {response.status_code}{suffix}") from exc

    def _url(self, path: str) -> str:
        if not path.startswith("/"):
            path = f"/{path}"
        return f"{self.base_url}{path}"
