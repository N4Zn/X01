"""Kiểm tra log online (Sheet) có khớp log local (GameLogs/*.json) không.

  python -I -X utf8 compare_local.py --local <thư mục GameLogs> --date 2026-10-08 [--csv sheet.csv] [--after 15:46:00]

So sánh round_end trên Sheet với entry `round` trong file log local theo (giây, ô, questionId, game):
số dòng chung / chỉ Sheet / chỉ local, và tên học sinh có trùng không.
"""
import argparse
import glob
import json
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # chạy được cả với python -I
import edux_lib as L


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--csv', default=None)
    ap.add_argument('--local', required=True)
    ap.add_argument('--date', required=True)
    ap.add_argument('--after', default='00:00:00', help='bỏ round trước giờ này (phiên thử)')
    a = ap.parse_args()
    cfg = L.load_config()
    rows, _ = L.dedupe(L.fetch_csv(a.csv or cfg['sheet_csv_url']))
    lo = '%s %s' % (a.date, a.after)
    sheet = {}
    for r in rows:
        if r['eventType'] != 'round_end' or not r['timestamp'].startswith(a.date) or r['timestamp'] < lo:
            continue
        slot = {'0': 'left', '1': 'right'}.get(r['slot']) or (r['side'] or '').lower()
        sheet[(r['timestamp'][:19], slot, r['questionId'], L.norm_game(cfg, r['gameName']))] = r['playerName']
    local = {}
    for f in glob.glob(os.path.join(a.local, a.date + '_*.json')):
        try:
            with open(f, encoding='utf-8-sig') as fh:
                d = json.load(fh)
        except Exception:
            continue
        for e in d.get('entries', []):
            if e['eventType'] == 'round' and e['time'] >= lo:
                local[(e['time'], e['slot'], e['questionId'], L.norm_game(cfg, e['game']))] = e['name']
    ks, kl = set(sheet), set(local)
    print('%-20s %7s %8s %8s %8s %8s' % ('game', 'chung', 'chiSheet', 'chiLocal', 'khacTen', 'khop%'))
    for g in sorted({k[3] for k in ks | kl}):
        s = {k for k in ks if k[3] == g}
        lc = {k for k in kl if k[3] == g}
        c = s & lc
        dn = sum(1 for k in c if sheet[k] != local[k])
        tot = len(s | lc)
        print('%-20s %7d %8d %8d %8d %7.1f%%' % (g, len(c), len(s - lc), len(lc - s), dn, 100 * (len(c) - dn) / tot if tot else 100))
    print('chiSheet = có online, không có local (phiên thử/trước --after hoặc game chưa đồng bộ giờ);')
    print('chiLocal = có local, chưa thấy online (chưa đẩy kịp, hoặc game chưa đẩy lên Sheet);')
    print('khacTen = cùng round nhưng tên học sinh online khác local.')


if __name__ == '__main__':
    main()
