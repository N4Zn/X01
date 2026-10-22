/**
 * EduXplore — TỔNG HỢP BÁO CÁO trên máy chủ Google (chạy theo giờ, không cần máy nào bật).
 *
 * Cập nhật: 2026-10-22. CÙNG DỰ ÁN với DriveUpload.gs (dùng chung ROOT_NAME, root_(), sub_(), json_()).
 *
 * Luồng:  GameLogs trên Drive  →  aggregateRun() (trigger mỗi 10 phút)  →  EduXplore_Logs/_report/report.json
 *         trang báo cáo (ReportGateway.gs, dự án RIÊNG) chỉ đọc report.json + dashboard.html.
 *
 * Phần tính (extract / score / buildReport) là BẢN CHUYỂN của Python tool/pipeline/edux_lib.py (ingest_local, score,
 * diagnose) + web/tools/build_report_json.py. Python vẫn là bản chuẩn; test đối chiếu: docs/danh-gia-nang-luc/tool/tests/
 * test_aggregate_parity.js. Sửa công thức → sửa Ở CẢ HAI chỗ rồi chạy lại test.
 *
 * Cấu hình: EduXplore_Logs/_report/config.json = bản chép của tool/pipeline/config.json (nguồn duy nhất là file trong repo;
 * đẩy lên bằng web/tools/push_to_drive.py). Đổi config → tự tính lại từ đầu.
 *
 * Tính tăng dần: chỉ đọc file phiên MỚI/ĐỔI (Drive modifiedDate), kết quả từng phiên lưu ở _state/agg_state.json.
 * Mỗi lượt tối đa ~4,5 phút (giới hạn Apps Script 6 phút); chưa xong thì lượt sau làm tiếp.
 *
 * CÀI ĐẶT: xem docs/sheets-report.md mục "Báo cáo tự động". Cần thêm quyền "script.scriptapp" vào appsscript.json
 * (oauthScopes) và chạy installTrigger() một lần.
 */

var REPORT_DIR = '_report';
var STATE_DIR = '_state';
var BUDGET_MS = 270 * 1000;
var SESSION_RX = /^\d{4}-\d{2}-\d{2}_\d{6}_.+\.json$/;

// ══ PHẦN TÍNH THUẦN (không đụng Drive) — chạy được cả trong Node để test ═════════════════════════════════════════

function edxNormGame_(cfg, g) { return (cfg.game_alias && cfg.game_alias[g]) || g; }

/** Tên đầy đủ / tên thường gọi / tên cũ → học sinh. */
function edxByName_(cfg) {
  var d = {};
  cfg.roster.forEach(function (s) {
    d[s.name] = s;
    if (s.alias && !(s.alias in d)) d[s.alias] = s;
    (s.also || []).forEach(function (a) { if (!(a in d)) d[a] = s; });
  });
  return d;
}

/**
 * 1 file log phiên → các round (dạng mảng gọn). Port của edux_lib.ingest_local cho MỘT file:
 * mỗi ô (trái/phải) = 1 bé = tên (không phải tên chung) xuất hiện nhiều round nhất.
 * Trả { rows: [[ts, code, game, slot(0|1), qid, ok(0|1), t, i]], st: {badClock, unassigned, notRoster, skipped} }.
 */
function edxExtractFile_(cfg, byname, base, data) {
  var st = { badClock: 0, unassigned: 0, notRoster: 0, skipped: 0 };
  var rows = [];
  if ((cfg.skip_files || []).indexOf(base) >= 0) { st.skipped = 1; return { rows: rows, st: st }; }
  var es = ((data && data.entries) || []).filter(function (e) { return e.eventType === 'round'; });
  if (!es.length) return { rows: rows, st: st };
  if (parseInt(String(es[0].time).slice(0, 4), 10) < cfg.min_valid_year) { st.badClock = es.length; return { rows: rows, st: st }; }
  var generic = {};
  cfg.generic_names.forEach(function (g) { generic[g] = true; });
  var ov = cfg.slot_override || {};
  ['left', 'right'].forEach(function (slot, slotIdx) {
    var rr = es.filter(function (e) { return e.slot === slot; });
    if (!rr.length) return;
    var cnt = {}, order = [];
    rr.forEach(function (e) {
      if (generic[e.name]) return;
      if (!(e.name in cnt)) { cnt[e.name] = 0; order.push(e.name); }
      cnt[e.name]++;
    });
    var kid = ov[base + '|' + slot] || null;
    if (!kid) {
      var best = 0;
      order.forEach(function (n) { if (cnt[n] > best) { best = cnt[n]; kid = n; } });
    }
    if (!kid) { st.unassigned += rr.length; return; }
    if (!(kid in byname)) { st.notRoster += rr.length; return; }
    var code = byname[kid].code;
    rr.forEach(function (e, i) {
      rows.push([String(e.time) + '.000', code, edxNormGame_(cfg, e.game), slotIdx, e.questionId || '',
                 e.correct ? 1 : 0, Number(e.answerTimeSec), i]);
    });
  });
  return { rows: rows, st: st };
}

