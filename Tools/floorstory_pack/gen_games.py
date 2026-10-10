#!/usr/bin/env python3
"""NGUỒN gốc của 15 game FloorStory dưới dạng dữ liệu fsData v3 (docs/floor-story-mechanics.md).
Chạy: python Tools/floorstory_pack/gen_games.py  → ghi WebTools/GenericGameBuilder/fs/games/<Game>.json
(items có `src` = đường dẫn Resources của ảnh gốc; make_flow_zips.py copy ảnh đó vào zip và engine bỏ qua `src`.)
Sửa game: sửa file này → chạy lại → chạy make_flow_zips.py + inline_web.py. Hoặc mở zip trong web builder, sửa, xuất (Unity import)."""
import json, os

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(REPO, 'WebTools', 'GenericGameBuilder', 'fs', 'games')

ANIMALS = {'rabbit': 'Thỏ', 'bear': 'Gấu', 'dog': 'Chó', 'chick': 'Gà con', 'pig': 'Lợn', 'duck': 'Vịt', 'panda': 'Gấu trúc', 'buffalo': 'Trâu', 'chicken': 'Gà', 'cow': 'Bò',
           'crocodile': 'Cá sấu', 'elephant': 'Voi', 'frog': 'Ếch', 'giraffe': 'Hươu cao cổ', 'goat': 'Dê', 'gorilla': 'Khỉ đột', 'hippo': 'Hà mã', 'horse': 'Ngựa', 'monkey': 'Khỉ',
           'owl': 'Cú mèo', 'parrot': 'Vẹt', 'penguin': 'Chim cánh cụt', 'rhino': 'Tê giác', 'sloth': 'Con lười', 'snake': 'Rắn', 'zebra': 'Ngựa vằn', 'whale': 'Cá voi', 'moose': 'Nai sừng tấm'}
SHAPES = [('tron', 'Hình tròn', 'circle', '#1E88E5'), ('vuong', 'Hình vuông', 'square', '#43A047'), ('tamgiac', 'Hình tam giác', 'triangle', '#FDD835'),
          ('chunhat', 'Hình chữ nhật', 'rect', '#FB8C00'), ('sao', 'Ngôi sao', 'star', '#8E24AA'), ('tim', 'Hình trái tim', 'heart', '#E53935'), ('thoi', 'Hình thoi', 'diamond', '#EC6FA7')]
COLORS = [('red', 'Đỏ', '#E53935'), ('blue', 'Xanh dương', '#1E88E5'), ('green', 'Xanh lá', '#43A047'), ('yellow', 'Vàng', '#FDD835'), ('orange', 'Cam', '#FB8C00'), ('pink', 'Hồng', '#EC6FA7'), ('purple', 'Tím', '#8E24AA')]
ICONS = [('sun', 'Mặt trời'), ('moon', 'Mặt trăng'), ('flower', 'Bông hoa'), ('star', 'Ngôi sao'), ('heart', 'Trái tim'), ('cake', 'Cái bánh'), ('icecream', 'Cây kem'), ('cup', 'Cốc sữa')]
FRUITS = {'apple': 'Táo', 'banana': 'Chuối', 'orange': 'Cam', 'strawberry': 'Dâu tây', 'watermelon': 'Dưa hấu', 'grape': 'Nho', 'mango': 'Xoài', 'pineapple': 'Dứa', 'pear': 'Lê', 'peach': 'Đào',
          'kiwi': 'Kiwi', 'papaya': 'Đu đủ', 'dragon_fruit': 'Thanh long', 'avocado': 'Bơ', 'blueberry': 'Việt quất'}


def it(id, label, **kw):
    d = {'id': id, 'label': label}; d.update(kw); return d


def animal(a, tags=None, **kw):
    d = it(a, ANIMALS[a], image=f'animal_{a}.png', src=f'GameImages/Animal/{a}', shape='circle', color='#F6B26B'); d.update(kw)
    if tags: d['tags'] = tags
    return d


def res(id, label, name, src, tags=None, **kw):
    d = it(id, label, image=name, src=src, shape='circle', color='#CCCCCC'); d.update(kw)
    if tags: d['tags'] = tags
    return d


CARDS = lambda **p: {'type': 'cards', 'p': p}
EQ = lambda **p: {'type': 'equals', 'p': p}
TXT = lambda **t: t
GAMES = {}

# ═══ 1-6: game bé 3 tuổi ═══════════════════════════════════════════════════════
GAMES['HinhDonGian'] = dict(title='Nhận biết hình đơn giản', sync=True, theme={'bg': '#E3F2FD', 'floor': '#BBDEFB'},
    items=[it(i, l, shape=s, color=c) for i, l, s, c in SHAPES],
    flow=dict(gen={'type': 'set', 'p': {}}, views=[CARDS()], judge=EQ(), text=TXT(prompt='Chọn {LABEL}!', say='{label}!', wrong='Đây là {name} nè!')))

GAMES['NhanBietMau'] = dict(title='Nhận biết màu sắc', sync=False, theme={'bg': '#FFF8E1', 'floor': '#FFECB3'},
    items=[it(i, l, shape='circle', color=c) for i, l, c in COLORS],
    flow=dict(gen={'type': 'set', 'p': {'every': 3}}, views=[CARDS()], judge=EQ(), text=TXT(prompt='Chọn màu {LABEL}!', say='Màu {label}!', wrong='Đây là màu {name} nè!')))

ICON_ITEMS = [it(k, l, icon=k) for k, l in ICONS]
GAMES['NhoChuoiHinh'] = dict(title='Nhớ chuỗi hình', sync=False, theme={'bg': '#FFF3E0', 'floor': '#FFE0B2'}, items=ICON_ITEMS,
    flow=dict(gen={'type': 'sequence', 'p': {}},
              views=[{'type': 'strip', 'p': {}, 'reveal': {'mode': 'sequential', 'showSec': .6, 'hold': 1.2, 'holdPerItem': .35, 'then': 'cover'}},
                     {'type': 'cards', 'p': {'y': .33, 'sizeU': .2, 'tall': 1, 'fig': .8}, 'appear': 'afterReveal'}],
              judge={'type': 'order', 'p': {}},
              text=TXT(prompt='Nhìn kỹ nè! Nhớ thứ tự nhé', ready='Chọn theo thứ tự, từ TRÁI sang PHẢI', say='Giỏi quá!', sayOk='Đúng rồi!')))

GAMES['LatTheNhoGiong'] = dict(title='Lật thẻ giống nhau', sync=False, theme={'bg': '#E8F5E9', 'floor': '#C8E6C9'}, items=ICON_ITEMS,
    flow=dict(gen={'type': 'pairs', 'p': {}}, views=[{'type': 'grid', 'p': {'covered': 1}}], judge={'type': 'pairs', 'p': {}},
              text=TXT(first='Chạm 2 thẻ giống nhau nhé!', prompt='Tìm các cặp giống nhau!', say='Giỏi quá! Không nhầm lần nào!', sayOk='Tìm đủ rồi!')))

W_TIERS = [(1, ['rabbit', 'duck', 'owl', 'frog', 'chick', 'parrot']), (2, ['bear', 'pig', 'goat', 'dog', 'monkey', 'penguin']), (3, ['elephant', 'hippo', 'rhino', 'buffalo', 'cow', 'horse'])]
GAMES['NangNhe'] = dict(title='Nặng hay nhẹ', sync=False, theme={'bg': '#E1F5FE', 'floor': '#A5D6A7'},
    items=[animal(a, value=v) for v, lst in W_TIERS for a in lst],
    flow=dict(gen={'type': 'values', 'p': {}}, views=[{'type': 'seesaw', 'p': {}}], judge=EQ(finishSec=2.4),
              text=TXT(prompt='Con nào {WORD} {suffix}?', say='{label} {word} {suffix}!', wrong='Nhìn bên nào chúi xuống nhé!')))

