# ControlActivity, Stop/Start, cài đặt game (Track A)

> **TL;DR**: K02 dùng 2 Activity/2 display: ControlActivity (Java View, display 0) điều khiển, UnityPlayerActivity (máy chiếu) chạy game. Stop KHÔNG destroy Unity; Start lần 2 gửi UnitySendMessage. Cài đặt game gửi bằng JSON.
> **Đọc khi**: sửa NativePlugins/ControlUiAndroidLib, GameControlBridge/ControlBridge, tab Cài đặt, luồng Start/Pause/Stop, lớp/điểm/registry hiển thị.
> **Cập nhật**: 2026-10-04 (tách nguyên văn từ CLAUDE.md gốc, chưa sửa nội dung)

## Track A — K02 dual-display + LiDAR touch (session "Dual display setup K02")

Deployment thứ 2 song song với luồng MenuScene gốc ở trên: tablet **K02** xuất HDMI ra máy
chiếu, dùng **2 display độc lập** (không mirror) + **LiDAR** làm nguồn touch sàn thay cho
tay chạm màn hình. Toàn bộ phần dưới đây chỉ áp dụng cho deployment K02 — không đụng tới
luồng MenuScene/StartScene gốc.

### Kiến trúc 2 Activity, 2 display

- **`ControlActivity`** (Java, `NativePlugins/ControlUiAndroidLib/`, đóng gói thành
  `Assets/Plugins/Android/controlui-release.aar`) — entry point mới trên K02, chạy **display 0**
  (màn hình tablet). Android View thuần (KHÔNG WebView — Chromium không init được GL context
  trên K02, lỗi driver GPU tầng hệ thống, đã xác nhận thực tế, không sửa được từ app). Menu chọn
  game + panel Pause/Stop/report lúc đang chơi, 2 View con trong CÙNG Activity (không phải 2
  Activity).
- **`UnityPlayerActivity`** — chạy **display phụ** (máy chiếu), khởi động qua
  `ActivityOptions.setLaunchDisplayId()` từ `ControlActivity.onStartClicked()`. Đã bỏ
  `MAIN`/`LAUNCHER` khỏi manifest của nó (`Assets/Plugins/Android/AndroidManifest.xml`) —
  `ControlActivity` là entry point duy nhất giờ.
