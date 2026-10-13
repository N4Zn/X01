#!/usr/bin/env python3
"""Tạo mẫu 'Chăm bạn Bi — từng câu viết tay' (fs/games/BeKhoeManhRound.json) từ BeKhoeManh.json:
cùng kho vật + cảnh + hiệu ứng, nhưng KHÔNG bốc nhu cầu ngẫu nhiên; mỗi câu là 1 round viết tay
{items:[3 vật], target, vars:{need}} — đáp án đúng = vật có tags.good == need, nhiễu = vật có tags.bad chứa need.
Chạy:  python Tools/floorstory_pack/gen_bi_rounds.py  rồi  python Tools/floorstory_pack/inline_web.py"""
import json, os, copy
here = os.path.dirname(os.path.abspath(__file__))
g = os.path.join(here, '..', '..', 'WebTools', 'GenericGameBuilder', 'fs', 'games')
d = json.load(open(os.path.join(g, 'BeKhoeManh.json'), encoding='utf-8'))
d = copy.deepcopy(d)
needs = [s.strip() for s in d['flow']['on']['start'][0]['values'].split(',')]
items = d['items']
rounds = []
for ni, need in enumerate(needs):
    goods = [i for i in items if (i.get('tags') or {}).get('good') == need]
    bads = [i for i in items if need in str((i.get('tags') or {}).get('bad', '')).split(',')]
    for gi, good in enumerate(goods):
        trio = [b['id'] for b in bads[:2]]
        pos = (gi + ni) % 3
        trio.insert(pos, good['id'])
        label = next(i for i in items if i['id'] == 'bi_' + need)['label']
        rounds.append({'items': trio, 'target': good['id'], 'vars': {'need': need}})
f = d['flow']
f['on'].pop('start', None)          # không bốc ngẫu nhiên nữa: nhu cầu của Bi do từng câu (round.vars.need) quyết định
f['manual'] = True; f['rounds'] = rounds; f['roundOrder'] = 'shuffle'
d['title'] = 'Chăm bạn Bi — từng câu viết tay'
d['desc'] = 'Giống "Chăm bạn Bi" nhưng mỗi câu là một round tự soạn: Bi cần gì (ô Biến: need=...), 3 vật, đáp án đúng. Sửa / thêm / xoá câu ở cột trái.'
json.dump(d, open(os.path.join(g, 'BeKhoeManhRound.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(len(rounds), 'câu')
