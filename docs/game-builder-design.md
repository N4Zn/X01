# Thiết kế lại Game Builder — một tool thống nhất, kiểu PowerPoint + lập trình kéo thả

> **TL;DR**: Game builder (`WebTools/GenericGameBuilder/`) là tool **dựng game lẻ**: Tạo/Mở → thêm ảnh, chỉnh → ▶ Chơi thử full màn hình → ⇩ Xuất zip. Bản v2 cũ là chuẩn. Cơ chế FloorStory (nguồn/hiển thị/chấm/phản hồi...) và các cơ chế rút ra từ Big Brain Academy + game chiếu sàn chỉ là **tính năng bổ sung** trong cùng tool, không phải chế độ riêng.
> **Đọc khi**: sắp làm/sửa builder, thêm cơ chế mới, quyết định có đưa tính năng X vào builder không.
> **Cập nhật**: 2026-10-10 — **bản nháp thiết kế để duyệt, chưa có code.** Thay cho hướng `fsd-mode` tách riêng đã làm trước đó (xem `floor-story-mechanics.md`, là runtime/engine, vẫn dùng lại).

## 1. Quyết định đã chốt (user, 2026-10-10)
- Builder là tool xây game **lẻ**. **Không** có "bộ sưu tập / hub nhiều game" trong builder.
- **Luật trận** (Sprint, Marathon, Party, luân phiên người…) nếu có sau này chỉ làm trong Unity, không đưa vào builder.
- Mục tiêu: **phủ cơ chế** để tool sẵn sàng, sau đó hoàn thiện dần game theo ưu tiên. Học theo các game đã được chơi nhiều (Big Brain Academy, floor-projection).
- Giao diện học PowerPoint: vùng trung tâm "Thiết kế game", thanh công cụ trên (cài đặt chung), panel phải (cài đặt riêng theo vật đang chọn).
- Kiến trúc: tách 7 lớp độc lập; round liên kết/kế thừa qua hook.
- Bỏ qua game BBA chưa rõ luật (Order Out, Pattern Puzzle). Add Agency = game cộng (đã có). Get in Shape = nhìn bóng đoán vật. True View = hình 3D xoay, hỏi góc nhìn theo mũi tên.

**Giả định, cần xác nhận** (chưa được user trả lời): (a) mỗi round chạy bằng engine hợp với loại của nó, chưa gộp thành 1 engine; (b) công cụ buổi học (chia đội/đồng hồ/vòng quay) ngoài phạm vi builder; (c) "phản ứng nền ở điểm chạm" và "chế độ giác quan/không thua" là mô-đun ưu tiên cao.

## 2. Ràng buộc thiết bị: chỉ có tap
LiDAR sàn chỉ cho **tap** (`project_lidar_touch_tap_only`). Không kéo/vuốt/giữ/chụm/vẽ/hover/bàn phím. Điểm chạm lệch (chân/tay), 2 đội chạm đồng thời, có độ trễ, trẻ chưa đọc được. Tool phải **không cho tạo tương tác thiết bị không làm được**; thao tác thiếu được thay như sau:

| Muốn | Dùng |
|---|---|
| Kéo vật vào chỗ | Chạm vật → chạm ô đích (`pick-place`) |
| Xoay kim, trượt thanh | Chạm xoay vòng giá trị / nút +− (`cycle`) |
| Giữ để chọn | Chạm bật/tắt (`toggle`) |
| Vẽ đường (Pathfinder) | Chọn lối ra + animation chạy đường |
| Đèn pin soi (Species Spotlight) | Cảnh tĩnh + đếm |
| Gõ chữ | Ô chữ chạm để điền |
| Vận động thật | Thẻ lệnh hiển thị + hẹn giờ, giáo viên bấm "xong" (không cảm biến) |

Đề chữ phải có **giọng đọc hoặc hình**. Phạt sai nhẹ. Tốc độ chấm thô (độ trễ LiDAR). Ngưỡng kích thước chạm tối thiểu: **chưa có số đo**, lấy từ calib (`docs/lidar-calib.md`) khi cần.

## 3. Mô hình khái niệm
| PowerPoint | Builder |
|---|---|
| Bộ slide | **Game**: kho tài nguyên, theme, biến toàn cục, danh sách round |
| Slide | **Round** |
| Đối tượng | **Object** (ảnh, chữ, hình, nhóm ô đáp án, thẻ, bảng ô, đồng hồ…) |
| Slide master/layout | **Round mẫu**: round khác `extends` nó |
| Animation pane | **Dòng thời gian** của round |
| Transition | **Chuyển round** (thứ tự, điều kiện, mang trạng thái) |

