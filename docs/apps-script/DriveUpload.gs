/**
 * EduXplore — nhận MỖI PHIÊN CHƠI MỘT FILE từ K02 và lưu vào Google Drive (thay cơ chế ghi từng dòng vào Sheet).
 *
 * Cập nhật: 2026-10-20. Khớp với Assets/Game/Scripts/UIScripts/Common/SheetsSyncManager.cs.
 *
 * Giao thức (app → script):
 *   POST <webAppUrl>?device=<deviceId>&name=<yyyy-MM-dd_HHmmss_Game.json>[&key=<secret>]
 *   Body = nguyên nội dung file GameLogs (text/plain, UTF-8). Cùng `name` gửi lại = GHI ĐÈ (idempotent).
 *   Trả:  {"ok":true,"path":"K02-01/2026-10-20/….json","bytes":N}   (N = số byte UTF-8 của body; app so với số byte đã gửi)
 *         {"ok":false,"error":"…"}
 *
 * Đọc ra (pipeline / kiểm tra):
 *   GET <webAppUrl>                                  → "EduXplore Drive upload — OK"
 *   GET <webAppUrl>?action=list[&since=yyyy-MM-dd][&device=ID][&key=…]  → {"ok":true,"files":[{"path","bytes","updated"}…]}
 *   GET <webAppUrl>?action=get&path=<device/day/name.json>[&key=…]      → nội dung file (text)
 *
 *   POST <webAppUrl>?action=put&name=config.json|dashboard.html&key=… (body = nội dung) → _report/<name>  (xem Aggregate.gs, ReportGateway.gs)
 *
 * Cấu trúc Drive:  <ROOT_NAME>/<deviceId>/<yyyy-MM-dd>/<name>.json   (ngày lấy từ tên file)
 *
 * CÀI ĐẶT (làm 1 lần):
 *   1. script.google.com → dự án mới (hoặc dự án cũ — thay hết code bằng file này).
 *   2. (Tuỳ chọn) Project Settings → Script properties → thêm SECRET = chuỗi bí mật; điền cùng chuỗi vào
 *      "secret" trong /sdcard/EduXplore/sheets_sync_config.json trên K02 và vào config pipeline.
 *   3. Deploy → New deployment → Web app → Execute as: Me; Who has access: Anyone → Deploy → cấp quyền Drive.
 *      Dự án cũ: Deploy → Manage deployments → Edit (bút chì) → New version → Deploy  (URL /exec giữ nguyên).
 *   4. Chép URL /exec vào "webAppUrl" của sheets_sync_config.json. GỬI SCRIPT NÀY LÊN TRƯỚC KHI CÀI BẢN APP MỚI.
 *   5. Kiểm tra: mở URL /exec trên trình duyệt → thấy "EduXplore Drive upload — OK".
 *
 * Dữ liệu có TÊN TRẺ EM: để thư mục gốc ở chế độ Riêng tư (mặc định); chỉ chia sẻ cho người cần. `Anyone` ở trên chỉ là
 * quyền gọi script, không công khai thư mục Drive.
 */

var ROOT_NAME = 'EduXplore_Logs';
var MAX_BODY = 20 * 1024 * 1024;

function doGet(e) {
  var p = (e && e.parameter) || {};
  if (!p.action) return text_('EduXplore Drive upload — OK');
  if (!authorized_(p)) return json_({ ok: false, error: 'unauthorized' });
  try {
    if (p.action === 'list') return json_({ ok: true, files: listFiles_(p.device || '', p.since || '') });
    if (p.action === 'get') return text_(readFile_(p.path || ''));
    return json_({ ok: false, error: 'unknown action' });
  } catch (err) {
    return json_({ ok: false, error: String(err) });
  }
}

