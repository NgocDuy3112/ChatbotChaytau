from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from contextlib import asynccontextmanager

from .api import chat
from .dependencies.gemini_client import get_gemini_client


@asynccontextmanager
async def lifespan(app: FastAPI):
    gemini_client = get_gemini_client()
    app.state.gemini_client = gemini_client
    yield


app = FastAPI(title="Chatbot with Google GenAI", lifespan=lifespan)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
app.include_router(chat.router)


@app.get("/")
async def root():
    return {"message": "Welcome to the Chatbot API powered by Google GenAI!"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)