from pydantic_settings import BaseSettings, SettingsConfigDict

from enum import Enum



class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env", 
        env_file_encoding="utf-8",
        ignore_extra_fields=True
    )
    GEMINI_API_KEY: str



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