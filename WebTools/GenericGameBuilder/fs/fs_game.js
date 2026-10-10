/* FloorStory web engine — World (StoryWorld.cs) + Game (FloorStoryController.cs: 2 đội, chế độ chung, điểm +1 khi đúng ngay lần đầu). */
(function(){
'use strict';
const F = window.FSW;
const { UI, hex, col, WHITE, Rng, tw, T, SFX, buildIcon } = F;

const CONFETTI = ['#FFD93D','#FF6B6B','#6BCB77','#4D96FF','#FF9F45','#C77DFF'];
const SHAPES = ['circle','square','rect','triangle','diamond','star','heart','moon','flower'];

class World {
  constructor(game, team, rootNode, data){
    this.g = game; this.team = team; this.root = rootNode; this.data = data; this.W = 512; this.H = 500;
    this.rng = new Rng((Math.random()*1e9) | 0); this._taskNo = 0; this._wait = null;
    this.p = data.p || {};
    this.build();
  }
  get U(){ return Math.min(this.W, this.H); }
  get sync(){ return !!this.data.sync; }
  get items(){ return this.data.items || []; }
  /** Tham số số học (p.key) — thiếu thì dùng mặc định `d`. */
  num(key, d){ const v = this.p[key]; return (v === undefined || v === null || v === '' || isNaN(+v)) ? d : +v; }
  str(key, d){ const v = this.p[key]; return (v === undefined || v === null || v === '') ? d : String(v); }
  P(fx, fy){ return { x:(fx-.5)*this.W, y:(fy-.5)*this.H }; }
  rand(a, b){ return this.rng.range(a, b); }
  shuffle(arr, r){ r = r || this.rng; for (let i=arr.length-1;i>0;i--){ const j = r.int(i+1); [arr[i],arr[j]] = [arr[j],arr[i]]; } return arr; }
  run(gen){ return this.g.run(gen); }
  nextTaskRng(){ this._taskNo++; return new Rng(this.g.sharedSeed*7919 + this._taskNo*104729); }
  addPoint(){ this.g.addPoint(this.team); }
  sfx(k){ this.g.sfx(k); }
  right(){ this.g.sfx('right'); }
  voice(text){ this.g.speak(text); }
  img(name){ return this.g.imgUrl(name); }
  theme(key, d){ const t = this.data.theme || {}; return t[key] || d; }

  build(){}              // dựng phần cố định (1 lần)
  beginTask(info, done){} // bắt đầu 1 lượt; gọi done(đúngNgayLầnĐầu, đáp án) đúng 1 lần
  describe(info, id, topic, desc, answers, correct){ Object.assign(info, { id, topic, description: desc, answers, correct }); }

  /** Vẽ 1 vật theo "diện mạo" item: ảnh > icon dựng sẵn > hình + màu. `size` = cạnh hộp. */
  drawItem(parent, item, x, y, size){
    if (item && item.image){ const u = this.img(item.image); if (u) return UI.pic(parent, { url:u }, WHITE, x, y, size, size); }
    if (item && item.icon && F.ICON_KEYS.indexOf(item.icon) >= 0) return buildIcon(parent, item.icon, x, y, size);
    const sh = (item && item.shape) || 'circle';
    const rect = sh === 'rect';
    const n = UI.pic(parent, rect ? 'square' : sh, hex((item && item.color) || '#90A4AE'), x, y, size*(rect?1.5:1), size*(rect?.95:1));
    n.preserveAspect = false; n._paint();
    return n;
  }
  /** Tên hiển thị của item (label, hoặc tên icon). */
  nameOf(item){ return (item && (item.label || (item.icon && F.ICON_NAMES[item.icon]) || item.id)) || ''; }

  confetti(cx, cy, count, sound){
    count = count || 16;
    const shapes = ['star','heart','circle'];
    for (let i=0;i<count;i++){
      const n = UI.pic(this.root, shapes[i%3], hex(CONFETTI[i%CONFETTI.length]), cx, cy, this.U*.055, this.U*.055);
      n.setAsLast();
      const ang = (i/count)*Math.PI*2 + this.rng.next()*.4, dist = this.U*(.22 + this.rng.next()*.28);
      this.run(tw.moveTo(n, { x: cx + Math.cos(ang)*dist, y: cy + Math.sin(ang)*dist }, .8));
      this.run(this._fadeDestroy(n, .5, .5));
    }
    if (sound !== false) this.sfx('powerup');
  }
  *_fadeDestroy(n, delay, dur){ yield delay; if (!n.dead) yield* tw.fade(n, 0, dur); n.destroy(); }
  say(text, pos, color, seconds){
    const t = UI.label(this.root, text, 34, color || WHITE, pos.x, pos.y, this.W*.92, 90); t.setAsLast();
    this.run(this._sayRoutine(t, seconds == null ? 1.6 : seconds));
  }
  *_sayRoutine(t, s){ yield* tw.popIn(t, .25); yield s; if (!t.dead) yield* tw.fade(t, 0, .3); t.destroy(); }
  setWaiting(on){
    if (on){ if (this._wait) return; this._wait = UI.label(this.root, 'Chờ bạn nhé...', 34, hex('#FFE27A'), 0, this.P(.5,.045).y, this.W*.8, 45); this._wait.setAsLast(); }
    else if (this._wait){ this._wait.destroy(); this._wait = null; }
  }
}

class Game {
  constructor(host, data, opts){
    F.injectCss();
    this.data = data; this.opts = opts || {}; this.stopped = false;
    this.tasks = []; this.sharedSeed = (Math.random()*1e6) | 0;
    this.scores = [0, 0]; this.begun = [0, 0]; this.finished = [0, 0];
    host.innerHTML = '';
    const wrap = document.createElement('div'); wrap.className = 'fs-stage-wrap';
    const stage = document.createElement('div'); stage.className = 'fs-stage';
    wrap.appendChild(stage); host.appendChild(wrap);
    this.wrap = wrap; this.stage = stage;
    const hud = document.createElement('div'); hud.className = 'fs-hud';
    hud.innerHTML = '<span id="fsHudL">Đội trái: 0</span><span class="fs-ttl"></span><span id="fsHudR">Đội phải: 0</span>';
    hud.querySelector('.fs-ttl').textContent = data.title || '';
    stage.appendChild(hud); this.hudL = hud.children[0]; this.hudR = hud.children[2];
    const div = document.createElement('div'); div.className = 'fs-div'; stage.appendChild(div);
    this.worlds = [0,1].map(i=>{
      const root = new F.Node(stage, 'rect', 0, 0, 512, 500);
      root.el.style.cssText += `;left:${i*512}px;top:40px;transform:none;overflow:hidden;`;
      const W = Game.WorldClass;
      return new W(this, i, root, data);
    });
    this._fit = ()=>{ const r = wrap.getBoundingClientRect(); const k = Math.min(r.width/1024, r.height/540) || 1; stage.style.transform = `scale(${k})`; stage.style.left = Math.max(0,(r.width-1024*k)/2) + 'px'; stage.style.top = Math.max(0,(r.height-540*k)/2) + 'px'; };
    this._fit(); window.addEventListener('resize', this._fit);
    if (window.ResizeObserver){ this._ro = new ResizeObserver(this._fit); this._ro.observe(wrap); }
    this._last = performance.now();
    this._timer = setInterval(()=> this._tick(), 16);
    this.worlds.forEach((w,i)=> this.run(this._teamLoop(i)));
  }
  run(gen){ const t = { gen, wait: 0, dead: false }; this.tasks.push(t); return t; }
  _tick(){
    const now = performance.now(); let dt = (now - this._last)/1000; this._last = now; dt = Math.min(dt, .1); T.dt = dt;
    for (let i = this.tasks.length-1; i >= 0; i--){
      const t = this.tasks[i];
      if (t.dead){ this.tasks.splice(i,1); continue; }
      if (t.wait > 0){ t.wait -= dt; continue; }
      let r; try{ r = t.gen.next(); }catch(e){ console.error('[FloorStory]', e); t.dead = true; this.tasks.splice(i,1); continue; }
      if (r.done){ this.tasks.splice(i,1); continue; }
      if (typeof r.value === 'number') t.wait = r.value;
    }
    const sec = now/1000; this.worlds.forEach(w=>{ try{ if (w.update) w.update(dt, sec); }catch(e){ console.error('[FloorStory update]', e); } });
  }
  *_teamLoop(i){
    const world = this.worlds[i], other = 1 - i;
    yield .3;
    while (!this.stopped){
      const taskIdx = this.begun[i]++;
      let done = false;
      const info = {};
      const begin = ()=> world.beginTask(info, (correct, ans)=>{
        if (done) return; done = true; this.finished[i]++;
        if (correct) this.addPoint(i);
      });
      if (!world.sync || this.finished[other] >= taskIdx) begin();
      else { world.setWaiting(true); while (this.finished[other] < taskIdx && !this.stopped) yield null; world.setWaiting(false); begin(); }
      while (!done && !this.stopped) yield null;
      yield .4;
    }
  }
  addPoint(team){ this.scores[team]++; (team === 0 ? this.hudL : this.hudR).textContent = (team === 0 ? 'Đội trái: ' : 'Đội phải: ') + this.scores[team]; }
  sfx(k){ if (this.opts.mute) return; (SFX[k] || SFX.tap)(); }
  speak(text){
    if (this.opts.mute || !text || !window.speechSynthesis) return;
    try{ speechSynthesis.cancel(); const u = new SpeechSynthesisUtterance(String(text).replace(/[!?.]/g,'')); u.lang = 'vi-VN'; u.rate = .9; speechSynthesis.speak(u); }catch(e){}
  }
  imgUrl(name){ return this.opts.imgUrl ? this.opts.imgUrl(name) : null; }
  stop(){
    this.stopped = true; clearInterval(this._timer); window.removeEventListener('resize', this._fit);
    if (this._ro) this._ro.disconnect();
    try{ if (window.speechSynthesis) speechSynthesis.cancel(); }catch(e){}
    this.tasks.length = 0;
    if (this.wrap.parentNode) this.wrap.parentNode.removeChild(this.wrap);
  }
}
Game.WorldClass = null; // do fs_flow.js gán (FlowWorld)

F.World = World; F.Game = Game; F.SHAPES = SHAPES;
})();
