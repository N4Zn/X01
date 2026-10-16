"""out.json (pipeline chấm điểm) + config.json  ->  report.json cho web báo cáo (edux-report.js).

Chạy (từ thư mục web/):
  python -I -X utf8 tools/build_report_json.py --out-json <.../out.json> --from 2026-10-08 --to 2026-10-09 --out data/report.json
Tuỳ chọn:
  --config   config.json của pipeline (mặc định ../tool/pipeline/config.json): lấy roster (cả bé chưa có điểm), diag, công thức.
  --lop-map  JSON {"CASA-21001": "Lá 1", ...}. Không có -> lớp = tên khối (class trong roster, vd "5 tuổi").
  --mon      tên môn của out.json (mặc định "Toán"; hiện pipeline chỉ chấm 1 môn).

Schema xem INTEGRATION.md mục "Dữ liệu".
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PIPE = os.path.normpath(os.path.join(HERE, '..', '..', 'tool', 'pipeline'))
sys.path.insert(0, PIPE)
import edux_lib as L  # noqa: E402  (nguồn duy nhất của nhãn nhận định DIAG)


def fmt(d):
    y, m, dd = d.split('-')
    return '%s/%s/%s' % (dd, m, y)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out-json', required=True)
    ap.add_argument('--config', default=os.path.join(PIPE, 'config.json'))
    ap.add_argument('--lop-map')
    ap.add_argument('--mon', default='Toán')
    ap.add_argument('--from', dest='dfrom', required=True)
    ap.add_argument('--to', dest='dto', required=True)
    ap.add_argument('--out', required=True)
    a = ap.parse_args()

    cfg = json.load(open(a.config, encoding='utf-8'))
    out = {d['code']: d for d in json.load(open(a.out_json, encoding='utf-8'))}
    lopmap = json.load(open(a.lop_map, encoding='utf-8')) if a.lop_map else {}
    f = cfg['formula']

    students = []
    for s in cfg['roster']:
        d = out.get(s['code'])
        st = {'code': s['code'], 'name': s['name'], 'alias': s.get('alias', ''),
              'khoi': s.get('class', ''), 'lop': lopmap.get(s['code'], s.get('class', '')), 'mon': {}}
        if d and d.get('hp'):
            hp = {}
            for h, v in d['hp'].items():
                g = v['diag']
                hp[h] = {'n': v['n'], 'ok': v['ok'], 'acc': round(v['acc'], 1),
                         't': None if v['t'] is None else round(v['t'], 2),
                         'k': round(v['k'], 3), 'pt': round(v['pt'], 1), 'low': bool(v['low']),
                         'diag': g['code'], 'tmed': round(g['tmed'], 2), 'few': bool(g['few'])}
            st['mon'][a.mon] = {'score': round(d['toan'], 1), 'n': d['n'], 'hp': hp}
        students.append(st)

    rep = {
        'version': 1,
        'ky': {'from': a.dfrom, 'to': a.dto, 'label': '%s → %s' % (fmt(a.dfrom), fmt(a.dto))},
        'formula': {'cuts': f['cuts'], 'low_sample': f['low_sample'], 'few_total': f['few_total'],
                    'base': f['base'], 'kmax': f['kmax'], 'tfast': f['tfast'], 'tslow': f['tslow']},
        'diag': {k: {'label': v[0], 'advice': v[1], 'color': v[2]} for k, v in L.DIAG.items()},
        'mon': [{'id': a.mon, 'hp': cfg['hp_order']}],
        'students': students,
    }
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(rep, fh, ensure_ascii=False, indent=1)
    sc = sum(1 for s in students if s['mon'])
    print('Ghi %s: %d bé (%d có điểm)' % (a.out, len(students), sc))


if __name__ == '__main__':
    main()
