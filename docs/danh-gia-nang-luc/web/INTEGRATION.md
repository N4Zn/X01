# Web báo cáo kết quả học tập (dashboard cho quản lý, PC)

> **TL;DR**: Widget JS thuần (không thư viện) `edux-report.js` + `edux-report.css`, đọc 1 file `report.json`, vẽ 3 tab: **Theo khối · Theo lớp · Từng học sinh**. Dùng chuột/bàn phím trên PC, bố cục vừa 1 màn hình (danh sách dài cuộn trong khung, trang không cuộn). Nhúng vào web đang chạy bằng 1 thẻ `<div>` + 2 file tĩnh, hoặc bằng `<iframe>`.
> **Đọc khi**: nhúng báo cáo vào web khác, đổi dữ liệu/ schema, chỉnh giao diện báo cáo.
> **Cập nhật**: 2026-10-16. Đã xem trên Chrome 1440×900 với dữ liệu minh hoạ (308 bé, 12 lớp) và trước đó với dữ liệu thật 08–09/10; chưa xem trên màn hình quản lý thật.

## Thư mục

| File | Là gì |
|---|---|
| `index.html` | Trang chạy độc lập (kiosk): tải `data/report.json`, không có thì `data/sample.json`. Cần máy chủ tĩnh (`python -m http.server 8080` trong thư mục này). |
| `edux-report.js` / `edux-report.css` | Widget. CSS nằm dưới `.edx`, không ảnh hưởng trang chủ. |
| `data/sample.json` | Dữ liệu **minh hoạ** (308 bé giả, khối 1–2, 12 lớp 1A1–2A6, 20–30 bé/lớp). Sinh bằng `tools/make_sample.py`. |
| `data/report.json` | Dữ liệu thật, **không commit** (có tên bé; `.gitignore`). Sinh bằng `tools/build_report_json.py`. |
| `tools/bundle.py` | Gói thành 1 file HTML tự chứa `dist/bao-cao-standalone.html` (mở bằng `file://`, gửi qua mail). |
| `logo.png` | Logo EduXplore (bản sao của `tool/logo.png`). |

## Giao diện

- **Theo khối**: 3 thẻ (Toàn trường · Khối 1 · Khối 2) chọn phạm vi, hàng KPI, rồi **so sánh các lớp bằng 7 kiểu** (nút "Kiểu xem"):
  1. **Cột**: 1 cột/lớp, chọn chỉ số (điểm TB, % đạt mục tiêu, % cần hỗ trợ, hoặc điểm TB một học phần), tuỳ chọn xếp giảm dần, vạch TB phạm vi.
  2. **Bản đồ nhiệt**: lớp × học phần, màu theo mức.
  3. **Phân bố mức**: thanh chồng 100% Đạt/Mức 3/Mức 2/Mức 1 mỗi lớp, xếp theo % đạt.
  4. **Biên độ điểm**: box plot điểm từng bé trong lớp (min, Q1, trung vị, Q3, max + từng chấm), cho thấy lớp đều hay phân hoá.
  5. **Radar**: chồng tối đa 6 lớp (mặc định 3 cao nhất + 3 thấp nhất) so với TB phạm vi.
  6. **Chính xác × tốc độ**: mỗi lớp 1 bong bóng (cỡ = số bé).
  7. **Bảng**: sắp xếp theo mọi cột.
  Click lớp ở bất kỳ kiểu nào để mở tab Theo lớp. Cột phải: phân bố mức, điểm TB học phần (so với toàn trường), danh sách cần chú ý.
- **Theo lớp**: chọn lớp (12 chip), bảng xếp hạng bé (sắp xếp theo cột, dòng TB lớp), đồ thị chính xác × tốc độ từng bé, phân bố mức, điểm TB học phần so với khối.
- **Từng học sinh**: danh sách lớp + ô **tìm tên/mã toàn trường**, hồ sơ theo học phần, nhận định riêng từng học phần, hạng, bản đồ kỹ năng.
- Địa chỉ `#khoi/all`, `#khoi/1`, `#lop/2/2A5`, `#hs/CASA-19050` mở thẳng 1 màn hình (`hash:true`).

## Dữ liệu

```
log game → pipeline (tool/pipeline/run.py) → out.json → tools/build_report_json.py → data/report.json → web
```

```bash
cd docs/danh-gia-nang-luc/web
python -I -X utf8 tools/build_report_json.py \
  --out-json <.../BaoCao/2026-10-08_2026-10-09/out.json> --from 2026-10-08 --to 2026-10-09 \
  --out data/report.json [--lop-map lop.json]
```

Nhãn nhận định (`diag`), công thức, roster lấy từ pipeline (`edux_lib.DIAG`, `config.json`) — web **không** giữ bản sao riêng.

Schema `report.json` (version 1):

```jsonc
{
  "version": 1,
  "sample": false,                              // true -> hiện nhãn "Dữ liệu minh hoạ"
  "ky": { "from": "2026-10-08", "to": "2026-10-09", "label": "08/10/2026 → 09/10/2026" },
  "formula": { "cuts": [25,50,75], "low_sample": 10, "few_total": 15, "base": 80, "kmax": 1.25, "tfast": 5, "tslow": 20 },
  "diag": { "good": { "label": "Thành thạo", "advice": "…", "color": "#1f7a52" }, "…": {} },
  "mon": [ { "id": "Toán", "hp": ["Nhận biết số","Đếm","Cộng","Trừ","So sánh"] } ],   // thêm môn = thêm phần tử; ≥2 môn tự hiện nút chọn môn
  "students": [{
    "code": "CASA-21001", "name": "Ngô Quốc An", "alias": "Quốc An",
    "khoi": "5 tuổi", "lop": "Lá 1",           // khối > lớp; bé chưa chơi: "mon": {}
    "mon": { "Toán": { "score": 99.4, "n": 32, "hp": {
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
    onSelectStudent: function (s) { console.log('chọn', s.code); }
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

- Pipeline mới chấm **môn Toán** (5 học phần). Schema đã sẵn nhiều môn; thêm môn là thêm vào `mon[]` và `students[].mon`.
- Chưa có xu hướng theo thời gian (chỉ 1 kỳ), chưa có chuẩn theo tuổi.
- Dữ liệu minh hoạ dùng 5 học phần Toán cho cả khối 1–2; khi có đề theo khối, sửa `mon[].hp` trong `report.json`.
- Chưa kiểm tra trên 1920×1080 thật; chưa xuất PDF/Excel từ web (dùng pipeline).
