/* EduXplore · Báo cáo kết quả học tập — dashboard cho quản lý (PC), widget không phụ thuộc thư viện.
 *
 *   EduxReport.mount(element, { data | dataUrl, theme, logo, hash, mon, height, onSelectStudent })
 *     -> { update(data), go({tab,khoi,lop,code,mon}), destroy() }
 *
 *   <div data-edux-report data-src="data/report.json" data-logo="logo.png"></div>   // tự gắn khi load
 *
 * Bốn tab: Toàn trường (xếp hạng lớp TRONG TỪNG KHỐI, theo từng môn) · Theo khối · Theo lớp · Từng học sinh.
 * Nguyên tắc: các khối học chương trình khác nhau -> KHÔNG có số liệu nào gộp / so sánh giữa các khối;
 * mọi so sánh chỉ xảy ra giữa các lớp trong cùng 1 khối. Nhiều môn: nút "Tất cả môn" (TB các môn bé đã chơi) hoặc từng môn.
 * Bố cục vừa 1 màn hình (≥1000px rộng); danh sách dài cuộn trong khung của nó, trang không cuộn.
 * Dữ liệu (schema v2: nhiều môn, weeks + hist theo tuần) và cách tích hợp: xem INTEGRATION.md.
 */
(function (global) {
  'use strict';

  var LV = ['Mức 1', 'Mức 2', 'Mức 3', 'Đạt mục tiêu'];
  var FLAG = ['slow_wrong', 'fast_wrong', 'slow_ok', 'shaky'];
  var SHORT = { slow_wrong: 'chậm-sai', fast_wrong: 'nhanh-nhầm', slow_ok: 'đúng-chậm', shaky: 'chưa vững' };
  var CMP = [['Kết quả', [['bar', 'Cột'], ['heat', 'Bản đồ nhiệt'], ['dist', 'Phân bố mức'], ['box', 'Biên độ điểm'], ['radar', 'Radar'], ['xy', 'Chính xác × tốc độ']]],
    ['Tiến bộ', [['trend', 'Xu hướng tuần'], ['prog', 'Mức tiến bộ']]], ['Tham gia', [['act', 'Mức độ chơi'], ['part', 'Theo tuần']]], ['Chi tiết', [['table', 'Bảng']]]];
  var UP = 3, DOWN = 5;   // ≥ +3 điểm: tiến bộ rõ; ≤ -3: đi xuống; giảm ≥ 5 so tuần trước: cảnh báo

  function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
  function num(x, d) { return (x == null || isNaN(x)) ? '–' : Number(x).toFixed(d || 0).replace('.', ','); }
  function mean(a) { return a.length ? a.reduce(function (s, x) { return s + x; }, 0) / a.length : null; }
  function median(a) { if (!a.length) return null; var b = a.slice().sort(function (x, y) { return x - y; }), n = b.length; return n % 2 ? b[(n - 1) / 2] : (b[n / 2 - 1] + b[n / 2]) / 2; }
  function quant(sorted, p) { var pos = (sorted.length - 1) * p, lo = Math.floor(pos), hi = Math.ceil(pos); return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo); }
  function uniq(a) { return a.filter(function (x, i) { return a.indexOf(x) === i; }); }
  function niceStep(r) { var p = Math.pow(10, Math.floor(Math.log(r) / Math.LN10)), f = r / p; return (f < 1.5 ? 1 : f < 3.5 ? 2 : f < 7.5 ? 5 : 10) * p; }
  function fold(s) { return String(s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'd').toLowerCase(); }
  function khoiName(k) { return /^khối/i.test(k) ? k : 'Khối ' + k; }
  function vals(a) { return a.filter(function (v) { return v != null; }); }
  function delta(v, d, suffix) {
    if (v == null || isNaN(v)) return '<span class="dl n">–</span>';
    var c = v >= 1 ? 'up' : v <= -1 ? 'dn' : 'fl';
    return '<span class="dl ' + c + '">' + (v >= 1 ? '▲ +' : v <= -1 ? '▼ ' : '▬ ') + num(v, d == null ? 1 : d) + (suffix || '') + '</span>';
  }

  function Widget(el, opts) {
    var data = null, M = null;
    var st = { tab: 'tq', khoi: null, lop: null, code: null, mon: opts.mon || null, theme: opts.theme || 'light',
      cmp: 'bar', metric: 'avg', cmpSort: false, rsel: null, tsel: null, prog: 'first', lchart: null, filt: 'all', q: '',
      sort: { tq: { k: null, d: -1 }, lop: { k: 'name', d: 1 }, rank: { k: 'score', d: -1 } } };
    try { var saved = global.localStorage.getItem('edux-report-theme'); if (saved === 'dark' || saved === 'light') st.theme = saved; } catch (e) { /* không có storage thì thôi */ }

    el.classList.add('edx');
    if (opts.height) el.style.height = opts.height; else el.classList.add('edx-vh');
    function applyTheme() { el.setAttribute('data-edx-theme', st.theme); }
    applyTheme();

    // ---------- mô hình dữ liệu ----------
    // Mỗi học sinh có 1 "view" cho mỗi lựa chọn môn: 'all' (TB các môn đã chơi) hoặc 1 môn. View của 'all' coi mỗi môn là 1 chiều (dim)
    // như học phần của 1 môn, nên mọi biểu đồ/bảng dùng chung code cho cả hai.
    function build(d) {
      var F = d.formula, cuts = F.cuts, weeks = d.weeks || [], nW = weeks.length, LOWPLAY = 2 * F.few_total;
      var monIds = d.mon.map(function (m) { return m.id; });
      function lv(p) { return p >= cuts[2] ? 3 : p >= cuts[1] ? 2 : p >= cuts[0] ? 1 : 0; }
      function hpOf(monId, khoi) {
        var m = d.mon.filter(function (x) { return x.id === monId; })[0], h = m && m.hp;
        return !h ? [] : Array.isArray(h) ? h : (h[khoi] || h['*'] || []);
      }
      function dimsOf(sel, khoi) { return sel === 'all' ? monIds.filter(function (id) { return hpOf(id, khoi).length; }) : hpOf(sel, khoi); }

      function mk(s, m, sel) {
        var o = { s: s, code: s.code, name: s.name, alias: s.alias || s.name, khoi: s.khoi, lop: s.lop, sel: sel, m: m, hist: [], series: [], wIdx: [], idle: 0, days: 0, flags: [], play: 'none' };
        if (!m) return o;
        var n = 0, ok = 0, tw = 0, tn = 0;
        Object.keys(m.hp).forEach(function (h) {
          var v = m.hp[h]; n += v.n; ok += v.ok;
          if (v.t != null) { tw += v.t * v.n; tn += v.n; }
          if (FLAG.indexOf(v.diag) >= 0) o.flags.push({ hp: h, code: v.diag, few: v.few });
        });
        o.score = m.score; o.lv = lv(m.score); o.acc = n ? 100 * ok / n : null; o.t = tn ? tw / tn : null;
        o.few = m.n < F.few_total;
        o.hist = m.hist || [];
        var idx = []; o.hist.forEach(function (h, i) { if (h) idx.push(i); });
        o.wIdx = idx; o.series = o.hist.map(function (h) { return h ? h.score : null; });
        o.lastW = idx.length ? idx[idx.length - 1] : -1; o.firstW = idx.length ? idx[0] : -1;
        o.idle = nW ? nW - 1 - o.lastW : 0;
        o.dFirst = idx.length >= 2 ? o.hist[o.lastW].score - o.hist[o.firstW].score : null;
        o.dPrev = idx.length >= 2 ? o.hist[o.lastW].score - o.hist[idx[idx.length - 2]].score : null;
        o.days = o.hist.reduce(function (sum, h) { return sum + (h ? h.days || 0 : 0); }, 0);
        o.play = (m.n < LOWPLAY || (nW >= 3 && idx.length <= 1)) ? 'few' : 'ok';
        return o;
      }
      function mkAll(s) {
        var subs = monIds.filter(function (id) { return s.mon && s.mon[id]; }).map(function (id) { return { id: id, m: s.mon[id] }; });
        if (!subs.length) return mk(s, null, 'all');
        var hp = {}, n = 0;
        subs.forEach(function (x) {
          var nn = 0, oo = 0, ww = 0, tt = 0;
          Object.keys(x.m.hp).forEach(function (h) { var v = x.m.hp[h]; nn += v.n; oo += v.ok; if (v.t != null) { ww += v.t * v.n; tt += v.n; } });
          hp[x.id] = { n: nn, ok: oo, acc: nn ? 100 * oo / nn : null, t: tt ? ww / tt : null, pt: x.m.score, low: nn < F.low_sample, diag: null, few: nn < F.few_total };
          n += x.m.n;
        });
        var hist = weeks.map(function (w, i) {
          var es = subs.map(function (x) { return x.m.hist && x.m.hist[i] ? { id: x.id, e: x.m.hist[i] } : null; }).filter(Boolean);
          if (!es.length) return null;
          var nn = 0, ok = 0, tw = 0, tn = 0, hh = {};
          es.forEach(function (q) { nn += q.e.n; if (q.e.acc != null) ok += q.e.acc * q.e.n / 100; if (q.e.t != null) { tw += q.e.t * q.e.n; tn += q.e.n; } hh[q.id] = q.e.score; });
          return { score: mean(es.map(function (q) { return q.e.score; })), n: nn, acc: nn ? 100 * ok / nn : null, t: tn ? tw / tn : null,
            days: Math.max.apply(null, es.map(function (q) { return q.e.days || 0; })), hp: hh };
        });
        return mk(s, { score: mean(subs.map(function (x) { return x.m.score; })), n: n, hp: hp, hist: hist }, 'all');
      }

      var cache = {};
      function list(sel) {
        if (!cache[sel]) cache[sel] = d.students.map(function (s) { return sel === 'all' ? mkAll(s) : mk(s, (s.mon && s.mon[sel]) || null, sel); });
        return cache[sel];
      }
      var base = list('all');
      var khois = uniq(base.map(function (x) { return x.khoi; }));
      var lopsOf = {};
      khois.forEach(function (k) { lopsOf[k] = uniq(base.filter(function (x) { return x.khoi === k; }).map(function (x) { return x.lop; })); });

      function group(lst) {
        var sc = lst.filter(function (x) { return x.m; });
        var dims = lst.length ? dimsOf(lst[0].sel, lst[0].khoi) : [];
        var g = { all: lst, sc: sc, n: lst.length, ns: sc.length, dist: [0, 0, 0, 0], hp: {}, dims: dims };
        sc.forEach(function (x) { g.dist[x.lv]++; });
        g.avg = mean(sc.map(function (x) { return x.score; }));
        g.lv = g.avg == null ? null : lv(g.avg);
        g.pctOk = sc.length ? 100 * g.dist[3] / sc.length : null;
        g.pctLow = sc.length ? 100 * (g.dist[0] + g.dist[1]) / sc.length : null;
        dims.forEach(function (h) { var a = sc.filter(function (x) { return x.m.hp[h]; }).map(function (x) { return x.m.hp[h].pt; }); if (a.length) g.hp[h] = { avg: mean(a), n: a.length }; });
        g.acc = mean(sc.filter(function (x) { return x.acc != null; }).map(function (x) { return x.acc; }));
        g.t = median(sc.filter(function (x) { return x.t != null; }).map(function (x) { return x.t; }));
        var weak = null; Object.keys(g.hp).forEach(function (h) { if (!weak || g.hp[h].avg < g.hp[weak].avg) weak = h; }); g.weak = weak;
        // mức độ chơi
        g.nNone = lst.length - sc.length; g.nFew = sc.filter(function (x) { return x.play === 'few'; }).length; g.nOk = sc.length - g.nFew;
        g.sumN = sc.reduce(function (s, x) { return s + x.m.n; }, 0);
        g.perCap = lst.length ? g.sumN / lst.length : null;
        g.pctPlayed = lst.length ? 100 * sc.length / lst.length : null;
        // tiến bộ (ghép cặp từng bé: tuần có chơi gần nhất so với tuần đầu / tuần trước)
        var df = vals(sc.map(function (x) { return x.dFirst; })), dp = vals(sc.map(function (x) { return x.dPrev; }));
        g.dFirst = mean(df); g.dPrev = mean(dp); g.nTrend = df.length;
        g.up = df.filter(function (v) { return v >= UP; }).length; g.down = df.filter(function (v) { return v <= -UP; }).length; g.flat = df.length - g.up - g.down;
        g.nDown = dp.filter(function (v) { return v <= -DOWN; }).length;
        g.nIdle = lst.filter(function (x) { return nW >= 2 && x.idle >= 2; }).length;
        g.wavg = weeks.map(function (w, i) { return mean(vals(sc.map(function (x) { return x.hist[i] ? x.hist[i].score : null; }))); });
        g.wpart = weeks.map(function (w, i) { return sc.filter(function (x) { return x.hist[i]; }).length; });
        g.partLast = nW && g.n ? 100 * g.wpart[nW - 1] / g.n : null;
        return g;
      }
      return { d: d, F: F, cuts: cuts, lv: lv, weeks: weeks, nW: nW, LOWPLAY: LOWPLAY, monIds: monIds, khois: khois, lopsOf: lopsOf, group: group, list: list, dimsOf: dimsOf,
        inLop: function (k, l, sel) { return list(sel || st.mon).filter(function (x) { return x.khoi === k && x.lop === l; }); },
        inKhoi: function (k, sel) { return list(sel || st.mon).filter(function (x) { return x.khoi === k; }); },
        byCode: function (c) { return list(st.mon).filter(function (x) { return x.code === c; })[0]; } };
    }
    function S() { return M.list(st.mon); }
    function hpl() { return M.dimsOf(st.mon, st.khoi); }
    function dimWord() { return st.mon === 'all' ? 'môn' : 'học phần'; }
    function monLabel() { return st.mon === 'all' ? 'Tất cả môn' : st.mon; }

    function reconcile() {
      if (M.monIds.length < 2 || !st.mon || (st.mon !== 'all' && M.monIds.indexOf(st.mon) < 0)) st.mon = M.monIds.length > 1 ? 'all' : M.monIds[0];
      if (st.khoi == null || M.khois.indexOf(st.khoi) < 0) st.khoi = M.khois[0];
      if (st.lop == null || M.lopsOf[st.khoi].indexOf(st.lop) < 0) st.lop = M.lopsOf[st.khoi][0];
      var list = M.inLop(st.khoi, st.lop);
      if (!st.code || !M.byCode(st.code) || M.byCode(st.code).khoi !== st.khoi || M.byCode(st.code).lop !== st.lop) {
        var first = list.filter(function (x) { return x.m; })[0] || list[0];
        st.code = first ? first.code : null;
      }
      if (!st.lchart) st.lchart = M.nW >= 2 ? 'trend' : 'xy';
      if (M.nW < 2 && (st.cmp === 'trend' || st.cmp === 'prog')) st.cmp = 'bar';
      if (!M.nW && st.cmp === 'part') st.cmp = 'bar';
      if (st.metric.indexOf('hp:') === 0 && hpl().indexOf(st.metric.slice(3)) < 0) st.metric = 'avg';
    }

    // ---------- thành phần nhỏ ----------
    function pill(i, t) { return '<span class="edx-pill lv' + i + '">' + esc(t || LV[i]) + '</span>'; }
    function stack(g, big) {
      if (!g.ns) return '<div class="edx-stack"></div>';
      return '<div class="edx-stack' + (big ? ' big' : '') + '">' + [3, 2, 1, 0].map(function (i) {
        return g.dist[i] ? '<i class="lv' + i + '" style="width:' + (100 * g.dist[i] / g.ns) + '%" title="' + LV[i] + ': ' + g.dist[i] + ' bé">' + (big && g.dist[i] / g.ns > 0.07 ? g.dist[i] : '') + '</i>' : '';
      }).join('') + '</div>';
    }
    // mức độ chơi: đều (xanh) / ít (vàng) / chưa chơi (xám); độ rộng theo sĩ số
    function playStack(g, big) {
      if (!g.n) return '<div class="edx-stack play"></div>';
      var seg = [['p2', g.nOk, 'chơi đều'], ['p1', g.nFew, 'chơi ít (< ' + M.LOWPLAY + ' câu)'], ['p0', g.nNone, 'chưa chơi']];
      return '<div class="edx-stack play' + (big ? ' big' : '') + '">' + seg.map(function (s) {
        return s[1] ? '<i class="' + s[0] + '" style="width:' + (100 * s[1] / g.n) + '%" title="' + s[2] + ': ' + s[1] + ' bé">' + (big && s[1] / g.n > 0.07 ? s[1] : '') + '</i>' : '';
      }).join('') + '</div>';
    }
    function dist(g) { return '<div class="edx-dist">' + [3, 2, 1, 0].map(function (i) { return '<div class="lv' + i + '"><b>' + g.dist[i] + '</b>' + LV[i] + '</div>'; }).join('') + '</div>'; }
    function heat(m, h) { var v = m && m.hp[h]; return v ? '<td class="h lv' + M.lv(v.pt) + (v.low ? ' low' : '') + '">' + Math.round(v.pt) + '</td>' : '<td class="n">–</td>'; }
    function heatAvg(g, h) { var v = g.hp[h]; return v ? '<td class="h lv' + M.lv(v.avg) + '">' + Math.round(v.avg) + '</td>' : '<td class="n">–</td>'; }
    function ticks() { return M.cuts.map(function (c) { return '<s style="left:' + c + '%"></s>'; }).join(''); }
    function hpBars(g, ref, refLabel) {
      return '<div class="edx-hp">' + g.dims.map(function (h) {
        var a = g.hp[h];
        if (!a) return '<div class="edx-hrow off"><span>' + esc(h) + '</span><span class="edx-track"></span><b>–</b></div>';
        var r = ref && ref.hp[h];
        return '<div class="edx-hrow"><span>' + esc(h) + '</span><span class="edx-track">' + ticks() + '<i class="lv' + M.lv(a.avg) + '" style="width:' + a.avg + '%"></i>' +
          (r ? '<u style="left:' + r.avg + '%" title="' + esc(refLabel) + ': ' + Math.round(r.avg) + '"></u>' : '') + '</span><b>' + Math.round(a.avg) + '</b></div>';
      }).join('') + '</div>' + (ref ? '<p class="edx-legend"><u></u>' + esc(refLabel) + ' · vạch đứt: mốc ' + M.cuts.join(' / ') + '</p>' : '');
    }
    function flagText(x) {
      if (!x.flags || !x.flags.length) return '';
      var s = x.flags.slice(0, 2).map(function (f) { return esc(f.hp) + ': ' + SHORT[f.code] + (f.few ? ' (ít mẫu)' : ''); }).join('; ');
      return s + (x.flags.length > 2 ? ' +' + (x.flags.length - 2) : '');
    }
    function chips(items, cur, act) {
      return items.map(function (it) { return '<button type="button" class="edx-chip sm' + (it[0] === cur ? ' on' : '') + '" data-a="' + act + '" data-v="' + esc(it[0]) + '">' + esc(it[1]) + '</button>'; }).join('');
    }
    function picker() {
      return '<div class="edx-picker">' + M.khois.map(function (k) {
        return '<div class="g"><small>' + esc(khoiName(k)) + '</small>' + M.lopsOf[k].map(function (l) {
          return '<button type="button" class="edx-chip sm' + (k === st.khoi && l === st.lop ? ' on' : '') + '" data-a="lop" data-khoi="' + esc(k) + '" data-lop="' + esc(l) + '">' + esc(l) + '</button>';
        }).join('') + '</div>';
      }).join('') + '</div>';
    }
    function sortList(list, key, getters) {
      var s = st.sort[key], g = getters[s.k] || getters.name;
      return list.slice().sort(function (a, b) {
        var x = g(a), y = g(b);
        if (x == null && y == null) return 0; if (x == null) return 1; if (y == null) return -1;
        return s.d * (typeof x === 'string' ? x.localeCompare(y, 'vi', { numeric: true }) : x - y);
      });
    }
    function th(label, tbl, key, cls, tip) {
      var s = st.sort[tbl], on = s.k === key;
      return '<th class="sortable' + (cls ? ' ' + cls : '') + (on ? ' on' : '') + '" data-a="sort" data-t="' + tbl + '" data-k="' + esc(key) + '"' + (tip ? ' title="' + esc(tip) + '"' : '') + '>' + label + '<i>' + (on ? (s.d > 0 ? ' ▲' : ' ▼') : '') + '</i></th>';
    }
    function spark(series, w, h) {
      var pts = [], n = Math.max(series.length, 2);
      series.forEach(function (v, i) { if (v != null) pts.push([i, v]); });
      if (pts.length < 2) return '<span class="n">–</span>';
      var ys = pts.map(function (p) { return p[1]; }), lo = Math.min.apply(null, ys), hi = Math.max.apply(null, ys);
      if (hi - lo < 8) { var mid = (hi + lo) / 2; lo = mid - 4; hi = mid + 4; }
      var P = pts.map(function (p) { return [3 + (w - 6) * p[0] / (n - 1), 3 + (h - 6) * (hi - p[1]) / (hi - lo)]; });
      var d = pts[pts.length - 1][1] - pts[0][1], c = d >= 1 ? 'var(--lv3)' : d <= -1 ? 'var(--lv0)' : 'var(--muted)', e = P[P.length - 1];
      return '<svg class="edx-spark" viewBox="0 0 ' + w + ' ' + h + '" width="' + w + '" height="' + h + '"><polyline points="' + P.map(function (p) { return p[0].toFixed(1) + ',' + p[1].toFixed(1); }).join(' ') + '" fill="none" style="stroke:' + c + '" stroke-width="1.8" stroke-linejoin="round"/><circle cx="' + e[0].toFixed(1) + '" cy="' + e[1].toFixed(1) + '" r="2.6" style="fill:' + c + '"/></svg>';
    }
    function nameLinks(a, f, max) {
      var shown = a.slice(0, max || 6);
      return shown.map(function (x) { return '<a data-a="code" data-code="' + esc(x.code) + '">' + esc(x.alias) + '</a>' + (f ? ' ' + f(x) : ''); }).join(', ') + (a.length > shown.length ? ' +' + (a.length - shown.length) : '') || '–';
    }

    // ---------- biểu đồ so sánh lớp (chỉ trong 1 khối) ----------
    function khoiList() { return M.inKhoi(st.khoi); }
    function lopRows() {
      return M.lopsOf[st.khoi].map(function (l) { var list = M.inLop(st.khoi, l); return { k: st.khoi, l: l, key: st.khoi + '|' + l, list: list, g: M.group(list) }; });
    }
    function metricDef() {
      var m = st.metric;
      if (m === 'pctOk') return { val: function (r) { return r.g.pctOk; }, col: function () { return 3; }, unit: '%' };
      if (m === 'pctLow') return { val: function (r) { return r.g.pctLow; }, col: function () { return 0; }, unit: '%' };
      if (m === 'pctPlayed') return { val: function (r) { return r.g.pctPlayed; }, col: function () { return 2; }, unit: '%' };
      if (m === 'partLast') return { val: function (r) { return r.g.partLast; }, col: function () { return 2; }, unit: '%' };
      if (m.indexOf('hp:') === 0) { var h = m.slice(3); return { val: function (r) { return r.g.hp[h] ? r.g.hp[h].avg : null; }, col: M.lv, unit: '' }; }
      return { val: function (r) { return r.g.avg; }, col: M.lv, unit: '' };
    }
    var W = 900, H = 330, measuring = false;
    function frame(inner) { return '<div class="edx-chartbox"><svg viewBox="0 0 ' + W + ' ' + H + '" role="img">' + inner + '</svg></div>'; }
    function yGrid(ml, mt, pw, ph, ymin, ymax, step) {
      var o = '', y;
      for (y = ymin; y <= ymax; y += step) o += '<line class="grid" x1="' + ml + '" x2="' + (ml + pw) + '" y1="' + (mt + (ymax - y) / (ymax - ymin) * ph) + '" y2="' + (mt + (ymax - y) / (ymax - ymin) * ph) + '"/><text x="' + (ml - 6) + '" y="' + (mt + (ymax - y) / (ymax - ymin) * ph + 4) + '" text-anchor="end">' + y + '</text>';
      return o;
    }
    function barChart(rows) {
      var md = metricDef(), items = rows.slice();
      if (st.cmpSort) items.sort(function (a, b) { var x = md.val(a), y = md.val(b); return (y == null ? -1 : y) - (x == null ? -1 : x); });
      var ml = 36, mr = 10, mt = 24, mb = 34, pw = W - ml - mr, ph = H - mt - mb, n = items.length, slot = pw / n, bw = Math.min(slot * 0.62, 58);
      function Y(v) { return mt + (100 - v) / 100 * ph; }
      var o = yGrid(ml, mt, pw, ph, 0, 100, 25), ref = md.val({ g: M.group(khoiList()) });
      items.forEach(function (r, i) {
        var v = md.val(r), cx = ml + slot * (i + 0.5);
        o += '<g data-a="lop" data-khoi="' + esc(r.k) + '" data-lop="' + esc(r.l) + '" class="clk"><title>' + esc(r.l) + ': ' + (v == null ? 'chưa có điểm' : num(v, 1) + md.unit) + '</title><rect class="hit" x="' + (cx - slot / 2) + '" y="' + mt + '" width="' + slot + '" height="' + ph + '"/>' +
          (v == null ? '' : '<rect x="' + (cx - bw / 2) + '" y="' + Y(v) + '" width="' + bw + '" height="' + (mt + ph - Y(v)) + '" rx="2" style="fill:var(--lv' + md.col(v) + ')"/><text class="val" x="' + cx + '" y="' + (Y(v) - 6) + '" text-anchor="middle">' + Math.round(v) + '</text>') +
          '<text class="xl" x="' + cx + '" y="' + (mt + ph + 18) + '" text-anchor="middle">' + esc(r.l) + '</text></g>';
      });
      if (ref != null) o += '<line class="ref" x1="' + ml + '" x2="' + (ml + pw) + '" y1="' + Y(ref) + '" y2="' + Y(ref) + '"/><text class="reflab" x="' + (ml + pw) + '" y="12" text-anchor="end">- - - TB ' + esc(khoiName(st.khoi)) + ': ' + num(ref, 1) + md.unit + '</text>';
      return frame(o);
    }
    function boxChart(rows) {
      var ml = 36, mr = 10, mt = 20, mb = 34, pw = W - ml - mr, ph = H - mt - mb, n = rows.length, slot = pw / n, bw = Math.min(slot * 0.5, 42);
      function Y(v) { return mt + (100 - v) / 100 * ph; }
      var o = yGrid(ml, mt, pw, ph, 0, 100, 25);
      M.cuts.forEach(function (c) { o += '<line class="quad" x1="' + ml + '" x2="' + (ml + pw) + '" y1="' + Y(c) + '" y2="' + Y(c) + '"/>'; });
      rows.forEach(function (r, i) {
        var cx = ml + slot * (i + 0.5), sc = r.g.sc.map(function (x) { return x.score; }).sort(function (a, b) { return a - b; });
        o += '<g data-a="lop" data-khoi="' + esc(r.k) + '" data-lop="' + esc(r.l) + '" class="clk"><rect class="hit" x="' + (cx - slot / 2) + '" y="' + mt + '" width="' + slot + '" height="' + ph + '"/><text class="xl" x="' + cx + '" y="' + (mt + ph + 18) + '" text-anchor="middle">' + esc(r.l) + '</text>';
        if (sc.length) {
          var q1 = quant(sc, 0.25), md = quant(sc, 0.5), q3 = quant(sc, 0.75), c = 'var(--lv' + M.lv(md) + ')';
          o += '<title>' + esc(r.l) + ': thấp nhất ' + num(sc[0]) + ' · Q1 ' + num(q1) + ' · trung vị ' + num(md) + ' · Q3 ' + num(q3) + ' · cao nhất ' + num(sc[sc.length - 1]) + '</title>' +
            '<line class="wh" x1="' + cx + '" x2="' + cx + '" y1="' + Y(sc[0]) + '" y2="' + Y(sc[sc.length - 1]) + '"/><line class="wh" x1="' + (cx - bw / 4) + '" x2="' + (cx + bw / 4) + '" y1="' + Y(sc[0]) + '" y2="' + Y(sc[0]) + '"/><line class="wh" x1="' + (cx - bw / 4) + '" x2="' + (cx + bw / 4) + '" y1="' + Y(sc[sc.length - 1]) + '" y2="' + Y(sc[sc.length - 1]) + '"/>' +
            '<rect x="' + (cx - bw / 2) + '" y="' + Y(q3) + '" width="' + bw + '" height="' + Math.max(1, Y(q1) - Y(q3)) + '" rx="2" style="fill:' + c + ';fill-opacity:.28;stroke:' + c + '" stroke-width="1.5"/>';
          sc.forEach(function (v, j) { o += '<circle class="pt" cx="' + (cx + ((j * 37) % 100 / 100 - 0.5) * bw * 0.85) + '" cy="' + Y(v) + '" r="2.2"/>'; });
          o += '<line x1="' + (cx - bw / 2) + '" x2="' + (cx + bw / 2) + '" y1="' + Y(md) + '" y2="' + Y(md) + '" style="stroke:' + c + '" stroke-width="3.5"/><text class="val" x="' + (cx + bw / 2 + 4) + '" y="' + (Y(md) + 4) + '" font-size="11">' + Math.round(md) + '</text>';
        }
        o += '</g>';
      });
      return frame(o);
    }
    function xyChart(rows) {
      var pts = rows.filter(function (r) { return r.g.t != null && r.g.acc != null; });
      if (!pts.length) return '<p class="edx-empty">Chưa đủ dữ liệu.</p>';
      var ml = 50, mr = 20, mt = 16, mb = 44, pw = W - ml - mr, ph = H - mt - mb;
      var ts = pts.map(function (p) { return p.g.t; }), as = pts.map(function (p) { return p.g.acc; });
      var tmin = Math.max(1, Math.min.apply(null, ts) * 0.9 - 0.2), tmax = Math.max.apply(null, ts) * 1.08 + 0.2;
      var ymin = Math.floor((Math.min.apply(null, as) - 3) / 5) * 5, ymax = Math.min(100, Math.ceil((Math.max.apply(null, as) + 2) / 5) * 5);
      function X(t) { return ml + (tmax - t) / (tmax - tmin) * pw; }
      function Y(a) { return mt + (ymax - a) / (ymax - ymin) * ph; }
      var o = '', y, t, ys = niceStep((ymax - ymin) / 5), xs = niceStep((tmax - tmin) / 6);
      for (y = Math.ceil(ymin / ys) * ys; y <= ymax; y += ys) o += '<line class="grid" x1="' + ml + '" x2="' + (ml + pw) + '" y1="' + Y(y) + '" y2="' + Y(y) + '"/><text x="' + (ml - 6) + '" y="' + (Y(y) + 4) + '" text-anchor="end">' + num(y) + '</text>';
      for (t = Math.ceil(tmin / xs) * xs; t <= tmax; t += xs) o += '<line class="grid" x1="' + X(t) + '" x2="' + X(t) + '" y1="' + mt + '" y2="' + (mt + ph) + '"/><text x="' + X(t) + '" y="' + (mt + ph + 16) + '" text-anchor="middle">' + num(t, xs < 1 ? 1 : 0) + '</text>';
      var maxN = Math.max.apply(null, pts.map(function (p) { return p.g.ns; }));
      pts.forEach(function (p) {
        var r = 8 + 14 * Math.sqrt(p.g.ns / maxN);
        o += '<g data-a="lop" data-khoi="' + esc(p.k) + '" data-lop="' + esc(p.l) + '" class="clk"><title>' + esc(p.l) + ': ' + num(p.g.acc, 1) + '% đúng · ' + num(p.g.t, 1) + 's/câu · điểm TB ' + num(p.g.avg) + '</title><circle cx="' + X(p.g.t) + '" cy="' + Y(p.g.acc) + '" r="' + r + '" style="fill:var(--lv' + p.g.lv + ');fill-opacity:.75;stroke:var(--lv' + p.g.lv + ')" stroke-width="2"/><text class="xl in" x="' + X(p.g.t) + '" y="' + (Y(p.g.acc) + 4) + '" text-anchor="middle">' + esc(p.l) + '</text></g>';
      });
      o += '<text class="ttl" x="' + (ml + pw / 2) + '" y="' + (H - 6) + '" text-anchor="middle">Tốc độ: giây/câu, trung vị lớp (nhanh ở bên phải →) · cỡ chấm = số bé có điểm</text><text class="ttl" transform="translate(13 ' + (mt + ph / 2) + ') rotate(-90)" text-anchor="middle">Chính xác: % câu đúng TB</text>';
      return frame(o);
    }
    var PAL = ['var(--s0)', 'var(--s1)', 'var(--s2)', 'var(--s3)', 'var(--s4)', 'var(--s5)'];
    function defaultSel(rows) {
      if (rows.length <= 6) return rows.map(function (r) { return r.key; });
      var byAvg = rows.slice().sort(function (a, b) { return (b.g.avg || 0) - (a.g.avg || 0); });
      return byAvg.slice(0, 3).concat(byAvg.slice(-3)).map(function (r) { return r.key; });
    }
    function selChips(rows, selKey) {
      var sel = st[selKey];
      return '<div class="edx-rleg"><small>Chọn tối đa 6 lớp:</small>' + rows.map(function (r) {
        var i = sel.indexOf(r.key);
        return '<button type="button" class="edx-chip sm' + (i >= 0 ? ' on sw' : '') + '" style="' + (i >= 0 ? '--sw:' + PAL[i] : '') + '" data-a="sel" data-k="' + selKey + '" data-v="' + esc(r.key) + '"' + (i < 0 && sel.length >= 6 ? ' disabled' : '') + '>' + esc(r.l) + '</button>';
      }).join('') + '</div>';
    }
    function radarMulti(rows) {
      var keys = rows.map(function (r) { return r.key; });
      if (!st.rsel) st.rsel = defaultSel(rows);
      st.rsel = st.rsel.filter(function (k) { return keys.indexOf(k) >= 0; });
      var sel = st.rsel, hs = hpl(), n = hs.length, legend = selChips(rows, 'rsel');
      if (n < 3) return legend + '<p class="edx-empty">Cần ít nhất 3 ' + dimWord() + ' để vẽ radar.</p>';
      var all = M.group(khoiList()), vs = [];
      rows.forEach(function (r) { if (sel.indexOf(r.key) >= 0) hs.forEach(function (h) { if (r.g.hp[h]) vs.push(r.g.hp[h].avg); }); });
      hs.forEach(function (h) { if (all.hp[h]) vs.push(all.hp[h].avg); });
      var lo = Math.max(0, Math.min(60, Math.floor((Math.min.apply(null, vs.concat([100])) - 8) / 10) * 10));
      var cx = W / 2, cy = H / 2 + 6, R = Math.max(60, Math.min(W / 2 - 90, H / 2 - 34)), o = '';
      function pt(i, v) { var a = -Math.PI / 2 + 2 * Math.PI * i / n, r = R * Math.max(0, v - lo) / (100 - lo); return [cx + r * Math.cos(a), cy + r * Math.sin(a)]; }
      [0.25, 0.5, 0.75, 1].forEach(function (f) { var v = lo + (100 - lo) * f; o += '<polygon class="ring" points="' + hs.map(function (h, i) { return pt(i, v).join(','); }).join(' ') + '"/><text x="' + (cx + 4) + '" y="' + (cy - R * f + 12) + '" font-size="10">' + Math.round(v) + '</text>'; });
      hs.forEach(function (h, i) {
        var p = pt(i, 100), q = pt(i, 100 + 0.2 * (100 - lo)), c = Math.cos(-Math.PI / 2 + 2 * Math.PI * i / n);
        o += '<line class="ax" x1="' + cx + '" y1="' + cy + '" x2="' + p[0] + '" y2="' + p[1] + '"/><text class="xl" x="' + q[0] + '" y="' + (q[1] + 4) + '" text-anchor="' + (Math.abs(c) < 0.2 ? 'middle' : c > 0 ? 'start' : 'end') + '">' + esc(h) + '</text>';
      });
      o += '<polygon class="ref2" points="' + hs.map(function (h, i) { return pt(i, all.hp[h] ? all.hp[h].avg : lo).join(','); }).join(' ') + '"/>';
      sel.forEach(function (k, j) {
        var r = rows.filter(function (x) { return x.key === k; })[0]; if (!r) return;
        var p2 = hs.map(function (h, i) { return pt(i, r.g.hp[h] ? r.g.hp[h].avg : lo); });
        o += '<polygon points="' + p2.map(function (p) { return p.join(','); }).join(' ') + '" style="fill:' + PAL[j] + ';fill-opacity:.1;stroke:' + PAL[j] + '" stroke-width="2.4"/>' + p2.map(function (p) { return '<circle cx="' + p[0] + '" cy="' + p[1] + '" r="3.5" style="fill:' + PAL[j] + '"/>'; }).join('');
      });
      return legend + frame(o) + '<p class="edx-legend">Nét đứt đen: TB ' + esc(khoiName(st.khoi)) + ' · trục bắt đầu từ ' + lo + ' điểm.</p>';
    }
    // biểu đồ đường theo tuần: series = [{name, vals[], color, dash, bold}]
    function lineChart(series, unit) {
      var all = []; series.forEach(function (s) { vals(s.vals).forEach(function (v) { all.push(v); }); });
      if (!all.length || M.nW < 2) return '<p class="edx-empty">Cần dữ liệu từ 2 tuần trở lên để vẽ xu hướng.</p>';
      var ml = 40, mr = 44, mt = 16, mb = 42, pw = W - ml - mr, ph = H - mt - mb, n = M.nW;
      var lo = Math.max(0, Math.floor((Math.min.apply(null, all) - 4) / 5) * 5), hi = Math.min(100, Math.ceil((Math.max.apply(null, all) + 3) / 5) * 5);
      if (hi - lo < 10) hi = Math.min(100, lo + 10);
      function X(i) { return ml + (i + 0.5) / n * pw; }
      function Y(v) { return mt + (hi - v) / (hi - lo) * ph; }
      var o = '', step = niceStep((hi - lo) / 5), y;
      for (y = Math.ceil(lo / step) * step; y <= hi; y += step) o += '<line class="grid" x1="' + ml + '" x2="' + (ml + pw) + '" y1="' + Y(y) + '" y2="' + Y(y) + '"/><text x="' + (ml - 6) + '" y="' + (Y(y) + 4) + '" text-anchor="end">' + num(y) + '</text>';
      M.weeks.forEach(function (w, i) { o += '<text class="xl" x="' + X(i) + '" y="' + (mt + ph + 16) + '" text-anchor="middle">' + esc(w.label) + '</text><text class="ttl" x="' + X(i) + '" y="' + (mt + ph + 30) + '" text-anchor="middle" font-size="10.5">' + esc(w.range) + '</text>'; });
      series.forEach(function (s) {
        var pts = []; s.vals.forEach(function (v, i) { if (v != null) pts.push([X(i), Y(v), v, i]); });
        if (!pts.length) return;
        o += '<polyline points="' + pts.map(function (p) { return p[0].toFixed(1) + ',' + p[1].toFixed(1); }).join(' ') + '" fill="none" style="stroke:' + s.color + '" stroke-width="' + (s.bold ? 3.2 : 2.2) + '"' + (s.dash ? ' stroke-dasharray="6 4"' : '') + ' stroke-linejoin="round"/>';
        if (!s.dash) pts.forEach(function (p) { o += '<circle cx="' + p[0].toFixed(1) + '" cy="' + p[1].toFixed(1) + '" r="' + (s.bold ? 4.5 : 3.4) + '" style="fill:' + s.color + '"><title>' + esc(s.name) + ' · ' + esc(M.weeks[p[3]].label) + ': ' + num(p[2], 1) + (unit || '') + '</title></circle>'; });
        var e = pts[pts.length - 1];
        o += '<text class="val" x="' + (e[0] + 8) + '" y="' + (e[1] + 4) + '" style="fill:' + s.color + '">' + Math.round(e[2]) + '</text>';
      });
      return frame(o);
    }
    function trendView(rows) {
      var keys = rows.map(function (r) { return r.key; });
      if (!st.tsel) st.tsel = defaultSel(rows);
      st.tsel = st.tsel.filter(function (k) { return keys.indexOf(k) >= 0; });
      var all = M.group(khoiList()), ser = [{ name: 'TB ' + khoiName(st.khoi), vals: all.wavg, color: 'var(--ink)', dash: true }];
      st.tsel.forEach(function (k, j) { var r = rows.filter(function (x) { return x.key === k; })[0]; if (r) ser.push({ name: r.l, vals: r.g.wavg, color: PAL[j], bold: false }); });
      return selChips(rows, 'tsel') + lineChart(ser) + '<p class="edx-legend">Nét đứt đen: TB ' + esc(khoiName(st.khoi)) + ' · điểm TB mỗi tuần của các bé có chơi tuần đó.</p>';
    }
    function progView(rows) {
      if (M.nW < 2) return '<p class="edx-empty">Cần dữ liệu từ 2 tuần trở lên.</p>';
      var key = st.prog === 'prev' ? 'dPrev' : 'dFirst';
      var items = rows.map(function (r) {
        var ds = vals(r.g.sc.map(function (x) { return x[key]; }));
        return { r: r, v: mean(ds), up: ds.filter(function (v) { return v >= UP; }).length, dn: ds.filter(function (v) { return v <= -UP; }).length, n: ds.length };
      }).sort(function (a, b) { return (b.v == null ? -99 : b.v) - (a.v == null ? -99 : a.v); });
      var mx = Math.max(5, Math.max.apply(null, items.map(function (i) { return i.v == null ? 0 : Math.abs(i.v); })));
      return '<div class="edx-sub"><small>So với:</small>' + chips([['first', 'tuần đầu'], ['prev', 'tuần trước']], st.prog, 'prog') + '<small style="margin-left:auto">điểm TB thay đổi của từng bé (ghép cặp) · ▲ ≥ +' + UP + ' · ▼ ≤ −' + UP + '</small></div>' +
        '<div class="edx-scroll">' + items.map(function (it) {
          var w = it.v == null ? 0 : Math.abs(it.v) / mx * 50;
          return '<div class="edx-pr" data-a="lop" data-khoi="' + esc(it.r.k) + '" data-lop="' + esc(it.r.l) + '"><span class="nm">' + esc(it.r.l) + '</span><div class="edx-div"><i class="mid"></i>' + (it.v == null ? '' : '<i class="' + (it.v >= 0 ? 'pos' : 'neg') + '" style="width:' + w + '%"></i>') + '</div>' +
            '<b>' + delta(it.v) + '</b><small>▲ ' + it.up + ' · ▬ ' + (it.n - it.up - it.dn) + ' · ▼ ' + it.dn + '</small></div>';
        }).join('') + '</div>';
    }
    function partView(rows) {
      if (!M.nW) return '<p class="edx-empty">Chưa có dữ liệu theo tuần.</p>';
      var g = M.group(khoiList());
      function cells(gr) { return M.weeks.map(function (w, i) { var p = gr.n ? 100 * gr.wpart[i] / gr.n : 0; return '<td class="pc" title="' + gr.wpart[i] + '/' + gr.n + ' bé"><span style="opacity:' + (0.12 + 0.88 * p / 100).toFixed(2) + '"></span><b>' + Math.round(p) + '%</b></td>'; }).join(''); }
      return '<div class="edx-scroll"><table class="edx-heat"><thead><tr><th class="l">Lớp</th>' + M.weeks.map(function (w) { return '<th title="' + esc(w.range) + '">' + esc(w.label) + '<br><small>' + esc(w.range) + '</small></th>'; }).join('') + '<th>Câu / bé</th><th>Bé vắng ≥ 2 tuần</th></tr></thead><tbody>' +
        rows.map(function (r) {
          return '<tr data-a="lop" data-khoi="' + esc(r.k) + '" data-lop="' + esc(r.l) + '"><td class="nm">' + esc(r.l) + '</td>' + cells(r.g) + '<td>' + num(r.g.perCap) + '</td><td>' + r.g.nIdle + '/' + r.g.n + '</td></tr>';
        }).join('') + '</tbody><tfoot><tr><td class="nm">TB ' + esc(khoiName(st.khoi)) + '</td>' + cells(g) + '<td>' + num(g.perCap) + '</td><td>' + g.nIdle + '/' + g.n + '</td></tr></tfoot></table></div>' +
        '<p class="edx-legend">Ô = % bé của lớp có chơi trong tuần (đậm = nhiều). Điểm của tuần chỉ đáng tin khi nhiều bé tham gia.</p>';
    }
    function actView(rows) {
      var items = rows.slice().sort(function (a, b) { return (b.g.perCap || 0) - (a.g.perCap || 0); });
      return '<div class="edx-scroll"><div class="edx-dleg"><span><i class="p2"></i>Chơi đều</span><span><i class="p1"></i>Chơi ít (&lt; ' + M.LOWPLAY + ' câu' + (M.nW >= 3 ? ' hoặc ≤ 1 tuần' : '') + ')</span><span><i class="p0"></i>Chưa chơi</span><small>xếp theo số câu / bé · số trong ô = số bé</small></div>' +
        items.map(function (r) {
          return '<div class="edx-ar" data-a="lop" data-khoi="' + esc(r.k) + '" data-lop="' + esc(r.l) + '"><span class="nm">' + esc(r.l) + '</span>' + playStack(r.g, true) + '<b>' + num(r.g.perCap) + '<small> câu/bé</small></b><small>' + r.g.ns + '/' + r.g.n + ' đã chơi</small></div>';
        }).join('') + '</div>';
    }
    function distView(rows) {
      var items = rows.slice().sort(function (a, b) { return (b.g.pctOk || 0) - (a.g.pctOk || 0) || (b.g.avg || 0) - (a.g.avg || 0); });
      return '<div class="edx-scroll"><div class="edx-dleg">' + [3, 2, 1, 0].map(function (i) { return '<span><i class="lv' + i + '"></i>' + LV[i] + '</span>'; }).join('') + '<small>xếp theo tỉ lệ đạt mục tiêu · số trong ô = số bé</small></div>' +
        items.map(function (r) {
          return '<div class="edx-dr" data-a="lop" data-khoi="' + esc(r.k) + '" data-lop="' + esc(r.l) + '"><span class="nm">' + esc(r.l) + '</span>' + stack(r.g, true) + '<b>' + num(r.g.pctOk) + '%</b><small>' + r.g.ns + '/' + r.g.n + ' bé</small></div>';
        }).join('') + '</div>';
    }
    function heatView(rows) {
      var g = M.group(khoiList()), D = hpl();
      return '<div class="edx-scroll"><table class="edx-heat"><thead><tr><th class="l">Lớp</th>' + D.map(function (h) { return '<th>' + esc(h) + '</th>'; }).join('') + '<th>' + (st.mon === 'all' ? 'Tất cả môn' : 'Điểm TB') + '</th></tr></thead><tbody>' +
        rows.map(function (r) {
          return '<tr data-a="lop" data-khoi="' + esc(r.k) + '" data-lop="' + esc(r.l) + '"><td class="nm">' + esc(r.l) + '</td>' + D.map(function (h) { return heatAvg(r.g, h); }).join('') +
            (r.g.avg == null ? '<td class="n">–</td>' : '<td class="h lv' + r.g.lv + '"><b>' + Math.round(r.g.avg) + '</b></td>') + '</tr>';
        }).join('') + '</tbody><tfoot><tr><td class="nm">TB ' + esc(khoiName(st.khoi)) + '</td>' + D.map(function (h) { return heatAvg(g, h); }).join('') + '<td class="h lv' + (g.lv == null ? 0 : g.lv) + '"><b>' + Math.round(g.avg) + '</b></td></tr></tfoot></table></div>';
    }
    function tableView(rows) {
      var D = hpl();
      var G = {
        name: function (r) { return r.l; }, ns: function (r) { return r.g.pctPlayed; }, avg: function (r) { return r.g.avg; }, pctOk: function (r) { return r.g.pctOk; }, low: function (r) { return r.g.dist[0] + r.g.dist[1]; },
        d: function (r) { return r.g.dFirst; }, part: function (r) { return r.g.partLast; }, cap: function (r) { return r.g.perCap; }
      };
      D.forEach(function (h) { G['hp:' + h] = function (r) { return r.g.hp[h] ? r.g.hp[h].avg : null; }; });
      var items = sortList(rows, 'lop', G);
      return '<div class="edx-scroll"><table><thead><tr>' + th('Lớp', 'lop', 'name', 'l') + th('Đã chơi', 'lop', 'ns') + th('Câu/bé', 'lop', 'cap') + th('Điểm TB', 'lop', 'avg') + th('Tiến bộ', 'lop', 'd', '', 'So với tuần đầu') + '<th>Xu hướng</th><th>Phân bố mức</th>' +
        D.map(function (h) { return th(esc(h), 'lop', 'hp:' + h); }).join('') + th('Đạt mục tiêu', 'lop', 'pctOk') + th('Cần hỗ trợ', 'lop', 'low') + th('Tuần gần nhất', 'lop', 'part', '', 'Tham gia tuần gần nhất') + '</tr></thead><tbody>' +
        items.map(function (r) {
          return '<tr data-a="lop" data-khoi="' + esc(r.k) + '" data-lop="' + esc(r.l) + '"><td class="nm">' + esc(r.l) + '</td><td>' + r.g.ns + '/' + r.g.n + '</td><td>' + num(r.g.perCap) + '</td>' +
            '<td>' + (r.g.avg == null ? '–' : '<b>' + num(r.g.avg) + '</b>') + '</td><td>' + delta(r.g.dFirst) + '</td><td>' + spark(r.g.wavg, 64, 22) + '</td><td style="min-width:8em">' + stack(r.g) + '</td>' + D.map(function (h) { return heatAvg(r.g, h); }).join('') +
            '<td>' + num(r.g.pctOk) + '%</td><td>' + (r.g.dist[0] + r.g.dist[1]) + '</td><td>' + num(r.g.partLast) + '%</td></tr>';
        }).join('') + '</tbody></table></div>';
    }

    // ---------- khung chung ----------
    function isDark() { return st.theme === 'dark' || (st.theme === 'auto' && global.matchMedia && global.matchMedia('(prefers-color-scheme: dark)').matches); }
    function header() {
      var d = M.d, logo = opts.logo ? '<img class="edx-logo" src="' + esc(opts.logo) + '" alt="EduXplore">' : '<div class="edx-wordmark">Edu<i>X</i>plore</div>';
      var mons = M.monIds.length > 1 && st.tab !== 'tq' ? '<span class="edx-monsel"><small>Môn:</small>' + chips([['all', 'Tất cả môn']].concat(M.monIds.map(function (id) { return [id, id]; })), st.mon, 'mon') + '</span>' : '';
      var T = [['tq', 'Toàn trường'], ['khoi', 'Theo khối'], ['lop', 'Theo lớp'], ['hs', 'Từng học sinh']];
      var sub = st.tab === 'tq' ? 'Xếp hạng lớp trong từng khối' : monLabel();
      return '<header class="edx-top"><div class="edx-brand">' + logo + '<div><h1>Báo cáo kết quả học tập</h1><p>' + esc(sub) + ' · Kỳ ' + esc(d.ky.label) + (M.nW ? ' · ' + M.nW + ' tuần' : '') + '</p></div></div>' +
        '<nav class="edx-tabs" role="tablist">' + T.map(function (t) { return '<button type="button" role="tab" class="edx-tab' + (st.tab === t[0] ? ' on' : '') + '" aria-selected="' + (st.tab === t[0]) + '" data-a="tab" data-v="' + t[0] + '">' + t[1] + '</button>'; }).join('') + '</nav>' +
        '<div class="edx-tools">' + (d.sample ? '<span class="edx-sample">Dữ liệu minh hoạ</span>' : '') + mons + '<button type="button" class="edx-chip sm" data-a="theme">' + (isDark() ? 'Nền sáng' : 'Nền tối') + '</button></div></header>';
    }
    function footer() {
      var F = M.F, c = M.cuts;
      return '<footer class="edx-foot"><span>Mỗi khối học chương trình riêng: chỉ xếp hạng / so sánh trong cùng khối. Điểm = ' + F.base + ' × tỉ lệ đúng × hệ số tốc độ (tối đa 100; hệ số ' + num(F.kmax, 2) + ' nếu TB ≤ ' + F.tfast + 's, 1,0 nếu ≥ ' + F.tslow + 's). "Tất cả môn" = TB điểm các môn bé đã chơi. Tiến bộ = tuần có chơi gần nhất so với tuần có chơi đầu tiên của từng bé. Chơi ít = dưới ' + M.LOWPLAY + ' câu' + (M.nW >= 3 ? ' hoặc chỉ 1 tuần' : '') + '.</span>' +
        '<span>Mức: &lt; ' + c[0] + ' Mức 1 · ' + c[0] + '–' + c[1] + ' Mức 2 · ' + c[1] + '–' + c[2] + ' Mức 3 · ≥ ' + c[2] + ' Đạt mục tiêu. Ô mờ / dấu *: dưới ' + F.low_sample + ' câu (ít mẫu).</span></footer>';
    }
    function kpi(items) { return '<div class="edx-kpi">' + items.map(function (t) { return '<div><small>' + t[0] + '</small><b>' + t[1] + '</b></div>'; }).join('') + '</div>'; }
    function progKpi(g) { return g.nTrend ? delta(g.dFirst) + '<em>▲ ' + g.up + ' · ▼ ' + g.down + ' bé</em>' : '–'; }
    function khoiPicker() {
      return '<div class="edx-picker"><div class="g"><small>Khối</small>' + chips(M.khois.map(function (k) { return [k, khoiName(k)]; }), st.khoi, 'khoi') + '</div><small class="edx-hint">Chỉ so sánh các lớp trong cùng khối.</small></div>';
    }

    // ---------- tab: toàn trường (xếp hạng lớp trong từng khối, từng môn) ----------
    function viewTq() {
      var cols = M.monIds.length > 1 ? [['all', 'Tất cả môn']].concat(M.monIds.map(function (id) { return [id, id]; })) : [[M.monIds[0], 'Điểm TB']];
      if (!st.sort.tq.k) st.sort.tq.k = 'm:' + cols[0][0];
      return '<div class="edx-tq">' + M.khois.map(function (k) { return tqPanel(k, cols); }).join('') + '</div>';
    }
    function tqPanel(k, cols) {
      var rows = M.lopsOf[k].map(function (l) {
        var g = {}; cols.forEach(function (c) { g[c[0]] = M.group(M.inLop(k, l, c[0])); });
        return { k: k, l: l, g: g, ga: M.group(M.inLop(k, l, 'all')) };
      });
      var rank = {};
      cols.forEach(function (c) {
        var arr = rows.filter(function (r) { return r.g[c[0]].avg != null; }).sort(function (a, b) { return b.g[c[0]].avg - a.g[c[0]].avg; });
        rank[c[0]] = { by: {}, top: arr[0] || null, n: arr.length };
        arr.forEach(function (r, i) { rank[c[0]].by[r.l] = i + 1; });
      });
      var G = { name: function (r) { return r.l; }, d: function (r) { return r.ga.dFirst; }, played: function (r) { return r.ga.pctPlayed; }, cap: function (r) { return r.ga.perCap; } };
      cols.forEach(function (c) { G['m:' + c[0]] = function (r) { return r.g[c[0]].avg; }; });
      var items = sortList(rows, 'tq', G), gk = M.group(M.inKhoi(k, 'all')), hasW = M.nW >= 2;
      var most = rows.slice().sort(function (a, b) { return (b.ga.perCap || 0) - (a.ga.perCap || 0); }), least = most[most.length - 1];
      var noneByLop = rows.filter(function (r) { return r.ga.nNone > 0; }).sort(function (a, b) { return b.ga.nNone - a.ga.nNone; });
      var o = '<section class="edx-panel"><h2><a class="lk" data-a="khoi" data-v="' + esc(k) + '">' + esc(khoiName(k)) + '</a><small>' + M.lopsOf[k].length + ' lớp · ' + gk.ns + '/' + gk.n + ' bé đã chơi · xếp hạng trong khối</small></h2>' +
        '<div class="edx-scroll"><table class="edx-rank"><thead><tr>' + th('Lớp', 'tq', 'name', 'l') +
        cols.map(function (c) { return th(esc(c[1]), 'tq', 'm:' + c[0], '', 'Điểm TB ' + c[1] + ' · ★ = đứng đầu khối'); }).join('') + (hasW ? th('Tiến bộ', 'tq', 'd', '', 'Tất cả môn, so với tuần đầu') : '') +
        th('Mức độ chơi', 'tq', 'played', '', 'Chơi đều / chơi ít / chưa chơi') + th('Câu/bé', 'tq', 'cap') + '</tr></thead><tbody>' +
        items.map(function (r) {
          return '<tr data-a="lop" data-khoi="' + esc(k) + '" data-lop="' + esc(r.l) + '"><td class="nm">' + esc(r.l) + '</td>' + cols.map(function (c) {
            var g = r.g[c[0]], rk = rank[c[0]].by[r.l];
            return g.avg == null ? '<td class="n">–</td>' : '<td class="h lv' + g.lv + (rk === 1 ? ' top' : '') + '"><b>' + Math.round(g.avg) + '</b><sup>' + (rk === 1 ? '★' : '#' + rk) + '</sup></td>';
          }).join('') + (hasW ? '<td>' + delta(r.ga.dFirst) + '</td>' : '') + '<td style="min-width:6.5em" title="' + r.ga.ns + '/' + r.ga.n + ' bé đã chơi">' + playStack(r.ga) + '<small class="pp">' + r.ga.nNone + ' chưa chơi</small></td><td>' + num(r.ga.perCap) + '</td></tr>';
        }).join('') + '</tbody><tfoot><tr><td class="nm">TB khối</td>' + cols.map(function (c) { var g = M.group(M.inKhoi(k, c[0])); return g.avg == null ? '<td class="n">–</td>' : '<td class="h lv' + g.lv + '"><b>' + Math.round(g.avg) + '</b></td>'; }).join('') +
        (hasW ? '<td>' + delta(gk.dFirst) + '</td>' : '') + '<td>' + playStack(gk) + '</td><td>' + num(gk.perCap) + '</td></tr></tfoot></table></div>' +
        '<div class="edx-top3"><small>Đứng đầu khối:</small>' + cols.map(function (c) {
          var t = rank[c[0]].top; if (!t) return '';
          return '<span class="tp"><b>' + esc(c[1]) + '</b> <a data-a="lop" data-khoi="' + esc(k) + '" data-lop="' + esc(t.l) + '">★ ' + esc(t.l) + '</a> <em>' + Math.round(t.g[c[0]].avg) + '</em></span>';
        }).join('') + '</div>' +
        '<ul class="edx-attn"><li>Chơi nhiều nhất: <b>' + esc(most[0].l) + '</b> (' + num(most[0].ga.perCap) + ' câu/bé) · ít nhất: <b>' + esc(least.l) + '</b> (' + num(least.ga.perCap) + ' câu/bé)</li>' +
        '<li>Chưa chơi: <b>' + gk.nNone + '</b> bé' + (noneByLop.length ? ' — nhiều nhất ' + noneByLop.slice(0, 3).map(function (r) { return '<a data-a="lop" data-khoi="' + esc(k) + '" data-lop="' + esc(r.l) + '">' + esc(r.l) + '</a> (' + r.ga.nNone + ')'; }).join(', ') : '') + ' · chơi ít: <b>' + gk.nFew + '</b> bé</li>' +
        '<li>Điểm thấp (Mức 1–2, tất cả môn): <b>' + (gk.dist[0] + gk.dist[1]) + '</b> bé (' + num(gk.pctLow) + '%)</li></ul></section>';
      return o;
    }

    // ---------- tab: theo khối ----------
    function viewKhoi() {
      var g = M.group(khoiList()), rows = lopRows(), kn = khoiName(st.khoi), D = hpl();
      var best = rows.filter(function (r) { return r.g.avg != null; }).sort(function (a, b) { return b.g.avg - a.g.avg; });
      var prog = rows.filter(function (r) { return r.g.dFirst != null; }).sort(function (a, b) { return b.g.dFirst - a.g.dFirst; });
      var play = rows.slice().sort(function (a, b) { return (b.g.perCap || 0) - (a.g.perCap || 0); });
      var noneByLop = rows.filter(function (r) { return r.g.nNone > 0; }).sort(function (a, b) { return b.g.nNone - a.g.nNone; });
      var o = khoiPicker() + kpi([['Đã chơi', g.ns + '<em> / ' + g.n + ' bé</em>'], ['Điểm TB ' + esc(kn.toLowerCase()), num(g.avg)], ['Tiến bộ so với tuần đầu', progKpi(g)],
        ['Tham gia tuần gần nhất', M.nW ? num(g.partLast) + '%<em>' + g.wpart[M.nW - 1] + '/' + g.n + ' bé</em>' : '–'], ['Chưa chơi', g.nNone + '<em>' + g.nFew + ' chơi ít</em>'], ['Cần hỗ trợ (Mức 1–2)', (g.dist[0] + g.dist[1]) + '<em>' + num(g.pctLow) + '%</em>']]);

      var sub = '';
      if (st.cmp === 'bar') {
        var ms = [['avg', 'Điểm TB'], ['pctOk', '% đạt mục tiêu'], ['pctLow', '% cần hỗ trợ'], ['pctPlayed', '% đã chơi']].concat(M.nW ? [['partLast', '% chơi tuần gần nhất']] : []).concat(D.map(function (h) { return ['hp:' + h, h]; }));
        sub = '<div class="edx-sub"><small>Chỉ số:</small>' + chips(ms, st.metric, 'metric') + '<label class="edx-chk"><input type="checkbox" id="edx-sort" data-a="cmpsort"' + (st.cmpSort ? ' checked' : '') + '> xếp giảm dần</label></div>';
      }
      var body = { bar: barChart, heat: heatView, dist: distView, box: boxChart, radar: radarMulti, xy: xyChart, trend: trendView, prog: progView, part: partView, act: actView, table: tableView }[st.cmp](rows);
      var hint = { bar: 'click cột để mở lớp', heat: 'màu = mức điểm', dist: 'số bé ở từng mức', box: 'hộp = 50% bé ở giữa, vạch = trung vị, chấm = từng bé', radar: 'hình dạng điểm mạnh/yếu',
        xy: 'góc trên bên phải = nhanh và chính xác', trend: 'điểm TB của lớp qua từng tuần', prog: 'lớp nào tiến bộ, lớp nào đi xuống', part: 'lớp nào chơi đều, lớp nào bỏ', act: 'lớp nào chơi nhiều / ít / chưa chơi', table: 'click tiêu đề để sắp xếp' }[st.cmp];
      var kinds = CMP.map(function (g2) {
        var its = g2[1].filter(function (x) { return (M.nW >= 2 || (x[0] !== 'trend' && x[0] !== 'prog')) && (M.nW || x[0] !== 'part'); });
        return its.length ? '<span class="grp"><small>' + g2[0] + ':</small>' + chips(its, st.cmp, 'cmp') + '</span>' : '';
      }).join('');
      o += '<div class="edx-fill khoi"><section class="edx-panel"><h2>So sánh các lớp<small>' + esc(kn) + ' · ' + rows.length + ' lớp · ' + esc(monLabel()) + ' · ' + hint + '</small></h2>' +
        '<div class="edx-sub kinds">' + kinds + '</div>' + sub + '<div class="edx-body">' + body + '</div></section>' +
        '<aside class="edx-col edx-scroll"><section><h2>Phân bố mức<small>' + esc(kn) + '</small></h2>' + dist(g) + '</section>' +
        '<section><h2>Điểm TB từng ' + dimWord() + '</h2>' + hpBars(g) + '</section>' +
        '<section><h2>Cần chú ý</h2><ul class="edx-attn">' +
        '<li>Chơi nhiều nhất: <b>' + esc(play[0].l) + '</b> (' + num(play[0].g.perCap) + ' câu/bé) · ít nhất: <b>' + esc(play[play.length - 1].l) + '</b> (' + num(play[play.length - 1].g.perCap) + ')</li>' +
        '<li>Bé chưa chơi: <b>' + g.nNone + '</b>' + (noneByLop.length ? ' — ' + noneByLop.slice(0, 4).map(function (r) { return '<a data-a="lop" data-khoi="' + esc(r.k) + '" data-lop="' + esc(r.l) + '">' + esc(r.l) + '</a> (' + r.g.nNone + ')'; }).join(', ') : '') + '</li>' +
        (best.length > 1 ? '<li>Điểm cao nhất: <b>' + esc(best[0].l) + '</b> (' + num(best[0].g.avg) + ') · thấp nhất: <b>' + esc(best[best.length - 1].l) + '</b> (' + num(best[best.length - 1].g.avg) + ')</li>' : '') +
        (prog.length > 1 ? '<li>Tiến bộ nhất: <b>' + esc(prog[0].l) + '</b> ' + delta(prog[0].g.dFirst) + ' · ít nhất: <b>' + esc(prog[prog.length - 1].l) + '</b> ' + delta(prog[prog.length - 1].g.dFirst) + '</li>' : '') +
        '<li>' + (g.dist[0] + g.dist[1]) + ' bé ở Mức 1–2 cần hỗ trợ thêm (' + num(g.pctLow) + '%).</li>' +
        '<li>' + dimWord().replace(/^./, function (c) { return c.toUpperCase(); }) + ' yếu nhất: <b>' + (g.weak ? esc(g.weak) + '</b> (' + Math.round(g.hp[g.weak].avg) + ' điểm)' : '–</b>') + '</li>' +
        (M.nW >= 2 ? '<li>' + g.nDown + ' bé giảm ≥ ' + DOWN + ' điểm so với tuần trước · ' + g.nIdle + ' bé không chơi từ 2 tuần.</li>' : '') + '</ul></section></aside></div>';
      return o;
    }

    // ---------- tab: theo lớp ----------
    function scatter(list) {
      var pts = list.filter(function (x) { return x.m && x.t != null && x.acc != null; });
      if (!pts.length) return '<p class="edx-empty">Chưa có đủ dữ liệu để vẽ.</p>';
      var ml = 50, mr = 16, mt = 14, mb = 46, pw = W - ml - mr, ph = H - mt - mb;
      var ts = pts.map(function (p) { return p.t; }), as = pts.map(function (p) { return p.acc; });
      var tmin = Math.max(1, Math.min.apply(null, ts) * 0.85), tmax = Math.max.apply(null, ts) * 1.1;
      if (tmax - tmin < 2) tmax = tmin + 2;
      var ymin = Math.min(60, Math.floor((Math.min.apply(null, as) - 5) / 10) * 10), ymax = 100;
      if (ymin < 0) ymin = 0;
      function X(t) { return ml + (tmax - t) / (tmax - tmin) * pw; }
      function Y(a) { return mt + (ymax - a) / (ymax - ymin) * ph; }
      var tfast = M.F.tfast, o = '', y, ys = ymax - ymin > 50 ? 20 : 10;
      for (y = Math.ceil(ymin / ys) * ys; y <= ymax; y += ys) o += '<line class="grid" x1="' + ml + '" x2="' + (W - mr) + '" y1="' + Y(y) + '" y2="' + Y(y) + '"/><text x="' + (ml - 8) + '" y="' + (Y(y) + 4) + '" text-anchor="end">' + y + '</text>';
      var st2 = niceStep((tmax - tmin) / 7), t;
      for (t = Math.ceil(tmin / st2) * st2; t <= tmax; t += st2) o += '<line class="grid" x1="' + X(t) + '" x2="' + X(t) + '" y1="' + mt + '" y2="' + (mt + ph) + '"/><text x="' + X(t) + '" y="' + (mt + ph + 16) + '" text-anchor="middle">' + num(t, st2 < 1 ? 1 : 0) + '</text>';
      if (tfast > tmin && tfast < tmax && 75 > ymin) {
        o += '<rect class="good" x="' + X(tfast) + '" y="' + Y(ymax) + '" width="' + (ml + pw - X(tfast)) + '" height="' + (Y(75) - Y(ymax)) + '"/>' +
          '<line class="quad" x1="' + X(tfast) + '" x2="' + X(tfast) + '" y1="' + mt + '" y2="' + (mt + ph) + '"/><line class="quad" x1="' + ml + '" x2="' + (W - mr) + '" y1="' + Y(75) + '" y2="' + Y(75) + '"/>' +
          '<text x="' + (W - mr - 6) + '" y="' + (mt + 16) + '" text-anchor="end" style="fill:var(--lv3)">Nhanh &amp; chính xác</text>';
      }
      var nlab = 0;
      pts.slice().sort(function (a, b) { return a.score - b.score; }).forEach(function (p) {
        var lab = (p.lv <= 1 || p.acc < 70) && nlab < 10; if (lab) nlab++;
        o += '<g data-a="code" data-code="' + esc(p.code) + '" class="clk"><title>' + esc(p.alias) + ': ' + Math.round(p.score) + ' điểm · ' + num(p.acc) + '% đúng · ' + num(p.t, 1) + 's</title><circle class="hit" cx="' + X(p.t) + '" cy="' + Y(p.acc) + '" r="13"/><circle class="dot' + (p.few ? ' hollow' : '') + '" cx="' + X(p.t) + '" cy="' + Y(p.acc) + '" r="6" style="--c:var(--lv' + p.lv + ')"/>' +
          (lab ? '<text class="xl" x="' + (X(p.t) + 9) + '" y="' + (Y(p.acc) + 4) + '">' + esc(p.alias) + '</text>' : '') + '</g>';
      });
      o += '<text class="ttl" x="' + (ml + pw / 2) + '" y="' + (H - 6) + '" text-anchor="middle">Tốc độ: giây/câu đúng (nhanh ở bên phải →)</text><text class="ttl" transform="translate(13 ' + (mt + ph / 2) + ') rotate(-90)" text-anchor="middle">Chính xác: % câu đúng</text>';
      return frame(o);
    }
    function viewLop() {
      var list = M.inLop(st.khoi, st.lop), g = M.group(list), gk = M.group(M.inKhoi(st.khoi)), D = hpl();
      var order = g.sc.slice().sort(function (a, b) { return b.score - a.score; }), rankOf = {};
      order.forEach(function (x, i) { rankOf[x.code] = i + 1; });
      var G = { name: function (x) { return x.alias; }, score: function (x) { return x.score; }, n: function (x) { return x.m.n; }, flags: function (x) { return x.flags.length; }, d: function (x) { return x.dFirst; } };
      D.forEach(function (h) { G['hp:' + h] = function (x) { return x.m.hp[h] ? x.m.hp[h].pt : null; }; });
      var rows = sortList(g.sc, 'rank', G), none = list.filter(function (x) { return !x.m; });
      var diff = g.avg != null && gk.avg != null ? g.avg - gk.avg : null;
      var lopAvgs = M.lopsOf[st.khoi].map(function (l) { return { l: l, a: M.group(M.inLop(st.khoi, l)).avg }; }).filter(function (r) { return r.a != null; }).sort(function (a, b) { return b.a - a.a; });
      var rk = lopAvgs.map(function (r) { return r.l; }).indexOf(st.lop) + 1;
      var o = picker() + kpi([['Lớp', esc(st.lop) + '<em>' + esc(khoiName(st.khoi)) + ' · ' + esc(monLabel()) + '</em>'], ['Đã chơi', g.ns + '<em> / ' + g.n + ' bé' + (g.nNone ? ' · chưa chơi ' + g.nNone : '') + '</em>'],
        ['Điểm TB lớp', num(g.avg) + (diff == null ? '' : '<em class="' + (diff >= 0 ? 'up' : 'dn') + '">' + (diff >= 0 ? '▲ +' : '▼ ') + num(diff, 1) + ' so với khối</em>')],
        ['Hạng trong khối', rk ? rk + '<em> / ' + lopAvgs.length + ' lớp</em>' : '–'], ['Tiến bộ so với tuần đầu', progKpi(g)], ['Tham gia tuần gần nhất', M.nW ? num(g.partLast) + '%<em>' + g.wpart[M.nW - 1] + '/' + g.n + ' bé</em>' : '–']]);
      var chartBody = st.lchart === 'trend' ? lineChart([{ name: 'TB ' + khoiName(st.khoi), vals: gk.wavg, color: 'var(--ink)', dash: true }, { name: 'Lớp ' + st.lop, vals: g.wavg, color: 'var(--red)', bold: true }]) +
          '<p class="edx-legend">Đỏ: lớp ' + esc(st.lop) + ' · đen đứt: TB ' + esc(khoiName(st.khoi)) + '</p>'
        : st.lchart === 'xy' ? scatter(list) : '<div class="edx-scroll">' + hpBars(g, gk, 'TB ' + khoiName(st.khoi)) + '</div>';
      var up = g.sc.filter(function (x) { return x.dFirst != null; }).sort(function (a, b) { return b.dFirst - a.dFirst; });
      var down = up.filter(function (x) { return x.dFirst < 0; }).slice(-3).reverse();
      var idle = list.filter(function (x) { return M.nW >= 2 && x.idle >= 2; });
      var lowest = g.sc.filter(function (x) { return x.lv <= 1; }).sort(function (a, b) { return a.score - b.score; });
      var few = g.sc.filter(function (x) { return x.play === 'few'; });
      var charts = (M.nW >= 2 ? [['trend', 'Xu hướng tuần']] : []).concat([['xy', 'Chính xác × tốc độ'], ['hp', dimWord() === 'môn' ? 'Môn' : 'Học phần']]);
      o += '<div class="edx-fill lop"><section class="edx-panel"><h2>Bảng xếp hạng<small>click tiêu đề để sắp xếp · click hàng để xem hồ sơ · ô mờ = ít mẫu</small></h2><div class="edx-scroll"><table><thead><tr><th>#</th>' + th('Bé', 'rank', 'name', 'l') + th('Điểm', 'rank', 'score') + th('Tiến bộ', 'rank', 'd', '', 'Tuần gần nhất so với tuần đầu') + '<th>Xu hướng</th>' +
        D.map(function (h) { return th(esc(h), 'rank', 'hp:' + h); }).join('') + th('Số câu', 'rank', 'n') + th('Cần chú ý', 'rank', 'flags', 'l') + '</tr></thead><tbody>' +
        rows.map(function (x) {
          return '<tr data-a="code" data-code="' + esc(x.code) + '"><td class="n">' + rankOf[x.code] + '</td><td class="nm">' + esc(x.alias) + '</td><td class="h lv' + x.lv + '">' + Math.round(x.score) + '</td><td>' + delta(x.dFirst) + '</td><td>' + spark(x.series, 64, 22) + '</td>' +
            D.map(function (h) { return heat(x.m, h); }).join('') + '<td>' + x.m.n + (x.few ? '*' : '') + '</td><td class="wrap">' + flagText(x) + '</td></tr>';
        }).join('') + '</tbody><tfoot><tr><td></td><td class="nm">TB lớp</td>' + (g.avg == null ? '<td class="n">–</td>' : '<td class="h lv' + g.lv + '">' + Math.round(g.avg) + '</td>') + '<td>' + delta(g.dFirst) + '</td><td>' + spark(g.wavg, 64, 22) + '</td>' +
        D.map(function (h) { return heatAvg(g, h); }).join('') + '<td>' + g.ns + '</td><td class="wrap" style="color:var(--muted)">TB ' + esc(khoiName(st.khoi)) + ': ' + num(gk.avg) + '</td></tr></tfoot></table>' +
        (none.length ? '<p class="edx-note edx-none"><b>Chưa chơi (' + none.length + '):</b> ' + nameLinks(none, null, 40) + '</p>' : '') + '</div></section>' +
        '<aside class="edx-col edx-scroll"><section class="edx-panel grow"><h2>' + chips(charts, st.lchart, 'lchart') + '</h2><div class="edx-body">' + chartBody + '</div></section>' +
        '<section><h2>Phân bố mức<small>lớp ' + esc(st.lop) + '</small></h2>' + dist(g) + '</section>' +
        '<section><h2>Cần chú ý</h2><ul class="edx-attn">' +
        '<li>Chưa chơi (' + none.length + '): ' + nameLinks(none, null, 6) + '</li>' +
        '<li>Chơi ít (' + few.length + '): ' + nameLinks(few, function (x) { return '<small class="mu">' + x.m.n + ' câu</small>'; }, 5) + '</li>' +
        '<li>Điểm thấp (Mức 1–2): ' + nameLinks(lowest, function (x) { return '<small class="mu">' + Math.round(x.score) + '</small>'; }, 5) + '</li>' +
        (M.nW >= 2 ? '<li>Tiến bộ nhất: ' + nameLinks(up.slice(0, 3), function (x) { return delta(x.dFirst); }) + '</li><li>Đi xuống: ' + nameLinks(down, function (x) { return delta(x.dFirst); }) + '</li><li>Không chơi từ 2 tuần: ' + nameLinks(idle, null, 4) + '</li>' : '') +
        '</ul></section></aside></div>';
      return o;
    }

    // ---------- tab: từng học sinh ----------
    function rosterList() {
      var base;
      if (st.q) {
        var f = fold(st.q);
        base = S().filter(function (x) { return fold(x.name + ' ' + x.alias + ' ' + x.code + ' ' + x.lop).indexOf(f) >= 0; }).slice(0, 150);
      } else base = M.inLop(st.khoi, st.lop).slice().sort(function (a, b) { return (b.m ? 1 : 0) - (a.m ? 1 : 0) || String(a.code).localeCompare(b.code); });
      if (st.filt === 'down') base = base.filter(function (x) { return x.dPrev != null && x.dPrev <= -DOWN; });
      else if (st.filt === 'idle') base = base.filter(function (x) { return M.nW >= 2 && x.idle >= 2; });
      else if (st.filt === 'low') base = base.filter(function (x) { return x.m && x.lv <= 1; });
      else if (st.filt === 'none') base = base.filter(function (x) { return !x.m; });
      return base;
    }
    function rosterHtml(list) {
      if (!list.length) return '<p class="edx-empty">Không có bé nào.</p>';
      return list.map(function (s) {
        var tr = s.dPrev == null ? '' : s.dPrev >= 1 ? '<i class="up">▲</i>' : s.dPrev <= -1 ? '<i class="dn">▼</i>' : '';
        return '<button type="button" data-a="code" data-code="' + esc(s.code) + '" class="' + (s.code === st.code ? 'on' : '') + '" title="' + esc(s.name + ' · ' + s.code) + '"><span class="n1">' + esc(s.alias) + (st.q ? '<small> ' + esc(s.lop) + '</small>' : '') + '</span>' +
          (s.m ? '<span class="sc lv' + s.lv + '">' + tr + Math.round(s.score) + '</span>' : '<span class="sc none">chưa chơi</span>') + '</button>';
      }).join('');
    }
    function viewHs() {
      var list = rosterList(), x = M.byCode(st.code);
      var filters = [['all', 'Tất cả'], ['none', 'Chưa chơi']].concat(M.nW >= 2 ? [['down', 'Giảm điểm'], ['idle', 'Ít chơi']] : []).concat([['low', 'Mức 1–2']]);
      return picker() + '<div class="edx-fill hs"><aside class="edx-panel"><h2>' + (st.q ? 'Tìm kiếm' : 'Lớp ' + esc(st.lop)) + '<small>' + list.length + ' bé</small></h2>' +
        '<input type="search" id="edx-q" class="edx-search" placeholder="Tìm tên / mã toàn trường…" value="' + esc(st.q) + '" autocomplete="off"><div class="edx-sub">' + chips(filters, st.filt, 'filt') + '</div>' +
        '<div class="edx-scroll edx-roster" id="edx-roster">' + rosterHtml(list) + '</div></aside>' +
        '<div class="edx-col edx-scroll">' + (x ? detail(x, list) : '<p class="edx-empty">Chưa chọn học sinh.</p>') + '</div></div>';
    }
    function hpSeries(x, h) { return x.hist.map(function (w) { return w && w.hp && w.hp[h] != null ? w.hp[h] : null; }); }
    function miniLine(v, ref, d) {
      var VW = 300, VH = 62, n = M.nW, P = vals(v).concat(vals(ref || []));
      if (!P.length) return '';
      var lo = Math.min.apply(null, P), hi = Math.max.apply(null, P); if (hi - lo < 6) { lo -= 3; hi += 3; }
      lo -= (hi - lo) * 0.12; hi += (hi - lo) * 0.28;
      function X(i) { return (i + 0.5) / n * VW; }
      function Y(a) { return 6 + (VH - 12) * (hi - a) / (hi - lo); }
      var o = '<svg viewBox="0 0 ' + VW + ' ' + VH + '" class="edx-mini" role="img">';
      if (ref) { var rp = []; ref.forEach(function (a, i) { if (a != null) rp.push(X(i).toFixed(1) + ',' + Y(a).toFixed(1)); }); if (rp.length > 1) o += '<polyline points="' + rp.join(' ') + '" class="rf"/>'; }
      var pp = []; v.forEach(function (a, i) { if (a != null) pp.push([X(i), Y(a), a, i]); });
      if (pp.length > 1) o += '<polyline points="' + pp.map(function (p) { return p[0].toFixed(1) + ',' + p[1].toFixed(1); }).join(' ') + '" class="ln"/>';
      pp.forEach(function (p, k) { var last = k === pp.length - 1; o += '<circle cx="' + p[0].toFixed(1) + '" cy="' + p[1].toFixed(1) + '" r="' + (last ? 4 : 2.8) + '" class="' + (last ? 'ed' : 'pt') + '"><title>' + esc(M.weeks[p[3]].label) + ': ' + num(p[2], d) + '</title></circle><text x="' + p[0].toFixed(1) + '" y="' + (p[1] - 7).toFixed(1) + '" text-anchor="middle" class="' + (last ? 'lb b' : 'lb') + '">' + num(p[2], d) + '</text>'; });
      return o + '</svg>';
    }
    function miniBars(v) {
      var VW = 300, VH = 62, n = M.nW, mx = Math.max.apply(null, vals(v).concat([1])), bw = VW / n * 0.46, o = '<svg viewBox="0 0 ' + VW + ' ' + VH + '" class="edx-mini" role="img">';
      v.forEach(function (a, i) {
        var cx = (i + 0.5) / n * VW, h = a == null ? 0 : (VH - 22) * a / mx, last = i === n - 1;
        if (a != null) o += '<rect x="' + (cx - bw / 2).toFixed(1) + '" y="' + (VH - 4 - h).toFixed(1) + '" width="' + bw.toFixed(1) + '" height="' + h.toFixed(1) + '" rx="1.5" class="' + (last ? 'bar ed' : 'bar') + '"/><text x="' + cx.toFixed(1) + '" y="' + (VH - 8 - h).toFixed(1) + '" text-anchor="middle" class="lb">' + Math.round(a) + '</text>';
      });
      return o + '</svg>';
    }
    function detail(x, list) {
      var gl = M.group(M.inLop(x.khoi, x.lop)), gk = M.group(M.inKhoi(x.khoi)), m = x.m, idx = list.indexOf(x), D = M.dimsOf(st.mon, x.khoi), dw = dimWord();
      var o = '<div class="edx-nav"><button type="button" class="edx-chip sm" data-a="code" data-code="' + esc(idx > 0 ? list[idx - 1].code : x.code) + '"' + (idx <= 0 ? ' disabled' : '') + '>◀ Bé trước</button>' +
        '<div class="edx-idbar"><div><small>Mã HS</small><b>' + esc(x.code) + '</b></div><div><small>Họ và tên</small><b>' + esc(x.name) + '</b></div><div><small>Tên gọi</small><b>' + esc(x.alias) + '</b></div><div><small>Lớp</small><b>' + esc(x.lop) + '</b></div><div><small>Kỳ báo cáo</small><b>' + esc(M.d.ky.label) + '</b></div></div>' +
        '<button type="button" class="edx-chip sm" data-a="code" data-code="' + esc(idx >= 0 && idx < list.length - 1 ? list[idx + 1].code : x.code) + '"' + (idx < 0 || idx >= list.length - 1 ? ' disabled' : '') + '>Bé sau ▶</button></div>';
      if (!m) return o + '<p class="edx-empty">Bé chưa chơi — chưa có kết quả ' + (st.mon === 'all' ? 'môn nào' : 'môn ' + esc(st.mon)) + ' trong kỳ này.</p>';
      var played = D.filter(function (h) { return m.hp[h]; }), multi = x.wIdx.length >= 2;
      var rank = gl.sc.slice().sort(function (a, b) { return b.score - a.score; }).indexOf(x) + 1;
      var below = gl.sc.filter(function (y) { return y.score < x.score; }).length, pctBeat = gl.ns > 1 ? Math.round(100 * below / (gl.ns - 1)) : null;
      var hpDelta = {};
      played.forEach(function (h) { var v = vals(hpSeries(x, h)); hpDelta[h] = v.length >= 2 ? v[v.length - 1] - v[0] : null; });

      // cột 1: phân tích kỹ năng + nhận định
      var c1 = '<section><h2>' + (st.mon === 'all' ? 'Phân tích theo môn' : 'Phân tích kỹ năng') + '<small>' + (st.mon === 'all' ? 'điểm TB từng môn bé đã chơi' : 'điểm = ' + M.F.base + ' × tỉ lệ đúng × hệ số tốc độ') + '</small></h2><div class="edx-tablewrap"><table><thead><tr><th class="l">' + (st.mon === 'all' ? 'Môn' : 'Học phần') + '</th><th>Điểm</th><th>So tuần đầu</th><th>Chính xác (%)</th><th>Giây/câu</th><th>Xu hướng</th><th>Mức</th></tr></thead><tbody>' +
        played.map(function (h) {
          var v = m.hp[h], l = M.lv(v.pt);
          return '<tr><td class="nm">' + esc(h) + '</td><td class="h lv' + l + (v.low ? ' low' : '') + '">' + Math.round(v.pt) + (v.low ? '*' : '') + '</td><td>' + delta(hpDelta[h], 0) + '</td><td>' + num(v.acc, 1) + '</td><td>' + num(v.t, 1) + '</td><td>' + spark(hpSeries(x, h), 64, 22) + '</td><td>' + pill(l) + '</td></tr>';
        }).join('') + '</tbody></table></div></section>';
      if (st.mon === 'all') {
        c1 += '<section><h2>Nhận định chi tiết<small>theo từng môn</small></h2><p class="edx-note">Nhận định (chậm-sai, nhanh-nhầm…) tính theo học phần của từng môn. Chọn môn để xem:</p><div class="edx-sub">' + chips(played.map(function (h) { return [h, h]; }), '', 'mon') + '</div></section>';
      } else {
        c1 += '<section><h2>Nhận định từng học phần<small>đánh giá riêng, không gộp</small></h2>' + played.map(function (h) {
          var v = m.hp[h], dg = M.d.diag[v.diag] || { label: v.diag, advice: '', color: '#888' };
          return '<div class="edx-dg"><b>' + esc(h) + '</b><div><span class="edx-pill pillc" style="--dc:' + esc(dg.color) + '">' + esc(dg.label) + '</span><span class="st">đúng ' + v.ok + '/' + v.n + (v.t != null ? ' · TB ' + num(v.t, 1) + 's/câu' : '') + (v.few ? ' · ít mẫu' : '') + '</span></div><div class="adv">' + esc(dg.advice) + '</div></div>';
        }).join('') + '</section>';
      }

      // cột 2: so với lớp + lịch sử theo tuần
      var c2 = '<section><h2>Điểm từng ' + dw + '<small>vạch đen: TB lớp</small></h2>' + D.map(function (h) {
        var v = m.hp[h], r = gl.hp[h];
        if (!v) return '<div class="edx-hrow off"><span>' + esc(h) + '</span><span class="edx-track"></span><b>–</b></div>';
        return '<div class="edx-hrow"><span>' + esc(h) + '</span><span class="edx-track">' + ticks() + '<i class="lv' + M.lv(v.pt) + '" style="width:' + v.pt + '%"></i>' + (r ? '<u style="left:' + r.avg + '%" title="TB lớp: ' + Math.round(r.avg) + '"></u>' : '') + '</span><b>' + Math.round(v.pt) + '</b></div>';
      }).join('') + '<p class="edx-legend"><u></u>TB lớp ' + esc(x.lop) + ' · vạch đứt: mốc ' + M.cuts.join(' / ') + '</p></section>';
      if (multi) {
        var hrows = [['Điểm tổng', '/ 100 · nét đứt: TB lớp', miniLine(x.series, gl.wavg, 0)], ['Chính xác', '% câu đúng', miniLine(x.hist.map(function (w) { return w ? w.acc : null; }), null, 1)],
          ['Thời gian TB', 'giây/câu, thấp hơn = nhanh hơn', miniLine(x.hist.map(function (w) { return w ? w.t : null; }), null, 1)], ['Số câu', 'mỗi tuần', miniBars(x.hist.map(function (w) { return w ? w.n : null; }))]];
        c2 += '<section class="edx-hist"><h2>Lịch sử theo tuần<small>' + x.wIdx.length + '/' + M.nW + ' tuần có chơi</small></h2>' + hrows.map(function (r) { return '<div class="hr"><div class="lab"><b>' + r[0] + '</b><small>' + r[1] + '</small></div>' + r[2] + '</div>'; }).join('') +
          '<div class="hr ax"><div class="lab"></div><div class="wax" style="grid-template-columns:repeat(' + M.nW + ',1fr)">' + M.weeks.map(function (w, i) { return '<span class="' + (i === M.nW - 1 ? 'last' : '') + '" title="' + esc(w.range) + '">' + esc(w.label) + '</span>'; }).join('') + '</div></div></section>';
      } else c2 += '<section><h2>Lịch sử theo tuần</h2><p class="edx-note">Chưa đủ dữ liệu: cần ít nhất 2 tuần có chơi để xem tiến bộ.</p></section>';

      // cột 3: điểm, radar, mục tiêu, nhận xét
      var target = M.cuts[2], gaps = played.filter(function (h) { return m.hp[h].pt < target; }).map(function (h) { return [h, Math.ceil(target - m.hp[h].pt)]; }).sort(function (a, b) { return b[1] - a[1]; });
      var strong = played.slice().sort(function (a, b) { return m.hp[b].pt - m.hp[a].pt; })[0], weak = played.slice().sort(function (a, b) { return m.hp[a].pt - m.hp[b].pt; })[0];
      var best = played.filter(function (h) { return hpDelta[h] != null; }).sort(function (a, b) { return hpDelta[b] - hpDelta[a]; })[0];
      var c3 = '<section><h2>Điểm EduXplore' + (st.mon === 'all' ? '<small>tất cả môn</small>' : '') + '</h2><div class="edx-bigscore"><div class="v lv' + x.lv + '" style="color:var(--c)">' + Math.round(x.score) + '<small>/ 100</small></div>' + pill(x.lv) +
        (multi ? '<p class="big-d">' + delta(x.dFirst, 0, ' điểm') + ' so với ' + esc(M.weeks[x.firstW].label) + ' · so với tuần trước ' + delta(x.dPrev, 0) + '</p>' : '') +
        '<p>Trung bình ' + played.length + ' ' + dw + ' · ' + m.n + ' câu' + (x.few ? ' (ít dữ liệu)' : '') + ' · TB lớp ' + num(gl.avg) + ' · TB khối ' + num(gk.avg) + '</p></div></section>' +
        '<section><h2>Bản đồ ' + (st.mon === 'all' ? 'các môn' : 'kỹ năng') + '<small>nét đứt: TB lớp</small></h2>' + (radar(m, gl, D) || '<p class="edx-note">Cần ít nhất 3 ' + dw + ' để vẽ.</p>') + '</section>' +
        '<section><h2>Mục tiêu<small>mức "' + LV[3] + '" từ ' + target + ' điểm</small></h2><div class="edx-goal"><div><span>Điểm hiện tại</span><b>' + Math.round(x.score) + '</b></div><div><span>Điểm mục tiêu</span><b>' + target + '</b></div>' +
        (gaps.length ? gaps.slice(0, 4).map(function (g2) { return '<div><span>Luyện thêm · ' + esc(g2[0]) + '</span><b class="dn">+' + g2[1] + '</b></div>'; }).join('') : '<div><span>Đã chơi</span><b class="up">đều đạt</b></div>') + '</div></section>' +
        '<section><h2>Nhận xét</h2><div class="edx-cm"><p><b>Mạnh nhất:</b> ' + esc(strong) + ' (' + Math.round(m.hp[strong].pt) + ' điểm)</p>' +
        (best && hpDelta[best] >= 1 ? '<p><b>Tiến bộ nhất:</b> ' + esc(best) + ', ' + delta(hpDelta[best], 0, ' điểm') + '</p>' : '') +
        '<p><b>Cần luyện:</b> ' + esc(weak) + ' (' + Math.round(m.hp[weak].pt) + ' điểm' + (m.hp[weak].diag && SHORT[m.hp[weak].diag] ? ', ' + SHORT[m.hp[weak].diag] : '') + ')</p>' +
        '<p><b>Chăm chỉ:</b> chơi ' + x.wIdx.length + '/' + M.nW + ' tuần, ' + x.days + ' buổi' + (pctBeat != null ? ' · điểm cao hơn ' + pctBeat + '% bạn trong lớp (hạng ' + rank + '/' + gl.ns + ')' : '') + '</p></div></section>';
      return o + '<div class="edx-hsgrid"><div class="edx-col">' + c1 + '</div><div class="edx-col">' + c2 + '</div><div class="edx-col">' + c3 + '</div></div>';
    }
    function radar(m, ref, D) {
      var hs = D.filter(function (h) { return m.hp[h]; });
      if (hs.length < 3) return '';
      var cx = 200, cy = 165, R = 112, n = hs.length;
      function pt(i, v) { var a = -Math.PI / 2 + 2 * Math.PI * i / n; return [cx + R * v / 100 * Math.cos(a), cy + R * v / 100 * Math.sin(a)]; }
      var o = '<svg class="edx-radar" viewBox="0 0 400 330" role="img" aria-label="Bản đồ kỹ năng">';
      [25, 50, 75, 100].forEach(function (r) { o += '<polygon class="ring" points="' + hs.map(function (h, i) { return pt(i, r).join(','); }).join(' ') + '"/>'; });
      hs.forEach(function (h, i) {
        var p = pt(i, 100), q = pt(i, 120), c = Math.cos(-Math.PI / 2 + 2 * Math.PI * i / n);
        o += '<line class="ax" x1="' + cx + '" y1="' + cy + '" x2="' + p[0] + '" y2="' + p[1] + '"/><text class="lab" x="' + q[0] + '" y="' + (q[1] + 5) + '" text-anchor="' + (Math.abs(c) < 0.2 ? 'middle' : c > 0 ? 'start' : 'end') + '">' + esc(h) + '</text>';
      });
      if (ref) o += '<polygon class="ref" points="' + hs.map(function (h, i) { return pt(i, ref.hp[h] ? ref.hp[h].avg : 0).join(','); }).join(' ') + '"/>';
      o += '<polygon class="me" points="' + hs.map(function (h, i) { return pt(i, m.hp[h].pt).join(','); }).join(' ') + '"/>';
      hs.forEach(function (h, i) { var p = pt(i, m.hp[h].pt); o += '<circle cx="' + p[0] + '" cy="' + p[1] + '" r="4"/>'; });
      return o + '</svg>';
    }

    // ---------- vẽ + sự kiện ----------
    function render() {
      if (!data) return;
      reconcile();
      var keep = el.querySelector('.edx-scroll'), top = keep ? keep.scrollTop : 0;
      el.innerHTML = '<div class="edx-sheet">' + header() + '<main class="edx-main">' + (st.tab === 'tq' ? viewTq() : st.tab === 'khoi' ? viewKhoi() : st.tab === 'lop' ? viewLop() : viewHs()) + '</main>' + footer() + '</div>';
      // biểu đồ vẽ theo đúng kích thước khung (1 đơn vị = 1px) để không bị chừa trống: đo rồi vẽ lại 1 lần
      var box = el.querySelector('.edx-chartbox');
      if (box && !measuring) {
        var bw = Math.round(box.clientWidth), bh = Math.round(box.clientHeight);
        if (bw > 80 && bh > 80 && (Math.abs(bw - W) > 8 || Math.abs(bh - H) > 8)) { W = bw; H = bh; measuring = true; try { render(); } finally { measuring = false; } return; }
      }
      if (st.tab === 'hs') { var act = el.querySelector('#edx-roster button.on'); if (act && act.scrollIntoView) act.scrollIntoView({ block: 'nearest' }); }
      else if (keep && top) { var ns = el.querySelector('.edx-scroll'); if (ns) ns.scrollTop = top; }
      if (opts.hash) syncHash();
    }
    function syncHash() {
      var h = st.tab === 'tq' ? 'tq' : st.tab === 'khoi' ? 'khoi/' + encodeURIComponent(st.khoi) : st.tab === 'lop' ? 'lop/' + encodeURIComponent(st.khoi) + '/' + encodeURIComponent(st.lop) : 'hs/' + encodeURIComponent(st.code || '');
      if (global.location.hash !== '#' + h) { st.hashSelf = '#' + h; try { global.history.replaceState(null, '', '#' + h); } catch (e) { global.location.hash = h; } }
    }
    function readHash() {
      var p = global.location.hash.replace(/^#/, '').split('/').map(decodeURIComponent);
      if (p[0] === 'tq') st.tab = 'tq';
      else if (p[0] === 'khoi' && p[1]) { st.tab = 'khoi'; if (M.khois.indexOf(p[1]) >= 0) st.khoi = p[1]; }
      else if (p[0] === 'lop' && p[1] && p[2]) { st.tab = 'lop'; st.khoi = p[1]; st.lop = p[2]; }
      else if (p[0] === 'hs' && p[1]) { var s = M && M.byCode(p[1]); if (s) { st.tab = 'hs'; st.khoi = s.khoi; st.lop = s.lop; st.code = s.code; } }
    }
    function go(t) {
      var k; for (k in t) if (Object.prototype.hasOwnProperty.call(t, k)) st[k] = t[k];
      if (t.khoi && !t.lop) st.lop = null;
      if ((t.khoi || t.lop) && !t.code) st.code = null;
      if (t.code) { var s = M.byCode(t.code); if (s) { st.khoi = s.khoi; st.lop = s.lop; } }
      if (t.khoi) { st.rsel = null; st.tsel = null; }
      render();
    }
    function onClick(e) {
      var n = e.target.closest ? e.target.closest('[data-a]') : null;
      if (!n || !el.contains(n) || n.disabled) return;
      var a = n.getAttribute('data-a'), v = n.getAttribute('data-v');
      if (a === 'tab') go({ tab: v });
      else if (a === 'khoi') go({ tab: 'khoi', khoi: v });
      else if (a === 'cmp') { st.cmp = v; render(); }
      else if (a === 'metric') { st.metric = v; render(); }
      else if (a === 'prog') { st.prog = v; render(); }
      else if (a === 'lchart') { st.lchart = v; render(); }
      else if (a === 'filt') { st.filt = v; render(); }
      else if (a === 'cmpsort') { /* xử lý ở onChange */ }
      else if (a === 'sel') { var key = n.getAttribute('data-k'), arr = st[key], i = arr.indexOf(v); if (i >= 0) arr.splice(i, 1); else if (arr.length < 6) arr.push(v); render(); }
      else if (a === 'lop') go({ tab: (st.tab === 'khoi' || st.tab === 'tq') ? 'lop' : st.tab, khoi: n.getAttribute('data-khoi'), lop: n.getAttribute('data-lop'), q: '' });
      else if (a === 'code') {
        var c = n.getAttribute('data-code');
        go({ tab: 'hs', code: c });
        if (opts.onSelectStudent) opts.onSelectStudent(M.byCode(c).s);
      }
      else if (a === 'sort') {
        var s = st.sort[n.getAttribute('data-t')], k2 = n.getAttribute('data-k');
        if (s.k === k2) s.d = -s.d; else { s.k = k2; s.d = k2 === 'name' ? 1 : -1; }
        render();
      }
      else if (a === 'mon') { st.mon = v; st.rsel = null; st.tsel = null; render(); }
      else if (a === 'theme') {
        st.theme = isDark() ? 'light' : 'dark'; applyTheme();
        try { global.localStorage.setItem('edux-report-theme', st.theme); } catch (er) { /* bỏ qua */ }
        render();
      }
    }
    function onChange(e) { if (e.target && e.target.id === 'edx-sort') { st.cmpSort = e.target.checked; render(); } }
    function onInput(e) {
      if (!e.target || e.target.id !== 'edx-q') return;
      st.q = e.target.value;
      var list = rosterList(), box = el.querySelector('#edx-roster');
      if (box) box.innerHTML = rosterHtml(list);
      var h2 = el.querySelector('.edx-fill.hs h2'); if (h2) h2.innerHTML = (st.q ? 'Tìm kiếm' : 'Lớp ' + esc(st.lop)) + '<small>' + list.length + ' bé</small>';
    }
    function onHash() { if (st.hashSelf === global.location.hash) return; readHash(); render(); }

    el.addEventListener('click', onClick);
    el.addEventListener('change', onChange);
    el.addEventListener('input', onInput);
    if (opts.hash) global.addEventListener('hashchange', onHash);

    function setData(d) { data = d; M = build(d); if (opts.hash) readHash(); render(); }
    this.update = setData;
    this.go = go;
    this.destroy = function () {
      el.removeEventListener('click', onClick); el.removeEventListener('change', onChange); el.removeEventListener('input', onInput);
      global.removeEventListener('hashchange', onHash); el.innerHTML = ''; el.classList.remove('edx', 'edx-vh');
    };

    if (opts.data) setData(opts.data);
    else if (opts.dataUrl) {
      el.innerHTML = '<div class="edx-sheet"><p class="edx-empty">Đang tải dữ liệu…</p></div>';
      global.fetch(opts.dataUrl, { cache: 'no-store' }).then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); }).then(setData).catch(function (err) {
        el.innerHTML = '<div class="edx-sheet"><p class="edx-empty">Không tải được dữ liệu báo cáo (' + esc(err.message) + '). Kiểm tra đường dẫn: ' + esc(opts.dataUrl) + '</p></div>';
      });
    }
  }

  var EduxReport = { mount: function (el, opts) { return new Widget(typeof el === 'string' ? global.document.querySelector(el) : el, opts || {}); } };
  global.EduxReport = EduxReport;

  global.document.addEventListener('DOMContentLoaded', function () {
    var nodes = global.document.querySelectorAll('[data-edux-report]');
    for (var i = 0; i < nodes.length; i++) {
      var n = nodes[i];
      EduxReport.mount(n, { dataUrl: n.getAttribute('data-src'), logo: n.getAttribute('data-logo') || null, theme: n.getAttribute('data-theme') || 'light', hash: n.hasAttribute('data-hash'), height: n.getAttribute('data-height') || null });
    }
  });
})(window);
