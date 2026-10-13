# Kiến trúc Game Builder — tổng quát hoá từ GenericGame v2

> **TL;DR**: Giữ nguyên **mô hình đối tượng của v2** (Nền · Item · Câu hỏi chung/riêng · Đáp án · Vùng thu thập · Round · Cài đặt) vì đã chạy tốt với cô giáo. Mọi game khác nhau thật sự ở **3 chỗ** → làm thành **3 ổ cắm** khai báo trong `capabilities.json`: **Kiểu hiển thị** (đáp án vẽ/hành xử thế nào), **Kiểu chấm** (chạm thế nào thì đúng), **Hiệu ứng** (khi nào · lên ai · làm gì · trễ bao lâu). Thêm game kiểu mới = thêm một mục vào ổ cắm, không đổi mô hình.
> **Đọc khi**: mở rộng web builder / `GenericGameController`, thêm kiểu chấm, kiểu hiển thị hoặc hiệu ứng mới.
> **Cập nhật**: 2026-10-13 · **chưa làm gì trong code**. Thay thế hướng "Slide/Trigger" trong [game-builder-core-engine.md](game-builder-core-engine.md). Khảo sát các họ game: [game-builder-generalization.md](game-builder-generalization.md) §1–§9. Bằng chứng: `Assets/Game/Scripts/Core/MiniGameKit/GenericGame/{SCHEMA.md,capabilities.json}`, `WebTools/GenericGameBuilder/fs/games/*.json`.

## 1. Nguyên tắc
1. **Người dùng cuối là cô giáo**: chỉ thấy các đối tượng quen (nền, câu hỏi, đáp án, nhân vật), danh sách lựa chọn và bảng điền. Không có "trigger", "biến", "sự kiện".
2. **Mô hình đối tượng cố định** (v2). Cái được mở rộng là *thuộc tính của đối tượng* và *3 ổ cắm*.
3. **`capabilities.json` là nguồn chân lý** (đã có): web builder đọc nó để vẽ form; Unity cài từng mục. Thêm một lựa chọn = **1 mục registry + 1 bản cài JS (chơi thử) + 1 bản cài C#**.
4. **Ngoài phạm vi builder** (giữ là game viết code): thời gian thực (LaneDash, SaveTheAstronaut), khám phá 3D (SolarSystem), công cụ (FRTest), lớp thưởng cược/hộp quà (WhoIsIt).

```
 Cô giáo ─ kéo thả / điền form ─▶ Web builder ──▶ game.json (+ ảnh, âm) ──▶ Unity (GenericGame, C#)
                                       │  ▲                │
                          đọc ─────────┘  └── chơi thử (JS) ┘      capabilities.json = danh mục 3 ổ cắm
```

## 2. Đối tượng (bản đồ v2, có chỗ mở rộng)

| Đối tượng | Là gì | Phạm vi | Chung / riêng | Thuộc tính chính | Mở rộng cần |
|---|---|---|---|---|---|
| **Cài đặt game** | luật chung | cả game | — | chế độ đội, điểm, đếm ngược, chờ 2 đội, sai thì sao, font | — |
| **Nền** | ảnh nền + nhạc nền | cả game | mirror 2 nửa | ảnh, nhạc | **đổi theo round** |
| **Item** | nhân vật / vật trang trí | cả game | mirror ảnh | ảnh, vị trí, lớp, hiệu ứng chờ, **phản ứng** (chạm / đúng / sai / 1 phần), gắn điểm | **Item theo round**; Item chạy được hiệu ứng *Di chuyển* |
| **Câu hỏi** | nhóm slot: chữ · số · icon (N lần) · ảnh · âm | từng round | **Chung** (Gộp: 1 vùng cả màn) / **Riêng** (Độc lập: mỗi đội 1 bộ) | nội dung, bố cục, hiệu ứng chờ, `hideOnCorrect`, `sendToBack` | **cách xuất hiện** (vào round: hiện x giây rồi úp, hiện lần lượt) |
| **Đáp án** | nhóm slot bấm được | từng round | mỗi đội 1 bộ (mirror) | nội dung, **thuộc tính chấm** (§3), âm, hiệu ứng riêng; **Kiểu hiển thị** của nhóm (§4) | thêm thuộc tính & trạng thái ban đầu |
| **Vùng thu thập** | ô đích đáp án bay vào | từng round | mirror hoặc không | ô, lấp theo giá trị, tự co giãn | — |
| **Round** | gói {câu hỏi, đáp án, thu thập, mục tiêu} | — | — | thứ tự xáo, chơi theo thời gian | **kiểu chấm / hiển thị theo round** (game xen nhiều dạng) |
| **Hiệu ứng** | thư viện + chỗ cắm | game → ghi đè theo slot/Item | — | §6 | khi nào / ai / trễ |

