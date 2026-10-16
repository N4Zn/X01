"""Gói web thành 1 file HTML tự chứa (CSS + JS + dữ liệu + logo nhúng), mở được bằng file:// hoặc gửi qua mail.
  python -I -X utf8 tools/bundle.py [--data data/report.json] [--out dist/bao-cao-standalone.html]
Mặc định dùng data/sample.json."""
import argparse
import base64
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.normpath(os.path.join(HERE, '..'))
ap = argparse.ArgumentParser()
ap.add_argument('--data', default=os.path.join(WEB, 'data', 'sample.json'))
ap.add_argument('--out', default=os.path.join(WEB, 'dist', 'bao-cao-standalone.html'))
a = ap.parse_args()


def rd(p, mode='r'):
    return open(p, mode, **({} if 'b' in mode else {'encoding': 'utf-8'})).read()


css = rd(os.path.join(WEB, 'edux-report.css'))
js = rd(os.path.join(WEB, 'edux-report.js'))
data = json.dumps(json.load(open(a.data, encoding='utf-8')), ensure_ascii=False, separators=(',', ':')).replace('</', '<\\/')
logo = 'data:image/png;base64,' + base64.b64encode(rd(os.path.join(WEB, 'logo.png'), 'rb')).decode()

html = """<!doctype html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Báo cáo kết quả học tập · EduXplore</title>
<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600&family=Source+Serif+4:wght@600&display=swap">
<style>
:root{--page-bg:#e9ebee}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--page-bg:#101316}}
:root[data-theme="dark"]{--page-bg:#101316}
body{margin:0;background:var(--page-bg)}
%s
</style></head><body>
<div id="edux-report"></div>
<script>window.EDUX_DATA=%s;</script>
<script>%s</script>
<script>EduxReport.mount('#edux-report',{data:window.EDUX_DATA,logo:%s,theme:'auto'});</script>
</body></html>
""" % (css, data, js, json.dumps(logo))
os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
open(a.out, 'w', encoding='utf-8', newline='\n').write(html)
print('Ghi', a.out, '%d KB' % (len(html.encode('utf-8')) // 1024))
