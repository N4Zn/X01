/* FloorStory — TRÌNH SOẠN SÂN KHẤU (chỉ web; Unity không cần, không có bản C#).
   Dựng 1 thế giới (1 đội, 512×500) bằng CHÍNH engine với 1 câu hỏi mẫu cố định (rng theo seed), hiện sẵn trạng thái cuối của mọi view,
   rồi phủ lớp tương tác: chọn / kéo / đổi cỡ đối tượng như PowerPoint.
   Ghi dữ liệu:  • đối tượng cảnh (data.scene[i] / flow.objects[i])  → spec.x, spec.y (0..1 của world), spec.w, spec.h (đơn vị U)
                 • phần tử do view tự bố trí (thẻ, vật, ô…)           → flow.views[v].p.ov[chỉ số] = [fx, fy]   (engine áp sau khi tính bố cục)
                 • Shift+kéo / chế độ "cả nhóm"                       → đổi tham số của view (x,y / x0..y1 / cx,cy / pos / slotU …)
   API:  const ed = FSW.edit(host, data, { imgUrl, beforeChange(), onChange(info), onSelect(handle|null), onRefresh(handles) });
         ed.refresh() · ed.reroll() · ed.select(id) · ed.setFlow(i) · ed.group = bool · ed.handles · ed.selected · ed.destroy() */
