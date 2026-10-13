# Engine lõi cho Game Builder kiểu PowerPoint

> **⚠ 2026-10-13: hướng này (Slide/Trigger/engine lõi mới) đã bị thay thế** bởi [game-builder-architecture.md](game-builder-architecture.md) — giữ mô hình đối tượng v2 (Nền · Item · Câu hỏi · Đáp án…) và mở rộng bằng 3 ổ cắm (Kiểu hiển thị · Kiểu chấm · Hiệu ứng). File này chỉ còn để tham khảo.

> **TL;DR**: Bỏ hướng "game.json chọn Generator/Judge/View trong danh mục do code định sẵn". Hướng mới: **một engine lõi nhỏ, tổng quát** (Deck › Slide › Đối tượng + Hoạt ảnh + Trigger + Biến) — người dùng **tự gắn** đối tượng, hiệu ứng, sự kiện, chuyển round giống PowerPoint. Code chỉ cài "động từ" (đối tượng, sự kiện, hành động, hiệu ứng); **game là chương trình-bằng-dữ-liệu do người dùng ghép**. Logic quen thuộc (đúng 1 phần / đúng hết / sai) là **macro sinh ra trigger thường**, không phải code ẩn. Engine seed: lõi `fs/*.js ⇄ FlowWorld` đã có (scene, bind, 20+ action, biến, biểu thức) — phải **tổng quát hoá**, không viết mới.
> **Cấu trúc file**: **Phần A** = mô hình cho **cô giáo** (6 phần của một vòng chơi: Nền · Câu hỏi · Đáp án · Luật chấm · Phản hồi · Kết thúc; đáp án có 4 "ý nghĩa": Đúng/Sai · Cặp · Thứ tự · Giá trị; luật chấm = Khay chọn). **Phần B** = engine bên dưới (Deck/Slide/Trigger…) cho người phát triển.
> **Đọc khi**: định thiết kế/mở rộng engine builder, hoặc thêm đối tượng/sự kiện/hành động mới.
> **Cập nhật**: 2026-10-13. **Chưa làm gì trong code.** Thay thế §10–§11 của [game-builder-generalization.md](game-builder-generalization.md) (phần khảo sát §2–§9 của file đó vẫn là bằng chứng hợp lệ).

---
# PHẦN A — Mô hình cho cô giáo (người dùng cuối)

> Người dùng cuối là **cô giáo**: không thấy "trigger", "biến", "sự kiện". Cô thấy **6 ô** của một vòng chơi, điền bằng form + thư viện kéo thả. Engine ở Phần B chạy bên dưới; khi cần "Nâng cao" mới mở ra.

## A.0 Bản chất của một vòng chơi (rà từ 17 game FloorStory + 10 game GenericGame + các game Kit)
```
Nền & trang trí ─ (xuyên suốt) ──────────────────────────────────────────────┐
  [Xem trước?] → Câu hỏi → Bé CHẠM → Khay chọn → CHẤM → Đúng / Sai / Xong vòng → Phản hồi → Kết thúc → vòng kế
```
Mọi game đã có đều rơi vào chuỗi này; khác nhau ở **nội dung 6 ô** và **mấy chạm thì chấm**. Điều tôi chưa thấy trong bản trước: *"chạm"* không phải lúc nào cũng đã là câu trả lời — có game phải **gom 2 chạm mới chấm** (Lật thẻ), có game phải **gom đủ cả dãy** (Nhớ chuỗi). Chỗ gom đó gọi là **Khay chọn**.

| # | Phần | Cô giáo trả lời | Đặt ở đâu | Đã có |
|---|---|---|---|---|
| 1 | **Nền & trang trí** | Cảnh trông thế nào? | Cả game (từng vòng đổi được) | v2 ✔ |
| 2 | **Câu hỏi** | Bé thấy/nghe gì, xuất hiện ra sao? | Từng vòng | v2 ✔ (nội dung); *cách xuất hiện*: FloorStory `reveal` ✔ |
| 3 | **Đáp án & vùng chạm** | Bé chạm vào đâu? Mỗi cái "có nghĩa" gì? | Từng vòng | v2 ✔ (chỉ đúng/sai); FS thẻ/lưới/dải |
| 4 | **Luật chấm** | Chạm mấy cái thì chấm? Thế nào là đúng? Sai thì sao? | Cả game → từng vòng | v2 `answerMode` ✔ (4 kiểu); FS `judge` ✔ |
| 5 | **Phản hồi** | Đúng/sai thì xảy ra gì (hiệu ứng, âm, điểm)? | Cả game → từng đáp án | v2 4 khe `fx` ✔; FS `on.*` ✔ |
| 6 | **Kết thúc & chuyển vòng** | Khi nào hết vòng? Sang vòng nào? | Cả game + từng vòng | chờ 2 đội ✔; rẽ nhánh ✘ |

Quy tắc đặt: **đặt một lần ở Game, vòng nào khác thì ghi đè** (như CSS).

