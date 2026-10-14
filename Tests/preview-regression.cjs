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
  let id = 0, hasModal = false, hit = null, elementSlots=[], linkPaths=[], gameBox={ left: 0, top: 0, width: 1600, height: 900 };
  const words = { textContent: '' };
  const thread = { d: '', setAttribute(key, value) { this[key] = value; } };
  const game = {
    style: {}, classList: { toggle() {} }, innerHTML: '',
    addEventListener: (name, handler) => { gameEvents[name] = handler; },
    getBoundingClientRect: () => gameBox,
    querySelector: selector => selector === '.modal'
      ? (hasModal ? { remove() { hasModal = false; } } : null)
      : selector === '.words' ? words : selector === '#drag-thread' ? thread : null,
    insertAdjacentHTML() { hasModal = true; },
    querySelectorAll: selector => selector==='.slot'?elementSlots:selector==='[data-link-actor]'?linkPaths:[]
  };
  const context = {
    document: {
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

async function main() {
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

  function campaignFixture(e) {
    const chapters = JSON.parse(fs.readFileSync(path.join(__dirname, '../기획서/메인스토리/campaign.json'), 'utf8')).chapters;
    for(const [index,chapter] of chapters.entries()){chapter.focusCharacterId||=['lian','sera','noa','heidel','minato'][index];chapter.focusStorySummary||='메인 스토리에 이어지는 인물의 과거와 현재';}
    for (const chapter of chapters) for (const stage of chapter.stages) { stage.unlocked = stage.id === 'c1s01'; stage.cleared = false; }
    e.run('data.campaign=' + JSON.stringify({ chapters, activeStage: null, phase: 'None', replay: false, currentStory: [] }));
    return chapters;
  }
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
  assert.ok(env.game.innerHTML.includes('원래 대상 · 리안'));
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
  assert.ok(env.game.innerHTML.includes('가로막기 · 세라'));
  assert.ok(env.game.innerHTML.includes('class="thread-intercept"'));
  assert.ok(env.game.innerHTML.includes('original-threat'));
  assert.equal(env.run("targetHint('sera','1:enemy_2_arm')"),'리안 대신 가로막기');
  env.run('data.battle.links=[];showBattle()');
  assert.ok(!env.game.innerHTML.includes('class="thread-intercept"'));
  assert.ok(!env.game.innerHTML.includes('가로막기 · 세라'));
  pass('the second ally can intercept an exact second-enemy forecast for the first ally; removing it restores the original route and label');

  env=environment();threeEnemies(env);env.run(`data.battle.intents[2].kind='Area';data.battle.links=[{actorId:'sera',slot:0,skillId:'sera_glimmer',cardId:'sera-card-1',kind:'Clash',targetId:'1:enemy_3_arm'}];showBattle();`);
  assert.ok(env.game.innerHTML.includes('원래 대상 · 아군 전체'));
  assert.ok(env.game.innerHTML.includes('광역 전체 유지'));
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

  console.log('TOTAL ' + passed + ' regression cases passed');
}

main().catch(error => { console.error(error); process.exitCode = 1; });
