# Web báo cáo kết quả học tập (dashboard cho quản lý, PC)

> **TL;DR**: Widget JS thuần (không thư viện) `edux-report.js` + `edux-report.css`, đọc 1 file `report.json`, vẽ 3 tab: **Theo khối · Theo lớp · Từng học sinh**. Dùng chuột/bàn phím trên PC, bố cục vừa 1 màn hình (danh sách dài cuộn trong khung, trang không cuộn). Nhúng vào web đang chạy bằng 1 thẻ `<div>` + 2 file tĩnh, hoặc bằng `<iframe>`.
> **Đọc khi**: nhúng báo cáo vào web khác, đổi dữ liệu/ schema, chỉnh giao diện báo cáo.
> **Cập nhật**: 2026-10-16. Đã xem trên Chrome 1440×900 với dữ liệu minh hoạ (308 bé, 12 lớp) và trước đó với dữ liệu thật 08–09/10; chưa xem trên màn hình quản lý thật.

## Thư mục

| File | Là gì |
|---|---|
| `index.html` | Trang chạy độc lập (kiosk): tải `data/report.json`, không có thì `data/sample.json`. Cần máy chủ tĩnh (`python -m http.server 8080` trong thư mục này). |
| `edux-report.js` / `edux-report.css` | Widget. CSS nằm dưới `.edx`, không ảnh hưởng trang chủ. |
| `data/sample.json` | Dữ liệu **minh hoạ** (296 bé giả, 3 môn, 6 tuần, khối 1–2, 12 lớp 1A1–2A6). Sinh bằng `tools/make_sample.py`. |
| `data/report.json` | Dữ liệu thật, **không commit** (có tên bé; `.gitignore`). Sinh bằng `tools/build_report_json.py`. |
| `tools/bundle.py` | Gói thành 1 file HTML tự chứa `dist/bao-cao-standalone.html` (mở bằng `file://`, gửi qua mail). |
| `logo.png` | Logo EduXplore (bản sao của `tool/logo.png`). |

## Giao diện

- **Nguyên tắc**: 2 khối học chương trình khác nhau → **không có số liệu nào gộp hay so sánh giữa các khối** (không "TB toàn trường"). Xếp hạng/so sánh chỉ giữa các lớp trong cùng 1 khối. Học phần của mỗi môn có thể khác nhau theo khối (`mon[].hp` dạng `{ "1": [...], "2": [...] }`).
- **Môn**: nút "Tất cả môn" (điểm TB các môn bé đã chơi; biểu đồ/bảng lấy *môn* làm chiều thay cho học phần) hoặc từng môn (chiều = học phần của môn đó). Chỉ 1 môn trong dữ liệu thì tự ẩn nút.
- **Toàn trường**: mỗi khối 1 bảng riêng (cạnh nhau): lớp × (Tất cả môn · từng môn), ô có hạng trong khối, ★ + viền = đứng đầu khối ở cột đó; cột tiến bộ; cột *mức độ chơi* (đều/ít/chưa chơi) + câu/bé; dòng TB khối; dải "Đứng đầu khối" (VD: Toán ★1A1, Tiếng Việt ★1A2); chơi nhiều/ít nhất, số bé chưa chơi (lớp nhiều nhất), số bé điểm thấp. Click tiêu đề cột để xếp lại, click lớp mở tab Theo lớp.
- **Theo khối** (chọn 1 khối): KPI (đã chơi, điểm TB, tiến bộ, tham gia tuần, chưa chơi/chơi ít, cần hỗ trợ), rồi **so sánh các lớp bằng 11 kiểu**, 4 nhóm:
  - *Kết quả*: **Cột** (chỉ số: điểm TB, % đạt, % cần hỗ trợ, % đã chơi, % chơi tuần gần nhất, hoặc 1 học phần/môn) · **Bản đồ nhiệt** · **Phân bố mức** · **Biên độ điểm** · **Radar** · **Chính xác × tốc độ**.
  - *Tiến bộ* (≥ 2 tuần): **Xu hướng tuần** · **Mức tiến bộ**.
  - *Tham gia*: **Mức độ chơi** (mỗi lớp: chơi đều / chơi ít / chưa chơi + câu/bé) · **Theo tuần** (lớp × tuần).
  - *Chi tiết*: **Bảng**.
  Cột phải: phân bố mức, điểm TB từng học phần/môn, "Cần chú ý" (chơi nhiều/ít nhất, lớp có nhiều bé chưa chơi, điểm cao/thấp nhất, tiến bộ, bé giảm điểm/bỏ chơi).