function doPost(e) {
  var lock = LockService.getScriptLock();
  try {
    var p = (e && e.parameter) || {};
    if (!authorized_(p)) return json_({ ok: false, error: 'unauthorized' });
    var name = safe_(p.name || '');
    var device = safe_(p.device || 'unknown');
    var body = (e && e.postData && e.postData.contents) || '';
    if (!body) return json_({ ok: false, error: 'empty body' });
    var bytes = Utilities.newBlob(body).getBytes().length;
    if (bytes > MAX_BODY) return json_({ ok: false, error: 'too large' });

    // ?action=put: đẩy file cấu hình/trang báo cáo vào _report (chỉ 2 tên cho phép; xem Aggregate.gs, ReportGateway.gs)
    if (p.action === 'put') {
      if (name !== 'config.json' && name !== 'dashboard.html') return json_({ ok: false, error: 'name not allowed' });
      lock.waitLock(20000);
      writeText_(sub_(root_(), '_report'), name, body);
      return json_({ ok: true, path: '_report/' + name, bytes: bytes });
    }

    if (!/\.json$/i.test(name)) return json_({ ok: false, error: 'bad name' });

    var m = /^(\d{4}-\d{2}-\d{2})_/.exec(name);
    var day = m ? m[1] : 'unknown';

    lock.waitLock(20000);   // tránh 2 request cùng tạo thư mục trùng tên
    var dayFolder = sub_(sub_(root_(), device), day);
    var it = dayFolder.getFilesByName(name);
    if (it.hasNext()) {
      var f = it.next();
      f.setContent(body);
      while (it.hasNext()) it.next().setTrashed(true);   // dọn bản trùng nếu có
    } else {
      dayFolder.createFile(name, body, MimeType.PLAIN_TEXT);
    }
    return json_({ ok: true, path: device + '/' + day + '/' + name, bytes: bytes });
  } catch (err) {
    return json_({ ok: false, error: String(err) });
  } finally {
    try { lock.releaseLock(); } catch (x) {}
  }
}

// ── helpers ──────────────────────────────────────────────────────────────────

function authorized_(p) {
  var secret = PropertiesService.getScriptProperties().getProperty('SECRET');
  return !secret || p.key === secret;
}

function safe_(s) {
  return String(s).replace(/[\\\/:*?"<>|\r\n\t]/g, '_').substring(0, 120);
}

function root_() { return sub_(DriveApp.getRootFolder(), ROOT_NAME); }

function sub_(parent, name) {
  var it = parent.getFoldersByName(name);
  return it.hasNext() ? it.next() : parent.createFolder(name);
}

function listFiles_(device, since) {
  var out = [];
  var devs = root_().getFolders();
  while (devs.hasNext()) {
    var d = devs.next();
    if (device && d.getName() !== device) continue;
    var days = d.getFolders();
    while (days.hasNext()) {
      var day = days.next();
      if (since && day.getName() < since) continue;
      var files = day.getFiles();
      while (files.hasNext()) {
        var f = files.next();
        out.push({
          path: d.getName() + '/' + day.getName() + '/' + f.getName(),
          bytes: f.getSize(),
          updated: Utilities.formatDate(f.getLastUpdated(), 'UTC', "yyyy-MM-dd'T'HH:mm:ss'Z'")
        });
      }
    }
  }
  return out;
}

function readFile_(path) {
  var parts = String(path).split('/');
  if (parts.length !== 3) throw new Error('bad path');
  var dev = root_().getFoldersByName(parts[0]);
  if (!dev.hasNext()) throw new Error('not found');
  var day = dev.next().getFoldersByName(parts[1]);
  if (!day.hasNext()) throw new Error('not found');
  var f = day.next().getFilesByName(parts[2]);
  if (!f.hasNext()) throw new Error('not found');
  return f.next().getBlob().getDataAsString('UTF-8');
}

function json_(o) {
  return ContentService.createTextOutput(JSON.stringify(o)).setMimeType(ContentService.MimeType.JSON);
}

function text_(s) {
  return ContentService.createTextOutput(s).setMimeType(ContentService.MimeType.TEXT);
}
