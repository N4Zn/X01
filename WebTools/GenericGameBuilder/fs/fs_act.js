/* FloorStory — cảnh (scene objects có ràng buộc `bind`), biến trạng thái (vars) và danh sách hành động (actions).
   Bản C# tương ứng: FloorStoryGame/Flow/FsScene.cs + FsActions.cs (PHẢI khớp). Biểu thức: fs_expr.js.

   Obj (đối tượng cảnh):  { id, role, look, x, y, dx, dy, w, h, fill, rot, alpha, scale, z:'back|front', show, anim, smooth, bind:{prop:expr}, repeat:{n,dx,dy}, forEachItem:{where} }
     look: { shape,color } | { image } | { item:'id'|'=expr' } | { icon } | { text,fs,color } | { kind:'rrect',color } | { kind:'hand',len,thick,color }
     x,y: toạ độ 0..1 của world (gốc dưới-trái, như P()); w,h: theo đơn vị U (h mặc định = w); dx,dy: lệch (đơn vị U); số hoặc "=biểu thức".
     bind: mỗi frame tính lại, làm mượt (smooth giây): show x y w h scale rot alpha color(#hex) item text
   Hành động ({do:...}; tham số "=biểu thức" được tính lúc chạy):
     sfx{k} say{text,fy,color,sec} confetti{at,n} bounce{on,amount,sec} shake{on} pulse{on,sec} move{on,to,sec,wait} scale{on,to,sec} fade{on,to,sec} tint{on,color,sec}
     rotate{on,to,sec} show{on} hide{on} pop{on} destroy{on} spawn{look,at,size,pop,id} set{var,expr} inc{var,by,mod} bag{var,values} pickItem{var,tag,value}
     if{cond,then,else} wait{sec} fly{from,to,sec,look} par{acts} text{on,text} std{ev:'right'|'wrong'}
   Đích (on/at/to/from): "@tapped" "@target" "@actor" "#id" | "=biểu thức(trả id)" | {fx,fy} */
