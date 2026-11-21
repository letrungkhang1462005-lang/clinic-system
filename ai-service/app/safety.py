"""Các quy tắc an toàn chạy TRƯỚC khi tìm tài liệu.

Chatbot của phòng khám không chẩn đoán, không kê đơn. Gặp tình huống khẩn cấp
thì chỉ dẫn gọi cấp cứu, không cố "trả lời thông minh".
"""
from .text_utils import contains_phrase, normalize

EMERGENCY_PHRASES = [
    "cap cuu", "kho tho", "khong tho duoc", "tho khong noi", "tho gap",
    "dau nguc du doi", "dau tim", "dot quy", "meo mieng", "liet nua nguoi",
    "ngat xiu", "bi ngat", "ngat di", "bat tinh", "co giat",
    "chay mau nhieu", "chay mau khong cam", "ngo doc", "uong nham", "nuot nham",
    "qua lieu", "non ra mau", "ho ra mau", "di ngoai ra mau", "tai nan giao thong",
]

SELF_HARM_PHRASES = [
    "muon chet", "khong muon song", "ket thuc cuoc song", "tu sat",
    "lam hai ban than", "tu lam dau ban than", "muon tu tu", "nghi den tu tu", "se tu tu",
]

MEDICAL_ADVICE_PHRASES = [
    "chan doan", "bi benh gi", "la benh gi", "co phai bi", "co phai toi bi",
    "uong thuoc gi", "dung thuoc gi", "thuoc nao tot", "lieu luong",
    "uong bao nhieu vien", "uong may vien", "co nguy hiem khong", "tu dieu tri",
    "ke don giup", "doc ket qua xet nghiem",
]

# Người dùng hỏi nên khám chuyên khoa nào
SPECIALTY_INTENT_PHRASES = ["kham khoa nao", "kham khoa gi", "nen kham", "nen den", "den khoa nao"]

# Xin đánh giá, xếp hạng bác sĩ: chatbot không có căn cứ và không nên so sánh
OUT_OF_SCOPE_PHRASES = ["gioi nhat", "tot nhat", "noi tieng nhat", "xuat sac nhat", "so sanh bac si"]

# Từ chỉ ý định tra cứu thông tin: nếu có, ưu tiên trả lời từ tài liệu thay vì gợi ý chuyên khoa
INFO_INTENT_PHRASES = [
    "gia", "phi", "tien", "gio", "dia chi", "dat lich", "huy lich", "thanh toan",
    "hoa don", "bao lau", "ngay nao", "thu may",
]

# Hỏi về giá: đoạn tài liệu trong bảng giá được ưu tiên
PRICE_INTENT_PHRASES = ["gia", "phi", "chi phi", "bao nhieu tien", "tien kham", "tien thuoc"]

GREETING_PHRASES = [
    "xin chao", "chao ban", "chao", "hello", "hi", "alo", "chao buoi sang", "chao buoi chieu",
]
THANKS_PHRASES = ["cam on", "cam on ban", "thanks", "thank you", "tam biet", "bye"]

# Triệu chứng -> chuyên khoa gợi ý. Thứ tự quan trọng: trẻ em ưu tiên Nhi khoa.
SYMPTOM_RULES: list[tuple[str, list[str]]] = [
    ("Nhi khoa", ["tre em", "con toi", "con minh", "be nha", "em be", "tre nho", "tre so sinh", "bé"]),
    ("Tai Mũi Họng", [
        "dau hong", "viem hong", "nghet mui", "so mui", "chay mui", "dau tai", "o tai",
        "viem xoang", "khan tieng", "amidan", "kho nghe",
    ]),
    ("Da liễu", ["noi man", "ngua", "mun", "viem da", "nam da", "rung toc", "di ung da"]),
    ("Răng Hàm Mặt", ["dau rang", "sau rang", "nho rang", "sung loi", "nieng rang", "chay mau chan rang"]),
    ("Nội tổng quát", [
        "sot", "dau dau", "dau bung", "met moi", "chong mat", "buon non", "tieu chay",
        "dau da day", "kho tieu", "ho khan", "bi ho", "ho nhieu", "ho keo dai",
    ]),
]