## A.1 Phần 1 — Nền & trang trí (xuyên suốt các vòng)
- **Bản chất**: các đối tượng **chỉ để nhìn**, không chấm điểm. Hiện từ đầu tới cuối game.
- Cô giáo làm: chọn ảnh nền; kéo thêm vật trang trí (cây, nhân vật, sông…), chỉnh vị trí/cỡ/thứ tự lớp; chọn *hiệu ứng nền* (thở, lắc nhẹ, trôi).
- Mở rộng thật sự cần (chưa có ở v2): **(a)** *đổi theo vòng* (vòng 3 nền khác); **(b)** *trang trí phản ứng* (nhân vật nhảy mừng khi đúng) — thuộc Phần 5, đích nhận hiệu ứng = "nhân vật Na"; **(c)** *thay đổi theo điểm* (cây lớn dần) — mục "Nâng cao: gắn với điểm".

## A.2 Phần 2 — Câu hỏi: **nội dung** × **cách xuất hiện**
- **Nội dung** (v2 đã tốt, giữ nguyên): nhóm slot Câu hỏi gồm **Chữ / Số / Icon (N lần) / Ảnh / Âm thanh** (có thể nhiều slot, có cả `chỉ phát âm`).
- **Cách xuất hiện theo thời gian** — đây là phần mới, và các game như *Nhớ chuỗi hình* chỉ khác ở đây:

| Cô giáo chọn | Nghĩa | Game ví dụ |
|---|---|---|
| **Luôn hiện** (mặc định) | hiện từ đầu vòng | quiz thường |
| **Hiện x giây rồi úp / ẩn** | cho xem rồi che | Nhớ hình |
| **Hiện lần lượt từng cái** (mỗi cái x giây, giữ y giây, rồi úp) | kể từng bước | **Nhớ chuỗi hình** (`reveal: sequential`) |
| **Chỉ phát âm** | không hiện hình | nghe-tìm số/chữ |
| **Đọc khi bé chạm vào** | loa nhỏ trên câu hỏi | cầm tay chỉ chữ |
- **Câu hỏi có thể chính là các đáp án** (Nhớ chuỗi: cùng những tấm ảnh; Lật thẻ: không có câu hỏi riêng). → ô "**Cho bé xem trước**" (không · x giây · lần lượt) áp lên *đáp án*, rồi mới bước vào chơi.
- Hiệu ứng vào/ra (hiện, bay vào, nảy…) chọn từ thư viện hiệu ứng như PowerPoint.

## A.3 Phần 3 — Đáp án & vùng chạm (phần cần phân loại kỹ nhất)
Một "đáp án" = **một thứ bé chạm được**. Bản chất gồm 3 lớp:

**① Hình thức** — nhìn thấy gì: ảnh / chữ / số / icon-N / hình khối / **vùng trong suốt** (chạm chỗ trên nền). Bố cục: tay, hàng, lưới, quạt, ngẫu nhiên không chồng.

**② Ý nghĩa** — *"nó là gì với luật chấm"* (đây là chỗ sót ở bản trước; **đúng/sai chỉ là 1 trong 4**):

| Ý nghĩa | Cô giáo điền | Game |
|---|---|---|
| **Đúng / Sai** | tick ✓ vào cái đúng | quiz ảnh, chọn quả, màu, hình |
| **Cặp** | cho 2 thẻ cùng *số cặp* | **Lật thẻ giống nhau**, Nối cặp |
| **Thứ tự** | đánh số 1, 2, 3… | **Nhớ chuỗi**, Cộng hình, Sắp hành tinh |
| **Giá trị** | nhập số (và hỏi "lớn nhất"/"nhỏ nhất"/"tổng bằng") | **Nặng–Nhẹ**, Tách số, So sánh |
→ Giao diện: **bảng như Excel**, mỗi hàng một đáp án, cột `Hình · Chữ · Âm · ✓Đúng · Cặp · Thứ tự · Giá trị`; **chỉ hiện cột phù hợp** với kiểu chơi đã chọn.

**③ Trạng thái** — nhìn thế nào ở từng lúc (quyết định hiệu ứng ở Phần 5):
`Úp (mặt sau)` · `Mở` · `Đang chọn` (viền sáng) · `Khớp / Đúng` · `Đã biến mất` · `Khoá` (không chạm được).
Cô giáo chỉ đặt **trạng thái ban đầu** (vd Lật thẻ: *Úp*; Nhớ chuỗi: *Ẩn cho tới khi xem xong*). Phần còn lại do luật chấm + phản hồi đổi.

## A.4 Phần 4 — Luật chấm: **Khay chọn → Chấm → Kết quả**
Mỗi lần bé chạm, đáp án đó **vào Khay chọn**. Khi Khay "đủ", engine **chấm**. Cô giáo chỉ trả lời 3 câu (dạng ô chọn):

