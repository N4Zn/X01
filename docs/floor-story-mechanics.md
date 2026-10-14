# FloorStory "flow" — game sinh động ghép từ mô-đun (web builder ⇄ Unity)

> **TL;DR**: 1 game = kho vật + biến trạng thái + cảnh + 1..n `flow`; mỗi flow = **nguồn (gen) → hiển thị (views) → chấm (judge) → phản hồi (on.\*)**. Soạn + chơi thử trên web builder, xuất zip (`meta.kind = "fsdata"`), Unity import rồi chạy CÙNG dữ liệu. **Cả 16 game FloorStory đã có zip** (`WebTools/FloorStoryFlows/`).
> **Đọc khi**: thêm/sửa mô-đun (cơ chế mới), sửa luật chơi, đồng bộ web ↔ Unity, thêm game mới.
> **Cập nhật**: 2026-10-13 @ 63314531 (builder v4.1: sân khấu chia đôi + khối lệnh Hiệu ứng, round.sound, bỏ chữ lời hỏi, fallback tiếng/icon đúng của Unity) / 2026-10-10 — builder v4.0 (File menu, thư viện mẫu, chơi thử toàn màn hình, Cơ bản/Nâng cao, định dạng chữ, âm thanh bằng file); web builder chạy thử đủ 16 game; Unity C# đã viết + biên dịch + kiểm chứng logic nguồn khớp JS, **chưa chạy trong Unity Editor/K02**.

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
  "sounds": { "right": "snd_1.wav", "wrong": "...", "music": "..." },         // CHUNG cả game; file trong assets/ (tuỳ chọn); item.sound = tiếng của vật; flows[].sounds = {prompt,right,wrong} riêng từng dạng câu hỏi (ưu tiên hơn)
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

