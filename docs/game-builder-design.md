# Thiết kế lại Game Builder — một tool thống nhất, kiểu PowerPoint + lập trình kéo thả

> **TL;DR**: Game builder (`WebTools/GenericGameBuilder/`) là tool **dựng game lẻ**: Tạo/Mở → thêm ảnh, chỉnh → ▶ Chơi thử full màn hình → ⇩ Xuất zip. Bản v2 cũ là chuẩn. Cơ chế FloorStory, Big Brain Academy, game chiếu sàn và game cổ điển (đào vàng, magic tile, chạy tại chỗ…) đều là **tính năng bổ sung** trong cùng tool. Kiến trúc: **8 lớp độc lập**, trong đó có **lớp mô phỏng thời gian thực** (mới) để làm game động.
> **Đọc khi**: sắp làm/sửa builder, thêm cơ chế mới, quyết định có đưa tính năng X vào builder không.
> **Cập nhật**: 2026-10-10 — bản nháp thiết kế; **đợt 1 đã làm (Builder v4.0): xem mục 12.** Thay cho hướng `fsd-mode` tách riêng đã làm trước đó. Engine `FSW`/`FlowWorld` (xem `floor-story-mechanics.md`) vẫn dùng lại làm nền.

## 1. Quyết định đã chốt (user, 2026-10-10)
- Builder chỉ dựng game **lẻ**. **Không** có hub/bộ sưu tập; **luật trận** (Sprint, Marathon, Party, luân phiên…) nếu có chỉ làm trong Unity.
- Mục tiêu: **phủ cơ chế** để tool sẵn sàng, rồi hoàn thiện game dần theo ưu tiên; học theo game đã chứng minh hấp dẫn (Big Brain Academy, floor-projection, game cổ điển).
- Giao diện học PowerPoint: trung tâm "Thiết kế game", thanh công cụ trên (cài đặt chung), panel phải (cài đặt riêng).
- Kiến trúc: các lớp độc lập + hook liên kết round. **Đưa lớp thời gian thực vào thiết kế trước** khi làm mô-đun.
- Bỏ qua BBA: Order Out, Pattern Puzzle. Add Agency = game cộng đã có. Get in Shape = bóng đoán vật. True View = khối 3D xoay hỏi góc nhìn.

**Phản hồi sau khi xem bản phác (2026-10-10), đã chốt:**
- **Người dùng là giáo viên** → logic sự kiện phải thật đơn giản, kiểu Scratch: câu chữ đời thường, chọn từ danh sách, không lộ biến/biểu thức. Phần logic (biến, điều kiện, hành vi chuyển động, luật, thời gian thực) tách độc lập thành mục **Nâng cao**; giáo viên nhìn vào không được thấy sợ.
- **Mọi ô chữ đều chỉnh được định dạng**: cỡ, màu, đậm, nghiêng, viền, bóng (và căn lề, font khi Unity hỗ trợ).
- **Game mới = chọn từ thư viện game mẫu** (các game điển hình đã làm), rồi sửa.
- **Mở/Lưu/Xuất gom vào menu File** (như PowerPoint); nút **▶ Chơi thử luôn hiện, ngoài cùng bên phải, cùng hàng với thanh công cụ** → bỏ hàng tiêu đề riêng.
- **Âm thanh: chỉ cần thêm file**, không dùng giọng đọc của trình duyệt; tối ưu sau.
- Tool đặt tên **Game Builder** (đợt 1 là v4.0; bản cuối hiện tại **v4.17** — `game_builder_v4.17.html`).

**Giả định, cần xác nhận** (user chưa trả lời): (a) mỗi round chạy bằng engine hợp với loại của nó, chưa gộp 1 engine; (b) công cụ buổi học (chia đội/đồng hồ/vòng quay) ngoài phạm vi; (c) "phản ứng nền ở điểm chạm" và "chế độ giác quan" ưu tiên cao; (d) ưu tiên nhóm **pad** (vận động) hay **va chạm/bắn** trước.

