# Tổng quát hoá cơ chế — dựng game MỚI từ đầu trên Game Builder

> **TL;DR**: Phạm vi = **mọi mini game không đóng băng** (8 họ, ~60 mục đăng ký), không chỉ FloorStory. Phân loại dựa trên **một định nghĩa chặt**: *đối tượng* = thứ có danh tính tham chiếu được, ẩn/hiện/xoá độc lập, có chỗ trong bố cục; còn lại (ảnh, màu, nhãn, âm, trạng thái, hiệu ứng, ràng buộc, cách bố trí…) là **thuộc tính** của một đối tượng. Theo định nghĩa đó chỉ có **5 loại đối tượng hiển thị** + **4 thực thể logic**; 16 "loại" của bản trước phần lớn là *vai trò* hoặc *thuộc tính* (§6 liệt kê từng cái).
> **Đọc khi**: định mở rộng engine (`fs/*.js` ⇄ `FlowWorld`, `GenericGameController`), thêm cơ chế, hoặc thiết kế luồng "Game mới → Từ đầu".
> **Cập nhật**: 2026-10-13 — bản 3 (bản 1 phân theo game; bản 2 chỉ xét FloorStory và lẫn đối tượng với thuộc tính). **Chưa làm gì trong code.** Độ tin cậy: FloorStory + GenericGame kiểm kê từ dữ liệu/schema; các game viết tay (F3–F8) mới đọc **ghi chú đầu class + field `[SerializeField]` + SCHEMA/README**, chưa đọc từng dòng logic — ghi rõ ở §9.

> **⚠ 2026-10-13 (sau bản 3)**: §10–§11 (6 chỗ cắm Generator/Judge/… chọn từ danh mục trong code) **bị thay thế** bởi [game-builder-architecture.md](game-builder-architecture.md) (mô hình đối tượng v2 + 3 ổ cắm Hiển thị / Chấm / Hiệu ứng). Phần khảo sát §1–§9 (họ game, đối tượng vs thuộc tính) vẫn là bằng chứng hợp lệ.

## 1. Phạm vi: 8 họ game (trừ 7 game đóng băng + HaiQua)

| Họ | Game (mục `GameRegistry`) | Dữ liệu điều khiển | Engine |
|---|---|---|---|
| **F1 FloorStory/flow** | NgayCuaBe, HinhHoc, CongHinh, BeKhoeManh(+Round), VoBongBay, SanKyDieu, CauBacQua, DatDoVaoCho, DongHoKhongLo, TrungMauSac, HinhDonGian, NhanBietMau, NhoChuoiHinh, LatTheNhoGiong, NangNhe, DemKhoiHop | `game.json` (fsData v3) | `FlowWorld` / `fs/*.js` |
| **F2 GenericGame v2** | CuaHangKemTruocSau, DongHo, SangTruaChieuToi, GameSangChuaChieuToiCuaBe, SoSanhSo (+ChuSo, 2Dau, 2DauChuSo), TachSo2, GhepAnhConThieu (+ThuNghiem*) | `game.json` (schema v2) | `GenericGameController` |
| **F3 Tổng hợp (CSV)** | Counting5, Counting, SaveEnvironment, Fruit, Animal, WaterAnimal, Things, SolarQuizEn/Vi, SolarOrder/2, TongHop, TestTongHop | CSV + `gameconfig.json` | `TestTongHopController` + 5 `*Display` |
| **F4 Nghe & tìm / thứ tự (MVC)** | SoDem, Numbers, ChuCai, ChuCaiThuong, ListenSelect · PlanetOrder, PlanetAlphabet | model sinh ngẫu nhiên | Controller+Model+View riêng |
| **F5 Phép tính** | AddNumber(5)(Digit), SubNumber(5)(Digit), TongHopToan | model sinh theo cấp độ | MVC / `MiniGameControllerBase` |
| **F6 Quiz viết tay trên Kit** | DemQua, FamilySpellingJump, SentenceBuilder, FamilyMember, WhoIsIt, WordHuntMaze | CSV/hằng + code | `MiniGameControllerBase` + `ButtonDisplay`/`WordGridDisplay` |
| **F7 Thời gian thực** | LaneDash (3D 3 làn), SaveTheAstronaut (băng chuyền 12 chặng) | config + code | `MonoBehaviour` riêng |
| **F8 Công cụ/khám phá** | SolarSystem (3D khám phá), FRTest (kiểm tra nhận diện mặt) | code | riêng |

Nền tảng chung (không phải game): `MiniGameControllerBase` (Combined / Independent / Solo, WaitBothTeams, đếm ngược, feedback mặc định), `GameHUD`, `ScoreManager`, `GameLogger`, scene Menu/Score/Tutorial.

## 2. Định nghĩa: đối tượng ≠ thuộc tính

**Đối tượng** khi thoả CẢ BA:
1. **Danh tính** — có `id`/chỉ số, hành động/ràng buộc/luật có thể nhắm tới nó ("quả táo thứ 3", `#hand`, `@tapped`).
2. **Tồn tại độc lập** — hiện/ẩn/sinh/xoá riêng, không kéo theo đối tượng khác.
3. **Có chỗ** — chiếm vị trí/kích thước trong bố cục (hoặc là thực thể logic có vòng đời).

**Bốn phép thử** (đủ 1 "không" → là thuộc tính):
- Xoá nó đi, phần còn lại có còn đứng được không? (ảnh nền của slot: xoá thì slot vẫn còn ⇒ thuộc tính)
- Có thể có **hai cái cùng loại với giá trị khác nhau** trong cùng một lúc không? (hai quả trứng khác màu ⇒ đối tượng; "màu" ⇒ thuộc tính)
- Có thể làm đích của một hành động độc lập không? (`shake` lên *thẻ*; không `shake` lên *chữ trên thẻ* riêng)
- Nó có nghĩa khi đứng một mình không? (icon trong slot, nhãn trên thẻ, tiếng của vật ⇒ không)

Ví dụ rút từ code: GenericGame tự tách **Item** (đối tượng: con thỏ, tên lửa) khỏi **icon** nhỏ trong slot (thuộc tính của slot); số lần chạm, màu xanh/đỏ khi đúng/sai, trạng thái úp — là **thuộc tính/trạng thái** chứ không phải đối tượng.

## 3. Thực thể logic (không vẽ, nhưng có danh tính) — 4 loại

| # | Thực thể | Là gì | Ví dụ (họ) | Không phải |
|---|---|---|---|---|
| **E1** | **Vật / mục nội dung** (Item) | bản ghi dữ liệu có id: hình, tên, thuộc tính, tiếng | `items[]` (F1); dòng CSV/`QuestionData.answers` (F3); từ Family, hành tinh (`PlanetData`), chữ cái, số, vật cản/thưởng (`LaneDashItemVisuals`) | "con thỏ trên màn hình" (đó là đối tượng sinh từ vật) |
| **E2** | **Câu hỏi / lượt** (Question, Round) | thể hiện của 1 lượt: đề, ứng viên, đáp án, cấu trúc con (số hạng A,B,C; chuỗi; cặp; bảng chữ + 3 từ; mẫu chữ khuyết) | `QuestionData` (F3/F6), `Q` (F1), `rounds[]` (F2), `TeamQ` (F5), `WordGridDisplay` 3 từ (F6) | |
| **E3** | **Người chơi / đội** | Left/Right, tên, điểm, mức độ, ngôi sao | `Team`, `ScoreManager`, cấp độ riêng từng bên (F5) | thanh điểm trên HUD (đó là chỉ số) |
| **E4** | **Phiên / lịch** | vòng đời game: bắt đầu → đếm ngược → câu → hết giờ/đủ điểm → ScoreScene | `MiniGameState`, `flowPick`, `roundOrder`, `GameTime` | |

(Trạng thái toàn cục — biến, bộ đếm, mốc — không phải thực thể: là **thuộc tính của E2/E3/E4** hoặc *dữ liệu của luật*, xem §5.)

## 4. Đối tượng hiển thị — 5 loại thực sự

Mỗi loại gồm nhiều **vai trò (role)** — vai trò là *nhãn tiện lợi/preset*, thực chất là giá trị của thuộc tính `role` trên cùng một loại.

