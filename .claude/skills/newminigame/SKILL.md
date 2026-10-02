---
name: newminigame
description: Biến 1 ý tưởng mini-game sơ bộ (floor-projector, 2 đội) thành scene Unity chạy được, qua quy trình mô tả → hỏi lại → 3 đề xuất → duyệt → triển khai bằng MiniGame Kit. Dùng khi user muốn tạo mini-game mới cho eduXploreGame2.0.
---

# /newminigame — Ý tưởng → mini-game chạy được

Quy trình 5 bước để tạo nhanh 1 mini-game mới cho `eduXploreGame2.0` (Unity, floor-projector,
2 đội Left/Right), dựa trên **MiniGame Kit** đã có sẵn tại
`Assets/Game/Scripts/Core/MiniGameKit/` (đọc `Assets/Game/Scripts/Core/MiniGameKit/README.md`
trước khi bắt đầu nếu chưa nắm rõ Kit).

Nguyên tắc xuyên suốt: **tái dùng tối đa, chỉ code phần thật sự mới**. 4 lớp đã có sẵn và
KHÔNG được viết lại: CSV question source, `IAnswerDisplay` (ButtonDisplay/FloatingDisplay/
MatchingDisplay), `AnswerValidator`/`MatchingValidator`, `ScoreManager`+`GameHUD`. Phần mới chỉ
nằm ở: (a) 1 controller kế thừa `MiniGameControllerBase`, (b) nếu ý tưởng cần cơ chế hoàn toàn
khác chọn-nút/matching thì thêm 1 `IAnswerDisplay` tự viết, (c) 1 Editor SceneBuilder, (d) CSV
nội dung.

## Bước 1 — Nhận mô tả sơ bộ

Lấy mô tả ý tưởng từ user (arg của lệnh, hoặc hỏi trực tiếp nếu chạy `/newminigame` không kèm mô
tả). Không cần làm gì thêm ở bước này — chỉ ghi nhận.

## Bước 2 — Hỏi lại

Dùng `AskUserQuestion`, hỏi đúng những trục ảnh hưởng tới việc chọn layer nào tái dùng được. Bỏ
qua câu nào đã rõ từ mô tả ở Bước 1 — mục tiêu là đủ thông tin để soạn 3 đề xuất ở Bước 3, không
phải hỏi cho đủ thủ tục. **Luôn đưa ra sẵn các lựa chọn thường dùng** (rút ra từ HaiQua/DemQua và
các game Kit khác) thay vì hỏi mở — user chọn nhanh hơn nhiều so với tự nghĩ câu trả lời.

### Trục cố định (hỏi mọi game mới)

1. **Kiểu chọn đáp án** gần nhất với ý tưởng: Single / MultiSelect (không thứ tự) /
   OrderedSequence (đúng thứ tự) / Matching (nối cặp) / "Cơ chế hoàn toàn khác" (không phải
   chọn nút — vd hứng vật rơi, bước qua ô, kéo-thả liên tục).
2. **Nội dung câu hỏi**: chữ / hình / âm thanh / ghép icon (IconCompose).
3. **Chế độ 2 đội**: gộp (2 đội cùng 1 câu hỏi, ai đúng trước ghi điểm — mặc định, Kit hỗ trợ
   sẵn, RẺ) hay độc lập (mỗi đội nhịp câu hỏi riêng — bắt buộc override
   `SetupIndependentDisplay`, tự quản lý state riêng cho từng bên — ĐẮT hơn hẳn, nói rõ với user
   đây là phần ngoài phạm vi mặc định của Kit trước khi chọn).
4. **Nhịp độ**: tĩnh (chờ chọn, không giới hạn hoặc giới hạn thời gian đơn giản) hay có yếu tố
   chuyển động/thời gian thực (vật rơi, obstacle, đếm ngược gấp).
5. **Chủ đề nội dung** (chữ cái, số đếm, con vật, màu sắc, ...) — để sinh CSV mẫu đúng ngữ cảnh.

### Trục bổ sung (hỏi khi liên quan — đây là các quyết định rút ra từ thực tế build HaiQua/DemQua,
KHÔNG hiển nhiên nếu không hỏi trước, dễ phải sửa lại nhiều lần nếu bỏ qua)

6. **Phản hồi hình ảnh khi chạm đúng/sai** — mặc định Kit tô xanh/đỏ (`ApplyFinalState`) + icon
   ✔/✖, hợp cho quiz có đáp án SAI thật sự. Nếu ý tưởng là "mọi lựa chọn đều đúng" kiểu gom/hái đồ
   (không có đáp án sai) thì màu xanh/icon that KHÔNG hợp bối cảnh — cần tự ẩn nút NGAY lúc chạm
   (trong hook chạm, trước khi Kit kịp tô màu) để né hẳn hiệu ứng đó, xem HaiQuaController/
   DemQuaController.OnFruitTapped.