## 2. Ràng buộc thiết bị: chỉ có tap
LiDAR sàn chỉ cho **tap** (`project_lidar_touch_tap_only`). Không kéo/vuốt/giữ/chụm/vẽ/hover/bàn phím. Điểm chạm lệch (chân/tay), 2 đội chạm đồng thời, có độ trễ, trẻ chưa đọc được. Tool **không cho tạo tương tác thiết bị không làm được**:

| Muốn | Dùng |
|---|---|
| Kéo vật vào chỗ | Chạm vật → chạm ô đích (`pick-place`) |
| Xoay kim, trượt thanh | Chạm xoay vòng giá trị / nút +− (`cycle`) |
| Giữ để chọn | Chạm bật/tắt (`toggle`) |
| Vẽ đường (Pathfinder) | Chọn lối ra + animation chạy đường |
| Đèn pin soi (Species Spotlight) | Cảnh tĩnh + đếm |
| Vuốt (Fruit Ninja), ngắm kéo | Chạm vật; chạm vào đâu bắn về đó |
| Gõ chữ | Ô chữ chạm để điền |
| Vận động thật | Pad: chạm xen kẽ hai vùng; hoặc thẻ lệnh + hẹn giờ, giáo viên bấm "xong" |

Đề chữ phải có **giọng đọc hoặc hình**. Phạt sai nhẹ. Tốc độ chấm thô (độ trễ LiDAR). Ngưỡng kích thước chạm/tốc độ tối đa: **chưa có số đo**, lấy từ calib (`docs/lidar-calib.md`).
**Chưa kiểm chứng trên K02:** một bước chân lên pad cho mấy sự kiện chạm? Chân đặt yên có sinh sự kiện lặp không? Khoảng cách tối thiểu giữa hai bước? Phải đo (scene thử pad) trước khi chốt mô-đun `pad`.

## 3. Mô hình khái niệm
| PowerPoint | Builder |
|---|---|
| Bộ slide | **Game**: kho tài nguyên, theme, biến toàn cục, danh sách round |
| Slide | **Round** |
| Đối tượng | **Object** (ảnh, chữ, hình, nhóm ô đáp án, thẻ, bảng ô, đồng hồ, vật động…) |
| Slide master/layout | **Round mẫu**: round khác `extends` nó |
| Animation pane | **Dòng thời gian** của round |
| Transition | **Chuyển round** (thứ tự, điều kiện, mang trạng thái) |

FloorStory không phải một loại game: chỉ là nhóm game có round kế thừa/chuyển tiếp. Mỗi **round** thuộc một trong ba kiểu, nằm cùng một Game:
- **Lượt (turn)**: nguồn → hiển thị → chấm theo từng câu (kiểu hiện tại).
- **Thời gian thực (realtime)**: mô phỏng chạy liên tục, kết thúc bằng luật (thắng/thua/hết giờ).
- **Kết hợp**: cảnh thời gian thực có câu hỏi `Q` bên trong (vd đào vàng mỗi vàng ứng một phép tính).

