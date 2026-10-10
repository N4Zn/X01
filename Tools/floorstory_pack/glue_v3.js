function fsdPlay(){
  const d = fsdData(); if (!d) return;
  if (!window.FSW){ alert('Thiếu engine chơi thử (fs/*.js chưa được nhúng — chạy Tools/floorstory_pack/inline_web.py).'); return; }
  const err = FSW.validate(d); if (err){ alert(err); return; }
  fsdStop();
  try{ fsdGame = FSW.play($('fsdHost'), d, { imgUrl: fsdImgUrl, mute: $('fsdMute').checked }); }
  catch(e){ console.error(e); alert('Lỗi chạy thử: ' + e.message); }
}
function fsdAuto(){ clearTimeout(fsdTimer); if (fsdGame) fsdTimer = setTimeout(fsdPlay, 500); }
function fsdSlug(s){ return String(s||'').normalize('NFD').replace(/[̀-ͯ]/g,'').replace(/đ/gi,'d').replace(/[^a-zA-Z0-9]+/g,'_').replace(/^_+|_+$/g,'').toLowerCase(); }
function fsdItemMode(it){ return fsdModes.get(it) || (it.image ? 'image' : it.icon ? 'icon' : 'shape'); }
function fsdThumb(it, box){
  box.innerHTML = '';
  try{ const n = new FSW.Node(box, 'rect', 0, 0, 44, 44); FSW.World.prototype.drawItem.call({ img: fsdImgUrl }, n, it, 0, 0, 34); }catch(e){}
}
// ── form sinh từ khai báo mô-đun (FSW.MOD) ──
function fsdEl(tag, cls, text){ const e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; }
/** Ô JSON: sửa tự do; chỉ ghi vào dữ liệu khi JSON hợp lệ (viền đỏ nếu sai). `empty` = giá trị coi như "không có" (xoá khoá). */
function fsdJsonArea(label, val, rows, onValid){
  const wrap = fsdEl('div', 'fsd-row'); wrap.style.alignItems = 'flex-start'; if (label){ const lb = fsdEl('label', null, label); lb.style.flex = '0 0 118px'; wrap.appendChild(lb); }
  const ta = document.createElement('textarea'); ta.rows = rows || 4; ta.style.cssText = 'flex:1; min-width:0; font-family:ui-monospace,monospace; font-size:11.5px;';
  ta.value = (val === undefined || val === null || val === '') ? '' : (typeof val === 'string' ? val : JSON.stringify(val, null, 1));
  ta.addEventListener('input', ()=>{
    const t = ta.value.trim();
    if (!t){ ta.style.outline = ''; onValid(undefined); return; }
    try{ const v = JSON.parse(t); ta.style.outline = ''; onValid(v); } catch(e){ ta.style.outline = '2px solid var(--bad)'; }
  });
  wrap.appendChild(ta); return wrap;
}
function fsdParamRow(def, key, holder, onSet){
  if (def.type === 'json') return fsdJsonArea(def.label, holder[key] === undefined ? '' : holder[key], 3, v=>{ if (v === undefined) delete holder[key]; else holder[key] = v; onSet(); });
  const row = fsdEl('div', 'fsd-row'), lb = fsdEl('label', null, def.label); if (def.help) lb.title = def.help; lb.style.flex = '0 0 150px';
  let inp;
  if (def.type === 'select'){
    inp = document.createElement('select'); inp.style.flex = '1';
    def.opts.forEach(o=>{ const op = document.createElement('option'); op.value = o; op.textContent = o; inp.appendChild(op); });
    inp.value = holder[key] != null ? holder[key] : def.def;
    inp.addEventListener('change', ()=>{ holder[key] = inp.value; onSet(); });
  } else {
    inp = document.createElement('input'); inp.type = def.type === 'text' ? 'text' : 'number';
    if (def.type !== 'text'){ inp.min = def.min; inp.max = def.max; inp.step = def.type === 'float' ? '0.05' : '1'; }
    const cur = holder[key]; inp.value = (cur === undefined || cur === null || cur === '') ? def.def : cur;
    inp.addEventListener('input', ()=>{
      if (def.type === 'text') holder[key] = inp.value;
      else { const v = parseFloat(inp.value); if (!isNaN(v)) holder[key] = def.type === 'int' ? Math.round(v) : v; }
      onSet();
    });
  }
  row.appendChild(lb); row.appendChild(inp); return row;
}
function fsdParams(wrap, kind, spec, onSet){
  spec.p = spec.p || {}; const defs = FSW.MOD[kind][spec.type].p;
  Object.keys(defs).forEach(k=> wrap.appendChild(fsdParamRow(defs[k], k, spec.p, onSet)));
}
function fsdTypeSelect(kind, spec, onType){
  const sel = document.createElement('select'); sel.style.width = '100%';
  Object.keys(FSW.MOD[kind]).forEach(k=>{ const op = document.createElement('option'); op.value = k; op.textContent = FSW.MOD[kind][k].name; if (k === spec.type) op.selected = true; sel.appendChild(op); });
  sel.addEventListener('change', ()=>{ spec.type = sel.value; spec.p = {}; onType(); });
  return sel;
}
let fsdFlowIdx = 0;
const FSD_EVENTS = [['start','Bắt đầu lượt (trước khi sinh câu)'],['setup','Sau khi dựng cảnh'],['step','Mỗi bước/lần chạm'],['right','Khi đúng / xong'],['wrong','Khi sai'],['end','Trước khi sang lượt sau']];
function fsdNormalize(d){
  d.theme = d.theme || {}; d.items = d.items || [];
  if (!d.flows || !d.flows.length){ d.flows = d.flow ? [d.flow] : []; }
  delete d.flow;
  d.flows.forEach(f=>{ f.text = f.text || {}; f.views = f.views || []; });
  fsdFlowIdx = Math.min(fsdFlowIdx, Math.max(0, d.flows.length - 1));
}
function renderFsd(){
  const d = fsdData(); if (!d || !window.FSW) return;
  fsdNormalize(d); const flows = d.flows, f = flows[fsdFlowIdx];
  $('fsdMechName').textContent = '🧩 ' + (d.title || game.meta.gameId);
  $('fsdMechDesc').textContent = 'Game = vars + cảnh + các flow. Mỗi flow = Nguồn (bốc vật) → Hiển thị (view, có thể "hiện n giây rồi úp") → Cách chấm → Phản hồi (hành động theo sự kiện). Sửa bên dưới, chơi thử bên phải.';
  $('fsdId').value = game.meta.gameId || ''; $('fsdTitle').value = d.title || game.meta.displayName || '';
  $('fsdSync').checked = !!d.sync;
  $('fsdBg').value = d.theme.bg || '#ffffff'; $('fsdFloor').value = d.theme.floor || '#cccccc';
  const wrap = $('fsdParams'); wrap.innerHTML = '';
  const again = ()=> renderFsd(), touch = ()=> fsdAuto();
  // nền / sàn có thể tắt
  const th = fsdEl('div', 'fsd-row');
  [['bg','Không vẽ nền'],['floor','Không vẽ sàn']].forEach(([k, lab])=>{
    const lb = fsdEl('label', 'chk', ''); lb.style.margin = '0'; const c = document.createElement('input'); c.type = 'checkbox'; c.checked = d.theme[k] === '';
    c.addEventListener('change', ()=>{ if (c.checked) d.theme[k] = ''; else d.theme[k] = $(k === 'bg' ? 'fsdBg' : 'fsdFloor').value; touch(); });
    lb.appendChild(c); lb.appendChild(document.createTextNode(' ' + lab)); th.appendChild(lb);
  });
  wrap.appendChild(th);
  // Trạng thái thế giới
  wrap.appendChild(fsdEl('div', 'fsd-sec', 'Biến trạng thái (vars) — giữ qua các lượt'));
  wrap.appendChild(fsdJsonArea('', d.vars || {}, 3, v=>{ if (v === undefined || (typeof v === 'object' && !Object.keys(v).length)) delete d.vars; else d.vars = v; touch(); }));
  wrap.appendChild(fsdEl('div', 'fsd-sec', 'Cảnh (scene) — đối tượng nền/nhân vật, ràng buộc bằng biểu thức'));
  wrap.appendChild(fsdJsonArea('', d.scene || [], 6, v=>{ if (v === undefined || (Array.isArray(v) && !v.length)) delete d.scene; else d.scene = v; touch(); }));
  // Flows
  wrap.appendChild(fsdEl('div', 'fsd-sec', 'Các flow (luân phiên theo lượt)'));
  const bar = fsdEl('div', 'fsd-bar');
  flows.forEach((_, i)=>{ const b = fsdEl('button', 'btn sm' + (i === fsdFlowIdx ? ' primary' : ''), 'Flow ' + (i+1)); b.addEventListener('click', ()=>{ fsdFlowIdx = i; again(); }); bar.appendChild(b); });
  const addF = fsdEl('button', 'btn sm', '＋ Flow'); addF.addEventListener('click', ()=>{ flows.push(JSON.parse(JSON.stringify(f))); fsdFlowIdx = flows.length - 1; again(); fsdAuto(); }); bar.appendChild(addF);
  if (flows.length > 1){ const delF = fsdEl('button', 'btn sm danger', '✕ Xoá flow này'); delF.addEventListener('click', ()=>{ flows.splice(fsdFlowIdx, 1); fsdFlowIdx = 0; again(); fsdAuto(); }); bar.appendChild(delF); }
  wrap.appendChild(bar);
  if (flows.length > 1) wrap.appendChild(fsdParamRow({ label:'Chọn flow mỗi lượt', type:'select', def:'alternate', opts:['alternate','random'] }, 'flowPick', d, touch));
  wrap.appendChild(fsdJsonArea('Đối tượng theo lượt (objects)', f.objects || [], 4, v=>{ if (v === undefined || (Array.isArray(v) && !v.length)) delete f.objects; else f.objects = v; touch(); }));
  // 1 Nguồn
  wrap.appendChild(fsdEl('div', 'fsd-sec', '1. Nguồn câu hỏi'));
  wrap.appendChild(fsdTypeSelect('gen', f.gen, ()=>{ again(); fsdAuto(); }));
  wrap.appendChild(fsdEl('p', 'fsd-desc', FSW.MOD.gen[f.gen.type].desc));
  fsdParams(wrap, 'gen', f.gen, touch);
  // 2 Hiển thị
  wrap.appendChild(fsdEl('div', 'fsd-sec', '2. Hiển thị (các view, vẽ từ dưới lên: view sau nằm trên)'));
  f.views.forEach((v, i)=>{
    const box = fsdEl('div', 'fsd-item'); box.style.display = 'block';
    const top = fsdEl('div', 'fsd-row'); top.appendChild(fsdTypeSelect('view', v, ()=>{ again(); fsdAuto(); }));
    const mv = (dx)=>{ const j = i + dx; if (j < 0 || j >= f.views.length) return; [f.views[i], f.views[j]] = [f.views[j], f.views[i]]; again(); fsdAuto(); };
    [['↑',()=>mv(-1)],['↓',()=>mv(1)],['✕',()=>{ f.views.splice(i,1); again(); fsdAuto(); }]].forEach(b=>{ const bt = fsdEl('button','btn',b[0]); bt.addEventListener('click', b[1]); top.appendChild(bt); });
    box.appendChild(top); box.appendChild(fsdEl('p', 'fsd-desc', FSW.MOD.view[v.type].desc));
    fsdParams(box, 'view', v, touch);
    v.reveal = v.reveal || {};
    box.appendChild(fsdEl('div', 'fsd-sec', 'Dòng thời gian của view này'));
    const rdefs = FSW.MOD.reveal.p;
    box.appendChild(fsdParamRow(rdefs.mode, 'mode', v.reveal, ()=>{ again(); fsdAuto(); }));
    if (v.reveal.mode && v.reveal.mode !== 'none') Object.keys(rdefs).filter(k=> k !== 'mode').forEach(k=> box.appendChild(fsdParamRow(rdefs[k], k, v.reveal, touch)));
    box.appendChild(fsdParamRow({ label:'Xuất hiện', type:'select', def:'now', opts:['now','afterReveal'], help:'afterReveal = chỉ hiện sau khi các view khác hiện-rồi-úp xong' }, 'appear', v, touch));
    wrap.appendChild(box);
  });
  const addV = fsdEl('button', 'btn sm', '＋ Thêm view'); addV.addEventListener('click', ()=>{ f.views.push({ type:'cards', p:{} }); again(); fsdAuto(); }); wrap.appendChild(addV);
  // 3 Chấm
  wrap.appendChild(fsdEl('div', 'fsd-sec', '3. Cách chấm'));
  wrap.appendChild(fsdTypeSelect('judge', f.judge, ()=>{ again(); fsdAuto(); }));
  wrap.appendChild(fsdEl('p', 'fsd-desc', FSW.MOD.judge[f.judge.type].desc));
  fsdParams(wrap, 'judge', f.judge, touch);
  // 4 Lời
  wrap.appendChild(fsdEl('div', 'fsd-sec', '4. Lời hiển thị'));
  wrap.appendChild(fsdEl('p', 'fsd-desc', 'Biến: ' + (FSW.MOD.gen[f.gen.type].vars || '') + '. {{biểu thức}} dùng được ($biến, q.*, tapped.*). HOA biến (vd {LABEL}) = in hoa.'));
  Object.keys(FSW.MOD.text).forEach(k=> wrap.appendChild(fsdParamRow({ label:FSW.MOD.text[k], type:'text', def:'' }, k, f.text, touch)));
  // 5 Phản hồi
  wrap.appendChild(fsdEl('div', 'fsd-sec', '5. Phản hồi — danh sách hành động theo sự kiện (trống = mặc định)'));
  wrap.appendChild(fsdEl('p', 'fsd-desc', 'Hành động: sfx say confetti bounce shake pulse move scale fade tint rotate show hide pop destroy spawn set inc bag pickItem if wait fly par text std. Đích: "@tapped" "@target" "@actor" "#id". Chi tiết: docs/floor-story-mechanics.md.'));
  f.on = f.on || {};
  FSD_EVENTS.forEach(([ev, lab])=> wrap.appendChild(fsdJsonArea(lab, f.on[ev] || '', 4, v=>{ if (v === undefined || (Array.isArray(v) && !v.length)) delete f.on[ev]; else f.on[ev] = v; touch(); })));
  const warn = FSW.validate(d); const w = fsdEl('p', 'fsd-desc', warn ? '⚠ ' + warn : '✔ Cấu hình hợp lệ'); if (warn) w.style.color = 'var(--bad)'; wrap.appendChild(w);
  renderFsdItems();
}
function renderFsdItems(){
  const d = fsdData(); if (!d) return;
  const wrap = $('fsdItems'); wrap.innerHTML = '';
  $('fsdItemCount').textContent = d.items.length;
  $('fsdItemsHelp').textContent = 'Mỗi vật: id (dùng trong biểu thức), tên, diện mạo (ảnh / icon dựng sẵn / hình + màu), giá trị (số), và "{ }" = các trường nâng cao (tags, ...). Vật xếp trước được mở trước.';
  d.items.forEach((it, idx)=>{
    const row = fsdEl('div', 'fsd-item'), th = fsdEl('div', 'fsd-thumb'); fsdThumb(it, th); row.appendChild(th);
    const text = (ph, key, w)=>{ const i = document.createElement('input'); i.type = 'text'; i.placeholder = ph; i.value = it[key] || ''; if (w) i.style.width = w;
      i.addEventListener('input', ()=>{ it[key] = i.value; if (key === 'label' && !fsdIdSet.has(it)) it.id = fsdSlug(i.value) || ('v' + idx); fsdAuto(); }); return i; };
    row.appendChild(text('id', 'id', '74px')); const idI = row.lastChild; idI.addEventListener('input', ()=>{ fsdIdSet.add(it); });
    row.appendChild(text('Tên', 'label'));
    const mode = fsdItemMode(it), sel = document.createElement('select');
    [['image','Ảnh'],['icon','Icon'],['shape','Hình + màu']].forEach(o=>{ const op = document.createElement('option'); op.value = o[0]; op.textContent = o[1]; if (o[0] === mode) op.selected = true; sel.appendChild(op); });
    sel.addEventListener('change', ()=>{
      const m = sel.value; fsdModes.set(it, m);
      if (m === 'image'){ it.icon = ''; } else if (m === 'icon'){ it.image = ''; if (!it.icon) it.icon = 'sun'; } else { it.image = ''; it.icon = ''; if (!it.shape) it.shape = 'circle'; if (!it.color) it.color = '#90A4AE'; }
      renderFsdItems(); fsdAuto();
    });
    row.appendChild(sel);
    if (mode === 'image'){
      const b = fsdEl('button', 'btn', it.image ? 'Đổi ảnh' : 'Chọn ảnh'); b.addEventListener('click', ()=>{ fsdPending = it; $('fsdFileInput').click(); }); row.appendChild(b);
    } else if (mode === 'icon'){
      const s2 = document.createElement('select');
      FSW.ICON_KEYS.forEach(k=>{ const op = document.createElement('option'); op.value = k; op.textContent = FSW.ICON_NAMES[k]; if (k === it.icon) op.selected = true; s2.appendChild(op); });
      s2.addEventListener('change', ()=>{ it.icon = s2.value; renderFsdItems(); fsdAuto(); }); row.appendChild(s2);
    } else {
      const s2 = document.createElement('select');
      ['circle','square','rect','rrect','triangle','diamond','star','heart','moon','flower'].forEach(k=>{ const op = document.createElement('option'); op.value = k; op.textContent = k; if (k === it.shape) op.selected = true; s2.appendChild(op); });
      s2.addEventListener('change', ()=>{ it.shape = s2.value; renderFsdItems(); fsdAuto(); }); row.appendChild(s2);
      const c = document.createElement('input'); c.type = 'color'; c.value = (it.color && /^#[0-9a-f]{6}$/i.test(it.color)) ? it.color : '#90a4ae';
      c.addEventListener('input', ()=>{ it.color = c.value; fsdThumb(it, th); fsdAuto(); }); row.appendChild(c);
    }
    const v = document.createElement('input'); v.type = 'number'; v.className = 'num'; v.title = 'Giá trị (số; dùng cho "so sánh", cỡ cửa...)'; v.value = it.value == null ? '' : it.value; v.placeholder = 'giá trị';
    v.addEventListener('input', ()=>{ if (v.value === '') delete it.value; else it.value = parseFloat(v.value) || 0; fsdAuto(); }); row.appendChild(v);
    const adv = fsdEl('button', 'btn', '{ }'); adv.title = 'Trường nâng cao (tags, ...)';
    const del = fsdEl('button', 'btn danger', '✕'); del.title = 'Xoá vật'; del.addEventListener('click', ()=>{ d.items.splice(idx, 1); renderFsdItems(); fsdAuto(); });
    const advBox = fsdEl('div'); advBox.style.cssText = 'width:100%; display:none;';
    adv.addEventListener('click', ()=>{
      if (advBox.style.display === 'none'){
        advBox.style.display = 'block'; advBox.innerHTML = '';
        const extra = {}; Object.keys(it).forEach(k=>{ if (['id','label','image','icon','shape','color','value'].indexOf(k) < 0) extra[k] = it[k]; });
        advBox.appendChild(fsdJsonArea('', extra, 3, val=>{
          Object.keys(it).forEach(k=>{ if (['id','label','image','icon','shape','color','value'].indexOf(k) < 0) delete it[k]; });
          if (val && typeof val === 'object' && !Array.isArray(val)) Object.assign(it, val); fsdAuto();
        }));
      } else advBox.style.display = 'none';
    });
    row.appendChild(adv); row.appendChild(del); row.appendChild(advBox);
    wrap.appendChild(row);
  });
}
