"""Chạy toàn bộ: Google Sheet (CSV) -> DB theo học sinh -> điểm -> Excel + phiếu A4.

  python -I -X utf8 run.py --from 2026-10-08 --to 2026-10-08                 # tải thẳng từ Sheet
  python -I -X utf8 run.py --csv sheet.csv --from 2026-10-01 --to 2026-10-31  # từ file CSV đã tải
Kết quả vào --out (mặc định ./ketqua): KetQua_<môn>_<kỳ>.xlsx, phieu/<mã>.html (+TatCa.html), out.json.
In PDF: chrome --headless=new --no-pdf-header-footer --print-to-pdf=<pdf> file:///<phieu/TatCa.html>
"""
import argparse
import json
import os
import shutil
import subprocess
import tempfile
from datetime import date

import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # chạy được cả với python -I
import edux_lib as L


def find_browser():
    for p in (r'C:\Program Files\Google\Chrome\Application\chrome.exe', r'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
              os.path.expandvars(r'%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe'),
              r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe', r'C:\Program Files\Microsoft\Edge\Application\msedge.exe'):
        if os.path.exists(p):
            return p
    return shutil.which('chrome') or shutil.which('google-chrome') or shutil.which('msedge')


def to_pdf(browser, html, pdf):
    """In 1 file HTML ra PDF A4 bằng Chrome/Edge headless (profile tạm để không đụng Chrome đang mở)."""
    with tempfile.TemporaryDirectory() as prof:
        subprocess.run([browser, '--headless=new', '--disable-gpu', '--no-pdf-header-footer', '--user-data-dir=' + prof,
                        '--print-to-pdf=' + os.path.abspath(pdf), 'file:///' + os.path.abspath(html).replace(os.sep, '/')],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
    return os.path.exists(pdf) and os.path.getsize(pdf) > 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--csv', help='URL export CSV hoặc file .csv (mặc định: sheet_csv_url trong config.json)')
    ap.add_argument('--from', dest='dfrom')
    ap.add_argument('--to', dest='dto')
    ap.add_argument('--out', default=os.path.join(L.HERE, 'ketqua'))
    ap.add_argument('--db', default=None, help='thư mục DB theo học sinh (mặc định ./db)')
    ap.add_argument('--no-db', action='store_true', help='không ghi/gộp DB, chấm thẳng từ lần tải này')
    ap.add_argument('--no-xlsx', action='store_true')
    ap.add_argument('--no-pages', action='store_true')
    ap.add_argument('--no-pdf', action='store_true', help='không in PDF (chỉ ra HTML)')
    a = ap.parse_args()
    if not a.dfrom and not a.dto:
        a.dfrom = a.dto = date.today().isoformat()   # mặc định: hôm nay
    cfg = L.load_config()
    rows = L.fetch_csv(a.csv or cfg['sheet_csv_url'])
    rounds, st, not_roster = L.ingest(cfg, rows)
    print('Nạp: %d dòng; %d trùng, %d sai đồng hồ/TEST, %d ngoài lớp, %d chưa gán; %d round của học sinh (%d gán nhờ nhận diện)'
          % (st['rows'], st['duplicates'], st['bad_clock_or_test'], st['not_in_roster'], st['unassigned'], st['rounds'], st['via_recognition']))
    big = [(n, c) for n, c in not_roster.most_common() if c >= 5 and n]
    if big:
        print('⚠ Tên ngoài danh sách lớp (>=5 round) - nếu là bé thật, thêm vào roster trong config.json:', ', '.join('%s(%d)' % x for x in big[:8]))
    if st['bad_clock_or_test']:
        print('⚠ %d dòng bị bỏ vì đồng hồ máy sai năm (K02 chưa đồng bộ giờ) hoặc TEST' % st['bad_clock_or_test'])
    if a.no_db:
        db_rows = rounds
    else:
        new, total = L.db_merge(rounds, a.db)
        print('DB: +%d round mới, tổng %d' % (new, total))
        db_rows = list(L.db_load(a.db).values())
    out, prows, excluded = L.score(cfg, db_rows, a.dfrom, a.dto)
    same = a.dfrom and a.dfrom == a.dto
    period = ('%s/%s/%s' % (a.dfrom[8:10], a.dfrom[5:7], a.dfrom[:4])) if same else '%s -> %s' % (a.dfrom or 'đầu', a.dto or 'nay')
    tag = (a.dfrom or 'all').replace('-', '') + ('' if same else '_' + (a.dto or 'nay').replace('-', ''))
    os.makedirs(a.out, exist_ok=True)
    with open(os.path.join(a.out, 'out.json'), 'w', encoding='utf-8') as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)
    for d in out:
        if 'toan' in d:
            detail = 'Toán=%.1f %s | ' % (d['toan'], L.LEVEL_NAMES[L.level(cfg['formula'], d['toan'])])
            detail += ', '.join('%s %d/%d=%.0f' % (h, v['ok'], v['n'], v['pt']) for h, v in d['hp'].items())
        else:
            detail = '—'
        print('%s %-24s n=%-3d %s' % (d['code'], d['name'], d['n'], detail))
    if excluded:
        print('Loại khi tính điểm:', dict(excluded))
    if not a.no_xlsx:
        import xl
        p = os.path.join(a.out, 'KetQua_%s_%s.xlsx' % (cfg['mon'], tag))
        xl.build(cfg, out, st, excluded, not_roster, p, period)
        print('Excel:', p)
    if not a.no_pages:
        import gen
        pd = os.path.join(a.out, 'phieu')
        os.makedirs(pd, exist_ok=True)
        shutil.copy(os.path.join(L.HERE, '..', '..', 'mau', 'logo.png'), os.path.join(pd, 'logo.png'))
        n = gen.build(cfg, out, prows, pd, period)
        print('Phiếu A4:', n, 'bé ->', pd)
        if not a.no_pdf:
            br = find_browser()
            if not br:
                print('Không tìm thấy Chrome/Edge để in PDF - mở phieu/TatCa.html rồi In -> Lưu PDF')
            else:
                if to_pdf(br, os.path.join(pd, 'TatCa.html'), os.path.join(a.out, 'BaoCao_TatCa_%s.pdf' % tag)):
                    print('PDF gộp:', os.path.join(a.out, 'BaoCao_TatCa_%s.pdf' % tag))
                od = os.path.join(a.out, 'pdf')
                os.makedirs(od, exist_ok=True)
                ok = 0
                for d in out:
                    if 'toan' in d:
                        nm = '%s_%s.pdf' % (d['code'], (d['alias'] or d['name']).replace(' ', ''))
                        ok += to_pdf(br, os.path.join(pd, d['code'] + '.html'), os.path.join(od, nm))
                print('PDF từng bé: %d file ->' % ok, od)


if __name__ == '__main__':
    main()
