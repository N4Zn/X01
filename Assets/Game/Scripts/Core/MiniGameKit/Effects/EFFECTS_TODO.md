# To-do hiệu ứng cho MiniGame Kit

Danh sách hiệu ứng nên có cho mini-game nói chung, và đặc biệt cho bối cảnh **máy chiếu sàn**
(projector chiếu xuống sàn, học sinh dẫm/nhảy để chọn). Đánh dấu rõ cái nào project **đã có sẵn**
(chỉ cần nối vào Kit) để tránh làm lại, và cái nào **chưa có** cần xây mới.

Chú thích trạng thái:
- `[có sẵn]` — script đã tồn tại trong project, chỉ cần dùng/nối vào Kit.
- `[nền tảng]` — có 1 phần liên quan, cần mở rộng thêm.
- `[chưa có]` — chưa có gì, cần viết mới.

---

## 1. Phản hồi đúng/sai (answer feedback)

- [x] Icon đúng/sai (✔ xanh / ✖ đỏ) bounce + pulse/shake — `[có sẵn]`
      `Assets/Game/Scripts/UIScripts/Common/FeedbackEffect.cs` (`Play(isCorrect, duration, withFadeOut)`)
      + `Assets/Game/Scripts/Editor/FeedbackIconBuilder.cs` (dựng UI icon).
- [x] Scale + fade pulse tổng quát (star burst, coin pop...) — `[có sẵn — MiniGameKit]`
      `Effects/PulseEffect.cs`.
- [x] Điền chữ vào chỗ trống khi trả lời đúng (fill-blank) — `[có sẵn — MiniGameKit]`
      `Effects/FillBlankEffect.cs` — cho câu hỏi dạng "từ có 1 chỗ thiếu" (spelling...), trả lời
      đúng thì thay "_" bằng đáp án thật, học sinh thấy từ hoàn chỉnh ngay (xem
      `FamilySpellingGameController.OnRoundResult`). Dùng lại được cho mọi game "điền khuyết" khác.
- [ ] Nút nảy khi bấm (button bounce/press feedback) — `[chưa có]`
      Scale nhỏ rồi bật lại ngay khi 1 đáp án được tap — phản hồi tức thời rất quan trọng cho
      trẻ nhỏ (biết là đã bấm trúng, trước khi biết đúng/sai). Nên gắn trực tiếp vào `ButtonItem`
      (Kit dùng chung `ButtonItem` cho hầu hết game).
- [ ] Flash toàn màn hình khi có kết quả (chớp nhẹ xanh/đỏ) — `[chưa có]`
      Hiệu ứng mạnh, nhìn thấy rõ từ xa/từ trên xuống — **rất hợp máy chiếu sàn** vì học sinh
      không nhìn chăm chăm vào 1 điểm cố định như màn hình điện thoại.
- [ ] Rung nhẹ đáp án sai (shake) tách riêng khỏi `FeedbackEffect` — `[nền tảng]`
      Hiện chỉ có sẵn INLINE bên trong `FeedbackEffect.RunEffect` (không tách được dùng riêng lẻ).

## 2. Điểm số & phần thưởng (score & reward)

- [x] Số điểm đếm chạy (count-up) khi điểm thay đổi — `[có sẵn]`
      `Assets/Game/Scripts/UIScripts/Common/ScoreCountUp.cs` — **chưa được nối vào
      `ScoreManager.OnScoreChanged`/`GameHUD` của Kit**, việc dễ nhất để làm ngay.
- [x] Phóng to theo giá trị (reward badge to dần) — `[có sẵn — MiniGameKit]`
      `Effects/ValueScale.cs`.
- [x] Rich-text màu xanh/đỏ nhất quán cho +N/-N điểm — `[có sẵn — MiniGameKit]`
      `Effects/FeedbackTextStyle.cs`.
- [ ] Text "+N" bay lên tại đúng vị trí ghi điểm rồi mờ dần (floating score popup) — `[chưa có]`
      Khác với text feedback cố định hiện tại — số điểm hiện NGAY TẠI chỗ vừa ghi điểm (vd cạnh
      đáp án vừa chọn) rồi bay lên/mờ đi. Kết hợp `PulseEffect` (fade) + thêm chuyển động trục Y.