| # | Loại đối tượng | Định nghĩa | Vai trò thấy trong các game |
|---|---|---|---|
| **O1** | **Hiển thị** (Display) | thứ chỉ để **xem**, không phải nhận chạm để ra kết quả | *nền* (ảnh/màu phủ nửa màn hình, skybox) · *vật cảnh/trang trí* (cây, sông, bàn, Item GenericGame) · *nhân vật* (Na, Bi, thỏ, runner, phi hành gia — là Prop có `role=actor` + hành vi) · *vật chuyển động* (vật cản, phần thưởng, power-up LaneDash; hành tinh/mặt trăng/tiểu hành tinh 3D) · *câu hỏi* (ảnh/chữ/icon/âm của slot câu hỏi, caption, mẫu chữ khuyết "C_T") · *chỉ số* (điểm, giờ còn lại, đếm trong rổ, km/h, đồng hồ số, chữ "BUỔI…", tên chặng) |
| **O2** | **Phần tử chạm** (Tappable) | thứ nhận **tap** và sinh kết quả | *đáp án* (`ButtonItem`, thẻ, vật ghép lớp, `FloatingItem` quỹ đạo, hành tinh quạt `SolarSystemDisplay`) · *đích/chỗ* (vùng đặt, ô trên bàn, làn trái-giữa-phải, gạch trong băng chuyền) · *ô lưới/chữ* (ô chữ 7×7 WordHunt) · *thẻ lật* · *quả/vật nhặt* (DemQua) · *hộp quà* (WhoIsIt) · *điều khiển* (Back, Retry, Yes/No cược, nút xanh/đỏ FRTest, thanh trượt tốc độ) · *vùng chạm tự do* (nền lấy vị trí — SanKyDieu) |
| **O3** | **Ô chứa** (Receptacle) | thứ **nhận / giữ** các đối tượng hay giá trị khác | *rổ* + các ô quả trong rổ (DemQua) · *vùng thu thập / thanh nhiên liệu* (`collectSlots`, TachSo2) · *chỗ khuyết* (khung nét đứt HinhHoc, lỗ tranh `hideOnCorrect`) · *dải thứ tự* (`strip`, chỗ trống "___ ___ ___") · *ô số hạng A/B/C* chứa N icon (AddNumber/Sub) |
| **O4** | **Nhóm** (Group) | gói nhiều đối tượng dùng **chung** bố cục/ràng buộc/vòng đời; có quy tắc xếp con | *hàng/lưới/vòng thẻ* · *bảng chữ* · *bập bênh*, *đồng hồ (mặt+kim)*, *đống khối* (widget) · *phương trình A + B = C* · *băng chuyền/làn* (`LaneTrack`, `SaveTheAstronautLane`) · *HUD hai đội* (thanh điểm + tên) · *lớp phủ/Modal* (hướng dẫn, kết thúc, thông tin hành tinh, đặt cược, chọn hộp quà) |
| **O5** | **Cổng nhìn** (Viewport) | thứ **vẽ một thế giới** vào khung màn hình | *nửa màn hình của mỗi đội* (mọi game 2 đội) · *camera 3D chia đôi* (LaneDash) · *camera thế giới 3D* (SolarSystem) · *khung camera mặt* (FRTest) · *video modal* |

**Vì sao chỉ 5**: các "loại" còn lại khác nhau ở **thuộc tính** (nội dung, hành vi, ràng buộc) chứ không ở bản chất đối tượng:
- *Nhân vật*, *vật cản*, *hành tinh*, *Item* = O1 khác `role` + `Hành vi` (di chuyển/quỹ đạo/bob) + va chạm.
- *Thẻ đáp án*, *vật ghép lớp*, *quả*, *chữ trên ô*, *hành tinh bấm được* = O2 khác `Skin` (ảnh / ghép lớp / n-icon / chữ / mô hình 3D).
- *Thẻ lật*, *quả đã hái*, *ô đã dẫm*, *ô đã sáng* = **trạng thái** của O2 (`Covered/Selected/Correct/Wrong/Revealed/Locked/Lit`).

### 4.1 Ai là đối tượng, ai KHÔNG (các nhầm lẫn hay gặp)
| Thứ | Là | Vì sao |
|---|---|---|
| Nhãn chữ trên thẻ / dưới vật | **thuộc tính** (`label`) của O2 | không xoá riêng, không có hai nhãn khác giá trị trên cùng một thẻ |
| Icon nhỏ trong slot, N icon xếp lưới | **thuộc tính** (`skin: icon-compose(n)`) | text = số icon (SCHEMA) — chỉ là kiểu hiển thị |
| Ảnh nền của slot, khung 9-slice, `slotFrames` | **thuộc tính** (`skin.background`) | |
| Chữ "BUỔI SÁNG" đứng riêng giữa cảnh | **đối tượng** O1(chỉ số), ràng buộc `$period` | có id, bind, xoá riêng |
| Mặt trời đổi vị trí theo buổi | **đối tượng** O1 + thuộc tính **ràng buộc** | "chỉ báo trạng thái" không phải loại, là O1 có `bind` |
| Kim đồng hồ | **đối tượng** O1 trong **Nhóm** (widget) | bind `rot` |
| Số trên mặt đồng hồ | **đối tượng** O2 (chạm được) trong nhóm | |
| Confetti, ✔/✖, "Giỏi quá!" bay lên | **Hiệu ứng** (`Effect`), không phải đối tượng | tạm thời, không tham chiếu được, chạy trên một đối tượng/điểm |
| Hoa/trái cây "chui ra" sau khi đúng | **đối tượng** O1 *sinh lúc chạy* | "sinh ra" là **vòng đời**, không phải loại |
| Thanh điểm 2 đội, đồng hồ đếm ngược, ✔ mặc định | **đối tượng của nền tảng (H)** | do `GameHUD`/Kit dựng, không soạn |
| Màu xanh/đỏ khi đúng/sai, tô xám khi khoá | **trạng thái/thuộc tính** | `showCorrectWrongTint` |
| "Đội trái / Đội phải" | **O5 Cổng nhìn** | hai viewport song song, cùng luật |

## 5. Thuộc tính — gom theo "thành phần" (component) gắn lên đối tượng

| Thành phần | Thuộc tính (đã thấy trong các họ) | Gắn lên |
|---|---|---|
| **Nội dung / Skin** | `text` · `image` · `icon` · `icon-compose(n)` · hình+màu · **ghép lớp** · mô hình 3D / primitive · khung camera · video · `imagePool` (ngẫu nhiên 1 ảnh) · khung 9-slice · `shape` (chữ nhật/vuông/tròn) · ép kiểu chữ (cỡ, màu, đậm, viền) | O1–O5 |
| **Liên kết dữ liệu** | `item` (trỏ E1) · `answerIndex` · `correct` · `value` (số cộng/so sánh) · `order` (thứ tự đúng) · `sound` | O2, O1(câu hỏi) |
| **Biến đổi** | `x, y, w, h` (% hoặc đơn vị) · `rot` · `scale` · `alpha` · `z`/`layerOrder` · `mirror` (nửa phải lật/không) | tất cả |
| **Bố trí** | `arrangement` = tay / ma trận / ngẫu nhiên không chồng / **spawn flow** / quỹ đạo / quạt cung / lưới NxN / làn / băng chuyền độ sâu / kim tự tháp (rổ) | O4, O2 |
| **Trạng thái** | `Normal / Selected / Correct / Wrong / Revealed / Locked` · `Covered/Hidden` · `Lit` (ô nhiên liệu) · `Found` (từ đã tìm) · `Stunned` (runner) | O2, O3, O1 |
| **Ràng buộc (bind)** | thuộc tính ← biến / `roundScore/totalScore/targetScore` / câu hỏi / vật | O1, O3 |
| **Hành vi nền** | `onIdle` (Breathing…) · `bob/pulse/float` · `swing` · `scroll` · `orbit` · `launchOnComplete` (bay lên khi thắng) · `smooth` | O1, O2 |
| **Phản ứng** | khe `onCorrectTap / onWrongTap / onCorrectRemove / onClick / onCorrect / onPartial / onWrong` (GenericGame), `on.right/wrong/step/setup/start/end` (Flow) — mỗi khe = *danh sách hiệu ứng + âm + cờ icon ✔✖* | O2, O1(item), toàn game |
| **Vòng đời** | game / dạng câu / lượt / tạm thời; `hideOnCorrect` · `sendToBack` · FlyToStay (ở lại tới hết lượt) · `stayFor` | tất cả |
| **Va chạm / thân** *(chưa có, thiết kế Sim)* | `body, group, overlap` | O1(vật chuyển động) |

**Hiệu ứng (Effect)** — *không* phải đối tượng, không phải thuộc tính: là **hành động tạm thời** chạy trên đối tượng/điểm. Bộ đã có: `Punch, Shake, FadeOut, Breathing, Pulse, FlyOff, FlyTo, FlyToStay` (Kit) + `bounce, rotate, move, scale, fade, tint, spawn, fly…` (Flow). Mỗi hiệu ứng có `đích` + `thời lượng` + `tham số`.

## 6. Đối chiếu với 16 "loại" của bản trước

