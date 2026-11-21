"""Chuẩn hóa văn bản tiếng Việt để so khớp không phụ thuộc dấu."""
import re
import unicodedata


def strip_accents(text: str) -> str:
    # "đ" không phải ký tự có dấu rời nên phải thay riêng
    text = unicodedata.normalize("NFC", text).replace("đ", "d").replace("Đ", "D")
    decomposed = unicodedata.normalize("NFD", text)
    return "".join(ch for ch in decomposed if unicodedata.category(ch) != "Mn")


def normalize(text: str) -> str:
    """Chữ thường, bỏ dấu, bỏ ký tự đặc biệt, gọn khoảng trắng.

    "Phí khám Nhi khoa?" -> "phi kham nhi khoa"
    """
    text = strip_accents(text).lower()
    text = re.sub(r"[^a-z0-9\s]", " ", text)
    return re.sub(r"\s+", " ", text).strip()


def contains_phrase(normalized_text: str, phrase: str) -> bool:
    """Tìm cụm từ theo ranh giới từ, tránh khớp nhầm như 'ho' trong 'hom nay'."""
    return f" {phrase} " in f" {normalized_text} "