FloorStory **không phải một loại game**: chỉ là nhóm game có round kế thừa/chuyển tiếp, bổ sung cho round độc lập kiểu cũ. Một Game có thể trộn cả hai loại round.

## 4. Giao diện
```
┌ Ribbon: [Game] [Chèn] [Hiệu ứng] [Chuyển round] [▶ Chơi thử] [⇩ Xuất] ┐
├ Danh sách ┬──────────────────────────────┬ Thuộc tính (theo vật đang chọn) ┤
│ round     │  Sân khấu "Thiết kế game"    │ Hiển thị | Dữ liệu | Hành vi |  │
│ (thumb,   │  kéo thả, chọn, căn, lớp     │ Chấm | Kiểm tra                 │
│ mũi tên   │  vạch 2 đội, vùng chạm tối   │ (form tự sinh từ schema mô-đun) │
│ chuyển)   ├──────────────────────────────┤                                  │
│           │ Dải dưới: Thời gian / Sự kiện│                                  │
└───────────┴──────────────────────────────┴──────────────────────────────────┘
```
- **Tab Game** của ribbon thay panel trái cũ: nền, nhạc, 2 đội/sync, theme, biến toàn cục, kho ảnh/vật.
- **Chơi thử** = overlay full màn hình (`#playtest`) như cũ, cho mọi loại round. Không còn khung chơi thử nhỏ.
- **Tab Kiểm tra** (Design Checker): lớp phủ vùng chạm tối thiểu, vạch chia 2 đội, cảnh báo vật/chữ nhỏ, thao tác không phải tap.
- Sự kiện–điều kiện–hành động là khối có form; đích chọn bằng cách **bấm trên sân khấu**; JSON chỉ ở tab Nâng cao.
- Game v2 cũ mở ra thành round thủ công, mọi thứ cũ phải còn chạy.

## 5. Kiến trúc: các lớp tách rời
Mỗi thành phần có **schema tham số** (nhãn, kiểu, mặc định, loại tham chiếu: vật/biến/object/biểu thức) → panel phải tự sinh form. **Thêm cơ chế mới = đăng ký một mô-đun** (hiện `MOD` mới có mặc định, phải bổ sung nhãn/kiểu).

| Lớp | Gắn vào | Nội dung |
|---|---|---|
| 1 Nguồn | Round | thủ công · bảng/CSV · sinh tự động (`set match calc values sequence pairs number …`) · lấy từ round trước |
| 2 Hiển thị | Object | object thông minh gắn nguồn bằng `bind` |
| 3 Thời gian | Round/Object | hiện lần lượt → giữ → úp/ẩn; hẹn giờ; trễ |
| 4 Chạm | Object | từ vựng input tap (mục 6) |
| 5 Chấm | Round | judge (mục 6) |
| 6 Phản hồi | Round/Object | sự kiện → hành động; hiệu ứng nền |
| 7 Trạng thái | Game/Round | biến (giữ qua lượt, mỗi đội riêng), cảnh bền, bảng "cấp → tham số" |

**Độ khó** là dữ liệu hạng nhất: bảng `cấp → tham số` cho gen; điều chỉnh bằng biến (`$level`) qua hành động (`on.right: inc level`), không cần bộ điều khiển riêng.
**Kỹ năng** gắn thẻ trên round (nhận biết, nhớ, phân tích, tính, không gian) để nối vào báo cáo năng lực (`docs/danh-gia-nang-luc`).
**Log**: mọi judge phát sự kiện chuẩn vào GameLogger (Click → Round → Session); không tuỳ chọn.

### Hook liên kết round
- `extends`: round con lấy object/chấm/phản hồi của round mẫu, ghi đè phần khác.
- `carry`: khai báo biến/đối tượng giữ sang round sau (cảnh bền của FloorStory).
- Đầu ra/đầu vào: round xuất `out.*`, round sau đọc `$prev.*` (vd round 1 cho xem chuỗi, round 2 dựng lại).
- Vòng đời: sự kiện `round.enter / round.exit / round.next`, hành động `goto` / `endRound`.
- Quy tắc chuyển: tuần tự · có điều kiện · ngẫu nhiên trong nhóm · lặp n lần. (`flowPick` hiện tại là bản sơ khai.)
- Round mẫu lặp n lượt có nguồn tự sinh, giữ trạng thái giữa lượt = một game FloorStory hiện nay.

