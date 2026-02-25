from __future__ import annotations

import os
import sys
from pathlib import Path

try:
    from PyQt6.QtCore import QStandardPaths
except ImportError:
    QStandardPaths = None


def _is_valid_sheets_dir(path: Path) -> bool:
    if not path.exists() or not path.is_dir():
        return False

    required_candidates = [
        "LIST_BUNDLE.json",
        "List_CongTy.json",
        "List_VaiTro.json",
        "List_CongViec.json",
        "List_CongTy.csv",
        "List_VaiTro.csv",
        "List_CongViec.csv",
    ]
    return any((path / filename).exists() for filename in required_candidates)


def get_instructions_dir() -> Path:
    """
    Resolve the directory containing instruction templates.
    Priority:
    1. CHATBOT_INSTRUCTIONS_DIR environment variable
    2. User AppData directory (e.g., %APPDATA%/ChatbotDesktop/instructions)
    3. PyInstaller bundled _MEIPASS/resources/instructions
    4. Source code relative path (app/resources/instructions)
    """
    # 1. Environment variable override
    env_dir = os.getenv("CHATBOT_INSTRUCTIONS_DIR")
    if env_dir:
        env_path = Path(env_dir)
        if env_path.exists() and env_path.is_dir():
            return env_path

    # 2. User AppData directory
    if QStandardPaths is not None:
        appdata_str = QStandardPaths.writableLocation(QStandardPaths.StandardLocation.AppDataLocation)
        if appdata_str:
            appdata_path = Path(appdata_str) / "instructions"
            if appdata_path.exists() and appdata_path.is_dir():
                return appdata_path

    # 3. PyInstaller bundled data
    meipass = getattr(sys, "_MEIPASS", None)
    if meipass:
        bundled_path = Path(meipass) / "resources" / "instructions"
        if bundled_path.exists() and bundled_path.is_dir():
            return bundled_path

    # 4. Source code relative path (fallback)
    # This file is at app/client/utils/resources.py
    # So app/resources/instructions is 3 levels up + resources/instructions
    src_path = Path(__file__).resolve().parents[3] / "resources" / "instructions"
    return src_path


def get_sheets_dir() -> Path:
    """
    Resolve the directory containing sheet data files.
    Priority:
    1. CHATBOT_SHEETS_DIR environment variable
    2. User AppData directory (e.g., %APPDATA%/ChatbotDesktop/sheets)
    3. PyInstaller bundled _MEIPASS/resources/sheets
    4. Source code relative path (app/resources/sheets)
    """
    env_dir = os.getenv("CHATBOT_SHEETS_DIR")
    if env_dir:
        env_path = Path(env_dir)
        if _is_valid_sheets_dir(env_path):
            return env_path

    candidate_dirs: list[Path] = []

    if QStandardPaths is not None:
        appdata_str = QStandardPaths.writableLocation(QStandardPaths.StandardLocation.AppDataLocation)
        if appdata_str:
            candidate_dirs.append(Path(appdata_str) / "sheets")

    meipass = getattr(sys, "_MEIPASS", None)
    if meipass:
        candidate_dirs.append(Path(meipass) / "resources" / "sheets")

    candidate_dirs.append(Path(__file__).resolve().parents[3] / "resources" / "sheets")

    for directory in candidate_dirs:
        if _is_valid_sheets_dir(directory):
            return directory

    return candidate_dirs[-1]