## 3. Thuộc tính của đáp án (cô giáo điền trong **bảng đáp án**)
Mỗi đáp án = một hàng: `Hình · Chữ/Số · Âm · …` cộng các cột *chỉ hiện khi kiểu chấm/hiển thị cần*:

| Cột | Dùng bởi | Có sẵn |
|---|---|---|
| **✓ Đúng** | Chọn 1 · Chọn nhiều · Theo thứ tự | ✔ (`correct`) |
| **Thứ tự** | Theo thứ tự | ✔ ngầm (= thứ tự khai báo các đáp án đúng, nút ↑↓) |
| **Giá trị (số)** | Cộng đủ số · Kiểu hiển thị có cơ chế (cân, kim…) | ✔ một phần (Cộng đủ số đọc số từ chữ) |
| **Cặp (số cặp)** | Ghép cặp | ✘ mới |

**Trạng thái đáp án** (theo `ItemState` của Kit + mới): `Bình thường · Đang chọn · Đúng · Sai · Hiện đáp án · Khoá` + **`Úp` (hiện mặt sau)** + **`Ẩn-chờ` (chưa hiện, đợi xem trước xong)**. Cô giáo chỉ đặt *trạng thái ban đầu*; phần còn lại do kiểu chấm và hiệu ứng đổi.

## 4. Ổ cắm ① — Kiểu hiển thị (đáp án / vùng chạm trông và hành xử thế nào)
**Hợp đồng của một kiểu hiển thị** (khai báo trong `capabilities.json`, thay cho `answerDisplayTypes` chỉ có `button` hiện nay):
`id · tên · hình minh hoạ · cột đáp án cần · tham số riêng · trạng thái hỗ trợ · điểm neo (anchor) có tên · kiểu chấm đi kèm được · cơ chế (nếu có)`.

Phân biệt hai thứ mà bản đầu trộn lẫn:
- **Bố cục** = đặt các đáp án ở đâu (tĩnh). Có sẵn v2: *tự đặt · ma trận · ngẫu nhiên · trôi (spawn)*. Kit có sẵn trong code nhưng chưa vào generic: *quỹ đạo · quạt cung · hai cột nối cặp · lưới chữ*.
- **Cơ chế** = hình vẽ **thay đổi liên tục theo một giá trị** của đáp án. Ví dụ **cân thăng bằng**: nghiêng về bên có đáp án nặng hơn (đọc cột *Giá trị*). Cùng loại: bập bênh, kim đồng hồ, thanh co giãn. Cơ chế thuộc *Kiểu hiển thị*, **không phải hiệu ứng** (xem §6.4).

| Kiểu hiển thị | Là | Cột cần | Có |
|---|---|---|---|
| Nút/thẻ — tự đặt · ma trận · ngẫu nhiên | bố cục | — | ✔ v2 |
| Trôi (spawn) | bố cục | — | ✔ v2 (cờ chung cả game) |
| Quỹ đạo · Quạt · Hai cột · Lưới chữ | bố cục | tuỳ | code Kit, **chưa vào generic** |
| **Cân thăng bằng / Bập bênh** | cơ chế | **Giá trị** | ✘ mới (game Nặng–Nhẹ) |
| **Vùng chạm tự do** | bố cục (vùng trong suốt) | — | ✘ mới (game Sàn kỳ diệu) |
| Kim đồng hồ · Thanh co giãn · Đống khối | cơ chế | Giá trị | ✘ tuỳ, cùng loại với cân |

