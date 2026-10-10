#!/usr/bin/env python3
"""Nhúng WebTools/GenericGameBuilder/fs/*.js + fs/games/*.json (mẫu game) vào game_builder_v2.html (giữa <!--FS-JS-BEGIN--> và <!--FS-JS-END-->)
để builder vẫn là 1 file. Chạy sau mỗi lần sửa fs/*.js hoặc gen_games.py:  python Tools/floorstory_pack/inline_web.py"""
import glob, json, os
here = os.path.dirname(os.path.abspath(__file__))
web = os.path.join(here, '..', '..', 'WebTools', 'GenericGameBuilder')
html = os.path.join(web, 'game_builder_v2.html')
order = ['fs_core.js', 'fs_expr.js', 'fs_game.js', 'fs_act.js', 'fs_flow.js']
s = open(html, encoding='utf-8').read()
a, b = '<!--FS-JS-BEGIN-->', '<!--FS-JS-END-->'
assert s.count(a) == 1 and s.count(b) == 1, 'thiếu marker FS-JS'
i, j = s.index(a) + len(a), s.index(b)
body = ''.join('\n<script>\n' + open(os.path.join(web, 'fs', f), encoding='utf-8').read().replace('</script>', '<\\/script>') + '\n</script>' for f in order)
games = {}
for f in sorted(glob.glob(os.path.join(web, 'fs', 'games', '*.json'))):
    g = json.load(open(f, encoding='utf-8')); gid = os.path.splitext(os.path.basename(f))[0]
    for it in g.get('items', []):
        it.pop('src', None); it.pop('srcFallback', None)   # đường dẫn Resources chỉ dùng khi tạo zip
    games[gid] = {'name': g.get('title', gid), 'desc': g.get('desc', ''), 'data': g}
body += '\n<script>\nwindow.FSW.TEMPLATES = ' + json.dumps(games, ensure_ascii=False).replace('</', '<\\/') + ';\n</script>'
open(html, 'w', encoding='utf-8', newline='').write(s[:i] + body + '\n' + s[j:])
print('inline ok', len(order), 'files', len(games), 'games')
