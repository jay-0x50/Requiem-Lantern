// Runs the actual browser script with isolated DOM, storage, fetch and timer stubs.
// No server mutations or third-party dependencies. Browser visual QA is separate.
// Run: node Tests/preview-regression.cjs
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');

const source = fs.readFileSync(path.join(__dirname, '../Preview/app.js'), 'utf8')
  .replace(/boot\(\);\s*$/, '');
const combatSource = fs.readFileSync(path.join(__dirname, '../Preview/combat-fx.js'), 'utf8');
const storySource = fs.readFileSync(path.join(__dirname, '../Preview/story-view.js'), 'utf8');
let passed = 0;
const skill = (id, kind = 'Attack') => ({
  id, name: id, kind, cost: 0, slots: 1, speed: 10, hitPowers: [6],number:kind==='Guard'?0:/breakthrough|heal/.test(id)?2:1,basePower:4,flamePower:2,flameCount:1
});
const state = {
  progress: { lanternFragments: 0, coins: 0, prologueCompleted: false, keepLevel: 0, memoryFragments: 0 },
  plannedCost: 0,
  availableEmber: 6,
  previews: [{ actorId: 'lian', slot: 0, preview: { grade: 'Advantage', summary: '우세' } }],
  story: {
    prologue: [
      { speaker: '리안', text: '첫 대사', portrait: 'Lian', background: 'black' },
      { speaker: '리안', text: '다음 대사', portrait: 'Lian' }
    ],
    firstMeal: [{ speaker: '세라', text: '식사하자', portrait: 'Sera', background: 'keep' }]
  },
  battle: {
    outcome: 'Ongoing', phase: 'Link',
    allies: [
      { id: 'lian', name: '리안', hp: 100, maxHp: 100, element: 'Radiance',slotCount:1,maxActionSlots:2,
        skills: [skill('lian_glimmer'), skill('lian_breakthrough'), skill('lian_guard', 'Guard')],parts:[],skillHand:[{id:'lian-card-1',skillId:'lian_glimmer'},{id:'lian-card-2',skillId:'lian_breakthrough'}],drawPile:['s1','s1','s2'],discardPile:[] },
      { id: 'minato', name: '미나토', hp: 90, maxHp: 100, element: 'Tide',slotCount:1,maxActionSlots:2,
        skills: [skill('minato_glimmer'), skill('minato_heal', 'Heal')],parts:[],skillHand:[{id:'minato-card-1',skillId:'minato_glimmer'},{id:'minato-card-2',skillId:'minato_heal'}],drawPile:['s1','s1','s2'],discardPile:[] }
    ],
    enemies: [{ id: 'anger', name: '분노', hp: 100, maxHp: 100, rift: 40, maxRift: 60,
      skills: [], parts: [{ id: 'arm', name: '팔', hp: 30 }, { id: 'core', name: '핵', hp: 30 }] }],
    intents: [{ id: 'intent1', actorId: 'anger', targetId: 'lian', hitPowers: [10], speed: 5, name: '난타', kind: 'Normal' }],
    links: [], events: [], round: 1, ember: 6, lantern: 0, gloom: 0
  }
};

function environment(storage = 'null') {
  const events = {}, gameEvents = {}, timers = new Map(), intervals = new Map();
  let id = 0, hasModal = false, modalHtml='', couponCode='', hit = null, tooltip=null, elementSlots=[], linkPaths=[], gameBox={ left: 0, top: 0, width: 1600, height: 900 };
  const words = { textContent: '' };
  const thread = { d: '', setAttribute(key, value) { this[key] = value; } };
  const game = {
    style: {}, classList: { toggle() {} }, innerHTML: '',
    addEventListener: (name, handler) => { gameEvents[name] = handler; },
    getBoundingClientRect: () => gameBox,
    querySelector: selector => selector === '.modal'
      ? (hasModal ? { remove() { hasModal = false;modalHtml=''; } } : null)
      : selector === '#coupon-code' ? {value:couponCode} : selector === '.intent-tooltip' ? tooltip : selector === '.words' ? words : selector === '#drag-thread' ? thread : null,
    insertAdjacentHTML(_position,html) { if(html.includes('class="intent-tooltip"'))tooltip={html,style:{},offsetHeight:420,remove(){tooltip=null;}};else {hasModal = true;modalHtml=html;} },
    querySelectorAll: selector => selector==='.slot'?elementSlots:selector==='[data-link-actor]'?linkPaths:[]
  };
  const context = {
    document: {
      addEventListener:(name,handler)=>{events[name]=handler;},hidden:false,
      querySelector: selector => selector === '#game' ? game : { style: {}, textContent: '' },
      elementFromPoint: () => hit ? { closest: () => hit } : null
    },
    localStorage: { getItem: () => storage, setItem() {} },
    innerWidth: 1600, innerHeight: 900,
    addEventListener: (name, handler) => { events[name] = handler; },
    removeEventListener() {},
    setTimeout: (handler, delay) => { timers.set(++id, { handler, delay }); return id; },
    clearTimeout: timer => timers.delete(timer),
    setInterval: handler => { intervals.set(++id, handler); return id; },
    clearInterval: timer => intervals.delete(timer),
    console, performance: { now: () => 1000 }, requestAnimationFrame() {},
    fetch: async () => ({ ok: true, json: async () => JSON.parse(JSON.stringify(state)) })
  };
  vm.createContext(context);
  vm.runInContext(combatSource, context);
  vm.runInContext(storySource, context);
  vm.runInContext(source, context);
  vm.runInContext('data=' + JSON.stringify(state), context);
  vm.runInContext('battleFx.attach(data.battle)', context);
  return {
    context, events, gameEvents, game, words, thread, timers, intervals,
    get tooltip(){return tooltip;},
    get modalHtml(){return modalHtml;},setCouponCode(value){couponCode=value;},
    run: code => vm.runInContext(code, context),
    setGeometry(box,slots=[],paths=[]) { gameBox=box;elementSlots=slots;linkPaths=paths; },
    setHit(targetId, disabled = false) {
      hit = targetId ? { disabled, dataset: { action: 'target:' + targetId } } : null;
    },
    click: async action => gameEvents.click({ target: { closest: () => ({ dataset: { action }, textContent: '' }) } }),
    flush() {
      const [timer, entry] = timers.entries().next().value;
      timers.delete(timer);
      entry.handler();
    }
  };
}

function pass(name) { passed++; console.log('PASS ' + name); }
async function storyUxChecks(){
 let env=environment();env.run(`data.story.prologue=[{speaker:'세라',text:'가나다라마바사',portrait:'Sera',listenerPortrait:'Lian',autoDelay:4.4},{speaker:'리안',text:'다음 줄 비밀',portrait:'Lian',listenerPortrait:'Sera'}];startStory('prologue');storyAuto=true;`);
 const originalTyping=[...env.intervals.values()][0];originalTyping();originalTyping();
 assert.equal(env.words.textContent,'가나');await env.click('storylog');
 assert.ok(env.modalHtml.includes('가나'));assert.ok(!env.modalHtml.includes('다라'));assert.ok(!env.modalHtml.includes('다음 줄 비밀'));assert.equal(env.intervals.size,0);assert.equal(env.timers.size,0);
 originalTyping();assert.equal(env.words.textContent,'가나');
 env.events.keydown({code:'Space',preventDefault(){}});assert.equal(env.run('storyIndex'),0);
 await env.click('close');assert.equal(env.intervals.size,1);assert.equal(env.words.textContent,'가나');
 env.context.nextStory();assert.equal(env.words.textContent,'가나다라마바사');assert.equal([...env.timers.values()][0].delay,4400);
 const staleAuto=[...env.timers.values()][0].handler;await env.click('storylog');staleAuto();assert.equal(env.run('storyIndex'),0);assert.equal(env.timers.size,0);
 await env.click('close');assert.equal(env.timers.size,1);staleAuto();assert.equal(env.run('storyIndex'),0);env.flush();assert.equal(env.run('storyIndex'),1);
 pass('story history contains only revealed text, pauses typing and Auto, resumes the same prefix, and rejects stale Auto callbacks');

 env=environment();env.run(`startStory('prologue');storyAuto=true;`);const pendingTyping=[...env.intervals.values()][0];pendingTyping();env.events.blur();assert.equal(env.intervals.size,0);pendingTyping();assert.equal(env.words.textContent,'첫');env.events.focus();assert.equal(env.intervals.size,1);env.context.nextStory();const oldAuto=[...env.timers.values()][0].handler;env.context.document.hidden=true;env.events.visibilitychange();assert.equal(env.timers.size,0);oldAuto();assert.equal(env.run('storyIndex'),0);env.context.document.hidden=false;env.events.visibilitychange();assert.equal([...env.timers.values()][0].delay,2600);oldAuto();assert.equal(env.run('storyIndex'),0);
 pass('window blur and hidden tabs pause both typing and Auto; returning preserves text and starts a fresh full wait');

 env=environment();env.run(`startStory('prologue');storyAuto=true;`);
 for(const [delay,expected] of [[undefined,2600],[0,1000],[-5,1000],[.2,1000],[6,6000],[99,6000],['invalid',2600]]){env.context.delay=delay;env.run('story[storyIndex].autoDelay=delay');assert.equal(env.context.storyAutoDelay(),expected);}
 env.run(`story=Array.from({length:80},(_,i)=>({speaker:'리안',text:'읽은 줄 '+i}));storyIndex=79;storyLog=story.map((l,i)=>i<79?'리안  '+l.text:'');storyVisibleCount=2;updateStoryWords();`);await env.click('storylog');assert.ok(env.modalHtml.includes('읽은 줄 0'));assert.ok(env.modalHtml.includes('읽은 줄 78'));assert.ok(!env.modalHtml.includes('읽은 줄 79'));assert.ok(env.modalHtml.includes('tabindex="0"'));
 pass('per-line Auto delay is clamped to one through six seconds and scrollable history retains every read line');

 env=environment();env.run(`data.story.prologue=[{speaker:'세라',text:'첫 줄',portrait:'Sera',listenerPortrait:'Lian'},{speaker:'리안',text:'응답',portrait:'Lian',listenerPortrait:'Sera'},{text:'잠시 말이 없다.',portrait:'Noa'},{speaker:'미나토',text:'괜찮아요?',portrait:'Minato',listenerPortrait:'Lian'}];startStory('prologue');`);
 const seats=()=>Array.from(env.run('storyPortraitSlots'));assert.deepEqual(seats(),['Sera','Lian']);
 env.context.nextStory();env.context.nextStory();assert.deepEqual(seats(),['Sera','Lian']);assert.ok(env.game.innerHTML.includes('story-position-right is-speaking'));assert.ok(env.game.innerHTML.includes('story-position-left story-listener is-listening'));
 env.context.nextStory();env.context.nextStory();assert.deepEqual(seats(),['Sera','Lian']);
 env.context.nextStory();env.context.nextStory();assert.deepEqual(seats(),['Minato','Lian']);
 env.run(`storyPortraits({speaker:'노아',portrait:'Noa',location:'새 장소'})`);env.run(`storyPortraits({speaker:'세라',portrait:'Sera',listenerPortrait:'Mare',location:'다른 장소'})`);assert.deepEqual(seats(),['Sera','Mare']);
 pass('speakers keep their left or right seat, narration preserves the cast, and a third participant replaces only the omitted seat');

 env=environment();campaignFixture(env);env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[1];data.campaign.phase='BeforeStory';data.campaign.currentStory=[{speaker:'리안',text:'가자.',portrait:'Lian'}];routeCampaign();`);assert.ok(env.game.innerHTML.includes('전투 시작'));
 env.run(`data.campaign.replay=true;routeCampaign();`);assert.ok(env.game.innerHTML.includes('항로로 돌아가기'));assert.ok(!env.game.innerHTML.includes('전투 시작'));
 env.run(`data.campaign.activeStage.before=[{}];data.campaign.activeStage.after=[{}];data.campaign.currentStory=[{text:'진입'},{text:'마무리'}];routeCampaign();`);assert.ok(env.game.innerHTML.includes('이어서 읽기'));env.context.nextStory();env.context.nextStory();assert.ok(env.game.innerHTML.includes('항로로 돌아가기'));
 env.run(`data.campaign.replay=false;data.campaign.phase='AfterStory';data.campaign.currentStory=[{text:'마무리'}];routeCampaign();`);assert.ok(env.game.innerHTML.includes('항로로 돌아가기'));
 env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[0];data.campaign.phase='BeforeStory';routeCampaign();`);assert.ok(env.game.innerHTML.includes('항로로 돌아가기'));
 pass('the final dialogue prompt matches battle entry, story completion and replay return paths');

 env=environment();campaignFixture(env);env.run(`for(const s of data.campaign.chapters[0].stages.slice(0,3)){s.cleared=true;s.unlocked=true;}data.campaign.chapters[0].stages[3].unlocked=true;showHub();`);
 assert.ok(env.game.innerHTML.includes('이어보기 · 1-4'));assert.ok(env.game.innerHTML.includes('data-action="stage-enter:c1s04"'));assert.ok(env.game.innerHTML.includes('낯선 불꽃'));assert.ok(env.game.innerHTML.includes('data-action="go:stages"'));
 env.context.showStages();assert.ok(env.game.innerHTML.includes('<small>등장인물</small>'));
 pass('the hub continues the exact unlocked story while retaining the route, whose cast is explicitly labeled');

 env=environment();recruitmentFixture(env);env.run(`data.recruitment.characters.find(c=>c.id==='kai').recruitable=false;data.recruitment.characters.push({id:'mare',name:'마레',rarity:4,portrait:'Mare',role:'화염 · 인양꾼',recruitable:true});data.recruitment.ownedCharacters.push('mare');showRecruitment();showRecruitRates();`);
 assert.ok(env.modalHtml.includes('마레'));assert.ok(!env.modalHtml.includes('<td>카이</td>'));assert.ok(env.modalHtml.includes('33.0667%'));env.context.closeModal();env.run(`showRoster(chars.findIndex(c=>c[0]==='Mare'));`);assert.ok(env.game.innerHTML.includes('/art/Mare.png'));assert.ok(env.game.innerHTML.includes('파티 편성'));env.run(`showRoster(chars.findIndex(c=>c[0]==='Kai'));`);assert.ok(env.game.innerHTML.includes('보유'));assert.ok(env.game.innerHTML.includes('party-picker:kai'));
 assert.equal(env.run("LanternCombatAtlasNames.mare"),'Mare');
 pass('Mare has her own portrait and SD atlas, recruitment excludes legacy Kai, and already-owned Kai remains usable');

 env=environment();env.run(`data.battle.links=[{actorId:'lian',slot:0,targetId:'intent1',kind:'Clash'}];data.previews=[{actorId:'lian',slot:0,grade:'Advantage',allyPower:12,enemyPower:8.4,summary:'우세'}];showBattle();`);assert.ok(env.game.innerHTML.includes('리안 → 예고 01'));assert.ok(env.game.innerHTML.includes('▲ 우세 · 기대 위력 아군 12 : 적 8.4'));
 env.run(`data.battle.intents[0].kind='Illusion';data.previews[0].summary='허상 — 피해 없이 행동만 소모합니다.';showBattle();`);assert.ok(env.game.innerHTML.includes('리안 → 예고 01'));assert.ok(env.game.innerHTML.includes('허상 — 피해 없이 행동만 소모합니다.'));assert.ok(!env.game.innerHTML.includes('기대 위력 아군 12'));
 pass('clash predictions identify the actor, the same enemy-owned forecast number and labeled faction powers while illusion keeps its harmless summary');
}
function fourParty(env){env.run(`
 const seed=data.battle.allies[0];
 const companion=(id,name,element)=>({...JSON.parse(JSON.stringify(seed)),id,name,portrait:id[0].toUpperCase()+id.slice(1),element,skills:seed.skills.map(k=>({...k,id:k.id.replace('lian',id)})),skillHand:seed.skillHand.map(c=>({...c,id:c.id.replace('lian',id),skillId:c.skillId.replace('lian',id)}))});
 data.battle.allies=[seed,companion('sera','세라','Flame'),data.battle.allies[1],companion('noa','노아','Shadow')];
 showBattle();
 `);}
