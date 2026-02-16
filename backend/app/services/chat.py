from collections.abc import AsyncGenerator
from typing import Any, List
import uuid
from datetime import datetime
import pathlib

from google import genai
from google.genai import types
from sqlmodel import Session, select

from ..logger import global_logger
from ..schemas.chat import ChatRequest, ChatResponse
from ..schemas.message import BaseMessage
from ..utils.file_utils import upload_file_to_gemini, get_file_hash, extract_docx_text
from ..utils.cache import make_request_key, get_cached_response, store_cached_response
from ..models.conversation import Conversation
from ..models.message import Message


def _gather_history_contents(session: Session, conversation_id: str) -> List[types.Content]:
    """Load conversation history from DB and convert to Gemini contents."""
    stmt = select(Message).where(Message.conversation_id == conversation_id).order_by(Message.created_at)
    history = session.exec(stmt).all()
    contents: list[types.Content] = []
    for msg in history:
        raw_role = getattr(msg.role, "value", msg.role)
        normalized_role = str(raw_role).strip().lower()
        if normalized_role == "assistant":
            role = "model"
        elif normalized_role in {"user", "model"}:
            role = normalized_role
        else:
            role = "user"
        text = ""
        try:
            if isinstance(msg.content, dict):
                text = msg.content.get("text", "")
            else:
                text = str(msg.content)
        except Exception:
            text = str(msg.content)
        contents.append(types.Content(role=role, parts=[types.Part(text=text)]))
    return contents

    
def _attach_files_to_contents(client: genai.Client, contents: List[types.Content], file_paths: List[str]) -> list[str]:
    """Upload files to Gemini and attach them to the contents; return list of file hashes."""
    file_hashes: list[str] = []
    for file_path in file_paths or []:
        p = pathlib.Path(file_path)
        if not p.exists():
            raise FileNotFoundError(f"File not found: {file_path}")

        try:
            fh = get_file_hash(p)
        except Exception:
            fh = file_path
        file_hashes.append(fh)

        if p.suffix.lower() == ".docx":
            extracted_text = extract_docx_text(p)
            if not extracted_text:
                extracted_text = "(No extractable text found in DOCX file.)"

            text_part = types.Part(
                text=f"Content from {p.name}:\n{extracted_text[:100000]}"
            )
            if contents and contents[-1].role == "user":
                contents[-1].parts.append(text_part)
            else:
                contents.append(types.Content(role="user", parts=[text_part]))
            continue

        uploaded = upload_file_to_gemini(client, file_path)
        file_part = types.Part(file_data=types.FileData(file_uri=uploaded.uri, mime_type=uploaded.mime_type))
        if contents and contents[-1].role == "user":
            contents[-1].parts.append(file_part)
        else:
            contents.append(types.Content(role="user", parts=[file_part]))

    return file_hashes