function edxSpeedK_(f, t) {
  if (t === null || t === undefined || t >= f.tslow) return 1.0;
  if (t <= f.tfast || f.tslow <= f.tfast) return f.kmax;
  return 1 + (f.kmax - 1) * (f.tslow - t) / (f.tslow - f.tfast);
}

function edxHpOf_(cfg, game, qid) {
  if (game === 'TongHopToan') {
    var m = /^THT_\d+_(\w+)/.exec(qid || '');
    return m ? (cfg.tht_hp[m[1]] || null) : null;
  }
  return (cfg.game_hp && cfg.game_hp[game]) || null;
}

/** Nhận định 1 học phần từ danh sách câu a=[{ok,t}] (port edux_lib.diagnose). */
function edxDiagnose_(cfg, a) {
  var g = cfg.diag, n = a.length;
  var ts = a.map(function (x) { return x.t; }).sort(function (p, q) { return p - q; });
  var tmed = n % 2 ? ts[(n - 1) / 2] : (ts[n / 2 - 1] + ts[n / 2]) / 2;
  var okc = 0;
  a.forEach(function (x) { if (x.ok) okc++; });
  var acc = 100 * okc / n, code;
  if (n < g.min_n) code = 'few';
  else if (acc >= g.acc_ok) code = tmed <= g.fast_s ? 'good' : tmed >= g.slow_s ? 'slow_ok' : 'ok';
  else code = tmed <= g.fast_s ? 'fast_wrong' : tmed >= g.slow_s ? 'slow_wrong' : 'shaky';
  return { code: code, tmed: tmed, acc: acc, n: n, few: n < g.few_n };
}

/** Toàn bộ round (đã sắp theo thứ tự chuẩn) → điểm từng học sinh trong [dateFrom, dateTo]. Port edux_lib.score. */
function edxScore_(cfg, db, dateFrom, dateTo) {
  var f = cfg.formula, use = [];
  db.forEach(function (x) {
    var d = x.ts.slice(0, 10);
    if ((dateFrom && d < dateFrom) || (dateTo && d > dateTo)) return;
    if (cfg.scored_games.indexOf(x.game) < 0) return;
    var key16 = x.ts.slice(0, 16);
    for (var i = 0; i < cfg.exclude_ranges.length; i++) {
      var r = cfg.exclude_ranges[i];
      if (r.from <= key16 && key16 <= r.to) return;
    }
    if (x.t < 0) return;
    var hp = edxHpOf_(cfg, x.game, x.qid);
    if (!hp) return;
    use.push({ code: x.code, hp: hp, ok: x.ok, t: x.t, ts: x.ts, sess: x.sess, slot: x.slot });
  });
  var cnt = {};
  use.forEach(function (x) { var k = x.sess + '|' + x.code + '|' + x.slot; cnt[k] = (cnt[k] || 0) + 1; });
  var rows = use.filter(function (x) { return cnt[x.sess + '|' + x.code + '|' + x.slot] >= cfg.min_rounds_per_session; });

  var byCode = {};
  rows.forEach(function (r) { (byCode[r.code] = byCode[r.code] || []).push(r); });
  var out = [];
  cfg.roster.forEach(function (s) {
    var rs = byCode[s.code] || [];
    var d = { code: s.code, name: s.name, alias: s.alias || '', n: rs.length, hp: {} };
    var any = false, sum = 0, cntHp = 0;
    cfg.hp_order.forEach(function (hp) {
      var a = rs.filter(function (r) { return r.hp === hp; });          // đã theo thứ tự ts (db đã sắp)
      a = a.slice(Math.max(0, a.length - f.window));
      if (!a.length) return;
      var ok = 0, tsOk = [];
      a.forEach(function (r) { if (r.ok) { ok++; if (r.t >= cfg.min_resp_sec) tsOk.push(r.t); } });
      var tm = tsOk.length ? tsOk.reduce(function (p, q) { return p + q; }, 0) / tsOk.length : null;
      var k = tm !== null ? edxSpeedK_(f, tm) : 1.0;
      var acc = ok / a.length;
      var pt = Math.min(100, f.base * acc * k);
      d.hp[hp] = { n: a.length, ok: ok, acc: 100 * acc, t: tm, k: k, pt: pt, low: a.length < f.low_sample, diag: edxDiagnose_(cfg, a) };
      sum += pt; cntHp++; any = true;
    });
    if (any) d.toan = sum / cntHp;
    out.push(d);
  });
  return { out: out, rows: rows };
}