function lateRound(env,round=5,boss=false){env.run(`data.battle.round=${round};data.battle.isBoss=${boss};data.battle.allies.forEach((a,n)=>{a.maxActionSlots=data.battle.isBoss&&n>=2?1:2;a.slotCount=a.maxActionSlots===2&&data.battle.round>=n+2?2:1;});showBattle();`);}
function threeEnemies(env){fourParty(env);env.run(`
 const original=data.battle.enemies[0];original.isEnemy=true;original.portrait='Anger';
 for(let n=2;n<=3;n++){const id='enemy_'+n;data.battle.enemies.push({...JSON.parse(JSON.stringify(original)),id,name:'그림자 군체 '+(n-1),parts:original.parts.map(p=>({...p,id:id+'_'+p.id,maxHp:p.hp}))});data.battle.intents.push({...JSON.parse(JSON.stringify(data.battle.intents[0])),id:'1:'+id+'_arm',actorId:id,targetId:'lian'});}
 showBattle();
 `);}

function candidateActionStatusChecks(){
 const label=(env,id)=>env.game.innerHTML.match(new RegExp('data-action-status="'+id+'"[^>]*>([^<]*)'))?.[1];
 let env=environment();fourParty(env);
 assert.equal(label(env,'lian'),'후보 2장 · 남은 행동 1/1');
 assert.ok(env.game.innerHTML.includes('매 턴 후보 2장 새로 추첨 · 미사용 카드도 교체'));
 env.run(`data.battle.allies[0].skillHand.pop();showBattle();`);
 assert.equal(label(env,'lian'),'후보 1장 · 남은 행동 1/1');
 pass('candidate labels use the actual hand size separately from the first-round one-action capacity');

 env=environment();fourParty(env);
 env.run(`data.battle.links=[{actorId:'lian',slot:0,skillId:'lian_glimmer',cardId:'lian-card-1',kind:'Strike',targetId:'anger'}];showBattle();`);
 assert.equal(label(env,'lian'),'후보 2장 · 남은 행동 0/1');assert.equal(label(env,'sera'),'후보 2장 · 남은 행동 1/1');
 env.run(`data.battle.links.push({...data.battle.links[0]});showBattle();`);
 assert.equal(label(env,'lian'),'후보 2장 · 남은 행동 0/1');
 pass('a reservation leaves both candidates visible while consuming one actual action, and duplicates never subtract twice');

 env=environment();fourParty(env);lateRound(env,2,true);
 assert.equal(label(env,'lian'),'후보 2장 · 남은 행동 2/2');assert.equal(label(env,'noa'),'후보 2장 · 남은 행동 1/1');
 env.run(`data.battle.links=[{actorId:'lian',slot:0,skillId:'lian_guard',kind:'Guard',targetId:'lian'}];showBattle();`);
 assert.equal(label(env,'lian'),'후보 2장 · 남은 행동 1/2');
 env.run(`data.battle.allies[0].skills.push({id:'custom_two',kind:'Attack',slots:2});data.battle.links=[{actorId:'lian',slot:0,skillId:'custom_two',kind:'Strike',targetId:'anger'},{actorId:'lian',slot:1,skillId:'lian_glimmer',kind:'Strike',targetId:'anger'},{actorId:'lian',slot:4,skillId:'lian_glimmer',kind:'Strike',targetId:'anger'}];showBattle();`);
 assert.equal(label(env,'lian'),'후보 2장 · 남은 행동 0/2');
 pass('round-two capacity, defense reservations and shared two-action skills count distinct open slots with the boss cap intact');

 env.run(`data.battle.allies[0].hp=0;showBattle();`);
 assert.equal(label(env,'lian'),'후보 2장 · 남은 행동 0/0');
 pass('an incapacitated fighter reports zero available and total actions even when an old reservation remains');
}

function campaignFixture(e) {
  const chapters = JSON.parse(fs.readFileSync(path.join(__dirname, '../기획서/메인스토리/campaign.json'), 'utf8')).chapters;
  for(const [index,chapter] of chapters.entries()){chapter.focusCharacterId||=['lian','sera','noa','heidel','minato'][index];chapter.focusStorySummary||='메인 스토리에 이어지는 인물의 과거와 현재';}
  for (const chapter of chapters) for (const stage of chapter.stages) { stage.unlocked = stage.id === 'c1s01'; stage.cleared = false; }
  e.run('data.campaign=' + JSON.stringify({ chapters, activeStage: null, phase: 'None', replay: false, currentStory: [] }));
  return chapters;
}