- **Theo lớp**: KPI (đã chơi/chưa chơi, điểm TB vs khối, **hạng trong khối**, tiến bộ, tham gia tuần), bảng xếp hạng bé (điểm, Δ so tuần đầu, sparkline, điểm từng học phần/môn, cờ chú ý; dòng TB lớp), danh sách **bé chưa chơi** dưới bảng, khung biểu đồ đổi được (Xu hướng tuần lớp vs khối · Chính xác × tốc độ · Học phần/Môn), "Cần chú ý": chưa chơi, chơi ít, điểm thấp, tiến bộ nhất, đi xuống, bỏ chơi ≥ 2 tuần (tên bé click mở hồ sơ).
- **Từng học sinh** (kế thừa phiếu InBody `mau/tien-bo-toan-a4.html`): danh sách lớp gọn (tên + điểm + mũi tên; bé chưa chơi ghi rõ), tìm tên/mã toàn trường, lọc *Chưa chơi / Giảm điểm / Ít chơi / Mức 1–2*; hồ sơ 3 cột — (1) phân tích kỹ năng (hoặc theo môn khi "Tất cả môn"), so tuần đầu, sparkline + nhận định từng học phần; (2) thanh điểm so TB lớp + lịch sử theo tuần (điểm, chính xác, thời gian, số câu); (3) điểm EduXplore + Δ, bản đồ kỹ năng, mục tiêu, nhận xét. Bé trước/sau để duyệt nhanh.
- Chưa có lịch sử (≤ 1 tuần): các kiểu/cột tiến bộ tự ẩn hoặc hiện "–", phần còn lại vẫn dùng bình thường.
- Quy ước tiến bộ: Δ = điểm tuần có chơi gần nhất − điểm tuần có chơi đầu tiên của từng bé (ghép cặp); ▲ khi ≥ +3, ▼ khi ≤ −3; cảnh báo "giảm" khi tụt ≥ 5 điểm so tuần trước; "ít chơi" = không chơi từ 2 tuần.
- Yếu tố quản lý cần xem và dữ liệu tương ứng: [quan-ly-can-xem.md](quan-ly-can-xem.md).
- Địa chỉ `#tq`, `#khoi/1`, `#lop/2/2A5`, `#hs/CASA-19050` mở thẳng 1 màn hình (`hash:true`).

## Dữ liệu

```
log game → pipeline (tool/pipeline/run.py) → out.json → tools/build_report_json.py → data/report.json → web
```

```bash
cd docs/danh-gia-nang-luc/web
# A) từ log -> CÓ lịch sử theo tuần (khuyên dùng)
python -I -X utf8 tools/build_report_json.py --local <GameLogs> --from 2026-09-07 --to 2026-10-16 --out data/report.json
python -I -X utf8 tools/build_report_json.py --csv [URL|file.csv] --from ... --to ...      # Google Sheet (mặc định URL trong config.json)
# B) từ out.json của 1 kỳ -> không có lịch sử
python -I -X utf8 tools/build_report_json.py --out-json <.../out.json> --from 2026-10-08 --to 2026-10-09 --out data/report.json
# tuỳ chọn: --lop-map lop.json  --mon Toán  --config <config.json>
```

Chế độ A chấm cả kỳ bằng công thức chuẩn, rồi chấm lại **từng tuần ISO (thứ Hai–Chủ nhật)** bằng đúng công thức đó → `weeks[]` và `mon[].hist[]`. Cần ≥ 2 tuần có dữ liệu thì mới thấy xu hướng; khoảng `--from/--to` càng dài càng nhiều tuần. Nhãn nhận định (`diag`), công thức, roster lấy từ pipeline (`edux_lib.DIAG`, `config.json`) — web **không** giữ bản sao riêng. Dữ liệu minh hoạ: `python -I -X utf8 tools/make_sample.py` (6 tuần, 12 lớp).

Schema `report.json` (version 2; version 1 — không có `weeks`/`hist` — vẫn đọc được):