| Bản trước | Kết luận | Là gì thật sự |
|---|---|---|
| V1 Nền | giữ | O1, `role=nền` |
| V2 Trang trí tĩnh | giữ | O1, `role=vật cảnh` |
| V3 Nhân vật/actor | **bỏ khỏi loại** | O1 `role=actor` + thuộc tính Hành vi (di chuyển tới đích) |
| V4 Chỉ báo trạng thái | **bỏ** | O1 + thuộc tính **ràng buộc** |
| V5 Nhãn chữ trong cảnh | **gộp** | O1 `role=chỉ số/nhãn`; nhãn *trên thẻ* là thuộc tính |
| V6 Widget có đầu vào | **→ O4 Nhóm** | nhiều đối tượng + ràng buộc chung |
| V7 Thẻ đáp án | **gộp** | O2 `meaning=đáp án`, `skin=thẻ` |
| V8 Vật đáp án ghép lớp | **bỏ khỏi loại** | O2 với `skin=ghép lớp` (thuộc tính) |
| V9 Vùng đích | **gộp** | O2 `meaning=đích` (hoặc O3 nếu giữ vật thả vào) |
| V10 Thẻ úp/lật | **bỏ** | O2 + **trạng thái** `Covered` |
| V11 Ô chuỗi | **→ O3 Ô chứa** | |
| V12 Vùng chạm tự do | **gộp** | O2 `meaning=vị trí` |
| V13 Vật sinh ra | **bỏ** | O1 + **vòng đời** `sinh lúc chạy` |
| V14 Hạt (confetti) | **bỏ** | **Hiệu ứng**, không phải đối tượng |
| V15 Lời nói/nhắc | **bỏ** | **Hiệu ứng** (`say`) hoặc O1 `role=câu hỏi` |
| V16 HUD hệ thống | **→ nền tảng H** (O4 do Kit dựng) | không soạn |

Thiếu so với bản trước: **O3 Ô chứa** và **O5 Cổng nhìn** (bản 2 chưa thấy vì chỉ xét FloorStory: rổ DemQua, vùng thu thập GenericGame, camera 3D LaneDash/SolarSystem, camera mặt FRTest).

## 7. Kiểm kê theo họ: đối tượng · thuộc tính nổi bật · luật · chạm

### F1 FloorStory/flow (16 game, kiểm kê từ dữ liệu)
- **O1**: nền (`fill`, 4 game), trang trí (cây, sông, bàn), nhân vật (actor 5 game), chỉ số/nhãn (BUỔI, số đồng hồ), vật sinh ra (hoa, trái, con vật — 3 game), kim đồng hồ (`kind:hand`).
- **O2**: thẻ (`cards`, 8 game), vật ghép lớp (`props+tpl`, 6 game), vùng đích (DatDoVaoCho/HinhHoc/DongHo), thẻ lật (LatThe), vùng tự do (SanKyDieu).
- **O3**: dải thứ tự (`strip`, 2 game), bóng đen/khung chỗ đặt (HinhHoc `forEachItem`).
- **O4**: bập bênh, đống khối, mặt đồng hồ.
- **Thuộc tính nổi bật**: 8 thuộc tính `bind` (`color` 7 game), `anim` (`bob`,`pulse`), `smooth`, `mirror`, `z=front`, `alpha`; 24 khoá tag tự do (phân loại / đại lượng / quan hệ / hình học / lời / tài nguyên).
- **Luật**: 8 `gen` (thực chất 5 hình dạng câu hỏi: chọn 11 game, chuỗi 2, cặp 1, số lượng 1, tự do 1); 4 judge (`equals` 13, `order` 2, `pairs`, `taps`); độ khó tăng dần lặp 5 lần; luân phiên 2 flow (2 game).
- **Chạm**: 1 tap-chọn / thứ tự / lật / vị trí.

### F2 GenericGame v2 (10 game imported, kiểm kê từ `game.json`)
- **O1**: nền (`layout.background`), **Item** (trang trí cả game: thỏ, tên lửa), **câu hỏi** (nhóm slot `question`: chữ/ảnh/icon/âm).
- **O2**: **đáp án** (nhóm slot `answers`, `ButtonItem`).
- **O3**: **vùng thu thập** (`collectSlots`, 2–10 ô; `fillByValue`, `autoStretch`), thanh nhiên liệu (SumToTarget).
- **O4**: nhóm slot (câu hỏi/đáp án) với `arrangement` = manual / matrix / random (và **spawnFlow** — cờ chung cả game: `SoSanhSo`, `SoSanhSoChuSo`).
- **Thuộc tính**: skin (image/icon/text/`imagePool`), `shape`, `correct`, `sound`, 4 khe `fx`/slot, Item `fx.onClick/onCorrect/…`, `text.bind` (totalScore…), `hideOnCorrect` + `sendToBack` (ghép tranh), `launchOnComplete`, `mirror`, `layerOrder`, `qFrame`, `slotFrames`.
- **Luật**: `answerMode` = Single (6 game) / MultiSelect / OrderedSequence / **SumToTarget** (TachSo2); `wrongEndsRound`, `waitBothTeams`, `playMode` Combined/Independent.
- **Hiệu ứng đã dùng**: `Breathing, Punch, Shake, FlyOff, FlyToStay, FadeOut`.

### F3 Tổng hợp CSV (kiểm kê từ `GameTypes` + Display)
- **O2**: đáp án (Button / **Floating quỹ đạo** / **hành tinh quạt cung** `SolarSystemDisplay`+`En` / `PlanetOrderDisplay`), ô 2 bảng Matching (3 trái ↔ 3 phải).
- **O1**: câu hỏi `QuestionMediaDisplay` = `Text | Image | Audio | IconCompose`.
- **Thuộc tính**: `ItemState` {Normal, Selected, Correct, Wrong, Revealed, Locked}, `AnswerMediaType`, `ChooseDisplayMode` (Auto/Button/Floating/…), `orbitRadius/orbitSpeed`, `floatingSize`, `difficulty` 1–10 + `adaptiveDifficulty` (lên/xuống bậc theo chuỗi đúng/sai), `independentPlay`, `weightFloating/Button/Matching` (xác suất hiển thị).
- **Luật**: `QuestionType` {Choose, Matching}; `AnswerMode` {Single, MultiSelect, OrderedSequence, SumToTarget}; `ClickResult` 6 kết quả; điểm: đúng +1, theo `GameSettings`.

### F4 Nghe & tìm / thứ tự (SoDem, Numbers, ChuCai, ListenSelect; PlanetOrder, PlanetAlphabet)
- **Cùng một khuôn MVC**: câu hỏi chung (`questionText`/âm thanh), **3 ô** mỗi bên (`p1Boxes/p2Boxes`: nút + chữ + ảnh), 2 icon ✔/✖, thanh điểm (`maxScore=10`), tên đội, `gameOverPanel`.
- **Khác nhau chỉ ở nội dung & luật chấm**: tìm số/chữ theo **âm thanh** (chọn 1) · chạm 3 ô **theo thứ tự tăng dần** / **bảng chữ cái** (OrderedSequence, sai thì reset lượt của bên đó).
- **Nhận xét**: 6 game cùng *một cấu trúc đối tượng* (3×O2 + 1×O1 câu hỏi + chỉ số) — khác ở **luật đáp án** (theo âm / theo thứ tự) và **kho nội dung** (số, chữ, hành tinh).

### F5 Phép tính (AddNumber / SubNumber / TongHopToan)
- **O4 Nhóm "phương trình"**: 3 ô O3 `A`, `B`, `C` (mỗi ô là Ô chứa N icon, có viền, một ô **ẩn**) + 2 nhãn toán tử (`+`/`−`, `=`) + 4 nút đáp án (O2).
- **Thuộc tính**: ô ẩn = trạng thái `Hidden`; **cấp độ riêng từng bên**, thưởng sao theo độ khó câu; hai bên **có câu riêng** (TongHopToan, `PerTeamDisplay`) để không chép nhau.
- **Luật**: sinh thủ tục `A ± B = C` theo phạm vi (≤5, ≤10), chữ số hay icon; `Digit` = ô hiện chữ số thay vì icon.

### F6 Quiz viết tay trên Kit
- **DemQua**: O2 quả (MultiSelect, mọi quả đều đúng) → **O3 rổ** (+ 5 ô quả kim tự tháp, **O1 chỉ số** đếm riêng mỗi round) · phát **âm thanh ghép "số + tên quả"** theo hàng đợi (hai bên không chồng tiếng).
- **FamilySpelling**: O1 ảnh + **mẫu chữ khuyết** (`wordTemplate`, chữ điền cam `#FF8A00`) + 4 O2 chữ cái; `TileAppearAnimator`, `SinglePopAnimator` (hoạt hình xuất hiện).
- **SentenceBuilder**: O1 ảnh + **O3 chỗ trống "___ ___ ___"** điền dần + 4 O2 từ (3 đúng theo thứ tự + 1 nhiễu); đọc nối câu khi đúng hết.
- **FamilyMember**: O1 ảnh + 4 O2 chữ.
- **WhoIsIt**: như FamilyMember **+ lớp thưởng**: **đặt sao hi vọng** (Yes/No, x2 điểm), **Jackpot** (cả hai sai → câu sau x2), **hộp quà bí mật** (bonus/penalty/share), huy hiệu điểm, `starBurst`, pháo hoa; thắng khi **đầy thanh HUD**.
- **WordHuntMaze**: **O4 bảng chữ 7×7** mỗi bên **sinh độc lập** (3 từ: ngang/dọc/chéo, xuôi hoặc ngược); ô là O2 `meaning=dẫm`; trạng thái `Found` (xanh cả từ khi đủ ô); **không gợi ý**.
- **Luật chung**: `AnswerMode` Single/MultiSelect/Ordered; điểm cố định; chơi theo `GameTime`.

