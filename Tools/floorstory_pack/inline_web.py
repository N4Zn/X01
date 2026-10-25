#!/usr/bin/env python3
"""Nhúng WebTools/GenericGameBuilder/fs/*.js + MẪU GAME vào game_builder_v4.19.html (giữa <!--FS-JS-BEGIN--> và <!--FS-JS-END-->)
để builder vẫn là 1 file. Mẫu game đọc THẲNG từ WebTools/FloorStoryFlows/*.zip (meta.fsData) — zip là nguồn duy nhất, không có bản sao khác.
Chạy sau mỗi lần sửa fs/*.js hoặc zip:  python Tools/floorstory_pack/inline_web.py"""
import glob, json, os, sys, zipfile
here = os.path.dirname(os.path.abspath(__file__))
web = os.path.join(here, '..', '..', 'WebTools', 'GenericGameBuilder')
flows = os.path.join(here, '..', '..', 'WebTools', 'FloorStoryFlows')
html = os.path.join(web, sys.argv[1] if len(sys.argv) > 1 else 'game_builder_v4.19.html')
order = ['fs_sfx.js', 'fs_core.js', 'fs_expr.js', 'fs_game.js', 'fs_act.js', 'fs_flow.js', 'fs_edit.js']
s = open(html, encoding='utf-8').read()
a, b = '<!--FS-JS-BEGIN-->', '<!--FS-JS-END-->'
assert s.count(a) == 1 and s.count(b) == 1, 'thiếu marker FS-JS'
i, j = s.index(a) + len(a), s.index(b)
body = ''.join('\n<script>\n' + open(os.path.join(web, 'fs', f), encoding='utf-8').read().replace('</script>', '<\/script>') + '\n</script>' for f in order)
games = {}
for f in sorted(glob.glob(os.path.join(flows, '*.zip'))):
    pkg = json.loads(zipfile.ZipFile(f).read('game.json').decode('utf-8')); meta = pkg.get('meta') or {}
    g = meta.get('fsData'); gid = os.path.splitext(os.path.basename(f))[0]
    if not g: continue
    for it in g.get('items', []):
        it.pop('src', None); it.pop('srcFallback', None)
    games[gid] = {'name': g.get('title', gid), 'desc': meta.get('description', ''), 'data': g}
body += '\n<script>\nwindow.FSW.TEMPLATES = ' + json.dumps(games, ensure_ascii=False).replace('</', '<\/') + ';\n</script>'
open(html, 'w', encoding='utf-8', newline='').write(s[:i] + body + '\n' + s[j:])
print('inline ok', len(order), 'files', len(games), 'games (tu zip)')
