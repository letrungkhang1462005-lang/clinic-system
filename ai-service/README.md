# Clinic AI Service

Trợ lý ảo của phòng khám (chatbot RAG). Bản 6A: trả lời dựa trên tài liệu của phòng khám,
chưa dùng mô hình ngôn ngữ lớn (LLM). Có quy tắc an toàn: không chẩn đoán, không kê đơn,
chỉ dẫn gọi cấp cứu khi có dấu hiệu nguy hiểm.

## Chạy

    python -m venv .venv
    .venv\Scripts\activate          (Windows CMD)
    pip install -r requirements.txt
    uvicorn app.main:app --port 8000

Mở http://localhost:8000/docs để thử.

## Kiểm thử và đo độ chính xác

    pytest
    python eval/run_eval.py                                   (bộ phát triển, đã dùng để chỉnh)
    python eval/run_eval.py eval/questions_heldout.json       (bộ chưa dùng để chỉnh)

## Thêm tri thức

Thêm hoặc sửa file .md trong thư mục `knowledge/`. Mỗi mục `## Tiêu đề` là một đoạn, dòng
`Từ khóa: ...` ngay dưới tiêu đề liệt kê các cách người dùng hay hỏi. Khởi động lại dịch vụ để nạp.

Lưu ý: giờ làm việc, địa chỉ, hotline và giá trong tài liệu là DỮ LIỆU MẪU. Khi dữ liệu thật trong
hệ thống thay đổi (ví dụ Admin đổi phí khám), phải cập nhật tài liệu tương ứng.