## 4. Giao diện
```
┌ Ribbon: [Game] [Chèn] [Hiệu ứng] [Chuyển round] [▶ Chơi thử] [⇩ Xuất] ┐
├ Danh sách ┬──────────────────────────────┬ Thuộc tính (theo vật đang chọn) ┤
│ round     │  Sân khấu "Thiết kế game"    │ Hiển thị | Dữ liệu | Chuyển động│
│ (thumb,   │  kéo thả, chọn, căn, lớp     │ | Hành vi | Chấm | Kiểm tra     │
│ mũi tên   │  vạch 2 đội, vùng chạm tối   │ (form tự sinh từ schema mô-đun) │
│ chuyển)   ├──────────────────────────────┤                                  │
│           │ Dải dưới: Thời gian / Sự kiện│                                  │
└───────────┴──────────────────────────────┴──────────────────────────────────┘
```
- **Tab Game** của ribbon thay panel trái cũ: nền, nhạc, 2 đội/sync, theme, biến toàn cục, kho ảnh/vật.
- **Chơi thử** = overlay full màn hình (`#playtest`) như cũ, cho mọi kiểu round. Bật **lớp gỡ lỗi** khi chơi thử: khung va chạm, số vật động, `$t`, tốc độ khung hình.
- **Tab Chuyển động** (vật động): thân (vận tốc, trọng lực), hành vi (đung đưa, trôi, bay theo đường…), nhóm va chạm, vòng đời. **Dải dưới** hiện thêm bộ hẹn giờ/spawner.
- **Tab Kiểm tra** (Design Checker): lớp phủ vùng chạm tối thiểu, vạch 2 đội, cảnh báo vật/chữ nhỏ, thao tác không phải tap, **ngân sách mô phỏng** (số vật động, vật tốc độ cao hơn độ trễ LiDAR).
- Sự kiện–điều kiện–hành động là khối có form; đích chọn bằng cách **bấm trên sân khấu**; JSON chỉ ở tab Nâng cao.
- Game v2 cũ mở ra thành round thủ công, mọi thứ cũ phải còn chạy.

## 5. Kiến trúc: 8 lớp độc lập
Mỗi thành phần có **schema tham số** (nhãn, kiểu, mặc định, loại tham chiếu: vật/biến/object/biểu thức) → panel phải tự sinh form. **Thêm cơ chế mới = đăng ký một mô-đun** (hiện `MOD` mới có mặc định, phải bổ sung nhãn/kiểu).

| Lớp | Gắn vào | Nội dung |
|---|---|---|
| 1 Nguồn | Round | thủ công · bảng/CSV · sinh tự động (`set match calc values sequence pairs number …`) · rải vật (`scatter`) · lấy từ round trước |
| 2 Hiển thị | Object | object thông minh gắn nguồn bằng `bind`; kiểu `line` (dây nối) |
| 3 Kịch bản thời gian | Round/Object | hiện lần lượt → giữ → úp/ẩn; chuỗi hành động có thứ tự |
| **4 Mô phỏng thời gian thực** | Round/Object | đồng hồ, thân/hành vi, va chạm, spawner (mục 5.1) |
| 5 Input | Object | từ vựng tap (mục 6) |
| 6 Chấm / luật kết thúc | Round | judge theo lượt hoặc luật `when → win/lose/end` |
| 7 Phản hồi | Round/Object | sự kiện → hành động; hiệu ứng nền |
| 8 Trạng thái | Game/Round | biến (giữ qua lượt, mỗi đội riêng), cảnh bền, bảng "cấp → tham số" |

**Độ khó** là dữ liệu hạng nhất: bảng `cấp → tham số` cho gen/sim; điều chỉnh bằng biến (`$level`) qua hành động (`on.right: inc level`), không cần bộ điều khiển riêng.
**Kỹ năng** gắn thẻ trên round để nối vào báo cáo năng lực (`docs/danh-gia-nang-luc`).
**Log**: mọi judge/luật phát sự kiện chuẩn vào GameLogger (Click → Round → Session); không tuỳ chọn.

### 5.1 Lớp mô phỏng thời gian thực (Sim)
**Nguyên tắc**: mô phỏng **động học** (kinematic) và xác định (deterministic), không phải vật lý rigid-body.

