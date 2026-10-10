/* FloorStory — ngôn ngữ biểu thức nhỏ (dùng cho: điều kiện lọc vật, gán biến, ràng buộc thuộc tính cảnh, tham số hành động, mẫu câu {{...}}).
   Bản C# (FloorStoryGame/Flow/FsExpr.cs) PHẢI cho cùng kết quả. Kiểu giá trị: số, chuỗi, bool, danh sách.
   Cú pháp:  số | 'chuỗi' | "chuỗi" | $biến | đường.dẫn (item.tags.kind, q.gap, tapped.key ...) | f(a,b) | ! - * / % + - < <= > >= == != && || | c ? a : b
   Hàm: label(id) tag(id,k) min max abs floor ceil round sqrt sin cos rand() randi(a,b) pick(i,a,b,..) has(csv,x) len(x) at(list,i) list(..) shuf(list|csv) str num upper lower cap var(tên) item(id) randItem(tag,giá_trị) hex(r,g,b)
   Phạm vi (scope): { vars, q, item, i, n, tapped, w } — `w` = world (rand dùng w.rng, item()/randItem() dùng w.items). */
(function(){
'use strict';
const F = window.FSW;

// ── tokenizer ──
function tokenize(s){
  const t = []; let i = 0;
  while (i < s.length){
    const c = s[i];
    if (c === ' ' || c === '\t' || c === '\n' || c === '\r'){ i++; continue; }
    if (/[0-9.]/.test(c) && (c !== '.' || /[0-9]/.test(s[i+1]||''))){ let j = i; while (j < s.length && /[0-9.]/.test(s[j])) j++; t.push({ k:'num', v:parseFloat(s.slice(i,j)) }); i = j; continue; }
    if (c === '"' || c === "'"){ let j = i+1; let out = ''; while (j < s.length && s[j] !== c){ out += s[j]; j++; } t.push({ k:'str', v:out }); i = j+1; continue; }
    if (c === '$' || /[A-Za-z_]/.test(c)){ let j = i+1; while (j < s.length && /[A-Za-z0-9_.]/.test(s[j])) j++; t.push({ k: c === '$' ? 'var' : 'id', v: s.slice(c === '$' ? i+1 : i, j) }); i = j; continue; }
    const two = s.substr(i,2);
    if (['==','!=','<=','>=','&&','||'].indexOf(two) >= 0){ t.push({ k:'op', v:two }); i += 2; continue; }
    if ('+-*/%<>!?:(),'.indexOf(c) >= 0){ t.push({ k:'op', v:c }); i++; continue; }
    throw new Error('Biểu thức: ký tự lạ "' + c + '" trong: ' + s);
  }
  return t;
}
// ── parser (precedence climbing) → AST ──
function parse(src){
  const toks = tokenize(src); let p = 0;
  const peek = ()=> toks[p], next = ()=> toks[p++], isOp = v=> toks[p] && toks[p].k === 'op' && toks[p].v === v;
  function expect(v){ if (!isOp(v)) throw new Error('Biểu thức: thiếu "' + v + '" trong: ' + src); p++; }
  function ternary(){ const c = or(); if (isOp('?')){ p++; const a = ternary(); expect(':'); const b = ternary(); return { t:'?', c, a, b }; } return c; }
  function bin(sub, ops){ let l = sub(); while (peek() && peek().k === 'op' && ops.indexOf(peek().v) >= 0){ const o = next().v; l = { t:'b', o, l, r: sub() }; } return l; }
  const or = ()=> bin(and, ['||']), and = ()=> bin(eq, ['&&']), eq = ()=> bin(rel, ['==','!=']), rel = ()=> bin(add, ['<','<=','>','>=']), add = ()=> bin(mul, ['+','-']), mul = ()=> bin(un, ['*','/','%']);
  function un(){ if (isOp('!')){ p++; return { t:'!', e: un() }; } if (isOp('-')){ p++; return { t:'neg', e: un() }; } return prim(); }
  function prim(){
    const t = next(); if (!t) throw new Error('Biểu thức: thiếu vế phải: ' + src);
    if (t.k === 'num') return { t:'n', v:t.v };
    if (t.k === 'str') return { t:'s', v:t.v };
    if (t.k === 'var') return { t:'var', v:t.v };
    if (t.k === 'id'){
      if (isOp('(')){ p++; const args = []; if (!isOp(')')){ do { args.push(ternary()); if (isOp(',')) p++; else break; } while (true); } expect(')'); return { t:'f', n:t.v, a:args }; }
      return { t:'id', v:t.v };
    }
    if (t.k === 'op' && t.v === '('){ const e = ternary(); expect(')'); return e; }
    throw new Error('Biểu thức: lỗi cú pháp gần "' + t.v + '" trong: ' + src);
  }
  const e = ternary(); if (p < toks.length) throw new Error('Biểu thức: thừa ký tự trong: ' + src);
  return e;
}
const cache = {};
const toNum = v=> typeof v === 'number' ? v : typeof v === 'boolean' ? (v ? 1 : 0) : (v === '' || v == null ? 0 : (isNaN(+v) ? 0 : +v));
const isNumLike = v=> typeof v === 'number' || typeof v === 'boolean' || (typeof v === 'string' && v !== '' && !isNaN(+v));
const truthy = v=> Array.isArray(v) ? v.length > 0 : (typeof v === 'string' ? (v !== '' && v !== '0' && v !== 'false') : !!v);
const eqv = (a,b)=> (isNumLike(a) && isNumLike(b)) ? toNum(a) === toNum(b) : String(a == null ? '' : a) === String(b == null ? '' : b);
function path(scope, dotted){
  const parts = dotted.split('.'); let cur = scope;
  for (const k of parts){ if (cur == null) return ''; cur = cur[k]; }
  return cur === undefined || cur === null ? '' : cur;
}
const num = toNum;
const FN = {
  min:(s,a)=> Math.min(...a.map(toNum)), max:(s,a)=> Math.max(...a.map(toNum)), abs:(s,a)=> Math.abs(toNum(a[0])), floor:(s,a)=> Math.floor(toNum(a[0])), ceil:(s,a)=> Math.ceil(toNum(a[0])),
  round:(s,a)=> Math.round(toNum(a[0])), sqrt:(s,a)=> Math.sqrt(toNum(a[0])), sin:(s,a)=> Math.sin(toNum(a[0])), cos:(s,a)=> Math.cos(toNum(a[0])),
  rand:(s)=> s.w.rng.next(), randi:(s,a)=> toNum(a[0]) + s.w.rng.int(Math.max(1, toNum(a[1]) - toNum(a[0]))),
  pick:(s,a)=>{ const i = Math.max(0, Math.min(a.length-2, Math.floor(toNum(a[0])))); return a[1+i]; },
  has:(s,a)=> String(a[0]).split(',').map(x=>x.trim()).indexOf(String(a[1])) >= 0,
  len:(s,a)=> Array.isArray(a[0]) ? a[0].length : String(a[0]).length, at:(s,a)=>{ const l = Array.isArray(a[0]) ? a[0] : []; const v = l[Math.floor(toNum(a[1]))]; return v === undefined ? '' : v; },
  list:(s,a)=> a, str:(s,a)=> String(a[0]), num:(s,a)=> toNum(a[0]), upper:(s,a)=> String(a[0]).toUpperCase(), lower:(s,a)=> String(a[0]).toLowerCase(),
  cap:(s,a)=>{ const x = String(a[0]); return x.charAt(0).toUpperCase() + x.slice(1); },
  shuf:(s,a)=>{ let l = Array.isArray(a[0]) && a.length === 1 ? a[0].slice() : (a.length === 1 && typeof a[0] === 'string' ? a[0].split(',').map(x=> isNumLike(x) ? +x : x.trim()) : a.slice()); for (let i=l.length-1;i>0;i--){ const j = s.w.rng.int(i+1); [l[i],l[j]] = [l[j],l[i]]; } return l; },
  var:(s,a)=>{ const v = s.vars[String(a[0])]; return v === undefined ? 0 : v; },
  item:(s,a)=> s.w.items.find(i=> i.id === String(a[0])) || '',
  label:(s,a)=>{ const i = s.w.items.find(x=> x.id === String(a[0])); return i ? (i.label || i.id) : ''; },
  tag:(s,a)=>{ const i = s.w.items.find(x=> x.id === String(a[0])); const v = i && i.tags ? i.tags[String(a[1])] : ''; return v === undefined ? '' : v; },
  randItem:(s,a)=>{ const l = s.w.items.filter(i=> eqv(((i.tags||{})[String(a[0])]), a[1])); return l.length ? l[s.w.rng.int(l.length)].id : ''; },
};
function ev(n, s){
  switch (n.t){
    case 'n': case 's': return n.v;
    case 'var': { const v = s.vars[n.v]; return v === undefined ? 0 : v; }
    case 'id': return path(s, n.v);
    case 'neg': return -toNum(ev(n.e, s));
    case '!': return !truthy(ev(n.e, s));
    case '?': return truthy(ev(n.c, s)) ? ev(n.a, s) : ev(n.b, s);
    case 'f': { const f = FN[n.n]; if (!f) throw new Error('Hàm không có: ' + n.n); return f(s, n.a.map(x=> ev(x, s))); }
    case 'b': {
      if (n.o === '&&'){ const l = ev(n.l, s); return truthy(l) ? ev(n.r, s) : l; }
      if (n.o === '||'){ const l = ev(n.l, s); return truthy(l) ? l : ev(n.r, s); }
      const l = ev(n.l, s), r = ev(n.r, s);
      switch (n.o){
        case '+': return (typeof l === 'string' || typeof r === 'string') && !(isNumLike(l) && isNumLike(r) && typeof l !== 'string' && typeof r !== 'string') ? String(l) + String(r) : toNum(l) + toNum(r);
        case '-': return toNum(l) - toNum(r); case '*': return toNum(l) * toNum(r); case '/': return toNum(r) === 0 ? 0 : toNum(l) / toNum(r); case '%': return toNum(r) === 0 ? 0 : toNum(l) % toNum(r);
        case '==': return eqv(l, r); case '!=': return !eqv(l, r);
        case '<': return toNum(l) < toNum(r); case '<=': return toNum(l) <= toNum(r); case '>': return toNum(l) > toNum(r); case '>=': return toNum(l) >= toNum(r);
      }
    }
  }
  throw new Error('Biểu thức: nút lạ ' + n.t);
}
function evalExpr(src, scope){ const ast = cache[src] || (cache[src] = parse(src)); return ev(ast, scope); }
/** Giá trị tham số: chuỗi bắt đầu bằng "=" là biểu thức; còn lại giữ nguyên. */
function val(v, scope){ return (typeof v === 'string' && v.charAt(0) === '=') ? evalExpr(v.slice(1), scope) : v; }
/** Mẫu câu: {tên} lấy từ vars của Q (viết HOA = in hoa); {{biểu thức}} tính theo scope. */
function template(tpl, qvars, scope){
  return String(tpl == null ? '' : tpl)
    .replace(/\{\{(.+?)\}\}/g, (m, e)=> { try { const r = evalExpr(e, scope); return Array.isArray(r) ? r.join(',') : String(r); } catch(err){ return m; } })
    .replace(/\{(\w+)\}/g, (m, k)=>{
      if (qvars && qvars[k] !== undefined) return qvars[k];
      const lk = k.toLowerCase(); if (k === k.toUpperCase() && qvars && qvars[lk] !== undefined) return String(qvars[lk]).toUpperCase();
      return m;
    });
}
F.expr = { eval: evalExpr, val, template, toNum, truthy, eqv, parse };
})();
