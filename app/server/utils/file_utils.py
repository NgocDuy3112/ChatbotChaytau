import pathlib
import hashlib
import re
import shutil
import tempfile
import unicodedata
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
    upload_path = path
    temp_dir: pathlib.Path | None = None

    if _path_has_non_ascii(path):
        try:
            temp_dir = pathlib.Path(tempfile.mkdtemp(prefix="gemini_upload_"))
            safe_name = _ascii_safe_filename(path.stem, path.suffix)
            upload_path = temp_dir / safe_name
            shutil.copy2(path, upload_path)
        except Exception as exc:
            global_logger.warning(
                f"Could not create ASCII-safe temp copy for {path.name}, fallback to original path: {exc}"
            )
            upload_path = path
            if temp_dir:
                shutil.rmtree(temp_dir, ignore_errors=True)
                temp_dir = None
    
    try:
        # Upload the file to the File API
        uploaded_file = client.files.upload(
            file=upload_path,
            config={"mime_type": mime_type}
        )
        global_logger.info(f"File uploaded to Gemini: {uploaded_file.uri}")
        
        return uploaded_file
    except Exception as e:
        global_logger.error(f"Failed to upload file {file_path} to Gemini: {e}")
        raise
    finally:
        if temp_dir:
            shutil.rmtree(temp_dir, ignore_errors=True)


def _path_has_non_ascii(path: pathlib.Path) -> bool:
    try:
        str(path).encode("ascii")
        return False
    except UnicodeEncodeError:
        return True


def _ascii_safe_filename(stem: str, suffix: str) -> str:
    normalized = unicodedata.normalize("NFKD", stem)
    ascii_stem = normalized.encode("ascii", "ignore").decode("ascii")
    ascii_stem = re.sub(r"[^A-Za-z0-9._-]+", "_", ascii_stem).strip("._-")
    if not ascii_stem:
        ascii_stem = "file"
    return f"{ascii_stem}{suffix}"


def extract_docx_text(file_path: pathlib.Path) -> str:
    """Extract plain text from a DOCX file.

    This parser reads multiple XML parts to handle files where content is not
    only in ``word/document.xml`` (headers, footers, comments, footnotes, etc).
    """
    try:
        with zipfile.ZipFile(file_path) as archive:
            part_names = _ordered_docx_xml_parts(archive)
            text_chunks: list[str] = []

            for part_name in part_names:
                try:
                    xml_bytes = archive.read(part_name)
                except KeyError:
                    continue

                chunk = _extract_text_from_docx_xml(xml_bytes)
                if chunk:
                    text_chunks.append(chunk)

        text = "\n\n".join(text_chunks).strip()
        global_logger.info(
            f"Extracted DOCX text from {file_path.name}: {len(text)} characters"
        )
        return text
    except Exception as e:
        global_logger.error(f"Failed to extract text from DOCX {file_path}: {e}")
        raise


def extract_xlsx_text(file_path: pathlib.Path) -> str:
    """Extract readable text from an XLSX workbook.

    The parser reads worksheet XML files and resolves shared strings when
    present. Output is a plain-text summary grouped by worksheet.
    """
    try:
        with zipfile.ZipFile(file_path) as archive:
            sheet_parts = _ordered_xlsx_sheet_xml_parts(archive)
            if not sheet_parts:
                return ""

            shared_strings = _read_xlsx_shared_strings(archive)
            sheet_chunks: list[str] = []

            for index, sheet_part in enumerate(sheet_parts, start=1):
                try:
                    xml_bytes = archive.read(sheet_part)
                except KeyError:
                    continue

                row_lines = _extract_rows_from_xlsx_sheet_xml(xml_bytes, shared_strings)
                if not row_lines:
                    continue

                sheet_name = pathlib.Path(sheet_part).stem
                chunk = f"Sheet {index} ({sheet_name})\n" + "\n".join(row_lines)
                sheet_chunks.append(chunk)

        text = "\n\n".join(sheet_chunks).strip()
        global_logger.info(
            f"Extracted XLSX text from {file_path.name}: {len(text)} characters"
        )
        return text
    except Exception as e:
        global_logger.error(f"Failed to extract text from XLSX {file_path}: {e}")
        raise