- **Đồng hồ**: bước cố định (mặc định 60 Hz), cộng dồn `dt` thật. Biến luôn có: `$t` (giây từ đầu round), `$dt`, `$tick`; mỗi vật có `self.age`. Cùng seed + cùng nhật ký input → cùng kết quả (dùng để test và phát lại).
- **Thân (body)** của vật: `vx vy`, gia tốc/trọng lực, `spin`, tốc độ + hướng, `life`, giới hạn sân (`none|bounce|wrap|kill|clamp`), nhóm va chạm (`group`), hình va chạm (tròn hoặc chữ nhật).
- **Hành vi (behavior)** gắn vào vật, mỗi hành vi có điều kiện `when` (nhờ đó biến `state` của vật làm máy trạng thái nhỏ, không cần khái niệm thêm): `move` (vận tốc/gia tốc) · `swing` (đung đưa quanh trục: tâm, dài, biên độ, tốc độ → lộ `self.angle`) · `path` (đi theo điểm/ease) · `scroll` (làn trôi) · `bounce` (nảy tường) · `follow`/`attach` (đi theo vật khác, vd móc kéo vàng) · `wander`.
- **Spawner**: tốc độ sinh, khu vực/làn, mẫu vật, giới hạn số lượng, `pattern` (bpm + làn × phách) để sinh theo nhịp, dùng chung nguồn ngẫu nhiên có seed.
- **Sự kiện mới** (nối vào bảng sự kiện lớp 7): `tick` · `every n giây` · `after n giây` · `timer(tên)` · `overlap(A,B)` / `leave` · `enterZone` · `outOfBounds` · `reach` · `lifeEnd` · `varChange` · input: `tap` (kèm vị trí), `tapObj`, `pad(tên)`.
- **Hàm biểu thức mới** (thuần, JS = C#): `dist(a,b)` · `angleTo(a,b)` · `near(id,r,nhóm)` · `nearest(nhóm)` · `count(nhóm)` · `inZone(id,vùng)` · `rate(khoá,cửa sổ)` (sự kiện/giây) · `since(khoá)` (giây từ lần cuối) · `lerp` · `clamp`.
- **Hành động mới**: `spawn` mở rộng (thân + hành vi + nhóm) · `shoot(từ, hướng/tới điểm, tốc độ, nhóm)` · `setBody` · `attach/detach` · `timer/cancel` · `emit(sự kiện tuỳ ý)` · `kill(nhóm)`.
- **Luật kết thúc** (lớp 6): danh sách `when biểu-thức → win | lose | end` (vd `$score>=$target`, `$lives<=0`, `$t>=60`). Chung cho round thời gian thực.
- **Phát hiện chạm**: chỉ tròn/chữ nhật căn trục; bắt đầu bằng so từng cặp có giới hạn số vật, thêm lưới băm khi cần. Không xoay hộp, không lực, không khối lượng, không xếp chồng.
- **Ngân sách K02** (MT8168 yếu): giới hạn cứng số vật động (đề xuất ≤ ~60, chốt sau khi đo), không cấp phát mỗi khung hình ở Unity, cập nhật sprite chỉ khi đổi. Tab Kiểm tra cảnh báo khi vượt.
- **Chạy cùng hai bên**: một lõi `Sim` ở `fs/` (JS) và một ở `FloorStoryGame/Flow/` (C#). Số học dùng `double`. **Test parity**: chạy kịch bản input cố định N tick ở cả hai, so trạng thái vật với sai số nhỏ.
- **Tương thích**: round kiểu lượt không chạy Sim; round kết hợp dùng cả hai. Lượt `Q`, `bind`, `anim` hiện có không đổi.

### 5.2 Hook liên kết round
- `extends`: round con lấy object/chấm/phản hồi của round mẫu, ghi đè phần khác.
- `carry`: khai báo biến/đối tượng giữ sang round sau (cảnh bền của FloorStory).
- Đầu ra/đầu vào: round xuất `out.*`, round sau đọc `$prev.*`.
- Vòng đời: sự kiện `round.enter / round.exit / round.next`, hành động `goto` / `endRound`.
- Quy tắc chuyển: tuần tự · có điều kiện · ngẫu nhiên trong nhóm · lặp n lần. (`flowPick` hiện tại là bản sơ khai.)
- Round mẫu lặp n lượt có nguồn tự sinh, giữ trạng thái giữa lượt = một game FloorStory hiện nay.

## 6. Thư viện cơ chế (catalog)
Ký hiệu: ✔ đã có · ＋ cần thêm. Nguồn gốc: BBA = Big Brain Academy, FP = game chiếu sàn, CD = game cổ điển.

**Input (lớp 5)**: ✔ chạm vật, chuỗi, đếm lượt chạm (kèm vị trí `tap`), lật cặp · ＋ `pick-place` · ＋ `cycle/toggle/+−` · ＋ `hotspot` · ＋ `buzz` (nhanh nhất thắng) · ＋ `timed-tap` (cửa sổ thời gian, nhịp) · ＋ `avoid` (vật cấm) · ＋ `reveal-tile` · ＋ `claim-tile` · ＋ **nhóm pad**: `pad` (vùng chạm lớn có tên, mỗi đội một bộ), `alternate` (chỉ tính khi khác pad trước), `chord` (hai pad cùng lúc = nhảy), `rate` (bước/giây).

**Hiển thị (lớp 2)**: ✔ cards, props, strip, grid, seesaw, stack, floor · ＋ `board` (bảng ô có trạng thái) · ＋ `palette` · ＋ `clock` · ＋ `voxel` (khối 3D xoay + chiếu theo hướng nhìn; True View, Block Spot) · ＋ `frame` (tranh có vùng khuyết) · ＋ `track` (đường đua theo đội) · ＋ `meter` · ＋ `line` (dây nối) · ＋ `lanes` (làn trôi + vạch chạm) · sau: `piano`, hotspot map, bàn cờ.

**Chấm / luật (lớp 6)**: ✔ equals, order, pairs, taps · ＋ `order`.`reverse` · ＋ `match-key` (so với mẫu hoặc `sum==n`) · ＋ `expr` · ＋ `fill` · ＋ `sim` (chạy quy tắc trên lưới/đồ thị rồi so đáp án) · ＋ `first` · ＋ `forbid` · ＋ `rules` (luật `when → win/lose/end`).

**Nguồn (lớp 1)**: ✔ các gen hiện có · ＋ bảng/CSV · ＋ `grid` · ＋ `voxel` · ＋ `delta` · ＋ `silhouette` · ＋ `scatter` (rải vật không chồng nhau).

**Phản hồi/hiệu ứng (lớp 7)**: ✔ hành động hiện có · ＋ `blur→sharp` · ＋ `silhouette` · ＋ `clip/hole` · ＋ `flash` · ＋ `shuffle` · ＋ `spin3d` · ＋ phản ứng nền tại điểm chạm (gợn nước, dấu chân, đốm sáng) · ＋ thưởng dài hạn (nhân vật lớn dần, tranh hiện dần).

**Mô phỏng (lớp 4)**: toàn bộ mục 5.1 là ＋ (hiện engine **không có** biến thời gian, sự kiện theo khung hình, va chạm, vật có vận tốc).

**Đóng gói game riêng**: ＋ `bubbles` (lưới lục giác, bám, nổ cụm ≥3, trứng mồ côi; Bắn trứng khủng long) là mô-đun có tham số vì là luật cụ thể của một game phổ biến.

**Chế độ giác quan**: cờ toàn game "không đếm giờ, không thua, nhấn mạnh âm thanh".

## 7. Phủ game: cổ điển, vận động, Big Brain Academy
### 7.1 Game động (cần lớp Sim)
| Game | Ghép từ | Khác biệt phải thêm |
|---|---|---|
| **Đào vàng** (mẫu dựng sẵn) | vật `claw` hành vi `swing` + `line` dây; vàng nhóm `gold` (value/weight từ kho vật) rải bằng `scatter`; chạm lúc `state=swing` → `state=extend` (`move` theo `self.angle`); `overlap(claw,gold)` → `attach`, `state=retract` với tốc độ `k/weight`; về trục → `inc score`; luật `$t>=60`/`$score>=$target` | Sim đầy đủ, `scatter`, `line` |
| **Magic tile** | `lanes` + spawner `pattern` (bpm, làn × phách) + `timed-tap` (hoàn hảo/tốt/hụt) + `rules` (`$lives<=0` thua) + tốc độ tăng theo `$t` | Bản nhịp (bảng làn × phách trong tab Dữ liệu), độ trễ LiDAR → mặc định chậm/thưa |
| **Chạy tại chỗ** | 2 `pad` + `alternate` + `rate('step',2)` → tốc độ cuộn nền/nhân vật + `track` + `rules` (đủ quãng đường hoặc hết giờ); biến thể: nhảy `chord`, lò cò, nhảy ô, đèn xanh–đỏ (`avoid`+timer), tiếp sức | Đo pad trên K02 (mục 2) |
| **Bắn trứng — bản quiz** | chạm ô → `spawn` + `shoot` tới điểm chạm → `overlap` → đúng/sai | Sim cơ bản |
| **Bắn trứng — đầy đủ** | `shoot` + `bounce` + mô-đun `bubbles` | `bubbles` |
| Duck Hunt, xếp tháp (chạm khi đung đưa khớp), Flappy (chạm = xung lực + trọng lực), Space Invaders/Pong/Snake (pad hướng + nút bắn), nhảy cóc làn đường | Ghép từ Sim + pad + spawner | — |
| Vỡ bóng bay, Whack Match, Color Count (BBA), Bug Splat (FP) | spawner + chạm + đếm | — |

### 7.2 Big Brain Academy (phủ cơ chế)
| Activity | Cơ chế |
|---|---|
| Odd One Out, Heavyweight, CubeGame, Balloon Burst, Flash Memory, Sound Bites, Speed Sorting, Face Case, Add Agency | ✔ có sẵn hoặc chỉnh nhỏ (reverse, âm theo vật, giọng đọc) |
| Reverse Retention | order + `reverse` |
| Memo-Random | gen `delta` + chọn hình đã mất |
| Covered Cages | reveal + `shuffle` |
| Fast Focus, Get in Shape, Shadow Shift | `blur→sharp`, `silhouette` |
| Frame Filler | view `frame` + `clip/hole` |
| Whack Match, Color Count | spawner |
| Match Blast, Art Parts, Mallet Math, Tick-Tock Turn | `board`/`palette`/`clock` + `match-key` + `cycle` |
| Train Turn, Bone Yard, Pathfinder | `board` + `sim` |
| True View, Block Spot | `voxel` + chiếu theo hướng |
| Species Spotlight | cảnh tĩnh + đếm (không có đèn pin) |

Bỏ qua: Order Out, Pattern Puzzle (chưa rõ luật). Ghost Clash, xu/avatar: thuộc nền tảng.

## 8. Tích hợp / không tích hợp và lý do
**Tích hợp** khi: tap-only; dùng lại ở ≥3 game; gói được thành mô-đun có tham số; web = Unity.
**Không tích hợp:**
| Không | Vì sao |
|---|---|
| Kéo/vuốt/giữ/vẽ, bàn phím | Thiết bị không có; tool cảnh báo, gợi ý thay bằng tap |
| Theo dõi chuyển động liên tục, đá/hockey, ném bóng, AR marker, pose | LiDAR chỉ có tap; K02 yếu (MT8168) |
| **Vật lý rigid-body** (lực, khối lượng, xếp chồng, ragdoll; Angry Birds, Cut the Rope) | Web/Unity lệch kết quả, khó test. **Vẫn cho** chuyển động động học + kiểm tra chồng lấn hình học đơn giản |
| Điều khiển liên tục (platformer, đua tự lái) | Chỉ có tap |
| Game phản xạ nhanh hơn độ trễ LiDAR | Tab Kiểm tra cảnh báo (tốc độ × cửa sổ chạm) |
| Ngôn ngữ lập trình tổng quát (while, hàm, đệ quy, mảng) | Dễ treo game, không đảm bảo web = Unity; sự kiện–điều kiện–hành động + biểu thức đủ; muốn mới thì lập trình viên thêm mô-đun |
| Bàn cờ có luật riêng (Monopoly, RiverCross, PathFinder, TrainPath) | Máy trạng thái riêng; 7 game đóng băng |
| Hub nhiều game, luật trận (Sprint, Party…), công cụ buổi học | User chốt: builder là game lẻ; luật trận làm trong Unity |
| Nhận diện giọng nói/mặt/tư thế | K02 yếu; FaceRecognition là project riêng |
| Sửa ảnh/âm thanh trong tool | Ngoài phạm vi; chỉ nhập, thay |
| Mạng, tài khoản, lưu tiến trình, bảng xếp hạng | Thuộc nền tảng (GameLogger, ScoreScene) |
| Công thức điểm tuỳ ý | Làm lệch báo cáo năng lực; giữ quy tắc điểm chuẩn |

## 9. Runtime & tương thích
- **Giữ nguyên** (runtime nền): ngôn ngữ biểu thức, hành động, engine `FSW` (`WebTools/GenericGameBuilder/fs/`), `FlowWorld` Unity, 16 zip FloorStory.
- **Làm lại**: vỏ tool, mô hình dữ liệu Game/Round, panel phải tự sinh, sân khấu kéo thả cho mọi object, dải thời gian, bảng sự kiện, hook liên kết round.
- **Thêm**: lõi `Sim` (mục 5.1) vào cả `fs/*.js` và `FloorStoryGame/Flow/*.cs`.
- Mỗi round chạy bằng engine hợp với kiểu của nó (engine cũ cho round thủ công, `FSW` + `Sim` cho round sinh tự động/thời gian thực); điểm dùng chung. Unity: `GenericGameController` / `FlowWorld` chọn theo loại khi import. Chưa gộp engine.
- Game v2 cũ và zip cũ tự chuyển/mở được; schema `fsData` mở rộng (thêm `body`, `behaviors`, `spawners`, `rules`, `pads`), version tăng, bản cũ vẫn đọc.
- Mô-đun mới thêm ở **cả** JS và C#, giữ parity (cùng seed → cùng kết quả).

## 10. Lộ trình đề xuất
1. **Vỏ mới**: ribbon, danh sách round, sân khấu, panel phải; nhét editor cũ làm "round thủ công"; chơi thử overlay chung; mọi game cũ vẫn chạy.
2. **Schema mô-đun + form tự sinh**; 8 lớp vào panel phải cho round sinh tự động.
3. **Dòng thời gian + bảng sự kiện/hành động trên sân khấu.**
4. **Hook liên kết round** (`extends`, `carry`, `goto`, `$prev`).
5. **Chuyển 16 game FloorStory sang cấu trúc mới**, đồng bộ importer Unity.
6. **Lõi Sim** (rào chắn cho mọi game động): đồng hồ + `$t`, thân/hành vi, spawner, va chạm, `rules`, hàm biểu thức mới, lớp gỡ lỗi, **JS và C# kèm test parity**. Thử bằng "chạy tại chỗ" nhỏ + một cảnh thử pad trên K02.
7. **Mô-đun theo ưu tiên**: (a) pad + `alternate/rate/chord` + `track` → game vận động; (b) `pick-place/cycle/hotspot` + `board/palette/clock` + `match-key/expr` → phủ nhiều BBA nhất; (c) `lanes` + `timed-tap` + bản nhịp → magic tile; (d) `shoot` + `line` + `scatter` → đào vàng, bắn trứng quiz; (e) hiệu ứng tri giác; (f) `voxel`, `sim`, `bubbles`; (g) buzz/avoid/tile.
Phần engine đã tách: `WebTools/GenericGameBuilder/fs/*.js` nhúng vào `game_builder_v4.17.html` bằng `Tools/floorstory_pack/inline_web.py`; phần glue UI vẫn nằm trong HTML.

## 11. Rủi ro / câu hỏi mở
- **Lệch web ↔ Unity** của Sim: bước cố định, `double`, cùng thứ tự cập nhật; test bằng kịch bản input cố định. Phải chốt thứ tự: input → hành vi → di chuyển → va chạm → sự kiện.
- **Hiệu năng K02**: chưa đo; ngân sách số vật động chỉ là đề xuất.
- **Độ trễ LiDAR** làm magic tile/đào vàng khó: mặc định chậm, cửa sổ rộng; cần số đo thật.
- **Pad chưa kiểm chứng** trên K02 (mục 2) — làm scene đo trước.
- Hai engine trong một Game: điểm chung được, trạng thái (biến, cảnh bền) chỉ giữ trong các round `FSW`. Gộp một engine để sau.
- `voxel`, `sim` (quy tắc lưới) và `bubbles` phức tạp nhất; làm sau cùng, thử trên K02.
- Unity chưa chạy `FlowWorld` trong Editor/K02 (xem `TODO.md`, mục "FloorStory flow"); nên chạy thử trước khi chồng thêm Sim.
- Nguồn khảo sát: trang hãng (EyeClick, LUMOplay, Lü) chỉ có tên game; luật BBA lấy từ Wikipedia/wiki fan, chưa chơi thử.

## 12. Trạng thái triển khai (đợt 1, Builder v4.0, 2026-10-10)
Làm lúc đó trên `game_builder_v4.0.html` (nay đã gộp vào `game_builder_v4.17.html`, bản cuối):
- ✅ **Khung kiểu PowerPoint cho editor kéo-thả** (`game_builder_v4.17.html`): hàng tab File | Cài đặt chung | Trang chủ | Chèn | Hiệu ứng | Xem (+ tên game, nhãn phiên bản, ▶ Chơi thử) → ribbon → [danh sách round | sân khấu | thuộc tính]. Cài đặt chung hiện thẳng trên ribbon (không còn cột/menu kéo ra); hiệu ứng gán theo đối tượng đang chọn (không chọn = hiệu ứng chung); Ctrl+D nhân bản round, Delete xoá đối tượng/round; thư viện ảnh đáp án sai chỉ hiện ở thể loại tự sinh câu hỏi.
- ✅ Thanh trên 1 hàng: menu **File** (Game mới / Mở / Lưu thành / Xuất zip) + tên game + nhãn phiên bản + **▶ Chơi thử** ngoài cùng bên phải, luôn hiện.
- ✅ **File → Game mới** = thư viện mẫu (Trang trống kéo-thả + 16 game FloorStory) → đặt mã game → sửa tiếp.
- ✅ Game cơ chế chơi thử **toàn màn hình** (`#playtest`, dùng chung với game kéo-thả); bỏ khung chơi thử nhỏ.
- ✅ Panel game cơ chế chia **Cơ bản / ⚙ Nâng cao** (ẩn JSON, biểu thức, biến, hành động khỏi chế độ Cơ bản).
- ✅ **Định dạng chữ** (cỡ, màu, đậm, nghiêng, viền màu/dày, bóng) theo vai trò: câu hỏi, lời khen/nhắc, chữ trên thẻ, chữ trong cảnh (`fsData.fonts`, `look.style`). Web + C# (`StoryUI.TextStyle`).
- ✅ **Âm thanh bằng file**: tiếng từng vật, tiếng đúng/sai, nhạc nền. Đã bỏ giọng đọc trình duyệt. Web + C# (`FlowWorld.PlayFile`).
- ⏳ Chưa làm: **game cơ chế (mẫu FloorStory) vẫn là panel form 2 cột, chưa có sân khấu/ribbon** (mới thống nhất vỏ + chơi thử), panel phải theo *vật đang chọn* (hiện theo round), **khối lệnh kiểu Scratch** cho phản hồi (hiện phần "Nâng cao" vẫn là JSON), định dạng chữ cho game kéo-thả v2 (cần đổi schema + Unity `GenericGameController`), lớp Sim thời gian thực, mô-đun mới (mục 6), hook liên kết round, tách file nguồn.
- ⚠ Unity (định dạng chữ, âm thanh, importer) mới biên dịch bằng Roslyn, chưa chạy trong Editor/K02.