GAMES['DemKhoiHop'] = dict(title='Đếm khối hộp', sync=False, theme={'bg': '#F3E5F5', 'floor': '#E1BEE7'}, items=[],
    flow=dict(gen={'type': 'number', 'p': {}}, views=[{'type': 'stack', 'p': {}}, CARDS(source='options', y=.33, sizeU=.22, tall=1)], judge=EQ(finishSec=2.2),
              text=TXT(prompt='Có bao nhiêu khối hộp?', say='{count} khối hộp!', wrong='')))

# ═══ 7: Trứng màu sắc ══════════════════════════════════════════════════════════
EGG_FRUIT = {'red': 'apple', 'blue': 'blueberry', 'green': 'pear', 'yellow': 'banana', 'orange': 'orange', 'pink': 'peach', 'purple': 'grape'}
egg_items = [it(i, l, shape='circle', color=c, tags={'kind': 'color', 'fruit': 'fruit_' + EGG_FRUIT[i]}) for i, l, c in COLORS]
egg_items += [res('fruit_' + f, FRUITS[f], f'food_{f}.png', f'Story/Food/{f}', {'kind': 'fruit'}) for f in EGG_FRUIT.values()]
GAMES['TrungMauSac'] = dict(title='Trứng màu sắc', sync=False, theme={'bg': '#FFF1CF', 'floor': ''}, items=egg_items,
    scene=[{'look': {'shape': 'circle', 'color': '#9ED67A'}, 'x': .5, 'y': -.18, 'w': 1.536, 'h': .7},
           {'look': {'shape': 'circle', 'color': '#FFD23F'}, 'x': .88, 'y': .80, 'w': .12},
           {'look': {'shape': 'circle', 'color': '#FFFFFF'}, 'x': .09, 'y': .91, 'w': .13, 'bind': {'color': 'q.targetItem.color'}}],
    flow=dict(gen={'type': 'set', 'p': {'startItems': 99, 'targetWhere': 'item.tags.kind=="color"', 'otherWhere': 'item.tags.kind=="color"'}},
              views=[{'type': 'props', 'p': {'layout': 'row', 'x0': .2, 'x1': .8, 'y0': .40, 'y1': .40, 'size': '0.25', 'tall': 1.32, 'preset': 'none', 'tpl': [
                  {'look': {'shape': 'circle', 'color': '#B98A56'}, 'w': '=size*1.35', 'h': '=size*.45', 'dy': '=-size*.55'},
                  {'look': {'shape': 'circle', 'color': '=opt.color'}, 'w': '=size', 'h': '=size*1.32'},
                  {'look': {'shape': 'square', 'color': '#FFFFFF'}, 'alpha': .55, 'w': '=size*.8', 'h': '=size*.106', 'dy': '=-size*.066'},
                  {'look': {'shape': 'circle', 'color': '#FFFFFF'}, 'alpha': .55, 'w': '=size*.13', 'dx': '=-size*.18', 'dy': '=size*.264'},
                  {'look': {'shape': 'circle', 'color': '#FFFFFF'}, 'alpha': .55, 'w': '=size*.1', 'dx': '=size*.16', 'dy': '=-size*.29'}]}}],
              judge=EQ(finishSec=2.4),
              text=TXT(prompt='Trứng màu {LABEL} đâu?', wrong='Trứng này màu {name} nè!'),
              on={'right': [
                  {'do': 'sfx', 'k': 'right'}, {'do': 'shake', 'on': '@tapped', 'sec': .4, 'amp': 12}, {'do': 'wait', 'sec': .4},
                  {'do': 'confetti', 'at': '@tapped', 'n': 8, 'sound': 0}, {'do': 'hide', 'on': '@tapped'},
                  {'do': 'spawn', 'look': {'shape': 'square', 'color': '#4CAF50'}, 'at': {'ref': '@tapped', 'dy': -.16}, 'w': .025, 'h': .16, 'pop': 0},
                  {'do': 'spawn', 'look': {'shape': 'flower', 'color': '=tapped.item.color'}, 'at': {'ref': '@tapped', 'dy': .02}, 'size': .30},
                  {'do': 'spawn', 'look': {'item': '=tapped.item.tags.fruit'}, 'at': {'ref': '@tapped', 'dx': .11, 'dy': .17}, 'size': .17},
                  {'do': 'spawn', 'look': {'text': '=tapped.name', 'fs': .068, 'color': '#FFFFFF'}, 'at': {'ref': '@tapped', 'dy': -.30}, 'w': .5, 'h': .1},
              ]}))

# ═══ 8: Một ngày của Bé ════════════════════════════════════════════════════════
DAY = [('thuc_day', 'Thức dậy', 0, 'Family/happy'), ('danh_rang', 'Đánh răng', 0, None), ('an_sang', 'Ăn sáng', 0, 'Family/hungry'), ('di_hoc', 'Đi học', 0, None),
       ('rua_tay', 'Rửa tay', 1, None), ('an_trua', 'Ăn cơm trưa', 1, 'Family/hungry'), ('ngu_trua', 'Ngủ trưa', 1, 'Family/sleepy'),
       ('ve_nha', 'Đi học về', 2, None), ('choi_san', 'Chơi ngoài sân', 2, 'Family/happy'), ('tam', 'Tắm', 2, None),
       ('an_toi', 'Ăn tối', 3, 'Family/hungry'), ('doc_truyen', 'Nghe kể chuyện', 3, None), ('di_ngu', 'Đi ngủ', 3, 'Family/sleepy')]
day_items = []
for k, l, per, fb in DAY:
    d = it(k, l, shape='circle', color='#FFD27A', tags={'act': 1, 'period': per})
    d['image'] = f'day_{k}.png'; d['src'] = f'Story/Day/{k}'
    if fb: d['srcFallback'] = fb
    day_items.append(d)
day_items.append(res('na', 'Na', 'day_na.png', 'Story/Day/na', None, color='#FFD6AA'))
for n, src in [('tree_oak', 'NatureKit/Isometric/tree_oak_NE'), ('tree_default', 'NatureKit/Isometric/tree_default_NE'), ('flower_redA', 'NatureKit/Isometric/flower_redA_NE'), ('flower_yellowB', 'NatureKit/Isometric/flower_yellowB_NE')]:
    day_items.append(res('decor_' + n, n, f'decor_{n}.png', src, None, color='#6CC24A'))
