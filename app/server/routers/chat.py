from fastapi import APIRouter, Depends
from fastapi.responses import StreamingResponse

from ..services.chat import generate_chat_response, generate_chat_response_stream
from ..schemas.chat import ChatRequest, ChatResponse
from ..schemas.message import BaseMessage
from ..dependencies.gemini_client import get_gemini_client
from ..dependencies.database import get_session
from sqlmodel import Session, select
from ..models.conversation import Conversation
from ..models.message import Message


router = APIRouter(prefix="/chat", tags=["chat"])



@router.post("/generate", response_model=ChatResponse)
async def chat_generate(
    request: ChatRequest, 
    client = Depends(get_gemini_client),
    session: Session = Depends(get_session)
):
    try:
        return await generate_chat_response(request, client, session)
    except Exception as e:
        import traceback
        traceback.print_exc()
        from fastapi import HTTPException
        raise HTTPException(status_code=500, detail=str(e))


@router.post("/stream")
async def chat_generate_stream(
    request: ChatRequest, 
    client = Depends(get_gemini_client),
    session: Session = Depends(get_session)
):
    async def event_generator():
        try:
            async for chunk in generate_chat_response_stream(request, client, session):
                # Ensure each chunk is yielded as an SSE data packet
                yield f"data:{chunk}\n\n"
        except Exception as e:
            import traceback
            traceback.print_exc()
            yield f"data:Error: {str(e)}\n\n"

    return StreamingResponse(event_generator(), media_type="text/event-stream")
