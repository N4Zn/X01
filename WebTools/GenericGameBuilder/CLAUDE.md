# GenericGame Builder (bản cuối v4.19) — ghi chú dự án (schema v2.1)

Công cụ web soạn game cho runtime Unity `GenericGameController`. Trả lời user bằng tiếng Việt.

## File
- **Bản làm việc duy nhất: `game_builder_v4.19.html`** (đã gộp v4.9 (giải mã ảnh trước khi chạy thử, `run()` chạy bước đầu ngay) + v4.11 (`soundWrong`: tiếng vật khi chạm SAI, mặc định 0; C# `FlowJudges.Equals` khớp); đã gộp v4.2 = ảnh nền chung + audio câu hỏi chung; v4.5 = ảnh nền vật/thẻ/ô; v4.6 = `cardImgScale`; nền v4.1 = sân khấu chia đôi 2 đội + tab Hiệu ứng khối lệnh — xem `docs/floor-story-mechanics.md` mục "Builder"). **v4.19 là bản cuối**; mọi bản cũ (v4.0/4.1/4.2/4.5/4.6) đã xoá (còn trong git history). Ghi chú bên dưới nói "v4.0/v4.1" thì áp dụng cho v4.19 (v4.7 = thẻ cards `bg` trống/none → trong suốt; v4.8 = sửa Đếm khối hộp, đếm theo hàng + khối lẻ tách riêng, âm câu hỏi chung 2 đội, icon ✔/✖ mặc định, `flow.noIcon`). `python Tools/floorstory_pack/inline_web.py` (mặc định v4.19) tái tạo phần engine + mẫu game trong HTML từ `fs/*.js` + zip `WebTools/FloorStoryFlows/*.zip`.
- `game_builder_v4.19.html` là 1 file, 1 IIFE → `game` không truy cập được từ ngoài. Không còn file `game_builder*.html` nào khác trong thư mục (bản cũ/backup ở `WebTools/Old/` hoặc git history).
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
- **Builder v4.0 (2026-10-10)**: thanh trên 1 hàng = menu **File** (Game mới… / Mở / Lưu thành / Xuất zip) + tên game + nhãn phiên bản (nay `v4.19`) + `▶ Chơi thử` ngoài cùng bên phải (luôn hiện). Game cơ chế chơi thử trong cùng khung toàn màn hình `#playtest` (không còn khung chơi thử nhỏ). **File → Game mới** mở thư viện mẫu `#galDlg` (Trang trống kéo-thả + 16 game FloorStory) → đặt mã game → sửa tiếp. Thiết kế tổng thể + lộ trình: **`docs/game-builder-design.md`** (bản nháp).
- Game cơ chế (`meta.kind="fsdata"`) panel `#fsdPanel` chia **Cơ bản** (mã game, nền/sàn, Chữ, Âm thanh, luật chơi dạng form, ảnh & vật) và **⚙ Nâng cao** (vars, scene, flow/view/chấm, hành động JSON) để giáo viên không bị rối. Zip xuất mở lại được; Unity import qua `FloorStoryPackImporter.ImportFlow`. Schema, biểu thức, hành động, công cụ, việc còn lại: **`docs/floor-story-mechanics.md`**.
- Engine = `fs/fs_core.js, fs_expr.js, fs_game.js, fs_act.js, fs_flow.js`; dữ liệu game = **zip `WebTools/FloorStoryFlows/*.zip` là NGUỒN DUY NHẤT** (đã bỏ `fs/games/*.json`, `gen_games.py`, `make_flow_zips.py`). Sửa game: mở zip trong builder → sửa → xuất zip → ghi đè zip trong `FloorStoryFlows/` → Unity import (hoặc `python Tools/floorstory_pack/sync_storydata.py` khi chưa mở Editor). **Chọn mẫu ở "Game mới"**: thử nạp zip gốc kèm ảnh (`fsTplZip`: `./<id>.zip` cạnh html — copy html đi đâu thì copy kèm zip game cùng thư mục — rồi `../FloorStoryFlows/<id>.zip`; chỉ đọc được khi chạy qua http). Không đọc được (file://, hoặc không có zip) thì IM LẶNG dùng dữ liệu mẫu nhúng sẵn (cách ban đầu, không ảnh) — KHÔNG hỏi chọn file zip. Mở xong luôn lưu thành game khác được. **Sửa xong phải chạy `python Tools/floorstory_pack/inline_web.py`** (nhúng engine + mẫu đọc từ zip vào HTML giữa `<!--FS-JS-BEGIN/END-->`, đừng sửa tay vùng này) + `gen_mod_cs.py` nếu đổi `MOD`. Luật JS và C# (`FloorStoryGame/Flow/*.cs`) PHẢI khớp.
- Glue UI nằm trong script chính của `game_builder_v4.19.html` (khối "Game cơ chế FloorStory": `renderFsd`, `fsdFontControls`, `fsdSoundControls`, `openGallery`, `fsdPlay`...) — **HTML là nguồn duy nhất** (đã bỏ `glue_v3.js`/`splice_glue.py`); sửa bằng Edit/replace có kiểm tra số lần khớp, không cắt theo `index()`.
- **Khung chung cho game cơ chế (2026-10-13)**: fsd-mode KHÔNG còn form riêng — `main` hiện lại; `#colRail` = danh sách câu hỏi (`fseRenderRail`: Tự động / Viết tay, round cards), `#colStageF` = sân khấu (`#fseHost`), `#colSideF` = tab Đối tượng (`#fseSide`) | Câu hỏi (`#fseRound`, `fseRenderRound`: chọn vật + đáp án theo loại nguồn) | Luật chơi (`#fsdBasic`+`#fsdAdv` dời vào) | Kho vật (`#fsdItems` dời vào). Form cũ trong `#fsdPanel` (ẩn) được `initFsdShell` DỜI vào ribbon/popover (Game ID, nền, chữ, âm thanh, cơ chế). Cơ chế = chữ ký `gen|views|judge` (`FSD_MECH`). Round viết tay: `flow.manual` + `flow.rounds[]`.
- **Trình soạn sân khấu game cơ chế (2026-10-13)**: `fs/fs_edit.js` (`FSW.edit`) + khối "TRÌNH SOẠN SÂN KHẤU" trong HTML (`fseMount`, `fseRenderProps`, `fseRenderMech`, `initFsdShell` — các pane `.fsd-only` chỉ hiện ở fsd-mode). Kéo/đổi cỡ đối tượng cảnh (`scene[]`, `flow.objects[]`), kéo từng phần tử của view → `p.ov` (cards/props/grid/strip; C# `FlowViews.OvPos`), Shift+kéo = cả nhóm, Đổi cơ chế từ game mẫu. Phím: Delete, Ctrl+D, mũi tên. Chi tiết: `docs/floor-story-mechanics.md`. Thêm file `fs_*.js` mới phải thêm vào `order` của `inline_web.py`.
- **Định dạng chữ**: `fsData.fonts{prompt,say,card,text}` = `{scale,color,bold,italic,outline("none"|"#hex"),outlineW,shadow}`; `look.style` (cùng dạng) ghi đè cho chữ trong cảnh. Web: `UI.label(..., role, style)` (`fs_core.js`); Unity: `StoryUI.TextStyle` + `FlowWorld.StyleFrom`. Chưa có định dạng chữ cho game kéo-thả v2 (xem design doc).
- **Âm thanh**: chỉ thêm file, KHÔNG dùng giọng đọc của trình duyệt (đã bỏ `speechSynthesis`). `item.sound` (tiếng vật: phát khi câu hỏi nhắc tới vật + khi chạm), `fsData.sounds{right,wrong,music}` (CHUNG cả game) và `flows[].sounds{prompt,right,wrong}` (riêng từng dạng câu hỏi/round, ưu tiên hơn tiếng chung; `prompt` ưu tiên hơn tiếng của vật). Game kéo-thả v2 vốn đã có âm thanh theo từng slot/round. Xuất zip giữ file đang dùng; Unity phát qua `FlowWorld.PlayFile` (Resources/StoryData/<id>/). Chưa chạy trong Unity Editor.
- Test nhanh: `python -m http.server 8765` ở gốc repo, mở `/WebTools/GenericGameBuilder/game_builder_v4.19.html`; `FSW.play(host, FSW.TEMPLATES.<Game>.data, {mute:true})`, `game.worlds[i]._Q / .elems / .vars` để bấm thử bằng `node.el.dispatchEvent(new PointerEvent('pointerdown'))`.
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

## Cài đặt hiển thị thống nhất (2026-10-14) — theo kiểu PowerPoint
- **Tab Hiệu ứng** (mọi game): panel bên phải = **Hiệu ứng chung của game** khi không chọn gì (khi đúng / khi sai / mọi sự kiện + icon ✔/✖ mặc định + **Âm thanh chung** đúng/sai/nhạc nền); **chọn 1 đối tượng trên sân khấu → panel tự đổi thành hiệu ứng RIÊNG của nó** (`fseRenderObjFx`: hiệu ứng chờ nhún / phập phồng), bấm vùng trống để về hiệu ứng chung.
- Tab Hiệu ứng có CÙNG bố cục ở mọi game: ribbon `Phản hồi` (Khi đúng · Khi sai · Chờ chạm/Mọi sự kiện · Âm thanh) + panel phải **mở sẵn** hết các sự kiện (kể cả âm thanh, icon); nhạc nền (`bgAudioRow`) nằm trong panel Hiệu ứng, không ở Cài đặt chung.
- **Âm thanh là 1 phần của hiệu ứng**: KHÔNG có cài đặt âm thanh riêng ở Cài đặt chung / không có tab "Âm thanh" riêng; nút Hiệu ứng > Âm thanh chỉ cuộn tới mục âm thanh trong panel Hiệu ứng.
- Icon ✔/✖ mặc định: cài đặt GAME `fsData.noIcon` (1 = tắt; thiếu = hiện); JS `World.showIcon` ↔ C# `FlowWorld.ShowIcon`. Đã bỏ `flow.noIcon`.
- Ghi chú cài đặt chung = nút **"?"** ở cuối thanh tab (`#ribHelp`), mở popover; hiện Ở MỌI game (builder là 1 tool, không có nút nào chỉ hiện ở 1 nhóm game).
- Còn popover: Định dạng chữ, Tuỳ biến cơ chế.
- **Giao diện chưa ok — tối ưu sau** (user ghi nhận 2026-10-14; xem TODO.md).

## Số đếm khối hộp (view `stack`, 2026-10-14)
- Số đếm xếp theo HÀNG: mỗi hàng khối của cụm chính (cùng x) = 1 hàng số nằm ngang (1-2-3 / 4-5-6 / 7-8-9), hàng đầu cao nhất, hàng sau thấp dần (`ROW_STEP`=0,9s); khối lẻ ngang hàng với hàng cuối. JS `rowTiers`/`badgeLayout` ↔ C# `StackView.RowTiers`/`BadgeLayout` PHẢI khớp.

## Thứ tự câu 2 đội (2026-10-14)
- MẶC ĐỊNH 2 đội phải có thứ tự câu hỏi/đáp án KHÁC nhau (seed riêng mỗi world, tránh nhìn bài nhau). Chỉ chế độ chơi chung (`sync`, 1 luồng audio) mới cùng seed/cùng câu. Unity: `FlowWorld._seedSerial`; web: mỗi `World` `Math.random`.

## Chạm sai & icon wrong (2026-10-14) — builder là 1 tool chung, FloorStory chỉ là mẫu
- Cài đặt **"Chạm sai"** (`settings.wrongEndsRound`): MẶC ĐỊNH của builder = **sai là hết câu** (tick). Bỏ tick = cho chọn lại. Game cơ chế (fsdata) có thêm ô `#fsdWrongEnds` ở tab Cài đặt, ghi cả `fsData.wrongEndsRound` lẫn `settings.wrongEndsRound`; thiếu khoá = true. **17 game mẫu FloorStory ghi `wrongEndsRound:false`** (cho thử lại) — đó là chọn lựa của MẪU, KHÔNG có nghĩa FloorStory không có tuỳ chọn sai-hết-câu.
- Quy tắc icon: chạm sai KẾT THÚC câu → hiện icon ✖ (kèm âm sai) rồi sang câu sau, câu tính sai. Cho chọn lại → chỉ phát âm sai, KHÔNG icon. Icon ✔ khi đúng (tắt riêng bằng `flow.noIcon`). Engine: `FlowWorld.WrongEnds/ShowIcon` + judge equals/order (C#), `World.wrongEnds/showIcon` (JS). Ghép cặp (pairs) không tính (lật sai là bản chất trò).
- Icon + âm thanh web lấy ĐÚNG của Unity gốc: icon = vòng tròn #26C752/#E02E2E + ✔/✖ (FeedbackIconBuilder); âm đúng/sai = `correct.ogg`/`wrong.ogg` nhúng qua `fs/fs_sfx.js`.

## Merge v4.10–v4.15 (2026-10-14) — cái nào cứng, cái nào là setting

Nguồn: `game_builder_v4.16.html` trên Drive. Phần HTML vỏ của bản đó cũ hơn bản repo (đã có sẵn), chỉ lấy phần engine `fs/*.js`.

| Thay đổi | Loại | Ghi chú |
|---|---|---|
| tween `shake/bounce/pulse` không chồng nhau, vật không trôi khỏi chỗ (v4.14) | **Cứng** (engine) | JS `fs_core.js` ↔ C# `StoryUI.TweenState` |
| vật có khung riêng (`bgImage`) vẽ cùng cỡ ô "?" / bỏ thẻ trắng (v4.13, v4.15) | **Cứng** | dữ liệu vật đã có `bgImage`; strip + grid |
| chọn đúng thì sáng lên thay vì mờ (strip, v4.15) | **Cứng** | C#: `Outline` vàng trên hình |
| nút "＋" trình sửa chuỗi tôn trọng `noAdjacent` | **Cứng** (glue editor) | |
| chuỗi không 2 hình liền nhau (v4.11) | **Setting** `gen sequence.noAdjacent`, mặc định **1** | đặt 0 để cho phép lặp liền |
| phát tiếng vật khi chạm sai (v4.10) | **Setting** `judge equals.soundWrong`, mặc định **1** | |
| ảnh mặt úp lưới thẻ (v4.13) | **Setting** `view grid.backImg` (mặc định không) + `backImgScale` (mặc định 1) | |

Zip mới nạp: `NhoChuoiHinh2` (ghi đè), `LatTheNhoGiong2` (game mới — cần chạy Unity importer để tạo scene/registry/ảnh).

## Thứ tự phản hồi khi đáp án có tiếng (2026-10-14)
Chạm đáp án → phát NGAY tiếng của vật → chờ tiếng xong + nghỉ 0,2s → mới hiện icon + phát tiếng đúng/sai (hiệu ứng khác). Áp cho judge `equals` (cả chạm sai khi `soundWrong`=1). JS `JUDGE.equals` + `World.playItem/playItemVoice` ↔ C# `FlowJudges.Equals` + `FlowWorld.PlayItem` (trả bool = có phát). Trước đây nghỉ 0,2s nằm TRƯỚC tiếng vật và icon/tiếng đúng phát chồng lên.

## `wrongEndsRound` — 1 giá trị, 2 chỗ lưu (2026-10-14)
Lưu ở `settings.wrongEndsRound` (nút "Chạm sai" ở Cài đặt chung) VÀ `meta.fsData.wrongEndsRound` (engine web + Unity chỉ đọc cái này; thiếu khoá = sai là hết câu). Lỗi cũ: đổi nút "Chạm sai" chỉ ghi `settings`, còn mở lại game thì `fsData` thiếu khoá đè ngược `settings` → web hiện "Cho thử lại" nhưng Unity "hết câu". Nay: đổi nút hoặc ô tick đều ghi cả hai; mở zip thiếu khoá fsData thì lấy từ `settings`; xuất zip luôn ghi tường minh `fsData.wrongEndsRound`; nút được vẽ lại khi nạp game.

## `iconAlign` — icon ✔/✖ ngang tâm đáp án (2026-10-14)
Cài đặt game `fsData.iconAlign = "answers"` (builder: tab Hiệu ứng → ô tick "Icon ✔/✖ nằm ngang tâm đáp án"; mặc định tắt = icon giữa màn hình). Bật thì mỗi lần hiện icon, tâm y của icon = trung bình y các đáp án (elem có `key`) — không phụ thuộc độ phân giải. Unity: `FloorStoryController.AlignIconsToAnswers` + `FlowWorld.TryAnswersWorldY`; web: `Game.showIcon`. NhoChuoiHinh2 bật. (Bản trước dùng `iconOffsetY` dịch cố định theo tỉ lệ → icon tụt ra ngoài màn hình; đã bỏ.)

## Merge v4.16 + sửa Unity (2026-10-14)
- v4.16 (Drive) chỉ thêm view `grid` param `shape` = `grid` | `u` (U: hàng trên đủ thẻ, còn lại xếp 2 bên) → **setting** (mặc định `grid`). JS `fs_flow.js` ↔ C# `GridView` (`uShape`, ô `i >= cols` → `r = 1 + (i-cols)/2`, `c = lẻ ? cols-1 : 0`).
- Strip "chọn đúng thì sáng lên": web = CSS drop-shadow theo viền hình. Unity bản đầu dùng `UI.Outline` → vẽ 4 bản sao lệch của khung → **nhìn như 2 viền**. Nay Unity dựng 1 Image "Glow" cùng sprite, to ×1.14, vàng, nằm ngay dưới hình (fade-in 0.3s), tắt khi Cover/Hide. Không dùng Outline cho hình có ảnh.
- Zip mới: `LatTheNhoGiong2` (kiểu U) — cần chạy Unity importer lần đầu (scene/registry/ảnh).

## v4.19 (2026-10-20) — 2 cài đặt chung về đáp án (game kéo-thả / GenericGame)
- `settings.keepAnswerOrder` (mặc định false): true = KHÔNG xáo — đáp án i đúng ô i như người soạn đặt ở từng round. Web: `renderAnswers` bỏ bước xáo `order`. Unity: `ButtonDisplay.KeepAnswerOrder` (mặc định false nên các game khác dùng chung ButtonDisplay không đổi) do `GenericGameController` gán từ settings; `PickSlots` trả thứ tự gốc. Kiểu bố trí `random` vẫn chọn VỊ TRÍ ô ngẫu nhiên (ý người soạn).
- `settings.mirrorAnswers` (mặc định true): false = vị trí/vùng ô đáp án 2 đội GIỐNG HỆT (không lật nửa phải). Một nguồn cho cả game; `answers.mirror` từng round không còn tác dụng với nhóm đáp án (câu hỏi/thu thập vẫn dùng `mirror` riêng). Web: `positionsFor(g, side, isAnswers)`, `flipRight` ở sân khấu soạn, vùng spawn; Unity: `ComputePositions(g, team, answers:true)`, `SetArea` của spawn, tutorial `mirrorAnswers`.
- UI: tab Cài đặt chung → nhóm "Đáp án" (2 ô tick). Game cũ thiếu 2 field → mặc định (xáo + lật) như trước, không đổi hành vi.
- **Game FloorStory (fsData) không cần 2 cài đặt này**: round viết tay (`flow.rounds`) vốn giữ nguyên thứ tự `items/options` do người soạn ghi (không xáo; chỉ `pairs` xáo vì đó là luật chơi), và bố cục thẻ/vật vốn KHÔNG lật gương cho đội phải (chỉ ảnh nền có tick `mirror`). Nguồn tự động (`gen`) có tham số `shuffle` riêng.
- Chế độ "Tự đặt từng ô": thêm ô nhập `Rộng ô% / Cao ô%` của ô ĐANG CHỌN (`syncManualSizeUi`; Vuông/Tròn thì cao tự khoá theo rộng) và nút "Áp cho tất cả ô" (đặt mọi ô của nhóm bằng cỡ ô đang chọn, giữ vị trí, dịch vào trong nếu tràn). Vẫn kéo góc dưới-phải được như cũ. Chỉ web, schema không đổi (slot `wPct/hPct` sẵn có).
