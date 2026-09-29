# SPEC-v1-edugame-overview.md

**Document**: Project Overview – EduGame Unity
**Created**: 2026-03-18
**Last Updated**: 2026-03-18
**Status**: ✅ Complete
**Owner**: Development Team
**Approach Applied**: UC1 (Visual Information) + UC2 (Overview Research)
**Primary Rules**: MECE, Pyramid Principle
**Supporting Rules**: First Principles, Systems Thinking, Feynman Technique

---

## Key Findings

| # | Finding | Chi tiết | Confidence |
|---|---------|----------|------------|
| 1 | **MVC + FSM Architecture** | Mỗi game scene có Model-View-Controller riêng, điều phối bởi Custom FSM dùng reflection | HIGH |
| 2 | **2-Player Split-Screen** | Canvas 1024x600 chia đôi, mỗi player có panel riêng với câu hỏi độc lập | HIGH |
| 3 | **2/5 Games Implemented** | AddUp (cộng số) và TrainPath (nhận dạng hướng) hoàn thành, 3 slot placeholder | HIGH |
| 4 | **Master Data Pipeline** | CSV → ScriptableObject → MasterDataCache, hỗ trợ cả Editor và Mobile build | HIGH |
| 5 | **Cross-Platform Build** | Android (API 33+) và iOS, IL2CPP backend, cần preserve attributes cho reflection | HIGH |
| 6 | **Singleton Pattern** | Generic MonoBehaviour Singleton thread-safe, dùng cho các hệ thống global | HIGH |
| 7 | **Extensible Game Slots** | Menu có 5 buttons, thêm game mới theo pattern MVC+State có sẵn | MEDIUM |
| 8 | **Monetization Ready** | Tích hợp Unity Ads 4.4.2, IAP 4.11.0, Google Mobile Ads | MEDIUM |

---

## Mục Lục

