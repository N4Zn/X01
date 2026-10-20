# Report realtime và đồng bộ Google Sheets (Track E)

> **TL;DR**: GameControlBridge.PushReport đẩy điểm lên ControlActivity; SheetsSyncManager gửi MỖI PHIÊN 1 FILE log lên Drive qua Apps Script (từ 2026-10-20; cơ chế dòng-vào-Sheet đã bỏ). Tên game khi log PHẢI qua GameSessionManager.ResolveActiveGameName; Independent play dùng LogIndependentRound.
> **Đọc khi**: sửa log/report/Sheets, thêm controller mới cần ghi log.
> **Cập nhật**: 2026-10-20 (mục cuối: đồng bộ theo file lên Drive; trước đó 2026-10-09: thêm mục "Khớp log online – local"; phần còn lại tách nguyên văn từ CLAUDE.md gốc 2026-10-04)

### Report + Google Sheets sync (Track E)

- `GameControlBridge.PushReport(secondsLeft, leftName, leftScore, rightName, rightScore)` —
  đẩy report throttle 1 lần/giây từ `MiniGameControllerBase.Update()`/
  `TestTongHopController.Update()` sang `ControlActivity.UpdateReport()` (hiện tên + điểm TỪNG
  BÊN riêng, không gộp tổng).
- `SheetsSyncManager.cs` — **từ 2026-10-20: mỗi phiên chơi 1 file lên Google Drive** (xem mục "Đồng bộ theo file" cuối trang). Phần dưới về hàng đợi từng dòng/Sheet là CƠ CHẾ CŨ, chỉ để tra dữ liệu lịch sử.
  Config: `/sdcard/EduXplore/sheets_sync_config.json` (`webAppUrl`, `enabled`, `intervalSeconds`, `deviceId`, `secret`).
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

### Khớp log online – local (2026-10-09)

Mục tiêu: dòng `round_end` trên Sheet = đúng dòng `round` trong file log local (`PlayerRecognitionService`), dùng cho báo cáo năng lực (`docs/danh-gia-nang-luc/`).

- **Mọi dòng** có thêm `deviceId` (config `deviceId` trong `sheets_sync_config.json`, rỗng = 8 ký tự mã Android), `sessionId` (= tên file log local, đặt ở `BeginGameSession`), `rid` (mã duy nhất mỗi dòng; dòng gửi lại giữ nguyên `rid`) — gắn tự động trong `SheetsSyncManager.Enqueue`.
- **Nguồn `round_end` duy nhất cho game độc lập = `PlayerRecognitionService.LogRound`** (tên đã nhận diện, cùng tên ghi vào file local). Game cũ chỉ gọi `LogRound` (AddNumber, SubNumber, ChuCai, ListenSelect, Numbers, SoDem, TongHopGame, PlanetAlphabet/Order, LaneDash, SaveTheAstronaut...) giờ cũng lên Sheet — trước đây thiếu hoàn toàn. Nơi tự đẩy dòng riêng (`MiniGameControllerBase.LogRoundResult`, `GameLogger` chế độ 2 người) gọi `LogRound(..., pushSheets: false)` để không đẩy trùng; `MiniGameControllerBase` dùng tên mà `LogRound` trả về (trước đây dùng tên của `GameSessionManager`, có thể là tên chung/cũ).
- `GameLogger.LogIndependentRound` **không** đẩy `round_end` nữa (tên của nó là tên lúc vào ván, hay sai); round độc lập của TestTongHop đi qua `LogRound(..., topic)`.
- **Trùng dòng**: nguyên nhân là POST cả hàng nghìn dòng gửi bù trong 1 request (timeout 10s) → app tưởng lỗi, gửi lại, Apps Script vẫn đã ghi. Sửa: tối đa 200 dòng/POST, timeout 30s, và Apps Script loại trùng theo `rid`.
- **Apps Script**: bản tham chiếu `docs/apps-script/Code.gs` (tự thêm cột cho field mới, loại trùng theo `rid`). Muốn có cột `deviceId/sessionId/rid/correctAnswer...` trên Sheet phải dán script này (hoặc sửa script hiện có cho map cột theo tên) rồi **Deploy → New version**. Chưa biết script đang chạy có tự thêm cột không.
- Đối chiếu 08/10/2026 (code cũ): TongHopToan khớp 300/300 dòng cả tên; Counting/Counting5 khớp số dòng nhưng tên sai (GameLogger); AddNumber5Digit, ChuCaiThuong chưa lên Sheet. Kiểm tra lại bằng `docs/danh-gia-nang-luc/tool/pipeline/compare_local.py`.
- **Chưa compile Unity / chưa test trên K02** các sửa này.