function edxR_(x, nd) { return x === null || x === undefined ? null : Number(Number(x).toFixed(nd)); }

function edxDateMs_(s) { var p = s.split('-'); return Date.UTC(+p[0], +p[1] - 1, +p[2]); }
function edxIso_(ms) { return new Date(ms).toISOString().slice(0, 10); }

function edxIsoWeek_(ms) {   // [năm ISO, số tuần ISO]
  var d = new Date(ms);
  var dayNum = (d.getUTCDay() + 6) % 7;                   // Thứ Hai = 0
  var thu = Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate() - dayNum + 3);
  var y = new Date(thu).getUTCFullYear();
  var jan4 = Date.UTC(y, 0, 4);
  var wk = 1 + Math.round(((thu - jan4) / 86400000 - 3 + ((new Date(jan4).getUTCDay() + 6) % 7)) / 7);
  return [y, wk];
}

function edxPad2_(n) { return (n < 10 ? '0' : '') + n; }

function edxMakeWeeks_(dfrom, dto) {
  var a = edxDateMs_(dfrom);
  a -= ((new Date(a).getUTCDay() + 6) % 7) * 86400000;
  var end = edxDateMs_(dto), out = [];
  while (a <= end) {
    var b = a + 6 * 86400000, iw = edxIsoWeek_(a), da = new Date(a), db_ = new Date(b);
    out.push({ id: iw[0] + '-W' + edxPad2_(iw[1]), from: edxIso_(a), to: edxIso_(b), label: 'T' + iw[1],
               range: edxPad2_(da.getUTCDate()) + '–' + edxPad2_(db_.getUTCDate()) + '/' + edxPad2_(db_.getUTCMonth() + 1) });
    a += 7 * 86400000;
  }
  return out;
}

var EDX_DIAG = {
  good: ['Thành thạo', 'Đúng và nhanh: có thể tăng độ khó.', '#1f7a52'],
  ok: ['Khá chắc', 'Đúng, tốc độ vừa phải: luyện thêm cho quen.', '#1f7a52'],
  slow_ok: ['Biết nhưng chậm', 'Đúng nhiều nhưng cần nhiều thời gian: kiến thức có rồi, cần luyện cho thạo.', '#4a7fb0'],
  fast_wrong: ['Nhanh nhưng hay nhầm', 'Trả lời vội/đoán: nhắc bé nhìn kỹ rồi mới chọn; không phải do chưa biết.', '#c0782a'],
  slow_wrong: ['Chậm và sai: chưa nắm', 'Chưa biết cách làm nên lâu và sai: cần dạy lại, làm mẫu từng bước.', '#b23a3a'],
  shaky: ['Chưa vững', 'Đúng chưa đều, tốc độ vừa: luyện thêm.', '#8a6d1a'],
  few: ['Chưa đủ dữ liệu', 'Quá ít câu để kết luận.', '#8a929c']
};

function edxFmtDate_(d) { var p = d.split('-'); return p[2] + '/' + p[1] + '/' + p[0]; }