```jsonc
{
  "version": 2,
  "weeks": [ { "id": "2026-W37", "from": "2026-09-07", "to": "2026-09-13", "label": "T37", "range": "07–13/09" } ],   // theo thứ tự thời gian
  "sample": false,                              // true -> hiện nhãn "Dữ liệu minh hoạ"
  "ky": { "from": "2026-10-08", "to": "2026-10-09", "label": "08/10/2026 → 09/10/2026" },
  "formula": { "cuts": [25,50,75], "low_sample": 10, "few_total": 15, "base": 80, "kmax": 1.25, "tfast": 5, "tslow": 20 },
  "diag": { "good": { "label": "Thành thạo", "advice": "…", "color": "#1f7a52" }, "…": {} },
  "mon": [ { "id": "Toán", "hp": ["Nhận biết số","Đếm","Cộng","Trừ","So sánh"] },        // hp: danh sách chung, hoặc {"1":[…],"2":[…]} nếu các khối học khác nhau
           { "id": "Tiếng Việt", "hp": { "1": ["Ghép vần"], "2": ["Chính tả"] } } ],     // thêm môn = thêm phần tử; ≥2 môn tự hiện nút "Tất cả môn"
  "students": [{
    "code": "CASA-21001", "name": "Ngô Quốc An", "alias": "Quốc An",
    "khoi": "5 tuổi", "lop": "Lá 1",           // khối > lớp; bé chưa chơi: "mon": {}
    "mon": { "Toán": { "score": 99.4, "n": 32,
      "hist": [ { "score": 91.2, "n": 18, "acc": 94.4, "t": 3.9, "days": 3, "hp": { "Đếm": 95.0 } }, null ],   // đúng thứ tự `weeks`; null = tuần đó bé không chơi
      "hp": {
        "Đếm": { "n": 12, "ok": 12, "acc": 100, "t": 3.37, "k": 1.25, "pt": 100, "low": false,
                 "diag": "good", "tmed": 3.15, "few": false } } } }
  }]
}
```

**Khối / lớp**: roster hiện chỉ có trường `class` ("4 tuổi", "5 tuổi") → coi là **khối**, lớp = cùng tên. Khi có tên lớp thật ("Lá 1"…), truyền `--lop-map {"CASA-21001":"Lá 1",…}` hoặc sửa roster; web tự nhóm.

## Cách tích hợp vào web đang chạy

Chọn 1 trong 3, từ ít việc đến nhiều việc.

### A. `<iframe>` (nhanh nhất, cách ly hoàn toàn CSS/JS)

1. Copy cả thư mục `web/` lên máy chủ, ví dụ `https://site.vn/baocao/`.
2. Đặt `data/report.json` ở đó (xem mục Cập nhật dữ liệu).
3. Trong trang đang chạy:

```html
<iframe src="/baocao/index.html" style="width:100%;height:100vh;border:0" title="Báo cáo kết quả học tập"></iframe>
```

Mở thẳng 1 màn hình: `/baocao/index.html#lop/5%20tuổi/Lá%201`, `#hs/CASA-21001`, `#khoi/5%20tuổi`. Nhược điểm: 2 lần đăng nhập nếu báo cáo cần quyền (iframe cùng domain thì dùng chung cookie).

### B. Nhúng widget vào 1 phần tử của trang (khuyên dùng)

```html
<link rel="stylesheet" href="/baocao/edux-report.css">
<div id="bao-cao" style="min-height:100vh"></div>
<script src="/baocao/edux-report.js"></script>
<script>
  var rp = EduxReport.mount('#bao-cao', {
    dataUrl: '/api/bao-cao',          // hoặc data: {…} nếu trang đã có JSON trong tay
    logo: '/baocao/logo.png',
    theme: 'light',                   // 'light' | 'dark' | 'auto' (theo hệ điều hành)
    hash: false,                      // true: đồng bộ tab/lớp/bé lên #hash (trang chỉ có 1 báo cáo)
    height: null,                     // mặc định cao đúng 1 màn hình (100vh); đặt '720px' nếu khung nhúng nhỏ hơn
    mon: 'Toán',                      // môn mở đầu
    onSelectStudent: function (s) { console.log('chọn', s.code); },
    onRange: function (r) { return fetch('/api/bao-cao?from=' + r.from + '&to=' + r.to).then(function (x) { return x.json(); }); }   // tuỳ chọn: bật ô chọn khoảng thời gian (cần report.bounds {min,max}); trả dữ liệu mới
  });
  // rp.update(newData)  rp.go({tab:'hs', code:'CASA-21001'})  rp.destroy()
</script>
```

Hoặc không viết JS: `<div data-edux-report data-src="/api/bao-cao" data-logo="/baocao/logo.png"></div>` (tự gắn khi trang load).

