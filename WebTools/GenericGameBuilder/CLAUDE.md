# GenericGame Builder — ghi chú dự án (schema v2.1)

Công cụ web soạn game cho runtime Unity `GenericGameController`. Trả lời user bằng tiếng Việt.

## File
- **Bản mới nhất: `game_builder_v2.html`** (1 file, 1 IIFE → `game` không truy cập được từ ngoài). Các file `game_builder*.html` khác là bản cũ/backup (v1, 1002...).
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
