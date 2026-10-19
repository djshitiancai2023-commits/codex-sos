// Shared by the page and regression tests. No network, storage or task execution.
export const languages=['zh-CN','zh-TW','en'];
export const taskGroup=state=>['running','reviewing'].includes(state)?'running':state==='waiting'?'waiting':state==='blocked'?'blocked':'other';
export function selectTasks(tasks,{filter='all',query=''}={}) {
  const needle=query.trim().toLocaleLowerCase();
  return tasks.filter(task=>(filter==='archived'?task.archived:!task.archived)&&
    (filter==='all'||filter==='archived'||(task.source==='native'&&task.stale?'other':taskGroup(task.state))===filter)&&
    (!needle||`${task.title||''}\n${task.text||''}`.toLocaleLowerCase().includes(needle)));
}
export function continuationDraft(task,language,kind='progress') {
  const index=Math.max(0,languages.indexOf(language));
  if(!['progress','continue'].includes(kind))throw Error('invalid_draft');
  const start=kind==='progress'?
    ['请帮我查看这件事的进展。','請幫我查看這件事的進展。','Please check the progress of this task.']:
    ['请接着处理这件事。','請接著處理這件事。','Please continue working on this task.'];
  const context=[start[index],`${['任务','任務','Task'][index]}: ${task.text||task.title||['本机任务','本機任務','Local task'][index]}`];
  if(task.link)context.push(`${['原任务','原任務','Original task'][index]}: ${task.link}`);
  else context.push(['请先确认对应的原任务；如果不能确定，请问我，不要新建或重跑。','請先確認對應的原任務；如果無法確定，請問我，不要新建或重跑。','First identify the original task. If it is unclear, ask me; do not create or rerun a task.'][index]);
  context.push((kind==='progress'?
    ['请先核对原任务和已有结果，告诉我已完成什么、还剩什么、是否需要我决定。先不要重复执行任务。','請先核對原任務和既有結果，告訴我已完成什麼、還剩什麼、是否需要我決定。先不要重複執行任務。','Check the original task and existing results. Tell me what is done, what remains and whether you need a decision. Do not repeat the work.']:
    ['请先核对当前状态和已有结果，从未完成的地方继续，避免重复已完成的操作。如果缺少关键决定，请用大白话问我。','請先核對目前狀態和既有結果，從未完成之處繼續，避免重複已完成的操作。如果缺少關鍵決定，請用白話問我。','Check the current state and existing results, then continue from the unfinished work without repeating completed actions. Ask me plainly if a key decision is missing.'])[index]);
  return context.join('\n\n');
}