| Câu hỏi | Lựa chọn |
|---|---|
| **1. Mỗi lượt bé chọn mấy cái?** | 1 · 2 · n cái · tất cả cái đúng · theo thứ tự cả dãy |
| **2. Chấm khi nào?** | ngay sau mỗi chạm · khi đủ số cái · khi bé bấm "Xong" |
| **3. Sai thì sao?** | cho thử lại · kết thúc vòng · chỉ trừ điểm · làm lại từ đầu dãy |
"Thế nào là đúng" **không hỏi** — nó suy ra từ cột Ý nghĩa ở Phần 3: *Đúng/Sai* → mọi cái chọn đều ✓ · *Cặp* → cùng số cặp · *Thứ tự* → đúng số kế tiếp · *Giá trị* → thoả điều kiện (lớn nhất/tổng…).

**Kết quả** mỗi lần chấm chỉ có 4 loại: **Đúng** · **Sai** · **Đang chờ** (khay chưa đủ) · **Xong vòng** (đã hết cái cần chọn).

**Sáu kiểu chơi dựng sẵn** (cô giáo chọn tên, 3 ô trên tự điền; "Tuỳ chỉnh" mới mở 3 ô):
| Tên | Chọn mấy | Chấm | Cột ý nghĩa |
|---|---|---|---|
| **Chọn 1 đáp án đúng** | 1 | ngay | ✓Đúng |
| **Chọn tất cả đáp án đúng** | tất cả cái đúng | ngay từng cái | ✓Đúng |
| **Lật cặp giống nhau** | 2 | khi đủ 2 | Cặp |
| **Chạm theo thứ tự** | cả dãy | ngay từng cái | Thứ tự |
| **Cộng cho đủ số** | n | ngay từng cái | Giá trị (tổng) |
| **Chạm đúng chỗ** (vùng) | 1 | ngay | ✓Đúng (vùng) |

**Ví dụ Lật thẻ giống nhau** theo 6 phần: Nền xanh lá · Câu hỏi: *không có* (hoặc xem trước các thẻ 3 giây) · Đáp án: N cặp thẻ, Ý nghĩa = **Cặp**, trạng thái đầu = **Úp** · Luật: *2 cái, chấm khi đủ 2, sai thì cho thử lại* · Phản hồi: *Đúng → cả 2 thẻ ting + biến mất; Sai → cả 2 thẻ úp lại* · Kết thúc: hết thẻ → +điểm, sang vòng kế.

## A.5 Phần 5 — Phản hồi: bảng "Khi nào × Cái gì × Hiệu ứng"
Một bảng duy nhất, cô giáo kéo hiệu ứng vào ô:

| Khi… | Hiệu ứng lên **đáp án vừa chọn** | lên **thứ khác** (câu hỏi · nhân vật · cả màn) | Âm |
|---|---|---|---|
| Bé **vừa chạm** | lật mở / sáng viền | | tách |
| **Đúng** | ting + bay / biến mất | nhân vật nhảy mừng | ting ting |
| **Sai** | lắc / úp lại | | ồ |
| **Xong vòng** | | confetti, câu hỏi phình | nhạc mừng |
| **Hết giờ** | | | |
- "**Đáp án vừa chọn**" = đúng tập đã vào khay (nên Lật thẻ làm 1 lần cho cả 2 thẻ).
- Hiệu ứng có **âm mặc định** (ting đúng, ồ sai, ✔/✖) — cô không điền cũng chạy (đã có ở v2/Unity).
- "Đích nhận hiệu ứng" là hộp chọn: `đáp án vừa chọn · các đáp án đúng · tất cả đáp án · câu hỏi · [chọn một vật trang trí] · cả màn`.

## A.6 Phần 6 — Kết thúc & chuyển vòng
- **Vòng kết thúc khi**: xong (đủ cái cần chọn) · sai (theo luật) · hết giờ · bé bấm nút.
- **Rồi**: sang vòng kế sau x giây · chờ cả 2 đội · rẽ nhánh (*nếu điểm ≥ n thì vòng Thưởng*) · lặp vòng này.
- **Thứ tự vòng**: lần lượt · ngẫu nhiên · theo bậc khó (nhóm vòng).
- **Điểm**: +n mỗi vòng đúng · +n mỗi lần chấm đúng · −n khi sai.
- **Game kết thúc khi**: đủ điểm · hết số vòng · hết giờ.

