#!/usr/bin/env python3
"""Sinh các gói zip "thay ảnh" cho game FloorStory — mở/sửa được bằng WebTools/GenericGameBuilder/game_builder_v2.html.

Chạy (từ gốc repo):  python Tools/floorstory_pack/make_packs.py [--out WebTools/FloorStoryPacks] [Game ...]

Mỗi gói = 1 game: game.json (meta.kind = "floorstory", meta.floorStory.images[]) + assets/<ảnh mặc định>.
Người dùng mở zip trong web builder → tab "Ảnh FloorStory" → thay ảnh → Xuất → Unity: Tools → FloorStoryGame → Import Pack Zips.
Unity ghi ảnh thay vào Assets/Game/Resources/StoryPack/<Game>/<key>.png; StoryUI.Load ưu tiên thư mục đó.

DANH MỤC (CATALOG) bên dưới phải khớp các lời gọi StoryUI.Load(...) trong Worlds/*World.cs — thêm/bớt ảnh của game thì sửa ở đây.
key = đường dẫn Resources (không đuôi) mà code gọi StoryUI.Load. Ảnh mặc định lấy từ Assets/Resources hoặc Assets/Game/Resources;
hình vẽ bằng code (hình học, icon) được vẽ lại gần đúng bằng PIL để làm hình xem trước.
"""
import json, math, os, sys, zipfile
from PIL import Image, ImageDraw, ImageFont

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
RES_ROOTS = [os.path.join(REPO, 'Assets', 'Resources'), os.path.join(REPO, 'Assets', 'Game', 'Resources')]
# Thư mục bị gõ sai chính tả trong repo: code gọi "SaveEnvironment/..." nhưng thư mục là "SaveEnviroment".
ALIASES = {'SaveEnvironment/': 'SaveEnviroment/'}
FONT = 'C:/Windows/Fonts/arial.ttf'


def find_res(key):
    cands = [key] + [key.replace(a, b, 1) for a, b in ALIASES.items() if key.startswith(a)]
    for k in cands:
        for root in RES_ROOTS:
            for ext in ('.png', '.jpg', '.jpeg'):
                p = os.path.join(root, k + ext)
                if os.path.isfile(p):
                    return p
    return None


# ── vẽ hình mặc định (xem trước) ────────────────────────────────────────────────
def hexrgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def canvas(n=512):
    return Image.new('RGBA', (n, n), (0, 0, 0, 0))


def draw_shape(kind, color, n=512):
    im = canvas(n); d = ImageDraw.Draw(im); c = hexrgb(color) + (255,); m = n * 0.1
    if kind == 'tron': d.ellipse([m, m, n - m, n - m], fill=c)
    elif kind == 'vuong': d.rectangle([m, m, n - m, n - m], fill=c)
    elif kind == 'chunhat': d.rectangle([m * 0.5, n * 0.22, n - m * 0.5, n * 0.78], fill=c)
    elif kind == 'tamgiac': d.polygon([(n / 2, m), (n - m, n - m), (m, n - m)], fill=c)
    elif kind == 'thoi': d.polygon([(n / 2, m), (n - m, n / 2), (n / 2, n - m), (m, n / 2)], fill=c)
    elif kind == 'sao':
        pts = []
        for i in range(10):
            r = (n / 2 - m) * (1 if i % 2 == 0 else 0.42); a = -math.pi / 2 + i * math.pi / 5
            pts.append((n / 2 + r * math.cos(a), n / 2 + r * math.sin(a)))
        d.polygon(pts, fill=c)
    elif kind == 'tim':
        pts = []
        for i in range(200):
            t = i / 200 * 2 * math.pi
            x = 16 * math.sin(t) ** 3
            y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
            pts.append((n / 2 + x * (n - 2 * m) / 34, n / 2 - y * (n - 2 * m) / 34 - n * 0.03))
        d.polygon(pts, fill=c)
    return im


