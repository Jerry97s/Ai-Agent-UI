# 에이전트 HTTP API 요약

기본 베이스 URL: `http://127.0.0.1:8787/` (`AGENT_HOST` / `AGENT_PORT`).  
클라이언트(WPF)는 실행 시 환경 변수 **`AGENT_BASE_URL`** / **`AI_AGENT_URL`** 로 베이스 URL을 바꿀 수 있습니다.

## LLM 모드

`python/.env` 또는 환경 변수로 **`OPENAI_API_KEY`** 를 설정하면 OpenAI 호환 Chat Completions를 사용합니다.

- **`OPENAI_BASE_URL`**: Azure OpenAI, 로컬 **Ollama**(`http://localhost:11434/v1`) 등 OpenAI 호환 엔드포인트.
- **`AGENT_CHAT_MODEL`**: 모델 ID (예: `gpt-4o-mini`, Ollama 모델명).

키가 없으면 **데모(에코)** 모드입니다. 자세한 표는 [`python/.env.example`](../python/.env.example).

## `GET /health`

**응답 예** (`200 OK`)

```json
{
  "status": "ok",
  "version": "1.1.0",
  "mode": "demo",
  "model": null
}
```

`mode`는 `"demo"`(에코) 또는 `"llm"` 입니다. `model`은 LLM 모드일 때만 문자열로 채워집니다.

클라이언트 기본 구현은 성공 상태 코드만 검사합니다.

## `POST /chat`

**요청** (`application/json`)

```json
{
  "message": "사용자 메시지"
}
```

**응답** (`application/json`)

```json
{
  "reply": "에이전트 응답 텍스트"
}
```

## 예시 (curl)

```bash
curl -s -X POST http://127.0.0.1:8787/chat \
  -H "Content-Type: application/json" \
  -d "{\"message\":\"hello\"}"
```

구현 코드: `python/agent_server.py` (FastAPI).
