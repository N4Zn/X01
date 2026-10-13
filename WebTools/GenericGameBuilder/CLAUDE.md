# GenericGame Builder v4.0 — ghi chú dự án (schema v2.1)

Công cụ web soạn game cho runtime Unity `GenericGameController`. Trả lời user bằng tiếng Việt.

## File
- **Bản mới nhất: `game_builder_v4.1.html`** (v4.0 giữ nguyên để người dùng thử; v4.1 = v4.0 + sân khấu game cơ chế chia đôi 2 đội + tab Hiệu ứng khối lệnh, xem `docs/floor-story-mechanics.md` mục "Builder v4.1"). Ghi chú bên dưới nói "v4.0" thì áp dụng cả v4.1.
- **Mới nhất: `game_builder_v4.6.html`** (= v4.5 + `cardImgScale`). Trước đó: `game_builder_v4.5.html` (v4.2 = ảnh nền chung + audio câu hỏi chung; v4.5 = ảnh nền vật/thẻ/ô, engine JS đã back-port vào `fs/*.js`; xem `docs/floor-story-mechanics.md`). `python Tools/floorstory_pack/inline_web.py game_builder_v4.6.html` tái tạo y hệt file (đã kiểm). Mặc định `inline_web.py` vẫn trỏ v4.1.
- (cũ) `game_builder_v4.0.html` (1 file, 1 IIFE → `game` không truy cập được từ ngoài). Các file `game_builder*.html` khác là bản cũ/backup (v1, 1002...).
- Publish làm Claude Artifact: https://claude.ai/artifact/J8VjLjobGYkDWJq456ddQ8
- Unity runtime: `Assets/Game/Scripts/Core/MiniGameKit/GenericGame/` — `GenericGameTypes.cs`, `GenericGameMigration.cs`, `GenericGameController.cs`, `SCHEMA.md`, `capabilities.json`; thêm `UIScripts/_TestTongHop/Items/ButtonItem.cs`.

## Mô hình v2.1
- `rounds[] = {target, question, answers, collect}`; mỗi cái là Group: `arrangement` (manual|matrix|random), `area`, `bgImage`, `bgIcon`, `mirror`, `autoStretch`, `fillByValue`, `fillReverse`, `slots[]`.
- Slot: ảnh nền (mặc định stretch), text/icon (có icon thì text = số icon, không hiện), sound, `fx` ghi đè hiệu ứng chung (trigger có ≥1 hiệu ứng, kể cả "None", hoặc sound → thay hẳn global).
- Item (`layout.decorations`) = đồ trang trí cả game; trigger onCorrect/onPartial/onWrong/onClick; hiệu ứng Fly* bị bỏ qua.
- `settings.iconColumns` mặc định 2. File v1 tự migrate (web + Unity).
- Toạ độ: % của 1 nửa (trái soạn, phải mirror). Combined: câu hỏi là 1 vùng chung toàn màn hình, toạ độ % cả stage (`layout.questionSpace="stage"`), kéo tự do 2 nửa, đáp án độc lập mỗi bên. Independent: câu hỏi mỗi bên, có `question.mirror`.
- Vùng đã chọn (FlyToStay) theo round: `rounds[].collect`, tab "📥 Đã chọn"; có ô tick không mirror (`collect.mirror`).
- Âm câu hỏi: phát đầu round, lặp sau 4s, chỉ ở Combined. HUD điểm chơi thử bị ẩn (chỉ web).

## Layers & audio (2026-10-03)
- Panel "Layers" (tab Nhân vật/Item): kéo từng dòng đổi thứ tự → `layout.layerOrder` (xem SCHEMA.md). Web gán `z-index = zOf(key)`; Unity `ApplyLayer`. Mặc định Nền < Câu hỏi < Item < Đáp án < Đáp án bay < Icon.
- Nút "+ Thêm ô" copy NGUYÊN slot cuối của nhóm (kể cả đúng/sai, âm thanh, fx).
- Nút ▶ nghe thử giờ toggle ▶/■ (`togglePreviewAudio`); âm thanh câu hỏi chơi thử có nhãn trạng thái `#ptAudioState`, bị trình duyệt chặn thì chờ chạm.
- Spawn: `clearAllSpawnItems` dọn cả `.pt-slot` sót (đáp án cuối không còn hiện sang round sau).
- **Đừng dùng script Python cắt/chèn theo `index()` trên file HTML này**: chuỗi như `moveCol` xuất hiện nhiều chỗ, từng cắt nhầm ~300 dòng. Dùng Edit.

