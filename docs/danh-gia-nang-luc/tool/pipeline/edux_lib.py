"""Thư viện pipeline đánh giá năng lực: Google Sheet (CSV) → DB theo học sinh → điểm.

Chạy qua run.py. Cấu hình ở config.json. Python 3.9+, chỉ dùng thư viện chuẩn (xlsx cần openpyxl).
"""
import bisect
import collections
import csv
import hashlib
import io
import json
import os
import re
import urllib.request
from datetime import datetime

HERE = os.path.dirname(os.path.abspath(__file__))


def load_config(path=None):
    with open(path or os.path.join(HERE, 'config.json'), encoding='utf-8') as f:
        return json.load(f)


# ── 1. Đọc Sheet ──────────────────────────────────────────────────────────────

def fetch_csv(url_or_path):
    """URL (export csv) hoặc đường dẫn file .csv → list[dict]."""
    if re.match(r'https?://', url_or_path):
        raw = None
        try:
            with urllib.request.urlopen(url_or_path, timeout=20) as r:
                raw = r.read().decode('utf-8-sig')
        except Exception as e:  # một số máy Python bị treo SSL handshake trong khi curl vẫn tải được
            print('urllib lỗi (%s) - thử curl' % type(e).__name__)
            import subprocess
            import tempfile
            tmp = os.path.join(tempfile.gettempdir(), 'edux_sheet.csv')
            subprocess.run(['curl', '-sL', '--max-time', '180', '-o', tmp, url_or_path], check=True)
            with open(tmp, encoding='utf-8-sig') as f:
                raw = f.read()
        if raw.lstrip().lower().startswith('<!doctype html') or '<html' in raw[:200].lower():
            raise RuntimeError('Sheet không trả về CSV (có thể chưa mở quyền xem bằng link): %s' % url_or_path)
    else:
        with open(url_or_path, encoding='utf-8-sig') as f:
            raw = f.read()
    return list(csv.DictReader(io.StringIO(raw)))


def num(s):
    """'3,79' / '3.79' / '' → float | None (Sheet locale vi dùng dấu phẩy)."""
    if s is None or s == '':
        return None
    try:
        return float(str(s).replace(',', '.'))
    except ValueError:
        return None


def boolean(s):
    s = str(s).strip().upper()
    return True if s == 'TRUE' else False if s == 'FALSE' else None


def parse_ts(s):
    try:
        return datetime.strptime(s[:23], '%Y-%m-%d %H:%M:%S.%f')
    except ValueError:
        try:
            return datetime.strptime(s[:19], '%Y-%m-%d %H:%M:%S')
        except ValueError:
            return None


# ── 2. Chuẩn hoá + loại trùng + gán học sinh ───────────────────────────────────

def norm_game(cfg, g):
    return cfg['game_alias'].get(g, g)


def student_lookup(cfg):
    """Tên đầy đủ HOẶC tên thường gọi (alias) → học sinh. Log cũ ghi alias (vd 'Đăng Bách')."""
    d = {}
    for s in cfg['roster']:
        d[s['name']] = s
        if s.get('alias'):
            d.setdefault(s['alias'], s)
        for a in s.get('also', []):          # tên cũ/biệt danh khác còn xuất hiện trong log
            d.setdefault(a, s)
    return d


def dedupe(rows):
    """Loại dòng trùng: theo `rid` nếu có (log mới), không thì theo nội dung y hệt (log cũ)."""
    seen, out, dup = set(), [], 0
    for r in rows:
        k = ('rid', r['rid']) if r.get('rid') else ('raw', tuple(r.values()))
        if k in seen:
            dup += 1
            continue
        seen.add(k)
        out.append(r)
    return out, dup


def row_key(r):
    if r.get('rid'):
        return 'r:' + r['rid']
    return 'h:' + hashlib.sha1('|'.join(str(v) for v in r.values()).encode('utf-8')).hexdigest()[:16]