/** db = [{ts, code, game, slot, qid, ok, t, sess}] đã sắp theo (ts, thứ tự gốc). Port build_report_json.main (chế độ A). */
function edxBuildReport_(cfg, db, dfrom, dto) {
  var f = cfg.formula, lopmap = cfg.lop_map || {};
  var cur = edxScore_(cfg, db, dfrom, dto), out = {};
  cur.out.forEach(function (d) { out[d.code] = d; });
  var weeks = edxMakeWeeks_(dfrom, dto), wk = [];
  weeks.forEach(function (w) {
    var r = edxScore_(cfg, db, w.from, w.to), o = {};
    r.out.forEach(function (d) { o[d.code] = d; });
    wk.push({ o: o, rows: r.rows });
  });
  var hpEntry = function (v) {
    var g = v.diag;
    return { n: v.n, ok: v.ok, acc: edxR_(v.acc, 1), t: edxR_(v.t, 2), k: edxR_(v.k, 3), pt: edxR_(v.pt, 1), low: !!v.low,
             diag: g.code, tmed: edxR_(g.tmed, 2), few: !!g.few };
  };
  var histEntry = function (d, rowsW, code) {
    if (!d || !Object.keys(d.hp).length) return null;
    var mine = rowsW.filter(function (r) { return r.code === code; });
    var n = 0, ok = 0, hp = {}, days = {}, ts = [];
    Object.keys(d.hp).forEach(function (h) { n += d.hp[h].n; ok += d.hp[h].ok; hp[h] = edxR_(d.hp[h].pt, 1); });
    mine.forEach(function (r) { days[r.ts.slice(0, 10)] = 1; if (r.ok) ts.push(r.t); });
    return { score: edxR_(d.toan, 1), n: n, acc: n ? edxR_(100 * ok / n, 1) : null,
             t: ts.length ? edxR_(ts.reduce(function (p, q) { return p + q; }, 0) / ts.length, 2) : null,
             days: Object.keys(days).length, hp: hp };
  };
  var students = cfg.roster.map(function (s) {
    var d = out[s.code];
    var st = { code: s.code, name: s.name, alias: s.alias || '', khoi: s.class || '', lop: lopmap[s.code] || s.class || '', mon: {} };
    if (d && Object.keys(d.hp).length) {
      var hp = {};
      Object.keys(d.hp).forEach(function (h) { hp[h] = hpEntry(d.hp[h]); });
      var m = { score: edxR_(d.toan, 1), n: d.n, hp: hp };
      if (weeks.length) m.hist = wk.map(function (w) { return histEntry(w.o[s.code], w.rows, s.code); });
      st.mon[cfg.mon || 'Toán'] = m;
    }
    return st;
  });
  var diag = {};
  Object.keys(EDX_DIAG).forEach(function (k) { diag[k] = { label: EDX_DIAG[k][0], advice: EDX_DIAG[k][1], color: EDX_DIAG[k][2] }; });
  var formula = {};
  ['cuts', 'low_sample', 'few_total', 'base', 'kmax', 'tfast', 'tslow'].forEach(function (k) { formula[k] = f[k]; });
  return { version: 2, ky: { from: dfrom, to: dto, label: edxFmtDate_(dfrom) + ' → ' + edxFmtDate_(dto) }, weeks: weeks,
           formula: formula, diag: diag, mon: [{ id: cfg.mon || 'Toán', hp: cfg.hp_order }], students: students };
}

/** state.bases = { base: { dev, rows: [[ts,code,game,slot,qid,ok,t,i]] } } → db đã sắp đúng thứ tự Python
 *  (file theo tên, ô trái rồi phải, thứ tự trong ô; rồi sắp ổn định theo ts). */
function edxFlatten_(bases) {
  var names = Object.keys(bases).sort();
  var db = [];
  names.forEach(function (b) {
    var sess = b.slice(0, -5), rows = bases[b].rows.slice();
    rows.sort(function (p, q) { return p[3] !== q[3] ? p[3] - q[3] : p[7] - q[7]; });   // ô trái trước, rồi i
    rows.forEach(function (r) { db.push({ ts: r[0], code: r[1], game: r[2], slot: r[3], qid: r[4], ok: !!r[5], t: r[6], sess: sess }); });
  });
  // sắp ổn định theo ts (Array.prototype.sort ổn định trong V8 ≥ 7.0)
  db.sort(function (p, q) { return p.ts < q.ts ? -1 : p.ts > q.ts ? 1 : 0; });
  return db;
}

/** Kỳ báo cáo: từ cfg.report_from (nếu có) hoặc ngày có dữ liệu sớm nhất, đến ngày có dữ liệu mới nhất. */
function edxReportFromBases_(cfg, bases) {
  var db = edxFlatten_(bases);
  var days = db.filter(function (x) { return cfg.scored_games.indexOf(x.game) >= 0; }).map(function (x) { return x.ts.slice(0, 10); }).sort();
  if (!days.length) return null;
  var dfrom = cfg.report_from && cfg.report_from > days[0] ? cfg.report_from : days[0];
  return edxBuildReport_(cfg, db, dfrom, days[days.length - 1]);
}

// ══ PHẦN DRIVE ═══════════════════════════════════════════════════════════════════════════════════════════════════

function findFile_(folder, name) {
  var it = folder.getFilesByName(name);
  return it.hasNext() ? it.next() : null;
}

function writeText_(folder, name, text) {
  var f = findFile_(folder, name);
  if (f) f.setContent(text); else folder.createFile(name, text, MimeType.PLAIN_TEXT);
}