1. [Tổng Quan Project](#1-tổng-quan-project)
2. [Kiến Trúc Hệ Thống](#2-kiến-trúc-hệ-thống)
3. [Scene Flow & FSM](#3-scene-flow--fsm)
4. [Chi Tiết Các Game](#4-chi-tiết-các-game)
5. [Master Data Pipeline](#5-master-data-pipeline)
6. [Cấu Trúc Thư Mục](#6-cấu-trúc-thư-mục)
7. [Dependencies & Build](#7-dependencies--build)
8. [Mở Rộng & Roadmap](#8-mở-rộng--roadmap)
9. [Tham Khảo](#9-tham-khảo)

---

## 1. Tổng Quan Project

### 1.1 Mô tả

EduGame là ứng dụng **game giáo dục toán học** trên Unity, hướng đến trẻ em, với gameplay **2 người chơi split-screen** trên cùng một thiết bị. Mục tiêu: học toán qua trò chơi thi đấu trực tiếp.

### 1.2 Thông số cơ bản

| Field | Value |
|-------|-------|
| **Engine** | Unity (IL2CPP backend) |
| **Platform** | Android (API 33+), iOS |
| **Orientation** | Landscape |
| **Resolution** | 1024x600 (split-screen) |
| **Company** | EduCompany |
| **Product** | EduGame |
| **Games hiện có** | AddUp, TrainPath (+ 3 placeholder) |
| **Đối tượng** | Trẻ em học toán |

### 1.3 Tổng quan trò chơi

```
+==========================================+
|           EDUGAME - 2 GAMES              |
+==========================================+
|                                          |
|  +----------------+  +----------------+  |
|  |   ADD UP       |  |  TRAIN PATH    |  |
|  |  Cộng 2 số     |  |  Chọn hướng    |  |
|  |  = Target      |  |  đúng pattern  |  |
|  |  10 rounds     |  |  20 rounds     |  |
|  |  100s limit    |  |  120s limit    |  |
|  +----------------+  +----------------+  |
|                                          |
|  [Game 3]  [Game 4]  [Game 5]           |
|  (Placeholder - chưa implement)          |
+==========================================+
```

---

## 2. Kiến Trúc Hệ Thống

### 2.1 Architecture Overview (MECE)

Hệ thống được phân chia thành **5 layer** không chồng chéo, bao phủ toàn bộ:

```
+================================================================+
|                    SYSTEM ARCHITECTURE                          |
+================================================================+
|                                                                 |
|  ┌─────────────────────────────────────────────────────────┐   |
|  │  LAYER 1: SCENES (Entry Points)                         │   |
|  │  StartScene → MenuScene → AddUpGame / TrainPathGame     │   |
|  └─────────────────────────────────────────────────────────┘   |
|                          │                                      |
|  ┌─────────────────────────────────────────────────────────┐   |
|  │  LAYER 2: MVC PER GAME                                  │   |
|  │  ┌───────────┐  ┌───────────┐  ┌───────────┐           │   |
|  │  │ Controller │  │   Model   │  │   View    │           │   |
|  │  │ (Logic +   │  │ (Data +   │  │ (UI +     │           │   |
|  │  │  FSM mgmt) │  │  Rules)   │  │  Events)  │           │   |
|  │  └───────────┘  └───────────┘  └───────────┘           │   |
|  └─────────────────────────────────────────────────────────┘   |
|                          │                                      |
|  ┌─────────────────────────────────────────────────────────┐   |
|  │  LAYER 3: CORE SYSTEMS                                   │   |
|  │  CustomFSMManager (State Machine)                        │   |
|  │  Singleton<T> (Global Access Pattern)                    │   |
|  │  MasterDataCache (Runtime Data Store)                    │   |
|  └─────────────────────────────────────────────────────────┘   |
|                          │                                      |
|  ┌─────────────────────────────────────────────────────────┐   |
|  │  LAYER 4: DATA PIPELINE                                  │   |
|  │  CSV Files → LoadCsvDataForEditor → ScriptableObjects    │   |
|  │  → MasterDataCache → Game Models                         │   |
|  └─────────────────────────────────────────────────────────┘   |
|                          │                                      |
|  ┌─────────────────────────────────────────────────────────┐   |
|  │  LAYER 5: PLATFORM & SERVICES                            │   |
|  │  Android/iOS Build │ Unity Ads │ IAP │ Google Ads        │   |
|  └─────────────────────────────────────────────────────────┘   |
|                                                                 |
+================================================================+
```

### 2.2 MVC Pattern Chi Tiết

Mỗi game scene tuân theo pattern MVC nghiêm ngặt:

```
+─────────────────────────────────────────────────────+
|                  MVC PER GAME SCENE                  |
+─────────────────────────────────────────────────────+
|                                                      |
|   USER INPUT                                         |
|       │                                              |
|       ▼                                              |
|   ┌────────┐  Events   ┌────────────┐               |
|   │  VIEW  │ ────────> │ CONTROLLER │               |
|   │        │           │            │               |
|   │ - UI   │  Update   │ - FSM Mgmt │               |
|   │ - Btns │ <──────── │ - Coroutine│               |
|   │ - Anim │           │ - Logic    │               |
|   └────────┘           └─────┬──────┘               |
|                              │                       |
|                     Read/Write                       |
|                              │                       |
|                         ┌────▼─────┐                 |
|                         │  MODEL   │                 |
|                         │          │                 |
|                         │ - Score  │                 |
|                         │ - State  │                 |
|                         │ - Data   │                 |
|                         └──────────┘                 |
|                                                      |
|   ┌──────────────────────────────────┐               |
|   │  STATE (Enum for FSM)            │               |
|   │  Initialize → Playing → Paused   │               |
|   │  → RoundResult → GameOver        │               |
|   │  → GameResult                    │               |
|   └──────────────────────────────────┘               |
+─────────────────────────────────────────────────────+
```

### 2.3 Custom FSM (Finite State Machine)

FSM sử dụng **reflection** để tự động tìm và gọi các method handler theo naming convention:

```
+──────────────────────────────────────────────────────+
|              CUSTOM FSM MANAGER                       |
+──────────────────────────────────────────────────────+
|                                                       |
|  State Enum: Initialize | Playing | Paused | ...      |
|                                                       |
|  Reflection tìm methods theo pattern:                 |
|  ┌─────────────────────────────────────────────┐     |
|  │ StateMachineEnter_<StateName>(Enum, Dict)   │     |
|  │ StateMachineExit_<StateName>(Enum, Dict)    │     |
|  │ StateMachineUpdate_<StateName>(float)→bool  │     |
|  └─────────────────────────────────────────────┘     |
|                                                       |
|  Ví dụ: State = "Playing"                             |
|  → Gọi StateMachineEnter_Playing() khi vào state     |
|  → Gọi StateMachineUpdate_Playing() mỗi frame        |
|  → Gọi StateMachineExit_Playing() khi rời state      |
|                                                       |
|  ⚠ LƯU Ý: IL2CPP cần preserve attributes             |
|     để reflection hoạt động trên mobile build         |
+──────────────────────────────────────────────────────+
```

---

## 3. Scene Flow & FSM

### 3.1 Scene Navigation

```
+──────────────────────────────────────────────────────────────+
|                      SCENE FLOW                               |
+──────────────────────────────────────────────────────────────+
|                                                               |
|  ┌──────────────┐                                             |
|  │  StartScene   │  (1) Load Master Data                      |
|  │  Index: 0     │  (2) Initialize Singletons                 |
|  └──────┬───────┘  (3) Preload CSV (Android: async)           |
|         │                                                     |
|         ▼                                                     |
|  ┌──────────────┐                                             |
|  │  MenuScene    │  5 game buttons (2 active, 3 placeholder)  |
|  │  Index: 1     │                                            |
|  └──┬───┬───┬───┘                                             |
|     │   │   │                                                 |
|     ▼   ▼   ▼                                                 |
|  ┌─────┐ ┌──────────┐ ┌───────────────┐                      |
|  │AddUp│ │TrainPath  │ │ Game 3/4/5    │                      |
|  │ Idx2│ │  Idx: 3   │ │ (Placeholder) │                      |
|  └──┬──┘ └────┬─────┘ └───────────────┘                      |
|     │         │                                               |
|     └────┬────┘                                               |
|          ▼                                                    |
|     Back to MenuScene                                         |
+──────────────────────────────────────────────────────────────+
```

### 3.2 Game State Machine (AddUp & TrainPath)

```
+──────────────────────────────────────────────────────+
|               GAME STATE TRANSITIONS                  |
+──────────────────────────────────────────────────────+
|                                                       |
|  ┌────────────┐                                       |
|  │ Initialize │ ─── Load data, reset state            |
|  └─────┬──────┘                                       |
|        │                                              |
|        ▼                                              |
|  ┌────────────┐   Pause btn   ┌────────┐             |
|  │  Playing   │ ────────────> │ Paused │             |
|  │            │ <──────────── │        │             |
|  └─────┬──────┘   Resume      └────────┘             |
|        │                                              |
|        │ Answer submitted                             |
|        ▼                                              |
|  ┌─────────────┐                                      |
|  │ RoundResult  │ ─── Show feedback (1.5s)            |
|  └─────┬───────┘                                      |
|        │                                              |
|        ├── More rounds? ──> Back to Playing           |
|        │                                              |
|        ▼ Time expired / All rounds done               |
|  ┌────────────┐                                       |
|  │  GameOver  │ ─── Stop timer, disable input         |
|  └─────┬──────┘                                       |
|        │                                              |
|        ▼                                              |
|  ┌─────────────┐                                      |
|  │ GameResult   │ ─── Show final scores               |
|  │              │     [Retry] → Initialize            |
|  │              │     [Back]  → MenuScene             |
|  └──────────────┘                                     |
+──────────────────────────────────────────────────────+
```

---

## 4. Chi Tiết Các Game

### 4.1 AddUp Game (Cộng Số)

| Field | Value |
|-------|-------|
| **Mô tả** | Chọn 2 trong 3 số có tổng bằng Target |
| **Rounds** | 10 |
| **Time Limit** | 100 giây |
| **Điểm/câu** | +10 đúng, +0 sai |
| **Players** | 2 (split-screen) |
| **Master Data** | `add_up_master.csv` (5 câu hỏi) |

**Gameplay Flow:**

```
+──────────────────────────────────────────────+
|              ADDUP GAMEPLAY                   |
+──────────────────────────────────────────────+
|                                               |
|   TARGET: 7                                   |
|   ┌─────────────────────────────┐            |
|   │  Hình ảnh minh họa (7 toa)  │            |
|   └─────────────────────────────┘            |
|                                               |
|   Chọn 2 số có tổng = 7:                     |
|                                               |
|   [  3  ]    [  4  ]    [  5  ]              |
|    Option A   Option B   Option C             |
|                                               |
|   Player chọn A + B → 3 + 4 = 7 ✅           |
|   → +10 điểm, border xanh, icon ✓            |
|   → Đợi 1.5s → Câu tiếp theo                 |
|                                               |
|   Player chọn A + C → 3 + 5 = 8 ❌           |
|   → +0 điểm, border tím, icon ✗              |
|   → Đợi 1.5s → Câu tiếp theo                 |
+──────────────────────────────────────────────+
```

**Split-Screen Layout:**

```
+========================+========================+
|     PLAYER 1 (LEFT)    |    PLAYER 2 (RIGHT)    |
+========================+========================+
|                        |                         |
|   Target: 7            |   Target: 9             |
|   🚂🚂🚂🚂🚂🚂🚂     |   🪙🪙🪙🪙🪙🪙🪙🪙🪙  |
|                        |                         |
|   [ 3 ] [ 4 ] [ 5 ]   |   [ 4 ] [ 5 ] [ 6 ]   |
|                        |                         |
|   Score: 30            |   Score: 20             |
|                        |                         |
+========================+========================+
|              Timer: 72s remaining               |
+=================================================+
```

### 4.2 TrainPath Game (Nhận Dạng Hướng)

| Field | Value |
|-------|-------|
| **Mô tả** | Xem pattern đường ray, chọn hướng đi đúng |
| **Rounds** | 20 |
| **Time Limit** | 120 giây |
| **Điểm/câu** | +10 đúng, +0 sai |
| **Players** | 2 (split-screen) |
| **Master Data** | `train_path_master.csv` (6 câu hỏi) |

**Gameplay Flow:**

```
+──────────────────────────────────────────────+
|            TRAINPATH GAMEPLAY                 |
+──────────────────────────────────────────────+
|                                               |
|   Question: ?                                 |
|   ┌─────────────────────────────┐            |
|   │  Hình đường ray (pattern)   │            |
|   │  train_path_1.png           │            |
|   └─────────────────────────────┘            |
|                                               |
|   Chọn hướng đúng:                            |
|                                               |
|   [ ← Left ]  [ ↑ Straight ]  [ → Right ]   |
|                                               |
|   Player chọn Straight ✅                     |
|   → +10 điểm, border xanh, icon ✓            |
|   → Đợi 1.5s → Câu tiếp theo                 |
+──────────────────────────────────────────────+
```

### 4.3 So Sánh 2 Games (MECE)

| Dimension | AddUp | TrainPath |
|-----------|-------|-----------|
| **Input type** | Chọn 2 options (toggle) | Chọn 1 option (single) |
| **Skill** | Phép cộng | Nhận dạng pattern |
| **Visual** | Số + hình minh họa | Hình đường ray + mũi tên |
| **Rounds** | 10 | 20 |
| **Time** | 100s | 120s |
| **Difficulty** | Số học cơ bản | Tư duy logic |
| **Data fields** | target, 3 options, 2 corrects | image, 3 directions, 1 correct |
| **Feedback color (sai)** | Tím (Purple) | Đỏ (Red) |

---

## 5. Master Data Pipeline

### 5.1 Data Flow Diagram

```
+──────────────────────────────────────────────────────────────+
|                  MASTER DATA PIPELINE                         |
+──────────────────────────────────────────────────────────────+
|                                                               |
|  STEP 1: SOURCE                                               |
|  ┌──────────────────────────┐                                 |
|  │ Assets/MasterData/Csv/   │                                 |
|  │ ├── add_up_master.csv    │  (5 questions)                  |
|  │ └── train_path_master.csv│  (6 questions)                  |
|  └────────────┬─────────────┘                                 |
|               │                                               |
|  STEP 2: PARSE & CACHE                                        |
|               ▼                                               |
|  ┌──────────────────────────┐                                 |
|  │ LoadCsvDataForEditor     │  Singleton                      |
|  │ ├── Editor: File.Read()  │                                 |
|  │ ├── Android: WWW async   │                                 |
|  │ └── iOS: File.Read()     │                                 |
|  └────────────┬─────────────┘                                 |
|               │                                               |
|  STEP 3: SCRIPTABLE OBJECTS                                   |
|               ▼                                               |
|  ┌──────────────────────────┐                                 |
|  │ ScriptableObject Classes │                                 |
|  │ ├── AddUpMaster          │  (question_id, target, options) |
|  │ └── TrainPathMaster      │  (question_id, image, answer)   |
|  └────────────┬─────────────┘                                 |
|               │                                               |
|  STEP 4: RUNTIME CACHE                                        |
|               ▼                                               |
|  ┌──────────────────────────┐                                 |
|  │ MasterDataCache          │  Dictionary<string, SO>         |
|  │ GetCache<AddUpMaster>()  │                                 |
|  │ GetCache<TrainPathMaster>│                                 |
|  └────────────┬─────────────┘                                 |
|               │                                               |
|  STEP 5: GAME CONSUMPTION                                     |
|               ▼                                               |
|  ┌──────────────────────────┐                                 |
|  │ Game Models              │                                 |
|  │ ├── AddUpGameModel       │  Load & shuffle questions       |
|  │ └── TrainPathGameModel   │  Track per-player progress      |
|  └──────────────────────────┘                                 |
|                                                               |
+──────────────────────────────────────────────────────────────+
```

### 5.2 Platform Loading Differences

| Platform | Load Method | Source Path |
|----------|-------------|-------------|
| **Editor** | `File.ReadAllText()` | `Assets/MasterData/Csv/` |
| **Android** | `WWW` coroutine (async) | `jar:file://...!/assets/MasterData/Csv/` |
| **iOS** | `File.ReadAllText()` | `Application.streamingAssetsPath/MasterData/Csv/` |

### 5.3 Startup Sequence

```
StartMainController.Awake()
    │
    ├── (1) Create LoadCsvDataForEditor singleton
    │
    ├── (2) [Android only] PreloadAllCsvFiles()
    │       └── WWW download từ APK → cache vào memory
    │
    ├── (3) LoadMasterData("AddUpMaster")
    │       └── Parse CSV → Create ScriptableObject → Cache
    │
    ├── (4) LoadMasterData("TrainPathMaster")
    │       └── Parse CSV → Create ScriptableObject → Cache
    │
    ├── (5) Verify cache (retry 10 lần, delay 0.5s)
    │
    └── (6) SceneManager.LoadScene("MenuScene")
```

---

## 6. Cấu Trúc Thư Mục

```
edu-game-01/
├── Assets/
│   ├── Game/
│   │   ├── Scripts/                          # Source code
│   │   │   ├── UIScripts/
│   │   │   │   ├── StartScreen/              # Startup MVC
│   │   │   │   │   └── Controllers/
│   │   │   │   │       └── StartMainController.cs
│   │   │   │   ├── MenuScene/                # Menu MVC
│   │   │   │   │   ├── Controller/
│   │   │   │   │   ├── View/
│   │   │   │   │   └── State/
│   │   │   │   ├── AddUpGame/                # Game 1 MVC
│   │   │   │   │   ├── Controllers/
│   │   │   │   │   ├── Models/
│   │   │   │   │   ├── Views/
│   │   │   │   │   └── State/
│   │   │   │   ├── TrainPathGame/            # Game 2 MVC
│   │   │   │   │   ├── Controllers/
│   │   │   │   │   ├── Models/
│   │   │   │   │   ├── Views/
│   │   │   │   │   └── State/
│   │   │   │   └── Common/
│   │   │   │       └── Singleton.cs          # Base singleton
│   │   │   ├── Library/
│   │   │   │   └── CustomFSM/
│   │   │   │       └── CustomFSMManager.cs   # State machine
│   │   │   ├── Util/
│   │   │   │   └── LoadCsvDataForEditor.cs   # CSV loader
│   │   │   └── Editor/                       # Scene builder tools
│   │   ├── Scenes/                           # Unity scenes
│   │   │   ├── StartScreen/StartScene.unity
│   │   │   ├── MenuScene/MenuScene.unity
│   │   │   ├── AddUpGame/AddUpGame.unity
│   │   │   └── TrainPathGame/TrainPathGame.unity
│   │   ├── Prefabs/                          # Reusable UI prefabs
│   │   └── Resources/                        # Dynamic-load assets
│   │       └── ui/
│   │           ├── addup/                    # AddUp images
│   │           └── trainpath/                # TrainPath images
│   ├── MasterData/
│   │   ├── Csv/                              # Source CSV
│   │   │   ├── add_up_master.csv
│   │   │   └── train_path_master.csv
│   │   ├── CsvClass/                         # ScriptableObject defs
│   │   │   ├── AddUpMaster.cs
│   │   │   ├── TrainPathMaster.cs
│   │   │   └── MasterDataCache.cs
│   │   └── Data/                             # Generated SOs
│   ├── StreamingAssets/                       # Mobile build assets
│   │   ├── MasterData/Csv/
│   │   ├── Android/
│   │   └── iOS/
│   └── TextMesh Pro/                         # UI text
├── Packages/manifest.json                    # Dependencies
├── ProjectSettings/                          # Unity settings
├── docs/                                     # Documentation
├── CLAUDE.md                                 # AI assistant rules
└── CLAUDE-RULES.md                           # Analysis framework
```

---

## 7. Dependencies & Build

### 7.1 Key Dependencies

| Package | Version | Mục đích |
|---------|---------|----------|
| **Unity Ads** | 4.4.2 | Quảng cáo in-game |
| **Unity Purchasing** | 4.11.0 | In-app purchase |
| **TextMeshPro** | 3.0.7 | UI text rendering |
| **Google Mobile Ads** | Latest | Google AdMob |
| **Svelto ECS** | 3.x | Entity Component System |
| **DOTween** | - | Animation framework |
| **SoundManagerPro** | - | Audio management |

### 7.2 Build Configuration

| Setting | Android | iOS |
|---------|---------|-----|
| **Scripting Backend** | IL2CPP | IL2CPP |
| **Target API** | 33+ | Standard |
| **Code Stripping** | Tắt khi debug | Tắt khi debug |
| **Orientation** | Landscape | Landscape |
| **Resolution** | 1024x600 | 1024x600 |

### 7.3 Build Lưu Ý Quan Trọng

```
⚠ CRITICAL BUILD NOTES:
┌──────────────────────────────────────────────────┐
│ 1. Target SDK = 33+ (default 0 gây lỗi)         │
│ 2. Code stripping → crash → tắt để debug         │
│ 3. FSM dùng reflection → cần IL2CPP preserve     │
│ 4. Google Ads phải init trước khi dùng            │
│ 5. CSV files → copy vào StreamingAssets           │
│ 6. Debug: adb logcat + Development Build          │
└──────────────────────────────────────────────────┘
```

---

## 8. Mở Rộng & Roadmap

### 8.1 Thêm Game Mới (Pattern)

Để thêm Game 3/4/5, follow pattern có sẵn:

```
Bước 1: Tạo thư mục MVC
  Assets/Game/Scripts/UIScripts/<NewGame>/
  ├── Controllers/<NewGame>Controller.cs
  ├── Models/<NewGame>Model.cs
  ├── Views/<NewGame>View.cs
  └── State/<NewGame>SceneState.cs

Bước 2: Tạo Master Data
  Assets/MasterData/Csv/<new_game>_master.csv
  Assets/MasterData/CsvClass/<NewGame>Master.cs

Bước 3: Tạo Scene
  Assets/Game/Scenes/<NewGame>/<NewGame>.unity

Bước 4: Register
  - Thêm vào StartMainController masterDataGroup
  - Thêm LoadMasterData method
  - Update MenuScene button handler
  - Thêm scene vào Build Settings
```

### 8.2 Hệ Thống hiện tại — Feedback Loops (Systems Thinking)

```
+───────────────────────────────────────────────────+
|            SYSTEM FEEDBACK LOOPS                   |
+───────────────────────────────────────────────────+
|                                                    |
|  REINFORCING LOOP (Positive):                      |
|  Player đúng → +10 điểm → Motivation ↑            |
|  → Chơi nhanh hơn → Nhiều câu hơn → Điểm cao     |
|                                                    |
|  BALANCING LOOP (Negative):                        |
|  Timer giảm → Áp lực tăng → Sai nhiều hơn         |
|  → Điểm thấp → Cân bằng difficulty                 |
|                                                    |
|  COMPETITIVE LOOP:                                 |
|  Player 1 điểm cao → Player 2 cố gắng hơn         |
|  → Cả 2 improve → Learning outcome tốt hơn         |
+───────────────────────────────────────────────────+
```

---

## 9. Tham Khảo

### Files Quan Trọng

| File | Đường dẫn | Vai trò |
|------|-----------|---------|
| CustomFSMManager | `Assets/Game/Scripts/Library/CustomFSM/CustomFSMManager.cs` | State machine engine |
| Singleton | `Assets/Game/Scripts/UIScripts/Common/Singleton.cs` | Base singleton pattern |
| StartMainController | `Assets/Game/Scripts/UIScripts/StartScreen/Controllers/StartMainController.cs` | Khởi tạo app |
| LoadCsvDataForEditor | `Assets/Game/Scripts/Util/LoadCsvDataForEditor.cs` | CSV parser & loader |
| MasterDataCache | `Assets/MasterData/CsvClass/MasterDataCache.cs` | Runtime data store |
| AddUpGameController | `Assets/Game/Scripts/UIScripts/AddUpGame/Controllers/AddUpGameController.cs` | AddUp game logic |
| TrainPathGameController | `Assets/Game/Scripts/UIScripts/TrainPathGame/Controllers/TrainPathGameController.cs` | TrainPath game logic |

### Documents Liên Quan

| Document | Mục đích |
|----------|----------|
| `CLAUDE.md` | Project guidelines cho AI assistant |
| `CLAUDE-RULES.md` | Analysis framework & rules |

---

## Session Summary

1. **What was changed?** Tạo mới tài liệu tổng quan project
2. **Files modified**: `docs/SPEC-v1-edugame-overview.md` (new)
3. **Approach applied**: UC1 (Visual Information) + UC2 (Overview Research) — MECE structure, Pyramid Principle (Key Findings first), ASCII diagrams, Systems Thinking (feedback loops)
4. **Final report**: Tài liệu bao gồm 9 sections MECE: Overview, Architecture, Scene Flow, Games Detail, Data Pipeline, Directory Structure, Dependencies, Extensibility, References. Sử dụng 8+ ASCII diagrams để trực quan hóa.
