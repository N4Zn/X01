"""Trang A4 tổng hợp cả lớp: đồ thị chính xác x tốc độ, phân bố mức, học phần TB lớp, bảng xếp hạng.

Đồ thị mặc định (mode='hp'): mỗi chấm = 1 bé x 1 học phần, KHÔNG gộp học phần. Một bé có thể "nhanh nhưng hay nhầm" ở
học phần này nhưng "chậm và sai (chưa nắm)" ở học phần khác; gộp lại sẽ kết luận sai. Chỉ chấm cần chú ý mới có nhãn tên.
mode='kid': mỗi bé 1 chấm gộp mọi học phần (kèm tên từng bé).

Gọi: class_page.page(cfg, out, rows, period, mode='hp') -> chuỗi HTML <main class="sheet"> (gen.build chèn làm trang đầu).
"""
import html
import math

import edux_lib as L

e = html.escape
COL = ['#b23a3a', '#c0782a', '#4a7fb0', '#1f7a52']   # Mức 1..Đạt mục tiêu
HPSHORT = {'Nhận biết số': 'NB số', 'Đếm': 'Đếm', 'Cộng': 'Cộng', 'Trừ': 'Trừ', 'So sánh': 'SS'}
FLAG = ('fast_wrong', 'slow_wrong', 'slow_ok', 'shaky')
SHORT = {'fast_wrong': 'nhanh-nhầm', 'slow_wrong': 'chậm-sai', 'slow_ok': 'đúng-chậm', 'shaky': 'chưa vững'}
CSS = """
.cl-kpi{display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin:6px 0 4px}
.cl-kpi div{border:1px solid var(--line);padding:5px 8px}
.cl-kpi small{display:block;color:var(--muted);font-size:10.5px}
.cl-kpi b{font-size:20px;font-family:var(--font-display)}
.cl-main{display:grid;grid-template-columns:minmax(0,1.55fr) minmax(0,1fr);gap:16px;align-items:start}
.cl-dist{display:flex;gap:4px;margin:4px 0 2px}
.cl-dist div{flex:1;text-align:center;color:#fff;border-radius:3px;padding:3px 0;font-size:11px;line-height:1.25}
.cl-dist b{font-size:17px;display:block}
.hrow{display:grid;grid-template-columns:78px minmax(0,1fr) 34px;gap:6px;align-items:center;padding:3px 0;font-size:11.5px}
.hrow .hb{height:10px;background:#eceff2;border-radius:2px;position:relative}
.hrow .hb i{position:absolute;left:0;top:0;bottom:0;border-radius:2px}
table.cl{width:100%;border-collapse:collapse;font-size:11px}
table.cl th{background:#6b7785;color:#fff;font-weight:600;padding:3px 4px;text-align:center;font-size:10.5px}
table.cl td{padding:0 4px;line-height:1.35;border-bottom:1px solid var(--line);text-align:center;white-space:nowrap}
table.cl td.l{text-align:left}
table.cl td.h{color:#fff;font-weight:600}
.cl-ins li{margin:3px 0;font-size:11.5px;line-height:1.3}
.cl-dot{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:3px;vertical-align:middle}
"""


