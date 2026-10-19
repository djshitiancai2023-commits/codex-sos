import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,rm,readFile,writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {BoardState,nativeState,originalUrl} from '../state.mjs';
import {notificationFilter,MetadataObserver} from '../observer.mjs';
import {startBoard} from '../server.mjs';
import {selectTasks,continuationDraft} from '../public/task-view.mjs';

// All task text, identifiers and snapshots below are fictional.
const id='00000000-0000-4000-8000-000000000001',at='2026-10-13T00:00:00.000Z';
test('stopped turn never becomes completed delivery',()=>{assert.equal(nativeState({type:'idle'}),'paused');assert.equal(nativeState({type:'active',activeFlags:['waitingOnApproval']}),'waiting');assert.equal(nativeState({type:'systemError'}),'blocked');});
test('official source fields are projected; duplicate and late events do not advance time',()=>{
  const b=new BoardState(()=>at);assert.ok(b.native(id,{type:'active',activeFlags:[],prompt:'FICTIONAL_NOT_ALLOWED'},Date.parse(at)));
  assert.equal(b.native(id,{type:'idle'},Date.parse(at)-1),false);assert.equal(b.native(id,{type:'idle'},Date.parse(at)),false);
  assert.equal(b.tasks[0].state,'running');assert.ok(!JSON.stringify(b.view()).includes('FICTIONAL_NOT_ALLOWED'));
});
test('refresh time and observation time are separate; stale is not blocked',()=>{
  let time=at;const b=new BoardState(()=>time);b.native(id,{type:'active'},Date.parse(at));time='2026-10-13T01:00:00.000Z';
  assert.equal(b.view().tasks[0].state,'running');assert.equal(b.view().tasks[0].stale,true);assert.equal(b.view().tasks[0].observedAt,at);
});
test('loaded identifier alone is not evidence of running/completed',()=>{const b=new BoardState();b.loaded([id,'../bad']);assert.equal(b.tasks.length,1);assert.equal(b.tasks[0].state,'unknown');assert.equal(b.tasks[0].observedAt,null);});
test('manual changes cannot impersonate native host state',()=>{const b=new BoardState(()=>at);b.native(id,{type:'active'},Date.parse(at));assert.throws(()=>b.update(id,{state:'completed'}));b.update(id,{title:'Fictional task'});assert.equal(b.tasks[0].state,'running');});
test('snapshot normalization is reused, but imported completion stays reported',()=>{
  const b=new BoardState(()=>at);b.import({schemaVersion:'dot-board.snapshot/2',importedAt:at,tasks:[{id:'demo',title:'Fictional task',state:'completed',observedAt:at,goal:'Prepare fictional draft',prompt:'DO_NOT_RETAIN',evidence:[{url:'https://example.com'}]}]});
  assert.equal(b.tasks[0].source,'reported');assert.equal(b.tasks[0].link,null);assert.ok(!JSON.stringify(b.view()).includes('DO_NOT_RETAIN'));
});
test('unbounded imports and unsafe original links rejected',()=>{
  for(const url of ['https://example.com','https://chatgpt.com/c/'+id+'?token=fake','codex://threads/'+id+'/more','file:///tmp/a','https://chatgpt.com@evil.test/c/'+id])assert.equal(originalUrl(url),null);
  assert.equal(originalUrl('codex://threads/'+id),'codex://threads/'+id);
  assert.throws(()=>new BoardState().import({schemaVersion:'unknown',tasks:[]}));
});
test('schema-derived filtering excludes all non-state event methods',()=>{
  const schema={oneOf:['thread/status/changed','item/started','thread/started','account/updated','item/agentMessage/delta'].map(m=>({properties:{method:{enum:[m]}}}))};
  const client={definitions:{InitializeCapabilities:{properties:{optOutNotificationMethods:{}}}}};
  assert.deepEqual(notificationFilter(schema,client),['item/started','thread/started','account/updated','item/agentMessage/delta']);assert.throws(()=>notificationFilter(schema,{}));
});
test('own persisted records survive restart; corruption does not block startup',async()=>{
  const dir=await mkdtemp(path.join(os.tmpdir(),'sos-board-test-'));try{
    const file=path.join(dir,'tasks.json');const b=new BoardState(()=>at);b.create({text:'Fictional test'});b.language='en';await b.save(file);
    const loaded=new BoardState();await loaded.load(file);assert.equal(loaded.language,'en');assert.equal(loaded.tasks[0].text,'Fictional test');
    await writeFile(file,'broken');const corrupt=new BoardState();await corrupt.load(file);assert.equal(corrupt.tasks.length,0);
  }finally{await rm(dir,{recursive:true,force:true});}
});
test('loopback service security and copy/open semantics',async()=>{
  const dir=await mkdtemp(path.join(os.tmpdir(),'sos-board-http-')),calls=[];
  const app=await startBoard({dataDir:dir,fixture:true,bridge:async(method,params)=>{calls.push({method,params});return {id:'report-1',previewId:'preview-1',language:'zh-CN',text:'SANITIZED',privacy:'review',answers:['a','b','c','d']};}});
  const origin=app.url.split('/#')[0],headers={'x-sos-token':app.token,'content-type':'application/json'};
  const post=(route,body={},extra={})=>fetch(origin+'/api/'+route,{method:'POST',headers:{...headers,...extra},body:JSON.stringify(body)});
  try {
    assert.equal((await fetch(origin+'/api/board')).status,403);
    assert.equal((await post('task',{text:'evil'},{origin:'https://evil.example'})).status,403);
    assert.equal((await post('task',{text:'evil'},{'sec-fetch-site':'cross-site'})).status,403);
    assert.equal((await fetch(origin+'/../state.mjs',{headers})).status,400);
    assert.equal((await post('execute',{command:'unsafe'})).status,404);
    const created=await(await post('task',{text:'Fictional dot job',prompt:'no',source:'native'})).json();
    assert.equal(created.task.source,'manual');assert.equal(calls.length,0);
    assert.equal((await post('copy-report')).status,400);assert.equal(calls.length,0);
    await post('open',{id:created.task.id});assert.deepEqual(calls.at(-1),{method:'open',params:{url:'https://chatgpt.com/'}});
    await post('diagnose');for(let n=0;n<20;n++){if(!(await(await fetch(origin+'/api/board',{headers})).json()).checking)break;await new Promise(r=>setTimeout(r,10));}
    assert.equal((await post('copy-report',{reportId:'stale'})).status,400);
    assert.equal((await post('copy-report',{reportId:'report-1',previewId:'stale'})).status,400);
    assert.equal((await post('copy-report',{reportId:'report-1',previewId:'preview-1'})).status,200);assert.equal(calls.at(-1).method,'copy-report');
    assert.equal(calls.at(-1).params.language,'zh-CN');
    assert.ok(!calls.some(c=>/send|submit|turn\/start/.test(c.method)));
    assert.ok(!(await readFile(path.join(dir,'tasks.json'),'utf8')).includes('SANITIZED'));
  }finally{await app.close();await rm(dir,{recursive:true,force:true});}
});
test('only explicitly allowlisted calls are present in status observer',async()=>{
  const source=await readFile(new URL('../observer.mjs',import.meta.url),'utf8');
  for(const forbidden of ['thread/list','thread/read','thread/resume','turn/start','thread/turns','auth.json','transcript_path','tool_input'])assert.ok(!source.includes(forbidden),forbidden);
  assert.ok(source.includes('thread/loaded/list'));assert.ok(source.includes('thread/status/changed'));
});

