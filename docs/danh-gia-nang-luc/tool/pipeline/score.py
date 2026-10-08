import json,glob,os,collections,re
G=('Blue_1','Red_1','Player_1','Player_2')
GAMES={'AddNumber5Digit','TongHopToan','Counting5','Counting'}
OVERRIDE={('2026-10-08_161210_Counting.json','right'):'Đinh Nguyễn Cát Tường'}
rows=[]; allnames=collections.Counter(); sess=[]
for f in sorted(glob.glob('C:/Users/ADMIN/Downloads/GameLogs/2026-10-08_*.json')):
    b=os.path.basename(f)
    try: d=json.load(open(f,encoding='utf8'))
    except: continue
    if 'entries' not in d: continue
    for e in d['entries']:
        if e['recognized']: allnames[e['name']]+=1
    if b<'2026-10-08_154620': continue
    es=d['entries']; game=es[0]['game']
    for slot in('left','right'):
        rr=[e for e in es if e['slot']==slot and e['eventType']=='round']
        if len(rr)<5: continue
        if game not in GAMES: continue
        c=collections.Counter(e['name'] for e in rr if e['name'] not in G)
        kid=OVERRIDE.get((b,slot)) or c.most_common(1)[0][0]
        for e in rr:
            q=e['questionId']
            if game=='TongHopToan':
                k=q.split('_')[2]; hp={'Recognize':'Nhận biết số','CountEasy':'Đếm','Count':'Đếm','Add':'Cộng','Sub':'Trừ','Sign':'So sánh'}[k]
            else: hp={'AddNumber5Digit':'Cộng','Counting5':'Đếm','Counting':'Đếm'}[game]
            rows.append(dict(kid=kid,hp=hp,ok=e['correct'],t=e['answerTimeSec'],game=game,ts=e['time'],sess=b))
        sess.append((b,slot,kid,len(rr)))
print(len(allnames),sorted(allnames))
kids=sorted({r['kid'] for r in rows}); print(len(kids),kids)
json.dump(rows,open('rows.json','w',encoding='utf8'),ensure_ascii=False)
