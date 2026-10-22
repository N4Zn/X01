"""Đẩy cấu hình + trang báo cáo lên Drive (thư mục EduXplore_Logs/_report) qua DriveUpload.gs (action=put).

  set EDUX_DRIVE_KEY=<SECRET>            (không ghi vào repo)
  python -I -X utf8 push_to_drive.py            # cả config.json và dashboard.html
  python -I -X utf8 push_to_drive.py --only config|dashboard

- config.json  = bản chép của ../../tool/pipeline/config.json (NGUỒN DUY NHẤT vẫn là file trong repo; bản trên Drive chỉ là sản phẩm sinh).
  Aggregate.gs tự tính lại từ đầu khi config đổi. Chỉ giữ các khoá Aggregate.gs dùng (không đẩy ghi chú/_doc).
- dashboard.html = web đóng gói 1 file, chừa chỗ dữ liệu (bundle.py --template).
URL lấy từ drive_url trong config.json.
"""
import argparse
import json
import os
import subprocess
import sys
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.normpath(os.path.join(HERE, '..'))
PIPE = os.path.normpath(os.path.join(HERE, '..', '..', 'tool', 'pipeline'))

KEYS = ['mon', 'roster', 'generic_names', 'game_alias', 'scored_games', 'game_hp', 'tht_hp', 'hp_order', 'min_valid_year',
        'exclude_ranges', 'min_rounds_per_session', 'min_resp_sec', 'formula', 'skip_files', 'slot_override', 'diag',
        'lop_map', 'report_from']


def put(url, key, name, body):
    q = urllib.parse.urlencode({'action': 'put', 'name': name, 'key': key})
    req = urllib.request.Request(url + ('&' if '?' in url else '?') + q, data=body.encode('utf-8'),
                                 headers={'Content-Type': 'text/plain; charset=utf-8'}, method='POST')
    with urllib.request.urlopen(req, timeout=120) as r:
        res = json.loads(r.read().decode('utf-8'))
    if not res.get('ok') or res.get('bytes') != len(body.encode('utf-8')):
        sys.exit('Đẩy %s lỗi: %s' % (name, res))
    print('OK %s (%d byte)' % (name, res['bytes']))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', choices=['config', 'dashboard'])
    a = ap.parse_args()
    key = os.environ.get('EDUX_DRIVE_KEY', '')
    cfg = json.load(open(os.path.join(PIPE, 'config.json'), encoding='utf-8'))
    url = cfg.get('drive_url')
    if not url or not key:
        sys.exit('Cần drive_url trong config.json và biến môi trường EDUX_DRIVE_KEY')
    if a.only in (None, 'config'):
        put(url, key, 'config.json', json.dumps({k: cfg[k] for k in KEYS if k in cfg}, ensure_ascii=False, separators=(',', ':')))
    if a.only in (None, 'dashboard'):
        out = os.path.join(WEB, 'dist', 'dashboard-template.html')
        subprocess.run([sys.executable, '-I', '-X', 'utf8', os.path.join(HERE, 'bundle.py'), '--template', '--out', out], check=True)
        put(url, key, 'dashboard.html', open(out, encoding='utf-8').read())


if __name__ == '__main__':
    main()
