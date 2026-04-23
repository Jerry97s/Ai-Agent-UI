"""FastAPI 에이전트 계약 테스트 (데모 모드 기준; CI에서는 OPENAI_API_KEY 미설정 권장)."""

from fastapi.testclient import TestClient

from agent_server import app


client = TestClient(app)


def test_health_contract():
    r = client.get("/health")
    assert r.status_code == 200
    data = r.json()
    assert data["status"] == "ok"
    assert data.get("version")
    assert data.get("mode") in ("demo", "llm")


def test_chat_returns_reply_shape():
    r = client.post("/chat", json={"message": "hello"})
    assert r.status_code == 200
    body = r.json()
    assert "reply" in body
    assert isinstance(body["reply"], str)
    assert len(body["reply"]) > 0


def test_chat_validation_empty_not_allowed():
    # min_length=1 이므로 빈 문자열은 422
    r = client.post("/chat", json={"message": ""})
    assert r.status_code == 422
