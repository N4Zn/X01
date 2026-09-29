# MiniGame Kit

Bộ khối dùng chung để dựng nhanh mini-game mới cho eduXploreGame2.0 (floor-projector, 2 đội
Left/Right), thay vì copy-paste FSM/scoring/CSV-loading như các game cũ (AddUpGame, TrainPathGame...).

Đây là bổ sung thuần (additive) — **không sửa** bất kỳ game/script nào đang có. Game cũ vẫn chạy
y nguyên.

## 4 lớp, ai làm gì

| Lớp | File | Vai trò |
|---|---|---|
| Câu hỏi (CSV) | `Questions/CsvQuestionLoader.cs`, `Questions/MiniGameQuestionSource.cs` | Parse CSV (cùng format với TestTongHopGame) → `QuestionData`, cấp phát theo difficulty |
| Hiển thị đáp án | *(tái dùng)* `IAnswerDisplay`, `ButtonDisplay`, `FloatingDisplay`, `MatchingDisplay` (`_TestTongHop/Display`) | Vẽ đáp án lên màn hình, trả kết quả qua callback |
| Kiểu chọn | *(tái dùng)* `AnswerValidator` (`_TestTongHop/Data`) — Single/MultiSelect/OrderedSequence. `Questions/MatchingValidator.cs` — chấm điểm matching thuần logic | Đúng/sai theo kiểu chọn |
| Điểm 2 đội | *(tái dùng)* `ScoreManager`, `GameHUD` (`UIScripts/Common`) | Điểm Left/Right, event `OnScoreChanged`, HUD 2 đội sẵn |
| **Vòng đời (mới)** | `MiniGameState.cs`, `MiniGameControllerBase.cs` | FSM chuẩn: Initialize → Tutorial → ShowQuestion → WaitAnswer → Feedback → (lặp) → GameOver |

`QuestionData`, `Team`, `AnswerMode`, `ClickResult`, ... là type **global** đã có sẵn trong
`GameTypes.cs` — Kit không định nghĩa lại, dùng thẳng để tương thích 100% với Display/Validator có sẵn.

## Cách dùng — game mới kế thừa `MiniGameControllerBase`

```csharp
public class MyGameController : MiniGameControllerBase
{
    [SerializeField] ButtonDisplay buttonDisplay; // hoặc display tự viết cho ý tưởng mới

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    // Tuỳ chọn — override khi ý tưởng mới cần thêm hiệu ứng/logic riêng:
    protected override void OnQuestionShown(QuestionData q) { }
    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer) { }
    protected override void OnPlayerFailed(Team team) { }
}
```

Base class tự lo: khởi tạo `CustomFSMManager`, wiring `ScoreManager`+`GameHUD`, lấy câu hỏi từ
`MiniGameQuestionSource`, coroutine feedback-delay/timeout, ghi điểm `GameSessionManager` rồi
load `ScoreScene`. Chỉ cần override hook cho phần thật sự mới.

**Chỉ bắt buộc 1 hook**: `GetDisplayForQuestion`. Còn lại có default rỗng.

**Không bắt buộc dùng base class này** — game có cơ chế liên tục/phi lượt (kéo-thả liên tục kiểu
RiverCrossGame) có thể bỏ qua, chỉ dùng `ScoreManager` + `GameHUD` trực tiếp.

**Feedback mặc định (âm thanh + icon đúng/sai, đếm ngược chuyển câu)** — chạy tự động cho MỌI
game kế thừa `MiniGameControllerBase`, không cần viết gì thêm:

- Trả lời đúng/sai → `MusicManager.PlayCorrectSfx()/PlayWrongSfx()` + icon ✔/✖ bounce
  (`FeedbackEffect`, cùng khối dùng lại từ AddUpGame/TestTongHopGame). Muốn có icon thật trên màn
  hình: gán 4 field `leftCorrectIcon/leftWrongIcon/rightCorrectIcon/rightWrongIcon` (kiểu
  `GameObject`) trong SceneBuilder — dựng bằng `FeedbackIconBuilder.Create(name, parent, isCorrect,
  anchorMin, anchorMax)` có sẵn. Để trống thì vẫn có âm thanh, chỉ không có icon.
- Chuyển câu → luôn có 1 khoảng chờ "Next in Ns" lấy từ **`GameSettings.Instance.RoundEndDelay`**
  (setting chung của app, 1-4s, không hard-code riêng từng game) — quan trọng cho game chiếu sàn:
  học sinh trả lời xong thường còn đứng nguyên tại chỗ, cần vài giây để biết mà di chuyển trước khi
  câu mới hiện ra. Muốn hiện số đếm ngược: gán `leftCountdownText/rightCountdownText` (kiểu `Text`).
  Để trống thì khoảng chờ vẫn chạy, chỉ không có chữ hiện ra.

Override `UseDefaultFeedbackFx`/`UseDefaultTransitionCountdown` thành `false` nếu game tự làm
phần này riêng (xem `WhoIsItGameController` — đã có feedback text/sfx/mystery-box riêng, tắt
`UseDefaultFeedbackFx` để tránh phát âm thanh 2 lần).

