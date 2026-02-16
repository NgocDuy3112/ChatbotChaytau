from collections.abc import Generator
from sqlmodel import create_engine, SQLModel, Session
from sqlalchemy.pool import StaticPool
import os

# SQLite database file path
sqlite_file_name = "database.db"
sqlite_url = f"sqlite:///{sqlite_file_name}"


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



def get_session() -> Generator[Session, None, None]:
    with Session(engine) as session:
        yield session