def draw_egg(color, n=512):
    im = canvas(n); d = ImageDraw.Draw(im); c = hexrgb(color) + (255,)
    d.ellipse([n * 0.15, n * 0.05, n * 0.85, n * 0.95], fill=c)
    w = (255, 255, 255, 140)
    d.rectangle([n * 0.22, n * 0.5, n * 0.78, n * 0.58], fill=w)
    d.ellipse([n * 0.34, n * 0.25, n * 0.44, n * 0.35], fill=w); d.ellipse([n * 0.58, n * 0.68, n * 0.66, n * 0.76], fill=w)
    return im


def draw_placeholder(label, n=512):
    im = Image.new('RGBA', (n, n), (240, 240, 244, 255)); d = ImageDraw.Draw(im)
    d.rectangle([4, 4, n - 5, n - 5], outline=(160, 160, 170, 255), width=6)
    try: f = ImageFont.truetype(FONT, 44); f2 = ImageFont.truetype(FONT, 34)
    except Exception: f = f2 = ImageFont.load_default()
    d.text((n / 2, n * 0.40), label, fill=(70, 70, 80, 255), font=f, anchor='mm')
    d.text((n / 2, n * 0.60), '(chưa có ảnh - thay bằng ảnh PNG nền trong suốt)', fill=(120, 120, 130, 255), font=f2, anchor='mm')
    return im


# ── danh mục ───────────────────────────────────────────────────────────────────
def R(key, label, group, fallback=None):
    """Ảnh có sẵn trong Resources (key); fallback = key khác dùng làm hình xem trước nếu key chưa có file."""
    return dict(key=key, label=label, group=group, src=('res', key, fallback))


def D(key, label, group, make):
    """Ảnh mặc định vẽ bằng code (make() trả PIL.Image); game vẫn vẽ bằng code nếu không thay."""
    return dict(key=key, label=label, group=group, src=('draw', make))


ANIMALS = {
    'rabbit': 'Thỏ', 'bear': 'Gấu', 'dog': 'Chó', 'chick': 'Gà con', 'pig': 'Lợn', 'duck': 'Vịt', 'panda': 'Gấu trúc',
    'buffalo': 'Trâu', 'chicken': 'Gà', 'cow': 'Bò', 'crocodile': 'Cá sấu', 'elephant': 'Voi', 'frog': 'Ếch', 'giraffe': 'Hươu cao cổ',
    'goat': 'Dê', 'gorilla': 'Khỉ đột', 'hippo': 'Hà mã', 'horse': 'Ngựa', 'monkey': 'Khỉ', 'owl': 'Cú mèo', 'parrot': 'Vẹt',
    'penguin': 'Chim cánh cụt', 'rhino': 'Tê giác', 'sloth': 'Con lười', 'snake': 'Rắn', 'zebra': 'Ngựa vằn', 'whale': 'Cá voi',
    'moose': 'Nai sừng tấm',
}


def animals(names, group='Con vật'):
    return [R('GameImages/Animal/' + n, ANIMALS[n], group) for n in names]


FRUITS = {
    'apple': 'Táo', 'banana': 'Chuối', 'orange': 'Cam', 'strawberry': 'Dâu tây', 'watermelon': 'Dưa hấu', 'grape': 'Nho', 'mango': 'Xoài',
    'pineapple': 'Dứa', 'pear': 'Lê', 'peach': 'Đào', 'kiwi': 'Kiwi', 'papaya': 'Đu đủ', 'dragon_fruit': 'Thanh long', 'avocado': 'Bơ',
    'blueberry': 'Việt quất',
}

SHAPES = [('tron', 'Hình tròn', '#1E88E5'), ('vuong', 'Hình vuông', '#43A047'), ('tamgiac', 'Hình tam giác', '#FDD835'),
          ('chunhat', 'Hình chữ nhật', '#FB8C00'), ('sao', 'Ngôi sao', '#8E24AA'), ('tim', 'Hình trái tim', '#E53935'), ('thoi', 'Hình thoi', '#EC6FA7')]