/** Lượt tổng hợp (gắn vào trigger mỗi 10 phút; chạy tay được). */
function aggregateRun() {
  var lock = LockService.getScriptLock();
  if (!lock.tryLock(5000)) return;                       // lượt trước còn chạy
  try {
    var t0 = Date.now();
    var root = root_(), repDir = sub_(root, REPORT_DIR), stDir = sub_(root, STATE_DIR);
    var cfgFile = findFile_(repDir, 'config.json');
    if (!cfgFile) throw new Error('Thiếu _report/config.json — chạy web/tools/push_to_drive.py');
    var cfgText = cfgFile.getBlob().getDataAsString('UTF-8');
    var cfg = JSON.parse(cfgText);
    var sig = Utilities.base64Encode(Utilities.computeDigest(Utilities.DigestAlgorithm.MD5, cfgText, Utilities.Charset.UTF_8));

    var stFile = findFile_(stDir, 'agg_state.json');
    var state = stFile ? JSON.parse(stFile.getBlob().getDataAsString('UTF-8')) : null;
    if (!state || state.sig !== sig) state = { sig: sig, since: 0, files: {}, bases: {} };   // config đổi → tính lại từ đầu

    var scanStart = Date.now();
    var byname = edxByName_(cfg);
    var q = "title contains '.json' and trashed = false";
    if (state.since) q += " and modifiedDate > '" + new Date(state.since).toISOString() + "'";
    var it = DriveApp.searchFiles(q), okFolder = {}, done = 0, finished = true, changed = false;
    while (it.hasNext()) {
      if (Date.now() - t0 > BUDGET_MS) { finished = false; break; }
      var f = it.next(), name = f.getName();
      if (!SESSION_RX.test(name)) continue;
      var id = f.getId(), upd = f.getLastUpdated().getTime();
      if (state.files[id] && state.files[id] >= upd) continue;
      var par = f.getParents();
      if (!par.hasNext()) continue;
      var day = par.next(), dayId = day.getId();
      if (!(dayId in okFolder)) okFolder[dayId] = folderIsLog_(day);
      if (!okFolder[dayId]) continue;
      var data;
      try { data = JSON.parse(f.getBlob().getDataAsString('UTF-8')); } catch (e) { state.files[id] = upd; continue; }
      var r = edxExtractFile_(cfg, byname, name, data);
      state.bases[name] = { dev: day.getParents().hasNext() ? day.getParents().next().getName() : '', rows: r.rows };
      state.files[id] = upd;
      changed = true; done++;
    }
    if (finished) state.since = scanStart - 120000;      // lùi 2 phút cho an toàn; file đã xử lý bị bỏ qua nhờ state.files
    if (changed || finished) {
      writeText_(stDir, 'agg_state.json', JSON.stringify(state));
    }
    var rep = edxReportFromBases_(cfg, state.bases);
    if (rep) {
      rep.generatedAt = Utilities.formatDate(new Date(), 'Asia/Ho_Chi_Minh', "yyyy-MM-dd'T'HH:mm:ssXXX");
      rep.sessions = Object.keys(state.bases).length;
      writeText_(repDir, 'report.json', JSON.stringify(rep));
    }
    console.log('aggregateRun: xử lý ' + done + ' file mới/đổi, ' + (finished ? 'xong' : 'CÒN, lượt sau làm tiếp') +
                ', ' + Object.keys(state.bases).length + ' phiên, ' + Math.round((Date.now() - t0) / 1000) + 's');
  } finally {
    lock.releaseLock();
  }
}

// Thư mục ngày hợp lệ = EduXplore_Logs/<máy>/<ngày>. Tên thư mục là ngày thì mới nhận.
function folderIsLog_(day) {
  if (!/^\d{4}-\d{2}-\d{2}$|^unknown$/.test(day.getName())) return false;
  var dev = day.getParents();
  if (!dev.hasNext()) return false;
  var root = dev.next().getParents();
  return root.hasNext() && root.next().getName() === ROOT_NAME;
}

/** Chạy MỘT LẦN để đặt trigger 10 phút. */
function installTrigger() {
  ScriptApp.getProjectTriggers().forEach(function (t) { if (t.getHandlerFunction() === 'aggregateRun') ScriptApp.deleteTrigger(t); });
  ScriptApp.newTrigger('aggregateRun').timeBased().everyMinutes(10).create();
}

/** Xoá kết quả tích luỹ để tính lại từ đầu (khi nghi ngờ số liệu). */
function aggregateReset() {
  var f = findFile_(sub_(root_(), STATE_DIR), 'agg_state.json');
  if (f) f.setTrashed(true);
}

if (typeof module !== 'undefined') {
  module.exports = { edxByName_: edxByName_, edxExtractFile_: edxExtractFile_, edxScore_: edxScore_, edxBuildReport_: edxBuildReport_,
                     edxFlatten_: edxFlatten_, edxReportFromBases_: edxReportFromBases_, edxMakeWeeks_: edxMakeWeeks_ };
}