## A.7 Duyệt lại các game điển hình qua 6 phần
| Game | Câu hỏi (cách xuất hiện) | Đáp án (ý nghĩa) | Luật chấm (mấy · khi nào) | Phản hồi chính | Kết thúc |
|---|---|---|---|---|---|
| Quiz ảnh/chữ (CuaHang, ChuCai, ListenSelect…) | luôn hiện / chỉ âm | Đúng/Sai | 1 · ngay | bay + ting / lắc | sau 1 chấm đúng |
| Chọn quả theo lệnh (DemQua…) | âm + ảnh | Đúng/Sai (nhiều) | tất cả cái đúng · từng cái | bay vào rổ | khi hết quả đúng |
| **Lật thẻ giống nhau** | xem trước tuỳ chọn | **Cặp** | 2 · khi đủ 2 | ting + biến mất / úp lại | hết thẻ |
| **Nhớ chuỗi hình** | **lần lượt rồi úp** | **Thứ tự** | cả dãy · từng cái | lật mở thẻ đúng | hết dãy |
| Cộng hình | hiện dải | Thứ tự | cả dãy · từng cái | ghép hình dần | hết dãy |
| **Nặng–Nhẹ** | âm "con nào nặng nhất" | **Giá trị** (lớn nhất) | 1 · ngay | ván nghiêng → ting/lắc | sau chấm |
| Tách số / So sánh số | số/icon | Giá trị (tổng) | n · từng cái | đổ đầy ô | khi đủ tổng |
| Sàn kỳ diệu | không | Vùng chạm (không cần đúng) | 1 · ngay | hiệu ứng tại điểm chạm | hết giờ |
| Ngày của bé (kể chuyện) | cảnh theo buổi | Đúng/Sai (chọn đồ) | 1 · ngay | nhân vật di chuyển | hết cảnh |
→ 9 dòng, **3 ý nghĩa mới ngoài Đúng/Sai** (Cặp, Thứ tự, Giá trị) và **1 kiểu xuất hiện mới** (lần lượt rồi úp) là toàn bộ chỗ khác nhau quan trọng.

## A.8 Quan hệ với engine (Phần B)
- 6 phần được **biên dịch** thành Slide + đối tượng + trigger/biến thường (Phần B §2–§3). Cô giáo không thấy chúng, **trừ khi bấm "Nâng cao"**.
- **Khay chọn** là một thành phần của lõi: danh sách đối tượng đang chọn + sự kiện `khay.đủ` — **không** phải ba hàm Judge cứng.
- Ba mức người dùng: **(1) Cô giáo**: 6 ô + kiểu chơi dựng sẵn + thư viện hiệu ứng · **(2) Cô nâng cao**: bảng `Khi… thì…` · **(3) Lập trình viên**: module mới.
- Vì sao 4 ý nghĩa (Đúng/Sai, Cặp, Thứ tự, Giá trị) không để người dùng tự định nghĩa: số lượng nhỏ, cô hiểu ngay, kiểm thử được; thêm kiểu (vd "Nhóm") là thêm một ý nghĩa + một kiểu chơi, không đụng lõi.

---
# PHẦN B — Engine bên dưới (cho người phát triển)

