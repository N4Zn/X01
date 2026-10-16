"""Dữ liệu pipeline chấm điểm -> report.json cho web báo cáo (edux-report.js).

Hai cách chạy (từ thư mục web/):

  A) Từ log (có LỊCH SỬ THEO TUẦN) - khuyên dùng:
     python -I -X utf8 tools/build_report_json.py --local <GameLogs> --from 2026-09-07 --to 2026-10-16 --out data/report.json
     python -I -X utf8 tools/build_report_json.py --csv   <URL|file.csv> --from ... --to ...      (Google Sheet; không --csv thì lấy URL trong config.json)
     Điểm cả kỳ = công thức chuẩn trên toàn kỳ; mỗi tuần (thứ Hai-Chủ nhật) chấm riêng bằng đúng công thức đó -> `hist`.

  B) Từ out.json của 1 kỳ (không có lịch sử):
     python -I -X utf8 tools/build_report_json.py --out-json <.../out.json> --from 2026-10-08 --to 2026-10-09 --out data/report.json

Tuỳ chọn: --config (mặc định ../tool/pipeline/config.json: roster, diag, công thức) · --lop-map {"CASA-21001":"1A1",...}
(không có: lớp = trường `class` của roster) · --mon (mặc định "Toán").
Schema: xem INTEGRATION.md mục "Dữ liệu".
"""
import argparse
import json
import os
import sys
from datetime import date, timedelta

HERE = os.path.dirname(os.path.abspath(__file__))
PIPE = os.path.normpath(os.path.join(HERE, '..', '..', 'tool', 'pipeline'))
sys.path.insert(0, PIPE)
import edux_lib as L  # noqa: E402  (nguồn duy nhất của công thức điểm và nhãn nhận định DIAG)


def fmt(d):
    y, m, dd = d.split('-')
    return '%s/%s/%s' % (dd, m, y)


def hp_entry(v):
    g = v['diag']
    return {'n': v['n'], 'ok': v['ok'], 'acc': round(v['acc'], 1), 't': None if v['t'] is None else round(v['t'], 2),
            'k': round(v['k'], 3), 'pt': round(v['pt'], 1), 'low': bool(v['low']),
            'diag': g['code'], 'tmed': round(g['tmed'], 2), 'few': bool(g['few'])}


def make_weeks(dfrom, dto):
    a = date.fromisoformat(dfrom)
    a -= timedelta(days=a.weekday())
    end = date.fromisoformat(dto)
    out = []
    while a <= end:
        b = a + timedelta(days=6)
        out.append({'id': '%d-W%02d' % a.isocalendar()[:2], 'from': a.isoformat(), 'to': b.isoformat(),
                    'label': 'T%d' % a.isocalendar()[1], 'range': '%02d–%02d/%02d' % (a.day, b.day, b.month)})
        a += timedelta(days=7)
    return out


def hist_entry(d, rows_w, code):
    """d: bản ghi học sinh của tuần (L.score); rows_w: các round dùng để chấm tuần đó."""
    if not d or not d.get('hp'):
        return None
    mine = [r for r in rows_w if r['code'] == code]
    n = sum(v['n'] for v in d['hp'].values())
    ok = sum(v['ok'] for v in d['hp'].values())
    ts = [r['t'] for r in mine if r['ok']]
    return {'score': round(d['toan'], 1), 'n': n, 'acc': round(100 * ok / n, 1) if n else None,
            't': round(sum(ts) / len(ts), 2) if ts else None,
            'days': len({r['ts'][:10] for r in mine}), 'hp': {h: round(v['pt'], 1) for h, v in d['hp'].items()}}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out-json')
    ap.add_argument('--local')
    ap.add_argument('--csv', nargs='?', const='')
    ap.add_argument('--config', default=os.path.join(PIPE, 'config.json'))
    ap.add_argument('--lop-map')
    ap.add_argument('--mon', default='Toán')
    ap.add_argument('--from', dest='dfrom', required=True)
    ap.add_argument('--to', dest='dto', required=True)
    ap.add_argument('--out', required=True)
    a = ap.parse_args()

    cfg = L.load_config(a.config) if hasattr(L, 'load_config') else json.load(open(a.config, encoding='utf-8'))
    f = cfg['formula']
    lopmap = json.load(open(a.lop_map, encoding='utf-8')) if a.lop_map else {}
    weeks, wk_out = [], []

    if a.out_json:
        out = {d['code']: d for d in json.load(open(a.out_json, encoding='utf-8'))}
    else:
        if a.local:
            db_rows, st, _ = L.ingest_local(cfg, a.local)
        else:
            rows = L.fetch_csv(a.csv or cfg['sheet_csv_url'])
            db_rows, st, _ = L.ingest(cfg, rows)
        o, _, _ = L.score(cfg, db_rows, a.dfrom, a.dto)
        out = {d['code']: d for d in o}
        weeks = make_weeks(a.dfrom, a.dto)
        for w in weeks:
            ow, rw, _ = L.score(cfg, db_rows, w['from'], w['to'])
            wk_out.append(({d['code']: d for d in ow}, rw))

    students = []
    for s in cfg['roster']:
        d = out.get(s['code'])
        st_ = {'code': s['code'], 'name': s['name'], 'alias': s.get('alias', ''),
               'khoi': s.get('class', ''), 'lop': lopmap.get(s['code'], s.get('class', '')), 'mon': {}}
        if d and d.get('hp'):
            m = {'score': round(d['toan'], 1), 'n': d['n'], 'hp': {h: hp_entry(v) for h, v in d['hp'].items()}}
            if weeks:
                m['hist'] = [hist_entry(od.get(s['code']), rw, s['code']) for od, rw in wk_out]
            st_['mon'][a.mon] = m
        students.append(st_)

    rep = {'version': 2,
           'ky': {'from': a.dfrom, 'to': a.dto, 'label': '%s → %s' % (fmt(a.dfrom), fmt(a.dto))},
           'weeks': weeks,
           'formula': {k: f[k] for k in ('cuts', 'low_sample', 'few_total', 'base', 'kmax', 'tfast', 'tslow')},
           'diag': {k: {'label': v[0], 'advice': v[1], 'color': v[2]} for k, v in L.DIAG.items()},
           'mon': [{'id': a.mon, 'hp': cfg['hp_order']}],
           'students': students}
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(rep, fh, ensure_ascii=False, separators=(',', ':'))
    sc = sum(1 for s in students if s['mon'])
    print('Ghi %s: %d bé (%d có điểm), %d tuần' % (a.out, len(students), sc, len(weeks)))


if __name__ == '__main__':
    main()
