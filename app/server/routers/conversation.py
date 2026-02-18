from fastapi import APIRouter, Depends, HTTPException

from ..services.conversation import *
from ..dependencies.database import get_session
from ..schemas.conversation import RenameConversationRequest




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


@router.delete("/{conversation_id}")
async def delete_conversation(
    conversation_id: str,
    session: Session = Depends(get_session),
):
    try:
        deleted = await delete_conversation_from_db(conversation_id, session)
        if not deleted:
            raise HTTPException(status_code=404, detail="Không tìm thấy cuộc trò chuyện")
        return {"status": "deleted", "conversation_id": conversation_id}
    except HTTPException:
        raise
    except Exception as e:
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))


@router.patch("/{conversation_id}/title", response_model=Conversation)
async def rename_conversation(
    conversation_id: str,
    request: RenameConversationRequest,
    session: Session = Depends(get_session),
):
    try:
        conversation = await rename_conversation_in_db(conversation_id, request.title, session)
        if conversation is None:
            raise HTTPException(status_code=404, detail="Không tìm thấy cuộc trò chuyện")
        return conversation
    except HTTPException:
        raise
    except Exception as e:
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))