**3 chế độ chơi** (`playMode`, field trên `MiniGameControllerBase`) — xem `MiniGamePlayMode.cs`:

- `Combined` (mặc định) — 2 đội cùng 1 câu hỏi, ai đúng trước ghi điểm. Không cần code thêm.
- `Solo` — 1 luồng chơi, không thi đua. Kỹ thuật giống hệt Combined; chỉ khác ở chỗ SceneBuilder
  không tạo cột tên/điểm bên phải (GameHUD tự bỏ qua field null), và GameOver ghi cùng 1 điểm vào
  cả 2 slot `GameSessionManager`.
- `Independent` — 2 đội tự nhịp câu hỏi riêng, không chờ nhau. **Bắt buộc override**
  `SetupIndependentDisplay(Team, QuestionData, onDone)` ở subclass, gọi thẳng
  `display.SetupPlayerIndependent(team, q, onDone)` (method có sẵn trên `ButtonDisplay`/
  `FloatingDisplay`, không có trên `MatchingDisplay`). Base tự lo 2 vòng lặp riêng + timer/round
  chung.

## Cơ chế hoàn toàn mới (không phải chọn nút/matching)

Viết 1 class `implements IAnswerDisplay` mới — chỉ cần đúng hợp đồng:

```csharp
public interface IAnswerDisplay
{
    void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed);
    void HidePlayerAnswers(Team team);
    void Cleanup();
}
```

`onResult(correct, team, playerAnswer)` gọi khi round kết thúc — `MiniGameControllerBase` tự lo
phần còn lại (ghi điểm, feedback, next round).

## Dựng scene nhanh

`Assets/Game/Scripts/Editor/MiniGameKit/MiniGameSceneBuilderHelpers.cs` — primitives để viết
1 Editor script `[MenuItem]` dựng scene bằng code (theo đúng pattern `AddUpSceneBuilder.cs` /
`TestTongHopSceneBuilder.cs` đã có), UI placeholder màu phẳng (không cần asset nghệ thuật để
chạy thử). Xem `_Template/` bên dưới làm ví dụ đầy đủ.

**HUD mặc định dùng prefab thật**: `InstantiateGameHudPrefab(parent)` (không cần truyền path) tự
dùng `DefaultGameHudPrefabPath` = `Assets/Game/Prefabs/GameHUD.prefab` — tự fallback về
`CreateGameHud()` (placeholder) nếu không tìm thấy prefab hoặc prefab thiếu component `GameHUD`.
Muốn dùng HUD khác cho 1 game cụ thể: gọi overload `InstantiateGameHudPrefab(parent, path)`.

**Board vuông quanh viền (dạng cờ tỷ phú)**: `CreateBoardTilesPerimeter(parent, outerMin, outerMax,
tileSize, colors, labels, labelFontSize)` — dựng N ô vuông xếp đều quanh viền 1 hình chữ nhật (bắt
đầu góc trên-trái, chiều kim đồng hồ), trả về `RectTransform[]` theo đúng thứ tự index để game dùng
làm neo vị trí quân cờ. Không biết gì về ý nghĩa ô (loại ô/luật chơi) — màu/nhãn hoàn toàn do caller
quyết định. Xem `MonopolyGameSceneBuilder.cs` làm ví dụ đầy đủ (board 20 ô, 5 loại ô đặc biệt).

**Overlay phủ toàn màn hình (công bố kết quả)**: `CreateFullscreenOverlay(parent, name,
backgroundColor, fontSize, textColor)` — dựng 1 panel (0,0)-(1,1) + 1 dòng chữ to giữa màn hình,
ẩn mặc định. Dùng cho khoảnh khắc cần chiếm TRỌN màn hình, dễ thấy từ xa (đổ xúc xắc, quay số, đếm
ngược lớn...) — gọi hàm này SAU CÙNG trong code dựng scene để sibling cao nhất (vẽ đè lên mọi thứ,
kể cả HUD). Trả về `(GameObject root, Text bigText)` — game tự bật/tắt `root` và set `bigText.text`.
Xem `MonopolyGameSceneBuilder.cs` (overlay đổ xúc xắc) làm ví dụ.

## `_Template/` — ví dụ chạy được ngay

`_Template/TemplateMiniGameController.cs` + `Editor/MiniGameKit/TemplateMiniGameSceneBuilder.cs`
+ CSV mẫu (`Assets/Game/Resources/MiniGameKit/_Template/choose.csv`) — chứng minh Kit chạy
end-to-end. Trong Unity: **Tools → MiniGameKit → Build Template Scene**, mở scene vừa tạo
(`Assets/Game/Scenes/_MiniGameKitTemplate/MiniGameKitTemplate.unity`), bấm Play.

Đây cũng là khuôn mẫu cho quy trình `/newminigame` (xem `.claude/skills/newminigame/SKILL.md`):
copy `_Template`, đổi tên, code phần `// TODO(idea)`.

## Reward system (tuỳ chọn) — `Reward/`

3 class thuần C#, dùng chung được cho mọi game (không phụ thuộc WhoIsItGame):

