from app import config
from app.knowledge import load_chunks, split_markdown
from app.text_utils import contains_phrase, normalize


def test_normalize_removes_accents_and_punctuation():
    assert normalize("Phí khám Nhi khoa?") == "phi kham nhi khoa"
    assert normalize("  Đau   đầu!! ") == "dau dau"


def test_contains_phrase_uses_word_boundaries():
    assert contains_phrase("toi bi sot", "sot")
    assert not contains_phrase("hom nay", "ho")  # "ho" không được khớp bên trong "hom"


def test_split_markdown_separates_keywords_from_body():
    text = "# Tiêu đề file\n\n## Mục A\nTừ khóa: a, b.\nNội dung A.\n\n## Mục B\nNội dung B.\n"
    chunks = split_markdown("x.md", text)
    assert [c.title for c in chunks] == ["Mục A", "Mục B"]
    assert chunks[0].keywords == "a, b."
    assert "Từ khóa" not in chunks[0].body  # từ khóa chỉ để tìm kiếm, không hiện cho người dùng
    assert chunks[1].keywords == ""


def test_real_knowledge_base_loads():
    chunks = load_chunks(config.KNOWLEDGE_DIR)
    assert len(chunks) >= 20
    assert all(c.title and c.body for c in chunks)