**Nhận xét từ rà soát 7 "view" của FloorStory** (`cards · props · strip · grid · seesaw · stack · floor`): chỉ **seesaw** (cân) và **floor** (vùng chạm) thực sự cần kiểu hiển thị mới. Các view còn lại **quy về v2 + trạng thái + chấm**:
- `cards`, `props` → đáp án nút/thẻ bố cục tự đặt (kích thước khác nhau do cô đặt, ảnh vẽ sẵn).
- `strip` → vùng thu thập (ô chỗ trống điền dần).
- `grid` (thẻ úp) → ma trận + trạng thái ban đầu **Úp** + kiểu chấm **Cặp**.
- `stack` → icon (N lần) của v2 — lưới phẳng; cụm khối 3D ngẫu nhiên và phần "đếm có số, gạch chỉ" xem §11.

**Cơ chế "cân" cụ thể**: nhóm đáp án có 2 (hoặc 3 = hai bập bênh nối) đáp án đặt lên hai đầu; tham số: số đáp án, góc nghiêng, hiệu ứng vào (lắc nhẹ → nghiêng, bật/tắt). Đáp án **chỉ chạm được sau khi cân nghiêng xong** (trạng thái *Khoá* → *Bình thường*). Đúng/sai vẫn do cô tick ✓ (hoặc nút "tự tick theo giá trị: lớn nhất / nhỏ nhất" trong builder — chỉ là tiện ích lúc soạn, runtime không đổi).

## 5. Ổ cắm ② — Kiểu chấm (chạm thế nào thì tính là đúng)
**Hợp đồng**: `id · đơn vị chấm (số chạm gom lại) · cột đáp án cần · kết quả phát ra · tham số (sai thì sao)`. Mỗi **kết quả** tự thành một *chỗ cắm hiệu ứng* (§6).

| Kiểu chấm | Đơn vị | Cột cần | Kết quả phát ra | Có |
|---|---|---|---|---|
| Chọn 1 đúng (`Single`) | 1 chạm | ✓ | đúng hết · sai | ✔ |
| Chọn nhiều (`MultiSelect`) | từng chạm đến đủ | ✓ | đúng 1 phần · đúng hết · sai (bỏ chọn được) | ✔ |
| Theo thứ tự (`OrderedSequence`) | từng chạm theo dãy | ✓ + thứ tự | đúng 1 phần · đúng hết · sai (làm lại từ đầu dãy) | ✔ |
| Cộng đủ số (`SumToTarget`) | từng chạm, cộng dồn | Giá trị | chưa tới · đủ · vượt | ✔ |
| **Ghép cặp** | **2 chạm** | **Cặp** | **chọn thẻ đầu · cặp đúng · cặp sai** · đúng hết (hết cặp) | ✘ mới (Lật thẻ; Kit có `Matching` ở code) |
| Không chấm | 1 chạm | — | chỉ "vừa chạm" | ✘ mới (Sàn kỳ diệu) |

- `ClickResult` của Kit (`WrongFinal · CorrectPartial · CorrectFinal · WrongInSequence · Deselected · WrongPartial`) là giao diện giữa kiểu chấm và hiệu ứng; **Ghép cặp** cần thêm kết quả "chờ cặp" (chạm đầu) — còn cặp đúng/sai dùng được ngữ nghĩa *đúng/sai chưa xong*.
- Cài đặt chung đi kèm: `wrongEndsRound` (sai thì kết thúc hay thử lại), `waitBothTeams`, điểm — **không đổi**.
- **Tương thích Hiển thị ⇄ Chấm** (builder lọc danh sách để cô không chọn sai): Cân ↔ Chọn 1; Thẻ úp (ma trận+Úp) ↔ Ghép cặp; Dải/Vùng thu thập ↔ Theo thứ tự; Nút/Trôi ↔ Chọn 1/Chọn nhiều/Cộng đủ số.

## 6. Ổ cắm ③ — Hiệu ứng
### 6.1 Mô hình (mở rộng v2, tương thích ngược)
Một hiệu ứng được xác định bởi **4 điều cô giáo chọn**:

