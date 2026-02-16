from pydantic import BaseModel

from .message import BaseMessage



class ConversationHistory(BaseModel):
    conversation_id: str
    messages: list[BaseMessage]