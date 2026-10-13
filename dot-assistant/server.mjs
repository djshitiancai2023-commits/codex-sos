import { createServer } from 'node:http';
import { randomBytes } from 'node:crypto';
import { readFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { BoardState, originalUrl } from './state.mjs';
import { MetadataObserver } from './observer.mjs';

export async function startBoard({dataDir,bridge,exe=null,now,port=0,fixture=false}) {
  await mkdir(dataDir,{recursive:true});
  const board=new BoardState(now),filename=path.join(dataDir,'tasks.json');await board.load(filename);
  const token=randomBytes(32).toString('base64url');
  let saveQueue=Promise.resolve(),saveFailed=false,diagnostic=null,checking=false,lastFault=0,lastBrowser=Date.now();
  const save=()=>{saveQueue=saveQueue.catch(()=>{}).then(()=>board.save(filename)).then(()=>{saveFailed=false;},()=>{saveFailed=true;});};
  const diagnose=async(language=board.language)=>{
    if(checking)return;checking=true;diagnostic=null;
    try {diagnostic=await bridge('diagnose',{language});}catch{diagnostic={error:true};}finally{checking=false;}
  };
  const observer=new MetadataObserver({exe,dataDir,board,onChange:save,onFault:()=>{
    if(Date.now()-lastFault<60000)return;lastFault=Date.now();void diagnose();
  }});
  const files=new Map(await Promise.all(['index.html','app.js','styles.css'].map(async name=>[name,await readFile(path.join(import.meta.dirname,'public',name))])));
  const server=createServer(async(req,res)=>{
    const origin=`http://127.0.0.1:${server.address().port}`;
    const reply=(status,data)=>{res.writeHead(status,{'content-type':'application/json; charset=utf-8'});res.end(JSON.stringify(data));};
    res.setHeader('Cache-Control','no-store');res.setHeader('X-Content-Type-Options','nosniff');
    res.setHeader('Content-Security-Policy',"default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; base-uri 'none'; frame-ancestors 'none'; form-action 'none'");
    res.setHeader('Referrer-Policy','no-referrer');
    if(req.headers.host!==`127.0.0.1:${server.address().port}`||req.socket.remoteAddress!=='127.0.0.1'||req.headers['sec-fetch-site']==='cross-site'||(req.headers.origin&&req.headers.origin!==origin))return reply(403,{error:'local_only'});
    if(req.method==='GET'&&['/','/app.js','/styles.css'].includes(req.url)){
      const name=req.url==='/'?'index.html':req.url.slice(1);res.setHeader('Content-Type',name.endsWith('.js')?'text/javascript':name.endsWith('.css')?'text/css':'text/html; charset=utf-8');return res.end(files.get(name));
    }
    if(req.headers['x-sos-token']!==token)return reply(403,{error:'restart'});
    if(req.method==='GET'&&req.url==='/api/board') {lastBrowser=Date.now();return reply(200,{...board.view(),diagnostic,checking,saveFailed,fixture});}
    if(req.method!=='POST'||req.headers['content-type']!=='application/json')return reply(400,{error:'invalid_request'});
    try {
      const chunks=[];let size=0;for await(const chunk of req){size+=chunk.length;if(size>200000)throw Error('too_large');chunks.push(chunk);}
      const input=JSON.parse(Buffer.concat(chunks).toString('utf8')||'{}');let result={ok:true};
      switch(req.url) {
        case '/api/task': result={task:board.create(input)};save();break;
        case '/api/update': result={task:board.update(input.id,input)};save();break;
        case '/api/import': result={count:board.import(input)};save();break;
        case '/api/language':
          if(!['zh-CN','zh-TW','en'].includes(input.language))throw Error('invalid_language');
          board.language=input.language;save();await bridge('language',{language:input.language});break;
        case '/api/connect': if(board.connection.state!=='connecting')void observer.connect(input.endpoint||null);break;
        case '/api/disconnect': observer.close();break;
        case '/api/diagnose': void diagnose();break;
        case '/api/diagnostic-language': if(diagnostic&&!diagnostic.error)diagnostic=await bridge('report',{language:board.language});break;
        case '/api/copy-report':
          if(!diagnostic||diagnostic.error||checking)throw Error('no_report');
          if(input.reportId!==diagnostic.id||input.previewId!==diagnostic.previewId)throw Error('stale_report');
          await bridge('copy-report',{language:diagnostic.language,reportId:diagnostic.id,previewId:diagnostic.previewId});break;
        case '/api/classic': await bridge('classic',{language:board.language});break;
        case '/api/open': {
          const task=board.tasks.find(t=>t.id===input.id);
          const link=task?originalUrl(task.link):null;
          await bridge('open',{url:link??'https://chatgpt.com/'});break;
        }
        case '/api/quit': reply(200,{ok:true});setTimeout(()=>void bridge('quit',{}).catch(()=>{}),100);return;
        default:return reply(404,{error:'not_found'});
      }
      await saveQueue;if(saveFailed&&['/api/task','/api/update','/api/import'].includes(req.url))throw Error('save_failed');
      reply(200,result);
    }catch{reply(400,{error:'operation_failed'});}
  });
  server.requestTimeout=10000;server.headersTimeout=10000;server.maxConnections=20;
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(port,'127.0.0.1',resolve);});
  const url=`http://127.0.0.1:${server.address().port}/#${token}`;
  // Browser closure releases the helper after a short grace period. This never stops Codex.
  const idle=setInterval(()=>{if(Date.now()-lastBrowser>180000&&!checking)void bridge('quit',{idle:true}).catch(()=>{});},30000);idle.unref();
  server.on('close',()=>{clearInterval(idle);observer.close();});
  if(exe)void observer.connect();
  return {server,board,observer,url,token,setReport:report=>{diagnostic=report;if(['zh-CN','zh-TW','en'].includes(report.language)){board.language=report.language;save();}},close:async()=>{observer.close();await saveQueue;await new Promise(r=>server.close(r));}};
}
