# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

"Tiny Explorers" by EduXplore -- Unity educational game for Android/iOS/macOS featuring 2-player split-screen gameplay. Three games are implemented (AddUp, TrainPath, PathFinder) plus a TeamSelect scene. Built with Unity 2022.3.62f1 (IL2CPP backend). Canvas reference resolution: 1024x600, ScreenSpaceOverlay.

## Architecture

**MVC per scene**: Each scene has its own State, View, Controller classes under `Assets/Game/Scripts/UIScripts/<SceneName>/`. Scene UIs are built programmatically via SceneBuilder editor tools in `Assets/Game/Scripts/Editor/`.

**Custom FSM**: `Assets/Game/Scripts/Library/CustomFSM/CustomFSMManager.cs` drives scene state transitions using reflection-based delegate creation. Controllers implement `StateMachineEnter_<State>` / `StateMachineExit_<State>` methods.

**Master Data pipeline**: CSV files in `Assets/MasterData/Csv/` -> parsed by `LoadCsvDataForEditor` -> cached in `MasterDataCache` (namespace `MasterData`). ScriptableObject classes in `Assets/MasterData/CsvClass/`. For standalone/iOS builds: CSV read directly from `StreamingAssets/MasterData/Csv/`. For Android: CSV preloaded via coroutine from StreamingAssets.

**IMPORTANT**: When updating CSV files, ALWAYS sync both locations:
- Source: `Assets/MasterData/Csv/<name>.csv`
- Build copy: `Assets/StreamingAssets/MasterData/Csv/<name>.csv`

Failing to sync causes runtime crashes (build reads from StreamingAssets, not Assets/MasterData).

**Singleton pattern**: `Assets/Game/Scripts/UIScripts/Common/Singleton.cs` -- generic MonoBehaviour singleton used across systems.

**GameSessionManager**: `Assets/Game/Scripts/UIScripts/Common/GameSessionManager.cs` -- Singleton storing cross-scene data (game mode, player names, scores, last played game).

## Scene Flow

```
StartScene (load CSV data) → HomeScene → TeamSelectScene → MenuScene (game select) → [AddUpGame | TrainPathGame | PathFinderGame] → ScoreScene → (Home / Replay)
```

### Build Scenes (in order)
1. StartScene
2. MenuScene
3. AddUpGame
4. TrainPathGame
5. HomeScene
6. CharacterSelectScene
7. ScoreScene
8. TeamSelectScene
9. PathFinderGame

Each scene has a SceneBuilder in `Assets/Game/Scripts/Editor/` for programmatic UI creation (`Tools > <SceneName> > Build Scene`).

## Games

| Game | Description | Data Source | Time Limit |
|------|-------------|-------------|------------|
| **AddUp** | Select 2 of 3 numbers that add up to a target | `add_up_master.csv` | 100s |
| **TrainPath** | 3x3 rotating grid, guess hidden track cell type (Left/Straight/Right) | `train_path_master.csv` (20 path templates) | 120s |
| **PathFinder** | Choose correct path avoiding obstacles to reach destination | `path_finder_master.csv` | TBD |

### TrainPath Game Details
- Path templates loaded from CSV: entry/exit points as `"row:col"`, path as `"r:c|r:c|..."`
- Cell types (Straight/TurnLeft/TurnRight/Bush) auto-computed from path direction via 2D cross product
- Grid rotates continuously during gameplay (12°/sec) for difficulty
- Train animates along path after answering (uses localPosition in shared gridContainer)
- Visual track strips (2 per cell) show entry/exit directions with colored Image strips

## Key Directories

