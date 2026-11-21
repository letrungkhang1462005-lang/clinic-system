"""Tìm đoạn tài liệu liên quan nhất bằng TF-IDF trên từ và cặp từ.

Có hai chỉ mục:
- Có dấu: dùng khi câu hỏi có dấu. Phân biệt được "đau" với "đâu", "thông tin" với "tổng thống".
- Không dấu: dùng khi người dùng gõ không dấu ("gio lam viec"). Chấp nhận nhập nhằng để vẫn tìm được.
"""
import re
import unicodedata
from dataclasses import dataclass

from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import linear_kernel

from .knowledge import Chunk
from .text_utils import normalize, strip_accents

# Từ hư thường gặp (viết không dấu để so khớp cả hai chỉ mục). Bỏ đi để điểm không bị
# kéo bởi các từ không mang nghĩa. Không đưa "nhi" vào đây vì "nhi khoa" là một chuyên khoa.
STOP_WORDS = frozenset(
    """la va cua co khong the nao bao nhieu ma toi ban minh em a oi vay nhe nha cho
    duoc o tai trong khi de den tu voi mot nhung cac nay do day kia thi se da dang
    con hay gi nhu may xin sao roi lai nen can phai muon biet hoi luc""".split()
)


def _nfc(text: str) -> str:
    return unicodedata.normalize("NFC", text).lower()


def has_diacritics(text: str) -> bool:
    return _nfc(text) != strip_accents(_nfc(text))


def tokenize_plain(text: str) -> list[str]:
    return [w for w in normalize(text).split() if w not in STOP_WORDS]


def tokenize_accented(text: str) -> list[str]:
    words = re.findall(r"[^\W_]+", _nfc(text))
    return [w for w in words if strip_accents(w) not in STOP_WORDS]


@dataclass(frozen=True)
class Hit:
    chunk: Chunk
    score: float


class _Index:
    def __init__(self, texts: list[str], tokenizer) -> None:
        self._tokenizer = tokenizer
        self._vectorizer = TfidfVectorizer(
            ngram_range=(1, 2), sublinear_tf=True, token_pattern=r"\S+", lowercase=False
        )
        self._matrix = self._vectorizer.fit_transform([" ".join(tokenizer(t)) for t in texts])

    def scores(self, query: str):
        tokens = self._tokenizer(query)
        if not tokens:
            return None
        return linear_kernel(self._vectorizer.transform([" ".join(tokens)]), self._matrix).ravel()


class Retriever:
    def __init__(self, chunks: list[Chunk]) -> None:
        if not chunks:
            raise ValueError("Không có tài liệu tri thức nào để lập chỉ mục.")
        self.chunks = chunks
        texts = [c.search_text for c in chunks]
        self._accented = _Index(texts, tokenize_accented)
        self._plain = _Index(texts, tokenize_plain)

    def search(self, query: str, top_k: int = 3) -> list[Hit]:
        index = self._accented if has_diacritics(query) else self._plain
        scores = index.scores(query)
        if scores is None:
            return []
        order = scores.argsort()[::-1][:top_k]
        return [Hit(self.chunks[i], float(scores[i])) for i in order]
