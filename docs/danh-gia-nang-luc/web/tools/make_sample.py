"""Sinh data/sample.json: dữ liệu MINH HOẠ (bé giả, điểm giả) để xem/thiết kế web khi chưa có dữ liệu thật.
Dùng đúng công thức điểm và luật nhận định của pipeline (edux_lib). 3 môn (Toán, Tiếng Việt, Khám phá), học phần
khác nhau theo khối (2 khối học chương trình khác nhau). Chạy: python -I -X utf8 tools/make_sample.py"""
import json
import os
import random
from datetime import date, timedelta
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PIPE = os.path.normpath(os.path.join(HERE, '..', '..', 'tool', 'pipeline'))
sys.path.insert(0, PIPE)
import edux_lib as L  # noqa: E402

cfg = json.load(open(os.path.join(PIPE, 'config.json'), encoding='utf-8'))
F, G = cfg['formula'], cfg['diag']
rnd = random.Random(20261016)

HO = ['Nguyễn', 'Trần', 'Lê', 'Phạm', 'Hoàng', 'Vũ', 'Đặng', 'Bùi', 'Đỗ', 'Ngô']
DEM = ['Minh', 'Gia', 'Bảo', 'Khánh', 'Ngọc', 'Hà', 'Anh', 'Tuệ', 'Hải', 'Thanh']
TEN = ['An', 'Bình', 'Chi', 'Dũng', 'Giang', 'Hân', 'Khoa', 'Lam', 'My', 'Nam', 'Phát', 'Quân', 'Sơn', 'Trang', 'Uyên', 'Vy', 'Yến', 'Đạt', 'Hiếu', 'Linh']

# khối -> 6 lớp (1A1..1A6, 2A1..2A6); mỗi lớp 20-30 bé
KHOI = {'1': ['1A%d' % i for i in range(1, 7)], '2': ['2A%d' % i for i in range(1, 7)]}
# môn -> khối -> học phần (khối khác nhau học khác nhau)
MON = {
    'Toán': {'1': ['Nhận biết số', 'Đếm', 'Cộng', 'Trừ', 'So sánh'], '2': ['Cộng', 'Trừ', 'So sánh', 'Hình học', 'Đo lường']},
    'Tiếng Việt': {'1': ['Nhận biết chữ', 'Ghép vần', 'Đọc từ', 'Nghe hiểu'], '2': ['Đọc từ', 'Đọc câu', 'Chính tả', 'Nghe hiểu', 'Từ vựng']},
    'Khám phá': {'1': ['Con vật', 'Cây cối', 'Cơ thể em', 'Thời tiết'], '2': ['Con vật', 'Thực vật', 'Cơ thể người', 'Thời tiết', 'An toàn']},
}
HARD = {'Cộng': .12, 'Trừ': .2, 'So sánh': .1, 'Hình học': .15, 'Đo lường': .22, 'Ghép vần': .1, 'Đọc câu': .14, 'Chính tả': .2, 'Từ vựng': .12, 'An toàn': .08}
BASE_N = {'Đếm': 14, 'Nhận biết số': 8, 'Cộng': 9, 'Trừ': 7, 'So sánh': 7}


def hp_stat(skill, hp, nscale=1.0):
    hard = HARD.get(hp, 0)
    n = max(2, int(rnd.gauss(BASE_N.get(hp, 8) * nscale, 2)))
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


WEEKS = []
d0 = date(2026, 9, 7)                       # 6 tuần: T37..T42 (tuần cuối đang diễn ra)
for i in range(6):
    a_, b_ = d0 + timedelta(days=7 * i), d0 + timedelta(days=7 * i + 6)
    WEEKS.append({'id': '2026-W%d' % a_.isocalendar()[1], 'from': a_.isoformat(), 'to': b_.isoformat(),
                  'label': 'T%d' % a_.isocalendar()[1], 'range': '%02d–%02d/%02d' % (a_.day, b_.day, b_.month)})


def merge(wk, hps):
    """Gộp các tuần -> thống kê học phần của cả kỳ (cùng công thức điểm / luật nhận định)."""
    hp = {}
    for h in hps:
        a = [w['hp'][h] for w in wk if h in w['hp']]
        if not a:
            continue
        n, ok = sum(x['n'] for x in a), sum(x['ok'] for x in a)
        t = sum(x['t'] * x['n'] for x in a) / n
        tm = sum(x['tmed'] * x['n'] for x in a) / n
        acc = ok / n
        k = L.speed_k(F, t) if ok else 1.0
        if 100 * acc >= G['acc_ok']:
            code = 'good' if tm <= G['fast_s'] else 'slow_ok' if tm >= G['slow_s'] else 'ok'
        else:
            code = 'fast_wrong' if tm <= G['fast_s'] else 'slow_wrong' if tm >= G['slow_s'] else 'shaky'
        hp[h] = {'n': n, 'ok': ok, 'acc': round(100 * acc, 1), 't': round(t, 2), 'k': round(k, 3), 'pt': round(min(100, F['base'] * acc * k), 1),
                 'low': n < F['low_sample'], 'diag': code, 'tmed': round(tm, 2), 'few': n < G['few_n']}
    return hp