- [ ] Đồng xu/sao bay từ điểm ghi về thanh HUD (fly-to-target) — `[chưa có]`
      Hiệu ứng "tiền bay vào ví" quen thuộc — tăng cảm giác thưởng, nhưng độ khó cao hơn (cần
      tính toán đường bay, dùng `LeanTween`/DOTween — project đã có sẵn DOTween qua Demigiant).

## 3. Tương tác nút/vùng chọn (button & tap interaction)

- [x] Bồng bềnh nhẹ liên tục (breathing/floating idle) — `[có sẵn]`
      `Assets/Game/Scripts/UIScripts/Common/FloatingEffect.cs` — dùng để "mời gọi" tương tác vào
      đáp án/nút chưa được chọn.
- [x] Xoay liên tục (decorative rotate) — `[có sẵn]`
      `Assets/Game/Scripts/UIScripts/Common/RotateEffect.cs`.
- [x] Thu nhỏ rồi biến mất (dùng khi loại bỏ 1 lựa chọn, vd MultiSelect ẩn đáp án đã chọn đúng) — `[có sẵn]`
      `Assets/Game/Scripts/UIScripts/Common/ShrinkAndDisappearEffect.cs`.
- [ ] Vòng loading khi giữ chân trên 1 ô (hold-to-confirm ring) — `[chưa có]`
      Hữu ích cho **sàn chiếu**: trẻ nhỏ hay dẫm nhầm/dẫm nhiều ô cùng lúc — 1 vòng tròn "nạp đầy"
      dưới chân trong X giây mới tính là chọn, giảm bấm nhầm. Cân nhắc kỹ trước khi thêm (đổi
      nhịp độ chơi, không phải game nào cũng cần).

## 4. Chuyển cảnh & nhịp game (transitions & pacing)

- [ ] Chuyển mượt giữa câu hỏi (fade/slide) thay vì `SetActive` tức thời — `[chưa có]`
      Hiện tại `MiniGameControllerBase`/mọi game đều bật/tắt GameObject ngay lập tức. Cần cân
      nhắc: thêm hook `OnBeforeHideQuestion`/`OnAfterShowQuestion` ở base class để game con chèn
      hiệu ứng mà không phải sửa flow chính.
- [ ] Đếm ngược khẩn cấp khi sắp hết giờ (timer pulsing/đổi màu) — `[chưa có]`
      Chỉ áp dụng game chơi theo thời gian (`totalRounds<=0`, đã có cơ chế timer trong Kit) — số
      đếm giờ đổi màu đỏ + nhấp nháy khi còn ≤5s.

## 5. Đặc thù máy chiếu sàn (floor-projector specific)

- [x] Vệt chân/gợn sóng tại điểm chạm (touch ripple) — `[có sẵn, nhưng cục bộ 1 game]`
      `Assets/Game/Scripts/UIScripts/RiverCrossGame/Views/TouchVisualizer.cs` — đọc thẳng
      `Input.touches`, tự vẽ overlay riêng, KHÔNG phụ thuộc game logic. Rất đáng đưa vào Kit dùng
      chung (hiện chỉ RiverCrossGame có).
- [ ] Gợn sóng đổi màu theo đúng/sai (colored ripple) — `[nền tảng]`
      Mở rộng `TouchVisualizer`: cho phép set màu ripple theo kết quả (xanh khi dẫm đúng, đỏ khi
      dẫm sai) thay vì màu cố định.
- [ ] Vùng "gọi mời" phát sáng quanh đáp án (landing-zone glow) — `[chưa có]`
      Viền phát sáng nhấp nháy quanh khu vực có thể dẫm vào — giúp trẻ nhỏ nhận biết "chỗ này
      dẫm được" từ xa/góc nhìn nghiêng, không cần đọc chữ.
- [ ] Hiệu ứng ăn mừng phủ TOÀN SÀN (không chỉ 1 góc UI nhỏ) — `[chưa có]`
      Confetti/particle trải rộng khắp màn hình khi thắng lớn — khác hẳn game điện thoại (hiệu
      ứng nhỏ ở giữa màn hình là đủ), sàn chiếu cần hiệu ứng LỚN vì nhiều bạn cùng đứng xem.
