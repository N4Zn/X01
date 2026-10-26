#!/usr/bin/env python3
"""Dựng roster trường CGD (4 lớp 1A1..1A4) từ cgd_students.tsv -> cgd_roster.json (định dạng roster.json của FaceEnroll + trường "code").
Mã HS: CGD{năm sinh}_{lớp bỏ chữ số đầu}{STT 3 chữ số}, vd 1A1 #1 -> CGD2020_A1001. App BỎ QUA trường "code" (và ghi lại roster.json sẽ làm mất nó)
nên bản gốc có mã giữ ở repo; tool báo cáo đọc mã từ file này.
Tên (khoá toàn cục trong app) phải duy nhất: tên trùng (khác lớp hoặc cùng lớp) được thêm hậu tố " (lớp)" hoặc " (lớp-STT)"; alias (tên hiển thị) giữ như danh sách gốc.
Chạy: python -I Tools/roster/make_cgd_roster.py   Đẩy lên máy: adb push Tools/roster/cgd_roster_device.json /sdcard/EduXplore/roster.json (xem docs/launcher-roster.md)"""
import json, os, sys
from collections import Counter
here = os.path.dirname(os.path.abspath(__file__))
YEAR = 2020
rows = []
for line in open(os.path.join(here, 'cgd_students.tsv'), encoding='utf-8'):
    if not line.strip(): continue
    stt, name, cls, gender = line.rstrip('\r\n').split('\t')
    rows.append((int(stt), ' '.join(name.split()), cls.strip(), 'nam' if gender.strip().lower() == 'nam' else 'nu'))
cnt = Counter(r[1] for r in rows)
used = set(); classes = {}
for stt, name, cls, gender in rows:
    key = name
    if cnt[name] > 1: key = f'{name} ({cls})'
    if key in used: key = f'{name} ({cls}-{stt})'
    assert key not in used, key
    used.add(key)
    code = f'CGD{YEAR}_{cls[1:]}{stt:03d}'
    classes.setdefault(cls, []).append({'name': key, 'alias': name, 'gender': gender, 'code': code})
out = {'version': 1, 'school': 'CGD', 'classes': [{'name': c, 'students': s} for c, s in classes.items()]}
json.dump(out, open(os.path.join(here, 'cgd_roster.json'), 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=2)
# bản đẩy lên máy: bỏ trường code (app bỏ qua nên giữ cũng không sao, nhưng file sạch hơn)
dev = {'version': 1, 'classes': [{'name': c['name'], 'students': [{k: v for k, v in s.items() if k != 'code'} for s in c['students']]} for c in out['classes']]}
json.dump(dev, open(os.path.join(here, 'cgd_roster_device.json'), 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=2)
print({c['name']: len(c['students']) for c in out['classes']}, 'dup:', sum(1 for v in cnt.values() if v > 1))
