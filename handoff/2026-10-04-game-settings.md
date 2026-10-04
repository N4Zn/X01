# Handoff — "Cài đặt game" chuyển từ Unity MenuScene sang ControlActivity (2026-10-03)

Yêu cầu gốc của user: đưa setting panel (trước ở MenuScene/Unity) vào `ControlActivity`. Phần KHUNG
đã làm xong (Unity); phần chi tiết (UI Android + gỡ timeout + chờ clear game cũ) bàn giao cho
Android Studio agent, user sẽ review lại.

## 5 tham số chốt

| Tham số | Kiểu / khoảng | Mặc định | Ghi chú |
|---|---|---|---|
| Thời gian chơi | giây, int | 100 | = `GameSettings.GameTime` (giữ nguyên cơ chế) |
| Thời gian chờ chuyển câu | giây, 1–4 | 2 | = `RoundEndDelay` (giữ nguyên cơ chế) |
| Tốc độ flow | hệ số **0.1–5x** | 1.0 | **UI = thanh trượt (giống thanh âm thanh) + ô nhập số ở cuối**, 2 chiều đồng bộ nhau |
| Âm lượng nhạc nền / hiệu ứng | 0–1 | 1 / 1 | giữ nguyên cơ chế (`MusicVolume`/`SfxVolume`) |
| Chờ clear mới chuyển round | tick, **mặc định BẬT** | bật | áp dụng TOÀN BỘ game, gồm cả Independent mode |

**Bỏ**: thời gian mỗi câu (`QuestionTimeout`) — giờ KHÔNG giới hạn, không còn trên UI.

## Đã làm (Unity)

- `GameSettings.cs`: thêm `FlowSpeed`, `WaitForClear` (+ PlayerPrefs), `ApplyJson(string)`; `QuestionTimeout`
  đánh dấu DEPRECATED (còn field để code cũ compile).
- `GameControlBridge.OnSettingsChanged(json)` — nhận từ Java qua `UnitySendMessage("GameControlBridge","OnSettingsChanged", json)`.
- `ControlBridge.Init()` — đọc Intent extra `com.eduxplore.control.SETTINGS_JSON` lúc cold-boot, áp trước khi load game.
- `MiniGameControllerBase.TransitionCountdown()` — chờ clear nếu `waitForZoneClearBeforeCountdown` HOẶC `GameSettings.WaitForClear` (Combined mode).
- `SpawnFlowDisplay.MoveAndMaybeDespawn` — nhân `speedPct` với `FlowSpeed` (GenericGame spawn flow).

**JSON contract** (field vắng hoặc âm = giữ nguyên; `waitForClear` -1 = không đổi, 0/1):
```json
{"musicVolume":0.8,"sfxVolume":1.0,"gameTime":100,"roundEndDelay":2.0,"flowSpeed":1.0,"waitForClear":1}
```

## Việc còn lại

### A. Android (`NativePlugins/ControlUiAndroidLib`) — làm hết
- A1. `ui/SettingsStore.java`: lưu JSON ở `/sdcard/EduXplore/game_settings.json` (fallback `getExternalFilesDir`), giống `ScoreStore`. Có default + `toJson()`.
- A2. UI "Cài đặt game" trong `ControlActivity` (build bằng code, dùng token màu `colors.xml`, nền sáng như các màn khác): 5 mục trên. Slider flow 0.1–5 (bước 0.1) + `EditText` số cuối dòng, nhập tay clamp 0.1–5.
- A3. Mỗi lần đổi giá trị: lưu + nếu Unity đã sống (`unityStarted`) gửi `UnitySendMessage("GameControlBridge","OnSettingsChanged", json)` (reflection như Pause/Resume đã có). Bấm Start lần đầu: đính JSON vào Intent extra `com.eduxplore.control.SETTINGS_JSON`.
- A4. Build lại `controlui-release.aar`, copy đè `Assets/Plugins/Android/controlui-release.aar`.

### B. Unity — gỡ giới hạn thời gian mỗi câu
- B7. Gỡ `QuestionTimeout`: các controller cũ đọc `GameSettings.QuestionTimeout` → `StartQuestionTimeout`/coroutine timeout. Đổi thành không giới hạn (không start coroutine; kiểm tra UI đếm ngược mỗi câu nếu có). Danh sách ở bảng dưới (cột QT). `AddNumberGameController` đã comment tắt từ trước.
- B8. Cuối cùng: xoá `QuestionTimeout` khỏi `GameSettings` + `MenuScene/HomeScene` setting panel (hoặc để nguyên, user quyết).

