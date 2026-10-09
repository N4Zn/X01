"""Chạy toàn bộ: log -> điểm -> Excel + phiếu A4 + PDF.

Nguồn log (chọn 1):
  --local <thư mục GameLogs>   log local của máy K02 (YYYY-MM-DD_HHMMSS_<Game>.json) - tên bé chính xác nhất
  --csv <URL|file.csv>         Google Sheet export CSV (mặc định: sheet_csv_url trong config.json)

Ví dụ:
  python -I -X utf8 run.py --local C:/.../GameLogs --from 2026-10-08 --to 2026-10-09 --per-day
      -> ketqua/2026-10-08/, ketqua/2026-10-09/ (từng ngày) + ketqua/2026-10-08_2026-10-09/ (gộp cả kỳ)
  python -I -X utf8 run.py --from 2026-10-09            (Sheet, 1 ngày -> ketqua/)
Mỗi thư mục kết quả có: KetQua_<môn>_<kỳ>.xlsx, BaoCao_TatCa_<kỳ>.pdf (trang đầu = tổng hợp lớp, rồi mỗi bé 1 trang A4), TongHopLop_<kỳ>.pdf, anh/<mã>_<tên>.png (ảnh từng bé), phieu/*.html, out.json.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from datetime import date, timedelta

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
    if os.path.exists(pdf):
        try:
            os.remove(pdf)
        except OSError:                      # file đang mở trong trình xem PDF -> ghi tên khác, không dùng bản cũ
            base, ext = os.path.splitext(pdf)
            pdf = base + '_moi' + ext
            print('  (file cũ đang mở, ghi thành %s)' % os.path.basename(pdf))
    with tempfile.TemporaryDirectory() as prof:
        subprocess.run([browser, '--headless=new', '--disable-gpu', '--no-pdf-header-footer', '--user-data-dir=' + prof,
                        '--print-to-pdf=' + os.path.abspath(pdf), 'file:///' + os.path.abspath(html).replace(os.sep, '/')],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=180)
    return os.path.exists(pdf) and os.path.getsize(pdf) > 0


def to_png(browser, html, png, scale=2):
    """Chụp 1 trang A4 (794x1123 px @96dpi, nhân scale) ra PNG bằng Chrome/Edge headless."""
    with tempfile.TemporaryDirectory() as prof:
        subprocess.run([browser, '--headless=new', '--disable-gpu', '--hide-scrollbars', '--user-data-dir=' + prof,
                        '--window-size=794,1123', '--force-device-scale-factor=%d' % scale, '--screenshot=' + os.path.abspath(png),
                        'file:///' + os.path.abspath(html).replace(os.sep, '/')],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=180)
    return os.path.exists(png) and os.path.getsize(png) > 0


def fmt_period(dfrom, dto):
    if dfrom and dfrom == dto:
        return '%s/%s/%s' % (dfrom[8:10], dfrom[5:7], dfrom[:4])
    f = lambda d: '%s/%s/%s' % (d[8:10], d[5:7], d[:4]) if d else '...'
    return '%s -> %s' % (f(dfrom), f(dto))


def report(cfg, db_rows, st, not_roster, dfrom, dto, outdir, a, by_day=None):
    out, prows, excluded = L.score(cfg, db_rows, dfrom, dto)
    period = fmt_period(dfrom, dto)
    tag = (dfrom or 'all').replace('-', '') + ('' if dfrom == dto else '_' + (dto or 'nay').replace('-', ''))
    os.makedirs(outdir, exist_ok=True)
    print('\n=== Kỳ %s -> %s' % (period, outdir))
    with open(os.path.join(outdir, 'out.json'), 'w', encoding='utf-8') as fh:
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
        p = os.path.join(outdir, 'KetQua_%s_%s.xlsx' % (cfg['mon'], tag))
        xl.build(cfg, out, st, excluded, not_roster, p, period, by_day)
        print('Excel:', p)
    if not a.no_pages:
        import gen
        pd = os.path.join(outdir, 'phieu')
        os.makedirs(pd, exist_ok=True)
        shutil.copy(os.path.join(L.HERE, '..', '..', 'mau', 'logo.png'), os.path.join(pd, 'logo.png'))
        n = gen.build(cfg, out, prows, pd, period)
        print('Phiếu A4:', n, 'bé')
        if not a.no_pdf:
            br = find_browser()
            if not br:
                print('Không tìm thấy Chrome/Edge để in PDF - mở phieu/TatCa.html rồi In -> Lưu PDF')
            else:
                allp = os.path.join(outdir, 'BaoCao_TatCa_%s.pdf' % tag)
                if to_pdf(br, os.path.join(pd, 'TatCa.html'), allp):
                    print('PDF gộp:', allp)
                lh = os.path.join(pd, 'LopHoc.html')
                if os.path.exists(lh) and to_pdf(br, lh, os.path.join(outdir, 'TongHopLop_%s.pdf' % tag)):
                    print('PDF tổng hợp lớp:', os.path.join(outdir, 'TongHopLop_%s.pdf' % tag))
                od = os.path.join(outdir, 'anh')
                os.makedirs(od, exist_ok=True)
                ok = 0
                if os.path.exists(lh):
                    to_png(br, lh, os.path.join(od, '00_TongHopLop.png'))
                for d in out:
                    if 'toan' in d:
                        nm = '%s_%s.png' % (d['code'], (d['alias'] or d['name']).replace(' ', ''))
                        ok += to_png(br, os.path.join(pd, d['code'] + '.html'), os.path.join(od, nm))
                print('Ảnh PNG từng bé: %d file ->' % ok, od)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--local', help='thư mục log local (GameLogs)')
    ap.add_argument('--csv', help='URL export CSV hoặc file .csv (mặc định: sheet_csv_url trong config.json)')
    ap.add_argument('--from', dest='dfrom')
    ap.add_argument('--to', dest='dto')
    ap.add_argument('--per-day', action='store_true', help='ngoài báo cáo gộp cả kỳ, ra thêm báo cáo từng ngày có dữ liệu')
    ap.add_argument('--out', default=os.path.join(L.HERE, 'ketqua'))
    ap.add_argument('--db', default=None, help='(chỉ với Sheet) thư mục DB theo học sinh, mặc định ./db')
    ap.add_argument('--no-db', action='store_true', help='(chỉ với Sheet) không ghi/gộp DB')
    ap.add_argument('--no-xlsx', action='store_true')
    ap.add_argument('--no-pages', action='store_true')
    ap.add_argument('--no-pdf', action='store_true', help='không in PDF (chỉ ra HTML)')
    a = ap.parse_args()
    if not a.dfrom and not a.dto:
        a.dfrom = a.dto = date.today().isoformat()   # mặc định: hôm nay
    cfg = L.load_config()

    if a.local:
        rounds, st, not_roster = L.ingest_local(cfg, a.local)
        print('Nạp %d file log local: %d round của học sinh (bỏ %d file hỏng/0 byte, %d file loại tay, %d round ngoài lớp, %d chưa gán)'
              % (st['files'], st['rounds'], st['files_bad'], st['files_skipped'], st['not_in_roster'], st['unassigned']))
        db_rows = rounds
    else:
        rows = L.fetch_csv(a.csv or cfg['sheet_csv_url'])
        rounds, st, not_roster = L.ingest(cfg, rows)
        print('Nạp: %d dòng; %d trùng, %d sai đồng hồ/TEST, %d ngoài lớp, %d chưa gán; %d round của học sinh (%d gán nhờ nhận diện)'
              % (st['rows'], st['duplicates'], st['bad_clock_or_test'], st['not_in_roster'], st['unassigned'], st['rounds'], st['via_recognition']))
        if a.no_db:
            db_rows = rounds
        else:
            new, total = L.db_merge(rounds, a.db)
            print('DB: +%d round mới, tổng %d' % (new, total))
            db_rows = list(L.db_load(a.db).values())
    big = [(n, c) for n, c in not_roster.most_common() if c >= 5 and n]
    if big:
        print('⚠ Tên ngoài danh sách lớp (>=5 round) - nếu là bé thật, thêm vào roster trong config.json:', ', '.join('%s(%d)' % x for x in big[:8]))
    if st['bad_clock_or_test']:
        print('⚠ %d dòng bị bỏ vì đồng hồ máy sai năm (K02 chưa đồng bộ giờ) hoặc TEST' % st['bad_clock_or_test'])

    multi = a.dfrom and a.dto and a.dfrom != a.dto
    if multi:
        tag = '%s_%s' % (a.dfrom, a.dto)
        days = []
        if a.per_day:
            d, end = date.fromisoformat(a.dfrom), date.fromisoformat(a.dto)
            while d <= end:
                days.append(d.isoformat())
                d += timedelta(days=1)
            have = {x['ts'][:10] for x in db_rows}
            days = [x for x in days if x in have]
        by_day = {}
        for dd in days:   # điểm từng ngày (cho sheet "Theo ngày" của báo cáo gộp)
            by_day[dd] = L.score(cfg, db_rows, dd, dd)[0]
        report(cfg, db_rows, st, not_roster, a.dfrom, a.dto, os.path.join(a.out, tag), a, by_day or None)
        for dd in days:
            report(cfg, db_rows, st, not_roster, dd, dd, os.path.join(a.out, dd), a)
    else:
        report(cfg, db_rows, st, not_roster, a.dfrom, a.dto, a.out, a)


if __name__ == '__main__':
    main()
