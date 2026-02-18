from pydantic_settings import BaseSettings, SettingsConfigDict
from functools import lru_cache
import os
import pathlib

from enum import Enum



class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file_encoding="utf-8",
        extra="ignore"
    )
    GEMINI_API_KEY: str


def _project_root() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parents[2]


def _default_env_file() -> pathlib.Path:
    return _project_root() / ".env"


def _legacy_server_env_file() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parent / ".env"


def resolve_env_file() -> str | None:
    external_env_file = os.getenv("APP_ENV_FILE") or os.getenv("SERVER_ENV_FILE")
    if external_env_file:
        return str(pathlib.Path(external_env_file).expanduser().resolve())

    default_env_file = _default_env_file()
    if default_env_file.exists():
        return str(default_env_file)

    legacy_env_file = _legacy_server_env_file()
    if legacy_env_file.exists():
        return str(legacy_env_file)

    return None


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    env_file = resolve_env_file()
    if env_file:
        return Settings(_env_file=env_file)
    return Settings()



class AcceptMimeTypes(str, Enum):
    PDF = "application/pdf"
    DOC = "application/msword"
    DOCX = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    XLS = "application/vnd.ms-excel"
    XLSX = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    PPT = "application/vnd.ms-powerpoint"
    PPTX = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    PNG = "image/png"
    JPEG = "image/jpeg"
    WEBP = "image/webp"
    HEIC = "image/heic"
    HEIF = "image/heif"