(function(){
'use strict';
const F = window.FSW, { UI, Node, Rng } = F;
const W = 512, H = 500, U = 500, SW = 1024, SH = 540, OY = 40, R3 = v=> Math.round(v*1000)/1000;
let cssOk = false;
function css(){
  if (cssOk) return; cssOk = true;
  const s = document.createElement('style');
  s.textContent = `
.fse-wrap{position:relative; width:100%; height:100%; overflow:hidden; background:#0b0c14;}
.fse-stage{position:absolute; left:0; top:0; width:${SW}px; height:${SH}px; transform-origin:0 0; background:#111; overflow:hidden; font-family:'Baloo 2','Quicksand','Inter',system-ui,sans-serif; user-select:none; -webkit-user-select:none;}
.fse-ov{position:absolute; left:0; top:${OY}px; width:${W}px; height:${H}px; z-index:60; cursor:default; touch-action:none;}
.fse-box{position:absolute; box-sizing:border-box; pointer-events:none; border:1.5px dashed rgba(77,150,255,.0);}
.fse-box.hov{border-color:rgba(77,150,255,.75);}
.fse-box.lock{border-color:rgba(255,160,60,.0);}
.fse-box.lock.hov{border-color:rgba(255,160,60,.85);}
.fse-box.sel{border:2px solid #4D96FF; box-shadow:0 0 0 1px rgba(255,255,255,.55);}
.fse-box.sel.lock{border-color:#FFA03C;}
.fse-grip{position:absolute; right:-7px; bottom:-7px; width:13px; height:13px; background:#fff; border:2px solid #4D96FF; border-radius:3px; pointer-events:auto; cursor:nwse-resize;}
.fse-tag{position:absolute; left:0; top:-19px; font:700 11px/1 system-ui,sans-serif; color:#fff; background:#4D96FF; padding:3px 6px; border-radius:4px; white-space:nowrap; pointer-events:none;}
.fse-box.lock .fse-tag{background:#E8890C;}
.fse-hud{position:absolute; left:0; top:0; width:${SW}px; height:${OY}px; display:flex; align-items:center; justify-content:space-between; padding:0 16px; box-sizing:border-box; color:#fff; font-weight:800; font-size:20px; background:#1b1d2b; z-index:3;} .fse-hud .t{font-size:15px; opacity:.8;} .fse-hud .r{opacity:.55;}
.fse-dv{position:absolute; left:${W-1}px; top:${OY}px; width:2px; height:${H}px; background:#000; z-index:2;}
.fse-gd{position:absolute; background:rgba(255,80,160,.85); pointer-events:none; display:none;}
.fse-hint{position:absolute; left:8px; bottom:6px; font:600 11px system-ui,sans-serif; color:rgba(255,255,255,.75); background:rgba(0,0,0,.45); padding:3px 7px; border-radius:6px; pointer-events:none; z-index:61;}
`;
  document.head.appendChild(s);
}

const lookKind = spec=>{ const l = (spec && spec.look) || {}; return l.text !== undefined ? 'text' : l.image ? 'image' : l.item !== undefined ? 'item' : l.icon ? 'icon' : l.kind === 'hand' ? 'hand' : 'shape'; };
const KIND_NAME = { text:'Chữ', image:'Ảnh', item:'Vật', icon:'Icon', hand:'Kim', shape:'Hình' };
const isNum = v=> v === undefined || v === null || (typeof v === 'number' && isFinite(v));
F.editLookKind = lookKind; F.editKindName = KIND_NAME;

F.edit = function(host, data, opts){
  css(); F.injectCss(); opts = opts || {};
  const ed = { data, handles: [], selected: null, group: false, flowIdx: 0, round: -1, seed: 20261013, world: null };
  let wrap, stage, ov, hudEl, dvEl, guideX, guideY, k = 1, timer = null, selId = null, drag = null, hoverId = null;
  const boxes = new Map();

  const g = {
    data, opts: { mute: true }, sharedSeed: ed.seed, tasks: [],
    run(gen){ const t = { gen, wait: 0, dead: false }; this.tasks.push(t); return t; },
    sfx(){}, playSound(){}, addPoint(){}, imgUrl: n=> opts.imgUrl ? opts.imgUrl(n) : null,
  };
  function tick(){
    const now = performance.now(); let dt = (now - (g._last || now)) / 1000; g._last = now; dt = Math.min(dt, .1); F.T.dt = dt;
    for (let i = g.tasks.length - 1; i >= 0; i--){
      const t = g.tasks[i];
      if (t.dead){ g.tasks.splice(i, 1); continue; }
      if (t.wait > 0){ t.wait -= dt; continue; }
      let r; try{ r = t.gen.next(); }catch(e){ console.error('[FS edit]', e); g.tasks.splice(i, 1); continue; }
      if (r.done){ g.tasks.splice(i, 1); continue; }
      if (typeof r.value === 'number') t.wait = r.value;
    }
    if (ed.world && !(drag && drag.live)){ try{ ed.world.update(dt, now / 1000); }catch(e){} }
    if (ed.world2 && !(drag && drag.live)){ try{ ed.world2.update(dt, now / 1000); }catch(e){} }
  }

  // ── dựng khung ──
  function mount(){
    host.innerHTML = '';
    wrap = document.createElement('div'); wrap.className = 'fse-wrap';
    stage = document.createElement('div'); stage.className = 'fse-stage';
    ov = document.createElement('div'); ov.className = 'fse-ov';
    guideX = document.createElement('div'); guideX.className = 'fse-gd'; guideX.style.cssText += ';width:1px;top:0;height:' + H + 'px;left:' + (W/2) + 'px';
    guideY = document.createElement('div'); guideY.className = 'fse-gd'; guideY.style.cssText += ';height:1px;left:0;width:' + W + 'px;top:' + (H/2) + 'px';
    hudEl = document.createElement('div'); hudEl.className = 'fse-hud'; hudEl.innerHTML = '<span>Đội trái: 0</span><span class="t"></span><span class="r">Đội phải: 0 (bản sao — xem trước)</span>'; hudEl.children[1].textContent = data.title || '';
    dvEl = document.createElement('div'); dvEl.className = 'fse-dv';
    stage.appendChild(hudEl); stage.appendChild(dvEl); stage.appendChild(ov); ov.appendChild(guideX); ov.appendChild(guideY);
    wrap.appendChild(stage); host.appendChild(wrap);
    ov.addEventListener('pointerdown', onDown); ov.addEventListener('pointermove', onMove); ov.addEventListener('pointerup', onUp); ov.addEventListener('pointercancel', onUp);
    ed.fit = ()=>{ const r = wrap.getBoundingClientRect(); k = Math.min(r.width / SW, r.height / SH) || 1; stage.style.transform = `scale(${k})`; stage.style.left = Math.max(0, (r.width - SW*k)/2) + 'px'; stage.style.top = Math.max(0, (r.height - H*k)/2) + 'px'; };
    ed.fit(); if (window.ResizeObserver){ ed._ro = new ResizeObserver(ed.fit); ed._ro.observe(wrap); }
    timer = setInterval(tick, 16);
  }

  // ── dựng thế giới + danh sách handle ──
  function build(){
    if (ed.world){ g.tasks.length = 0; }
    Array.from(stage.children).forEach(c=>{ if (c !== ov && c !== hudEl && c !== dvEl) stage.removeChild(c); });
    const root = new Node(stage, 'rect', 0, 0, W, H); root.el.style.cssText += ';left:0;top:' + OY + 'px;transform:none;overflow:hidden;';
    stage.insertBefore(root.el, ov);
    const root2 = new Node(stage, 'rect', 0, 0, W, H); root2.el.style.cssText += ';left:' + W + 'px;top:' + OY + 'px;transform:none;overflow:hidden;';
    stage.insertBefore(root2.el, ov);
    g.sharedSeed = ed.seed;
    const w = ed.world = new F.FlowWorld(g, 0, root, data);
    w.rng = new Rng(ed.seed); w.editMode = true; w.editFlow = ed.flowIdx; w.editRound = ed.round;
    ed.error = F.validate(data);   // cấu hình chưa đủ (vd thiếu vật) → chỉ hiện cảnh, chưa ra câu hỏi
    if (!ed.error){ try{ w.beginTask({}, ()=>{}); }catch(e){ console.error('[FS edit] beginTask', e); } }
    // đội phải: bản sao chỉ để xem (đúng bố cục lúc chơi 2 đội)
    const w2 = ed.world2 = new F.FlowWorld(g, 1, root2, data);
    w2.rng = new Rng(ed.seed); w2.editMode = true; w2.editFlow = ed.flowIdx; w2.editRound = ed.round;
    if (!ed.error){ try{ w2.beginTask({}, ()=>{}); }catch(e){ console.error('[FS edit] beginTask2', e); } }
    collect();
  }
  function collect(){
    const w = ed.world, H_ = [], flow = w.flow || {};
    const addObj = (o, src, specs, cnt)=>{
      const nd = o.node; if (!nd || nd.dead || nd.parent !== w.root) return;
      const idx = specs.indexOf(o.origin); if (idx < 0) return;
      const sp = o.origin, n = (cnt[idx] = (cnt[idx] || 0) + 1), multi = !!o.multi || n > 1;
      let why = '';
      if (multi) why = 'Đối tượng này sinh tự động (lặp / theo từng vật) — chỉnh trong "Nâng cao".';
      else if (sp.fill) why = 'Nền phủ kín — chỉ đổi được màu / ảnh.';
      else if (!isNum(sp.x) || !isNum(sp.y)) why = 'Vị trí là biểu thức — chỉnh trong "Nâng cao".';
      else if (sp.bind && (sp.bind.x !== undefined || sp.bind.y !== undefined)) why = 'Vị trí do ràng buộc (bind) điều khiển lúc chơi.';
      H_.push({ id: 'o:' + src + ':' + idx + (n > 1 ? '#' + n : ''), dup: n > 1, type: 'obj', src, idx, spec: sp, node: nd, kind: lookKind(sp), locked: !!why, why, canSize: !sp.fill && !multi && isNum(sp.w) && isNum(sp.h), label: (KIND_NAME[lookKind(sp)] || 'Đối tượng') + (multi ? ' ⟳ (lặp)' : '') + (sp.look && sp.look.text !== undefined ? ': ' + String(sp.look.text).slice(0, 14) : sp.look && sp.look.item ? ': ' + sp.look.item : '') });
    };
    const c1 = {}, c2 = {};
    w.objs.forEach(o=> addObj(o, 'scene', data.scene || [], c1));
    w.taskObjs.forEach(o=> { if (o.origin) addObj(o, 'objects', flow.objects || [], c2); });
    (w.V || []).forEach((v, vi)=>{
      const spec = (flow.views || [])[vi]; if (!spec) return;
      const t = spec.type, nm = (F.MOD.view[t] || {}).name || t;
      let nodes = [];
      if (t === 'cards' || t === 'props' || t === 'grid') nodes = (v.elems || []).map(e=> e.node);
      else if (t === 'strip') nodes = (v.bg || []);
      else if (t === 'seesaw') nodes = (v.elems || []).map(e=> e.node);
      const movable = ['cards','props','grid','strip'].indexOf(t) >= 0, sizable = ['cards','props','strip'].indexOf(t) >= 0;
      nodes.forEach((nd, i)=> {
        if (!nd || nd.dead) return;
        H_.push({ id: 'v:' + vi + ':' + i, type: 'view', vi, slot: i, spec, vtype: t, node: nd, kind: 'view', locked: !movable, why: movable ? '' : 'View "' + nm + '" tự bố trí — chỉnh bằng thông số bên phải.', canSize: sizable, label: nm + ' #' + (i + 1) });
      });
    });
    ed.handles = H_;
    drawBoxes();
    if (selId && !H_.some(h=> h.id === selId)) selId = null;
    ed.selected = H_.find(h=> h.id === selId) || null;
    paint();
    if (opts.onRefresh) opts.onRefresh(H_);
  }

  // ── hình học ──
  const ctr = nd=> ({ x: W/2 + nd.x, y: H/2 - nd.y });
  const dims = nd=> { const full = nd._fill; return { w: full ? W : (nd.w || 30) * Math.abs(nd.sx), h: full ? H : (nd.h || 30) * Math.abs(nd.sy) }; };
  function frameOf(h){
    const c = ctr(h.node), d = dims(h.node);
    return { x: c.x, y: c.y, w: d.w, h: d.h, rot: h.node._fill ? 0 : (h.node.rot || 0) };
  }
  function toStage(e){ const r = ov.getBoundingClientRect(); return { x: (e.clientX - r.left) / k, y: (e.clientY - r.top) / k }; }
  function domIndex(nd){ return nd.el.parentNode ? Array.prototype.indexOf.call(nd.el.parentNode.children, nd.el) : -1; }
  function hit(p){
    const list = ed.handles.filter(h=> h.node && !h.node.dead && h.node.active).sort((a, b)=> domIndex(b.node) - domIndex(a.node));
    const test = (h, big)=>{
      const f = frameOf(h), area = f.w * f.h, isBig = area > .5 * W * H;
      if (isBig !== big) return false;
      const a = -f.rot * Math.PI / 180, dx = p.x - f.x, dy = p.y - f.y, c = Math.cos(a), s = Math.sin(a);
      const lx = dx * c + dy * s, ly = -dx * s + dy * c;
      return Math.abs(lx) <= f.w/2 + 3 && Math.abs(ly) <= f.h/2 + 3;
    };
    return list.find(h=> test(h, false)) || list.find(h=> test(h, true)) || null;
  }

  // ── khung chọn ──
  function drawBoxes(){
    boxes.forEach(b=> b.remove()); boxes.clear();
    ed.handles.forEach(h=>{
      const b = document.createElement('div'); b.className = 'fse-box' + (h.locked ? ' lock' : ''); ov.appendChild(b); boxes.set(h.id, b);
    });
  }
  function paint(){
    ed.handles.forEach(h=>{
      const b = boxes.get(h.id); if (!b) return;
      const on = h.id === selId, hv = h.id === hoverId;
      b.classList.toggle('sel', on); b.classList.toggle('hov', hv && !on);
      const show = on || hv;
      b.style.display = show && h.node.active && !h.node.dead ? '' : 'none';
      if (!show) return;
      const f = frameOf(h);
      b.style.left = (f.x - f.w/2) + 'px'; b.style.top = (f.y - f.h/2) + 'px'; b.style.width = f.w + 'px'; b.style.height = f.h + 'px';
      b.style.transform = f.rot ? `rotate(${-f.rot}deg)` : '';
      let tag = b.querySelector('.fse-tag'), grip = b.querySelector('.fse-grip');
      if (on && !tag){ tag = document.createElement('div'); tag.className = 'fse-tag'; b.appendChild(tag); }
      if (tag) tag.textContent = (h.locked ? '🔒 ' : '') + h.label;
      if (on && h.canSize && !h.locked && !grip){ grip = document.createElement('div'); grip.className = 'fse-grip'; grip.dataset.grip = '1'; b.appendChild(grip); }
      if ((!on || !h.canSize || h.locked) && grip) grip.remove();
      if (!on && tag) tag.remove();
    });
  }
  function setSel(id, silent){
    selId = id; ed.selected = ed.handles.find(h=> h.id === id) || null; paint();
    if (!silent && opts.onSelect) opts.onSelect(ed.selected);
  }

  // ── tương tác ──
  function onDown(e){
    if (e.button > 0) return;
    const p = toStage(e), grip = e.target && e.target.dataset && e.target.dataset.grip;
    let h = ed.selected;
    if (!grip){ h = hit(p); setSel(h ? h.id : null); }
    if (!h) return;
    ov.setPointerCapture(e.pointerId);
    const f = frameOf(h);
    drag = { mode: grip ? 'size' : 'move', h, p0: p, f0: f, moved: false, live: false, shift: e.shiftKey || ed.group,
             nodes: null, start: null };
    if (drag.mode === 'move'){
      const sibs = (h.type === 'view' && drag.shift) ? ed.handles.filter(o=> o.type === 'view' && o.vi === h.vi) : [h];
      drag.nodes = sibs.map(o=> ({ o, x: o.node.x, y: o.node.y }));
      drag.groupMove = h.type === 'view' && drag.shift;
      if (h.locked && !drag.groupMove) drag.blocked = true;
    } else { drag.w0 = h.node.w; drag.h0 = h.node.h; }
    e.preventDefault();
  }
  function onMove(e){
    const p = toStage(e);
    if (!drag){ const h = hit(p); const id = h ? h.id : null; if (id !== hoverId){ hoverId = id; paint(); } ov.style.cursor = h ? (h.locked ? 'not-allowed' : 'move') : 'default'; return; }
    const dx = p.x - drag.p0.x, dy = p.y - drag.p0.y;
    if (!drag.moved && Math.hypot(dx, dy) < 3) return;
    drag.moved = true; drag.live = true;
    if (drag.mode === 'move'){
      if (drag.blocked){ return; }
      let ax = dx, ay = dy;
      if (!drag.groupMove){   // bắt dính trục giữa
        const nx = drag.f0.x + dx, ny = drag.f0.y + dy;
        guideX.style.display = Math.abs(nx - W/2) < 5 ? 'block' : 'none'; guideY.style.display = Math.abs(ny - H/2) < 5 ? 'block' : 'none';
        if (Math.abs(nx - W/2) < 5) ax = W/2 - drag.f0.x; if (Math.abs(ny - H/2) < 5) ay = H/2 - drag.f0.y;
      }
      drag.ax = ax; drag.ay = ay;
      drag.nodes.forEach(n=> n.o.node.setPos(n.x + ax, n.y - ay));
    } else {
      const h = drag.h, nd = h.node, c = { x: drag.f0.x, y: drag.f0.y };
      let nw = Math.max(10, 2 * Math.abs(p.x - c.x)), nh = Math.max(10, 2 * Math.abs(p.y - c.y));
      const keep = e.shiftKey || h.type === 'view' || !(h.spec && h.spec.h !== undefined) || lookKind(h.spec) === 'image' || lookKind(h.spec) === 'item' || lookKind(h.spec) === 'icon';
      if (keep && !(h.type === 'obj' && lookKind(h.spec) === 'text')){ const r = Math.max(nw / drag.f0.w, nh / drag.f0.h); nw = drag.f0.w * r; nh = drag.f0.h * r; }
      drag.nw = nw; drag.nh = nh;
      nd.setSize(nw / Math.abs(nd.sx || 1), nh / Math.abs(nd.sy || 1));
      if (nd.kind === 'text') nd.fit();
    }
    paint();
  }
  function onUp(e){
    guideX.style.display = guideY.style.display = 'none';
    const d = drag; drag = null; if (!d) return;
    try{ ov.releasePointerCapture(e.pointerId); }catch(_){}
    if (!d.moved){ return; }
    if (d.mode === 'move' && d.blocked){ if (opts.onBlocked) opts.onBlocked(d.h); build(); setSel(d.h.id, true); return; }
    if (opts.beforeChange) opts.beforeChange();
    let info = null;
    if (d.mode === 'move') info = commitMove(d);
    else info = commitSize(d);
    ed.refresh(); if (opts.onChange) opts.onChange(info);
  }

  // ── ghi dữ liệu ──
  const par = (spec, key)=> F.par('view', spec, key);
  function commitMove(d){
    const h = d.h, ax = d.ax || 0, ay = d.ay || 0, dfx = ax / W, dfy = -ay / H;
    if (h.type === 'obj'){
      const sp = h.spec, nd = h.node, dxU = +sp.dx || 0, dyU = +sp.dy || 0;
      sp.x = R3((nd.x - dxU * U) / W + .5); sp.y = R3((nd.y - dyU * U) / H + .5);
      return { type: 'move', id: h.id };
    }
    const spec = h.spec; spec.p = spec.p || {};
    if (d.groupMove){
      const p = spec.p, t = spec.type;
      const sh = (key, dv)=>{ p[key] = R3(+par(spec, key) + dv); };
      if (t === 'cards'){ sh('x', dfx); sh('y', dfy); }
      else if (t === 'strip'){ sh('y', dfy); }
      else if (t === 'props'){
        const lay = par(spec, 'layout');
        if (lay === 'circle'){ sh('cx', dfx); sh('cy', dfy); }
        else if (lay === 'fixed'){ let pos = p.pos; if (typeof pos === 'string'){ try{ pos = JSON.parse(pos); }catch(_){ pos = []; } } p.pos = (pos || []).map(q=> [R3(+q[0] + dfx), R3(+q[1] + dfy)]); }
        else { sh('x0', dfx); sh('x1', dfx); sh('y0', dfy); sh('y1', dfy); }
      }
      if (p.ov) Object.keys(p.ov).forEach(i=>{ p.ov[i] = [R3(+p.ov[i][0] + dfx), R3(+p.ov[i][1] + dfy)]; });
      return { type: 'group', id: h.id };
    }
    spec.p.ov = spec.p.ov || {};
    spec.p.ov[h.slot] = [R3(h.node.x / W + .5), R3(h.node.y / H + .5)];
    return { type: 'ov', id: h.id };
  }
  function commitSize(d){
    const h = d.h, nw = d.nw || d.f0.w, nh = d.nh || d.f0.h;
    if (h.type === 'obj'){
      const sp = h.spec, hadH = sp.h !== undefined;
      sp.w = R3(nw / U);
      if (hadH || Math.abs(nh - nw) > 1.5) sp.h = R3(nh / U);
      return { type: 'size', id: h.id };
    }
    const spec = h.spec; spec.p = spec.p || {};
    if (spec.type === 'cards') spec.p.sizeU = R3(Math.min(.4, Math.max(.1, nw / U)));
    else if (spec.type === 'props') spec.p.size = String(R3(nw / U));
    else if (spec.type === 'strip') spec.p.slotU = R3(Math.min(.3, Math.max(.05, nw / U)));
    return { type: 'size', id: h.id };
  }

  // ── API ──
  /** Sinh n câu hỏi mẫu (tự động) của flow `fi`, đổi thành round viết tay — dùng khi chuyển game sang "Viết tay". */
  ed.sampleRounds = (n, fi)=>{
    const out = [], seen = new Set(), det = document.createElement('div'), flow = (F.flowsOf(data)[fi] || {}), type = (flow.gen || {}).type;
    const g2 = Object.assign({}, g, { tasks: [], run(gen){ return { gen }; } });
    for (let i = 0; i < n; i++){
      for (let tr = 0; tr < 8; tr++){   // tránh trùng câu đã có
        const root = new Node(det, 'rect', 0, 0, W, H), w = new F.FlowWorld(g2, 0, root, data);
        w.rng = new Rng(((Math.random() * 1e9) | 0) + i + tr); w.editMode = true; w.editFlow = fi; w.editRound = -1;
        w.taskNo = (type === 'set' || type === 'match') ? 60 : i * 2 + tr; w.ok = i * 2 + tr;   // set/match: dùng cả kho vật; còn lại: độ khó tăng dần
        let r = null; try{ w.beginTask({}, ()=>{}); r = F.qToRound(type, w._Q); }catch(e){ console.error('[FS edit] sampleRounds', e); }
        det.innerHTML = '';
        if (!r) break;
        const key = JSON.stringify(r); if (seen.has(key) && tr < 7) continue;
        seen.add(key); out.push(r); break;
      }
    }
    return out;
  };
  ed.setRound = i=>{ ed.round = i; ed.refresh(); };
  ed.refresh = ()=>{ build(); setSel(selId, true); };
  ed.reroll = ()=>{ ed.seed = (Math.random() * 1e9) | 0; ed.refresh(); };
  ed.setFlow = i=>{ ed.flowIdx = i; ed.refresh(); };
  ed.select = id=>{ setSel(id); };
  ed.destroy = ()=>{ clearInterval(timer); if (ed._ro) ed._ro.disconnect(); g.tasks.length = 0; host.innerHTML = ''; };
  mount(); build();
  return ed;
};

/** Thao tác dữ liệu dùng chung cho builder (không đụng DOM). */
F.editOps = {
  newShape(shape){ return { look: { shape, color: '#42A5F5' }, x: .5, y: .5, w: .22, h: .22 }; },
  newText(){ return { look: { text: 'Chữ mới', fs: .07, color: '#FFFFFF' }, x: .5, y: .5, w: .6, h: .12 }; },
  newImage(name){ return { look: { image: name }, x: .5, y: .5, w: .3, h: .3 }; },
  newItem(id){ return { look: { item: id }, x: .5, y: .5, w: .22, h: .22 }; },
};
})();