stars = [(.15, .88), (.32, .78), (.55, .90), (.68, .70), (.42, .66), (.90, .90)]
GAMES['NgayCuaBe'] = dict(title='Một ngày của Bé', sync=False, theme={'bg': '', 'floor': ''}, items=day_items, vars={'period': 0},
    scene=[{'fill': True, 'look': {'color': '#8FD3FF'}, 'smooth': .7, 'bind': {'color': 'pick($period,"#8FD3FF","#4FB0FF","#FFB070","#1C2B63")'}}]
          + [{'look': {'shape': 'star', 'color': '#FFFFCC'}, 'x': x, 'y': y, 'w': .05, 'alpha': 0, 'smooth': .7, 'bind': {'alpha': '$period==3?1:0'}} for x, y in stars]
          + [{'look': {'shape': 'circle', 'color': '#FFE27A'}, 'x': .20, 'y': .64, 'w': .17, 'smooth': .8,
              'bind': {'x': 'pick($period,.2,.5,.82,.92)', 'y': 'pick($period,.64,.85,.6,-.1)', 'color': 'pick($period,"#FFE27A","#FFF04D","#FF7A3D","#FF7A3D")'}},
             {'look': {'shape': 'moon', 'color': '#FFFFD9'}, 'x': .08, 'y': -.1, 'w': .15, 'alpha': 0, 'smooth': .8, 'bind': {'x': '$period==3?.78:.08', 'y': '$period==3?.8:-.1', 'alpha': '$period==3?1:0'}},
             {'look': {'shape': 'square', 'color': '#6CC24A'}, 'x': .5, 'y': .07, 'w': 1.024, 'h': .22},
             {'look': {'item': 'decor_tree_oak'}, 'x': .08, 'y': .30, 'w': .30}, {'look': {'item': 'decor_tree_default'}, 'x': .93, 'y': .30, 'w': .26},
             {'look': {'item': 'decor_flower_redA'}, 'x': .30, 'y': .20, 'w': .10}, {'look': {'item': 'decor_flower_yellowB'}, 'x': .72, 'y': .21, 'w': .10},
             {'id': 'na', 'role': 'actor', 'look': {'item': 'na'}, 'x': .5, 'y': .50, 'w': .36},
             {'look': {'text': '="BUỔI "+pick($period,"SÁNG","TRƯA","CHIỀU","TỐI")', 'fs': .108, 'color': '#FFFFFF'}, 'x': .5, 'y': .93, 'w': .9, 'h': .12, 'z': 'front'}],
    flow=dict(gen={'type': 'set', 'p': {'n': 3, 'startItems': 99, 'targetWhere': 'item.tags.act==1 && item.tags.period==$period', 'otherWhere': 'item.tags.act==1 && item.tags.period!=$period', 'distinctBy': 'period', 'noRepeat': 1}},
              views=[CARDS(y=.17, sizeU=.30, tall=1.05, fig=.8, label=1, bg='#FFFFFF')], judge=EQ(),
              text=TXT(prompt='Na làm gì nào?', say='{label}!', wrong='Chưa đúng! Na làm gì lúc này nhỉ?'),
              on={'right': [{'do': 'std', 'ev': 'right'}, {'do': 'inc', 'var': 'period', 'mod': 4}]}))

# ═══ 9: Xây nhà hình học ═══════════════════════════════════════════════════════
PARTS = [  # id, label, shape, kind, x, y, w, h, color, requires
    ('chimney', 'ống khói', 'square', 'chunhat', .66, .76, .07, .13, '#8D7B6A', 'roof'), ('wall', 'thân nhà', 'square', 'chunhat', .50, .50, .52, .28, '#F2C57C', ''),
    ('roof', 'mái nhà', 'triangle', 'tamgiac', .50, .73, .62, .22, '#E0453F', 'wall'), ('roundwin', 'cửa sổ tròn', 'circle', 'tron', .50, .72, .10, .10, '#BDE6FF', 'roof'),
    ('door', 'cửa ra vào', 'square', 'chunhat', .50, .425, .11, .19, '#7A4A2A', 'wall'), ('winl', 'cửa sổ trái', 'square', 'vuong', .33, .52, .11, .11, '#8ED0FF', 'wall'),
    ('winr', 'cửa sổ phải', 'square', 'vuong', .67, .52, .11, .11, '#8ED0FF', 'wall'), ('sun', 'mặt trời', 'circle', 'tron', .14, .84, .14, .14, '#FFD93D', 'wall'),
    ('ball', 'quả bóng', 'circle', 'tron', .18, .33, .09, .09, '#FF6B6B', 'wall')]
KINDS = [('tron', 'Hình tròn', 'circle', '#FF8A3D'), ('vuong', 'Hình vuông', 'square', '#4D96FF'), ('tamgiac', 'Hình tam giác', 'triangle', '#E0453F'), ('chunhat', 'Hình chữ nhật', 'rect', '#2FB26B')]
hh_items = [it(pid, lab, shape=sh, color=c, tags={'part': 1, 'kind': kd, 'requires': rq, 'shape': sh, 'color': c, 'x': x, 'y': y, 'w': w, 'h': h}) for pid, lab, sh, kd, x, y, w, h, c, rq in PARTS]
hh_items += [it('kind_' + i if False else i, l, shape=s, color=c, tags={'isKind': 1}) for i, l, s, c in KINDS]
hh_items.append(res('tree', 'Cây', 'decor_tree_oak.png', 'NatureKit/Isometric/tree_oak_NE', None))
hh_vars = {'built': 0, 'roofc': '#E0453F', 'wallc': '#F2C57C'}
hh_vars.update({'built_' + p[0]: 0 for p in PARTS})
reset = [{'do': 'set', 'var': 'built_' + p[0], 'expr': '0'} for p in PARTS] + [{'do': 'set', 'var': 'built', 'expr': '0'},
         {'do': 'set', 'var': 'roofc', 'expr': 'pick(randi(0,4),"#E0453F","#8E5BD8","#2F9E8F","#E8872E")'}, {'do': 'set', 'var': 'wallc', 'expr': 'pick(randi(0,4),"#F2C57C","#F4A6C0","#9AD5A0","#9FC9F2")'}]
GAMES['HinhHoc'] = dict(title='Xây nhà hình học', sync=False, theme={'bg': '#CFEFFF', 'floor': ''}, items=hh_items, vars=hh_vars,
    scene=[{'look': {'shape': 'square', 'color': '#7ACB5B'}, 'x': .5, 'y': .12, 'w': 1.024, 'h': .27}, {'look': {'item': 'tree'}, 'x': .90, 'y': .43, 'w': .32},
           {'forEachItem': {'where': 'item.tags.part==1'}, 'look': {'shape': '=item.tags.shape', 'color': '#2F3B55'}, 'alpha': .35, 'x': '=item.tags.x', 'y': '=item.tags.y', 'w': '=item.tags.w', 'h': '=item.tags.h',
            'bind': {'show': 'var("built_"+item.id)==0'}},
           {'forEachItem': {'where': 'item.tags.part==1'}, 'look': {'shape': '=item.tags.shape', 'color': '#FFF2A0'}, 'x': '=item.tags.x', 'y': '=item.tags.y', 'w': '=item.tags.w', 'h': '=item.tags.h',
            'anim': {'type': 'pulse', 'amp': .06, 'speed': 7}, 'bind': {'show': 'q.subject.id==item.id && var("built_"+item.id)==0', 'alpha': '0.55'}},
           {'forEachItem': {'where': 'item.tags.part==1'}, 'id': '="part_"+item.id', 'look': {'shape': '=item.tags.shape', 'color': '=item.tags.color'}, 'x': '=item.tags.x', 'y': '=item.tags.y', 'w': '=item.tags.w', 'h': '=item.tags.h',
            'show': '0', 'smooth': .3, 'bind': {'show': 'var("built_"+item.id)==1', 'color': 'item.id=="roof"?$roofc:(item.id=="wall"?$wallc:((item.tags.shape=="square" && item.tags.w<.12 && item.tags.h<.12 && item.id!="ball") && $built>=9 ? "#FFF06B" : item.tags.color))'}}],
    flow=dict(gen={'type': 'match', 'p': {'subjectWhere': 'item.tags.part==1 && var("built_"+item.id)==0 && (item.tags.requires=="" || var("built_"+item.tags.requires)==1)', 'optionsWhere': 'item.tags.isKind==1', 'n': 3, 'answerTag': 'kind', 'shuffle': 1}},
              views=[CARDS(y=.15, sizeU=.28, tall=1.05, fig=.58, label=1, bg='#FFF4D6')], judge=EQ(finishSec=0.3),
              text=TXT(prompt='Xây {{q.subject.label}}: hình nào vừa khít?', wrong='Chưa vừa! Thử hình khác nhé'),
              on={'right': [{'do': 'sfx', 'k': 'right'}, {'do': 'hide', 'on': '@tapped'},
                            {'do': 'fly', 'to': '="part_"+q.subject.id', 'sec': .5, 'look': {'item': '=tapped.itemId'}},
                            {'do': 'set', 'var': '="built_"+q.subject.id', 'expr': '1'}, {'do': 'inc', 'var': 'built'}, {'do': 'sfx', 'k': 'wood'},
                            {'do': 'say', 'text': 'Đây là {{lower(tapped.name)}}!', 'fy': .84},
                            {'do': 'if', 'cond': '$built>=9', 'then': [{'do': 'wait', 'sec': .4}, {'do': 'confetti', 'at': {'fx': .5, 'fy': .55}, 'n': 20}, {'do': 'say', 'text': 'Nhà xong rồi! Giỏi quá!', 'fy': .84, 'sec': 2, 'color': '#FFE27A'}, {'do': 'wait', 'sec': 1.8}] + reset, 'else': [{'do': 'wait', 'sec': .3}]}]}))

