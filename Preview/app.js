'use strict';
const game=document.querySelector('#game');
let data,page='title',selectedActor='lian',selectedSlot=0,selectedSkill='lian_glimmer',autoBattle=false;
let story=[],storyIndex=0,storyReturn='hub',storyKey='',storyLog=[],typingTimer,autoTimer,storyAuto=false,typed=false;
function loadSettings(){const defaults={assisted:true,motion:true,sound:true};try{const saved=JSON.parse(localStorage.getItem('rl-settings')||'null');for(const key of Object.keys(defaults))if(typeof saved?.[key]==='boolean')defaults[key]=saved[key];}catch{}return defaults;}
let settings=loadSettings(),settingsReturn='hub';
let busy=false,viewToken=0,lastReward=null,dragInfo=null,toastTimer,context,battleAutoTimer,autoBattleWaiting=false,suppressClickUntil=0;
const chars=[
 ['Lian','리안','광휘 · 수호자','은백색 머리의 파수꾼.\n소등의 날 이후, 13년간 홀로 등대 요새를 지켰다.\n파편의 대가로 어린 시절의 기억 대부분을 잃었다.','…살아 있으면, 그걸로 됐어.'],
 ['Sera','세라','화염 · 돌격수','사라진 고아원 아이들을 찾아 요새에 합류했다.\n밝고 큰 웃음 뒤에 남겨진 마음을 감추고 있다.','괜찮아 괜찮아! 어둠 따위, 내가 태워버릴 테니까!'],
 ['Minato','미나토','물결 · 치유사','나른하고 다정한 의사 지망생.\n치료하지 못한 환자들의 이름을 모두 기억한다.','잃어버린 이름을, 잊지 않으려고.'],
 ['Heidel','하이델','서리 · 사수','고글과 개조 석궁을 쓰는 실용주의 기술자.\n등대 요새의 공방을 맡는다.','요새의 공방을 맡은 실용주의 기술자.'],
 ['Noa','노아','그림자 · 조율사','철종 교단에서 도망친 수수께끼의 잔영.\n그림자와 대화할 수 있다.','그림자와 대화하는 수수께끼의 잔영.'],
 ['Kai','카이','등불지기','얼어붙은 폐허에서 기억을 잃고 깨어났다.\n잔영들의 빛을 증폭하고 조율한다.','손에 남은 것은, 꺼져가는 등불 하나.'],
 ['Orphe','오르페','철종 교단 · 종지기','철종 교단의 수장.\n빛이 죽은 세계에 ‘안식’을 제안한다.','온화한 미소로 안식을 설파하는 자.']
];
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const art=id=>chars.find(c=>c[0].toLowerCase()===String(id).toLowerCase())?.[0]||id;
const btn=(text,action,cls='',extra='')=>`<button class="${cls}" data-action="${action}" ${extra}>${text}</button>`;
const footer=hint=>`<footer><span>REQUIEM LANTERN &nbsp; / &nbsp; PLAY PREVIEW 0.1</span><span>${hint}</span></footer>`;
const header=(over,title,back='hub')=>`<header>${btn('〈','go:'+back,'back','aria-label="이전 화면"')}<div class="titles"><div class="eyebrow">${over}</div><div class="heading">${title}</div></div><div class="wallet"><span>등불 조각 &nbsp; <b class="gold">${data.progress.lanternFragments}</b></span><span>코인 &nbsp; ${data.progress.coins.toLocaleString()}</span></div></header>`;
const hero=(id,cls)=>`<img class="hero ${settings.motion?'breathe ':''}${cls}" src="/art/${art(id)}.png" alt="${esc(chars.find(c=>c[0]===art(id))?.[1]||id)}">`;
const color=e=>({Radiance:'#f2a541',Flame:'#e77a5d',Tide:'#75c8c4',Frost:'#a7d4e9',Shadow:'#b09bd8'}[e]||'#f2a541');
const battleFx=new LanternCombatFX(game,{motion:()=>settings.motion,sound:tone,impact:combatSound,color});
const element=e=>({Radiance:'광휘',Flame:'화염',Tide:'물결',Frost:'서리',Shadow:'그림자'}[e]);
function resize(){game.style.transform=`scale(${Math.min(innerWidth/1600,innerHeight/900)})`;}
addEventListener('resize',resize);resize();
function tone(freq=680,duration=.1){if(!settings.sound)return;try{context??=new AudioContext();const o=context.createOscillator(),g=context.createGain();o.frequency.value=freq;o.type='sine';g.gain.setValueAtTime(.07,context.currentTime);g.gain.exponentialRampToValueAtTime(.001,context.currentTime+duration);o.connect(g);g.connect(context.destination);o.start();o.stop(context.currentTime+duration);}catch{}}
function combatSound(kind,amount=0){
 if(!settings.sound)return;
 try{
  context??=new AudioContext();if(context.state==='suspended')context.resume();
  const now=context.currentTime,swish=kind==='swish',metal=kind==='clash',duration=swish?.12:metal?.38:.22;
  const output=context.createGain();output.gain.value=swish?.2:.28;output.connect(context.destination);
  // A short filtered rush under the strike, and non-harmonic ringing at contact.
  const buffer=context.createBuffer(1,Math.ceil(context.sampleRate*duration),context.sampleRate),samples=buffer.getChannelData(0);
  for(let i=0;i<samples.length;i++){const t=i/context.sampleRate;samples[i]=(Math.random()*2-1)*Math.exp(-t/(swish?.04:.022));}
  const noise=context.createBufferSource(),filter=context.createBiquadFilter();noise.buffer=buffer;
  filter.type=swish?'bandpass':'highpass';filter.frequency.setValueAtTime(swish?800:1900,now);filter.frequency.exponentialRampToValueAtTime(swish?3300:700,now+duration);filter.Q.value=.8;
  noise.connect(filter);filter.connect(output);noise.start(now);noise.stop(now+duration);
  if(!swish){
   const frequencies=metal?[670,1137,1781,2679,3881]:[105,187,743];
   frequencies.forEach((frequency,i)=>{
    const oscillator=context.createOscillator(),envelope=context.createGain();oscillator.type='sine';
    oscillator.frequency.setValueAtTime(frequency*(1+Math.min(50,amount)*.001),now);
    envelope.gain.setValueAtTime((metal?.23:.3)/(1+i*.65),now);envelope.gain.exponentialRampToValueAtTime(.0001,now+duration/(1+i*.15));
    oscillator.connect(envelope);envelope.connect(output);oscillator.start(now);oscillator.stop(now+duration);oscillator.onended=()=>{oscillator.disconnect();envelope.disconnect();};
   });
  }
  noise.onended=()=>{noise.disconnect();filter.disconnect();output.disconnect();};
 }catch{}
}
function toast(msg){clearTimeout(toastTimer);const t=document.querySelector('#toast');t.textContent=msg;t.style.display='block';toastTimer=setTimeout(()=>t.style.display='none',4500);}
function normalizeState(value){return {...value,previews:(value.previews||[]).map(p=>({...p,...p.preview}))};}
async function command(action,extra={}){const r=await fetch('/api/action',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({action,...extra})});const d=await r.json();if(d.battle)data=normalizeState(d);if(d.error){toast(d.error);return false;}if(!r.ok)throw Error(d.error||'전투 처리에 실패했습니다.');if(d.reward)lastReward=d.reward;return true;}
function pauseStoryAuto(){clearTimeout(autoTimer);autoTimer=null;}
function scheduleStoryAuto(){pauseStoryAuto();if(page==='story'&&storyAuto&&typed&&!game.querySelector('.modal'))autoTimer=setTimeout(()=>{autoTimer=null;nextStory();},2600);}
function pauseBattleAuto(){clearTimeout(battleAutoTimer);battleAutoTimer=null;}
function scheduleBattleAuto(){pauseBattleAuto();if(page!=='battle'||busy||!autoBattle||!autoBattleWaiting||game.querySelector('.modal')||data.battle.outcome!=='Ongoing')return;const token=viewToken;battleAutoTimer=setTimeout(()=>{battleAutoTimer=null;if(token===viewToken&&page==='battle'&&!busy&&autoBattle&&autoBattleWaiting&&!game.querySelector('.modal'))resolveRound();},1100);}
function setBackdrop(asset){game.style.backgroundImage="linear-gradient(90deg,#0a101de0,#0b142443 70%,#090f1b65),url('/art/"+asset+".png')";}
function setScreen(name,html){battleFx.clear();setBackdrop(['hub','archive','settings'].includes(name)?'Keep':'Background');cleanupDrag();clearInterval(typingTimer);pauseStoryAuto();pauseBattleAuto();viewToken++;page=name;if(name!=='battle')autoBattleWaiting=false;game.classList.toggle('reduce-motion',!settings.motion);game.innerHTML=`<div class="screen ${name}">${html}</div>`;if(name==='battle')scheduleBattleAuto();}
function go(name){if(busy&&page==='resolve')return;if(name==='settings'&&page!=='settings')settingsReturn=page;const map={title:showTitle,hub:showHub,stages:showStages,roster:()=>showRoster(0),settings:showSettings,archive:showArchive,characterStories:showCharacterStories,battle:showBattle};map[name]?.();}
function showTitle(){setScreen('title',`<div class="title-copy"><div class="eyebrow">T H E &nbsp; L A S T &nbsp; L I G H T</div><h1>REQUIEM<br>LANTERN</h1><div class="rule"></div><h2>레퀴엠 랜턴</h2><p>태양이 꺼진 지 13년.<br>우리가 잃어버린 것들은, 아직 빛나고 있다.</p><div class="title-actions">${btn(data.progress.prologueCompleted?'등대 요새로 돌아가기 &nbsp; →':'마지막 불씨를 따라서 &nbsp; →','begin','primary')}${btn('설정','go:settings')}</div></div><div class="title-inscription">기억이 사라져도<br>등불은 남는다.</div>${footer('초기 플레이 프리뷰 · Unity와 같은 C# 전투 로직')}`);}
function hubCard(n,title,sub,action,primary=false){return `<button class="hub-card ${primary?'primary':''}" data-action="${action}"><span class="num">${n}</span><span><strong>${title}</strong><small>${sub}</small></span><span class="arrow">→</span></button>`;}
function showHub(){setScreen('hub',`${header('BEACON KEEP / 영야의 해안','등대 요새','title')}${hero('Lian','hub-hero')}<div class="hub-quote"><div class="eyebrow">등대 복구 &nbsp; ${data.progress.keepLevel}단계</div><p>“…살아 있으면, 그걸로 됐어.”</p><div class="name">리안 &nbsp; / &nbsp; 등대의 파수꾼</div></div><div class="hub-side"><div class="eyebrow">MAIN STORY</div><h1>꺼진 등대</h1><p>이 작은 불을, 함께 지키기로 했다.<br>요새 바깥에서 다시 그림자가 움직인다.</p>${hubCard('01','작전실','예고선을 읽고, 빛실을 잇다','go:stages',true)}${hubCard('02','잔영 기록','별의 파편을 품은 동료들','go:roster')}${hubCard('03','첫 식사','잠시, 어둠을 잊는 시간','story:firstMeal')}<div class="hub-nav">${btn('기록실','go:archive')}${btn('등대 복구','repair')}${btn('설정','go:settings')}</div></div>${footer('등불지기 카이 · 작은 온기에서 시작되는 이야기')}`);}
function startStory(key,ret='hub'){const lines=key.startsWith('character:')?(data.characterStories||[]).find(s=>s.id===key.slice(10))?.lines:data.story[key];if(!lines?.length){toast('이야기를 불러오지 못했습니다.');return;}story=lines;storyKey=key;storyIndex=0;storyReturn=ret;storyLog=[];storyAuto=false;drawStory();}
function drawStory(){if(storyIndex>=story.length){finishStory();return;}const l=story[storyIndex];setScreen('story',RequiemStoryView.render({line:l,key:storyKey,index:storyIndex,total:story.length,auto:storyAuto,location:l.location,caption:l.caption,portraitHtml:l.portrait?hero(l.portrait,'story-hero'):'',footerHtml:footer('Space / 대화창 클릭으로 계속')}));if(l.background==='keep')setBackdrop('Keep');const words=game.querySelector('.words');let count=0;typed=false;storyLog[storyIndex]=`${l.speaker||'기억'}  ${l.text}`;typingTimer=setInterval(()=>{words.textContent=l.text.slice(0,++count);if(count>=l.text.length){clearInterval(typingTimer);typed=true;scheduleStoryAuto();}},24);}
function nextStory(){if(page!=='story'||game.querySelector('.modal'))return;if(!typed){clearInterval(typingTimer);game.querySelector('.words').textContent=story[storyIndex].text;typed=true;scheduleStoryAuto();return;}storyIndex++;drawStory();}
async function finishStory(){clearInterval(typingTimer);clearTimeout(autoTimer);if(storyKey==='prologue')await command('prologue');go(storyReturn);}
function showStages(){setScreen('stages',`${header('CHAPTER 01 / 꺼진 등대','작전실')}<div class="chapter"><div class="eyebrow">THE FIRST EMBER</div><h1>첫 번째 불빛</h1><p>등대에 도착한 리안, 세라, 미나토.<br>몰려드는 ‘분노’로부터 남은 온기를 지킨다.</p></div><div class="map-path">${['요새 앞의 그림자','얼어붙은 길','남겨진 편지','시계탑의 잔향'].map((n,i)=>`<div class="map-node ${i?'locked':''}"><div class="number">0${i+1}</div><p>${n}</p>${i?'<small class="muted">준비 중</small>':'<small class="gold">선택된 작전</small>'}</div>`).join('')}</div><div class="mission"><div><h2>1-1 &nbsp; 요새 앞의 그림자</h2><p>3인 출격 · 연광 전투 튜토리얼 · 분노 개체 (팔 / 핵)<br>첫 클리어: 코인 120 · 등불 조각 10 · 기억의 파편 1</p></div>${btn('출격 준비 &nbsp; →','deploy','primary')}</div>${footer('초기 구현 구간 1-1 · 나머지 지역은 이후 업데이트 예정')}`);}
function showRoster(i){const c=chars[i];setScreen('roster',`${header('AFTERGLOW ARCHIVE / 인물 기록','잔영과 등불지기')}${hero(c[0],'roster-hero')}<div class="roster-info"><div class="eyebrow">CHARACTER &nbsp; 0${i+1} / 07</div><h1>${c[1]}</h1><div class="role">${c[2]}</div><div class="bio">${c[3]}</div><blockquote>“${c[4]}”</blockquote><div class="roster-tabs">${chars.map((c,n)=>btn(c[1],'character:'+n,n===i?'primary':'')).join('')}</div></div>${footer('원화 기준 인물 기록 · 후반부의 핵심 반전은 숨겨져 있습니다')}`);}
function showSettings(){setScreen('settings',`${header('PREFERENCES','환경 설정',settingsReturn)}<div class="settings"><h1>편안한 속도로, 빛을 잇도록.</h1>${[['assisted','점화 타이밍 보조','자동으로 ‘양호’ 판정. 끄면 클릭 / Space로 점화합니다.'],['motion','움직임과 전투 효과','빛실 흐름, 타격 움직임, 화면 흔들림을 재생합니다. 끄면 간소하게 표시합니다.'],['sound','효과음','빛실 연결과 점화에 유리종 음색을 재생합니다.']].map(([id,title,desc])=>`<div class="setting"><div><strong>${title}</strong><p>${desc}</p></div>${btn(settings[id]?'켜짐':'꺼짐','setting:'+id,settings[id]?'primary':'')}</div>`).join('')}</div>${footer('진행은 로컬 서버에 저장 · 설정은 이 브라우저에 저장')}`);}
function showArchive(){setScreen('archive',`${header('MEMORY ARCHIVE','기록실')}<div class="settings"><div class="eyebrow">THE LANTERNS REMEMBER</div><h1>빛은 기억을 품는다.</h1><p class="muted" style="font-size:22px">지나온 이야기를 다시 펼쳐 봅니다.</p><div class="archive-columns"><div>${hubCard('00','마지막 불씨','폐허에서 깨어난 등불지기와 리안의 만남','replay:prologue')}${hubCard('01','첫 식사','등대 요새에 남겨진 작은 온기','replay:firstMeal')}${hubCard('02','캐릭터 이야기','리안 · 세라 · 노아가 남긴 이름과 약속','go:characterStories')}</div><div><div class="eyebrow">보관 중인 기억</div><div class="archive-count">${String(data.progress.memoryFragments).padStart(2,'0')}</div><p class="muted">전투에서 되찾은 기억의 파편.<br>복구와 이야기 확장을 위한 첫 기록입니다.</p></div></div></div>${footer('설정집의 인물과 사건을 기준으로 구성한 초기 각색')}`);}
function showCharacterStories(){
 setScreen('character-stories',`${header('AFTERGLOW / CHARACTER STORIES','캐릭터 이야기','archive')}<p class="story-spoiler-note">인물의 과거와 후반부 내용이 포함됩니다. 권장 감상 순서를 확인하고 읽어 주세요.</p><div class="character-story-cards">${(data.characterStories||[]).map(s=>`<article class="character-story-card"><div class="eyebrow">${esc(s.name)} · ★5</div><img src="/art/${art(s.id)}.png" alt="${esc(s.name)}"><h2>${esc(s.title)}</h2><p>${esc(s.summary)}</p><small>${esc(s.recommendedAfter)}</small>${btn('이야기 읽기 →','read-character:'+s.id,'primary',`aria-label="${esc(s.name)}의 ${esc(s.title)} 읽기"`)}</article>`).join('')}</div>${footer('세 잔영의 합류, 상실, 그리고 스스로 고른 결의')}`);
}
const unit=id=>data.battle.allies.concat(data.battle.enemies).find(u=>u.id===id);
const skill=(actor,id)=>unit(actor)?.skills.find(s=>s.id===id);
const intent=id=>data.battle.intents.find(i=>i.id===id);
function targetName(id){return intent(id)?.name||unit(id)?.name||data.battle.enemies.flatMap(u=>u.parts).find(p=>p.id===id)?.name||id;}
function actorPoint(id){const i=data.battle.allies.findIndex(a=>a.id===id);return {x:172+i*265,y:446};}
function targetPoint(id){let i=data.battle.intents.findIndex(a=>a.id===id);if(i>=0)return {x:945,y:270+i*122};if(data.battle.allies.some(a=>a.id===id))return actorPoint(id);i=data.battle.enemies[0].parts.findIndex(a=>a.id===id);if(i>=0)return {x:1190+i*227,y:550};return {x:1360,y:447};}
function slotPoint(actor,slot){return{x:113+data.battle.allies.findIndex(a=>a.id===actor)*340+slot*146,y:756};}
function path(a,b,cls,c='',intentId=''){return `<path class="${cls}" ${intentId?`data-intent-line="${esc(intentId)}"`:''} d="M${a.x},${a.y} Q${(a.x+b.x)/2},${Math.min(a.y,b.y)-65} ${b.x},${b.y}" ${c?`style="stroke:${c}"`:''}/>`;}
function showBattle(){const s=data.battle,e=s.enemies[0];let lines='';for(const i of s.intents){const p=targetPoint(i.id);for(const a of i.kind==='Area'?s.allies:[unit(i.targetId)].filter(Boolean))lines+=path(p,actorPoint(a.id),'thread-enemy','',i.id);}for(const l of s.links){let c='#f2a541';if(l.kind==='Clash'){const v=data.previews?.find(p=>p.actorId===l.actorId&&p.slot===l.slot);c=v?.grade==='Advantage'?'#75c8c4':v?.grade==='Disadvantage'?'#e77a5d':'#f2a541';}lines+=path(slotPoint(l.actorId,l.slot),targetPoint(l.targetId),'thread-player flow',c);}
const selected=unit(selectedActor),sk=skill(selectedActor,selectedSkill)||selected.skills[0];
setScreen('battle',`<header>${btn('〈','retreat','back','aria-label="퇴각"')}<div class="titles"><div class="heading">1-1 &nbsp; 요새 앞의 그림자</div><div class="phase">ROUND ${String(s.round).padStart(2,'0')} &nbsp; / &nbsp; ② 연결</div></div><div class="resources"><span class="gold">불씨 ${s.ember} / 6 <small class="muted">예약 ${data.plannedCost}</small></span><span>등불 ${s.lantern} / 100</span><span class="muted">어둠도 ${s.gloom}</span></div></header><div class="battle-controls">${btn('AI 제안','recommend')}${btn('이전 연결','reuse')}${btn('초기화','clear')}${btn(autoBattle?'오토 켜짐':'수동 연결','automode',autoBattle?'primary':'')}${btn(s.lanternQueued?'해방 예약':'등불 해방','lantern','',s.lantern<100||s.lanternQueued?'disabled':'')}${btn('기록','battlelog')}</div><div class="battle-help">슬롯을 선택한 뒤 예고선·부위·아군을 클릭하세요.<br>드래그로도 연결할 수 있습니다. 보조를 끄면 직접 점화합니다.</div><svg class="threads" viewBox="0 0 1600 900" aria-hidden="true">${lines}<path id="drag-thread" class="thread-player"/></svg>${s.allies.map((a,i)=>`<img src="/art/${art(a.id)}.png" data-unit="${a.id}" class="field-hero ${settings.motion?'breathe':''} ${a.hp<=0?'dead':''}" style="left:${40+i*265}px;animation-delay:${-i}s" alt="${a.name}">${btn('','target:'+a.id,'ally-target',`style="left:${50+i*265}px" aria-label="${a.name}에게 지원 연결"`)}<div class="field-name" style="left:${50+i*265}px">${a.name}<div class="bar"><span data-hp-bar="${a.id}" style="background:${color(a.element)};width:${a.hp/a.maxHp*100}%"></span></div></div>`).join('')}<div class="enemy-stats"><div class="name">UMBRA / 분노</div><div class="bar"><span data-hp-bar="${e.id}" style="width:${e.hp/e.maxHp*100}%"></span></div><small data-hp-text="${e.id}">${e.hp} / ${e.maxHp} &nbsp; · &nbsp; 균열 ${e.rift} / ${e.maxRift}</small></div><img src="/art/Anger.png" class="enemy" data-unit="${e.id}" alt="분노 개체">${s.intents.map((i,n)=>`<button class="intent" data-action="target:${i.id}" style="top:${228+n*122}px"><strong>${i.name}${i.kind==='Area'?' · 광역':''}</strong><small>${i.hitPowers.length}타 × ${i.hitPowers.join('/')} &nbsp; 속도 ${i.speed}</small></button>`).join('')}<div class="parts">${e.parts.map(p=>btn(`${p.name} &nbsp; ${p.hp<=0?'파괴됨':`${p.hp}/${p.maxHp}`}`,'target:'+p.id,'',p.hp<=0?'disabled':'')).join('')}</div>${btn('본체 공격','target:'+e.id,'body-target')}<div class="battle-prediction">${(data.previews||[]).filter(p=>s.links.some(l=>l.actorId===p.actorId&&l.slot===p.slot&&l.kind==='Clash')).map(p=>esc(p.summary)).join('<br>')}</div><div class="action-dock">${s.allies.map(a=>`<div class="fighter"><div class="fighter-title"><span style="color:${color(a.element)}">${a.name} <small>${element(a.element)}</small></span><small data-hp-text="${a.id}">${a.hp}/${a.maxHp}</small></div><div class="bar"><span data-hp-bar="${a.id}" style="background:${color(a.element)};width:${a.hp/a.maxHp*100}%"></span></div><div class="slots">${[0,1].map(n=>{const l=s.links.find(l=>l.actorId===a.id&&(l.slot===n||skill(a.id,l.skillId).slots===2));return `<div class="slot-wrap">${btn(esc(l?skill(a.id,l.skillId).name:`슬롯 ${n+1} +`),`slot:${a.id}:${n}`,`slot ${selectedActor===a.id&&selectedSlot===n?'active':''}`,`data-actor="${a.id}" data-slot="${n}" ${a.hp<=0?'disabled':''}`)}<div class="slot-target">${l?(l.kind==='Clash'?'맞불 · ':'')+esc(targetName(l.targetId)):'미연결'}</div></div>`;}).join('')}</div></div>`).join('')}<div class="skill-panel"><div class="skill-title">${selected.name} · 슬롯 ${selectedSlot+1}</div><div class="skills">${selected.skills.map(k=>{const replace=s.links.filter(l=>l.actorId===selectedActor&&(l.slot===selectedSlot||k.slots===2||skill(l.actorId,l.skillId).slots===2)).reduce((sum,l)=>sum+skill(l.actorId,l.skillId).cost,0);return btn(`${k.name} · ${k.cost}`,`skill:${k.id}`,k.id===sk.id?'primary':'',(k.cost>data.availableEmber+replace||k.slots===2&&selectedSlot!==0)?'disabled':'');}).join('')}</div><div class="skill-actions">${btn('연결 해제','remove')}${btn('빛실 실행 →','resolve','primary',s.links.length?'':'disabled')}</div></div></div>${footer('보라 점선: 적 예고선 / 실선: 나의 연결 / 초록: 맞불 우세 / 주황: 접전 / 붉음: 열세')}`);battleFx.attach(s);}
async function linkTarget(id){const sk=skill(selectedActor,selectedSkill);const kind=sk.kind==='Heal'?'Support':sk.kind==='Guard'?'Guard':intent(id)?'Clash':'Strike';if(await command('link',{actorId:selectedActor,slot:selectedSlot,skillId:selectedSkill,kind,targetId:id})){tone(890,.2);showBattle();battleFx.connected(selectedActor,selectedSlot,id,kind);}}
function modal(title,body,buttons=btn('닫기','close','primary')){pauseStoryAuto();pauseBattleAuto();game.insertAdjacentHTML('beforeend',`<div class="modal"><h2>${title}</h2>${body}<div class="modal-actions">${buttons}</div></div>`);}
function closeModal(){game.querySelector('.modal')?.remove();if(!game.querySelector('.modal')){scheduleStoryAuto();scheduleBattleAuto();}}
function delay(t){return new Promise(r=>setTimeout(r,t));}
async function ignite(link){return new Promise(resolve=>{const sk=skill(link.actorId,link.skillId);modal(`${unit(link.actorId).name} / ${sk.name}`,`<p>${settings.motion?'두 원이 겹칠 때 클릭 또는 Space':'카운트다운의 ‘지금 점화’에 클릭 또는 Space'}</p><div class="ignite-area"><div class="ring"></div><div class="ring moving"></div><div class="ignite-grade"></div></div>`,'');game.querySelector('.modal').classList.add('ignite-modal');const area=game.querySelector('.ignite-area'),moving=area.querySelector('.moving'),out=area.querySelector('.ignite-grade');const start=performance.now();let done=false;const frame=()=>{if(done)return;const time=performance.now()-start;const diameter=330-150*time/1100;if(settings.motion)moving.style.width=moving.style.height=Math.max(35,diameter)+'px';else{moving.hidden=true;out.textContent=Math.abs(time-1100)<130?'지금 점화':time<1100?((1100-time)/1000).toFixed(1):'늦음';}if(time>1850)complete(null);else requestAnimationFrame(frame);};const complete=stamp=>{if(done)return;done=true;removeEventListener('keydown',key);area.removeEventListener('pointerdown',tap);const err=stamp===null?Infinity:Math.abs(stamp-start-1100),factor=(1-data.battle.gloom*.1)*(data.battle.lanternQueued?1.2:1);const grade=err<=80*factor?'Perfect':err<=200*factor?'Good':'Miss';out.textContent={Perfect:'점 화',Good:'양 호',Miss:'빗나감'}[grade];tone(grade==='Perfect'?1046:520,.25);setTimeout(()=>{closeModal();resolve(grade);},460);};const key=e=>{if(e.code==='Space'){e.preventDefault();complete(performance.now());}};const tap=()=>complete(performance.now());addEventListener('keydown',key);area.addEventListener('pointerdown',tap);requestAnimationFrame(frame);});}
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
function showResult(){const win=data.battle.outcome==='Victory';setScreen('result',`${hero('Lian','result-hero')}<div class="result-info"><div class="eyebrow">${win?'THE LIGHT REMAINS':'A MOMENT IN THE DARK'}</div><h1>${win?'지켜낸 불씨':'아직, 끝이 아니야'}</h1><p>${win?'어둠이 물러난 자리.\n우리는 또 하나의 기억을 되찾았다.':'빛실을 다시 잇고, 다른 길을 찾아보자.\nAI 제안으로 맞불과 부위 파괴를 확인할 수 있다.'}</p>${win&&lastReward?`<div class="rewards"><div class="reward"><b>+${lastReward.coins}</b>코인</div><div class="reward"><b>+${lastReward.lanternFragments}</b>등불 조각</div><div class="reward"><b>+${lastReward.memoryFragments}</b>기억의 파편</div></div>`:''}${btn(win?'남겨진 기억 읽기 →':'다시 도전하기 →',win?'story:epilogue':'deploy','primary')}${btn('등대로','go:hub')}</div>${footer('진행 상황 자동 저장 · 기록실에서 이야기를 다시 볼 수 있습니다')}`);}
game.addEventListener('click',async e=>{const button=e.target.closest('[data-action]');if(!button||button.disabled)return;const action=button.dataset.action;if(dragInfo?.moved||performance.now()<suppressClickUntil)return;tone();try{if(action.startsWith('go:'))return go(action.slice(3));if(action.startsWith('read-character:'))return startStory('character:'+action.slice(15),'characterStories');if(action.startsWith('character:'))return showRoster(+action.split(':')[1]);if(action.startsWith('story:'))return startStory(action.slice(6));if(action.startsWith('replay:'))return startStory(action.slice(7),'archive');if(action.startsWith('setting:')){const id=action.slice(8);settings[id]=!settings[id];try{localStorage.setItem('rl-settings',JSON.stringify(settings));}catch{}return showSettings();}if(action.startsWith('slot:')){const [,actor,n]=action.split(':');selectedActor=actor;selectedSlot=+n;selectedSkill=data.battle.links.find(l=>l.actorId===actor&&l.slot===+n)?.skillId||unit(actor).skills[0].id;return showBattle();}if(action.startsWith('skill:')){selectedSkill=action.slice(6);return showBattle();}if(action.startsWith('target:'))return await linkTarget(action.slice(7));
switch(action){case'begin':return data.progress.prologueCompleted?showHub():startStory('prologue');case'storynext':return nextStory();case'storyauto':storyAuto=!storyAuto;button.textContent=storyAuto?'자동 켜짐':'자동';scheduleStoryAuto();return;case'storyskip':return finishStory();case'storylog':clearTimeout(autoTimer);return modal('지나온 대화',`<div class="log">${esc(storyLog.join('\n\n'))}</div>`);case'close':return closeModal();case'deploy':await command('restart');selectedActor='lian';selectedSkill='lian_glimmer';selectedSlot=0;autoBattle=false;lastReward=null;return showBattle();case'recommend':await command('recommend');showBattle();toast('광역 맞불 → 부위 파괴 → 부상 회복 순서로 제안했습니다.');return;case'clear':case'reuse':await command(action);return showBattle();case'remove':await command('remove',{actorId:selectedActor,slot:selectedSlot});return showBattle();case'lantern':await command('lantern',{actorId:selectedActor});return showBattle();case'automode':autoBattle=!autoBattle;if(!autoBattle)autoBattleWaiting=false;if(autoBattle)await command('recommend');return showBattle();case'resolve':return resolveRound();case'battlelog':return modal('라운드 기록',`<div class="log">${esc(data.battle.events.length?data.battle.events.map(e=>e.message).join('\n'):data.battle.links.map(l=>unit(l.actorId).name+' → '+targetName(l.targetId)+' : '+l.reason).join('\n'))}</div>`);case'retreat':return modal('등대로 돌아갈까요?','<p>진행 중인 전투의 보상은 받을 수 없습니다.</p>',btn('전투 계속','close','primary')+btn('등대로','go:hub'));case'repair':if(await command('repair')){showHub();toast('등대의 불빛이 조금 더 따뜻해졌습니다.');}return;}}
catch(err){toast(err.message);}});
function cleanupDrag(){game.querySelector('#drag-thread')?.setAttribute('d','');battleFx.drag(null,[],null,null);dragInfo=null;}
function validDropTargets(sk){
 if(!sk)return [];
 if(sk.kind==='Heal'||sk.kind==='Guard')return data.battle.allies.filter(u=>u.hp>0).map(u=>u.id);
 if(sk.kind!=='Attack')return [];
 const enemies=data.battle.enemies.filter(u=>u.hp>0);
 return [...data.battle.intents.filter(i=>!i.cancelled&&!i.resolved&&enemies.some(u=>u.id===i.actorId)).map(i=>i.id),...enemies.flatMap(u=>[u.id,...u.parts.filter(p=>p.hp>0).map(p=>p.id)])];
}
game.addEventListener('pointerdown',e=>{
 const slot=e.target.closest('.slot');if(!slot||slot.disabled||busy||page!=='battle'||dragInfo)return;
 const actor=slot.dataset.actor,slotIndex=+slot.dataset.slot,sameSlot=selectedActor===actor&&selectedSlot===slotIndex;
 const pickedSkill=sameSlot?selectedSkill:data.battle.links.find(l=>l.actorId===actor&&l.slot===slotIndex)?.skillId||unit(actor).skills[0].id;
 dragInfo={x:e.clientX,y:e.clientY,pointerId:e.pointerId,actor,slot:slotIndex,skillId:pickedSkill,moved:false};
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
 suppressClickUntil=performance.now()+250;selectedActor=drag.actor;selectedSlot=drag.slot;selectedSkill=drag.skillId;
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
addEventListener('keydown',e=>{if(e.code==='Space'&&page==='story'){e.preventDefault();nextStory();}if(e.code==='Escape'&&!busy){if(game.querySelector('.modal'))closeModal();else if(page==='battle')modal('등대로 돌아갈까요?','<p>진행 중인 전투는 종료됩니다.</p>',btn('전투 계속','close','primary')+btn('등대로','go:hub'));else if(page==='settings')go(settingsReturn);else if(page!=='title')showHub();}});
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
