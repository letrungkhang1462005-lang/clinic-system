"""Đọc tài liệu Markdown và cắt thành các đoạn (chunk) theo tiêu đề.

Mỗi mục "## Tiêu đề" là một đoạn. Dòng "Từ khóa: ..." ngay dưới tiêu đề là
các cách hỏi thường gặp, giúp tìm đúng đoạn dù người dùng dùng từ khác với tài
liệu. Dòng này chỉ dùng để tìm kiếm, không hiện trong câu trả lời.
"""
from dataclasses import dataclass
from pathlib import Path

KEYWORD_PREFIX = "Từ khóa:"


@dataclass(frozen=True)
class Chunk:
    source: str    # tên file, ví dụ "bang-gia.md"
    title: str     # tiêu đề mục, ví dụ "Phí khám theo bác sĩ"
    body: str      # nội dung hiển thị cho người dùng
    keywords: str = ""

    @property
    def search_text(self) -> str:
        # Lặp tiêu đề để từ khóa trong tiêu đề có trọng số cao hơn
        return f"{self.title}. {self.title}. {self.keywords}. {self.body}"


def split_markdown(source: str, text: str) -> list[Chunk]:
    chunks: list[Chunk] = []
    title: str | None = None
    lines: list[str] = []
    keywords = ""

    def flush() -> None:
        if title and any(line.strip() for line in lines):
            chunks.append(Chunk(source, title, "\n".join(lines).strip(), keywords))

    for raw in text.splitlines():
        if raw.startswith("## "):
            flush()
            title, lines, keywords = raw[3:].strip(), [], ""
        elif raw.startswith("# "):
            continue
        elif title is not None:
            if raw.startswith(KEYWORD_PREFIX):
                keywords = raw[len(KEYWORD_PREFIX):].strip()
            else:
                lines.append(raw)
    flush()
    return chunks


def load_chunks(directory: Path) -> list[Chunk]:
    chunks: list[Chunk] = []
    for path in sorted(directory.glob("*.md")):
        chunks.extend(split_markdown(path.name, path.read_text(encoding="utf-8")))
    return chunks