7. **Âm thanh phản hồi**: (a) mặc định Kit là đủ — `PlayCorrectSfx()/PlayWrongSfx()` tự động khi
   round kết thúc (chỉ Combined mode); (b) cần âm thanh MỖI LẦN chạm đúng 1 phần trong round (round
   có nhiều lượt chạm, không chỉ 1 lượt — vd gom nhiều vật) → tự gọi
   `MusicManager.Instance?.PlayCorrectSfx()` thủ công (Independent mode KHÔNG tự phát gì); (c) cần
   GHÉP nhiều clip audio thành 1 câu (vd "số + tên vật") → dùng pattern hàng đợi phát lần lượt giữa
   2 bên để không chồng tiếng, xem `DemQuaController.AnnounceCount`/`_audioQueue`.
8. **Đếm ngược đầu round ("Start/Next in Ns")**: (a) mặc định Kit — chỉ có "Start in Ns" 1 LẦN lúc
   vào game (`InitialStartCountdownThenBegin`), không lặp lại mỗi round; (b) cần đếm ngược "Next in
   Ns" MỖI ROUND — Combined mode đã bật sẵn (`UseDefaultTransitionCountdown`), Independent mode cần
   bật thêm `useIndependentRoundCountdown` (mặc định tắt) VÀ gán `leftCountdownText/
   rightCountdownText` ở SceneBuilder (bật cờ mà quên gán Text = khoảng dừng vô hình mỗi round,
   bug thật đã gặp); (c) cần chờ vùng trả lời THẬT SỰ sạch (không còn ai đứng/chạm) trước khi đếm,
   không chỉ chờ hết thời gian feedback cố định → bật `waitForZoneClearBeforeCountdown` (hiện chỉ
   có ở Combined mode).
9. **Cách tính điểm**: (a) mặc định Kit — +1 điểm/round đúng, tự động; (b) +N điểm/round tuỳ số
   lượng item thu thập được TRONG round đó (không phải cố định +1 dù hái được mấy quả) → override
   `OnRoundResult`, cộng thêm phần chênh lệch qua `ScoreManager.AddPoints(team, N-1)` (Kit đã tự
   +1 sẵn), xem `DemQuaController.OnRoundResult`; (c) hệ thống thưởng nâng cao (đặt cược/hope
   star, jackpot nhân đôi, hộp quà bí ẩn, vòng quay may mắn) → dùng sẵn `Reward/` (xem README.md
   của Kit), không tự viết lại.

Nếu game dùng ảnh/âm thanh THẬT (không phải placeholder màu) ngay từ đầu, xem thêm ghi chú "Ảnh/âm
thanh thật" ở Bước 4 TRƯỚC khi hỏi Bước 2 — có thể ảnh hưởng câu trả lời trục 6/7.

## Bước 3 — Tổng hợp 3 đề xuất

Với câu trả lời ở Bước 2, soạn đúng 3 phương án khả thi, mỗi phương án nêu:

- **Tên ngắn gọn**.
- **Core loop** (2-3 câu): điều gì diễn ra trong 1 vòng chơi.
- **Tái dùng gì nguyên xi**: display nào (ButtonDisplay/FloatingDisplay/MatchingDisplay), kiểu
  chọn nào (AnswerValidator mode nào), có dùng `MiniGameControllerBase` trực tiếp không.
  Càng tái dùng nhiều, size ước lượng càng nhỏ.
- **Phần thật sự mới**: liệt kê cụ thể (vd "1 IAnswerDisplay mới cho hứng vật rơi", "logic
  combo trong OnRoundResult"). Nếu không có gì mới ngoài nội dung CSV → nói rõ (phương án rẻ
  nhất, gần như chỉ đổi content).
- **Ước lượng**: S (chỉ CSV + wiring, dùng thẳng display có sẵn) / M (thêm 1 display hoặc hook
  logic vừa phải) / L (cơ chế chuyển động/thời gian thực, nhiều state mới).

Trình bày 3 phương án qua **1 lần gọi `AskUserQuestion`** (1 câu hỏi, 3 option, mỗi option có
description tóm tắt core loop + ước lượng) để user chọn trực tiếp — không hỏi rời kiểu "plan ổn
không". Luôn có ít nhất 1 phương án size S (an toàn, chắc chắn dựng nhanh được) trong 3 đề xuất.

## Bước 4 — Triển khai đề xuất được chọn (làm thật nhanh)

Thứ tự thao tác:

1. Đặt tên game (PascalCase, không dấu, khớp quy ước hiện có: `AddUpGame`, `RiverCrossGame`...).
2. Copy `Assets/Game/Scripts/Core/MiniGameKit/_Template/TemplateMiniGameController.cs` →
   `Assets/Game/Scripts/UIScripts/<TenGame>/Controllers/<TenGame>Controller.cs` (theo đúng cấu
   trúc thư mục `UIScripts/<TenGame>/Controllers|Models|Views` như các game hiện có), đổi tên
   class, implement các `// TODO(idea)` theo đúng đề xuất đã chọn:
   - Nếu chọn display có sẵn (Button/Floating/Matching) → chỉ đổi field + `GetDisplayForQuestion`.
   - Nếu chọn "cơ chế hoàn toàn khác" → viết 1 class mới `implements IAnswerDisplay` (xem hợp
     đồng trong README.md của Kit), đặt cùng thư mục `Views/`.