ICONS = [('sun', 'Mặt trời'), ('moon', 'Mặt trăng'), ('flower', 'Bông hoa'), ('star', 'Ngôi sao'),
         ('heart', 'Trái tim'), ('cake', 'Cái bánh'), ('icecream', 'Cây kem'), ('cup', 'Cốc sữa')]
EGGS = [('red', 'Đỏ', '#E53935'), ('blue', 'Xanh dương', '#1E88E5'), ('green', 'Xanh lá', '#43A047'), ('yellow', 'Vàng', '#FDD835'),
        ('orange', 'Cam', '#FB8C00'), ('pink', 'Hồng', '#EC6FA7'), ('purple', 'Tím', '#8E24AA')]
BALLOON_COLORS = [('red', 'Đỏ'), ('blue', 'Xanh dương'), ('green', 'Xanh lá'), ('yellow', 'Vàng'), ('orange', 'Cam'), ('pink', 'Hồng'), ('purple', 'Tím')]

SHAPE_SLOTS = [D('Story/Shapes/' + k, l, 'Hình (thay hình vẽ bằng code)', lambda k=k, h=h: draw_shape(k, h)) for k, l, h in SHAPES]
ICON_SLOTS = [D('Story/Icons/' + k, l, 'Biểu tượng (thay hình vẽ bằng code)', lambda l=l: draw_placeholder(l)) for k, l in ICONS]

DAY_ACTS = [('thuc_day', 'Thức dậy', 'Family/happy'), ('danh_rang', 'Đánh răng', None), ('an_sang', 'Ăn sáng', 'Family/hungry'), ('di_hoc', 'Đi học', None),
            ('rua_tay', 'Rửa tay', None), ('an_trua', 'Ăn cơm trưa', 'Family/hungry'), ('ngu_trua', 'Ngủ trưa', 'Family/sleepy'), ('ve_nha', 'Đi học về', None),
            ('choi_san', 'Chơi ngoài sân', 'Family/happy'), ('tam', 'Tắm', None), ('an_toi', 'Ăn tối', 'Family/hungry'), ('doc_truyen', 'Nghe kể chuyện', None),
            ('di_ngu', 'Đi ngủ', 'Family/sleepy')]
BI_STATES = [('hungry', 'Bi đói', 'Family/hungry'), ('thirsty', 'Bi khát', 'Family/thirsty'), ('sleepy', 'Bi buồn ngủ', 'Family/sleepy'),
             ('bored', 'Bi chán', 'Family/sad'), ('dirty', 'Bi bẩn tay', 'Family/sad'), ('cold', 'Bi lạnh', 'Family/cold'), ('happy', 'Bi vui (đủ 5 sao)', 'Family/happy')]
HEALTH = [('xem_tivi', 'Xem tivi'), ('dien_thoai', 'Chơi điện thoại'), ('chay_nhay', 'Chạy nhảy'), ('rua_tay', 'Rửa tay'), ('lau_ao', 'Lau tay vào áo'),
          ('an_luon', 'Ăn luôn (tay bẩn)'), ('ao_am', 'Mặc áo ấm'), ('coi_ao', 'Cởi áo'), ('an_kem', 'Ăn kem')]
NATURE = lambda n: 'NatureKit/Isometric/' + n + '_NE'

