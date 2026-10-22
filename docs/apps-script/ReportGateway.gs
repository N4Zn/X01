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
 *  3. appsscript.json: "oauthScopes": ["https://www.googleapis.com/auth/drive.readonly"] và "webapp": { "executeAs": "USER_DEPLOYING", "access": "ANYONE_ANONYMOUS" }.
 *  4. Chạy authorize() một lần → cấp quyền. Deploy → New deployment → Web app → Execute as: Me; Who has access: Anyone.
 */

var ROOT_NAME = 'EduXplore_Logs';
var DATA_MARK = '/*EDUX_DATA*/null';

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

/** Chạy 1 lần để cấp quyền đọc Drive. */
function authorize() { reportDir_(); }
