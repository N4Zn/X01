# 0008 — Cơ chế chung của minigame nằm MỘT chỗ ở `MiniGameControllerBase`

**Bối cảnh**: cùng một cơ chế (đếm "Next in", chờ vùng sạch, dọn câu sau feedback, đồng bộ 2 đội, âm câu hỏi lặp 4s) bị cài lặp ở GenericGame,
FloorStory (hand-coded world + FlowWorld) và Kit; mỗi bản một kiểu nên game mới (tạo bằng cách nào cũng vậy) lặp lại lỗi cũ
(lệch round 2 đội, câu cũ không biến mất, không lặp audio).

**Quyết định**: mọi cơ chế chung viết một lần ở base; game chỉ nối hook (`CleanupIndependentDisplay`, `SyncIndependentRounds`,
`OnWaitingForPartner`, `PlayQuestionAudioOnce`). Thứ tự cuối lượt Combined = Independent. Bảng + checklist ở
`Core/MiniGameKit/README.md` mục "Cơ chế chung" là NƠI DUY NHẤT ghi; skill `newminigame` bước 0 áp dụng cho mọi cách tạo game, không có
"loại game" nào được miễn. Không ghi chú cơ chế riêng trong doc từng loại game.

**Hệ quả**: sửa cơ chế = sửa base + cập nhật bảng. GenericGame đã chuyển sang dùng vòng lặp âm của base (bỏ bản riêng). Chưa chạy Editor.