## 6. Thư viện cơ chế (catalog)
Ký hiệu: ✔ đã có · ＋ cần thêm. Nguồn: BBA = Big Brain Academy, FP = game chiếu sàn thương mại.

**Input (lớp 4)**: ✔ chạm vật, chuỗi, đếm lượt chạm, lật cặp · ＋ `pick-place` (chọn rồi đặt) · ＋ `cycle/toggle/+−` · ＋ `hotspot` trên ảnh · ＋ `buzz` (chạm nhanh nhất thắng) · ＋ `rate` (đua số lượt chạm) · ＋ `timed-tap` (đèn xanh, nhịp) · ＋ `avoid` (vật cấm) · ＋ `reveal-tile` (lật ô lộ tranh) · ＋ `claim-tile` (chiếm ô theo đội).

**Hiển thị (lớp 2)**: ✔ cards, props, strip, grid, seesaw, stack, floor · ＋ `board` (bảng ô có trạng thái: bật/tắt, giá trị, mảnh đặt vào) · ＋ `palette` (bảng mảnh để chọn) · ＋ `clock` (mặt đồng hồ, kim xoay bằng cycle) · ＋ `voxel` (khối 3D xoay + **chiếu theo hướng nhìn**, dùng cho True View, Block Spot) · ＋ `frame` (tranh có vùng khuyết, Frame Filler) · ＋ `track` (thanh tiến độ/đường đua theo đội) · ＋ `meter` (thanh mục tiêu chung) · sau: `piano`, bản đồ hotspot, bàn cờ.

**Chấm (lớp 5)**: ✔ equals, order, pairs, taps · ＋ `order` có `reverse` (Reverse Retention) · ＋ `match-key` (trạng thái người chơi so với mẫu hoặc điều kiện `sum==n`: Match Blast, Art Parts, Mallet Math, Train Turn) · ＋ `expr` (biểu thức bool, cửa thoát chung) · ＋ `fill` (điền ô) · ＋ `sim` (chạy quy tắc trên lưới/đồ thị rồi so đáp án: Bone Yard, Train Turn, Pathfinder) · ＋ `first` (buzz) · ＋ `forbid` (chạm vật cấm).

**Nguồn (lớp 1)**: ✔ các gen hiện có · ＋ bảng/CSV (nhập từ Excel, khớp MiniGame Kit) · ＋ `grid` (bảng ngẫu nhiên) · ＋ `voxel` (khối ngẫu nhiên + câu hỏi hướng nhìn) · ＋ `delta` (bớt/đổi một phần bộ hình: Memo-Random) · ＋ `silhouette` (hỏi bóng đoán vật: Get in Shape, Shadow Shift).

**Phản hồi/hiệu ứng (lớp 6)**: ✔ danh sách hành động hiện có · ＋ `blur→sharp` (Fast Focus) · ＋ `silhouette` (đổ bóng đen) · ＋ `clip/hole` (cắt vùng khuyết) · ＋ `flash` (thoáng qua) · ＋ `shuffle` (xáo vị trí, Covered Cages) · ＋ `spin3d` (xoay khối) · ＋ phản ứng nền tại điểm chạm (gợn nước, dấu chân, đốm sáng) · ＋ phần thưởng dài hạn (nhân vật lớn dần, tranh hiện dần).

**Spawner (lớp 3/6)**: ＋ sinh vật có tốc độ, đường bay (bay lên/rơi/lượn/nảy), tuổi thọ, chạm thì nổ; kèm mạng/đồng hồ đếm ngược/sao. Phủ Vỡ bóng bay, Whack Match, Color Count, Bug Splat. Chỉ dùng tween, không vật lý.

**Chế độ giác quan**: cờ toàn game "không đếm giờ, không thua, nhấn mạnh âm thanh" (nhóm Sensory của LUMOplay).

## 7. Phủ Big Brain Academy (phủ cơ chế, không phủ từng game)
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
Order Out, Pattern Puzzle: bỏ qua (chưa rõ luật). Ghost Clash, xu/avatar: thuộc nền tảng.

