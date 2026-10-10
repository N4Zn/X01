# FloorStory — khung chung cho game "thế giới có trạng thái" (bé 3-5 tuổi), mỗi game 1 scene

> **TL;DR**: `FloorStoryController` (Independent, mỗi đội 1 `StoryWorld`) dùng chung; MỖI GAME 1 SCENE RIÊNG (tên scene = tên game, field `gameName` chọn world). UI dựng bằng code, ảnh thiếu thì dùng hình/chữ thay thế.
> **Đọc khi**: thêm/sửa game NgayCuaBe, HinhHoc, CongHinh, BeKhoeManh, VoBongBay, SanKyDieu, CauBacQua, DatDoVaoCho, DongHoKhongLo, TrungMauSac, HinhDonGian, NhoChuoiHinh, LatTheNhoGiong, NangNhe, DemKhoiHop; thêm ảnh/giọng đọc cho chúng.
> **Cập nhật**: 2026-10-10 (chưa chạy thử trong Unity — mới compile-check bằng Roslyn; thêm gói zip thay ảnh)

## Kiến trúc
- Code: `Assets/Game/Scripts/UIScripts/FloorStoryGame/` — `Core/` (`StoryUI` helper + tween, `ShapeSprites` hình sinh runtime, `StoryWorld` base), `Controllers/FloorStoryController.cs`, `Worlds/*World.cs` (mỗi game 1 file).
- Scene: menu **Tools → FloorStoryGame → Build ALL Scenes** (hoặc `Build One/<tên>`) → `Assets/Game/Scenes/FloorStory/<TênGame>.unity` (tự thêm Build Settings; chạy lại sẽ ghi đè scene cùng tên). Mở scene rồi bấm Play.
- Đăng ký: `GameRegistry.cs` (khối "FloorStory", `sceneName` = tên game) + `game_registry.json` → sau khi sửa json phải build lại aar (`sh Tools/build-aar.sh controlui`).
- 1 "round" của Kit = 1 lượt (task) của 1 đội. Điểm +1 chỉ khi đúng NGAY lần đầu; chạm sai vẫn được thử lại (có gợi ý sau 2 lần sai). Log = đúng/sai của lần đầu (`QuestionData` điền trong `BeginTask`, id dạng `NGAY_0_danh_rang`).
- **LiDAR chỉ bắn 1 cú chạm tức thời (pointerDown+Up+Click cùng lúc) — KHÔNG có kéo/giữ.** Mọi tương tác phải là "chạm". `StoryUI.OnTap`, `StoryUI.TapArea`.
- **Chế độ chung (Synchronized)**: world override `Synchronized => true` + dùng `NextTaskRng()` (cùng seed 2 đội → cùng câu). Controller giữ `BeginTask` của lượt k tới khi đội kia xong k lượt (đội xong trước thấy "Chờ bạn nhé..."); `onDone` vẫn gọi ngay nên thời gian log đúng. Game tự chấm điểm nhiều lần trong 1 lượt dùng `AddPoint()` (cộng thẳng ScoreManager).
- Icon ghép bằng code: `StoryIcons.Build(parent, key, pos, size)` (8 hình); khối hộp 3D: `StoryCube` (Graphic mesh).
- Thêm game mới: viết `XxxWorld : StoryWorld` (`Build()` + `BeginTask(info, done)`), thêm 1 dòng `case` ở `FloorStoryController.CreateWorld`, thêm tên vào `FloorStorySceneBuilder.Games` (+ 1 MenuItem), thêm `Set(...)` ở GameRegistry + json.

