"""Kiểm thử lớp HTTP. Cần cài httpx (đã có trong requirements.txt)."""
from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_health():
    body = client.get("/health").json()
    assert body["status"] == "ok"
    assert body["chunks"] >= 20


def test_chat_returns_answer_and_sources():
    response = client.post("/chat", json={"message": "Phòng khám mở cửa lúc mấy giờ?"})
    assert response.status_code == 200
    body = response.json()
    assert body["mode"] == "retrieval"
    assert body["sources"][0]["title"] == "Giờ làm việc"


def test_chat_rejects_empty_and_too_long_messages():
    assert client.post("/chat", json={"message": ""}).status_code == 422
    assert client.post("/chat", json={"message": "a" * 501}).status_code == 422
    assert client.post("/chat", json={}).status_code == 422
