import hashlib
import json
import os
import pathlib
from datetime import datetime, timedelta
from typing import Any, Optional
from sqlmodel import select

from ..logger import global_logger
from ..models.cache import CachedResponse


CACHE_KEY_SCHEMA_VERSION = "v2"


def _compute_file_hash(path: str) -> str:
    try:
        from .file_utils import get_file_hash

        p = pathlib.Path(path)
        if p.exists():
            return get_file_hash(p)
        return path
    except Exception as e:
        global_logger.debug(f"File hash error for {path}: {e}")
        return path


def make_request_key(payload: Any) -> str:
    """Create a deterministic hash for a ChatRequest-like object.

    The payload should have `model`, `input`, `instructions`, and `file_paths` attributes.
    """
    normalized = {
        "schema_version": CACHE_KEY_SCHEMA_VERSION,
        "model": getattr(payload, "model", ""),
        "input": getattr(payload, "input", ""),
        "instructions": getattr(payload, "instructions", "") or "",
        "file_hashes": [],
    }

    for p in getattr(payload, "file_paths", []) or []:
        normalized["file_hashes"].append(_compute_file_hash(p))

    payload_json = json.dumps(normalized, sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(payload_json.encode("utf-8")).hexdigest()


def get_cached_response(session, request_key: str) -> Optional[CachedResponse]:
    stmt = select(CachedResponse).where(CachedResponse.request_key == request_key)
    cached = session.exec(stmt).first()
    if not cached:
        return None
    if cached.expires_at and cached.expires_at < datetime.now():
        try:
            session.delete(cached)
            session.commit()
        except Exception:
            pass
        return None
    return cached


def store_cached_response(
    session,
    request_key: str,
    model: str,
    input_text: str,
    instructions: Optional[str],
    file_hashes: list[str],
    response_text: str,
    meta_data: Optional[dict] = None,
    ttl_days: Optional[int] = None,
):
    if ttl_days is None:
        try:
            ttl_days = int(os.getenv("CACHE_TTL_DAYS", "30"))
        except Exception:
            ttl_days = 30

    expires_at = datetime.now() + timedelta(days=ttl_days) if ttl_days > 0 else None

    stmt = select(CachedResponse).where(CachedResponse.request_key == request_key)
    cached = session.exec(stmt).first()
    if cached:
        cached.model = model
        cached.input_text = input_text
        cached.instructions = instructions
        cached.file_hashes = file_hashes
        cached.response_text = response_text
        cached.meta_data = meta_data or {}
        cached.created_at = datetime.now()
        cached.expires_at = expires_at
    else:
        cached = CachedResponse(
            request_key=request_key,
            model=model,
            input_text=input_text,
            instructions=instructions,
            file_hashes=file_hashes,
            response_text=response_text,
            meta_data=meta_data or {},
            created_at=datetime.now(),
            expires_at=expires_at,
        )
        session.add(cached)

    session.commit()
    session.refresh(cached)
    return cached
