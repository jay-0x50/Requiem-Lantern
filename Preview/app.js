'use strict';
const game=document.querySelector('#game');
let data,page='title',selectedActor='lian',selectedSlot=0,selectedSkill='lian_glimmer',selectedCardId='',autoBattle=false;
let manualSlotFocus=false,titleStarting=false,titleStartKeyHeld='';
let story=[],storyIndex=0,storyReturn='hub',storyKey='',storyLog=[],typingTimer,autoTimer,storyAuto=false,typed=false,storyFinishing=false;
let storyVisibleCount=0,storyTypingEpoch=0,storyAutoEpoch=0,storySuspended=false,storyPortraitSlots=['',''],storyScene={background:'',location:''};
let selectedChapter=1,selectedStage='',rosterIndex=0,campaignBusy=false;
let selectedBanner='sera',collectionBusy=false,recruitmentResult=null,couponReward=null,couponDraft='',couponNotice='',lastCommandError='';
function loadSettings(){const defaults={assisted:true,motion:true,sound:true};try{const saved=JSON.parse(localStorage.getItem('rl-settings')||'null');for(const key of Object.keys(defaults))if(typeof saved?.[key]==='boolean')defaults[key]=saved[key];}catch{}return defaults;}
let settings=loadSettings(),settingsReturn='hub';
let busy=false,viewToken=0,lastReward=null,dragInfo=null,toastTimer,context,combatAudio,battleAutoTimer,autoBattleWaiting=false,suppressClickUntil=0,intentTipOwner=null;
const chars=[
 ['Lian','리안','광휘 · 수호자','은백색 머리의 파수꾼.\n소등의 날 이후, 13년간 홀로 등대 요새를 지켰다.\n파편의 대가로 어린 시절의 기억 대부분을 잃었다.','…살아 있으면, 그걸로 됐어.'],
 ['Sera','세라','화염 · 돌격수','사라진 고아원 아이들을 찾아 요새에 합류했다.\n밝고 큰 웃음 뒤에 남겨진 마음을 감추고 있다.','괜찮아 괜찮아! 어둠 따위, 내가 태워버릴 테니까!'],
 ['Minato','미나토','물결 · 치유사','나른하고 다정한 의사 지망생.\n치료하지 못한 환자들의 이름을 모두 기억한다.','잃어버린 이름을, 잊지 않으려고.'],
 ['Heidel','하이델','서리 · 사수','고글과 개조 석궁을 쓰는 실용주의 기술자.\n등대 요새의 공방을 맡는다.','요새의 공방을 맡은 실용주의 기술자.'],
 ['Noa','노아','그림자 · 조율사','철종 교단에서 도망친 수수께끼의 잔영.\n그림자와 대화할 수 있다.','그림자와 대화하는 수수께끼의 잔영.'],
 ['Kai','카이','등불지기','얼어붙은 폐허에서 기억을 잃고 깨어났다.\n잔영들의 빛을 증폭하고 조율한다.','손에 남은 것은, 꺼져가는 등불 하나.'],
 ['Mare','마레','화염 · 인양꾼','영야의 연안에서 구조와 인양을 맡아 온 잔영.\n귀항호의 줄과 닻을 점검하며 돌아올 자리를 남긴다.\n놓친 구조줄의 감촉을 아직 손에서 지우지 못했다.','줄은 내가 잡을게. 넌 앞으로 가.'],
 ['Rowan','로웬','봉쇄국 · 경비대장','한때 리안과 같은 문을 지켰던 파수꾼.','문 밖에도 사람이 있다.'],
 ['Ellen','엘렌','회색 인장 연합 · 집행관','세라와 함께 아이들을 돌보던 친구.','이 통행표를 누구에게 줄지, 내가 정할 거야.'],
 ['Eve','이브','철종 교단 · 기록관','노아와 기록실을 함께 지키던 서기.','지워진 이름에도 대답이 남아 있다.'],
 ['Orphe','오르페','철종 교단 · 종지기','철종 교단의 수장.\n빛이 죽은 세계에 ‘안식’을 제안한다.','온화한 미소로 안식을 설파하는 자.']
];
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const art=id=>chars.find(c=>c[0].toLowerCase()===String(id).toLowerCase())?.[0]||id;
const btn=(text,action,cls='',extra='')=>`<button class="${cls}" data-action="${action}" ${extra}>${text}</button>`;
const footer=hint=>`<footer><span>REQUIEM LANTERN &nbsp; / &nbsp; PLAY PREVIEW 0.1</span><span>${hint}</span></footer>`;
const header=(over,title,back='hub')=>`<header>${btn('〈','go:'+back,'back','aria-label="이전 화면"')}<div class="titles"><div class="eyebrow">${over}</div><div class="heading">${title}</div></div><div class="wallet"><span>등불 조각 &nbsp; <b class="gold">${data.progress.lanternFragments}</b></span><span>코인 &nbsp; ${data.progress.coins.toLocaleString()}</span></div></header>`;
const hero=(id,cls)=>`<img class="hero ${settings.motion?'breathe ':''}${cls}" src="/art/${art(id)}.png" alt="${esc(chars.find(c=>c[0]===art(id))?.[1]||id)}">`;
const color=e=>({Radiance:'#f2a541',Flame:'#e77a5d',Tide:'#75c8c4',Frost:'#a7d4e9',Shadow:'#b09bd8'}[e]||'#f2a541');
const battleFx=new LanternCombatFX(game,{motion:()=>settings.motion,impact:combatSound,stopSound:()=>combatAudio?.stop(),color,slotPoint,targetHint,hideTooltip:hideIntentTooltip});
const element=e=>({Radiance:'광휘',Flame:'화염',Tide:'물결',Frost:'서리',Shadow:'그림자'}[e]);
function resize(){game.style.transform=`scale(${Math.min(innerWidth/1600,innerHeight/900)})`;hideIntentTooltip();}
addEventListener('resize',resize);resize();
function tone(freq=680,duration=.1){if(!settings.sound)return;try{unlockCombatAudio();const o=context.createOscillator(),g=context.createGain();o.frequency.value=freq;o.type='sine';g.gain.setValueAtTime(.07,context.currentTime);g.gain.exponentialRampToValueAtTime(.001,context.currentTime+duration);o.connect(g);g.connect(context.destination);o.start();o.stop(context.currentTime+duration);}catch{}}
// Shared sampled combat audio. Preload is started by a real UI gesture; playback
// never waits for decoding, because a late sound would miss weapon contact.
const combatAudioFamilies=['clash','hit_cut','hit_blunt','hit_heavy','swing','brace','ignite','break'];
function combatFamily(kind,amount=0,metadata={}){
 if(kind==='swish')return'swing';
 if(kind==='hit'){
  const id=String(metadata.actorId||'').toLowerCase();
  if(['sera','noa'].includes(id))return'hit_cut';
  if(id==='minato')return'hit_blunt';
  return (['lian','heidel'].includes(id)||metadata.isEnemy)&&(metadata.skillNumber>=2||amount>=18)?'hit_heavy':'hit_blunt';
 }
 return combatAudioFamilies.includes(kind)?kind:null;
}
class CombatSampleAudio{
 constructor(audioContext,enabled){
  this.context=audioContext;this.enabled=enabled;this.buffers=new Map();this.last=new Map();this.voices=new Set();this.loading=null;
  this.master=audioContext.createGain();this.master.gain.value=.8;this.master.connect(audioContext.destination);
 }
 preload(){
  if(!this.loading)this.loading=Promise.all(combatAudioFamilies.flatMap(family=>[1,2,3].map(async variant=>{
   const key=family+'_'+String(variant).padStart(2,'0');
   try{const response=await fetch('/audio/combat/'+key+'.wav');if(!response.ok)return;const buffer=await this.context.decodeAudioData(await response.arrayBuffer());this.buffers.set(key,buffer);}catch{}
  })));
  return this.loading;
 }
 play(kind,amount=0,metadata={}){
  if(!this.enabled()||this.context.state!=='running'||this.voices.size>=8)return false;
  const family=combatFamily(kind,amount,metadata);if(!family)return false;
  const available=[1,2,3].map(n=>family+'_'+String(n).padStart(2,'0')).filter(key=>this.buffers.has(key));
  if(!available.length)return false;
  const previous=this.last.get(family),index=available.indexOf(previous),key=available[(index+1)%available.length];
  const buffer=this.buffers.get(key),now=this.context.currentTime,duration=buffer.duration;
  if(!Number.isFinite(duration)||duration<=0)return false;
  let source,gain,envelope,voice;
  try{
   source=this.context.createBufferSource();envelope=this.context.createGain();gain=this.context.createGain();source.buffer=buffer;
   const level=Number.isFinite(metadata.level)?Math.max(0,Math.min(1,metadata.level)):1;
   const volume=({clash:.5,hit_cut:.45,hit_blunt:.52,hit_heavy:.58,swing:.18,brace:.34,ignite:.2,break:.48})[family]*level;
   envelope.gain.setValueAtTime(0,now);envelope.gain.linearRampToValueAtTime(1,now+Math.min(.003,duration/4));
   envelope.gain.setValueAtTime(1,now+Math.max(.003,duration-.018));envelope.gain.linearRampToValueAtTime(0,now+duration);
   source.connect(envelope);envelope.connect(gain);gain.connect(this.master);voice={source,gain,envelope,volume};this.voices.add(voice);this.rebalance();
   source.onended=()=>this.release(voice);source.start(now);source.stop(now+duration);this.last.set(family,key);return true;
  }catch{if(voice)this.release(voice);else{source?.disconnect();envelope?.disconnect();gain?.disconnect();}return false;}
 }
 rebalance(){const count=Math.max(1,this.voices.size);for(const voice of this.voices)voice.gain.gain.setValueAtTime(voice.volume/count,this.context.currentTime);}
 release(voice){if(!this.voices.delete(voice))return;voice.source.disconnect();voice.envelope.disconnect();voice.gain.disconnect();this.rebalance();}
 stop(){const voices=[...this.voices];for(const voice of voices){try{voice.source.stop();}catch{}}for(const voice of voices)this.release(voice);}
}
function unlockCombatAudio(){
 try{
  if(!context||context.state==='closed'){context=new AudioContext();combatAudio=new CombatSampleAudio(context,()=>settings.sound);}
  if(!combatAudio)combatAudio=new CombatSampleAudio(context,()=>settings.sound);
  if(context.state==='suspended')context.resume().catch(()=>{});
  combatAudio.preload();
 }catch{}
}
function combatSound(kind,amount=0,metadata={}){return settings.sound&&combatAudio?combatAudio.play(kind,amount,metadata):false;}
function toast(msg){clearTimeout(toastTimer);const t=document.querySelector('#toast');t.textContent=msg;t.style.display='block';toastTimer=setTimeout(()=>t.style.display='none',4500);}
function normalizeState(value){return {...value,previews:(value.previews||[]).map(p=>({...p,...p.preview}))};}
async function command(action,extra={}){lastCommandError='';const r=await fetch('/api/action',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({action,...extra})});const d=await r.json();if(d.battle)data=normalizeState(d);if(d.error){lastCommandError=d.error;toast(d.error);return false;}if(!r.ok)throw Error(d.error||'전투 처리에 실패했습니다.');if(d.reward)lastReward=d.reward;if(d.recruitmentResult)recruitmentResult=d.recruitmentResult;if(d.couponReward)couponReward=d.couponReward;return true;}
function pauseStoryAuto(){clearTimeout(autoTimer);autoTimer=null;storyAutoEpoch++;}
function storyAutoDelay(){const delay=Number(story[storyIndex]?.autoDelay);return Math.max(1,Math.min(6,Number.isFinite(delay)?delay:2.6))*1000;}
function scheduleStoryAuto(){pauseStoryAuto();if(page==='story'&&storyAuto&&typed&&!storyFinishing&&!storySuspended&&!document.hidden&&!game.querySelector('.modal')){const epoch=storyAutoEpoch,token=viewToken,index=storyIndex;autoTimer=setTimeout(()=>{if(epoch!==storyAutoEpoch||token!==viewToken||index!==storyIndex||page!=='story'||!storyAuto||!typed||storySuspended||document.hidden||game.querySelector('.modal'))return;autoTimer=null;nextStory();},storyAutoDelay());}}
function pauseStoryTyping(){clearInterval(typingTimer);typingTimer=null;storyTypingEpoch++;}
function updateStoryWords(){const line=story[storyIndex],text=String(line?.text||'').slice(0,storyVisibleCount);const words=game.querySelector('.words');if(words)words.textContent=text;storyLog[storyIndex]=text?`${line.speaker||'기억'}  ${text}`:'';}
function resumeStoryTyping(){
 pauseStoryTyping();if(page!=='story'||typed||storyFinishing||storySuspended||document.hidden||game.querySelector('.modal')||!story[storyIndex])return;
 const epoch=storyTypingEpoch,token=viewToken,index=storyIndex;
 typingTimer=setInterval(()=>{if(epoch!==storyTypingEpoch||token!==viewToken||index!==storyIndex||page!=='story'||storySuspended||document.hidden||game.querySelector('.modal'))return;storyVisibleCount=Math.min(storyVisibleCount+1,String(story[index].text||'').length);updateStoryWords();if(storyVisibleCount>=String(story[index].text||'').length){pauseStoryTyping();typed=true;scheduleStoryAuto();}},24);
}
function pauseBattleAuto(){clearTimeout(battleAutoTimer);battleAutoTimer=null;}
function scheduleBattleAuto(){pauseBattleAuto();if(page!=='battle'||busy||!autoBattle||!autoBattleWaiting||game.querySelector('.modal')||data.battle.outcome!=='Ongoing')return;const token=viewToken;battleAutoTimer=setTimeout(()=>{battleAutoTimer=null;if(token===viewToken&&page==='battle'&&!busy&&autoBattle&&autoBattleWaiting&&!game.querySelector('.modal'))resolveRound();},1100);}
function setBackdrop(asset){game.style.backgroundImage="linear-gradient(90deg,#0a101de0,#0b142443 70%,#090f1b65),url('/art/"+asset+".png')";}
function setScreen(name,html){battleFx.clear();setBackdrop(['hub','archive','settings','recruitment','roster'].includes(name)?'Keep':'Background');cleanupDrag();pauseStoryTyping();pauseStoryAuto();pauseBattleAuto();viewToken++;page=name;if(name!=='battle')autoBattleWaiting=false;game.classList.toggle('reduce-motion',!settings.motion);game.innerHTML=`<div class="screen ${name}">${html}</div>`;if(name==='battle')scheduleBattleAuto();}
async function go(name){
 if((busy&&page==='resolve')||campaignBusy||collectionBusy)return;
 const leavingStage=data.campaign?.activeStage&&['hub','title','stages','archive'].includes(name);
 if(leavingStage){
  campaignBusy=true;const token=viewToken;
  try{if(!await command('stage-exit',{stageId:data.campaign.activeStage.id})||token!==viewToken)return;}
  catch(error){toast(error.message);return;}finally{campaignBusy=false;}
 }
 if(name==='settings'&&page!=='settings')settingsReturn=page;
 const map={title:showTitle,hub:showHub,stages:showStages,roster:()=>showRoster(0),settings:showSettings,recruitment:showRecruitment,archive:showArchive,battle:showBattle};map[name]?.();
}
async function beginFromTitle(){
 if(page!=='title'||titleStarting||campaignBusy||busy||game.querySelector('.modal'))return;
 titleStarting=true;
 try{tone();if(data.progress.prologueCompleted)await go('hub');else if(campaignStages()[0])await enterCampaign(campaignStages()[0].id);else startStory('prologue');}
 catch(error){toast(error.message);}finally{titleStarting=false;}
}
function showTitle(){setScreen('title',`<div class="title-copy"><div class="eyebrow">T H E &nbsp; L A S T &nbsp; L I G H T</div><h1>REQUIEM<br>LANTERN</h1><div class="rule"></div><h2>레퀴엠 랜턴</h2><p>태양이 꺼진 지 13년.<br>우리가 잃어버린 것들은, 아직 빛나고 있다.</p></div>${btn('<span>화면을 클릭해 시작</span><small>CLICK TO START</small>','begin','title-start-prompt','aria-label="화면을 클릭해 시작"')}${btn('설정','go:settings','title-settings')}<div class="title-inscription">기억이 사라져도<br>등불은 남는다.</div>${footer('Enter / Space로도 시작할 수 있습니다')}`);}
function hubCard(n,title,sub,action,primary=false){return `<button class="hub-card ${primary?'primary':''}" data-action="${action}"><span class="num">${n}</span><span><strong>${title}</strong><small>${sub}</small></span><span class="arrow">→</span></button>`;}
function showHub(){const chapters=campaignChapters(),next=campaignStages().find(s=>s.unlocked&&!s.cleared),chapter=chapters.find(c=>c.number===next?.chapter)||chapters[chapters.length-1];setScreen('hub',`${header('BEACON KEEP / 영야의 해안','등대 요새','title')}${hero('Lian','hub-hero')}<div class="hub-quote"><div class="eyebrow">등대 복구 &nbsp; ${data.progress.keepLevel}단계</div><p>“…살아 있으면, 그걸로 됐어.”</p><div class="name">리안 &nbsp; / &nbsp; 등대의 파수꾼</div></div><div class="hub-side"><div class="eyebrow">MAIN STORY</div><h1>${esc(chapterFocusLabel(chapter)||'함께 남긴 이야기')}</h1><p>${esc(chapter?.focusStorySummary||'이 작은 불을, 함께 지키기로 했다.')}</p>${next?hubCard('01','이어보기 · '+esc(displayStageNumber(next)),esc(next.title),'stage-enter:'+next.id,true):hubCard('01','항로실','귀항호와 함께 지나온 해안으로','go:stages',true)}${hubCard('02','잔영 기록','별의 파편을 품은 동료들','go:roster')}${hubCard('03','함께 남긴 이야기','장과 스테이지를 따라 이어지는 동료들의 기록','go:archive')}<div class="hub-nav">${btn('항로실','go:stages')}${btn('잔영 소환','go:recruitment','summon-entry')}${btn('기록실','go:archive')}${btn('등대 복구','repair')}${btn('설정','go:settings')}</div></div>${footer('등불지기 카이 · 작은 온기에서 시작되는 이야기')}`);}
function startStory(key,ret='hub'){if(!key.startsWith('stage:')&&!['prologue','firstMeal','epilogue'].includes(key)){toast('이야기는 메인 스토리의 스테이지에서 이어집니다.');return;}const lines=key.startsWith('stage:')?data.campaign?.currentStory:data.story?.[key];if(!lines?.length){toast('이야기를 불러오지 못했습니다.');return;}story=lines;storyKey=key;storyIndex=0;storyReturn=ret;storyLog=[];storyAuto=false;storyFinishing=false;storyPortraitSlots=['',''];storyScene={background:'',location:''};drawStory();}
function storyPortraits(line){
 const background=String(line.background||'').toLowerCase(),location=String(line.location||'');
 if((background&&storyScene.background&&background!==storyScene.background)||(location&&storyScene.location&&location!==storyScene.location))storyPortraitSlots=['',''];
 if(background)storyScene.background=background;if(location)storyScene.location=location;
 const desired=[...new Set([line.portrait,line.listenerPortrait].filter(Boolean).map(id=>art(id)))].slice(0,2);
 const speaker=chars.find(c=>c[1]===line.speaker||c[0].toLowerCase()===String(line.speaker||'').toLowerCase())?.[0];
 // Narration holds the previous cast. New speakers occupy an empty seat or
 // replace the character omitted from the line's explicit two-person cast.
 if(speaker||!storyPortraitSlots.some(Boolean))for(const id of desired){if(storyPortraitSlots.includes(id))continue;let seat=storyPortraitSlots.indexOf('');if(seat<0)seat=storyPortraitSlots.findIndex(old=>!desired.includes(old));if(seat>=0)storyPortraitSlots[seat]=id;}
 return storyPortraitSlots.map((id,seat)=>id?hero(id,`story-hero story-position-${seat?'right':'left'} ${id===speaker?'is-speaking':'story-listener is-listening'}`):'').join('');
}
function storyNextLabel(){
 if(data.campaign?.replay&&storyKey.startsWith('stage:')&&data.campaign.activeStage?.after?.length&&storyIndex===data.campaign.activeStage.before.length-1&&storyIndex<story.length-1)return '이어서 읽기';
 if(storyIndex<story.length-1)return '다음';
 if(storyKey.startsWith('stage:')){const campaign=data.campaign;return campaign?.phase==='BeforeStory'&&!campaign.replay&&campaign.activeStage?.kind!=='Story'?'전투 시작':'항로로 돌아가기';}
 return storyReturn==='archive'?'기록실로 돌아가기':storyReturn==='stages'?'항로로 돌아가기':'등대로 돌아가기';
}
function drawStory(){if(storyFinishing)return;if(storyIndex>=story.length){finishStory();return;}const l=story[storyIndex];setScreen('story',RequiemStoryView.render({line:l,key:storyKey,index:storyIndex,total:story.length,auto:storyAuto,location:l.location,caption:l.caption,portraitsHtml:storyPortraits(l),nextLabel:storyNextLabel(),footerHtml:footer('Space / 대화창 클릭으로 계속')}));if(l.background==='keep'||storyScene.background==='keep')setBackdrop('Keep');storyVisibleCount=0;typed=false;updateStoryWords();resumeStoryTyping();}
function nextStory(){if(page!=='story'||storyFinishing||game.querySelector('.modal'))return;if(!typed){pauseStoryTyping();storyVisibleCount=String(story[storyIndex].text||'').length;updateStoryWords();typed=true;scheduleStoryAuto();return;}pauseStoryAuto();storyIndex++;drawStory();}
async function finishStory(){
 if(storyFinishing||page!=='story')return;
 storyFinishing=true;pauseStoryTyping();pauseStoryAuto();const token=viewToken,key=storyKey;
 try{
  if(key.startsWith('stage:')){
   const stageId=data.campaign?.activeStage?.id;
   if(!stageId||!await command('stage-story',{stageId,expectedPhase:data.campaign.phase}))return;
   if(token!==viewToken||storyKey!==key)return;
   return routeCampaign();
  }
  if(key==='prologue'&&!await command('prologue'))return;
  if(token===viewToken&&storyKey===key)go(storyReturn);
 }catch(error){toast(error.message);}finally{storyFinishing=false;}
}
const campaignChapters=()=>data.campaign?.chapters||[];
const campaignStages=()=>campaignChapters().flatMap(c=>c.stages);
const chapterFocusName=chapter=>chars.find(c=>c[0].toLowerCase()===String(chapter?.focusCharacterId||'').toLowerCase())?.[1]||'';
const chapterFocusLabel=chapter=>chapterFocusName(chapter)?chapterFocusName(chapter)+'의 장':'';
const chapterDisplayTitle=chapter=>{const name=chapterFocusName(chapter),title=String(chapter?.title||'');return name&&title.startsWith(name+' · ')?title.slice(name.length+3):title;};
const chapterReachable=chapter=>chapter?.stages.some(stage=>stage.unlocked||stage.cleared);
function focusMainChapter(characterId){
 const chapter=campaignChapters().find(c=>String(c.focusCharacterId).toLowerCase()===String(characterId).toLowerCase());
 if(!chapter){toast('이 인물의 이야기는 메인 스토리에서 이어집니다.');return;}
 if(chapterReachable(chapter))return showStages(chapter.number,'');
 const next=campaignStages().find(stage=>stage.unlocked&&!stage.cleared),reachable=campaignChapters().filter(chapterReachable);
 const frontier=next?.chapter||reachable[reachable.length-1]?.number||1;
 showStages(frontier,next?.id||'');
 toast(`${chapterFocusLabel(chapter)}(제${chapter.number}장)은 앞선 스테이지를 완료하면 열립니다. 현재 진행할 수 있는 장을 표시합니다.`);
}