students, seq = [], {}
for khoi, lops in KHOI.items():
    yy = 20 if khoi == '1' else 19          # năm sinh: khối 1 sinh 2020, khối 2 sinh 2019
    lazy = rnd.choice(lops[1:])              # 1 lớp chơi rất ít (để thấy "chơi ít / chưa chơi")
    for lop in lops:
        base_m = {m: rnd.uniform(.70, .93) for m in MON}      # mỗi lớp mạnh/yếu khác nhau theo môn
        slope_l = rnd.uniform(-.004, .026)    # xu hướng của lớp (điểm/tuần ~ 0-3)
        engage = rnd.uniform(.62, .95) if lop != lazy else rnd.uniform(.28, .4)
        p_none = .05 if lop != lazy else .22
        for _ in range(rnd.randint(20, 30)):
            seq[yy] = seq.get(yy, 0) + 1
            name = '%s %s %s' % (rnd.choice(HO), rnd.choice(DEM), rnd.choice(TEN))
            st = {'code': 'CASA-%d%03d' % (yy, seq[yy]), 'name': name, 'alias': name.split(' ', 1)[1],
                  'khoi': khoi, 'lop': lop, 'mon': {}}
            if rnd.random() > p_none:
                skill0 = {m: min(.97, max(.25, rnd.gauss(base_m[m] - 2.5 * slope_l, .12))) for m in MON}
                slope = slope_l + rnd.gauss(0, .014)
                p_play = .3 if rnd.random() < .1 else min(.97, max(.2, rnd.gauss(engage, .12)))
                wk = {m: [] for m in MON}
                hist = {m: [] for m in MON}
                for i in range(len(WEEKS)):
                    present = rnd.random() <= p_play
                    played_m = [m for m in MON if rnd.random() < .8] or [rnd.choice(list(MON))]
                    for m in MON:
                        if not present or m not in played_m:
                            hist[m].append(None); continue
                        hps = MON[m][khoi]
                        sk = min(.98, max(.2, skill0[m] + slope * i + rnd.gauss(0, .02)))
                        played = [h for h in hps if rnd.random() < .9] or hps[:2]
                        w = {'hp': {h: hp_stat(sk, h, .45) for h in played}}
                        wk[m].append(w)
                        ws = list(w['hp'].values())
                        n = sum(x['n'] for x in ws)
                        hist[m].append({'score': round(sum(x['pt'] for x in ws) / len(ws), 1), 'n': n,
                                        'acc': round(100 * sum(x['ok'] for x in ws) / n, 1), 't': round(sum(x['t'] * x['n'] for x in ws) / n, 2),
                                        'days': rnd.randint(1, 4), 'hp': {h: x['pt'] for h, x in w['hp'].items()}})
                for m in MON:
                    if wk[m]:
                        hp = merge(wk[m], MON[m][khoi])
                        st['mon'][m] = {'score': round(sum(v['pt'] for v in hp.values()) / len(hp), 1),
                                        'n': sum(v['n'] for v in hp.values()), 'hp': hp, 'hist': hist[m]}
            students.append(st)

rep = {'version': 2, 'sample': True,
       'ky': {'from': WEEKS[0]['from'], 'to': '2026-10-16', 'label': '07/09/2026 → 16/10/2026'},
       'weeks': WEEKS,
       'formula': {k: F[k] for k in ('cuts', 'low_sample', 'few_total', 'base', 'kmax', 'tfast', 'tslow')},
       'diag': {k: {'label': v[0], 'advice': v[1], 'color': v[2]} for k, v in L.DIAG.items()},
       'mon': [{'id': m, 'hp': MON[m]} for m in MON],      # hp: danh sách, hoặc {khối: danh sách} khi các khối học khác nhau
       'students': students}
dst = os.path.join(HERE, '..', 'data', 'sample.json')
os.makedirs(os.path.dirname(dst), exist_ok=True)
with open(dst, 'w', encoding='utf-8') as fh:
    json.dump(rep, fh, ensure_ascii=False, separators=(',', ':'))
print('Ghi', os.path.normpath(dst), len(students), 'bé,', len(WEEKS), 'tuần,', len(MON), 'môn,', os.path.getsize(dst) // 1024, 'KB')