| | Chọn | v2 hiện nay | Mở rộng |
|---|---|---|---|
| **Khi nào** (chỗ cắm) | từ danh sách | `Chờ · Đúng hết · Sai · Đúng 1 phần` (+ Item: `Chạm · Đúng · 1 phần · Sai`) | + **Vào round** · + **Vừa chạm** · + các kết quả riêng của kiểu chấm (VD *chọn thẻ đầu · cặp đúng · cặp sai*) · + **Sai từ lần thứ n** (VD gợi ý sau 2 lần sai khi cho thử lại) |
| **Lên ai** (đích) | từ danh sách | ngầm = chính đối tượng chủ | **đáp án vừa chọn** (Ghép cặp: cả 2) · **các đáp án đúng** · **tất cả đáp án** · **câu hỏi** · **một Item** (chọn tên) · **cả màn** |
| **Làm gì** | thư viện (§6.2) + tham số | 7 hiệu ứng | thêm vài hiệu ứng nguyên tố |
| **Trễ** (bắt đầu sau … giây) | số | không có (mọi hiệu ứng trong một chỗ cắm chạy **đồng thời**) | **có** — nhiều hiệu ứng khác trễ = **chuỗi** |
Quy tắc tương thích: trễ mặc định `0` → game cũ không đổi. Hiệu ứng ghi đè theo slot/Item giữ nguyên cơ chế "trigger nào có nội dung thì thay chung".

### 6.2 Thư viện hiệu ứng (nhóm theo việc làm)
| Nhóm | Có sẵn (v2) | Cần thêm |
|---|---|---|
| Tại chỗ | Phóng-co (`Punch`) · Rung (`Shake`) · Nhịp thở (`Breathing`) | Nảy · Nhấp nháy · Đổi màu |
| Hiện / Ẩn | Mờ dần (`FadeOut`) | Hiện · Thu nhỏ biến mất · **Lật úp/mở** · **Lần lượt trên nhóm** (hiện / nháy / **đánh số 1…n** từng phần tử theo thứ tự, có âm mỗi cái; xong thì giữ · úp · ẩn) |
| Di chuyển | Bay tới điểm (`FlyTo`) · Bay tới ô & ở lại (`FlyToStay`) · Bay đi (`FlyOff`) | **Di chuyển tới** [điểm / Item / ô] (không biến mất, dùng được cho Item) · **Rơi** · **Nhảy cung** |
| Trang trí / Âm | ✔/✖ mặc định · âm riêng · âm mặc định | Pháo giấy · Nói (đọc chữ) |
**Đích của "Di chuyển tới"** là *điểm kéo trên canvas* (như `FlyTo`) **hoặc vị trí của một Item** (kể cả Item vô hình dùng làm điểm đánh dấu) — để khi cảnh đổi theo round (VD khe sông rộng hẹp) một hiệu ứng chung vẫn đúng.
**Mẫu ghép dựng sẵn**: một chuỗi hiệu ứng lưu lại có tên ("Rơi xuống nước", "Bắc cầu", "Bay vào rổ") — cô chèn một lần rồi chỉnh điểm đến.

### 6.3 Ví dụ: Dài–Ngắn (`CauBacQua`) — hiệu ứng nhiều đối tượng, nối tiếp
Hiện chạy bằng chuỗi `on.right / on.wrong` viết trong dữ liệu flow (đích `@tapped`, `@actor`, `#carrot`). Cùng kịch bản diễn đạt bằng mô hình mới (số giây là **minh hoạ**):

**Chỗ cắm "Đúng hết"** (chọn thanh gỗ dài đủ qua sông)
| Trễ | Lên ai | Làm gì |
|---|---|---|
| 0 | Thanh gỗ vừa chạm | Di chuyển tới [Cầu] · 0.5s |
| 0 | — | Âm "đúng" (mặc định) |
| 0.5 | — | Âm "cộp" |
| 0.5 | Thỏ (Item) | Nảy · 0.6s |
| 1.1 | Thỏ (Item) | Di chuyển tới [Cà rốt] · 1.0s |
| 2.1 | Cà rốt (Item) | Thu nhỏ biến mất · 0.3s |
| 2.1 | Thỏ (Item) | Pháo giấy |

