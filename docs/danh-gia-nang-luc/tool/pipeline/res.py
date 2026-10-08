import json,collections,csv
rows=json.load(open('rows.json',encoding='utf8'))
ROSTER=[('x',n) for n in ['Ngô Quốc An','Nguyễn Đăng Bách','Nguyễn Ngọc Bảo Châu','Vũ Đình Khánh','Nguyễn Anh Khôi','Tạ Ngọc Khuê','Nguyễn Phương Linh','Đào Khánh Ngọc','Phạm Minh Ngọc','Đặng Tâm Như','Nguyễn Hà Linh Phương','Dư Thanh Trà','Doãn Minh Trí','Đinh Nguyễn Cát Tường','Nguyễn Hòa Vũ','Trần Bảo Vy','Trương Thảo Vy','Đặng Minh Anh']]
HP=['Nhận biết số','Đếm','Cộng','Trừ','So sánh']
def K(t): 
    return 1.25 if t<=5 else 1.0 if t>=20 else 1+0.25*(20-t)/15
def lv(p): return 'Đạt mục tiêu' if p>=75 else 'Mức 3' if p>=50 else 'Mức 2' if p>=25 else 'Mức 1'
out=[]
for i,(_,name) in enumerate(ROSTER,1):
    code='CASA-22001' if name=='Đặng Minh Anh' else 'CASA-21%03d'%i
    rs=[r for r in rows if r['kid']==name]
    d={'code':code,'name':name,'n':len(rs),'hp':{}}
    for hp in HP:
        a=sorted([r for r in rs if r['hp']==hp],key=lambda r:r['ts'])[-50:]
        if not a: continue
        ok=sum(r['ok'] for r in a)
        ts=[r['t'] for r in a if r['ok'] and r['t']>=0.5]
        tm=sum(ts)/len(ts) if ts else None
        k=K(tm) if tm is not None else 1.0
        acc=ok/len(a)
        d['hp'][hp]=dict(n=len(a),ok=ok,acc=100*acc,t=tm,k=k,pt=min(100,80*acc*k),low=len(a)<10)
    if d['hp']:
        d['toan']=sum(v['pt'] for v in d['hp'].values())/len(d['hp'])
    sess=sorted({r['sess'][11:17]+' '+r['game'] for r in rs})
    d['sess']=sess
    out.append(d)
    print(code,name,'n=%d'%d['n'],'Toán=%s'%('%.1f %s'%(d['toan'],lv(d['toan'])) if 'toan' in d else '—'),'|',', '.join('%s %d/%d %.0f%% %s×%.2f=%.1f%s'%(h,v['ok'],v['n'],v['acc'],'%.1fs'%v['t'] if v['t'] else '-',v['k'],v['pt'],'*' if v['low'] else '') for h,v in d['hp'].items()))
json.dump(out,open('out.json','w',encoding='utf8'),ensure_ascii=False)
