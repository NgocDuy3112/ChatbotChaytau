from pydantic import BaseModel, Field
from datetime import datetime

from ..schemas.message import *



class ChatRequest(BaseModel):
    conversation_id: str | None = Field(None, description="Unique identifier for the conversation")
    instructions: str | None = Field(None, description="System instructions for the AI model")
    input: str = Field(..., description="User input prompt for the AI model")
    model: str = Field("gemini-2.0-flash-exp", description="AI model to be used for the conversation")
    file_paths: list[str] | None = Field(
        default_factory=list, 
        description="List of file paths to be used as context for the conversation"
    )
    search_grounding: bool = Field(True, description="Enable Google Search grounding")



class ChatResponse(BaseModel):
    conversation_id: str = Field(..., description="Unique identifier for the conversation")
    created_at: datetime = Field(
        default_factory=datetime.now, description="The time the response was created"
    )
    output: BaseMessage = Field(..., description="Message in the response")
    status: Status = Field("pending", description="Status of the response")