'use strict';
const game=document.querySelector('#game');
let data,page='title',selectedActor='lian',selectedSlot=0,selectedSkill='lian_glimmer',selectedCardId='',autoBattle=false;
let story=[],storyIndex=0,storyReturn='hub',storyKey='',storyLog=[],typingTimer,autoTimer,storyAuto=false,typed=false,storyFinishing=false;
let selectedChapter=1,selectedStage='',rosterIndex=0,campaignBusy=false;
function loadSettings(){const defaults={assisted:true,motion:true,sound:true};try{const saved=JSON.parse(localStorage.getItem('rl-settings')||'null');for(const key of Object.keys(defaults))if(typeof saved?.[key]==='boolean')defaults[key]=saved[key];}catch{}return defaults;}
let settings=loadSettings(),settingsReturn='hub';
let busy=false,viewToken=0,lastReward=null,dragInfo=null,toastTimer,context,combatAudio,battleAutoTimer,autoBattleWaiting=false,suppressClickUntil=0;
const chars=[
 ['Lian','리안','광휘 · 수호자','은백색 머리의 파수꾼.\n소등의 날 이후, 13년간 홀로 등대 요새를 지켰다.\n파편의 대가로 어린 시절의 기억 대부분을 잃었다.','…살아 있으면, 그걸로 됐어.'],
 ['Sera','세라','화염 · 돌격수','사라진 고아원 아이들을 찾아 요새에 합류했다.\n밝고 큰 웃음 뒤에 남겨진 마음을 감추고 있다.','괜찮아 괜찮아! 어둠 따위, 내가 태워버릴 테니까!'],
 ['Minato','미나토','물결 · 치유사','나른하고 다정한 의사 지망생.\n치료하지 못한 환자들의 이름을 모두 기억한다.','잃어버린 이름을, 잊지 않으려고.'],
 ['Heidel','하이델','서리 · 사수','고글과 개조 석궁을 쓰는 실용주의 기술자.\n등대 요새의 공방을 맡는다.','요새의 공방을 맡은 실용주의 기술자.'],
 ['Noa','노아','그림자 · 조율사','철종 교단에서 도망친 수수께끼의 잔영.\n그림자와 대화할 수 있다.','그림자와 대화하는 수수께끼의 잔영.'],
 ['Kai','카이','등불지기','얼어붙은 폐허에서 기억을 잃고 깨어났다.\n잔영들의 빛을 증폭하고 조율한다.','손에 남은 것은, 꺼져가는 등불 하나.'],
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
const battleFx=new LanternCombatFX(game,{motion:()=>settings.motion,impact:combatSound,stopSound:()=>combatAudio?.stop(),color});
const element=e=>({Radiance:'광휘',Flame:'화염',Tide:'물결',Frost:'서리',Shadow:'그림자'}[e]);
function resize(){game.style.transform=`scale(${Math.min(innerWidth/1600,innerHeight/900)})`;}
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
async function command(action,extra={}){const r=await fetch('/api/action',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({action,...extra})});const d=await r.json();if(d.battle)data=normalizeState(d);if(d.error){toast(d.error);return false;}if(!r.ok)throw Error(d.error||'전투 처리에 실패했습니다.');if(d.reward)lastReward=d.reward;return true;}
function pauseStoryAuto(){clearTimeout(autoTimer);autoTimer=null;}
function scheduleStoryAuto(){pauseStoryAuto();if(page==='story'&&storyAuto&&typed&&!game.querySelector('.modal'))autoTimer=setTimeout(()=>{autoTimer=null;nextStory();},2600);}
function pauseBattleAuto(){clearTimeout(battleAutoTimer);battleAutoTimer=null;}
function scheduleBattleAuto(){pauseBattleAuto();if(page!=='battle'||busy||!autoBattle||!autoBattleWaiting||game.querySelector('.modal')||data.battle.outcome!=='Ongoing')return;const token=viewToken;battleAutoTimer=setTimeout(()=>{battleAutoTimer=null;if(token===viewToken&&page==='battle'&&!busy&&autoBattle&&autoBattleWaiting&&!game.querySelector('.modal'))resolveRound();},1100);}
function setBackdrop(asset){game.style.backgroundImage="linear-gradient(90deg,#0a101de0,#0b142443 70%,#090f1b65),url('/art/"+asset+".png')";}
function setScreen(name,html){battleFx.clear();setBackdrop(['hub','archive','settings'].includes(name)?'Keep':'Background');cleanupDrag();clearInterval(typingTimer);pauseStoryAuto();pauseBattleAuto();viewToken++;page=name;if(name!=='battle')autoBattleWaiting=false;game.classList.toggle('reduce-motion',!settings.motion);game.innerHTML=`<div class="screen ${name}">${html}</div>`;if(name==='battle')scheduleBattleAuto();}
async function go(name){
 if((busy&&page==='resolve')||campaignBusy)return;
 const leavingStage=data.campaign?.activeStage&&['hub','title','stages','archive'].includes(name);
 if(leavingStage){
  campaignBusy=true;const token=viewToken;
  try{if(!await command('stage-exit',{stageId:data.campaign.activeStage.id})||token!==viewToken)return;}
  catch(error){toast(error.message);return;}finally{campaignBusy=false;}
 }
 if(name==='settings'&&page!=='settings')settingsReturn=page;
 const map={title:showTitle,hub:showHub,stages:showStages,roster:()=>showRoster(0),settings:showSettings,archive:showArchive,battle:showBattle};map[name]?.();
}
function showTitle(){setScreen('title',`<div class="title-copy"><div class="eyebrow">T H E &nbsp; L A S T &nbsp; L I G H T</div><h1>REQUIEM<br>LANTERN</h1><div class="rule"></div><h2>레퀴엠 랜턴</h2><p>태양이 꺼진 지 13년.<br>우리가 잃어버린 것들은, 아직 빛나고 있다.</p><div class="title-actions">${btn(data.progress.prologueCompleted?'등대 요새로 돌아가기 &nbsp; →':'마지막 불씨를 따라서 &nbsp; →','begin','primary')}${btn('설정','go:settings')}</div></div><div class="title-inscription">기억이 사라져도<br>등불은 남는다.</div>${footer('초기 플레이 프리뷰 · Unity와 같은 C# 전투 로직')}`);}
function hubCard(n,title,sub,action,primary=false){return `<button class="hub-card ${primary?'primary':''}" data-action="${action}"><span class="num">${n}</span><span><strong>${title}</strong><small>${sub}</small></span><span class="arrow">→</span></button>`;}
function showHub(){const chapters=campaignChapters(),next=campaignStages().find(s=>s.unlocked&&!s.cleared),chapter=chapters.find(c=>c.number===next?.chapter)||chapters[chapters.length-1];setScreen('hub',`${header('BEACON KEEP / 영야의 해안','등대 요새','title')}${hero('Lian','hub-hero')}<div class="hub-quote"><div class="eyebrow">등대 복구 &nbsp; ${data.progress.keepLevel}단계</div><p>“…살아 있으면, 그걸로 됐어.”</p><div class="name">리안 &nbsp; / &nbsp; 등대의 파수꾼</div></div><div class="hub-side"><div class="eyebrow">MAIN STORY</div><h1>${esc(chapterFocusLabel(chapter)||'함께 남긴 이야기')}</h1><p>${esc(chapter?.focusStorySummary||'이 작은 불을, 함께 지키기로 했다.')}</p>${hubCard('01','작전실','예고선을 읽고, 빛실을 잇다','go:stages',true)}${hubCard('02','잔영 기록','별의 파편을 품은 동료들','go:roster')}${hubCard('03','함께 남긴 이야기','장과 스테이지를 따라 이어지는 동료들의 기록','go:archive')}<div class="hub-nav">${btn('기록실','go:archive')}${btn('등대 복구','repair')}${btn('설정','go:settings')}</div></div>${footer('등불지기 카이 · 작은 온기에서 시작되는 이야기')}`);}
function startStory(key,ret='hub'){if(!key.startsWith('stage:')&&!['prologue','firstMeal','epilogue'].includes(key)){toast('이야기는 메인 스토리의 스테이지에서 이어집니다.');return;}const lines=key.startsWith('stage:')?data.campaign?.currentStory:data.story?.[key];if(!lines?.length){toast('이야기를 불러오지 못했습니다.');return;}story=lines;storyKey=key;storyIndex=0;storyReturn=ret;storyLog=[];storyAuto=false;storyFinishing=false;drawStory();}
function drawStory(){if(storyFinishing)return;if(storyIndex>=story.length){finishStory();return;}const l=story[storyIndex];setScreen('story',RequiemStoryView.render({line:l,key:storyKey,index:storyIndex,total:story.length,auto:storyAuto,location:l.location,caption:l.caption,portraitHtml:l.portrait?hero(l.portrait,'story-hero'):'',listenerHtml:l.listenerPortrait&&l.listenerPortrait!==l.portrait?hero(l.listenerPortrait,'story-listener'):'',footerHtml:footer('Space / 대화창 클릭으로 계속')}));if(l.background==='keep')setBackdrop('Keep');const words=game.querySelector('.words');let count=0;typed=false;storyLog[storyIndex]=`${l.speaker||'기억'}  ${l.text}`;typingTimer=setInterval(()=>{words.textContent=l.text.slice(0,++count);if(count>=l.text.length){clearInterval(typingTimer);typed=true;scheduleStoryAuto();}},24);}
function nextStory(){if(page!=='story'||storyFinishing||game.querySelector('.modal'))return;if(!typed){clearInterval(typingTimer);game.querySelector('.words').textContent=story[storyIndex].text;typed=true;scheduleStoryAuto();return;}storyIndex++;drawStory();}
async function finishStory(){
 if(storyFinishing||page!=='story')return;
 storyFinishing=true;clearInterval(typingTimer);pauseStoryAuto();const token=viewToken,key=storyKey;
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
function showStages(chapter=selectedChapter,stageId=selectedStage){
 const chapters=campaignChapters().filter(c=>c.stages.some(s=>s.unlocked||s.cleared)),current=chapters.find(c=>c.number===Number(chapter))||chapters[0];
 if(!current){setScreen('stages',`${header('MAIN STORY','작전실')}<div class="loading">스테이지를 불러오는 중입니다.</div>${footer('메인 스토리')}`);return;}
 selectedChapter=current.number;
 const visibleStages=current.stages.filter(s=>s.unlocked||s.cleared);
 const stage=visibleStages.find(s=>s.id===stageId)||visibleStages.find(s=>s.unlocked&&!s.cleared)||visibleStages[0];selectedStage=stage.id;
 const status=stage.cleared?'완료한 이야기':stage.unlocked?'진행 가능':'앞선 스테이지를 완료하면 열립니다';
 const cast=(stage.participants||[]).map(id=>chars.find(c=>c[0].toLowerCase()===id)?.[1]||id).join(' · ');
 setScreen('stages',`${header('MAIN CAMPAIGN / 메인 스토리','작전실')}<nav class="chapter-tabs" aria-label="장 선택">${chapters.map(c=>btn(`<small>CHAPTER ${String(c.number).padStart(2,'0')}${chapterFocusLabel(c)?' · '+esc(chapterFocusLabel(c)):''}</small><strong>${esc(chapterDisplayTitle(c))}</strong>`,`chapter:${c.number}`,c.number===current.number?'primary':'')).join('')}</nav><div class="campaign-heading"><span class="eyebrow">제${current.number}장${chapterFocusLabel(current)?' · '+esc(chapterFocusLabel(current)):''} · ${current.stages.filter(s=>s.cleared).length} / ${current.stages.length}</span><p>${esc(stage.summary)}</p></div><div class="campaign-map" aria-label="${current.number}장 스테이지">${visibleStages.map(s=>btn(`<span class="stage-node-number">${esc(displayStageNumber(s))}</span><span class="stage-node-kind">${stageLabel(s)}</span><strong>${esc(s.title)}</strong><small>${s.cleared?'✓ 완료':s.unlocked?'진행 가능':'잠김'}</small>`,`stage-select:${s.id}`,`campaign-node ${s.cleared?'cleared':''} ${s.unlocked?'':'locked'} ${s.id===selectedStage?'selected':''}`,`aria-pressed="${s.id===selectedStage}"`)).join('')}</div><aside class="campaign-detail"><div class="eyebrow">${esc(displayStageNumber(stage))} · ${stageLabel(stage)}</div><h2>${esc(stage.title)}</h2><div class="stage-place">${esc(stage.location)}</div><p>${esc(stage.summary)}</p><div class="stage-cast">${esc(cast)}</div>${stage.kind!=='Story'?`<div class="stage-opponent">${stage.humanEnemy?'인간 적대 세력':'엄브라'} · ${esc(stage.enemyName)}<br>출격 ${(stage.party||[]).map(id=>chars.find(c=>c[0].toLowerCase()===id)?.[1]||id).join(' · ')}</div>`:''}<small class="stage-status">${status}</small>${btn(stage.cleared?'다시 출격 →':stage.kind==='Story'?'이야기 시작 →':'스테이지 시작 →',`stage-enter:${stage.id}`,'primary',stage.unlocked?'':'disabled')}${stage.cleared?btn('이야기 다시 보기',`stage-replay:${stage.id}`,'stage-replay'):''}</aside>${footer('이야기 완료 → 전투 → 후속 이야기 · 스테이지 종류에 따라 순서가 달라집니다')}`);
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
function selectionForSlot(actorId,slot,preserve=false){
 const actor=unit(actorId),linked=slotLink(actorId,slot),current=preserve&&cardFor(actor,selectedCardId);
 if(current&&!cardReserved(actorId,current.id,slot))return{skillId:current.skillId,cardId:current.id};
 if(preserve&&defenseSkill(skill(actorId,selectedSkill)))return{skillId:selectedSkill,cardId:''};
 if(linked&&defenseSkill(skill(actorId,linked.skillId)))return{skillId:linked.skillId,cardId:''};
 const linkedCard=linked&&cardFor(actor,linked.cardId);
 if(linkedCard)return{skillId:linkedCard.skillId,cardId:linkedCard.id};
 const card=handFor(actor).find(c=>!cardReserved(actorId,c.id,slot)&&!skillLocked(actor,skill(actorId,c.skillId)||{}));
 if(card)return{skillId:card.skillId,cardId:card.id};
 const defense=actor.skills.find(defenseSkill);return{skillId:defense?.id||actor.skills[0]?.id,cardId:''};
}
function selectFirstFighter(){const actor=data.battle.allies.find(a=>a.hp>0)||data.battle.allies[0];if(!actor)return;selectedActor=actor.id;selectedSlot=0;const choice=selectionForSlot(actor.id,0);selectedSkill=choice.skillId;selectedCardId=choice.cardId;}
const characterProgress=id=>(data.progress.characters||[]).find(c=>String(c.id).toLowerCase()===String(id).toLowerCase())||{id,level:1,syncLevel:0};
const skillLocked=(actor,k)=>(k.requiredSyncLevel||0)>(actor.syncLevel??characterProgress(actor.id).syncLevel??0);
const sanityText=value=>(value||0)>0?'+'+value:String(value||0);
const flameChance=value=>Math.max(10,Math.min(90,50+2*(value||0)));
function flameBadges(count,on=false,dark=false){return `<span class="flame-badges ${dark?'darkness':''}" aria-label="${dark?'어둠':'불'} ${count}개">${Array.from({length:Math.max(1,count||1)},()=>`<i class="flame ${on?'on':'off'}" aria-hidden="true"></i>`).join('')}</span>`;}
function showRoster(i){
 rosterIndex=i;const roster=chars.filter(c=>!['Rowan','Ellen','Eve'].includes(c[0])),c=roster[i],progress=characterProgress(c[0]),combat=['Lian','Sera','Minato','Heidel','Noa'].includes(c[0]),focusChapter=campaignChapters().find(chapter=>String(chapter.focusCharacterId).toLowerCase()===c[0].toLowerCase());
 setScreen('roster',`${header('AFTERGLOW ARCHIVE / 인물 기록','잔영과 등불지기')}${hero(c[0],'roster-hero')}<div class="roster-info"><div class="eyebrow">CHARACTER &nbsp; ${String(i+1).padStart(2,'0')} / ${String(roster.length).padStart(2,'0')}</div><h1>${c[1]}</h1><div class="role">${c[2]}</div><div class="bio">${c[3]}</div><blockquote>“${c[4]}”</blockquote>${combat?`<div class="character-growth"><span>레벨 ${progress.level} · 동기화 ${progress.syncLevel}</span><small>${progress.syncLevel>0?'3스킬 개방':'동기화하면 3스킬이 열립니다'}</small>${btn(progress.syncLevel>0?'동기화 완료':'동기화 · 등불 조각 10',`synchronize:${c[0].toLowerCase()}`,'primary',progress.syncLevel>0||data.progress.lanternFragments<10?'disabled':'')}</div>`:''}${focusChapter?`<div class="roster-story-entry">${btn('메인 스토리 · '+c[1]+'의 장 보기','focus-chapter:'+c[0].toLowerCase())}<small>제${focusChapter.number}장의 스테이지로 이어지는 이야기${chapterReachable(focusChapter)?'':' · 앞선 스테이지 완료 후 개방'}</small></div>`:''}<div class="roster-tabs">${roster.map((c,n)=>btn(c[1],'character:'+n,n===i?'primary':'')).join('')}</div></div>${footer('동기화는 레벨과 별도로 진행됩니다 · 3스킬 개방')}`);
}
function showSettings(){setScreen('settings',`${header('PREFERENCES','환경 설정',settingsReturn)}<div class="settings"><h1>편안한 속도로, 빛을 잇도록.</h1>${[['assisted','점화 타이밍 보조','자동으로 ‘양호’ 판정. 끄면 클릭 / Space로 점화합니다.'],['motion','움직임과 전투 효과','빛실 흐름, 타격 움직임, 화면 흔들림을 재생합니다. 끄면 간소하게 표시합니다.'],['sound','효과음','무기 충돌, 타격, 불씨와 장비 효과음을 재생합니다.']].map(([id,title,desc])=>`<div class="setting"><div><strong>${title}</strong><p>${desc}</p></div>${btn(settings[id]?'켜짐':'꺼짐','setting:'+id,settings[id]?'primary':'')}</div>`).join('')}</div>${footer('진행은 로컬 서버에 저장 · 설정은 이 브라우저에 저장')}`);}
function showArchive(){
 const cleared=campaignStages().filter(s=>s.cleared),chapters=campaignChapters().filter(c=>c.stages.some(s=>s.cleared));
 setScreen('archive',`${header('MAIN STORY / MEMORY ARCHIVE','기록실')}<div class="campaign-archive"><div class="eyebrow">함께 지나온 이야기 · ${cleared.length} / ${campaignStages().length}</div><h1>빛은 기억을 품는다.</h1><p>완료한 스테이지의 대화를 다시 펼쳐 봅니다.</p><div class="archive-stage-list">${chapters.map(c=>`<section><h2>제${c.number}장${chapterFocusLabel(c)?' · '+esc(chapterFocusLabel(c)):''} · ${esc(chapterDisplayTitle(c))}</h2>${c.stages.filter(s=>s.cleared).map(s=>btn(`<span>${esc(displayStageNumber(s))}</span><strong>${esc(s.title)}</strong><small>${stageLabel(s)}</small>`,`stage-replay:${s.id}`)).join('')}</section>`).join('')}</div></div>${footer('이야기 다시 보기는 전투와 보상 없이 재생합니다')}`);
}
const unit=id=>data.battle.allies.concat(data.battle.enemies).find(u=>u.id===id);
const skill=(actor,id)=>unit(actor)?.skills.find(s=>s.id===id);
const intent=id=>data.battle.intents.find(i=>i.id===id);
function targetName(id){return intent(id)?.name||unit(id)?.name||data.battle.enemies.flatMap(u=>u.parts).find(p=>p.id===id)?.name||id;}
function actorPoint(id){const i=data.battle.allies.findIndex(a=>a.id===id);return {x:172+i*265,y:446};}
function targetPoint(id){let i=data.battle.intents.findIndex(a=>a.id===id);if(i>=0)return {x:945,y:270+i*122};if(data.battle.allies.some(a=>a.id===id))return actorPoint(id);i=data.battle.enemies[0].parts.findIndex(a=>a.id===id);if(i>=0)return {x:1190+i*227,y:550};return {x:1360,y:447};}
function slotPoint(actor,slot){return{x:113+data.battle.allies.findIndex(a=>a.id===actor)*340+slot*146,y:756};}
function path(a,b,cls,c='',intentId=''){return `<path class="${cls}" ${intentId?`data-intent-line="${esc(intentId)}"`:''} d="M${a.x},${a.y} Q${(a.x+b.x)/2},${Math.min(a.y,b.y)-65} ${b.x},${b.y}" ${c?`style="stroke:${c}"`:''}/>`;}
function showBattle(){
 const s=data.battle,e=s.enemies[0];if(!unit(selectedActor)||unit(selectedActor).isEnemy)selectFirstFighter();
 const selected=unit(selectedActor),sk=skill(selectedActor,selectedSkill)||selected.skills.find(k=>!skillLocked(selected,k))||selected.skills[0];selectedSkill=sk.id;
 if(selectedSlot>=(selected.slotCount||2))selectedSlot=0;
 let lines='';
 for(const i of s.intents){const p=targetPoint(i.id);for(const a of i.kind==='Area'?s.allies:[unit(i.targetId)].filter(Boolean))lines+=path(p,actorPoint(a.id),'thread-enemy','',i.id);}
 for(const l of s.links){let c='#f2a541';if(l.kind==='Clash'){const v=data.previews?.find(p=>p.actorId===l.actorId&&p.slot===l.slot);c=v?.grade==='Advantage'?'#75c8c4':v?.grade==='Disadvantage'?'#e77a5d':'#f2a541';}lines+=path(slotPoint(l.actorId,l.slot),targetPoint(l.targetId),'thread-player flow',c);}
 const choice=selectionForSlot(selectedActor,selectedSlot,true);
 if(!defenseSkill(sk)&&(!cardFor(selected,selectedCardId)||cardReserved(selectedActor,selectedCardId,selectedSlot))){selectedSkill=choice.skillId;selectedCardId=choice.cardId;}
 const skills=handFor(selected).map((card,index)=>{
  const k=skill(selectedActor,card.skillId);if(!k)return'';
  const replace=s.links.filter(l=>l.actorId===selectedActor&&(l.slot===selectedSlot||k.slots===2||skill(l.actorId,l.skillId)?.slots===2)).reduce((sum,l)=>sum+(skill(l.actorId,l.skillId)?.cost||0),0),locked=skillLocked(selected,k),reserved=cardReserved(selectedActor,card.id,selectedSlot);
  return btn(`<span>${k.number}스킬 · ${esc(k.name)}</span>${flameBadges(k.flameCount||1)}<small>${reserved?'연결 중':'불씨 '+k.cost}</small>`,`skill-card:${card.id}`,card.id===selectedCardId?'primary':'',`data-card-id="${esc(card.id)}" title="제시된 카드 ${index+1} · 기본 위력 ${k.basePower||0} · 켜진 불마다 +${k.flamePower||0}" ${(locked||reserved||k.cost>data.availableEmber+replace||k.slots===2&&selectedSlot!==0)?'disabled':''}`);
 }).join('')+selected.skills.filter(defenseSkill).map(k=>btn(`<span>${esc(k.name)}</span>${flameBadges(k.flameCount||1)}<small>방어 행동</small>`,`skill:${k.id}`,k.id===selectedSkill&&!selectedCardId?'primary':'',`title="기본 위력 ${k.basePower||0} · 켜진 불마다 +${k.flamePower||0}"`)).join('');
 setScreen('battle',`<header>${btn('〈','retreat','back','aria-label="퇴각"')}<div class="titles"><div class="heading">${esc(s.stageName||'요새 앞의 그림자')}</div><div class="phase">ROUND ${String(s.round).padStart(2,'0')} &nbsp; / &nbsp; ② 연결</div></div><div class="resources"><span class="gold">불씨 ${s.ember} / 6 <small class="muted">예약 ${data.plannedCost}</small></span><span>등불 ${s.lantern} / 100</span><span class="muted">어둠도 ${s.gloom}</span></div></header>
 <div class="battle-controls">${btn('AI 제안','recommend')}${btn('이전 연결','reuse')}${btn('초기화','clear')}${btn(autoBattle?'오토 켜짐':'수동 연결','automode',autoBattle?'primary':'')}${btn(s.lanternQueued?'해방 예약':'등불 해방','lantern','',s.lantern<100||s.lanternQueued?'disabled':'')}${btn('기록','battlelog')}</div>
 <div class="battle-help">정신력이 높을수록 불이 켜집니다. 맞불에서 두 번 연속 이기면 공격합니다.<br>슬롯을 드래그하여 연결하세요. 속성 우세는 기존 상성으로 판정합니다.</div><svg class="threads" viewBox="0 0 1600 900" aria-hidden="true">${lines}<path id="drag-thread" class="thread-player"/></svg>
 ${s.allies.map((a,i)=>`<img src="/art/${art(a.portrait||a.id)}.png" data-unit="${a.id}" class="field-hero ${settings.motion?'breathe':''} ${a.hp<=0?'dead':''}" style="left:${40+i*265}px;animation-delay:${-i}s" alt="${esc(a.name)}">${btn('','target:'+a.id,'ally-target',`style="left:${50+i*265}px" aria-label="${esc(a.name)}에게 지원 연결"`)}<div class="field-name" style="left:${50+i*265}px">${esc(a.name)}<div class="bar"><span data-hp-bar="${a.id}" style="background:${color(a.element)};width:${a.hp/a.maxHp*100}%"></span></div></div>`).join('')}
 <div class="enemy-stats"><div class="name">${e.isHuman?'HUMAN':'UMBRA'} / ${esc(e.name)}</div><div class="bar"><span data-hp-bar="${e.id}" style="width:${e.hp/e.maxHp*100}%"></span></div><small data-hp-text="${e.id}">${e.hp} / ${e.maxHp} &nbsp; · &nbsp; 균열 ${e.rift} / ${e.maxRift}</small><div class="enemy-sanity"><span data-sanity="${e.id}">정신력 ${sanityText(e.sanity)}</span> · 어둠 발현 ${flameChance(e.sanity)}%</div></div>
 <img src="/art/${art(e.portrait||e.id)}.png" class="enemy ${e.isHuman?'human-enemy':''}" data-unit="${e.id}" alt="${esc(e.name)}">
 ${s.intents.map((i,n)=>`<button class="intent" data-action="target:${i.id}" style="top:${228+n*122}px"><strong>${esc(i.name)}${i.kind==='Area'?' · 광역':''}</strong><small>${i.flameCount?`기본 ${i.basePower} + 어둠당 ${i.flamePower}`:`위력 ${i.hitPowers.join('/')}`} &nbsp; 속도 ${i.speed}</small>${flameBadges(i.flameCount||i.hitPowers.length,false,true)}</button>`).join('')}
 <div class="parts">${e.parts.map(p=>btn(`${esc(p.name)} &nbsp; ${p.hp<=0?'파괴됨':`${p.hp}/${p.maxHp}`}`,'target:'+p.id,'',p.hp<=0?'disabled':'')).join('')}</div>${btn('본체 공격','target:'+e.id,'body-target')}
 <div class="battle-prediction">${(data.previews||[]).filter(p=>s.links.some(l=>l.actorId===p.actorId&&l.slot===p.slot&&l.kind==='Clash')).map(p=>esc(p.summary)).join('<br>')}</div>
 <div class="action-dock">${s.allies.map(a=>`<div class="fighter"><div class="fighter-title"><span style="color:${color(a.element)}">${esc(a.name)} <small>${element(a.element)}</small></span><small data-hp-text="${a.id}">${a.hp}/${a.maxHp}</small></div><div class="fighter-sanity"><span data-sanity="${a.id}">정신력 ${sanityText(a.sanity)}</span><small>점등 ${flameChance(a.sanity)}%</small></div><div class="bar"><span data-hp-bar="${a.id}" style="background:${color(a.element)};width:${a.hp/a.maxHp*100}%"></span></div><div class="slots">${Array.from({length:a.slotCount||2},(_,n)=>{const l=s.links.find(l=>l.actorId===a.id&&(l.slot===n||skill(a.id,l.skillId)?.slots===2));return `<div class="slot-wrap">${btn(esc(l?skill(a.id,l.skillId).name:`슬롯 ${n+1} +`),`slot:${a.id}:${n}`,`slot ${selectedActor===a.id&&selectedSlot===n?'active':''}`,`data-actor="${a.id}" data-slot="${n}" ${a.hp<=0?'disabled':''}`)}<div class="slot-target">${l?(l.kind==='Clash'?'맞불 · ':'')+esc(targetName(l.targetId)):'미연결'}</div></div>`;}).join('')}</div></div>`).join('')}
 <div class="skill-panel"><div class="skill-title">${esc(selected.name)} · 슬롯 ${selectedSlot+1} · 카드 구성 1×3 / 2×2${(selected.syncLevel||0)>0?' / 3×1':''}</div><div class="deck-status">제시 ${handFor(selected).length}/2 · 남은 카드 ${selected.drawPile?.length||0} · 사용한 카드 ${selected.discardPile?.length||0}</div><div class="skills">${skills}</div><div class="skill-actions">${btn('연결 해제','remove')}${btn('빛실 실행 →','resolve','primary',s.links.length?'':'disabled')}</div></div></div>${footer('보라 점선: 적 예고선 / 실선: 나의 연결 / 초록: 맞불 우세 / 주황: 접전 / 붉음: 열세')}`);battleFx.attach(s);
}
async function linkTarget(id){
 const sk=skill(selectedActor,selectedSkill);if(!sk)return;
 if(!defenseSkill(sk)&&!cardFor(unit(selectedActor),selectedCardId)){toast('제시된 스킬 카드 두 장 중에서 선택해 주세요.');return;}
 const kind=sk.kind==='Heal'?'Support':defenseSkill(sk)?'Guard':intent(id)?'Clash':'Strike';
 if(await command('link',{actorId:selectedActor,slot:selectedSlot,skillId:selectedSkill,cardId:defenseSkill(sk)?'':selectedCardId,kind,targetId:id})){showBattle();battleFx.connected(selectedActor,selectedSlot,id,kind);combatSound(kind==='Guard'?'brace':'ignite',0,{actorId:selectedActor,skillNumber:sk.number||0,skillId:sk.id,level:.45});}
}
function modal(title,body,buttons=btn('닫기','close','primary')){pauseStoryAuto();pauseBattleAuto();game.insertAdjacentHTML('beforeend',`<div class="modal"><h2>${title}</h2>${body}<div class="modal-actions">${buttons}</div></div>`);}
function closeModal(){game.querySelector('.modal')?.remove();if(!game.querySelector('.modal')){scheduleStoryAuto();scheduleBattleAuto();}}
function delay(t){return new Promise(r=>setTimeout(r,t));}
async function ignite(link){return new Promise(resolve=>{const sk=skill(link.actorId,link.skillId);modal(`${unit(link.actorId).name} / ${sk.name}`,`<p>${settings.motion?'두 원이 겹칠 때 클릭 또는 Space':'카운트다운의 ‘지금 점화’에 클릭 또는 Space'}</p><div class="ignite-area"><div class="ring"></div><div class="ring moving"></div><div class="ignite-grade"></div></div>`,'');game.querySelector('.modal').classList.add('ignite-modal');const area=game.querySelector('.ignite-area'),moving=area.querySelector('.moving'),out=area.querySelector('.ignite-grade');const start=performance.now();let done=false;const frame=()=>{if(done)return;const time=performance.now()-start;const diameter=330-150*time/1100;if(settings.motion)moving.style.width=moving.style.height=Math.max(35,diameter)+'px';else{moving.hidden=true;out.textContent=Math.abs(time-1100)<130?'지금 점화':time<1100?((1100-time)/1000).toFixed(1):'늦음';}if(time>1850)complete(null);else requestAnimationFrame(frame);};const complete=stamp=>{if(done)return;done=true;removeEventListener('keydown',key);area.removeEventListener('pointerdown',tap);const err=stamp===null?Infinity:Math.abs(stamp-start-1100),factor=(1-data.battle.gloom*.1)*(data.battle.lanternQueued?1.2:1);const grade=err<=80*factor?'Perfect':err<=200*factor?'Good':'Miss';out.textContent={Perfect:'점 화',Good:'양 호',Miss:'빗나감'}[grade];combatSound('ignite',0,{actorId:link.actorId,skillNumber:sk.number||0,skillId:sk.id});setTimeout(()=>{closeModal();resolve(grade);},460);};const key=e=>{if(e.code==='Space'){e.preventDefault();complete(performance.now());}};const tap=()=>complete(performance.now());addEventListener('keydown',key);area.addEventListener('pointerdown',tap);requestAnimationFrame(frame);});}
async function resolveRound(){
 if(busy||page!=='battle'||game.querySelector('.modal'))return;
 pauseBattleAuto();autoBattleWaiting=false;busy=true;page='resolve';cleanupDrag();game.querySelectorAll('button').forEach(b=>b.disabled=true);
 const token=viewToken;
 try{
  const grades={};
  for(const l of [...data.battle.links].sort((a,b)=>skill(b.actorId,b.skillId).speed-skill(a.actorId,a.skillId).speed))grades[l.actorId+':'+l.slot]=settings.assisted||autoBattle||skill(l.actorId,l.skillId).kind!=='Attack'?'Good':await ignite(l);
  if(!await command('resolve',{grades})){busy=false;showBattle();return;}
  // Keep the pre-round DOM and health until the matching authoritative event lands.
  if(!await battleFx.play(data.battle.events,data.battle,{fast:autoBattle})||token!==viewToken){busy=false;return;}
  if(data.battle.outcome!=='Ongoing'){if(data.battle.outcome==='Victory')await command('reward');busy=false;showResult();return;}
  if(!await command('next')){busy=false;showBattle();return;}
  if(autoBattle&&!await command('recommend')){busy=false;showBattle();return;}
  busy=false;autoBattleWaiting=autoBattle;showBattle();
 }catch(e){busy=false;autoBattleWaiting=false;toast(e.message);showBattle();}
}
function showResult(){
 const win=data.battle.outcome==='Victory',campaign=!!data.campaign?.activeStage,follow=win&&data.campaign?.phase==='AfterStory';
 const action=win?(follow?'campaign-story':campaign?'go:stages':'story:epilogue'):'deploy',label=win?(follow?'후속 이야기 →':campaign?'다음 스테이지 →':'남겨진 기억 읽기 →'):'다시 도전하기 →';
 setScreen('result',`${hero(data.battle.allies[0]?.portrait||data.battle.allies[0]?.id||'Lian','result-hero')}<div class="result-info"><div class="eyebrow">${win?'THE LIGHT REMAINS':'A MOMENT IN THE DARK'}</div><h1>${win?'지켜낸 불씨':'아직, 끝이 아니야'}</h1><p>${win?(follow?'남겨진 대화를 마치면 다음 스테이지가 열립니다.':'우리가 지나온 길에 또 하나의 기록이 남았다.'):'빛실을 다시 잇고, 다른 길을 찾아보자.\n정신력과 속성 우세를 살펴 맞불을 연결하세요.'}</p>${win&&lastReward?`<div class="rewards"><div class="reward"><b>+${lastReward.coins}</b>코인</div><div class="reward"><b>+${lastReward.lanternFragments}</b>등불 조각</div><div class="reward"><b>+${lastReward.memoryFragments}</b>기억의 파편</div></div>`:''}${btn(label,action,'primary')}${btn('등대로','go:hub')}</div>${footer('진행 상황 자동 저장 · 기록실에서 완료한 이야기를 다시 볼 수 있습니다')}`);
}
game.addEventListener('click',async e=>{const button=e.target.closest('[data-action]');if(!button||button.disabled)return;const action=button.dataset.action;if(campaignBusy)return;if(dragInfo?.moved||performance.now()<suppressClickUntil)return;tone();try{if(action.startsWith('go:'))return go(action.slice(3));if(action.startsWith('chapter:'))return showStages(+action.slice(8),'');if(action.startsWith('stage-select:'))return showStages(selectedChapter,action.slice(13));if(action.startsWith('stage-enter:'))return enterCampaign(action.slice(12));if(action.startsWith('stage-replay:'))return enterCampaign(action.slice(13),true);if(action.startsWith('synchronize:')){if(await command('synchronize',{actorId:action.slice(12)})){showRoster(rosterIndex);toast('동기화가 완료되어 3스킬이 열렸습니다.');}return;}if(action.startsWith('focus-chapter:'))return focusMainChapter(action.slice(14));if(action.startsWith('character:'))return showRoster(+action.split(':')[1]);if(action.startsWith('story:'))return startStory(action.slice(6));if(action.startsWith('replay:'))return startStory(action.slice(7),'archive');if(action.startsWith('setting:')){const id=action.slice(8);settings[id]=!settings[id];if(id==='sound'){if(settings.sound)unlockCombatAudio();else combatAudio?.stop();}try{localStorage.setItem('rl-settings',JSON.stringify(settings));}catch{}return showSettings();}if(action.startsWith('slot:')){const [,actor,n]=action.split(':');selectedActor=actor;selectedSlot=+n;const choice=selectionForSlot(actor,+n);selectedSkill=choice.skillId;selectedCardId=choice.cardId;return showBattle();}if(action.startsWith('skill-card:')){const card=cardFor(unit(selectedActor),action.slice(11));if(!card||cardReserved(selectedActor,card.id,selectedSlot))return;selectedSkill=card.skillId;selectedCardId=card.id;return showBattle();}if(action.startsWith('skill:')){const chosen=skill(selectedActor,action.slice(6));if(!chosen||!defenseSkill(chosen))return;selectedSkill=chosen.id;selectedCardId='';return showBattle();}if(action.startsWith('target:'))return await linkTarget(action.slice(7));
switch(action){case'begin':return data.progress.prologueCompleted?go('hub'):data.campaign?enterCampaign(campaignStages()[0].id):startStory('prologue');case'storynext':return nextStory();case'storyauto':storyAuto=!storyAuto;button.textContent=storyAuto?'자동 켜짐':'자동';scheduleStoryAuto();return;case'storyskip':return finishStory();case'storylog':clearTimeout(autoTimer);return modal('지나온 대화',`<div class="log">${esc(storyLog.join('\n\n'))}</div>`);case'close':return closeModal();case'campaign-story':return routeCampaign();case'deploy':if(!await command('restart'))return;selectFirstFighter();autoBattle=false;lastReward=null;return showBattle();case'recommend':await command('recommend');showBattle();toast('광역 맞불 → 부위 파괴 → 부상 회복 순서로 제안했습니다.');return;case'clear':case'reuse':await command(action);return showBattle();case'remove':await command('remove',{actorId:selectedActor,slot:selectedSlot});return showBattle();case'lantern':await command('lantern',{actorId:selectedActor});return showBattle();case'automode':autoBattle=!autoBattle;if(!autoBattle)autoBattleWaiting=false;if(autoBattle)await command('recommend');return showBattle();case'resolve':return resolveRound();case'battlelog':return modal('라운드 기록',`<div class="log">${esc(data.battle.events.length?data.battle.events.map(e=>e.message).join('\n'):data.battle.links.map(l=>unit(l.actorId).name+' → '+targetName(l.targetId)+' : '+l.reason).join('\n'))}</div>`);case'retreat':return modal('등대로 돌아갈까요?','<p>진행 중인 전투의 보상은 받을 수 없습니다.</p>',btn('전투 계속','close','primary')+btn('등대로','go:hub'));case'repair':if(await command('repair')){showHub();toast('등대의 불빛이 조금 더 따뜻해졌습니다.');}return;}}
catch(err){toast(err.message);}});
function cleanupDrag(){game.querySelector('#drag-thread')?.setAttribute('d','');battleFx.drag(null,[],null,null);dragInfo=null;}
function validDropTargets(sk){
 if(!sk)return [];
 if(['Heal','Guard','Evade','Counter'].includes(sk.kind))return data.battle.allies.filter(u=>u.hp>0).map(u=>u.id);
 if(sk.kind!=='Attack')return [];
 const enemies=data.battle.enemies.filter(u=>u.hp>0);
 return [...data.battle.intents.filter(i=>!i.cancelled&&!i.resolved&&enemies.some(u=>u.id===i.actorId)).map(i=>i.id),...enemies.flatMap(u=>[u.id,...u.parts.filter(p=>p.hp>0).map(p=>p.id)])];
}
game.addEventListener('pointerdown',e=>{
 const slot=e.target.closest('.slot');if(!slot||slot.disabled||busy||campaignBusy||page!=='battle'||dragInfo)return;
 const actor=slot.dataset.actor,slotIndex=+slot.dataset.slot,sameSlot=selectedActor===actor&&selectedSlot===slotIndex;
 const choice=selectionForSlot(actor,slotIndex,sameSlot);
 dragInfo={x:e.clientX,y:e.clientY,pointerId:e.pointerId,actor,slot:slotIndex,skillId:choice.skillId,cardId:choice.cardId,moved:false};
});
addEventListener('pointermove',e=>{
 if(!dragInfo||(dragInfo.pointerId!==undefined&&e.pointerId!==dragInfo.pointerId))return;
 if(Math.hypot(e.clientX-dragInfo.x,e.clientY-dragInfo.y)>8)dragInfo.moved=true;if(!dragInfo.moved)return;
 const r=game.getBoundingClientRect(),p={x:(e.clientX-r.left)*1600/r.width,y:(e.clientY-r.top)*900/r.height},a=slotPoint(dragInfo.actor,dragInfo.slot);
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
 const drag=dragInfo;cleanupDrag();if(!drag.moved||busy||page!=='battle')return;
 suppressClickUntil=performance.now()+250;selectedActor=drag.actor;selectedSlot=drag.slot;selectedSkill=drag.skillId;selectedCardId=drag.cardId;
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
addEventListener('blur',cleanupDrag);
addEventListener('keydown',e=>{if(e.code==='Space'&&page==='story'){e.preventDefault();nextStory();}if(e.code==='Escape'&&!busy&&!campaignBusy){if(game.querySelector('.modal'))closeModal();else if(page==='battle')modal('등대로 돌아갈까요?','<p>진행 중인 전투는 종료됩니다.</p>',btn('전투 계속','close','primary')+btn('등대로','go:hub'));else if(page==='settings')go(settingsReturn);else if(page!=='title')go('hub');}});
async function loadCombatLayouts(){
 const layouts={};
 await Promise.all(['Lian','Sera','Minato','Anger'].map(async name=>{
  try{const response=await fetch(`/combat/${name}ActionsLayout.json`);if(!response.ok)return;const layout=await response.json();
   if(layout.bodyHeight>0&&layout.frames?.length===6&&layout.frames.every(f=>['x','y','width','height','footX','footY'].every(k=>Number.isFinite(f[k]))&&f.width>0&&f.height>0))layouts[name.toLowerCase()]=layout;
  }catch{}
 }));battleFx.options.layouts=layouts;
}
async function boot(){game.innerHTML='<div class="loading">마지막 불씨를 불러오는 중…</div>';try{const r=await fetch('/api/state');if(!r.ok)throw Error('로컬 전투 서버에 연결하지 못했습니다.');data=normalizeState(await r.json());await loadCombatLayouts();showTitle();if(data.warning)toast(data.warning);}catch(e){game.innerHTML=`<div class="loading">${esc(e.message)}<br>Start-Preview.ps1로 실행해 주세요.</div>`;}}
boot();