**Chỗ cắm "Sai"** (thanh gỗ ngắn)
| Trễ | Lên ai | Làm gì |
|---|---|---|
| 0 | Thanh gỗ vừa chạm | Di chuyển tới [Mặt sông] · 0.5s |
| 0.5 | — | Âm "tõm" |
| 0.5 | Thanh gỗ vừa chạm | Di chuyển tới [Đáy sông] · 0.5s + Mờ dần |

Cần có để dựng được: **Nền theo round** (bờ + sông có khe khác nhau) và **Item theo round** (Thỏ, Cà rốt, điểm "Cầu"/"Mặt sông"/"Đáy sông" đặt theo từng khe), **Trễ/Chuỗi**, **Di chuyển tới**, Item được chạy hiệu ứng di chuyển. Thanh gỗ dài/ngắn là đáp án nút bố cục tự đặt với hai kích thước khác nhau — **không cần kiểu hiển thị mới**.

### 6.4 Hiệu ứng ≠ cơ chế hiển thị (cần phân biệt)
| | Hiệu ứng | Cơ chế (của Kiểu hiển thị) |
|---|---|---|
| Khi chạy | **một lần**, khi có sự kiện | **liên tục**, theo dữ liệu |
| Phụ thuộc | chỗ cắm + đích + trễ cô chọn | **giá trị** của đáp án |
| Ví dụ | gỗ bay → rơi sông; thẻ úp lại; ting | cân nghiêng theo cân nặng; kim theo giờ |
| Cô điền ở | tab **Hiệu ứng** | tab **Đáp án** (chọn kiểu hiển thị + cột Giá trị) |
Cầu nối: kiểu hiển thị có thể có **hiệu ứng vào round tích hợp** (cân lắc rồi nghiêng) chỉ bật/tắt + tốc độ.

## 7. Vòng đời một round (ổ cắm tác động ở đâu)
```
Dựng round ─▶ [Vào round] ─▶ Bé chạm ─▶ [Vừa chạm] ─▶ Kiểu chấm ─▶ kết quả ─▶ [chỗ cắm hiệu ứng tương ứng]
   │              │                                       (theo đơn vị chấm)                   │
   │   hiệu ứng vào (hiện rồi úp,                                                          chạy chuỗi
   │   cân lắc→nghiêng)                                                                    trên các đích
   └ Nền(+theo round) · Item(+theo round) · Câu hỏi · Đáp án (Kiểu hiển thị, trạng thái ban đầu) · Vùng thu thập
                                                                  └▶ Điểm ─▶ khoá/ẩn ─▶ chờ cả 2 đội? ─▶ "Next in Ns" ─▶ round kế / hết giờ
```
Ổ cắm *Kiểu hiển thị* tác động ở **Dựng round**, *Kiểu chấm* ở **sau khi chạm**, *Hiệu ứng* ở **các chỗ cắm** (vào round · vừa chạm · kết quả chấm · chờ). Mọi thứ khác (điểm, chờ 2 đội, đếm ngược, âm câu hỏi lặp 4s) là luật chung v2 — không đổi.

## 8. Phủ game: tổng quát hoá về mô hình
Ký hiệu: ✔ = v2 đủ · ✘ = cần thêm mục ổ cắm / thuộc tính.