test('invalid state flags and overlong notes do not crash or silently trim',()=>{
  assert.equal(nativeState({type:'active',activeFlags:{unexpected:true}}),'running');
  const board=new BoardState();assert.throws(()=>board.create({text:'字'.repeat(1201)}));
  assert.equal(board.tasks.length,0);board.create({text:'字'.repeat(1200)});assert.equal(board.tasks[0].text.length,1200);
});

test('production observer fails closed on version mismatch and forbidden notifications',async()=>{
 const dir=await mkdtemp(path.join(os.tmpdir(),'sos-observer-fixture-'));
 const names=['thread/status/changed','item/started','thread/started','account/updated','item/agentMessage/delta'];
 const runner=async(_exe,args)=>{
   if(args[0]==='--version')return 'codex-cli 1.2.3';
   const out=args.at(-1);
   await writeFile(path.join(out,'ServerNotification.json'),JSON.stringify({oneOf:names.map(n=>({properties:{method:{enum:[n]}}}))}));
   await writeFile(path.join(out,'ClientRequest.json'),JSON.stringify({definitions:{InitializeCapabilities:{properties:{optOutNotificationMethods:{}}}}}));return '';
 };
 for(const mismatch of [true,false]){
   const board=new BoardState(),calls=[];let socket;
   const factory=()=>{socket={close(){},send(text){const m=JSON.parse(text);calls.push(m);if(m.id===1)queueMicrotask(()=>socket.onmessage({data:JSON.stringify({id:1,result:{userAgent:`fictional/${mismatch?'9.9.9':'1.2.3'} (Windows)`}})}));if(m.id===2)queueMicrotask(()=>socket.onmessage({data:JSON.stringify({id:2,result:{data:[id]}})}));}};queueMicrotask(()=>socket.onopen());return socket;};
   const observer=new MetadataObserver({exe:'fictional-not-executed',dataDir:dir,board,run:runner,createSocket:factory});
   try{
     await observer.connect('ws://127.0.0.1:12345');await new Promise(r=>setImmediate(r));
     assert.deepEqual(calls[0].params.capabilities.optOutNotificationMethods,names.slice(1));
     if(mismatch){assert.equal(board.connection.state,'incompatible');assert.equal(calls.length,1);}
     else{
       assert.equal(board.tasks[0].state,'unknown');assert.equal(board.tasks[0].observedAt,null);
       socket.onmessage({data:JSON.stringify({method:'thread/status/changed',params:{threadId:id,status:{type:'active',activeFlags:['waitingOnApproval']}},emittedAtMs:Date.now()})});
       assert.equal(board.tasks[0].state,'waiting');
       socket.onmessage({data:JSON.stringify({method:'item/agentMessage/delta',params:{delta:'FICTIONAL_PRIVATE_CANARY'}})});
       assert.equal(board.connection.state,'incompatible');assert.ok(!JSON.stringify(board).includes('FICTIONAL_PRIVATE_CANARY'));
       assert.deepEqual(calls.map(c=>c.method),['initialize','initialized','thread/loaded/list']);
     }
   }finally{observer.close();}
 }
 await rm(dir,{recursive:true,force:true});
});