- `Assets/Game/Scripts/UIScripts/` -- All scene code (MVC per scene)
  - `Common/` -- Singleton, GameSessionManager, GameSettings, DataManager, PlayerInfo, TeamData, ClassData, TutorialPanel, LongPressButton
  - `HomeScene/` -- Home screen (State/View/Controller)
  - `CharacterSelectScene/` -- Character/mode selection
  - `TeamSelectScene/` -- Team selection (State/Views/Models/Controllers)
  - `MenuScene/` -- Game selection menu
  - `AddUpGame/` -- AddUp game (Models/Views/Controllers)
  - `TrainPathGame/` -- TrainPath game (Models/Views/Controllers)
  - `PathFinderGame/` -- PathFinder game (Models/Views/Controllers including MazeGenerator)
  - `ScoreScene/` -- Score display after game
  - `StartScreen/` -- Initial data loading screen
- `Assets/Game/Scripts/Editor/` -- SceneBuilder editor tools (one per scene)
- `Assets/MasterData/Csv/` -- Source CSV data files
- `Assets/MasterData/CsvClass/` -- ScriptableObject master data classes (namespace `MasterData`)
- `Assets/StreamingAssets/MasterData/Csv/` -- CSV copies for builds (MUST be synced with source)

## Master Data Workflow

### Adding new master data
1. Create CSV file in `Assets/MasterData/Csv/`
2. **Copy CSV to `Assets/StreamingAssets/MasterData/Csv/`** (required for builds!)
3. Create master class in `Assets/MasterData/CsvClass/` (follow `AddUpMaster.cs` or `TrainPathMaster.cs` pattern)
4. Create Importer in `Assets/Editor/CSVImporter/`
5. Register in `StartMainController.cs` `masterDataGroup` array
6. Add `Load<MasterName>` method to `LoadCsvDataForEditor`
7. In game controller, add `InitializeGameWithMasterData()` coroutine with retry logic (see `AddUpGameController` or `TrainPathGameController` for pattern)

### CSV data format
- **AddUp**: `question_id,number_a,number_b,hidden_position,answer_1,answer_2,answer_3,answer_4,correct_answer,image_type,difficulty`
- **TrainPath**: `template_id,entry,path,exit,difficulty` (entry/exit as `"row:col"`, path as `"r:c|r:c|..."`)
- **PathFinder**: `question_id,correct_path,obstacle_left,obstacle_middle,obstacle_right,destination,theme,difficulty`

### Syncing CSV files
```bash
# After editing any CSV in Assets/MasterData/Csv/, sync to StreamingAssets:
cp Assets/MasterData/Csv/*.csv Assets/StreamingAssets/MasterData/Csv/
```

## Build Notes

### Android
- **Target SDK**: 34 (Android 14) -- set in ProjectSettings
- **Min SDK**: 22
- **Scripting Backend**: IL2CPP
- **Keystore**: `Build/edugame.keystore` (alias: `edugame`)
- Keystore passwords are NOT saved in ProjectSettings -- must be set via `PlayerSettings.Android.keystorePass`/`keyaliasPass` before build
- Code stripping/minify can cause crashes -- disable temporarily to debug
- Use `adb logcat` and Development Build with Script Debugging for crash investigation
- Google Mobile Ads must be initialized before use
- Reflection-based code (FSM) needs IL2CPP preserve attributes
- On Android, CSV files must be preloaded from StreamingAssets via `PreloadAllCsvFiles()` coroutine before use

### macOS
- Build output: `Builds/macOS/EduGame.app`
- Apple Silicon native (M1/M2/M3)
- Lightmapper auto-switches to GPU on Apple Silicon (harmless warning)

### Build Outputs
- Android: `Builds/Android/EduGame.apk`
- macOS: `Builds/macOS/EduGame.app`

## Dependencies

Key packages: TextMeshPro, Unity Ads (4.4.2), Unity Purchasing (4.11.0), Google Mobile Ads, Svelto ECS.

## Known Issues (Resolved)

- ~~Missing scripts on Player/ball_exp_yellow_eff prefabs~~ -- cleaned via PrefabUtility (2026-03-26)
- ~~DOTweenSettings.asset orphaned~~ -- deleted, DOTween not in use (2026-03-26)
- ~~StreamingAssets CSV out of sync~~ -- synced train_path_master.csv and add_up_master.csv (2026-03-26)
