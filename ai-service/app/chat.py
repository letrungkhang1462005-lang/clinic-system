"""Điều phối câu hỏi: an toàn trước, rồi truy hồi tài liệu, rồi soạn câu trả lời."""
from dataclasses import dataclass, field
from pathlib import Path

from . import config, safety
from .knowledge import load_chunks
from .retriever import Hit, Retriever
from .text_utils import normalize

# Các giá trị của ChatAnswer.mode
MODE_RETRIEVAL = "retrieval"            # trả lời từ tài liệu
MODE_NO_ANSWER = "no_answer"            # không có thông tin liên quan
MODE_GREETING = "greeting"
MODE_THANKS = "thanks"
MODE_EMERGENCY = "emergency"
MODE_SELF_HARM = "self_harm"
MODE_MEDICAL_ADVICE = "medical_advice"  # từ chối chẩn đoán, kê đơn
MODE_TRIAGE = "triage"                  # gợi ý chuyên khoa

PRICE_SOURCE = "bang-gia.md"
PRICE_BOOST = 1.5
CANDIDATES = 6


@dataclass(frozen=True)
class Source:
    title: str
    file: str
    score: float


@dataclass(frozen=True)
class ChatAnswer:
    answer: str
    mode: str
    sources: list[Source] = field(default_factory=list)


GREETING_TEXT = (
    "Xin chào! Tôi là trợ lý ảo của phòng khám. Tôi có thể giúp bạn về giờ làm việc, bảng giá, "
    "quy trình khám, cách đặt và hủy lịch, thanh toán. Tôi không chẩn đoán bệnh hay kê đơn thuốc."
)
THANKS_TEXT = "Rất vui được hỗ trợ bạn. Chúc bạn nhiều sức khỏe!"
EMPTY_TEXT = 'Bạn hãy nhập câu hỏi, ví dụ: "Phòng khám mở cửa lúc mấy giờ?".'
RANKING_TEXT = (
    "Tôi không thể đánh giá hay xếp hạng bác sĩ. Bạn xem học vị, kinh nghiệm và phí khám của "
    "từng bác sĩ tại mục Bác sĩ trên website để chọn người phù hợp."
)


class ChatService:
    def __init__(self, retriever: Retriever, min_score: float = config.MIN_SCORE,
                 top_k: int = config.TOP_K, second_ratio: float = config.SECOND_CHUNK_RATIO) -> None:
        self._retriever = retriever
        self._min_score = min_score
        self._top_k = top_k
        self._second_ratio = second_ratio

    @classmethod
    def from_directory(cls, directory: Path = config.KNOWLEDGE_DIR, **kwargs) -> "ChatService":
        return cls(Retriever(load_chunks(directory)), **kwargs)

    @property
    def chunk_count(self) -> int:
        return len(self._retriever.chunks)

    def answer(self, message: str) -> ChatAnswer:
        normalized = normalize(message)
        if not normalized:
            return ChatAnswer(EMPTY_TEXT, MODE_NO_ANSWER)

        # 1) An toàn: luôn kiểm tra trước, kể cả khi tin nhắn có vẻ hỏi thông tin
        if safety.is_self_harm(message):
            return ChatAnswer(safety.SELF_HARM_MESSAGE, MODE_SELF_HARM)
        if safety.is_emergency(normalized):
            return ChatAnswer(safety.EMERGENCY_MESSAGE, MODE_EMERGENCY)

        # 2) Chào hỏi
        if safety.is_greeting(normalized):
            return ChatAnswer(GREETING_TEXT, MODE_GREETING)
        if safety.is_thanks(normalized):
            return ChatAnswer(THANKS_TEXT, MODE_THANKS)

        specialty = safety.suggest_specialty(normalized)

        # 3) Xin chẩn đoán, kê đơn: từ chối, rồi hướng sang đặt lịch
        if safety.asks_medical_advice(normalized):
            text = (
                "Tôi không thể chẩn đoán bệnh hay tư vấn thuốc, vì việc này cần bác sĩ khám trực tiếp. "
                + safety.triage_text(specialty)
            )
            return ChatAnswer(text, MODE_MEDICAL_ADVICE)

        # 4) Hỏi nên khám khoa nào, hoặc mô tả triệu chứng mà không hỏi thông tin cụ thể
        if safety.asks_specialty(normalized) or (specialty and not safety.has_info_intent(normalized)):
            return ChatAnswer(safety.triage_text(specialty), MODE_TRIAGE)

        if safety.asks_ranking(normalized):
            return ChatAnswer(RANKING_TEXT, MODE_NO_ANSWER)

        # 5) Truy hồi tài liệu
        hits = self._rank(message, normalized)
        # Ngưỡng xét trên điểm GỐC (chưa ưu tiên), để câu ngoài chủ đề như "giá bitcoin"
        # không vượt ngưỡng chỉ nhờ được cộng điểm ưu tiên.
        if not hits or max(h.score for h in hits) < self._min_score:
            return ChatAnswer(self._no_answer_text(), MODE_NO_ANSWER)

        used = self._select(hits)
        text = "\n\n".join(f"{h.chunk.title}\n{h.chunk.body}" for h in used)
        sources = [Source(h.chunk.title, h.chunk.source, round(h.score, 3)) for h in used]
        return ChatAnswer(text, MODE_RETRIEVAL, sources)

    def _rank(self, message: str, normalized: str) -> list[Hit]:
        hits = self._retriever.search(message, CANDIDATES)
        if safety.has_price_intent(normalized):
            # Hỏi giá thì đoạn trong bảng giá phải đứng trước đoạn mô tả chuyên khoa.
            # Chỉ đổi thứ tự, không đổi điểm gốc của từng đoạn.
            hits = sorted(
                hits,
                key=lambda h: h.score * (PRICE_BOOST if h.chunk.source == PRICE_SOURCE else 1.0),
                reverse=True,
            )
        return hits

    def _select(self, hits: list[Hit]) -> list[Hit]:
        # Đoạn đứng đầu luôn được dùng. Đoạn thứ hai chỉ thêm vào khi cũng đủ liên quan.
        best = max(h.score for h in hits)
        used = [hits[0]]
        for h in hits[1:self._top_k]:
            if h.score >= self._min_score and h.score >= best * self._second_ratio:
                used.append(h)
        return used

    @staticmethod
    def _no_answer_text() -> str:
        return (
            "Xin lỗi, tôi chưa có thông tin về câu hỏi này. Bạn vui lòng liên hệ lễ tân qua hotline "
            f"{config.HOTLINE} hoặc đến quầy lễ tân để được hỗ trợ."
        )