3. Copy `Assets/Game/Scripts/Editor/MiniGameKit/TemplateMiniGameSceneBuilder.cs` →
   `Assets/Game/Scripts/Editor/<TenGame>SceneBuilder.cs`, đổi tên/path, dùng lại
   `MiniGameSceneBuilderHelpers` (Canvas, GameHUD, ButtonItem group, TutorialPanel, ...) — chỉ
   thêm phần hiển thị riêng của ý tưởng nếu có display mới. Đặt `[MenuItem("Tools/<TenGame>/Build
   Scene")]`. **Nhớ gọi `MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath)`**
   (Template không gọi vì chỉ là ví dụ — game thật thì cần, để load `ScoreScene`/`MenuScene`
   quay lại được).

   **Ảnh/âm thanh THẬT (không phải placeholder màu)** — nếu ý tưởng dùng ảnh chụp/PNG thật ngay từ
   đầu (không chờ "chạy đúng logic rồi mới thay art" như quy ước cũ):
   - Dùng `CreateButtonGridTwoRows` thay vì `CreateButtonGroupHorizontal` (button vuông pixel cố
     định, dễ bấm, xem README.md của Kit).
   - Gọi `MakeButtonGroupBackgroundTransparent(group)` ngay sau khi tạo group — nếu không, màu
     nền "thẻ bài" trắng đục mặc định của `ButtonItem` sẽ lộ khung xấu phía sau ảnh PNG nền trong
     suốt.
   - Đặt file ảnh/audio theo THƯ MỤC NỘI DUNG dùng chung (`Resources/TestTongHop/images/<loại>/`,
     `Resources/TestTongHop/audio/<loại>/`), KHÔNG đặt theo tên game riêng — game khác có nội dung
     tương tự (vd cùng bộ quả/con vật) tái dùng được luôn, không phải xin lại ảnh.
   - Nếu ảnh/audio KHÔNG đi qua CSV/QuestionData (ảnh nền phẳng theo màn hình, audio ghép số+tên...)
     — load bằng `Resources.Load<T>()` TRỰC TIẾP, KHÔNG qua `AssetOverrideLoader.GetSprite/GetClip`
     (tự cộng nhầm prefix nếu path không đúng quy ước `TestTongHop/...`, xem gotcha trong
     `AssetOverrideLoader.cs`).
   - Layout cần tinh chỉnh nhiều lần (vị trí/khoảng cách/kích thước) → đưa ra field
     `[SerializeField]` + `OnValidate()` trên CONTROLLER thay vì hard-code số trong SceneBuilder,
     xem `DemQuaController.ApplyBasketFruitLayout()` — tránh phải Build Scene lại mỗi lần chỉnh 1
     con số.
4. Sinh CSV nội dung 5-10 câu, đúng chủ đề đã chọn ở Bước 2, đặt tại
   `Assets/Game/Resources/MiniGameKit/<TenGame>/choose.csv` (và `matching.csv` nếu dùng Matching)
   — đúng format ghi trong `CsvQuestionLoader.cs`. **Đặt trong `Resources/` là đủ** — KHÔNG cần
   bước "sync CSV → StreamingAssets" (đó là quy tắc riêng của `Assets/MasterData/Csv/`, không áp
   dụng cho Kit).
5. Thêm 1 dòng vào `Assets/Game/Scripts/UIScripts/Common/GameRegistry.cs`:
   `Set(<category>, <index_trống_tiếp_theo>, "<TenGame>", "<TenScene>", Engine.<NhómPhùHợp>);`
   — đọc file trước để chọn category/index trống hợp lý, không ghi đè entry đang dùng.

Không tự ý mở rộng phạm vi ngoài đề xuất đã duyệt — nếu phát hiện cần thêm gì đó không nằm trong
3 phương án ban đầu, hỏi lại user trước khi làm.

## Bước 5 — Bàn giao

Không tự chạy được Unity Editor trong môi trường này. Báo cho user chính xác:

1. Mở Unity Editor, project `eduXploreGame2.0`.
2. Menu **Tools → `<TenGame>` → Build Scene** (đã tạo ở Bước 4.3) — dựng scene tự động.
3. Mở scene vừa tạo (đường dẫn đã nêu), bấm **Play** để chơi thử.
4. Nếu ổn, quay lại **MenuScene** sẽ thấy game mới trong danh sách (nhờ đã đăng ký
   `GameRegistry` ở Bước 4.5).

Dừng đúng ở "sẵn sàng Play" — không khẳng định đã tự chạy/tự test được.
