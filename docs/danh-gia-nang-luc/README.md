# Đánh giá năng lực học sinh từ log game

> **TL;DR**: Báo cáo năng lực kiểu InBody (1 trang A4) dựng từ log raw của game. Học sinh định danh `CASA-YYNNN`; chỉ game chơi độc lập được tính điểm; điểm học phần = `min(100, 80 × %đúng × hệ số tốc độ)`; môn = TB học phần; TỔNG = TB môn. Tool web dựng báo cáo nằm ở `tool/`.
> **Đọc khi**: sửa công thức điểm, thêm trường vào log phục vụ báo cáo, làm DB theo học sinh, chỉnh/dựng lại tool báo cáo.
> **Cập nhật**: 2026-10-09 @ d7e0ea11 (pipeline đọc Sheet/log local, nhận định theo học phần, trang tổng hợp lớp: xem `tool/pipeline/README.md`; tool web vẫn chạy trên dữ liệu mẫu)

## Thư mục này

| Đường dẫn | Là gì |
|---|---|
| `tool/bao-cao-nang-luc.html` | Tool web 1 file: import JSON log → báo cáo A4. Mở thẳng bằng trình duyệt (cần `logo.png` cạnh nó). |
| `tool/tests/test3.js`, `test4.js` | Test Node (stub DOM): công thức điểm; nhiều môn, TỔNG, tắt môn/học phần. Chạy: `node tool/tests/test3.js` |
| `mau/hoso-nang-luc-toan.html` | Mẫu InBody-style đầu tiên (dữ liệu giả, môn Toán). |
| `mau/tien-bo-toan-a4.html` | Mẫu 1 trang A4 cho phụ huynh, tiến bộ theo tháng (dữ liệu giả T5–T10). |

Bản đã publish (private, claude.ai): https://claude.ai/artifact/BCxGukyahbminV6tQyUDxr — bản gốc là file trong `tool/`.

## Luồng dữ liệu

```
log raw (game)  →  gán học sinh  →  lọc game độc lập  →  điểm học phần  →  môn  →  TỔNG  →  báo cáo
```

Tool nhận các nguồn sau (kéo thả/dán JSON, tự nhận dạng):

- **Session JSON của `GameLogger`**: `gameName`, `playerLeft/Right`, `sessionDate/Time`, `rounds[{round, questionId, topic, questionType, answerMode, playerLeft/RightRecognized, isCorrect, winnerName, responseTimeSeconds, clicks[]}]`. Round độc lập có `clicks` rỗng, `winnerName = isCorrect ? playerName : ""`.
- **Event Google Sheets (`SheetsSyncManager`)**: `round_end`, `recognition`, `session_end` (xem `docs/sheets-report.md`).
- **Log nhận diện (`PlayerRecognitionService`)**: `entries` với `eventType` = `recognition`/`round`.
- **`class_rounds.jsonl` của ControlActivity** (`ScoreStore`): `{t,s,name,game,phan,r,qid,q,a,c,ok,sec}`.
- **`roster.json`** (FaceEnroll > Quản lý lớp): `{version, classes:[{name, students:[{name, alias, gender, birthdate}]}]}`.
- **`game_registry.json`** (ControlUI): môn và học phần.

Lưu ý khi import: `roster.json` và `game_registry.json` **thay thế toàn bộ** danh sách hiện có, không gộp.

## Quyết định thiết kế (đã chốt)

### 1. Định danh học sinh

`CASA-` + 2 số cuối năm sinh + STT 3 chữ số theo thứ tự ghi danh **trong cùng năm sinh**. Ví dụ lớp 5 tuổi, người ghi danh đầu tiên → `CASA-21001`. Thứ tự lấy từ thứ tự trong `roster.json` (hoặc `enrolledAt` nếu có). Roster không có năm sinh thì suy từ tên lớp "N tuổi". Mã trường (`CASA`) chỉnh được trong tool.

### 2. Gán kết quả round cho học sinh (từ nhận diện khuôn mặt)

Round được gán cho **sự kiện nhận diện gần nhất trước nó** (cùng game, cùng slot/bên). Nhận diện thất bại → round để "chưa gán" và **không tính điểm**. Tên chung (`Player 1`, `Blue_1`, `Red_1`, `?`) không phải học sinh.

### 3. Môn và học phần: theo ControlUI

Lấy từ `game_registry.json`: môn = `categoryNames`; học phần = `group` của game nếu có, không thì tên category. Game tổng hợp (`TestTongHop`/`TongHop`) ưu tiên map theo `topic` của câu hỏi chứ không theo tên game. Game chưa có trong registry: gán tay ở mục 7 của tool.

