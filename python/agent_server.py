"""
HTTP API for the AI Agent. Run: uvicorn agent_server:app --host 127.0.0.1 --port 8000
Or: python agent_server.py
"""
from __future__ import annotations

import os
from typing import Optional

from fastapi import FastAPI
from pydantic import BaseModel, Field
from uvicorn import Config, Server

app = FastAPI(title="AI Agent API", version="1.0.0")


class ChatRequest(BaseModel):
    message: str = Field(..., min_length=1)


class ChatResponse(BaseModel):
    reply: str


def _run_agent(message: str) -> str:
    """
    Plug in your model / LangChain / LlamaIndex / etc. here.
    This sample echoes the prompt with a short prefix.
    """
    prefix = os.environ.get("AGENT_REPLY_PREFIX", "[Python Agent] ")
    return f"{prefix}{message}"


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/chat", response_model=ChatResponse)
async def chat(req: ChatRequest) -> ChatResponse:
    text = req.message.strip()
    if not text:
        return ChatResponse(reply="(빈 메시지는 처리할 수 없습니다.)")
    reply = _run_agent(text)
    return ChatResponse(reply=reply)


def main() -> None:
    host = os.environ.get("AGENT_HOST", "127.0.0.1")
    port = int(os.environ.get("AGENT_PORT", "8787"))
    server = Server(
        Config(app, host=host, port=port, log_level="info"),
    )
    server.run()


if __name__ == "__main__":
    main()
