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


async def delete_conversation_from_db(conversation_id: str, session: Session) -> bool:
    conversation = session.get(Conversation, conversation_id)
    if conversation is None:
        return False

    message_statement = select(Message).where(Message.conversation_id == conversation_id)
    messages = session.exec(message_statement).all()
    for message in messages:
        session.delete(message)

    session.delete(conversation)
    session.commit()
    return True


async def rename_conversation_in_db(conversation_id: str, title: str, session: Session) -> Conversation | None:
    conversation = session.get(Conversation, conversation_id)
    if conversation is None:
        return None

    cleaned_title = title.strip()
    conversation.title = cleaned_title if cleaned_title else None
    session.add(conversation)
    session.commit()
    session.refresh(conversation)
    return conversation