def kid_stats(cfg, out, rows):
    """Mỗi bé có điểm: % đúng chung, TB giây câu đúng chung, số câu, điểm Toán, mức (cho bảng xếp hạng / mode kid)."""
    f = cfg['formula']
    ks = []
    for d in out:
        if 'toan' not in d:
            continue
        rs = [r for r in rows if r['code'] == d['code']]
        ok = sum(r['ok'] for r in rs)
        ts = sorted(r['t'] for r in rs)       # trung vị mọi câu (không bị lệch bởi 1 câu treo 50s)
        tmed = ts[len(ts) // 2] if len(ts) % 2 else (ts[len(ts) // 2 - 1] + ts[len(ts) // 2]) / 2
        flags = [(h, d['hp'][h]['diag']) for h in cfg['hp_order'] if h in d['hp'] and d['hp'][h]['diag']['code'] in FLAG]
        ks.append(dict(d=d, name=d['alias'] or d['name'], n=len(rs), acc=100.0 * ok / len(rs), t=tmed,
                       score=d['toan'], lv=L.level(f, d['toan']), flags=flags))
    return ks


def hp_points(cfg, out):
    pts = []
    for d in out:
        for h, v in d['hp'].items():
            g = v['diag']
            pts.append(dict(x=g['tmed'], y=g['acc'], n=g['n'], color=L.DIAG[g['code']][2], hollow=g['few'] or g['code'] == 'few',
                            label=('%s·%s' % (d['alias'] or d['name'], HPSHORT.get(h, h))) if g['code'] in FLAG else None,
                            code=g['code'], kid=d['alias'] or d['name'], hp=h))
    return pts


def kid_points(cfg, ks):
    f = cfg['formula']
    return [dict(x=k['t'], y=k['acc'], n=k['n'], color=COL[k['lv']], hollow=k['n'] < f['few_total'], label=k['name'], code='',
                 kid=k['name'], hp='', ring='#c0782a' if k['flags'] else None) for k in ks]


def scatter(cfg, pts, mode='hp'):
    W, H = 560, 410
    ml, mr, mt, mb = 46, 14, 14, 44
    pw, ph = W - ml - mr, H - mt - mb
    g = cfg['diag']
    xmin = max(1.2, min(p['x'] for p in pts) * 0.85)
    xmax = max(10.0, max(p['x'] for p in pts) * 1.15)
    if mode == 'hp':
        ymin, ystep = 0.0, 20
    else:
        ymin, ystep = max(0.0, math.floor((min(p['y'] for p in pts) - 8) / 10) * 10), 10
    ymax = 112.0 if mode == 'hp' else 104.0
    lg = math.log
    X = lambda v: ml + (lg(xmax) - lg(max(v, xmin))) / (lg(xmax) - lg(xmin)) * pw    # trục X log, đảo: nhanh ở bên PHẢI
    Y = lambda v: mt + (ymax - v) / (ymax - ymin) * ph
    s = ['<svg viewBox="0 0 %d %d" width="100%%" style="display:block;height:auto">' % (W, H)]
    xf, xs, yg = g['fast_s'], g['slow_s'], g['acc_ok']
    s.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#1f7a52" opacity=".07"/>' % (X(xf), Y(ymax), X(xmin) - X(xf), Y(yg) - Y(ymax)))
    for v in (1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 30, 40, 60):
        if xmin <= v <= xmax:
            s.append('<line x1="%.1f" y1="%d" x2="%.1f" y2="%d" stroke="#e3e6ea"/><text x="%.1f" y="%d" font-size="10" text-anchor="middle" fill="#6b7785">%d</text>'
                     % (X(v), mt, X(v), mt + ph, X(v), mt + ph + 14, v))
    v = int(ymin)
    while v <= 100:
        s.append('<line x1="%d" y1="%.1f" x2="%d" y2="%.1f" stroke="#e3e6ea"/><text x="%d" y="%.1f" font-size="10" text-anchor="end" fill="#6b7785">%d</text>'
                 % (ml, Y(v), ml + pw, Y(v), ml - 5, Y(v) + 3.5, v))
        v += ystep
    for xv in ((xf, xs) if mode == 'hp' else (xf,)):
        if xmin <= xv <= xmax:
            s.append('<line x1="%.1f" y1="%d" x2="%.1f" y2="%d" stroke="#8a929c" stroke-dasharray="4 3"/>' % (X(xv), mt, X(xv), mt + ph))
    s.append('<line x1="%d" y1="%.1f" x2="%d" y2="%.1f" stroke="#8a929c" stroke-dasharray="4 3"/>' % (ml, Y(yg), ml + pw, Y(yg)))
    if mode == 'hp':
        caps = [(ml + pw - 4, mt + 12, 'end', '#1f7a52', 'Thành thạo'), (ml + 4, mt + 12, 'start', '#4a7fb0', 'Đúng nhưng chậm'),
                (ml + pw - 4, mt + ph - 6, 'end', '#c0782a', 'Nhanh nhưng hay nhầm'), (ml + 4, mt + ph - 6, 'start', '#b23a3a', 'Chậm và sai: chưa nắm')]
    else:
        caps = [(ml + pw - 4, mt + 12, 'end', '#1f7a52', 'Nhanh & chính xác'), (ml + 4, mt + 12, 'start', '#9a6a1a', 'Chính xác nhưng chậm'),
                (ml + pw - 4, mt + ph - 6, 'end', '#9a6a1a', 'Nhanh nhưng hay sai'), (ml + 4, mt + ph - 6, 'start', '#b23a3a', 'Cần hỗ trợ')]
    boxes = []
    for x, y, anc, col, t in caps:
        s.append('<text x="%.1f" y="%.1f" font-size="10" fill="%s" text-anchor="%s">%s</text>' % (x, y, col, anc, e(t)))
        w = 5.6 * len(t)
        boxes.append((x - w if anc == 'end' else x, y - 10, x if anc == 'end' else x + w, y + 3))
    s.append('<text x="%.1f" y="%d" font-size="11" text-anchor="middle" fill="#1c1d20">Tốc độ: thời gian trung vị mỗi câu (giây, thang log) - nhanh ở bên phải →</text>' % (ml + pw / 2, H - 8))
    s.append('<text transform="translate(12 %.1f) rotate(-90)" font-size="11" text-anchor="middle" fill="#1c1d20">Chính xác: %% câu đúng</text>' % (mt + ph / 2))
    P = [(X(p['x']), Y(p['y']), 3.2 + min(p['n'], 40) / 14.0, p) for p in sorted(pts, key=lambda p: (-p['y'], p['x']))]
    dots = [(x - r - 1, y - r - 1, x + r + 1, y + r + 1, id(p)) for x, y, r, p in P]

    def overlap(b, c):
        return not (b[2] < c[0] or b[0] > c[2] or b[3] < c[1] or b[1] > c[3])
    # chấm tốt vẽ trước (mờ), chấm cần chú ý vẽ sau cho nổi
    for x, y, r, p in sorted(P, key=lambda t: t[3]['label'] is not None):
        op = '0.15' if p['hollow'] else ('0.5' if (p['label'] is None and mode == 'hp') else '0.9')
        ring = p.get('ring')
        s.append('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="%s" fill-opacity="%s" stroke="%s" stroke-width="%s"/>'
                 % (x, y, r + (1.5 if ring else 0), p['color'], op, ring or p['color'], '2.6' if ring else '1.5'))
    labels = []
    for x, y, r, p in P:
        if not p['label']:
            continue
        w = 5.9 * len(p['label']) + 2
        best = None
        for mult in (1.0, 1.9, 3.0, 4.2, 5.5):
            d = (r + 3) * mult
            for dx, dy, anc in [(d, 4, 'start'), (-d, 4, 'end'), (0, -d + 1, 'middle'), (0, d + 9, 'middle'), (d, -d + 6, 'start'),
                                (d, d + 6, 'start'), (-d, -d + 6, 'end'), (-d, d + 6, 'end')]:
                bx = x + dx - (w if anc == 'end' else w / 2 if anc == 'middle' else 0)
                b = (bx, y + dy - 9, bx + w, y + dy + 3)
                if b[0] < ml - 2 or b[2] > ml + pw + 2 or b[1] < mt - 2 or b[3] > mt + ph + 2:
                    continue
                pen = sum(overlap(b, c) for c in boxes) * 3 + sum(overlap(b, c) for c in dots if c[4] != id(p))
                if best is None or pen < best[0]:
                    best = (pen, dx, dy, anc, b)
                if pen == 0:
                    break
            if best and best[0] == 0:
                break
        if best is None:
            best = (0, r + 3, 4, 'start', (x, y, x + w, y + 4))
        boxes.append(best[4])
        b = best[4]
        lx, ly = min(max(x, b[0]), b[2]), min(max(y, b[1]), b[3])
        if math.hypot(lx - x, ly - y) > r + 4:
            labels.append('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#9aa1aa" stroke-width=".7"/>' % (x, y, lx, ly))
        labels.append('<text x="%.1f" y="%.1f" font-size="10.5" text-anchor="%s" fill="#1c1d20">%s</text>' % (x + best[1], y + best[2], best[3], e(p['label'])))
    s.extend(labels)
    s.append('</svg>')
    return ''.join(s)


def page(cfg, out, rows, period, mode='kid'):
    f = cfg['formula']
    g = cfg['diag']
    HP = cfg['hp_order']
    ks = kid_stats(cfg, out, rows)
    if not ks:
        return ''
    codes = {k['d']['code'] for k in ks}
    nq = sum(k['n'] for k in ks)
    avg = sum(k['score'] for k in ks) / len(ks)
    ok_all = sum(r['ok'] for r in rows if r['code'] in codes)
    acc_all = 100.0 * ok_all / max(1, nq)
    tc = sorted(r['t'] for r in rows if r['code'] in codes)
    t_med = tc[len(tc) // 2] if tc else 0
    nolist = [d['alias'] or d['name'] for d in out if 'toan' not in d]
    cnt = [sum(1 for k in ks if k['lv'] == i) for i in range(4)]
    dist = ''.join('<div style="background:%s"><b>%d</b>%s</div>' % (COL[i], cnt[i], L.LEVEL_NAMES[i]) for i in (3, 2, 1, 0))
    # học phần TB lớp
    hrows, hpavg = '', []
    for h in HP:
        vs = [k['d']['hp'][h]['pt'] for k in ks if h in k['d']['hp']]
        if vs:
            m = sum(vs) / len(vs)
            hpavg.append((h, m, len(vs)))
            hrows += '<div class="hrow"><div>%s</div><div class="hb"><i style="width:%.1f%%;background:%s"></i></div><div class="num"><b>%.0f</b></div></div>' % (
                h, min(m, 100), COL[L.level(f, m)], m)
        else:
            hrows += '<div class="hrow muted"><div>%s</div><div style="font-size:10.5px">chưa có điểm</div><div></div></div>' % h
    # cần chú ý theo học phần (không gộp)
    groups = {c: [] for c in FLAG}
    for k in ks:
        for h in HP:
            v = k['d']['hp'].get(h)
            if v and v['diag']['code'] in FLAG:
                groups[v['diag']['code']].append('%s (%s%s)' % (k['name'], HPSHORT.get(h, h), ', ít mẫu' if v['diag']['few'] else ''))
    ins = ''
    for c in ('slow_wrong', 'fast_wrong', 'slow_ok', 'shaky'):
        if groups[c]:
            lab, adv, col = L.DIAG[c]
            ins += '<li><span class="cl-dot" style="background:%s"></span><b>%s:</b> %s</li>' % (col, e(lab), e(', '.join(groups[c])))
    if not ins:
        ins = '<li>Không có học phần nào cần chú ý đặc biệt.</li>'
    srt = sorted(ks, key=lambda k: -k['score'])
    summ = ''
    if hpavg:
        w = min(hpavg, key=lambda x: x[1])
        summ = '<li><b>Học phần yếu nhất lớp:</b> %s (%.0f điểm)</li>' % (w[0], w[1])
    if nolist:
        summ += '<li class="muted"><b>Chưa có điểm:</b> %s</li>' % e(', '.join(nolist))
    # bảng xếp hạng
    tr = ''
    for i, k in enumerate(srt, 1):
        cells = ''
        for h in HP:
            if h in k['d']['hp']:
                v = k['d']['hp'][h]['pt']
                cells += '<td class="h" style="background:%s%s">%.0f</td>' % (COL[L.level(f, v)], ';opacity:.6' if k['d']['hp'][h]['low'] else '', v)
            else:
                cells += '<td class="muted">-</td>'
        note = '; '.join('%s: %s' % (HPSHORT.get(h, h), SHORT[dg['code']]) for h, dg in k['flags'])
        tr += ('<tr><td>%d</td><td class="l"><b>%s</b></td><td class="num"><b>%.0f</b></td>%s'
               '<td class="num">%d%s</td><td class="l" style="white-space:normal;font-size:10px;line-height:1.2;color:#8a4b0f">%s</td>'
               '<td><span class="pill l%d">%s</span></td></tr>') % (
            i, e(k['name']), k['score'], cells, k['n'], '*' if k['n'] < f['few_total'] else '', e(note), k['lv'], L.LEVEL_NAMES[k['lv']])
    hdr = ''.join('<th>%s</th>' % h for h in HP)
    cls = ', '.join(sorted({d['class'] for d in out if d.get('class')})) or ''
    if mode == 'kid':
        pts, title, sub = kid_points(cfg, ks), 'Chính xác và tốc độ', 'mỗi chấm là một bé (gộp mọi học phần: chỉ để nhìn tổng quan); chấm to = nhiều câu; rỗng = ít mẫu; viền cam = có học phần cần chú ý'
        legend = ('<span style="color:%s">●</span> Đạt mục tiêu &nbsp;<span style="color:%s">●</span> Mức 3 &nbsp;<span style="color:%s">●</span> Mức 2 &nbsp;'
                  '<span style="color:%s">●</span> Mức 1 &nbsp;<span style="color:#c0782a">◎</span> viền cam: xem cột "Cần chú ý" (nhận định riêng từng học phần)') % (COL[3], COL[2], COL[1], COL[0])
    else:
        pts, title, sub = hp_points(cfg, out), 'Chính xác và tốc độ theo học phần', 'mỗi chấm = 1 bé x 1 học phần (không gộp); chỉ ghi tên chấm cần chú ý; rỗng = ít mẫu'
        legend = ''.join('<span style="color:%s">●</span> %s &nbsp;' % (L.DIAG[c][2], L.DIAG[c][0]) for c in ('good', 'slow_ok', 'fast_wrong', 'slow_wrong', 'few'))
        legend = legend.replace('Thành thạo', 'Thành thạo/khá').replace('Chưa đủ dữ liệu', 'Ít mẫu')
        ngood = sum(1 for p in pts if p['code'] in ('good', 'ok'))
        legend += '<br>%d/%d lượt ở vùng tốt (mờ, không ghi tên). Nét đứt: %gs, %gs, %g%% đúng' % (ngood, len(pts), g['fast_s'], g['slow_s'], g['acc_ok'])
    return f'''<main class="sheet">
  <div class="top"><img class="logo" src="logo.png" alt="EduXplore" height="58"><div class="dev">[Tổng hợp cả lớp {e(cfg['mon'])}] [Kỳ {e(period)}]</div></div>
  <div class="cl-kpi">
    <div><small>Lớp</small><b>{e(cls)}</b></div>
    <div><small>Số bé có điểm</small><b class="num">{len(ks)}</b></div>
    <div><small>Điểm TB lớp</small><b class="num">{avg:.0f}</b></div>
    <div><small>% đúng - giây/câu (trung vị)</small><b class="num">{acc_all:.0f}% - {t_med:.1f}s</b></div>
  </div>
  <div class="cl-main">
    <section><h2>{title} <small>{sub}</small></h2>
      {scatter(cfg, pts, mode)}
      <div class="legend">{legend}</div></section>
    <aside>
      <section><h2>Phân bố mức</h2><div class="cl-dist">{dist}</div></section>
      <section><h2>Điểm TB từng học phần</h2>{hrows}</section>
      <section><h2>Cần chú ý theo học phần</h2><ul class="ins cl-ins">{ins}{summ}</ul></section>
    </aside>
  </div>
  <section style="margin-top:8px"><h2>Bảng xếp hạng <small>ô màu = điểm học phần theo mức; mờ = ít mẫu; nhầm = trả lời vội/đoán, sai = chưa nắm</small></h2>
    <table class="cl"><tr><th>#</th><th>Bé</th><th>Điểm</th>{hdr}<th>Số câu</th><th>Cần chú ý (theo học phần)</th><th style="width:88px">Mức</th></tr>{tr}</table></section>
  <div class="foot"><span>Điểm = {f['base']:g} × tỉ lệ đúng × hệ số tốc độ (tối đa 100). Nhận định từng học phần theo trung vị thời gian mọi câu và % đúng.</span><span>Kỳ báo cáo: {e(period)}. * ít mẫu.</span></div>
</main>'''
