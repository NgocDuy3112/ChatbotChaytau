from typing import Any, AsyncGenerator
import uuid
from datetime import datetime
from google import genai
from google.genai import types

from ..logger import global_logger
from ..schemas.chat import ChatRequest, ChatResponse
from ..schemas.message import BaseMessage
from ..utils.file_utils import upload_file_to_gemini




async def generate_chat_response(request: ChatRequest, client: genai.Client) -> ChatResponse:
    # FINAL CODE
    conversation_id = request.conversation_id or str(uuid.uuid4())
    contents: list[Any] = [request.input]
    if request.file_paths:
        try:
            for file_path in request.file_paths:
                file_part = upload_file_to_gemini(client, file_path)
                contents.append(file_part)
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
        output_message = BaseMessage(
            role="assistant", 
            content={"text": response.text or ""}, 
            created_at=datetime.now()
        )
        return ChatResponse(
            conversation_id=conversation_id, 
            output=output_message, 
            status="completed", 
            created_at=datetime.now()
        )   
    except Exception as e:
        global_logger.error(f"Gemini generation error: {e}")
        raise e



async def generate_chat_response_stream(
    request: ChatRequest, client: genai.Client
) -> AsyncGenerator[str, None]:
    contents: list[Any] = [request.input]
    if request.file_paths:
        try:
            for file_path in request.file_paths:
                file_part = upload_file_to_gemini(client, file_path)
                contents.append(file_part)
        except Exception as e:
            global_logger.error(f"Error processing files: {e}")
            raise e
            
    config = None
    if request.instructions:
        config = types.GenerateContentConfig(system_instruction=request.instructions)
        
    try:
        for chunk in client.models.generate_content_stream(
            model=request.model,
            contents=contents,
            config=config
        ):
            if chunk.text:
                yield chunk.text
    except Exception as e:
        global_logger.error(f"Gemini streaming error: {e}")
        raise e