const stageLabel=s=>({Story:'이야기',Battle:'전투',StoryBattle:'이야기 + 전투'}[s.kind]||'이야기');
const displayStageNumber=s=>String(s.number).includes('-')?s.number:`${s.chapter}-${s.number}`;
// Route geometry is built from visible ports only. Undiscovered stages never
// contribute a node, a path, a label, or a silhouette to the voyage chart.
function voyagePorts(stages){return stages.map((stage,index)=>{const row=Math.floor(index/5),column=index%5;return{stage,x:100+(row%2?4-column:column)*208,y:110+row*210};});}
function voyageRoute(ports){return ports.slice(1).map((port,index)=>{const previous=ports[index],dx=port.x-previous.x,dy=port.y-previous.y;return`M${previous.x},${previous.y} C${previous.x+dx*.5},${previous.y+dy*.16} ${port.x-dx*.5},${port.y-dy*.16} ${port.x},${port.y}`;}).join(' ');}
const voyageIcon=stage=>stage.isBoss?'♜':({Story:'✦',Battle:'⚔',StoryBattle:'◇'}[stage.kind]||'✦');
function showStages(chapter=selectedChapter,stageId=selectedStage){
 const chapters=campaignChapters().filter(c=>c.stages.some(s=>s.unlocked||s.cleared)),current=chapters.find(c=>c.number===Number(chapter))||chapters[0];
 if(!current){setScreen('stages',`${header('MAIN STORY','작전실')}<div class="loading">스테이지를 불러오는 중입니다.</div>${footer('메인 스토리')}`);return;}
 selectedChapter=current.number;
 const visibleStages=current.stages.filter(s=>s.unlocked||s.cleared);
 const stage=visibleStages.find(s=>s.id===stageId)||visibleStages.find(s=>s.unlocked&&!s.cleared)||visibleStages[0];selectedStage=stage.id;
 const status=stage.cleared?'완료한 이야기':stage.unlocked?'진행 가능':'앞선 스테이지를 완료하면 열립니다';
 const cast=(stage.participants||[]).map(id=>chars.find(c=>c[0].toLowerCase()===id)?.[1]||id).join(' · ');
 const ports=voyagePorts(visibleStages),frontier=ports.find(p=>p.stage.unlocked&&!p.stage.cleared)||ports[ports.length-1],route=voyageRoute(ports);
 setScreen('stages',`${header('LANTERN VOYAGE / 메인 스토리','귀항호 · 항로실')}<nav class="chapter-tabs" aria-label="장 선택">${chapters.map(c=>btn(`<small>CHAPTER ${String(c.number).padStart(2,'0')}${chapterFocusLabel(c)?' · '+esc(chapterFocusLabel(c)):''}</small><strong>${esc(chapterDisplayTitle(c))}</strong>`,`chapter:${c.number}`,c.number===current.number?'primary':'')).join('')}</nav><div class="campaign-heading"><span class="eyebrow">제${current.number}장${chapterFocusLabel(current)?' · '+esc(chapterFocusLabel(current)):''} · ${current.stages.filter(s=>s.cleared).length} / ${current.stages.length}</span><p>등대의 불빛을 싣고, 기억이 남은 해안으로.</p></div><div class="campaign-map voyage-map" aria-label="${current.number}장 항해 지도"><div class="voyage-chart-grain"></div><div class="voyage-compass" aria-hidden="true">N<span>✧</span>S</div><svg class="voyage-route" viewBox="0 0 1040 540" aria-hidden="true"><path class="route-shadow" d="${route}"/><path class="route-stitch" d="${route}"/></svg>${ports.map(({stage:s,x,y})=>btn(`<span class="stage-port-icon" aria-hidden="true">${voyageIcon(s)}</span><span class="stage-node-number">${esc(displayStageNumber(s))}</span><strong>${esc(s.title)}</strong><span class="stage-node-kind">${s.isBoss?'보스 기항지 · ':''}${stageLabel(s)}${s.kind!=='Story'?' · 적 '+(s.enemyCount||1)+'명':''}</span><small>${s.cleared?'✓ 지나온 항구':'다음 기항지'}</small>`,`stage-select:${s.id}`,`campaign-node voyage-port ${s.cleared?'cleared':''} ${s.isBoss?'boss-port':''} ${s.id===selectedStage?'selected':''}`,`style="left:${x}px;top:${y}px" data-port="${esc(s.id)}" aria-pressed="${s.id===selectedStage}" aria-label="${esc(displayStageNumber(s))} ${esc(s.title)}, ${stageLabel(s)}${s.cleared?', 완료':', 진행 가능'}"`)).join('')}<div class="voyage-ship-marker" style="left:${Math.min(900,frontier.x+54)}px;top:${frontier.y-74}px" data-ship-port="${esc(frontier.stage.id)}"><img src="/art/VoyageShip.png" alt="등불을 싣고 이동하는 귀항호"><span>귀항호</span></div>${ports.length<=5?'<div class="voyage-vessel-portrait" aria-hidden="true"><img src="/art/VoyageShip.png" alt=""></div>':''}<div class="voyage-log"><span>BEACON KEEP → COASTAL MEMORIES</span><strong>${frontier.stage.cleared?'항해 기록':'다음 기항'} · ${esc(displayStageNumber(frontier.stage))} ${esc(frontier.stage.location)}</strong><small>✦ 이야기　 ⚔ 전투　 ◇ 이야기와 전투　 ♜ 보스</small></div></div><aside class="campaign-detail"><div class="eyebrow">${esc(displayStageNumber(stage))} · ${stageLabel(stage)}</div><h2>${esc(stage.title)}</h2><div class="stage-place">${esc(stage.location)}</div><p>${esc(stage.summary)}</p><div class="stage-cast"><small>등장인물</small>${esc(cast)}</div>${stage.kind!=='Story'?`<div class="stage-opponent">${stage.humanEnemy?'인간 적대 세력':'엄브라'} · ${esc(stage.enemyName)} · 적 ${stage.enemyCount||1}명<br>출격 ${(data.recruitment?.party||[]).map(id=>chars.find(c=>c[0].toLowerCase()===id)?.[1]||id).join(' · ')}</div>`:''}<small class="stage-status">${status}</small>${btn(stage.cleared?'다시 기항 →':stage.kind==='Story'?'이야기 시작 →':'기항하기 →',`stage-enter:${stage.id}`,'primary',stage.unlocked?'':'disabled')}${stage.cleared?btn('이야기 다시 보기',`stage-replay:${stage.id}`,'stage-replay'):''}</aside>${footer('완료한 항구와 다음 기항지만 표시합니다 · 항구를 선택하면 이야기와 전투를 확인할 수 있습니다')}`);
 setBackdrop('VoyageSea');
}
async function enterCampaign(stageId,replay=false){
 if(campaignBusy||busy)return;campaignBusy=true;
 try{if(!await command(replay?'stage-replay':'stage-enter',{stageId}))return;lastReward=null;autoBattle=false;routeCampaign();}catch(error){toast(error.message);}finally{campaignBusy=false;}
}
function routeCampaign(){
 const c=data.campaign,stage=c?.activeStage;
 if(!stage)return showStages();selectedChapter=stage.chapter;selectedStage=stage.id;
 if(c.phase==='BeforeStory'||c.phase==='AfterStory')return startStory(`stage:${stage.id}:${c.phase}`,'stages');
 if(c.phase==='Battle'){selectFirstFighter();return showBattle();}
 const next=campaignStages().find(s=>s.unlocked&&!s.cleared);
 if(c.replay)return showStages(stage.chapter,stage.id);
 if(next){selectedChapter=next.chapter;selectedStage=next.id;}return showStages();
}
const defenseSkill=k=>['Guard','Evade','Counter'].includes(k?.kind);
const handFor=actor=>actor?.skillHand||[];
const cardFor=(actor,id)=>handFor(actor).find(card=>card.id===id);
function slotLink(actorId,slot){return data.battle.links.find(l=>l.actorId===actorId&&(l.slot===slot||skill(actorId,l.skillId)?.slots===2));}
function cardReserved(actorId,cardId,slot){return data.battle.links.some(l=>l.actorId===actorId&&l.cardId===cardId&&l.slot!==slot&&!(skill(actorId,l.skillId)?.slots===2&&slot===1));}
// Offered cards are choices; action capacity is the authoritative round limit.
const actionCount=actor=>Math.max(1,Math.min(2,Number(actor?.slotCount)||1));
const actionOpen=(actor,slot)=>!!actor&&Number.isInteger(slot)&&slot>=0&&slot<actionCount(actor);
const actionFits=(actor,slot,k)=>actionOpen(actor,slot)&&!!k&&(k.slots!==2||slot===0&&actionCount(actor)>=2);
function secondActionLabel(actor){
 const index=data.battle.allies.findIndex(a=>a.id===actor.id);
 const capped=Number(actor.maxActionSlots)===1||(actor.maxActionSlots==null&&data.battle.isBoss&&index>=2);
 return capped?'보스전 · 1행동':`R${index+2}부터 개방`;
}
function selectionForSlot(actorId,slot,preserve=false){
 const actor=unit(actorId),linked=slotLink(actorId,slot),current=preserve&&cardFor(actor,selectedCardId);
 if(current&&actionFits(actor,slot,skill(actorId,current.skillId))&&!cardReserved(actorId,current.id,slot))return{skillId:current.skillId,cardId:current.id};
 if(preserve&&actionOpen(actor,slot)&&defenseSkill(skill(actorId,selectedSkill)))return{skillId:selectedSkill,cardId:''};
 if(linked&&defenseSkill(skill(actorId,linked.skillId)))return{skillId:linked.skillId,cardId:''};
 const linkedCard=linked&&cardFor(actor,linked.cardId);
 if(linkedCard)return{skillId:linkedCard.skillId,cardId:linkedCard.id};
 const card=handFor(actor).find(c=>!cardReserved(actorId,c.id,slot)&&!skillLocked(actor,skill(actorId,c.skillId)||{})&&actionFits(actor,slot,skill(actorId,c.skillId)));
 if(card)return{skillId:card.skillId,cardId:card.id};
 const defense=actor.skills.find(defenseSkill);return{skillId:defense?.id||actor.skills[0]?.id,cardId:''};
}
function selectFirstFighter(){const actor=data.battle.allies.find(a=>a.hp>0)||data.battle.allies[0];if(!actor)return;selectedActor=actor.id;selectedSlot=0;manualSlotFocus=false;const choice=selectionForSlot(actor.id,0);selectedSkill=choice.skillId;selectedCardId=choice.cardId;}
const characterProgress=id=>(data.progress.characters||[]).find(c=>String(c.id).toLowerCase()===String(id).toLowerCase())||{id,level:1,syncLevel:0};
const skillLocked=(actor,k)=>(k.requiredSyncLevel||0)>(actor.syncLevel??characterProgress(actor.id).syncLevel??0);
const sanityText=value=>(value||0)>0?'+'+value:String(value||0);
const flameChance=value=>Math.max(10,Math.min(90,50+2*(value||0)));
function flameBadges(count,on=false,dark=false){return `<span class="flame-badges ${dark?'darkness':''}" aria-label="${dark?'어둠':'불'} ${count}개">${Array.from({length:Math.max(1,count||1)},()=>`<i class="flame ${on?'on':'off'}" aria-hidden="true"></i>`).join('')}</span>`;}
// New cards fill an empty action. A reserved card edits its own action; selecting
// an action chip explicitly keeps that action as the replacement destination.
function dockSlot(actorId,k,cardId=''){
 const actor=unit(actorId),own=cardId&&data.battle.links.find(l=>l.actorId===actorId&&l.cardId===cardId);
 if(own)return own.slot;
 if(k?.slots===2)return 0;
 const same=selectedActor===actorId,current=same&&selectedSlot<actionCount(actor)?selectedSlot:0;
 if(same&&manualSlotFocus)return current;
 if(same&&!slotLink(actorId,current))return current;
 const empty=Array.from({length:actionCount(actor)},(_,n)=>n).find(n=>!slotLink(actorId,n));
 return empty??current;
}
function dockChoice(actorId,cardId,skillId=''){
 const actor=unit(actorId),card=cardId&&cardFor(actor,cardId),k=skill(actorId,card?.skillId||skillId);
 if(!actor||!k||(!card&& !defenseSkill(k)))return null;
 const slot=dockSlot(actorId,k,cardId),replaced=data.battle.links.filter(l=>l.actorId===actorId&&(l.slot===slot||k.slots===2||skill(actorId,l.skillId)?.slots===2));
 const available=data.availableEmber+replaced.reduce((sum,l)=>sum+(skill(actorId,l.skillId)?.cost||0),0);
 const reason=actor.hp<=0?'전투 불능':skillLocked(actor,k)?'동기화 필요':k.slots===2&&actionCount(actor)<2?'행동 2칸 필요':cardId&&cardReserved(actorId,cardId,slot)?'다른 행동에 예약됨':k.cost>available?'불씨 부족':'';
 return{actor,k,card,slot,reason,disabled:!!reason};
}
function selectDockChoice(actorId,cardId,skillId=''){
 const choice=dockChoice(actorId,cardId,skillId);if(!choice||choice.disabled)return false;
 if(actorId!==selectedActor)manualSlotFocus=false;
 selectedActor=actorId;selectedSlot=choice.slot;selectedSkill=choice.k.id;selectedCardId=choice.card?.id||'';
 return true;
}
const collectionCharacter=id=>data.recruitment?.characters.find(c=>c.id===String(id).toLowerCase());
const ownsCharacter=id=>!!data.recruitment?.ownedCharacters.includes(String(id).toLowerCase());
const collectionAllowed=()=>!busy&&!campaignBusy&&!data.campaign?.activeStage&&['hub','recruitment','roster','settings'].includes(page);
const couponContext=()=>settingsReturn==='hub'&&!busy&&!campaignBusy&&!data.campaign?.activeStage;
const couponAllowed=()=>page==='settings'&&couponContext();
const rarityText=rarity=>'★'.repeat(rarity);
const collectionName=id=>collectionCharacter(id)?.name||chars.find(c=>c[0].toLowerCase()===id)?.[1]||id;
function lockCollectionInputs(){game.querySelectorAll('button,input').forEach(node=>node.disabled=true);}
function showRecruitment(){
 const r=data.recruitment;if(!r){toast('잔영 소환 정보를 불러오지 못했습니다.');return;}
 const banner=r.banners.find(b=>b.id===selectedBanner)||r.banners[0];if(!banner)return;selectedBanner=banner.id;
 const featured=collectionCharacter(banner.featuredCharacterId),portrait=featured.portrait,remaining=Math.max(0,r.hardPity-r.pity),disabled=collectionBusy||!collectionAllowed(),amount=count=>(r.costPerPull*count).toLocaleString();
 setScreen('recruitment',`${header('AFTERGLOW RESONANCE','잔영 소환')}<nav class="recruit-banner-tabs" aria-label="픽업 배너">${r.banners.map(b=>{const c=collectionCharacter(b.featuredCharacterId);return btn(`<img src="/art/${esc(c.portrait)}.png" alt=""><span><small>★5 PICKUP</small><strong>${esc(c.name)}</strong></span>`,`banner:${b.id}`,b.id===banner.id?'primary selected':'',`aria-pressed="${b.id===banner.id}" ${collectionBusy?'disabled':''}`);}).join('')}</nav><section class="recruit-banner" aria-label="${esc(featured.name)} 픽업"><div class="recruit-banner-glow"></div><img class="recruit-featured" src="/art/${esc(portrait)}.png" alt="${esc(featured.name)}"><div class="recruit-banner-copy"><div class="eyebrow">PICKUP · ${rarityText(featured.rarity)}</div><h1>${esc(featured.name)}</h1><div class="recruit-role">${esc(featured.role)}</div><p>${esc(chars.find(c=>c[0].toLowerCase()===featured.id)?.[4]||'남겨진 빛이, 새로운 동료를 부릅니다.')}</p><span class="recruit-owned">${ownsCharacter(featured.id)?'보유한 동료':'아직 만나지 못한 동료'}</span></div><div class="recruit-banner-caption">등대에 남은 빛으로 잔영의 공명을 부릅니다.</div></section><aside class="recruit-panel" aria-busy="${collectionBusy}"><div class="recruit-balance"><span>보유 별가루</span><strong>${r.stardust.toLocaleString()}</strong><small>공용 캐릭터 조각 ${r.characterShards.toLocaleString()}</small></div><div class="recruit-pity"><div><span>★5 누적</span><b>${r.pity} / ${r.hardPity}</b></div><div class="bar"><span style="width:${Math.min(100,r.pity/r.hardPity*100)}%"></span></div><strong>다음 ★5까지 최대 ${remaining}회</strong><small>누적 횟수와 픽업 확정 상태는 모든 배너가 공유합니다.</small></div><div class="recruit-guarantee ${r.pickupGuaranteed?'guaranteed':''}"><b>${r.pickupGuaranteed?'다음 ★5는 픽업 확정':'★5 획득 시 픽업 확률 50%'}</b><span>${r.pickupGuaranteed?`다음 ★5는 현재 선택한 ${esc(featured.name)}입니다.`:'픽업이 아닌 ★5 획득 후, 다음 ★5는 픽업으로 확정됩니다.'}</span></div><div class="recruit-rate">기본 ★5 확률 ${(r.fiveStarRate*100).toFixed(1)}% ${btn('확률과 소환 규칙','recruit-rates','',collectionBusy?'disabled':'')}</div><div class="recruit-pull-actions">${[1,10].map(count=>btn(`<strong>${count}회 소환</strong><small>별가루 ${amount(count)}</small>`,`recruit:${count}`,count===10?'primary':'',disabled||r.stardust<r.costPerPull*count?'disabled':'')).join('')}</div><div class="recruit-request-status" role="status">${collectionBusy?'등불의 공명을 확인하는 중…':r.stardust<r.costPerPull?'소환에 필요한 별가루가 부족합니다.':'원하는 픽업 배너를 선택한 뒤 소환하세요.'}</div></aside>${footer('소환 결과와 보유 동료는 진행 기록에 저장됩니다')}`);
}
function showRecruitRates(){
 if(page!=='recruitment'||collectionBusy||game.querySelector('.modal'))return;const r=data.recruitment,banner=r.banners.find(b=>b.id===selectedBanner),five=r.fiveStarRate*100,pool=r.characters.filter(c=>c.recruitable!==false),fours=pool.filter(c=>c.rarity===4);
 modal('소환 확률과 보증',`<div class="recruit-rules"><p>기본 확률 · 픽업 확정과 80회 천장 적용 전</p><table><thead><tr><th>동료</th><th>등급</th><th>기본 확률</th></tr></thead><tbody>${pool.map(c=>`<tr><td>${esc(c.name)}${c.id===banner.featuredCharacterId?' · 픽업':''}</td><td>${rarityText(c.rarity)}</td><td>${(c.rarity===5?five*(c.id===banner.featuredCharacterId?0.5:0.25):(100-five)/fours.length).toFixed(c.rarity===5?1:4)}%</td></tr>`).join('')}</tbody></table><ul><li>★5 기본 확률은 ${(r.fiveStarRate*100).toFixed(1)}%입니다. 80회 안에 반드시 ★5를 획득합니다.</li><li>★5 중 픽업은 50%, 다른 ★5 두 명은 각각 25%입니다.</li><li>픽업이 아닌 ★5 획득 후 다음 ★5는 선택한 배너의 픽업으로 확정됩니다. 배너를 바꿔도 보증과 누적 횟수는 이어집니다.</li><li>1회 별가루 ${r.costPerPull}, 10회 ${r.costPerPull*10}입니다. 10회도 각 소환에 같은 규칙을 적용합니다.</li><li>중복 동료는 ★4 조각 20개, ★5 조각 100개로 바뀝니다. 조각은 공용입니다.</li></ul><small>★4 표시 확률은 소수점 아래 네 자리에서 반올림합니다.</small></div>` ,btn('확인','close','primary'),'recruit-rules-modal');
}
function showRecruitResults(){
 if(!recruitmentResult?.pulls.length)return;const result=recruitmentResult;
 modal('등불에 응답한 동료',`<div class="recruit-result-summary">${result.pulls.length}회 소환 · 별가루 ${result.cost.toLocaleString()} 사용</div><div class="recruit-results-grid ${result.pulls.length===1?'single-result':''}">${result.pulls.map((pull,index)=>{const c=collectionCharacter(pull.characterId);return `<article class="recruit-result-card rarity-${pull.rarity}" data-recruit-result="${index+1}"><span class="result-rarity">${rarityText(pull.rarity)}</span><img src="/art/${esc(c.portrait)}.png" alt="${esc(pull.name)}"><strong>${esc(pull.name)}</strong><span class="result-status ${pull.isNew?'is-new':''}">${pull.isNew?'NEW · 새 동료':`중복 · 공용 조각 +${pull.duplicateShards}`}</span>${pull.isPickup?'<small>픽업</small>':''}</article>`;}).join('')}</div>`,btn('보유 동료 보기','recruit-roster')+btn('확인','close','primary'),'recruitment-results');
}
async function pullRecruitment(count){
 const r=data.recruitment;if(page!=='recruitment'||collectionBusy||game.querySelector('.modal')||!collectionAllowed()||![1,10].includes(count)||!r.banners.some(b=>b.id===selectedBanner))return;
 if(r.stardust<r.costPerPull*count){toast('별가루가 부족합니다.');return;}
 recruitmentResult=null;collectionBusy=true;showRecruitment();let success=false;
 try{success=await command('recruit',{bannerId:selectedBanner,count});}catch(error){toast(error.message);}finally{collectionBusy=false;}
 showRecruitment();if(success)showRecruitResults();
}
function showPartyPicker(actorId){
 if(page!=='roster'||collectionBusy||!collectionAllowed()||!ownsCharacter(actorId))return;
 const party=data.recruitment.party,existing=party.indexOf(actorId);
 modal(`${collectionName(actorId)} · 파티 편성`,`<p class="party-picker-help">${existing>=0?'자리를 바꿀 동료를 선택하세요. 두 동료의 순서가 교환됩니다.':'교체할 동료를 선택하세요. 선택한 자리에 새 동료가 편성됩니다.'}</p><div class="party-picker">${party.map((id,slot)=>{const c=collectionCharacter(id);return btn(`<b>${slot+1}번</b><img src="/art/${esc(c.portrait)}.png" alt=""><strong>${esc(c.name)}</strong><small>${rarityText(c.rarity)}</small><span>${id===actorId?'현재 자리':existing>=0?'서로 교환':'이 자리로 교체'}</span>`,`equip:${actorId}:${slot}`,'',id===actorId?'disabled':'');}).join('')}</div>`,btn('취소','close'),'party-picker-modal');
}
async function equipCharacter(actorId,slot){
 if(page!=='roster'||collectionBusy||!collectionAllowed()||!ownsCharacter(actorId)||!Number.isInteger(slot)||slot<0||slot>3||data.recruitment.party[slot]===actorId)return;
 collectionBusy=true;lockCollectionInputs();let success=false;
 try{success=await command('equip-character',{actorId,slot});}catch(error){toast(error.message);}finally{collectionBusy=false;}
 showRoster(rosterIndex);if(success)toast(`${collectionName(actorId)}의 파티 편성을 변경했습니다.`);
}
async function synchronizeCharacter(actorId){
 if(page!=='roster'||collectionBusy||game.querySelector('.modal')||!collectionAllowed()||!ownsCharacter(actorId))return;
 collectionBusy=true;lockCollectionInputs();let success=false;
 try{success=await command('synchronize',{actorId});}catch(error){toast(error.message);}finally{collectionBusy=false;}
 showRoster(rosterIndex);if(success)toast('동기화가 완료되어 3스킬이 열렸습니다.');
}
function showRoster(i){
 const roster=chars.filter(c=>!['Rowan','Ellen','Eve'].includes(c[0]));rosterIndex=Math.max(0,Math.min(roster.length-1,i));const c=roster[rosterIndex],id=c[0].toLowerCase(),progress=characterProgress(id),playable=collectionCharacter(id),owned=ownsCharacter(id),partyIndex=data.recruitment?.party.indexOf(id)??-1,focusChapter=campaignChapters().find(chapter=>String(chapter.focusCharacterId).toLowerCase()===id),editable=owned&&collectionAllowed()&&!collectionBusy;
 setScreen('roster',`${header('AFTERGLOW ARCHIVE / 인물 기록','잔영과 등불지기')}${hero(c[0],'roster-hero')}<div class="roster-info"><div class="eyebrow">CHARACTER &nbsp; ${String(rosterIndex+1).padStart(2,'0')} / ${String(roster.length).padStart(2,'0')}</div><div class="roster-ownership ${owned?'owned':''}">${playable?`${rarityText(playable.rarity)} · ${owned?'보유':'미보유'}${partyIndex>=0?' · 파티 '+(partyIndex+1)+'번':''}`:'이야기 속 인물'}</div><h1>${c[1]}</h1><div class="role">${c[2]}</div><div class="bio">${c[3]}</div><blockquote>“${c[4]}”</blockquote>${playable?`<div class="character-growth"><span>레벨 ${progress.level} · 동기화 ${progress.syncLevel}</span><small>${!owned?(playable.recruitable===false?'이야기 속 등불지기입니다. 기존 보유 기록은 유지됩니다.':'소환으로 획득하면 성장과 편성이 열립니다.'):progress.syncLevel>0?'3스킬 개방':'동기화하면 3스킬이 열립니다'}</small>${btn(progress.syncLevel>0?'동기화 완료':'동기화 · 등불 조각 10',`synchronize:${id}`,'primary',!editable||progress.syncLevel>0||data.progress.lanternFragments<10?'disabled':'')}${btn(partyIndex>=0?'편성 '+(partyIndex+1)+'번 · 자리 변경':'파티 편성',`party-picker:${id}`,'',!editable?'disabled':'')}</div>`:''}${focusChapter?`<div class="roster-story-entry">${btn('메인 스토리 · '+c[1]+'의 장 보기','focus-chapter:'+id)}<small>제${focusChapter.number}장의 스테이지로 이어지는 이야기${chapterReachable(focusChapter)?'':' · 앞선 스테이지 완료 후 개방'}</small></div>`:''}<div class="roster-tabs">${roster.map((entry,n)=>{const k=collectionCharacter(entry[0]),has=ownsCharacter(entry[0]);return btn(`<strong>${esc(entry[1])}</strong><small>${k?`${rarityText(k.rarity)} · ${has?'보유':'미보유'}`:'이야기 인물'}</small>`,'character:'+n,n===rosterIndex?'primary':'',`data-owned="${has}"`);}).join('')}</div></div>${footer('인물의 기록과 이야기 열람은 소유 여부와 별개입니다')}`);
}
function showSettings(){
 const allowed=couponContext(),rewards=couponReward&&[['stardust','별가루'],['coins','코인'],['lanternFragments','등불 조각'],['memoryFragments','기억의 파편'],['characterShards','공용 조각']].filter(([key])=>couponReward[key]>0).map(([key,label])=>`${label} +${couponReward[key].toLocaleString()}`).join(' · ');
 setScreen('settings',`${header('PREFERENCES','환경 설정',settingsReturn)}<div class="settings settings-with-coupon"><h1>편안한 속도로, 빛을 잇도록.</h1>${[['assisted','점화 타이밍 보조','자동으로 ‘양호’ 판정. 끄면 클릭 / Space로 점화합니다.'],['motion','움직임과 전투 효과','빛실 흐름, 타격 움직임, 화면 흔들림을 재생합니다. 끄면 간소하게 표시합니다.'],['sound','효과음','무기 충돌, 타격, 불씨와 장비 효과음을 재생합니다.']].map(([id,title,desc])=>`<div class="setting"><div><strong>${title}</strong><p>${desc}</p></div>${btn(settings[id]?'켜짐':'꺼짐','setting:'+id,settings[id]?'primary':'')}</div>`).join('')}<section class="coupon-setting"><div><label for="coupon-code">쿠폰 입력</label><p>${allowed?'코드를 입력하고 보상을 받으세요.':'쿠폰은 거점의 환경 설정에서 사용할 수 있습니다.'}</p></div><div class="coupon-entry"><input id="coupon-code" aria-label="쿠폰 코드" autocomplete="off" spellcheck="false" maxlength="80" placeholder="쿠폰 코드를 입력하세요" value="${esc(couponDraft)}" ${!allowed||collectionBusy?'disabled':''}>${btn(collectionBusy?'확인 중…':'쿠폰 사용','redeem-code','primary',!allowed||collectionBusy?'disabled':'')}</div><div class="coupon-status" role="status">${esc(couponNotice)}${couponReward?`<small>${esc(rewards)}</small>`:''}</div></section></div>${footer('진행은 로컬 서버에 저장 · 설정은 이 브라우저에 저장')}`);
}
async function redeemCoupon(){
 if(!couponAllowed()||collectionBusy||game.querySelector('.modal'))return;couponDraft=game.querySelector('#coupon-code')?.value.trim()||'';if(!couponDraft){couponNotice='쿠폰 코드를 입력해 주세요.';return showSettings();}
 collectionBusy=true;couponReward=null;couponNotice='';lockCollectionInputs();let success=false;
 try{success=await command('redeem-code',{code:couponDraft});couponNotice=success?'쿠폰 보상을 받았습니다.':lastCommandError||'쿠폰을 적용하지 못했습니다.';if(success)couponDraft='';}catch(error){couponNotice=error.message;toast(error.message);}finally{collectionBusy=false;}
 showSettings();
}
function showArchive(){
 const cleared=campaignStages().filter(s=>s.cleared),chapters=campaignChapters().filter(c=>c.stages.some(s=>s.cleared));
 setScreen('archive',`${header('MAIN STORY / MEMORY ARCHIVE','기록실')}<div class="campaign-archive"><div class="eyebrow">함께 지나온 이야기 · ${cleared.length} / ${campaignStages().length}</div><h1>빛은 기억을 품는다.</h1><p>완료한 스테이지의 대화를 다시 펼쳐 봅니다.</p><div class="archive-stage-list">${chapters.map(c=>`<section><h2>제${c.number}장${chapterFocusLabel(c)?' · '+esc(chapterFocusLabel(c)):''} · ${esc(chapterDisplayTitle(c))}</h2>${c.stages.filter(s=>s.cleared).map(s=>btn(`<span>${esc(displayStageNumber(s))}</span><strong>${esc(s.title)}</strong><small>${stageLabel(s)}</small>`,`stage-replay:${s.id}`)).join('')}</section>`).join('')}</div></div>${footer('이야기 다시 보기는 전투와 보상 없이 재생합니다')}`);
}
const unit=id=>data.battle.allies.concat(data.battle.enemies).find(u=>u.id===id);
const skill=(actor,id)=>unit(actor)?.skills.find(s=>s.id===id);
const intent=id=>data.battle.intents.find(i=>i.id===id);
function targetName(id){const action=intent(id);if(action)return `${unit(action.actorId)?.name||'적'} · ${action.name}`;const owner=data.battle.enemies.find(u=>u.parts.some(p=>p.id===id));return unit(id)?.name||(owner?`${owner.name} · ${owner.parts.find(p=>p.id===id).name}`:id);}
function actorPoint(id){return LanternBattleGeometry.allyPoint(data.battle.allies.findIndex(a=>a.id===id),data.battle.allies.length);}
function targetPoint(id){const s=data.battle,action=LanternBattleGeometry.intentPoint(s,id),part=LanternBattleGeometry.partPoint(s,id);if(action)return action;if(part)return part;if(s.allies.some(a=>a.id===id))return actorPoint(id);const owner=s.enemies.findIndex(e=>e.id===id);return owner>=0?LanternBattleGeometry.enemyPoint(owner,s.enemies.length):null;}
function interceptLink(action){return data.battle.links.find(l=>l.kind==='Clash'&&l.targetId===action.id&&unit(l.actorId)?.hp>0);}
function targetHint(actorId,targetId){const action=intent(targetId);if(!action)return '';if(action.kind==='Illusion')return '허상 연결 · 피해 없이 행동 소모';const original=unit(action.targetId),preview=(data.previews||[]).find(p=>p.actorId===actorId&&data.battle.links.some(l=>l.actorId===actorId&&l.slot===p.slot&&l.targetId===targetId));return (action.kind==='Area'?'광역 맞불 · 패배 시 전원 피격':original&&original.id!==actorId?`${original.name} 대신 가로막기`:'놓아서 맞불 연결')+(preview?' · '+({Advantage:'유리',Disadvantage:'불리',Neutral:'대등'}[preview.grade]||'대등'):'');}
const intentType=kind=>({Normal:['단일','✧'],Area:['광역','✦'],Piercing:['관통','↟'],Illusion:['허상','◈']}[kind]||['단일','✧']);
function intentPower(action){const hits=action.hitPowers||[],count=Math.max(1,action.flameCount||hits.length||1),base=action.basePower>0?action.basePower:hits.length?Math.max(1,Math.floor(hits.reduce((a,b)=>a+b,0)/hits.length)-2):4,flame=action.flamePower>0?action.flamePower:2;return{count,base,flame,max:base+flame*count};}
function intentAssignment(action){const linked=interceptLink(action),interceptor=linked&&unit(linked.actorId),changed=interceptor&&!['Area','Illusion'].includes(action.kind)&&action.targetId!=='all'&&interceptor.id!==action.targetId;return{linked,interceptor,changed,original:action.kind==='Area'||action.targetId==='all'?'아군 전체':unit(action.targetId)?.name||'대상 없음',label:interceptor?`${action.kind==='Illusion'?'허상 연결':changed?'대신 맞불':'맞불'} · ${interceptor.name}`:action.kind==='Illusion'?'허상 · 미연결':'맞불 미지정'};}
function affinityScale(attack,defense){if(['Flame:Frost','Frost:Tide','Tide:Flame','Radiance:Shadow','Shadow:Radiance'].includes(attack+':'+defense))return 1.2;if(['Frost:Flame','Tide:Frost','Flame:Tide'].includes(attack+':'+defense))return .85;return 1;}
function intentDetails(action){
 const enemy=unit(action.actorId),power=intentPower(action),assignment=intentAssignment(action),part=enemy?.parts.find(p=>p.id===action.partId),actor=assignment.interceptor||unit(selectedActor),chosen=actor&&skill(actor.id,assignment.linked?.skillId||selectedSkill),enemyElement=action.element||enemy?.element,allyElement=chosen?.element||actor?.element;
 const effects={Normal:'맞불이 없으면 방어·회피·반격 보호로 대응할 수 있습니다.',Area:'맞불 패배 시 살아 있는 아군 전체가 피격됩니다. 방어 대행은 적용되지 않습니다.',Piercing:'방어·회피·반격 대행을 무시합니다. 공격 스킬의 맞불로 대응할 수 있습니다.',Illusion:'피해를 주지 않는 허상입니다. 맞불을 연결하면 해당 행동만 소모됩니다.'};
 const affinity=actor?`${actor.name} 기준 · 아군 ${element(allyElement)||'속성'} → ${element(enemy?.element)||'적'} ×${affinityScale(allyElement,enemy?.element)} / 적 ${element(enemyElement)||'속성'} → ${element(actor.element)||'아군'} ×${affinityScale(enemyElement,actor.element)}`:'동료의 스킬을 선택하면 속성 상성을 확인합니다.';
 const preview=assignment.linked&&(data.previews||[]).find(p=>p.actorId===assignment.linked.actorId&&p.slot===assignment.linked.slot);
 return `<div class="intent-detail-overline">${esc(enemy?.name||'적')} · ${intentType(action.kind)[0]} 공격</div><h3>${esc(action.name)}</h3><dl><dt>위력 범위</dt><dd>${power.base}–${power.max}<small>속성·어둠도 적용 전</small></dd><dt>어둠 ${power.count}개</dt><dd>기본 ${power.base} + 켜진 어둠마다 ${power.flame}</dd><dt>정신력 / 점등</dt><dd>${sanityText(enemy?.sanity)} / ${flameChance(enemy?.sanity)}%</dd><dt>기본 속도 / 속성</dt><dd>${action.speed} / ${element(enemyElement)||'미상'}</dd><dt>원래 표적</dt><dd>${esc(assignment.original)}</dd><dt>공격 부위</dt><dd>${esc(part?`${part.name} · ${part.hp}/${part.maxHp||part.hp}`:'본체')}</dd><dt>현재 배정</dt><dd>${esc(assignment.label)}${action.kind==='Area'&&assignment.interceptor?'<small>광역의 전체 범위는 유지됩니다.</small>':''}</dd></dl><p class="intent-detail-effect"><b>특수 효과</b> ${effects[action.kind]||effects.Normal}${part?' 이 부위가 파괴되면 해당 부위의 예고가 취소됩니다.':''}${action.kind!=='Illusion'&&enemyElement==='Shadow'&&data.battle.gloom>=2?` 현재 어둠도 ${data.battle.gloom}: 그림자 위력 ×${(1+(data.battle.gloom-1)*.1).toFixed(1)}.`:''}</p>${action.kind!=='Illusion'?`<div class="intent-detail-affinity"><b>속성 상성</b>${esc(affinity)}${preview?.summary?`<small>${esc(preview.summary)}</small>`:''}</div>`:''}`;
}
function hideIntentTooltip(){intentTipOwner?.removeAttribute?.('aria-describedby');intentTipOwner=null;game.querySelector('.intent-tooltip')?.remove();}
function tooltipBounds(anchor,width,height){height=Math.min(720,height);const leftSide=anchor.x-width-12,left=leftSide>=12?leftSide:anchor.x+anchor.width+12;return{x:Math.max(12,Math.min(1588-width,left)),y:Math.max(108,Math.min(844-height,anchor.y))};}
function showIntentTooltip(id,node){
 if(page!=='battle'||busy||campaignBusy||dragInfo||game.querySelector('.modal')||node?.disabled)return;
 const action=intent(id);if(!action||action.cancelled||action.resolved||!unit(action.actorId)||unit(action.actorId).hp<=0)return;
 hideIntentTooltip();game.insertAdjacentHTML('beforeend',`<aside class="intent-tooltip" id="intent-details" role="tooltip" data-tooltip-intent="${esc(id)}">${intentDetails(action)}</aside>`);
 const tip=game.querySelector('.intent-tooltip');if(!tip)return;const center=logicalElementCenter(node),fallback=LanternBattleGeometry.intentRect(data.battle,id),r=node?.getBoundingClientRect?.(),g=game.getBoundingClientRect(),anchor=center&&r?{x:(r.left-g.left)*1600/g.width,y:(r.top-g.top)*900/g.height,width:r.width*1600/g.width}:fallback;
 const point=tooltipBounds(anchor,360,tip.offsetHeight||360);tip.style.left=point.x+'px';tip.style.top=point.y+'px';intentTipOwner=node;node?.setAttribute?.('aria-describedby','intent-details');
}
function enemyUnitHtml(enemy,index){
 const s=data.battle,foot=LanternBattleGeometry.enemyFoot(index,s.enemies.length),height=LanternBattleGeometry.enemyHeight(s.enemies.length),own=s.intents.filter(i=>i.actorId===enemy.id),width=243.33333333333334;
 const number=s.enemies.length>1?`<b class="enemy-number">${index+1}</b> `:'',stats=`<div class="enemy-stats" data-enemy-stats="${esc(enemy.id)}" style="left:${foot.x-width/2}px;top:108.33333333333334px;width:${width}px"><div class="name">${number}${enemy.isHuman?'HUMAN':'UMBRA'} / ${esc(enemy.name)}</div><div class="bar"><span data-hp-bar="${enemy.id}" style="width:${enemy.hp/enemy.maxHp*100}%"></span></div><small data-hp-text="${enemy.id}">${enemy.hp} / ${enemy.maxHp} · 균열 ${enemy.rift} / ${enemy.maxRift}</small><div class="enemy-sanity"><span data-sanity="${enemy.id}">정신력 ${sanityText(enemy.sanity)}</span> · 어둠 ${flameChance(enemy.sanity)}%</div></div>`;
 const parts=`<div class="parts" data-enemy-parts="${esc(enemy.id)}" style="left:${foot.x-width/2}px;top:180px;width:${width}px">${enemy.parts.map(p=>btn(`${esc(p.name)} ${p.hp<=0?'파괴':`${p.hp}/${p.maxHp}`}`,'target:'+p.id,'',`data-owner="${esc(enemy.id)}" ${p.hp<=0||enemy.hp<=0?'disabled':''}`)).join('')}</div>`;
 const intents=own.map((i,n)=>{const box=LanternBattleGeometry.intentRect(s,i.id),assignment=intentAssignment(i),[type,symbol]=intentType(i.kind),power=intentPower(i);
 return btn(`<span class="intent-topline"><b>${String(n+1).padStart(2,'0')}</b><span>${symbol} ${type}</span></span><span class="intent-lamps" aria-label="어둠 ${power.count}개"><i class="intent-darkness-symbol" aria-hidden="true"></i><b>×${power.count}</b></span><strong class="intent-shortname">${esc(i.name)}</strong><span class="intent-recipient">표적 · ${esc(assignment.original)}</span><span class="intent-assignment ${assignment.changed?'intercepted':''}">${esc(assignment.label)}</span>`,'target:'+i.id,`intent intent-tile intent-${['Normal','Area','Piercing','Illusion'].includes(i.kind)?i.kind.toLowerCase():'normal'} ${assignment.changed?'intercepted':''} ${i.cancelled?'intent-cancelled':''}`,`data-owner="${esc(enemy.id)}" data-original-target="${esc(i.targetId)}" data-interceptor="${esc(assignment.interceptor?.id||'')}" data-intent-id="${esc(i.id)}" style="left:${box.x}px;top:${box.y}px;width:${box.width}px;height:${box.height}px" aria-label="${esc(enemy.name)} ${esc(i.name)}, 원래 대상 ${esc(assignment.original)}${assignment.interceptor?', '+(i.kind==='Illusion'?'허상 연결':assignment.changed?'가로막기':'맞불')+' '+esc(assignment.interceptor.name):i.kind==='Illusion'?', 허상 미연결':', 맞불 미지정'}" ${enemy.hp<=0||i.cancelled||i.resolved?'disabled':''}`);
 }).join('');
 return stats+parts+intents+`<img src="/art/${art(enemy.portrait||enemy.id)}.png" class="enemy ${enemy.isHuman?'human-enemy':''} ${enemy.hp<=0?'dead':''}" data-unit="${enemy.id}" style="left:${foot.x-height*.45}px;top:${foot.y-height}px;width:${height*.9}px;height:${height}px" alt="${esc(enemy.name)}">`+btn(`<span>${esc(enemy.name)} · 본체</span>`,'target:'+enemy.id,'body-target',`data-owner="${esc(enemy.id)}" style="left:${foot.x-width/2}px;top:${foot.y-height}px;width:${width}px;height:${height+36}px" aria-label="${esc(enemy.name)} 본체 공격" ${enemy.hp<=0?'disabled':''}`);
}
function logicalElementCenter(node){
 if(!node?.getBoundingClientRect)return null;
 const box=node.getBoundingClientRect(),stage=game.getBoundingClientRect();
 if(!(box.width>0&&box.height>0&&stage.width>0&&stage.height>0))return null;
 return{x:(box.left+box.width/2-stage.left)*1600/stage.width,y:(box.top+box.height/2-stage.top)*900/stage.height};
}
function slotPoint(actor,slot){
 const node=[...game.querySelectorAll('.slot')].find(n=>n.dataset.actor===actor&&Number(n.dataset.slot)===slot);
 return logicalElementCenter(node)||LanternBattleGeometry.slotPoint(data.battle.allies.findIndex(a=>a.id===actor),slot);
}
function combatUnitHtml(actor,index){
 const foot=LanternBattleGeometry.allyFoot(index,data.battle.allies.length),stem=LanternCombatAtlasStem(actor.id),layout=battleFx.options.layouts?.[actor.id],crop=layout?.frames?.[0];
 let sprite='width:183px;height:183px;left:-91.5px;top:-183px;background-size:300% 200%;background-position:0% 0%';
 if(crop){const scale=LanternBattleGeometry.allyBodyHeight/layout.bodyHeight;sprite=`width:${crop.width*scale}px;height:${crop.height*scale}px;left:${-(crop.footX-crop.x)*scale}px;top:${-(crop.footY-crop.y)*scale}px;background-size:${layout.atlasWidth*scale}px ${layout.atlasHeight*scale}px;background-position:${-crop.x*scale}px ${-crop.y*scale}px`;}
 return `<div data-unit="${actor.id}" data-combat-src="/combat/${stem}.png" class="field-hero sd-unit ${settings.motion?'breathe':''} ${actor.hp<=0?'dead':''}" style="left:${foot.x}px;top:${foot.y}px;animation-delay:${-index}s" role="img" aria-label="${esc(actor.name)} 전투 캐릭터"><span class="combat-idle-sprite" style="${sprite};background-image:url('/combat/${stem}.png')"></span></div>${btn('','target:'+actor.id,'ally-target',`style="left:${foot.x-75}px;top:${foot.y-190}px" aria-label="${esc(actor.name)}에게 지원 연결"`)}<div class="field-name" style="left:${foot.x-75}px"><i class="party-number">${index+1}</i>${esc(actor.name)}<div class="bar"><span data-hp-bar="${actor.id}" style="background:${color(actor.element)};width:${actor.hp/actor.maxHp*100}%"></span></div></div>`;
}
function refreshBattleThreads(){
 game.querySelectorAll('[data-link-actor]').forEach(n=>{const a=slotPoint(n.dataset.linkActor,+n.dataset.linkSlot),b=targetPoint(n.dataset.linkTarget);n.setAttribute('d',`M${a.x},${a.y} Q${(a.x+b.x)/2},${Math.min(a.y,b.y)-65} ${b.x},${b.y}`);});
}
function path(a,b,cls,c='',intentId=''){return `<path class="${cls}" ${intentId?`data-intent-line="${esc(intentId)}"`:''} d="M${a.x},${a.y} Q${(a.x+b.x)/2},${Math.min(a.y,b.y)-65} ${b.x},${b.y}" ${c?`style="stroke:${c}"`:''}/>`;}
function actionAvailability(actor){
 const capacity=actor.hp>0?actionCount(actor):0,reserved=new Set();
 for(const link of data.battle.links.filter(l=>l.actorId===actor.id)){
  const count=skill(actor.id,link.skillId)?.slots===2?2:1;
  for(let n=0;n<count;n++){const slot=link.slot+n;if(Number.isInteger(slot)&&slot>=0&&slot<capacity)reserved.add(slot);}
 }
 return{remaining:Math.max(0,capacity-reserved.size),capacity};
}
function fighterDock(actor,index){
 const active=actor.id===selectedActor,deck=`카드 구성 1×3 / 2×2${(actor.syncLevel||0)>0?' / 3×1':''}`;
 const actions=actionAvailability(actor),statusLabel=`후보 ${handFor(actor).length}장 · 남은 행동 ${actions.remaining}/${actions.capacity}`;
 const cards=handFor(actor).map((card,index)=>{
  const choice=dockChoice(actor.id,card.id);if(!choice)return'';
  const k=choice.k,linked=data.battle.links.find(l=>l.actorId===actor.id&&l.cardId===card.id),chosen=active&&selectedCardId===card.id;
  const min=k.basePower||0,max=min+(k.flamePower||0)*(k.flameCount||1),status=choice.reason|| (linked?`행동 ${linked.slot+1}${k.slots===2?'·2':''} 예약`:chosen?'선택 중':`행동 ${choice.slot+1}${k.slots===2?'·2':''}에 연결`);
  return btn(`<span class="card-topline"><b>${k.number}스킬</b><span class="card-power">위력 ${min}–${max}</span></span><strong class="card-name">${esc(k.name)}</strong><span class="card-bottomline">${flameBadges(k.flameCount||1)}<small>불씨 ${k.cost}${k.slots===2?' · 2칸':''}</small></span><span class="card-state">${esc(status)}</span>`,`skill-card:${actor.id}:${card.id}`,`skill-card ${chosen?'primary selected':''} ${linked?'reserved':''}`,`data-actor="${actor.id}" data-card-id="${esc(card.id)}" data-card-index="${index+1}" title="${esc(actor.name)} · ${k.number}스킬 · ${esc(k.name)} · 기본 위력 ${min} · 켜진 불마다 +${k.flamePower||0} · ${esc(status)}" aria-label="${esc(actor.name)} ${k.number}스킬 ${esc(k.name)}, 위력 ${min}에서 ${max}, 불 ${k.flameCount||1}개, 불씨 ${k.cost}, ${esc(status)}" aria-pressed="${chosen}" ${choice.disabled?'disabled':''}`);
 }).join('');
 const defense=actor.skills.filter(defenseSkill).map(k=>{const choice=dockChoice(actor.id,'',k.id),chosen=active&&selectedSkill===k.id&&!selectedCardId;return btn(esc(k.name),`defense:${actor.id}:${k.id}`,`defense-button ${chosen?'primary':''}`,`aria-pressed="${chosen}" aria-label="${esc(actor.name)} ${esc(k.name)} 선택" ${choice?.disabled?'disabled':''}`);}).join('');
 return `<section class="fighter ${active?'focused':''} ${actor.hp<=0?'incapacitated':''}" style="--actor-color:${color(actor.element)}" aria-label="${esc(actor.name)} 스킬과 행동"><span class="deck-status" data-action-status="${esc(actor.id)}" title="${deck} · 남은 카드 ${actor.drawPile?.length||0} · 사용한 카드 ${actor.discardPile?.length||0}">${statusLabel}</span><div class="fighter-cards">${cards||'<div class="empty-hand">사용할 카드가 없습니다</div>'}</div><div class="fighter-identity"><div class="dock-portrait"><img src="/art/${art(actor.portrait||actor.id)}.png" alt="" aria-hidden="true"></div><div class="fighter-vitals"><div class="fighter-title"><strong><i class="party-number">${index+1}</i>${esc(actor.name)}</strong><small data-hp-text="${actor.id}">${actor.hp}/${actor.maxHp}</small></div><div class="bar"><span data-hp-bar="${actor.id}" style="background:${color(actor.element)};width:${actor.hp/actor.maxHp*100}%"></span></div><div class="fighter-sanity"><span data-sanity="${actor.id}">정신력 ${sanityText(actor.sanity)}</span><small>점등 ${flameChance(actor.sanity)}%</small></div></div><div class="fighter-defense">${defense}</div></div><div class="slots">${Array.from({length:2},(_,n)=>{const locked=!actionOpen(actor,n),l=!locked&&slotLink(actor.id,n),k=l&&skill(actor.id,l.skillId),shared=k?.slots===2;return `<div class="slot-wrap ${locked?'locked':''}">${btn(`<b>${n+1}</b><span>${esc(locked?'잠김':k?k.name:'미연결')}</span>`,`slot:${actor.id}:${n}`,`slot ${!locked&&active&&(selectedSlot===n||selectedSlot===0&&skill(actor.id,selectedSkill)?.slots===2)?'active':''}`,`data-actor="${actor.id}" data-slot="${n}" aria-pressed="${!locked&&active&&selectedSlot===n}" aria-label="${esc(actor.name)} 행동 ${n+1}${locked?' 잠김, '+secondActionLabel(actor):''}${shared?' 두 칸 스킬':''}, ${esc(locked?'잠김':k?k.name:'미연결')}" ${actor.hp<=0||locked?'disabled':''}`)}<div class="slot-target" title="${esc(locked?secondActionLabel(actor):l?targetName(l.targetId):'미연결')}">${locked?esc(secondActionLabel(actor)):l?(l.kind==='Clash'?'맞불 · ':'')+esc(targetName(l.targetId)):'대상을 선택하세요'}</div></div>`;}).join('')}</div></section>`;
}
function clashPredictionHtml(preview){
 const link=data.battle.links.find(l=>l.actorId===preview.actorId&&l.slot===preview.slot&&l.kind==='Clash'),action=link&&intent(link.targetId),number=action?data.battle.intents.filter(i=>i.actorId===action.actorId).findIndex(i=>i.id===action.id)+1:0;
 const owner=unit(preview.actorId)?.name||preview.actorId,enemy=data.battle.enemies.length>1&&action?`${unit(action.actorId)?.name||'적'} · `:'',label=`${owner} → ${enemy}${number?'예고 '+String(number).padStart(2,'0'):'맞불'}`;
 const grade=({Advantage:'▲ 우세',Close:'= 접전',Disadvantage:'▼ 열세'})[preview.grade]||'',power=value=>Number(value.toFixed(1));
 const summary=action?.kind!=='Illusion'&&Number.isFinite(preview.allyPower)&&Number.isFinite(preview.enemyPower)?`${grade} · 기대 위력 아군 ${power(preview.allyPower)} : 적 ${power(preview.enemyPower)} · 2연승 시 공격`:preview.summary||'';
 return `<span class="prediction-owner">${esc(label)}</span> · ${esc(summary)}`;
}
function showBattle(){
 const s=data.battle;if(!unit(selectedActor)||unit(selectedActor).isEnemy||unit(selectedActor).hp<=0)selectFirstFighter();
 const selected=unit(selectedActor),selectedCard=cardFor(selected,selectedCardId);if(selectedCard)selectedSkill=selectedCard.skillId;
 const sk=skill(selectedActor,selectedSkill)||selected.skills.find(k=>!skillLocked(selected,k))||selected.skills[0];selectedSkill=sk.id;
 if(!actionOpen(selected,selectedSlot)){selectedSlot=0;manualSlotFocus=false;}
 if(!actionFits(selected,selectedSlot,sk)){const available=selectionForSlot(selectedActor,selectedSlot);selectedSkill=available.skillId;selectedCardId=available.cardId;}
 let lines='';
 for(const i of s.intents){const p=targetPoint(i.id),owner=unit(i.actorId);if(!p||!owner||owner.hp<=0||i.cancelled||i.resolved)continue;lines+=path(targetPoint(owner.id),p,'thread-enemy enemy-origin','',i.id);const {interceptor,changed}=intentAssignment(i);for(const a of (i.kind==='Area'?s.allies:[unit(i.targetId)]).filter(a=>a&&a.hp>0))lines+=path(p,actorPoint(a.id),'thread-enemy'+(changed?' original-threat':''),'',i.id);if(changed)lines+=path(p,actorPoint(interceptor.id),'thread-intercept','#75c8c4',i.id);}
 for(const l of s.links){let c='#f2a541';if(l.kind==='Clash'){const v=data.previews?.find(p=>p.actorId===l.actorId&&p.slot===l.slot);c=v?.grade==='Advantage'?'#75c8c4':v?.grade==='Disadvantage'?'#e77a5d':'#f2a541';}lines+=path(LanternBattleGeometry.slotPoint(s.allies.findIndex(a=>a.id===l.actorId),l.slot),targetPoint(l.targetId),'thread-player flow',c).replace('<path ','<path data-link-actor="'+esc(l.actorId)+'" data-link-slot="'+l.slot+'" data-link-target="'+esc(l.targetId)+'" ');}
 const choice=selectionForSlot(selectedActor,selectedSlot,true);
 if(!defenseSkill(skill(selectedActor,selectedSkill))&&(!cardFor(selected,selectedCardId)||cardReserved(selectedActor,selectedCardId,selectedSlot))){selectedSkill=choice.skillId;selectedCardId=choice.cardId;}
 const selectedSk=skill(selectedActor,selectedSkill),connected=s.links.reduce((sum,l)=>sum+(skill(l.actorId,l.skillId)?.slots||1),0),capacity=s.allies.filter(a=>a.hp>0).reduce((sum,a)=>sum+actionCount(a),0);
 setScreen('battle',`<header>${btn('〈','retreat','back','aria-label="퇴각"')}<div class="titles"><div class="heading">${esc(s.stageName||'요새 앞의 그림자')}</div><div class="phase">ROUND ${String(s.round).padStart(2,'0')} &nbsp; / &nbsp; ② 연결</div></div><div class="resources"><span class="gold">불씨 ${s.ember} / 6 <small class="muted">예약 ${data.plannedCost}</small></span><span>등불 ${s.lantern} / 100</span><span class="muted">어둠도 ${s.gloom}</span></div></header>
 <div class="battle-controls">${btn('AI 제안','recommend')}${btn('이전 연결','reuse')}${btn('초기화','clear')}${btn(autoBattle?'오토 켜짐':'수동 연결','automode',autoBattle?'primary':'')}${btn(s.lanternQueued?'해방 예약':'등불 해방','lantern','',s.lantern<100||s.lanternQueued?'disabled':'')}${btn('기록','battlelog')}</div>
 <div class="battle-help">매 턴 후보 2장 새로 추첨 · 미사용 카드도 교체<br>파티 순서대로 두 번째 행동이 열립니다.${s.isBoss?'<br>보스전은 1·2번 동료만 최대 2행동입니다.':'<br>일반전은 R5부터 4명 모두 최대 2행동입니다.'}</div><svg class="threads" viewBox="0 0 1600 900" aria-hidden="true">${lines}<path id="drag-thread" class="thread-player"/></svg>
 ${s.allies.map(combatUnitHtml).join('')}
 ${s.enemies.map(enemyUnitHtml).join('')}
 <div class="battle-prediction">${(data.previews||[]).filter(p=>s.links.some(l=>l.actorId===p.actorId&&l.slot===p.slot&&l.kind==='Clash')).map(clashPredictionHtml).join('<br>')}</div>
 <div class="action-dock" aria-label="아군 스킬 선택">${s.allies.map(fighterDock).join('')}<aside class="command-rail"><div class="rail-overline">빛실 연결</div><div class="planned-actions"><strong>${connected}<small> / ${capacity}</small></strong><span>행동 예약 · 불씨 ${data.plannedCost} / ${s.ember}</span></div><div class="selected-action"><b>${esc(selected.name)} · 행동 ${selectedSlot+1}${selectedSk?.slots===2?'·2':''}</b><span>${esc(selectedSk?.name||'스킬 선택')}<small> → 대상 클릭 또는 드래그</small></span></div><div class="command-actions">${btn('연결 해제','remove','',slotLink(selectedActor,selectedSlot)?'':'disabled')}${btn(`<strong>빛실 실행 <span>→</span></strong><small>${connected?`${connected}개 행동으로 전투 시작`:'스킬을 골라 대상을 연결하세요'}</small>`,'resolve','primary resolve-button',s.links.length?'':'disabled')}</div></aside></div>${footer('카드 클릭 → 대상 클릭 / 카드 드래그 → 연결 / 행동 칸 클릭 → 예약 교체')}`);refreshBattleThreads();battleFx.attach(s);
}
async function linkTarget(id){
 if(busy||campaignBusy||game.querySelector('.modal'))return;
 const sk=skill(selectedActor,selectedSkill);if(!sk)return;
 if(!actionFits(unit(selectedActor),selectedSlot,sk)){toast('아직 열리지 않은 행동입니다. 사용할 수 있는 행동을 선택해 주세요.');return;}
 if(!defenseSkill(sk)&&!cardFor(unit(selectedActor),selectedCardId)){toast('제시된 스킬 카드 두 장 중에서 선택해 주세요.');return;}
 const kind=sk.kind==='Heal'?'Support':defenseSkill(sk)?'Guard':intent(id)?'Clash':'Strike';
 const actor=selectedActor,slot=selectedSlot;
 if(await command('link',{actorId:actor,slot,skillId:selectedSkill,cardId:defenseSkill(sk)?'':selectedCardId,kind,targetId:id})){manualSlotFocus=false;showBattle();battleFx.connected(actor,slot,id,kind);combatSound(kind==='Guard'?'brace':'ignite',0,{actorId:actor,skillNumber:sk.number||0,skillId:sk.id,level:.45});}
}
function modal(title,body,buttons=btn('닫기','close','primary'),className=''){cleanupDrag();if(page==='story')pauseStoryTyping();pauseStoryAuto();pauseBattleAuto();game.insertAdjacentHTML('beforeend',`<div class="modal ${className}"><h2>${title}</h2>${body}<div class="modal-actions">${buttons}</div></div>`);}
function closeModal(){game.querySelector('.modal')?.remove();if(!game.querySelector('.modal')){resumeStoryTyping();scheduleStoryAuto();scheduleBattleAuto();}}
function delay(t){return new Promise(r=>setTimeout(r,t));}
async function ignite(link){return new Promise(resolve=>{const sk=skill(link.actorId,link.skillId);modal(`${unit(link.actorId).name} / ${sk.name}`,`<p>${settings.motion?'두 원이 겹칠 때 클릭 또는 Space':'카운트다운의 ‘지금 점화’에 클릭 또는 Space'}</p><div class="ignite-area"><div class="ring"></div><div class="ring moving"></div><div class="ignite-grade"></div></div>`,'');game.querySelector('.modal').classList.add('ignite-modal');const area=game.querySelector('.ignite-area'),moving=area.querySelector('.moving'),out=area.querySelector('.ignite-grade');const start=performance.now();let done=false;const frame=()=>{if(done)return;const time=performance.now()-start;const diameter=330-150*time/1100;if(settings.motion)moving.style.width=moving.style.height=Math.max(35,diameter)+'px';else{moving.hidden=true;out.textContent=Math.abs(time-1100)<130?'지금 점화':time<1100?((1100-time)/1000).toFixed(1):'늦음';}if(time>1850)complete(null);else requestAnimationFrame(frame);};const complete=stamp=>{if(done)return;done=true;removeEventListener('keydown',key);area.removeEventListener('pointerdown',tap);const err=stamp===null?Infinity:Math.abs(stamp-start-1100),factor=(1-data.battle.gloom*.1)*(data.battle.lanternQueued?1.2:1);const grade=err<=80*factor?'Perfect':err<=200*factor?'Good':'Miss';out.textContent={Perfect:'점 화',Good:'양 호',Miss:'빗나감'}[grade];combatSound('ignite',0,{actorId:link.actorId,skillNumber:sk.number||0,skillId:sk.id});setTimeout(()=>{closeModal();resolve(grade);},460);};const key=e=>{if(e.code==='Space'){e.preventDefault();complete(performance.now());}};const tap=()=>complete(performance.now());addEventListener('keydown',key);area.addEventListener('pointerdown',tap);requestAnimationFrame(frame);});}
async function resolveRound(){
 if(busy||page!=='battle'||game.querySelector('.modal'))return;
 pauseBattleAuto();autoBattleWaiting=false;manualSlotFocus=false;busy=true;page='resolve';cleanupDrag();battleFx.execution(true);game.querySelectorAll('button').forEach(b=>b.disabled=true);
 const token=viewToken;
 try{
  const grades={};
  for(const l of [...data.battle.links].sort((a,b)=>skill(b.actorId,b.skillId).speed-skill(a.actorId,a.skillId).speed))grades[l.actorId+':'+l.slot]=settings.assisted||autoBattle||skill(l.actorId,l.skillId).kind!=='Attack'?'Good':await ignite(l);
  if(!await command('resolve',{grades})){busy=false;showBattle();return;}
  // Keep the pre-round DOM and health until the matching authoritative event lands.
  if(!await battleFx.play(data.battle.events,data.battle,{fast:autoBattle,holdDock:true})||token!==viewToken){busy=false;if(token===viewToken)showBattle();return;}
  if(data.battle.outcome!=='Ongoing'){if(data.battle.outcome==='Victory')await command('reward');busy=false;showResult();return;}
  const previousSlots=new Map(data.battle.allies.map(a=>[a.id,actionCount(a)]));
  if(!await command('next')){busy=false;showBattle();return;}
  const gains=data.battle.allies.filter(a=>a.hp>0&&actionCount(a)>(previousSlots.get(a.id)||1)).map(a=>a.name);
  if(gains.length)toast(gains.join(' · ')+'의 두 번째 행동이 열렸습니다.');
  if(autoBattle&&!await command('recommend')){busy=false;showBattle();return;}
  busy=false;autoBattleWaiting=autoBattle;showBattle();
 }catch(e){busy=false;autoBattleWaiting=false;toast(e.message);if(token===viewToken)showBattle();}
 finally{if(token===viewToken)battleFx.execution(false);}
}
function showResult(){
 const win=data.battle.outcome==='Victory',campaign=!!data.campaign?.activeStage,follow=win&&data.campaign?.phase==='AfterStory';
 const action=win?(follow?'campaign-story':campaign?'go:stages':'story:epilogue'):'deploy',label=win?(follow?'후속 이야기 →':campaign?'다음 스테이지 →':'남겨진 기억 읽기 →'):'다시 도전하기 →';
 setScreen('result',`${hero(data.battle.allies[0]?.portrait||data.battle.allies[0]?.id||'Lian','result-hero')}<div class="result-info"><div class="eyebrow">${win?'THE LIGHT REMAINS':'A MOMENT IN THE DARK'}</div><h1>${win?'지켜낸 불씨':'아직, 끝이 아니야'}</h1><p>${win?(follow?'남겨진 대화를 마치면 다음 스테이지가 열립니다.':'우리가 지나온 길에 또 하나의 기록이 남았다.'):'빛실을 다시 잇고, 다른 길을 찾아보자.\n정신력과 속성 우세를 살펴 맞불을 연결하세요.'}</p>${win&&lastReward?`<div class="rewards"><div class="reward"><b>+${lastReward.coins}</b>코인</div><div class="reward"><b>+${lastReward.lanternFragments}</b>등불 조각</div><div class="reward"><b>+${lastReward.memoryFragments}</b>기억의 파편</div></div>`:''}${btn(label,action,'primary')}${btn('등대로','go:hub')}</div>${footer('진행 상황 자동 저장 · 기록실에서 완료한 이야기를 다시 볼 수 있습니다')}`);
}
game.addEventListener('click',async e=>{const button=e.target.closest('[data-action]');if(!button){if(page==='title')await beginFromTitle();return;}if(button.disabled)return;const action=button.dataset.action;if(campaignBusy||collectionBusy)return;if(dragInfo?.moved||performance.now()<suppressClickUntil)return;if(action==='begin')return beginFromTitle();tone();try{if(action.startsWith('go:'))return go(action.slice(3));if(action.startsWith('chapter:'))return showStages(+action.slice(8),'');if(action.startsWith('stage-select:'))return showStages(selectedChapter,action.slice(13));if(action.startsWith('stage-enter:'))return enterCampaign(action.slice(12));if(action.startsWith('stage-replay:'))return enterCampaign(action.slice(13),true);if(action.startsWith('banner:')){if(page==='recruitment'&&data.recruitment.banners.some(b=>b.id===action.slice(7))){selectedBanner=action.slice(7);return showRecruitment();}return;}if(action.startsWith('recruit:'))return pullRecruitment(Number(action.slice(8)));if(action.startsWith('party-picker:'))return showPartyPicker(action.slice(13));if(action.startsWith('equip:')){const [,actorId,slot]=action.split(':');return equipCharacter(actorId,Number(slot));}if(action.startsWith('synchronize:'))return synchronizeCharacter(action.slice(12));if(action.startsWith('focus-chapter:'))return focusMainChapter(action.slice(14));if(action.startsWith('character:'))return showRoster(+action.split(':')[1]);if(action.startsWith('story:'))return startStory(action.slice(6));if(action.startsWith('replay:'))return startStory(action.slice(7),'archive');if(action.startsWith('setting:')){const id=action.slice(8);settings[id]=!settings[id];if(id==='sound'){if(settings.sound)unlockCombatAudio();else combatAudio?.stop();}try{localStorage.setItem('rl-settings',JSON.stringify(settings));}catch{}return showSettings();}if(action.startsWith('slot:')){const [,actor,n]=action.split(':');if(!unit(actor)||unit(actor).hp<=0||!actionOpen(unit(actor),+n))return;selectedActor=actor;selectedSlot=skill(actor,slotLink(actor,+n)?.skillId)?.slots===2?0:+n;manualSlotFocus=true;const choice=selectionForSlot(actor,selectedSlot);selectedSkill=choice.skillId;selectedCardId=choice.cardId;return showBattle();}if(action.startsWith('skill-card:')){const [,actor,...cardParts]=action.split(':'),cardId=cardParts.join(':');if(selectDockChoice(actor,cardId))return showBattle();return;}if(action.startsWith('defense:')){const [,actor,skillId]=action.split(':');if(selectDockChoice(actor,'',skillId))return showBattle();return;}if(action.startsWith('target:'))return await linkTarget(action.slice(7));
switch(action){case'recruit-rates':return showRecruitRates();case'recruit-roster':{const id=recruitmentResult?.pulls.find(p=>p.isNew)?.characterId||selectedBanner,roster=chars.filter(c=>!['Rowan','Ellen','Eve'].includes(c[0]));return showRoster(Math.max(0,roster.findIndex(c=>c[0].toLowerCase()===id)));}case'redeem-code':return redeemCoupon();case'storynext':return nextStory();case'storyauto':storyAuto=!storyAuto;button.textContent=storyAuto?'자동 켜짐':'자동';scheduleStoryAuto();return;case'storyskip':return finishStory();case'storylog':return modal('지나온 대화',`<div class="log story-log" tabindex="0" aria-label="현재까지 읽은 대화">${esc(storyLog.filter(Boolean).join('\n\n'))||'아직 읽은 대화가 없습니다.'}</div>`);case'close':return closeModal();case'campaign-story':return routeCampaign();case'deploy':if(!await command('restart'))return;selectFirstFighter();autoBattle=false;lastReward=null;return showBattle();case'recommend':await command('recommend');showBattle();toast('광역 맞불 → 부위 파괴 → 부상 회복 순서로 제안했습니다.');return;case'clear':case'reuse':await command(action);return showBattle();case'remove':await command('remove',{actorId:selectedActor,slot:selectedSlot});return showBattle();case'lantern':await command('lantern',{actorId:selectedActor});return showBattle();case'automode':autoBattle=!autoBattle;if(!autoBattle)autoBattleWaiting=false;if(autoBattle)await command('recommend');return showBattle();case'resolve':return resolveRound();case'battlelog':return modal('라운드 기록',`<div class="log">${esc(data.battle.events.length?data.battle.events.map(e=>e.message).join('\n'):data.battle.links.map(l=>unit(l.actorId).name+' → '+targetName(l.targetId)+' : '+l.reason).join('\n'))}</div>`);case'retreat':return modal('등대로 돌아갈까요?','<p>진행 중인 전투의 보상은 받을 수 없습니다.</p>',btn('전투 계속','close','primary')+btn('등대로','go:hub'));case'repair':if(await command('repair')){showHub();toast('등대의 불빛이 조금 더 따뜻해졌습니다.');}return;}}
catch(err){toast(err.message);}});
function cleanupDrag(){hideIntentTooltip();game.querySelector('#drag-thread')?.setAttribute('d','');battleFx.drag(null,[],null,null);dragInfo=null;}
game.addEventListener('pointerover',e=>{const node=e.target.closest('.intent-tile');if(node&&!node.contains(e.relatedTarget))showIntentTooltip(node.dataset.intentId,node);});
game.addEventListener('pointerout',e=>{const node=e.target.closest('.intent-tile');if(node&&node===intentTipOwner&&!node.contains(e.relatedTarget)&&document.activeElement!==node)hideIntentTooltip();});
game.addEventListener('focusin',e=>{const node=e.target.closest('.intent-tile');if(node)showIntentTooltip(node.dataset.intentId,node);});
game.addEventListener('focusout',e=>{const node=e.target.closest('.intent-tile');if(node&&node===intentTipOwner&&!node.contains(e.relatedTarget))hideIntentTooltip();});
function validDropTargets(sk){
 if(!sk)return [];
 if(['Heal','Guard','Evade','Counter'].includes(sk.kind))return data.battle.allies.filter(u=>u.hp>0).map(u=>u.id);
 if(sk.kind!=='Attack')return [];
 const enemies=data.battle.enemies.filter(u=>u.hp>0);
 return [...data.battle.intents.filter(i=>!i.cancelled&&!i.resolved&&enemies.some(u=>u.id===i.actorId)).map(i=>i.id),...enemies.flatMap(u=>[u.id,...u.parts.filter(p=>p.hp>0).map(p=>p.id)])];
}
game.addEventListener('pointerdown',e=>{
 hideIntentTooltip();
 const source=e.target.closest('.skill-card,.slot');if(!source||source.disabled||busy||campaignBusy||page!=='battle'||dragInfo||game.querySelector('.modal'))return;
 const actor=source.dataset.actor;let slotIndex,choice;
 if(source.dataset.cardId){const candidate=dockChoice(actor,source.dataset.cardId);if(!candidate||candidate.disabled)return;slotIndex=candidate.slot;choice={skillId:candidate.k.id,cardId:candidate.card.id};}
 else{if(!actionOpen(unit(actor),+source.dataset.slot))return;slotIndex=skill(actor,slotLink(actor,+source.dataset.slot)?.skillId)?.slots===2?0:+source.dataset.slot;const sameSlot=selectedActor===actor&&selectedSlot===slotIndex;choice=selectionForSlot(actor,slotIndex,sameSlot);}
 if(!choice.skillId||!actionFits(unit(actor),slotIndex,skill(actor,choice.skillId)))return;
 dragInfo={x:e.clientX,y:e.clientY,pointerId:e.pointerId,actor,slot:slotIndex,skillId:choice.skillId,cardId:choice.cardId,origin:source.dataset.cardId?{x:e.clientX,y:e.clientY}:null,moved:false};
});
addEventListener('pointermove',e=>{
 if(!dragInfo||(dragInfo.pointerId!==undefined&&e.pointerId!==dragInfo.pointerId))return;
 if(Math.hypot(e.clientX-dragInfo.x,e.clientY-dragInfo.y)>8)dragInfo.moved=true;if(!dragInfo.moved)return;
 const r=game.getBoundingClientRect(),p={x:(e.clientX-r.left)*1600/r.width,y:(e.clientY-r.top)*900/r.height},a=dragInfo.origin?{x:(dragInfo.origin.x-r.left)*1600/r.width,y:(dragInfo.origin.y-r.top)*900/r.height}:slotPoint(dragInfo.actor,dragInfo.slot);
 const ids=validDropTargets(skill(dragInfo.actor,dragInfo.skillId));
 const hit=document.elementFromPoint(e.clientX,e.clientY)?.closest('[data-action^="target:"]');
 const near=ids.map(id=>({id,d:Math.hypot(targetPoint(id).x-p.x,targetPoint(id).y-p.y)})).sort((a,b)=>a.d-b.d)[0];
 const focus=hit&&!hit.disabled?(ids.includes(hit.dataset.action.slice(7))?hit.dataset.action.slice(7):null):near&&near.d<145?near.id:null;
 const end=focus?targetPoint(focus):p;
 game.querySelector('#drag-thread')?.setAttribute('d',`M${a.x},${a.y} Q${(a.x+end.x)/2},${Math.min(a.y,end.y)-65} ${end.x},${end.y}`);
 battleFx.drag(dragInfo.actor,ids,end,focus);
});
addEventListener('pointerup',async e=>{
 if(!dragInfo||(dragInfo.pointerId!==undefined&&e.pointerId!==dragInfo.pointerId))return;
 const drag=dragInfo;cleanupDrag();if(!drag.moved||busy||page!=='battle'||game.querySelector('.modal'))return;
 suppressClickUntil=performance.now()+250;if(selectedActor!==drag.actor)manualSlotFocus=false;selectedActor=drag.actor;selectedSlot=drag.slot;selectedSkill=drag.skillId;selectedCardId=drag.cardId;
 const ids=validDropTargets(skill(selectedActor,selectedSkill)),hit=document.elementFromPoint(e.clientX,e.clientY)?.closest('[data-action^="target:"]');
 let targetId;
 if(hit&&!hit.disabled){const id=hit.dataset.action.slice(7);if(ids.includes(id))targetId=id;else{showBattle();toast('선택한 스킬에 맞는 대상으로 연결해 주세요.');return;}}
 else{
  const r=game.getBoundingClientRect(),p={x:(e.clientX-r.left)*1600/r.width,y:(e.clientY-r.top)*900/r.height};
  const near=ids.map(id=>({id,d:Math.hypot(targetPoint(id).x-p.x,targetPoint(id).y-p.y)})).sort((a,b)=>a.d-b.d)[0];
  if(near&&near.d<145)targetId=near.id;
 }
 try{if(targetId)await linkTarget(targetId);else{showBattle();toast('스킬에 맞는 예고선, 부위 또는 아군 가까이에 놓아주세요.');}}catch(err){toast(err.message);}
});
addEventListener('pointercancel',e=>{if(dragInfo&&(dragInfo.pointerId===undefined||e.pointerId===dragInfo.pointerId)){suppressClickUntil=performance.now()+250;cleanupDrag();}});
function suspendStoryReading(suspended){storySuspended=suspended;if(suspended){pauseStoryTyping();pauseStoryAuto();}else{resumeStoryTyping();scheduleStoryAuto();}}
addEventListener('blur',()=>{titleStartKeyHeld='';cleanupDrag();suspendStoryReading(true);});
addEventListener('focus',()=>suspendStoryReading(!!document.hidden));
document.addEventListener('visibilitychange',()=>suspendStoryReading(!!document.hidden||document.hasFocus?.()===false));
addEventListener('keyup',e=>{if(titleStartKeyHeld===e.code)titleStartKeyHeld='';});
addEventListener('keydown',e=>{
 if(collectionBusy){if(['Enter','NumpadEnter','Space','Escape'].includes(e.code))e.preventDefault?.();return;}
 if(page==='settings'&&e.code==='Enter'&&e.target?.id==='coupon-code'){e.preventDefault();if(!e.repeat)void redeemCoupon();return;}
 if(titleStartKeyHeld===e.code){e.preventDefault();return;}
 if(page==='title'&&['Enter','NumpadEnter','Space'].includes(e.code)){
  const control=e.target?.closest?.('button,a,input,textarea,select,[contenteditable="true"]')||document.activeElement?.closest?.('button,a,input,textarea,select,[contenteditable="true"]');
  if(control&&control.dataset?.action!=='begin')return;
  e.preventDefault();if(e.repeat||e.isComposing)return;titleStartKeyHeld=e.code;void beginFromTitle();return;
 }
 if(e.code==='Space'&&page==='story'){e.preventDefault();nextStory();}if(e.code==='Escape'&&!busy&&!campaignBusy){if(game.querySelector('.modal'))closeModal();else if(page==='battle')modal('등대로 돌아갈까요?','<p>진행 중인 전투는 종료됩니다.</p>',btn('전투 계속','close','primary')+btn('등대로','go:hub'));else if(page==='settings')go(settingsReturn);else if(page!=='title')go('hub');}});
async function loadCombatLayouts(){
 const layouts={};
 await Promise.all(Object.keys(LanternCombatAtlasNames).map(async id=>{
  try{const response=await fetch(`/combat/${LanternCombatAtlasStem(id)}Layout.json`);if(!response.ok)return;const layout=await response.json();
   if(layout.bodyHeight>0&&layout.frames?.length===6&&layout.frames.every(f=>['x','y','width','height','footX','footY'].every(k=>Number.isFinite(f[k]))&&f.width>0&&f.height>0))layouts[id]=layout;
  }catch{}
 }));battleFx.options.layouts=layouts;
}
async function boot(){game.innerHTML='<div class="loading">마지막 불씨를 불러오는 중…</div>';try{const r=await fetch('/api/state');if(!r.ok)throw Error('로컬 전투 서버에 연결하지 못했습니다.');data=normalizeState(await r.json());await loadCombatLayouts();showTitle();if(data.warning)toast(data.warning);}catch(e){game.innerHTML=`<div class="loading">${esc(e.message)}<br>Start-Preview.ps1로 실행해 주세요.</div>`;}}
boot();
