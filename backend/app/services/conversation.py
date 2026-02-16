from sqlmodel import Session, select

from ..schemas.message import BaseMessage
from ..models.conversation import Conversation
from ..models.message import Message



async def list_conversations_from_db(session: Session):
    statement = select(Conversation).order_by(Conversation.created_at.desc())
    return session.exec(statement).all()



async def get_history_from_db(conversation_id: str, session: Session) -> list[BaseMessage]:
    statement = select(Message).where(Message.conversation_id == conversation_id).order_by(Message.created_at.asc())
    messages = session.exec(statement).all()
    return [BaseMessage(role=m.role, content=m.content, created_at=m.created_at) for m in messages]