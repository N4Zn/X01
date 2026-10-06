# Report realtime và đồng bộ Google Sheets (Track E)

> **TL;DR**: GameControlBridge.PushReport đẩy điểm lên ControlActivity; SheetsSyncManager gửi log lên Apps Script. Tên game khi log PHẢI qua GameSessionManager.ResolveActiveGameName; Independent play dùng LogIndependentRound.
> **Đọc khi**: sửa log/report/Sheets, thêm controller mới cần ghi log.
> **Cập nhật**: 2026-10-04 (tách nguyên văn từ CLAUDE.md gốc, chưa sửa nội dung)

### Report + Google Sheets sync (Track E)

- `GameControlBridge.PushReport(secondsLeft, leftName, leftScore, rightName, rightScore)` —
  đẩy report throttle 1 lần/giây từ `MiniGameControllerBase.Update()`/
  `TestTongHopController.Update()` sang `ControlActivity.UpdateReport()` (hiện tên + điểm TỪNG
  BÊN riêng, không gộp tổng).
- `SheetsSyncManager.cs` — gom log (`GameLogger`, `MiniGameControllerBase.LogRoundResult`,
  `PlayerRecognitionService`) gửi lên Google Sheet qua Apps Script Web App (không cần OAuth phía
  app), batch mỗi ~3s. Config: `/sdcard/EduXplore/sheets_sync_config.json` (SharedStorage)
  (`webAppUrl`, `enabled`, `intervalSeconds`).
  Hàng đợi được lưu ở `/sdcard/EduXplore/sheets_pending.jsonl` (ghi nối ở `Enqueue`, ghi lại phần
  còn lại sau mỗi POST thành công) → mất mạng + tắt máy, lần mở sau tự gửi bù. Mọi dòng `round_end`
  có `questionId` (cột mới trên Sheet — Apps Script cần map thêm cột này nếu map theo tên cố định).
  > **Gotcha round**: số round log PHẢI lấy từ bộ đếm đúng chế độ — TestTongHop chế độ chung dùng
  > `gameModel.RoundsPlayed`, Independent dùng `_xxxRoundsCompleted+1` (xem `LogRoundNumber`);
  > MiniGameKit Independent luôn tăng `_left/_rightRoundIndex` kể cả `totalRounds<=0`.
  > **Gotcha Apps Script**: sửa code trong editor **KHÔNG** tự cập nhật URL `/exec` đang chạy —
  > bắt buộc **Deploy → Manage deployments → Edit (bút chì) → New version → Deploy** thì code mới
  > mới thật sự chạy. Response log giờ có in body (`{"ok":true,"count":N}`) để kiểm tra ngay thay
  > vì phải mở Sheet thủ công.
- **Gotcha ghi tên game vào log**: PHẢI dùng `GameSessionManager.ResolveActiveGameName(fallback)`
  (không hardcode literal tên scene) ở MỌI nơi log game — nhiều game share chung 1
  scene/controller, hardcode sẽ ghi sai tên khi có > 1 variant (đã fix 14 chỗ hardcode kiểu
  `BeginGameSession("XxxGame")` rải rác ở các controller cũ không kế thừa
  `MiniGameControllerBase`, vd `AddNumberGameController`, `AddUpGameController`, ...).
- **Independent play mode** (`playMode`/`independentPlay = true`, vd Counting5): KHÔNG dùng được
  `GameLogger.BeginRound()`/`EndRound()` gốc (dựa vào 1 `_currentRound` dùng chung — 2 bên tự
  nhịp câu hỏi riêng nên có thể chồng thời gian nhau, bên này `BeginRound()` sẽ đè mất câu đang
  dở của bên kia). Dùng `GameLogger.LogIndependentRound()` /
  `GameManager.RecordIndependentAnswer()` thay thế — ghi trực tiếp, không qua `_currentRound`.