# ═══ 10: Chăm bạn Bi ═══════════════════════════════════════════════════════════
NEEDS = [('an', 'Bi đói bụng! Cho Bi ăn gì?', 'hungry', 'Family/hungry'), ('uong', 'Bi khát nước! Bi cần uống gì?', 'thirsty', 'Family/thirsty'), ('ngu', 'Bi buồn ngủ! Bi cần gì?', 'sleepy', 'Family/sleepy'),
         ('van_dong', 'Bi chán quá! Bi cần làm gì?', 'bored', 'Family/sad'), ('ve_sinh', 'Tay Bi bẩn rồi! Bi cần gì?', 'dirty', 'Family/sad'), ('lanh', 'Bi lạnh quá! Bi cần gì?', 'cold', 'Family/cold')]
bk = []
for n, prompt, st, fb in NEEDS:
    bk.append(res('bi_' + n, prompt, f'bi_{st}.png', f'Story/Bi/{st}', {'bi': 1}, srcFallback=fb, color='#F6B26B'))
bk.append(res('bi_happy', 'Bi vui', 'bi_happy.png', 'Story/Bi/happy', {'bi': 1}, srcFallback='Family/happy', color='#F6B26B'))
bk.append(res('garden', 'Nền vườn', 'garden_bg.png', 'Background/Carrot_BG', None))
for f in ['apple', 'banana', 'orange', 'strawberry', 'watermelon', 'grape', 'mango', 'pineapple', 'pear', 'peach', 'kiwi', 'papaya', 'dragon_fruit', 'avocado']:
    bk.append(res('f_' + f, FRUITS[f], f'food_{f}.png', f'Story/Food/{f}', {'good': 'an'}))
bk += [res('carrot', 'Cà rốt', 'carrot.png', 'Fruit/carrot', {'good': 'an'}), res('thuc_an', 'Bữa cơm', 'thuc_an.png', 'SaveEnvironment/thuc_an', {'good': 'an'}),
       res('keo', 'Kẹo', 'vo_keo.png', 'SaveEnvironment/vo_keo', {'bad': 'an,uong'}, why='Ăn nhiều kẹo sẽ sâu răng!'),
       res('soda', 'Nước ngọt', 'vo_lon_coca.png', 'SaveEnvironment/vo_lon_coca', {'bad': 'an,uong'}, why='Nước ngọt không tốt cho Bi!'),
       res('banh', 'Bánh ngọt', 'mieng_pizza.png', 'SaveEnvironment/mieng_pizza', {'bad': 'van_dong'}, why='Ăn vặt nhiều thì hết đói bữa chính!'),
       res('nuoc', 'Nước lọc', 'giot_nuoc.png', 'SaveEnvironment/giot_nuoc', {'good': 'uong'}), res('giuong', 'Đi ngủ', 'bed.png', 'NatureKit/Isometric/bed_NE', {'good': 'ngu'}),
       res('tivi', 'Xem tivi', 'health_xem_tivi.png', 'Story/Health/xem_tivi', {'bad': 'ngu,van_dong'}, why='Xem tivi khuya hại mắt!'),
       res('dienthoai', 'Chơi điện thoại', 'health_dien_thoai.png', 'Story/Health/dien_thoai', {'bad': 'ngu'}, why='Chơi điện thoại nhiều hại mắt!'),
       res('chaynhay', 'Chạy nhảy', 'health_chay_nhay.png', 'Story/Health/chay_nhay', {'good': 'van_dong'}), res('ruatay', 'Rửa tay', 'health_rua_tay.png', 'Story/Health/rua_tay', {'good': 've_sinh'}),
       res('lauao', 'Lau vào áo', 'health_lau_ao.png', 'Story/Health/lau_ao', {'bad': 've_sinh'}, why='Tay vẫn còn vi khuẩn đấy!'),
       res('anluon', 'Ăn luôn', 'health_an_luon.png', 'Story/Health/an_luon', {'bad': 've_sinh'}, why='Tay bẩn có vi khuẩn, phải rửa trước!'),
       res('aoam', 'Mặc áo ấm', 'health_ao_am.png', 'Story/Health/ao_am', {'good': 'lanh'}), res('coiao', 'Cởi áo', 'health_coi_ao.png', 'Story/Health/coi_ao', {'bad': 'lanh'}, why='Cởi áo ra sẽ càng lạnh, dễ ốm!'),
       res('ankem', 'Ăn kem', 'health_an_kem.png', 'Story/Health/an_kem', {'bad': 'lanh'}, why='Ăn kem lúc lạnh sẽ ho đấy!')]
for d in bk:
    if d['id'] in ('keo', 'soda', 'banh', 'tivi', 'dienthoai', 'lauao', 'anluon', 'coiao', 'ankem'):
        d['tags']['why'] = d.pop('why')
    d.pop('why', None)
GAMES['BeKhoeManh'] = dict(title='Chăm bạn Bi (khỏe mạnh)', sync=False, theme={'bg': '#FFF1D6', 'floor': ''}, items=bk, vars={'need': 'an', 'stars': 0, 'stage': 0},
    scene=[{'fill': True, 'look': {'item': 'garden'}}, {'fill': True, 'look': {'color': '#FFFFFF'}, 'alpha': .12}]
          + [{'look': {'shape': 'star', 'color': '#CFC6B2'}, 'x': .5 + (i - 2) * .10, 'y': .945, 'w': .075, 'bind': {'color': '$stars>' + str(i) + '?"#FFC83D":"#CFC6B2"'}, 'smooth': .2} for i in range(5)]
          + [{'id': 'bi', 'role': 'actor', 'look': {'item': 'bi_an'}, 'x': .5, 'y': .585, 'w': .36, 'h': .47, 'bind': {'item': '"bi_"+$need', 'scale': '0.82+0.12*min($stage,3)'}}],
    flow=dict(gen={'type': 'set', 'p': {'n': 3, 'startItems': 99, 'targetWhere': 'item.tags.good==$need', 'otherWhere': 'has(item.tags.bad,$need)'}},
              views=[CARDS(y=.145, sizeU=.28, tall=1.05, fig=.8, label=1, bg='#FFFFFF')], judge=EQ(finishSec=0.3),
              text=TXT(prompt='{{label("bi_"+$need)}}', wrong='{{tag(tapped.itemId,"why")}}'),
              on={'start': [{'do': 'bag', 'var': 'need', 'values': 'an,uong,ngu,van_dong,ve_sinh,lanh'}],
                  'wrong': [{'do': 'sfx', 'k': 'tap'}, {'do': 'shake', 'on': '@tapped'}, {'do': 'shake', 'on': '#bi', 'sec': .45, 'amp': 16}, {'do': 'say', 'text': '{{tag(tapped.itemId,"why")||"Bi không cần cái này!"}}', 'fy': .40, 'color': '#FFE27A', 'sec': 1.8}],
                  'right': [{'do': 'sfx', 'k': 'right'}, {'do': 'move', 'on': '@tapped', 'to': {'fx': .5, 'fy': .53}, 'sec': .45, 'wait': 0}, {'do': 'scale', 'on': '@tapped', 'to': .4, 'sec': .45},
                            {'do': 'hide', 'on': '@tapped'}, {'do': 'set', 'var': 'need', 'expr': '"happy"'}, {'do': 'bounce', 'on': '#bi', 'amount': .15, 'sec': .5}, {'do': 'sfx', 'k': 'plop'}, {'do': 'inc', 'var': 'stars'},
                            {'do': 'if', 'cond': '$stars>=5', 'then': [{'do': 'set', 'var': 'stars', 'expr': '5'}, {'do': 'inc', 'var': 'stage'}, {'do': 'say', 'text': 'Bi lớn lên rồi! Khỏe quá!', 'fy': .40, 'color': '#FFE27A', 'sec': 2},
                                                                       {'do': 'confetti', 'at': '#bi', 'n': 22}, {'do': 'wait', 'sec': 1.6}, {'do': 'set', 'var': 'stars', 'expr': '0'}],
                             'else': [{'do': 'say', 'text': 'Bi khỏe hơn rồi!', 'fy': .40, 'sec': 1.2}, {'do': 'wait', 'sec': .8}]}]}))