def ingest(cfg, rows):
    """Sheet rows → (rounds đã gán học sinh, thống kê). Mỗi round là dict phẳng."""
    st = collections.Counter()
    st['rows'] = len(rows)
    rows, dup = dedupe(rows)
    st['duplicates'] = dup
    byname = student_lookup(cfg)
    generic = set(cfg['generic_names'])

    ev = []
    for r in rows:
        ts = parse_ts(r.get('timestamp', ''))
        if ts is None or ts.year < cfg['min_valid_year']:
            st['bad_clock_or_test'] += 1
            continue
        r['_ts'] = ts
        ev.append(r)
    ev.sort(key=lambda r: r['_ts'])

    # nhận diện thành công, theo ô
    rec = {'left': ([], []), 'right': ([], [])}
    for r in ev:
        if r['eventType'] == 'recognition' and boolean(r.get('recognized')) and r['playerName'] in byname:
            slot = r.get('slot')
            if slot in rec:
                rec[slot][0].append(r['_ts'])
                rec[slot][1].append((r['playerName'], norm_game(cfg, r['gameName'])))

    rounds, not_roster = [], collections.Counter()
    for r in ev:
        if r['eventType'] != 'round_end':
            continue
        game = norm_game(cfg, r['gameName'])
        slot = r.get('slot')
        if slot in ('0', '1'):
            slot = 'left' if slot == '0' else 'right'
        elif not slot:
            slot = (r.get('side') or r.get('team') or '').lower() or None
        ok = boolean(r.get('correct'))
        if ok is None:
            ok = boolean(r.get('isCorrect'))
        t = num(r.get('responseTimeSec'))
        if ok is None or t is None:
            st['no_result'] += 1
            continue
        name, how = r.get('playerName', ''), 'app'
        # Tên trong dòng chỉ đáng tin với dòng "dạng mới" (có `team`: MiniGameKit / LogRound — tên đã được app
        # gán từ nhận diện; đối chiếu 08/10: khớp log local 300/300). Dòng GameLogger cũ (chỉ có `side`) ghi tên
        # lúc vào ván, hay cũ/sai → bỏ, dùng nhận diện gần nhất.
        trust = cfg['name_priority'] == 'app' or (cfg['name_priority'] == 'schema' and r.get('team'))
        if name in generic or not name or not trust:
            how = 'recognition'
            ts_list, names = rec.get(slot, ([], []))
            i = bisect.bisect_right(ts_list, r['_ts']) - 1   # nhận diện gần nhất TRƯỚC round
            cand = []
            if i >= 0:
                cand.append(((r['_ts'] - ts_list[i]).total_seconds(), names[i][0]))
            if i + 1 < len(ts_list):                          # hoặc SAU round: round đầu ván có thể được ghi
                cand.append(((ts_list[i + 1] - r['_ts']).total_seconds(), names[i + 1][0]))  # trước khi nhận diện xong
            name = ''
            if cand:
                d, nm = min(cand, key=lambda x: x[0])
                if d <= cfg['recog_max_gap_sec']:
                    name = nm
            if not name:
                st['unassigned'] += 1
                continue
        if name not in byname:
            not_roster[name] += 1
            st['not_in_roster'] += 1
            continue
        rounds.append({
            'key': row_key(r), 'ts': r['_ts'].strftime('%Y-%m-%d %H:%M:%S.%f')[:23],
            'code': byname[name]['code'], 'name': byname[name]['name'], 'game': game, 'slot': slot,
            'qid': r.get('questionId', ''), 'ok': ok, 't': t, 'round': r.get('round', ''),
            'sess': r.get('sessionId', ''), 'dev': r.get('deviceId', ''), 'how': how,
        })
    st['rounds'] = len(rounds)
    st['via_recognition'] = sum(1 for x in rounds if x['how'] == 'recognition')
    return rounds, st, not_roster


def ingest_local(cfg, folder):
    """Thư mục log local (GameLogs/YYYY-MM-DD_HHMMSS_<Game>.json của PlayerRecognitionService) → rounds.

    Mỗi file = 1 phiên, mỗi ô (trái/phải) = 1 bé: bé của ô = tên (không phải tên chung) xuất hiện nhiều round nhất;
    round chưa nhận diện (Player_x, Blue_1, Red_1) gán cho bé đó. `slot_override` / `skip_files` trong config để chốt tay.
    Tên trong log local là tên app đã gán từ nhận diện nên tốt hơn log trên Sheet.
    """
    import glob
    st = collections.Counter()
    byname = student_lookup(cfg)
    generic = set(cfg['generic_names'])
    skip = set(cfg.get('skip_files', []))
    ov = cfg.get('slot_override', {})
    rounds, not_roster = [], collections.Counter()
    for f in sorted(glob.glob(os.path.join(folder, '*.json'))):
        base = os.path.basename(f)
        if not re.match(r'^\d{4}-\d\d-\d\d_\d{6}_', base):
            continue                      # file session kiểu cũ của GameLogger (Counting5_2026-...json)
        st['files'] += 1
        if base in skip:
            st['files_skipped'] += 1
            continue
        try:
            with open(f, encoding='utf-8-sig') as fh:
                d = json.load(fh)
        except Exception:
            st['files_bad'] += 1          # 0 byte / hỏng
            continue
        es = [e for e in d.get('entries', []) if e.get('eventType') == 'round']
        if not es:
            continue
        if int(es[0]['time'][:4]) < cfg['min_valid_year']:
            st['bad_clock_or_test'] += len(es)
            continue
        for slot in ('left', 'right'):
            rr = [e for e in es if e['slot'] == slot]
            if not rr:
                continue
            cnt = collections.Counter(e['name'] for e in rr if e['name'] not in generic)
            kid = ov.get(base + '|' + slot) or (cnt.most_common(1)[0][0] if cnt else None)
            if not kid:
                st['unassigned'] += len(rr)
                continue
            if kid not in byname:
                not_roster[kid] += len(rr)
                st['not_in_roster'] += len(rr)
                continue
            for i, e in enumerate(rr):
                rounds.append({
                    'key': 'l:%s:%s:%d' % (base, slot, i), 'ts': e['time'] + '.000',
                    'code': byname[kid]['code'], 'name': byname[kid]['name'], 'game': norm_game(cfg, e['game']), 'slot': slot,
                    'qid': e.get('questionId', ''), 'ok': bool(e['correct']), 't': float(e['answerTimeSec']),
                    'round': e.get('round', ''), 'sess': base[:-5], 'dev': '', 'how': 'session',
                })
    st['rounds'] = len(rounds)
    return rounds, st, not_roster


