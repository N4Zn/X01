/* FloorStory web engine — "flow" = 1 câu hỏi sinh động, ghép từ các MÔ-ĐUN độc lập (7 lớp):
     1 gen   (nguồn)    : bốc vật từ kho → Q (câu hỏi dạng dữ liệu): set | match | calc | values | sequence | pairs | number
     2 views (hiển thị) : vẽ Q: cards | strip | grid | seesaw | stack | props | floor   (mỗi view có thể có reveal = hiện n giây rồi úp/ẩn)
     3 reveal (thời gian): gắn vào view
     4/5 judge (chạm/chấm): equals | order | pairs | taps
     6 on    (phản hồi) : danh sách hành động theo sự kiện start/right/wrong/step/end (fs_act.js)
     7 vars + scene     : trạng thái thế giới qua các lượt + đối tượng cảnh có ràng buộc (fs_act.js)
   Bập bênh, đống khối chỉ là VIEW. Dữ liệu 1 game = fsData v3 — docs/floor-story-mechanics.md.
   QUAN TRỌNG: luật ở đây PHẢI khớp bản C# bên Unity (FloorStoryGame/Flow/*.cs). Sửa 1 bên thì sửa bên kia. */
(function(){
'use strict';
const F = window.FSW;
const { UI, hex, col, WHITE, tw, World, Game, cubeSprite } = F, X = F.expr;

// ── Khai báo mô-đun (form builder + mặc định cho cả web lẫn Unity) ───────────────────────
const I = (label, def, min, max, help)=> ({ label, type:'int', def, min, max, help });
const FL = (label, def, min, max, help)=> ({ label, type:'float', def, min, max, help });
const S = (label, def, help)=> ({ label, type:'text', def, help });
const J = (label, def, help)=> ({ label, type:'json', def, help });
const SEL = (label, def, opts, help)=> ({ label, type:'select', def, opts, help });
const MOD = {
  gen: {
    set: { name:'Bốc n vật, hỏi 1 vật', desc:'Bốc n vật từ kho; 1 vật là đáp án. Có thể lọc đáp án/vật nhiễu bằng biểu thức (theo tag + biến) và buộc vật nhiễu khác nhóm.',
      minItems: p=> Math.max(2, p.n||3),
      p: { n:I('Số vật mỗi câu',3,2,6), startItems:I('Số vật mở sẵn lúc đầu',3,2,99,'Chỉ các vật đầu danh sách được hỏi lúc đầu (99 = tất cả)'), addItems:I('Mỗi đợt mở thêm (vật)',2,0,10), every:I('Mở thêm sau mỗi (lượt)',4,1,30),
           targetWhere:S('Lọc đáp án (biểu thức)','', 'vd: item.tags.period == $period'), otherWhere:S('Lọc vật nhiễu (biểu thức)','','vd: item.tags.period != $period'),
           distinctBy:S('Vật nhiễu khác nhau theo tag','','tên tag, vd: period'), noRepeat:I('Không lặp đáp án liền kề (1/0)',0,0,1) },
      vars:'{label} = tên vật đáp án' },
    match: { name:'Chủ thể → đáp án theo thuộc tính', desc:'Chọn 1 chủ thể (vật) và các lựa chọn; đáp án = lựa chọn có id bằng tag của chủ thể (hoặc ngẫu nhiên nếu không đặt tag). Dùng cho: hình nào lấp chỗ trống, đặt đồ vào vị trí, cửa vừa cỡ, giờ trên đồng hồ...',
      minItems:()=> 2,
      p: { subjectWhere:S('Lọc chủ thể (biểu thức)','','vd: item.tags.part == 1'), optionsWhere:S('Lọc lựa chọn (biểu thức)','','vd: item.tags.kind2 == "zone"'), n:I('Số lựa chọn',3,2,12),
           answerTag:S('Tag của chủ thể trỏ tới id đáp án','','trống = đáp án ngẫu nhiên'), shuffle:I('Xáo lựa chọn (1/0)',1,0,1), noRepeat:I('Không lặp chủ thể/đáp án liền kề (1/0)',0,0,1) },
      vars:'{subject} = tên chủ thể, {label} = tên đáp án' },
    calc: { name:'Tính giá trị bằng biểu thức', desc:'Sinh biến ngẫu nhiên (defs) và danh sách lựa chọn kèm giá trị tính được (options); đáp án = lớn nhất/nhỏ nhất. Dùng cho: bắc cầu (khúc gỗ dài hơn sông).',
      minItems:()=> 1,
      p: { defs:J('Biến (JSON: tên → biểu thức)','{}'), options:J('Lựa chọn (JSON: [{item,v}])','[]'), answer:SEL('Đáp án','max',['max','min']), shuffle:I('Xáo lựa chọn (1/0)',1,0,1) },
      vars:'{label} = tên lựa chọn đúng' },
    values: { name:'Bốc vật khác "giá trị", hỏi lớn/nhỏ nhất', desc:'Bốc 2 (rồi 3) vật có giá trị khác nhau; đáp án = vật có giá trị LỚN nhất (hoặc NHỎ nhất khi hỏi ngược).', minItems:()=> 2,
      p: { threeAfter:I('Lên 3 vật sau (lượt đúng ngay lần đầu)',4,1,30), lessAfter:I('Bắt đầu hỏi "nhỏ" sau (lượt đúng)',2,0,30), lessEvery:I('Hỏi "nhỏ" mỗi (lượt)',3,2,10),
           wordMore:S('Từ "lớn"','nặng'), wordLess:S('Từ "nhỏ"','nhẹ'), suffix2:S('Đuôi khi 2 vật','hơn'), suffix3:S('Đuôi khi 3 vật','nhất') },
      vars:'{word} = từ lớn/nhỏ, {suffix} = hơn/nhất, {label} = tên vật đáp án' },
    none: { name:'Không có câu hỏi', desc:'Không sinh câu hỏi (dùng cho game chạm tự do như Sàn kỳ diệu).', minItems:()=> 0, p: {}, vars:'' },
    sequence: { name:'Chuỗi hình', desc:'Bốc k loại hình rồi xếp thành chuỗi dài dần (có thể lặp hình hoặc bắt khác nhau).', minItems: p=> Math.max(3, p.kinds||4),
      p: { where:S('Lọc vật được bốc (biểu thức)','','vd: item.tags.kind == "shape"'), kinds:I('Số loại hình mỗi lượt',4,3,6), startLen:I('Chuỗi bắt đầu dài',1,1,6), maxLen:I('Chuỗi dài tối đa',6,1,8), growEvery:I('Dài thêm 1 sau mỗi (lượt)',2,1,10),
           lenBy:SEL('Đếm theo','ok',['ok','task'],'ok = lượt đúng ngay lần đầu; task = mọi lượt'), distinct:I('Mọi hình trong chuỗi khác nhau (1/0)',0,0,1) },
      vars:'{len} = độ dài chuỗi, {names} = tên các hình nối bằng ", rồi "' },
    pairs: { name:'Các cặp thẻ giống nhau', desc:'Bốc số cặp tăng dần (mỗi bảng +1 cặp); mỗi vật xuất hiện 2 lần, xáo trộn.', minItems: p=> Math.max(2, p.startPairs||2),
      p: { startPairs:I('Số cặp bảng đầu',2,2,6), maxPairs:I('Số cặp tối đa',6,2,8) }, vars:'{pairs} = số cặp' },
    number: { name:'Một số đếm', desc:'Chọn 1 số (tăng dần theo lượt) + các số gần nó làm đáp án. Dùng với view "Đống khối" để đếm.', minItems:()=> 0,
      p: { minStart:I('Số nhỏ nhất lúc đầu',1,1,6), startMax:I('Số lớn nhất lúc đầu',3,2,12), maxCount:I('Số lớn nhất tối đa',10,3,12), growEvery:I('Tăng độ khó sau mỗi (lượt đúng ngay lần đầu)',2,1,10),
           options:I('Số đáp án',3,2,4), objectId:S('Vật cần đếm (id; trống = khối hộp 3D)','') },
      vars:'{count} = số đúng' },
  },
  view: {
    cards: { name:'Hàng thẻ', desc:'Hàng thẻ chạm được: hiện các vật của câu hỏi, hoặc các số đáp án.',
      p: { source:SEL('Nội dung','items',['items','options'],'items = các vật; options = các số (dùng với gen "Một số đếm")'), y:FL('Vị trí dọc (0 dưới – 1 trên)',.46,.1,.9), sizeU:FL('Cỡ thẻ (theo cạnh nhỏ)',.32,.1,.4), tall:FL('Tỉ lệ cao/rộng',1.1,.8,1.3), fig:FL('Cỡ hình trong thẻ',.66,.4,.9), label:I('Chữ tên dưới hình (1/0)',0,0,1), bg:S('Màu thẻ','#FFFFFF') } },
    strip: { name:'Dải hình (theo chuỗi)', desc:'Hàng ô hiện các hình của chuỗi theo thứ tự. Thường dùng với "Hiện n giây rồi úp".',
      p: { y:FL('Vị trí dọc',.70,.1,.9), slotU:FL('Cỡ ô (đơn vị U, tối đa)',.2,.05,.3), plain:I('Chỉ hình, không ô nền (1/0)',0,0,1) } },
    grid: { name:'Lưới thẻ', desc:'Lưới thẻ cho các cặp (có thể úp sẵn). Chạm để lật.', p: { covered:I('Úp sẵn (1/0)',1,0,1) } },
    seesaw: { name:'Bập bênh', desc:'Các vật đứng trên bập bênh; bên giá trị LỚN hơn chúi xuống. Chạm vào chính vật. 3 vật = 2 bập bênh nối nhau.', p: {} },
    stack: { name:'Đống khối 3D', desc:'Đống khối (hoặc vật) đẳng cự với đúng số lượng của câu hỏi.', p: { color:SEL('Màu khối','random',['random','#FF7043','#42A5F5','#66BB6A','#FFCA28','#AB47BC','#26C6DA']) } },
    props: { name:'Vật đặt tự do (props)', desc:'Mỗi lựa chọn là 1 vật tại 1 vị trí (hàng, cột, vòng tròn, lưới, ngẫu nhiên, cố định). Hình dạng mỗi vật vẽ theo mẫu `tpl` (danh sách đối tượng, biểu thức dùng opt/i/n/size). Dùng cho: trứng, bóng bay, cổng, cửa, khúc gỗ, vùng đặt đồ, số trên đồng hồ...',
      p: { layout:SEL('Bố cục','row',['row','col','circle','grid','random','fixed']), x0:FL('Từ x',.15,0,1), x1:FL('Đến x',.85,0,1), y0:FL('Từ y',.46,0,1), y1:FL('Đến y',.46,0,1),
           cx:FL('Tâm x (vòng tròn)',.5,0,1), cy:FL('Tâm y (vòng tròn)',.46,0,1), r:FL('Bán kính (đơn vị U)',.3,.05,.6), startDeg:FL('Góc bắt đầu (độ, theo kim đồng hồ từ số 12)',0,-360,360),
           cols:I('Số cột (lưới)',3,1,8), hU:FL('Chiều cao cố định (đơn vị U; 0 = theo tỉ lệ)',0,0,1), yExpr:S('Vị trí dọc theo biểu thức (opt/i/n/size)',''), pos:J('Vị trí cố định (JSON [[x,y],..])','[]'), size:S('Cỡ (đơn vị U, số hoặc =biểu thức)','0.2'), tall:FL('Tỉ lệ cao/rộng',1,.3,3),
           preset:SEL('Mẫu hình','bare',['bare','card','zone','none'],'khi `tpl` trống'), tpl:J('Mẫu hình (JSON: danh sách đối tượng, ưu tiên hơn mẫu)','[]'), anim:SEL('Hoạt hình','none',['none','bob','float','pulse']), labelBelow:I('Chữ tên dưới vật (1/0)',0,0,1) } },
    floor: { name:'Nền chạm (floor)', desc:'Vùng chạm phủ kín world; mỗi lần chạm sinh sự kiện "step" kèm vị trí chạm (dùng với chấm "Đếm lượt chạm").', p: {} },
  },
  judge: {
    equals: { name:'Chạm đúng đáp án', desc:'Chạm vật/số đúng = đúng; sai thì thử lại. Đúng/sai chạy hành động on.right / on.wrong (mặc định: bounce + confetti + câu khen; rung + câu sai).',
      p: { hintAfter:I('Gợi ý sau (lần sai)',2,1,5), finishSec:FL('Giây chờ trước câu sau',1.8,.5,4) } },
    order: { name:'Chạm theo đúng thứ tự chuỗi', desc:'Chạm các vật theo thứ tự chuỗi. Mỗi bước đúng chạy on.step; xong chạy on.right. Nếu có view "Dải hình" thì ô tương ứng lật ra.',
      p: { hintAfter:I('Gợi ý sau (lần sai ở 1 bước)',2,1,5), finishSec:FL('Giây chờ trước câu sau',2.2,.5,4) } },
    pairs: { name:'Lật 2 thẻ ghép cặp', desc:'Chạm lật 2 thẻ: giống nhau thì ăn (+1 điểm ngay), khác nhau thì úp lại sau vài giây.',
      p: { hideSec:FL('Giây hiện thẻ sai trước khi úp lại',1.5,.8,4), bonus:I('Thưởng khi xong bảng không nhầm (1/0)',1,0,1), finishSec:FL('Giây chờ trước bảng sau',2.2,.5,4) } },
    taps: { name:'Đếm lượt chạm', desc:'Mỗi lần chạm trên view "Nền chạm" chạy on.step; đủ số lần thì xong lượt (on.right). Không có đúng/sai.',
      p: { count:I('Số lần chạm mỗi lượt',6,1,30), finishSec:FL('Giây chờ trước lượt sau',1,.2,4) } },
  },
  reveal: {
    p: { mode:SEL('Hiện','none',['none','sequential','all'],'none = hiện luôn; sequential = từng vật cách nhau; all = cùng lúc'), showSec:FL('Giây giữa 2 vật',.6,.2,3), hold:FL('Giữ thêm (giây)',1.2,0,6), holdPerItem:FL('Giữ thêm mỗi vật (giây)',.35,0,2),
         then:SEL('Sau đó','cover',['cover','hide','keep'],'cover = úp (dấu ?); hide = ẩn; keep = giữ nguyên') },
  },
  text: { prompt:'Câu hỏi lúc đầu', ready:'Câu hỏi sau khi hiện xong (nếu có)', say:'Câu khen khi đúng', sayOk:'Câu khen khi đúng nhưng đã sai trước đó (nếu có)', wrong:'Câu khi chạm sai', first:'Câu riêng cho lượt đầu (nếu có)' },
};

// ── tiện ích ─────────────────────────────────────────────────────────────────
function par(kind, spec, key){
  const defs = ((MOD[kind]||{})[spec.type] || {}).p || {}, d = (defs[key]||{}).def, v = (spec.p||{})[key], t = (defs[key]||{}).type;
  if (t === 'text' || t === 'select' || t === 'json') return (v === undefined || v === null) ? d : v;
  const n = parseFloat(v); return (v === undefined || v === null || v === '' || isNaN(n)) ? d : n;
}
const jpar = (kind, spec, key, d)=>{ const v = par(kind, spec, key); if (v && typeof v === 'object') return v; try { return JSON.parse(v); } catch(e){ return d; } };
const PALETTE = ['#FF7043','#42A5F5','#66BB6A','#FFCA28','#AB47BC','#26C6DA'];
const tagOf = (it, k)=> ((it && it.tags) || {})[k];
const sceneFor = (w, extra)=> w.scope(extra);

// ═════════════════════════ GEN ═════════════════════════════════════════════════
const GEN = {
  set(w, spec){
    const r = w.sync ? w.nextTaskRng() : w.rng, items = w.items, P = k=> par('gen', spec, k), n = Math.min(P('n'), items.length);
    const allowed = Math.min(items.length, Math.max(n, P('startItems') + P('addItems')*Math.floor((w.taskNo-1)/Math.max(1, P('every')))));
    const tw_ = P('targetWhere'), ow_ = P('otherWhere'), dist = P('distinctBy');
    if (!tw_ && !ow_ && !dist){
      const idx = []; for (let i=0;i<allowed;i++) idx.push(i); w.shuffle(idx, r);
      const picks = idx.slice(0, n).map(i=> items[i]), target = r.int(n), name = w.nameOf(picks[target]);
      return { items: picks, target, vars:{ label:name }, id:'SET_' + (picks[target].id||name), desc:'Chọn ' + name, answers: picks.map(p=> w.nameOf(p)) };
    }
    const pool = items.slice(0, allowed), ok = (it, e)=> !e || X.truthy(w.ev('=' + e, { item: it }));
    let tp = pool.filter(it=> ok(it, tw_)); if (!tp.length) tp = pool.slice();
    if (P('noRepeat') && w._lastTarget && tp.length > 1) tp = tp.filter(it=> it.id !== w._lastTarget);
    const target = tp[r.int(tp.length)]; w._lastTarget = target.id;
    let others = w.shuffle(pool.filter(it=> it !== target && ok(it, ow_)), r);
    if (dist){
      const used = new Set([String(tagOf(target, dist))]), chosen = [];
      others.forEach(it=>{ if (chosen.length < n-1 && !used.has(String(tagOf(it, dist)))){ used.add(String(tagOf(it, dist))); chosen.push(it); } });
      others.forEach(it=>{ if (chosen.length < n-1 && chosen.indexOf(it) < 0) chosen.push(it); });
      others = chosen;
    } else others = others.slice(0, n-1);
    const picks = w.shuffle([target].concat(others), r), ti = picks.indexOf(target), name = w.nameOf(target);
    return { items: picks, target: ti, vars:{ label:name }, id:'SET_' + (target.id||name), desc:'Chọn ' + name, answers: picks.map(p=> w.nameOf(p)) };
  },
  match(w, spec){
    const r = w.sync ? w.nextTaskRng() : w.rng, P = k=> par('gen', spec, k), ok = (it, e)=> !e || X.truthy(w.ev('=' + e, { item: it }));
    const subjPool = w.items.filter(it=> ok(it, P('subjectWhere'))), optPool = w.items.filter(it=> ok(it, P('optionsWhere')) && (P('optionsWhere') || it !== undefined));
    let sp = subjPool.length ? subjPool : w.items.slice();
    if (P('noRepeat') && w._lastSubject && sp.length > 1) sp = sp.filter(it=> it.id !== w._lastSubject);
    const subject = sp[r.int(sp.length)]; w._lastSubject = subject.id;
    const pool = optPool.filter(it=> it !== subject), n = Math.min(P('n'), pool.length), tag = P('answerTag');
    let ans = tag ? pool.find(it=> it.id === String(tagOf(subject, tag))) : null;
    if (!ans){ let c = pool; if (P('noRepeat') && w._lastAns && c.length > 1) c = c.filter(it=> it.id !== w._lastAns); ans = c[r.int(c.length)]; }
    w._lastAns = ans.id;
    let options;
    if (P('shuffle')) options = w.shuffle([ans].concat(w.shuffle(pool.filter(it=> it !== ans), r).slice(0, n-1)), r);
    else { options = pool.slice(0, n); if (options.indexOf(ans) < 0) options[options.length-1] = ans; }
    return { items: options, target: options.indexOf(ans), subject, vars:{ subject: w.nameOf(subject), label: w.nameOf(ans) }, id:'MATCH_' + (subject.id||'') + '_' + ans.id, desc:'Ghép ' + w.nameOf(subject), answers: options.map(p=> w.nameOf(p)) };
  },
  calc(w, spec){
    const r = w.sync ? w.nextTaskRng() : w.rng, P = k=> par('gen', spec, k), Qv = {}, defs = jpar('gen', spec, 'defs', {}), opts = jpar('gen', spec, 'options', []);
    const scope = ()=> ({ q: Object.assign({ v: Qv }, Qv) });
    Object.keys(defs).forEach(k=>{ Qv[k] = w.ev('=' + defs[k], scope()); });
    let list = opts.map(o=> ({ item: w.items.find(i=> i.id === String(w.ev(o.item, scope()))) || w.items[0], v: X.toNum(w.ev('=' + o.v, scope())) }));
    if (P('shuffle')) w.shuffle(list, r);
    let ti = 0; list.forEach((o,i)=>{ if (P('answer') === 'min' ? o.v < list[ti].v : o.v > list[ti].v) ti = i; });
    return { items: list.map(o=> o.item), optv: list.map(o=> o.v), v: Qv, target: ti, vars:{ label: w.nameOf(list[ti].item) }, id:'CALC_' + ti, desc:'Chọn theo giá trị', answers: list.map(o=> w.nameOf(o.item)) };
  },
  values(w, spec){
    const P = k=> par('gen', spec, k), byVal = {};
    w.items.forEach(it=>{ const v = +it.value || 0; (byVal[v] = byVal[v] || []).push(it); });
    const vals = Object.keys(byVal).map(Number).sort((a,b)=> a-b);
    const n = (w.ok >= P('threeAfter') && vals.length >= 3) ? 3 : 2;
    const less = w.ok >= P('lessAfter') && w.taskNo % P('lessEvery') === 0;
    let chosen;
    if (n === 3) chosen = vals.length === 3 ? vals.slice() : w.shuffle(vals.slice()).slice(0,3).sort((a,b)=>a-b);
    else if (w.ok < 2) chosen = [vals[0], vals[vals.length-1]];
    else chosen = w.shuffle(vals.slice()).slice(0,2);
    const items = chosen.map(v=> byVal[v][w.rand(0, byVal[v].length)]); w.shuffle(items);
    let target = 0; for (let i=1;i<n;i++) if (less ? (+items[i].value < +items[target].value) : (+items[i].value > +items[target].value)) target = i;
    const word = less ? P('wordLess') : P('wordMore'), suffix = n === 2 ? P('suffix2') : P('suffix3');
    return { items, target, vars:{ word, suffix, label:w.nameOf(items[target]) }, id:'VAL_' + (items[target].id||target) + (less?'_min':'_max') + n, desc:'Con nào ' + word + ' ' + suffix, answers: items.map(p=> w.nameOf(p)) };
  },
  none(w, spec){ return { items: [], target: -1, vars:{}, id:'NONE', desc:'', answers:[] }; },
  sequence(w, spec){
    const P = k=> par('gen', spec, k), srcItems = P('where') ? w.items.filter(it=> X.truthy(w.ev('=' + P('where'), { item: it }))) : w.items, kinds = Math.min(P('kinds'), srcItems.length);
    const cnt = P('lenBy') === 'task' ? (w.taskNo - 1) : w.ok;
    const len = Math.min(P('maxLen'), P('startLen') + Math.floor(cnt / Math.max(1, P('growEvery'))));
    const all = srcItems.map((_,i)=> i); w.shuffle(all);
    const items = all.slice(0, kinds).map(i=> srcItems[i]); let seq = [];
    if (P('distinct')){ const idx = items.map((_,i)=> i); w.shuffle(idx); seq = idx.slice(0, Math.min(len, kinds)); }
    else for (let i=0;i<len;i++){
      let v, g = 0;
      do { v = w.rand(0, kinds); g++; } while (g < 30 && ((len <= 3 && seq.indexOf(v) >= 0) || (i >= 2 && seq[i-1] === v && seq[i-2] === v)));
      seq.push(v);
    }
    return { items, seq, target: seq[0], vars:{ len: seq.length, names: seq.map(i=> w.nameOf(items[i]).toLowerCase()).join(', rồi ') }, id:'SEQ_' + seq.length + '_' + seq.join('-'), desc:'Chuỗi ' + seq.length + ' hình', answers: items.map(p=> w.nameOf(p)) };
  },
  pairs(w, spec){
    const P = k=> par('gen', spec, k);
    const pairs = Math.max(2, Math.min(P('maxPairs'), P('startPairs') + (w.taskNo-1), w.items.length));
    const all = w.items.map((_,i)=> i); w.shuffle(all);
    const faces = []; for (let i=0;i<pairs;i++) faces.push(i, i); w.shuffle(faces);
    return { items: all.slice(0, pairs).map(i=> w.items[i]), faces, pairs, vars:{ pairs }, id:'PAIRS_' + pairs, desc:'Lật thẻ giống nhau: ' + pairs + ' cặp', answers:[faces.join(',')] };
  },
  number(w, spec){
    const P = k=> par('gen', spec, k), lv = Math.floor(w.ok / Math.max(1, P('growEvery'))), maxC = Math.min(12, P('maxCount'));
    const lo = Math.min(P('minStart') + lv, 4), hi = Math.min(P('startMax') + 2*lv, maxC);
    const count = w.rand(Math.min(lo, hi), hi + 1), opts = [count]; let g = 0;
    while (opts.length < P('options') && g++ < 80){ const v = count + w.rand(-2, 3); if (v >= 1 && v <= 12 && opts.indexOf(v) < 0) opts.push(v); }
    w.shuffle(opts);
    const obj = w.items.find(i=> i.id === P('objectId')) || null;
    return { items: obj ? [obj] : [], options: opts, number: count, target: opts.indexOf(count), vars:{ count }, id:'NUM_' + count, desc:'Đếm ' + count, answers: opts.map(String) };
  },
};

// ═════════════════════════ VIEW ════════════════════════════════════════════════
// V = { elems:[{node,key,kind,name,fig,item}], parts:[{show(),cover(),hide()}], hideAll(), setActive(b), intro(): generator, ready }
const PRESET_TPL = {
  bare: [{ look:{ item:'=opt.id' }, w:'=size' }],
  card: [{ look:{ kind:'rrect', color:'#FFFFFF' }, w:'=size', h:'=size*1.1', alpha:.96 }, { look:{ item:'=opt.id' }, w:'=size*.66' }],
  zone: [{ look:{ shape:'circle', color:'#FFFFFF' }, w:'=size', alpha:.38 }, { look:{ shape:'circle', color:'#FFFFFF' }, w:'=size*.75', alpha:.35 }],
  none: [],
};
const VIEW = {
  cards(w, spec, Q){
    const P = k=> par('view', spec, k), isNum = P('source') === 'options', list = isNum ? Q.options : Q.items, n = list.length;
    const spacing = n <= 3 ? .31 : n === 4 ? .24 : .72/(n-1), cs = Math.min(w.U*P('sizeU'), w.W*spacing*.93), tall = P('tall');
    const elems = [];
    list.forEach((it, i)=>{
      const pos = w.P(.5 + (i - (n-1)/2)*spacing, P('y'));
      let card, fig = null;
      if (isNum) card = UI.card(w.root, null, String(it), hex('#7E57C2'), pos.x, pos.y, cs, cs*tall, null, 72).rt;
      else {
        card = UI.card(w.root, null, '', hex(P('bg')), pos.x, pos.y, cs, cs*tall, null).rt; card.alpha = .96;
        fig = w.drawItem(card, it, 0, P('label') ? cs*tall*.1 : 0, cs*P('fig'));
        if (P('label')) UI.label(card, w.nameOf(it), 22, hex('#3A2A12'), 0, -cs*tall*.38, cs*.96, cs*tall*.26, false);
      }
      w.track(card);
      elems.push({ node: card, key: i, kind:'card', name: isNum ? String(it) : w.nameOf(it), fig, item: isNum ? null : it, itemId: isNum ? '' : it.id });
    });
    return { elems, parts: [], ready: true, hideAll(){}, cover(){}, hide(){},
      setActive(b){ elems.forEach(e=>{ e.node.active = b; if (b) w.run(tw.popIn(e.node, .25)); }); },
      *intro(){ elems.forEach(e=> w.run(tw.popIn(e.node, .3))); } };
  },
  props(w, spec, Q){
    const P = k=> par('view', spec, k), list = Q.items, n = list.length, U = w.U, layout = P('layout');
    const tplIn = jpar('view', spec, 'tpl', []), tpl = (tplIn && tplIn.length) ? tplIn : PRESET_TPL[P('preset')] || [];
    const posIn = jpar('view', spec, 'pos', []), tall = P('tall'), elems = [], pts = [];
    const sizeOf = (i, it)=> { const ex = { opt: Object.assign({}, it, { v: (Q.optv||[])[i] }), i, n, item: it }; return +w.ev(P('size'), ex) || .2; };
    const area = { x0:P('x0'), x1:P('x1'), y0:P('y0'), y1:P('y1') };
    if (layout === 'row') list.forEach((_,i)=> pts.push(w.P(n > 1 ? area.x0 + (area.x1-area.x0)*i/(n-1) : (area.x0+area.x1)/2, area.y0)));
    else if (layout === 'col') list.forEach((_,i)=> pts.push(w.P(area.x0, n > 1 ? area.y0 + (area.y1-area.y0)*i/(n-1) : area.y0)));
    else if (layout === 'circle') list.forEach((_,i)=>{ const a = (P('startDeg') + i*360/n)*Math.PI/180, c = w.P(P('cx'), P('cy')); pts.push({ x: c.x + Math.sin(a)*P('r')*U, y: c.y + Math.cos(a)*P('r')*U }); });
    else if (layout === 'grid'){ const cols = P('cols'), rows = Math.ceil(n/cols); list.forEach((_,i)=>{ const r = Math.floor(i/cols), c = i%cols; pts.push(w.P(area.x0 + (area.x1-area.x0)*(cols > 1 ? c/(cols-1) : .5), area.y0 + (area.y1-area.y0)*(rows > 1 ? r/(rows-1) : 0))); }); }
    else if (layout === 'fixed') list.forEach((_,i)=>{ const p = posIn[i] || [.5,.5]; pts.push(w.P(p[0], p[1])); });
    else { // random: tránh đè nhau
      const placed = [], s0 = sizeOf(0, list[0]) * U;
      list.forEach(()=>{ let best = null, bs = 1e18; for (let a=0;a<80;a++){ const c = w.P(area.x0 + w.rng.next()*(area.x1-area.x0), area.y0 + w.rng.next()*(area.y1-area.y0)); let sc = 0; placed.forEach(q=>{ const d = Math.hypot(c.x-q.x, c.y-q.y); if (d < s0) sc += s0 - d; }); if (sc < bs){ bs = sc; best = c; if (sc === 0) break; } } placed.push(best); pts.push(best); });
    }
    list.forEach((it, i)=>{
      const s = sizeOf(i, it), ex = { opt: Object.assign({}, it, { v: (Q.optv||[])[i] }), i, n, item: it, size: s };
      const hh = P('hU') > 0 ? P('hU')*U : s*U*tall, py = P('yExpr') ? w.P(0, +w.ev(P('yExpr'), ex)).y : pts[i].y;
      const cont = UI.pic(w.root, null, col(255,255,255,0), pts[i].x, py, s*U, hh, true); cont.preserveAspect = false; w.track(cont);
      w.makeObjs(tpl.map(t=> Object.assign({ rel:true }, t)), cont, w.taskObjs, ex);
      if (P('labelBelow')) UI.label(cont, w.nameOf(it), 26, WHITE, 0, -hh*.5 - U*.045, U*.5, U*.08);
      if (P('anim') !== 'none') w.taskObjs.push({ node: cont, spec:{ anim:{ type: P('anim') === 'pulse' ? 'pulse' : 'bob', amp: P('anim') === 'float' ? .02 : .012, speed: P('anim') === 'float' ? 1.6 : 3 } }, extra:{}, cur:{}, animOn:true, x0: cont.x, y0: cont.y, t0: w.rng.next()*6.28 });
      elems.push({ node: cont, key: i, kind:'prop', name: w.nameOf(it), fig: null, item: it, itemId: it.id });
    });
    return { elems, parts: [], ready: true, hideAll(){}, cover(){}, hide(){}, setActive(b){ elems.forEach(e=>{ e.node.active = b; if (b) w.run(tw.popIn(e.node, .25)); }); },
      *intro(){ elems.forEach(e=> w.run(tw.popIn(e.node, .3))); } };
  },
  floor(w, spec, Q){
    const V = { elems: [], parts: [], ready: true, onTap: null, hideAll(){}, cover(){}, hide(){}, setActive(){}, *intro(){} };
    const a = UI.tapArea(w.root, p=>{ if (V.onTap) V.onTap(p); }); w.track(a);
    return V;
  },
  strip(w, spec, Q){
    const P = k=> par('view', spec, k), len = Q.seq.length, slot = Math.min(w.U*P('slotU'), w.W*.92/len*.9), gap = slot*.1, rowW = len*slot + (len-1)*gap;
    const bg = [], face = [], qq = [], y = w.P(.5, P('y')).y, plain = !!P('plain');
    for (let i=0;i<len;i++){
      const b = UI.pic(w.root, 'rrect', plain ? col(255,255,255,0) : col(255,255,255,.9), -rowW/2 + slot/2 + i*(slot+gap), y, slot, slot); b.sliced = true; b.preserveAspect = false; w.track(b); bg.push(b);
      const q = UI.label(b, '?', Math.round(slot*.6), hex('#90A4AE'), 0, 0, slot, slot, false); q.active = false; qq.push(q);
      face.push(w.drawItem(b, Q.items[Q.seq[i]], 0, 0, slot*.82));
    }
    return { elems: [], bg, ready: true,
      parts: face.map((f,i)=> ({ show(){ f.active = true; qq[i].active = false; w.run(tw.popIn(f, .3)); }, cover(){ f.active = false; qq[i].active = true; }, hide(){ f.active = false; qq[i].active = false; } })),
      hideAll(){ face.forEach((f,i)=>{ f.active = false; qq[i].active = false; }); }, setActive(){}, *intro(){},
      mark(i){ face[i].active = true; qq[i].active = false; w.run(tw.popIn(face[i], .3)); bg[i].color = plain ? bg[i].color : hex('#C8F7C5'); if (plain) w.run(tw.fade(face[i], .4, .3)); },
      pulseSlot(i, alive){ w.run(tw.pulse(bg[i], alive, .06, 6)); } };
  },
  grid(w, spec, Q){
    const P = k=> par('view', spec, k), total = Q.faces.length, pairs = Q.pairs, rows = pairs <= 5 ? 2 : 3, cols = Math.ceil(total/rows);
    const areaW = w.W*.94, areaH = w.H*.76, cs = Math.min(areaW/cols, areaH/rows)*.88, stepX = areaW/cols, stepY = areaH/rows, origin = w.P(.5,.47);
    const covered = !!P('covered'), elems = [];
    Q.faces.forEach((fi, i)=>{
      const r = Math.floor(i/cols), c = i % cols;
      const holder = UI.pic(w.root, null, col(255,255,255,0), origin.x + (c-(cols-1)/2)*stepX, origin.y + ((rows-1)/2 - r)*stepY, cs, cs, true); holder.preserveAspect = false; w.track(holder);
      const back = UI.pic(holder, 'rrect', hex('#26A69A'), 0, 0, cs, cs); back.sliced = true; back.preserveAspect = false; UI.label(back, '?', Math.round(cs*.6), WHITE, 0, 0, cs, cs);
      const front = UI.pic(holder, 'rrect', WHITE, 0, 0, cs, cs); front.sliced = true; front.preserveAspect = false; w.drawItem(front, Q.items[fi], 0, 0, cs*.82);
      back.active = covered; front.active = !covered;
      elems.push({ node: holder, key: fi, kind:'flip', back, front, faceUp: !covered, matched:false, name: w.nameOf(Q.items[fi]) });
    });
    return { elems, ready: true,
      parts: elems.map(e=> ({ show(){ e.back.active = false; e.front.active = true; e.faceUp = true; }, cover(){ e.back.active = true; e.front.active = false; e.faceUp = false; }, hide(){ e.node.active = false; } })),
      hideAll(){ elems.forEach(e=>{ e.back.active = true; e.front.active = false; e.faceUp = false; }); },
      setActive(){}, *intro(){ elems.forEach(e=> w.run(tw.popIn(e.node, .25))); },
      *flip(e, up){
        e.faceUp = up; const rt = e.node; if (rt.dead) return;
        for (let t=0;t<.12;t+=F.T.dt){ if (rt.dead) return; rt.sx = F.lerp(1,0,t/.12); yield null; }
        if (rt.dead) return; e.back.active = !up; e.front.active = up;
        for (let t=0;t<.12;t+=F.T.dt){ if (rt.dead) return; rt.sx = F.lerp(0,1,t/.12); yield null; }
        if (!rt.dead) rt.sx = 1;
      } };
  },
  seesaw(w, spec, Q){
    const n = Q.items.length, U = w.U, W = w.W, size = n === 2 ? U*.30 : U*.20, plankLen = W*(n === 2 ? .86 : .80), thick = U*.04;
    const pivotY = n === 2 ? [.30] : [.60,.25], pairs = n === 2 ? [[0,1]] : [[0,1],[1,2]], elems = [], saws = [];
    pairs.forEach((pr, s)=>{
      const pv = w.P(.5, pivotY[s]), sw = { pivot: pv, animal:[null,null], local:[{x:0,y:0},{x:0,y:0}], items:[] };
      w.track(UI.pic(w.root, 'triangle', hex('#8D6E63'), pv.x, pv.y - U*.055, U*.14, U*.13));
      const plank = UI.pic(w.root, 'rrect', hex('#C98B4B'), pv.x, pv.y, plankLen, thick); plank.sliced = true; plank.preserveAspect = false; w.track(plank); sw.plank = plank;
      const swap = w.rand(0,2) === 0;
      for (let side=0; side<2; side++){
        const ii = pr[swap ? 1 - side : side], it = Q.items[ii];
        const a = w.drawItem(w.root, it, pv.x, pv.y, size); w.track(a);
        if (n === 2) UI.label(a, w.nameOf(it), 30, WHITE, 0, size*.62, size*1.6, size*.3);
        sw.animal[side] = a; sw.items[side] = it; sw.local[side] = { x:(side === 0 ? -1 : 1)*plankLen*.32, y: thick*.5 + size*.5 };
        elems.push({ node: a, key: ii, kind:'item', name: w.nameOf(it), fig: null, item: it, itemId: it.id });
      }
      setAngle(sw, 0); saws.push(sw);
    });
    function setAngle(sw, deg){
      sw.plank.rot = deg; const a = deg*Math.PI/180, c = Math.cos(a), s = Math.sin(a);
      for (let i=0;i<2;i++){ const l = sw.local[i], an = sw.animal[i]; if (an && !an.dead) an.setPos(sw.pivot.x + l.x*c - l.y*s, sw.pivot.y + l.x*s + l.y*c); }
    }
    const V = { elems, parts: [], ready: false, hideAll(){}, setActive(){}, cover(){}, hide(){},
      *intro(){
        elems.forEach(e=> w.run(tw.popIn(e.node, .35)));
        const A = 15; yield .5;
        for (let t=0;t<.7;t+=F.T.dt){ const wob = Math.sin(t*14)*2.5; saws.forEach(sw=>{ if (!sw.plank.dead) setAngle(sw, wob); }); yield null; }
        const tg = saws.map(sw=> (+sw.items[0].value > +sw.items[1].value) ? A : -A);
        w.sfx('wood');
        for (let t=0;t<.9;t+=F.T.dt){ const e = F.outBack(t/.9); saws.forEach((sw,i)=>{ if (!sw.plank.dead) setAngle(sw, tg[i]*e); }); yield null; }
        saws.forEach((sw,i)=>{ if (!sw.plank.dead) setAngle(sw, tg[i]); });
        V.ready = true;
      } };
    return V;
  },
  stack(w, spec, Q){
    const P = k=> par('view', spec, k), count = Q.number, U = w.U, W = w.W, H = w.H;
    const heights = makeStack(w, count), cp = P('color');
    const color = hex(cp === 'random' ? PALETTE[w.rand(0, PALETTE.length)] : cp), obj = Q.items[0] || null;
    const s = Math.min(W*.9/6.2, H*.52/6.2, U*.11), cells = [];
    for (let x=0;x<3;x++) for (let y=0;y<3;y++) for (let z=0;z<heights[x][y];z++) cells.push([x,y,z]);
    cells.sort((a,b)=> (a[0]+a[1]+a[2]) - (b[0]+b[1]+b[2]));
    const iso = c=> ({ x:(c[0]-c[1])*s, y:-(c[0]+c[1])*s*.5 + c[2]*s });
    let minX=1e9, maxX=-1e9, minY=1e9, maxY=-1e9;
    cells.forEach(c=>{ const p = iso(c); minX=Math.min(minX,p.x-s); maxX=Math.max(maxX,p.x+s); minY=Math.min(minY,p.y-s); maxY=Math.max(maxY,p.y+s); });
    const pc = w.P(.5,.60), center = { x: pc.x - (minX+maxX)/2, y: pc.y - (minY+maxY)/2 };
    const cubes = cells.map(c=>{
      const p = iso(c), n = obj ? w.drawItem(w.root, obj, center.x+p.x, center.y+p.y, 2*s*.9) : UI.pic(w.root, cubeSprite(color), WHITE, center.x+p.x, center.y+p.y, 2*s, 2*s);
      n.scale = 0; w.track(n); return n;
    });
    const V = { elems: [], ready: true, center, solvedAt: { x:center.x, y:center.y + s },
      parts: cubes.map(c=> ({ show(){ w.run(tw.popIn(c, .25)); }, cover(){ c.scale = 0; }, hide(){ c.scale = 0; } })),
      hideAll(){ cubes.forEach(c=> c.scale = 0); }, setActive(){},
      *intro(){ for (const c of cubes){ if (c.dead) return; w.run(tw.popIn(c, .25)); yield .07; } },
      *countAloud(onDone){
        const badges = [];
        for (let i=0;i<cubes.length;i++){
          if (cubes[i].dead) return;
          const b = UI.pic(w.root, 'circle', col(38,26,77,.85), cubes[i].x, cubes[i].y + s*.5, s*.85, s*.85); UI.label(b, String(i+1), Math.round(s*.7), WHITE, 0, 0, s*.85, s*.85);
          badges.push(b); w.track(b); w.run(tw.popIn(b, .2)); w.sfx('tap'); yield .4;
        }
        if (onDone) onDone(); yield 1.6; badges.forEach(b=> b.destroy());
      },
      solved(cb){ w.run(V.countAloud(cb)); },
      hintExtra(){ if (!V._counted){ V._counted = true; w.run(V.countAloud(null)); } } };
    return V;
  },
};
function makeStack(w, count){
  const R = n=> w.rand(0, n);
  for (let at=0; at<80; at++){
    const h = [[0,0,0],[0,0,0],[0,0,0]]; h[R(3)][R(3)] = 1; let placed = 1, g = 0;
    while (placed < count && g++ < 200){
      const x = R(3), y = R(3); if (h[x][y] >= 3) continue;
      let sup = h[x][y] > 0;
      if (!sup) for (let dx=-1; dx<=1 && !sup; dx++) for (let dy=-1; dy<=1 && !sup; dy++){ const nx=x+dx, ny=y+dy; if (nx>=0&&nx<3&&ny>=0&&ny<3&&h[nx][ny]>0) sup = true; }
      if (!sup) continue; h[x][y]++; placed++;
    }
    if (placed === count && noHidden(h)) return h;
  }
  const f = [[0,0,0],[0,0,0],[0,0,0]]; for (let i=0;i<count;i++) f[i%3][Math.floor(i/3)%3]++; return f;
}
function noHidden(h){
  for (let x=0;x<3;x++) for (let y=0;y<3;y++) for (let z=0;z<h[x][y];z++){
    if (h[x][y] > z+1 && x+1<3 && h[x+1][y] > z && y+1<3 && h[x][y+1] > z) return false;
  }
  return true;
}

// ═════════════════════════ JUDGE ═══════════════════════════════════════════════
const allElems = V=> V.reduce((a,v)=> a.concat(v.elems.map(e=> Object.assign(e, { view: v }))), []);
const JUDGE = {
  equals(w, spec, Q, V, finish){
    const P = k=> par('judge', spec, k);
    let mistakes = 0, firstWrong = -1, hint = false, answered = false, busy = false;
    const elems = allElems(V).filter(e=> e.key != null);
    const onTap = function*(el){
      if (!w.active || answered || busy || !w.isReady()) return;
      const cx = { tapped: el, tap: { x: el.node.x, y: el.node.y }, mistakes };
      if (el.key === Q.target){
        answered = true; hint = false; busy = true;
        yield* w.fire('right', cx, w._stdRight.bind(w));
        const solved = V.find(v=> v.solved);
        if (solved){ const st = { done:false }; w.run(solved.countAloud(()=> st.done = true)); while (!st.done){ if (w.stale(Q)) return; yield null; } w.confetti(solved.solvedAt.x, solved.solvedAt.y, 14); }
        yield P('finishSec');
        if (w.stale(Q)) return;
        yield* w.fire('end', cx);
        finish(mistakes === 0, [mistakes === 0 ? Q.target : firstWrong]);
      } else {
        mistakes++; if (firstWrong < 0) firstWrong = el.key; busy = true;
        yield* w.fire('wrong', cx, w._stdWrong.bind(w));
        busy = false;
        if (w.stale(Q)) return;
        if (mistakes >= P('hintAfter') && !hint){
          hint = true; V.forEach(v=> v.hintExtra && v.hintExtra());
          elems.filter(e=> e.key === Q.target).forEach(e=> w.run(tw.pulse(e.node, ()=> hint && !answered, .12, 7)));
        }
      }
    };
    elems.forEach(el=> UI.onTap(el.node, ()=> w.run(onTap(el))));
  },
  order(w, spec, Q, V, finish){
    const P = k=> par('judge', spec, k), T = w.flow.text || {}, strip = V.find(v=> v.mark);
    const elems = allElems(V).filter(e=> e.key != null), len = Q.seq.length, taps = [];
    let cur = 0, stepMist = 0, total = 0, hint = false, finished = false, busy = false;
    const onTap = function*(el){
      if (!w.active || !w.isReady() || finished || busy) return;
      taps.push(el.key);
      const cx = { tapped: el, tap: { x: el.node.x, y: el.node.y }, step: cur, last: false };
      if (el.key === Q.seq[cur]){
        hint = false; stepMist = 0; const my = cur; cur++;
        if (strip) strip.mark(my);
        const last = cur >= len; cx.last = last;
        if (last) finished = true;
        yield* w.fire('step', cx, function*(){ w.right(); w.run(tw.bounce(el.node, .15, .3)); });
        if (last){
          const perfect = total === 0;
          yield* w.fire('right', cx, function*(){ w.confetti(0, w.H*.05, 14); w.say(X.template(perfect ? (T.say||'') : (T.sayOk||T.say||''), Q.vars, w.scope(cx)), w.P(.5,.55), hex('#1B5E20'), 1.6); });
          yield P('finishSec'); if (w.stale(Q)) return;
          yield* w.fire('end', cx); finish(perfect, taps.slice());
        } else if (strip){ const nx = cur; strip.pulseSlot(nx, ()=> w.active && !finished && cur === nx); }
      } else {
        total++; stepMist++; busy = true; yield* w.fire('wrong', cx, function*(){ w.sfx('tap'); w.run(tw.shake(el.node, .35, 10)); const t = T.wrong ? X.template(T.wrong, Object.assign({}, Q.vars, { name: el.name }), w.scope(cx)) : ''; if (t) w.say(t, w.P(.5,.14), hex('#BF360C'), 1.3); }); busy = false;
        if (w.stale(Q)) return;
        if (stepMist >= P('hintAfter') && !hint){
          hint = true; const at = cur, want = elems.find(e=> e.key === Q.seq[cur]);
          if (want) w.run(tw.pulse(want.node, ()=> hint && cur === at && !finished, .15, 7));
        }
      }
    };
    elems.forEach(el=> UI.onTap(el.node, ()=> w.run(onTap(el))));
    if (strip) w.onReadyOnce(()=> strip.pulseSlot(0, ()=> w.active && !finished && cur === 0));
  },
  pairs(w, spec, Q, V, finish){
    const P = k=> par('judge', spec, k), T = w.flow.text || {}, grid = V.find(v=> v.flip);
    if (!grid) throw new Error('Cách chấm "ghép cặp" cần 1 view "Lưới thẻ".');
    let mistakes = 0, matched = 0, busy = false; const open = [];
    const resolve = function*(a, b){
      yield .45; if (!w.active || w.stale(Q)) return;
      if (a.key === b.key){
        a.matched = b.matched = true; matched++; w.right(); w.addPoint();
        w.confetti(a.node.x, a.node.y, 6, false); w.confetti(b.node.x, b.node.y, 6, false);
        w.run(tw.bounce(a.node, .25, .4)); yield* tw.bounce(b.node, .25, .4);
        w.run(tw.scaleTo(a.node, 0, .25)); yield* tw.scaleTo(b.node, 0, .25);
        if (!a.node.dead) a.node.active = false; if (!b.node.dead) b.node.active = false;
        busy = false;
        if (matched >= Q.pairs){
          const perfect = mistakes === 0;
          w.confetti(0, 0, 16); w.say(X.template(perfect ? T.say : (T.sayOk || T.say), Q.vars, w.scope()), w.P(.5,.5), hex('#1B5E20'), 1.8);
          yield P('finishSec'); finish(!!P('bonus') && perfect, [mistakes]);
        }
      } else {
        mistakes++; w.sfx('plop'); yield* tw.shake(b.node, .3, 8);
        yield Math.max(0, P('hideSec') - .75);
        if (!w.active || w.stale(Q)) return;
        w.run(grid.flip(a, false)); yield* grid.flip(b, false); busy = false;
      }
    };
    allElems(V).forEach(el=> UI.onTap(el.node, ()=>{
      if (!w.active || !w.isReady() || busy || el.matched || el.faceUp) return;
      w.sfx('tap'); w.run(grid.flip(el, true)); open.push(el);
      if (open.length < 2) return;
      busy = true; const a = open[0], b = open[1]; open.length = 0; w.run(resolve(a, b));
    }));
  },
  taps(w, spec, Q, V, finish){
    const P = k=> par('judge', spec, k), fl = V.find(v=> 'onTap' in v);
    if (!fl) throw new Error('Cách chấm "đếm lượt chạm" cần 1 view "Nền chạm".');
    let count = 0, done = false;
    fl.onTap = function(p){
      if (!w.active || done || !w.isReady()) return;
      count++; const cx = { tap: p, step: count - 1 };
      w.run((function*(){
        yield* w.fire('step', cx);
        if (count >= P('count') && !done){
          done = true; yield* w.fire('right', cx, function*(){ w.right(); }); yield P('finishSec'); if (w.stale(Q)) return; yield* w.fire('end', cx); finish(true, [count]);
        }
      })());
    };
  },
};

// ═════════════════════════ WORLD ═══════════════════════════════════════════════
const flowsOf = d=> (d.flows && d.flows.length) ? d.flows : (d.flow ? [d.flow] : []);
class FlowWorld extends World {
  build(){
    this._items = []; this.ok = 0; this.taskNo = 0; this.gen = 0; this.active = false; this._ready = false; this._readyCbs = [];
    this.vars = JSON.parse(JSON.stringify(this.data.vars || {})); this._bags = {}; this.objs = []; this.taskObjs = []; this.elems = []; this._Q = null; this.flow = flowsOf(this.data)[0];
    const TH = this.data.theme || {}, bgc = TH.bg === undefined ? '#E3F2FD' : TH.bg, flc = TH.floor === undefined ? '#BBDEFB' : TH.floor;   // '' = không vẽ
    if (bgc) UI.fill(this.root, hex(bgc));
    if (flc){ const f = UI.pic(this.root, 'square', hex(flc), 0, this.P(.5,.06).y, this.W, this.H*.14); f.preserveAspect = false; }
    this.makeObjs((this.data.scene || []).filter(o=> o.z !== 'front'), this.root, this.objs, {});
    this.makeObjs((this.data.scene || []).filter(o=> o.z === 'front'), this.root, this.objs, {});
    this.prompt = UI.label(this.root, '', 40, WHITE, 0, this.P(.5,.92).y, this.W*.94, this.H*.13);
  }
  update(dt, time){ this.updateObjs(this.objs.concat(this.taskObjs), dt, time); }
  track(n){ this._items.push(n); return n; }
  clearItems(){
    this.gen++; this.active = false; this._items.forEach(n=>{ if (n && n.destroy) n.destroy(); }); this._items = []; this._readyCbs = [];
    this.taskObjs.forEach(o=>{ if (o.node && o.node.destroy) o.node.destroy(); }); this.taskObjs = []; this.elems = [];
  }
  stale(Q){ return Q !== this._Q; }
  isReady(){ return this._ready; }
  onReadyOnce(cb){ if (this._ready) cb(); else this._readyCbs.push(cb); }
  /** Chạy hành động của sự kiện `ev` (on[ev]); không có thì chạy mặc định `std` (generator function). */
  *fire(ev, cx, std){
    const list = ((this.flow.on || {})[ev]) || [];
    if (list.length) yield* this.runActs(list, cx); else if (std) yield* std(cx);
  }
  /** Chạy hành động tức thì (không chờ) — dùng cho on.start trước khi sinh câu hỏi. */
  exec(list){ const g = this.runActs(list, {}); for (let i=0;i<1000;i++){ const r = g.next(); if (r.done) break; } }
  beginTask(info, done){
    this.clearItems(); this.active = true; this.taskNo++; this._ready = false;
    const flows = flowsOf(this.data); this.flow = flows[this.data.flowPick === 'random' ? this.rng.int(flows.length) : (this.taskNo-1) % flows.length];
    const f = this.flow, gen = this.gen, T = f.text || {};
    this.vars._task = this.taskNo; this.vars._ok = this.ok;
    if ((f.on || {}).start) this.exec(f.on.start);
    const Q = this._Q = GEN[f.gen.type](this, f.gen);
    Object.assign(info, { id:Q.id, topic:f.gen.type, description:Q.desc, answers:Q.answers, correct:Q.target });
    this.makeObjs((f.objects || []).filter(o=> o.z !== 'front'), this.root, this.taskObjs, {});
    const first = this.taskNo === 1 && T.first ? T.first : T.prompt;
    this.prompt.text = X.template(first, Q.vars, this.scope()); this.voice(this.prompt.text);
    const V = (f.views || []).map(s=> VIEW[s.type](this, s, Q));
    this.makeObjs((f.objects || []).filter(o=> o.z === 'front'), this.root, this.taskObjs, {});
    this.objs.filter(o=> (o.spec||{}).z === 'front').forEach(o=> o.node.setAsLast()); this.prompt.setAsLast();
    this.elems = allElems(V);
    if ((f.on || {}).setup) this.exec(f.on.setup);
    const finish = (ok, ans)=>{ if (ok) this.ok++; done(ok, ans); };
    JUDGE[f.judge.type](this, f.judge, Q, V, finish);
    this.run(this._present(f, V, Q, gen, T));
  }
  *_present(f, V, Q, gen, T){
    const specs = f.views || [];
    const revs = V.map((v,i)=> ({ v, r: specs[i].reveal || {} })).filter(o=> (o.r.mode || 'none') !== 'none');
    const defer = V.filter((v,i)=> specs[i].appear === 'afterReveal' && revs.length);
    defer.forEach(v=> v.elems.forEach(e=> e.node.active = false));
    revs.forEach(o=> o.v.hideAll());
    const intros = V.filter(v=> !revs.some(o=> o.v === v) && defer.indexOf(v) < 0);
    intros.forEach(v=> this.run(v.intro()));
    if (revs.length){
      yield .5;
      for (const o of revs){
        if (gen !== this.gen) return;
        const r = o.r, P = k=> par('reveal', { type:'p', p: r }, k), parts = o.v.parts;
        if (r.mode === 'all'){ parts.forEach(p=> p.show()); yield P('showSec'); }
        else for (const p of parts){ if (gen !== this.gen) return; p.show(); this.sfx('pop'); yield P('showSec'); }
        yield P('hold') + P('holdPerItem')*parts.length;
        if (gen !== this.gen) return;
        if (r.then === 'hide') parts.forEach(p=> p.hide()); else if (r.then !== 'keep') parts.forEach(p=> p.cover());
        this.sfx('plop');
      }
      defer.forEach(v=>{ v.elems.forEach(e=>{ e.node.active = true; this.run(tw.popIn(e.node, .25)); }); });
      if (T.ready){ this.prompt.text = X.template(T.ready, Q.vars, this.scope()); this.voice(this.prompt.text); }
    }
    while (V.some(v=> v.ready === false)) { if (gen !== this.gen) return; yield null; }
    this._ready = true; const cbs = this._readyCbs; this._readyCbs = []; cbs.forEach(cb=> cb());
  }
}
Game.WorldClass = FlowWorld;

F.MOD = MOD; F.par = par; F.flowsOf = flowsOf; F.GEN = GEN; F.FlowWorld = FlowWorld;
/** Kiểm tra cấu hình trước khi chạy/xuất; trả về chuỗi lỗi hoặc null. */
F.validate = function(d){
  const flows = flowsOf(d); if (!flows.length) return 'Thiếu cấu hình flow.';
  for (let fi = 0; fi < flows.length; fi++){
    const f = flows[fi], tag = flows.length > 1 ? `[flow ${fi+1}] ` : '';
    if (!f.gen || !f.judge || !f.views) return tag + 'Thiếu cấu hình flow.';
    const g = MOD.gen[f.gen.type]; if (!g) return tag + 'Nguồn không có: ' + f.gen.type;
    const gp = {}; Object.keys(g.p).forEach(k=> gp[k] = par('gen', f.gen, k));
    const need = g.minItems(gp); if ((d.items||[]).length < need) return `${tag}Nguồn "${g.name}" cần ít nhất ${need} vật (đang có ${(d.items||[]).length}).`;
    if (f.gen.type === 'values' && new Set((d.items||[]).map(i=> +i.value || 0)).size < 2) return tag + 'Cần ít nhất 2 giá trị khác nhau (cột "Giá trị").';
    if (!f.views.length) return tag + 'Cần ít nhất 1 view hiển thị.';
    for (const v of f.views) if (!MOD.view[v.type]) return tag + 'View không có: ' + v.type;
    if (!MOD.judge[f.judge.type]) return tag + 'Cách chấm không có: ' + f.judge.type;
    const has = t=> f.views.some(v=> v.type === t), gt = f.gen.type, jt = f.judge.type;
    if (jt === 'order' && gt !== 'sequence') return tag + 'Chấm "theo thứ tự" cần nguồn "Chuỗi hình".';
    if (jt === 'pairs' && (gt !== 'pairs' || !has('grid'))) return tag + 'Chấm "ghép cặp" cần nguồn "Các cặp thẻ" + view "Lưới thẻ".';
    if (jt === 'equals' && gt === 'pairs') return tag + 'Nguồn "Các cặp thẻ" chỉ chấm được bằng "ghép cặp".';
    if (jt === 'taps' && !has('floor')) return tag + 'Chấm "đếm lượt chạm" cần view "Nền chạm".';
    if (has('seesaw') && gt !== 'values') return tag + 'View "Bập bênh" cần nguồn "Bốc vật khác giá trị".';
    if (has('stack') && gt !== 'number') return tag + 'View "Đống khối" cần nguồn "Một số đếm".';
    if (has('strip') && gt !== 'sequence') return tag + 'View "Dải hình" cần nguồn "Chuỗi hình".';
    if (has('grid') && gt !== 'pairs') return tag + 'View "Lưới thẻ" cần nguồn "Các cặp thẻ".';
    if ((jt === 'equals' || jt === 'order') && !f.views.some(v=> ['cards','seesaw','props'].indexOf(v.type) >= 0)) return tag + 'Cần view chạm được (Hàng thẻ / Bập bênh / Vật đặt tự do).';
    for (const k of ['tpl','pos','defs','options']){ for (const o of [f.views.map(v=> v.p), [f.gen.p]].reduce((a,b)=> a.concat(b), [])){ const v = o && o[k]; if (typeof v === 'string' && v.trim()){ try { JSON.parse(v); } catch(e){ return `${tag}JSON sai ở "${k}": ${e.message}`; } } } }
  }
  return null;
};
/** Chơi thử: host = DOM; data = fsData; opts = { imgUrl(name)→url, mute }. Trả về Game (có .stop()). */
F.play = function(host, data, opts){ return new Game(host, JSON.parse(JSON.stringify(data)), opts); };
})();