PACKS = {
    'NgayCuaBe': dict(title='Một ngày của Bé', intro='Nhân vật Na, các việc trong ngày (thẻ chọn) và cây/hoa trang trí. Việc chưa có ảnh dùng tạm ảnh Family/* hoặc thẻ chữ.',
        images=[R('Story/Day/na', 'Na (nhân vật bé gái)', 'Nhân vật')]
               + [R('Story/Day/' + k, l, 'Việc trong ngày', fb) for k, l, fb in DAY_ACTS]
               + [R(NATURE(n), l, 'Trang trí') for n, l in [('tree_oak', 'Cây sồi'), ('tree_default', 'Cây'), ('flower_redA', 'Hoa đỏ'), ('flower_yellowB', 'Hoa vàng')]]),
    'HinhHoc': dict(title='Xây nhà hình học', intro='Các hình (tròn, vuông, tam giác, chữ nhật) vẽ bằng code; chỉ có cái cây trang trí thay được.',
        images=[R(NATURE('tree_oak'), 'Cây sồi', 'Trang trí')]),
    'BeKhoeManh': dict(title='Chăm bạn Bi (khỏe mạnh)', intro='Nhân vật Bi theo 7 trạng thái (nên cùng một nhân vật), thức ăn/đồ vật trên thẻ chọn, nền vườn.',
        images=[R('Background/Carrot_BG', 'Nền vườn', 'Nền')]
               + [R('Story/Bi/' + k, l, 'Nhân vật Bi', fb) for k, l, fb in BI_STATES]
               + [R('Story/Food/' + k, v, 'Trái cây') for k, v in FRUITS.items() if k != 'blueberry']
               + [R('Fruit/carrot', 'Cà rốt', 'Thức ăn'), R('SaveEnvironment/thuc_an', 'Bữa cơm', 'Thức ăn'), R('SaveEnvironment/vo_keo', 'Kẹo', 'Không tốt'),
                  R('SaveEnvironment/vo_lon_coca', 'Nước ngọt', 'Không tốt'), R('SaveEnvironment/mieng_pizza', 'Bánh ngọt', 'Không tốt'),
                  R('SaveEnvironment/giot_nuoc', 'Nước lọc', 'Đồ uống'), R(NATURE('bed'), 'Cái giường', 'Đồ vật')]
               + [R('Story/Health/' + k, l, 'Sức khỏe') for k, l in HEALTH]),
    'CongHinh': dict(title='Cổng hình', intro='Hình cổng vẽ bằng code; con vật đi qua cổng thay được.',
        images=animals(['rabbit', 'dog', 'duck', 'pig', 'chick', 'bear'])),
    'VoBongBay': dict(title='Vỡ bóng bay (bé nhỏ)', intro='Bóng bay 7 màu và con vật chui ra khi vỡ bóng.',
        images=[R('Balloon/balloon_' + k, 'Bóng ' + l.lower(), 'Bóng bay') for k, l in BALLOON_COLORS] + animals([
            'bear', 'buffalo', 'chick', 'chicken', 'cow', 'crocodile', 'dog', 'duck', 'elephant', 'frog', 'giraffe', 'goat', 'gorilla', 'hippo', 'horse',
            'monkey', 'owl', 'panda', 'parrot', 'penguin', 'pig', 'rabbit', 'rhino', 'sloth', 'snake', 'zebra', 'whale', 'moose'])),
    'SanKyDieu': dict(title='Sàn kỳ diệu (bé nhỏ)', intro='Hoa và cây mọc ra khi chạm (sao, tim, bóng vẽ bằng code).',
        images=[R(NATURE(n), n, 'Hoa') for n in ['flower_redA', 'flower_redB', 'flower_yellowA', 'flower_yellowC', 'flower_purpleA', 'flower_purpleB']]
               + [R(NATURE(n), n, 'Cây / vườn') for n in ['tree_oak', 'tree_default', 'mushroom_red', 'plant_bush', 'flower_redC', 'tree_oak_dark']]),
    'CauBacQua': dict(title='Dài – ngắn, to – bé', intro='Thỏ, cà rốt và các con vật đi qua cửa to/bé.',
        images=animals(['rabbit', 'chick', 'dog', 'elephant']) + [R('Fruit/carrot', 'Cà rốt', 'Đồ vật')]),
    'DatDoVaoCho': dict(title='Đặt đồ vào chỗ (trên, dưới, trái, phải)', intro='Các con vật đặt quanh cái bàn.',
        images=animals(['rabbit', 'bear', 'dog', 'chick', 'pig', 'duck', 'panda'])),
    'TrungMauSac': dict(title='Trứng màu sắc', intro='Quả trứng 7 màu (nếu thay: dùng ĐÚNG màu ghi trên nhãn) và trái cây nở ra cùng màu.',
        images=[D('Story/Egg/' + k, 'Trứng ' + l.lower(), 'Trứng', lambda h=h: draw_egg(h)) for k, l, h in EGGS]
               + [R('Story/Food/' + f, FRUITS[f], 'Trái cây') for f in ['apple', 'blueberry', 'pear', 'banana', 'orange', 'peach', 'grape']]),
    'HinhDonGian': dict(title='Nhận biết hình đơn giản', intro='Mỗi hình có MÀU CỐ ĐỊNH (tim đỏ, tam giác vàng, vuông xanh lá, tròn xanh dương...). Ảnh thay thế nên giữ màu đó, nền trong suốt.',
        images=SHAPE_SLOTS),
    'NhoChuoiHinh': dict(title='Nhớ chuỗi hình', intro='8 biểu tượng dùng làm chuỗi cần nhớ. Mỗi biểu tượng nên có màu đặc trưng, nền trong suốt.',
        images=ICON_SLOTS),
    'LatTheNhoGiong': dict(title='Lật thẻ giống nhau', intro='8 biểu tượng in trên mặt thẻ. Mỗi biểu tượng nên có màu đặc trưng, nền trong suốt.',
        images=ICON_SLOTS),
    'NangNhe': dict(title='Nặng hay nhẹ', intro='18 con vật chia 3 nhóm nhẹ / vừa / nặng.',
        images=animals(['rabbit', 'duck', 'owl', 'frog', 'chick', 'parrot'], 'Con vật nhẹ')
               + animals(['bear', 'pig', 'goat', 'dog', 'monkey', 'penguin'], 'Con vật vừa')
               + animals(['elephant', 'hippo', 'rhino', 'buffalo', 'cow', 'horse'], 'Con vật nặng')),
}
# Không có gói: DongHoKhongLo (đồng hồ vẽ bằng code), DemKhoiHop (khối hộp 3D vẽ bằng mesh) — chưa có ảnh nào thay được.


