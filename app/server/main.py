from fastapi import FastAPI
from contextlib import asynccontextmanager

from .routers import chat, conversation
from .dependencies.gemini_client import get_gemini_client
from .dependencies.database import create_db_and_tables


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Initialize database
    create_db_and_tables()
    gemini_client = get_gemini_client()
    app.state.gemini_client = gemini_client
    yield


app = FastAPI(title="Chatbot with Google GenAI", lifespan=lifespan)
app.include_router(chat.router)
app.include_router(conversation.router)


@app.get("/")
async def root():
    return {"message": "Welcome to the Chatbot API powered by Google GenAI!"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="localhost", port=8000)