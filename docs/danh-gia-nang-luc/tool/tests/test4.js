const fs=require('fs');
const html=fs.readFileSync(require('path').join(__dirname,'..','bao-cao-nang-luc.html'),'utf8');
const src=html.split('<script>')[1].split('</script>')[0];
const els={};
function stub(id){return els[id]||(els[id]={id,innerHTML:'',textContent:'',value:'',style:{},hidden:false,className:'',offsetHeight:1123,clientWidth:900,classList:{toggle(){},add(){},remove(){}},addEventListener(){},querySelector:()=>stub('x'+Math.random()),querySelectorAll:()=>[],setAttribute(){},getAttribute(){return null},hasAttribute(){return false},closest(){return null},parentNode:{}})}
global.document={querySelector:s=>stub(s),querySelectorAll:()=>[]};
global.window={addEventListener(){},innerWidth:1400};
global.localStorage={_d:{},getItem(k){return this._d[k]||null},setItem(k,v){this._d[k]=v}};
global.navigator={};
eval(src);
const E=window.__edux,cfg=E.cfg(),chk=(n,c)=>console.log(c?'OK  ':'FAIL',n);
const sheet=()=>els['#sheet'].innerHTML;
E.render();
const subs=[...new Set(E.data().filter(r=>r.ind2).map(r=>r.sub))];
const ph={};E.data().filter(r=>r.ind2).forEach(r=>{(ph[r.sub]=ph[r.sub]||new Set()).add(r.phan)});
console.log(subs,Object.fromEntries(Object.entries(ph).map(([k,v])=>[k,[...v]])));
let s=sheet();
const secs=(s.match(/<h2>Môn [^<]*/g)||[]);console.log(secs);
chk('multi: TỔNG box',s.includes('<h2>TỔNG'));
chk('one section per môn',secs.length===subs.length);
chk('each subject has Điểm môn row',(s.match(/Điểm môn /g)||[]).length>=subs.length);
chk('no NaN',!/NaN|undefined|Infinity/.test(s));
console.log('radar svgs',(s.match(/<svg[^>]*Mạng nhện/g)||[]).length);
// tắt 1 môn -> không còn TỔNG
cfg.off[subs[1]]=true;E.render();s=sheet();
chk('1 môn: không TỔNG',!s.includes('<h2>TỔNG')&&s.includes('<h2>Điểm môn'));
chk('1 môn: chỉ 1 section',(s.match(/<h2>Môn /g)||[]).length===1);
// tắt 1 học phần của môn 0
cfg.off={};const p0=[...ph[subs[0]]][0];cfg.offP[subs[0]+'/'+p0]=true;E.render();s=sheet();
chk('tắt học phần '+p0,!new RegExp('<td class="l">'+p0+'</td>').test(s));
chk('no NaN after',!/NaN|undefined|Infinity/.test(s));
for(const v of ['bars','text']){cfg.skillView=v;cfg.offP={};E.render();chk(v+' view ok',!/NaN|undefined|Infinity/.test(sheet()))}
cfg.layout='one';E.render();chk('one-col ok',!/NaN|undefined|Infinity/.test(sheet()));