test('editing a note preserves its identity and state; invalid edits are atomic',()=>{
  let time=at;const b=new BoardState(()=>time),task=b.create({text:'Fictional draft'});
  b.update(task.id,{state:'waiting'});const before=JSON.stringify(task);
  assert.throws(()=>b.update(task.id,{text:'This must not be applied',link:'https://evil.example/'}));
  assert.equal(JSON.stringify(task),before);
  assert.throws(()=>b.update(task.id,{text:'字'.repeat(1201)}));assert.equal(JSON.stringify(task),before);
  assert.throws(()=>b.update(task.id,{text:'  '}));assert.equal(JSON.stringify(task),before);
  time='2026-10-19T00:00:00.000Z';
  b.update(task.id,{text:'Fictional revised draft',link:'https://chatgpt.com/c/'+id});
  assert.equal(task.id,b.tasks[0].id);assert.equal(task.source,'manual');assert.equal(task.state,'waiting');
  assert.equal(task.text,'Fictional revised draft');assert.equal(task.title,'Fictional revised draft');
  assert.equal(task.observedAt,at);assert.equal(task.updatedAt,time);assert.equal(task.link,'https://chatgpt.com/c/'+id);
});

test('invalid create links preserve all existing cards and native records stay read-only',()=>{
  const b=new BoardState(()=>at);b.create({text:'Keep this fictional note'});const before=JSON.stringify(b.tasks);
  assert.throws(()=>b.create({text:'Rejected fictional note',link:'https://chatgpt.com/c/'+id+'?secret=fake'}));
  assert.equal(JSON.stringify(b.tasks),before);
  b.native(id,{type:'active'},Date.parse(at));const nativeBefore=JSON.stringify(b.tasks[1]);
  for(const change of [{text:'overwrite'},{archived:true},{state:'completed'}])assert.throws(()=>b.update(id,change));
  assert.equal(JSON.stringify(b.tasks[1]),nativeBefore);
});

test('archive and restore survive restart without deleting a note or changing runtime state',async()=>{
  const dir=await mkdtemp(path.join(os.tmpdir(),'sos-board-archive-'));try{
    const file=path.join(dir,'tasks.json'),b=new BoardState(()=>at),task=b.create({text:'Fictional archive note'});
    b.update(task.id,{state:'running'});b.update(task.id,{archived:true});await b.save(file);
    const loaded=new BoardState();await loaded.load(file);
    assert.equal(selectTasks(loaded.view().tasks).length,0);assert.equal(selectTasks(loaded.view().tasks,{filter:'archived'}).length,1);
    assert.equal(loaded.tasks[0].state,'running');loaded.update(task.id,{archived:false});
    assert.equal(selectTasks(loaded.view().tasks).length,1);assert.equal(loaded.tasks[0].text,'Fictional archive note');
    assert.throws(()=>loaded.update(task.id,{archived:'false'}));
  }finally{await rm(dir,{recursive:true,force:true});}
});

