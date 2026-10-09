import json,re,math,collections,html,os
HERE=os.path.dirname(os.path.abspath(__file__))
T=os.path.join(HERE,'..','..','mau','tien-bo-toan-a4.html')
css=re.search(r'<style>(.*?)</style>',open(T,encoding='utf8').read(),re.S).group(1)
css+="""
body{padding:0;background:#fff}
@page{size:A4;margin:0}
.sheet{page-break-after:always;height:1123px;min-height:0;overflow:hidden;border-top:4px solid var(--red)}
.sheet:last-child{page-break-after:auto}
.pill{display:inline-block;padding:0 6px;border-radius:9px;font-size:10.5px;font-weight:600;color:#fff}
.l0{background:#b23a3a}.l1{background:#c0782a}.l2{background:#4a7fb0}.l3{background:#1f7a52}
.muted{color:var(--muted)}
:root{--font-display:"Cambria","Times New Roman",serif}
.pill{white-space:nowrap}
td.l{white-space:nowrap}
.na td{color:#9aa0a6;background:#fafafa}
.dots{display:flex;flex-wrap:wrap;gap:2px;align-items:flex-end;min-height:20px}
.dots i{display:block;width:9px;border-radius:2px 2px 0 0;background:#1f7a52}
.dots i.x{background:#b23a3a}
.srow{display:grid;grid-template-columns:150px minmax(0,1fr);gap:8px;border-bottom:1px solid var(--line);padding:5px 0;align-items:center}
.srow .t{font-size:11px;line-height:1.25}.srow .t b{font-weight:600;font-size:12px}
.warn{font-size:10.5px;color:var(--flat);margin-top:3px}
"""
out = []; rows = []; CALL = {}; HP = []; GN = {}; PERIOD = ''; F = {}
def lv(p): return 3 if p>=F['cuts'][2] else 2 if p>=F['cuts'][1] else 1 if p>=F['cuts'][0] else 0
LN=['Mức 1','Mức 2','Mức 3','Đạt mục tiêu']
e=html.escape
def radar(hp):
    cx,cy,R=112,100,66; n=5; s=''
    for k in (0.25,0.5,0.75,1):
        s+='<polygon points="%s" fill="none" stroke="#c9ced4" stroke-width="1"/>'%' '.join('%.1f,%.1f'%(cx+R*k*math.sin(2*math.pi*i/n),cy-R*k*math.cos(2*math.pi*i/n)) for i in range(n))
    pts=[]
    for i,h in enumerate(HP):
        a=2*math.pi*i/n
        s+='<line x1="%d" y1="%d" x2="%.1f" y2="%.1f" stroke="#c9ced4"/>'%(cx,cy,cx+R*math.sin(a),cy-R*math.cos(a))
        v=hp[h]['pt'] if h in hp else 0
        pts.append('%.1f,%.1f'%(cx+R*v/100*math.sin(a),cy-R*v/100*math.cos(a)))
        lx,ly=cx+(R+14)*math.sin(a),cy-(R+10)*math.cos(a)+3
        anc='middle' if abs(math.sin(a))<.3 else 'start' if math.sin(a)>0 else 'end'
        s+='<text x="%.1f" y="%.1f" font-size="10" text-anchor="%s" fill="%s">%s</text>'%(lx,ly,anc,'#1c1d20' if h in hp else '#9aa0a6',e(h))
    s+='<polygon points="%s" fill="rgba(168,37,43,.18)" stroke="#a8252b" stroke-width="1.6"/>'%' '.join(pts)
    for p,h in zip(pts,HP):
        if h in hp:
            x,y=p.split(',')
            s+='<circle cx="%s" cy="%s" r="2.6" fill="#a8252b"/>'%(x,y)
    return s