- **Không đụng CSS trang chủ**: mọi luật dưới `.edx`; biến màu `--lv0…` chỉ gắn trên `.edx`. Trang chủ có `* {box-sizing…}` hay `button {…}` riêng thì kiểm tra lại nút (widget tự đặt `font`, `color` cho `button`).
- **Font**: `index.html` nạp Google Fonts (IBM Plex Sans, Source Serif 4). Máy chiếu offline thì tự có fallback (`Segoe UI`, Georgia); muốn đúng font thì tải font về và `@font-face` trong trang chủ.
- **React / Vue / Angular**: gắn trong `useEffect` / `onMounted` / `ngAfterViewInit`, lưu giá trị trả về, gọi `destroy()` khi huỷ:
  ```js
  useEffect(() => { const w = EduxReport.mount(ref.current, { dataUrl }); return () => w.destroy(); }, [dataUrl]);
  ```
- **CSP**: cần cho phép `script-src` + `style-src` từ nơi đặt 2 file, `connect-src` tới `dataUrl`, và `fonts.googleapis.com` / `fonts.gstatic.com` nếu dùng font Google.

### C. Trang/route riêng trong ứng dụng

Giống B nhưng đặt vào 1 route (`/bao-cao`), để widget chiếm toàn màn hình (`height` mặc định 100vh), `hash:true`. Menu chính của trang chỉ cần link tới route đó.

> **Từ 2026-10-22**: báo cáo cho giáo viên chạy tự động trên Google (Drive → `Aggregate.gs` → `report.json` → `ReportGateway.gs` → Google Sites), xem `docs/sheets-report.md` mục "Báo cáo tự động cho giáo viên". Các cách dưới đây vẫn dùng được cho web khác.

## Cấp dữ liệu cho web (chọn 1)

1. **File tĩnh** (đơn giản): job chạy pipeline → `build_report_json.py` → ghi vào `data/report.json` trên máy chủ. Hiện `tool/pipeline/chay.bat` chạy tay; có thể đặt Task Scheduler chạy cuối buổi. `fetch` đã tắt cache (`no-store`) nên cập nhật là thấy ngay khi tải lại.
2. **API** của web đang chạy: endpoint trả đúng schema ở trên (`Content-Type: application/json`). Nếu API khác domain: bật CORS cho domain trang. Phân quyền theo lớp (giáo viên chỉ thấy lớp mình) làm ở API: chỉ trả `students` của lớp được phép, widget tự bỏ tab/khối trống.
3. **Dán thẳng** vào trang: `EduxReport.mount(el, { data: <object> })`.

**Bảo mật**: dữ liệu có tên và kết quả của trẻ. Đừng đặt `report.json` ở thư mục public không cần đăng nhập; không commit `data/report.json` (đã `.gitignore`); `sample.json` là dữ liệu giả nên an toàn.

## Hiển thị

- Từ 1000px rộng: cao đúng 1 màn hình, danh sách dài (bảng 30 bé, danh sách học sinh) cuộn **trong khung**; hẹp hơn: xếp 1 cột và cuộn trang (điện thoại vẫn xem được).
- Cỡ chữ gốc `clamp(12px, 0.95vw, 20px)` → 1440px ≈ 13,7px, 1920px ≈ 18px. Chỉnh trong `edux-report.css` (`.edx { font: … }`).
- Biểu đồ vẽ theo đúng kích thước khung nên co giãn theo cửa sổ; rê chuột vào cột/chấm để xem số chi tiết.
- Nền tối/sáng: nút "Nền tối" ở góc phải, nhớ bằng `localStorage`; `theme:'auto'` theo hệ điều hành.
- Nếu sau này chiếu lên máy chiếu sàn (chỉ tap, không cuộn): cần bản bố cục riêng với nút to và phân trang; chưa làm.

## Giới hạn hiện tại

- Pipeline mới chấm **môn Toán**; web và schema đã hỗ trợ nhiều môn (thêm vào `mon[]` và `students[].mon`, mỗi môn có `score`, `n`, `hp`, `hist`). Khi có thêm môn, chạy `build_report_json.py` cho từng môn rồi gộp `students[].mon`.
- Chưa có xu hướng theo thời gian (chỉ 1 kỳ), chưa có chuẩn theo tuổi.
- Dữ liệu minh hoạ có 3 môn (Toán, Tiếng Việt, Khám phá) với học phần khác nhau theo khối; tên môn/học phần trong sample là giả định để xem giao diện.
- Chưa kiểm tra trên 1920×1080 thật; chưa xuất PDF/Excel từ web (dùng pipeline).