# ═══ 11: Vỡ bóng bay ═══════════════════════════════════════════════════════════
vb = [res('balloon_' + c, l, f'balloon_{c}.png', f'Balloon/balloon_{c}', {'kind': 'balloon'}, color=h) for c, l, h in COLORS]
vb += [animal(a, {'kind': 'animal'}) for a in ANIMALS]
GAMES['VoBongBay'] = dict(title='Vỡ bóng bay (bé nhỏ)', sync=False, theme={'bg': '#BFE9FF', 'floor': ''}, items=vb,
    scene=[{'look': {'shape': 'circle', 'color': '#FFFFFF'}, 'alpha': .75, 'x': x, 'y': y, 'w': .26, 'h': .13} for x, y in [(.18, .80), (.62, .70), (.86, .84)]]
          + [{'look': {'shape': 'square', 'color': '#7ACB5B'}, 'x': .5, 'y': .06, 'w': 1.024, 'h': .14},
             {'look': {'shape': 'circle', 'color': '#FFFFFF'}, 'x': .10, 'y': .915, 'w': .12, 'bind': {'color': 'q.targetItem.color'}}],
    flow=dict(gen={'type': 'set', 'p': {'startItems': 99, 'targetWhere': 'item.tags.kind=="balloon"', 'otherWhere': 'item.tags.kind=="balloon"'}},
              views=[{'type': 'props', 'p': {'layout': 'row', 'x0': .2, 'x1': .8, 'y0': .52, 'y1': .52, 'size': '0.30', 'tall': 1.33, 'preset': 'none', 'anim': 'float',
                                              'tpl': [{'look': {'item': '=opt.id'}, 'w': '=size*.75', 'h': '=size'}]}}],
              judge=EQ(finishSec=2.4), text=TXT(prompt='Vỡ bóng màu {LABEL}!', wrong='Bóng này màu {name} nè!'),
              on={'right': [{'do': 'sfx', 'k': 'right'}, {'do': 'sfx', 'k': 'pop'}, {'do': 'confetti', 'at': '@tapped', 'n': 8, 'sound': 0}, {'do': 'hide', 'on': '@tapped'},
                            {'do': 'pickItem', 'var': 'animal', 'tag': 'kind', 'value': 'animal'},
                            {'do': 'spawn', 'look': {'item': '=$animal'}, 'at': '@tapped', 'size': .30}, {'do': 'spawn', 'look': {'text': '=label($animal)', 'fs': .068, 'color': '#FFFFFF'}, 'at': {'ref': '@tapped', 'dy': -.19}, 'w': .5, 'h': .1, 'pop': 0}]}))

# ═══ 12: Sàn kỳ diệu ═══════════════════════════════════════════════════════════
sk = [res('hoa_' + n, n, f'nk_{n}.png', f'NatureKit/Isometric/{n}_NE', {'kind': 'hoa'}, shape='flower', color='#FF6FA5') for n in ['flower_redA', 'flower_redB', 'flower_yellowA', 'flower_yellowC', 'flower_purpleA', 'flower_purpleB']]
sk += [res('vuon_' + n, n, f'nk_{n}.png', f'NatureKit/Isometric/{n}_NE', {'kind': 'vuon'}, shape='flower', color='#6BCB77') for n in ['tree_oak', 'tree_default', 'mushroom_red', 'plant_bush', 'flower_redC', 'tree_oak_dark']]
BR = '["#FFD93D","#FF6B6B","#6BCB77","#4D96FF","#FF9F45","#C77DFF"]'
GAMES['SanKyDieu'] = dict(title='Sàn kỳ diệu (bé nhỏ)', sync=False, theme={'bg': '', 'floor': ''}, items=sk, vars={'theme': 0},
    scene=[{'fill': True, 'look': {'color': '#CFF5C0'}, 'smooth': .6, 'bind': {'color': 'pick($theme,"#CFF5C0","#1C2B63","#FFD9E8","#BFE9FF","#DFF3B8")'}}],
    flow=dict(gen={'type': 'none', 'p': {}}, views=[{'type': 'floor', 'p': {}}], judge={'type': 'taps', 'p': {'count': 6, 'finishSec': 1}},
              text=TXT(prompt='{{pick($theme,"Dậm chân cho hoa nở!","Dậm chân cho sao sáng!","Dậm chân cho tim bay!","Dậm chân cho bóng nổi!","Dậm chân cho vườn mọc lên!")}}'),
              on={'step': [{'do': 'sfx', 'k': '=pick($theme,"up","star","plop","pop","wood")'},
                           {'do': 'if', 'cond': '$theme==0', 'then': [{'do': 'spawn', 'look': {'item': '=randItem("kind","hoa")'}, 'at': '@tap', 'size': '=0.18+0.05*rand()'}]},
                           {'do': 'if', 'cond': '$theme==1', 'then': [{'do': 'spawn', 'look': {'shape': 'star', 'color': '=pick(randi(0,2),"#FFD93D","#FF6B6B")'}, 'at': '@tap', 'size': '=0.10+0.07*rand()'}]},
                           {'do': 'if', 'cond': '$theme==2', 'then': [{'do': 'spawn', 'look': {'shape': 'heart', 'color': '=pick(randi(0,4),"#FF6FA5","#FF9AB8","#E8457C","#FFB3C9")'}, 'at': '@tap', 'size': '=0.10+0.07*rand()'}]},
                           {'do': 'if', 'cond': '$theme==3', 'then': [{'do': 'spawn', 'look': {'shape': 'circle', 'color': '=pick(randi(0,6),"#FFD93D","#FF6B6B","#6BCB77","#4D96FF","#FF9F45","#C77DFF")'}, 'at': '@tap', 'size': '=0.09+0.09*rand()'}]},
                           {'do': 'if', 'cond': '$theme==4', 'then': [{'do': 'spawn', 'look': {'item': '=randItem("kind","vuon")'}, 'at': '@tap', 'size': .26}]}],
                  'end': [{'do': 'inc', 'var': 'theme', 'mod': 5}]}))

