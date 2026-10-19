import {selectTasks,continuationDraft} from './task-view.mjs';
const $=id=>document.getElementById(id);
const labels={
 subtitle:['dot 助手 · 预览版','dot 助手 · 預覽版','dot assistant · Preview'],
 eyebrow:['任务在这里，求助也在这里','任務在這裡，求助也在這裡','Your tasks. A helping hand.'],
 headline:['知道它在忙什么，下一步该做什么。','知道它在忙什麼，下一步該做什麼。','Know what is happening. Know what to do next.'],
 intro:['先照看任务，出问题时按一下救生圈。实际工作仍由官方 Codex / dot 完成。','先照看任務，出問題時按一下救生圈。實際工作仍由官方 Codex / dot 完成。','Keep tasks together; use SOS when something goes wrong. Codex / dot still does the actual work.'],
 classic:['截图 / 一句话求助','截圖 / 一句話求助','Screenshot / quick help'],quit:['关闭助手','關閉助手','Close assistant'],add:['＋ 记下一件事','＋ 記下一件事','＋ Add a task'],
 connected:['本机状态已连接','本機狀態已連接','Local status connected'],connecting:['正在尝试本机连接…','正在嘗試本機連線…','Trying the local connection…'],disconnected:['尚未连接自动任务状态','尚未連接自動任務狀態','Automatic task status is not connected'],
 connectionLive:['只接收本机运行状态，不读取聊天。当前只覆盖这个连接，不代表全部云端 dot 任务。','只接收本機執行狀態，不讀取聊天。目前只涵蓋此連線，不代表全部雲端 dot 任務。','Only local runtime states are received, not chats. This connection does not cover all cloud dot tasks.'],
 connectionMissing:['当前 Codex 未提供可连接的本机通道。可以先记任务或求助；空看板不代表没有任务。','目前 Codex 未提供可連接的本機通道。可以先記任務或求助；空看板不代表沒有任務。','No supported local connection is available. You can still keep task notes or get help. An empty board does not mean you have no tasks.'],
 connect:['尝试连接','嘗試連線','Try connection'],disconnect:['断开观察','中斷觀察','Disconnect observer'],
 composerTitle:['把要交给 dot 的事先放这里','把要交給 dot 的事先放這裡','Keep your next dot task here'],
 taskLabel:['你想让它做什么？','你想讓它做什麼？','What would you like it to do?'],linkLabel:['原任务链接（选填）','原任務連結（選填）','Original task link (optional)'],
 composerNote:['只保存在本机，尚未发送。保存后可复制给 dot；不会自动启动任务。最多 1200 字。','只儲存在本機，尚未傳送。儲存後可複製給 dot；不會自動啟動任務。最多 1200 字。','Saved on this computer, not sent. Copy it to dot when ready; no task is started automatically. Up to 1,200 characters.'],
 saveTask:['保存任务卡','儲存任務卡','Save task card'],cancelTask:['取消','取消','Cancel'],
 emptyTitle:['从一件事开始','從一件事開始','Start with one task'],emptyNote:['记下要交给 dot 的事，或尝试连接本机任务。遇到错误，不用找日志，直接点“截图 / 一句话求助”。','記下要交給 dot 的事，或嘗試連接本機任務。遇到錯誤，不用找記錄，直接點「截圖 / 一句話求助」。','Add something for dot, or try connecting local tasks. If an error appears, choose “Screenshot / quick help”; no log hunting needed.'],
 all:['全部','全部','All'],running:['正在办','正在辦','Working'],waiting:['等我决定','等我決定','Needs me'],blocked:['遇到问题','遇到問題','Needs help'],other:['其他记录','其他記錄','Other records'],
 paused:['本轮已停止 · 未核实交付','本輪已停止 · 未核實交付','Turn stopped · delivery unverified'],queued:['还没发送','尚未傳送','Not sent yet'],completed:['已标记完成 · 未独立核实','已標記完成 · 未獨立核實','Marked complete · not independently verified'],unknown:['状态待核实','狀態待核實','Status unverified'],reviewing:['复核中（记录）','複核中（記錄）','Reviewing (reported)'],partial:['部分完成（记录）','部分完成（記錄）','Partial (reported)'],canceled:['已取消（记录）','已取消（記錄）','Canceled (reported)'],
 native:['官方运行状态','官方執行狀態','Official runtime state'],manual:['我记下的任务','我記下的任務','My task note'],reported:['导入记录 · 不是实时状态','匯入記錄 · 不是即時狀態','Imported record · not live'],unnamed:['本机任务','本機任務','Local task'],
 observed:['最近观察：','最近觀察：','Last observation: '],notObserved:['尚未收到状态事件','尚未收到狀態事件','No status event received yet'],stale:['最近没有新状态；不能据此认定卡死。','最近沒有新狀態；不能據此認定卡住。','No recent state event; this alone does not mean a task is stuck.'],
 nextWorking:['继续交给 Codex；需要补充时打开原任务。','繼續交給 Codex；需要補充時開啟原任務。','Let Codex work; open the original task if you need to add something.'],
 nextWaiting:['打开原任务，看它需要你决定什么。','開啟原任務，看看需要你決定什麼。','Open the original task to see what it needs from you.'],
 nextHelp:['先查看 SOS 材料；不要删除任务或数据。','先查看 SOS 資料；不要刪除任務或資料。','Review SOS first; do not delete tasks or data.'],
 nextOther:['打开原任务核实结果；记录不是交付证明。','開啟原任務核實結果；記錄不是交付證明。','Check the original task; a recorded state is not proof of delivery.'],
 open:['打开原任务','開啟原任務','Open original'],openDot:['打开官方 dot 入口','開啟官方 dot 入口','Open ChatGPT / dot'],copyTask:['复制给 dot','複製給 dot','Copy for dot'],help:['SOS 帮我看看','SOS 幫我看看','Check with SOS'],mark:['我的记录：','我的記錄：','My record: '],
 diagnosisTitle:['SOS · 本机求助材料','SOS · 本機求助資料','SOS · local help'],diagnosisScope:['这份检查针对本机 Codex。不会假定云端 dot 故障发生在这台电脑，也不把“本轮停止”当作故障。','這份檢查針對本機 Codex。不會假定雲端 dot 故障發生在這台電腦，也不把「本輪停止」當作故障。','This check concerns local Codex. It does not locate cloud dot failures on this PC or treat a stopped turn as a fault.'],
 review:['查看并复制求助材料','查看並複製求助資料','Review and copy help report'],reviewTitle:['先看清楚，再复制','先看清楚，再複製','Review before copying'],reviewHint:['以下就是将复制的文字。原截图不在其中。自动遮盖可能遗漏，请快速看一眼；不会自动发送。','以下就是將複製的文字。原始截圖不在其中。自動遮蓋可能遺漏，請快速檢查；不會自動傳送。','The exact text below will be copied, without screenshots. Redaction can miss details: take a quick look. Nothing is sent automatically.'],
 copyReport:['复制这些材料','複製這些資料','Copy this report'],checking:['正在本机检查，请稍候…','正在本機檢查，請稍候…','Checking locally…'],diagnosticFailed:['这次没有完成。可以打开截图求助继续排查。','這次未完成。可以開啟截圖求助繼續檢查。','The check did not finish. Open Screenshot / quick help to continue.'],
 four1:['这大概是什么','這大概是什麼','What it may be'],four2:['是不是只有我','是不是只有我','Has anyone else seen it?'],four3:['现在最安全怎么做','現在最安全怎麼做','Safest next step'],four4:['还不行怎么办','還不行怎麼辦','If that does not help'],
 advancedTitle:['连接与导入（可选）','連線與匯入（選填）','Connection and import (optional)'],advancedNote:['优先使用现有官方本机服务。若你已有明确开启的本机 App Server，可填它的本机地址；这里只接收状态，不创建或续跑任务。不支持云端全账户扫描。','優先使用現有官方本機服務。若已有明確啟用的本機 App Server，可填入其本機位址；这里只接收狀態，不建立或續跑任務。不支援雲端全帳戶掃描。','Use an existing official local service. If you explicitly started a local App Server, enter its loopback address. This only observes state; it never starts or resumes tasks or scans a cloud account.'],
 connectEndpoint:['连接这个本机地址','連接此本機位址','Connect local address'],importLabel:['导入已有 dot-task-board 任务快照（仅本机 JSON，最多 100 项）','匯入既有 dot-task-board 任務快照（僅本機 JSON，最多 100 項）','Import a dot-task-board snapshot (local JSON, up to 100 tasks)'],
 privacyFooter:['非官方社区工具，与 OpenAI 无隶属关系。任务记录只存在这台电脑；查看状态不消耗模型用量。SOS 可联网查官方状态和少量已清理的问题关键词。','非官方社群工具，與 OpenAI 無隸屬關係。任務記錄只存在這台電腦；查看狀態不消耗模型用量。SOS 可連網查官方狀態和少量已清理的問題關鍵字。','Unofficial community tool, not affiliated with OpenAI. Task notes stay on this PC; observing state uses no model tokens. SOS may fetch public service status and search GitHub using cleaned error terms.'],
 copied:['已复制，尚未发送。','已複製，尚未傳送。','Copied, not sent.'],opened:['已打开入口，尚未发送或提交。','已開啟入口，尚未傳送或提交。','Opened the destination; nothing was sent or submitted.'],failed:['这次操作没有完成，原来的资料仍然保留。','這次操作未完成，原有資料仍保留。','That action did not finish. Your existing information is still here.'],saved:['任务卡已保存在本机，尚未发送。','任務卡已儲存在本機，尚未傳送。','Task card saved locally, not sent.'],saveFailed:['任务记录暂时无法保存到磁盘，请先复制需要的文字。','任務記錄暫時無法儲存，請先複製需要的文字。','Task notes cannot currently be saved to disk. Copy anything you need.'],imported:['已导入任务记录；不是实时状态。','已匯入任務記錄；不是即時狀態。','Task records imported; these are not live states.'],fixture:['虚构验收模式：不会运行真实体检或读取真实任务。','虛構驗收模式：不會執行真實檢查或讀取真實任務。','Fictional test mode: no real doctor or real task access.'],closed:['助手已关闭。Codex 本身没有被关闭。','助手已關閉。Codex 本身未被關閉。','Assistant closed. Codex itself was not stopped.'],long:['文字太长，原输入没有改变。最多 1200 字。','文字過長，原輸入未改變。最多 1200 字。','Too long; existing input was kept. Limit: 1,200 characters.']
};
Object.assign(labels,{
 edit:['修改','修改','Edit'],archive:['收起','收起','Archive'],restore:['放回任务页','放回任務頁','Restore'],archived:['已收起','已收起','Archived'],
 editTitle:['补充或修改这件事','補充或修改這件事','Edit this task note'],saveChanges:['保存修改','儲存修改','Save changes'],
 searchLabel:['找任务','找任務','Find a task'],searchPlaceholder:['输入任务里的几个字','輸入任務裡的幾個字','Search task notes'],
 noResultsTitle:['这里暂时没有匹配的任务','這裡暫時沒有相符的任務','No matching tasks here'],
 noResultsNote:['换个关键词或查看全部。收起的任务可以在“已收起”里放回来。','換個關鍵字或查看全部。收起的任務可以在「已收起」裡放回來。','Try another search or view all. Archived notes can be restored from Archived.'],
 clearFilter:['查看全部任务','查看全部任務','Show all tasks'],updated:['记录更新：','記錄更新：','Note updated: '],
 connectionGuideTitle:['找不到 dots 的任务？','找不到 dots 的任務？','Cannot find your dots tasks?'],
 connectionGuide:['在 ChatGPT 打开你的 dot → 个人页面 → 活动，查看原任务。本页目前只能自动观察兼容的本机 Codex；云端任务可以先记成卡片，贴上原任务链接，再用“问进展”。任务需要本机工作时，保持这台电脑在线并打开 ChatGPT。','在 ChatGPT 開啟你的 dot → 個人頁面 → 活動，查看原任務。本頁目前只能自動觀察相容的本機 Codex；雲端任務可先記成卡片，貼上原任務連結，再用「問進展」。任務需要本機工作時，保持電腦在線並開啟 ChatGPT。','In ChatGPT, open your dot’s profile → Activity to inspect the original task. This page automatically observes compatible local Codex only. Keep a card with the original link for cloud work, then use “Ask progress”. Keep the connected PC online with ChatGPT open for work that needs it.'],
 officialGuide:['查看官方操作说明','查看官方操作說明','Read the official guide'],
 askProgress:['问进展','問進展','Ask progress'],handoffTitle:['把这段话交给原来的 dot','把這段話交給原來的 dot','Take this draft to your original dot'],
 handoffHint:['先看一眼再复制，粘贴到原来的 dot 对话中。本助手不会发送、重跑或续办任务。','先看一眼再複製，貼到原來的 dot 對話中。本助手不會傳送、重跑或繼續任務。','Review and copy, then paste into your existing dot conversation. This assistant does not send, rerun or resume work.'],
 draftProgress:['问一下进展','問一下進展','Check progress'],draftContinue:['接着做这件事','接著做這件事','Continue this task'],
 copyHandoff:['复制上面的文字','複製上面的文字','Copy the text above'],closeHandoff:['收起草稿','收起草稿','Close draft'],
 archivedNotice:['已收起，可在“已收起”里放回来。任务本身没有被停止。','已收起，可在「已收起」裡放回來。任務本身未被停止。','Archived here. Restore it from Archived; the actual task was not stopped.'],
 restoredNotice:['已放回任务页，任务本身没有被启动。','已放回任務頁，任務本身未被啟動。','Restored to the board; no task was started.'],
 invalidLink:['这个链接暂不支持。请粘贴原来的 ChatGPT 对话链接，或先留空；原输入还在。','暫不支援此連結。請貼上原來的 ChatGPT 對話連結，或先留空；原輸入仍保留。','That link is not supported. Paste the original ChatGPT conversation link or leave it empty. Your input is kept.'],
 statusOld:['这是上次记录的状态','這是上次記錄的狀態','Previously recorded state'],
 nextStale:['请回原任务确认当前进展；上次状态不能说明现在是否仍在运行。','請回原任務確認目前進展；上次狀態無法說明現在是否仍在執行。','Check the original task for current progress; the last state does not prove it is still running.'],
 edited:['修改已保存在本机，尚未发送。','修改已儲存在本機，尚未傳送。','Changes saved locally, not sent.']
});
let language='zh-CN',view=null,filter='all',lastCards='',lastPreview='',closed=false,editingId=null,saving=false,draftTaskId=null,draftKind='progress',lastDraft='';
const t=key=>labels[key]?.[['zh-CN','zh-TW','en'].indexOf(language)]??key;
const key=location.hash.slice(1)||sessionStorage.getItem('sos-key')||'';sessionStorage.setItem('sos-key',key);history.replaceState(null,'',location.pathname);
async function api(route,body) {const r=await fetch('/api/'+route,{method:body===undefined?'GET':'POST',headers:{'x-sos-token':key,...(body===undefined?{}:{'content-type':'application/json'})},...(body===undefined?{}:{body:JSON.stringify(body)})});const data=await r.json();if(!r.ok)throw Error(data.error||'failed');return data;}
function notice(text){$('notice').textContent=text;}
function el(tag,text,className){const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(className)n.className=className;return n;}
async function action(work,message){try{await work();if(message)notice(t(message));await refresh();}catch(error){notice(t(error.message==='invalid_link'?'invalidLink':'failed'));}}
function translate(){document.documentElement.lang=language;for(const id of Object.keys(labels)){if($(id))$(id).textContent=t(id);}$('search').placeholder=t('searchPlaceholder');notice('');$('copyStatus').textContent='';lastCards='';}
function openComposer(task=null){if(!$('composer').hidden){$('taskText').focus();return;}editingId=task?.id??null;$('taskText').value=task?.text??'';$('taskLink').value=task?.link??'';$('composer').hidden=false;render();$('taskText').focus();}
function showHandoff(task){draftTaskId=task.id;draftKind='progress';lastDraft='';$('handoffPanel').hidden=false;render();$('handoffPanel').scrollIntoView({behavior:'smooth'});$('copyHandoff').focus();}
function render(){
  if(!view)return;
  $('composerTitle').textContent=t(editingId?'editTitle':'composerTitle');$('saveTask').textContent=t(editingId?'saveChanges':'saveTask');
  for(const id of ['saveTask','cancelTask','taskText','taskLink'])$(id).disabled=saving;
  if(lastPreview!==view.diagnostic?.previewId){lastPreview=view.diagnostic?.previewId;$('copyStatus').textContent='';}
  $('connectionTitle').textContent=t(view.connection.state==='connected'?'connected':view.connection.state==='connecting'?'connecting':'disconnected');
  $('connectionNote').textContent=t(view.connection.state==='connected'?'connectionLive':'connectionMissing');
  $('connect').textContent=t(view.connection.state==='connected'?'disconnect':'connect');$('connect').disabled=view.connection.state==='connecting';
  $('fixture').hidden=!view.fixture;$('fixture').textContent=t('fixture');if(view.saveFailed)notice(t('saveFailed'));
  const query=$('search').value;
  const stamp=JSON.stringify([view.tasks,language,filter,query,$('composer').hidden]);if(stamp!==lastCards){lastCards=stamp;$('filters').replaceChildren();
    for(const f of ['all','running','waiting','blocked','other','archived']){const n=selectTasks(view.tasks,{filter:f}).length;const b=el('button',`${t(f)} ${n}`);b.setAttribute('aria-pressed',String(f===filter));b.onclick=()=>{filter=f;render();};$('filters').append(b);}
    const visible=selectTasks(view.tasks,{filter,query});
    $('tasks').replaceChildren();for(const task of visible){
      const card=el('article',undefined,'card'),title=task.title||`${t('unnamed')} ${task.id.slice(0,8)}`;
      card.append(el('span',task.stale?`${t('statusOld')} · ${t(task.state)}`:t(task.state),`badge ${task.stale?'unknown':task.state}`),el('span',t(task.source),'badge'),el('h2',title));
      const updated=task.source==='manual'?task.updatedAt||task.observedAt:task.observedAt;
      card.append(el('p',t(task.source==='manual'?'updated':'observed')+(updated?new Date(updated).toLocaleString(language):t('notObserved')),'meta'));
      if(task.stale)card.append(el('p',t('stale'),'meta'));
      card.append(el('p',t(task.stale?'nextStale':task.state==='running'?'nextWorking':task.state==='waiting'?'nextWaiting':task.state==='blocked'?'nextHelp':task.state==='queued'?'composerNote':'nextOther'),'next'));
      const controls=el('div',undefined,'actions');
      const open=el('button',t(task.link?'open':'openDot'));open.onclick=()=>action(()=>api('open',{id:task.id}),'opened');controls.append(open);
      if(task.text){const copy=el('button',t('copyTask'));copy.onclick=()=>action(()=>navigator.clipboard.writeText(task.text),'copied');controls.append(copy);}
      const progress=el('button',t('askProgress'));progress.onclick=()=>showHandoff(task);controls.append(progress);
      const help=el('button',t('help'));help.onclick=()=>action(()=>api('classic',{}));controls.append(help);card.append(controls);
      if(task.source!=='native'){
        const manage=el('div',undefined,'actions secondary-actions'),edit=el('button',t('edit'));edit.disabled=!$('composer').hidden;edit.onclick=()=>openComposer(task);
        const archive=el('button',t(task.archived?'restore':'archive'));archive.onclick=()=>action(()=>api('update',{id:task.id,archived:!task.archived}),task.archived?'restoredNotice':'archivedNotice');manage.append(edit,archive);card.append(manage);
        const label=el('label',t('mark'));const select=el('select');select.setAttribute('aria-label',t('mark')+title);for(const s of ['queued','running','waiting','blocked','paused','completed','canceled','partial','reviewing','unknown']){const o=el('option',t(s));o.value=s;select.append(o);}select.value=task.state;select.onchange=()=>action(()=>api('update',{id:task.id,state:select.value}));label.append(select);card.append(label);
      }
      $('tasks').append(card);
    }const first=!view.tasks.length&&filter==='all'&&!query;
    $('empty').hidden=visible.length!==0;$('emptyTitle').textContent=t(first?'emptyTitle':'noResultsTitle');$('emptyNote').textContent=t(first?'emptyNote':'noResultsNote');$('clearFilter').hidden=first;
  }
  const draftTask=view.tasks.find(task=>task.id===draftTaskId);
  if(draftTask&&!$('handoffPanel').hidden){const draft=continuationDraft(draftTask,language,draftKind);if(lastDraft!==draft){lastDraft=draft;$('handoffText').textContent=draft;$('handoffStatus').textContent='';}for(const kind of ['progress','continue'])$('draft'+(kind==='progress'?'Progress':'Continue')).setAttribute('aria-pressed',String(draftKind===kind));}
  else{$('handoffPanel').hidden=true;draftTaskId=null;lastDraft='';$('handoffText').textContent='';}
  $('diagnosis').hidden=!view.checking&&!view.diagnostic;
  if(view.checking){$('diagnosisText').textContent=t('checking');$('review').disabled=true;$('copyReport').disabled=true;}
  else if(view.diagnostic?.error){$('diagnosisText').textContent=t('diagnosticFailed');$('review').disabled=true;$('reviewPanel').hidden=true;}
  else if(view.diagnostic){$('diagnosisText').replaceChildren();for(const [i,text] of view.diagnostic.answers.entries()){const a=el('div',undefined,'answer');a.append(el('b',`${i+1}. ${t('four'+(i+1))}`),el('p',text));$('diagnosisText').append(a);} $('review').disabled=false;$('copyReport').disabled=false;$('privacy').textContent=view.diagnostic.privacy;$('report').textContent=view.diagnostic.text;}
}
async function refresh(){if(closed)return;try{view=await api('board');if(view.language!==language){language=view.language;$('language').value=language;translate();}render();}catch{notice(t('failed'));}}
translate();
$('add').onclick=()=>openComposer();$('cancelTask').onclick=()=>{$('composer').hidden=true;editingId=null;render();};
$('search').oninput=()=>render();$('clearFilter').onclick=()=>{filter='all';$('search').value='';render();};
$('draftProgress').onclick=()=>{draftKind='progress';render();};$('draftContinue').onclick=()=>{draftKind='continue';render();};
$('closeHandoff').onclick=()=>{$('handoffPanel').hidden=true;draftTaskId=null;render();};
$('copyHandoff').onclick=()=>{$('handoffStatus').textContent='';return action(async()=>{const shown=$('handoffText').textContent;if(!shown||$('handoffPanel').hidden)throw Error('no_draft');await navigator.clipboard.writeText(shown);$('handoffStatus').textContent=t('copied');},'copied');};
$('taskText').addEventListener('paste',e=>{const box=$('taskText'),text=e.clipboardData.getData('text/plain');if(box.value.length-box.selectionEnd+box.selectionStart+text.length>1200){e.preventDefault();notice(t('long'));}});
$('saveTask').onclick=async()=>{if(saving)return;saving=true;render();const edited=editingId!==null;await action(async()=>{await api(edited?'update':'task',{...(edited?{id:editingId}:{}),text:$('taskText').value,link:$('taskLink').value});$('taskText').value='';$('taskLink').value='';$('composer').hidden=true;editingId=null;filter='all';$('search').value='';},edited?'edited':'saved');saving=false;render();};
$('classic').onclick=()=>action(()=>api('classic',{}));
$('connect').onclick=()=>action(()=>api(view?.connection.state==='connected'?'disconnect':'connect',{}));
$('connectEndpoint').onclick=()=>action(()=>api('connect',{endpoint:$('endpoint').value.trim()}));
$('language').onchange=()=>action(async()=>{await api('language',{language:$('language').value});await api('diagnostic-language',{});});
$('review').onclick=()=>{$('reviewPanel').hidden=false;$('reviewPanel').scrollIntoView({behavior:'smooth'});};
$('copyReport').onclick=()=>action(async()=>{await api('copy-report',{reportId:view?.diagnostic?.id,previewId:view?.diagnostic?.previewId});$('copyStatus').textContent=t('copied');},'copied');
$('importFile').onchange=()=>action(async()=>{const f=$('importFile').files[0];if(!f||f.size>200000)throw Error();await api('import',JSON.parse(await f.text()));$('importFile').value='';},'imported');
$('quit').onclick=()=>action(async()=>{await api('quit',{});closed=true;document.body.replaceChildren(el('main',t('closed')));});
await refresh();setInterval(refresh,3000);
