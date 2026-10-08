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
const E=window.__edux,cfg=E.cfg();
const chk=(n,c)=>console.log(c?'OK  ':'FAIL',n);
const sheet=()=>els['#sheet'].innerHTML;
// no NaN in all default render
E.render();chk('render ok',!/NaN|undefined|Infinity/.test(sheet()));
// scoring via direct data
function mk(ok,t,n){return Array.from({length:n},(_,i)=>({ts:Date.now()+i,sub:'Toán',phan:'Đếm',ok:i<ok,t:t,stn:'A',ind2:true}))}
const sc=(rs)=>E.scoreOf(rs).phans[0];
let p=sc(mk(4,3,4));  console.log(p.pct,p.k);chk('100% đúng, 3s -> 100',Math.round(p.pct)===100&&p.k===1.25);
p=sc(mk(4,30,4));    chk('100% đúng, chậm -> 80',Math.round(p.pct)===80&&p.k===1);
p=sc(mk(1,1,4));     chk('25% đúng, rất nhanh -> 25',Math.round(p.pct)===25);
p=sc(mk(1,30,4));    chk('25% đúng, chậm -> 20',Math.round(p.pct)===20);
p=sc(mk(2,12.5,4));  console.log(p.pct,p.k);chk('50% , 12.5s -> k=1.125, 45',Math.abs(p.k-1.125)<1e-9&&Math.abs(p.pct-45)<1e-9);
p=sc(mk(0,5,4));     chk('0 đúng -> 0',p.pct===0);
E.formulaBox();console.log(els['#formula'].innerHTML);