# ═══ 13: Dài–ngắn, to–bé ═══════════════════════════════════════════════════════
cb = [res('rabbit', 'Thỏ', 'animal_rabbit.png', 'GameImages/Animal/rabbit', None, color='#F3F3F3'), res('carrot', 'Cà rốt', 'carrot.png', 'Fruit/carrot', None, shape='triangle', color='#FF8A3D'),
      it('longlog', 'Dài', shape='rrect', color='#A0652D'), it('shortlog', 'Ngắn', shape='rrect', color='#A0652D')]
for a, vn, sz in [('chick', 'Gà con', 0), ('dog', 'Chó', 1), ('elephant', 'Voi', 2)]:
    cb.append(animal(a, {'kind': 'pet3', 'door': f'door{sz}'}, value=sz))
for s, (lab, col_) in enumerate([('cửa bé', '#6FB7E9'), ('cửa vừa', '#F2B24C'), ('cửa to', '#E0627F')]):
    cb.append(it(f'door{s}', lab, shape='rrect', color=col_, value=s, tags={'kind': 'door'}))
bridge_objs = [
    {'look': {'shape': 'square', 'color': '#6CC24A'}, 'x': '=0.5-q.gap/2-0.25', 'y': .52, 'w': '=0.5*wr', 'h': .16}, {'look': {'shape': 'square', 'color': '#6CC24A'}, 'x': '=0.5+q.gap/2+0.25', 'y': .52, 'w': '=0.5*wr', 'h': .16},
    {'look': {'shape': 'square', 'color': '#4DA6FF'}, 'x': .5, 'y': .50, 'w': '=q.gap*wr', 'h': .20},
    {'id': 'rabbit', 'role': 'actor', 'look': {'item': 'rabbit'}, 'x': '=0.5-q.gap/2-0.08', 'y': .67, 'w': .17}, {'id': 'carrot', 'look': {'item': 'carrot'}, 'x': '=0.5+q.gap/2+0.08', 'y': .65, 'w': .13, 'anim': {'type': 'bob', 'amp': .012, 'speed': 3}}]
GAMES['CauBacQua'] = dict(title='Dài – ngắn, to – bé', sync=False, theme={'bg': '#D9F1FF', 'floor': ''}, items=cb, flowPick='alternate',
    flows=[
      dict(objects=bridge_objs, gen={'type': 'calc', 'p': {'defs': {'gap': 'pick(randi(0,3),0.3,0.4,0.5)', 'rs': 'shuf(0.5,0.65,0.8)'},
                                                           'options': [{'item': 'longlog', 'v': 'gap*(1.18+0.12*rand())'}, {'item': 'shortlog', 'v': 'gap*at(rs,0)'}, {'item': 'shortlog', 'v': 'gap*at(rs,1)'}], 'answer': 'max', 'shuffle': 1}},
           views=[{'type': 'props', 'p': {'layout': 'col', 'x0': .5, 'y0': .36, 'y1': .13, 'size': '=opt.v*wr', 'hU': .07, 'preset': 'none', 'tpl': [
               {'look': {'kind': 'rrect', 'color': '#A0652D'}, 'w': '=size', 'h': .07}, {'look': {'shape': 'circle', 'color': '#D9A066'}, 'dx': '=-size/2+0.0245', 'w': .0434}, {'look': {'shape': 'circle', 'color': '#D9A066'}, 'dx': '=size/2-0.0245', 'w': .0434}]}}],
           judge=EQ(finishSec=0.9), text=TXT(prompt='Chọn khúc gỗ DÀI hơn sông!'),
           on={'wrong': [{'do': 'sfx', 'k': 'wrong'}, {'do': 'move', 'on': '@tapped', 'to': {'fx': .5, 'fy': .615}, 'sec': .5}, {'do': 'say', 'text': 'Khúc gỗ NGẮN quá! Rơi xuống nước rồi', 'fy': .80, 'color': '#FFE27A', 'sec': 1.5},
                         {'do': 'sfx', 'k': 'plop'}, {'do': 'move', 'on': '@tapped', 'to': {'fx': .5, 'fy': .38}, 'sec': .5}, {'do': 'hide', 'on': '@tapped'}],
               'right': [{'do': 'sfx', 'k': 'right'}, {'do': 'move', 'on': '@tapped', 'to': {'fx': .5, 'fy': .615}, 'sec': .5}, {'do': 'say', 'text': 'Khúc gỗ DÀI nên bắc qua được!', 'fy': .80, 'sec': 2}, {'do': 'sfx', 'k': 'wood'},
                         {'do': 'bounce', 'on': '@actor', 'amount': .12, 'sec': .9}, {'do': 'move', 'on': '@actor', 'to': {'fx': '=0.5+q.gap/2+0.03', 'fy': .67}, 'sec': 1.0}, {'do': 'sfx', 'k': 'plop'},
                         {'do': 'scale', 'on': '#carrot', 'to': 0, 'sec': .3, 'wait': 0}, {'do': 'confetti', 'at': '@actor', 'n': 10}]}),
      dict(objects=[{'role': 'actor', 'look': {'item': '=q.subject.id'}, 'x': .5, 'y': '=0.17+pick(q.subject.value,0.15,0.22,0.36)/2/hr-0.04/hr', 'w': '=pick(q.subject.value,0.15,0.22,0.36)', 'anim': {'type': 'bob', 'amp': .012, 'speed': 3}}],
           gen={'type': 'match', 'p': {'subjectWhere': 'item.tags.kind=="pet3"', 'optionsWhere': 'item.tags.kind=="door"', 'n': 3, 'answerTag': 'door', 'shuffle': 1}},
           views=[{'type': 'props', 'p': {'layout': 'row', 'x0': .2, 'x1': .8, 'y0': .5, 'y1': .5, 'size': '=pick(opt.value,0.14,0.21,0.30)', 'tall': 1.5, 'yExpr': '=0.43+pick(opt.value,0.22,0.32,0.44)/2/hr', 'preset': 'none', 'tpl': [
               {'look': {'kind': 'rrect', 'color': '=opt.color'}, 'w': '=size', 'h': '=size*1.5'}, {'look': {'shape': 'circle', 'color': '#FFF4D6'}, 'dx': '=size*.28', 'w': .025},
               {'look': {'text': '=pick(opt.value,"BÉ","VỪA","TO")', 'fs': .052, 'color': '#FFFFFF'}, 'dy': '=size*.45', 'w': '=size', 'h': .1}]}}],
           judge=EQ(finishSec=1.0), text=TXT(prompt='{subject} {{pick(q.subject.value,"BÉ","VỪA","TO")}} — đi cửa nào?', wrong='Cửa không vừa! Tìm cửa vừa hơn nhé!'),
           on={'right': [{'do': 'sfx', 'k': 'right'}, {'do': 'say', 'text': '{subject} đi cửa vừa khít!', 'fy': .80, 'sec': 2}, {'do': 'scale', 'on': '@actor', 'to': .5, 'sec': .7, 'wait': 0}, {'do': 'move', 'on': '@actor', 'to': '@tapped', 'sec': .7},
                         {'do': 'sfx', 'k': 'wood'}, {'do': 'fade', 'on': '@actor', 'to': 0, 'sec': .25, 'wait': 0}, {'do': 'bounce', 'on': '@tapped', 'amount': .1, 'sec': .4}, {'do': 'confetti', 'at': '@tapped', 'n': 10}]})])

