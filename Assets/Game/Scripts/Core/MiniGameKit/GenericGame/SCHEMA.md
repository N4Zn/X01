# GenericGame — pipeline web tool kéo-thả → Unity

Mục tiêu: tạo mini-game bằng web tool kéo-thả (chọn vị trí/loại đáp án/ảnh/hiệu ứng), chơi thử
NGAY trên web, xuất ra 1 gói (`game.json` + ảnh/âm thanh), copy vào project — **không cần mở
Unity Editor dựng scene cho từng game mới**. 1 scene Unity duy nhất
(`Assets/Game/Scenes/_GenericGame/GenericGamePlayer.unity`, dựng 1 lần qua
**Tools → GenericGame → Build Player Scene**) đọc `game.json` lúc runtime và tự dựng layout/nội
dung/hiệu ứng tương ứng.

Nguồn chân lý duy nhất mô tả "Kit hỗ trợ được những gì" là
[`capabilities.json`](capabilities.json) — web tool đọc file này (bản nhúng/copy trong chính nó)
để biết hiện lựa chọn nào trong form. Thêm display type/effect mới vào Kit (code C#) PHẢI thêm
mục tương ứng vào `capabilities.json` rồi copy đè bản nhúng trong web tool — không có cơ chế tự
đồng bộ 2 chiều.

## Luồng thêm 1 game mới (sau khi đã build scene 1 lần)

1. Web tool xuất 1 gói gồm `game.json` + các file ảnh/âm thanh được dùng.
2. Copy `game.json` → `Assets/Game/Resources/GenericGames/<gameId>/game.json`
3. Copy ảnh → `Assets/Game/Resources/TestTongHop/images/GenericGames/<gameId>/*.png`
   (Texture Type tự động ép về Sprite — xem `GenericGameTextureImportProcessor.cs`, không cần sửa
   tay `.meta` như trước đây.)
4. Copy âm thanh → `Assets/Game/Resources/TestTongHop/audio/GenericGames/<gameId>/*.mp3`
5. Thêm 1 dòng vào `GameRegistry.cs`:
   `Set(cat, idx, "<gameId>", "<displayName>", "GenericGamePlayer", Engine.MiniGameKit, ...)`
   — `sceneName` LUÔN là `"GenericGamePlayer"` (mọi game generic dùng chung đúng 1 scene, giống
   quy ước nhiều-variant-chung-1-scene đã có sẵn trong Kit, phân biệt nhau qua
   `GameSessionManager.SelectedGameName`).
6. (Tuỳ chọn, cho K02) thêm mục tương ứng vào `game_registry.json` + rebuild `controlui-release.aar`.
7. Rebuild APK. KHÔNG cần mở Unity Editor dựng lại scene.

## `game.json` — schema (schemaVersion 2)

**v2 (hiện tại)**: mỗi ROUND có 2 nhóm slot — `question` và `answers` — mỗi nhóm có bố cục riêng (tự đặt/ma trận/random), mỗi
**slot** = vị trí + nội dung (ảnh nền / icon / chữ / âm thanh / hiệu ứng ghi đè riêng). **Item** (nhân vật/đồ vật trang trí) là
của CẢ GAME (`layout.decorations`), không theo round. Game cũ (`schemaVersion` 1: `layout.slots`/`answerArea`/`randomArea`/
`questionArea` dùng chung + `round.answers.slots[]` chỉ có nội dung) vẫn mở được — web tool tự chuyển sang v2 khi mở, Unity tự chuyển
lúc nạp (`GenericGameMigration.FromV1`, cùng quy tắc với `migrateV1` trong `game_builder.html`); xuất lại từ web tool là ra v2.

```jsonc
{
  "schemaVersion": 2,
  "meta": { "gameId": "ThuNghiem1", "displayName": "Thử nghiệm 1", "category": "Đếm" },
  "settings": {
    "playMode": "Combined",          // "Combined" | "Independent"
    "answerMode": "MultiSelect",     // "Single" | "MultiSelect" | "OrderedSequence" | "SumToTarget"
    "scoringMode": "perCorrectCount",// "flat" | "perCorrectCount"
    "flatPoints": 1,
    "countdownMode": "nextInN",      // "none" | "nextInN"
    "countdownSeconds": 3,           // CHƯA dùng thật — Kit hiện hardcode 3s, giữ chỗ cho sau
    "hasTargetScore": false, "targetScore": 10,  // chỉ để HIỂN THỊ qua item bind="targetScore", KHÔNG có logic thắng/thua
    "wrongEndsRound": true,          // false = "cho thử lại" — xem mục riêng bên dưới
    "waitBothTeams": false,          // true (chỉ Combined) = 2 đội cùng câu, mỗi đội chơi hết câu của mình, CẢ 2 xong mới sang câu — xem mục "Chờ cả 2 đội"
    "showCorrectWrongTint": true,    // false = tắt tô xanh/đỏ/xám mặc định của Kit
    "questionAudioOnly": false,      // true = ẩn hẳn chữ/ảnh/icon câu hỏi, chỉ phát âm thanh slot câu hỏi (chỉ Combined) — xem mục riêng
    "questionFontSize": 0, "answerFontSize": 0, "questionTextColor": "", "answerTextColor": ""
  },
  "layout": {                        // CẤP GAME — bố cục câu hỏi/đáp án nằm ở từng round
    "spawnFlow": { "enabled": false, "direction": "BottomToTop", "itemWPct": 15, "itemHPct": 15, "speedPct": 25, "spawnIntervalSec": 1, "maxConcurrent": 4 },
    "collectSlots": [], "collectArea": { "xPct": 5, "yPct": 5, "wPct": 40, "hPct": 30 }, "collectCols": 5,
    "collectFillReverse": false, "collectAutoStretch": false, "collectFillByValue": false,   // "Vùng thu thập" (FlyToStay)
    "background": "bg.png", "backgroundAudio": null,
    "qFrame": null,                  // khung 9-slice dưới chữ slot câu hỏi
    "slotFrames": [],                // ảnh nền ô đáp án xoay vòng (cho đáp án chưa có ảnh nền)
    "decorations": [ /* ITEM — xem mục "Item" bên dưới */ ]
  },
  "effects": {                       // hiệu ứng CHUNG cho mọi đáp án — mỗi slot ghi đè riêng được bằng slot.fx
    "onIdle":          { "effects": [], "sound": null },
    "onCorrectTap":    { "effects": [ { "type": "Punch", "effectParams": { "duration": 0.3, "scale": 1.25 } } ], "sound": "correct.mp3", "playSound": true, "showIcon": true },
    "onWrongTap":      { "effects": [ { "type": "Shake", "effectParams": { "duration": 0.3, "amplitudePct": 1.5, "speed": 30 } } ], "sound": "wrong.mp3", "playSound": true, "showIcon": true },
    "onCorrectRemove": { "effects": [
        { "type": "FlyTo", "effectParams": { "duration": 0.45, "targetXPct": 50, "targetYPct": 92, "scale": 0.3 } }
      ], "sound": null, "playSound": true, "showIcon": false }
  },
  "rounds": [
    {
      "target": 0,                   // chỉ dùng ở SumToTarget (0 = mặc định) — JsonUtility không nhận null cho int
      "question": {
        "arrangement": "manual",     // "manual" | "matrix" | "random" — xem "Nhóm slot" bên dưới
        "area": { "xPct": 8, "yPct": 8, "wPct": 84, "hPct": 40 },
        "matrix": { "cols": 3, "itemWPct": 22, "itemHPct": 22 },   // chỉ để web tool nhớ cấu hình khi soạn (kết quả đã ghi vào toạ độ từng slot)
        "random": { "itemWPct": 15, "itemHPct": 20, "gapPct": 2 }, // chỉ dùng khi arrangement="random"
        "shape": "rectangle",        // "rectangle" | "square" | "circle"
        "bgImage": null, "bgIcon": null,   // ảnh nền / icon MẶC ĐỊNH cho mọi slot trong nhóm
        "slots": [
          { "xPct": 8, "yPct": 8, "wPct": 84, "hPct": 40, "text": "3 + 2 = ?", "image": null, "icon": null, "imagePool": null, "sound": "doc_cau_hoi.mp3", "correct": false, "fx": {} }
        ]
      },
      "answers": {
        "arrangement": "manual", "area": { "xPct": 5, "yPct": 50, "wPct": 90, "hPct": 45 },
        "matrix": { "cols": 3, "itemWPct": 22, "itemHPct": 22 }, "random": { "itemWPct": 15, "itemHPct": 20, "gapPct": 2 },
        "shape": "rectangle", "bgImage": null, "bgIcon": "apple.png",
        "slots": [
          { "xPct": 8,  "yPct": 58, "wPct": 24, "hPct": 30, "text": "5", "correct": true,  "sound": "nam.mp3", "fx": {} },
          { "xPct": 38, "yPct": 58, "wPct": 24, "hPct": 30, "text": "4", "correct": false, "imagePool": "pool1",
            "fx": { "onWrongTap": { "effects": [ { "type": "Shake", "effectParams": { "duration": 0.4, "amplitudePct": 3, "speed": 30 } } ], "sound": null } } }
        ]
      }
    }
  ],
  "imagePools": [
    { "id": "pool1", "name": "Trái cây sai", "images": ["b.png", "c.png", "d.png"] }
  ]
}
```

### Nhóm slot (`rounds[].question` / `rounds[].answers`)

- **`slots[]` — không giới hạn số lượng.** Mỗi slot = `xPct/yPct/wPct/hPct` + nội dung. Số slot đáp án mỗi round có thể khác nhau;
  Unity dựng sẵn 1 pool `ButtonItem` theo số slot đáp án LỚN NHẤT trong mọi round rồi mỗi round đặt lại vị trí/hình dạng/nội dung
  cho các ô đang dùng (`ApplyRoundLayout`/`ApplyRoundContent`), các ô thừa do `ButtonDisplay` tự ẩn.
- **`arrangement`**: `manual` (tự đặt/kéo từng ô), `matrix` (web tool tính lưới 1 lần lúc soạn, kết quả ghi thẳng vào toạ độ từng slot — Unity
  không biết gì về ma trận), `random` (Unity xáo lại vị trí **mỗi round, mỗi bên độc lập** trong `area`; cỡ ô = `random.itemWPct`×`itemHPct`,
  số ô = số slot; tự tránh đè nhau — mỗi ô thử 200 vị trí ngẫu nhiên, giữ vị trí có diện tích chồng nhỏ nhất, `gapPct` = khoảng trống
  % THÊM giữa 2 ô liền kề; vùng quá nhỏ vẫn có thể chồng nhẹ do hết lượt thử). Toạ độ trong `slots[]` bị bỏ qua khi `random`.
  **Spawn liên tục** KHÔNG nằm ở đây — là cờ chung cả game (`layout.spawnFlow.enabled`, xem mục Spawn).
- **`shape`**: `"circle"` đổi nền nút ĐÁP ÁN sang sprite tròn (`ButtonItem.ApplyShape`, trả về sprite gốc khi round sau là `rectangle` —
  hình dạng đổi theo round). `"square"` chỉ là quy ước web tool tự khoá tỉ lệ lúc kéo. Ảnh nền riêng của slot KHÔNG bị clip tròn theo.
- **Nội dung slot**: `image` = **ảnh nền** của slot (không có thì lấy `group.bgImage`; `imagePool` khác rỗng thì BỎ QUA `image`, random 1
  ảnh trong tập — chốt 1 LẦN lúc game khởi động, xem giới hạn); `icon` = **icon nhỏ** nằm trên nền (không có thì lấy `group.bgIcon`);
  `text` = chữ. **Có icon (riêng hoặc chung) thì `text` là SỐ LƯỢNG icon** (để trống/không phải số = 1, tối đa 20) và chữ **không hiện** —
  chữ và icon không bao giờ cùng lúc. Người soạn tự đảm bảo `text` là số khi dùng icon (web tool tô viền đỏ ô chữ nếu không phải số).
- **Nền đáp án** (ưu tiên): ảnh riêng/pool/`bgImage` của nhóm (giữ tỉ lệ) > `layout.slotFrames` xoay vòng theo thứ tự ô (kéo kín) > thẻ trắng.
- **`correct`** (chỉ đáp án): đáp án đúng. Với `OrderedSequence`, thứ tự chạm = thứ tự các slot `correct:true` trong mảng `slots[]`
  (web tool cho ↑/↓ đổi thứ tự — chỉ đổi NỘI DUNG giữa các slot, giữ nguyên vị trí). `SumToTarget` bỏ qua `correct`.
- **`sound`**: slot đáp án = phát khi chạm vào slot (cộng với âm thanh của hiệu ứng trúng/sai). Slot câu hỏi = **âm thanh câu hỏi** — xem mục riêng.
- **`fx`** — hiệu ứng **GHI ĐÈ** riêng slot: `{ onIdle, onCorrectTap, onWrongTap, onCorrectRemove }` (mỗi cái là 1 `ActionFx`). Trigger nào có
  ≥1 effect (kể cả `"None"` để cố ý TẮT hiệu ứng chung) thì thay danh sách effect chung, có `sound` riêng thì thay âm thanh chung — phần còn lại
  VẪN theo chung (slot chỉ đặt âm thanh thì giữ hiệu ứng chung); trigger nào trống thì dùng nguyên hiệu ứng chung. Slot câu hỏi chỉ dùng `onIdle` (vd Phóng to-nhỏ liên tục). Web tool: nút ✨ ở dòng slot (cột phải).
  Unity: `SlotFxOf` + `Pick(slotOverride, global)`.
- Web tool xuất `fx` chỉ gồm trigger có nội dung; JsonUtility tự tạo object rỗng cho trigger thiếu → Unity phân biệt "có ghi đè" bằng nội dung, không bằng null.

`xPct/yPct` tính từ **góc trên-trái của NỬA màn hình** (0-100%, y tăng xuống dưới), áp dụng CHUNG cho 2 bên — bên PHẢI tự động lấy ảnh
gương ngang (`xPct' = 100 - xPct - wPct`) của chính layout bên trái, không khai báo riêng.

`effectParams` (không phải `"params"` — đó là từ khoá C#, JsonUtility sẽ không parse được) khớp field của struct `EffectParams`
(`duration, scale, distancePct, angleDeg, amplitudePct, speed, loops, targetXPct, targetYPct`) — field nào effect không dùng thì bỏ qua.

Mỗi "chỗ cắm" (`onIdle`/`onCorrectTap`/`onWrongTap`/`onCorrectRemove`) là 1 **mảng** effect (`effects: EffectSpec[]`) CHẠY ĐỒNG THỜI + 1
`sound` tuỳ chọn (tên file trong `assets/`, phát song song). Những effect thao tác CHUNG 1 thuộc tính (vd 2 effect cùng đổi `localScale`)
sẽ ghi đè lẫn nhau — tự chọn tổ hợp hợp lý, Kit không tự kiểm tra xung đột.

`targetXPct`/`targetYPct` (FlyTo/FlyToStay) và `angleDeg` (FlyOff) khai báo THEO NỬA TRÁI — Unity tự mirror ngang cho nửa phải lúc chạy.

### Tương thích với game cũ (schemaVersion 1)

v1: `layout.slots` (toạ độ chung) + `answerArea` + `randomArea` + `slotShape` + `questionArea` (1 khung câu hỏi chung) + `rounds[].question`
`{text,image,audio}` + `rounds[].answers[]` `{text,image,imagePool,iconCount,correct}`. Chuyển sang v2 (web `migrateV1` / Unity
`GenericGameMigration.FromV1`): mỗi round nhận 1 bản sao bố cục chung (`random` nếu `randomArea.enabled`, không thì `manual`; ở `random`
toạ độ lấy từ 1 mẫu xáo); đáp án `iconCount > 0` → `icon = image`, `text = iconCount`; câu hỏi → 1 slot ở `questionArea` (`audio` thành
`sound`); câu hỏi bị ẩn (`questionArea.wPct <= 0`) → không có slot câu hỏi. `questionGenerator` (bộ sinh câu hỏi tự động) **đã bị bỏ** —
game v1 nào từng bật nó phải soạn lại round bằng tay (web tool báo khi mở).

## FlyToStay — "Bay tới & Ở lại" + Vùng thu thập (layout.collectSlots)

Khác `FlyTo` (bay tới 1 điểm rồi **biến mất**), `FlyToStay` bay tới 1 Ô ĐÍCH rồi **Ở LẠI VĨNH
VIỄN** — dùng khi đáp án đúng cần "đọng lại" thành 1 khu vực mới trên màn hình (vd giỏ chứa chất
dần trong round, mỗi đáp án đúng xếp vào 1 ô riêng thay vì chồng lên nhau tại 1 điểm).

Vị trí bay tới KHÔNG phải 1 điểm tĩnh duy nhất mà là **1 mảng ô** — `layout.collectSlots: RectPct[]`
— KHÁC HẲN slot đáp án (nơi HIỆN đáp án để bấm): đây là nơi đáp án **ĐÃ CHỌN ĐÚNG** bay tới rồi
ở lại. Web tool có 2 cách soạn mảng này (mục "Vùng thu thập"), chọn qua nút "Lưới"/"Tự do" —
Unity/preview không biết gì về cách soạn, chỉ đọc thẳng mảng rect cuối cùng, y hệt slot đáp án:

- **"Lưới"** — CHÍNH công cụ "Ma trận" đã dùng để tạo slot đáp án (đếm + cột + rộng/cao%, tính
  lưới 1 lần lúc soạn), thêm 2 tuỳ chọn riêng cho vùng thu thập:
  - **Thứ tự điền** A→Z (mặc định, ô số 1 = ô lưới ĐẦU tiên) hay Z→A (ô số 1 = ô lưới CUỐI cùng,
    đảo toàn bộ mảng) — cách để đổi chiều xếp (vd 1 cột thì ô đầu ở trên hay ở dưới) mà không cần
    tự tính lại vị trí tay.
  - **Không ép khít** — vùng kéo-thả NHỎ HƠN tổng kích thước các ô (gap tính ra ÂM) thì các ô tự
    **ĐÈ LÊN NHAU** có chủ đích thay vì bị ép giãn ra vừa đúng khung — áp dụng CHUNG cho cả "Ma
    trận" của slot đáp án (vùng đáp án) lẫn "Lưới" của `layout.collectSlots` (vùng thu thập),
    không riêng gì vùng thu thập.
- **"Tự do"** — bỏ hẳn công thức lưới, thêm/bớt từng ô bằng nút +/−, mỗi ô kéo-thả + resize ĐỘC
  LẬP (không theo hàng/cột nào) — dùng khi cần bố cục bất đối xứng mà "Lưới" không tính ra được.

Mỗi lần `FlyToStay` được dùng (1 lần/đáp án đúng, ở `onCorrectTap` hoặc `onCorrectRemove`) sẽ bay
vào **Ô TIẾP THEO** trong mảng ô đang áp dụng của round đó (tăng dần theo bộ đếm riêng mỗi bên) —
**KHÔNG quay vòng khi hết ô**, chỉ clamp ở ô cuối (đứng yên tại chỗ cũ đó, ghi đè) để không lỗi;
người soạn TỰ đảm bảo số đáp án đúng tối đa trong 1 round ≤ số ô (TRỪ KHI `collectAutoStretch=true`,
xem dưới — lúc đó không bao giờ thiếu/dư ô). Bộ đếm + mọi item đã "đọng lại" đều **RESET (dọn sạch
+ đếm lại từ ô đầu) mỗi round mới** — mặc định KHÔNG tích luỹ xuyên suốt game, mỗi round bắt đầu
lại với "giỏ" trống.

Nếu KHÔNG cấu hình `collectSlots` (mảng rỗng) — `FlyToStay` fallback về hành vi giống hệt `FlyTo`
cũ: dùng `targetXPct`/`targetYPct` tĩnh của chính effect đó làm 1 điểm cố định duy nhất, giữ
nguyên kích thước item hiện tại nhân `scale` (không co giãn theo ô như khi có `collectSlots`).

**Gotcha đã gặp + sửa (web preview)**: icon đã "đọng lại" dùng CHUNG class `.pt-slot` với các nút
đáp án bình thường (clone qua `el.cloneNode(true)`) — mọi thao tác quét theo class đó cho CẢ nửa
màn hình (`lockAllSlots`/`unlockAllSlots`/`startIdleEffect`, dùng khi round kết thúc/khoá đáp án
hoặc mở khoá lại ở chế độ "cho thử lại") VÔ TÌNH quét trúng luôn các icon đã bay — nặng nhất là
`stopIdleOnElement()` (gọi từ `lockAllSlots`) reset thẳng `style.transform=''`, xoá mất transform
đang giữ icon ở vị trí đã bay tới (chỉ `transform` tạo hiệu ứng dịch chuyển, `left`/`top` CSS gốc
không đổi) → icon "nhảy" về đúng vị trí cũ NGAY KHI round đó kết thúc, dù chưa có round mới nào bắt
đầu. **Đã sửa**: clone FlyToStay được đánh dấu `dataset.collected='1'` ngay lúc tạo, 3 hàm quét
trên đổi sang selector `'.pt-slot:not([data-collected])'` để loại trừ — icon chỉ thật sự biến mất
khi round MỚI bắt đầu (`resetCollectArea`), không bị động tới giữa chừng nữa. Icon ✔ fallback
(`fx.showIcon`) cũng từng hiện NGAY LẬP TỨC (che mất hiệu ứng bay) — đã sửa sang **chờ hiệu ứng
chạy xong mới hiện** (lấy `duration` lớn nhất trong các effect thật của `onCorrectRemove`, xem
`runCorrectRemove()`). Cả 2 fix đã áp dụng SONG SONG ở Unity (`PlayCorrectRemoveClone` vốn KHÔNG
dính bug snap-back vì clone là GameObject riêng, không nằm trong mảng `_leftSlotItems`/
`_rightSlotItems` mà các hàm khoá/mở khoá/reveal thật sự duyệt qua — chỉ cần sửa phần icon-timing,
qua coroutine `ShowFeedbackIconAfterDelay`).

### Tự co giãn theo round — `layout.collectAutoStretch`

Mặc định `false`: `collectSlots` là mảng TĨNH, soạn 1 lần lúc thiết kế, dùng y nguyên cho mọi
round (dù soạn bằng "Lưới" hay "Tự do"). Nếu game có **số đáp án đúng khác nhau giữa các round**
(vd round này 2 đáp án đúng, round khác 4) và muốn "giỏ" luôn **vừa khít, không thừa/thiếu ô**,
bật `collectAutoStretch=true` — khi đó `collectSlots` đã soạn bị **BỎ QUA hoàn toàn**, Unity/preview
tự tính lại 1 lưới MỚI ở đầu MỖI ROUND, lấp đầy `layout.collectArea` với đúng
`QuestionData.correctAnswers.Length` ô (không gap, ô luôn liền kề/lấp đầy), dùng `layout.collectCols`
để bẻ dòng và `layout.collectFillReverse` để đảo thứ tự điền (y hệt ý nghĩa "Thứ tự điền" ở trên,
nhưng áp dụng MỖI ROUND thay vì bake sẵn 1 lần). Web tool bật qua checkbox "Tự co giãn theo số đáp
án đúng/round" trong chế độ "Lưới" — lúc đó "Số ô"/kích thước ô ở panel chỉ còn là bản xem trước,
không áp dụng lúc chơi thật. Chỉ áp dụng được ở chế độ "Lưới" (chế độ "Tự do" không có khái niệm
cột/lấp đầy để tự tính lại).

## Đáp án "N icon lặp lại" + Bộ sinh câu hỏi tự động — đã thay bằng mô hình slot (v2)

- **`RoundAnswerSpec.iconCount`** (v1) không còn ở v2: slot có `icon` thì `text` chính là số lượng icon — xem "Nội dung slot" ở trên. Unity dùng
  THẲNG `AnswerMediaType.IconCompose` native của Kit (`ButtonItem.SetContent(text, "resolvedPath:count")` → `ItemMediaHelper.ApplyMedia`, prefab đã
  có `iconSlot`/`iconContainer`/`GridLayoutGroup`); slot câu hỏi dựng hàng icon bằng `HorizontalLayoutGroup` (`QuestionSlotUi`). Web preview ghép
  bằng lưới `<img>` CSS (chỉ xấp xỉ hình, không nhất thiết khớp pixel với bản Unity).
- **`questionGenerator` (bộ sinh phép tính tự động) và `questionGenerator.parts` đã bị XOÁ** (web + Unity) — bị ràng buộc quá nhiều. Muốn câu hỏi
  dạng "A − B = ?" thì soạn tay: câu hỏi nhiều slot (vd 3 slot "A", "−", "B") mỗi slot có vị trí/ảnh nền riêng, hoặc 1 slot chữ.

## Cỡ chữ câu hỏi/đáp án (settings.questionFontSize / answerFontSize)

`GenericGameSettings.questionFontSize`/`answerFontSize` (float, mặc định 0) — `0` = tự co vừa ô (chữ slot câu hỏi, best-fit) / giữ cỡ chữ
đặt sẵn trong prefab (nút đáp án), không ghi đè. `> 0` = ghi đè cỡ chữ trong slot câu hỏi (`Text` dựng lúc runtime) và `TextMeshProUGUI` của mỗi
nút đáp án qua `ButtonItem.SetFontSize()` (gọi lại mỗi round khi áp nội dung). CHỈ có tác dụng khi slot đang hiện dạng CHỮ — không ảnh hưởng icon.
Web tool: 1 dòng "Cỡ chữ: Hỏi [50][màu] Đáp [50][màu]" trong "CÀI ĐẶT CHUNG"; game MỚI mặc định 50 (file cũ giữ giá trị đã lưu).
`settings.questionAudioOnly` không còn ô tick trên web (web luôn ghi `false`): slot câu hỏi không có chữ/ảnh thì tự chỉ phát âm thanh.

## Màu chữ câu hỏi/đáp án (settings.questionTextColor / answerTextColor)

Hex `"#RRGGBB"`/`"#RRGGBBAA"` (string), `""` (mặc định) = không ghi đè: chữ câu hỏi trắng như cũ, chữ đáp án giữ màu prefab.
Unity: `GenericGameController.ParseTextColor` (câu hỏi, lúc dựng `Text` slot) và `ApplyAnswerTextStyle` → `ButtonItem.SetTextColor()`
(đáp án, gọi cùng chỗ với cỡ chữ mỗi round). CHỈ tác dụng khi slot hiện CHỮ. Web tool: ô chọn màu cùng dòng với cỡ chữ, nút ↺ (bỏ ghi đè) hiện khi đã chọn màu.

## "Cộng dồn tới mục tiêu" (answerMode = SumToTarget) + tên lửa + ảnh khung

**`settings.answerMode = "SumToTarget"`** — mọi đáp án đều bấm được; mỗi lần bấm cộng **giá trị SỐ**
của đáp án (parse từ `text`, không phải số/≤0 → 1) vào "thanh nhiên liệu" = `layout.collectSlots`
(đáp án `"3"` phủ 3 ô liền nhau; thẻ đáp án bay tới rồi biến mất, thay bằng các ô SÁNG LÊN màu
gradient xanh→vàng→cam→đỏ theo vị trí ô, kèm số thứ tự ô — cần gắn `FlyToStay` vào
`onCorrectRemove` để có hiệu ứng bay). Tổng **ĐÚNG BẰNG** mục tiêu → thắng round; **VƯỢT** → hiệu
ứng `onWrongTap` + khoá đáp án + gợi ý "Chạm thanh nhiên liệu để chọn lại"; hết đáp án mà chưa đủ →
cũng hiện gợi ý. Round **không bao giờ kết thúc vì sai** — chạm vào vùng thanh = chọn lại từ đầu
(đáp án trả về chỗ cũ, ô tắt hết, không trừ điểm; vô hiệu sau khi đã thắng). Ô MỤC TIÊU của round
được viền vàng.

- `rounds[].target` (int) — mục tiêu của round; `0`/thiếu = số ô của `layout.collectSlots` (hoặc 10 nếu
  chưa có ô nào). Web tool xuất `0` thay vì `null` (JsonUtility không nhận null cho int).
- Cờ `correct` của đáp án **bị bỏ qua** (QuestionData.correctAnswers rỗng). Điểm `perCorrectCount` =
  số lần bấm trong lượt thắng (không tính các lần đã chọn lại).
- Tự bật `layout.collectFillByValue` và **bỏ qua** `collectAutoStretch` (thanh có đúng "Số ô" đã soạn).
  `collectFillByValue=true` dùng RIÊNG (không SumToTarget) = đáp án số N chiếm N ô, ô sáng lên, nhưng
  luật thắng/thua vẫn là của answerMode đang chọn.
- Unity: `AnswerMode.SumToTarget` + `AnswerValidator.ValidateSum` (Kit — cộng `QuestionData.sumValues`,
  so với `QuestionData.sumTarget`: `==` → CorrectFinal, `<` → CorrectPartial, `>` → WrongFinal),
  logic phía GenericGame ở `GenericGameController.HandleSumTapped/SumReset/SpawnLitCells`.
- **Giới hạn**: chưa hỗ trợ spawn flow; Combined mode nếu CẢ 2 bên cùng vượt mục tiêu thì Kit vẫn tự
  kết thúc round (giống `wrongEndsRound=false`, xem trên).

**`layout.decorations[].launchOnComplete`** (bool) — deco (vd tên lửa) bay thẳng LÊN khỏi màn hình
(~1.5s, tăng tốc) khi bên đó THẮNG round (mọi answerMode), tự về chỗ cũ + chạy lại hiệu ứng liên tục
ở round mới. Có deco loại này thì thời gian chờ sau khi thắng được nâng tối thiểu 1.8s.

**`layout.qFrame`** (string) — ảnh khung **9-slice** đặt DƯỚI chữ của slot câu hỏi (viền 90px nguồn, co giãn giữ 4 góc; chỉ khi slot là chữ trần —
không ảnh nền, không icon). **`layout.slotFrames`** (string[]) — ảnh nền ô đáp án gán **xoay vòng** theo thứ tự ô cho đáp án chưa có ảnh nền (riêng/chung nhóm);
chữ vẽ đè lên. Unity: `BuildQuestionFrameSprite` + `ShowQuestionSlots` (khung câu hỏi), `ApplyAnswerContent` + `ButtonItem.SetBackgroundSprite` (nền đáp án); cỡ
chữ đáp án trên ô có khung không tự co theo ô như web (dùng `answerFontSize` hoặc cỡ prefab).

## Spawn liên tục (layout.spawnFlow)

`layout.spawnFlow` — cờ **CHUNG của cả game**: `enabled=true` thì MỌI round đều hiện đáp án theo kiểu spawn, GHI ĐÈ cách sắp xếp (manual/matrix/random)
của nhóm đáp án (toạ độ từng slot bị bỏ qua). Đáp án KHÔNG hiện sẵn hết cùng lúc — tự xuất hiện định kỳ (`spawnIntervalSec`), trôi theo 1 hướng cố
định (`direction`: `BottomToTop`/`TopToBottom`/`LeftToRight`/`RightToLeft`) với tốc độ `speedPct` (% chiều di chuyển/giây — vd 25 nghĩa là mất ~4s đi hết
100% theo hướng dọc), rồi tự biến mất nếu KHÔNG bị chạm trúng khi ra khỏi màn hình — **không trừ điểm khi bỏ lỡ**, chỉ mất lượt xem đúng đáp án đó (sẽ
random lại answerIndex khác cho lượt sau). `maxConcurrent` giới hạn số item cùng lúc trên màn hình (1 bên). Dùng `rounds[].answers.area` (vùng đáp án CỦA ROUND ĐÓ,
khác nhau được giữa các round — `SpawnFlowDisplay.SetArea`) làm "dải" random vị trí trên trục VUÔNG GÓC với hướng bay. Web tool: nút "Spawn liên tục" ở tab Đáp án
(bật cho cả game; chọn bố cục khác thì tắt).

Implement bằng `SpawnFlowDisplay.cs` — 1 `IAnswerDisplay` HOÀN TOÀN RIÊNG (không dùng chung `ButtonDisplay`), theo đúng khuôn `FloatingDisplay.cs`. Item bị chạm
trúng (đúng hoặc sai) KHÔNG bị huỷ ngay — chỉ "đóng băng" tại chỗ để hiệu ứng kịp chạy trên đúng vị trí đó, chờ ẩn câu hỏi/đáp án lúc "Next in Ns" mới thật sự mất.
Nội dung slot (ảnh nền/icon/chữ/hình dạng) áp qua `OnItemSpawned` → `ApplyAnswerContent`.

**Giới hạn**: `onIdle` (Breathing mời gọi) CHƯA hỗ trợ ở spawn flow — item xuất hiện rải rác theo thời gian, không có "lúc round bắt đầu" rõ ràng.

## Bối cảnh (nền) + Item (nhân vật/đồ vật trang trí) + điểm hiển thị

**Item = đồ vật/con vật trang trí cho CẢ GAME** (vd con thỏ) — luôn hiện ở mọi round, KHÔNG theo round; khác hẳn **icon nhỏ** nằm trong từng slot câu hỏi/đáp án.

```jsonc
"layout": {
  ...,
  "background": "bg.png",   // 1 ảnh nền chung, phủ kín mỗi nửa màn hình, nằm dưới cùng — null = không có
  "backgroundAudio": "bgmusic.mp3", // nhạc nền lặp lại suốt game, thay nhạc nền mặc định của Kit — null = dùng nhạc mặc định
  "decorations": [
    {
      "id": "deco1", "image": "rabbit.png", "mirrorImage": true,  // true (mặc định) = ảnh tự lật ngang ở nửa phải, text KHÔNG bao giờ lật
      "xPct": 60, "yPct": 5, "wPct": 25, "hPct": 25,   // theo nửa TRÁI, bên phải tự mirror ngang VỊ TRÍ
      "launchOnComplete": false,                       // true = bay lên khỏi màn hình khi THẮNG round (tên lửa)
      "effect": { "type": "Breathing", "effectParams": { "duration": 0.6, "scale": 1.08, "loops": -1 } },   // hiệu ứng CHỜ liên tục
      "fx": {                                          // phản hồi theo sự kiện — MẶC ĐỊNH rỗng, web tool bấm "+ Hiệu ứng" mới thêm
        "onCorrect": { "effects": [], "sound": null }, // trả lời ĐÚNG (hết round)
        "onPartial": { "effects": [], "sound": null }, // đúng 1 PHẦN (MultiSelect/Ordered còn đáp án phải chọn)
        "onWrong":   { "effects": [ { "type": "Shake", "effectParams": { "duration": 0.3, "amplitudePct": 1.5, "speed": 30 } } ], "sound": "oh.mp3" },
        "onClick":   { "effects": [ { "type": "Punch", "effectParams": { "duration": 0.3, "scale": 1.25 } } ], "sound": "tho_keu.mp3" }  // item bị CHẠM
      },
      "text": { "bind": "totalScore", "fontSizePct": 7 }  // "" | "roundScore" | "totalScore" | "targetScore"
    }
  ]
}
```

**Thứ tự lớp (z-order)**: phần tử XUẤT HIỆN SAU trong mảng `decorations[]` hiển thị ĐÈ LÊN TRÊN các phần tử trước nó — nhất quán ở web tool
(canvas trái/phải, play-test) lẫn Unity (`BuildDecorations()` dựng theo đúng thứ tự mảng cho cả 2 bên). Web tool tự đưa 1 item xuống CUỐI mảng khi chạm/kéo
THÂN nó trên canvas, hoặc dùng nút ↑↓ trong panel "Nhân vật / Item" để sắp tay.

**Phản hồi (`fx`)**: chỉ chạy ở BÊN vừa trả lời (`GenericGameController.FireItems`). Mỗi trigger là 1 `ActionFx` (nhiều effect đồng thời + 1 `sound`);
âm thanh trong `onClick` chính là tiếng kêu khi dẫm/chạm vào item. `Punch`/`Shake`/`FadeOut`/`Breathing` chạy được; `FlyOff`/`FlyTo`/`FlyToStay` bị **BỎ QUA** ở item
(bay đi mất thì hết trang trí) — cả ở web lẫn Unity. Trong lúc phản hồi, hiệu ứng chờ tạm dừng rồi chạy lại. Item "tên lửa" (`launchOnComplete`) bỏ qua phản hồi (có luồng bay riêng).
Item **chỉ bắt chạm khi có `fx.onClick`** (`raycastTarget` + `Button`) — còn lại thuần hiển thị, không chặn chạm của đáp án. `text.bind` đọc trực tiếp từ `ScoreManager`
(totalScore/roundScore theo ĐÚNG bên trái-phải tương ứng) hoặc từ `settings.targetScore` tĩnh. Chưa có chế độ "ma trận/random" cho item — chỉ tự đặt từng item.

## Chờ cả 2 đội xong mới sang câu (`settings.waitBothTeams`)

Chỉ có nghĩa khi `playMode="Combined"` (2 đội cùng 1 câu hỏi). Mặc định `false` = luật gốc: đội nào ĐÚNG trước thắng, đội kia bị khoá, round kết thúc cho cả 2.
`true` = không ai "thắng trước": mỗi đội chơi hết câu của mình — **đúng** → ghi điểm ngay, ô đội đó khoá/giữ nguyên, đội kia vẫn chơi tiếp; **sai** → đội đó xong lượt
(hoặc thử lại nếu `wrongEndsRound=false`, lúc đó chưa tính xong). Khi CẢ 2 đội xong thì mới tới "Next in Ns" và sang câu mới. Hết giờ cả game thì game kết thúc như thường.
- Unity: `MiniGameControllerBase.WaitForBothTeams`/`MarkTeamDone` đếm đội xong; `ButtonDisplay`/`SpawnFlowDisplay.WaitBothTeams` tắt việc khoá/dọn bên kia khi 1 đội đúng và tắt `CheckBothWrong`.
  Âm thanh câu hỏi vẫn lặp cho tới khi cả 2 đội xong. Web builder: ô tick "Chờ cả 2 đội xong mới sang câu" (chỉ hiện khi 2 đội = Gộp), preview qua `waitBothOn()`/`teamDoneCombined()`.
- Web builder tự tick ô này khi chuyển 2 đội sang Gộp (game mới mặc định Gộp + tick). Field thiếu trong JSON cũ = `false`.
- Đội xong lượt: icon ✔/✖ GIỮ nguyên trên màn hình; sau `feedbackDelayCorrect/Wrong` ẩn hết ĐÁP ÁN của đội đó (hook `OnTeamTurnDone` → `GenericGameController.HideTeamAnswersAfterDelay`; câu hỏi/item giữ). Cả 2 đội xong → sau feedback delay: `CleanupCurrentDisplay` ẩn đồng bộ icon + đáp án + câu hỏi → (wait-for-clear nếu bật) → "Next in Ns" → câu sau.
- Chưa test trong Editor; chưa kiểm tra riêng tổ hợp với `SumToTarget` ở spawn flow (spawn flow vốn chưa hỗ trợ SumToTarget).

## Chạm sai — kết thúc round hay cho thử lại?

`settings.wrongEndsRound = false` cho phép chạm sai KHÔNG kết thúc round — ô vừa sai mở khoá lại
sau khoảng `feedbackDelayWrong` (mặc định 2s), chơi tiếp CÙNG câu hỏi tới khi đúng. Cài đặt này
lệch khỏi quy tắc gốc của `AnswerValidator.cs` ("Sai → WrongFinal: kết thúc round ngay") bằng cách
GenericGameController tự ý KHÔNG forward kết quả sai lên vòng lặp round (Independent) hoặc tự mở
khoá lại bên vừa sai (Combined) — xem `ButtonDisplay.ResetTeamAttempt()` (hàm bổ sung riêng cho
mục đích này, không ảnh hưởng game nào khác). **Giới hạn**: ở Combined mode, nếu CẢ 2 bên cùng sai
thì Kit vẫn tự kết thúc round (đó là quyết định ở tầng `ButtonDisplay.CheckBothWrong()`, không can
thiệp được từ GenericGameController) — `wrongEndsRound=false` chỉ có tác dụng khi còn ít nhất 1 bên
chưa sai.

`settings.showCorrectWrongTint = false` tắt màu xanh/đỏ/xám mặc định Kit tự tô lên nền nút khi
đúng/sai/khoá (hành vi gốc `ButtonItem.SetState()`, chạy độc lập với hệ thống effects ở trên) —
hữu ích khi đáp án là ảnh nền trong suốt, tô màu đè lên ảnh có thể xấu. Cách làm: Generic GameController
đợi đúng 1 frame sau khi Kit tự tô xong rồi ghi đè lại màu nền về trong suốt/bình thường
(`SuppressTintNextFrame`) — không sửa được tận gốc trong `ButtonDisplay.cs` vì đó là hành vi dùng
chung cho mọi game khác.

## Kiểu đáp án "Theo đúng thứ tự" (OrderedSequence)

`settings.answerMode = "OrderedSequence"` — học sinh phải chạm ĐÚNG THEO THỨ TỰ các đáp án đã đánh
dấu `correct:true`, thứ tự đó CHÍNH LÀ thứ tự các slot đó xuất hiện trong mảng `round.answers.slots[]`
(không có field "order" riêng) — web tool có nút ↑/↓ ở từng dòng slot để đổi thứ tự (chỉ đổi nội dung, giữ nguyên vị trí), hiện số thứ tự (badge) cạnh mỗi đáp án đã đánh dấu đúng. Vị trí HIỂN THỊ trên màn hình vẫn
được xáo ngẫu nhiên mỗi round như các kiểu khác (`PickSlots`) — học sinh phải tự nhận diện ĐÚNG NỘI
DUNG cần chạm tiếp theo, không phải theo vị trí cố định. Chạm bất kỳ đáp án nào không phải "đáp án
đúng tiếp theo" (kể cả 1 đáp án đúng nhưng chưa tới lượt, hoặc 1 đáp án sai hẳn) → `WrongFinal` kết
thúc round ngay, giống hệt chạm đáp án sai (không retry trừ khi `wrongEndsRound=false`) — đúng hành
vi gốc của `AnswerValidator.ValidateOrdered()`, không có code mới ở `GenericGameController.cs` vì
`HandleAnswerTapped()` đã xử lý `ClickResult` chung cho mọi answerMode.

## Hiệu ứng mặc định + đáp án cuối (2026-10-08)

Web builder tạo game mới với hiệu ứng chung mặc định **"Mờ dần biến mất" (`FadeOut`)** ở `onCorrectTap` (đúng hết round, hiện icon ✔), `onWrongTap` (sai, hiện icon ✖) và `onCorrectRemove` (đúng chưa hết round, không icon). Muốn khác thì đổi hiệu ứng chung hoặc đặt riêng ở slot.
- Đáp án ĐÚNG CUỐI (hết round) chỉ chạy `onCorrectTap` (slot tự đặt thì theo slot, không thì theo chung) — câu chỉ có 1 đáp án đúng thì đặt ở đây là đủ. `onCorrectRemove` KHÔNG còn áp cho đáp án cuối, TRỪ thu thập: slot không tự đặt `onCorrectTap` và `onCorrectRemove` chung có `FlyToStay` → đáp án cuối cũng bay vào vùng "Đã chọn". (Trước đây `onCorrectRemove` chung luôn "ăn" mất `onCorrectTap` riêng của slot.) `SumToTarget` vẫn như cũ (đáp án cuối bay vào thanh theo `onCorrectRemove`).
- Unity: `FadeOut` chạy trên nút gốc (đáp án cuối/sai) để alpha 0 → `RestoreAnswerAlpha` trả về 1 khi sang round mới / thử lại.

## Âm thanh + icon ✔/✖ mặc định (fallback) cho onCorrectTap/onWrongTap/onCorrectRemove

Mỗi `ActionFx` giờ có thêm 2 field — **CHỈ có ý nghĩa ở `onCorrectTap`/`onWrongTap`/`onCorrectRemove`**
(bỏ qua ở `onIdle` — không có khái niệm "đúng/sai" ở chỗ cắm đó):

- `playSound` (mặc định `true`) — có `sound` riêng thì dùng `sound` đó; KHÔNG có thì tự dùng âm
  thanh đúng/sai **mặc định của Kit** (`MusicManager.PlayCorrectSfx()`/`PlayWrongSfx()` — đúng âm
  thanh đã dùng chung cho mọi game khác trong Kit, không phải âm thanh riêng của GenericGame).
  `false` = tắt hẳn, không phát gì kể cả khi có `sound`.
- `showIcon` — hiện icon ✔/✖ bounce mặc định của Kit (dùng chung `FeedbackIconBuilder`/
  `FeedbackEffect` như mọi game khác). Mặc định `true` ở `onCorrectTap`/`onWrongTap` (chỉ bắn 1
  lần/round, kết thúc round). Mặc định **`false`** ở `onCorrectRemove` — chỗ cắm này bắn LIÊN TỤC
  nhiều lần/round (mỗi lần gom 1 đáp án đúng ở MultiSelect/OrderedSequence), hiện icon to mỗi lần
  sẽ gây rối mắt — tự bật lên nếu game thật sự muốn.

Cách làm: `GenericGameController` override `UseDefaultFeedbackFx => false` để **tắt hẳn** cơ chế
feedback tự động của `MiniGameControllerBase` (nếu không tắt, âm thanh mặc định sẽ phát ĐÈ LÊN âm
thanh custom mỗi lần đúng/sai), rồi tự quyết định sound/icon trong `PlayActionFx()` (onCorrectTap/
onWrongTap) và `PlayCorrectRemoveClone()` (onCorrectRemove) theo đúng 2 cờ trên của TỪNG game. Icon
dùng lại nguyên `MiniGameControllerBase.ShowFeedbackIcon()`/`HideFeedbackIcon()` (tách ra từ
`PlayDefaultFeedbackFx`/`HideDefaultFeedbackIcons` gốc — hành vi các game khác không đổi).

## Âm thanh câu hỏi (slot câu hỏi `sound`) + questionAudioOnly

- **Âm thanh câu hỏi = `sound` của các slot câu hỏi.** Vào round: phát lần lượt âm thanh của các slot câu hỏi (theo thứ tự `slots[]`), xong thì **nghỉ 4 giây rồi
  phát lại**, lặp tới khi round kết thúc (có người trả lời đúng / cả 2 bên sai / hết giờ). **CHỈ phát ở `playMode = "Combined"`** — chế độ `Independent` 2 bên mỗi bên 1
  nhịp câu hỏi riêng nên âm thanh sẽ chồng nhau, vì vậy KHÔNG phát. Unity: `StartQuestionAudio`/`StopQuestionAudio` (vòng lặp coroutine `QuestionAudioLoop`,
  hằng `QuestionAudioRepeatPause = 4`); web: `startQuestionAudio`/`stopQuestionAudio`.
- `settings.questionAudioOnly = true` — ẩn hẳn chữ/ảnh/icon câu hỏi (mọi slot câu hỏi), chỉ còn âm thanh — dạng "nghe và chọn" (vd SoDem/ChuCai). Vì âm thanh chỉ phát ở
  Combined, game `Independent` + `questionAudioOnly` sẽ không có gì để nghe — đừng kết hợp 2 cái này.
- Game v1 từng phát `question.audio` ở cả Independent — khi mở game cũ, hành vi Independent đổi theo quy tắc mới (không phát nữa).

## Round tự xáo trộn + chơi liên tục theo thời gian (đã mặc định, không cần bật)

**Đã xác nhận đúng ở cả Unity lẫn web tool, không cần sửa gì thêm**:
- Thứ tự round CHƠI THẬT luôn được xáo ngẫu nhiên — Unity (`BuildQuestionPool()`) và web tool
  (`shuffledPool()`) đều tự shuffle 1 danh sách chỉ-số round MỖI LẦN bắt đầu game/hết vòng, KHÔNG
  phụ thuộc thứ tự soạn trong editor. Nút "🔀 Xáo trộn thứ tự" ở cột round chỉ xáo lại thứ tự HIỂN
  THỊ/LƯU trong editor (tiện xem lại), không ảnh hưởng gameplay thật vì dù sao cũng bị shuffle lại.
- Game CHƠI LIÊN TỤC (endless) — hết danh sách round thì tự xáo lại và chơi tiếp từ đầu
  (`PullNextQuestion`/`nextRoundIndex` đều reshuffle khi cursor vượt quá độ dài mảng), KHÔNG dừng
  khi hết round.
- Game KẾT THÚC theo THỜI GIAN, không theo số round — `MiniGameControllerBase.totalRounds` mặc
  định là 0 (`GenericGamePlayerSceneBuilder` không set field này), nghĩa là `_useTimer = true`:
  hết `GameSettings.Instance.GameTime` (cấu hình chung toàn app, menu Cài đặt) thì game mới kết
  thúc, bất kể đã chơi được bao nhiêu round.

## Giới hạn đã biết

- Chỉ hỗ trợ display "button" (Single/MultiSelect/OrderedSequence/SumToTarget) — Matching/FloatingDisplay (quỹ đạo tròn kiểu SolarOrder) CHƯA có trong engine tổng
  quát này (vẫn phải tạo game kiểu cũ qua `/newminigame` nếu cần). Bố cục đáp án: tự đặt/ma trận/random theo từng round, hoặc spawn liên tục (cờ chung cả game; CHƯA có `onIdle`).
- Trong 1 round có thể trộn slot có icon và slot chỉ có chữ (mỗi ô tự quyết định cách hiển thị). Ảnh nền riêng của slot không bị clip theo `shape="circle"`.
- Item chưa có chế độ ma trận/random (chỉ tự đặt từng item); `FlyOff`/`FlyTo`/`FlyToStay` bị bỏ qua ở phản hồi của item.
- `onIdle` (Breathing mời gọi) CHƯA hỗ trợ ở spawn flow. `onCorrectRemove` chỉ có ý nghĩa khi `answerMode = MultiSelect/OrderedSequence/SumToTarget` (lúc 1 đáp án đúng
  tự ẩn giữa round) — với `Single`, round luôn kết thúc ngay ở lần chạm đúng đầu tiên nên dùng `onCorrectTap`.
- `countdownSeconds` chưa điều chỉnh được — Kit dùng cố định 3s (Independent) hoặc `GameSettings.RoundEndDelay` (Combined, cấu hình chung toàn app).
- Âm thanh slot câu hỏi chỉ phát ở Combined (xem mục trên); `sound` của effect phát ngay lúc hành động xảy ra, không ghép nối được với nhau.
- `imagePool` chỉ random 1 LẦN lúc game khởi động (`BuildQuestionPool()`), KHÔNG re-random mỗi lần round đó lặp lại trong CÙNG 1 phiên chơi.
- Chạm SAI (`WrongFinal`) mặc định kết thúc round ngay (hành vi gốc của Kit); muốn cho thử lại dùng `settings.wrongEndsRound=false` (xem mục riêng ở trên).

Những giới hạn trên đều có thể gỡ dần sau — mỗi lần gỡ 1 giới hạn: sửa `GenericGameController.cs` + cập nhật `capabilities.json` + đồng bộ lại bản nhúng trong web tool.

## Bổ sung v2.1 (web + Unity)

- **Câu hỏi chung ở chế độ Gộp**: `playMode="Combined"` → câu hỏi là 1 vùng CHUNG của cả màn hình (toạ độ % theo CẢ stage, kéo tự do qua 2 nửa, không mirror; Unity dựng 1 bộ slot trên lớp phủ `SharedQuestionLayer`). `Independent` → mỗi bên 1 bộ, toạ độ theo nửa màn hình. `layout.questionSpace="stage"` đánh dấu toạ độ mới; file cũ không có thì Combined tự quy đổi ×0.5 lúc nạp.
- **`group.mirror`** (mặc định true): câu hỏi ở chế độ Độc lập — false = nửa phải KHÔNG lật (giữ chiều đọc trái→phải).
- **`layout.layerOrder`** (string[], dưới→trên; nền luôn dưới cùng): thứ tự vẽ gồm `"question"`, `"answers"`, `"collect"` (đáp án bay sau khi chọn), `"feedback"` (icon ✔/✖) và `"deco:<id>"` cho từng item. Web xuất đầy đủ; thiếu/rỗng thì mặc định **Nền < Câu hỏi < Item < Đáp án < Đáp án bay < Icon feedback**. Unity gắn Canvas `overrideSorting` (sortingOrder = (vị trí+1)×10) vào từng đối tượng — `GenericGameController.ApplyLayer`/`NormalizeLayerKeys` (thuật toán bổ sung khoá thiếu PHẢI giống `layerKeys()` trong web). Chỉ sắp theo lớp hệ thống, không theo từng slot riêng lẻ.
- **`rounds[].collect.mirror`** (mặc định true): vùng "Đã chọn" (FlyToStay / thanh nhiên liệu) — false = nửa phải dùng NGUYÊN toạ độ nửa trái, không lật ngang. Unity: `CollectFlipsFor(team)` dùng ở `UnionSlots`/`SpawnLitCells`.
- **Ảnh nền slot mặc định STRETCH** (kéo kín ô, không giữ tỉ lệ).
- **`settings.iconColumns`** (mặc định 2): số cột lưới icon trong 1 slot (`ButtonItem.ArrangeIconGrid`, `QuestionSlotUi.grid`).
- **Vùng thu thập theo round**: `rounds[].collect` (nhóm slot, chỉ cần toạ độ + `arrangement` manual/matrix + `autoStretch`/`fillByValue`/`fillReverse`) thay cho `layout.collect*` cấp game (file cũ tự chép vào mọi round). Web: tab "📥 Đã chọn" trên canvas.
- Web chơi thử: ẩn ô tổng điểm giữa màn hình (Unity chưa đổi HUD của Kit).

## Scene riêng cho từng game (Import Zip) — cách khuyến nghị

`Tools → GenericGame → Import Zip (1 file)… / Import Zips (cả thư mục)…` (`GenericGameZipImporter.cs`) biến 1 gói .zip của web tool thành game có scene riêng:
- `Assets/Game/GenericGames/<id>/game.json` + `<id>_LayoutBase.prefab` (SINH TỰ ĐỘNG) + `<id>_Layout.prefab` (**Prefab Variant** — chỉnh tay lưu ở đây).
- `Assets/Game/Scenes/_GenericGame/Games/<id>.unity` (nhân bản từ scene mẫu `GenericGamePlayer`, tự thêm Build Settings). Ảnh/âm thanh vào `Resources/TestTongHop/{images,audio}/GenericGames/<id>/` như cũ.
- `<id>` = tên file zip bỏ dấu, PascalCase (vd "cửa hàng kem" → `CuaHangKem`); ghi đè `meta.gameId/displayName` trong game.json.
- Prefab chứa marker `GenericBakedRect` cho TỪNG ô câu hỏi/đáp án/vùng thu thập của MỌI round + từng item. Kéo/resize marker trong Scene view (chọn round xem bằng `GenericGameBaked.previewRound`; nửa trái là bản soạn, nửa phải tự mirror). Lúc chạy `GenericGameBaked.ApplyTo()` ghi toạ độ marker đè vào package → mọi logic khác (mirror, random, matrix...) không đổi. Chỉ sửa được VỊ TRÍ/KÍCH THƯỚC; nội dung (chữ, ảnh, đúng/sai, hiệu ứng) vẫn nằm ở game.json (sửa trên web rồi import lại).
- Re-import cùng zip: ghi lại game.json/Base/asset, GIỮ Variant + scene (override của object còn tồn tại trong Base vẫn giữ).
- Sau import: thêm 1 dòng `GameRegistry.Set(..., "<id>", "<tên>", "<id>", Engine.MiniGameKit, ...)` (sceneName = `<id>`, KHÔNG phải GenericGamePlayer) + `game_registry.json` cho K02.
