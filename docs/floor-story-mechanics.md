# FloorStory "flow" — game sinh động ghép từ mô-đun (web builder ⇄ Unity)

> **TL;DR**: 1 game = kho vật + biến trạng thái + cảnh + 1..n `flow`; mỗi flow = **nguồn (gen) → hiển thị (views) → chấm (judge) → phản hồi (on.\*)**. Soạn + chơi thử trên web builder, xuất zip (`meta.kind = "fsdata"`), Unity import rồi chạy CÙNG dữ liệu. **Cả 16 game FloorStory đã có zip** (`WebTools/FloorStoryFlows/`).
> **Đọc khi**: thêm/sửa mô-đun (cơ chế mới), sửa luật chơi, đồng bộ web ↔ Unity, thêm game mới.
> **Cập nhật**: 2026-10-10 — builder v4.0 (File menu, thư viện mẫu, chơi thử toàn màn hình, Cơ bản/Nâng cao, định dạng chữ, âm thanh bằng file); web builder chạy thử đủ 16 game; Unity C# đã viết + biên dịch + kiểm chứng logic nguồn khớp JS, **chưa chạy trong Unity Editor/K02**.

## Luồng làm việc dài hạn
1. **Thử logic bằng code** → chạy thử trong Unity (world code tay `FloorStoryGame/Worlds/*`).
2. **Thêm cơ chế vào web builder nếu cần**: sửa `WebTools/GenericGameBuilder/fs/*.js` (+ bản C# `FloorStoryGame/Flow/*.cs`), rồi chạy các script ở mục "Công cụ". Game mới: thêm vào `Tools/floorstory_pack/gen_games.py` → ra zip mẫu.
3. **Builder** (File → Mở zip / Game mới → chọn mẫu) → sửa ảnh, chữ, âm thanh, luật chơi (mục **Cơ bản**; vars/cảnh/flow/hành động ở mục **Nâng cao**) → **▶ Chơi thử** (toàn màn hình) → File → **Xuất gói game**.
4. **Unity**: `Tools → FloorStoryGame → Import Pack Zips` (hoặc `Tools/GenericGame/Import Zip`, tự nhận `fsdata`) → build lại aar nếu game mới (K02 cần registry) → build APK.

## 7 lớp độc lập
| Lớp | Có sẵn | Ghi chú |
|---|---|---|
| 1 Nguồn (`gen`) | `set` (bốc n hỏi 1, lọc đáp án/nhiễu bằng biểu thức, khác nhóm theo tag, mở dần vật) · `match` (chủ thể → đáp án theo tag, hoặc ngẫu nhiên) · `calc` (biến + lựa chọn tính bằng biểu thức, đáp án max/min) · `values` (khác giá trị, hỏi lớn/nhỏ) · `sequence` (chuỗi có lặp/khác nhau) · `pairs` · `number` · `none` | thêm gen = thêm hàm vào `GEN` (JS) + `FlowGens.cs` |
| 2 Hiển thị (`views`) | `cards` hàng thẻ · `props` vật tự do (hàng/cột/vòng/lưới/ngẫu nhiên/cố định + mẫu hình `tpl` bằng đối tượng) · `strip` dải hình · `grid` lưới thẻ · `seesaw` bập bênh · `stack` đống khối 3D · `floor` nền chạm | bập bênh/đống khối chỉ là view |
| 3 Dòng thời gian | `reveal` trên mỗi view: `none/sequential/all` → giữ → `cover/hide/keep`; view khác `appear: afterReveal` | |
| 4 Chạm / 5 Chấm (`judge`) | `equals` · `order` · `pairs` (lật 2 thẻ) · `taps` (đếm lượt chạm, không đúng/sai) | |
| 6 Phản hồi (`on`) | hành động theo sự kiện `start setup step right wrong end`; trống = mặc định | xem "Hành động" |
| 7 Trạng thái | `vars` (số/chuỗi, giữ qua lượt, mỗi đội riêng) + `scene` (đối tượng ràng buộc `bind`, hoạt hình `anim`) + `sync` (chế độ chung) | NgayCuaBe/BeKhoe/HinhHoc dùng vars+scene |

## Schema `meta.fsData` (trong `game.json` của zip; `assets/*` = ảnh)
```jsonc
{ "version": 3, "title": "...", "sync": false, "theme": { "bg": "#E3F2FD", "floor": "#BBDEFB" },   // "" = không vẽ
  "items": [ { "id":"sun", "label":"Mặt trời", "image":"it_0.png", "icon":"sun", "shape":"circle", "color":"#FFD23F",
               "value": 1, "tags": { "kind":"x", "period": 0 } } ],       // diện mạo: image > icon > shape+color; tags/value tự do
  "fonts": { "prompt": Style, "say": Style, "card": Style, "text": Style },  // định dạng chữ theo vai trò (tuỳ chọn)
  "sounds": { "right": "snd_1.wav", "wrong": "...", "music": "..." },         // file trong assets/ (tuỳ chọn); item.sound = tiếng của vật
  "vars": { "period": 0 },                                                 // trạng thái giữ qua lượt
  "scene": [ Obj ],                                                        // đối tượng cảnh persistent
  "flowPick": "alternate",                                                 // alternate | random (khi có nhiều flows)
  "flows": [ { "objects": [ Obj ], "gen": {"type","p"}, "views": [ {"type","p","reveal":{...},"appear":"now|afterReveal"} ],
               "judge": {"type","p"}, "text": { "prompt","ready","first","say","sayOk","wrong" }, "on": { "start":[Act], "setup":[Act], "step":[Act], "right":[Act], "wrong":[Act], "end":[Act] } } ] }
```
(`flow` đơn lẻ vẫn đọc được thay cho `flows`.) Tham số + mặc định của mô-đun nằm ở `MOD` (đầu `fs_flow.js`); thiếu trong `p` thì dùng mặc định — Unity dùng cùng mặc định (`FsModData.cs` sinh tự động).

**Style** (chữ): `{ scale (cỡ ×, 1 = mặc định), color "#hex", bold, italic, outline ("none" | "#hex"), outlineW (px), shadow }`; thiếu trường = mặc định. `prompt` = câu hỏi, `say` = lời khen/nhắc, `card` = chữ trên thẻ/tên vật, `text` = chữ trong cảnh (`look.style` ghi đè từng chữ).

**Obj** (cảnh): `{ id, role, look, x, y, dx, dy, w, h, fill, rot, alpha, scale, z:"back|front", show, anim:{type:bob|pulse,amp,speed}, smooth, bind:{prop:biểu-thức}, repeat:{n,dx,dy}, forEachItem:{where} }`.
`look`: `{shape,color}` | `{image}` | `{item:"id"|"=biểu thức"}` | `{icon}` | `{text,fs,color,outline}` | `{kind:"rrect"}` | `{kind:"hand",len,thick,color}`. x,y ∈ 0..1 của world (gốc dưới-trái); w,h theo đơn vị U (cạnh nhỏ của world, 500px); số hoặc `"=biểu thức"`. `bind` (KHÔNG cần dấu `=`): `show x y w h scale rot alpha color item text`, mỗi frame, làm mượt `smooth` giây. `role:"actor"` = nhân vật mà hành động `@actor` trỏ tới.

**Biểu thức** (`fs_expr.js` / `FsExpr.cs`, kết quả khớp từng ký tự): số, `'chuỗi'`, `$biến`, đường dẫn (`item.tags.kind`, `q.gap`, `q.subject.id`, `tapped.key`), toán tử `! - * / % + - < <= > >= == != && || ?:`, hàm `min max abs floor ceil round sqrt sin cos rand() randi(a,b) pick(i,a,b,..) has(csv,x) len at list shuf str num upper lower cap var(tên) item(id) label(id) tag(id,k) randItem(tag,giá_trị)`. Biến tự có: `$_task` (số lượt), `$_ok` (số lượt đúng ngay lần đầu), `wr`/`hr` (W/U, H/U). Tham số hành động/đối tượng: chuỗi bắt đầu bằng `=` là biểu thức. Mẫu câu: `{tên}` biến của Q (`{LABEL}` = in hoa), `{{biểu thức}}`.

**Hành động** (`fs_act.js` / `FlowWorld.Objects.cs`): `sfx say confetti bounce shake pulse move scale fade tint rotate show hide pop destroy spawn set inc bag pickItem if wait fly par text std`. Đích `on/at/to`: `"@tapped"` `"@target"` `"@actor"` `"#id"` `"=biểu thức trả id"` `{fx,fy,dx,dy}` `{ref:đích,dx,dy}` `"@tap"`. `wait:0` = không chờ xong.

**Q** (câu hỏi dạng dữ liệu): `{items, target, subject?, seq?, faces?, pairs?, number?, options?, optv?, v{}, vars{}, id, desc, answers}`. Quy tắc điểm: đúng NGAY lần đầu = +1 (`done(true)`); riêng `pairs` cộng +1 mỗi cặp. Sai `hintAfter` lần thì nhấp nháy đáp án.

## Công cụ (đều cần python; một số cần node)
| Việc | Lệnh |
|---|---|
| Sinh JSON 16 game (nguồn gốc dữ liệu game) | `python Tools/floorstory_pack/gen_games.py` → `WebTools/GenericGameBuilder/fs/games/*.json` |
| Nhúng engine + mẫu game vào builder | `python Tools/floorstory_pack/inline_web.py` (sau khi sửa `fs/*.js` hoặc game) |
| Sinh zip game (kèm ảnh gốc từ Resources) | `python Tools/floorstory_pack/make_flow_zips.py [Game ...]` → `WebTools/FloorStoryFlows/*.zip` |
| Sinh `FsModData.cs` (mặc định tham số cho Unity) | `python Tools/floorstory_pack/gen_mod_cs.py` (sau khi sửa `MOD`) |
| Gói thay-ảnh cũ (game code tay, không phải flow) | `python Tools/floorstory_pack/make_packs.py` → `WebTools/FloorStoryPacks/` |

## Code
- **Web**: `WebTools/GenericGameBuilder/fs/`: `fs_core.js` (Node/UI/tween/hình/icon/khối 3D), `fs_expr.js`, `fs_game.js` (World+Game: 2 đội, sync, điểm), `fs_act.js` (cảnh+hành động), `fs_flow.js` (MOD+GEN+VIEW+JUDGE+FlowWorld+validate). Glue UI trong `game_builder_v4.0.html` (khối "Game cơ chế FloorStory": `renderFsd`, `fsdFontControls`, `fsdSoundControls`, `openGallery`...; HTML là nguồn duy nhất).
- **Unity**: `Assets/Game/Scripts/UIScripts/FloorStoryGame/Flow/`: `FsJson` (JSON nhỏ), `FsExpr`+`FsRng` (biểu thức + random mulberry32 giống JS), `FsMod`+`FsModData`, `FlowGens` (nguồn; qua `IFlowCore` nên test được bằng .NET thuần), `FlowViews`, `FlowJudges`, `FlowWorld` + `FlowWorld.Objects` (cảnh/hành động). Controller: `FloorStoryController.CreateWorld` — có `Resources/StoryData/<game>/game.json` thì chạy `FlowWorld`, không thì world code tay cũ. Importer: `Editor/FloorStoryPackImporter.cs` (`ImportFlow`: ghi `Resources/StoryData/<id>/{game.json,ảnh}`, dựng scene `FloorStorySceneBuilder.BuildScene(id)` nếu chưa có, `GenericGameRegistryWriter.Register` nếu chưa có).
- **Kiểm chứng parity** (đã chạy): cùng seed, 8 lượt × 16 game → kết quả nguồn (`id/target/items/seq/faces/options/vars`) JS và C# trùng 100% (chênh duy nhất: `target` của gen `pairs` — không dùng). Biểu thức + RNG trùng. Cách chạy lại: biên dịch `FsJson/FsExpr/FsMod/FsModData/FlowGens` + 1 file test bằng `dotnet` đi kèm Unity (`NetCoreRuntime`).

## Chưa làm / lưu ý
- **Chưa chạy trong Unity Editor/K02** (mới biên dịch bằng Roslyn + test logic). Phần hiển thị/hoạt hình C# chưa được nhìn thấy. Khi test: Import zip → mở scene `Assets/Game/Scenes/FloorStory/<id>.unity` → Play.
- Giọng đọc: không dùng giọng của trình duyệt; người soạn tự thêm file âm thanh (`item.sound`, `sounds.*`). Unity `StoryVoice/*.mp3` cũ vẫn chưa gắn vào flow. Phát file bên Unity (`FlowWorld.PlayFile`) chưa chạy trong Editor.
- Chưa có ảnh: `Story/Day/*` (trừ vài cái dùng ảnh Family), `Story/Bi/*`, `Story/Health/*` → thẻ vẽ bằng hình tạm; thêm ảnh trong builder (đổi diện mạo vật sang "Ảnh").
- Khác biệt nhỏ so với world code tay cũ: gợi ý/hiệu ứng đơn giản hoá (vd HinhHoc không có khung nét đứt; TrungMauSac không "nở nốt" trứng còn lại; VoBongBay bóng không "bay lên").
- Game `NhanBietMau` là game mới (chưa có world code tay) → import sẽ tự đăng ký registry; nhớ build lại aar.
- **View `stack` (đếm khối) — quy tắc không che nhau (2026-10-13)**: cụm khối đẳng cự SÁT NHAU, KHÔNG xếp tầng (mọi khối thấy mặt trên). Khối nằm ngẫu nhiên trong lưới 3×3 (cụm liền nhau); n từ 4 đến 9 thi thoảng (40%) có 1–3 khối "ra ngoài" cạnh cụm cho sinh động; n = 10 thì 9 khối + 1 khối ra ngoài. Game `DemKhoiHop` giới hạn `maxCount = 10` (muốn nhiều hơn thì clone game, nâng `maxCount`; view xử lý được tới 12). Số đếm vẽ NGOÀI hình (hàng phía trên) có gạch chỉ xuống đúng khối. Chọn ô: `cubeCells` (web, `fs_flow.js`) ↔ `FlowGens.CubeCells` (Unity) — đã so khớp 40 seed × 12 số, trùng 100%; hình vẽ `FlowViews.StackView` chưa nhìn trong Unity Editor.