| Game (họ) | Nền / Item | Câu hỏi | Hiển thị | Chấm | Hiệu ứng nổi bật | Cần thêm |
|---|---|---|---|---|---|---|
| Quiz ảnh/chữ/số, nghe & tìm (CuaHangKem, ChuCai, SoDem, SoSanh…) | ✔ | chữ/ảnh/số/icon/âm (`audioOnly`) | nút | Chọn 1 | Phóng-co · Rung · Mờ | — |
| Chọn nhiều / thu thập (DemQua, GhepAnh) | ✔ | âm + ảnh | nút/ngẫu nhiên + vùng thu thập | Chọn nhiều | Bay tới ô & ở lại | — |
| Cộng đủ số (TachSo2) | ✔ | số | nút + thanh | Cộng đủ số | bay vào thanh | — |
| Đáp án trôi (SoSanhSo) | ✔ | số | trôi | Chọn 1 | | — |
| Theo thứ tự (PlanetOrder, SentenceBuilder, CongHinh) | ✔ | chữ/ảnh | nút + vùng thu thập (dải) | Theo thứ tự | | — |
| **Lật thẻ giống nhau** (+ Nối cặp) | ✔ | không / xem trước | ma trận, trạng thái **Úp** | **Ghép cặp** | *Lật mở · Úp lại · ting+biến mất* | Chấm Ghép cặp · cột Cặp · trạng thái Úp · chỗ cắm cặp · hiệu ứng Lật |
| **Nhớ chuỗi hình** | ✔ | hình **hiện lần lượt rồi úp** | nút, đáp án **Ẩn-chờ** tới khi xem xong | Theo thứ tự | *Hiện lần lượt rồi úp* | chỗ cắm Vào round · trạng thái Ẩn-chờ · hiệu ứng Lần lượt trên nhóm |
| **Nặng–Nhẹ** | ✔ | âm | **Cân** (Giá trị) | Chọn 1 | cân nghiêng (cơ chế) + ting | Kiểu hiển thị Cân · cột Giá trị |
| **Dài–Ngắn** | **Nền + Item theo round** | âm | nút (2 kích thước) | Chọn 1 | **chuỗi** gỗ→cầu→thỏ→cà rốt | Nền/Item theo round · Trễ · Di chuyển tới · Item chạy hiệu ứng |
| Ngày của bé | **Nền theo round** (buổi) | — | nút/thẻ | Chọn 1 | nhân vật phản ứng | Nền/Item theo round |
| Vỡ bóng bay · Trứng màu · Bé khoẻ mạnh | ✔ (Item phản ứng) | âm/hình | nút ngẫu nhiên | Chọn 1 / nhiều | vỡ, nở, mừng | — |
| Sàn kỳ diệu | ✔ | — | **Vùng chạm tự do** | **Không chấm** | hiệu ứng tại điểm chạm | Hiển thị Vùng chạm · Chấm "không chấm" · đích "điểm chạm" |
| **Đếm khối hộp** | ✔ | icon × N + âm | nút số | Chọn 1 (thử lại) | **Lần lượt đánh số** lên câu hỏi (khi đúng; khi sai từ lần 2) | Hiệu ứng Lần lượt · chỗ cắm Sai từ lần n · (tuỳ) cơ chế Đống khối 3D — chi tiết §11 |
| Đồng hồ khổng lồ | ✔ | hình/đồng hồ | nút số | Chọn 1 | | (tuỳ) cơ chế kim |
| Ngoài builder | WhoIsIt · LaneDash · SaveTheAstronaut · SolarSystem · FRTest | | | | | giữ game code |

**Con số rút ra**: sau khi rà, toàn bộ khác biệt nằm ở **2 kiểu hiển thị mới** (Cân, Vùng chạm), **2 kiểu chấm mới** (Ghép cặp, Không chấm), **3 thuộc tính/trạng thái** (cột Cặp, cột Giá trị, trạng thái Úp / Ẩn-chờ), **cảnh theo round** (Nền + Item), và **mô hình hiệu ứng mở rộng** (chỗ cắm Vào round / Vừa chạm / kết quả cặp / Sai từ lần n; đích; trễ; ~6 hiệu ứng nguyên tố: Di chuyển tới, Rơi, Lật, **Lần lượt trên nhóm**, Nảy, Pháo giấy). Không cần thêm loại đối tượng nào.

