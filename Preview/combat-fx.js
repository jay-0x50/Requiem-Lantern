'use strict';

// Presentation owns a copy of the pre-round state. Only server events change it;
// effects never calculate damage or feed values back into the battle simulation.
class LanternCombatFX {
 constructor(root, options={}) {
  this.root=root; this.options=options; this.epoch=0; this.pending=new Map();
  this.units=new Map(); this.parts=new Map(); this.cancelled=new Set();
  this.atlases=new Map();this.presentationElapsed=0;this.tensionCount=0;this.rollActors=new Set();
  // Decode ahead of combat. Failed or unfinished loads retain the original portrait.
  if(typeof Image!=='undefined')for(const name of ['Lian','Sera','Minato','Anger']){
   const asset={ready:false,image:new Image()};this.atlases.set(name.toLowerCase(),asset);
   asset.image.onload=()=>{asset.ready=true;};asset.image.onerror=()=>{asset.ready=false;};asset.image.src=`/combat/${name}Actions.png`;
  }
 }
 get motion(){return this.options.motion?.()!==false;}
 attach(battle){
  this.clear(); this.battle=battle;
  this.units=new Map([...battle.allies,...battle.enemies].map(u=>[u.id,{...u}]));
  this.parts=new Map(battle.enemies.flatMap(u=>u.parts.map(p=>[p.id,{...p}])));
  this.cancelled=new Set();this.rollActors=new Set();this.presentationElapsed=0;this.tensionCount=0;this.lastClash=null;this.active=null;
  this.root.querySelector('.battle')?.insertAdjacentHTML('beforeend',
   '<div class="combat-fx" aria-hidden="true"></div><div class="drag-cursor" aria-hidden="true"><i></i><span></span></div><div class="combat-caption" role="status" aria-live="polite"><div class="caption-overline">RESONANCE / 連光</div><strong></strong><span></span><div class="playback-progress"><i></i></div></div>');
 }
 clear(){
  this.epoch++;
  this.options.stopSound?.();
  this.releaseMelee();
  for(const [id,done] of this.pending){clearTimeout(id);done(false);}this.pending.clear();
  this.root.querySelectorAll('.combat-fx,.combat-caption,.drag-cursor').forEach(n=>n.remove());
  this.root.querySelectorAll('.fx-active,.drop-ready,.drop-focus,.fx-lunge,.fx-hit').forEach(n=>n.classList.remove('fx-active','drop-ready','drop-focus','fx-lunge','fx-hit'));
  this.root.querySelector('.battle')?.classList.remove('resolving','fx-shake','tension-roll');
 }
 wait(ms){return new Promise(done=>{const id=setTimeout(()=>{this.pending.delete(id);done(true);},ms);this.pending.set(id,done);});}
 step(ms){
  const remaining=Math.max(0,28000-this.presentationElapsed),duration=Math.min(Math.max(0,ms),remaining);
  this.presentationElapsed+=duration;
  return duration>0?this.wait(duration):Promise.resolve(true);
 }
 attackStyle(e,clash){
  if(clash||(!e.skillId&&!e.skillNumber))return'clash';
  return ({lian:'shield-blow',sera:'double-slash',minato:'staff-wave'}[e.actorId])||'strike';
 }
 audioMetadata(e){return{actorId:e.actorId,skillNumber:e.skillNumber||0,skillId:e.skillId||'',clashIndex:e.clashIndex||0,isEnemy:!!this.units.get(e.actorId)?.isEnemy};}
 point(id){
  if(!this.battle||!id)return null;
  let i=this.battle.allies.findIndex(u=>u.id===id);if(i>=0)return{x:172+i*265,y:446};
  i=this.battle.intents.findIndex(u=>u.id===id);if(i>=0)return{x:945,y:270+i*122};
  i=this.battle.enemies.flatMap(u=>u.parts).findIndex(u=>u.id===id);if(i>=0)return{x:1190+i*227,y:550};
  return this.battle.enemies.some(u=>u.id===id)?{x:1360,y:410}:null;
 }
 tint(id){return this.options.color?.(this.units.get(id)?.element)||'#f2ba70';}
 target(id){return [...this.root.querySelectorAll('[data-action^="target:"]')].find(n=>n.dataset.action==='target:'+id);}
 portrait(id){return [...this.root.querySelectorAll('[data-unit]')].find(n=>n.dataset.unit===id);}
 node(cls,p,tint,text){
  if(!p)return null;const layer=this.root.querySelector('.combat-fx');if(!layer)return null;
  const n=document.createElement('div');n.className=cls;n.style.left=p.x+'px';n.style.top=p.y+'px';
  if(tint)n.style.setProperty('--fx-color',tint);if(text)n.textContent=text;layer.appendChild(n);return n;
 }
 expire(n,ms=800){if(n)this.wait(ms).then(()=>n.remove());return n;}
 pulse(p,tint,big=false){return this.expire(this.node('fx-ring'+(big?' large':''),p,tint),850);}
 shards(p,tint,big=false){
  this.pulse(p,tint,big);if(!this.motion)return;
  for(let i=0;i<(big?18:10);i++){
   const n=this.node('fx-spark',p,tint);if(!n)continue;
   const angle=Math.PI*2*i/(big?18:10),distance=(big?135:65)+(i%3)*23;
   n.style.setProperty('--dx',Math.cos(angle)*distance+'px');n.style.setProperty('--dy',Math.sin(angle)*distance+'px');n.style.setProperty('--angle',angle+'rad');this.expire(n,700);
  }
 }
 beam(a,b,tint,opposed=false){
  if(!a||!b)return;const n=this.node('fx-beam'+(opposed?' opposed':''),a,tint);if(!n)return;
  n.style.width=Math.hypot(b.x-a.x,b.y-a.y)+'px';n.style.setProperty('--angle',Math.atan2(b.y-a.y,b.x-a.x)+'rad');
  this.expire(n,500);
 }
 moveUnit(id,cls){
  const n=this.portrait(id);if(!n)return;n.classList.remove(cls);void n.offsetWidth;n.classList.add(cls);
  this.wait(350).then(()=>n.classList.remove(cls));
 }
 createFighter(id,side){
  const original=this.portrait(id),start=this.point(id);if(!original||!start)return null;
  const wrapper=this.node('melee-fighter '+side,{x:start.x,y:170},this.tint(id));if(!wrapper)return null;
  const sprite=document.createElement('div');sprite.className='melee-sprite';sprite.dataset.unitPose=id;
  const names={lian:'Lian',sera:'Sera',minato:'Minato',anger:'Anger'};
  const unit=this.units.get(id),name=names[id];
  const identityMatches=!!name&&!unit?.isHuman&&(!unit?.portrait||unit.portrait.toLowerCase()===name.toLowerCase());
  const atlas=identityMatches&&(!this.atlases.has(id)||this.atlases.get(id).ready);
  sprite.style.backgroundImage=atlas?`url('/combat/${name}Actions.png')`:`url('${original.src}')`;
  if(!atlas)sprite.style.backgroundSize='contain';
  wrapper.appendChild(sprite);
  const layout=atlas?this.options.layouts?.[id]:null;
  const atlasImage=this.atlases.get(id)?.image;
  const fighter={id,side,wrapper,sprite,original,atlas,layout,
   atlasWidth:atlasImage?.naturalWidth||layout?.atlasWidth||1536,atlasHeight:atlasImage?.naturalHeight||layout?.atlasHeight||1024,
   visibility:original.style.visibility,start:start.x,x:start.x};
  original.style.visibility='hidden';this.pose(fighter,0,'approach');return fighter;
 }
 pose(fighter,frame,phase){
  if(!fighter)return;fighter.sprite.dataset.frame=String(frame);fighter.wrapper.dataset.phase=phase;
  const crop=fighter.atlas&&fighter.layout?.frames?.[frame];
  if(crop){
   const scale=(fighter.id==='anger'?410:380)/fighter.layout.bodyHeight;
   fighter.sprite.style.inset='auto';fighter.sprite.style.width=crop.width*scale+'px';fighter.sprite.style.height=crop.height*scale+'px';
   fighter.sprite.style.left=240-(crop.footX-crop.x)*scale+'px';fighter.sprite.style.top=440-(crop.footY-crop.y)*scale+'px';
   fighter.sprite.style.backgroundSize=`${fighter.atlasWidth*scale}px ${fighter.atlasHeight*scale}px`;
   fighter.sprite.style.backgroundPosition=`${-crop.x*scale}px ${-crop.y*scale}px`;
  }else fighter.sprite.style.backgroundPosition=fighter.atlas?`${frame%3*50}% ${Math.floor(frame/3)*100}%`:'center bottom';
 }
 setPosePhase(pair,phase){
  const stage=this.root.querySelector('.battle');if(stage)stage.dataset.combatPhase=phase;
  for(const fighter of pair)if(fighter)fighter.wrapper.dataset.phase=phase;
 }
 weaponOffset(fighter,frame){
  const pose=fighter?.layout?.frames?.[frame];
  if(pose&&Number.isFinite(pose.contactX)&&Number.isFinite(pose.contactY)){
   const scale=(fighter.id==='anger'?410:380)/fighter.layout.bodyHeight;
   return{x:(pose.contactX-pose.footX)*scale,y:(pose.contactY-pose.footY)*scale};
  }
  return{x:fighter?.side==='enemy-side'?-155:155,y:-210};
 }
 place(fighter,x,rotation=0,duration=0){
  if(!fighter)return;
  fighter.x=x;fighter.wrapper.style.transition=this.motion?`left ${duration}ms cubic-bezier(.2,.75,.25,1), transform ${duration}ms ease`:'none';
  if(this.motion){fighter.wrapper.style.left=x+'px';fighter.wrapper.style.transform=`translateX(-50%) rotate(${rotation}deg)`;}
 }
 releaseMelee(){
  for(const fighter of this.meleePair||[]){if(!fighter)continue;fighter.original.style.visibility=fighter.visibility;fighter.wrapper.remove();}
  this.meleePair=null;
  const stage=this.root.querySelector('.battle');if(stage){stage.classList.remove('melee-active','hit-stop','contact-kick');stage.dataset.combatPhase='';stage.dataset.attackStyle='';}
 }
 async melee(e,fast,epoch,clash){
  // Atlas cells share one foot pivot. Translation belongs to the wrapper;
  // switching a pose never crops or rescales a character's individual frame.
  const ongoing=this.meleePair?.length===2&&this.meleePair.some(f=>f?.id===e.actorId)&&this.meleePair.some(f=>f?.id===e.targetId);
  if(!ongoing)this.releaseMelee();
  const attacker=ongoing?this.meleePair.find(f=>f?.id===e.actorId):this.createFighter(e.actorId,this.units.get(e.actorId)?.isEnemy?'enemy-side':'ally-side');
  const defender=ongoing?this.meleePair.find(f=>f?.id===e.targetId):this.createFighter(e.targetId,this.units.get(e.targetId)?.isEnemy?'enemy-side':'ally-side');
  const pair=[attacker,defender];this.meleePair=pair;
  const stage=this.root.querySelector('.battle');stage?.classList.add('melee-active');
  const pause=ms=>this.step(this.motion?ms*(fast?.76:1):90);
  const style=this.attackStyle(e,clash),special=this.motion&&e.type==='Damage'&&style!=='clash',heavy=special&&style==='shield-blow';
  if(stage){stage.dataset.attackStyle=style;stage.dataset.skillNumber=String(e.skillNumber||0);}
  const valid=()=>epoch===this.epoch;
  const attackFrame=clash&&e.actorId==='anger'&&e.targetId==='sera'?3:2;
  const counterFrame=clash&&e.targetId==='anger'&&e.actorId==='sera'?3:2;
  const reach=this.weaponOffset(attacker,attackFrame),counterReach=this.weaponOffset(defender,counterFrame);
  const center=f=>f===attacker?1055-reach.x:clash?1055-counterReach.x:1055+(f?.side==='enemy-side'?65:-65);
  const direction=f=>f?.side==='enemy-side'?-1:1;
  const hit={x:1055,y:610+(clash?(reach.y+counterReach.y)/2:reach.y)};
  try{
   if(!ongoing){
    this.setPosePhase(pair,'approach');
    if(!await pause(20)||!valid())return;
    for(const f of pair)this.place(f,center(f),0,fast?130:180);
    if(!await pause(180)||!valid())return;
   }
   this.setPosePhase(pair,'windup');this.pose(attacker,heavy?4:1,'windup');this.pose(defender,clash?1:0,'windup');
   this.place(attacker,center(attacker)-direction(attacker)*(heavy?42:24),-direction(attacker)*(heavy?7:5),100);
   if(clash)this.place(defender,center(defender)-direction(defender)*18,-direction(defender)*4,100);
   if(!await pause(['Clash','Tie'].includes(e.type)?80:heavy?180:style==='double-slash'?80:style==='staff-wave'?115:150)||!valid())return;
   if(special&&style==='double-slash'){
    this.setPosePhase(pair,'feint');this.pose(attacker,2,'feint');this.place(attacker,center(attacker)-direction(attacker)*12,-direction(attacker)*3,45);
    this.options.impact?.('swish',0,this.audioMetadata(e));this.expire(this.node('weapon-cut feint-cut',hit,this.tint(e.actorId)),190);
    if(!await pause(65)||!valid())return;this.pose(attacker,3,'feint');
    if(!await pause(55)||!valid())return;
   }
   this.setPosePhase(pair,'swing');this.pose(attacker,attackFrame,'swing');if(clash)this.pose(defender,counterFrame,'swing');
   this.place(attacker,center(attacker),0,70);
   if(clash)this.place(defender,center(defender),0,70);
   this.options.impact?.('swish',e.amount,this.audioMetadata(e));
   if(!await pause(style==='double-slash'&&special?45:75)||!valid())return;
   this.setPosePhase(pair,'contact');this.pose(attacker,attackFrame,'contact');this.pose(defender,clash?counterFrame:5,'contact');
   if(e.type==='Damage')this.applyEvent(e);
   this.options.impact?.(clash?'clash':'hit',e.amount,this.audioMetadata(e));
   if(this.motion){
    stage?.classList.add('hit-stop','contact-kick');
    this.shards(hit,clash?'#ffdea1':this.tint(e.actorId));
    if(special&&style==='staff-wave')this.expire(this.node('staff-wave',hit,'#9eddd8'),350);
    else this.expire(this.node('weapon-cut '+(heavy?'heavy-cut ':style==='double-slash'?'second-cut ':'')+(direction(attacker)<0?'reverse':''),hit,this.tint(e.actorId)),heavy?330:230);
    if(heavy){this.pulse(hit,'#ffdd95',true);this.expire(this.node('shield-impact',hit,'#ffe0a0'),300);}
    if(clash)this.expire(this.node('weapon-contact',hit,'#ffe4b0'),170);
   }
   if(e.type==='Damage')this.expire(this.node('fx-number', this.motion?{x:center(defender),y:320}:this.point(e.targetId),this.tint(e.targetId),String(e.amount)),800);
   else {this.caption(e.type==='Tie'?'맞불 · 무승부':'맞불 · '+(e.winnerStreak||0)+'연속 우세',e.message);this.expire(this.node('fx-link-label',hit,'#ffdfa5',e.type==='Tie'?'무승부':(e.winnerStreak||0)+'연속'),600);}
   // Both silhouettes hold on the exact contact pose before the recoil.
   if(!await this.step(this.motion?(heavy?100:style==='double-slash'&&special?45:65):90)||!valid())return;
   stage?.classList.remove('hit-stop');
   this.setPosePhase(pair,'recoil');this.pose(attacker,3,'recoil');this.pose(defender,e.type==='Damage'?5:4,'recoil');
   this.place(defender,center(defender)-direction(defender)*(e.type==='Damage'?46:22),-direction(defender)*7,110);
   if(!await pause(['Clash','Tie'].includes(e.type)?100:170)||!valid())return;
   stage?.classList.remove('contact-kick');
   if(['Clash','Tie'].includes(e.type))return;
   const defeated=(this.units.get(e.targetId)?.hp??1)<=0;
   this.setPosePhase(pair,'recover');this.pose(attacker,4,'recover');this.pose(defender,defeated?5:4,'recover');
   if(defeated&&defender)defender.wrapper.style.opacity='.25';
   for(const f of pair)this.place(f,center(f),0,120);
   if(!await pause(130)||!valid())return;
   this.setPosePhase(pair,'return');for(const f of pair){this.pose(f,f===defender&&defeated?5:0,'return');this.place(f,f?.start,0,170);}
   await pause(180);
  }finally{
   // A cancelled coroutine must not restore over a newer encounter.
   if(this.meleePair===pair&&(!['Clash','Tie'].includes(e.type)||!valid()))this.releaseMelee();
  }
 }
 number(id,amount,heal=false){this.expire(this.node('fx-number'+(heal?' healing':''),this.point(id),heal?'#9ce8d6':this.tint(id),heal?'+'+amount:String(amount)),950);}
 caption(title,detail='',tag='③ 실행 · 빛이 맞닿는 순간'){
  const n=this.root.querySelector('.combat-caption');if(!n)return;n.classList.add('visible');
  n.querySelector('strong').textContent=title;n.querySelector('span').textContent=detail;n.querySelector('.caption-overline').textContent=tag;
 }
 drag(actor,ids,p,focus){
  this.root.querySelectorAll('.drop-ready,.drop-focus').forEach(n=>n.classList.remove('drop-ready','drop-focus'));
  if(!p){this.root.querySelector('.drag-cursor')?.classList.remove('visible');return;}
  ids.forEach(id=>this.target(id)?.classList.add('drop-ready'));this.target(focus)?.classList.add('drop-focus');
  const cursor=this.root.querySelector('.drag-cursor');if(!cursor)return;
  cursor.classList.add('visible');cursor.classList.toggle('matched',!!focus);cursor.style.left=p.x+'px';cursor.style.top=p.y+'px';cursor.style.setProperty('--fx-color',this.tint(actor));
  cursor.querySelector('span').textContent=focus?(this.battle.intents.some(i=>i.id===focus)?'놓아서 맞불 연결':'놓아서 연결'):'빛실을 이으세요';
 }
 connected(actor,slot,target,kind){
  const a=this.options.slotPoint?.(actor,slot)||{x:111+this.battle.allies.findIndex(u=>u.id===actor)*360+slot*174,y:821},b=this.point(target),c=this.tint(actor);
  this.beam(a,b,c);this.shards(b,c);this.expire(this.node('fx-link-label',b,c,kind==='Clash'?'맞불 연결':kind==='Support'?'치유 연결':kind==='Guard'?'호위 연결':'공격 연결'),1000);
 }
 applyEvent(e){
  const u=this.units.get(e.targetId);
  if(e.type==='Damage'&&u)u.hp=Math.max(0,u.hp-Math.max(0,e.amount));
  if(e.type==='Heal'&&u)u.hp=Math.min(u.maxHp,u.hp+Math.max(0,e.amount));
  if(e.type==='PartBreak'&&this.parts.has(e.targetId))this.parts.get(e.targetId).hp=0;
  if(e.type==='Rift'&&u)u.rift=0;
  if(e.type==='Cancel'&&e.targetId)this.cancelled.add(e.targetId);
  this.paint();
 }
 paint(){
  for(const u of this.units.values()){
   this.root.querySelectorAll('[data-hp-bar]').forEach(n=>{if(n.dataset.hpBar===u.id)n.style.width=u.hp/u.maxHp*100+'%';});
   this.root.querySelectorAll('[data-hp-text]').forEach(n=>{if(n.dataset.hpText===u.id)n.textContent=u.hp+' / '+u.maxHp+(u.isEnemy?' · 균열 '+u.rift+' / '+u.maxRift:'');});
   this.portrait(u.id)?.classList.toggle('dead',u.hp<=0);
   this.root.querySelectorAll('[data-sanity]').forEach(n=>{if(n.dataset.sanity===u.id)n.textContent='정신력 '+((u.sanity||0)>0?'+':'')+(u.sanity||0);});
  }
  for(const p of this.parts.values()){const n=this.target(p.id);if(n){n.textContent=p.name+' · '+(p.hp<=0?'파괴됨':p.hp+'/'+p.maxHp);n.classList.toggle('part-broken',p.hp<=0);}}
  for(const id of this.cancelled){this.target(id)?.classList.add('intent-cancelled');this.root.querySelectorAll('[data-intent-line]').forEach(n=>{if(n.dataset.intentLine===id)n.classList.add('intent-cancelled');});}
 }
 sync(battle){this.units=new Map([...battle.allies,...battle.enemies].map(u=>[u.id,{...u}]));this.parts=new Map(battle.enemies.flatMap(u=>u.parts.map(p=>[p.id,{...p}])));this.paint();}
 showFlameRoll(e){
  this.rollActors=new Set([e.actorId,...(e.enemyFlameStates?.length?[e.targetId]:[])]);
  this.root.querySelectorAll('.flame-roll').forEach(n=>n.remove());
  const card=this.node('flame-roll',{x:1055,y:150});if(!card)return null;
  card.dataset.clashIndex=String(e.clashIndex||0);
  const title=document.createElement('div');title.className='flame-roll-title';title.textContent=e.clashIndex?`맞불 ${e.clashIndex}회 · 2연속 우세를 겨룹니다`:'스킬 점등';card.appendChild(title);
  const sides=[];
  const make=(states,power,sanity,enemy,id,base,flamePower)=>{
   const side=document.createElement('div');side.className='flame-roll-side'+(enemy?' darkness':'');
   const label=document.createElement('small');label.textContent=(this.units.get(id)?.name||'')+' · 정신력 '+((sanity||0)>0?'+':'')+(sanity||0);side.appendChild(label);
   const flames=document.createElement('div');flames.className='rolled-flames';const lamps=[];
   for(const state of states||[]){const lamp=document.createElement('i');lamp.className='flame unknown';lamp.dataset.flame='unknown';flames.appendChild(lamp);lamps.push(lamp);}side.appendChild(flames);
   const value=document.createElement('strong');value.textContent='—';side.appendChild(value);card.appendChild(side);
   sides.push({states:states||[],lamps,value,power:power||0,base:base||0,flamePower:flamePower||0,lit:0,enemy});
  };
  make(e.flameStates,e.actorPower,e.actorSanity,this.units.get(e.actorId)?.isEnemy,e.actorId,e.basePower,e.flamePower);
  if(e.enemyFlameStates?.length)make(e.enemyFlameStates,e.targetPower,e.targetSanity,this.units.get(e.targetId)?.isEnemy,e.targetId,e.enemyBasePower,e.enemyFlamePower);
  return{card,sides};
 }
 revealFlame(side,index){
  const state=side.states[index];if(index>=side.states.length)return;
  const lamp=side.lamps[index];lamp.className='flame '+(state?'on':'off');lamp.dataset.flame=state?'on':'off';side.lit+=state?1:0;
  const rawFinal=side.base+side.states.filter(Boolean).length*side.flamePower,partial=side.base+side.lit*side.flamePower;
  if(side.base>0&&rawFinal>0)side.value.textContent=String(Math.round(side.power*partial/rawFinal*10)/10);
  if(index===side.states.length-1)side.value.textContent=String(Math.round(side.power*10)/10);
 }
 async animateFlameRoll(e,fast,epoch){
  const roll=this.showFlameRoll(e);if(!roll)return;
  const stage=this.root.querySelector('.battle'),actor=this.units.get(e.actorId),close=!!e.enemyFlameStates?.length&&Math.abs(e.actorPower-e.targetPower)<=2;
  const dramatic=this.motion&&!fast&&this.tensionCount<2&&this.presentationElapsed<22000&&((e.clashIndex>=2&&this.lastClash?.winnerStreak===1)||close||(actor&&actor.hp/actor.maxHp<=.35));
  const count=Math.max(1,...roll.sides.map(s=>s.states.length));
  if(!this.motion){for(const side of roll.sides)for(let index=0;index<side.states.length;index++)this.revealFlame(side,index);this.options.impact?.('ignite',0,{...this.audioMetadata(e),level:.35});this.caption(e.clashIndex?'맞불 · 점등':'스킬 · 점등',e.message,'③ 실행 · 정신력과 불');await this.step(110);return;}
  if(dramatic){this.tensionCount++;roll.card.classList.add('tension');stage?.classList.add('tension-roll');}
  const total=dramatic?820:fast?220:280,lead=dramatic?90:30,hold=dramatic?200:50,beat=(total-lead-hold)/count;
  try{
   if(!await this.step(lead)||epoch!==this.epoch)return;
   for(let index=0;index<count;index++){
    for(const side of roll.sides){const lamp=side.lamps[index];if(lamp)lamp.classList.add('flicker');}
    if(!await this.step(beat*.45)||epoch!==this.epoch)return;
    for(const side of roll.sides)this.revealFlame(side,index);
    this.options.impact?.('ignite',0,{...this.audioMetadata(e),level:.35});
    if(!await this.step(beat*.55)||epoch!==this.epoch)return;
   }
   for(const side of roll.sides)side.value.textContent=String(Math.round(side.power*10)/10);
   roll.card.classList.add('decided');this.caption(e.clashIndex?'맞불 · 점등':'스킬 · 점등',e.message,'③ 실행 · 정신력과 불');
   await this.step(hold);
  }finally{if(epoch===this.epoch)stage?.classList.remove('tension-roll');}
 }
 async event(e,fast,epoch){
  const pause=ms=>this.step(this.motion?(fast?ms*.7:ms):110),a=this.point(e.actorId),b=this.point(e.targetId),c=this.tint(e.actorId);
  if(e.type==='Ignite'){
   this.lastClash=null;this.rollActors.clear();this.root.querySelectorAll('.flame-roll').forEach(n=>n.remove());
   this.root.querySelectorAll('.fx-active').forEach(n=>n.classList.remove('fx-active'));
   this.active={actor:e.actorId,target:e.targetId,clash:this.battle.intents.some(i=>i.id===e.targetId)};
   this.portrait(e.actorId)?.classList.add('fx-active');this.target(e.targetId)?.classList.add('fx-active');
   this.caption(e.message,this.active.clash?'불과 어둠을 밝혀 두 번 연속 우세를 겨룹니다':'공격 자세를 잡습니다','③ 실행 · 점등 / 격돌 / 타격');await pause(230);
  }else if(e.type==='FlameRoll'){
   this.caption(e.clashIndex?'맞불 · 점등':'스킬 · 점등',e.enemyFlameStates?.length?'불과 어둠의 위력을 판정합니다…':this.units.get(e.actorId)?.isEnemy?'어둠의 발현을 확인합니다…':'불의 점등을 확인합니다…','③ 실행 · 정신력과 불');await this.animateFlameRoll(e,fast,epoch);
  }else if(e.type==='Damage'||e.type==='Tie'||e.type==='Clash'){
   const clash=e.type!=='Damage';if(clash)this.lastClash=e;
   if(e.type==='Damage'&&!this.rollActors.has(e.actorId)){this.root.querySelectorAll('.flame-roll').forEach(n=>n.remove());this.rollActors.clear();}
   this.caption(clash?'맞불 · 무기 격돌':this.units.get(e.actorId)?.isEnemy?'적의 공격':'공격',e.message,'③ 실행 · 격돌 / 타격');
   await this.melee(e,fast,epoch,clash);
  }else{
   this.applyEvent(e);
   if(e.type==='Heal'){this.beam(a,b,'#75c8c4');this.pulse(b,'#75c8c4',true);this.number(e.targetId,e.amount,true);this.caption('치유의 물결',e.message);this.options.impact?.('ignite',0,{...this.audioMetadata(e),level:.35});}
   else if(e.type==='Guard'||e.type==='Evade'||e.type==='Counter'){this.pulse(b,'#a7d4e9',true);this.expire(this.node('fx-guard',b,'#a7d4e9','◇'),1400);this.caption(e.type==='Evade'?'회피':e.type==='Counter'?'반격 준비':'빛의 호위',e.message);this.options.impact?.(e.type==='Evade'?'swing':'brace',0,this.audioMetadata(e));}
   else if(e.type==='PartBreak'||e.type==='Rift'){
    this.shards(b,'#e6c7ff',true);this.expire(this.node('fx-fracture',b,'#e6c7ff','✧'),900);
    this.caption(e.type==='Rift'?'균열 붕괴':'부위 파괴',e.message);this.options.impact?.('break',e.amount,this.audioMetadata(e));
    if(this.motion){const stage=this.root.querySelector('.battle');stage?.classList.add('fx-shake');this.wait(330).then(()=>stage?.classList.remove('fx-shake'));}
   }else if(e.type==='ClashWin'){
    this.caption('맞불 · 2연속 우세',e.message);this.expire(this.node('fx-verdict',{x:1060,y:470},'#91dfd2','2연속 우세'),750);
   }else if(e.type==='Stalemate'){
    this.releaseMelee();this.caption('맞불 · 결착 없음',e.message);this.active=null;
   }else if(e.type==='Sanity'){this.caption('정신력 변화',e.message);}
   else if(e.type==='Resonance'){this.shards(b,'#8dddd3',true);this.caption('속성 공명',e.message);this.options.impact?.('ignite',0,{...this.audioMetadata(e),level:.35});}
   else if(e.type==='Lantern'){this.pulse(a,'#ffcc7b',true);this.caption('등불 해방',e.message);this.options.impact?.('ignite',0,{...this.audioMetadata(e),level:.35});}
   else if(e.type==='Relink'){if(this.active){this.active.target=e.targetId;this.active.clash=false;}this.caption('빛실 재연결',e.message);this.beam(a,b,c);}
   else if(e.type==='BattleEnd'||e.type==='WrapUp'){this.releaseMelee();this.root.querySelectorAll('.flame-roll').forEach(n=>n.remove());this.caption(e.type==='BattleEnd'?'남겨진 빛':'라운드 정리',e.message);this.active=null;}
   else if(e.type==='Illusion'){this.caption('허상 소멸',e.message);this.active=null;}
   if(!['Cancel','DefeatUnit'].includes(e.type))await pause(e.type==='Rift'||e.type==='PartBreak'?650:330);
  }
 }
 async play(events,finalBattle,{fast=false}={}){
  const epoch=this.epoch;this.presentationElapsed=0;this.tensionCount=0;this.rollActors=new Set();this.lastClash=null;this.root.querySelector('.battle')?.classList.add('resolving');
  const phase=this.root.querySelector('.phase');if(phase)phase.textContent='ROUND '+String(this.battle.round).padStart(2,'0')+' / ③ 실행';
  for(let i=0;i<events.length;i++){
   if(epoch!==this.epoch)return false;
   const bar=this.root.querySelector('.playback-progress i');if(bar)bar.style.width=(i+1)/events.length*100+'%';
   await this.event(events[i],fast,epoch);
  }
  if(epoch!==this.epoch)return false;this.releaseMelee();this.root.querySelectorAll('.flame-roll').forEach(n=>n.remove());this.sync(finalBattle);await this.wait(fast?240:550);return epoch===this.epoch;
 }
}
globalThis.LanternCombatFX=LanternCombatFX;
