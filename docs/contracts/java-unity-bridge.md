# Contract: ControlActivity (Java) ↔ Unity

> **TL;DR**: mọi thay đổi ở đây phải sửa CẢ Java và C# cùng lúc, rồi `sh Tools/build-aar.sh controlui` + build lại APK.
> **Đọc khi**: thêm/đổi message, Intent extra, hoặc field settings.
> **Cập nhật**: 2026-10-04 (đối chiếu `GameControlBridge.cs`, `ControlBridge.cs`, `SettingsStore.java`, `GameSettings.cs`)

## Java → Unity: `UnitySendMessage(gameObject, method, string)`
GameObject `GameControlBridge` (`Assets/Game/Scripts/UIScripts/Common/GameControlBridge.cs`):

| Method | Payload | Ý nghĩa |
|---|---|---|
| `OnPauseRequested` | bỏ qua | Pause game (timer/touch/nhạc) |
| `OnResumeRequested` | bỏ qua | Tiếp tục |
| `OnStopRequested` | bỏ qua | Pause + che đen máy chiếu. **Không** destroy Activity |
| `OnSettingsChanged` | JSON settings (dưới) | Áp + lưu settings ngay, không load lại scene |
| `OnLoadGameRequested` | `"sceneName\|gameName"` | Start lần 2 trở đi, load game mới trong Unity đang sống |

GameObject `CalibControlBridge` (`UIScripts/Calib/CalibControlBridge.cs`): `OnStartCalibRequested`, `OnStartSequentialRequested`, `OnCaptureStepRequested`, `OnStepBackRequested`, `OnCandidateChosen` (payload = chỉ số ứng viên), `OnSaveRequested`, `OnExitRequested`.

## Unity → Java: `AndroidJavaClass("com.eduxplore.control.ControlActivity").CallStatic`
`OnRound(json)` (MỖI câu/round vừa trả lời — xem bên dưới), `UpdateReport(...)` (điểm + thời gian, throttle 1 lần/giây), `UpdateLivePlayers(leftJson, rightJson)`, `OnGameEnded()` (hết giờ tự nhiên → ControlActivity quay Menu). Calib gọi ngược `com.eduxplore.control.CalibActivity`.

## Intent extras (cold-boot, đọc 1 lần ở `ControlBridge.Init`)
- `com.eduxplore.control.SCENE_NAME`, `com.eduxplore.control.GAME_NAME` (định danh nội bộ `name`, không phải `displayName`)
- `com.eduxplore.control.SETTINGS_JSON` (cùng format JSON settings)

## JSON settings
Nguồn: `ui/SettingsStore.java` (lưu `/sdcard/EduXplore/game_settings.json`); bên nhận: `GameSettings.ApplyJson` (`GameSettings.cs`).

| Field | Kiểu | Khoảng / mặc định | Ghi chú |
|---|---|---|---|
| `musicVolume`, `sfxVolume` | float | 0–1, mặc định 1 | |
| `gameTime` | int (giây) | > 0, mặc định 100 | |
| `roundEndDelay` | float (giây) | 1–4, mặc định 2 | clamp phía Unity |
| `flowSpeed` | float | 0.1–5, mặc định 1 | hệ số tốc độ game spawn liên tục |
| `waitForClear` | int | 1 bật (mặc định), 0 tắt, -1 không đổi | OR với cờ riêng từng scene |

Quy tắc: field vắng hoặc âm (`gameTime` ≤ 0, `flowSpeed` ≤ 0) = **không đổi**. Thêm field mới: thêm cả vào `SettingsStore.java`, `GameSettings.Dto` và bảng này, mặc định -1 ở Dto.

## Unity → Java: `OnRound(json)` (từng câu, 2026-10-04)
Gửi từ `PlayerRecognitionService.LogRound` → `GameControlBridge.PushRound` (điểm chèn chung của mọi game; game cũ gọi `LogRound` trực tiếp cũng được phủ). ControlActivity lưu vào `/sdcard/EduXplore/class_rounds.jsonl` (`ui/ScoreStore.java`).

| Field | Ý nghĩa |
|---|---|
| `name`, `recognized` | tên học sinh nhận diện; `recognized=false` (Player_1...) thì bị bỏ qua |
| `game` | định danh game (`GameSessionManager.ResolveActiveGameName`); không có trong registry thì dùng game đang chọn |
| `slot` | `left`/`right` |
| `round`, `questionId` | số thứ tự câu; id câu hỏi (`QuestionData.id`, GenericGame = `<gameId>_<chỉ số round>`) |
| `question`, `answer`, `correctAnswer` | chữ đọc được; câu hỏi chỉ ảnh/âm thanh → `"Câu hỏi media"` (override `DescribeQuestionForLog`/`DescribeAnswerForLog`/`DescribeCorrectForLog` ở `MiniGameControllerBase`) |
| `correct`, `sec` | đúng/sai, số giây trả lời |

Điểm: học phần = round đúng / round đã chơi trong **50 round gần nhất** của học sinh ở học phần đó; môn = TB các học phần đã có điểm; "Tất cả" = TB các môn đã có điểm (chưa chơi hiện 0 nhưng không tính vào TB).