## Việc còn dở
- Unity đã theo `collect.mirror` (`CollectFlipsFor` trong controller; SCHEMA.md/capabilities.json đã cập nhật).
- Code Unity mới chỉ compile-check bằng Roslyn (0 lỗi), **chưa chạy trong Editor**: cần test round/collect/mirror/icon grid.

## Chờ cả 2 đội (2026-10-08)
- `settings.waitBothTeams` (ô tick `#waitBothTeams`, chỉ hiện khi 2 đội = Gộp): 2 đội cùng câu, cả 2 xong mới sang câu. Web preview: `waitBothOn()`, `teamDoneCombined()`, `combinedWin()`, `waitBothWrong()` (quanh `onCombinedPick`/`onSpawnPickCombined`/`sumToTargetPick`). Unity: xem SCHEMA.md mục "Chờ cả 2 đội". Chưa test trong Editor.
- Chữ SỐ (text chỉ gồm chữ số): web preview đậm + viền đen, trắng trừ khi đặt màu riêng (`.stxt-num`), khớp `NumberTextStyle` bên Unity.

## Lộ ảnh hoàn chỉnh (2026-10-08, port từ `game_builder_v2_6_1004_reveal.html`)
- Slot câu hỏi: `hideOnCorrect` (👻, mờ đi sau khi đúng + giữ màn hình thêm 1.8s `holdMsFor`), `sendToBack` (⬇, vẽ sau). `FlyToStay.stayFor` (giây ở lại rồi biến mất). Nút ⧉ khớp vị trí slot 1 (chỉ web). Unity: `RevealQuestionOnCorrect`, `OrderQuestionSlots`, `FadeAndDestroyAfter`; SCHEMA.md mục "hideOnCorrect/sendToBack" + "stayFor".
- Chưa port: hold 1.8s ở spawn flow / SumToTarget; chưa test trong Editor (game mẫu `GhepAnhConThieu`).
- File reveal gốc là fork của bản cũ — KHÔNG ghi đè builder bằng nó (mất `waitBothTeams`, chữ số viền, `effFx` gộp từng phần).

