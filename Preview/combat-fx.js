'use strict';

// Presentation owns a copy of the pre-round state. Only server events change it;
// effects never calculate damage or feed values back into the battle simulation.
class LanternCombatFX {
 constructor(root, options={}) {
  this.root=root; this.options=options; this.epoch=0; this.pending=new Map();
  this.units=new Map(); this.parts=new Map(); this.cancelled=new Set();
  this.atlases=new Map();
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
  this.cancelled=new Set(); this.active=null;
  this.root.querySelector('.battle')?.insertAdjacentHTML('beforeend',
   '<div class="combat-fx" aria-hidden="true"></div><div class="drag-cursor" aria-hidden="true"><i></i><span></span></div><div class="combat-caption" role="status" aria-live="polite"><div class="caption-overline">RESONANCE / 連光</div><strong></strong><span></span><div class="playback-progress"><i></i></div></div>');
 }
 clear(){
  this.epoch++;
  this.releaseMelee();
  for(const [id,done] of this.pending){clearTimeout(id);done(false);}this.pending.clear();
  this.root.querySelectorAll('.combat-fx,.combat-caption,.drag-cursor').forEach(n=>n.remove());
  this.root.querySelectorAll('.fx-active,.drop-ready,.drop-focus,.fx-lunge,.fx-hit').forEach(n=>n.classList.remove('fx-active','drop-ready','drop-focus','fx-lunge','fx-hit'));
  this.root.querySelector('.battle')?.classList.remove('resolving','fx-shake');
 }
 wait(ms){return new Promise(done=>{const id=setTimeout(()=>{this.pending.delete(id);done(true);},ms);this.pending.set(id,done);});}
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
  const atlas=!this.atlases.has(id)||this.atlases.get(id).ready;
  sprite.style.backgroundImage=atlas?`url('/combat/${names[id]||'Lian'}Actions.png')`:`url('${original.src}')`;
  if(!atlas)sprite.style.backgroundSize='contain';
  wrapper.appendChild(sprite);
  const layout=this.options.layouts?.[id];
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
  const stage=this.root.querySelector('.battle');if(stage){stage.classList.remove('melee-active','hit-stop','contact-kick');stage.dataset.combatPhase='';}
 }
 async melee(e,fast,epoch,clash){
  // Atlas cells share one foot pivot. Translation belongs to the wrapper;
  // switching a pose never crops or rescales a character's individual frame.
  this.releaseMelee();
  const attacker=this.createFighter(e.actorId,this.units.get(e.actorId)?.isEnemy?'enemy-side':'ally-side');
  const defender=this.createFighter(e.targetId,this.units.get(e.targetId)?.isEnemy?'enemy-side':'ally-side');
  const pair=[attacker,defender];this.meleePair=pair;
  const stage=this.root.querySelector('.battle');stage?.classList.add('melee-active');
  const pause=ms=>this.wait(this.motion?ms*(fast?.76:1):90);
  const valid=()=>epoch===this.epoch;
  const attackFrame=clash&&e.actorId==='anger'&&e.targetId==='sera'?3:2;
  const counterFrame=clash&&e.targetId==='anger'&&e.actorId==='sera'?3:2;
  const reach=this.weaponOffset(attacker,attackFrame),counterReach=this.weaponOffset(defender,counterFrame);
  const center=f=>f===attacker?1055-reach.x:clash?1055-counterReach.x:1055+(f?.side==='enemy-side'?65:-65);
  const direction=f=>f?.side==='enemy-side'?-1:1;
  const hit={x:1055,y:610+(clash?(reach.y+counterReach.y)/2:reach.y)};
  try{
   this.setPosePhase(pair,'approach');
   // One painted frame at the original position before starting the approach.
   if(!await pause(20)||!valid())return;
   for(const f of pair)this.place(f,center(f),0,fast?130:180);
   if(!await pause(180)||!valid())return;
   this.setPosePhase(pair,'windup');this.pose(attacker,1,'windup');this.pose(defender,clash?1:0,'windup');
   this.place(attacker,center(attacker)-direction(attacker)*24,-direction(attacker)*5,100);
   if(clash)this.place(defender,center(defender)-direction(defender)*18,-direction(defender)*4,100);
   if(!await pause(150)||!valid())return;
   this.setPosePhase(pair,'swing');this.pose(attacker,attackFrame,'swing');if(clash)this.pose(defender,counterFrame,'swing');
   this.place(attacker,center(attacker),0,70);
   if(clash)this.place(defender,center(defender),0,70);
   this.options.impact?.('swish',e.amount);
   if(!await pause(75)||!valid())return;
   this.setPosePhase(pair,'contact');this.pose(attacker,attackFrame,'contact');this.pose(defender,clash?counterFrame:5,'contact');
   if(e.type==='Damage')this.applyEvent(e);
   this.options.impact?.(clash?'clash':'hit',e.amount);
   if(this.motion){
    stage?.classList.add('hit-stop','contact-kick');
    this.shards(hit,clash?'#ffdea1':this.tint(e.actorId));
    this.expire(this.node('weapon-cut '+(direction(attacker)<0?'reverse':''),hit,this.tint(e.actorId)),230);
    if(clash)this.expire(this.node('weapon-contact',hit,'#ffe4b0'),170);
   }
   if(e.type==='Damage')this.expire(this.node('fx-number', this.motion?{x:center(defender),y:320}:this.point(e.targetId),this.tint(e.targetId),String(e.amount)),800);
   else {this.caption('무기 충돌 · 상쇄',e.message);this.expire(this.node('fx-link-label',hit,'#ffdfa5','상쇄'),600);}
   // Both silhouettes hold on the exact contact pose before the recoil.
   if(!await this.wait(this.motion?65:90)||!valid())return;
   stage?.classList.remove('hit-stop');
   this.setPosePhase(pair,'recoil');this.pose(attacker,3,'recoil');this.pose(defender,e.type==='Damage'?5:4,'recoil');
   this.place(defender,center(defender)-direction(defender)*(e.type==='Damage'?46:22),-direction(defender)*7,110);
   if(!await pause(170)||!valid())return;
   stage?.classList.remove('contact-kick');
   const defeated=(this.units.get(e.targetId)?.hp??1)<=0;
   this.setPosePhase(pair,'recover');this.pose(attacker,4,'recover');this.pose(defender,defeated?5:4,'recover');
   if(defeated&&defender)defender.wrapper.style.opacity='.25';
   for(const f of pair)this.place(f,center(f),0,120);
   if(!await pause(130)||!valid())return;
   this.setPosePhase(pair,'return');for(const f of pair){this.pose(f,f===defender&&defeated?5:0,'return');this.place(f,f?.start,0,170);}
   await pause(180);
  }finally{
   // A cancelled coroutine must not restore over a newer encounter.
   if(this.meleePair===pair)this.releaseMelee();
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
  const a={x:113+this.battle.allies.findIndex(u=>u.id===actor)*340+slot*146,y:756},b=this.point(target),c=this.tint(actor);
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
  }
  for(const p of this.parts.values()){const n=this.target(p.id);if(n){n.textContent=p.name+' · '+(p.hp<=0?'파괴됨':p.hp+'/'+p.maxHp);n.classList.toggle('part-broken',p.hp<=0);}}
  for(const id of this.cancelled){this.target(id)?.classList.add('intent-cancelled');this.root.querySelectorAll('[data-intent-line]').forEach(n=>{if(n.dataset.intentLine===id)n.classList.add('intent-cancelled');});}
 }
 sync(battle){this.units=new Map([...battle.allies,...battle.enemies].map(u=>[u.id,{...u}]));this.parts=new Map(battle.enemies.flatMap(u=>u.parts.map(p=>[p.id,{...p}])));this.paint();}
 async event(e,fast,epoch){
  const pause=ms=>this.wait(this.motion?(fast?ms*.7:ms):110),a=this.point(e.actorId),b=this.point(e.targetId),c=this.tint(e.actorId);
  if(e.type==='Ignite'){
   this.root.querySelectorAll('.fx-active').forEach(n=>n.classList.remove('fx-active'));
   this.active={actor:e.actorId,target:e.targetId,clash:this.battle.intents.some(i=>i.id===e.targetId)};
   this.portrait(e.actorId)?.classList.add('fx-active');this.target(e.targetId)?.classList.add('fx-active');
   this.caption(e.message,this.active.clash?'무기를 맞대고 타점을 겨룹니다':'공격 자세를 잡습니다','③ 실행 · 접근 / 격돌 / 반격');await pause(230);
  }else if(e.type==='Damage'||e.type==='Tie'){
   const clash=this.active?.clash&&(e.actorId===this.active.actor||e.targetId===this.active.actor);
   this.caption(clash?'맞불 · 무기 격돌':this.units.get(e.actorId)?.isEnemy?'적의 반격':'직접 공격',e.message,'③ 실행 · 접근 / 격돌 / 반격');
   await this.melee(e,fast,epoch,clash);
  }else{
   this.applyEvent(e);
   if(e.type==='Heal'){this.beam(a,b,'#75c8c4');this.pulse(b,'#75c8c4',true);this.number(e.targetId,e.amount,true);this.caption('치유의 물결',e.message);}
   else if(e.type==='Guard'){this.pulse(b,'#a7d4e9',true);this.expire(this.node('fx-guard',b,'#a7d4e9','◇'),1400);this.caption('빛의 호위',e.message);}
   else if(e.type==='PartBreak'||e.type==='Rift'){
    this.shards(b,'#e6c7ff',true);this.expire(this.node('fx-fracture',b,'#e6c7ff','✧'),900);
    this.caption(e.type==='Rift'?'균열 붕괴':'부위 파괴',e.message);this.options.sound?.(130,.3);
    if(this.motion){const stage=this.root.querySelector('.battle');stage?.classList.add('fx-shake');this.wait(330).then(()=>stage?.classList.remove('fx-shake'));}
   }else if(e.type==='ClashWin'||e.type==='Clash'){
    this.caption(e.type==='ClashWin'?'맞불 우세':'맞불 종료',e.message);this.expire(this.node('fx-verdict',{x:1060,y:470},e.type==='ClashWin'?'#91dfd2':'#e8c894',e.type==='ClashWin'?'맞불 우세':'맞불 종료'),950);this.active=null;
   }else if(e.type==='Resonance'){this.shards(b,'#8dddd3',true);this.caption('속성 공명',e.message);}
   else if(e.type==='Lantern'){this.pulse(a,'#ffcc7b',true);this.caption('등불 해방',e.message);}
   else if(e.type==='Relink'){if(this.active){this.active.target=e.targetId;this.active.clash=false;}this.caption('빛실 재연결',e.message);this.beam(a,b,c);}
   else if(e.type==='BattleEnd'||e.type==='WrapUp'){this.caption(e.type==='BattleEnd'?'남겨진 빛':'라운드 정리',e.message);this.active=null;}
   else if(e.type==='Illusion'){this.caption('허상 소멸',e.message);this.active=null;}
   if(!['Cancel','DefeatUnit'].includes(e.type))await pause(e.type==='Rift'||e.type==='PartBreak'?650:330);
  }
 }
 async play(events,finalBattle,{fast=false}={}){
  const epoch=this.epoch;this.root.querySelector('.battle')?.classList.add('resolving');
  const phase=this.root.querySelector('.phase');if(phase)phase.textContent='ROUND '+String(this.battle.round).padStart(2,'0')+' / ③ 실행';
  for(let i=0;i<events.length;i++){
   if(epoch!==this.epoch)return false;
   const bar=this.root.querySelector('.playback-progress i');if(bar)bar.style.width=(i+1)/events.length*100+'%';
   await this.event(events[i],fast,epoch);
  }
  if(epoch!==this.epoch)return false;this.sync(finalBattle);await this.wait(fast?240:550);return epoch===this.epoch;
 }
}
globalThis.LanternCombatFX=LanternCombatFX;