def build(game, out_dir):
    spec = PACKS[game]
    entries, files, missing = [], {}, []
    for it in spec['images']:
        key = it['key']
        fname = key.replace('/', '__')
        data, ext = None, '.png'
        kind = it['src'][0]
        if kind == 'res':
            p = find_res(key) or (find_res(it['src'][2]) if it['src'][2] else None)
            if p:
                ext = os.path.splitext(p)[1].lower()
                data = open(p, 'rb').read()
            else:
                missing.append(key)
                import io; b = io.BytesIO(); draw_placeholder(it['label']).save(b, 'PNG'); data = b.getvalue()
        else:
            import io; b = io.BytesIO(); it['src'][1]().save(b, 'PNG'); data = b.getvalue()
        fname += ext
        files[fname] = data
        entries.append(dict(key=key, label=it['label'], group=it['group'], file=fname, default=fname))
    meta = dict(gameId=game, displayName=spec['title'], category='FloorStory', kind='floorstory',
                floorStory=dict(version=1, game=game, title=spec['title'], intro=spec['intro'], images=entries))
    gj = dict(schemaVersion=2, meta=meta, settings={}, layout={}, effects={}, rounds=[], imagePools=[])
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, game + '.zip')
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('game.json', json.dumps(gj, ensure_ascii=False, indent=2))
        for n, d in files.items():
            z.writestr('assets/' + n, d)
    return path, len(entries), missing


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    args = sys.argv[1:]
    out = os.path.join(REPO, 'WebTools', 'FloorStoryPacks')
    if '--out' in args:
        i = args.index('--out'); out = os.path.abspath(args[i + 1]); del args[i:i + 2]
    games = args or list(PACKS)
    for g in games:
        path, n, missing = build(g, out)
        print(f'{g}: {n} ảnh, {os.path.getsize(path) // 1024} KB' + (f'  THIẾU file mặc định (dùng ảnh trống): {", ".join(missing)}' if missing else ''))


if __name__ == '__main__':
    main()