# ── 3. DB theo học sinh (tích luỹ, lưu theo mã) ────────────────────────────────

def db_dir(base=None):
    d = base or os.path.join(HERE, 'db')
    os.makedirs(d, exist_ok=True)
    return d


def db_load(base=None):
    d = db_dir(base)
    out = {}
    for f in os.listdir(d):
        if f.startswith('CASA-') and f.endswith('.jsonl'):
            with open(os.path.join(d, f), encoding='utf-8') as fh:
                for line in fh:
                    if line.strip():
                        x = json.loads(line)
                        out[x['key']] = x
    return out


def db_merge(rounds, base=None):
    """Gộp rounds mới vào DB (khoá `key`, chạy lại không nhân đôi). Trả (mới, tổng)."""
    d = db_dir(base)
    cur = db_load(base)
    new = 0
    for x in rounds:
        if x['key'] not in cur:
            new += 1
        cur[x['key']] = x
    per = collections.defaultdict(list)
    for x in cur.values():
        per[x['code']].append(x)
    for code, lst in per.items():
        lst.sort(key=lambda x: x['ts'])
        with open(os.path.join(d, code + '.jsonl'), 'w', encoding='utf-8', newline='\n') as fh:
            for x in lst:
                fh.write(json.dumps(x, ensure_ascii=False) + '\n')
    return new, len(cur)


# ── 4. Điểm ─────────────────────────────────────────────────────────────────────

def speed_k(f, t):
    if t is None or t >= f['tslow']:
        return 1.0
    if t <= f['tfast'] or f['tslow'] <= f['tfast']:
        return f['kmax']
    return 1 + (f['kmax'] - 1) * (f['tslow'] - t) / (f['tslow'] - f['tfast'])


def level(f, p):
    c = f['cuts']
    return 3 if p >= c[2] else 2 if p >= c[1] else 1 if p >= c[0] else 0


LEVEL_NAMES = ['Mức 1', 'Mức 2', 'Mức 3', 'Đạt mục tiêu']


def hp_of(cfg, x):
    if x['game'] == 'TongHopToan':
        m = re.match(r'THT_\d+_(\w+)', x['qid'] or '')
        return cfg['tht_hp'].get(m.group(1)) if m else None
    return cfg['game_hp'].get(x['game'])


def session_label(cfg, rows_sorted, gap):
    """Phiên cho log chưa có sessionId: chuỗi round cùng game cách nhau < gap giây."""
    last, cur, n = {}, {}, 0
    for x in rows_sorted:
        if x['sess']:
            x['_sess'] = x['sess']
            continue
        t = parse_ts(x['ts'])
        k = x['game']
        if k not in last or (t - last[k]).total_seconds() > gap:
            n += 1
            cur[k] = '%s_%s_%s' % (x['ts'][:10], x['ts'][11:19].replace(':', ''), x['game'])
        last[k] = t
        x['_sess'] = cur[k]