## Cơ chế + Round viết tay (2026-10-13)
- Mọi game là dạng round; **cơ chế** = bộ (nguồn | hiển thị | chấm). 16 game mẫu quy về ~11 cơ chế (`FSD_MECH` trong builder, khoá `gen|views|judge`). Mở game mẫu = dùng chung cơ chế; "Đổi cơ chế…" / tạo game mới có tuỳ chọn "Chỉ lấy cơ chế (xoá ảnh & vật mẫu)".
- Round: **Tự động** (sinh từ kho vật như cũ) hoặc **Viết tay**: `flows[].manual=true` + `flows[].rounds[]` (+ `roundOrder:'shuffle'` xáo mỗi vòng). Schema 1 round theo loại nguồn (vật bằng id): `set {items,target}` · `match {subject,options,answer}` · `values {items,less?}` · `sequence {items,seq[chỉ số]}` · `pairs {items}` · `number {count,options?}`; `calc/none` chỉ tự động. Round không hợp lệ (thiếu vật) → rơi về nguồn tự động. Engine: `ROUND` + `_nextRound` (fs_flow.js) ⇄ `FlowGens.FromRound` + `FlowWorld.NextRound` (C#), đã đối chiếu JS⇄C# bằng `cst/RoundTest.cs` (khớp).
- Builder: game cơ chế dùng CÙNG khung với game kéo-thả (rail "Câu hỏi" | sân khấu | tab phải Đối tượng / Câu hỏi / Luật chơi / Kho vật; ribbon File, Cài đặt chung, Trang chủ, Chèn, Hiệu ứng, Xem). Ctrl+D nhân bản câu, Delete xoá đối tượng (không chọn = xoá câu).

## Trình soạn sân khấu (web, 2026-10-13) + ghi đè vị trí `p.ov`
- `fs/fs_edit.js` (`FSW.edit`, chỉ web, KHÔNG có bản C#): chạy chính engine với 1 câu hỏi mẫu cố định (seed) trong chế độ soạn (`world.editMode`: hiện sẵn trạng thái cuối của view, bỏ dòng thời gian), phủ lớp chọn/kéo/đổi cỡ. Builder: tab **Thiết kế | Chèn | Cơ chế** khi mở game cơ chế; bảng phải theo đối tượng đang chọn.
- Đối tượng cảnh (`scene[]`, `flows[].objects[]`): kéo → `x,y` (0..1), đổi cỡ → `w,h` (đơn vị U). Đối tượng sinh tự động (`forEachItem`/`repeat`), vị trí là biểu thức hoặc `bind` x/y bị khoá (chỉnh trong Nâng cao).
- Phần tử do view tự bố trí: kéo từng cái → `views[v].p.ov = {"<chỉ số>": [fx, fy]}` (0..1 của world). Hỗ trợ view `cards`, `props`, `grid`, `strip` (engine áp SAU khi tính bố cục: JS `ovPos` trong `fs_flow.js`, C# `FlowViews.OvPos` — PHẢI khớp). Shift+kéo / "Kéo cả nhóm" đổi tham số view (`cards`: `x,y`; `props`: `x0..y1`/`cx,cy`/`pos`; `strip`: `y`) và dời luôn các `ov`. Kéo góc đổi cỡ cả nhóm (`sizeU` / `size` / `slotU`). `seesaw`, `stack`, `floor` tự bố trí, không kéo được.
- `cards` có thêm tham số `x` (tâm ngang, mặc định 0,5). Mẫu mới: Chèn → hình / chữ / ảnh / vật. **Đổi cơ chế…**: lấy `flows`, `vars`, `scene`, `theme`, `sync` từ 1 game mẫu, có thể giữ `items` của mình (nếu thiếu tag so với mẫu thì `validate` báo lỗi). Hoàn tác dùng chung Ctrl+Z.

## Builder v4.1 (2026-10-13) — bản làm tiếp; v4.0 giữ nguyên để người dùng thử
- **v4.1** (đã gộp vào `game_builder_v4.6.html`) = v4.0 + (1) **sân khấu chia đôi như lúc chơi thật**: trái = đội 1 (sửa được), phải = bản sao xem trước (HUD "Đội trái / Đội phải", vạch giữa; stage 1024×540; overlay chọn/kéo lệch `OY=40`); (2) tab **Hiệu ứng** = khối lệnh cho `flows[].on.*` (`fseRenderFx`: chọn hành động từ danh sách, trường theo từng loại, đích chọn từ danh sách "vật vừa chạm / đáp án đúng / nhân vật / đối tượng #id / toạ độ / biểu thức", `if` lồng then/else; `spawn fly bag pickItem par` sửa bằng JSON từng khối; ô JSON cả `on` ở cuối). Khối lệnh chỉ ghi `on` (schema không đổi, Unity không cần sửa).
- **v4.2** (đã gộp vào `game_builder_v4.6.html`) = v4.1 + **ảnh nền chung cho cả game** (đối tượng cảnh `{fill:true, look:{image}}` đầu `scene[]`, nút "Tải ảnh nền" ở Cài đặt chung → Chủ đề; Unity đọc như mọi đối tượng cảnh) + dòng 🔈 audio câu hỏi chung (`flows[].sounds.prompt`) cho nguồn tự động. Không đổi engine JS.
- **v4.5** (đã gộp vào `game_builder_v4.6.html`) = v4.2 + **ảnh nền cho vật/thẻ/ô** (engine JS đổi, đã back-port sang `fs/*.js` + C#): (1) `item.bgImage` + `item.figScale` (mặc định .72) — vật vẽ trên ô ảnh nền (JS `drawItem` ở `fs_game.js`, C# `FlowWorld.DrawItem`); nút "🖼 Ảnh nền cho tất cả vật"; (2) view `cards` thêm `cardImg` (ảnh nền thẻ thay màu); (3) view `strip` thêm `slotImg`, `slotImgScale`, `coverImg` (thay dấu ?), `coverImgScale` (`Mark` không tô xanh khi có `slotImg`); (4) kiểu tham số mới `img` trong `MOD` (khoá kết thúc `Img`/`Image` được xuất vào zip). C#: `FlowViews.CardsView/StripView`, `FsMod` kiểu `img`, `FsModData.cs` đã sinh lại. **v4.6** thêm `cards.cardImgScale` (ảnh nền thẻ là con của thẻ, nằm dưới hình; C# `CardsView`). Zip mẫu: `WebTools/FloorStoryFlows/NhoChuoiHinh.zip` (v4.6, strip+cards có ảnh; thay game `NhoChuoiHinh` cũ), `ConNaoNangHon.zip` (v4.2: bập bênh, ảnh nền, audio từng round). Chưa chạy trong Unity Editor.
- **`round.prompt`**: lời hỏi riêng của 1 round viết tay (ưu tiên hơn `text.prompt`/`text.first`; JS `rprompt` trong `beginTask`, C# `rprompt` trong `FlowWorld.BeginTask`). Tab Câu hỏi của round `set`/`match` dựng GIỐNG game kéo-thả (nhóm ❓ Câu hỏi + nhóm ✅ Đáp án: ảnh `mediaBox` · chữ · âm thanh · ↑↓ · ✓ · ✕ · `+ Slot` = vật mới hoặc từ kho); sửa ảnh/chữ/âm thanh của đáp án chính là sửa vật trong Kho vật.
- **`round.sound`** (2026-10-13): file audio đọc câu hỏi của 1 round viết tay; ưu tiên hơn `flow.sounds.prompt` và tiếng của đáp án (JS `Q.rsound` → `playPrompt`; C# `Q["rsound"]` → `PlayTargetItem`). Tab Câu hỏi có nút "🔈 Chèn audio"; export tự kèm file.
- **Mẫu không còn chữ lời hỏi/gợi ý/khen-chê** (2026-10-13): `gen_games.py` `strip_texts()` đặt `flow.text.*` = '', bỏ mọi `say` và nhãn tên spawn khi chạm. Giữ: nhãn trên thẻ/đáp án, nhãn cảnh cố định ("BUỔI …", "BÉ/VỪA/TO"), số trên đồng hồ, đồng hồ số. Lời hỏi do audio (round.sound / sound của vật) đảm nhận.
- **Fallback tiếng/icon đúng của Unity** (2026-10-13): khi đúng (`on.right`/std) FlowWorld phát tiếng đúng = `sounds.right` của flow/game nếu có, không thì `MusicManager.PlayCorrectSfx` (và `sfx` k=right/wrong cũng vậy), đồng thời hiện icon ✔ mặc định của Kit (`FeedbackIconBuilder`, `StoryContext.showIcon`). `FloorStoryController.UseDefaultFeedbackFx=false` để không phát 2 lần. Scene phải dựng lại (Build All Scene) để có 4 icon.
- **`round.vars` (2026-10-13)**: mỗi round viết tay có thêm `vars:{tên:giá trị}` — gán vào `world.vars` ngay khi chọn round (JS `beginTask`, C# `FlowWorld.BeginTask`), để lời hỏi / `bind` trong cảnh dùng `$tên` (vd Bi: `need`). Ô "Biến" trong tab Câu hỏi (dạng `need=uong`). Mẫu `BeKhoeManhRound` ("Chăm bạn Bi — từng câu viết tay", 21 round, sinh bằng `Tools/floorstory_pack/gen_bi_rounds.py` từ BeKhoeManh: bỏ `on.start` bag, mỗi round {3 vật, đáp án, need}).
- **Ảnh nền đối xứng (2026-10-13)**: `fsData.mirror` (mặc định bật; `false` = tắt; ô "Ảnh nền nửa phải: lật gương" ở Cài đặt chung). Nửa phải (đội 2) lật ngang (`scaleX(-1)`) ĐỐI TƯỢNG NỀN `fill` có ảnh (JS `fs_act.js` nhánh `spec.fill`; C# `FlowWorld.Objects.cs` nhánh `fill`). CHƯA lật bố cục thẻ / vật (vị trí giữ nguyên, chỉ ảnh nền đối xứng).
- **Chơi chung (`sync`) 2026-10-13**: trước đây chỉ gen `set/match/calc` dùng seed chung → `on.start` (`bag`, `rand`), `values/sequence/pairs/number`, bố trí ngẫu nhiên, xáo round vẫn lệch giữa 2 đội. Nay đầu `beginTask` (khi `sync`) đặt `rng = Rng(sharedSeed*104729 + taskNo*7919 + 13)` (JS `fs_flow.js`; C# `FlowWorld.BeginTask`) → mọi random của lượt giống nhau ở cả 2 đội. Đã test web: cả 16 mẫu 2 đội cùng câu khi `sync`, khác nhau khi không tick. C# mới biên dịch, chưa chạy Editor.
- `python Tools/floorstory_pack/inline_web.py [file.html]` mặc định nhúng vào v4.1 (v4.0 muốn nhúng lại phải truyền tên).

## Chưa làm / lưu ý
- **Chưa chạy trong Unity Editor/K02** (mới biên dịch bằng Roslyn + test logic). Phần hiển thị/hoạt hình C# chưa được nhìn thấy. Khi test: Import zip → mở scene `Assets/Game/Scenes/FloorStory/<id>.unity` → Play.
- Giọng đọc: không dùng giọng của trình duyệt; người soạn tự thêm file âm thanh (`item.sound`, `sounds.*`). Unity `StoryVoice/*.mp3` cũ vẫn chưa gắn vào flow. Phát file bên Unity (`FlowWorld.PlayFile`) chưa chạy trong Editor.
- Chưa có ảnh: `Story/Day/*` (trừ vài cái dùng ảnh Family), `Story/Bi/*`, `Story/Health/*` → thẻ vẽ bằng hình tạm; thêm ảnh trong builder (đổi diện mạo vật sang "Ảnh").
- Khác biệt nhỏ so với world code tay cũ: gợi ý/hiệu ứng đơn giản hoá (vd HinhHoc không có khung nét đứt; TrungMauSac không "nở nốt" trứng còn lại; VoBongBay bóng không "bay lên").
- Game `NhanBietMau` là game mới (chưa có world code tay) → import sẽ tự đăng ký registry; nhớ build lại aar.
- **View `stack` (đếm khối) — quy tắc không che nhau (2026-10-13)**: cụm khối đẳng cự SÁT NHAU, KHÔNG xếp tầng (mọi khối thấy mặt trên). Khối nằm ngẫu nhiên trong lưới 3×3 (cụm liền nhau); n từ 4 đến 9 thi thoảng (40%) có 1–3 khối "ra ngoài" cạnh cụm cho sinh động; n = 10 thì 9 khối + 1 khối ra ngoài. Game `DemKhoiHop` giới hạn `maxCount = 10` (muốn nhiều hơn thì clone game, nâng `maxCount`; view xử lý được tới 12). Số đếm vẽ NGOÀI hình (hàng phía trên) có gạch chỉ xuống đúng khối. Chọn ô: `cubeCells` (web, `fs_flow.js`) ↔ `FlowGens.CubeCells` (Unity) — đã so khớp 40 seed × 12 số, trùng 100%; hình vẽ `FlowViews.StackView` chưa nhìn trong Unity Editor.
