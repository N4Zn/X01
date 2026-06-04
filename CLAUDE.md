# CLAUDE.md — EduXplore 2.0

Derived from: `EduGame/edu-game` (EduXplore v1).
Unity 2022.3.62f1 · IL2CPP · Android primary target.

## Deployment context

**Floor projection setup** — projector chiếu xuống sàn nhà, người chơi đứng/nhảy tương tác.
- Landscape locked (LandscapeLeft)
- Multi-touch bắt buộc
- Screen never sleeps (`LauncherBehaviour.keepScreenAwake = true`)
- App là **Android HOME launcher** — không thoát được bằng Back button

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

## Phase roadmap

- **Phase 1** ✅ — Dự án mới, launcher, vào thẳng MenuScene
- **Phase 2** — Refactor `PlayerSlot` (1P / 2P / 3-4P chung code)
- **Phase 3** — Game modes: Endless, Survival, Time Attack, Sudden Death
- **Phase 4** — 3-4 người cùng màn hình (multi-touch zones)
- **Phase 5** — Multi-device (LAN/Bluetooth)
