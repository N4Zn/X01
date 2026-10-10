/* FloorStory web engine — phần lõi (port của StoryUI/ShapeSprites/StoryWorld/FloorStoryController bên Unity).
   Nguồn thật: WebTools/GenericGameBuilder/fs/*.js → nhúng vào game_builder_v2.html bằng Tools/floorstory_pack/inline_web.py.
   Toạ độ: gốc ở GIỮA phần tử cha, y hướng LÊN (giống RectTransform). Mỗi "thế giới" của 1 đội = 512×500. */
(function(){
'use strict';
const FSW = window.FSW = window.FSW || {};

// ── tiện ích ─────────────────────────────────────────────────────────────────
const clamp = (v,a,b)=> Math.max(a, Math.min(b, v));
const lerp = (a,b,t)=> a + (b-a)*t;
const smooth = t=> t*t*(3-2*t);
const outBack = t=>{ const c1 = 1.70158, c3 = c1 + 1; return 1 + c3*Math.pow(t-1,3) + c1*Math.pow(t-1,2); };
function hex(h){
  h = String(h||'#ff00ff').replace('#','');
  if (h.length === 3) h = h.split('').map(c=>c+c).join('');
  const n = parseInt(h.slice(0,6), 16);
  return { r:(n>>16)&255, g:(n>>8)&255, b:n&255, a: h.length >= 8 ? parseInt(h.slice(6,8),16)/255 : 1 };
}
const col = (r,g,b,a)=> ({ r, g, b, a: a==null ? 1 : a });
const WHITE = col(255,255,255,1);
const rgba = c=> `rgba(${Math.round(c.r)},${Math.round(c.g)},${Math.round(c.b)},${c.a})`;
const mix = (a,b,t)=> col(lerp(a.r,b.r,t), lerp(a.g,b.g,t), lerp(a.b,b.b,t), lerp(a.a,b.a,t));

/** Random có seed (mulberry32) — cùng seed ở 2 đội → cùng câu hỏi (chế độ chung). */
class Rng {
  constructor(seed){ this.s = (seed>>>0) || 1; }
  next(){ let t = this.s += 0x6D2B79F5; t = Math.imul(t ^ t>>>15, t|1); t ^= t + Math.imul(t ^ t>>>7, t|61); return ((t ^ t>>>14)>>>0) / 4294967296; }
  int(n){ return Math.floor(this.next()*n); }
  range(a,b){ return a + this.int(b-a); }
}

// ── hình học sinh bằng canvas (cùng công thức ShapeSprites.cs) → mask-image ─────
const N = 128;
const STAR = (()=>{ const p=[]; for (let i=0;i<10;i++){ const a=Math.PI*.5+i*Math.PI/5, r=i%2===0?.98:.4; p.push([Math.cos(a)*r, Math.sin(a)*r]); } return p; })();
function inPoly(poly,x,y){ let ins=false; for (let i=0,j=poly.length-1;i<poly.length;j=i++){ if ((poly[i][1]>y)!==(poly[j][1]>y) && x < (poly[j][0]-poly[i][0])*(y-poly[i][1])/(poly[j][1]-poly[i][1])+poly[i][0]) ins=!ins; } return ins; }
const INSIDE = {
  circle:  (x,y)=> x*x+y*y <= .9025,
  diamond: (x,y)=> Math.abs(x)+Math.abs(y) <= .95,
  triangle:(x,y)=>{ if (y < -.85 || y > .9) return false; const t=(.9-y)/1.75; return Math.abs(x) <= t*.95; },
  star:    (x,y)=> inPoly(STAR,x,y),
  heart:   (x,y)=>{ const hx=x*1.3, hy=y*1.3+.1, a=hx*hx+hy*hy-1; return a*a*a - hx*hx*hy*hy*hy <= 0; },
  moon:    (x,y)=>{ const o=x*x+y*y <= .81; const cx=x-.42, cy=y-.18; return o && !(cx*cx+cy*cy <= .6084); },
  flower:  (x,y)=>{ if (x*x+y*y <= .0729) return true; for (let i=0;i<5;i++){ const a=Math.PI*.5+i*Math.PI*2/5, dx=x-Math.cos(a)*.58, dy=y-Math.sin(a)*.58; if (dx*dx+dy*dy <= .1296) return true; } return false; },
};
const maskCache = {};
function shapeURL(name){
  if (maskCache[name]) return maskCache[name];
  const f = INSIDE[name]; if (!f) return null;
  const c = document.createElement('canvas'); c.width = c.height = N;
  const ctx = c.getContext('2d'), im = ctx.createImageData(N,N);
  for (let j=0;j<N;j++) for (let i=0;i<N;i++){
    let acc = 0;
    for (let sy=0;sy<3;sy++) for (let sx=0;sx<3;sx++){
      const x = ((i+(sx+.5)/3)/N)*2-1, y = 1-((j+(sy+.5)/3)/N)*2;   // y lên trên
      if (f(x,y)) acc++;
    }
    const k = (j*N+i)*4; im.data[k]=im.data[k+1]=im.data[k+2]=255; im.data[k+3]=Math.round(acc*255/9);
  }
  ctx.putImageData(im,0,0);
  return (maskCache[name] = c.toDataURL('image/png'));
}

// ── Node = RectTransform + Graphic ───────────────────────────────────────────
let cssDone = false;
function injectCss(){
  if (cssDone) return; cssDone = true;
  const s = document.createElement('style');
  s.textContent = `
.fs-stage-wrap{position:relative; width:100%; height:100%; display:flex; align-items:center; justify-content:center; overflow:hidden; background:#0b0c14;}
.fs-stage{position:absolute; left:0; top:0; width:1024px; height:540px; transform-origin:0 0; background:#111; font-family:'Baloo 2','Quicksand','Inter',system-ui,sans-serif; user-select:none; -webkit-user-select:none; touch-action:manipulation;}
.fs-n{position:absolute; left:50%; top:50%; box-sizing:border-box; pointer-events:none; transform-origin:50% 50%;}
.fs-fill{left:0; top:0; width:100%; height:100%; transform:none;}
.fs-t{display:flex; align-items:center; justify-content:center; text-align:center; font-weight:800; line-height:1.05; overflow:visible; word-break:break-word;}
.fs-hud{position:absolute; left:0; top:0; width:1024px; height:40px; display:flex; align-items:center; justify-content:space-between; padding:0 16px; color:#fff; font-weight:800; font-size:20px; background:#1b1d2b; z-index:3;}
.fs-hud .fs-ttl{font-size:15px; opacity:.8;}
.fs-div{position:absolute; left:511px; top:40px; width:2px; height:500px; background:#000; z-index:2;}
`;
  document.head.appendChild(s);
}

class Node {
  constructor(parent, kind, x, y, w, h){
    this.kind = kind; this.parent = parent && parent.el ? parent : null; this.kids = []; this.dead = false;
    const el = this.el = document.createElement('div');
    el.className = 'fs-n' + (kind === 'text' ? ' fs-t' : '');
    this._x = x||0; this._y = y||0; this._w = w||0; this._h = h||0; this._sx = 1; this._sy = 1; this._rot = 0;
    this._color = WHITE; this._sprite = null; this._pa = true; this._sliced = false; this._fill = false; this._tap = null;
    if (this.parent){ this.parent.kids.push(this); this.parent.el.appendChild(el); }
    else if (parent && parent.appendChild) parent.appendChild(el);
    this._layout();
  }
  get x(){ return this._x; } set x(v){ this._x = v; this._layout(); }
  get y(){ return this._y; } set y(v){ this._y = v; this._layout(); }
  get w(){ return this._w; } set w(v){ this._w = v; this._layout(); }
  get h(){ return this._h; } set h(v){ this._h = v; this._layout(); }
  get sx(){ return this._sx; } set sx(v){ this._sx = v; this._layout(); }
  get sy(){ return this._sy; } set sy(v){ this._sy = v; this._layout(); }
  get scale(){ return this._sx; } set scale(v){ this._sx = this._sy = v; this._layout(); }
  get rot(){ return this._rot; } set rot(v){ this._rot = v; this._layout(); }
  setPos(x,y){ this._x = x; this._y = y; this._layout(); }
  setSize(w,h){ this._w = w; this._h = h; this._layout(); this._paint(); }
  _layout(){
    const st = this.el.style;
    if (this._fill) return;
    st.width = this._w + 'px'; st.height = this._h + 'px';
    st.transform = `translate(${this._x - this._w/2}px,${-this._y - this._h/2}px) rotate(${-this._rot}deg) scale(${this._sx},${this._sy})`;
  }
  get color(){ return this._color; } set color(c){ this._color = c; this._paint(); }
  get alpha(){ return this._color.a; } set alpha(a){ this._color = col(this._color.r, this._color.g, this._color.b, a); this._paint(); }
  get sprite(){ return this._sprite; } set sprite(s){ this._sprite = s; this._paint(); }
  get preserveAspect(){ return this._pa; } set preserveAspect(v){ this._pa = v; this._paint(); }
  get sliced(){ return this._sliced; } set sliced(v){ this._sliced = v; this._paint(); }
  get active(){ return this.el.style.display !== 'none'; } set active(v){ this.el.style.display = v ? '' : 'none'; }
  _paint(){
    if (this.kind === 'text'){ this.el.style.color = rgba(this._color); return; }
    if (this.kind !== 'img') return;
    const st = this.el.style, s = this._sprite, c = this._color;
    st.opacity = 1; st.backgroundImage = 'none'; st.webkitMaskImage = st.maskImage = 'none';
    st.borderRadius = '0'; st.backgroundColor = 'transparent';
    const size = this._pa ? 'contain' : '100% 100%';
    if (s && typeof s === 'object' && s.url){
      st.backgroundImage = `url("${s.url}")`; st.backgroundSize = size; st.backgroundRepeat = 'no-repeat'; st.backgroundPosition = 'center';
      st.opacity = c.a;
    } else if (s && typeof s === 'object' && s.svg){
      st.backgroundImage = `url("${s.svg}")`; st.backgroundSize = size; st.backgroundRepeat = 'no-repeat'; st.backgroundPosition = 'center';
      st.opacity = c.a;
    } else {
      st.backgroundColor = rgba(c);
      const name = typeof s === 'string' ? s : null;
      if (name === 'rrect'){ st.borderRadius = Math.min(22, Math.min(this._w, this._h)/2) + 'px'; }
      else if (name && name !== 'square' && shapeURL(name)){
        const u = `url("${shapeURL(name)}")`;
        st.webkitMaskImage = st.maskImage = u;
        st.webkitMaskSize = st.maskSize = size; st.webkitMaskRepeat = st.maskRepeat = 'no-repeat'; st.webkitMaskPosition = st.maskPosition = 'center';
      }
    }
  }
  // chữ
  get text(){ return this.el.textContent; }
  set text(t){ this.el.textContent = t; this.fit(); }
  fit(){
    if (this.kind !== 'text') return;
    const el = this.el; let fs = this._fs || 30; el.style.fontSize = fs + 'px';
    const min = Math.max(10, (this._fs||30)/2);
    let guard = 40;
    while (guard-- > 0 && fs > min && (el.scrollHeight > this._h + 2 || el.scrollWidth > this._w + 2)){ fs -= 2; el.style.fontSize = fs + 'px'; }
  }
  setAsLast(){ if (this.parent) this.parent.el.appendChild(this.el); else if (this.el.parentNode) this.el.parentNode.appendChild(this.el); }
  destroy(){
    if (this.dead) return;
    this.dead = true;
    if (this.el.parentNode) this.el.parentNode.removeChild(this.el);
    if (this.parent){ const i = this.parent.kids.indexOf(this); if (i >= 0) this.parent.kids.splice(i,1); }
    const mark = n=>{ n.dead = true; n.kids.forEach(mark); };
    this.kids.slice().forEach(mark);
  }
}

// ── UI helpers (StoryUI) ────────────────────────────────────────────────────
const UI = {
  rect(parent, x,y,w,h){ return new Node(parent, 'rect', x,y,w,h); },
  pic(parent, sprite, color, x,y,w,h, raycast){
    const n = new Node(parent, 'img', x,y,w,h); n._sprite = sprite; n._color = color || WHITE; n._paint();
    if (raycast) n.el.style.pointerEvents = 'auto';
    return n;
  },
  fill(parent, color, sprite){
    const n = new Node(parent, 'img', 0,0,0,0); n._fill = true; n.el.classList.add('fs-fill'); n.el.style.width = '100%'; n.el.style.height = '100%'; n.el.style.transform = 'none';
    n._sprite = sprite || null; n._color = color || WHITE; n._paint(); return n;
  },
  label(parent, text, fontSize, color, x,y,w,h, outline){
    const n = new Node(parent, 'text', x,y,w,h); n._fs = fontSize; n._color = color || WHITE;
    n.el.style.color = rgba(n._color);
    if (outline !== false) n.el.style.textShadow = '2px 2px 0 rgba(0,0,0,.85), -2px 2px 0 rgba(0,0,0,.85), 2px -2px 0 rgba(0,0,0,.85), -2px -2px 0 rgba(0,0,0,.85)';
    n.text = text;
    return n;
  },
  onTap(node, cb){
    node._tap = cb;
    if (!node._tapBound){
      node._tapBound = true;
      node.el.style.pointerEvents = 'auto';
      node.el.addEventListener('pointerdown', e=>{ e.preventDefault(); e.stopPropagation(); if (node._tap && !node.dead) node._tap(e); });
    }
  },
  /** Vùng chạm vô hình phủ kín; trả về toạ độ local (gốc giữa, y lên). */
  tapArea(parent, cb){
    const n = UI.fill(parent, col(255,255,255,0));
    n.el.style.pointerEvents = 'auto';
    n.el.addEventListener('pointerdown', e=>{
      const r = n.el.getBoundingClientRect(), k = r.width / (parent._w || 512);
      cb({ x: (e.clientX - r.left) / k - (parent._w||512)/2, y: -((e.clientY - r.top) / k - (parent._h||500)/2) });
    });
    return n;
  },
  /** Thẻ bấm: nền bo góc + (ảnh) + (chữ). `sprite` null → chỉ chữ. */
  card(parent, sprite, label, bg, x,y,w,h, onTap, fontSize){
    fontSize = fontSize || 30;
    const c = { basePos:{x,y} };
    const b = UI.pic(parent, 'rrect', bg, x,y,w,h, true); b.sliced = true; b.preserveAspect = false; b._paint();
    c.rt = c.bg = b; c.hasIcon = !!sprite;
    if (sprite){
      const lh = label ? h*.24 : 0;
      c.icon = UI.pic(b, sprite, WHITE, 0, lh*.5, w*.86, h*.9 - lh);
    }
    if (label){
      c.label = sprite ? UI.label(b, label, Math.max(18, fontSize-8), WHITE, 0, -h*.38, w*.96, h*.26)
                       : UI.label(b, label, fontSize, WHITE, 0, 0, w*.92, h*.86);
    }
    if (onTap) UI.onTap(b, onTap);
    return c;
  },
};

// ── Tween (generator): `yield* tween(...)` ───────────────────────────────────
const T = { dt: 0.016 };
function* moveTo(n, to, dur){
  if (!n || n.dead) return; const f = { x:n.x, y:n.y };
  for (let t=0; t<dur; t+=T.dt){ if (n.dead) return; const k = smooth(t/dur); n.setPos(lerp(f.x,to.x,k), lerp(f.y,to.y,k)); yield null; }
  if (!n.dead) n.setPos(to.x,to.y);
}
function* moveResize(n, to, toSize, dur){
  if (!n || n.dead) return; const f = { x:n.x, y:n.y, w:n.w, h:n.h };
  for (let t=0; t<dur; t+=T.dt){ if (n.dead) return; const k = smooth(t/dur); n._x = lerp(f.x,to.x,k); n._y = lerp(f.y,to.y,k); n._w = lerp(f.w,toSize.x,k); n._h = lerp(f.h,toSize.y,k); n._layout(); yield null; }
  if (!n.dead){ n._x = to.x; n._y = to.y; n._w = toSize.x; n._h = toSize.y; n._layout(); }
}
function* scaleTo(n, to, dur, overshoot){
  if (!n || n.dead) return; const f = n.scale;
  for (let t=0; t<dur; t+=T.dt){ if (n.dead) return; const k = t/dur; n.scale = lerp(f, to, overshoot ? outBack(k) : smooth(k)); yield null; }
  if (!n.dead) n.scale = to;
}
function* popIn(n, dur){ if (!n || n.dead) return; n.scale = 0; yield* scaleTo(n, 1, dur == null ? .35 : dur, true); }
function* shake(n, dur, amp){
  dur = dur == null ? .4 : dur; amp = amp == null ? 14 : amp;
  if (!n || n.dead) return; const o = { x:n.x, y:n.y };
  for (let t=0; t<dur; t+=T.dt){ if (n.dead) return; n.setPos(o.x + Math.sin(t*50)*amp*(1-t/dur), o.y); yield null; }
  if (!n.dead) n.setPos(o.x,o.y);
}
function* bounce(n, amount, dur){
  amount = amount == null ? .25 : amount; dur = dur == null ? .5 : dur;
  if (!n || n.dead) return; const b = { x:n.sx, y:n.sy };
  for (let t=0; t<dur; t+=T.dt){ if (n.dead) return; const k = 1 + amount*Math.sin(t/dur*Math.PI); n._sx = b.x*k; n._sy = b.y*k; n._layout(); yield null; }
  if (!n.dead){ n._sx = b.x; n._sy = b.y; n._layout(); }
}
function* fade(n, to, dur){
  if (!n || n.dead) return; const f = n.alpha;
  for (let t=0; t<dur; t+=T.dt){ if (n.dead) return; n.alpha = lerp(f,to,t/dur); yield null; }
  if (!n.dead) n.alpha = to;
}
function* colorTo(n, to, dur){
  if (!n || n.dead) return; const f = n.color;
  for (let t=0; t<dur; t+=T.dt){ if (n.dead) return; n.color = mix(f,to,smooth(t/dur)); yield null; }
  if (!n.dead) n.color = to;
}
function* pulse(n, alive, amount, speed){
  amount = amount == null ? .12 : amount; speed = speed == null ? 6 : speed;
  if (!n || n.dead) return; const b = { x:n.sx, y:n.sy }; let t = 0;
  while (!n.dead && alive()){ t += T.dt*speed; const k = 1 + amount*(.5+.5*Math.sin(t)); n._sx = b.x*k; n._sy = b.y*k; n._layout(); yield null; }
  if (!n.dead){ n._sx = b.x; n._sy = b.y; n._layout(); }
}

// ── Âm thanh (bíp WebAudio; không có giọng thật — Unity dùng StoryVoice/*.mp3) ──
let ac = null;
function tone(f, d, type, v, delay){
  try{
    const AC = window.AudioContext || window.webkitAudioContext; if (!AC) return;
    ac = ac || new AC(); const t0 = ac.currentTime + (delay||0);
    const o = ac.createOscillator(), g = ac.createGain();
    o.type = type || 'sine'; o.frequency.value = f; g.gain.setValueAtTime(v == null ? .12 : v, t0); g.gain.exponentialRampToValueAtTime(.0001, t0 + d);
    o.connect(g); g.connect(ac.destination); o.start(t0); o.stop(t0 + d + .02);
  }catch(e){}
}
const SFX = {
  right(){ tone(523,.12,'triangle'); tone(659,.12,'triangle',.12,.1); tone(784,.2,'triangle',.12,.2); },
  wrong(){ tone(180,.25,'sawtooth',.08); },
  tap(){ tone(380,.06,'square',.05); }, pop(){ tone(620,.07,'sine'); }, plop(){ tone(300,.1,'sine'); },
  wood(){ tone(160,.15,'triangle',.15); }, powerup(){ [523,659,784,1047].forEach((f,i)=> tone(f,.12,'triangle',.1,i*.07)); },
  star(){ tone(880,.15,'sine'); }, up(){ tone(440,.1,'sine'); tone(660,.12,'sine',.1,.08); }, bell(){ tone(1046,.3,'sine'); },
};

// ── Icon ghép từ hình (port StoryIcons.cs) ───────────────────────────────────
const ICON_KEYS = ['sun','moon','flower','star','heart','cake','icecream','cup'];
const ICON_NAMES = { sun:'Mặt trời', moon:'Mặt trăng', flower:'Bông hoa', star:'Ngôi sao', heart:'Trái tim', cake:'Cái bánh', icecream:'Cây kem', cup:'Cốc sữa' };
function iconParts(key){
  const P = (sh,c,x,y,w,h,rot,free)=> ({ sh, c, x, y, w, h, rot: rot||0, free: !!free });
  switch (key){
    case 'sun': { const a=[]; for (let i=0;i<8;i++){ const an=i*Math.PI/4; a.push(P('rrect','#FFB300',Math.cos(an)*.41,Math.sin(an)*.41,.11,.24,an*180/Math.PI-90,true)); }
      a.push(P('circle','#FFD23F',0,0,.58,.58), P('circle','#7A4B00',-.08,.04,.05,.05), P('circle','#7A4B00',.08,.04,.05,.05)); return a; }
    case 'moon': return [P('moon','#FFE066',0,0,.92,.92), P('star','#FFF3B0',.28,.26,.16,.16)];
    case 'flower': return [P('flower','#FF6FB5',0,0,.92,.92), P('circle','#FFD23F',0,0,.26,.26)];
    case 'star': return [P('star','#FFC107',0,0,.92,.92)];
    case 'heart': return [P('heart','#F44336',0,0,.88,.88)];
    case 'cake': return [P('rrect','#E0E0E0',0,-.38,.98,.1,0,true), P('rrect','#F7A1C4',0,-.17,.84,.36,0,true), P('rrect','#FFF3E0',0,.10,.64,.28,0,true),
                         P('square','#4DA6FF',0,.31,.07,.18,0,true), P('circle','#FF8F00',0,.44,.1,.14)];
    case 'icecream': return [P('triangle','#E0A458',0,-.22,.46,.6,180), P('circle','#FF8FB3',0,.08,.54,.54), P('circle','#8D5A3B',0,.30,.38,.38), P('circle','#E53935',.02,.47,.1,.1)];
    case 'cup': return [P('rrect','#5DADE2',-.04,-.04,.56,.64,0,true), P('square','#FFFFFF',-.04,0,.56,.1,0,true), P('square','#5DADE2',.34,0,.1,.34,0,true),
                        P('square','#5DADE2',.28,.15,.16,.09,0,true), P('square','#5DADE2',.28,-.15,.16,.09,0,true), P('circle','#FFFFFF',-.04,.30,.48,.1,0,true)];
    default: return [P('circle','#CCCCCC',0,0,.8,.8)];
  }
}
function buildIcon(parent, key, x, y, s){
  const root = UI.rect(parent, x, y, s, s);
  iconParts(key).forEach(p=>{
    const n = UI.pic(root, p.sh, hex(p.c), p.x*s, p.y*s, p.w*s, p.h*s);
    if (p.sh === 'rrect'){ n.sliced = true; n.preserveAspect = false; n._paint(); }
    else if (p.free || p.sh === 'square'){ n.preserveAspect = false; n._paint(); }
    if (p.rot) n.rot = p.rot;
  });
  return root;
}

// ── Khối hộp 3D đẳng cự (SVG) ───────────────────────────────────────────────
const cubeCache = {};
function cubeSprite(color){
  const key = rgba(color); if (cubeCache[key]) return cubeCache[key];
  const sh = k=> rgba(col(clamp(color.r*k,0,255), clamp(color.g*k,0,255), clamp(color.b*k,0,255), 1));
  const svg = `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'>`
    + `<polygon points='50,0 100,25 50,50 0,25' fill='${sh(1.12)}' stroke='rgba(0,0,0,.3)' stroke-width='1'/>`
    + `<polygon points='0,25 50,50 50,100 0,75' fill='${sh(.86)}' stroke='rgba(0,0,0,.3)' stroke-width='1'/>`
    + `<polygon points='50,50 100,25 100,75 50,100' fill='${sh(.68)}' stroke='rgba(0,0,0,.3)' stroke-width='1'/></svg>`;
  return (cubeCache[key] = { svg: 'data:image/svg+xml;utf8,' + encodeURIComponent(svg) });
}

Object.assign(FSW, { clamp, lerp, smooth, outBack, hex, col, WHITE, rgba, mix, Rng, Node, UI, T, SFX, ICON_KEYS, ICON_NAMES, buildIcon, cubeSprite, injectCss,
  tw: { moveTo, moveResize, scaleTo, popIn, shake, bounce, fade, colorTo, pulse } });
})();