# ═══ 14: Đặt đồ vào chỗ ════════════════════════════════════════════════════════
dd = [it('tren', 'trên', shape='circle', color='#FFFFFF', value=0, tags={'kind': 'zone'}), it('duoi', 'dưới', shape='circle', color='#FFFFFF', value=1, tags={'kind': 'zone'}),
      it('trai', 'bên trái', shape='circle', color='#FFFFFF', value=2, tags={'kind': 'zone'}), it('phai', 'bên phải', shape='circle', color='#FFFFFF', value=3, tags={'kind': 'zone'})]
for a, vn in [('rabbit', 'chú thỏ'), ('bear', 'chú gấu'), ('dog', 'chú chó'), ('chick', 'chú gà con'), ('pig', 'chú lợn'), ('duck', 'chú vịt'), ('panda', 'gấu trúc')]:
    d = animal(a, {'kind': 'pet'}); d['label'] = vn; dd.append(d)
GAMES['DatDoVaoCho'] = dict(title='Đặt đồ vào chỗ (trên, dưới, trái, phải)', sync=False, theme={'bg': '#FFF1D6', 'floor': ''}, items=dd,
    scene=[{'look': {'shape': 'square', 'color': '#E7C08A'}, 'x': .5, 'y': .11, 'w': 1.024, 'h': .24},
           {'look': {'shape': 'square', 'color': '#8A5A2B'}, 'x': .5, 'y': .50, 'dx': -.17, 'dy': -.115, 'w': .04, 'h': .17}, {'look': {'shape': 'square', 'color': '#8A5A2B'}, 'x': .5, 'y': .50, 'dx': .17, 'dy': -.115, 'w': .04, 'h': .17},
           {'look': {'kind': 'rrect', 'color': '#B9763A'}, 'x': .5, 'y': .50, 'w': .46, 'h': .06},
           {'look': {'shape': 'triangle', 'color': '#FFFFFF'}, 'x': .5, 'y': .82, 'w': .07, 'bind': {'rot': 'pick(q.target,0,180,90,-90)'}, 'smooth': 0}],
    flow=dict(objects=[{'role': 'actor', 'look': {'item': '=q.subject.id'}, 'x': .5, 'y': .13, 'w': .20, 'anim': {'type': 'bob', 'amp': .012, 'speed': 3}}],
              gen={'type': 'match', 'p': {'subjectWhere': 'item.tags.kind=="pet"', 'optionsWhere': 'item.tags.kind=="zone"', 'n': 4, 'shuffle': 0, 'noRepeat': 1}},
              views=[{'type': 'props', 'p': {'layout': 'fixed', 'pos': [[.5, .63], [.5, .36], [0.109, .5], [0.891, .5]], 'size': '0.20', 'preset': 'zone', 'anim': 'pulse'}}],
              judge=EQ(finishSec=1.3), text=TXT(prompt='Đặt {subject} {label} cái bàn', wrong='Không phải chỗ này!'),
              on={'wrong': [{'do': 'move', 'on': '@actor', 'to': '@tapped', 'sec': .45}, {'do': 'say', 'text': 'Không phải chỗ này!', 'fy': .74, 'color': '#FFE27A', 'sec': 1.2}, {'do': 'shake', 'on': '@actor', 'sec': .35, 'amp': 10},
                          {'do': 'wait', 'sec': .35}, {'do': 'move', 'on': '@actor', 'to': {'fx': .5, 'fy': .13}, 'sec': .4}],
                  'right': [{'do': 'sfx', 'k': 'right'}, {'do': 'move', 'on': '@actor', 'to': '@tapped', 'sec': .55}, {'do': 'sfx', 'k': 'plop'}, {'do': 'bounce', 'on': '@actor', 'amount': .2, 'sec': .5},
                            {'do': 'say', 'text': 'Đúng rồi! {{cap(q.subject.label)}} ở {label} cái bàn', 'fy': .74, 'sec': 2}, {'do': 'confetti', 'at': '@tapped', 'n': 10}]}))

# ═══ 15: Cổng hình ═════════════════════════════════════════════════════════════
ch = [it(i, l, shape=s, color=c, tags={'kind': 'gate', 'aw': aw, 'ah': ah}) for (i, l, s, c, aw, ah) in
      [('tron', 'Hình tròn', 'circle', '#FF8A3D', 1, 1), ('vuong', 'Hình vuông', 'square', '#4D96FF', 1, 1), ('tamgiac', 'Hình tam giác', 'triangle', '#E0453F', 1.05, .95), ('chunhat', 'Hình chữ nhật', 'square', '#2FB26B', 1.2, .78)]]
ch += [animal(a, {'kind': 'animal'}) for a in ['rabbit', 'dog', 'duck', 'pig', 'chick', 'bear']]
GAMES['CongHinh'] = dict(title='Cổng hình', sync=False, theme={'bg': '#D8F0FF', 'floor': '#8ED36B'}, items=ch,
    flow=dict(objects=[{'role': 'actor', 'look': {'item': '=randItem("kind","animal")'}, 'x': .5, 'y': .45, 'w': .17, 'anim': {'type': 'bob', 'amp': .012, 'speed': 3}}],
              gen={'type': 'sequence', 'p': {'where': 'item.tags.kind=="gate"', 'kinds': 4, 'startLen': 1, 'maxLen': 3, 'growEvery': 3, 'lenBy': 'task', 'distinct': 1}},
              views=[{'type': 'strip', 'p': {'y': .835, 'slotU': .09, 'plain': 1}},
                     {'type': 'props', 'p': {'layout': 'fixed', 'pos': [[.26, .64], [.74, .64], [.26, .26], [.74, .26]], 'size': '0.27', 'preset': 'none', 'tpl': [
                         {'look': {'shape': '=opt.shape', 'color': '=opt.color'}, 'w': '=size*opt.tags.aw', 'h': '=size*opt.tags.ah'},
                         {'look': {'shape': '=opt.shape', 'color': '#1B2442'}, 'w': '=size*opt.tags.aw*.62', 'h': '=size*opt.tags.ah*.62'},
                         {'look': {'text': '=opt.label', 'fs': .052, 'color': '#FFFFFF'}, 'dy': '=-size*.62', 'w': .5, 'h': .08}]}}],
              judge={'type': 'order', 'p': {'finishSec': 1.2}},
              text=TXT(prompt='{{len(q.seq)==1?"Tìm cổng ":"Đi qua cổng: "}}{names}', wrong='Đây là {name}. Tìm đúng cổng nhé!', say=''),
              on={'step': [{'do': 'sfx', 'k': 'up'}, {'do': 'move', 'on': '@actor', 'to': '@tapped', 'sec': .45}, {'do': 'bounce', 'on': '@tapped', 'amount': .12, 'sec': .4}, {'do': 'say', 'text': '{{tapped.name}}!', 'fy': .74, 'sec': 1},
                             {'do': 'if', 'cond': '!last', 'then': [{'do': 'move', 'on': '@actor', 'to': {'fx': .5, 'fy': .45}, 'sec': .4}]}],
                  'right': [{'do': 'confetti', 'at': '@tapped', 'n': 14}]}))

# ═══ 16: Đồng hồ khổng lồ ══════════════════════════════════════════════════════
STORIES = [(6, 'Na thức dậy lúc 6 giờ'), (7, 'Na đi học lúc 7 giờ'), (8, 'Lớp học bắt đầu lúc 8 giờ'), (9, 'Na vẽ tranh lúc 9 giờ'), (10, 'Na ăn trái cây lúc 10 giờ'), (11, 'Na rửa tay lúc 11 giờ'),
           (12, 'Cả nhà ăn cơm trưa lúc 12 giờ'), (1, 'Na ngủ trưa lúc 1 giờ'), (2, 'Na dậy chơi lúc 2 giờ'), (3, 'Na ăn bánh lúc 3 giờ'), (4, 'Na chơi ngoài sân lúc 4 giờ'), (5, 'Na tắm lúc 5 giờ')]
