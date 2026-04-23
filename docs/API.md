# 에이전트 HTTP API 요약

기본 베이스 URL: `http://127.0.0.1:8787/` (환경 변수로 변경 가능)

## `GET /health`

**응답**

- `200 OK`, 본문 예: `{"status":"ok"}`

클라이언트는 성공 상태 코드만 확인합니다.

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