## Game cơ chế FloorStory — "flow" v3 (2026-10-10)
- **Khung v4.0 (editor kéo-thả)**: hàng tab `#ribTabs` = **File | Cài đặt chung | Trang chủ | Chèn | Hiệu ứng | Xem** (dựng bằng `initShell()`; File cũng là tab, nút gốc `btnNewFs/btnOpenGame/btnSaveAs/btnExport` nằm trong `#fileMenu` ẩn, ribbon dùng nút proxy) + `#ribbon`; `#colRail` (danh sách round, `renderRail()`) | `#colLayout` (sân khấu) | `#colRounds` (thuộc tính round đang chọn + `#decoHost` item đang chọn + `#fxPane` ngăn hiệu ứng). Tab **Cài đặt chung** chứa THẲNG các ô của cột cũ `#colSettings` (đã ẩn): `initShell` DỜI phần tử (JS cũ buộc theo id nên vẫn chạy). **Hiệu ứng gắn theo đối tượng đang chọn** (`renderFxPane`, `shellSync` bọc `renderStage/renderRounds/renderDecoList`): chọn ô → hiệu ứng riêng của ô, chọn item → `.deco-fx` của item, không chọn → hiệu ứng chung. **Thư viện ảnh đáp án sai** chỉ hiện khi `meta.genre='autogen'` hoặc game đã có `imagePools`. Phím: **Ctrl+D** nhân bản round, **Delete** xoá đối tượng đang chọn (không chọn gì = xoá round; Ctrl+Z hoàn tác). Gói ảnh chỉ hiện tab File; game cơ chế dùng cùng tên tab (File, Cài đặt chung, Trang chủ, Chèn, Hiệu ứng, Xem) nhưng pane riêng `fset/fhome/fins/ffx/fview` (`initFsdShell`, `shellModeSync`). File đã đổi tên `game_builder_v4.0.html`.
- **Builder v4.0 (2026-10-10)**: thanh trên 1 hàng = menu **File** (Game mới… / Mở / Lưu thành / Xuất zip) + tên game + nhãn `v4.0` + `▶ Chơi thử` ngoài cùng bên phải (luôn hiện). Game cơ chế chơi thử trong cùng khung toàn màn hình `#playtest` (không còn khung chơi thử nhỏ). **File → Game mới** mở thư viện mẫu `#galDlg` (Trang trống kéo-thả + 16 game FloorStory) → đặt mã game → sửa tiếp. Thiết kế tổng thể + lộ trình: **`docs/game-builder-design.md`** (bản nháp).
- Game cơ chế (`meta.kind="fsdata"`) panel `#fsdPanel` chia **Cơ bản** (mã game, nền/sàn, Chữ, Âm thanh, luật chơi dạng form, ảnh & vật) và **⚙ Nâng cao** (vars, scene, flow/view/chấm, hành động JSON) để giáo viên không bị rối. Zip xuất mở lại được; Unity import qua `FloorStoryPackImporter.ImportFlow`. Schema, biểu thức, hành động, công cụ, việc còn lại: **`docs/floor-story-mechanics.md`**.
- Engine = `fs/fs_core.js, fs_expr.js, fs_game.js, fs_act.js, fs_flow.js`; dữ liệu 16 game = `fs/games/*.json` (sinh từ `Tools/floorstory_pack/gen_games.py`). **Sửa xong phải chạy `python Tools/floorstory_pack/inline_web.py`** (nhúng vào HTML giữa `<!--FS-JS-BEGIN/END-->`, đừng sửa tay vùng này) + `gen_mod_cs.py` nếu đổi `MOD` + `make_flow_zips.py` nếu đổi game. Luật JS và C# (`FloorStoryGame/Flow/*.cs`) PHẢI khớp.
- Glue UI nằm trong script chính của `game_builder_v4.0.html` (khối "Game cơ chế FloorStory": `renderFsd`, `fsdFontControls`, `fsdSoundControls`, `openGallery`, `fsdPlay`...) — **HTML là nguồn duy nhất** (đã bỏ `glue_v3.js`/`splice_glue.py`); sửa bằng Edit/replace có kiểm tra số lần khớp, không cắt theo `index()`.
- **Khung chung cho game cơ chế (2026-10-13)**: fsd-mode KHÔNG còn form riêng — `main` hiện lại; `#colRail` = danh sách câu hỏi (`fseRenderRail`: Tự động / Viết tay, round cards), `#colStageF` = sân khấu (`#fseHost`), `#colSideF` = tab Đối tượng (`#fseSide`) | Câu hỏi (`#fseRound`, `fseRenderRound`: chọn vật + đáp án theo loại nguồn) | Luật chơi (`#fsdBasic`+`#fsdAdv` dời vào) | Kho vật (`#fsdItems` dời vào). Form cũ trong `#fsdPanel` (ẩn) được `initFsdShell` DỜI vào ribbon/popover (Game ID, nền, chữ, âm thanh, cơ chế). Cơ chế = chữ ký `gen|views|judge` (`FSD_MECH`). Round viết tay: `flow.manual` + `flow.rounds[]`.
- **Trình soạn sân khấu game cơ chế (2026-10-13)**: `fs/fs_edit.js` (`FSW.edit`) + khối "TRÌNH SOẠN SÂN KHẤU" trong HTML (`fseMount`, `fseRenderProps`, `fseRenderMech`, `initFsdShell` — các pane `.fsd-only` chỉ hiện ở fsd-mode). Kéo/đổi cỡ đối tượng cảnh (`scene[]`, `flow.objects[]`), kéo từng phần tử của view → `p.ov` (cards/props/grid/strip; C# `FlowViews.OvPos`), Shift+kéo = cả nhóm, Đổi cơ chế từ game mẫu. Phím: Delete, Ctrl+D, mũi tên. Chi tiết: `docs/floor-story-mechanics.md`. Thêm file `fs_*.js` mới phải thêm vào `order` của `inline_web.py`.
- **Định dạng chữ**: `fsData.fonts{prompt,say,card,text}` = `{scale,color,bold,italic,outline("none"|"#hex"),outlineW,shadow}`; `look.style` (cùng dạng) ghi đè cho chữ trong cảnh. Web: `UI.label(..., role, style)` (`fs_core.js`); Unity: `StoryUI.TextStyle` + `FlowWorld.StyleFrom`. Chưa có định dạng chữ cho game kéo-thả v2 (xem design doc).
- **Âm thanh**: chỉ thêm file, KHÔNG dùng giọng đọc của trình duyệt (đã bỏ `speechSynthesis`). `item.sound` (tiếng vật: phát khi câu hỏi nhắc tới vật + khi chạm), `fsData.sounds{right,wrong,music}` (CHUNG cả game) và `flows[].sounds{prompt,right,wrong}` (riêng từng dạng câu hỏi/round, ưu tiên hơn tiếng chung; `prompt` ưu tiên hơn tiếng của vật). Game kéo-thả v2 vốn đã có âm thanh theo từng slot/round. Xuất zip giữ file đang dùng; Unity phát qua `FlowWorld.PlayFile` (Resources/StoryData/<id>/). Chưa chạy trong Unity Editor.
- Test nhanh: `python -m http.server 8765` ở gốc repo, mở `/WebTools/GenericGameBuilder/game_builder_v4.0.html`; `FSW.play(host, FSW.TEMPLATES.<Game>.data, {mute:true})`, `game.worlds[i]._Q / .elems / .vars` để bấm thử bằng `node.el.dispatchEvent(new PointerEvent('pointerdown'))`.
- Bẫy đã gặp: `bind` là biểu thức THUẦN (không dấu `=`), tham số còn lại mới cần `=`; tên biến/`id` động dùng `"=\"part_\"+item.id"`; trong `.fs-n` đặt `left/top:50%` nên node `fill` phải ghi đè width/height inline.

## Chế độ gói FloorStory (2026-10-10)
- Zip có `meta.kind="floorstory"` + `meta.floorStory.images[]` (`{key,label,group,file,default}`) → body có class `fs-mode`: ẩn `main`/Chơi thử/Lưu thành, hiện `#fsPanel` (thẻ ảnh: Thay ảnh, kéo-thả, ↺ Gốc, Đặt lại tất cả). Code: khối "Gói FloorStory" trước `importGamePackage` (`fsPack/fsReplace/renderFsPanel/updateFsMode`). Thay ảnh = `fileToAsset(file,'fs')` rồi gán `img.file`; `file===default` = chưa thay.
- Xuất dùng `doExport` bình thường (meta giữ nguyên). Round/layout rỗng mặc định — Unity bỏ qua. Tạo gói: `Tools/floorstory_pack/make_packs.py`; import: Unity `FloorStoryPackImporter`. Chi tiết `docs/floor-story-game.md`.

## Wait-to-do (làm sau, chưa ưu tiên)
- [ ] **Safari: chưa chơi thử (Play/preview) được** — builder chạy trên trình duyệt Safari không vào được chế độ chơi thử. Chưa điều tra nguyên nhân (nghi: autoplay audio bị chặn, `requestAnimationFrame`/API chưa hỗ trợ, hoặc cú pháp JS/CSS Safari không nhận). Khi làm: test trên Safari thật (macOS/iOS), mở Web Inspector xem lỗi Console. Hiện dùng Chrome/Edge.

## Lưu ý kỹ thuật
- Test web: click DOM + `javascript_tool`; browser pane ẩn nên polyfill `requestAnimationFrame` bằng setTimeout; server local `node srv.js` cổng 8765.
- Unity JsonUtility không có dict/null int; field class luôn được khởi tạo mặc định → "có override" xét theo nội dung.
- Khi sửa script bằng bash: backtick trong `node -e` nháy kép bị hỏng → viết script qua file.

## Giao diện cột trái (2026-10-05)
- Cột "Cài đặt chung": mọi ghi chú gom vào nút `?` cạnh tiêu đề (`#settingsHelp`); các mục khác cũng ẩn `.hint` sau `?` (JS cuối file). Mặc định MỌI mục đóng, nhớ trạng thái mở bằng localStorage (`gb.open.*`).
- Lựa chọn 2 trạng thái dùng `bindCycle` (1 nút, click đổi giá trị + đổi chữ): 2 đội, Điểm, Đếm ngược, Chạm sai. Kiểu đáp án = dropdown `#selAnswerMode`. Đã bỏ ô Chủ đề/nhóm (meta.category vẫn nằm trong json, khai báo sau) và ô tick "chỉ phát âm thanh".
- Cỡ chữ mặc định 50 + màu chữ (`questionTextColor`/`answerTextColor`) cùng 1 dòng.
- Hiệu ứng web: mọi chuyển động CSS đi qua `runTransition` (ép reflow trước khi đổi giá trị đích — thiếu thì "Mờ dần"/"Bay" nhảy thẳng tới cuối); bản sao hiệu ứng đánh `data-collected` để khoá ô không reset transform; `doPartial` ẩn ô gốc khi có hiệu ứng thật.
