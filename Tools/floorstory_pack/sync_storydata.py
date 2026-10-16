#!/usr/bin/env python3
"""Thay Editor khi chưa mở Unity: zip game FloorStory (WebTools/FloorStoryFlows/*.zip) → Assets/Game/Resources/StoryData/<id>/game.json
(giống FloorStoryPackImporter: ghi meta.fsData dạng gọn). Zip là nguồn duy nhất; file Resources chỉ là bản Unity sinh ra từ zip.
Chạy:  python Tools/floorstory_pack/sync_storydata.py [GameId ...]"""
import glob, json, os, sys, zipfile
here = os.path.dirname(os.path.abspath(__file__)); root = os.path.join(here, '..', '..')
ids = sys.argv[1:]
for f in sorted(glob.glob(os.path.join(root, 'WebTools', 'FloorStoryFlows', '*.zip'))):
    gid = os.path.splitext(os.path.basename(f))[0]
    if ids and gid not in ids: continue
    g = ((json.loads(zipfile.ZipFile(f).read('game.json').decode('utf-8')).get('meta') or {}).get('fsData'))
    d = os.path.join(root, 'Assets', 'Game', 'Resources', 'StoryData', gid)
    if not g or not os.path.isdir(d): print('bo qua', gid); continue
    new = json.dumps(g, ensure_ascii=False, separators=(',', ':'))
    p = os.path.join(d, 'game.json'); old = open(p, encoding='utf-8').read() if os.path.exists(p) else ''
    if old != new: open(p, 'w', encoding='utf-8', newline='').write(new); print('cap nhat', gid)