### F7 Thời gian thực
- **LaneDash**: **O5** 2 camera 3D chia màn hình · **O4 `LaneTrack`** 3 làn · O1 `runner avatar`, **vật cản / phần thưởng / power-up** (pool theo biến thể, mesh Kenney hoặc primitive), **mặt đường cuộn** · chỉ số km/h, mét, số thưởng. Thuộc tính: `speed`/`ramp`, `stunDuration`, `recoverAccel`, `horseBoost`, **cấp độ khó chung 2 bên** (theo thời gian), chạm theo **toạ độ X màn hình → làn**.
- **SaveTheAstronaut**: **O4 băng chuyền 12 chặng + Trái Đất** (độ sâu `S = step + t/travel`) · mỗi chặng là **O2 gạch hình thang** (`TrapezoidImage`, viền, ảnh lộ hành tinh) · O1 phi hành gia (nghiêng theo làn), tên chặng. Thuộc tính: pattern đúng/sai **bền qua lượt chết** (dấu hố đen vĩnh viễn), chặng xa nhỏ/mờ. Luật: đi hết 12 chặng = +1 lượt; sai hoặc **không kịp bấm** (`travelDuration`) = chết, `S` về 0.
- **Điểm chung**: đồng hồ liên tục, không có "câu hỏi rời rạc" (log mỗi sự kiện né/va/nhặt thành 1 round); **chưa có tương đương trong builder**.

### F8 Công cụ/khám phá
- **SolarSystem**: **O5** thế giới 3D (skybox, ánh sáng mặt trời) · O1 mặt trời (halo, quay), hành tinh (quỹ đạo, xoay trục), mặt trăng, **vành đai tiểu hành tinh** (280 hạt) · O2 hành tinh (tap → **O4 info panel**: tên, mô tả, khoảng cách, đường kính, số vệ tinh, nhiệt độ; video modal) · điều khiển: thanh tốc độ, đổi tỉ lệ thật/mô phỏng, reset camera. **Không có điểm/luật.**
- **FRTest**: O5 khung camera + O2 hai cặp nút xanh/đỏ + chỉ số đếm ngược; nhận diện mặt **bật/tắt theo pha** (tiết kiệm CPU). Công cụ kiểm tra, không phải game.

## 8. Ma trận: loại đối tượng × họ

| | O1 Hiển thị | O2 Chạm | O3 Chứa | O4 Nhóm | O5 Cổng nhìn |
|---|---|---|---|---|---|
| F1 FloorStory | ● nền, vật cảnh, actor, nhãn | ● thẻ, ghép lớp, đích, lật | ● dải, chỗ khuyết | ● bập bênh, khối, đồng hồ | ● 2 nửa |
| F2 GenericGame | ● Item, câu hỏi | ● đáp án | ● vùng thu thập | ● nhóm slot, spawnFlow | ● 2 nửa |
| F3 Tổng hợp | ● câu hỏi media | ● button, floating, quạt, matching | | ● bảng matching | ● 2 nửa |
| F4 Nghe/thứ tự | ● câu hỏi | ● 3 ô | | | ● 2 nửa |
| F5 Phép tính | ● toán tử | ● 4 nút | ● ô A/B/C | ● phương trình | ● 2 nửa |
| F6 Quiz Kit | ● ảnh, mẫu chữ | ● nút, quả, ô chữ, hộp quà, Yes/No | ● rổ, chỗ trống | ● bảng 7×7, lớp cược | ● 2 nửa |
| F7 Real-time | ● runner, vật cản, thưởng | ● gạch, làn | | ● băng chuyền, làn | ● camera 3D |
| F8 Công cụ | ● hành tinh, mặt trời | ● hành tinh, nút | | ● info panel | ● 3D, camera mặt |

Nhận xét: **O2 và O5 có ở mọi họ**; **O3 chỉ xuất hiện khi có cơ chế "nhận/đếm"** (DemQua, thu thập, phương trình, dải) — builder hiện chưa coi nó là loại riêng; **F7/F8 cần O5 + hành vi thời gian thực** mà builder chưa có.

## 9. Điều đã/chưa kiểm chứng
- ✔ Đã đọc kỹ: `GenericGame/SCHEMA.md`, `MiniGameKit/README.md`, 10 `game.json` GenericGame, `GameTypes.cs`, `TongHopConfig`, header + field của controller/view F4–F8, dữ liệu 17 file F1.
- ⚠ Chưa đọc từng dòng logic của F3–F8: các thuộc tính ở §7 lấy từ chú thích đầu class và tên field; chi tiết sai số (vd luật chấm của PlanetOrder khi sai) cần xác minh khi thực sự tổng quát hoá từng game.
- ⚠ Game Unity ngoài `Assets/Game/Scripts/UIScripts/*` (nếu có) chưa quét; HaiQua và 7 game đóng băng cố ý bỏ qua.

## 10. Kiến trúc tổng quan: một game = 6 điểm biến thiên + mô hình thời gian

Từ §3–§7 rút ra: mọi game không đóng băng đều là **cùng một khung**, khác nhau ở 6 chỗ cắm. Dựng game mới từ đầu = chọn một giá trị cho mỗi chỗ cắm từ **danh mục** (catalog), không viết code mới.

| # | Chỗ cắm | Trả lời câu hỏi | Gồm | Thuộc về |
|---|---|---|---|---|
| **C** | **Content** (kho nội dung) | Có những *vật* nào, mang thông tin gì? | E1 Item + tag/giá trị + media + âm | dữ liệu |
| **G** | **Generator** (sinh lượt) | Mỗi lượt lấy gì từ kho, ghép thành đề thế nào? | E2 Round | logic |
| **P** | **Presentation** (sân khấu) | Đối tượng nào hiện ra, bố trí & skin ra sao? | O1–O5 + thuộc tính + bố trí | giao diện |
| **J** | **Judge** (chấm) | Chuỗi chạm nào là đúng? | predicate trên lịch sử chạm của lượt | logic |
| **R** | **Reaction** (phản ứng) | Khi sự kiện xảy ra thì hiệu ứng/âm/biến nào chạy? | khe `on.*` → danh sách Effect/Action | logic + giao diện |
| **S** | **Session** (phiên) | Điểm, thắng/thua, độ khó, lượt đội, lớp thưởng? | E3 + E4 + chế độ chơi | logic |

**Mô hình thời gian** (chọn 1 trước khi chọn 6 cái trên):
- **T1 Theo lượt** (round-based): có Round rời rạc → G/J/R áp dụng đầy đủ. Phủ F1–F6 (≈ 85% game).
- **T2 Liên tục** (real-time): không có lượt; thay G bằng **Spawner theo thời gian**, J bằng **va chạm / cửa sổ thời gian**, S bằng **mạng/quãng đường**. Phủ F7.
- **T3 Khám phá** (không chấm): chỉ P + R. Phủ F8.

### 10.1 Mỗi chỗ cắm có danh mục gì (suy ra từ game đã có)

**G — Generator** (cách sinh Round từ Content):
`chọn-1-trong-nhóm` (+ gây nhiễu theo tag) · `chọn-k-có-thứ-tự` (theo khoá sắp xếp: số, bảng chữ cái, khoảng cách) · `cặp` (hai cột) · `số-lượng` (N vật cùng loại) · `so-sánh-2-số` · `phương-trình` (A±B=C, che 1 ô) · `từ-che-chữ / câu-điền-chỗ-trống` · `lưới-chữ` (đặt từ ngang/dọc/chéo) · `tự-do` (không đề, chỉ tương tác). Tham số chung: nhóm nguồn (tag), số ứng viên, độ khó, hạt giống chung hay riêng từng đội.

**J — Judge** (predicate trên chuỗi chạm):
`bằng` (1 chạm trúng đáp án) · `thứ-tự` (chạm lần lượt theo khoá) · `cặp` · `tập-đủ` (chạm hết mọi vật thuộc tập, DemQua / WordHunt) · `tổng-bằng-mục-tiêu` (SumToTarget) · `đếm-đạt-N` · `vị-trí` (chạm đúng vùng — SanKyDieu) · `cửa-sổ-thời-gian/va-chạm` (T2). Tham số: sai thì *kết thúc lượt / reset lượt / chỉ trừ điểm*, có chờ cả hai đội không.

**R — Reaction** (khe sự kiện × Effect):
Sự kiện: `start, setup, step (mỗi chạm đúng một phần), right, wrong, timeout, end, onIdle`. Effect/Action: nhóm *chuyển động* (move, fly, flyToStay, shake, punch, bounce), *hình* (fade, scale, tint, spawn, hide), *đặt giá trị* (set biến, cộng điểm, bind), *âm* (sfx, đọc tên, nhạc), *phản hồi hệ thống* (✔/✖, confetti, banner), *luồng* (`next`, `endRound`, `endGame`, chọn flow kế tiếp).

