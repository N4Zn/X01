# ADR 0006 — Tách tài liệu theo tầng để tiết kiệm token
- **Trạng thái**: chấp nhận (2026-10-04)
- **Quyết định**: `CLAUDE.md` gốc ≤ ~8KB (kiến trúc + bản đồ "đọc gì khi làm gì" + changelog mốc lớn); `docs/*.md` kiến thức chi tiết (đầu file có TL;DR, đọc khi, ngày cập nhật); CLAUDE.md lồng theo thư mục chỉ là con trỏ + gotcha; `TODO.md` việc dở; `notes/YYYY-MM.md` nhật ký theo ngày (không tự đọc); `docs/adr/` quyết định; `docs/contracts/` giao thức giữa các phần. `/logwork` cập nhật cuối ngày.
- **Hệ quả**: đổi kiến trúc lớn → thêm ADR + 1 dòng changelog ở CLAUDE.md gốc.