DIAG = {   # mã -> (nhãn ngắn, lời khuyên, màu)
    'good': ('Thành thạo', 'Đúng và nhanh: có thể tăng độ khó.', '#1f7a52'),
    'ok': ('Khá chắc', 'Đúng, tốc độ vừa phải: luyện thêm cho quen.', '#1f7a52'),
    'slow_ok': ('Biết nhưng chậm', 'Đúng nhiều nhưng cần nhiều thời gian: kiến thức có rồi, cần luyện cho thạo.', '#4a7fb0'),
    'fast_wrong': ('Nhanh nhưng hay nhầm', 'Trả lời vội/đoán: nhắc bé nhìn kỹ rồi mới chọn; không phải do chưa biết.', '#c0782a'),
    'slow_wrong': ('Chậm và sai: chưa nắm', 'Chưa biết cách làm nên lâu và sai: cần dạy lại, làm mẫu từng bước.', '#b23a3a'),
    'shaky': ('Chưa vững', 'Đúng chưa đều, tốc độ vừa: luyện thêm.', '#8a6d1a'),
    'few': ('Chưa đủ dữ liệu', 'Quá ít câu để kết luận.', '#8a929c'),
}


def diagnose(cfg, a):
    """Nhận định 1 học phần của 1 bé từ danh sách câu `a` (dict có ok, t). Không gộp giữa các học phần:
    nhanh+sai (vội/đoán) khác hẳn chậm+sai (chưa biết)."""
    g = cfg['diag']
    n = len(a)
    ts = sorted(x['t'] for x in a)
    tmed = ts[n // 2] if n % 2 else (ts[n // 2 - 1] + ts[n // 2]) / 2
    acc = 100.0 * sum(x['ok'] for x in a) / n
    if n < g['min_n']:
        code = 'few'
    elif acc >= g['acc_ok']:
        code = 'good' if tmed <= g['fast_s'] else 'slow_ok' if tmed >= g['slow_s'] else 'ok'
    else:
        code = 'fast_wrong' if tmed <= g['fast_s'] else 'slow_wrong' if tmed >= g['slow_s'] else 'shaky'
    return dict(code=code, tmed=tmed, acc=acc, n=n, few=n < g['few_n'])


def score(cfg, db_rows, date_from=None, date_to=None):
    """DB → (out theo học sinh, rows dùng để vẽ phiếu, thống kê loại trừ)."""
    f = cfg['formula']
    excluded = collections.Counter()
    use = []
    for x in sorted(db_rows, key=lambda x: x['ts']):
        d = x['ts'][:10]
        if (date_from and d < date_from) or (date_to and d > date_to):
            continue
        if x['game'] not in cfg['scored_games']:
            excluded['game không tính điểm: ' + x['game']] += 1
            continue
        hit = next((r for r in cfg['exclude_ranges'] if r['from'] <= x['ts'][:16] <= r['to']), None)
        if hit:
            excluded['loại khoảng giờ: ' + hit['why']] += 1
            continue
        if x['t'] < 0:
            excluded['thời gian âm'] += 1
            continue
        hp = hp_of(cfg, x)
        if not hp:
            excluded['không map được học phần'] += 1
            continue
        x['hp'] = hp
        use.append(x)
    session_label(cfg, use, cfg['session_gap_sec'])
    # phiên quá ngắn của 1 bé: bỏ dở
    cnt = collections.Counter((x['_sess'], x['code'], x['slot']) for x in use)
    keep = []
    for x in use:
        if cnt[(x['_sess'], x['code'], x['slot'])] < cfg['min_rounds_per_session']:
            excluded['phiên bỏ dở (<%d câu của 1 bé)' % cfg['min_rounds_per_session']] += 1
        else:
            keep.append(x)
    rows = [dict(kid=x['name'], code=x['code'], hp=x['hp'], ok=x['ok'], t=x['t'], game=x['game'], ts=x['ts'], sess=x['_sess']) for x in keep]

    out = []
    for s in cfg['roster']:
        rs = [r for r in rows if r['code'] == s['code']]
        d = {'code': s['code'], 'name': s['name'], 'alias': s.get('alias', ''), 'class': s.get('class', ''), 'n': len(rs), 'hp': {}}
        for hp in cfg['hp_order']:
            a = sorted([r for r in rs if r['hp'] == hp], key=lambda r: r['ts'])[-f['window']:]
            if not a:
                continue
            ok = sum(r['ok'] for r in a)
            ts = [r['t'] for r in a if r['ok'] and r['t'] >= cfg['min_resp_sec']]
            tm = sum(ts) / len(ts) if ts else None
            k = speed_k(f, tm) if tm is not None else 1.0
            acc = ok / len(a)
            d['hp'][hp] = dict(n=len(a), ok=ok, acc=100 * acc, t=tm, k=k, pt=min(100, f['base'] * acc * k), low=len(a) < f['low_sample'],
                               diag=diagnose(cfg, a))
        if d['hp']:
            d['toan'] = sum(v['pt'] for v in d['hp'].values()) / len(d['hp'])
        d['sess'] = sorted({r['sess'] for r in rs})
        out.append(d)
    return out, rows, excluded