**P — Presentation**: 5 loại đối tượng × thuộc tính (§4–5); lớp bố cục (`arrangement`) chọn: tay / hàng / lưới / ngẫu nhiên không chồng / spawn flow / quỹ đạo / quạt cung / làn / băng chuyền; `layerOrder`, `mirror`.

**S — Session**: chế độ đội (Combined / Independent / Solo, WaitBothTeams) · chấm điểm (+1, theo độ khó, nhân đôi) · thắng khi (đủ điểm / hết giờ / đủ lượt / đầy thanh) · độ khó (cố định / thích nghi theo chuỗi / tăng dần trong game) · **lớp thưởng** (cược, jackpot, hộp quà) tuỳ chọn.

### 10.2 Phân rã game thật vào 6 chỗ cắm (kiểm chứng khung)
| Game | G | P (đối tượng chính) | J | R nổi bật | S |
|---|---|---|---|---|---|
| Fruit / Animal | chọn-1 theo tag | O1 câu hỏi + O2 đáp án (Button/Floating) | bằng | ✔ + FlyTo | +1, timer, độ khó thích nghi |
| SoDem / ChuCai | chọn-1 + nhiễu | O1(âm) + 3×O2 | bằng | ✔/✖ mặc định | tới 10 điểm |
| PlanetOrder / Alphabet | chọn-k có thứ tự | 3×O2 | thứ-tự (sai → reset) | reset ô | tới 10 điểm |
| Matching | cặp | O4 2 cột O2 | cặp | nối + khoá | +1 mỗi bộ |
| DemQua | số-lượng | O2 quả + O3 rổ + O1 đếm | tập-đủ | FlyToStay, counter, âm ghép | mỗi round |
| TachSo2 / CuaHang | số-mục-tiêu | O2 + O3 thu thập | tổng-bằng-mục-tiêu | fill ô, FlyOff | theo ô đầy |
| SoSanhSo | so-sánh-2-số | spawn flow O2 | bằng | Punch / Shake | +1 |
| AddNumber / SubNumber | phương-trình | O4 phương trình (3×O3) + 4×O2 | bằng (đã tính) | lộ ô ẩn | sao theo độ khó, mức riêng từng đội |
| FamilySpelling | từ-che-chữ | O1 ảnh + mẫu chữ + 4×O2 | bằng | điền chữ cam | +1 |
| SentenceBuilder | câu-điền-chỗ-trống | O1 + O3 "___ ___ ___" + 4×O2 | thứ-tự | điền dần, đọc câu | +1 |
| WordHuntMaze | lưới-chữ (riêng từng đội) | O4 lưới 7×7 | tập-đủ-theo-đường | tô xanh từ | 3 từ |
| WhoIsIt | chọn-1 | O1 + 4×O2 + lớp cược/hộp quà | bằng | star-burst, Jackpot | đầy thanh HUD |
| HinhHoc (flow) | tập (forEachItem) | O3 chỗ khuyết + O2 hình | bằng/vị-trí | bay vào chỗ | +1 |
| DongHoKhongLo | giá trị | O4 widget đồng hồ (O1 kim, O2 số) | bằng | kim `smooth` | +1 |
| LatTheNhoGiong | cặp | O2 thẻ (`Covered`) | cặp có trạng thái | lật | cặp đủ |
| LaneDash | **spawner theo thời gian** | O5 3D + O4 làn + O1 vật cản/thưởng | **va chạm** | choáng, nhặt | quãng đường |
| SaveTheAstronaut | **bước thời gian** | O4 băng chuyền + O2 gạch | **đúng gạch + kịp giờ** | chết → về 0 | 12 chặng |
| SolarSystem | — | O5 3D + O1 + O2 (→ O4 info) | — | mở panel | — |

→ **15/18 dòng nằm gọn trong T1** và chỉ khác nhau ở các ô của bảng; 2 dòng T2 và 1 dòng T3 cần module riêng.

### 10.3 Kiến trúc phân tầng (bottom-up)
```
L7  Authoring     Builder: Mẫu (template) → Chọn 6 chỗ cắm → Chơi thử → Xuất zip
L6  Session       đội · điểm · thắng/thua · độ khó · lớp thưởng          [S]
L5  Rules         sự kiện → điều kiện → Action ; Judge ; Reaction         [J,R]
L4  Round         Generator + biến của lượt (Q)                           [G]
L3  Behavior/FX   Effect library · Behavior (idle/orbit/scroll) · Spawner
L2  Object model  O1–O5 + components (Skin·Bind·Transform·State·Layout)   [P]
L1  Content       Item store · tag · pool · media/âm
L0  Platform      Viewport 2 đội · HUD · Input tap · Audio · Log · Score  (Kit, không soạn)
```
- **Hợp đồng duy nhất** giữa web và Unity: một **schema** (superset của fsData v3 + khe `fx`/collect của GenericGame v2) + **bộ test số học** (đã có: `roundcmp.js` so web vs Unity theo cùng seed).
- **Hai runtime hiện tại là hai nửa**: GenericGame v2 mạnh ở P/R (nhóm slot, khe `fx`, collect, spawn flow); FloorStory flow mạnh ở G/J + biến + bind. Hợp nhất = lấy phần mạnh của mỗi bên làm L2–L5 chung; **GenericGame cũ nhập vào như "một flow" (slot → object, rounds → manual rounds)** để game đã xuất không hỏng.
- **Mô-đun nhúng cho T2/T3**: LaneDash, SaveTheAstronaut, SolarSystem không đưa vào schema dữ liệu; builder coi chúng là **O5 "module"** (chọn module + vài tham số: tốc độ, số làn, bộ vật cản) — vẫn dùng chung L0/L1/L6.

### 10.4 Quy trình dựng game mới từ đầu (đích đến)
1. **Chọn thời gian**: Theo lượt / Liên tục / Khám phá.
2. **Content**: tạo/nhập Kho vật (ảnh, tên, âm, tag). Có thư viện sẵn.
3. **Generator**: chọn kiểu đề + nguồn (tag) + số ứng viên → hệ tự sinh bộ lượt (xem trước 5 lượt mẫu).
4. **Presentation**: chọn bố cục + skin cho nhóm *Câu hỏi* và nhóm *Đáp án* (giao diện slot như game kéo-thả đã có); kéo thêm đối tượng trang trí/Ô chứa nếu cần.
5. **Judge**: chọn luật chấm + chính sách khi sai.
6. **Reaction**: mỗi khe (đúng/sai/bắt đầu/…) gắn Effect + âm; có mặc định sẵn theo Judge.
7. **Session**: chế độ đội, điều kiện thắng, độ khó.
8. **Chơi thử hai nửa → Xuất zip → Import Unity** (đã có).
Mẫu (template) = một combination đặt sẵn cho 6 chỗ cắm; người dùng mở lên là có game chạy được rồi mới sửa — đây là chỗ 16 game FloorStory + 10 game GenericGame trở thành **thư viện mẫu**, không còn là engine riêng.

### 10.5 Khoảng trống so với builder v4.1 (xếp theo tầng phủ game)
| Khoảng trống | Tầng | Game được mở khoá | Ghi chú |
|---|---|---|---|
| Judge `tập-đủ`, `cặp`, `thứ-tự` chọn được ở UI cho Generator `chọn` | L5 | DemQua, SentenceBuilder, PlanetOrder, F4 | engine đã có `order/pairs`, UI chưa phơi hết |
| **O3 Ô chứa** là loại thật (có đếm, nhận vật, hiện số) | L2 | DemQua, TachSo2, phương trình, SentenceBuilder | bây giờ rải rác (`collectSlots`, `strip`) |
| Generator `phương-trình`, `từ-che-chữ`, `số-lượng` | L4 | F5, FamilySpelling, DemQua | sinh số học & ghép mẫu chữ |
| Bố cục **lưới 2D + Judge theo đường** | L2/L5 | WordHuntMaze | module nhỏ |
| **Lớp thưởng** (cược / jackpot / hộp quà) như Session plug-in | L6 | WhoIsIt | tuỳ chọn, bật/tắt |
| Hợp nhất schema F1⇄F2 | L2–L5 | giảm công bảo trì 2 engine | hợp đồng + test |
| **T2 Liên tục** (Spawner + va chạm + làn) | L3 | LaneDash, SaveTheAstronaut | tốn nhất; làm sau cùng, dạng module |
| **T3 / O5 module** (3D, camera, video) | L2 | SolarSystem, FRTest | không đưa vào builder, chỉ chọn module |

Tầng phủ: sau khi lấp 5 dòng đầu, builder dựng được **≈ 85% game** (mọi game T1) mà không viết C# mới.

## 10b. Thành phần của một game & ĐẶT Ở CẤP NÀO (Game · Dạng câu · Round · Đội · Đối tượng)