dh = [it(f'h{n}', str(n), shape='circle', color='#CFE8FF', value=n, tags={'opt': 1}) for n in range(1, 13)]
dh += [it(f's{h}', t, shape='circle', color='#FFFFFF', tags={'story': 1, 'hour': h, 'optid': f'h{h}'}) for h, t in STORIES]
hand = lambda hid, ln, th, c: {'id': hid, 'look': {'kind': 'hand', 'len': ln, 'thick': th, 'color': c}, 'x': .5, 'y': .46}
clock_view = {'type': 'props', 'p': {'layout': 'circle', 'cx': .5, 'cy': .46, 'r': .325, 'startDeg': 30, 'size': '0.15', 'preset': 'none', 'tpl': [
    {'look': {'shape': 'circle', 'color': '=(opt.id=="h3"||opt.id=="h6"||opt.id=="h9"||opt.id=="h12")?"#FFD27A":"#CFE8FF"'}, 'w': '=size'},
    {'look': {'text': '=opt.label', 'fs': .088, 'color': '#2A2F55', 'outline': 0}, 'w': .14, 'h': .14}]}}
clock_scene = [{'look': {'shape': 'circle', 'color': '#3A3F6B'}, 'x': .5, 'y': .46, 'w': .86}, {'look': {'shape': 'circle', 'color': '#FFFFFF'}, 'x': .5, 'y': .46, 'w': .80},
               hand('minute', .28, .022, '#2A2F55'), hand('hand', .20, .034, '#E0453F'), {'look': {'shape': 'circle', 'color': '#2A2F55'}, 'x': .5, 'y': .46, 'w': .05},
               {'id': 'digital', 'look': {'text': '', 'fs': .088, 'color': '#2A2F55', 'outline': 0}, 'x': .5, 'y': .06, 'w': .5, 'h': .1, 'z': 'front'}]
match_p = {'subjectWhere': 'item.tags.story==1 && ($_task>=4 || item.tags.hour%3==0)', 'optionsWhere': 'item.tags.opt==1', 'n': 12, 'answerTag': 'optid', 'shuffle': 0, 'noRepeat': 1}
set_flow = dict(gen={'type': 'match', 'p': match_p}, views=[clock_view], judge=EQ(finishSec=1.5),
                text=TXT(prompt='{subject}\nĐặt đồng hồ {{q.subject.tags.hour}} giờ!', wrong=''),
                on={'setup': [{'do': 'rotate', 'on': '#hand', 'to': 0, 'sec': 0}, {'do': 'text', 'on': '#digital', 'text': ''}],
                    'wrong': [{'do': 'sfx', 'k': 'wrong'}, {'do': 'rotate', 'on': '#hand', 'to': '=-((tapped.key+1)%12)*30', 'sec': .6}, {'do': 'say', 'text': 'Kim đang chỉ {{tapped.key+1}}, chưa phải {{q.subject.tags.hour}} giờ!', 'fy': .12, 'color': '#FFE27A', 'sec': 1.4},
                              {'do': 'wait', 'sec': 1}, {'do': 'rotate', 'on': '#hand', 'to': 0, 'sec': .5}],
                    'right': [{'do': 'sfx', 'k': 'right'}, {'do': 'rotate', 'on': '#hand', 'to': '=-(q.subject.tags.hour%12)*30', 'sec': .7}, {'do': 'text', 'on': '#digital', 'text': '{{q.subject.tags.hour}}:00'},
                              {'do': 'bounce', 'on': '@tapped', 'amount': .2, 'sec': .5}, {'do': 'say', 'text': '{{q.subject.tags.hour}} giờ! Giỏi quá!', 'fy': .12, 'sec': 1.8}, {'do': 'confetti', 'at': {'fx': .5, 'fy': .46}, 'n': 12}]})
read_flow = dict(gen={'type': 'match', 'p': match_p}, views=[clock_view], judge=EQ(finishSec=1.5), text=TXT(prompt='Mấy giờ rồi?', wrong=''),
                 on={'setup': [{'do': 'rotate', 'on': '#hand', 'to': '=-(q.subject.tags.hour%12)*30', 'sec': 0}, {'do': 'text', 'on': '#digital', 'text': ''}],
                     'wrong': [{'do': 'sfx', 'k': 'wrong'}, {'do': 'say', 'text': 'Chưa đúng! Nhìn kim ngắn nhé', 'fy': .12, 'color': '#FFE27A', 'sec': 1.4}, {'do': 'shake', 'on': '@tapped'}, {'do': 'wait', 'sec': .9}],
                     'right': [{'do': 'sfx', 'k': 'right'}, {'do': 'text', 'on': '#digital', 'text': '{{q.subject.tags.hour}}:00'}, {'do': 'bounce', 'on': '@tapped', 'amount': .2, 'sec': .5},
                               {'do': 'say', 'text': '{{q.subject.tags.hour}} giờ! Giỏi quá!', 'fy': .12, 'sec': 1.8}, {'do': 'confetti', 'at': {'fx': .5, 'fy': .46}, 'n': 12}]})
GAMES['DongHoKhongLo'] = dict(title='Đồng hồ khổng lồ', sync=False, theme={'bg': '#FFF1D6', 'floor': ''}, items=dh, scene=clock_scene, flows=[set_flow, read_flow], flowPick='alternate')


DESC = {
    'HinhDonGian': 'Nghe tên hình → chạm đúng hình (mỗi hình 1 màu cố định). 2 đội cùng câu, chờ nhau.', 'NhanBietMau': 'Nghe tên màu → chạm đúng màu.',
    'NhoChuoiHinh': 'Hiện từng hình rồi úp lại → chạm theo đúng thứ tự trái → phải.', 'LatTheNhoGiong': 'Lật 2 thẻ; giống nhau thì ăn +1 điểm, khác nhau thì úp lại.',
    'NangNhe': 'Bập bênh nghiêng → chạm con nặng/nhẹ nhất.', 'DemKhoiHop': 'Đống khối 3D → chạm số đúng; sai 2 lần thì đếm 1,2,3.',
    'TrungMauSac': 'Chạm trứng đúng màu → nở hoa + trái cây cùng màu.', 'NgayCuaBe': 'Trời đổi theo buổi; chọn việc hợp với buổi (biến period).',
    'HinhHoc': 'Xây nhà từ bóng đen: chọn hình vừa khít, hình bay vào chỗ, nhà xong thì xây nhà mới.', 'BeKhoeManh': 'Bi cần gì thì cho cái đó; đủ 5 sao thì Bi lớn lên.',
    'VoBongBay': 'Vỡ bóng đúng màu → con vật chui ra.', 'SanKyDieu': 'Chạm đâu mọc hoa/sao/tim/bóng/cây đó (không đúng/sai).', 'CauBacQua': 'Luân phiên: chọn khúc gỗ dài hơn sông / chọn cửa vừa cỡ con vật.',
    'DatDoVaoCho': 'Đặt con vật vào đúng chỗ: trên, dưới, trái, phải cái bàn.', 'CongHinh': 'Đi qua các cổng hình theo thứ tự (1 → 3 cổng).', 'DongHoKhongLo': 'Luân phiên: đặt giờ / đọc giờ trên đồng hồ khổng lồ.',
}


def write_all():
    os.makedirs(OUT, exist_ok=True)
    for gid, g in GAMES.items():
        d = dict(g); d['version'] = 3; d['desc'] = DESC.get(gid, '')
        json.dump(d, open(os.path.join(OUT, gid + '.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(len(GAMES), 'game ->', OUT)


if __name__ == '__main__':
    write_all()
