from collections.abc import AsyncGenerator
from typing import Any
import uuid
from datetime import datetime
from google import genai
from google.genai import types
from sqlmodel import Session, select

from ..logger import global_logger
from ..schemas.chat import ChatRequest, ChatResponse
from ..schemas.message import BaseMessage
from ..utils.file_utils import upload_file_to_gemini
from ..models.conversation import Conversation
from ..models.message import Message




async def generate_chat_response(
    request: ChatRequest, 
    client: genai.Client,
    session: Session
) -> ChatResponse:
    # Handle conversation_id
    conversation_id = request.conversation_id or str(uuid.uuid4())
    
    # Check if conversation exists, create if not
    db_conversation = session.get(Conversation, conversation_id)
    if not db_conversation:
        db_conversation = Conversation(id=conversation_id)
        session.add(db_conversation)
        session.commit()
        session.refresh(db_conversation)

    # Save user message
    user_msg = Message(
        conversation_id=conversation_id,
        role="user",
        content={"text": request.input}
    )
    session.add(user_msg)
    
    # Construct contents with history
    contents: list[Any] = []
    
    # Get previous messages for context
    statement = select(Message).where(Message.conversation_id == conversation_id).order_by(Message.created_at)
    history = session.exec(statement).all()
    
    for msg in history:
        # Gemini expects 'user' or 'model' roles
        role = "model" if msg.role == "assistant" else msg.role
        contents.append(types.Content(role=role, parts=[types.Part(text=msg.content["text"])]))

    # Add files if any (adding to the last part for now)
    if request.file_paths:
        try:
            for file_path in request.file_paths:
                uploaded_file = upload_file_to_gemini(client, file_path)
                file_part = types.Part(
                    file_data=types.FileData(
                        file_uri=uploaded_file.uri, 
                        mime_type=uploaded_file.mime_type
                    )
                )
                if contents and contents[-1].role == "user":
                    contents[-1].parts.append(file_part)
                else:
                    contents.append(types.Content(role="user", parts=[file_part]))
        except Exception as e:
            global_logger.error(f"Error processing files: {e}")
            raise e
            
    config = None
    if request.instructions:
        config = types.GenerateContentConfig(system_instruction=request.instructions)
        
    try:
        response = client.models.generate_content(
            model=request.model, 
            contents=contents, 
            config=config
        )
        
        # Save assistant message
        assistant_msg = Message(
            conversation_id=conversation_id,
            role="assistant",
            content={"text": response.text or ""}
        )
        session.add(assistant_msg)
        session.commit()
        session.refresh(assistant_msg)
        
        output_message = BaseMessage(
            role="assistant", 
            content={"text": response.text or ""}, 
            created_at=assistant_msg.created_at
        )
        return ChatResponse(
            conversation_id=conversation_id, 
            output=output_message, 
            status="completed", 
            created_at=datetime.now()
        )   
    except Exception as e:
        session.rollback()
        global_logger.error(f"Gemini generation error: {e}")
        raise e



async def generate_chat_response_stream(
    request: ChatRequest, 
    client: genai.Client,
    session: Session
) -> AsyncGenerator[str, None]:
    # Stream implementation also needs history persistence
    conversation_id = request.conversation_id or str(uuid.uuid4())
    
    db_conversation = session.get(Conversation, conversation_id)
    if not db_conversation:
        db_conversation = Conversation(id=conversation_id)
        session.add(db_conversation)
        session.commit()

    user_msg = Message(
        conversation_id=conversation_id,
        role="user",
        content={"text": request.input}
    )
    session.add(user_msg)
    session.commit()

    contents: list[Any] = []
    statement = select(Message).where(Message.conversation_id == conversation_id).order_by(Message.created_at)
    history = session.exec(statement).all()
    
    for msg in history:
        role = "model" if msg.role == "assistant" else msg.role
        contents.append(types.Content(role=role, parts=[types.Part(text=msg.content["text"])]))

    if request.file_paths:
        try:
            for file_path in request.file_paths:
                uploaded_file = upload_file_to_gemini(client, file_path)
                file_part = types.Part(
                    file_data=types.FileData(
                        file_uri=uploaded_file.uri, 
                        mime_type=uploaded_file.mime_type
                    )
                )
                if contents and contents[-1].role == "user":
                    contents[-1].parts.append(file_part)
                else:
                    contents.append(types.Content(role="user", parts=[file_part]))
        except Exception as e:
            global_logger.error(f"Error processing files: {e}")
            raise e
            
    config = None
    if request.instructions:
        config = types.GenerateContentConfig(system_instruction=request.instructions)
        
    full_response_text = ""
    try:
        for chunk in client.models.generate_content_stream(
            model=request.model,
            contents=contents,
            config=config
        ):
            if chunk.text:
                full_response_text += chunk.text
                yield chunk.text
        
        # After stream ends, save assistant message
        assistant_msg = Message(
            conversation_id=conversation_id,
            role="assistant",
            content={"text": full_response_text}
        )
        session.add(assistant_msg)
        session.commit()
        
    except Exception as e:
        session.rollback()
        global_logger.error(f"Gemini streaming error: {e}")
        raise e