Nguyên tắc kế thừa: **Round ghi đè Dạng câu, Dạng câu ghi đè Game, Game ghi đè mặc định hệ thống** (như CSS). Mỗi thuộc tính chỉ nên *khai báo ở cấp rộng nhất còn đúng*, cấp hẹp chỉ ghi đè khi khác.

| Cấp | Là gì | Ví dụ thuộc tính | Hiện nằm ở |
|---|---|---|---|
| **Game** | cố định suốt phiên | tên · chế độ đội (`playMode`, `waitBothTeams`) · điều kiện thắng · thời gian · **Kho vật** · biến toàn cục · nền & Item trang trí (`layout.background/decorations`) · hiệu ứng/âm mặc định · font · `layerOrder` · `wrongEndsRound` · `answerMode` | F1 `fsData` gốc; F2 `settings` + `layout`; F3 `gameconfig.json` |
| **Dạng câu** (Flow/Mode) | một *kiểu* lượt, game có thể có nhiều, luân phiên | Generator (`gen`) · cách hiện (`views`/bố cục) · Judge · lời hỏi · khe phản ứng `on.*` · cảnh (`objects`) dùng riêng · `roundOrder` | F1 `flows[]`; **F2 không có (ngầm 1 dạng)**; F3 `QuestionType` Choose/Matching |
| **Round** | một lượt cụ thể (thủ công) hoặc sinh tự động | vật tham gia & đáp án đúng · `vars` · `prompt` · `sound` · `target` (SumToTarget) · bố cục/nội dung nhóm slot Câu hỏi + Đáp án · `iconCount` | F1 `rounds[]` (khi `manual`) hoặc Generator; F2 `rounds[]`; F3 `QuestionData` |
| **Đội** | mỗi bên chơi riêng | hạt giống (chung/riêng) · cấp độ · điểm · `mirror` (lật nửa phải) | F1 `sync`; F5 `TeamQ`; F2 `playMode` |
| **Đối tượng** | từng O1–O5 | skin · transform · bind · hành vi nền · trạng thái · `fx` của riêng nó | F1 `scene[]`; F2 slot/Item/`collectSlots` |
| **Runtime** (không soạn) | do engine sinh khi chơi | `Q` hiện tại · biến tính · trạng thái chạm · điểm đang có | `FlowWorld`/`GenericGameController` |

### Round = gì?
Round là **một thể hiện của Dạng câu**, gồm 3 phần:
1. **Đề** (Q): vật được chọn từ Kho, đáp án đúng, biến (`vars`), lời hỏi, âm, mục tiêu.
2. **Sân khấu của lượt**: bố cục & nội dung nhóm slot (số slot đáp án đổi theo round, hình dạng đổi theo round — F2), hoặc cảnh do Generator dựng (F1).
3. **Vòng đời**: bắt đầu (`setup/start`) → chạm (`step`) → đúng/sai (`right/wrong`) → kết thúc (`end`) → *chờ đội kia* (nếu `waitBothTeams`) → round kế.
Round có hai nguồn: **thủ công** (người soạn liệt kê) hoặc **tự sinh** (Generator + Kho vật + độ khó). Dạng nào cũng có thể **trộn**: Generator làm nền, vài round thủ công ghi đè.

### Thành phần → đặt ở đâu (đề xuất thống nhất, cho UI builder)
| Thành phần | Đặt ở cấp | Lý do |
|---|---|---|
| Kho vật (ảnh, tên, âm, tag) | Game | dùng chung mọi round/dạng |
| Nền, vật trang trí cả game | Game (ghi đè theo Dạng câu nếu cần) | F2 `layout.decorations`, F1 `scene` |
| Chế độ đội, thắng/thua, thời gian | Game | thuộc Phiên |
| Generator + Judge + Reaction mặc định | **Dạng câu** | đây là "luật của kiểu lượt" |
| Lời hỏi, âm câu hỏi | Dạng câu (mặc định) → Round (ghi đè) | hiện F1 đã vậy (`round.prompt/sound`) |
| Bố cục nhóm slot (số ô, vị trí, hình) | **Round** (mặc định lấy từ Dạng câu) | F2 đã vậy; F1 `views` |
| Đáp án đúng, vật tham gia, mục tiêu | **Round** | là dữ liệu của lượt |
| Biến, bind | Game (khai báo) · Round (giá trị) · Đối tượng (liên kết) | F1 `vars`/`round.vars`/`bind` |
| Hiệu ứng khi đúng/sai | Game (mặc định) → Dạng câu → **slot/Đối tượng** (ghi đè) | F2 khe `fx` trên slot, F1 `on.*` |
| Skin đối tượng, hành vi nền | Đối tượng | |

### Khoảng lệch hiện tại
- **F2 không có cấp Dạng câu**: 1 game GenericGame = 1 dạng; muốn luân phiên "chọn" ↔ "kéo thả" phải đẻ game khác. F1 có `flows[]` nhưng bố cục nhóm slot nằm trong `views`, không ở Round.
- **Hai nơi đặt Judge/`answerMode`**: F2 ở `settings` (Game), F1 ở `flow.judge` (Dạng câu) ⇒ khi hợp nhất, F2 `answerMode` = Judge của flow ngầm.
- **Reaction**: F2 gắn *trên slot* (cấp Đối tượng), F1 gắn *theo sự kiện* (cấp Dạng câu); cần hỗ trợ cả hai theo thứ tự kế thừa trên.
- **Round tự sinh vs thủ công**: UI v4.1 chỉ cho sửa vật/đáp án ở round thủ công; Generator thì ẩn tham số ⇒ người soạn khó thấy "round này ra từ đâu".

## 10c. Hướng "đối tượng + hiệu ứng + trigger → flow": đánh giá & thiết kế

**Kết luận: đúng hướng, và là điểm hội tụ tự nhiên của hai engine hiện có** — F2 đã có *hiệu ứng gắn trên đối tượng* (4 khe `fx` trên slot, `onClick/onCorrect/onPartial/onWrong` trên Item), F1 đã có *sự kiện → hành động* (`on.start/setup/step/right/wrong/end` + 19 action + biến/biểu thức). Mỗi bên chỉ có một nửa: F2 sự kiện cố định 4 khe, không có điều kiện/biến/chuỗi; F1 gắn ở cấp flow chứ không ở đối tượng. Hướng mới = **sự kiện mở + gắn trên bất kỳ đối tượng + chuỗi được**.

Cần chỉnh một chỗ để khỏi rối: "vùng câu hỏi > câu hỏi > đáp án" đang trộn **ba cây khác nhau**:

| Cây | Nội dung | Quan hệ | Ví dụ |
|---|---|---|---|
| **A. Cây dữ liệu** (logic) | Game > Round > **Câu hỏi** (1) > **Đáp án** (n, có `correct`) | sở hữu / thuộc về | đáp án 2 là đúng của câu hỏi này |
| **B. Cây cảnh** (hiển thị) | Viewport > **Vùng** > Slot/Đối tượng | chứa / bố cục | Vùng Đáp án chứa 4 slot xếp quạt |
| **C. Đồ thị hành vi** | Sự kiện → Trigger → Hiệu ứng/Action → (Sự kiện mới) | kích hoạt | chạm đúng → bay → hết bay → round kế |

"Câu hỏi" và "Đáp án" là **vai trò dữ liệu** (cây A); "Vùng" là **đối tượng hiển thị** (cây B, loại O4). Hai cây gặp nhau ở **liên kết dữ liệu** (`bind`): slot câu hỏi ← `round.question`; Vùng Đáp án ← `round.answers` ("mỗi đáp án một slot theo mẫu"). Tách như vậy mới giải quyết được: số đáp án mỗi round khác nhau, một đáp án hiện ở nhiều nơi, Generator tự sinh round mà không đụng cảnh.

### Mô hình hành vi (cây C)
- **Sự kiện** `{tên, nguồn, dữ liệu}` — do *đối tượng hoặc nền tảng* phát ra.
- **Trigger** `{on: sự kiện, from: bộ chọn, if: điều kiện?, do: [bước...]}` — gắn trên một **đối tượng hay một phạm vi** (đối tượng · Vùng · Round · Game). Gắn ở phạm vi rộng thì kế thừa xuống, hẹp ghi đè (§10b).
- **Bước** (step): *Hiệu ứng* lên một đích (`đích, thời lượng, tham số`) · *Action* (đặt biến, cộng điểm, phát âm, đổi trạng thái, phát sự kiện, round kế…) · *Chờ* một sự kiện (`await`) · nhóm **song song / nối tiếp** (như "With previous / After previous" của PowerPoint).
- **Bộ chọn** `this · question · answers[*] · answers[correct] · answers[tapped] · region:X · tag:Y` — gắn trigger lên *vai trò* thay vì từng phiên bản, nên mẫu "mỗi đáp án" áp cho cả 4 (hoặc 7) slot dù số slot đổi theo round.
- **Flow** = tập trigger *thực sự chạy trong một lượt*, hiển thị thành **dòng thời gian suy ra** (không ai vẽ tay): `Bắt đầu │ Tương tác │ Đúng │ Sai │ Kết thúc`.