### C. Unity — chờ clear cho game cũ (KHÔNG kế thừa `MiniGameControllerBase`)
- C8b. **Independent mode ở Base**: `IndependentRoundCountdown(team)` có TODO — khi `WaitForClear` bật, chờ RIÊNG nửa màn hình của `team` sạch (`FloorZoneClearer.Await(halfRect, cb)`, KHÔNG dùng `AwaitBothSides`) trước khi đếm. Cần `RectTransform` nửa trái/phải (xem `GameHUD`). Lưu ý: hiện đếm này chỉ chạy khi `useIndependentRoundCountdown = true`; với `WaitForClear` thì chờ clear cả khi cờ đó tắt → xem lại luồng `IndependentLoop`, chèn chờ clear TRƯỚC `QuestionData q = PullNextQuestion()` từ round 2 trở đi.
- C9. Game cũ: trước khi sang câu/round kế, nếu `GameSettings.Instance.WaitForClear` thì `FloorZoneClearer.AwaitBothSides(canvasRoot, cb)` rồi mới chạy delay `RoundEndDelay`. Mẫu chuẩn: `TestTongHopController` (+ `AnswerDisplayManager.ZoneCleared`, tức SolarQuiz đã làm). Nên viết 1 helper dùng chung (vd `FloorZoneClearer.AwaitIfEnabled(root, cb)` gọi cb ngay nếu setting tắt) để khỏi lặp 17 lần. **Chỉ làm game KHÔNG nằm trong blacklist** (user chọn bên dưới).

### D. Tốc độ flow các game ngoài GenericGame
- `BalloonGameController`/`BalloonGameConfig` (`speedBase`), `LaneDash` (`baseSpeed`, `spawnInterval`): nhân hệ số `GameSettings.FlowSpeed`. Chưa grep hết game spawn liên tục khác — rà thêm.
- Quyết định mở: `SpawnFlowDisplay` hiện CHỈ nhân tốc độ trôi (giữ nguyên `spawnIntervalSec`). Muốn "nhanh hơn thì dày hơn" thì chia `_spawnIntervalSec / FlowSpeed`.

### E. Cuối
- Cập nhật `CLAUDE.md` (mục Track A), test trên K02: đổi từng tham số khi đang chơi, cold-boot, Start lần 2.

## BLACKLIST — ĐÓNG BĂNG, KHÔNG ĐỤNG (user chốt 2026-10-04, chờ user yêu cầu mở lại)

AddUp, NumberAddUp, PathFinder, TrainPath, Monopoly, BalloonGame (SoDem2/ChuCai2), RiverCross: đã comment/gỡ
khỏi game registry (`GameRegistry.cs` comment `DISABLED`, `game_registry.json` xoá dòng). **Bỏ qua hoàn toàn ở
B7/C9/D** — không sửa controller/scene/config của các game này. Muốn mở lại: bỏ `// DISABLED ...` ở C# + thêm lại
dòng JSON (`{"name":"PathFinder","displayName":"Tìm đường","sceneName":"PathFinderGame","category":5}`, Monopoly
`MonopolyGame` cat 5, TrainPath `TrainPathGame` cat 6, RiverCross `RiverCrossGame` cat 7, SoDem2 `BalloonGame` cat 0
group "Đếm", ChuCai2 `BalloonGame` cat 1) rồi build lại aar. AddUp/NumberAddUp vốn chưa đăng ký registry.

## Game cũ (không kế thừa Base) — bảng tham khảo

QT = số chỗ dùng `QuestionTimeout`; DELAY = số chỗ dùng `RoundEndDelay`. Registry name → scene → controller:

| Game (registry) | Scene / Controller | QT | DELAY |
|---|---|---|---|
| AddNumber, AddNumber5 | AddNumberGame / AddNumberGameController | 9 (đã comment tắt) | 3 |
| (AddUp) | AddUpGame / AddUpGameController | 8 | 3 |
| (NumberAddUp) | NumberAddUpGame / NumberAddUpGameController | 8 | 3 |
| SoDem | SoDemGame / SoDemController | 0 | 3 |
| Numbers | NumbersGame / NumbersController | 0 | 3 |
| ChuCai | ChuCaiGame / ChuCaiController | 0 | 3 |
| ListenSelect | ListenGame / ListenSelectController | 0 | 3 |
| SoDem2, ChuCai2 | BalloonGame / BalloonGameController (+ flow D) | 0 | 1 |
| PathFinder | PathFinderGame / PathFinderGameController | 10 | 3 |
| PlanetOrder | PlanetOrderGame / PlanetOrderController | 9 | 4 |
| PlanetAlphabet | PlanetAlphabetGame / PlanetAlphabetController | 9 | 4 |
| TrainPath | TrainPathGame / TrainPathGameController | 10 | 3 |
| TongHop | TongHopGame / TongHopGameController | 9 | 3 |
| Counting, Counting5, Fruit, Animal, WaterAnimal, Things, SaveEnvironment, SolarQuizEn/Vi, SolarOrder(2), TestTongHop | TestTongHopGame / TestTongHopController | 0 | 1 — **đã có chờ clear (SolarQuiz)**, chỉ cần nối `WaitForClear` |
| RiverCross | RiverCrossGame / RiverCrossController | 0 | 0 (chỉ dùng GameTime) |
| LaneDash | LaneDashGame / LaneDashController | 0 | 0 (arcade; flow D) |
| Monopoly | MonopolyGame / MonopolyGameController | 2 | 0 |
| SaveTheAstronaut | SaveTheAstronautGame / SaveTheAstronautController | 0 | 0 |

Game đã kế thừa Base (được chờ clear tự động qua Base): GenericGame (mọi game import từ zip), DemQua, FamilyMember,
FamilySpelling, HaiQua, SentenceBuilder, WhoIsIt, WordHuntMaze.