async def generate_chat_response(request: ChatRequest, client: genai.Client, session: Session) -> ChatResponse:
    """Generate a single (non-stream) chat response.

    Behavior:
    - Persist user message
    - Check local cache; return cached response if available
    - Otherwise call Gemini, persist assistant message and cache the result
    - On Gemini failure, attempt to return a cached offline response
    """
    conversation_id = request.conversation_id or str(uuid.uuid4())

    # Ensure conversation exists
    db_conv = session.get(Conversation, conversation_id)
    if not db_conv:
        db_conv = Conversation(id=conversation_id)
        session.add(db_conv)

    # Persist user message (flush so subsequent queries see it)
    user_msg = Message(conversation_id=conversation_id, role="user", content={"text": request.input})
    session.add(user_msg)
    try:
        session.flush()
    except Exception:
        # flush is best-effort; ignore failures here and continue
        pass

    # Build request key (includes file hashes)
    request_key = make_request_key(request)

    # Try cache first
    try:
        cached = get_cached_response(session, request_key)
        if cached:
            assistant_msg = Message(conversation_id=conversation_id, role="assistant", content={"text": cached.response_text})
            session.add(assistant_msg)
            session.commit()
            session.refresh(assistant_msg)

            output = BaseMessage(role="assistant", content={"text": cached.response_text}, created_at=assistant_msg.created_at)
            return ChatResponse(conversation_id=conversation_id, output=output, status="cached", created_at=assistant_msg.created_at)
    except Exception as e:
        global_logger.debug(f"Cache lookup failed: {e}")

    # Construct contents from history (includes the user message we just added)
    contents: list[types.Content] = _gather_history_contents(session, conversation_id)

    # Attach files (uploads) when needed
    file_hashes: list[str] = []
    if request.file_paths:
        file_hashes = _attach_files_to_contents(client, contents, request.file_paths)

    config = types.GenerateContentConfig(system_instruction=request.instructions) if request.instructions else None

    try:
        response = client.models.generate_content(model=request.model, contents=contents, config=config)

        assistant_msg = Message(conversation_id=conversation_id, role="assistant", content={"text": response.text or ""})
        session.add(assistant_msg)
        session.commit()
        session.refresh(assistant_msg)

        # Best-effort cache store
        try:
            store_cached_response(session, request_key, request.model, request.input, request.instructions, file_hashes, response.text or "")
        except Exception as e:
            global_logger.debug(f"Failed to store cache: {e}")

        output = BaseMessage(role="assistant", content={"text": response.text or ""}, created_at=assistant_msg.created_at)
        return ChatResponse(conversation_id=conversation_id, output=output, status="completed", created_at=datetime.now())

    except Exception as exc:
        session.rollback()
        global_logger.error(f"Gemini generation error: {exc}")
        # Fallback to cached offline response if available
        try:
            cached = get_cached_response(session, request_key)
            if cached:
                assistant_msg = Message(conversation_id=conversation_id, role="assistant", content={"text": cached.response_text})
                session.add(assistant_msg)
                session.commit()
                session.refresh(assistant_msg)

                output = BaseMessage(role="assistant", content={"text": cached.response_text}, created_at=assistant_msg.created_at)
                return ChatResponse(conversation_id=conversation_id, output=output, status="cached_offline", created_at=assistant_msg.created_at)
        except Exception:
            pass
        raise


async def generate_chat_response_stream(request: ChatRequest, client: genai.Client, session: Session) -> AsyncGenerator[str, None]:
    """Generator that yields response chunks (SSE/stream) and caches the full result.

    If a cached response exists, yields it as a single chunk and returns.
    """
    conversation_id = request.conversation_id or str(uuid.uuid4())

    db_conv = session.get(Conversation, conversation_id)
    if not db_conv:
        db_conv = Conversation(id=conversation_id)
        session.add(db_conv)
        session.commit()

    user_msg = Message(conversation_id=conversation_id, role="user", content={"text": request.input})
    session.add(user_msg)
    try:
        session.flush()
    except Exception:
        pass

    request_key = make_request_key(request)

    # If cached, return single-chunk cached response
    try:
        cached = get_cached_response(session, request_key)
        if cached:
            assistant_msg = Message(conversation_id=conversation_id, role="assistant", content={"text": cached.response_text})
            session.add(assistant_msg)
            session.commit()
            yield cached.response_text
            return
    except Exception as e:
        global_logger.debug(f"Cache lookup failed: {e}")

    # Build contents (history + optional files)
    contents: list[types.Content] = _gather_history_contents(session, conversation_id)
    file_hashes: list[str] = []
    if request.file_paths:
        file_hashes = _attach_files_to_contents(client, contents, request.file_paths)

    config = types.GenerateContentConfig(system_instruction=request.instructions) if request.instructions else None

    full_text = ""
    try:
        for chunk in client.models.generate_content_stream(model=request.model, contents=contents, config=config):
            if getattr(chunk, "text", None):
                text = chunk.text
                full_text += text
                yield text

        # Persist assistant message and cache the full text
        assistant_msg = Message(conversation_id=conversation_id, role="assistant", content={"text": full_text})
        session.add(assistant_msg)
        session.commit()
        try:
            store_cached_response(session, request_key, request.model, request.input, request.instructions, file_hashes, full_text)
        except Exception as e:
            global_logger.debug(f"Failed to store cache after stream: {e}")

    except Exception as exc:
        session.rollback()
        global_logger.error(f"Gemini streaming error: {exc}")
        # Fallback: yield cached offline if available
        try:
            cached = get_cached_response(session, request_key)
            if cached:
                yield cached.response_text
                return
        except Exception:
            pass
        raise
