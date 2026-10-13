import { spawn } from 'node:child_process';
import { readFile, mkdtemp, rm } from 'node:fs/promises';
import path from 'node:path';

export function notificationFilter(serverSchema,clientSchema) {
  if (!clientSchema.definitions?.InitializeCapabilities?.properties?.optOutNotificationMethods) throw Error('incompatible');
  const names=serverSchema.oneOf?.map(x=>x.properties?.method?.enum?.[0]);
  if(!names || names.length<5 || names.some(x=>typeof x!=='string') || !names.includes('thread/status/changed'))throw Error('incompatible');
  return names.filter(n=>n!=='thread/status/changed');
}
export function runBounded(exe,args,options={}) {
  return new Promise((resolve,reject)=>{
    const p=spawn(exe,args,{windowsHide:true,stdio:['ignore','pipe','ignore'],...options});let text='';
    const timer=setTimeout(()=>{p.kill();reject(Error('timeout'));},15000);
    p.stdout.on('data',b=>{text+=b;if(text.length>1000000){p.kill();reject(Error('oversized'));}});
    p.on('error',()=>{clearTimeout(timer);reject(Error('unavailable'));});
    p.on('exit',code=>{clearTimeout(timer);code===0?resolve(text):reject(Error('unavailable'));});
  });
}
export class MetadataObserver {
  constructor({exe,dataDir,board,onChange=()=>{},onFault=()=>{},run=runBounded,createSocket=url=>new WebSocket(url)}) {Object.assign(this,{exe,dataDir,board,onChange,onFault,run,createSocket});this.generation=0;this.timers=new Set();}
  close() {
    this.generation++;for(const t of this.timers)clearTimeout(t);this.timers.clear();
    // Kill only our proxy client, never the user's daemon/task/process tree.
    this.proxy?.kill();this.proxy=null;this.ws?.close();this.ws=null;
    this.board.connection={state:'disconnected'};this.onChange();
  }
  async connect(endpoint=null) {
    this.close();const generation=this.generation;
    if(!this.exe){this.board.connection={state:'unavailable'};this.onChange();return;}
    this.board.connection={state:'connecting'};this.onChange();let schemaDir;
    try {
      schemaDir=await mkdtemp(path.join(this.dataDir,'protocol-'));
      const version=(await this.run(this.exe,['--version'])).trim().replace(/^codex-cli /,'');
      await this.run(this.exe,['app-server','generate-json-schema','--experimental','--out',schemaDir]);
      const suppress=notificationFilter(JSON.parse(await readFile(path.join(schemaDir,'ServerNotification.json'),'utf8')),JSON.parse(await readFile(path.join(schemaDir,'ClientRequest.json'),'utf8')));
      if(this.generation!==generation)return;
      let send;let buffer='';let initialized=false;let sequence=1;
      const fail=(state)=>{if(this.generation!==generation)return;this.close();this.board.connection={state};this.onChange();};
      const receive=(message)=>{
        if(this.generation!==generation)return;
        let m;try{m=JSON.parse(message);}catch{return fail('incompatible');}
        if(m.id===1) {
          // Require server/CLI version agreement before requesting any task identifiers.
          if(!m.result?.userAgent?.includes('/'+version+' '))return fail('incompatible');
          initialized=true;this.board.connection={state:'connected',version,source:endpoint?'local-endpoint':'local-daemon'};
          send({method:'initialized'});send({id:++sequence,method:'thread/loaded/list',params:{limit:100}});this.onChange();
        } else if(m.id===2) {if(!initialized||!Array.isArray(m.result?.data))return fail('incompatible');this.board.loaded(m.result.data);this.onChange();}
        else if(m.method==='thread/status/changed'&&initialized) {
          const p=m.params;
          if(this.board.native(p?.threadId,p?.status,m.emittedAtMs)) {this.onChange();if(p.status.type==='systemError')this.onFault();}
        } else if(m.method) {
          // Unexpected payload is never displayed, retained, logged or forwarded.
          // Version/schema mismatch is disconnected instead of broadening reads.
          fail('incompatible');
        }
      };
      if(endpoint) {
        if(!/^ws:\/\/127\.0\.0\.1:[1-9]\d{0,4}\/?$/.test(endpoint)||new URL(endpoint).port>65535)throw Error('invalid_endpoint');
        const ws=this.createSocket(endpoint);this.ws=ws;
        await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('unavailable')),4000);ws.onopen=()=>{clearTimeout(timer);resolve();};ws.onerror=()=>{clearTimeout(timer);reject(Error('unavailable'));};});
        if(this.generation!==generation){ws.close();return;}
        ws.onmessage=e=>{if(e.data.length>200000)fail('incompatible');else receive(e.data);};ws.onclose=()=>fail('disconnected');
        send=m=>ws.send(JSON.stringify(m));
      } else {
        const p=spawn(this.exe,['app-server','proxy'],{windowsHide:true,stdio:['pipe','pipe','ignore']});this.proxy=p;
        p.on('error',()=>fail('unavailable'));p.on('exit',()=>fail('unavailable'));
        p.stdout.on('data',b=>{buffer+=b;if(buffer.length>200000)return fail('incompatible');let at;while((at=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,at);buffer=buffer.slice(at+1);if(line)receive(line);}});
        p.stdin.on('error',()=>fail('unavailable'));send=m=>p.stdin.write(JSON.stringify(m)+'\n');
      }
      send({id:1,method:'initialize',params:{clientInfo:{name:'codex_sos_metadata',version:'0.2.0'},capabilities:{experimentalApi:true,requestAttestation:false,optOutNotificationMethods:suppress}}});
      const timer=setTimeout(()=>{this.timers.delete(timer);if(!initialized)fail('unavailable');},5000);this.timers.add(timer);
    } catch {if(generation===this.generation){this.close();this.board.connection={state:'unavailable'};this.onChange();}}
    finally {if(schemaDir)await rm(schemaDir,{recursive:true,force:true}).catch(()=>{});} // only our freshly created temp directory
  }
}