### Đồng bộ theo file lên Drive (2026-10-20) — THAY cơ chế từng dòng

**Vì sao đổi**: cơ chế cũ ghi mọi round/click thành 1 dòng vào 1 Sheet → đầy giới hạn 10M ô (~144k dòng × 30 cột), Apps Script chậm dần > timeout 30s → app gửi lại → Sheet nhận lặp vô hạn cùng 1 lô 200 dòng, từ 09/10 không có dữ liệu mới. Hàng đợi từng dòng cũng dễ kẹt (1 dòng hỏng chặn cả lô), `WaitForSeconds` bị đóng băng khi Stop (`timeScale=0`).

**Cơ chế mới**
- Nguồn = chính file log local `GameLogs/yyyy-MM-dd_HHmmss_<Game>.json` (`PlayerRecognitionService` ghi lại cả file sau mỗi sự kiện; `GameLogger.Export` ghi file click chi tiết). Không còn `Enqueue`/`rid`/`sessionId`/`sheets_pending.jsonl`; `LogRound(pushSheets, topic)` giữ tham số nhưng không tác dụng.
- `SheetsSyncManager` quét `GameLogs` mỗi `intervalSeconds` (tối thiểu 15s; mặc định 30s; thời gian thực), file mới/đã đổi (so `độ dài|mtime` ở `sheets_sync_state.tsv`) → `POST <webAppUrl>?device=<id>&name=<file>[&key=<secret>]`, body = nguyên file. Cùng tên = ghi đè → idempotent. Lần chạy đầu tự gửi bù toàn bộ `GameLogs`; hàng đợi cũ được đổi tên `sheets_pending.jsonl.legacy` (mọi dòng đều suy ra được từ GameLogs).
- Chỉ tính xong khi script trả `{"ok":true,"bytes":N}` và N = số byte đã gửi. Lỗi: lùi nhịp 30s→15 phút theo từng file, quá 6 lần bỏ qua tới khi file đổi/khởi động lại; không chặn file khác. Tối đa 25 file/chu kỳ, file ghi trong 3s gần nhất để chu kỳ sau.
- Drive: `EduXplore_Logs/<deviceId>/<yyyy-MM-dd>/<file>.json` (`docs/apps-script/DriveUpload.gs`, cài đặt ở đầu file). Có `doGet?action=list|get` cho pipeline (`run.py --drive`). Thư mục để Riêng tư (có tên trẻ em).
- Báo cáo: `docs/danh-gia-nang-luc/tool/pipeline` (`run.py --drive` hoặc `--local <thư mục Drive>`), dùng lại `ingest_local`.

**Triển khai (thứ tự quan trọng)**: (1) dán `DriveUpload.gs`, Deploy (dự án cũ: New version để giữ URL); (2) mở URL `/exec` thấy "EduXplore Drive upload — OK"; (3) mới cài bản app này lên K02 (script cũ nhận body kiểu file sẽ báo lỗi, app tự lùi nhịp, không ghi rác); (4) xem `adb logcat -s Unity | grep SheetsSyncManager` → "Chu kỳ: gửi n/m file".
**Chưa compile Unity / chưa test trên K02 / chưa chạy Apps Script thật.** Sheet cũ (143k dòng) giữ nguyên để tra; không còn ghi vào.
