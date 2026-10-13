import {normalizeAgents} from './agents.mjs';
export const SCHEMA_VERSION = 'dot-board.snapshot/2';
export class SnapshotError extends Error {
  constructor(code) { super(code); this.name = 'SnapshotError'; this.code = code; }
}
const own = (v,k) => Object.prototype.hasOwnProperty.call(v,k);
const record = v => v && typeof v === 'object' && !Array.isArray(v);
const textRisk = /(?:\/workspace\/|\/user_notes\/|\/agent_notes\/|\/root\/|\/home\/|\/Users\/|[A-Za-z]:\\|\bsk-[A-Za-z0-9_-]{12,}|\bgh[pousr]_[A-Za-z0-9]{20,}|\bgithub_pat_[A-Za-z0-9_]{20,}|\bBearer\s+[A-Za-z0-9._-]{12,}|(?:access[_-]?token|api[_-]?key|authorization|password|secret)\s*[=:]|dream_notes|system\s*prompt)/i;
export function safeText(value, max = 500, fallback = '') {
  if (typeof value !== 'string') return fallback;
  const clean = value.replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/g,'').trim();
  return textRisk.test(clean) ? fallback : clean.slice(0,max);
}
export function timestamp(value) {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?Z$/.test(value)) return null;
  return Number.isFinite(Date.parse(value)) ? value : null;
}
export function safeLink(value) {
  if (typeof value !== 'string' || value.length > 1800) return null;
  try {
    const url = new URL(value);
    if (!['https:','http:'].includes(url.protocol) || url.username || url.password) return null;
    if (/^(localhost|127\.|0\.|\[|10\.|192\.168\.|169\.254\.)/i.test(url.hostname)) return null;
    if (/^172\.(1[6-9]|2\d|3[01])\./.test(url.hostname)) return null;
    if ([...url.searchParams].some(([k,v]) => /token|secret|key|signature|auth|password/i.test(k)||textRisk.test(v))) return null;
    if (textRisk.test(decodeURIComponent(url.pathname))) return null;
    const fragment=decodeURIComponent(url.hash.slice(1));
    if(textRisk.test(fragment)||/(?:^|[?&#])[^=]*(?:token|secret|key|signature|auth|password)[^=]*=/i.test(fragment))return null;
    return url.href;
  } catch { return null; }
}
function verification(value, config) {
  const v = record(value) ? value : {};
  return Object.fromEntries(['sourceReview','deployment','businessAcceptance'].map(key => {
    const row = record(v[key]) ? v[key] : {};
    const state = own(config.verification,row.state) ? row.state : 'unknown';
    return [key, {state, note:safeText(row.note,300), observedAt:timestamp(row.observedAt)}];
  }));
}
export function normalizeSnapshot(input, config) {
  if (!record(input) || !['dot-board.snapshot/1',SCHEMA_VERSION].includes(input.schemaVersion)) throw new SnapshotError('unsupported_schema');
  if (!Array.isArray(input.tasks) || input.tasks.length > config.maxTasks) throw new SnapshotError('invalid_tasks');
  if (!timestamp(input.importedAt)) throw new SnapshotError('invalid_import_time');
  const seen = new Set();
  const tasks = input.tasks.map(raw => {
    if (!record(raw)) throw new SnapshotError('invalid_task');
    const id = safeText(raw.id,100);
    if (!/^[a-zA-Z0-9_.:-]+$/.test(id) || seen.has(id)) throw new SnapshotError('invalid_task_id');
    seen.add(id);
    const title = safeText(raw.title,160);
    if (!title) throw new SnapshotError('invalid_task_title');
    const evidence = (Array.isArray(raw.evidence) ? raw.evidence : []).slice(0,8).flatMap(e => {
      const url = record(e) ? safeLink(e.url) : null;
      return url ? [{label:safeText(e.label,70,'查看证据'),url}] : [];
    });
    const state = own(config.states,raw.state) ? raw.state : 'unknown';
    return {
      id, title, ...(Number.isSafeInteger(raw.taskNumber)&&raw.taskNumber>0?{taskNumber:raw.taskNumber}:{}), project:safeText(raw.project,100,'未分类'), state,
      stage:safeText(raw.stage,100,'阶段未记录'), ownerRole:safeText(raw.ownerRole,80),
      observedAt:timestamp(raw.observedAt), observation:safeText(raw.observation,600),
      blocker:safeText(raw.blocker,400), nextAction:safeText(raw.nextAction,400), nextOwnerRole:safeText(raw.nextOwnerRole,80),
      goal:safeText(raw.goal,400), acceptanceCriteria:safeText(raw.acceptanceCriteria,400), retainedDecision:safeText(raw.retainedDecision,300),
      evidence, verification:verification(raw.verification,config),
      ...(record(raw.manualStatus)&&raw.manualStatus.source==='owner_manual'&&Number.isSafeInteger(raw.manualStatus.version)&&raw.manualStatus.version>0&&['queued','running','blocked','paused','completed','canceled'].includes(raw.manualStatus.state)&&timestamp(raw.manualStatus.updatedAt)?{manualStatus:{source:'owner_manual',version:raw.manualStatus.version,state:raw.manualStatus.state,updatedAt:timestamp(raw.manualStatus.updatedAt),reason:safeText(raw.manualStatus.reason,1200)}}:{})
    };
  });
  const coverage = record(input.coverage) ? input.coverage : {};
  const source = record(input.source) ? input.source : {};
  const agents=normalizeAgents(input.agents,tasks,config,{safeText,timestamp,safeLink,SnapshotError});
  return {
    schemaVersion:SCHEMA_VERSION, importedAt:input.importedAt,
    source:{label:safeText(source.label,140,'导入快照'), mode:source.mode === 'synthetic' ? 'synthetic' : 'reported'},
    coverage:{scope:safeText(coverage.scope,300,config.text.subset), completeness:'selected_subset', includedCount:tasks.length},
    tasks,agents
  };
}
