from collections.abc import Generator
from sqlmodel import create_engine, SQLModel, Session
from sqlalchemy import text
import pathlib
import os


def _project_root() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parents[3]


def _default_db_path() -> pathlib.Path:
    return _project_root() / "database.db"


def _legacy_server_db_path() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parents[1] / "database.db"


def _resolve_sqlite_file_path() -> pathlib.Path:
    external_db_path = (
        os.getenv("APP_DB_PATH")
        or os.getenv("SERVER_DB_PATH")
        or os.getenv("SQLITE_FILE_PATH")
    )
    if external_db_path:
        path = pathlib.Path(external_db_path).expanduser().resolve()
    else:
        default_path = _default_db_path()
        if default_path.exists():
            path = default_path
        else:
            legacy_path = _legacy_server_db_path()
            path = legacy_path if legacy_path.exists() else default_path

    path.parent.mkdir(parents=True, exist_ok=True)
    return path


sqlite_file_path = _resolve_sqlite_file_path()
sqlite_url = f"sqlite:///{sqlite_file_path.as_posix()}"


# create_engine for SQLite
# connect_args={"check_same_thread": False} is needed for SQLite to work with FastAPI
engine = create_engine(
    sqlite_url,
    echo=os.getenv("DEBUG", "false").lower() in ("1", "true", "yes"),
    connect_args={"check_same_thread": False},
)



def create_db_and_tables():
    # This imports are necessary to ensure SQLModel knows about the tables
    from ..models.conversation import Conversation
    from ..models.message import Message
    from ..models.file import UploadedFile
    from ..models.cache import CachedResponse
    
    SQLModel.metadata.create_all(engine)
    _ensure_conversation_title_column()


def _ensure_conversation_title_column() -> None:
    with engine.connect() as connection:
        table_info = connection.execute(text("PRAGMA table_info(conversation)"))
        existing_columns = {str(row[1]) for row in table_info.fetchall() if len(row) > 1}
        if "title" in existing_columns:
            return

        connection.execute(text("ALTER TABLE conversation ADD COLUMN title TEXT"))
        connection.commit()



def get_session() -> Generator[Session, None, None]:
    with Session(engine) as session:
        yield session