### Bộ sự kiện chuẩn (suy ra từ cả 8 họ)
| Phạm vi | Sự kiện | Ai phát |
|---|---|---|
| Game | `game.start · game.timeout · game.win · game.end` | nền tảng |
| Round | `round.setup · round.start · round.right · round.wrong · round.complete · round.end · round.next` | nền tảng (+ Judge) |
| Đối tượng | `appear · idle · tap · tap.correct · tap.wrong · selected · deselected · removed · state.changed · hover*` | nền tảng + Judge |
| Chứa/Nhóm | `receive (nhận 1 vật) · full · count.changed` | O3/O4 |
| Biến | `var.changed(tên)` | engine |
| Thời gian | `timer(sau N s / mỗi N s)` | engine |
| Thực kiện (T2) | `overlap(a,b) · spawn · exit-screen` | module |
| Thông điệp | `emit(tên)` do người soạn tự phát | trigger |
*(`hover` không có — LiDAR chỉ là tap; chỉ ghi để tránh thiết kế nhầm.)*

### Hai thứ KHÔNG nên là trigger
1. **Judge** (chấm đúng/sai) là **thành phần khai báo** (`bằng / thứ-tự / cặp / tập-đủ / tổng / đường`) đặt trên Vùng đáp án hoặc Round; nó *đọc* các `tap` rồi *phát* `tap.correct/wrong`, `round.right/wrong/complete`. Nếu bắt người soạn tự nối đồ thị để chấm thì mỗi game đều phải dựng lại cùng một đống dây và dễ sai.
2. **Bind** (trạng thái → thuộc tính, vd kim đồng hồ theo biến) là **phản ứng liên tục**, khác trigger (**tức thời**). Giữ riêng: `bind` = khai báo, `trigger` = mệnh lệnh. (F1 `bind/smooth` đã đúng thế.)

### Vòng đời mặc định do nền tảng phát (người soạn chỉ gắn thêm)
```
round.setup → [Generator sinh Q, dựng slot, bind] → round.start
   ↘ object.tap → [Judge] → tap.correct | tap.wrong → (round.right | round.wrong | round.complete)
round.complete → round.end → [chờ đội kia nếu waitBothTeams] → round.next | game.win
```
Mỗi sự kiện có **trigger mặc định** (✔/✖ + âm hệ thống, FlyTo cho đáp án đúng) — là lớp gốc của kế thừa, người soạn không cần làm gì cũng chạy.

### Phác schema (game "Chọn quả")
```
data:   round.question={image,sound}  round.answers=[{item, correct}]      // cây A (Generator sinh)
scene:  Vùng Q (bind=round.question)  ·  Vùng A (each answer → slot mẫu, arrangement=quạt)   // cây B
judge:  Vùng A: equals
triggers (cây C):
  round.start         → Q: Breathing ; Q.sound: play
  answers[*].tap.correct → this: FlyTo(Q) + ✔ ; Q: Punch ; score +1
  answers[*].tap.wrong   → this: Shake + ✖
  round.complete      → chờ 0.8s ; round.next
```
`DemQua` khác ở chỗ nào? `judge: set (tập-đủ)` · Vùng B (rổ) `receive/count.changed` · trigger `fruits[*].tap → this: FlyToStay(basket.next) ; basket.count++` · `basket.full → say(count)`. Cùng cơ chế, không thêm loại mới.

### Thử mô hình trên các họ
| Họ | Mô hình có đủ không? | Phần cần thêm |
|---|---|---|
| F1, F2 | ✔ — chính là hợp nhất hai bên | gộp tên sự kiện; `fx` slot → trigger `this` |
| F3, F4 | ✔ | không |
| F5 phương trình | ✔ | Generator + Vùng nhóm `A+B=C` |
| F6 DemQua/Sentence | ✔ | O3 `receive/full`, Judge `tập-đủ/thứ-tự` |
| F6 WordHunt | ✔ | Judge `đường`, lưới 2D |
| F6 WhoIsIt (cược/hộp quà) | ✔ nhờ `await` | Vùng modal + `await(modal.closed)` |
| F7 LaneDash/Astronaut | ✔ về mô hình (`timer` + `spawn` + `overlap`) | engine thực kiện/va chạm — module |
| F8 SolarSystem | ✔ (`tap → mở panel`) | 3D là module |
Tức: **mô hình sự kiện phủ được mọi họ; phần khó là Judge/Generator/module, không phải trigger.**

### Rủi ro (cần chốt trước khi làm)
1. **Khó đọc khi trigger rải khắp nơi** → bắt buộc có *Dòng thời gian Flow* suy ra từ trigger (xem theo sự kiện, không theo đối tượng) + chế độ lọc theo đối tượng.
2. **Thứ tự & đồng thời** → quy tắc cố định: trigger cùng sự kiện chạy theo *phạm vi hẹp → rộng*, cùng phạm vi theo thứ tự khai báo; `stopPropagation` để chặn mặc định; **cấm vòng** (`emit` có độ sâu tối đa).
3. **Web ⇄ Unity phải trùng thứ tự sự kiện** → cả hai engine ghi **vết sự kiện** (trace) và so sánh theo cùng seed, mở rộng test `roundcmp.js` hiện có.
4. **Độ phức tạp cho giáo viên** → 3 mức hiển thị: (1) *Dạng mẫu* (chọn "Khi đúng: Bay lên" từ danh sách) → (2) *Danh sách hiệu ứng theo khe* (như F2 hiện nay) → (3) *Trigger đầy đủ* (điều kiện, chuỗi, `await`). Mức 1–2 sinh ra trigger của mức 3, để một dữ liệu duy nhất.
5. **Trùng từ "flow"**: hiện `flow` = *Dạng câu* (F1). Đề xuất đổi: **Dạng câu** (Mode) · **Luồng** (tập trigger) để khỏi lẫn.
6. **K02 yếu** → trigger rẻ; chi phí nằm ở hiệu ứng ⇒ dùng pool, giới hạn hiệu ứng đồng thời.

### Hệ quả cho UI builder (hướng đích)
Ba bảng cạnh nhau: **Cây** (A+B: Round › Câu hỏi › Đáp án ‖ Viewport › Vùng › Slot) · **Thuộc tính** (§5: skin, transform, bind, trạng thái, bố cục) · **Luồng** (trigger theo sự kiện, 3 mức hiển thị). Thêm mới = chọn loại đối tượng (O1–O5) + vai trò, không có "loại game". Cách này gần PowerPoint (cây + thuộc tính + Animation Pane) — hợp với giáo viên.

## 10d. Lập trình kéo thả: cái gì kéo, cái gì chọn, cái gì phải là mã

Kiến trúc: **Builder kéo thả (Kho vật · Cây cảnh · Thuộc tính · Luồng khối) → `game.json` một schema (Kho vật · Round · Cảnh · Judge · Trigger · Phiên) → Runtime (Generator · Cảnh+bind · Judge · Trigger) trên nền tảng (2 đội · HUD · tap LiDAR · âm · điểm · log)**. Web `fs/*.js` ⇄ Unity `FlowWorld` chạy cùng schema, so bằng vết sự kiện theo seed.

| Nhóm | Thao tác | Gồm | Ghi chú |
|---|---|---|---|
| **A. Đối tượng** | **Kéo** từ Palette vào Cây cảnh | Hình/Ảnh · Chữ · Nền · **Vùng** (hàng/lưới/quạt/ngẫu nhiên/tay) · **Slot câu hỏi** · **Slot đáp án** · **Ô chứa** · **Mẫu dựng sẵn** (Bập bênh, Đồng hồ, Đống khối, Rổ, Bảng chữ, Phương trình, Modal cược) | Viewport 2 đội có sẵn. Mẫu dựng sẵn = nhóm đối tượng + bind + trigger lưu sẵn, mở ra vẫn sửa được |
| **B. Thuộc tính** | **Kéo** vật/ảnh/âm từ Kho vào ô; **kéo tay cầm** đổi vị trí/cỡ/xoay; **chọn** bind, trạng thái, bố cục | skin · transform · gắn-vào (parent) · bind · trạng thái · hành vi nền | Biểu thức bind phức tạp chỉ ở ô "nâng cao" |
| **C1. Khối Sự kiện** (đầu khối) | Kéo vào Luồng | `Khi round bắt đầu / chạm đúng / chạm sai / đủ / hết giờ / biến đổi / sau N giây / nhận thông điệp` | chọn *nguồn* bằng bộ chọn (`this`, `đáp án[*]`, `vùng X`) |
| **C2. Khối Làm** (xếp chồng) | Kéo dưới khối sự kiện | **Hiệu ứng** (bay tới, bay đi, lắc, nảy, nhịp thở, mờ, xoay, đổi màu, hiện/ẩn, sinh vật) · **Action** (đặt/tăng biến, cộng điểm, phát âm, đổi trạng thái, báo thông điệp, round kế, kết thúc) | mỗi khối có ô: đích, thời lượng, tham số |
| **C3. Khối Điều khiển** (bao khối khác) | Kéo bao quanh | `Nếu… thì…` · `Song song` · `Lặp n lần` · `Chờ sự kiện` · `Đợi N giây` | không cho vòng vô hạn |
| **C4. Khối Giá trị** (thả vào ô tham số) | Kéo vào ô | thuộc tính vật (`value`, `tên`) · biến · `round.target` · ngẫu nhiên · đội | thay cho gõ biểu thức |
| **D. Thẻ chọn** | **Chọn thẻ + điền tham số** (không kéo) | **Generator** · **Judge** · **Phiên** (đội, thắng, độ khó, thưởng) · thứ tự/luân phiên Dạng câu | danh mục đóng, an toàn, kiểm thử được |
| **E. Không kéo được** | mã / module | Generator hoặc Judge *mới ngoài danh mục* · vật lý/va chạm · 3D/camera · nhận diện mặt · video | đăng ký module một lần, builder chỉ chọn + tham số |