def page(d):
    name=d['name']; call=d['alias'] or name; hp=d['hp']; rs=[r for r in rows if r['code']==d['code']]
    tot=d['toan']; n=d['n']
    tr=''
    for h in HP:
        if h in hp:
            v=hp[h]
            tr+='<tr><td class="l">%s</td><td><b class="num">%.0f</b>%s</td><td class="num">%d/%d</td><td class="num">%.1f</td><td class="num">%s</td><td class="num">x%.2f</td><td><span class="pill l%d">%s</span></td></tr>'%(h,v['pt'],'<span class="flat">*</span>' if v['low'] else '',v['ok'],v['n'],v['acc'],'%.1f'%v['t'] if v['t'] else '-',v['k'],lv(v['pt']),LN[lv(v['pt'])])
        else:
            tr+='<tr class="na"><td class="l">%s</td><td colspan="6">Chưa có điểm (chưa chơi học phần này)</td></tr>'%h
    bars=''
    for h in HP:
        if h in hp:
            p=hp[h]['pt']
            bars+='<div class="brow"><div class="blab">%s</div><div class="bt"><span class="tick" style="left:25%%">25</span><span class="tick" style="left:50%%">50</span><span class="tick" style="left:75%%">75</span><div class="lane"><div class="bar" style="width:%.1f%%"></div></div></div><div class="bx">%.0f</div></div>'%(h,min(p,100),p)
        else:
            bars+='<div class="brow"><div class="blab muted">%s</div><div class="muted" style="font-size:11px;padding:6px 0">chưa có điểm</div><div></div></div>'%h
    sess=collections.OrderedDict()
    for r in sorted(rs,key=lambda r:r['ts']):
        sess.setdefault(r['sess'],[]).append(r)
    sh=''
    for sid,v in sess.items():
        g=v[0]['game']; ok=sum(x['ok'] for x in v); okt=[x['t'] for x in v if x['ok'] and x['t']>=0.5]
        dots=''.join('<i class="%s" style="height:%dpx"></i>'%('' if x['ok'] else 'x',max(4,min(22,round(x['t']*1.6)))) for x in v)
        sh+='<div class="srow"><div class="t"><b>%s</b><br><span class="muted">%s%s:%s - %d/%d đúng - TB %.1fs</span></div><div class="dots">%s</div></div>'%(GN.get(g,g),(sid[8:10]+'/'+sid[5:7]+' ') if '->' in PERIOD else '',sid[11:13],sid[13:15],ok,len(v),sum(okt)/len(okt) if okt else 0,dots)
    ps=[(h,hp[h]) for h in HP if h in hp]
    best=max(ps,key=lambda x:(x[1]['pt'],x[1]['n'])); worst=min(ps,key=lambda x:(x[1]['pt'],-x[1]['n']))
    allt=[x['t'] for x in rs if x['ok'] and x['t']>=0.5]; tm=sum(allt)/len(allt)
    sp='nhanh' if tm<=5 else 'vừa phải' if tm<10 else 'chậm'
    ins='<li><b>Mạnh nhất:</b> %s (%.0f điểm)</li>'%(best[0],best[1]['pt'])
    if len(ps)>1 and worst[1]['pt']<best[1]['pt']-1:
        ins+='<li><b>Cần luyện:</b> %s (%.0f điểm, đúng %d/%d)</li>'%(worst[0],worst[1]['pt'],worst[1]['ok'],worst[1]['n'])
    ins+='<li><b>Tốc độ:</b> %s, TB %.1f giây/câu đúng</li>'%(sp,tm)
    miss=[h for h in HP if h not in hp]
    if miss: ins+='<li><b>Chưa có điểm:</b> %s</li>'%e(', '.join(miss))
    gap=[(h,F['cuts'][2]-v['pt']) for h,v in ps if v['pt']<F['cuts'][2]]
    goal='<dt>Điểm hiện tại</dt><dd class="num">%.0f</dd><dt>Điểm mục tiêu</dt><dd class="num">%d</dd>'%(tot,F['cuts'][2])
    if gap:
        for h,g in sorted(gap,key=lambda x:-x[1]): goal+='<dt>Luyện %s</dt><dd class="num">+%.0f</dd>'%(h.lower(),g)
    else:
        goal+='<dt>Học phần đã chơi</dt><dd>đều đạt</dd>'
    low=[h for h,v in ps if v['low']]
    warns=[]
    if n<F['few_total']: warns.append('Chỉ %d câu tính điểm, kết quả mang tính tham khảo.'%n)
    if low: warns.append('* Học phần dưới 10 câu: ít mẫu (%s).'%e(', '.join(low)))
    L=lv(tot)
    W=''.join('<div class="warn">'+w+'</div>' for w in warns)
    return f'''<main class="sheet">
  <div class="top"><img class="logo" src="logo.png" alt="EduXplore" height="58"><div class="dev">[Hồ sơ năng lực Toán] [Báo cáo theo buổi chơi]</div></div>
  <div class="idbar" style="grid-template-columns:1fr 1.8fr .9fr .8fr 1.4fr">
    <div><small>Mã HS</small><b>{d['code']}</b></div><div><small>Họ và tên</small><b>{e(name)}</b></div>
    <div><small>Tên gọi</small><b>{e(call)}</b></div><div><small>Lớp</small><b>{e(d['class'])}</b></div>
    <div><small>Kỳ báo cáo</small><b class="num">{PERIOD}</b></div></div>
  <div class="cols"><div class="main">
    <section><h2>Phân tích kỹ năng <small>điểm = {F['base']:g} × tỉ lệ đúng × hệ số tốc độ</small></h2>
      <table><tr><th>Kỹ năng</th><th class="dk">Điểm</th><th>Đúng/Số câu</th><th>Chính xác (%)</th><th>Thời gian (giây)</th><th>Hệ số tốc độ</th><th style="width:92px">Mức</th></tr>{tr}</table>
      {W}</section>
    <section><h2>Điểm từng học phần <small>mốc {F['cuts'][0]} / {F['cuts'][1]} / {F['cuts'][2]}</small></h2>{bars}
      <div class="legend"><b>{F['cuts'][0]}</b> Mức 2 - <b>{F['cuts'][1]}</b> Mức 3 - <b>{F['cuts'][2]}</b> Đạt mục tiêu</div></section>
    <section><h2>Nhịp trả lời trong buổi <small>mỗi cột là một câu, cao = lâu, đỏ = sai</small></h2>{sh}</section>
  </div>
  <aside class="aside">
    <section><h2>Điểm EduXplore</h2><div class="score"><b class="num">{tot:.0f}</b><span>/ 100</span></div>
      <div class="delta"><span class="pill l{L}">{LN[L]}</span></div><div class="note">Trung bình {len(ps)} học phần đã chơi</div></section>
    <section><h2>Bản đồ kỹ năng</h2><svg viewBox="0 0 224 200" width="224" height="200">{radar(hp)}</svg></section>
    <section><h2>Mục tiêu</h2><dl class="kv">{goal}</dl></section>
    <section><h2>Nhận xét</h2><ul class="ins">{ins}</ul></section>
  </aside></div>
  <div class="foot"><span>Hệ số tốc độ: {F['kmax']:g} nếu TB ≤ {F['tfast']:g}s, 1,0 nếu ≥ {F['tslow']:g}s, tuyến tính ở giữa. Điểm tối đa 100.</span><span>Kỳ báo cáo: {PERIOD}. Chưa có xu hướng theo tháng / chuẩn theo tuổi.</span></div>
</main>'''

def doc(pages,title):
    return '<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>%s</title><style>%s</style></head><body>%s</body></html>'%(title,css,''.join(pages))

def build(cfg_,out_,rows_,outdir,period):
    """Ghi outdir/<mã>.html + TatCa.html (cần logo.png cạnh). Trả số phiếu."""
    global out,rows,CALL,HP,GN,PERIOD,F
    out,rows,HP,GN,PERIOD,F=out_,rows_,cfg_['hp_order'],cfg_['game_names'],period,cfg_['formula']
    ds=[d for d in out if 'toan' in d]
    os.makedirs(outdir,exist_ok=True)
    for d in ds:
        open(os.path.join(outdir,d['code']+'.html'),'w',encoding='utf8').write(doc([page(d)],'Hồ sơ Toán '+d['name']))
    open(os.path.join(outdir,'TatCa.html'),'w',encoding='utf8').write(doc([page(d) for d in ds],'Hồ sơ Toán '+period))
    return len(ds)
