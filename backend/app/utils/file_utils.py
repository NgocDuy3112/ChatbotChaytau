import pathlib
import hashlib
import zipfile
import xml.etree.ElementTree as ET
from typing import Any
from google.genai import types, Client

from ..configs import AcceptMimeTypes
from ..logger import global_logger



def get_file_hash(file_path: pathlib.Path) -> str:
    """Calculates the SHA-256 hash of a file."""
    sha256_hash = hashlib.sha256()
    with open(file_path, "rb") as f:
        # Read in chunks to handle large files
        for byte_block in iter(lambda: f.read(4096), b""):
            sha256_hash.update(byte_block)
    return sha256_hash.hexdigest()



def upload_file_to_gemini(client: Client, file_path: str) -> Any:
    """Uploads a file to Gemini File API and returns the File object."""
    path = pathlib.Path(file_path)
    if not path.exists():
        global_logger.error(f"File not found: {file_path}")
        raise FileNotFoundError(f"File not found: {file_path}")
    
    mime_type = get_mime_type(path)
    
    try:
        # Upload the file to the File API
        uploaded_file = client.files.upload(
            file=path,
            config={"mime_type": mime_type}
        )
        global_logger.info(f"File uploaded to Gemini: {uploaded_file.uri}")
        
        return uploaded_file
    except Exception as e:
        global_logger.error(f"Failed to upload file {file_path} to Gemini: {e}")
        raise


def extract_docx_text(file_path: pathlib.Path) -> str:
    """Extract plain text from a DOCX file."""
    try:
        with zipfile.ZipFile(file_path) as archive:
            xml_bytes = archive.read("word/document.xml")

        root = ET.fromstring(xml_bytes)
        text_parts: list[str] = []
        for node in root.iter():
            if node.tag.endswith("}t") and node.text:
                text_parts.append(node.text)

        return "\n".join(text_parts).strip()
    except Exception as e:
        global_logger.error(f"Failed to extract text from DOCX {file_path}: {e}")
        raise



def get_mime_type(file_path: pathlib.Path) -> str:
    """Returns the mime-type based on the file extension."""
    suffix = file_path.suffix.lower()
    
    match suffix:
        case ".pdf":
            return AcceptMimeTypes.PDF.value
        case ".doc":
            return AcceptMimeTypes.DOC.value
        case ".docx":
            return AcceptMimeTypes.DOCX.value
        case ".xls":
            return AcceptMimeTypes.XLS.value
        case ".xlsx":
            return AcceptMimeTypes.XLSX.value
        case ".ppt":
            return AcceptMimeTypes.PPT.value
        case ".pptx":
            return AcceptMimeTypes.PPTX.value
        case ".png":
            return AcceptMimeTypes.PNG.value
        case ".jpg" | ".jpeg":
            return AcceptMimeTypes.JPEG.value
        case ".webp":
            return AcceptMimeTypes.WEBP.value
        case ".heic":
            return AcceptMimeTypes.HEIC.value
        case ".heif":
            return AcceptMimeTypes.HEIF.value
        case _:
            raise ValueError(f"Unsupported file extension: {suffix}")