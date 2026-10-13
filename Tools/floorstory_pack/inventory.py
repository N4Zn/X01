"""Kiểm kê mọi thứ trong fs/games/*.json (loại đối tượng, thuộc tính, hành động, biểu thức, tag, biến) kèm số game dùng. Chạy từ gốc repo: python Tools/floorstory_pack/inventory.py — dùng cho docs/game-builder-generalization.md."""
import json,glob,os,re,collections,sys
sys.stdout.reconfigure(encoding='utf-8')
C=collections.defaultdict(lambda: collections.defaultdict(set))   # category -> key -> games
def add(cat,key,g): C[cat][key].add(g)
def lookkind(l):
    if not isinstance(l,dict): return '?'
    for k in ('item','image','icon','text','kind','shape'):
        if k in l: return k+(':'+str(l[k]) if k=='kind' else '')
    return '?'
def walkobj(o,g,where):
    if not isinstance(o,dict): return
    add('obj.look@'+where, lookkind(o.get('look')), g)
    for k in ('role','z','anim','fill','repeat','forEachItem','smooth','rot','alpha','show','id'):
        if k in o:
            add('obj.attr@'+where, k+(('='+str(o[k])) if k in('role','z') else ''), g)
    for k in (o.get('bind') or {}): add('obj.bind.prop', k, g)
    if isinstance(o.get('anim'),dict): add('obj.anim.type', o['anim'].get('type'), g)
    for k in o:
        if isinstance(o[k],str) and o[k].startswith('='): add('obj.expr-attr',k,g)
def acts(lst,g,ev):
    for a in lst or []:
        if not isinstance(a,dict): continue
        add('action',a.get('do'),g); add('event->action', ev+':'+str(a.get('do')), g)
        for k in ('then','else','acts'):
            if isinstance(a.get(k),list): acts(a[k],g,ev)
        if a.get('do')=='spawn': add('spawn.look', lookkind(a.get('look')), g)
        for k in ('on','at','to'):
            v=a.get(k)
            if isinstance(v,str): add('target-ref', re.sub(r'[A-Za-z0-9_]+$','X',v) if v.startswith('#') else v if v.startswith('@') else 'expr', g)
            elif isinstance(v,dict): add('target-ref','{point}',g)
FUN=re.compile(r'\b([a-zA-Z_]+)\(')
for f in sorted(glob.glob('WebTools/GenericGameBuilder/fs/games/*.json')):
    g=os.path.basename(f)[:-5]; d=json.load(open(f,encoding='utf-8'))
    s=json.dumps(d,ensure_ascii=False)
    for fn in FUN.findall(re.sub(r'"[a-zA-Z]+":','',s)): 
        if fn not in('do',): add('expr.func',fn,g)
    for it in d['items']:
        add('item.appearance', 'image' if it.get('image') else 'icon' if it.get('icon') else 'shape', g)
        for k,v in (it.get('tags') or {}).items(): add('item.tag', k+':'+('num' if isinstance(v,(int,float)) else 'str'), g)
        if 'value' in it: add('item.field','value',g)
        if 'sound' in it: add('item.field','sound',g)
    for k in (d.get('vars') or {}): add('state.var',k,g)
    for o in d.get('scene',[]): walkobj(o,g,'scene')
    for fl in (d.get('flows') or [d['flow']]):
        add('flow.gen',fl['gen']['type'],g); add('flow.judge',fl['judge']['type'],g)
        for k,v in (fl['gen'].get('p') or {}).items(): add('gen.param@'+fl['gen']['type'],k,g)
        for o in fl.get('objects',[]): walkobj(o,g,'flow.objects')
        for v in fl.get('views',[]):
            add('view',v['type'],g)
            for k,val in (v.get('p') or {}).items():
                add('view.param@'+v['type'],k,g)
                if k=='tpl':
                    for lay in val:
                        add('tpl.layer.look',lookkind(lay.get('look')),g)
                        for kk in lay:
                            if kk not in('look',): add('tpl.layer.attr',kk,g)
                if k=='layout': add('view.layout',val,g)
            if v.get('reveal'): add('view.reveal',v['reveal'].get('mode'),g)
        for ev,l in (fl.get('on') or {}).items(): acts(l,g,ev)
        for k in (fl.get('text') or {}): add('flow.text.key',k,g)
        if fl.get('manual'): add('flow.sched','manual-rounds',g)
        if fl.get('sounds'): add('flow.sounds',','.join(fl['sounds']),g)
    if len(d.get('flows',[]))>1: add('flow.sched','multi-flow:'+d.get('flowPick','?'),g)
    if d.get('sync'): add('game.opt','sync',g)
    if d.get('theme'): add('game.opt','theme',g)
    if d.get('mirror') is not None: add('game.opt','mirror',g)
for cat in sorted(C):
    print('##',cat)
    for k,gs in sorted(C[cat].items(), key=lambda x:-len(x[1])): print('  %-34s %2d  %s'%(k,len(gs),' '.join(sorted(gs))[:110]))