test('manual notes do not become stale runtime observations; disconnect invalidates even a fresh native state',()=>{
  let time=at;const b=new BoardState(()=>time),note=b.create({text:'Fictional manual record'});
  b.native(id,{type:'active'},Date.parse(at));b.connection={state:'connected'};
  assert.equal(b.view().tasks.find(t=>t.id===id).stale,false);
  b.connection={state:'disconnected'};
  assert.equal(b.view().tasks.find(t=>t.id===id).stale,true);
  assert.equal(selectTasks(b.view().tasks,{filter:'running'}).length,0);
  assert.equal(selectTasks(b.view().tasks,{filter:'other'}).length,2);
  time='2026-10-19T00:00:00.000Z';assert.equal(b.view().tasks.find(t=>t.id===note.id).stale,false);
});

test('search combines category and archive filters and reads only note title/text',()=>{
  const tasks=[
    {id:'1',state:'running',title:'Fictional Alpha',text:'整理书单',source:'manual'},
    {id:'2',state:'waiting',title:'Fictional Beta',text:'检查版本',source:'manual'},
    {id:'3',state:'running',title:'Fictional Alpha archive',text:'书单',archived:true,source:'manual'},
    {id:'4',state:'blocked',title:'Fictional Delta',text:'notes',link:'https://example.com/SECRET',source:'reported'}
  ];
  assert.deepEqual(selectTasks(tasks,{query:' ALPHA '}).map(t=>t.id),['1']);
  assert.deepEqual(selectTasks(tasks,{filter:'archived',query:'书单'}).map(t=>t.id),['3']);
  assert.equal(selectTasks(tasks,{filter:'waiting',query:'Alpha'}).length,0);
  assert.deepEqual(selectTasks(tasks,{filter:'waiting'}).map(t=>t.id),['2']);
  assert.equal(selectTasks(tasks,{query:'SECRET'}).length,0);
  assert.equal(selectTasks(tasks,{query:'not present'}).length,0);
});

test('three-language continuation drafts identify original work and never include unrelated imported fields',()=>{
  const task={text:'Fictional project note',title:'Fictional title',link:'https://chatgpt.com/c/'+id,prompt:'FICTIONAL_PRIVATE_CANARY',token:'FICTIONAL_SECRET_CANARY'};
  for(const language of ['zh-CN','zh-TW','en']){
    const progress=continuationDraft(task,language,'progress'),resume=continuationDraft(task,language,'continue');
    for(const draft of [progress,resume]){
      assert.ok(draft.includes(task.text));assert.ok(draft.includes(task.link));assert.ok(!draft.includes('CANARY'));
    }
    assert.notEqual(progress,resume);
  }
  assert.match(continuationDraft({text:'Fictional note'},'en'),/If it is unclear, ask me; do not create or rerun/);
  assert.match(continuationDraft(task,'en','continue'),/without repeating completed actions/);
  assert.throws(()=>continuationDraft(task,'en','execute'));
});

test('note edit and archive HTTP actions are local-only; new browser module has a JavaScript MIME type',async()=>{
  const dir=await mkdtemp(path.join(os.tmpdir(),'sos-board-edit-http-')),calls=[];
  const app=await startBoard({dataDir:dir,fixture:true,bridge:async(method)=>{calls.push(method);return {};}});
  const origin=app.url.split('/#')[0],headers={'x-sos-token':app.token,'content-type':'application/json'};
  const post=(route,body)=>fetch(origin+'/api/'+route,{method:'POST',headers,body:JSON.stringify(body)});
  try{
    const module=await fetch(origin+'/task-view.mjs');assert.equal(module.status,200);assert.match(module.headers.get('content-type'),/javascript/);
    const created=await(await post('task',{text:'Fictional original'})).json();
    assert.equal((await post('update',{id:created.task.id,text:'Fictional edited',archived:true})).status,200);
    let state=await(await fetch(origin+'/api/board',{headers})).json();assert.equal(state.tasks[0].archived,true);assert.equal(state.tasks[0].text,'Fictional edited');
    const rejected=await post('update',{id:created.task.id,text:'MUST_NOT_APPLY',link:'https://evil.example'});assert.equal(rejected.status,400);assert.equal((await rejected.json()).error,'invalid_link');
    await post('update',{id:created.task.id,archived:false});
    state=await(await fetch(origin+'/api/board',{headers})).json();assert.equal(state.tasks[0].text,'Fictional edited');assert.equal(state.tasks[0].archived,false);
    assert.deepEqual(calls,[]);
    const saved=JSON.parse(await readFile(path.join(dir,'tasks.json'),'utf8'));assert.equal(saved.tasks[0].text,'Fictional edited');
  }finally{await app.close();await rm(dir,{recursive:true,force:true});}
});