# Chuyên khoa chưa có bác sĩ nhận lịch (khớp với dữ liệu mẫu)
SPECIALTIES_WITHOUT_DOCTOR = {"Da liễu", "Răng Hàm Mặt"}

EMERGENCY_MESSAGE = (
    "Tình huống bạn mô tả có thể nguy hiểm. Hãy gọi cấp cứu 115 hoặc đến cơ sở y tế "
    "có cấp cứu gần nhất ngay lập tức, đừng chờ đặt lịch khám. "
    "Nếu có người ở cạnh, hãy nhờ họ ở bên và giúp bạn."
)

SELF_HARM_MESSAGE = (
    "Tôi rất tiếc khi bạn đang cảm thấy như vậy, và điều này rất quan trọng. "
    "Nếu bạn có nguy cơ làm hại bản thân, hãy gọi cấp cứu 115 hoặc đến cơ sở y tế "
    "gần nhất ngay, và ở cạnh một người thân hoặc bạn bè tin cậy. "
    "Bạn xứng đáng được hỗ trợ trực tiếp từ người thật."
)


def _any(normalized: str, phrases: list[str]) -> bool:
    return any(contains_phrase(normalized, p) for p in phrases)


def is_self_harm(raw_message: str) -> bool:
    # "tu tu" không dấu có thể là "từ từ", nên chỉ tin khi có dấu "tự tử"
    return "tự tử" in raw_message.lower() or _any(normalize(raw_message), SELF_HARM_PHRASES)


def is_emergency(normalized: str) -> bool:
    return _any(normalized, EMERGENCY_PHRASES)


def asks_medical_advice(normalized: str) -> bool:
    return _any(normalized, MEDICAL_ADVICE_PHRASES)


def asks_specialty(normalized: str) -> bool:
    return _any(normalized, SPECIALTY_INTENT_PHRASES)


def asks_ranking(normalized: str) -> bool:
    return _any(normalized, OUT_OF_SCOPE_PHRASES)


def has_info_intent(normalized: str) -> bool:
    return _any(normalized, INFO_INTENT_PHRASES)


def has_price_intent(normalized: str) -> bool:
    return _any(normalized, PRICE_INTENT_PHRASES)


def is_greeting(normalized: str) -> bool:
    return len(normalized.split()) <= 4 and _any(normalized, GREETING_PHRASES)


def is_thanks(normalized: str) -> bool:
    return len(normalized.split()) <= 4 and _any(normalized, THANKS_PHRASES)


def suggest_specialty(normalized: str) -> str | None:
    for specialty, phrases in SYMPTOM_RULES:
        if _any(normalized, [normalize(p) for p in phrases]):
            return specialty
    return None


def triage_text(specialty: str | None) -> str:
    """Câu gợi ý chuyên khoa. Luôn kèm cảnh báo đây không phải chẩn đoán."""
    if specialty is None:
        return (
            "Phòng khám có các chuyên khoa Nội tổng quát, Nhi khoa, Tai Mũi Họng, Da liễu và "
            "Răng Hàm Mặt. Nếu chưa chắc nên khám khoa nào, bạn có thể bắt đầu với Nội tổng quát "
            "hoặc hỏi lễ tân để được hướng dẫn."
        )
    note = (
        f" Hiện khoa {specialty} đang cập nhật bác sĩ, bạn vui lòng liên hệ lễ tân."
        if specialty in SPECIALTIES_WITHOUT_DOCTOR
        else f" Bạn có thể đặt lịch với bác sĩ khoa {specialty} trong mục Đặt lịch khám."
    )
    return (
        f"Với những gì bạn mô tả, khoa phù hợp để bắt đầu là {specialty}.{note} "
        "Đây chỉ là gợi ý chuyên khoa, không phải chẩn đoán. Bác sĩ sẽ khám và đánh giá trực tiếp. "
        "Nếu triệu chứng nặng lên đột ngột (khó thở, đau ngực, co giật...), hãy gọi cấp cứu 115."
    )
