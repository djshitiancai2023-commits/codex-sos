// These are observed agents. A task's free-text owner role is never an agent.
export function normalizeAgents(input,tasks,config,{safeText,timestamp,safeLink,SnapshotError}) {
  if (input === undefined) return [];
  if (!Array.isArray(input) || input.length > (config.maxAgents ?? 100)) throw new SnapshotError('invalid_agents');
  const record=v=>v&&typeof v==='object'&&!Array.isArray(v);
  const ids=new Set(),taskIds=new Set(tasks.map(t=>t.id));
  const identifier=value=>typeof value==='string'&&/^[A-Za-z0-9_.:-]{1,100}$/.test(value)&&safeText(value,100)===value?value:null;
  const list=(value,max=100)=>Array.isArray(value)?value.slice(0,max):[];
  const agents=input.map(raw=>{
    if (!record(raw)) throw new SnapshotError('invalid_agent');
    const id=identifier(raw.id),name=safeText(raw.name,100);
    if (!id || ids.has(id) || !name) throw new SnapshotError('invalid_agent_identity');
    ids.add(id);
    const activity=record(raw.activity)?raw.activity:{};
    const source=record(raw.source)?raw.source:{};
    const kind=['coordinator','owner','worker','reviewer','researcher'].includes(raw.kind)?raw.kind:'unknown';
    const type=['agent','subagent'].includes(raw.type)?raw.type:'unknown';
    const assigned=[...new Set(list(raw.taskIds).map(identifier).filter(Boolean))];
    const evidence=list(raw.latestResult?.evidence,8).flatMap(e=>{const url=record(e)?safeLink(e.url):null;return url?[{label:safeText(e.label,70,'查看成果'),url}]:[];});
    const latestSummary=record(raw.latestResult)?safeText(raw.latestResult.summary,500):'';
    return {
      id,name,role:safeText(raw.role,100),type,kind,
      parentAgentId:identifier(raw.parentAgentId),relationshipState:raw.parentAgentId===null&&type==='agent'?'known':'unknown',
      projectNames:[...new Set(list(raw.projectNames,50).map(v=>safeText(v,100)).filter(Boolean))],
      taskIds:assigned.filter(id=>taskIds.has(id)),unresolvedTaskCount:assigned.filter(id=>!taskIds.has(id)).length,
      responsibility:safeText(raw.responsibility,400),
      activity:{state:Object.hasOwn(config.agentStates,activity.state)?activity.state:'unknown',summary:safeText(activity.summary,600),observedAt:timestamp(activity.observedAt),blocker:safeText(activity.blocker,400),nextAction:safeText(activity.nextAction,400)},
      latestResult:latestSummary?{summary:latestSummary,observedAt:timestamp(raw.latestResult.observedAt),evidence}:null,
      source:{kind:['host_observation','verified_report'].includes(source.kind)?source.kind:'reported',label:safeText(source.label,120,'导入记录'),observedAt:timestamp(source.observedAt)}
    };
  });
  for(const agent of agents){
    if(agent.parentAgentId&&ids.has(agent.parentAgentId))agent.relationshipState='known';
    else if(agent.parentAgentId){agent.parentAgentId=null;agent.relationshipState='unknown';}
  }
  const byId=new Map(agents.map(a=>[a.id,a]));
  for(const start of agents){
    const chain=new Set();let node=start;
    while(node){if(chain.has(node.id))throw new SnapshotError('invalid_agent_hierarchy');chain.add(node.id);node=byId.get(node.parentAgentId);}
  }
  return agents;
}
