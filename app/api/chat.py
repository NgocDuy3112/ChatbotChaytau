from fastapi import APIRouter, Depends
from fastapi.responses import StreamingResponse

from ..services.chat import generate_chat_response, generate_chat_response_stream
from ..schemas.chat import ChatRequest, ChatResponse
from ..dependencies.gemini_client import get_gemini_client


router = APIRouter(prefix="/chat", tags=["chat"])




@router.post("/generate", response_model=ChatResponse)
async def chat_generate(request: ChatRequest, client = Depends(get_gemini_client)):
    response = await generate_chat_response(request, client)
    return response



@router.post("/stream")
async def chat_generate_stream(request: ChatRequest, client = Depends(get_gemini_client)):
    return StreamingResponse(
        generate_chat_response_stream(request, client),
        media_type="text/event-stream"
    )
