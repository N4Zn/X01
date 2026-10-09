# Pipeline chấm điểm: Google Sheet → DB theo học sinh → Excel + phiếu A4

> **Cập nhật**: 2026-10-09 @ 3801ba44. Đọc log **trực tiếp từ Google Sheet** (không cần kéo file log về). Python 3.9+, chỉ cần `openpyxl` cho Excel.

## Từ log local (tên bé chính xác nhất) + gộp nhiều ngày

```bash
python -I -X utf8 run.py --local C:/.../GameLogs --from 2026-10-08 --to 2026-10-09 --per-day --out C:/.../BaoCao
```
Ra `<out>/<từ>_<đến>/` (báo cáo gộp cả kỳ, Excel có thêm sheet "Theo ngày") và `<out>/<ngày>/` cho từng ngày có dữ liệu. Mỗi thư mục: Excel + `BaoCao_TatCa_*.pdf` + `pdf/<mã>_<tên>.pdf`. Chỉ đọc file `YYYY-MM-DD_HHMMSS_<Game>.json` (bỏ file 0 byte, file `skip_files`, ô `slot_override` trong `config.json`; bé của ô = tên nhận diện nhiều nhất trong phiên). Chạy lại 08/10 từ log local cho đúng số lần chấm đầu tiên (ví dụ Quốc An 99,8).

## Nhận định theo từng học phần (không gộp) + trang tổng hợp lớp

- Mỗi (bé, học phần) được nhận định riêng (`edux_lib.diagnose`, ngưỡng ở `config.json` mục `diag`): trung vị thời gian mọi câu + % đúng → *Thành thạo / Khá chắc / Biết nhưng chậm / **Nhanh nhưng hay nhầm** (vội, đoán) / **Chậm và sai: chưa nắm** (chưa học) / Chưa vững / Chưa đủ dữ liệu*. Nhanh-sai và chậm-sai là hai chuyện khác nhau, không được gộp thành "kém và chậm". Phiếu từng bé có mục "Nhận định từng học phần".
- Trang tổng hợp lớp (`class_page.py`, trang đầu của `BaoCao_TatCa_*.pdf` và file `TongHopLop_*.pdf`, ảnh `anh/00_TongHopLop.png`): đồ thị chính xác x tốc độ, mỗi chấm = 1 bé x 1 học phần, chỉ ghi tên chấm cần chú ý; phân bố mức; điểm TB học phần; bảng xếp hạng. `mode='kid'` trong `class_page.page` cho bản 1 chấm/bé (gộp học phần).
- Ảnh PNG từng bé (`anh/<mã>_<tên>.png`, 1588x2246) thay cho PDF lẻ. Nếu PDF cũ đang mở trong trình xem, tool ghi thêm file `*_moi.pdf` thay vì ghi đè.
- Tên thường gọi đổi trong `config.json` (`alias`; `also` = tên cũ vẫn nhận diện trong log, vd Nghé, trước là É).

## Dùng hằng ngày (link Sheet cũ, không cần sửa Apps Script)

Bấm đúp **`chay.bat`** (báo cáo hôm nay) hoặc `chay.bat 2026-10-09` (ngày khác). Tool tải CSV từ link Sheet trong `config.json`, lọc trùng/rác, gán bé, chấm điểm, ra `ketqua\<ngày>\`: `KetQua_Toán_<ngày>.xlsx`, `BaoCao_TatCa_<ngày>.pdf` (mỗi bé 1 trang A4), `pdf\<mã>_<tên>.pdf` từng bé. Cần Python 3 + `pip install openpyxl` + Chrome/Edge (in PDF). ~45 giây.

Đọc thông báo cuối khi chạy: **⚠ tên ngoài lớp ≥5 round** (bé mới chưa có trong `roster` → thêm vào `config.json`), **⚠ dòng sai đồng hồ** (K02 chưa đồng bộ giờ lúc mở máy → các dòng đó bị bỏ), "Loại khi tính điểm".
Với app/Sheet cũ: game `AddNumber5Digit`/`ChuCaiThuong` không có trên Sheet (không tính được điểm Cộng từ game đó); `Counting`/`Counting5` gán bé theo nhận diện gần nhất (~79% khớp).

## Chạy

```bash
cd docs/danh-gia-nang-luc/tool/pipeline
python -I -X utf8 run.py --from 2026-10-08 --to 2026-10-08          # tải CSV từ Sheet (URL trong config.json)
python -I -X utf8 run.py --csv sheet.csv --from 2026-10-01 --to 2026-10-31   # hoặc từ file CSV đã tải
python -I -X utf8 compare_local.py --local <thư mục GameLogs> --date 2026-10-08 --after 15:46:00   # online có khớp local?
```

Kết quả trong `ketqua/` (Excel `KetQua_Toán_<kỳ>.xlsx`, `phieu/<mã>.html` + `TatCa.html`, `out.json`). In PDF: `chrome --headless=new --no-pdf-header-footer --print-to-pdf=<pdf> file:///.../phieu/TatCa.html`.