- [ ] Text/icon đọc được từ nhiều góc (không chỉ đúng chiều camera) — `[chưa có, cần cân nhắc]`
      Trẻ đứng quanh sàn ở các hướng khác nhau — chữ nằm ngang 1 chiều chỉ người đứng đúng hướng
      đọc được. Có thể không cần thiết cho mọi thành phần, nhưng đáng note cho hướng dẫn/label.
- [ ] Viền màn hình phát sáng nhấp nháy để thu hút chú ý ngoại vi (edge glow) — `[chưa có]`
      Trẻ hay nhìn xuống chân mình, không nhìn giữa màn hình — hiệu ứng ở RÌA màn hình dễ lọt vào
      tầm mắt ngoại vi hơn hiệu ứng ở giữa.

## 6. Thu hút chú ý / chờ (attract & idle)

- [ ] Hiệu ứng chờ nhập liệu (idle bounce khi đang chờ 1 bên chưa bấm) — `[chưa có]`
      Hữu ích cho bet phase (đang chờ Yes/No) hoặc chờ 1 bên trả lời — nhấp nháy nhẹ báo "đến
      lượt bạn". Có thể ghép `FloatingEffect` có sẵn cho việc này ngay, không cần viết mới.
- [ ] Màn hình "attract mode" khi không ai chơi (menu/idle animation) — `[chưa có]`
      Ngoài phạm vi 1 mini-game — thuộc MenuScene, không phải việc của Kit.

## 7. Ăn mừng / kết thúc (celebration & game over)

- [x] Bounce-in + pulse cho người thắng ở màn kết quả — `[có sẵn]`
      `Assets/Game/Scripts/UIScripts/Common/WinnerEffect.cs` (`PlayBounceIn`, `StartPulse`).
- [ ] Confetti/particle khi thắng ván (không chỉ 1 câu) — `[chưa có]`
      Xem thêm mục 5 (celebration phủ toàn sàn) — đây là bản dùng ở màn `ScoreScene`, quy mô nhỏ
      hơn được vì không cần phủ hết sàn, chỉ cần nổi bật quanh tên người thắng.

## 8. Âm thanh (audio)

SFX chung đã có sẵn qua `MusicManager.Instance` (Singleton, xem
`Assets/Game/Scripts/UIScripts/Common/MusicManager.cs`):
`PlayQuestionSfx()`, `PlayCorrectSfx()`, `PlayWrongSfx()`, `PlayExplosionSfx()`,
`PlayMeteorFallSfx()`, `PlayMainMusic()`, `PlayGameplayMusic()`.

- [ ] SFX riêng cho reward system (hộp quà mở ra, đặt sao hi vọng, jackpot dồn điểm) — `[chưa có]`
      `MusicManager` chưa có sẵn — cần thêm `AudioClip` field + `PlayXSfx()` wrapper mới (không
      có overload `PlaySfx(AudioClip)` public, phải sửa `MusicManager.cs` để thêm).
- [ ] Âm thanh bước chân/dẫm sàn (footstep cue) — `[chưa có]`
      Đặc thù sàn chiếu — phản hồi âm thanh ngay khi chạm, tách biệt khỏi SFX đúng/sai (chạm =
      xác nhận thao tác, đúng/sai = kết quả).

---

## Ưu tiên đề xuất (dễ → khó, tác động cao trước)

1. Nối `ScoreCountUp` (đã có sẵn) vào `GameHUD`/`ScoreManager` của Kit — gần như miễn phí.
2. Đưa `TouchVisualizer` (đã có sẵn, đang kẹt trong RiverCrossGame) thành helper dùng chung của
   Kit — giá trị cao cho MỌI game sàn chiếu, không riêng WhoIsIt.
3. Nút nảy khi bấm (button bounce) — nhỏ, dễ, tăng cảm giác phản hồi rõ rệt.
4. Flash toàn màn hình đúng/sai — dễ làm, hiệu quả cao cho tầm nhìn xa/góc nghiêng.
5. Landing-zone glow + colored ripple — đặc thù sàn chiếu, tác động trải nghiệm lớn nhất nhưng
   cần thời gian hơn (phải sửa `TouchVisualizer` + thêm state theo dõi đúng/sai theo vị trí).
6. Confetti phủ toàn sàn khi thắng lớn — hiệu ứng "wow" nhưng tốn công nhất (cần asset/Particle
   System), để cuối.
