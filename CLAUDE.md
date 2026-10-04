# CLAUDE.md — EduXplore 2.0

Derived from: `EduGame/edu-game` (EduXplore v1).
Unity 2022.3.62f1 · IL2CPP · Android primary target.

> **Bàn giao việc giữa agent**: xem `handoff/README.md` (quy ước + bảng trạng thái) TRƯỚC khi làm việc được giao.

## Deployment context

**Floor projection setup** — projector chiếu xuống sàn nhà, người chơi đứng/nhảy tương tác.
- Landscape locked (LandscapeLeft)
- Multi-touch bắt buộc
- Screen never sleeps (`LauncherBehaviour.keepScreenAwake = true`)
- App là **Android HOME launcher** — không thoát được bằng Back button
  > **K02 dual-display (Track A, xem section riêng bên dưới)**: `ControlActivity` (entry point
  > mới trên K02) hiện là launcher thường (`MAIN`/`LAUNCHER`), **KHÔNG** đăng ký `HOME`/`DEFAULT`
  > — chủ động tắt theo yêu cầu lúc test. Vai trò HOME thật của K02 giờ do 1 **app Launcher riêng**
  > đảm nhiệm (`D:\X_projects\Launcher`, ngoài repo này — xem section "Launcher — Home launcher
  > thật của K02" bên dưới), gọi `ControlActivity` qua 1 trong 2 "hero card" của nó chứ không tự
  > đăng ký HOME. Muốn quay lại cách cũ (chính `ControlActivity` tự làm HOME): thêm `HOME`+
  > `DEFAULT` category vào intent-filter của nó trong
  > `NativePlugins/ControlUiAndroidLib/controlui/src/main/AndroidManifest.xml`.

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

## Track A — K02 dual-display + LiDAR touch (tóm tắt)

Deployment K02 song song luồng MenuScene: tablet xuất HDMI ra máy chiếu, **2 display độc lập**, LiDAR thay touch sàn. Chi tiết ở `docs/`, KHÔNG nằm trong file này.

- **2 Activity, 2 display**: `ControlActivity` (Java, `NativePlugins/ControlUiAndroidLib/` → `controlui-release.aar`, display 0, View thuần, KHÔNG WebView) điều khiển; `UnityPlayerActivity` (máy chiếu) chạy game. Vào game qua `ControlBridge.LoadGame(scene, gameName)`.
- **Stop không bao giờ destroy `UnityPlayerActivity`** (Unity kill cả process). Stop = pause + che đen; Start lần 2 = `UnitySendMessage("GameControlBridge","OnLoadGameRequested","scene|game")`.
- **Settings** từ ControlActivity → Unity bằng JSON (`GameControlBridge.OnSettingsChanged` → `GameSettings.ApplyJson`; cold-boot qua Intent extra). Contract: `docs/control-activity.md`.
- **LiDAR touch** trong process Unity (`LidarTouchBridge.cs` + `liblidar_unity.so`), bắn `PointerEventData`, không qua OS. Config calib nằm TRÊN THIẾT BỊ, không trong APK.
- **Tên game**: `name` (định danh, đừng đổi) ≠ `displayName`; sửa registry ở CẢ `GameRegistry.cs` và `game_registry.json`.
- **Launcher** (Home thật) và **FaceRecognition** là project Kotlin NGOÀI repo (`D:\X_projects\...`).
- **7 game đóng băng** (AddUp, NumberAddUp, PathFinder, TrainPath, Monopoly, RiverCross, Balloon): không đụng, xem `AGENTS.md` + `handoff/FROZEN.txt`.

## Đọc gì khi làm gì (đừng đọc hết)

| Việc | Đọc |
|---|---|
| ControlActivity, tab Cài đặt, Start/Pause/Stop, GameControlBridge, registry | `docs/control-activity.md` |
| LiDAR không nhận/lệch, LidarTouchBridge, native, Calib, tune config | `docs/lidar-calib.md` |
| Cài/khôi phục máy K02 (HDMI, quyền USB, audio) | `docs/k02-device.md` |
| Launcher, Quản lý lớp, FaceEnroll | `docs/launcher-roster.md` |
| Log/report/Google Sheets | `docs/sheets-report.md` |
| Import zip GenericGame, scene riêng | `docs/generic-game-import.md`, `WebTools/GenericGameBuilder/CLAUDE.md` |
| Build `.aar` (Android modules) | `Tools/build-aar.sh`, `handoff/build-aar.md` |
| Giao việc / quy tắc agent | `AGENTS.md`, `handoff/README.md` |
| Việc đang dở | `TODO.md` · nhật ký theo ngày: `notes/YYYY-MM.md` (không tự đọc) |

Nhiều doc có thể lỗi thời so với code: kiểm tra dòng "Cập nhật" đầu mỗi doc, nghi ngờ thì đối chiếu code.

## Phase roadmap

- **Phase 1** ✅ — Dự án mới, launcher, vào thẳng MenuScene
- **Phase 2** — Refactor `PlayerSlot` (1P / 2P / 3-4P chung code)
- **Phase 3** — Game modes: Endless, Survival, Time Attack, Sudden Death
- **Phase 4** — 3-4 người cùng màn hình (multi-touch zones)
- **Phase 5** — Multi-device (LAN/Bluetooth)

## GenericGame Builder

Dự án web riêng, ghi chú đầy đủ tại `WebTools/GenericGameBuilder/CLAUDE.md` (runtime Unity: `Assets/Game/Scripts/Core/MiniGameKit/GenericGame/`).

## Architecture changelog (chỉ mốc lớn; chi tiết theo ngày ở `notes/`)

- 2026-09: Track A K02 dual-display + LiDAR touch; Launcher riêng; Quản lý lớp; calib vùng tương tác.
- 2026-10-03: GenericGame import zip → scene riêng; lớp/điểm thật (hết mock); USB/audio K02 cấu hình bằng root.
- 2026-10-04: settings chuyển sang ControlActivity (JSON contract); wait-clear dùng chung; đóng băng 7 game; chuẩn hoá docs (tách CLAUDE.md), LFS, workflow agent (`AGENTS.md`, `handoff/`).
