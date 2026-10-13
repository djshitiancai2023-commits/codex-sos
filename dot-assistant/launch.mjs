import { createInterface } from 'node:readline';
import { startBoard } from './server.mjs';

let sequence=0,app;const pending=new Map();
const send=value=>process.stdout.write(JSON.stringify(value)+'\n');
const lines=createInterface({input:process.stdin});
lines.on('line',line=>{try{const m=JSON.parse(line);if(m.method==='report-ready'){app?.setReport(m.result);return;}const p=pending.get(m.id);if(p){pending.delete(m.id);clearTimeout(p.timer);m.error?p.reject(Error('native_failed')):p.resolve(m.result);}}catch{}});
const bridge=(method,params)=>new Promise((resolve,reject)=>{const id=++sequence;const timer=setTimeout(()=>{pending.delete(id);reject(Error('timeout'));},90000);pending.set(id,{resolve,reject,timer});send({id,method,params});});
app=await startBoard({dataDir:process.env.SOS_BOARD_DATA,exe:process.env.SOS_CODEX_EXE||null,bridge,fixture:process.env.SOS_BOARD_FIXTURE==='1'});
send({method:'ready',url:app.url});
lines.on('close',()=>{void app.close().finally(()=>process.exit(0));});