## 8. Tích hợp / không tích hợp và lý do
**Tích hợp** khi: tap-only; dùng lại ở ≥3 game; gói được thành mô-đun có tham số; web = Unity.
**Không tích hợp:**
| Không | Vì sao |
|---|---|
| Kéo/vuốt/giữ/vẽ, bàn phím | Thiết bị không có; tool cảnh báo, gợi ý thay bằng tap |
| Theo dõi chuyển động liên tục, đá/hockey, ném bóng, AR marker, pose | LiDAR chỉ có tap; K02 yếu (MT8168) |
| Vật lý thật | Web/Unity lệch kết quả, khó test; tween đủ |
| Ngôn ngữ lập trình tổng quát (while, hàm, đệ quy, mảng) | Dễ treo game, không đảm bảo web = Unity, người dùng không phải lập trình viên; muốn mới thì lập trình viên thêm mô-đun |
| Game phản xạ nhanh | Độ trễ LiDAR + trẻ nhỏ |
| Bàn cờ có luật riêng (Monopoly, RiverCross, PathFinder, TrainPath) | Máy trạng thái riêng; 7 game đóng băng |
| Hub nhiều game, luật trận (Sprint, Party…), công cụ buổi học | User chốt: builder là game lẻ; luật trận làm trong Unity |
| Nhận diện giọng nói/mặt/tư thế | K02 yếu; FaceRecognition là project riêng |
| Sửa ảnh/âm thanh trong tool | Ngoài phạm vi; chỉ nhập, thay |
| Mạng, tài khoản, lưu tiến trình, bảng xếp hạng | Thuộc nền tảng (GameLogger, ScoreScene) |
| Công thức điểm tuỳ ý | Làm lệch báo cáo năng lực; giữ quy tắc điểm chuẩn |

## 9. Runtime & tương thích
- **Giữ nguyên** (là runtime, không phải việc làm lại): ngôn ngữ biểu thức, hành động, engine `FSW` (`WebTools/GenericGameBuilder/fs/`), `FlowWorld` Unity, 16 zip FloorStory.
- **Làm lại**: vỏ tool, mô hình dữ liệu Game/Round, panel phải tự sinh, sân khấu kéo thả cho mọi object, dải thời gian, bảng sự kiện, hook liên kết round.
- Mỗi round chạy bằng engine hợp với nó (engine cũ cho round thủ công, `FSW` cho round có nguồn tự sinh); điểm dùng chung. Unity: `GenericGameController` / `FlowWorld` chọn theo loại khi import. Chưa gộp engine (rủi ro lớn, xem mục 10).
- Game v2 cũ và zip cũ tự chuyển/mở được.
- Mô-đun mới thêm ở **cả** `fs/*.js` và `FloorStoryGame/Flow/*.cs`, giữ parity (cùng seed → cùng câu hỏi).

## 10. Lộ trình đề xuất
1. **Vỏ mới**: ribbon, danh sách round, sân khấu, panel phải; nhét editor cũ làm "round thủ công"; mọi thứ cũ vẫn chạy; chơi thử overlay chung.
2. **Schema mô-đun + form tự sinh**; đưa 7 lớp vào panel phải cho round sinh tự động.
3. **Dòng thời gian + bảng sự kiện/hành động trên sân khấu.**
4. **Hook liên kết round** (`extends`, `carry`, `goto`, `$prev`).
5. **Chuyển 16 game FloorStory sang cấu trúc mới**, đồng bộ importer Unity.
6. **Mô-đun mới theo ưu tiên**: (a) input `pick-place/cycle/hotspot` + `board/palette/clock` + `match-key/expr` → phủ nhiều activity BBA nhất; (b) spawner + mạng/giờ; (c) hiệu ứng tri giác; (d) `voxel` + `sim`; (e) buzz/rate/timed-tap/avoid/tile.
Cần tách `game_builder_v2.html` (440 KB) thành nhiều file nguồn rồi gộp ra một HTML khi build.

## 11. Rủi ro / câu hỏi mở
- Hai engine trong một Game: điểm chung được, trạng thái (biến, cảnh bền) chỉ giữ trong các round `FSW`. Gộp một engine là việc lớn, để sau.
- `voxel` + `sim` là hai mô-đun phức tạp nhất; nên làm sau cùng và kiểm tra trên K02.
- Parity JS ↔ C# cho mô-đun mới: mỗi mô-đun cần test so khớp như đã làm cho gen.
- Unity chưa chạy trong Editor/K02 với `FlowWorld`; nên chạy thử trước khi chồng thêm mô-đun (xem `TODO.md`, mục "FloorStory flow").
- Nguồn khảo sát: trang hãng (EyeClick, LUMOplay, Lü) chỉ có tên game, ít mô tả; luật BBA lấy từ Wikipedia/wiki fan, chưa chơi thử.
