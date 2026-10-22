// Đối chiếu bản JS (docs/apps-script/Aggregate.gs) với Python (web/tools/build_report_json.py) trên CÙNG thư mục log.
//   node test_aggregate_parity.js <GameLogs> <từ> <đến>        vd: node test_aggregate_parity.js C:/.../GameLogs 2026-10-01 2026-10-22
// Không in tên bé. Thoát mã 1 nếu có số lệch > 0,11 (sai số làm tròn) hoặc khác cấu trúc.
const fs = require('fs'), path = require('path'), cp = require('child_process'), os = require('os');
const [logs, dfrom, dto] = process.argv.slice(2);
if (!logs || !dfrom || !dto) { console.error('Dùng: node test_aggregate_parity.js <GameLogs> <từ> <đến>'); process.exit(2); }
const root = path.resolve(__dirname, '..', '..', '..', '..');
const cfgPath = path.join(root, 'docs', 'danh-gia-nang-luc', 'tool', 'pipeline', 'config.json');
const cfg = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));

const mod = { exports: {} };
new Function('module', fs.readFileSync(path.join(root, 'docs', 'apps-script', 'Aggregate.gs'), 'utf8'))(mod);
const A = mod.exports;

const byname = A.edxByName_(cfg), bases = {};
fs.readdirSync(logs).filter(f => /^\d{4}-\d{2}-\d{2}_\d{6}_.+\.json$/.test(f)).forEach(f => {
  let d; try { d = JSON.parse(fs.readFileSync(path.join(logs, f), 'utf8').replace(/^\uFEFF/, '')); } catch (e) { return; }
  bases[f] = { rows: A.edxExtractFile_(cfg, byname, f, d).rows };
});
const js = A.edxBuildReport_(cfg, A.edxFlatten_(bases), dfrom, dto);

const tmp = path.join(os.tmpdir(), 'edux_parity_py.json');
cp.execFileSync('python', ['-I', '-X', 'utf8', path.join(root, 'docs', 'danh-gia-nang-luc', 'web', 'tools', 'build_report_json.py'),
  '--local', logs, '--from', dfrom, '--to', dto, '--out', tmp], { stdio: 'inherit' });
const py = JSON.parse(fs.readFileSync(tmp, 'utf8'));

let bad = 0, nnum = 0;
function cmp(a, b, p) {
  if (typeof a === 'number' && typeof b === 'number') { nnum++; if (Math.abs(a - b) > 0.11) { if (bad++ < 15) console.log('LỆCH', p.replace(/CASA-\d+/, 'CASA-*'), a, b); } return; }
  if (a === null || b === null || typeof a !== 'object' || typeof b !== 'object') { if (a !== b) { if (bad++ < 15) console.log('KHÁC', p.replace(/CASA-\d+/, 'CASA-*'), String(a).slice(0, 30), String(b).slice(0, 30)); } return; }
  if (Array.isArray(a) !== Array.isArray(b)) { bad++; console.log('KIỂU', p); return; }
  const ka = Object.keys(a).sort(), kb = Object.keys(b).sort();
  if (ka.join() !== kb.join()) { bad++; console.log('KHÓA', p, ka.filter(k => !kb.includes(k)), kb.filter(k => !ka.includes(k))); return; }
  ka.forEach(k => cmp(a[k], b[k], p + '/' + k));
}
cmp(js, py, '');
const scored = js.students.filter(s => Object.keys(s.mon).length).length;
console.log(`so ${nnum} số; ${js.students.length} bé (${scored} có điểm), ${js.weeks.length} tuần; ${bad ? bad + ' LỆCH' : 'KHỚP'}`);
process.exit(bad ? 1 : 0);
