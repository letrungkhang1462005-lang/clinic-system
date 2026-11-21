"""API của dịch vụ AI. Chạy:  uvicorn app.main:app --port 8000

Trong kiến trúc đầy đủ, chỉ Clinic.Api (ASP.NET) gọi dịch vụ này. Người dùng và
trình duyệt không gọi trực tiếp, nên việc kiểm tra đăng nhập và phân quyền nằm ở Clinic.Api.
"""
from fastapi import FastAPI
from pydantic import BaseModel, Field

from .chat import ChatService

app = FastAPI(title="Clinic AI Service", version="0.1.0")

# Lập chỉ mục một lần khi khởi động (vài chục đoạn tài liệu nên rất nhanh)
service = ChatService.from_directory()


class ChatRequest(BaseModel):
    message: str = Field(min_length=1, max_length=500)
    session_id: str | None = Field(default=None, max_length=64)  # dùng ở bước sau để nhớ hội thoại


class SourceOut(BaseModel):
    title: str
    file: str
    score: float


class ChatResponse(BaseModel):
    answer: str
    mode: str
    sources: list[SourceOut]


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "chunks": service.chunk_count}


@app.post("/chat", response_model=ChatResponse)
def chat(request: ChatRequest) -> ChatResponse:
    result = service.answer(request.message)
    return ChatResponse(
        answer=result.answer,
        mode=result.mode,
        sources=[SourceOut(title=s.title, file=s.file, score=s.score) for s in result.sources],
    )
