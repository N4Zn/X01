#!/usr/bin/env python3
"""Bước 2 của luồng: sinh zip game FloorStory (meta.kind = "fsdata") từ WebTools/GenericGameBuilder/fs/games/*.json → WebTools/FloorStoryFlows/<GameId>.zip.
Mỗi item có `src` (đường dẫn Resources) → copy ảnh gốc vào assets/<item.image>; thiếu ảnh thì bỏ `image` (game vẽ bằng hình/icon thay thế, như Unity).
Mở zip trong web builder (⇧ Mở game đã xuất) → sửa ảnh/vật/cơ chế → Xuất → Unity import.
Chạy (sau gen_games.py):  python Tools/floorstory_pack/make_flow_zips.py [GameId ...]"""
import json, os, sys, zipfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_packs import find_res  # tìm ảnh trong Assets/Resources, Assets/Game/Resources (có alias SaveEnvironment)

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(REPO, 'WebTools', 'GenericGameBuilder', 'fs', 'games')
OUT = os.path.join(REPO, 'WebTools', 'FloorStoryFlows')


def build(gid):
    g = json.load(open(os.path.join(SRC, gid + '.json'), encoding='utf-8'))
    files, missing = {}, []
    for it in g.get('items', []):
        src, fb = it.pop('src', None), it.pop('srcFallback', None)
        if not it.get('image'):
            continue
        p = find_res(src) if src else None
        if not p and fb: p = find_res(fb)
        if p:
            ext = os.path.splitext(p)[1].lower()
            name = os.path.splitext(it['image'])[0] + ext
            it['image'] = name; files[name] = open(p, 'rb').read()
        else:
            missing.append(it['id']); it.pop('image', None)
    desc = g.pop('desc', '')
    gj = dict(schemaVersion=2, meta=dict(gameId=gid, displayName=g['title'], category='FloorStory', kind='fsdata', fsData=g),
              settings={}, layout={}, effects={}, rounds=[], imagePools=[])
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, gid + '.zip')
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('game.json', json.dumps(gj, ensure_ascii=False, indent=1))
        for n, d in files.items(): z.writestr('assets/' + n, d)
    return path, len(files), missing


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    ids = sys.argv[1:] or sorted(f[:-5] for f in os.listdir(SRC) if f.endswith('.json'))
    for gid in ids:
        p, n, miss = build(gid)
        print(f'{gid}: {n} anh, {os.path.getsize(p) // 1024} KB' + (f'  (thieu anh, ve bang hinh: {", ".join(miss)})' if miss else ''))