## 9. Màn hình soạn của cô giáo (bám theo đối tượng, như v2)
`Cài đặt` · `Nền & Item` (có nút "chỉ round này") · `Câu hỏi` (chung/riêng, cách xuất hiện) · `Đáp án` (**Kiểu hiển thị** dạng thư viện hình · **Cách chấm** lọc theo kiểu hiển thị · bảng đáp án với cột hiện theo lựa chọn · trạng thái ban đầu) · `Hiệu ứng` (mỗi **chỗ cắm** là một danh sách: *Lên ai · Làm gì · Trễ*; có "Mẫu ghép") · `Vùng thu thập` · danh sách round. Chơi thử hai nửa ngay trên web.

## 10. Khoảng trống hiện tại (từ chính tài liệu v2)
- `answerDisplayTypes` chỉ có `button`; `Matching`/`Floating` của Kit **chưa** vào engine tổng quát (SCHEMA "Giới hạn đã biết").
- Hiệu ứng trong một chỗ cắm chạy **đồng thời**, đích ngầm là chủ sở hữu; Item **bỏ qua** `FlyTo/FlyToStay/FlyOff`.
- Chưa có chỗ cắm **Vào round**, **Vừa chạm**; slot câu hỏi chỉ dùng `onIdle`.
- `answerMode`, bố cục, hiển thị là **cấp game**; game xen nhiều dạng (CauBacQua xen 2 flow) cần đặt cấp round.
- Nền và Item là **cấp game**; cảnh đổi theo round (Dài–Ngắn, Ngày của bé) chưa có.
- Thứ tự round **luôn xáo ngẫu nhiên** (SCHEMA "Round tự xáo trộn"): chưa có nhóm độ khó / tăng dần (Đếm khối gốc bắt đầu 1–3 rồi tăng tới 10).
- Chưa có gợi ý theo số lần sai và chưa có hiệu ứng tuần tự trên một nhóm (đếm 1, 2, 3…).

## 11. Ví dụ dựng từ đầu: **Đếm khối hộp**
**Game gốc** (FloorStory `DemKhoiHop`, `fs/games/DemKhoiHop.json` + view `stack` trong `fs_flow.js`): hiện một cụm N khối (N tăng dần từ 1–3 tới 10), bé chạm số đúng trong 3 số; sai thì thử lại; **sai từ lần thứ 2 → các khối được đếm lần lượt 1, 2, 3… (số nằm ngoài hình, có gạch chỉ vào từng khối) và số đúng nhấp nháy**; khi đúng cũng đếm lại rồi pháo giấy.

### 11.1 Thao tác của cô giáo (tên tab/nút thật của builder v4.1)
| # | Tab / nút | Thao tác | Đối tượng · ổ cắm | Lần | Có sẵn |
|---|---|---|---|---|---|
| 0 | File → **Game mới** | chọn game mẫu quiz gần nhất rồi sửa | — | 1 | ✔ |
| 1 | **Cài đặt chung** | mã game · **Chế độ đội** = Độc lập · **Kiểu đáp án** = Chọn 1 · **Sai thì** = cho thử lại · **Số cột icon** = 3 | Cài đặt · *Kiểu chấm* | 5 | ✔ |
| 2 | Cài đặt chung → *Nền chung* | chọn ảnh nền; (tuỳ) ảnh ô đáp án | Nền | 1–2 | ✔ |
| 3 | Chèn → **Nhân vật / Item** | (tuỳ) thêm nhân vật cổ vũ: chọn ảnh, đặt vị trí, gắn phản ứng đúng/sai | Item | 3–5 | ✔ |
| 4 | Trang chủ → *Vùng soạn* **❓ Câu hỏi** (round 1) | chọn ảnh **Icon** = khối hộp · gõ **số lượng** (vd 5) · chỉnh khung · chèn âm "Có bao nhiêu khối?" | Câu hỏi (icon × N) | 4 | ✔ |
| 5 | *Vùng soạn* **✅ Đáp án** | *Sắp xếp ô* = Ma trận (3 cột) · *Hình dạng ô* · Chèn → **Ô đáp án** cho đủ 3 · gõ 3 số · **tick ✓** số đúng · (tuỳ) âm đọc số | Đáp án · *Kiểu hiển thị* = nút/thẻ | 7–10 | ✔ |
| 6 | **Hiệu ứng** | giữ mặc định, hoặc: Đúng hết = Phóng-co + ✔ · Sai = Rung + ✖ | *Hiệu ứng* | 0–4 | ✔ |
| 7 | Chèn → **Thêm round** (Ctrl+D) ×9 | nhân bản round 1; ở mỗi bản: sửa số khối, sửa 3 đáp án, đổi dấu ✓ | Round | 9 × ~6 | ✔ (nhập tay) |
| 8 | Chơi thử → File → **Xuất gói game (.zip)** → import Unity | | — | 3 | ✔ |
**Tổng ≈ 80 thao tác**, khoảng 70% là bước 7 (gõ số ở 10 round). Icon mặc định của nhóm câu hỏi và hiệu ứng đặt ở mức game nên nhân bản round không phải chọn lại.

