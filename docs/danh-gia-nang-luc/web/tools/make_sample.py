"""Sinh data/sample.json: dữ liệu MINH HOẠ (bé giả, điểm giả) để xem/thiết kế web khi chưa có dữ liệu thật.
Dùng đúng công thức điểm và luật nhận định của pipeline (edux_lib). Chạy: python -I -X utf8 tools/make_sample.py"""
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PIPE = os.path.normpath(os.path.join(HERE, '..', '..', 'tool', 'pipeline'))
sys.path.insert(0, PIPE)
import edux_lib as L  # noqa: E402

cfg = json.load(open(os.path.join(PIPE, 'config.json'), encoding='utf-8'))
F, G = cfg['formula'], cfg['diag']
HP = cfg['hp_order']
rnd = random.Random(20261016)

HO = ['Nguyễn', 'Trần', 'Lê', 'Phạm', 'Hoàng', 'Vũ', 'Đặng', 'Bùi', 'Đỗ', 'Ngô']
DEM = ['Minh', 'Gia', 'Bảo', 'Khánh', 'Ngọc', 'Hà', 'Anh', 'Tuệ', 'Hải', 'Thanh']
TEN = ['An', 'Bình', 'Chi', 'Dũng', 'Giang', 'Hân', 'Khoa', 'Lam', 'My', 'Nam', 'Phát', 'Quân', 'Sơn', 'Trang', 'Uyên', 'Vy', 'Yến', 'Đạt', 'Hiếu', 'Linh']

# khối -> 6 lớp (1A1..1A6, 2A1..2A6); mỗi lớp 20-30 bé; năng lực nền của lớp rải 0.72-0.95
KHOI = {'1': ['1A%d' % i for i in range(1, 7)], '2': ['2A%d' % i for i in range(1, 7)]}
BASE_N = {'Nhận biết số': 8, 'Đếm': 14, 'Cộng': 9, 'Trừ': 7, 'So sánh': 7}


def hp_stat(skill, hp):
    hard = {'Nhận biết số': 0, 'Đếm': 0, 'Cộng': .12, 'Trừ': .2, 'So sánh': .1}[hp]
    n = max(2, int(rnd.gauss(BASE_N[hp], 3)))
    p = min(.99, max(.25, skill - hard + rnd.gauss(0, .06)))
    ok = sum(rnd.random() < p for _ in range(n))
    base_t = 3.0 + (1 - skill) * 8 + hard * 6
    t = max(1.6, rnd.gauss(base_t, 1.4))
    tm = max(1.6, t + rnd.gauss(0, .6))
    acc = ok / n
    k = L.speed_k(F, t) if ok else 1.0
    pt = min(100, F['base'] * acc * k)
    if n < G['min_n']:
        code = 'few'
    elif 100 * acc >= G['acc_ok']:
        code = 'good' if tm <= G['fast_s'] else 'slow_ok' if tm >= G['slow_s'] else 'ok'
    else:
        code = 'fast_wrong' if tm <= G['fast_s'] else 'slow_wrong' if tm >= G['slow_s'] else 'shaky'
    return {'n': n, 'ok': ok, 'acc': round(100 * acc, 1), 't': round(t, 2), 'k': round(k, 3), 'pt': round(pt, 1),
            'low': n < F['low_sample'], 'diag': code, 'tmed': round(tm, 2), 'few': n < G['few_n']}


students, seq = [], {}
for khoi, lops in KHOI.items():
    yy = 20 if khoi == '1' else 19          # năm sinh: khối 1 sinh 2020, khối 2 sinh 2019
    for lop in lops:
        base = rnd.uniform(.72, .95) + (.02 if khoi == '2' else 0)
        for _ in range(rnd.randint(20, 30)):
            seq[yy] = seq.get(yy, 0) + 1
            name = '%s %s %s' % (rnd.choice(HO), rnd.choice(DEM), rnd.choice(TEN))
            st = {'code': 'CASA-%d%03d' % (yy, seq[yy]), 'name': name, 'alias': name.split(' ', 1)[1],
                  'khoi': khoi, 'lop': lop, 'mon': {}}
            if rnd.random() > .05:                      # ~5% chưa có điểm
                skill = min(.98, max(.2, rnd.gauss(base, .13)))
                played = [h for h in HP if rnd.random() < .88] or HP[:2]
                hp = {h: hp_stat(skill, h) for h in played}
                st['mon']['Toán'] = {'score': round(sum(v['pt'] for v in hp.values()) / len(hp), 1),
                                     'n': sum(v['n'] for v in hp.values()), 'hp': hp}
            students.append(st)

rep = {'version': 1, 'sample': True,
       'ky': {'from': '2026-10-08', 'to': '2026-10-15', 'label': '08/10/2026 → 15/10/2026'},
       'formula': {k: F[k] for k in ('cuts', 'low_sample', 'few_total', 'base', 'kmax', 'tfast', 'tslow')},
       'diag': {k: {'label': v[0], 'advice': v[1], 'color': v[2]} for k, v in L.DIAG.items()},
       'mon': [{'id': 'Toán', 'hp': HP}],
       'students': students}
dst = os.path.join(HERE, '..', 'data', 'sample.json')
os.makedirs(os.path.dirname(dst), exist_ok=True)
json.dump(rep, open(dst, 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=1)
print('Ghi', os.path.normpath(dst), len(students), 'bé')