## Game hiện có
| name | Nội dung | Ảnh dùng |
|---|---|---|
| `NgayCuaBe` | Các buổi trong ngày: trời đổi theo buổi, chọn việc hợp buổi | `Story/Day/*` (chưa có → `Family/*` hoặc thẻ chữ) |
| `HinhHoc` | Xây nhà từ bóng đen: tròn/vuông/tam giác/chữ nhật | không cần (vẽ bằng code) |
| `BeKhoeManh` | Bi cần ăn/uống/ngủ/vận động/vệ sinh/giữ ấm (6 nhu cầu); đủ 5 sao → Bi lớn. Nền vườn `Background/Carrot_BG` | `Story/Bi/<hungry thirsty sleepy bored dirty cold happy>` (cùng 1 nhân vật; thiếu → `Family/*`, là các bé KHÁC NHAU nên chưa liền mạch), `Story/Food/*` (32 trái cây, đã chép), `SaveEnvironment/*`, `NatureKit` (giường), `Story/Health/*` (chưa có → thẻ chữ) |
| `CongHinh` | Đi qua cổng hình (1 → 2 → 3 cổng liên tiếp), vị trí cổng xáo mỗi lượt | không cần (vẽ bằng code), con vật `GameImages/Animal/*` |
| `VoBongBay` | Bé nhỏ: vỡ bóng đúng màu → con vật chui ra | `Balloon/*`, `GameImages/Animal/*` (có sẵn) |
| `SanKyDieu` | Bé nhỏ: chạm đâu mọc hoa/sao/tim/bóng/cây đó (không đúng/sai) | NatureKit + hình code |
| `CauBacQua` | Dài–ngắn (bắc cầu cho Thỏ) xen to–bé (chui cửa) | `GameImages/Animal/*`, `Fruit/carrot` |
| `DatDoVaoCho` | Trên/dưới/trái/phải: chạm đúng vị trí quanh cái bàn | `GameImages/Animal/*` |
| `DongHoKhongLo` | Giờ đúng: dậm số trên mặt đồng hồ (đặt giờ / đọc giờ) | không cần |
| `TrungMauSac` | Bé 3 tuổi: chọn trứng đúng màu → nở hoa + trái cây cùng màu | `Story/Food/*` (có sẵn), còn lại vẽ bằng code |
| `HinhDonGian` | Bé 3 tuổi: chọn hình được gọi (mỗi hình 1 màu cố định: tim đỏ, tam giác vàng, vuông xanh lá, tròn xanh dương; sau đó +chữ nhật cam, sao tím, +tim, thoi hồng). **CHẾ ĐỘ CHUNG**: 2 đội cùng câu, đội xong trước chờ | không cần |
| `NhoChuoiHinh` | Trí nhớ: xem chuỗi 1→6 hình (mặt trời/trăng/hoa/sao/tim/bánh/kem/cốc), úp lại, chạm 4 hình theo thứ tự trái→phải | không cần (`StoryIcons`) |
| `LatTheNhoGiong` | Trí nhớ: lật 2 thẻ, giống thì ăn +1 điểm ngay, khác thì úp lại sau 1.5s; 4 thẻ → 12 thẻ (mỗi bảng +1 cặp); xong bảng không nhầm: +1 thưởng | không cần |
| `NangNhe` | Bập bênh: con nặng hơn chúi xuống → chạm con nặng/nhẹ (2 con; sau 4 lượt đúng: 3 con trên 2 bập bênh nối nhau) | `GameImages/Animal/*` |
| `DemKhoiHop` | Đếm khối hộp 3D (isometric, `StoryCube`), chọn 1 trong 3 số; 1–3 khối → tối đa 10; sai 2 lần thì đếm 1,2,3 từng khối | không cần |

## Ảnh/âm thanh bổ sung (tuỳ chọn — game vẫn chạy khi thiếu)
- Thả PNG nền trong suốt vào `Assets/Game/Resources/Story/<nhóm>/<tên>.png` (importer tự đặt Sprite — `FloorStoryTextureImportProcessor`).
  - `Story/Day/`: `na` (nhân vật bé gái), `thuc_day danh_rang an_sang di_hoc rua_tay an_trua ngu_trua ve_nha choi_san tam an_toi doc_truyen di_ngu`.
  - `Story/Health/`: `xem_tivi dien_thoai chay_nhay rua_tay lau_ao an_luon`.
