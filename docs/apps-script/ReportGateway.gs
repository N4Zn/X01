/**
 * EduXplore — CỔNG XEM BÁO CÁO (dự án Apps Script RIÊNG, nhỏ, chỉ đọc).
 *
 * Cập nhật: 2026-10-22. KHÔNG cùng dự án với DriveUpload.gs/Aggregate.gs: link công khai của cổng này chỉ đọc được
 * 2 file trong EduXplore_Logs/_report/ (report.json + dashboard.html), không có đường nào tới log thô hay ghi/xoá.
 *
 * Link xem:  https://script.google.com/macros/s/<ID>/exec?t=<VIEW_TOKEN>
 * Nhúng Google Sites: Nhúng → Theo URL → dán link trên. Đổi VIEW_TOKEN = thu hồi link cũ.
 *
 * CÀI ĐẶT (1 lần):
 *  1. script.google.com → Dự án mới → dán file này.
 *  2. Project Settings → Script properties → thêm VIEW_TOKEN = chuỗi dài ngẫu nhiên (≥ 20 ký tự, khác SECRET).
 *     Chọn khoảng thời gian (từ/đến ngày bất kỳ): thêm 2 Script properties
 *       AGG_URL = URL /exec của dự án tổng hợp (X01_GameLog)   AGG_KEY = SECRET của dự án đó
 *     → getRange() gọi ?action=report&from&to ở dự án đó (tính chính xác theo công thức, không nhân đôi code tính điểm).
 *  3. appsscript.json: "oauthScopes": ["https://www.googleapis.com/auth/drive.readonly", "https://www.googleapis.com/auth/script.external_request"]
 *     và "webapp": { "executeAs": "USER_DEPLOYING", "access": "ANYONE_ANONYMOUS" }.
 *  4. Chạy authorize() một lần → cấp quyền. Deploy → New deployment → Web app → Execute as: Me; Who has access: Anyone.
 *     Đổi code sau này: Deploy → Manage deployments → bút chì → New version.
 */

var ROOT_NAME = 'EduXplore_Logs';
var DATA_MARK = '/*EDUX_DATA*/null';
var TOKEN_MARK = '/*EDUX_TOKEN*/""';

function doGet(e) {
  var t = (e && e.parameter && e.parameter.t) || '';
  var tok = PropertiesService.getScriptProperties().getProperty('VIEW_TOKEN');
  if (!tok || t !== tok) return HtmlService.createHtmlOutput('Không có quyền truy cập.').setTitle('EduXplore');
  try {
    var dir = reportDir_();
    var html = readText_(dir, 'dashboard.html');
    var json = readText_(dir, 'report.json');
    if (html.indexOf(DATA_MARK) < 0) throw new Error('dashboard.html thiếu chỗ đặt dữ liệu');
    var data = json.replace(/<\//g, '<\\/');            // không để dữ liệu tự đóng thẻ <script>
    html = html.split(DATA_MARK).join(data);             // split/join: không bị ký tự đặc biệt của replace()
    html = html.split(TOKEN_MARK).join(JSON.stringify(t)); // trang dùng token này để gọi getRange()
    return HtmlService.createHtmlOutput(html)
      .setTitle('Báo cáo kết quả học tập · EduXplore')
      .addMetaTag('viewport', 'width=device-width, initial-scale=1')
      .setXFrameOptionsMode(HtmlService.XFrameOptionsMode.ALLOWALL);   // cho phép nhúng iframe (Google Sites)
  } catch (err) {
    return HtmlService.createHtmlOutput('Chưa có báo cáo (' + String(err.message || err) + ').').setTitle('EduXplore');
  }
}

function reportDir_() {
  var r = DriveApp.getFoldersByName(ROOT_NAME);
  if (!r.hasNext()) throw new Error('thiếu thư mục ' + ROOT_NAME);
  var d = r.next().getFoldersByName('_report');
  if (!d.hasNext()) throw new Error('thiếu thư mục _report');
  return d.next();
}

function readText_(dir, name) {
  var it = dir.getFilesByName(name);
  if (!it.hasNext()) throw new Error('thiếu ' + name);
  return it.next().getBlob().getDataAsString('UTF-8');
}

/**
 * Trang gọi (google.script.run) để xem báo cáo của khoảng ngày bất kỳ. Trả chuỗi JSON.
 * Phải có đúng VIEW_TOKEN; chỉ chuyển tiếp 2 ngày (yyyy-MM-dd) tới dự án tổng hợp, không nhận tham số nào khác.
 */
function getRange(t, from, to) {
  var tok = PropertiesService.getScriptProperties().getProperty('VIEW_TOKEN');
  if (!tok || t !== tok) throw new Error('Không có quyền truy cập');
  var ok = /^\d{4}-\d{2}-\d{2}$/;
  if (!ok.test(from) || !ok.test(to)) throw new Error('Ngày không hợp lệ');
  var props = PropertiesService.getScriptProperties();
  var url = props.getProperty('AGG_URL'), key = props.getProperty('AGG_KEY');
  if (!url || !key) throw new Error('Chưa cấu hình AGG_URL / AGG_KEY');
  var q = url + (url.indexOf('?') < 0 ? '?' : '&') + 'action=report&from=' + from + '&to=' + to + '&key=' + encodeURIComponent(key);
  var r = UrlFetchApp.fetch(q, { muteHttpExceptions: true, followRedirects: true });
  var body = r.getContentText();
  if (r.getResponseCode() !== 200) throw new Error('Máy chủ tổng hợp trả lỗi ' + r.getResponseCode());
  var j;
  try { j = JSON.parse(body); } catch (e) { throw new Error('Phản hồi không hợp lệ'); }
  if (j.ok === false) throw new Error(j.error || 'Lỗi tính báo cáo');
  return body;
}

/** Chạy 1 lần để cấp quyền đọc Drive (và gọi ngoài). */
function authorize() { reportDir_(); UrlFetchApp.fetch('https://www.google.com', { muteHttpExceptions: true }); }
