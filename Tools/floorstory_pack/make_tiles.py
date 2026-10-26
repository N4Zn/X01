#!/usr/bin/env python3
"""Dựng ảnh thẻ game (480x288) cho ControlActivity: NativePlugins/ControlUiAndroidLib/controlui/src/main/assets/game_tiles/<GameId>.jpg
từ chính ảnh của game trong Assets/Game/Resources/StoryData/<GameId>/ (nền cảnh + vài vật). Chạy: python Tools/floorstory_pack/make_tiles.py [GameId ...]
Sau đó build lại aar: sh Tools/build-aar.sh controlui"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
SD = os.path.join(root, 'Assets', 'Game', 'Resources', 'StoryData')
OUT = os.path.join(root, 'NativePlugins', 'ControlUiAndroidLib', 'controlui', 'src', 'main', 'assets', 'game_tiles')
W, H, S = 480, 288, 2   # vẽ gấp đôi rồi thu nhỏ cho mịn

def img(g, f): return Image.open(os.path.join(SD, g, f)).convert('RGBA')
def hexc(h): return tuple(int(h[i:i+2], 16) for i in (1, 3, 5))
def cover(im, w, h):
    r = max(w / im.width, h / im.height); im = im.resize((int(im.width * r + .5), int(im.height * r + .5)), Image.LANCZOS)
    x, y = (im.width - w) // 2, (im.height - h) // 2; return im.crop((x, y, x + w, y + h))
def gradient(c1, c2):
    base = Image.new('RGB', (W * S, H * S)); d = ImageDraw.Draw(base)
    for y in range(H * S): t = y / (H * S - 1); d.line([(0, y), (W * S, y)], fill=tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3)))
    return base.convert('RGBA')
def bokeh(canvas):
    ov = Image.new('RGBA', canvas.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    for cx, cy, r in [(40, 60, 34), (110, 250, 44), (400, 40, 30), (440, 220, 50), (300, 270, 24), (230, 24, 20)]:
        d.ellipse([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], fill=(255, 255, 255, 46))
    return Image.alpha_composite(canvas, ov)
def bg_image(g, f, dim=0):
    b = cover(img(g, f), W * S, H * S)
    if dim: b = Image.alpha_composite(b, Image.new('RGBA', b.size, (255, 255, 255, dim)))
    return b
def paste(canvas, im, cx, cy, size, rot=0):
    im = im.resize((int(size * S), int(size * S * im.height / im.width)), Image.LANCZOS)
    if rot: im = im.rotate(rot, expand=True, resample=Image.BICUBIC)
    canvas.alpha_composite(im, (int(cx * S - im.width / 2), int(cy * S - im.height / 2)))
def shadow(canvas, cx, cy, w, h):
    ov = Image.new('RGBA', canvas.size, (0, 0, 0, 0)); ImageDraw.Draw(ov).ellipse([(cx - w / 2) * S, (cy - h / 2) * S, (cx + w / 2) * S, (cy + h / 2) * S], fill=(0, 0, 0, 60))
    canvas.alpha_composite(ov.filter(ImageFilter.GaussianBlur(5 * S)))
def card(canvas, cx, cy, size, fill=(255, 255, 255, 255), border=None):
    ov = Image.new('RGBA', canvas.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov); r = size * .16 * S
    box = [(cx - size / 2) * S, (cy - size / 2) * S, (cx + size / 2) * S, (cy + size / 2) * S]
    sh = Image.new('RGBA', canvas.size, (0, 0, 0, 0)); ImageDraw.Draw(sh).rounded_rectangle([box[0], box[1] + 4 * S, box[2], box[3] + 4 * S], r, fill=(0, 0, 0, 70)); canvas.alpha_composite(sh.filter(ImageFilter.GaussianBlur(4 * S)))
    d.rounded_rectangle(box, r, fill=fill, outline=border, width=int(3 * S) if border else 0); canvas.alpha_composite(ov)

def NhanBietMau():
    c = bokeh(gradient(hexc('#FFF8E1'), hexc('#FFCC80'))); cols = ['#E53935', '#1E88E5', '#43A047', '#FDD835', '#FB8C00']
    for i, col in enumerate(cols):
        cx = 62 + i * 89; card(c, cx, 120 if i % 2 == 0 else 150, 74)
        d = ImageDraw.Draw(c); cy = (120 if i % 2 == 0 else 150) * S; r = 24 * S; d.ellipse([cx * S - r, cy - r, cx * S + r, cy + r], fill=hexc(col))
    return c
def BeKhoeManhRound():
    c = bg_image('BeKhoeManhRound', 'garden_bg.png')
    paste(c, img('BeKhoeManhRound', 'bi_hungry.png'), 150, 160, 190); paste(c, img('BeKhoeManhRound', 'bi_happy.png'), 330, 150, 170)
    return c
def ConNaoNangHon():
    c = bg_image('ConNaoNangHon', 'sc_3.png'); d = ImageDraw.Draw(c)
    d.polygon([(240 * S, 250 * S), (205 * S, 285 * S), (275 * S, 285 * S)], fill=(120, 85, 60))
    plank = Image.new('RGBA', (400 * S, 16 * S), (0, 0, 0, 0)); ImageDraw.Draw(plank).rounded_rectangle([0, 0, 400 * S - 1, 16 * S - 1], 8 * S, fill=(176, 125, 80)); plank = plank.rotate(9, expand=True, resample=Image.BICUBIC)
    c.alpha_composite(plank, (int(240 * S - plank.width / 2), int(222 * S - plank.height / 2)))
    paste(c, img('ConNaoNangHon', 'size_chick.png'), 398, 140, 96); paste(c, img('ConNaoNangHon', 'size_elephant.png'), 112, 186, 150); return c
def NhoChuoiHinh2():
    c = bg_image('NhoChuoiHinh2', 'sc_10.png', 40)
    for i, (fr, it) in enumerate([('sc_58.png', 'it_39.png'), ('sc_19.png', 'it_40.png'), ('sc_60.png', 'it_41.png')]):
        cx = 88 + i * 100; shadow(c, cx, 150, 80, 16); paste(c, img('NhoChuoiHinh2', fr), cx, 120, 100); paste(c, img('NhoChuoiHinh2', it), cx, 120, 62)
    paste(c, img('NhoChuoiHinh2', 'sc_29.png'), 392, 120, 100)
    for i, it in enumerate(['it_42.png', 'it_43.png', 'it_44.png', 'it_45.png']): paste(c, img('NhoChuoiHinh2', it), 90 + i * 100, 238, 62)
    return c
def HinhQuanhBe():
    c = bg_image('HinhQuanhBe', 'sc_21.png'); g = 'HinhQuanhBe'
    for i, f in enumerate(['sh_tron.png', 'sh_vuong.png', 'sh_tamgiac.png', 'sh_sao.png']):
        cx = 70 + i * 113; card(c, cx, 150, 92); paste(c, img(g, f), cx, 150, 68)
    paste(c, img(g, 'ob_ball.png'), 405, 238, 78); paste(c, img(g, 'ob_gift.png'), 70, 238, 66); return c
def LatTheNhoGiong2():
    c = bg_image('LatTheNhoGiong2', 'nen_go.png'); g = 'LatTheNhoGiong2'
    spots = [(96, 76), (216, 76), (336, 76), (156, 196), (276, 196), (396, 196)]; kinds = ['it_sun.png', None, 'it_moon.png', None, 'it_sun.png', 'it_star.png']
    frames = ['khung_xanh_duong.png', None, 'khung_xanh_duong.png', None, 'khung_xanh_duong.png', 'khung_hong.png']
    for (cx, cy), it, fr in zip(spots, kinds, frames):
        shadow(c, cx, cy + 50, 96, 14)
        if it: paste(c, img(g, fr), cx, cy, 108); paste(c, img(g, it), cx, cy, 66)
        else: paste(c, img(g, 'o_hoi.png'), cx, cy, 108)
    return c

GAMES = {f.__name__: f for f in [NhanBietMau, BeKhoeManhRound, ConNaoNangHon, NhoChuoiHinh2, HinhQuanhBe, LatTheNhoGiong2]}
# ── Game GenericGame / không có StoryData: ảnh lấy từ Resources/TestTongHop/images/GenericGames/<GameId>/ hoặc vẽ tay ──
GG = os.path.join(root, 'Assets', 'Game', 'Resources', 'TestTongHop', 'images', 'GenericGames')
def gimg(g, f): return Image.open(os.path.join(GG, g, f)).convert('RGBA')
def font(sz):
    from PIL import ImageFont
    for f in ['C:/Windows/Fonts/arialbd.ttf', '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf']:
        try: return ImageFont.truetype(f, int(sz * S))
        except Exception: pass
    return ImageFont.load_default()
def BongBay():
    g = 'BongBay'
    c = cover(gimg(g, 'nen_bong_bay.png'), W * S, H * S)
    # bóng bay đủ 7 màu rải theo 2 hàng, mỗi bóng 1 số (đúng kiểu game: bóng đáp án 1..5)
    spots = [('bong_do', 70, 170, 118, -8, 4), ('bong_vang', 160, 120, 124, 5, 1), ('bong_xanhla', 252, 175, 120, -4, 5),
             ('bong_tim', 342, 122, 124, 6, 3), ('bong_cam', 424, 178, 116, -6, 2), ('bong_hong', 30, 92, 70, 8, 0), ('bong_xanhduong', 458, 84, 66, -8, 0)]
    for name, cx, cy, size, rot, num in spots:
        paste(c, gimg(g, name + '.png'), cx, cy, size, rot)
        if num:
            d = ImageDraw.Draw(c); f = font(size * .42); t = str(num)
            bb = d.textbbox((0, 0), t, font=f); tw, th = bb[2] - bb[0], bb[3] - bb[1]
            d.text((cx * S - tw / 2 - bb[0], (cy - size * .06) * S - th / 2 - bb[1]), t, font=f, fill=(255, 255, 255), stroke_width=int(3 * S), stroke_fill=(60, 60, 60))
    return c
def PoseTest():
    c = bokeh(gradient(hexc('#E3F2FD'), hexc('#90CAF9'))); d = ImageDraw.Draw(c)
    def person(cx, cy, k, col, pose):   # que xương: đầu, thân, tay, chân + khớp
        j = {'head': (0, -62), 'neck': (0, -44), 'hip': (0, 6), 'lsh': (-16, -40), 'rsh': (16, -40)}
        j.update(pose)
        pt = lambda n: ((cx + j[n][0] * k) * S, (cy + j[n][1] * k) * S)
        for a, b in [('neck', 'hip'), ('lsh', 'rsh'), ('lsh', 'lel'), ('lel', 'lwr'), ('rsh', 'rel'), ('rel', 'rwr'), ('hip', 'lkn'), ('lkn', 'lan'), ('hip', 'rkn'), ('rkn', 'ran')]:
            d.line([pt(a), pt(b)], fill=col, width=int(7 * k * S), joint='curve')
        hx, hy = pt('head'); r = 15 * k * S; d.ellipse([hx - r, hy - r, hx + r, hy + r], outline=col, width=int(7 * k * S))
        for n in j:
            if n != 'head':
                x, y = pt(n); rr = 6 * k * S; d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=(255, 255, 255), outline=col, width=int(3 * k * S))
    person(150, 160, 1.7, hexc('#1E88E5'), {'lel': (-34, -52), 'lwr': (-44, -84), 'rel': (34, -52), 'rwr': (44, -84), 'lkn': (-12, 40), 'lan': (-14, 80), 'rkn': (12, 40), 'ran': (14, 80)})   # 2 tay giơ lên
    person(330, 160, 1.7, hexc('#E53935'), {'lel': (-36, -30), 'lwr': (-62, -34), 'rel': (36, -30), 'rwr': (62, -34), 'lkn': (-24, 36), 'lan': (-40, 80), 'rkn': (24, 36), 'ran': (40, 80)})   # dang tay + chân
    return c
GAMES.update({'BongBay': BongBay, 'PoseTest': PoseTest})

if __name__ == '__main__':
    for gid in (sys.argv[1:] or GAMES):
        im = GAMES[gid]().convert('RGB').resize((W, H), Image.LANCZOS)
        im.save(os.path.join(OUT, gid + '.jpg'), quality=88); print('ok', gid)
