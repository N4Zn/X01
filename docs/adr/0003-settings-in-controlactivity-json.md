# ADR 0003 — Cài đặt game nằm ở ControlActivity, truyền sang Unity bằng JSON
- **Trạng thái**: chấp nhận (2026-10-04)
- **Bối cảnh**: panel setting cũ trên Unity chiếu ra sàn, giáo viên không chỉnh được từ tablet; thời gian mỗi câu không cần nữa.
- **Quyết định**: tab Cài đặt trong ControlActivity, lưu `/sdcard/EduXplore/game_settings.json`; gửi bằng `OnSettingsChanged`/Intent extra; field vắng hoặc âm = không đổi. Thời gian mỗi câu bỏ (không giới hạn).
- **Hệ quả**: wait-clear = OR(cờ scene, `GameSettings.WaitForClear`), mặc định BẬT cho mọi game kể cả Independent. Còn mở: `QuestionTimeout` deprecated chưa gỡ (TODO B7/B8).