## 1. Vì sao hướng cũ sai
Bản trước (6 chỗ cắm: Content · Generator · Presentation · Judge · Reaction · Session) vẫn là **"game chọn từ danh mục trong code"**: Generator, Judge, View là enum do lập trình viên viết; người dùng chỉ điền tham số. Hậu quả:
- Muốn luật mới (đúng 2/3 mới qua, sai thì quay về slide trước) → phải sửa code.
- Không làm được thứ rất thường trong PowerPoint: **hoạt ảnh tự do**, **chuyển slide có rẽ nhánh**, **trigger gắn vào bất kỳ đối tượng**.
- Mọi đường đều qua `game.json` mà ý nghĩa của nó nằm **trong code** (`gen.type = 'values'` chỉ có nghĩa vì C#/JS có hàm `values`).

**Nguyên tắc mới: code cài động từ, người dùng ghép câu.** Số "động từ" nhỏ và cố định (đối tượng, sự kiện, hành động, hiệu ứng, hàm); số "câu" (game) là vô hạn và nằm trong dữ liệu.

## 2. Mô hình lõi — 7 khái niệm, mượn từ PowerPoint

| Khái niệm | PowerPoint | Trong engine | Ghi chú |
|---|---|---|---|
| **Deck** | file `.pptx` | một game | danh sách Slide + biến toàn cục + bảng dữ liệu + chủ đề + cấu hình đội |
| **Slide** | slide | **một round / một màn** | có đối tượng, hoạt ảnh, trigger, biến cục bộ; có `chuyển sang` slide khác |
| **Đối tượng** | shape / ảnh / chữ / nhóm | node trong cây cảnh | loại, vị trí/cỡ/xoay, kiểu, **trạng thái**, cha–con (Nhóm) |
| **Hoạt ảnh** | Animation Pane | hiệu ứng có `đích · thời điểm · thời lượng · easing` | vào / nhấn / ra / đường đi / **đổi thuộc tính**; xếp trên **dòng thời gian** của slide |
| **Trigger** | Trigger (Animation) + Hyperlink | `Khi [sự kiện] [nếu điều kiện] → [chuỗi hành động]` | gắn trên **bất kỳ** đối tượng, slide hoặc deck |
| **Hành động** | Action Settings | thứ trigger chạy | phát hoạt ảnh, đặt/tăng biến, âm, hiện/ẩn, **chuyển slide**, điểm, kết thúc |
| **Biến + biểu thức** | (VBA / không có) | số / chữ / đúng-sai, phạm vi Deck·Slide·Đối tượng·Đội | biểu thức tối thiểu: `+ − × ÷ so sánh và/hoặc`, `random`, `đếm(nhóm)` |

Hết. Mọi game (kể cả Nặng–Nhẹ, Đếm quả, quiz đáp án) là **tổ hợp 7 thứ này**. Không có "kiểu game" trong lõi.

### 2.1 Đối tượng
- **Loại** (~8, mở rộng được bằng module): `Hình` (chữ nhật/tròn/…) · `Ảnh` · `Chữ` · `Nhóm` (chứa đối tượng khác, có bố cục: tay/hàng/lưới/quạt/ngẫu nhiên) · `Âm` · `Video` · `Nút` (= hình + chữ có click) · `Widget` (module).
- **Thuộc tính** là dữ liệu của đối tượng, không phải đối tượng khác: nội dung/skin, transform, alpha, z, **gắn-vào** (cha), **bind** (thuộc tính ← biểu thức, tính lại mỗi khung), **trạng thái**.
- **Trạng thái** là tập nhãn đặt tự do (`ẩn`, `khoá`, `đã chọn`, `đúng`, `sai`, …) + **kiểu theo trạng thái** (đổi màu/ảnh khi vào trạng thái) — thay cho cờ viết cứng như `Covered/Locked`.
- Mọi đối tượng có `tên`, `nhãn (tag)` để bộ chọn nhắm tới (`#a1`, `.dapan`, `@tapped`, `trong:Nhóm1`).

### 2.2 Hoạt ảnh (animation)
- Preset (≈15): `Hiện/Ẩn · Nảy vào · Bay vào · Bay ra · Mờ dần · Nhịp thở · Nảy · Lắc · Xoay · Phóng to/thu · Đổi màu · Đường đi (A→B) · Bay tới & ở lại` + **tuỳ chỉnh**: đặt thuộc tính theo thời gian (keyframe đơn giản).
- Xếp trên **dòng thời gian slide** (như Animation Pane): `Khi click · Cùng lúc với trước · Sau trước · Sau N giây · Khi trigger`. Mỗi hoạt ảnh có tên để trigger gọi.
- Trigger và dòng thời gian là **một hệ**: dòng thời gian = chuỗi trigger `slide.enter` có thứ tự; người dùng thích kiểu nào dùng kiểu đó.

### 2.3 Sự kiện (do engine/nền tảng phát) và Hành động (do người dùng ghép)
| Sự kiện | Phát khi |
|---|---|
| `slide.vào / slide.ra` | slide hiện / rời |
| `click(đối tượng)` | chạm LiDAR/touch trúng đối tượng (tap-only) |
| `sau(N giây)` · `mỗi(N giây)` | bộ hẹn giờ trong slide |
| `biến.đổi(tên)` | giá trị biến đổi |
| `hoạt-ảnh.xong(tên)` | một hoạt ảnh chạy xong |
| `trạng-thái.đổi(đối tượng, nhãn)` | đối tượng vào/ra trạng thái |
| `nhóm.đầy/rỗng/đếm` | số con trong Nhóm đổi |
| `thông-điệp(tên)` | ai đó `phát` |
| `deck.hết-giờ` | đồng hồ cả game |
| *(module)* `chạm-nhau`, `ra-khỏi-màn`… | module real-time đăng ký |

| Hành động (≈20) | |
|---|---|
| `Phát hoạt ảnh (tên / đích + preset)` · `Hiện / Ẩn / Xoá / Nhân bản đối tượng` · `Đặt / Tăng biến` · `Đặt trạng thái` · `Phát âm` · `Nói (đọc chữ)` · `Cộng điểm(đội)` · `Phát thông điệp` · `Chờ N giây` · `Chờ sự kiện` · `Nếu…thì…khác` · `Song song` · `Lặp` · `Chuyển slide (kế / tới / ngẫu nhiên / rẽ nhánh)` · `Kết thúc game` |

Lõi `fs_act.js` hiện đã có: `sfx say confetti bounce shake pulse move scale fade tint rotate show hide pop destroy spawn set inc bag pickItem if wait fly par text std` + đích `@tapped @target @actor #id`. **≈ 70% động từ đã có**; thiếu chủ yếu `click` là sự kiện tổng quát, `chuyển slide`, `trạng thái`, `đếm nhóm`, `chờ sự kiện`, bộ chọn theo nhãn.

### 2.4 Chuyển round = chuyển slide
- Slide có **`khi kết thúc`** (một hành động `chuyển slide`) hoặc trigger tự do `→ chuyển tới Slide B`.
- Kiểu chuyển: `kế tiếp · tới Slide X · ngẫu nhiên trong tập · lặp slide này · rẽ nhánh theo biểu thức` (vd `nếu điểm ≥ 3 → Slide Thắng, không → Slide Ôn`).
- Hiệu ứng chuyển: `cắt · mờ · trượt · không`.
- **Chờ đội kia** (hiện là `waitBothTeams`) = một ô của Slide: "Chuyển khi: ngay / cả hai đội xong".
- Bộ sắp xếp Slide (như Slide Sorter) hiển thị **đồ thị chuyển** để người dùng nhìn luồng cả game.

## 3. Logic đơn giản có sẵn: Quiz là **macro**, không phải code ẩn
Yêu cầu cốt lõi của người dùng: "đúng 1 phần, đúng hết, sai…". Cách làm:

1. Người dùng đánh dấu một **Nhóm** là *Câu hỏi* và đặt cho từng con đối tượng nhãn **Đúng / Sai** (hộp kiểm trong bảng thuộc tính).
2. Chọn **Kiểu chấm** (4 lựa chọn): `Chọn 1 đúng` · `Chọn nhiều — cần đủ` · `Chọn nhiều — tính từng phần` · `Tuỳ chỉnh`.
3. Builder **sinh ra trigger và biến thường** (xem được, sửa được, có nút "Chuyển thành trigger"):
```
biến slide:  q.đúng = 0 · q.sai = 0 · q.cần = đếm(.đúng)
mỗi đối tượng .đúng:  Khi click → q.đúng++ ; đặt trạng thái "đã chọn" ; phát sự kiện  q.chọn-đúng
mỗi đối tượng .sai :  Khi click → q.sai++  ; phát sự kiện  q.chọn-sai
Khi biến.đổi(q.đúng) nếu q.đúng = q.cần → phát  q.hoàn-thành      (kiểu "cần đủ")
Khi q.chọn-sai nếu kiểu = "Chọn 1" → phát q.sai-kết-thúc
```
4. Sự kiện `q.chọn-đúng · q.chọn-sai · q.một-phần · q.hoàn-thành · q.sai-kết-thúc` là chỗ **người dùng gắn hiệu ứng và chuyển slide** — đúng quy trình PowerPoint: *chọn sự kiện, kéo hiệu ứng vào*.

→ Kiến thức "chấm quiz" nằm **một chỗ** (macro) nhưng kết quả là **dữ liệu thường**; sau này thêm kiểu chấm mới (vd "đúng thứ tự") là thêm macro, không đụng engine.

Cùng cách làm cho: **Đếm** (biến + trigger), **Hẹn giờ** (sự kiện `sau/mỗi`), **Điểm đội** (hành động `Cộng điểm`), **Đồng hồ/Bập bênh** (mẫu dựng sẵn = Nhóm + bind + trigger).

## 4. Dữ liệu động (thay cho "Generator trong code"): Bảng dữ liệu → Slide
PowerPoint cũng có nhu cầu này (mail merge). Phiên bản của ta:
- **Bảng dữ liệu** của Deck, như một sheet: cột = `ảnh · tên · âm · giá trị · tag…` (người dùng tự đặt cột).
- **Slide mẫu** có ô gắn cột bằng bind: `Ảnh ← hàng.ảnh`, `Chữ ← hàng.tên`, `đúng ← hàng.giá trị = max(hàng)`.
- **Tập Slide** sinh từ bảng: `mỗi hàng một slide` · `chọn k hàng ngẫu nhiên không lặp` · `nhóm theo cột` · **thứ tự**: tuần tự / ngẫu nhiên (seed theo đội).
- "Bốc 2–3 con có giá trị khác nhau" (Nặng–Nhẹ) = **cấu hình tập Slide** (`chọn k=2..3 hàng, giá trị khác nhau`) chứ không phải hàm `values` trong code. Người dùng cũng có thể bỏ qua và **dựng tay từng slide**, như PowerPoint.
- Hàm giá trị dùng được trong bind: `random(a,b) · hàng.cột · đếm(nhãn) · tối-đa(cột) · xáo(danh sách)`.

## 5. Nền tảng (không phải lõi, người dùng không soạn)
Hai đội + hai viewport + lật nửa phải, tap LiDAR → sự kiện `click`, HUD (điểm, giờ), âm mặc định, log. Lộ ra cho lõi dưới dạng **biến dựng sẵn** (`đội.điểm`, `đội.tên`, `deck.giờ-còn-lại`) và **hành động dựng sẵn** (`Cộng điểm`, `Kết thúc game`). Mỗi sự kiện mang theo `đội`.

Cách chạy hai đội: Slide chạy **độc lập** trên mỗi đội (hạt giống chung hoặc riêng); biến phạm vi `Đội` tách đôi, phạm vi `Deck` dùng chung.

## 6. Các tầng kiến trúc
```
L5  Builder kéo thả       Slide Sorter · Canvas · Bảng thuộc tính · Animation/Trigger Pane · Bảng dữ liệu
L4  Thư viện (macro)      Quiz · Đếm · Hẹn giờ · Điểm · Mẫu dựng sẵn (Bập bênh, Đồng hồ…) · Hiệu ứng mẫu · Slide mẫu
                          → khi dùng, SINH RA dữ liệu lõi (L2), không có logic ẩn
L3  Module (mở rộng bằng mã)  đăng ký thêm LOẠI ĐỐI TƯỢNG / SỰ KIỆN / HÀNH ĐỘNG / HÀM (3D, real-time, lưới chữ, camera mặt…)
L2  deck.json             biểu diễn thuần khai báo của L1 — KHÔNG chứa khái niệm game
L1  Engine lõi            cây cảnh · bộ phát hoạt ảnh · event bus · trình thông dịch trigger/hành động · kho biến/biểu thức · quản lý slide
L0  Nền tảng              2 đội · tap · âm · điểm/HUD · log · tài nguyên
```
- **Một đặc tả lõi, hai bản cài** (JS cho builder + chơi thử, C# cho Unity), cùng **bộ test tuân thủ**: chạy cùng deck + cùng seed + cùng chuỗi click → so **vết sự kiện** (trace) và trạng thái cuối.
- **Mở rộng không sửa lõi**: module đăng ký vào 4 sổ đăng ký (`objectTypes · events · actions · functions`). Builder đọc sổ đăng ký để vẽ Palette và ô tham số → thêm module = thêm khối kéo thả mới.

## 7. Chọn engine lõi
| Phương án | Mô tả | Ưu | Nhược |
|---|---|---|---|
| **A. Tổng quát hoá lõi FloorStory** (`fs/*.js ⇄ FlowWorld`) | giữ scene+bind+action+biểu thức; bỏ `gen/view/judge` khỏi lõi, thêm Slide, trigger gắn đối tượng, sự kiện click chung, trạng thái, timeline | ~70% động từ đã có; **đã có parity web⇄Unity + seed + test**; đã có `par/wait/if`; chạy trên K02 | phải gỡ phần "kiểu game" ra khỏi lõi; đồng bộ hai bản khi mở rộng |
| B. Dựa trên GenericGame v2 | slot + khe fx | đã ra bản chạy ổn | mô hình theo slot, không có biến/biểu thức/chuyển slide ⇒ khó thành PowerPoint |
| C. Viết engine mới (ECS) | sạch | tự do thiết kế | chi phí cao, mất parity hiện có |
| D. Dùng engine có sẵn (Unity Timeline/Visual Scripting; web Pixi/GSAP…) | | nhanh dựng hoạt ảnh | **hai nền tảng hai mô hình khác nhau** → không giữ được "xuất một lần, chạy cả web lẫn Unity"; nặng cho MT8168 |
**Khuyến nghị: A.** F2 (GenericGame) và F1 hiện có trở thành **thư viện mẫu + importer** (slot → đối tượng, `flows[].on.*` → trigger) để game cũ không hỏng; không còn là engine riêng.

## 8. Phủ các game đang có (dựng bằng lõi, macro hay module)
| Game / họ | Dựng bằng | Ghi chú |
|---|---|---|
| Quiz chọn đáp án (Fruit, SoDem, ChuCai, FamilyMember, SoSanh…) | Slide + Nhóm đáp án + **macro Quiz** + bảng dữ liệu | không cần Generator/Judge trong code |
| Thứ tự (PlanetOrder, SentenceBuilder) | Quiz kiểu "đúng thứ tự" (macro) | thêm macro |
| Ghép cặp / lật thẻ (Matching, LatThe) | trạng thái đối tượng + biến "đang mở" + trigger | không cần kiểu riêng |
| Đếm (DemQua, TachSo2, CuaHang) | Nhóm "rổ" (`nhóm.đầy/đếm`) + biến + hoạt ảnh Bay-tới-ở-lại | |
| Phép tính (AddNumber…) | bảng dữ liệu + `random` + biểu thức | |
| Nặng–Nhẹ, Dài–Ngắn, Đồng hồ khổng lồ, Bập bênh | **Mẫu dựng sẵn** (Nhóm + bind) + Quiz + bảng dữ liệu | |
| Animation thuần / kể chuyện (Ngày của bé…) | Slide + dòng thời gian + chuyển slide | đúng kiểu PowerPoint |
| WhoIsIt (cược, hộp quà) | Slide modal + `Chờ sự kiện` + biến điểm | |
| WordHuntMaze | module **lưới chữ** (loại đối tượng + sự kiện `đường-chọn-xong`) | |
| LaneDash, SaveTheAstronaut | module **real-time** (Spawner, va chạm, làn) | gắn lõi qua sự kiện |
| SolarSystem, FRTest | module 3D / camera | |

## 9. Ví dụ dựng từ lõi
**(a) Quiz tĩnh 1 round, 3 đáp án.** Slide có Ảnh câu hỏi + Nhóm(3 ảnh đáp án). Đánh dấu 1 con *Đúng*, macro Quiz "Chọn 1 đúng". Gắn: `q.chọn-đúng → con đó Bay-tới(Ảnh câu hỏi) + ✔ + Cộng điểm +1 → Chuyển slide kế`; `q.chọn-sai → con đó Lắc + ✖`. Sao chép slide, thay ảnh = 10 round. Hoặc 1 slide mẫu + bảng dữ liệu 10 hàng.

**(b) Animation thuần.** Slide 1: Nền + Ảnh "Mặt trời" `Hiện (cùng lúc)` + `Đường đi (cung)` 3 giây + `Chữ "Buổi sáng"` `Mờ vào sau 1s` → `sau(5s) → Chuyển slide 2` (mờ). Không có trigger click, không có biến.

**(c) Đếm quả.** Biến slide `đã = 0`. 5 ảnh quả (nhãn `.qua`). `.qua: click → Bay-tới-ở-lại(rổ.ô-kế) ; đã++ ; Nói(đã)`. `biến.đổi(đã) nếu đã = 5 → Nói "Đủ rồi" ; Chờ 1s ; Chuyển slide kế`.

**(d) Nặng–Nhẹ (rẽ nhánh theo điểm).** Slide mẫu: Nhóm bập bênh (mẫu dựng sẵn: Ván `rot ← dấu(giá-trị trái − giá-trị phải)×15°`, 2 vật gắn vào ván), Quiz "Chọn 1" với *Đúng* = `giá trị = tối đa`. `slide.vào → [song song: vật Nảy-vào] → Chờ 0.5s → Ván Lắc 0.7s → Phát âm gỗ → Ván nghiêng 0.9s → bỏ trạng thái "khoá" của 2 vật`. Bảng dữ liệu = 18 con vật (cột `ảnh/tên/giá trị`). Tập Slide: `chọn 2 hàng khác giá trị ×5 + chọn 3 hàng khác giá trị ×5`. `nếu điểm đội ≥ 7 → Slide Thưởng`.

## 10. Builder (hướng đích)
Bố cục như PowerPoint: **trái** Slide Sorter (có đồ thị chuyển) · **giữa** Canvas (kéo thả, tay cầm) · **phải** Thuộc tính (skin, transform, bind, trạng thái, nhãn) · **dưới** Animation/Trigger Pane (dòng thời gian + danh sách trigger theo sự kiện) · tab **Dữ liệu** (bảng). Palette theo các sổ đăng ký (đối tượng · hiệu ứng · hành động · macro). Chơi thử hai nửa ngay trong builder bằng bản JS của lõi.

Ba mức độ phức tạp cùng một dữ liệu: **(1) Mẫu & macro** (quiz, đếm) → **(2) Danh sách hiệu ứng theo sự kiện** → **(3) Trigger/biến/biểu thức đầy đủ**.

## 11. Rủi ro và việc cần chứng minh trước
1. **Biểu diễn trigger quá tự do → khó debug.** Cần: bảng Trigger theo sự kiện, đánh dấu trigger đang chạy khi chơi thử, nhật ký sự kiện, giới hạn độ sâu `phát`.
2. **Hiệu năng K02 (MT8168).** Hoạt ảnh/trigger rẻ; chi phí ở số đối tượng + hiệu ứng đồng thời → giới hạn + dùng pool; đo trên máy thật.
3. **Giữ parity JS ⇄ C#.** Bắt buộc bộ test tuân thủ + vết sự kiện (mở rộng `roundcmp.js` đang có).
4. **LiDAR chỉ có tap.** Không có kéo/rê/giữ ⇒ không đưa các sự kiện đó vào lõi; "kéo thả" chỉ ở **builder**, không ở runtime.
5. **Không để macro thành hộp đen.** Luôn cho "Chuyển thành trigger".
6. **Giữ game đã xuất.** Importer cho GenericGame v2 và FloorStory v3, giữ `schemaVersion`.
7. **Chưa kiểm** (cần đọc từng dòng trước khi làm): `fs_core.js`/`FsScene.cs` hiện xử lý sự kiện click tổng quát được tới đâu; `std{ev:right|wrong}` và `judge` đang buộc chặt với `Q` thế nào.

## 12. Lộ trình (đề xuất, chưa chốt)
- **S0 — Spike chứng minh** (không đụng game cũ): trên bản JS lõi dựng được *(a) quiz tĩnh 3 đáp án có chuyển slide* và *(b) animation thuần 2 slide* **chỉ bằng dữ liệu**; đo vết sự kiện. Sau đó cài đúng spec đó sang C# và chạy test tuân thủ.
- **S1 — Lõi đủ dùng**: Slide + chuyển slide, sự kiện click chung, trạng thái + kiểu theo trạng thái, bộ chọn theo nhãn, `Chờ sự kiện`, `phát thông điệp`.
- **S2 — Macro Quiz + Hoạt ảnh preset + dòng thời gian** trong builder.
- **S3 — Bảng dữ liệu → tập Slide** + hàm giá trị.
- **S4 — Mẫu dựng sẵn** (Bập bênh, Đồng hồ, Rổ…) và **importer** F1/F2 → deck.
- **S5 — Module** (lưới chữ, real-time, 3D) qua sổ đăng ký.
Nghiệm thu mỗi bước = **dựng lại một game cũ chỉ bằng UI** (S2: một quiz; S3: Nặng–Nhẹ; S5: WordHunt) và so với bản code.
