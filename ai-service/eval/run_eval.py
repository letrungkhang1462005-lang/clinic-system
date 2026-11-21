"""Chấm chatbot trên bộ câu hỏi mẫu và in tỷ lệ đúng.

Chạy từ thư mục ai-service:   python eval/run_eval.py
Dùng kết quả này làm số liệu trong CV và báo cáo (ví dụ: "đúng 57/59 câu").
Với câu hỏi tra cứu, đoạn tài liệu ĐỨNG ĐẦU phải đúng mới tính là đúng.
"""
import json
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.chat import ChatService  # noqa: E402


def main(path: str = "eval/questions.json") -> int:
    items = json.loads(Path(path).read_text(encoding="utf-8"))
    service = ChatService.from_directory()

    per_mode: dict[str, list[bool]] = defaultdict(list)
    failures = []

    for item in items:
        result = service.answer(item["question"])
        ok = result.mode == item["mode"]
        if ok and item["mode"] == "retrieval":
            ok = bool(result.sources) and result.sources[0].title == item["title"]
        per_mode[item["mode"]].append(ok)
        if not ok:
            top = result.sources[0].title if result.sources else "-"
            failures.append((item["question"], item["mode"], result.mode, item.get("title", ""), top))

    total = sum(len(v) for v in per_mode.values())
    passed = sum(sum(v) for v in per_mode.values())

    print(f"Tổng: {passed}/{total} câu đúng ({passed / total:.0%})\n")
    for mode, results in per_mode.items():
        print(f"  {mode:15s} {sum(results)}/{len(results)}")

    if failures:
        print("\nCác câu sai:")
        for q, expected, got, title, top in failures:
            print(f"  - {q}\n      mong đợi: {expected} {title!r} | nhận được: {got} {top!r}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