### 4. Chỉ tính game chơi độc lập

Game **2 người** (round có `clicks`, hoặc không có `side`) là thi đấu vui: lưu/hiển thị riêng ("Thi đấu vui", tỉ lệ thắng), **không vào điểm**. Mỗi game có thể ép chế độ Auto / Độc lập / 2 người (mục 7).

### 5. Công thức điểm

Mỗi học phần, trong khoảng thời gian báo cáo, lấy tối đa **50 câu gần nhất** của học sinh:

```
Điểm học phần = min(100,  80 × (đúng ÷ đã chơi) × hệ số tốc độ)
```

Hệ số tốc độ theo thời gian trả lời trung bình `t` (mặc định chỉ tính **câu đúng**):

- `t ≤ 5s` → ×1,25 · `t ≥ 20s` → ×1 · giữa 5–20s giảm tuyến tính: `k = 1 + 0,25 × (20 − t) ÷ 15`.
- Mốc: 8s ×1,20 · 10s ×1,17 · 12,5s ×1,125 · 15s ×1,08 · 18s ×1,03.

Điểm môn = trung bình điểm các học phần **đã chơi** (chưa chơi = bỏ qua, không phải 0). **TỔNG** = trung bình điểm các môn đã chọn.

**Mức** theo điểm (không phải theo % đúng): `< 25` Mức 1 · `25–50` Mức 2 · `50–75` Mức 3 · `≥ 75` Đạt mục tiêu. Đúng 100% nhưng chậm = 80 → vẫn đạt.

Mọi tham số chỉnh được trong mục 5 của tool: điểm gốc 80, hệ số tối đa 1,25, hai ngưỡng 5s/20s, cửa sổ 50 câu, ba mốc mức, "tốc độ tính trên mọi câu".

**Vì sao nhân, không cộng**: bản đầu dùng `75% đúng + 25% tốc độ` (cộng). Với cộng, người đúng ít câu nhưng rất nhanh có thể vượt người đúng nhiều câu nhưng chậm (ví dụ 23 điểm < 26 điểm dù đúng nhiều hơn). Nhân thì tốc độ chỉ *thưởng thêm* cho độ đúng, không bù được cho việc sai. Ví dụ: đúng 25% & rất nhanh = 25; đúng 25% & chậm = 20; đúng 100% & chậm = 80; đúng 100% & ≤5s = 100.

### 6. Cấu trúc báo cáo

- 1 trang A4, tỉ lệ chữ/hình ≈ 50/50, logo EduXplore thật, **không** có tab/chip (là bản in), **không** có mục "Chăm chỉ".
- **Bước 1 — môn**: tick các môn đưa vào báo cáo. Từ 2 môn trở lên tự có thêm ô **TỔNG** (kèm điểm từng môn); 1 môn thì ô điểm là "Điểm môn X".
- **Bước 2 — học phần**: mỗi môn tick riêng các học phần. Học phần bị tắt bị loại khỏi *mọi* phép tính (điểm, TỔNG, lịch sử, so sánh).
- Mỗi môn một khối riêng (bảng/thanh/văn bản học phần + dòng "Điểm môn"), mạng nhện riêng cho môn có ≥3 học phần.
- Các mục bật/tắt: thông tin HS, điểm, mục tiêu, phân tích, mạng nhện, lịch sử, thi đấu vui, nhận xét, log gốc; cột bảng; chỉ số lịch sử.
- Thời gian tổng hợp: tất cả/30/90/180 ngày/tuỳ chọn; gom theo tháng/tuần/ngày/phiên; so với kỳ trước (hoặc kỳ đầu nếu chọn "tất cả"); TB nhóm.

## Việc còn mở / cần làm