(function(){
'use strict';
const F = window.FSW, { UI, hex, col, WHITE, tw, World, T } = F, X = F.expr;

function* rotTo(n, to, dur){
  if (!n || n.dead) return; const f = n.rot; dur = dur || 0;
  if (dur <= 0){ n.rot = to; return; }
  for (let t=0; t<dur; t+=T.dt){ if (n.dead) return; n.rot = F.lerp(f, to, F.smooth(t/dur)); yield null; }
  if (!n.dead) n.rot = to;
}
tw.rotTo = rotTo;

const P = World.prototype;
P.scope = function(extra){ return Object.assign({ wr:this.W/this.U, hr:this.H/this.U, vars:this.vars, q: Object.assign({}, this._Q||{}, (this._Q||{}).v||{}, { targetItem: (this._Q && this._Q.items && this._Q.target >= 0) ? (this._Q.items[this._Q.target] || '') : '' }), w:this }, extra||{}); };
P.ev = function(v, extra){ return X.val(v, this.scope(extra)); };
P.num = function(v, d, extra){ const r = this.ev(v, extra); const n = X.toNum(r); return (v === undefined || v === null || v === '') ? d : n; };

/** Vẽ 1 "diện mạo" tại (x,y) px, cỡ w×h px. */
P.drawLook = function(parent, look, x, y, w, h, extra){
  look = look || {}; const ev = v=> this.ev(v, extra);
  const kind = look.kind;
  if (kind === 'hand'){
    const root = UI.rect(parent, x, y, 0, 0), len = (+ev(look.len)||.3)*this.U, th = (+ev(look.thick)||.03)*this.U;
    const b = UI.pic(root, 'square', hex(ev(look.color)||'#2A2F55'), 0, len/2, th, len); b.preserveAspect = false; return root;
  }
  if (look.text !== undefined){
    const n = UI.label(parent, String(ev(look.text)), Math.round((+ev(look.fs)||.06)*this.U), hex(ev(look.color)||'#FFFFFF'), x, y, w||this.U, h||this.U*.2, look.outline !== 0);
    return n;
  }
  const itemId = look.item !== undefined ? String(ev(look.item)) : null;
  if (itemId){
    const it = this.items.find(i=> i.id === itemId);
    if (it){ const n = this.drawItem(parent, it, x, y, Math.min(w, h) || w); if (w && h && !(it.image || it.icon)){ n.setSize(w, h); } return n; }
  }
  if (look.image){ const u = this.img(String(ev(look.image))); if (u) return UI.pic(parent, { url:u }, WHITE, x, y, w, h); }
  if (look.icon) return F.buildIcon(parent, String(ev(look.icon)), x, y, Math.min(w, h));
  const sh = ev(look.shape) || (kind === 'rrect' ? 'rrect' : 'square'), n = UI.pic(parent, sh === 'rect' ? 'square' : sh, hex(ev(look.color)||'#90A4AE'), x, y, w, h);
  n.preserveAspect = false; if (sh === 'rrect') n.sliced = true; n._paint(); return n;
};

/** Tạo 1 đối tượng cảnh (đã bung repeat/forEachItem). */
P.makeObj = function(spec, parent, store, extra){
  const sc = ()=> Object.assign({}, extra||{}), ev = v=> this.ev(v, extra);
  const W = this.W, H = this.H, U = this.U;
  const fx = +ev(spec.x == null ? .5 : spec.x), fy = +ev(spec.y == null ? .5 : spec.y);
  const w = (+ev(spec.w == null ? .2 : spec.w))*U, h = (spec.h == null ? +ev(spec.w == null ? .2 : spec.w) : +ev(spec.h))*U;
  const pos = spec.rel ? { x:0, y:0 } : this.P(fx, fy);
  const o = { spec, extra: extra || {}, id: spec.id ? String(ev(spec.id)) : null, role: spec.role || null, cur: {}, animOn: true, dead: false, t0: this.rng.next()*6.28 };
  if (spec.fill){
    const lk = spec.look || {}, itId = lk.item !== undefined ? String(ev(lk.item)) : '', itm = itId ? this.items.find(i=> i.id === itId) : null, url = lk.image ? this.img(String(ev(lk.image))) : (itm && itm.image ? this.img(itm.image) : null);
    o.node = url ? UI.fill(parent, WHITE, { url }) : UI.fill(parent, hex(ev(lk.color)||'#FFFFFF'));
    if (url) o.node.preserveAspect = false;
  }
  else o.node = this.drawLook(parent, spec.look, pos.x + (+ev(spec.dx||0))*U, pos.y + (+ev(spec.dy||0))*U, w, h, extra);
  o.x0 = o.node.x; o.y0 = o.node.y;
  if (spec.rot !== undefined) o.node.rot = +ev(spec.rot); if (spec.scale !== undefined) o.node.scale = +ev(spec.scale);
  if (spec.alpha !== undefined) o.node.alpha = +ev(spec.alpha);
  if (spec.show !== undefined) o.node.active = X.truthy(ev(spec.show));
  store.push(o); return o;
};
P.makeObjs = function(specs, parent, store, extra){
  (specs || []).forEach(spec=>{
    if (spec.forEachItem){
      this.items.forEach((it, idx)=>{
        const ex = Object.assign({}, extra||{}, { item: it, idx });
        if (spec.forEachItem.where && !X.truthy(this.ev('=' + spec.forEachItem.where, ex))) return;
        this.makeObj(spec, parent, store, ex);
      });
    } else if (spec.repeat){
      const n = Math.round(+this.ev(spec.repeat.n, extra));
      for (let i=0;i<n;i++){
        const s = Object.assign({}, spec); s.x = (+this.ev(spec.x == null ? .5 : spec.x, extra)) + i*(+spec.repeat.dx||0); s.y = (+this.ev(spec.y == null ? .5 : spec.y, extra)) + i*(+spec.repeat.dy||0); delete s.repeat;
        this.makeObj(s, parent, store, Object.assign({}, extra||{}, { i, n }));
      }
    } else this.makeObj(spec, parent, store, extra);
  });
};
/** Mỗi frame: tính lại `bind` + hoạt hình nhẹ (bob/pulse). */
P.updateObjs = function(list, dt, time){
  const U = this.U;
  list.forEach(o=>{
    const nd = o.node; if (!nd || nd.dead) return;
    const sp = o.spec, bind = sp.bind;
    if (bind){
      const smooth = sp.smooth == null ? .3 : +sp.smooth, k = smooth <= 0 ? 1 : 1 - Math.exp(-dt/(smooth/3));
      Object.keys(bind).forEach(prop=>{
        let tv; try { tv = X.eval(String(bind[prop]), this.scope(o.extra)); } catch(e){ return; }
        const num = X.toNum(tv), cur = o.cur;
        switch (prop){
          case 'show': nd.active = X.truthy(tv); break;
          case 'item': if (cur.item !== String(tv)){ cur.item = String(tv); this._relook(o, { item: String(tv) }); } break;
          case 'text': nd.text = String(tv); break;
          case 'color': { const c = hex(String(tv)); nd.color = cur.color ? F.mix(nd.color, c, k) : c; cur.color = 1; break; }
          case 'x': { const px = this.P(num, 0).x; o.x0 = cur.x === undefined ? px : o.x0 + (px - o.x0)*k; cur.x = 1; if (!o.moving) nd.x = o.x0; break; }
          case 'y': { const py = this.P(0, num).y; o.y0 = cur.y === undefined ? py : o.y0 + (py - o.y0)*k; cur.y = 1; if (!o.moving) nd.y = o.y0; break; }
          case 'w': { const v = num*U; nd.setSize(cur.w === undefined ? v : nd.w + (v - nd.w)*k, nd.h); cur.w = 1; break; }
          case 'h': { const v = num*U; nd.setSize(nd.w, cur.h === undefined ? v : nd.h + (v - nd.h)*k); cur.h = 1; break; }
          case 'scale': nd.scale = cur.scale === undefined ? num : nd.scale + (num - nd.scale)*k; cur.scale = 1; break;
          case 'rot': nd.rot = cur.rot === undefined ? num : nd.rot + (num - nd.rot)*k; cur.rot = 1; break;
          case 'alpha': nd.alpha = cur.alpha === undefined ? num : nd.alpha + (num - nd.alpha)*k; cur.alpha = 1; break;
        }
      });
    }
    const an = sp.anim;
    if (an && o.animOn && !o.moving){
      const amp = (+an.amp || .012)*U, sp2 = +an.speed || 3, s = Math.sin(time*sp2 + o.t0);
      if (an.type === 'pulse') nd.scale = 1 + (+an.amp || .06)*(.5+.5*s); else nd.y = o.y0 + s*amp;
    }
  });
};
P._relook = function(o, look){
  const old = o.node, parent = old.parent || this.root, w = old.w, h = old.h, x = old.x, y = old.y;
  const n = this.drawLook(parent, look, x, y, w, h, o.extra); n.rot = old.rot; o.node = n; old.destroy();
  if (o.spec.show === undefined) n.active = true;
};
P.objById = function(id){ return (this.objs.concat(this.taskObjs)).find(o=> o.id === id && o.node && !o.node.dead) || null; };
P.objByRole = function(role){ return (this.objs.concat(this.taskObjs)).find(o=> o.role === role && o.node && !o.node.dead) || null; };

// ── đích của hành động ──
P.nodesOf = function(spec, cx){
  if (spec == null) return [];
  if (typeof spec === 'object' && spec.fx !== undefined) return [];
  let s = X.val(spec, this.scope(cx));
  if (typeof s !== 'string') return [];
  if (s === '@tapped') return cx && cx.tapped ? [cx.tapped.node] : [];
  if (s === '@target') return (this.elems||[]).filter(e=> e.key === (this._Q||{}).target).map(e=> e.node);
  if (s === '@actor'){ const o = this.objByRole('actor'); return o ? [o.node] : []; }
  if (s.charAt(0) === '#'){ const o = this.objById(s.slice(1)); return o ? [o.node] : []; }
  const o = this.objById(s); return o ? [o.node] : [];
};
P.pointOf = function(spec, cx){
  if (spec && typeof spec === 'object' && spec.ref !== undefined){ const b = this.pointOf(spec.ref, cx); return { x: b.x + (+this.ev(spec.dx||0, cx))*this.U, y: b.y + (+this.ev(spec.dy||0, cx))*this.U }; }
  if (spec && typeof spec === 'object' && spec.fx !== undefined){ const p = this.P(+this.ev(spec.fx, cx), +this.ev(spec.fy, cx)); return { x: p.x + (+this.ev(spec.dx||0, cx))*this.U, y: p.y + (+this.ev(spec.dy||0, cx))*this.U }; }
  const ns = this.nodesOf(spec, cx); if (ns.length){ const o = this._objOf(ns[0]); return { x: ns[0].x, y: ns[0].y }; }
  if (cx && cx.tap && (spec === '@tap')) return cx.tap;
  return { x:0, y:0 };
};
P._objOf = function(node){ return (this.objs.concat(this.taskObjs)).find(o=> o.node === node) || null; };
P._moveObj = function*(node, to, sec){
  const o = this._objOf(node);
  if (o){ o.moving = true; }
  yield* tw.moveTo(node, to, sec);
  if (o){ o.x0 = to.x; o.y0 = to.y; o.moving = false; }
};

P.runActs = function*(list, cx){
  cx = cx || {};
  for (const a of (list || [])){
    const n = k=> this.num(a[k], undefined, cx), nz = (k, d)=> (a[k] === undefined ? d : this.num(a[k], d, cx));
    switch (a.do){
      case 'sfx': this.sfx(String(this.ev(a.k, cx) || 'tap')); break;
      case 'std': if (a.ev === 'wrong') yield* this._stdWrong(cx); else yield* this._stdRight(cx); break;
      case 'say': this.say(X.template(a.text, (this._Q||{}).vars, this.scope(cx)), this.P(.5, nz('fy', .14)), hex(this.ev(a.color, cx) || '#FFFFFF'), nz('sec', 1.6)); break;
      case 'confetti': { const p = a.at ? this.pointOf(a.at, cx) : { x:0, y:0 }; this.confetti(p.x, p.y, nz('n', 12), a.sound !== 0); break; }
      case 'bounce': this.nodesOf(a.on, cx).forEach(nd=> this.run(tw.bounce(nd, nz('amount', .25), nz('sec', .5)))); break;
      case 'shake': this.nodesOf(a.on, cx).forEach(nd=> this.run(tw.shake(nd, nz('sec', .35), nz('amp', 10)))); break;
      case 'pulse': this.nodesOf(a.on, cx).forEach(nd=> this.run(tw.pulse(nd, ()=> true, .12, 7))); break;
      case 'move': { const to = this.pointOf(a.to, cx), sec = nz('sec', .5);
        const ns = this.nodesOf(a.on, cx); const gens = ns.map(nd=> this._moveObj(nd, to, sec));
        if (a.wait === 0) gens.forEach(g=> this.run(g)); else { gens.slice(1).forEach(g=> this.run(g)); if (gens[0]) yield* gens[0]; } break; }
      case 'scale': { const g = this.nodesOf(a.on, cx).map(nd=> tw.scaleTo(nd, nz('to', 1), nz('sec', .4), !!a.overshoot)); if (a.wait === 0) g.forEach(x=> this.run(x)); else { g.slice(1).forEach(x=> this.run(x)); if (g[0]) yield* g[0]; } break; }
      case 'fade': { const g = this.nodesOf(a.on, cx).map(nd=> tw.fade(nd, nz('to', 0), nz('sec', .3))); if (a.wait === 0) g.forEach(x=> this.run(x)); else { g.slice(1).forEach(x=> this.run(x)); if (g[0]) yield* g[0]; } break; }
      case 'tint': { const c = hex(String(this.ev(a.color, cx))); const g = this.nodesOf(a.on, cx).map(nd=> tw.colorTo(nd, c, nz('sec', .3))); if (a.wait === 0) g.forEach(x=> this.run(x)); else { g.slice(1).forEach(x=> this.run(x)); if (g[0]) yield* g[0]; } break; }
      case 'rotate': { const g = this.nodesOf(a.on, cx).map(nd=> rotTo(nd, nz('to', 0), nz('sec', .5))); if (a.wait === 0) g.forEach(x=> this.run(x)); else { g.slice(1).forEach(x=> this.run(x)); if (g[0]) yield* g[0]; } break; }
      case 'show': this.nodesOf(a.on, cx).forEach(nd=> nd.active = true); break;
      case 'hide': this.nodesOf(a.on, cx).forEach(nd=> nd.active = false); break;
      case 'pop': this.nodesOf(a.on, cx).forEach(nd=>{ nd.active = true; this.run(tw.popIn(nd, nz('sec', .3))); }); break;
      case 'destroy': this.nodesOf(a.on, cx).forEach(nd=> nd.destroy()); break;
      case 'spawn': { const p = a.at ? this.pointOf(a.at, cx) : { x:0, y:0 }, s = nz('size', .2)*this.U, sw = a.w !== undefined ? nz('w', .2)*this.U : s, sh = a.h !== undefined ? nz('h', .2)*this.U : s;
        const o = { spec:{}, extra:{}, id: a.id ? String(this.ev(a.id, cx)) : null, role: a.role || null, cur:{}, animOn:true, t0:0 };
        o.node = this.drawLook(this.root, a.look, p.x, p.y, sw, sh, cx); o.x0 = o.node.x; o.y0 = o.node.y; this.taskObjs.push(o);
        if (a.pop !== 0) this.run(tw.popIn(o.node, .3)); break; }
      case 'set': this.vars[String(this.ev(a.var, cx))] = this.ev(a.expr, cx); break;
      case 'inc': { const vn = String(this.ev(a.var, cx)); let v = X.toNum(this.vars[vn]) + nz('by', 1); if (a.mod !== undefined){ const m = this.num(a.mod, 0, cx); if (m > 0) v = ((v % m) + m) % m; } this.vars[vn] = v; break; }
      case 'bag': { const vals = String(this.ev(a.values, cx)).split(',').map(s=> s.trim()); const key = a.var; const bag = this._bags[key] = this._bags[key] && this._bags[key].length ? this._bags[key] : this.shuffle(vals.slice());
        this.vars[key] = bag.shift(); break; }
      case 'pickItem': this.vars[a.var] = this.ev('=randItem(' + JSON.stringify(a.tag) + ',' + JSON.stringify(a.value) + ')', cx) || ''; break;
      case 'if': yield* this.runActs(X.truthy(this.ev('=' + String(a.cond), cx)) ? a.then : a.else, cx); break;
      case 'wait': yield nz('sec', .3); break;
      case 'text': this.nodesOf(a.on, cx).forEach(nd=> nd.text = X.template(a.text, (this._Q||{}).vars, this.scope(cx))); break;
      case 'par': { const hs = (a.acts || []).map(l=> { const st = { done:false }; this.run((function*(self, l2){ yield* self.runActs(Array.isArray(l2) ? l2 : [l2], cx); st.done = true; })(this, l)); return st; }); while (hs.some(h=> !h.done)) yield null; break; }
      case 'fly': { const from = cx.tapped ? { x: cx.tapped.node.x, y: cx.tapped.node.y } : { x:0, y:0 }, to = this.pointOf(a.to, cx), sz = this.nodesOf(a.to, cx)[0];
        const s0 = cx.tapped ? Math.min(cx.tapped.node.w, cx.tapped.node.h)*.8 : this.U*.2;
        const look = a.look || { item: '=tapped.item' }; const o = { spec:{}, extra:{}, id:null, role:null, cur:{}, animOn:false, t0:0 };
        o.node = this.drawLook(this.root, look, from.x, from.y, s0, s0, Object.assign({}, cx, { tapped: Object.assign({}, cx.tapped, { item: cx.tapped && cx.tapped.itemId }) }));
        o.node.setAsLast(); this.taskObjs.push(o);
        const sx = sz ? sz.w : s0, sy = sz ? sz.h : s0;
        yield* tw.moveResize(o.node, to, { x: sx, y: sy }, nz('sec', .5)); o.node.destroy(); break; }
    }
  }
};
P._stdRight = function*(cx){
  const Q = this._Q || {}, T2 = (this.flow && this.flow.text) || {}; this.right();
  (this.elems||[]).filter(e=> e.key === Q.target).forEach(e=>{ if (e.kind === 'card') e.node.color = hex('#C8F7C5'); this.run(tw.bounce(e.fig || e.node, .3, .6)); this.confetti(e.node.x, e.node.y, 10, false); });
  if (T2.say) this.say(X.template(T2.say, Q.vars, this.scope(cx)), this.P(.5, .14), hex('#1B5E20'), 1.6);
};
P._stdWrong = function*(cx){
  const T2 = (this.flow && this.flow.text) || {}, el = cx.tapped; this.sfx('tap'); if (el) this.run(tw.shake(el.node, .35, 10));
  if (T2.wrong) this.say(X.template(T2.wrong, Object.assign({}, (this._Q||{}).vars, { name: el ? el.name : '', NAME: el ? el.name : '' }), this.scope(cx)), this.P(.5, .14), hex('#BF360C'), 1.3);
};
})();