- Giọng đọc đề (không bắt buộc): `Assets/Game/Resources/StoryVoice/<key>.mp3` — key: `buoi_0..3`, `hinh_tron hinh_vuong hinh_tamgiac hinh_chunhat hinh_vuakhit`, `bi_an bi_uong bi_ngu bi_van_dong bi_ve_sinh`, `mau_<red|blue|green|yellow|orange|pink|purple>`, `con_<tên file animal>`, `san_<hoa|sao|tim|bong|vuon>`, `cau_dai`, `cua_<chick|dog|elephant>`, `vitri_<tren|duoi|trai|phai>`, `gio_dat_1..12`, `gio_doc`; game bé 3 tuổi: `hinh_sao hinh_tim hinh_thoi` (+ `hinh_*` ở trên), `nho_chon`, `nang_hon nhe_hon nang_nhat nhe_nhat`, `dem_khoi`, `so_1..so_10` (TrungMauSac dùng `mau_<màu>`).
- Âm hiệu ứng: `Resources/StorySfx/*.ogg` (pop, tap, wood, bell, powerup, star, up, plop — Kenney CC0).

## Gói zip thay ảnh (mở bằng web game builder) — 2026-10-10
- **Mục đích**: người không rành Unity thay ảnh từng game. 13 gói trong `WebTools/FloorStoryPacks/<Game>.zip` (chưa có gói cho `DongHoKhongLo`, `DemKhoiHop` vì không có ảnh nào thay được).
- **Tạo/cập nhật gói**: `python Tools/floorstory_pack/make_packs.py [Game ...]` (ảnh mặc định lấy từ Resources; hình vẽ bằng code được vẽ lại gần đúng bằng PIL để xem trước). **Danh mục ảnh nằm trong script (`PACKS`) — thêm/bớt ảnh của game thì sửa ở đó** cho khớp `StoryUI.Load(...)`.
- **Web builder** (`WebTools/GenericGameBuilder/game_builder_v2.html`): mở zip qua "⇧ Mở game đã xuất"; `meta.kind = "floorstory"` → hiện panel thay ảnh (bấm/kéo-thả), ẩn phần soạn round + Chơi thử (game chạy trên Unity). Xuất = zip cùng tên; `meta.floorStory.images[i].file` đổi sang `fs_N.png` khi có thay.
- **Unity**: `Tools → FloorStoryGame → Import Pack Zips (thay ảnh)` (hoặc `Tools/GenericGame/Import Zip` — tự nhận gói FloorStory và chuyển hướng). Ảnh bị thay → `Assets/Game/Resources/StoryPack/<Game>/<key>.png`; ảnh đặt lại gốc → xoá bản thay. Rồi build lại APK. Không đụng scene/registry/aar.
- **Runtime**: `StoryUI.Load(path)` thử `StoryPack/<game>/<path>`, rồi `StoryPack/_all/<path>` (thay cho mọi game), rồi ảnh gốc; `StoryUI.PackGame` do `FloorStoryController` đặt. Điểm móc ảnh cho game vẽ bằng code: `Story/Shapes/<tron|vuong|tamgiac|chunhat|sao|tim|thoi>` (HinhDonGian), `Story/Icons/<sun|moon|flower|star|heart|cake|icecream|cup>` (StoryIcons → NhoChuoiHinh + LatThe), `Story/Egg/<màu>` (TrungMauSac).
- Ảnh thay nên là PNG nền trong suốt; HinhDonGian giữ màu cố định của từng hình, TrungMauSac giữ đúng màu trứng. Chưa chạy thử trong Unity.

## Lưu ý / chưa làm
- Trái/phải trong `DatDoVaoCho` tính theo MÀN HÌNH (bé quay mặt vào màn chiếu).
- Pipeline đánh giá năng lực (`docs/danh-gia-nang-luc/tool/pipeline/` config `game_hp`): chưa map các game này → cần thêm; topic dạng `HinhHoc_NhanBietHinh`, `NgayCuaBe_BuoiTrongNgay`, `BeKhoeManh_<need>`, `CauBacQua_DaiNgan|ToBe`, `DatDoVaoCho_ViTri`, `DongHoKhongLo_DatGio|DocGio`.
- Chưa có icon trong `Resources/GameIcons/<name>.png` cho 8 game (menu dùng icon mặc định).