function recruitmentFixture(env){
 const characters=[['lian','리안',5,'Lian'],['sera','세라',5,'Sera'],['noa','노아',5,'Noa'],['minato','미나토',4,'Minato'],['heidel','하이델',4,'Heidel'],['kai','카이',4,'Kai']].map(([id,name,rarity,portrait])=>({id,name,rarity,portrait,role:'등불의 동료'}));
 env.run('data.recruitment='+JSON.stringify({characters,banners:characters.filter(c=>c.rarity===5).map(c=>({id:c.id,name:c.name+' 픽업',featuredCharacterId:c.id})),costPerPull:160,fiveStarRate:.008,hardPity:80,pity:0,pickupGuaranteed:false,ownedCharacters:['lian','minato','heidel','kai'],party:['lian','minato','heidel','kai'],stardust:3200,characterShards:0})+';tone=()=>{};');
}
async function recruitmentChecks(){
 let env=environment();recruitmentFixture(env);env.run("page='hub';showRecruitment();");
 assert.ok(env.game.innerHTML.includes('별가루 160'));assert.ok(env.game.innerHTML.includes('별가루 1,600'));assert.ok(env.game.innerHTML.includes('최대 80회'));assert.ok(env.game.innerHTML.includes('픽업 확률 50%'));
 env.run('data.recruitment.pity=79;data.recruitment.pickupGuaranteed=true;');await env.click('banner:noa');assert.equal(env.run('selectedBanner'),'noa');assert.ok(env.game.innerHTML.includes('최대 1회'));assert.ok(env.game.innerHTML.includes('다음 ★5는 픽업 확정'));
 await env.click('recruit-rates');assert.ok(env.modalHtml.includes('0.4%'));assert.ok(env.modalHtml.includes('0.2%'));assert.ok(env.modalHtml.includes('33.0667%'));assert.equal(env.run('data.recruitment.pity'),79);assert.equal(env.run('data.recruitment.pickupGuaranteed'),true);env.run('globalThis.behindModalRequests=0;command=async()=>{behindModalRequests++;return true;};');await env.context.pullRecruitment(1);assert.equal(env.context.behindModalRequests,0);
 pass('pickup selection exposes the real price, shared hard pity and guarantee without mutating them, and probability details list the full pool');

 env=environment();recruitmentFixture(env);env.run("page='hub';showRecruitment();");const calls=[];let release;
 env.context.fetch=async(_url,args)=>{calls.push(JSON.parse(args.body));return new Promise(done=>release=done);};
 const pending=env.context.pullRecruitment(10);await env.click('recruit:10');await env.click('banner:lian');env.events.keydown({code:'Escape',preventDefault(){}});assert.equal(calls.length,1);assert.equal(calls[0].action,'recruit');assert.equal(calls[0].bannerId,'sera');assert.equal(calls[0].count,10);assert.equal(env.run('page'),'recruitment');assert.equal(env.run('selectedBanner'),'sera');assert.equal(env.run('collectionBusy'),true);
 const payload=JSON.parse(env.run('JSON.stringify(data)'));payload.recruitment.stardust=1600;payload.recruitment.pity=0;payload.recruitment.ownedCharacters.push('sera');payload.recruitmentResult={bannerId:'sera',cost:1600,pulls:Array.from({length:10},(_,n)=>({characterId:n===0?'sera':'minato',name:n===0?'세라':'미나토',rarity:n===0?5:4,isPickup:n===0,isNew:n===0,duplicateShards:n===0?0:20,pityAfter:n,guaranteedAfter:false}))};
 release({ok:true,json:async()=>payload});await pending;assert.equal(env.run('collectionBusy'),false);assert.equal(env.run('data.recruitment.stardust'),1600);assert.equal((env.modalHtml.match(/data-recruit-result=/g)||[]).length,10);assert.ok(env.modalHtml.includes('NEW · 새 동료'));assert.ok(env.modalHtml.includes('중복 · 공용 조각 +20'));assert.ok(env.modalHtml.includes('★★★★★'));
 await env.click('recruit-roster');assert.equal(env.run('page'),'roster');assert.ok(env.game.innerHTML.includes('★★★★★ · 보유'));assert.ok(env.game.innerHTML.includes('data-action="party-picker:sera"'));
 pass('one pending ten-pull request blocks repeats, banner changes and Escape, then renders the authoritative rarity, NEW and duplicate-shard results');

 env=environment();recruitmentFixture(env);env.run("page='hub';showRecruitment();globalThis.recruitRequests=[];command=async(action,args)=>{recruitRequests.push({action,...args});lastCommandError='저장하지 못했습니다.';return false;};recruitmentResult={pulls:[{}]};");await env.context.pullRecruitment(1);assert.equal(env.run('recruitmentResult'),null);assert.equal(env.modalHtml,'');assert.equal(env.run('collectionBusy'),false);assert.equal(env.run('data.recruitment.stardust'),3200);
 env.run('data.recruitment.stardust=0;');await env.context.pullRecruitment(1);assert.equal(env.context.recruitRequests.length,1);
 pass('failed or unaffordable recruitment restores input without stale results or optimistic currency deductions');

 env=environment();recruitmentFixture(env);campaignFixture(env);env.run("page='hub';data.progress.lanternFragments=100;showRoster(1);globalThis.denied=[];command=async(action,args)=>{denied.push({action,...args});return true;};");
 assert.ok(env.game.innerHTML.includes('★★★★★ · 미보유'));assert.ok(env.game.innerHTML.includes('data-action="focus-chapter:sera"'));assert.ok(/data-action="synchronize:sera"[^>]*disabled/.test(env.game.innerHTML));assert.ok(/data-action="party-picker:sera"[^>]*disabled/.test(env.game.innerHTML));
 await env.click('synchronize:sera');await env.click('party-picker:sera');await env.click('equip:sera:0');assert.equal(env.context.denied.length,0);assert.equal(env.modalHtml,'');
 env.run("data.campaign.activeStage=data.campaign.chapters[0].stages[0];showRoster(0);");await env.click('party-picker:lian');await env.click('equip:lian:2');assert.equal(env.context.denied.length,0);
 pass('unowned characters keep their story and portrait records but forged sync/equip inputs and active-stage edits never reach the API');

 env=environment();recruitmentFixture(env);env.run("data.recruitment.ownedCharacters.push('sera');page='hub';showRoster(1);");await env.click('party-picker:sera');assert.equal((env.modalHtml.match(/data-action="equip:sera:/g)||[]).length,4);assert.ok(env.modalHtml.includes('이 자리로 교체'));
 let equipRelease;const equipCalls=[];env.context.fetch=async(_url,args)=>{equipCalls.push(JSON.parse(args.body));return new Promise(done=>equipRelease=done);};const equipping=env.click('equip:sera:1');await env.click('equip:sera:2');assert.equal(equipCalls.length,1);assert.deepEqual(equipCalls[0],{action:'equip-character',actorId:'sera',slot:1});
 const equipped=JSON.parse(env.run('JSON.stringify(data)'));equipped.recruitment.party[1]='sera';equipRelease({ok:true,json:async()=>equipped});await equipping;assert.equal(env.run('data.recruitment.party[1]'),'sera');assert.ok(env.game.innerHTML.includes('보유 · 파티 2번'));await env.click('party-picker:sera');assert.ok(env.modalHtml.includes('두 동료의 순서가 교환됩니다.'));assert.ok(/data-action="equip:sera:1"[^>]*disabled/.test(env.modalHtml));env.context.closeModal();campaignFixture(env);env.run("data.campaign.chapters[0].stages[1].unlocked=true;showStages(1,'c1s02');");assert.ok(env.game.innerHTML.includes('출격 리안 · 세라 · 하이델 · 카이'));
 pass('an acquired character selects one of four actual slots, sends its exact ID once, displays the returned party and offers clear swap semantics');

 env=environment();recruitmentFixture(env);env.run("page='battle';settingsReturn='battle';showSettings();globalThis.couponCalls=[];command=async(action,args)=>{couponCalls.push({action,...args});return true;};");env.setCouponCode('WELCOME');await env.context.redeemCoupon();assert.equal(env.context.couponCalls.length,0);assert.ok(/id="coupon-code"[^>]*disabled/.test(env.game.innerHTML));
 env.run("page='hub';settingsReturn='hub';showSettings();");assert.ok(!/id="coupon-code"[^>]*disabled/.test(env.game.innerHTML));env.setCouponCode('');await env.context.redeemCoupon();assert.ok(env.game.innerHTML.includes('쿠폰 코드를 입력해 주세요.'));assert.equal(env.context.couponCalls.length,0);
 env=environment();recruitmentFixture(env);env.run("page='hub';settingsReturn='hub';showSettings();");
 let couponRelease;const couponCalls=[];env.context.fetch=async(_url,args)=>{couponCalls.push(JSON.parse(args.body));return new Promise(done=>couponRelease=done);};env.setCouponCode('  WELCOME  ');const redeeming=env.context.redeemCoupon();await env.click('redeem-code');env.events.keydown({code:'Enter',target:{id:'coupon-code'},preventDefault(){}});assert.equal(couponCalls.length,1);assert.equal(couponCalls[0].code,'WELCOME');
 const rewarded=JSON.parse(env.run('JSON.stringify(data)'));rewarded.recruitment.stardust+=1600;rewarded.couponReward={code:'WELCOME',stardust:1600,coins:200,lanternFragments:10,memoryFragments:0,characterShards:0};couponRelease({ok:true,json:async()=>rewarded});await redeeming;assert.equal(env.run('collectionBusy'),false);assert.ok(env.game.innerHTML.includes('쿠폰 보상을 받았습니다.'));assert.ok(env.game.innerHTML.includes('별가루 +1,600'));assert.equal(env.run('couponDraft'),'');
 pass('coupons are enabled only from keep settings, reject empty input, block duplicate Enter/click requests and render returned rewards');

 env.context.fetch=async()=>({ok:false,json:async()=>({error:'이미 사용한 쿠폰입니다.'})});env.setCouponCode('WELCOME');await env.context.redeemCoupon();assert.equal(env.run('couponReward'),null);assert.ok(env.game.innerHTML.includes('이미 사용한 쿠폰입니다.'));assert.ok(!env.game.innerHTML.includes('별가루 +1,600'));assert.equal(env.run('data.recruitment.stardust'),4800);
 pass('a rejected coupon keeps the server balance and shows its actual error instead of the previous success rewards');
}

async function titleInputChecks(){
 let env=environment();env.run('tone=()=>{};showTitle();');const initialTitle=env.game.innerHTML;
 assert.ok(initialTitle.includes('화면을 클릭해 시작'));assert.ok(initialTitle.includes('CLICK TO START'));assert.ok(!initialTitle.includes('마지막 불씨를 따라서'));assert.ok(initialTitle.includes('REQUIEM<br>LANTERN'));assert.ok(initialTitle.includes('우리가 잃어버린 것들은, 아직 빛나고 있다.'));
 await env.gameEvents.click({target:{closest:()=>null}});assert.equal(env.run('page'),'story');assert.equal(env.run('storyKey'),'prologue');
 env=environment();env.run('tone=()=>{};data.progress.prologueCompleted=true;showTitle();');assert.equal(env.game.innerHTML,initialTitle);await env.click('begin');assert.equal(env.run('page'),'hub');
 pass('title copy is identical for new and existing players, a scene click starts the prologue, and the prompt returns an existing player to the keep');

 env=environment();campaignFixture(env);env.run(`tone=()=>{};showTitle();globalThis.titleCalls=[];command=async(action,args)=>{titleCalls.push({action,...args});await new Promise(done=>globalThis.releaseTitle=done);data.campaign.activeStage=data.campaign.chapters[0].stages[0];data.campaign.phase='BeforeStory';data.campaign.currentStory=[{speaker:'카이',text:'첫 불씨'}];return true;};`);
 const pending=env.gameEvents.click({target:{closest:()=>null}});await env.click('begin');let prevented=0;
 env.events.keydown({code:'Enter',target:{closest:()=>null},preventDefault(){prevented++;}});
 assert.equal(env.context.titleCalls.length,1);assert.equal(env.context.titleCalls[0].stageId,'c1s01');assert.equal(env.run('titleStarting'),true);assert.equal(prevented,1);
 env.context.releaseTitle();await pending;assert.equal(env.run('storyKey'),'stage:c1s01:BeforeStory');assert.equal(env.run('titleStarting'),false);
 pass('scene clicks, the prompt and Enter share one pending start and enter campaign 1-1 exactly once');

 env=environment();env.run('tone=()=>{};showTitle();globalThis.advanced=0;nextStory=()=>advanced++;');
 env.events.keydown({code:'Space',target:{closest:()=>null},preventDefault(){}});assert.equal(env.run('page'),'story');assert.equal(env.run('storyKey'),'prologue');
 env.events.keydown({code:'Space',repeat:true,preventDefault(){}});assert.equal(env.context.advanced,0);
 env.events.keyup({code:'Space'});env.events.keydown({code:'Space',preventDefault(){}});assert.equal(env.context.advanced,1);
 env=environment();env.run('tone=()=>{};data.progress.prologueCompleted=true;showTitle();');env.events.keydown({code:'Enter',target:{closest:()=>null},preventDefault(){}});assert.equal(env.run('page'),'hub');
 pass('Space and Enter start from the title while a held start key cannot advance the first story line');

 env=environment();env.run('tone=()=>{};showTitle();globalThis.unwantedStarts=0;command=async()=>{unwantedStarts++;return true;};');
 const settingsButton={dataset:{action:'go:settings'}};settingsButton.closest=()=>settingsButton;
 env.events.keydown({code:'Enter',target:settingsButton,preventDefault(){throw Error('settings key was intercepted');}});assert.equal(env.run('page'),'title');
 await env.click('go:settings');assert.equal(env.run('page'),'settings');env.events.keydown({code:'Space',preventDefault(){}});await env.click('setting:motion');assert.equal(env.run('page'),'settings');
 env.events.keydown({code:'Escape'});assert.equal(env.run('page'),'title');env.events.keyup({code:'Space'});assert.equal(env.run('page'),'title');
 await env.click('go:settings');env.events.keydown({code:'Space',preventDefault(){}});await env.click('go:title');env.events.keyup({code:'Space'});env.events.keydown({code:'Space',repeat:true,preventDefault(){}});
 assert.equal(env.run('page'),'title');assert.equal(env.run('settings.motion'),false);assert.equal(env.run('data.progress.prologueCompleted'),false);assert.equal(env.context.unwantedStarts,0);
 pass('settings Enter, Space, toggle, Escape and back activation never leak into a title start');
}

async function intentDockChecks(){
 let env=environment();fourParty(env);
 env.run(`data.battle.isBoss=true;data.battle.enemies[0].element='Shadow';data.battle.enemies[0].sanity=20;data.battle.intents=Array.from({length:6},(_,n)=>({id:'3:anger_'+(n%2?'core':'arm')+(n>1?'_'+(Math.floor(n/2)+1):''),actorId:'anger',partId:n%2?'core':'arm',targetId:data.battle.allies[n%4].id,name:'예고 '+(n+1),kind:n===1?'Area':n===3?'Piercing':n===5?'Illusion':'Normal',speed:5+n,basePower:4,flamePower:3,flameCount:2,element:'Shadow'}));showBattle();`);
 const boxes=JSON.parse(env.run('JSON.stringify(data.battle.intents.map(i=>LanternBattleGeometry.intentRect(data.battle,i.id)))'));
 assert.equal((env.game.innerHTML.match(/data-intent-id=/g)||[]).length,6);
 boxes.forEach((box,n)=>{assert.equal(box.y,215);assert.ok(Math.abs(box.width-134/1.2)<1e-8);assert.ok(Math.abs(box.height-124/1.2)<1e-8);assert.ok(box.x>=800&&box.x+box.width<=1880/1.2+1e-8);if(n)assert.ok(box.x>=boxes[n-1].x+boxes[n-1].width+9.99);});
 assert.ok(env.game.innerHTML.includes('표적 · 아군 전체'));assert.ok(env.game.innerHTML.includes('intent-illusion'));
 for(let n=0;n<6;n++){const point=JSON.parse(env.run(`JSON.stringify(targetPoint(data.battle.intents[${n}].id))`));assert.ok(Math.abs(point.x-boxes[n].x-boxes[n].width/2)<1e-8);assert.ok(Math.abs(point.y-boxes[n].y-boxes[n].height/2)<1e-8);}
 pass('six boss forecasts form one bounded horizontal darkness row, and target threads use each exact tile center');

 const intentId=env.run('data.battle.intents[0].id'),attrs={},node={dataset:{intentId},disabled:false,contains:other=>other===node,getBoundingClientRect:()=>({left:1450,top:215,width:112,height:103}),setAttribute:(key,value)=>attrs[key]=value,removeAttribute:key=>delete attrs[key]};node.closest=selector=>selector==='.intent-tile'?node:null;
 env.run(`globalThis.inspectCalls=[];command=async(action,args)=>{inspectCalls.push({action,...args});return true;};selectedActor='sera';selectedSkill='sera_glimmer';selectedCardId='sera-card-1';selectedSlot=0;`);
 const before=env.run('JSON.stringify([selectedActor,selectedSkill,selectedCardId,selectedSlot,data.battle.links])');
 env.gameEvents.pointerover({target:node,relatedTarget:null});
 assert.ok(env.tooltip);assert.ok(env.tooltip.html.includes('4–10'));assert.ok(env.tooltip.html.includes('속성·어둠도 적용 전'));assert.ok(env.tooltip.html.includes('+20 / 90%'));assert.ok(env.tooltip.html.includes('팔 · 30/30'));assert.ok(env.tooltip.html.includes('속성 상성'));assert.ok(env.tooltip.html.includes('특수 효과'));
 assert.equal(attrs['aria-describedby'],'intent-details');assert.equal(env.run('JSON.stringify([selectedActor,selectedSkill,selectedCardId,selectedSlot,data.battle.links])'),before);assert.equal(env.context.inspectCalls.length,0);
 const tipRect={x:parseFloat(env.tooltip.style.left),y:parseFloat(env.tooltip.style.top),width:360,height:420};assert.ok(tipRect.x>=12&&tipRect.x+tipRect.width<=1588);assert.ok(tipRect.y>=108&&tipRect.y+tipRect.height<=844);
 env.gameEvents.pointerout({target:node,relatedTarget:null});assert.equal(env.tooltip,null);assert.equal(attrs['aria-describedby'],undefined);
 env.gameEvents.focusin({target:node});assert.ok(env.tooltip);assert.equal(env.run('JSON.stringify([selectedActor,selectedSkill,selectedCardId,selectedSlot,data.battle.links])'),before);
 env.gameEvents.focusout({target:node,relatedTarget:null});assert.equal(env.tooltip,null);
 env.gameEvents.pointerout({target:{closest:()=>null},relatedTarget:null});env.gameEvents.focusout({target:{closest:()=>null},relatedTarget:null});
 pass('hover and keyboard focus inspect real power, sanity, part, effects and affinity without selecting a card or assigning an action');

 env.run(`data.battle.links=[{actorId:'sera',slot:0,skillId:'sera_glimmer',kind:'Clash',targetId:${JSON.stringify(intentId)}}];data.previews=[{actorId:'sera',slot:0,summary:'기존 상세 판정 유지'}];showBattle();`);
 assert.ok(env.game.innerHTML.includes('대신 맞불 · 세라'));const details=env.run(`intentDetails(intent(${JSON.stringify(intentId)}))`);assert.ok(details.includes('현재 배정'));assert.ok(details.includes('기존 상세 판정 유지'));assert.ok(details.includes('대신 맞불 · 세라'));
 assert.equal(env.run("intentDetails(data.battle.intents[1]).includes('아군 전체')"),true);assert.equal(env.run("intentDetails(data.battle.intents[3]).includes('방어·회피·반격 대행을 무시')"),true);assert.equal(env.run("intentDetails(data.battle.intents[5]).includes('피해를 주지 않는 허상')"),true);
 env.run(`data.battle.intents[5].targetId='all';data.battle.links[0].targetId=data.battle.intents[5].id;data.previews[0].summary='2연승 시 공격';showBattle();`);
 const illusionDetails=env.run('intentDetails(data.battle.intents[5])');assert.ok(!illusionDetails.includes('2연승 시 공격'));assert.ok(!illusionDetails.includes('속성 상성'));assert.ok(illusionDetails.includes('맞불을 연결하면 해당 행동만 소모'));assert.ok(illusionDetails.includes('기본 속도 / 속성'));assert.ok(illusionDetails.includes('아군 전체'));assert.ok(illusionDetails.includes('허상 연결 · 세라'));assert.equal(env.run('intentAssignment(data.battle.intents[5]).changed'),false);
 assert.ok(!env.game.innerHTML.includes('class="thread-intercept"'));
 assert.equal(env.run("targetHint('sera',data.battle.intents[5].id)"),'허상 연결 · 피해 없이 행동 소모');
 env.run('data.battle.gloom=3;');assert.ok(!env.run('intentDetails(data.battle.intents[5])').includes('그림자 위력'));
 assert.ok(env.game.innerHTML.includes('class="intent-darkness-symbol"'));assert.ok(env.game.innerHTML.includes('aria-label="어둠 2개"'));assert.ok(env.game.innerHTML.includes('<b>×2</b>'));
 env.run(`data.battle.intents[0].name='<img src=x>';`);assert.ok(env.run('intentDetails(data.battle.intents[0])').includes('&lt;img src=x&gt;'));
 pass('interception keeps the original recipient and current partner separate, reuses the authoritative summary and escapes detailed names');

 const inspect=()=>env.gameEvents.focusin({target:node});
 inspect();assert.ok(env.tooltip);env.gameEvents.pointerdown({target:{closest:()=>null}});assert.equal(env.tooltip,null);
 inspect();env.run('cleanupDrag()');assert.equal(env.tooltip,null);
 inspect();env.events.resize();assert.equal(env.tooltip,null);
 inspect();env.events.blur();assert.equal(env.tooltip,null);
 inspect();env.run("modal('기록','내용')");assert.equal(env.tooltip,null);env.run('closeModal()');
 env.run(`busy=true;`);inspect();assert.equal(env.tooltip,null);env.run(`busy=false;dragInfo={moved:true};`);inspect();assert.equal(env.tooltip,null);env.run(`dragInfo=null;`);
 env.run(`data.battle.intents[0].cancelled=true;`);inspect();assert.equal(env.tooltip,null);env.run(`data.battle.intents[0].cancelled=false;`);
 inspect();env.run("setScreen('hub','정리')");assert.equal(env.tooltip,null);assert.equal(attrs['aria-describedby'],undefined);
 pass('inspection closes on pointer input, drag cleanup, resize, blur, modal and screen changes and never opens while busy, dragging or cancelled');

 for(const anchor of [{x:0,y:0,width:112},{x:1580,y:890,width:112}]){const p=JSON.parse(env.run(`JSON.stringify(tooltipBounds(${JSON.stringify(anchor)},360,950))`));assert.ok(p.x>=12&&p.x+360<=1588);assert.ok(p.y>=108&&p.y+720<=844);}
 assert.deepEqual(JSON.parse(env.run('JSON.stringify(intentPower({hitPowers:[7,8]}))')),{count:2,base:5,flame:2,max:9});
 pass('edge tooltips stay inside the logical viewport and legacy attack powers use the Core fallback formula');

 env=environment();threeEnemies(env);
 env.run(`for(const enemy of data.battle.enemies)data.battle.intents.push({...data.battle.intents.find(i=>i.actorId===enemy.id),id:'2:'+enemy.id+':extra'});showBattle();`);
 const rows=JSON.parse(env.run('JSON.stringify(data.battle.enemies.map(e=>data.battle.intents.filter(i=>i.actorId===e.id).map(i=>LanternBattleGeometry.intentRect(data.battle,i.id))))'));
 rows.forEach((row,n)=>{assert.equal(row.length,2);assert.ok(row[0].x>=40/1.2);assert.ok(row[1].x+row[1].width<=1880/1.2+1e-8);if(n)assert.ok(row[0].x>rows[n-1][1].x+rows[n-1][1].width);});
 pass('three ordinary enemies retain separate compact owner rows with no adjacent-row overlap or viewport spill');
}

async function main() {
  if(process.argv.includes('--recruitment-only')){await recruitmentChecks();console.log('TOTAL '+passed+' recruitment UI cases passed');return;}
  if(process.argv.includes('--title-only')){await titleInputChecks();console.log('TOTAL '+passed+' title input cases passed');return;}
  if(process.argv.includes('--intent-dock-only')){await intentDockChecks();console.log('TOTAL '+passed+' intent-dock cases passed');return;}
  if(process.argv.includes('--action-status-only')){candidateActionStatusChecks();console.log('TOTAL '+passed+' candidate/action status cases passed');return;}
  let env = environment('{broken');
  assert.equal(env.run('settings.assisted'), true);
  assert.equal(env.run('settings.motion'), true);
  pass('corrupt localStorage falls back to defaults');

  env = environment('{"assisted":false,"motion":"bad"}');
  assert.equal(env.run('settings.assisted'), false);
  assert.equal(env.run('settings.motion'), true);
  pass('settings merge accepts boolean fields only');

  await env.context.command('read');
  assert.equal(env.run('data.previews[0].grade'), 'Advantage');
  assert.equal(env.run('data.previews[0].actorId'), 'lian');
  pass('command response flattens nested previews');

  env = environment();
  await env.context.boot();
  assert.equal(env.run('data.previews[0].summary'), '우세');
  pass('boot response flattens nested previews');

  env = environment();
  env.run("page='battle';showBattle=()=>{};tone=()=>{};command=async(action,args)=>{globalThis.record={action,...args};return true};selectedSkill='lian_breakthrough';selectedCardId='lian-card-2';");
  function pointerDown(actor = 'lian', slot = '0') {
    env.gameEvents.pointerdown({ clientX: 113, clientY: 756, pointerId: 1,
      target: { closest: () => ({ dataset: { actor, slot }, disabled: false }) } });
  }
  function pointerMove(x, y) { env.events.pointermove({ clientX: x, clientY: y, pointerId: 1 }); }
  async function pointerUp(x, y) { await env.events.pointerup({ clientX: x, clientY: y, pointerId: 1 }); }

  pointerDown(); pointerMove(945, 270); env.setHit('intent1'); await pointerUp(945, 270);
  assert.equal(env.context.record.skillId, 'lian_breakthrough');
  assert.equal(env.context.record.targetId, 'intent1');
  pass('same-slot tactical skill survives drag');

  env.run("suppressClickUntil=0;selectedSkill='lian_breakthrough';selectedCardId='lian-card-2'");
  pointerDown(); pointerMove(1392.5, 611.5); env.setHit('anger'); await pointerUp(1392.5, 611.5);
  assert.equal(env.context.record.targetId, 'anger');
  pass('actual body target button wins over nearby core');

  env.context.record = null;
  env.run('suppressClickUntil=0');
  pointerDown(); pointerMove(333.33333333333337, 495.83333333333337); env.setHit(null); await pointerUp(333.33333333333337, 495.83333333333337);
  assert.equal(env.context.record, null);
  pass('attack snapping excludes allied units');

  env.run("suppressClickUntil=0;selectedActor='minato';selectedSlot=0;selectedSkill='minato_heal';selectedCardId='minato-card-2';");
  pointerDown('minato'); pointerMove(333.33333333333337, 495.83333333333337); env.setHit(null); await pointerUp(333.33333333333337, 495.83333333333337);
  assert.equal(env.context.record.targetId, 'lian');
  assert.equal(env.context.record.kind, 'Support');
  assert.equal(env.context.record.skillId, 'minato_heal');
  pass('heal snapping selects live allies and retains heal skill');

  env.context.record = null;
  env.run('suppressClickUntil=0');
  pointerDown('minato'); pointerMove(1392.5, 611.5); env.setHit('anger'); await pointerUp(1392.5, 611.5);
  assert.equal(env.context.record, null);
  pass('heal rejects explicit enemy target');

  env.run('suppressClickUntil=0');
  pointerDown('minato'); pointerMove(800, 400);
  assert.ok(env.thread.d);
  env.events.pointercancel({ pointerId: 1 });
  assert.equal(env.run('dragInfo'), null);
  assert.equal(env.thread.d, '');
  pass('pointercancel clears drag state and line');

  env = environment();
  env.run("page='story';storyAuto=true;typed=true;storyLog=['대사'];scheduleStoryAuto()");
  assert.equal(env.timers.size, 1);
  await env.click('storylog');
  assert.equal(env.timers.size, 0);
  await env.click('close');
  assert.equal(env.timers.size, 1);
  pass('story log pauses and resumes automatic advance');

  env.context.modal('pause', '');
  env.run('typed=false;scheduleStoryAuto()');
  env.run('typed=true;scheduleStoryAuto()');
  assert.equal(env.timers.size, 0);
  env.context.closeModal();
  assert.equal(env.timers.size, 1);
  pass('typing completion cannot advance behind another modal');

  env = environment();
  env.run("page='battle';autoBattle=true;autoBattleWaiting=true;scheduleBattleAuto()");
  assert.equal(env.timers.size, 1);
  env.context.modal('retreat', '');
  assert.equal(env.timers.size, 0);
  await env.context.resolveRound();
  assert.equal(env.run('busy'), false);
  env.run('resolveRound=()=>{globalThis.resolveCalls=(globalThis.resolveCalls||0)+1}');
  env.context.closeModal();
  assert.equal(env.timers.size, 1);
  env.flush();
  assert.equal(env.context.resolveCalls, 1);
  pass('battle modal pauses pending auto round and close resumes once');

  env = environment();
  env.run("page='battle';autoBattle=true;autoBattleWaiting=true;scheduleBattleAuto()");
  env.context.modal('log', '');
  env.run('autoBattle=false');
  env.context.closeModal();
  assert.equal(env.timers.size, 0);
  pass('disabled auto battle does not restart on modal close');

  env = environment();
  env.run("page='title';go('settings')");
  assert.equal(env.run('settingsReturn'), 'title');
  assert.ok(env.game.innerHTML.includes('data-action="go:title"'));
  await env.click('setting:motion');
  assert.equal(env.run('settingsReturn'), 'title');
  await env.click('go:title');
  assert.equal(env.run('page'), 'title');
  assert.equal(env.run('data.progress.prologueCompleted'), false);
  pass('title settings toggle returns to title without bypassing prologue');

  env = environment();
  env.context.setScreen('hub', '');
  assert.ok(env.game.style.backgroundImage.includes('Keep.png'));
  env.context.setScreen('battle', '');
  assert.ok(env.game.style.backgroundImage.includes('Background.png'));
  env.context.startStory('firstMeal');
  assert.ok(env.game.style.backgroundImage.includes('Keep.png'));
  pass('keep backdrop applies to hub and keep story, battlefield uses ruins');

  env = environment();
  env.run(`
    page='battle';
    data.battle.links=[{actorId:'lian',slot:0,skillId:'lian_glimmer',kind:'Strike',targetId:'anger'}];
    globalThis.fxCommands=[];
    command=async(action,args)=>{
      fxCommands.push({action,args});
      if(action==='resolve'){
        data.battle={...data.battle,enemies:data.battle.enemies.map(u=>({...u,hp:70})),events:[{type:'Damage',actorId:'lian',targetId:'anger',amount:30}]};
      }
      return true;
    };
    battleFx.play=async(events,finalBattle)=>{
      globalThis.playbackArgs={preHp:battleFx.units.get('anger').hp,finalHp:finalBattle.enemies[0].hp,eventType:events[0].type};
      return new Promise(done=>globalThis.releasePlayback=done);
    };
  `);
  const pendingPlayback = env.context.resolveRound();
  await Promise.resolve(); await Promise.resolve();
  assert.equal(env.context.playbackArgs.preHp, 100);
  assert.equal(env.context.playbackArgs.finalHp, 70);
  assert.equal(env.context.playbackArgs.eventType, 'Damage');
  assert.equal(env.context.fxCommands[0].args.grades['lian:0'], 'Good');
  await env.context.resolveRound();
  assert.equal(env.context.fxCommands.length, 1);
  env.run("page='hub';viewToken++"); env.context.releasePlayback(false); await pendingPlayback;
  assert.equal(env.run('busy'), false); assert.equal(env.run('page'), 'hub');
  assert.equal(env.context.fxCommands.length, 1);
  pass('resolve preserves pre-round FX HP, prevents duplicate execution and stops after playback cancellation');

  env = environment();
  env.run(`
    page='battle';globalThis.fxCommands=[];globalThis.resultCalls=0;
    command=async action=>{
      fxCommands.push(action);
      if(action==='resolve'){data.battle.outcome='Victory';data.battle.events=[{type:'BattleEnd'}];}
      return true;
    };
    battleFx.play=()=>new Promise(done=>globalThis.releasePlayback=done);
    showResult=()=>{resultCalls++;};
  `);
  const victoryPlayback = env.context.resolveRound();
  await Promise.resolve(); await Promise.resolve();
  assert.equal(env.context.fxCommands.join(','), 'resolve'); assert.equal(env.context.resultCalls, 0);
  env.context.releasePlayback(true); await victoryPlayback;
  assert.equal(env.context.fxCommands.join(','), 'resolve,reward'); assert.equal(env.context.resultCalls, 1);
  assert.equal(env.run('busy'), false);
  pass('victory rewards and results wait for the complete authoritative event playback');

  env = environment();
  env.run(`
    page='battle';globalThis.fxCommands=[];globalThis.battleDraws=0;
    command=async action=>{fxCommands.push(action);return true;};
    battleFx.play=async()=>{throw Error('presentation fixture failure');};
    showBattle=()=>{battleDraws++;page='battle';};
  `);
  await env.context.resolveRound();
  assert.equal(env.context.fxCommands.join(','), 'resolve'); assert.equal(env.context.battleDraws, 1);
  assert.equal(env.run('busy'), false); assert.equal(env.run('autoBattleWaiting'), false);
  pass('presentation failure unlocks the battlefield without advancing or awarding the battle');

  env=environment();campaignFixture(env);
  env.run(`data.characterStories=[{id:'noa',name:'노아',title:'별도 결말',lines:[{text:'독립 스토리 대사'}]}];data.story['character:noa']=[{text:'숨겨진 독립 스토리'}];data.campaign.chapters[0].stages[0].cleared=true;globalThis.storyCommands=[];command=async action=>{storyCommands.push(action);return true;};tone=()=>{};showArchive();`);
  const mainArchive=env.game.innerHTML;
  await env.click('go:characterStories');await env.click('read-character:noa');env.context.startStory('character:noa');
  assert.equal(env.game.innerHTML,mainArchive);assert.equal(env.run('page'),'archive');
  assert.equal(env.run('typeof showCharacterStories'),'undefined');assert.deepEqual(Array.from(env.context.storyCommands),[]);
  assert.ok(env.game.innerHTML.includes('stage-replay:c1s01'));assert.ok(!env.game.innerHTML.includes('독립 스토리'));assert.ok(!source.includes('characterStories'));assert.ok(!source.includes('read-character:'));
  pass('standalone character story routes and payload are retired; the archive exposes only completed main-stage replay');


  env = environment();
  const chapters = campaignFixture(env);
  env.context.showStages();
  assert.equal((env.game.innerHTML.match(/data-action="chapter:/g) || []).length,1);
  assert.equal((env.game.innerHTML.match(/data-action="stage-select:/g) || []).length,1);
  assert.ok(env.game.innerHTML.includes(chapters[0].stages[0].title));
  assert.ok(!env.game.innerHTML.includes(chapters[0].stages[3].title));assert.ok(!env.game.innerHTML.includes('CHAPTER 02'));
  env.context.showStages(3,chapters[2].stages[8].id);
  assert.ok(!env.game.innerHTML.includes('침묵의 기록관'));assert.ok(!env.game.innerHTML.includes('이브'));
  env.run(`for(const stage of data.campaign.chapters[0].stages.slice(0,3)){stage.cleared=true;stage.unlocked=true;}data.campaign.chapters[0].stages[3].unlocked=true;showStages();`);
  assert.equal((env.game.innerHTML.match(/data-action="stage-select:/g)||[]).length,4);
  assert.ok(env.game.innerHTML.includes(chapters[0].stages[3].title));assert.ok(!env.game.innerHTML.includes('外壁'));assert.ok(!env.game.innerHTML.includes(chapters[0].stages[4].title));
  env.context.showArchive();assert.ok(env.game.innerHTML.includes('제1장'));assert.ok(!env.game.innerHTML.includes('제2장'));assert.ok(!env.game.innerHTML.includes('아직 기록된 이야기가 없습니다'));
  pass('map shows completed and next stages while future chapters, scenes and archive placeholders stay hidden');

  env = environment();campaignFixture(env);
  env.run(`tone=()=>{};globalThis.stageCalls=[];command=async(action,args)=>{stageCalls.push({action,...args});data.campaign.activeStage=data.campaign.chapters[0].stages[0];data.campaign.phase='BeforeStory';data.campaign.currentStory=[{speaker:'카이',text:'꺼지지 않은 불',portrait:'Kai',listenerPortrait:'Lian',caption:'1-1 · 첫 불씨'}];return true;};`);
  await env.click('begin');
  assert.equal(env.context.stageCalls[0].action,'stage-enter');assert.equal(env.context.stageCalls[0].stageId,'c1s01');
  assert.equal(env.run('storyKey'),'stage:c1s01:BeforeStory');
  assert.ok(env.game.innerHTML.includes('story-listener'));assert.ok(env.game.innerHTML.includes('1-1 · 첫 불씨'));
  pass('first start enters campaign 1-1 and dialogue displays the listening companion');

  env = environment();campaignFixture(env);
  env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[1];data.campaign.phase='BeforeStory';data.campaign.currentStory=[{speaker:'리안',text:'뒤로 물러서.',portrait:'Lian'}];routeCampaign();globalThis.finishCalls=[];command=(action,args)=>{finishCalls.push({action,...args});return new Promise(done=>globalThis.releaseStory=()=>{data.campaign.phase='Battle';done(true);});};`);
  const finish = env.context.finishStory();
  await env.context.finishStory();
  assert.equal(env.context.finishCalls.length,1);assert.equal(env.context.finishCalls[0].stageId,'c1s02');
  env.context.releaseStory();await finish;
  assert.equal(env.run('page'),'battle');assert.equal(env.context.finishCalls[0].action,'stage-story');
  pass('double skip sends stage-story once and mixed stage opens its battle after the response');

  env = environment();campaignFixture(env);
  env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[0];data.campaign.phase='BeforeStory';data.campaign.currentStory=[{text:'첫 이야기'}];routeCampaign();command=()=>new Promise(done=>globalThis.releaseStory=()=>{data.campaign.phase='Complete';done(true);});`);
  const staleFinish=env.context.finishStory();
  env.context.showHub();env.context.releaseStory();await staleFinish;
  assert.equal(env.run('page'),'hub');
  pass('pending story completion never navigates over a newer screen');

  env = environment();campaignFixture(env);
  env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[0];data.campaign.phase='BeforeStory';data.campaign.currentStory=[{text:'첫 이야기'}];routeCampaign();globalThis.stageCalls=[];command=async(action,args)=>{stageCalls.push({action,...args});data.campaign.chapters[0].stages[0].cleared=true;data.campaign.chapters[0].stages[1].unlocked=true;data.campaign.phase='Complete';return true;};`);
  await env.context.finishStory();
  assert.equal(env.run('page'),'stages');assert.equal(env.run('selectedStage'),'c1s02');
  assert.deepEqual(Array.from(env.context.stageCalls).map(c=>c.action),['stage-story']);
  pass('story-only completion selects the newly unlocked stage without requesting battle rewards');

  env = environment();campaignFixture(env);
  env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[6];data.campaign.activeStage.cleared=true;data.campaign.activeStage.unlocked=true;data.campaign.replay=true;data.campaign.phase='BeforeStory';data.campaign.currentStory=[{text:'함께 먹은 저녁'}];routeCampaign();globalThis.stageCalls=[];command=async(action,args)=>{stageCalls.push({action,...args});data.campaign.phase='Complete';return true;};`);
  await env.context.finishStory();
  assert.equal(env.run('page'),'stages');assert.equal(env.run('selectedStage'),'c1s07');
  assert.deepEqual(Array.from(env.context.stageCalls).map(c=>c.action),['stage-story']);
  env.context.showArchive();assert.ok(!env.game.innerHTML.includes('go:characterStories'));
  pass('stage replay returns to its chapter without rewards and archive uses main campaign records');

  env = environment();campaignFixture(env);
  env.run(`data.battle.outcome='Victory';data.campaign.activeStage=data.campaign.chapters[0].stages[1];data.campaign.phase='AfterStory';showResult();`);
  assert.ok(env.game.innerHTML.includes('data-action="campaign-story"'));assert.ok(!env.game.innerHTML.includes('story:epilogue'));
  pass('mixed stage result continues into its own after-story');

  env = environment();
  env.run(`data.battle.allies[0].syncLevel=0;data.battle.allies[0].slotCount=1;data.battle.allies[0].sanity=-20;data.battle.allies[0].skills.push({id:'lian_third',name:'세 번째 불',number:3,requiredSyncLevel:1,flameCount:3,basePower:5,flamePower:2,cost:1,slots:1,kind:'Attack'});showBattle();`);
  assert.ok(env.game.innerHTML.includes('정신력 -20'));assert.ok(env.game.innerHTML.includes('점등 10%'));
  assert.ok(!env.game.innerHTML.includes('세 번째 불'));assert.equal((env.game.innerHTML.match(/data-action="skill-card:lian:/g)||[]).length,2);
  assert.equal((env.game.innerHTML.match(/data-action="slot:lian:/g)||[]).length,2);assert.match(env.game.innerHTML,/data-action="slot:lian:1"[^>]*disabled/);
  assert.ok(env.game.innerHTML.includes('카드 구성 1×3 / 2×2'));
  env.run(`data.battle.allies[0].syncLevel=1;data.battle.allies[0].sanity=20;data.battle.allies[0].skillHand[1]={id:'lian-card-3',skillId:'lian_third'};showBattle();`);
  assert.ok(env.game.innerHTML.includes('3스킬</b>')&&env.game.innerHTML.includes('세 번째 불'));assert.ok(env.game.innerHTML.includes('/ 3×1'));assert.ok(env.game.innerHTML.includes('점등 90%'));
  pass('battle offers exactly two actual cards plus defense, with synchronized third skills appearing only when drawn');

  env = environment();
  env.run(`data.battle.allies[0].skills.push({id:'lian_evade',name:'회피',kind:'Evade',cost:0,slots:1});selectedSkill='lian_evade';selectedCardId='';globalThis.defenseCall=null;command=async(action,args)=>{defenseCall={action,...args};return true;};showBattle=()=>{};`);
  await env.context.linkTarget('lian');
  assert.equal(env.context.defenseCall.kind,'Guard');
  assert.deepEqual(Array.from(env.context.validDropTargets(env.run("skill('lian','lian_evade')"))),['lian','minato']);
  pass('evasion uses defense links and supports only living allies');


  env=environment();campaignFixture(env);
  env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[1];data.campaign.phase='Battle';page='battle';globalThis.exitCalls=[];command=(action,args)=>{exitCalls.push({action,...args});return new Promise(done=>globalThis.releaseExit=()=>{data.campaign.activeStage=null;data.campaign.phase='None';done(true);});};tone=()=>{};`);
  const retreat=env.context.go('hub');
  await env.context.go('archive');await env.click('character:0');
  assert.equal(env.context.exitCalls.length,1);assert.equal(env.run('page'),'battle');
  assert.equal(env.context.exitCalls[0].action,'stage-exit');assert.equal(env.context.exitCalls[0].stageId,'c1s02');
  env.context.releaseExit();await retreat;
  assert.equal(env.run('page'),'hub');assert.equal(env.run('data.campaign.activeStage'),null);
  pass('retreat exits the API campaign session once and blocks competing navigation while pending');

  env=environment();campaignFixture(env);
  env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[1];data.campaign.phase='AfterStory';data.battle.outcome='Victory';showResult();globalThis.exitCalls=[];command=async(action,args)=>{exitCalls.push({action,...args});if(action==='stage-exit'){data.campaign.activeStage=null;data.campaign.phase='None';}else if(action==='stage-enter'){data.campaign.activeStage=data.campaign.chapters[0].stages[1];data.campaign.phase='AfterStory';data.campaign.currentStory=[{text:'전투 뒤의 약속'}];}return true;};`);
  await env.context.go('hub');await env.context.enterCampaign('c1s02');
  assert.equal(env.run('page'),'story');assert.equal(env.run('storyKey'),'stage:c1s02:AfterStory');
  assert.deepEqual(Array.from(env.context.exitCalls).map(c=>c.action),['stage-exit','stage-enter']);
  pass('leaving a won mixed stage permits saved after-story resume without restart or reward requests');

  env=environment();campaignFixture(env);
  env.run(`data.campaign.activeStage=data.campaign.chapters[0].stages[1];data.campaign.phase='BeforeStory';data.campaign.currentStory=[{text:'출격 전'}];routeCampaign();globalThis.exitCalls=[];command=async(action,args)=>{exitCalls.push({action,...args});data.campaign.activeStage=null;return true;};`);
  env.events.keydown({code:'Escape'});await Promise.resolve();await Promise.resolve();
  assert.equal(env.run('page'),'hub');assert.equal(env.context.exitCalls[0].action,'stage-exit');
  pass('Escape from a stage story exits its API session before returning to the keep');


  env=environment();campaignFixture(env);
  env.run(`data.progress.prologueCompleted=true;data.campaign.activeStage=data.campaign.chapters[0].stages[3];data.campaign.phase='Battle';page='title';tone=()=>{};globalThis.exitCalls=[];command=async(action,args)=>{exitCalls.push({action,...args});data.campaign.activeStage=null;return true;};`);
  await env.click('begin');assert.equal(env.run('page'),'hub');assert.equal(env.context.exitCalls[0].action,'stage-exit');
  pass('returning from reloaded title also clears an active server battle before opening the keep');


  env=environment();
  lateRound(env);
  env.run(`data.battle.allies[0].skillHand=[{id:'duplicate-one',skillId:'lian_glimmer'},{id:'duplicate-two',skillId:'lian_glimmer'}];selectFirstFighter();showBattle();tone=()=>{};globalThis.cardCalls=[];command=async(action,args)=>{cardCalls.push({action,...args});return true;};`);
  assert.equal((env.game.innerHTML.match(/data-action="skill-card:lian:/g)||[]).length,2);
  await env.click('skill-card:lian:duplicate-two');await env.context.linkTarget('intent1');
  assert.equal(env.context.cardCalls[0].cardId,'duplicate-two');assert.equal(env.context.cardCalls[0].skillId,'lian_glimmer');
  env.run(`data.battle.links=[{actorId:'lian',slot:0,skillId:'lian_glimmer',cardId:'duplicate-two',kind:'Clash',targetId:'intent1'}];selectedSlot=1;selectedCardId='';selectedSkill='lian_glimmer';showBattle();`);
  assert.equal(env.run('selectedCardId'),'duplicate-one');
  assert.equal(env.run("dockChoice('lian','duplicate-two').slot"),0);assert.equal(env.run("dockChoice('lian','duplicate-two').disabled"),false);await env.click('skill-card:lian:duplicate-two');assert.equal(env.run('selectedSlot'),0);assert.equal(env.run('selectedCardId'),'duplicate-two');
  await env.click('slot:lian:0');assert.equal(env.run('selectedCardId'),'duplicate-two');
  pass('identical offered skills retain distinct card identities and reserved-card clicks edit their own action');

  env=environment();
  env.run(`page='battle';selectedActor='lian';selectedSlot=0;selectedSkill='lian_glimmer';selectedCardId='lian-card-1';data.battle.links=[{actorId:'lian',slot:0,skillId:'lian_glimmer',cardId:'lian-card-1',kind:'Strike',targetId:'anger'}];tone=()=>{};command=async(action,args)=>{globalThis.dragCardCall={action,...args};return true;};`);
  await env.click('slot:lian:0');
  await env.click('skill-card:lian:lian-card-2');
  env.gameEvents.pointerdown({clientX:113,clientY:756,pointerId:2,target:{closest:()=>({dataset:{actor:'lian',slot:'0'},disabled:false})}});
  env.events.pointermove({clientX:945,clientY:270,pointerId:2});env.setHit('intent1');await env.events.pointerup({clientX:945,clientY:270,pointerId:2});
  assert.equal(env.context.dragCardCall.cardId,'lian-card-2');assert.equal(env.context.dragCardCall.skillId,'lian_breakthrough');
  pass('same-slot drag uses a newly selected offered card when replacing an existing connection');

  env=environment();
  env.run(`tone=()=>{};selectedCardId='lian-card-2';globalThis.guardCardCall=null;command=async(action,args)=>{guardCardCall={action,...args};return true;};`);
  await env.click('defense:lian:lian_guard');await env.context.linkTarget('lian');
  assert.equal(env.context.guardCardCall.cardId,'');assert.equal(env.context.guardCardCall.kind,'Guard');
  assert.equal(env.run('selectedCardId'),'');
  pass('defense remains available outside the card bag and sends no attack card identity');

  env=environment();
  lateRound(env);
  env.run(`showBattle();tone=()=>{};globalThis.directCalls=[];command=async(action,args)=>{directCalls.push({action,...args});if(action==='link'){data.battle.links=data.battle.links.filter(l=>l.actorId!==args.actorId||l.slot!==args.slot);data.battle.links.push(args);}return true;};`);
  assert.equal((env.game.innerHTML.match(/data-action="skill-card:/g)||[]).length,4);
  assert.ok(env.game.innerHTML.includes('class="dock-portrait"'));assert.ok(env.game.innerHTML.includes('class="command-rail"'));assert.ok(!env.game.innerHTML.includes('class="skill-panel"'));
  await env.click('skill-card:lian:lian-card-1');await env.click('target:intent1');
  await env.click('skill-card:lian:lian-card-2');await env.click('target:arm');
  assert.deepEqual(Array.from(env.context.directCalls).map(c=>[c.actorId,c.slot,c.cardId]),[['lian',0,'lian-card-1'],['lian',1,'lian-card-2']]);
  assert.equal(env.run('data.battle.links.length'),2);assert.ok(env.game.innerHTML.includes('2개 행동으로 전투 시작'));
  assert.equal(env.run('manualSlotFocus'),false);
  pass('all fighters offer both cards and successive direct-card links fill two actions without replacing the first');

  const colonEnv=environment();
  colonEnv.run(`data.battle.allies[0].skillHand[0].id='lian:card:11';tone=()=>{};command=async(action,args)=>{globalThis.colonCardCall={action,...args};return true;};showBattle();`);
  await colonEnv.click('skill-card:lian:lian:card:11');await colonEnv.click('target:intent1');
  assert.equal(colonEnv.run('selectedCardId'),'lian:card:11');assert.equal(colonEnv.context.colonCardCall.cardId,'lian:card:11');
  pass('actor-aware card routing preserves the colon-separated card IDs generated by the actual core');

  await env.click('skill-card:lian:lian-card-1');assert.equal(env.run('selectedSlot'),0);
  await env.click('target:core');assert.equal(env.run('data.battle.links.length'),2);assert.equal(env.run("slotLink('lian',0).targetId"),'core');assert.equal(env.run("slotLink('lian',1).targetId"),'arm');
  await env.click('skill-card:minato:minato-card-2');assert.equal(env.run('selectedActor'),'minato');assert.equal(env.run('selectedSlot'),0);
  await env.click('target:lian');assert.equal(env.context.directCalls.at(-1).kind,'Support');assert.equal(env.context.directCalls.at(-1).cardId,'minato-card-2');
  pass('reserved cards return to their own action and another fighter’s healing card keeps its card identity');

  env=environment();
  env.run(`page='battle';tone=()=>{};command=async(action,args)=>{globalThis.directDragCall={action,...args};return true;};`);
  env.gameEvents.pointerdown({clientX:646,clientY:704,pointerId:8,target:{closest:()=>({dataset:{actor:'minato',cardId:'minato-card-2'},disabled:false})}});
  assert.equal(env.run('dragInfo.actor'),'minato');assert.equal(env.run('dragInfo.cardId'),'minato-card-2');
  env.events.pointermove({clientX:333.33333333333337,clientY:495.83333333333337,pointerId:8});env.setHit('lian');await env.events.pointerup({clientX:333.33333333333337,clientY:495.83333333333337,pointerId:8});
  assert.equal(env.context.directDragCall.actorId,'minato');assert.equal(env.context.directDragCall.slot,0);assert.equal(env.context.directDragCall.cardId,'minato-card-2');assert.equal(env.context.directDragCall.kind,'Support');
  const directDragCall=env.context.directDragCall;await env.click('skill-card:lian:lian-card-1');assert.equal(env.context.directDragCall,directDragCall);assert.equal(env.run('selectedActor'),'minato');
  pass('direct card drag selects its owner, validates ally healing, and suppresses the release click');

  env.run('suppressClickUntil=0');env.context.directDragCall=null;
  env.gameEvents.pointerdown({clientX:646,clientY:704,pointerId:9,target:{closest:()=>({dataset:{actor:'minato',cardId:'minato-card-2'},disabled:false})}});
  env.events.pointermove({clientX:1330,clientY:550,pointerId:9});env.setHit('anger');await env.events.pointerup({clientX:1330,clientY:550,pointerId:9});assert.equal(env.context.directDragCall,null);
  env.run('suppressClickUntil=0');env.gameEvents.pointerdown({clientX:110,clientY:704,pointerId:10,target:{closest:()=>({dataset:{actor:'lian',cardId:'lian-card-1'},disabled:false})}});
  env.events.pointermove({clientX:900,clientY:350,pointerId:10});assert.ok(env.thread.d);env.events.pointercancel({pointerId:10});assert.equal(env.run('dragInfo'),null);assert.equal(env.thread.d,'');assert.equal(env.context.directDragCall,null);
  pass('direct card drag rejects an enemy for healing and pointer cancellation commits no action');

  env=environment();
  env.run(`page='battle';tone=()=>{};globalThis.modalDragCalls=[];command=async(action,args)=>{modalDragCalls.push({action,...args});return true;};`);
  env.gameEvents.pointerdown({clientX:110,clientY:704,pointerId:11,target:{closest:()=>({dataset:{actor:'lian',cardId:'lian-card-1'},disabled:false})}});
  env.events.pointermove({clientX:945,clientY:270,pointerId:11});env.events.keydown({code:'Escape'});
  assert.equal(env.run('dragInfo'),null);assert.equal(env.thread.d,'');assert.ok(env.game.querySelector('.modal'));
  env.setHit('intent1');await env.events.pointerup({clientX:945,clientY:270,pointerId:11});await env.context.linkTarget('intent1');
  env.gameEvents.pointerdown({clientX:110,clientY:704,pointerId:12,target:{closest:()=>({dataset:{actor:'lian',cardId:'lian-card-1'},disabled:false})}});
  assert.equal(env.run('dragInfo'),null);assert.equal(env.context.modalDragCalls.length,0);assert.ok(env.game.querySelector('.modal'));
  pass('a modal cancels card drag and blocks underlying target links without dismissing the modal');

  env=environment();
  lateRound(env);
  env.run(`data.battle.allies[0].skills.push({id:'lian_third',name:'시험용 두 행동 공격',number:2,requiredSyncLevel:1,flameCount:3,basePower:5,flamePower:2,cost:2,slots:2,kind:'Attack'});data.battle.allies[0].skillHand[1]={id:'lian-third-card',skillId:'lian_third'};selectedSlot=1;manualSlotFocus=true;showBattle();`);
  assert.equal(env.run("dockChoice('lian','lian-third-card').disabled"),true);
  assert.equal(env.run("selectDockChoice('lian','lian-third-card')"),false);
  env.run('data.battle.allies[0].syncLevel=1');assert.equal(env.run("selectDockChoice('lian','lian-third-card')"),true);assert.equal(env.run('selectedSlot'),0);
  env.run(`data.battle.allies[1].skills.push({id:'minato_care',name:'포기하지 않는 손',number:3,requiredSyncLevel:1,flameCount:3,basePower:5,flamePower:2,cost:2,slots:1,kind:'Heal'});data.battle.allies[1].syncLevel=1;data.battle.allies[1].skillHand[1]={id:'minato-care-card',skillId:'minato_care'};data.battle.links=[{actorId:'minato',slot:0,cardId:'minato-card-1',skillId:'minato_glimmer',targetId:'arm',kind:'Strike'}];`);
  assert.equal(env.run("selectDockChoice('minato','minato-care-card')"),true);assert.equal(env.run('selectedSlot'),1);assert.equal(env.run('selectedCardId'),'minato-care-card');
  pass('synchronization gates cards, a custom two-action attack uses action zero, and the one-action third heal uses an empty action');

  env=environment();
  env.run(`data.battle.allies[0].skills[0].cost=2;data.battle.allies[0].skills[1].cost=3;data.availableEmber=0;data.battle.links=[{actorId:'lian',slot:0,skillId:'lian_glimmer',cardId:'lian-card-1',targetId:'arm',kind:'Strike'}];selectedSlot=0;manualSlotFocus=true;`);
  assert.equal(env.run("dockChoice('lian','lian-card-1').disabled"),false);assert.equal(env.run("dockChoice('lian','lian-card-2').reason"),'불씨 부족');
  assert.equal(env.run("selectDockChoice('lian','lian-card-2')"),false);
  env.run('data.availableEmber=1');assert.equal(env.run("selectDockChoice('lian','lian-card-2')"),true);assert.equal(env.run('selectedSlot'),0);
  env.run('data.battle.allies[1].hp=0');assert.equal(env.run("selectDockChoice('minato','minato-card-1')"),false);
  pass('direct-card availability includes refunded replacement cost, rejects insufficient ember, and prevents dead actors acting');


  env=environment();campaignFixture(env);
  env.run(`data.campaign.chapters[2].stages[0].unlocked=true;globalThis.focusCommands=[];command=async action=>{focusCommands.push(action);return true;};tone=()=>{};showRoster(4);`);
  assert.ok(env.game.innerHTML.includes('focus-chapter:noa'));assert.ok(env.game.innerHTML.includes('메인 스토리 · 노아의 장 보기'));assert.ok(!env.game.innerHTML.includes('read-character:'));
  await env.click('focus-chapter:noa');
  assert.equal(env.run('page'),'stages');assert.equal(env.run('selectedChapter'),3);assert.equal(env.run('selectedStage'),env.run('data.campaign.chapters[2].stages[0].id'));
  assert.ok(env.game.innerHTML.includes('노아의 장'));assert.deepEqual(Array.from(env.context.focusCommands),[]);
  pass('an accessible character-story button opens that character’s main chapter without starting a separate story or granting rewards');

  env=environment();campaignFixture(env);
  env.run(`globalThis.focusCommands=[];command=async action=>{focusCommands.push(action);return true;};toast=message=>{globalThis.focusNotice=message;};tone=()=>{};showRoster(4);`);
  assert.ok(env.game.innerHTML.includes('앞선 스테이지 완료 후 개방'));
  await env.click('focus-chapter:noa');
  assert.equal(env.run('page'),'stages');assert.equal(env.run('selectedChapter'),1);
  assert.ok(env.context.focusNotice.includes('노아의 장(제3장)'));assert.ok(env.context.focusNotice.includes('앞선 스테이지'));
  assert.ok(env.game.innerHTML.includes('리안의 장'));assert.ok(!env.game.innerHTML.includes('CHAPTER 03'));assert.deepEqual(Array.from(env.context.focusCommands),[]);
  pass('a locked character chapter explains its prerequisite and shows the real current frontier without bypassing progression');

  env=environment();campaignFixture(env);
  env.run(`data.campaign.chapters[0].stages[0].cleared=true;showArchive();`);
  assert.ok(env.game.innerHTML.includes('제1장 · 리안의 장'));assert.ok(!env.game.innerHTML.includes('캐릭터 이야기'));
  env.context.showRoster(5);assert.ok(!env.game.innerHTML.includes('focus-chapter:kai'));
  pass('main replay records name their focus character and characters without a focus chapter receive no invented story entry');

  env=environment();campaignFixture(env);
  env.run(`data.campaign.chapters[0].stages.forEach(s=>{s.cleared=true;s.unlocked=true;});data.campaign.chapters[1].stages[0].unlocked=true;showHub();`);
  assert.ok(env.game.innerHTML.includes('<h1>세라의 장</h1>'));assert.ok(env.game.innerHTML.includes(env.run('data.campaign.chapters[1].focusStorySummary')));
  env.context.showStages(2,'');
  assert.ok(env.game.innerHTML.includes('세라의 장'));assert.ok(!env.game.innerHTML.includes('<strong>세라 · '));
  pass('the keep follows the current main-story frontier and chapter titles avoid repeating their focus character');

  env=environment();fourParty(env);
  assert.equal((env.game.innerHTML.match(/data-action="skill-card:/g)||[]).length,8);
  assert.equal((env.game.innerHTML.match(/class="field-hero sd-unit/g)||[]).length,4);
  assert.equal((env.game.innerHTML.match(/class="dock-portrait"/g)||[]).length,4);
  assert.ok(env.game.innerHTML.includes('/combat/NoaSDActions.png'));
  assert.ok(env.game.innerHTML.includes('/art/Noa.png'));
  pass('four allies show eight direct skill cards, four SD bodies and original portrait thumbnails');

  lateRound(env);
  env.run(`tone=()=>{};command=async(action,args)=>{if(action==='link')data.battle.links.push(args);return true;};`);
  await env.click('skill-card:noa:noa-card-1');await env.click('target:intent1');
  await env.click('skill-card:noa:noa-card-2');await env.click('target:arm');
  assert.equal(env.run('selectedActor'),'noa');assert.equal(env.run('data.battle.links.length'),2);
  assert.equal(env.run('data.battle.links[0].slot'),0);assert.equal(env.run('data.battle.links[1].slot'),1);
  pass('the fourth fighter fills both free actions using distinct offered cards');

  env=environment();fourParty(env);env.run(`tone=()=>{};command=async(action,args)=>{globalThis.fourDefense={action,...args};return true;};`);
  await env.click('defense:noa:noa_guard');await env.click('target:sera');
  assert.equal(env.context.fourDefense.actorId,'noa');assert.equal(env.context.fourDefense.targetId,'sera');assert.equal(env.context.fourDefense.cardId,'');
  pass('fourth-fighter defense selects its owner and supports another living ally without spending an attack card');

  env=environment();fourParty(env);
  env.setGeometry({left:50,top:20,width:800,height:450},[{dataset:{actor:'noa',slot:'1'},getBoundingClientRect:()=>({left:620,top:411,width:73,height:12})}]);
  assert.deepEqual(JSON.parse(JSON.stringify(env.run("slotPoint('noa',1)"))),{x:1213,y:794});
  assert.deepEqual(JSON.parse(JSON.stringify(env.run('logicalElementCenter(null)'))),null);
  pass('slot anchors use measured DOM centers converted through the actual viewport scale');

  let renderedLine='';const measuredLink={dataset:{linkActor:'noa',linkSlot:'1',linkTarget:'arm'},setAttribute:(_key,value)=>{renderedLine=value;}};
  env.setGeometry({left:50,top:20,width:800,height:450},[{dataset:{actor:'noa',slot:'1'},getBoundingClientRect:()=>({left:620,top:411,width:73,height:12})}],[measuredLink]);
  env.run('refreshBattleThreads()');assert.ok(renderedLine.startsWith('M1213,794 '));assert.ok(renderedLine.endsWith(env.run("(()=>{const p=targetPoint('arm');return p.x+','+p.y})()")));
  pass('reserved connection threads use the same measured slot anchor as dragging and contact feedback');

  env=environment();fourParty(env);
  env.run(`battleFx.options.layouts={lian:{atlasWidth:1800,atlasHeight:1200,bodyHeight:300,frames:[{x:11,y:20,width:460,height:510,footX:201,footY:488}]}};showBattle();`);
  assert.ok(env.game.innerHTML.includes('background-size:1100px 733.3333333333334px'));
  assert.ok(env.game.innerHTML.includes('data-combat-src="/combat/LianSDActions.png"'));
  assert.ok(!env.game.innerHTML.includes('src="/art/Lian.png" data-unit='));
  assert.equal(env.run('LanternBattleGeometry.allyBodyHeight'),183.33333333333334);
  assert.equal(env.run("actorPoint('noa').x"),725);
  pass('planning draws the measured first SD pose at the shared compact foot anchor without a full illustration body');

  env=environment();fourParty(env);env.run('showHub()');
  assert.ok(env.game.innerHTML.includes('/art/Lian.png'));assert.ok(!env.game.innerHTML.includes('SDActions'));
  env.context.showRoster(4);assert.ok(env.game.innerHTML.includes('/art/Noa.png'));assert.ok(!env.game.innerHTML.includes('SDActions'));
  env.run("storyKey='prologue';story=data.story.prologue;storyIndex=0;drawStory()");
  assert.ok(env.game.innerHTML.includes('/art/Lian.png'));assert.ok(!env.game.innerHTML.includes('SDActions'));
  pass('the keep, roster and story continue to show the original large illustrations');

  env=environment();fourParty(env);env.run(`data.battle.allies[3].id='heidel';data.battle.allies[3].name='하이델';data.battle.allies[3].portrait='Heidel';showBattle();`);
  assert.ok(env.game.innerHTML.includes('/combat/HeidelSDActions.png'));assert.ok(env.game.innerHTML.includes('/art/Heidel.png'));
  assert.equal((env.game.innerHTML.match(/data-action="skill-card:/g)||[]).length,8);
  pass('a story party containing Heidel uses his own SD body and original portrait');

  env=environment();env.run('data.battle.allies.splice(1);showBattle()');
  assert.equal((env.game.innerHTML.match(/class="field-hero sd-unit/g)||[]).length,1);
  assert.equal((env.game.innerHTML.match(/data-action="skill-card:/g)||[]).length,2);
  assert.ok(env.game.innerHTML.includes('/ 1</small>'));
  pass('early story encounters with fewer recruited companions keep only their real offered cards and action capacity');

  env=environment();threeEnemies(env);
  assert.equal((env.game.innerHTML.match(/data-enemy-stats=/g)||[]).length,3);
  for(const id of ['anger','enemy_2','enemy_3']){
    assert.ok(env.game.innerHTML.includes('data-unit="'+id+'"'));
    assert.ok(env.game.innerHTML.includes('data-action="target:'+id+'"'));
    assert.ok(env.game.innerHTML.includes('data-hp-bar="'+id+'"'));
  }
  assert.ok(env.game.innerHTML.includes('data-action="target:enemy_3_core"'));
  assert.ok(env.game.innerHTML.includes('data-owner="enemy_2"'));
  assert.ok(env.game.innerHTML.includes('표적 · 리안'));
  assert.equal((env.game.innerHTML.match(/data-action="skill-card:/g)||[]).length,8);
  pass('three opponents each own a body, HP, parts and forecast while four allies retain eight actual cards');

  const second=JSON.parse(JSON.stringify(env.run("targetPoint('enemy_2')"))),third=JSON.parse(JSON.stringify(env.run("targetPoint('enemy_3')")));
  assert.ok(third.x>second.x);
  assert.ok(env.run("targetPoint('enemy_2_core').x < targetPoint('enemy_3_core').x"));
  assert.ok(env.run("targetPoint('1:enemy_2_arm').x < targetPoint('1:enemy_3_arm').x"));
  assert.equal(env.run("targetPoint('unknown')"),null);
  env.run(`data.battle.enemies=JSON.parse(JSON.stringify(data.battle.enemies));data.battle.enemies[2].id='replacement';`);
  assert.ok(env.run("targetPoint('replacement').x > targetPoint('enemy_2').x"));
  pass('enemy, part and intent anchors use their real owner in the current state and unknown targets never fall back to the first enemy');

  env=environment();threeEnemies(env);env.run(`tone=()=>{};command=async(action,args)=>{globalThis.interceptRecord={action,...args};if(action==='link')data.battle.links.push(args);return true;};`);
  await env.click('skill-card:sera:sera-card-1');await env.click('target:1:enemy_2_arm');
  assert.equal(env.context.interceptRecord.actorId,'sera');assert.equal(env.context.interceptRecord.kind,'Clash');
  assert.equal(env.context.interceptRecord.targetId,'1:enemy_2_arm');
  assert.ok(env.game.innerHTML.includes('data-original-target="lian" data-interceptor="sera"'));
  assert.ok(env.game.innerHTML.includes('대신 맞불 · 세라'));
  assert.ok(env.game.innerHTML.includes('class="thread-intercept"'));
  assert.ok(env.game.innerHTML.includes('original-threat'));
  assert.equal(env.run("targetHint('sera','1:enemy_2_arm')"),'리안 대신 가로막기');
  env.run('data.battle.links=[];showBattle()');
  assert.ok(!env.game.innerHTML.includes('class="thread-intercept"'));
  assert.ok(!env.game.innerHTML.includes('대신 맞불 · 세라'));
  pass('the second ally can intercept an exact second-enemy forecast for the first ally; removing it restores the original route and label');

  env=environment();threeEnemies(env);env.run(`data.battle.intents[2].kind='Area';data.battle.links=[{actorId:'sera',slot:0,skillId:'sera_glimmer',cardId:'sera-card-1',kind:'Clash',targetId:'1:enemy_3_arm'}];showBattle();`);
  assert.ok(env.game.innerHTML.includes('표적 · 아군 전체'));
  assert.ok(env.run("intentDetails(intent('1:enemy_3_arm'))").includes('광역의 전체 범위는 유지'));
  assert.ok(!env.game.innerHTML.includes('class="thread-intercept"'));
  assert.equal(env.run("targetHint('sera','1:enemy_3_arm')"),'광역 맞불 · 패배 시 전원 피격');
  pass('an area forecast keeps the whole-party threat and never promises single-character protection');

  env=environment();threeEnemies(env);env.run(`data.battle.enemies[1].hp=0;showBattle();`);
  assert.ok(!env.run("validDropTargets(skill('lian','lian_glimmer')).includes('enemy_2')"));
  assert.ok(!env.run("validDropTargets(skill('lian','lian_glimmer')).includes('enemy_2_arm')"));
  assert.ok(!env.run("validDropTargets(skill('lian','lian_glimmer')).includes('1:enemy_2_arm')"));
  assert.ok(env.run("validDropTargets(skill('lian','lian_glimmer')).includes('enemy_3')"));
  assert.ok(env.game.innerHTML.includes('data-unit="enemy_3"'));
  pass('defeating one opponent removes only its eligible targets while the third opponent remains available');

  env=environment();threeEnemies(env);env.run(`tone=()=>{};command=async(action,args)=>{globalThis.thirdDrop={action,...args};return true;};`);
  env.gameEvents.pointerdown({clientX:1050,clientY:700,pointerId:4,target:{closest:()=>({dataset:{actor:'noa',cardId:'noa-card-1'},disabled:false})}});
  env.events.pointermove({clientX:1475,clientY:256,pointerId:4});env.setHit('1:enemy_3_arm');
  await env.events.pointerup({clientX:1475,clientY:256,pointerId:4});
  assert.equal(env.context.thirdDrop.actorId,'noa');assert.equal(env.context.thirdDrop.targetId,'1:enemy_3_arm');
  assert.equal(env.context.thirdDrop.cardId,'noa-card-1');
  env.context.thirdDrop=null;env.run('suppressClickUntil=0');
  env.gameEvents.pointerdown({clientX:1050,clientY:700,pointerId:5,target:{closest:()=>({dataset:{actor:'noa',cardId:'noa-card-2'},disabled:false})}});
  env.events.pointermove({clientX:1475,clientY:256,pointerId:5});env.events.pointercancel({pointerId:5});
  await env.events.pointerup({clientX:1475,clientY:256,pointerId:5});assert.equal(env.context.thirdDrop,null);
  pass('direct-card dragging preserves the fourth actor and third-enemy intent IDs, and cancelling sends no action');

  env=environment();fourParty(env);
  assert.equal((env.game.innerHTML.match(/data-action="skill-card:/g)||[]).length,8);
  assert.equal((env.game.innerHTML.match(/class="slot-wrap locked"/g)||[]).length,4);
  assert.ok(env.game.innerHTML.includes('/ 4</small>'));
  for(const round of [2,3,4,5])assert.ok(env.game.innerHTML.includes('R'+round+'부터 개방'));
  pass('round one retains eight offered choices but exposes only four actions with the future unlock rounds');

  env.run(`tone=()=>{};globalThis.firstRoundCalls=[];command=async(action,args)=>{firstRoundCalls.push({action,...args});if(action==='link'){data.battle.links=data.battle.links.filter(l=>l.actorId!==args.actorId||l.slot!==args.slot);data.battle.links.push(args);}return true;};`);
  await env.click('skill-card:lian:lian-card-2');await env.click('target:enemy_2');
  await env.click('skill-card:lian:lian-card-1');await env.click('target:intent1');
  assert.deepEqual(Array.from(env.context.firstRoundCalls).map(c=>[c.slot,c.cardId]),[[0,'lian-card-2'],[0,'lian-card-1']]);
  assert.equal(env.run('data.battle.links.length'),1);assert.equal(env.run("slotLink('lian',0).cardId"),'lian-card-1');
  assert.equal(env.run('manualSlotFocus'),false);
  pass('either first-round card can be chosen and a full one-action bank replaces its current reservation without creating a second action');

  env.run(`globalThis.firstRoundCalls=[];selectedActor='lian';selectedSlot=0;selectedCardId='lian-card-1';`);
  await env.click('slot:noa:1');assert.equal(env.run('selectedActor'),'lian');assert.equal(env.run('selectedSlot'),0);
  env.gameEvents.pointerdown({clientX:1100,clientY:810,pointerId:99,target:{closest:()=>({dataset:{actor:'noa',slot:'1'},disabled:false})}});
  assert.equal(env.run('dragInfo'),null);
  env.run(`selectedActor='noa';selectedSlot=1;selectedSkill='noa_glimmer';selectedCardId='noa-card-1';`);
  await env.context.linkTarget('intent1');assert.equal(env.context.firstRoundCalls.length,0);
  env.run('showBattle()');assert.equal(env.run('selectedSlot'),0);assert.equal(env.run('manualSlotFocus'),false);
  pass('locked action click, forged drag source and stale selected action send no commands and redraw clamps to the first usable action');

  env=environment();fourParty(env);
  env.run(`data.battle.allies[0].syncLevel=1;data.battle.allies[0].skills.push({id:'lian_wide',name:'시험용 두 행동 공격',number:2,requiredSyncLevel:1,slots:2,kind:'Attack',cost:0});data.battle.allies[0].skillHand[1]={id:'lian-s3-card',skillId:'lian_wide'};data.battle.allies[2].syncLevel=1;data.battle.allies[2].skills.push({id:'minato_s3',name:'한 행동 치유',number:3,requiredSyncLevel:1,slots:1,kind:'Heal',cost:0});data.battle.allies[2].skillHand[1]={id:'minato-s3-card',skillId:'minato_s3'};showBattle();`);
  assert.equal(env.run("dockChoice('lian','lian-s3-card').reason"),'행동 2칸 필요');assert.equal(env.run("selectDockChoice('lian','lian-s3-card')"),false);
  assert.equal(env.run("selectDockChoice('minato','minato-s3-card')"),true);assert.equal(env.run('selectedSlot'),0);
  lateRound(env,2);assert.equal(env.run("selectDockChoice('lian','lian-s3-card')"),true);assert.equal(env.run('selectedSlot'),0);
  pass('a custom two-action attack waits for capacity while Minato’s one-action S3 heal is usable in round one');

  env=environment();fourParty(env);
  for(const [round,capacity] of [[1,4],[2,5],[3,6],[4,7],[5,8],[9,8]]){
    lateRound(env,round,false);assert.ok(env.game.innerHTML.includes('/ '+capacity+'</small>'));
    assert.equal((env.game.innerHTML.match(/class="slot-wrap locked"/g)||[]).length,8-capacity);
  }
  pass('normal encounters reveal one additional action per round in fixed party order and stop at eight');
  for(const [round,capacity] of [[1,4],[2,5],[3,6],[4,6],[9,6]]){
    lateRound(env,round,true);assert.ok(env.game.innerHTML.includes('/ '+capacity+'</small>'));
    assert.equal((env.game.innerHTML.match(/보스전 · 1행동<\/div>/g)||[]).length,2);
    assert.equal(env.run("actionCount(unit('minato'))"),1);assert.equal(env.run("actionCount(unit('noa'))"),1);
  }
  pass('boss encounters stop at six actions and label the third and fourth fighters’ permanent one-action limit');

  env.run(`selectedActor='lian';selectedSlot=1;manualSlotFocus=true;data.battle.round=1;data.battle.allies.forEach(a=>a.slotCount=1);showBattle();`);
  assert.equal(env.run('selectedSlot'),0);assert.equal(env.run('manualSlotFocus'),false);assert.ok(env.game.innerHTML.includes('/ 4</small>'));
  pass('restarting an encounter resets a previously focused second action to the real first-round capacity');

  env=environment();fourParty(env);
  env.run(`globalThis.gainNotices=[];toast=message=>gainNotices.push(message);battleFx.play=async()=>true;command=async action=>{if(action==='next'){data.battle.round=2;data.battle.allies[0].slotCount=2;}return true;};`);
  await env.context.resolveRound();assert.equal(env.context.gainNotices.length,1);assert.equal(env.context.gainNotices[0],'리안의 두 번째 행동이 열렸습니다.');
  assert.ok(env.game.innerHTML.includes('/ 5</small>'));
  pass('the actual next-round presentation announces the newly unlocked fighter and updates the action counter');

  env=environment();fourParty(env);lateRound(env,1,true);
  env.run(`data.battle.allies.forEach(a=>{a.syncLevel=1;const id=a.id+'_single_s3';a.skills.push({id,name:'고조',number:3,requiredSyncLevel:1,slots:1,kind:'Attack',cost:3});a.skillHand[1]={id:a.id+'-s3-card',skillId:id};});showBattle();`);
  for(const id of ['lian','sera','minato','noa']){assert.equal(env.run(`selectDockChoice('${id}','${id}-s3-card')`),true);assert.equal(env.run('selectedSlot'),0);}
  assert.equal(env.run("actionCount(unit('noa'))"),1);assert.equal(env.run("skill('noa','noa_single_s3').slots"),1);
  pass('all synchronized third skills use one action from round one, including the boss-limited third and fourth fighters');

  env=environment();fourParty(env);lateRound(env,3);
  env.run(`selectedActor='lian';selectedSlot=1;manualSlotFocus=true;data.battle.allies[0].hp=0;showBattle();`);
  assert.equal(env.run('selectedActor'),'sera');assert.equal(env.run('selectedSlot'),0);assert.equal(env.run('manualSlotFocus'),false);
  assert.ok(env.game.innerHTML.includes('/ 4</small>'));
  pass('a defeated selected fighter gives focus to the next living ally and removes its unlocked actions from the capacity');

  env=environment();fourParty(env);
  env.run(`data.battle.allies[0].syncLevel=1;data.battle.allies[0].skills.push({id:'lian_new_s3',name:'고조',number:3,requiredSyncLevel:1,slots:1,kind:'Attack',cost:3});data.battle.allies[0].skillHand[0].skillId='lian_new_s3';selectedActor='lian';selectedCardId='lian-card-1';selectedSkill='lian_glimmer';showBattle();`);
  assert.equal(env.run('selectedSkill'),'lian_new_s3');assert.ok(env.game.innerHTML.includes('class="party-number">4</i>노아'));
  assert.equal((env.game.innerHTML.match(/class="party-number"/g)||[]).length,8);
  pass('the selected action follows a refreshed card identity and both battlefield and dock show fixed party order numbers');

  env=environment();campaignFixture(env);
  env.run(`for(const stage of data.campaign.chapters[0].stages.slice(0,3)){stage.cleared=true;stage.unlocked=true;}data.campaign.chapters[0].stages[3].unlocked=true;showStages();`);
  assert.equal((env.game.innerHTML.match(/data-port=/g)||[]).length,4);
  assert.equal((env.game.innerHTML.match(/data-action="stage-select:/g)||[]).length,4);
  assert.ok(env.game.innerHTML.includes('voyage-map'));assert.ok(env.game.style.backgroundImage.includes('VoyageSea.png'));
  assert.ok(env.game.innerHTML.includes('/art/VoyageShip.png'));assert.ok(env.game.innerHTML.includes('data-ship-port="c1s04"'));
  assert.equal(env.run('voyagePorts(data.campaign.chapters[0].stages.filter(s=>s.unlocked||s.cleared)).length'),4);
  assert.equal(env.run('voyageRoute(voyagePorts(data.campaign.chapters[0].stages.filter(s=>s.unlocked||s.cleared))).match(/ C/g).length'),3);
  assert.ok(!env.game.innerHTML.includes('data-port="c1s05"'));assert.ok(!env.game.innerHTML.includes('boss-port selected'));
  pass('voyage chart connects only the four known ports and anchors the lantern vessel at the next real stage without future silhouettes');

  env.run(`for(const stage of data.campaign.chapters[0].stages){stage.cleared=true;stage.unlocked=true;}showStages(1,'chapter1_beacon_defense');`);
  assert.equal((env.game.innerHTML.match(/data-port=/g)||[]).length,10);assert.ok(env.game.innerHTML.includes('boss-port selected'));
  assert.ok(env.game.innerHTML.includes('data-action="stage-replay:chapter1_beacon_defense"'));assert.ok(env.game.innerHTML.includes('data-ship-port="c1s10"'));
  const portGeometry=JSON.parse(JSON.stringify(env.run('voyagePorts(data.campaign.chapters[0].stages).map(({x,y})=>({x,y}))')));
  assert.deepEqual(portGeometry.slice(4,7),[{x:932,y:110},{x:932,y:320},{x:724,y:320}]);
  assert.ok(portGeometry.every(p=>p.x>=100&&p.x<=932&&p.y>=110&&p.y<=320));
  pass('completed chapter voyage bends across two rows, identifies the discovered boss port and preserves stage-specific replay');

  env=environment();fourParty(env);lateRound(env,3,true);
  env.run(`selectedActor='minato';selectedSlot=0;manualSlotFocus=true;selectedSkill='minato_heal';selectedCardId='minato-card-2';data.battle.links=[];data.battle.allies[2].skillHand=[{id:'minato:card:new:1',skillId:'minato_glimmer'},{id:'minato:card:new:2',skillId:'minato_glimmer'}];showBattle();`);
  assert.equal(env.run('selectedCardId'),'minato:card:new:1');assert.equal(env.run('selectedSkill'),'minato_glimmer');
  assert.ok(!env.game.innerHTML.includes('data-action="skill-card:minato:minato-card-2"'));
  env.run(`globalThis.newHandCall=null;command=async(action,args)=>{newHandCall={action,...args};return true;};showBattle=()=>{};`);
  await env.context.linkTarget('anger');assert.equal(env.context.newHandCall.cardId,'minato:card:new:1');
  pass('a wholly refreshed hand discards the unused old selection and links only a new full colon-containing card identity');

  env=environment();fourParty(env);
  env.run(`busy=true;page='resolve';globalThis.hiddenDockCalls=[];command=async(action,args)=>{hiddenDockCalls.push({action,...args});return true;};`);
  await env.context.linkTarget('anger');assert.equal(env.context.hiddenDockCalls.length,0);
  pass('a concealed execution dock cannot submit a stale direct target link while presentation is busy');

  env=environment();
  env.run(`page='battle';globalThis.cancelDraws=0;globalThis.executionModes=[];battleFx.execution=active=>executionModes.push(active);battleFx.play=async()=>false;command=async()=>true;showBattle=()=>{cancelDraws++;page='battle';};`);
  await env.context.resolveRound();
  assert.equal(env.context.cancelDraws,1);assert.equal(env.run('busy'),false);assert.deepEqual(Array.from(env.context.executionModes),[true,false]);
  pass('same-screen playback cancellation redraws and restores the planning dock without advancing the round');

  await recruitmentChecks();
  await storyUxChecks();
  await titleInputChecks();
  candidateActionStatusChecks();
  await intentDockChecks();
  console.log('TOTAL ' + passed + ' regression cases passed');
}

main().catch(error => { console.error(error); process.exitCode = 1; });
