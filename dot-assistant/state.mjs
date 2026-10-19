import { randomUUID } from 'node:crypto';
import { readFile, writeFile, rename, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { normalizeSnapshot } from './vendor/dot-task-board/src/domain/snapshot.mjs';

export const UUID = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
const states = ['running','reviewing','waiting','blocked','partial','paused','queued','completed','canceled','unknown'];
const config = { maxTasks:100,maxAgents:0,states:Object.fromEntries(states.map(s=>[s,{}])),verification:{unknown:'unknown'},text:{subset:'selected subset'} };
const bounded = (v,n) => typeof v === 'string' ? v.replace(/[\u0000-\u001f\u007f]/g,' ').trim().slice(0,n) : '';
function checkedLink(value) {
  if (value === undefined || value === null || value === '') return null;
  const link = originalUrl(typeof value === 'string' ? value.trim() : value);
  if (!link) throw Error('invalid_link');
  return link;
}
export function originalUrl(value) {
  if (typeof value !== 'string') return null;
  if (/^codex:\/\/threads\/[a-f0-9-]{36}$/i.test(value) && UUID.test(value.split('/').at(-1))) return value;
  if (/^https:\/\/chatgpt\.com\/c\/[a-f0-9-]{36}$/i.test(value) && UUID.test(value.split('/').at(-1))) return value;
  return null;
}
export function nativeState(status) {
  if (status?.type === 'systemError') return 'blocked';
  if (status?.type === 'active') return Array.isArray(status.activeFlags)&&status.activeFlags.some(f=>['waitingOnApproval','waitingOnUserInput'].includes(f))?'waiting':'running';
  if (status?.type === 'idle') return 'paused'; // A stopped turn is not a delivered task.
  return 'unknown';
}
export class BoardState {
  constructor(now=()=>new Date().toISOString()) { this.now=now; this.tasks=[]; this.connection={state:'disconnected'}; this.language='zh-CN'; this.lastSequence=new Map(); }
  native(id,status,emittedAtMs) {
    if (!UUID.test(id) || !['active','idle','systemError','notLoaded'].includes(status?.type)) return false;
    const at=Number.isSafeInteger(emittedAtMs)?emittedAtMs:Date.parse(this.now());
    if (at> Date.parse(this.now())+30000 || at <= (this.lastSequence.get(id)??0)) return false;
    this.lastSequence.set(id,at);
    let task=this.tasks.find(t=>t.id===id && t.source==='native');
    if (!task) { if (this.tasks.length>=100) return false; task={id,source:'native',title:'',link:`codex://threads/${id}`};this.tasks.push(task); }
    Object.assign(task,{state:nativeState(status),observedAt:new Date(at).toISOString(),nativeStatus:status.type});
    return true;
  }
  loaded(ids) {
    for (const id of ids.slice(0,100)) if(UUID.test(id) && !this.tasks.some(t=>t.id===id) && this.tasks.length<100)
      this.tasks.push({id,source:'native',title:'',state:'unknown',observedAt:null,link:`codex://threads/${id}`});
  }
  create(input) {
    const text=bounded(input.text,1200);if(!text || input.text.length>1200 || this.tasks.length>=100) throw Error('invalid_task');
    const task={id:randomUUID(),source:'manual',title:bounded(input.title,80)||text.slice(0,60),text,link:checkedLink(input.link),state:'queued',observedAt:this.now(),updatedAt:this.now(),archived:false};
    this.tasks.push(task);return task;
  }
  update(id,input) {
    const task=this.tasks.find(t=>t.id===id);if(!task)throw Error('not_found');
    // Validate the entire edit before changing the existing card.
    const changes={};
    if (input.title!==undefined) changes.title=bounded(input.title,80);
    if (input.text!==undefined) {
      if(task.source==='native'||typeof input.text!=='string'||input.text.length>1200||!bounded(input.text,1200))throw Error('invalid_task');
      changes.text=bounded(input.text,1200);
      if(input.title===undefined)changes.title=changes.text.slice(0,60);
    }
    if (input.link!==undefined) changes.link=checkedLink(input.link);
    if (input.state!==undefined) {
      if(task.source==='native'||!states.includes(input.state))throw Error('read_only');
      changes.state=input.state;changes.observedAt=this.now();
    }
    if (input.archived!==undefined) {
      if(task.source==='native'||typeof input.archived!=='boolean')throw Error('read_only');
      changes.archived=input.archived;
    }
    if(task.source!=='native'&&Object.keys(changes).length)changes.updatedAt=this.now();
    Object.assign(task,changes);
    return task;
  }
  import(input) {
    // Never retain unrecognized fields, agent chatter, raw evidence or arbitrary links.
    const snapshot=normalizeSnapshot({...input,agents:[]},config);
    const tasks=snapshot.tasks.map(t=>({id:randomUUID(),source:'reported',title:t.title,state:t.state,observedAt:t.observedAt,text:t.goal,link:null}));
    if(this.tasks.length+tasks.length>100)throw Error('too_many_tasks');
    this.tasks.push(...tasks);return tasks.length;
  }
  view() { return {tasks:this.tasks.map(t=>({...t,stale:t.source==='native'&&(this.connection.state!=='connected'||!t.observedAt||Date.parse(this.now())-Date.parse(t.observedAt)>120000)})),connection:this.connection,language:this.language,readAt:this.now()}; }
  async load(filename) {
    try {
      const text=await readFile(filename,'utf8');if(text.length>300000)return;
      const data=JSON.parse(text);if(['zh-CN','zh-TW','en'].includes(data.language))this.language=data.language;
      for(const t of (Array.isArray(data.tasks)?data.tasks:[]).slice(0,100)) {
        if(!UUID.test(t.id)||!['manual','reported','native'].includes(t.source))continue;
        this.tasks.push({id:t.id,source:t.source,title:bounded(t.title,80),text:bounded(t.text,1200),state:states.includes(t.state)?t.state:'unknown',observedAt:typeof t.observedAt==='string'&&Number.isFinite(Date.parse(t.observedAt))?t.observedAt:null,updatedAt:typeof t.updatedAt==='string'&&Number.isFinite(Date.parse(t.updatedAt))?t.updatedAt:null,link:originalUrl(t.link),archived:t.source!=='native'&&t.archived===true});
      }
    } catch { /* Corrupt/read-only settings must not prevent startup. */ }
  }
  async save(filename) {
    await mkdir(path.dirname(filename),{recursive:true});
    const tmp=filename+'.tmp';
    await writeFile(tmp,JSON.stringify({language:this.language,tasks:this.tasks}),{mode:0o600});await rename(tmp,filename);
  }
}