- **`ControlBridge.cs`** (Unity, `Assets/Game/Scripts/UIScripts/Common/`) — đọc scene+tên game
  qua Intent extra lúc cold-boot (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`), set
  `GameSessionManager.SelectedGameName` rồi `SceneManager.LoadScene()` thẳng vào game đã chọn
  (không qua MenuScene). `LoadGame(sceneName, gameName)` là entry point dùng chung — cả cold-boot
  lẫn lúc Unity đã sống sẵn (xem bên dưới) đều gọi qua đây.

**Giao diện `ControlActivity` (2026-09-17)** — nền **sáng**, đồng bộ bảng màu với Launcher (xem
section riêng bên dưới) — toàn bộ màu đi qua token trong `res/values/colors.xml` của
`ControlUiAndroidLib` (`bg/panel/text/accent/good/live/bad/...`), đổi giá trị token là đổi hết cả
UI, không hardcode hex rải rác trong Java (2 chỗ hardcode còn lại — `0xFF0B1710`, chữ đen trên nút
START/Chơi lại nền xanh `good` — không cần đổi, không phụ thuộc theme sáng/tối). Có logo EduXplore
góc trên-trái top bar (`@drawable/logo_eduxplore_transparent` — bản NỀN TRONG SUỐT, khác
`@drawable/logo_eduxplore` bản NỀN ĐẶC/navy dùng full-bleed cho `showLogoOnSecondaryDisplay()` ở
máy chiếu — **đừng nhầm 2 file, dùng sai bản sẽ dính khung màu**). Chữ "Môn học"/"Lớp" và giá trị
đang chọn phóng to (21sp, trước là 12.5sp) vì đây là tiêu đề chính của màn hình.

**Tên game hiển thị ≠ định danh nội bộ** — `GameRegistry.cs`'s `GameEntry` giờ có 2 field tách
biệt: `name` (định danh nội bộ ổn định — khoá tra icon `Assets/Resources/GameIcons/`, khoá CSV
variant, giá trị gửi Unity qua `EXTRA_GAME_NAME`/`OnLoadGameRequested` — **không đổi khi sửa câu
chữ**) và `displayName` (tiếng Việt, hiện lên UI). Mirror `game_registry.json`
(`NativePlugins/ControlUiAndroidLib/controlui/src/main/assets/`) đã thêm field `displayName`
tương ứng — sửa tên hiển thị thì sửa Ở CẢ 2 FILE (C# + JSON) để không lệch. `ControlActivity.java`
dùng `item.displayName` để vẽ danh sách chọn game, nhưng vẫn giữ `selectedGameName`/`item.name`
(định danh nội bộ) cho toàn bộ logic chọn/gửi Unity — chỗ hiển thị tên game ở nơi khác
(pause card, banner, summary...) tra ngược qua `displayNameOf(selectedGameName)`.

### ControlActivity — lớp/điểm THẬT, hết mock (2026-10-03)

`MockData.java` đã xoá. **Lớp/học sinh**: `ui/ClassRepo.java` đọc `/sdcard/EduXplore/classes.json` + `enrolled.json` (do "Quản lý lớp" ghi; chỉ đọc name/alias/className bằng JsonReader, bỏ qua embedding) — nạp lại ở `onResume()`. **Điểm** (cập nhật 2026-10-04): Unity đẩy TỪNG round qua `ControlActivity.OnRound(json)` (xem `docs/contracts/java-unity-bridge.md`) → `ui/ScoreStore.java` ghi nối `/sdcard/EduXplore/class_rounds.jsonl` (fallback `getExternalFilesDir`), chỉ học sinh đã nhận diện tên. Quá 20.000 round thì phần cũ chuyển sang `class_rounds_archive.jsonl` (không xoá; app chưa đọc archive). Điểm 1 học phần = round đúng / round đã chơi trong **50 round gần nhất** × 100; môn = TB các học phần đã chơi; "Tất cả" = TB các môn đã chơi; chưa chơi hiện 0 nhưng không vào trung bình. **Học phần** = `group` của game trong registry, không có thì tên môn. Tab Lớp học có 1 nút chạm đổi vòng kiểu điểm (Tất cả → Môn → Phần). Tab Lịch sử: chọn học sinh → ván theo ngày → từng câu (câu hỏi, chọn, đúng, ✓/✗, giây; media ghi "Câu hỏi media"). `class_scores.json` cũ đã bỏ.

### Cài đặt game trong ControlActivity (2026-10-04)

Tab "Cài đặt" của `ControlActivity` thay panel setting cũ trên Unity: thời gian game, chờ chuyển round (1–4s), tốc độ flow (0.1–5x, slider + ô số), nhạc, SFX, ô tick "chờ clear mới chuyển round" (mặc định BẬT, áp mọi game kể cả Independent). Thời gian mỗi câu: bỏ (không giới hạn). Lưu `/sdcard/EduXplore/game_settings.json` (`ui/SettingsStore.java`). Gửi Unity: đang sống → `UnitySendMessage("GameControlBridge","OnSettingsChanged",json)`; cold-boot → Intent extra `com.eduxplore.control.SETTINGS_JSON` (`ControlBridge.Init`). Unity phía nhận: `GameSettings.ApplyJson` (field vắng/âm = không đổi). Chờ clear: Base `TransitionCountdown`/`IndependentPlayerLoop` + `FloorZoneClearer.AwaitIfEnabled/AwaitSide`. Flow: `SpawnFlowDisplay`, `LaneTrack`. Chi tiết + trạng thái: `handoff/2026-10-04-game-settings.md`. **7 game đóng băng** (AddUp, NumberAddUp, PathFinder, TrainPath, Monopoly, RiverCross, Balloon): không đụng, xem `handoff/FROZEN.txt` + `AGENTS.md`.

### Stop/Start — KHÔNG destroy UnityPlayerActivity (gotcha quan trọng)

**Unity tự gọi `Process.killProcess()` cả process (dùng chung với `ControlActivity`) khi
`UnityPlayerActivity` bị destroy** — hành vi mặc định của Unity/IL2CPP trên Android, xác nhận
qua log `Process: Sending signal... SIG: 9` ngay sau khi Unity chạy xong quy trình quit của nó.
Không sửa được từ code app. Vì vậy:

- **Stop** (`GameControlBridge.OnStopRequested`) **KHÔNG BAO GIỜ** finish/destroy
  `UnityPlayerActivity` — chỉ pause game (timer/touch/nhạc) + che đen display máy chiếu bằng 1
  Canvas overlay riêng (`GameControlBridge.ShowBlankOverlay`). Activity vẫn sống nguyên, giống
  hệt việc bấm Home để app chạy nền.
- **Start lần 2 trở đi** không launch Activity mới — gửi `UnitySendMessage("GameControlBridge",
  "OnLoadGameRequested", "scene|gameName")` cho instance Unity đang sống, Unity tự
  `ControlBridge.LoadGame()` load scene mới. `ControlActivity.unityStarted` (boolean) theo dõi
  đã từng Start lần đầu chưa để quyết định startActivity() thật hay chỉ gửi message.
- **Hết giờ tự nhiên** (không bấm Stop) → `GameControlBridge.PushGameEnded()` (gọi từ
  `StateMachineEnter_GameOver` của `TestTongHopController`/`MiniGameControllerBase`) báo
  `ControlActivity.OnGameEnded()` tự quay Menu — coi như hết 1 round. Display máy chiếu KHÔNG bị
  đụng, vẫn hiện đúng ScoreScene/kết quả.
- `TaskRemovedWatcherService` — chỉ còn xử lý case user thật sự swipe 1 trong 2 task qua Recents
  (kill cả process dọn sạch cả 2 display). Không còn liên quan gì đến nút Stop nữa.
- `WatchdogService` (process riêng `:watchdog`, bind vào `HeartbeatService` để phát hiện process
  chính chết bất ngờ rồi tự relaunch `ControlActivity`) — code có sẵn nhưng **đang tắt**
  (`ControlActivity.onCreate()`, dòng `startService(new Intent(this, WatchdogService.class))` bị
  comment) theo yêu cầu user lúc test. Bật lại khi cần.
