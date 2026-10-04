# Hàng đợi ý tưởng mini-game — cho loop tự động qua `/newminigame`

Mỗi mục là 1 ý tưởng, viết ĐỦ CHI TIẾT — loop chạy tự động, KHÔNG có người trả lời câu hỏi giữa
chừng, nên càng rõ càng ít phải tự đoán. Không bắt buộc trả lời hết, nhưng nên có nếu biết trước:
cơ chế chính, nội dung/chủ đề, 1 đội hay 2 đội (độc lập/gộp), có cần âm thanh/đếm ngược/cách tính
điểm gì đặc biệt không (xem 9 trục ở Bước 2 của `.claude/skills/newminigame/SKILL.md` để tham khảo,
KHÔNG bắt buộc điền đủ — thiếu thì loop tự suy luận hợp lý và ghi chú lại giả định đã chọn).

**Trạng thái mỗi mục** (sửa trực tiếp ký hiệu đầu dòng):
- `[ ]` — chưa làm
- `[~]` — đang làm dở (loop bị ngắt giữa chừng, tick sau tiếp tục từ đây)
- `[x]` — loop đã triển khai xong, ĐANG CHỜ REVIEW
- `[r]` — đã review xong, ok (hoặc đã sửa tay sau review)

Sau khi review 1 game `[x]`, nếu phát hiện lỗi/pattern cần tránh cho các game SAU trong queue —
cập nhật vào `Assets/Game/Scripts/Core/MiniGameKit/README.md` (hoặc SKILL.md nếu là quy trình) chứ
không chỉ sửa riêng game đó, để loop tick tiếp theo tự động tránh lặp lại.

---

<!-- Thêm ý tưởng mới bên dưới, theo mẫu:

## [ ] <Tên game>
Mô tả: ...

-->
