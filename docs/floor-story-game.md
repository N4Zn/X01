# FloorStory — khung chung cho game "thế giới có trạng thái" (bé 3-5 tuổi), mỗi game 1 scene

> **TL;DR**: `FloorStoryController` (Independent, mỗi đội 1 `StoryWorld`) dùng chung; MỖI GAME 1 SCENE RIÊNG (tên scene = tên game, field `gameName` chọn world). UI dựng bằng code, ảnh thiếu thì dùng hình/chữ thay thế.
> **Đọc khi**: thêm/sửa game NgayCuaBe, HinhHoc, CongHinh, BeKhoeManh, VoBongBay, SanKyDieu, CauBacQua, DatDoVaoCho, DongHoKhongLo; thêm ảnh/giọng đọc cho chúng.
> **Cập nhật**: 2026-10-09 (chưa chạy thử trong Unity — mới compile-check bằng Roslyn)

## Kiến trúc
- Code: `Assets/Game/Scripts/UIScripts/FloorStoryGame/` — `Core/` (`StoryUI` helper + tween, `ShapeSprites` hình sinh runtime, `StoryWorld` base), `Controllers/FloorStoryController.cs`, `Worlds/*World.cs` (mỗi game 1 file).
- Scene: menu **Tools → FloorStoryGame → Build ALL Scenes** (hoặc `Build One/<tên>`) → `Assets/Game/Scenes/FloorStory/<TênGame>.unity` (tự thêm Build Settings; chạy lại sẽ ghi đè scene cùng tên). Mở scene rồi bấm Play.
- Đăng ký: `GameRegistry.cs` (khối "FloorStory", `sceneName` = tên game) + `game_registry.json` → sau khi sửa json phải build lại aar (`sh Tools/build-aar.sh controlui`).
- 1 "round" của Kit = 1 lượt (task) của 1 đội. Điểm +1 chỉ khi đúng NGAY lần đầu; chạm sai vẫn được thử lại (có gợi ý sau 2 lần sai). Log = đúng/sai của lần đầu (`QuestionData` điền trong `BeginTask`, id dạng `NGAY_0_danh_rang`).
- **LiDAR chỉ bắn 1 cú chạm tức thời (pointerDown+Up+Click cùng lúc) — KHÔNG có kéo/giữ.** Mọi tương tác phải là "chạm". `StoryUI.OnTap`, `StoryUI.TapArea`.
- Thêm game mới: viết `XxxWorld : StoryWorld` (`Build()` + `BeginTask(info, done)`), thêm 1 dòng `case` ở `FloorStoryController.CreateWorld`, thêm tên vào `FloorStorySceneBuilder.Games` (+ 1 MenuItem), thêm `Set(...)` ở GameRegistry + json.

## Game hiện có
| name | Nội dung | Ảnh dùng |
|---|---|---|
| `NgayCuaBe` | Các buổi trong ngày: trời đổi theo buổi, chọn việc hợp buổi | `Story/Day/*` (chưa có → `Family/*` hoặc thẻ chữ) |
| `HinhHoc` | Xây nhà từ bóng đen: tròn/vuông/tam giác/chữ nhật | không cần (vẽ bằng code) |
| `BeKhoeManh` | Bi cần ăn/uống/ngủ/vận động/vệ sinh; đủ 5 sao → Bi lớn | `Story/Food/*` (32 trái cây, đã chép), `SaveEnvironment/*`, `NatureKit` (giường), `Story/Health/*` (chưa có → thẻ chữ) |
| `CongHinh` | Đi qua cổng hình (1 → 2 → 3 cổng liên tiếp), vị trí cổng xáo mỗi lượt | không cần (vẽ bằng code), con vật `GameImages/Animal/*` |
| `VoBongBay` | Bé nhỏ: vỡ bóng đúng màu → con vật chui ra | `Balloon/*`, `GameImages/Animal/*` (có sẵn) |
| `SanKyDieu` | Bé nhỏ: chạm đâu mọc hoa/sao/tim/bóng/cây đó (không đúng/sai) | NatureKit + hình code |
| `CauBacQua` | Dài–ngắn (bắc cầu cho Thỏ) xen to–bé (chui cửa) | `GameImages/Animal/*`, `Fruit/carrot` |
| `DatDoVaoCho` | Trên/dưới/trái/phải: chạm đúng vị trí quanh cái bàn | `GameImages/Animal/*` |
| `DongHoKhongLo` | Giờ đúng: dậm số trên mặt đồng hồ (đặt giờ / đọc giờ) | không cần |

## Ảnh/âm thanh bổ sung (tuỳ chọn — game vẫn chạy khi thiếu)
- Thả PNG nền trong suốt vào `Assets/Game/Resources/Story/<nhóm>/<tên>.png` (importer tự đặt Sprite — `FloorStoryTextureImportProcessor`).
  - `Story/Day/`: `na` (nhân vật bé gái), `thuc_day danh_rang an_sang di_hoc rua_tay an_trua ngu_trua ve_nha choi_san tam an_toi doc_truyen di_ngu`.
  - `Story/Health/`: `xem_tivi dien_thoai chay_nhay rua_tay lau_ao an_luon`.
- Giọng đọc đề (không bắt buộc): `Assets/Game/Resources/StoryVoice/<key>.mp3` — key: `buoi_0..3`, `hinh_tron hinh_vuong hinh_tamgiac hinh_chunhat hinh_vuakhit`, `bi_an bi_uong bi_ngu bi_van_dong bi_ve_sinh`, `mau_<red|blue|green|yellow|orange|pink|purple>`, `con_<tên file animal>`, `san_<hoa|sao|tim|bong|vuon>`, `cau_dai`, `cua_<chick|dog|elephant>`, `vitri_<tren|duoi|trai|phai>`, `gio_dat_1..12`, `gio_doc`.
- Âm hiệu ứng: `Resources/StorySfx/*.ogg` (pop, tap, wood, bell, powerup, star, up, plop — Kenney CC0).

## Lưu ý / chưa làm
- Trái/phải trong `DatDoVaoCho` tính theo MÀN HÌNH (bé quay mặt vào màn chiếu).
- Pipeline đánh giá năng lực (`docs/danh-gia-nang-luc/tool/pipeline/` config `game_hp`): chưa map các game này → cần thêm; topic dạng `HinhHoc_NhanBietHinh`, `NgayCuaBe_BuoiTrongNgay`, `BeKhoeManh_<need>`, `CauBacQua_DaiNgan|ToBe`, `DatDoVaoCho_ViTri`, `DongHoKhongLo_DatGio|DocGio`.
- Chưa có icon trong `Resources/GameIcons/<name>.png` cho 8 game (menu dùng icon mặc định).