- `HopeStarTracker` — mỗi đội có N lượt "đặt cược" trước khi câu hỏi hiện ra; đặt + đúng câu đó → x2 điểm.
- `JackpotTracker` — cả 2 đội sai → điểm câu tiếp theo x2 (dồn); ai đúng ăn trọn rồi reset về gốc.
- `MysteryRewardPool` — kho phần thưởng (bonus/penalty/chia sẻ điểm), rút ngẫu nhiên 3 cho "hộp quà".
- `LuckyWheelPool` — kho kết quả cho "vòng quay may mắn": quay ra ĐÚNG 1 kết quả ngẫu nhiên (khác
  `MysteryRewardPool` — không có bước "chọn 1 trong 3", chỉ "quay và nhận"). Xem `MonopolyGameController.LuckyWheelCoroutine`.

Thứ tự tính điểm đúng (xem `WhoIsItGameController.OnMysteryBoxClicked`): điểm gốc đã nhân jackpot
→ thưởng/phạt từ hộp quà (số điểm thưởng CŨNG nhân theo jackpot multiplier) → cuối cùng nhân x2
nếu có đặt sao hi vọng (áp dụng trên TỔNG, không áp dụng lên phần chia cho đối thủ).

Cách tích hợp vào 1 game mới: game tự sở hữu instance `HopeStarTracker`/`JackpotTracker`, và chèn
"bet phase" bằng cách override `StateMachineEnter_ShowQuestion` (chạy 1 coroutine hiện UI đặt cược
rồi gọi `base.StateMachineEnter_ShowQuestion(...)`), chèn "chọn hộp quà" bằng cách override
`StateMachineEnter_Feedback` tương tự (chờ người chơi bấm hộp rồi mới gọi `base....Feedback(...)`
để thực sự chuyển câu tiếp theo). Không cần sửa `MiniGameControllerBase`.

## Effects (tuỳ chọn) — `Effects/`

3 class thuần C#/static, tách từ code hiệu ứng lặp lại giữa các game (ban đầu viết tay trong
`WhoIsItGameController`) — dùng chung cho mọi game cần "hiệu ứng" tương tự:

- `PulseEffect.ScaleFadePulse(RectTransform, CanvasGroup, fromScale, toScale, duration)` —
  coroutine "to dần rồi mờ đi" tổng quát (star burst, coin pop, badge nhấn mạnh...). Gọi qua
  `StartCoroutine(PulseEffect.ScaleFadePulse(...))`, không cần viết coroutine riêng mỗi lần.
- `FeedbackTextStyle` — quy ước màu (xanh=được điểm, đỏ=mất/chia điểm) + hàm dựng rich-text
  (`Gain(n)`, `Loss(n)`, `Shared(n)`, `Bold()`, `Colored()`) dùng được với cả `UnityEngine.UI.Text`
  (bật `supportRichText`) và `TextMeshProUGUI`. Tránh mỗi game tự chế mã màu riêng rồi lệch nhau.
- `ValueScale.LogScale(value, baseValue, ...)` — tính hệ số phóng to theo log2 của bội số so với
  giá trị gốc, có trần trên — dùng cho UI "to dần theo điểm/giá trị" (reward badge, combo...) mà
  không vỡ layout dù giá trị tăng nhiều lần liên tiếp.
- `FillBlankEffect.Fill(template, answer)` — thay chỗ trống đầu tiên ("_") trong template bằng
  đáp án thật (vd `Fill("M_M", "O")` → `"MOM"`). Dùng cho câu hỏi dạng "từ có 1 chỗ thiếu"
  (spelling...) — trả lời đúng thì điền chữ vào ngay để học sinh thấy từ hoàn chỉnh, thay vì chỉ
  tô xanh nút bấm (xem `FamilySpellingGameController.OnRoundResult`).
- `CycleRevealEffect.Spin(target, randomValue, finalValue, duration, interval)` — coroutine "quay
  số rồi dừng lại đúng kết quả": đổi `target.text` ngẫu nhiên liên tục trong `duration` giây rồi
  dừng hẳn ở `finalValue` (đã biết trước). Dùng cho mọi khoảnh khắc random-reveal (đổ xúc xắc, quay
  thưởng...) mà không cần animation vật lý thật. Xem `MonopolyGameController.RollDiceCoroutine`/
  `LuckyWheelCoroutine`.

Đây là nơi bổ sung thêm các hiệu ứng dùng chung khác trong tương lai (xem gợi ý dưới) — mỗi hiệu
ứng nên là 1 hàm/class độc lập, không phụ thuộc game cụ thể nào.

### Hiệu ứng phổ biến khác — to-do list

Danh sách đầy đủ (kể cả hiệu ứng đã có sẵn rải rác trong project nhưng chưa nối vào Kit, và
hiệu ứng đặc thù máy chiếu sàn) → xem **[EFFECTS_TODO.md](EFFECTS_TODO.md)**.

## Đăng ký game mới vào menu

Sau khi scene chạy được, thêm 1 dòng vào `GameRegistry.cs`:

```csharp
Set(<category>, <index>, "TenGame", "TenScene", Engine.<NhómPhùHợp>);
```
