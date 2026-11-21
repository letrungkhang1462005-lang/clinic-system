"""Cấu hình của dịch vụ AI. Có thể ghi đè bằng biến môi trường khi triển khai."""
import os
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent

# Thư mục chứa các tài liệu tri thức (.md) của phòng khám
KNOWLEDGE_DIR = Path(os.getenv("KNOWLEDGE_DIR", BASE_DIR / "knowledge"))

# Độ tương đồng tối thiểu để coi một đoạn tài liệu là "có liên quan".
# Dưới ngưỡng này chatbot trả lời "chưa có thông tin" thay vì đoán bừa.
MIN_SCORE = float(os.getenv("MIN_SCORE", "0.18"))

# Số đoạn tài liệu tối đa đưa vào câu trả lời
TOP_K = int(os.getenv("TOP_K", "2"))

# Chỉ lấy thêm đoạn thứ hai nếu điểm của nó đạt tỷ lệ này so với đoạn tốt nhất
SECOND_CHUNK_RATIO = float(os.getenv("SECOND_CHUNK_RATIO", "0.75"))

HOTLINE = os.getenv("CLINIC_HOTLINE", "1900 0000 (số mẫu)")