### 11.2 Dựng được tới đâu
**Làm được bằng cái có sẵn**: nền, nhân vật, câu hỏi = N khối (lưới phẳng), 3 đáp án số, chọn 1, thử lại, hiệu ứng đúng/sai, âm, 10 round, 2 đội.

**Game gốc làm được nhưng kiến trúc hiện tại chưa dựng được**:
| Thiếu | Ổ cắm | Cần thêm |
|---|---|---|
| **Đếm lần lượt 1, 2, 3…** (khi sai từ lần 2 và khi đúng) — cốt lõi sư phạm | Hiệu ứng | hiệu ứng **Lần lượt trên nhóm** (đánh số từng icon theo thứ tự + âm mỗi cái) lên đích *câu hỏi*; chỗ cắm **Sai từ lần thứ n** |
| Số đúng **nhấp nháy** sau lần sai thứ 2 | Hiệu ứng | đích *các đáp án đúng* + Nhịp thở ở cùng chỗ cắm |
| **Cụm khối 3D ngẫu nhiên**, sát nhau, mỗi lượt khác | Kiểu hiển thị (cơ chế) | "Đống khối" (cột Giá trị = N) — tuỳ chọn; thay thế: icon lưới phẳng |
| **Độ khó tăng dần** (1–3 → 10) | Round | thứ tự round theo nhóm độ khó (v2 xáo ngẫu nhiên hoàn toàn) |
| Sinh 10 round tự động | — | chưa có (kiến trúc không có bộ sinh round) |

### 11.3 Thêm thao tác khi các mục trên có đủ (đặt một lần ở mức game, không theo round)
| # | Tab | Thao tác |
|---|---|---|
| 6b | Hiệu ứng → chỗ cắm **Sai từ lần thứ 2** | thêm [Lên ai = **Câu hỏi** · Làm gì = **Lần lượt: đánh số** + âm mỗi cái] · thêm [Lên ai = **Các đáp án đúng** · Nhịp thở] |
| 6c | Hiệu ứng → chỗ cắm **Đúng hết** | thêm [Câu hỏi · Lần lượt: đánh số] rồi [Pháo giấy · trễ sau khi đếm xong] |
→ +6–8 thao tác. *Lần lượt trên nhóm* cũng là hiệu ứng dùng cho **Nhớ chuỗi** (hiện lần lượt rồi úp): cùng một mục thư viện, khác tham số (việc làm với mỗi phần tử: hiện / nháy / đánh số; kết thúc: giữ / úp / ẩn).

## 12. Chưa kiểm chứng
- Cách gõ tay 10 round ở bước 7 và các nhãn nút ở §11.1 lấy từ `game_builder_v4.1.html` (ribbon: File · Cài đặt chung · Trang chủ · Chèn · Hiệu ứng · Xem); chưa chạy thử trên trình duyệt.
- Thời lượng/độ chặn (blocking) chính xác của từng bước trong `on.right/on.wrong` của `CauBacQua` (bảng §6.3 là minh hoạ).
- Mức độ tái dùng `ButtonDisplay` cho "thẻ úp + ghép cặp" ở Unity (cần đọc `AnswerValidator.cs`, `ButtonDisplay.cs`).
- Các họ game viết tay (Tổng hợp CSV, Nghe-tìm, Phép tính, Quiz Kit) mới đọc ở mức field/ghi chú đầu class (xem generalization §9).
