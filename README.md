# Chatbot Chaytau

A Windows desktop chatbot built for analyzing optimal operating scenarios and cross-referencing legal/regulatory documents. Runs as a single, self-contained desktop app — no separate installation of Python, dependencies, or a server required.

## Overview

Chatbot Chaytau pairs a native desktop UI with an AI backend so a non-technical user can just double-click and chat:

- **Desktop client** (PyQt6) — a chat window with conversation history, streaming responses, and file attachments.
- **Local backend** (FastAPI) — started automatically by the client on launch; talks to the Gemini API and persists conversations.
- **Packaged as a single executable** (`ChatbotDesktop.exe`) via PyInstaller, so the end user runs one file with no setup.

## Features

- 💬 **Streaming chat** over Server-Sent Events, so responses appear token-by-token instead of all at once.
- 📎 **File-aware conversations** — attach PDF, DOCX, XLSX, or images; DOCX/XLSX are parsed and injected as text context, other files are uploaded directly to Gemini.
- 🗂️ **Conversation history** — list, rename, and delete past conversations, backed by a local SQLModel database.
- 🔍 **Search grounding** — optional Google Search grounding so answers can reference up-to-date information, not just the model's training data.
- ⚡ **Response caching** — identical requests (same input, model, instructions, and attached files) are served from a local cache instead of re-calling the API, and cached answers act as an offline fallback if a request to Gemini fails.
- 🚀 **Zero-install desktop app** — the client auto-launches the backend on a local port, so the user experience is just "open the app."

## Architecture

```
app/
├── client/                # PyQt6 desktop application
│   ├── app.py              # App entry: launches backend, opens main window
│   ├── backend_launcher.py # Starts/stops the local FastAPI backend
│   ├── ui/main_window.py   # Chat window, conversation list, attachments
│   ├── api/client.py       # HTTP client used by the UI to call the backend
│   ├── workers/            # Background thread for streaming responses
│   └── state/store.py      # In-app chat state
│
└── server/                # FastAPI backend (runs locally, launched by the client)
    ├── main.py              # FastAPI app + router registration
    ├── routers/             # /chat and /conversation endpoints
    ├── services/            # Chat generation logic, Gemini calls, caching
    ├── models/              # SQLModel tables: Conversation, Message, Cache
    ├── dependencies/        # Gemini client + database session setup
    └── utils/               # File hashing, DOCX/XLSX text extraction, caching
```

The client and server communicate over HTTP on `localhost`; the server is started as a background process by the client on startup and shut down when the app closes.

## Tech Stack

| Layer | Technology |
|---|---|
| Desktop UI | PyQt6 |
| Backend API | FastAPI, Uvicorn |
| AI model | Google Gemini (`google-genai`) |
| Data | SQLModel (SQLite) |
| Packaging | PyInstaller |
| Language | Python 3.11 |

## Running from source

```bash
cd app
pip install -r requirements.txt
```

Create a `.env` file in the project root with your Gemini API key:

```
GEMINI_API_KEY=your_api_key_here
```

Then launch the desktop app (this starts the backend automatically):

```bash
python -m client.main
```

## Building the standalone executable

On Windows, with a Python virtual environment set up under `app/.venv`:

```powershell
./build_desktop.ps1
```

This bundles the client and its backend into a single `ChatbotDesktop.exe` using PyInstaller — no Python installation needed on the machine that runs it.

## Notes

- This project was built for a specific end user (a domain expert without a technical background), so the priority throughout was a zero-friction experience: one executable, no terminal, no setup steps.
- The response cache also doubles as a lightweight offline fallback — if a live Gemini call fails, a previously cached answer to the same question is served instead of a hard error.
