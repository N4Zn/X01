/**
 * Apps Script nhận log từ SheetsSyncManager (Unity). Bản tham chiếu — dán vào editor của Sheet rồi
 * Deploy → Manage deployments → Edit → New version → Deploy (sửa code KHÔNG tự cập nhật URL /exec).
 *
 * - Cột theo TÊN: key lạ trong JSON tự thêm thành cột mới ở cuối hàng tiêu đề (không cần sửa script
 *   khi app thêm field như sessionId, deviceId, rid, correctAnswer).
 * - Loại trùng theo `rid` (mã duy nhất mỗi dòng do app sinh): dòng gửi lại khi mạng chập chờn bị bỏ qua.
 *   Chỉ so với RECENT_ROWS dòng cuối của Sheet (đủ cho gửi bù sau mất mạng).
 * - Trả về {"ok":true,"count":N,"skipped":M} — app in body này ra log.
 */
var SHEET_GID = 0;          // tab ghi log (gid trên URL)
var RECENT_ROWS = 5000;     // số dòng cuối dùng để dò rid trùng

function doPost(e) {
  var lock = LockService.getScriptLock();
  lock.waitLock(30000);
  try {
    var rows = JSON.parse(e.postData.contents).rows || [];
    var sh = SpreadsheetApp.getActiveSpreadsheet().getSheets().filter(function (s) { return s.getSheetId() === SHEET_GID; })[0];
    if (!sh) throw new Error('Không thấy tab gid=' + SHEET_GID);

    var lastCol = Math.max(sh.getLastColumn(), 1);
    var header = sh.getRange(1, 1, 1, lastCol).getValues()[0];
    if (sh.getLastRow() === 0) header = [];
    var col = {};
    header.forEach(function (h, i) { if (h !== '') col[h] = i; });

    // key lạ → cột mới
    var added = false;
    rows.forEach(function (r) {
      Object.keys(r).forEach(function (k) {
        if (!(k in col)) { col[k] = header.length; header.push(k); added = true; }
      });
    });
    if (added) sh.getRange(1, 1, 1, header.length).setValues([header]);

    // loại trùng theo rid
    var seen = {};
    if (col.rid !== undefined && sh.getLastRow() > 1) {
      var from = Math.max(2, sh.getLastRow() - RECENT_ROWS + 1);
      sh.getRange(from, col.rid + 1, sh.getLastRow() - from + 1, 1).getValues()
        .forEach(function (v) { if (v[0] !== '') seen[v[0]] = true; });
    }
    var out = [], skipped = 0;
    rows.forEach(function (r) {
      if (r.rid) { if (seen[r.rid]) { skipped++; return; } seen[r.rid] = true; }
      var line = new Array(header.length).fill('');
      Object.keys(r).forEach(function (k) { line[col[k]] = r[k] === null ? '' : r[k]; });
      out.push(line);
    });
    if (out.length) sh.getRange(sh.getLastRow() + 1, 1, out.length, header.length).setValues(out);
    return ContentService.createTextOutput(JSON.stringify({ ok: true, count: out.length, skipped: skipped }))
      .setMimeType(ContentService.MimeType.JSON);
  } catch (err) {
    return ContentService.createTextOutput(JSON.stringify({ ok: false, error: String(err) }))
      .setMimeType(ContentService.MimeType.JSON);
  } finally {
    lock.releaseLock();
  }
}
