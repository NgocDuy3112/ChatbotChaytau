from fastapi import APIRouter, Depends

from ..services.conversation import *
from ..dependencies.database import get_session




router = APIRouter(prefix="/conversation", tags=["chat"])


@router.get("/", response_model=list[Conversation])
async def list_conversations(session: Session = Depends(get_session)):
    try:
        return await list_conversations_from_db(session)
    except Exception as e:
        import traceback
        traceback.print_exc()
        from fastapi import HTTPException
        raise HTTPException(status_code=500, detail=str(e))


@router.get("/history/{conversation_id}", response_model=list[BaseMessage])
async def get_history(
    conversation_id: str,
    session: Session = Depends(get_session),
):
    try:
        return await get_history_from_db(conversation_id, session)
    except Exception as e:
        import traceback
        traceback.print_exc()
        from fastapi import HTTPException
        raise HTTPException(status_code=500, detail=str(e))