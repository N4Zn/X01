# Pipeline chấm điểm từ log thật (chạy 2026-10-08)

Script Python (cần `openpyxl`; chạy `python -I -X utf8`) — đường dẫn đang gắn cứng theo máy dev, sửa đầu file trước khi chạy:

1. `score.py` — đọc `Downloads/GameLogs/2026-10-08_*.json` (log `entries` của `PlayerRecognitionService`) → `rows.json`. Mỗi file = 1 phiên, 2 ô (trái/phải) = 2 bé; bé của ô = tên nhận diện nhiều round nhất, round chưa nhận diện (Player_x, Blue_1, Red_1) gán cho bé đó; `OVERRIDE` để ép tay (vd `161210_Counting` ô phải = Cát Tường). Bỏ phiên trước 15:46 (thử nghiệm), phiên < 5 câu, DemQua, ChuCaiThuong (thi đấu).
2. `res.py` — danh sách lớp + mã `CASA-21001..17` (alphabet theo tên rồi họ đệm; Minh Anh 4 tuổi = `CASA-22001`) → điểm học phần/môn → `out.json`. Học phần của TongHopToan tách theo tiền tố `THT_xx_<Kind>`: Recognize=Nhận biết số, CountEasy/Count=Đếm, Add=Cộng, Sub=Trừ, Sign=So sánh. Câu < 0,5s không tính vào tốc độ; không cắt trần thời gian.
3. `xl.py` → `KetQua_Toan_<ngày>.xlsx` (Tổng hợp / Chi tiết học phần / Thi đấu & loại trừ / Chưa có điểm).
4. `gen.py` → phiếu A4 mỗi bé (`rep/CASA-*.html` + `TatCa.html`), in PDF bằng `chrome --headless=new --no-pdf-header-footer --print-to-pdf`. Cần `logo.png` cạnh file HTML.

Quy tắc loại dữ liệu và các bất thường đã gặp: xem README cha, mục "Chạy thật 08/10".
