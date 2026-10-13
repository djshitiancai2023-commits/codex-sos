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
let language='zh-CN',view=null,filter='all',lastCards='',lastPreview='',closed=false;
const t=key=>labels[key]?.[['zh-CN','zh-TW','en'].indexOf(language)]??key;
const key=location.hash.slice(1)||sessionStorage.getItem('sos-key')||'';sessionStorage.setItem('sos-key',key);history.replaceState(null,'',location.pathname);
async function api(route,body) {const r=await fetch('/api/'+route,{method:body===undefined?'GET':'POST',headers:{'x-sos-token':key,...(body===undefined?{}:{'content-type':'application/json'})},...(body===undefined?{}:{body:JSON.stringify(body)})});if(!r.ok)throw Error('failed');return r.json();}
function notice(text){$('notice').textContent=text;}
function el(tag,text,className){const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(className)n.className=className;return n;}
async function action(work,message){try{await work();if(message)notice(t(message));await refresh();}catch{notice(t('failed'));}}
function translate(){document.documentElement.lang=language;for(const id of Object.keys(labels)){if($(id))$(id).textContent=t(id);}notice('');$('copyStatus').textContent='';lastCards='';}
const group=state=>['running','reviewing'].includes(state)?'running':state==='waiting'?'waiting':state==='blocked'?'blocked':'other';
function render(){
  if(lastPreview!==view.diagnostic?.previewId){lastPreview=view.diagnostic?.previewId;$('copyStatus').textContent='';}
  $('connectionTitle').textContent=t(view.connection.state==='connected'?'connected':view.connection.state==='connecting'?'connecting':'disconnected');
  $('connectionNote').textContent=t(view.connection.state==='connected'?'connectionLive':'connectionMissing');
  $('connect').textContent=t(view.connection.state==='connected'?'disconnect':'connect');$('connect').disabled=view.connection.state==='connecting';
  $('fixture').hidden=!view.fixture;$('fixture').textContent=t('fixture');if(view.saveFailed)notice(t('saveFailed'));
  const stamp=JSON.stringify([view.tasks,language,filter]);if(stamp!==lastCards){lastCards=stamp;$('filters').replaceChildren();
    for(const f of ['all','running','waiting','blocked','other']){const n=view.tasks.filter(x=>f==='all'||group(x.state)===f).length;const b=el('button',`${t(f)} ${n}`);b.setAttribute('aria-pressed',String(f===filter));b.onclick=()=>{filter=f;render();};$('filters').append(b);}
    $('tasks').replaceChildren();for(const task of view.tasks.filter(x=>filter==='all'||group(x.state)===filter)){
      const card=el('article',undefined,'card'),title=task.title||`${t('unnamed')} ${task.id.slice(0,8)}`;
      card.append(el('span',t(task.state),`badge ${task.state}`),el('span',t(task.source),'badge'),el('h2',title));
      card.append(el('p',t('observed')+(task.observedAt?new Date(task.observedAt).toLocaleString(language):t('notObserved')),'meta'));
      if(task.stale)card.append(el('p',t('stale'),'meta'));
      card.append(el('p',t(task.state==='running'?'nextWorking':task.state==='waiting'?'nextWaiting':task.state==='blocked'?'nextHelp':'nextOther'),'next'));
      const controls=el('div',undefined,'actions');
      const open=el('button',t(task.link?'open':'openDot'));open.onclick=()=>action(()=>api('open',{id:task.id}),'opened');controls.append(open);
      if(task.text){const copy=el('button',t('copyTask'));copy.onclick=()=>action(()=>navigator.clipboard.writeText(task.text),'copied');controls.append(copy);}
      const help=el('button',t('help'));help.onclick=()=>action(()=>api('classic',{}));controls.append(help);card.append(controls);
      if(task.source!=='native'){const label=el('label',t('mark'));const select=el('select');select.setAttribute('aria-label',t('mark')+title);for(const s of ['queued','running','waiting','blocked','paused','completed','canceled']){const o=el('option',t(s));o.value=s;select.append(o);}select.value=task.state;select.onchange=()=>action(()=>api('update',{id:task.id,state:select.value}));label.append(select);card.append(label);}
      $('tasks').append(card);
    }$('empty').hidden=view.tasks.length!==0;
  }
  $('diagnosis').hidden=!view.checking&&!view.diagnostic;
  if(view.checking){$('diagnosisText').textContent=t('checking');$('review').disabled=true;$('copyReport').disabled=true;}
  else if(view.diagnostic?.error){$('diagnosisText').textContent=t('diagnosticFailed');$('review').disabled=true;$('reviewPanel').hidden=true;}
  else if(view.diagnostic){$('diagnosisText').replaceChildren();for(const [i,text] of view.diagnostic.answers.entries()){const a=el('div',undefined,'answer');a.append(el('b',`${i+1}. ${t('four'+(i+1))}`),el('p',text));$('diagnosisText').append(a);} $('review').disabled=false;$('copyReport').disabled=false;$('privacy').textContent=view.diagnostic.privacy;$('report').textContent=view.diagnostic.text;}
}
async function refresh(){if(closed)return;try{view=await api('board');if(view.language!==language){language=view.language;$('language').value=language;translate();}render();}catch{notice(t('failed'));}}
translate();
$('add').onclick=()=>{$('composer').hidden=false;$('taskText').focus();};$('cancelTask').onclick=()=>{$('composer').hidden=true;};
$('taskText').addEventListener('paste',e=>{const box=$('taskText'),text=e.clipboardData.getData('text/plain');if(box.value.length-box.selectionEnd+box.selectionStart+text.length>1200){e.preventDefault();notice(t('long'));}});
$('saveTask').onclick=()=>action(async()=>{await api('task',{text:$('taskText').value,link:$('taskLink').value});$('taskText').value='';$('taskLink').value='';$('composer').hidden=true;},'saved');
$('classic').onclick=()=>action(()=>api('classic',{}));
$('connect').onclick=()=>action(()=>api(view?.connection.state==='connected'?'disconnect':'connect',{}));
$('connectEndpoint').onclick=()=>action(()=>api('connect',{endpoint:$('endpoint').value.trim()}));
$('language').onchange=()=>action(async()=>{await api('language',{language:$('language').value});await api('diagnostic-language',{});});
$('review').onclick=()=>{$('reviewPanel').hidden=false;$('reviewPanel').scrollIntoView({behavior:'smooth'});};
$('copyReport').onclick=()=>action(async()=>{await api('copy-report',{reportId:view?.diagnostic?.id,previewId:view?.diagnostic?.previewId});$('copyStatus').textContent=t('copied');},'copied');
$('importFile').onchange=()=>action(async()=>{const f=$('importFile').files[0];if(!f||f.size>200000)throw Error();await api('import',JSON.parse(await f.text()));$('importFile').value='';},'imported');
$('quit').onclick=()=>action(async()=>{await api('quit',{});closed=true;document.body.replaceChildren(el('main',t('closed')));});
await refresh();setInterval(refresh,3000);