Nguyên tắc: **cái gì thường thay giữa các game → khối/thẻ; cái gì cố định và cần kiểm thử chặt → thẻ danh mục; cái gì ngoài khuôn → module.** Ba mức hiển thị một dữ liệu: (1) *chọn mẫu* ("Khi đúng: Bay lên") → (2) *danh sách hiệu ứng theo khe* (như F2) → (3) *khối đầy đủ*.

## 10e. Ví dụ: game **Nặng – Nhẹ** (`NangNhe`, FloorStory)

**Luật chơi (đọc từ `fs/games/NangNhe.json` + `fs_flow.js` `values` / `seesaw`)**: bốc 2 (rồi 3) con vật có `value` khác nhau (1 nhẹ · 2 vừa · 3 nặng); các con đứng trên bập bênh, bên giá trị lớn chúi xuống; hỏi con nặng nhất hoặc nhẹ nhất (`less`); đội chạm đúng con; `judge = equals`, chờ `finishSec 2.4` rồi sang lượt kế. 3 vật = 2 bập bênh nối nhau. Không có chữ trên màn hình (`text` rỗng) — đề bằng âm `round.sound`.

### Phân rã theo 6 chỗ cắm
| Chỗ cắm | Giá trị của Nặng–Nhẹ |
|---|---|
| **Content** | 18 con vật (ảnh tròn, tên), mỗi con có `value` 1/2/3 |
| **Generator** | `values`: bốc 2→3 vật khác `value`; `target` = vật lớn nhất, hoặc nhỏ nhất khi `less`; biến `word` (nặng/nhẹ) |
| **Presentation** | xem bảng đối tượng bên dưới |
| **Judge** | `equals` (1 chạm trúng `target`) |
| **Reaction** | khi bắt đầu: hiện vật → lắc → nghiêng (+ âm gỗ); chạm đúng: ✔ + âm; chạm sai: ✖ + âm |
| **Session** | 2 đội, mỗi đội câu riêng (`sync:false`), nền `#E1F5FE` + sàn `#A5D6A7` |

### Đối tượng (cây cảnh) và vai trò dữ liệu
| Đối tượng | Loại | Vai trò dữ liệu | Thuộc tính đáng chú ý |
|---|---|---|---|
| Nền trời + sàn | O1 | — | màu theo `theme` |
| **Nhóm "Bập bênh"** ×1 (hoặc ×2) | O4 | — | mẫu dựng sẵn; vị trí pivot theo số vật |
| Trụ (tam giác) | O1 | — | skin hình học, màu nâu |
| **Ván** | O1 | — | `rot` = **bind** theo giá trị 2 vật; có bước lắc ±2.5° rồi nghiêng ±15° |
| **Vật trái / Vật phải** | **O2** | `Đáp án` (1 trong số là `target`) | skin ảnh tròn + nhãn tên (2 vật); **gắn vào ván** (parent + vị trí cục bộ) nên nghiêng theo ván |
| Viewport mỗi đội | O5 | — | nền tảng |
| (Hạt ✔/✖, âm) | Hiệu ứng | — | mặc định hệ thống |

### Trigger (cây hành vi)
| Sự kiện | Điều kiện | Làm |
|---|---|---|
| `round.start` | — | vật: nảy xuất hiện (0.35s, song song) → chờ 0.5s → ván: lắc 0.7s → phát âm gỗ → ván: nghiêng tới góc (0.9s, ra-vào mềm) → mở khoá chạm |
| `đáp án[*].tap.correct` | — | ✔ + âm đúng · vật: nảy/bay · `score +1` |
| `đáp án[*].tap.wrong` | — | ✖ + âm sai · vật: lắc |
| `round.complete` | — | chờ 2.4s → `round.next` |
| *(bind, không phải trigger)* | luôn | `ván.rot ← dấu(value[trái] − value[phải]) × 15°` |

### Kéo thả: dựng lại từ đầu thế nào
1. Kéo **Mẫu "Bập bênh"** vào Cây cảnh (đã gồm Trụ, Ván, 2 slot vật, bind `rot`, trigger mở màn).
2. Ở **Kho vật** thêm cột `value`, nhập 1/2/3 cho từng con (kéo ảnh vào).
3. Thẻ **Generator** chọn "Bốc vật khác giá trị" → số vật 2→3, hỏi *lớn nhất / nhỏ nhất* xen kẽ.
4. Thẻ **Judge** chọn "Chạm đúng 1 vật".
5. Ở **Luồng**, giữ trigger mặc định (✔/✖ + âm); thêm khối "Khi chạm đúng → vật Nảy" nếu muốn.
6. Chơi thử, xuất zip.

### Tổng quát hoá từ ví dụ này
- Đổi **nội dung + lời hỏi** (không đổi cảnh) → **Cao–Thấp**, **Lớn–Bé**, **Nhiều–Ít**: cùng Generator `values`, chỉ khác cột giá trị và âm đề.
- Đổi **cảnh** (thay mẫu Bập bênh bằng "hai cột" hoặc "hàng thẻ") → **cùng game** với trình bày khác: bằng chứng rằng `Generator` và `Presentation` độc lập.
- Đổi **Judge** sang `thứ-tự` → "xếp từ nhẹ đến nặng" (chạm lần lượt), không đụng Content.

### Khoảng lệch cần lấp để dựng game này *hoàn toàn* bằng kéo thả
| Hiện trạng | Thiếu |
|---|---|
| `seesaw` là **view viết cứng** trong `fs_flow.js` / Unity | **Mẫu dựng sẵn** + **gắn-vào (parent)** + **bind xoay có làm mượt/ease** |
| Lắc → nghiêng là mã trong `intro()` | Chuỗi trigger `round.start` có **chờ/song song** (đã có `rotate`, `wait`; cần xác minh `ease`) |
| Khoá chạm tới khi nghiêng xong là cờ `V.ready` | Trạng thái `Locked`/`Normal` của O2 do trigger đặt |
| `values` có sẵn nhưng tham số (2→3 vật, xen kẽ lớn/nhỏ) nằm trong cấu hình flow | Hiện thành thẻ Generator trong UI |

## 11. Lộ trình đề xuất (thứ tự theo giá trị/rủi ro)
- **P0 — Chốt ontology**: user duyệt §2–§5, §10; đổi tên trong UI cho khớp (Câu hỏi / Đáp án / Ô chứa / Nhóm / Hiệu ứng).
- **P1 — Phơi Judge + Reaction mặc định** trong UI cho game chọn: 3 luật (`bằng/thứ-tự/tập-đủ`) + khe phản ứng; không đổi engine.
- **P2 — O3 Ô chứa + Generator số-lượng / phương-trình / từ-che-chữ**: mở khoá DemQua, F5, FamilySpelling, SentenceBuilder.
- **P3 — Hợp nhất schema F1⇄F2** + importer GenericGame → flow; giữ test seed.
- **P4 — Session plug-in** (độ khó thích nghi, lớp thưởng) + lưới 2D.
- **P5 — Module T2/T3** (LaneDash, SaveTheAstronaut, SolarSystem) có tham số, chung L0/L1/L6.
Mỗi bước: viết một game cũ lại bằng builder làm **bài kiểm nghiệm chấp nhận** (ví dụ P2 = dựng lại DemQua và FamilySpelling chỉ bằng UI, so với bản code).

## 12. Rủi ro / cần kiểm chứng trước khi làm
- Luật chấm và kho nội dung của F3–F8 mới đọc ở mức field (§9) — P1/P2 cần đọc từng dòng logic của game đích (vd `PlanetOrder` sai thì reset thế nào).
- Hai runtime hợp nhất có thể làm vỡ game đã xuất ⇒ giữ `schemaVersion` và đường nhập cũ.
- `HaiQua` và 7 game đóng băng không nằm trong lộ trình; nếu sau này mở lại, chúng cũng rơi vào khung 6 chỗ cắm nhưng chưa phân tích.