1. **Lệch với ControlActivity**: `ScoreStore.java` (điểm hiện trên máy) vẫn chỉ tính `đúng ÷ đã chơi`, **chưa có tốc độ**. Muốn khớp với báo cáo thì phải sửa `ScoreStore.phanScores`/`scorePct` (+ nơi hiển thị) theo công thức mục 5. Hiện hai nơi cho hai con số khác nhau.
2. **Định danh log**: đã thêm `deviceId`, `sessionId`, `rid` vào mọi dòng Sheet và đẩy đủ game độc lập qua `LogRound` (2026-10-09, xem `docs/sheets-report.md` mục "Khớp log online – local") — **chưa compile/test trên K02, chưa deploy Apps Script mới**. Vẫn chưa có `studentId` và cờ chế độ chơi (độc lập/2 người) trong log; pipeline dùng danh sách `scored_games`.
3. **DB theo học sinh**: pipeline `tool/pipeline/` đọc thẳng Google Sheet, gán học sinh, gộp dồn vào `db/<mã>.jsonl` (lưu theo ID). Còn lại: tool web `bao-cao-nang-luc.html` chưa đọc DB này (vẫn import file); phiếu A4 từ pipeline chưa có xu hướng nhiều tháng.
4. **Độ khó**: chưa dùng, coi mỗi game cùng một độ khó. Làm sau.
5. **Chuẩn theo lứa tuổi**: chưa có; tạm dùng ngưỡng cố định 25/50/75 trên điểm.
6. **Chưa làm trong tool**: biểu đồ lịch sử chỉ vẽ TỔNG (chưa vẽ riêng từng môn); "Mục tiêu" và "Nhận xét" gộp học phần của mọi môn.
7. **Chưa kiểm chứng**: toàn bộ kiểm thử bằng dữ liệu mẫu + stub DOM; chưa xem trực quan (độ vừa 1 trang A4, in ấn) và chưa chạy với log thật.

## Chạy thật 08/10/2026 (log K02 → Excel + phiếu A4)

Đã chạy công thức trên log thật của K02 (33 file `2026-10-08_*.json`) bằng script ở `tool/pipeline/`. Ghi lại để chạy lại khi có thêm log:

- **Cấu trúc log mới**: mỗi file = 1 phiên, 2 ô chơi độc lập (mỗi ô 1 bé). Tên bé của ô = tên nhận diện nhiều round nhất (nhận diện nhảy giữa chừng là bình thường); round chưa nhận diện (`Player_1/2`, `Blue_1`, `Red_1`) gán cho bé của ô. Ca mơ hồ → user chốt (vd `161210_Counting` ô phải: Trí 14 / Tường 9 → chốt **Tường**, vì phiên sau ghép Tường–Linh).
- **Loại**: log thử nghiệm (trước 15:46, bấm bừa 0,1s, tên Blue_1/Red_1); file 0 byte; phiên bỏ dở < 5 câu; file session trùng (`161209_Counting.json` trùng `161210`); **DemQua** (chỉ test 1 loại quả, mọi lần chạm đều đúng ⇒ luôn 100%); **ChuCaiThuong** (2 bé cùng câu, thi đấu, bé thua không bấm thì không có dòng log, thử lại ghi nhiều dòng cùng round).
- **Đồng hồ K02 nhảy**: máy mở lên với ngày `2025-04-14 16:24`, đến khi đồng bộ thì nhảy về giờ thật giữa phiên (file `2025-04-14_*` và giờ giật lùi). Log `2025-04-14_*` trong backup K02 (`Documents/K02_backup/EduXplore_2026-10-06`) có dữ liệu các bé nhưng: 2 log cùng phiên (session JSON vs entries) cho tên khác nhau, buổi đông bé đứng quanh → **chưa đưa vào điểm** (kết quả cũng gần như không khác).
- **Học phần**: TongHopToan tách theo tiền tố câu (xem `pipeline/README.md`); AddNumber5Digit=Cộng; Counting/Counting5=Đếm. Câu "dạng chưa quen" (`THT_13_Sub` đúng 63%, `1 + ? = 3`) **không phải lỗi đề** (user xác nhận) — vẫn tính. Thời gian câu dài là thật, không cắt trần.
- **Ít mẫu**: học phần < 10 câu đánh `*`; bé < 15 câu tổng ghi "ÍT DỮ LIỆU"; học phần chưa chơi ghi "chưa có điểm" (không tính 0).
- **Mã HS**: roster thật nằm trên K02 `/sdcard/EduXplore/roster.json` (bản backup 06/10 có 17 bé lớp 5 tuổi + lớp Dev, có `alias`). `CASA-21001..21017` xếp theo tên (chữ cuối) rồi họ đệm — **không** theo thứ tự ghi danh. Đặng Minh Anh (4 tuổi, alias Ỉn, add 06–07/10) = `CASA-22001`.
- **Chưa có điểm** (log 08/10): Doãn Minh Trí, Nguyễn Hòa Vũ, Đặng Minh Anh.
- **Từ 2026-10-09** cùng kỳ này chạy lại được trực tiếp từ Google Sheet bằng `tool/pipeline/run.py` (xem `tool/pipeline/README.md`; đối chiếu Sheet–local ở đó). Kết quả đầu ra bản chạy từ file local (ngoài repo): `Downloads/GameLogs/KetQua_Toan_20261008.xlsx`, `Downloads/GameLogs/BaoCao_HS_20261008/*.pdf` (15 phiếu + file gộp). Phiếu một-ngày không có xu hướng tháng/chuẩn tuổi/TB lớp.
