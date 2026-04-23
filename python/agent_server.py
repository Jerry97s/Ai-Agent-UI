"""
HTTP API for the AI Agent.

- Demo: OPENAI_API_KEY 미설정 시 에코(프리픽스).
- LLM: OPENAI_API_KEY 설정 시 OpenAI 호환 Chat Completions (Ollama/Azure 등은 OPENAI_BASE_URL로).

실행: python agent_server.py
"""
from __future__ import annotations

import logging
import os
from typing import Any

from fastapi import FastAPI
from openai import AsyncOpenAI
from pydantic import AliasChoices, BaseModel, Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict
from uvicorn import Config, Server

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class Settings(BaseSettings):
    """환경 변수 및 선택적 `.env`(python 디렉터리)."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # 표준 OPENAI_* / 기존 AGENT_* 이름 모두 허용
    openai_api_key: str | None = Field(
        None,
        validation_alias=AliasChoices("OPENAI_API_KEY", "openai_api_key"),
    )
    openai_base_url: str | None = Field(
        None,
        validation_alias=AliasChoices("OPENAI_BASE_URL", "openai_base_url"),
    )
    chat_model: str = Field(
        default="gpt-4o-mini",
        validation_alias=AliasChoices("AGENT_CHAT_MODEL", "OPENAI_MODEL", "chat_model"),
    )
    reply_prefix_demo: str = Field(
        default="[Python Agent] ",
        validation_alias=AliasChoices("AGENT_REPLY_PREFIX", "reply_prefix_demo"),
    )

    @field_validator("openai_api_key", mode="before")
    @classmethod
    def empty_key_is_none(cls, v: Any) -> Any:
        if isinstance(v, str) and not v.strip():
            return None
        return v


settings = Settings()

app = FastAPI(title="AI Agent API", version="1.1.0")


class ChatRequest(BaseModel):
    message: str = Field(..., min_length=1)


class ChatResponse(BaseModel):
    reply: str


async def _run_agent_async(message: str) -> str:
    if settings.openai_api_key:
        client = AsyncOpenAI(
            api_key=settings.openai_api_key,
            base_url=settings.openai_base_url or None,
            timeout=120.0,
            max_retries=2,
        )
        completion = await client.chat.completions.create(
            model=settings.chat_model,
            messages=[{"role": "user", "content": message}],
            temperature=0.7,
        )
        chunk = completion.choices[0].message.content
        text = (chunk or "").strip()
        return text if text else "(모델이 빈 응답을 반환했습니다.)"

    return f"{settings.reply_prefix_demo}{message}"


@app.get("/health")
async def health() -> dict[str, Any]:
    return {
        "status": "ok",
        "version": app.version,
        "mode": "llm" if settings.openai_api_key else "demo",
        "model": settings.chat_model if settings.openai_api_key else None,
    }


@app.post("/chat", response_model=ChatResponse)
async def chat(req: ChatRequest) -> ChatResponse:
    text = req.message.strip()
    if not text:
        return ChatResponse(reply="(빈 메시지는 처리할 수 없습니다.)")
    try:
        reply = await _run_agent_async(text)
        return ChatResponse(reply=reply)
    except Exception as ex:
        logger.exception("chat handler failed")
        # 클라이언트는 200 본문으로 에러 메시지를 표시하기 쉬움
        return ChatResponse(reply=f"(에이전트 오류) {ex}")


def main() -> None:
    host = os.environ.get("AGENT_HOST", "127.0.0.1")
    port = int(os.environ.get("AGENT_PORT", "8787"))
    logger.info(
        "Starting agent host=%s port=%s mode=%s",
        host,
        port,
        "llm" if settings.openai_api_key else "demo",
    )
    server = Server(
        Config(app, host=host, port=port, log_level="info"),
    )
    server.run()


if __name__ == "__main__":
    main()
