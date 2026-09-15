# CLAUDE.md — EduXplore 2.0

Derived from: `EduGame/edu-game` (EduXplore v1).
Unity 2022.3.62f1 · IL2CPP · Android primary target.

## Deployment context

**Floor projection setup** — projector chiếu xuống sàn nhà, người chơi đứng/nhảy tương tác.
- Landscape locked (LandscapeLeft)
- Multi-touch bắt buộc
- Screen never sleeps (`LauncherBehaviour.keepScreenAwake = true`)
- App là **Android HOME launcher** — không thoát được bằng Back button
  > **K02 dual-display (Track A, xem section riêng bên dưới)**: `ControlActivity` (entry point
  > mới trên K02) hiện là launcher thường (`MAIN`/`LAUNCHER`), **KHÔNG** đăng ký `HOME`/`DEFAULT`
  > — chủ động tắt theo yêu cầu lúc test. Bật lại khi cần: thêm `HOME`+`DEFAULT` category vào
  > intent-filter của `ControlActivity` trong `NativePlugins/ControlUiAndroidLib/controlui/src/main/AndroidManifest.xml`.

## Điểm khác so với v1

| | v1 (edu-game) | v2 (eduXploreGame2.0) |
|---|---|---|
| Package | com.EduCompany.EduGame | com.EduXplore.EduXploreGame20 |
| Launcher | Không | Có (HOME + DEFAULT intent) |
| Scene đầu | StartScene → HomeScene → Menu | StartScene → MenuScene thẳng |
| Back button | Thoát app | Về MenuScene / noop ở menu |
| Screen sleep | Mặc định | Never sleep |

## Scene flow

```
StartScene (load CSV + TongHopConfig + AssetOverrides)
    └── MenuScene  ← màn hình chính, không thoát được
            ├── TestTongHopGame  (game chính — Choose + Matching)
            ├── AddUpGame
            ├── TrainPathGame
            └── ... (thêm dần)
```

## Key scripts

- `LauncherBehaviour.cs` — gắn vào MenuScene: chặn Back, giữ màn hình sáng, lock landscape
- `StartMainController.cs` — load data rồi LoadScene("MenuScene") thay vì HomeScene
- `AndroidManifest.xml` — `Assets/Plugins/Android/` — HOME + DEFAULT category

## Architecture (kế thừa từ v1)

**MVC per scene** · **Custom FSM** (`CustomFSMManager.cs`) · **Singleton pattern**
**GameSessionManager** — cross-scene data (mode, player names, scores)
**TongHopConfig** — JSON tunable config tại `StreamingAssets/TestTongHop/gameconfig.json`
**GameLogger** — 3-tier logging: Click → Round → Session → JSON file

## Canvas

Reference resolution: **1024×600**, ScreenSpaceOverlay.
> Nếu projector là 1920×1080, scale UI bằng `Canvas Scaler → Match Width Or Height = 0.5`.

## Build

- **Target SDK**: 34 · **Min SDK**: 22 · **IL2CPP**
- Keystore: `Keystore/edugame.keystore` (alias: `edugame`)
- Package: `com.EduXplore.EduXploreGame20`
- **QUAN TRỌNG**: Sync CSV sau khi sửa:
  ```
  Assets/MasterData/Csv/*.csv  →  Assets/StreamingAssets/MasterData/Csv/
  ```

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

### LiDAR touch — trong-process Unity, không qua OS injection

`LidarTouchBridge.cs` (Singleton, `Assets/Game/Scripts/UIScripts/Common/`) đọc dữ liệu qua
`liblidar_unity.so` (native plugin port từ `MyNativeApp_v4`, xem `NativePlugins/LidarUnity/`) rồi
tự bắn `PointerEventData` vào `EventSystem` — y hệt 1 cú chạm thật, KHÔNG qua Android
InputManager/AccessibilityService/`dispatchGesture`/`injectInputEvent` (không cần root/quyền OS
đặc biệt). USB bridge (`LidarUsbBridge.java`,
`Assets/Plugins/Android/lidarlib-release.aar`) đọc CP2102 UART, gọi
`UnitySendMessage("LidarTouchBridge", "OnUartFdReady", fd)`.

- **Nút cứng bật/tắt** (van an toàn — LiDAR có thể bắn touch rất nhanh/nhiều): `KeyCode.JoystickButton0`
  (xác nhận thực tế trên K02, KHÔNG phải `KeyCode.Return`). Mặc định OFF lúc khởi động.
- **Vòng tròn debug touch** (thay "Show taps" của Android — không dùng được trên K02):
  `showTouchIndicator`/`indicatorColor`/`indicatorDiameter` trên `LidarTouchBridge` — hiện đang
  màu đỏ, x1.5 kích thước gốc.
- **Config calib** (`half_x`, `hight_floor`, `ymax`, `shift_x/y`, `offset_angle`, ...) — file JSON
  editable trên máy KHÔNG cần rebuild: `Application.persistentDataPath/lidar_config.json`, gọi
  `LidarTouchBridge.Instance.ReloadConfig()` hoặc restart app để áp dụng. Giá trị calib thật đã
  xác nhận hoạt động: `{half_x:1130, hight_floor:1350, ymax:-600, shift_x_floor:1, shift_y:1,
  shift_x:1, offset_angle:-5, nums_point_report:2}`.
- `GameControlBridge.OnPauseRequested/OnResumeRequested`/`OnStopRequested` đều gọi
  `LidarTouchBridge.SetTouchEnabled()` — dùng CHUNG cờ với nút cứng, nên nút cứng có thể ghi đè
  trạng thái Pause của control panel nếu bấm không đúng lúc (biết trước, chưa fix — hỏi trước
  khi đụng nếu cần tách riêng 2 cờ).

### Report + Google Sheets sync (Track E)

- `GameControlBridge.PushReport(secondsLeft, leftName, leftScore, rightName, rightScore)` —
  đẩy report throttle 1 lần/giây từ `MiniGameControllerBase.Update()`/
  `TestTongHopController.Update()` sang `ControlActivity.UpdateReport()` (hiện tên + điểm TỪNG
  BÊN riêng, không gộp tổng).
- `SheetsSyncManager.cs` — gom log (`GameLogger`, `MiniGameControllerBase.LogRoundResult`,
  `PlayerRecognitionService`) gửi lên Google Sheet qua Apps Script Web App (không cần OAuth phía
  app), batch mỗi ~3s. Config: `Application.persistentDataPath/sheets_sync_config.json`
  (`webAppUrl`, `enabled`, `intervalSeconds`).
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

## Phase roadmap

- **Phase 1** ✅ — Dự án mới, launcher, vào thẳng MenuScene
- **Phase 2** — Refactor `PlayerSlot` (1P / 2P / 3-4P chung code)
- **Phase 3** — Game modes: Endless, Survival, Time Attack, Sudden Death
- **Phase 4** — 3-4 người cùng màn hình (multi-touch zones)
- **Phase 5** — Multi-device (LAN/Bluetooth)