def _ordered_docx_xml_parts(archive: zipfile.ZipFile) -> list[str]:
    """Return DOCX XML part names in a preferred processing order."""
    names = [
        n
        for n in archive.namelist()
        if n.startswith("word/")
        and n.endswith(".xml")
        and not n.startswith("word/_rels/")
    ]

    preferred_prefixes = (
        "word/document.xml",
        "word/header",
        "word/footer",
        "word/footnotes.xml",
        "word/endnotes.xml",
        "word/comments.xml",
    )

    def _order_key(name: str) -> tuple[int, str]:
        for index, prefix in enumerate(preferred_prefixes):
            if name.startswith(prefix):
                return (index, name)
        return (len(preferred_prefixes), name)

    return sorted(names, key=_order_key)


def _extract_text_from_docx_xml(xml_bytes: bytes) -> str:
    """Extract readable text from a DOCX XML part."""
    try:
        root = ET.fromstring(xml_bytes)
    except ET.ParseError:
        return ""

    paragraph_texts: list[str] = []
    for paragraph in root.iter():
        if not paragraph.tag.endswith("}p"):
            continue

        run_text: list[str] = []
        for node in paragraph.iter():
            if node.tag.endswith("}t") and node.text:
                run_text.append(node.text)
            elif node.tag.endswith("}tab"):
                run_text.append("\t")
            elif node.tag.endswith("}br") or node.tag.endswith("}cr"):
                run_text.append("\n")

        text = "".join(run_text).strip()
        if text:
            paragraph_texts.append(text)

    if paragraph_texts:
        return "\n".join(paragraph_texts)

    fallback_parts: list[str] = []
    for node in root.iter():
        if node.tag.endswith("}t") and node.text:
            fallback_parts.append(node.text)
    return "\n".join(fallback_parts).strip()


def _ordered_xlsx_sheet_xml_parts(archive: zipfile.ZipFile) -> list[str]:
    names = [
        name
        for name in archive.namelist()
        if name.startswith("xl/worksheets/") and name.endswith(".xml")
    ]

    def _order_key(name: str) -> tuple[int, str]:
        match = re.search(r"sheet(\d+)\.xml$", name)
        if match:
            return (int(match.group(1)), name)
        return (10_000, name)

    return sorted(names, key=_order_key)


def _read_xlsx_shared_strings(archive: zipfile.ZipFile) -> list[str]:
    try:
        xml_bytes = archive.read("xl/sharedStrings.xml")
    except KeyError:
        return []

    try:
        root = ET.fromstring(xml_bytes)
    except ET.ParseError:
        return []

    strings: list[str] = []
    for item in root.iter():
        if not item.tag.endswith("}si"):
            continue

        parts: list[str] = []
        for node in item.iter():
            if node.tag.endswith("}t") and node.text:
                parts.append(node.text)
        strings.append("".join(parts).strip())

    return strings


def _extract_rows_from_xlsx_sheet_xml(
    xml_bytes: bytes,
    shared_strings: list[str],
    max_rows: int = 600,
) -> list[str]:
    try:
        root = ET.fromstring(xml_bytes)
    except ET.ParseError:
        return []

    rows: list[str] = []

    for row in root.iter():
        if not row.tag.endswith("}row"):
            continue

        cells: list[str] = []
        for cell in row:
            if not cell.tag.endswith("}c"):
                continue
            value = _extract_xlsx_cell_value(cell, shared_strings)
            if value:
                cells.append(value)

        if cells:
            rows.append(" | ".join(cells))

        if len(rows) >= max_rows:
            rows.append("[...] truncated")
            break

    return rows


def _extract_xlsx_cell_value(cell: ET.Element, shared_strings: list[str]) -> str:
    cell_type = cell.attrib.get("t", "")

    if cell_type == "inlineStr":
        inline_parts: list[str] = []
        for node in cell.iter():
            if node.tag.endswith("}t") and node.text:
                inline_parts.append(node.text)
        return _normalize_extracted_text("".join(inline_parts))

    raw_value = ""
    for node in cell:
        if node.tag.endswith("}v") and node.text is not None:
            raw_value = node.text
            break

    if not raw_value:
        return ""

    if cell_type == "s":
        try:
            index = int(raw_value)
            if 0 <= index < len(shared_strings):
                return _normalize_extracted_text(shared_strings[index])
            return ""
        except ValueError:
            return ""

    if cell_type == "b":
        return "TRUE" if raw_value == "1" else "FALSE"

    return _normalize_extracted_text(raw_value)


def _normalize_extracted_text(value: str, max_chars: int = 400) -> str:
    normalized = " ".join(value.replace("\r", " ").replace("\n", " ").split()).strip()
    if len(normalized) > max_chars:
        return normalized[:max_chars] + "..."
    return normalized



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