Sheet phải để chế độ "ai có link đều xem được" thì tải CSV không cần đăng nhập (hiện đang vậy; Sheet chứa tên bé nên cân nhắc khoá lại và tải bằng file).

## Các bước (`edux_lib.py`)

1. **Tải + chuẩn hoá**: số thập phân dấu phẩy (`3,79`), `TRUE/FALSE`. Hai dạng dòng `round_end`: *mới* (MiniGameKit / `LogRound`: có `slot`, `team`, `correct`) và *cũ* (`GameLogger`: có `side`, `isCorrect`).
2. **Loại trùng**: theo `rid` (log mới) hoặc theo nội dung y hệt (log cũ). Trên Sheet cũ ~80% dòng trùng do app gửi lại khi mạng chập chờn.
3. **Loại rác**: dòng có đồng hồ máy sai năm (`2025-04-14`), `TEST-DIAG`; tên ngoài danh sách lớp (người thử như `nam`, `Vinh`...).
4. **Gán học sinh**: dòng dạng mới → tin tên trong dòng (đối chiếu 08/10: khớp log local 300/300). Dòng dạng cũ hoặc tên chung (`Player_1`, `Blue_1`, `Red_1`...) → nhận diện thành công gần nhất (trước hoặc sau round, trong `recog_max_gap_sec`) của đúng ô đó. Không có → bỏ ("chưa gán"). Tên có thể là họ tên đầy đủ hoặc tên thường gọi.
5. **DB theo học sinh** (`db/<mã>.jsonl`, không đưa lên git): gộp dồn theo khoá `rid`/hash, chạy lại không nhân đôi → giữ lịch sử khi Sheet bị dọn. `--no-db` để chấm thẳng từ lần tải này.
6. **Chấm điểm** (`score`): game trong `scored_games`, loại `exclude_ranges` (phiên thử), loại phiên bỏ dở (<5 câu của 1 bé); công thức `min(100, 80 × %đúng × hệ số tốc độ)` trên 50 câu gần nhất (xem README cha mục 5); tốc độ tính trên câu đúng, bỏ câu <0,5s.

## `config.json`

Danh sách lớp + mã (`roster`), tên chung (`generic_names`), game tính điểm (`scored_games`), game→học phần (`game_hp`, TongHopToan theo tiền tố câu `THT_xx_<Kind>` ở `tht_hp`), khoảng giờ loại, tham số công thức, URL Sheet. **Sửa ở đây, không sửa code.** `name_priority`: `schema` (mặc định) / `app` / `recognition`.

## Kết quả đối chiếu 08/10/2026 (Sheet vs log local)

| Game | Dòng chung | Chỉ Sheet | Chỉ local | Tên khác |
|---|---|---|---|---|
| TongHopToan | 300 | 2 | 4 | **0** |
| Counting5 | 100 | 0 | 0 | 100 (tên GameLogger cũ/sai) |
| Counting | 52 | 0 | 0 | 52 (như trên) |
| AddNumber5Digit | 0 | 0 | 29 | — (chưa đẩy lên Sheet) |
| ChuCaiThuong | 0 | 0 | 57 | — (thi đấu, chưa đẩy lên Sheet) |

- **Số round khớp** với log local ở mọi game đã đẩy lên Sheet. `Counting`/`Counting5` tên khác vì app cũ ghi tên lúc vào ván → pipeline dùng nhận diện gần nhất thay thế (≈ 79% khớp bản chấm từ log local); app đã sửa (xem `docs/sheets-report.md`, mục "Khớp log online – local") nên log mới sẽ khớp 100%.
- Chênh điểm so với lần chấm từ file local: các bé có điểm Cộng từ `AddNumber5Digit` (Bảo Châu, Dư Thanh Trà) vì game đó chưa lên Sheet; vài câu đầu ván/Đếm gán lệch do nhận diện nhầm (log cũ không có `owner`).

## Chưa làm

- Log cũ (trước 08/10) chưa kiểm tra chất lượng; lọc theo `--from` để tránh.
- Độ khó, chuẩn theo tuổi, nhiều môn (hiện chỉ môn Toán), xu hướng nhiều tháng trên phiếu.
- Roster đang nằm trong `config.json`; nên nạp từ `roster.json` thật của K02.
