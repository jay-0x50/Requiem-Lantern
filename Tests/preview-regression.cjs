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
      { id: 'lian', name: '리안', hp: 100, maxHp: 100, element: 'Radiance',
        skills: [skill('lian_glimmer'), skill('lian_breakthrough'), skill('lian_guard', 'Guard')],parts:[],skillHand:[{id:'lian-card-1',skillId:'lian_glimmer'},{id:'lian-card-2',skillId:'lian_breakthrough'}],drawPile:['s1','s1','s2'],discardPile:[] },
      { id: 'minato', name: '미나토', hp: 90, maxHp: 100, element: 'Tide',
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
  let id = 0, hasModal = false, hit = null;
  const words = { textContent: '' };
  const thread = { d: '', setAttribute(key, value) { this[key] = value; } };
  const game = {
    style: {}, classList: { toggle() {} }, innerHTML: '',
    addEventListener: (name, handler) => { gameEvents[name] = handler; },
    getBoundingClientRect: () => ({ left: 0, top: 0, width: 1600, height: 900 }),
    querySelector: selector => selector === '.modal'
      ? (hasModal ? { remove() { hasModal = false; } } : null)
      : selector === '.words' ? words : selector === '#drag-thread' ? thread : null,
    insertAdjacentHTML() { hasModal = true; },
    querySelectorAll: () => []
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
  pointerDown(); pointerMove(172, 446); env.setHit(null); await pointerUp(172, 446);
  assert.equal(env.context.record, null);
  pass('attack snapping excludes allied units');

  env.run("suppressClickUntil=0;selectedActor='minato';selectedSlot=0;selectedSkill='minato_heal';selectedCardId='minato-card-2';");
  pointerDown('minato'); pointerMove(172, 446); env.setHit(null); await pointerUp(172, 446);
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
  assert.ok(!env.game.innerHTML.includes('세 번째 불'));assert.equal((env.game.innerHTML.match(/data-action="skill-card:/g)||[]).length,2);
  assert.equal((env.game.innerHTML.match(/data-action="slot:lian:/g)||[]).length,1);
  assert.ok(env.game.innerHTML.includes('카드 구성 1×3 / 2×2'));
  env.run(`data.battle.allies[0].syncLevel=1;data.battle.allies[0].sanity=20;data.battle.allies[0].skillHand[1]={id:'lian-card-3',skillId:'lian_third'};showBattle();`);
  assert.ok(env.game.innerHTML.includes('3스킬 · 세 번째 불'));assert.ok(env.game.innerHTML.includes('/ 3×1'));assert.ok(env.game.innerHTML.includes('점등 90%'));
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
  env.run(`data.battle.allies[0].skillHand=[{id:'duplicate-one',skillId:'lian_glimmer'},{id:'duplicate-two',skillId:'lian_glimmer'}];selectFirstFighter();showBattle();tone=()=>{};globalThis.cardCalls=[];command=async(action,args)=>{cardCalls.push({action,...args});return true;};`);
  assert.equal((env.game.innerHTML.match(/data-action="skill-card:/g)||[]).length,2);
  await env.click('skill-card:duplicate-two');await env.context.linkTarget('intent1');
  assert.equal(env.context.cardCalls[0].cardId,'duplicate-two');assert.equal(env.context.cardCalls[0].skillId,'lian_glimmer');
  env.run(`data.battle.links=[{actorId:'lian',slot:0,skillId:'lian_glimmer',cardId:'duplicate-two',kind:'Clash',targetId:'intent1'}];selectedSlot=1;selectedCardId='';selectedSkill='lian_glimmer';showBattle();`);
  assert.equal(env.run('selectedCardId'),'duplicate-one');
  assert.ok(env.game.innerHTML.includes('data-card-id="duplicate-two" title="제시된 카드 2 · 기본 위력 4 · 켜진 불마다 +2" disabled'));
  await env.click('slot:lian:0');assert.equal(env.run('selectedCardId'),'duplicate-two');
  pass('identical offered skills retain distinct card identities and another slot cannot reserve the same card');

  env=environment();
  env.run(`page='battle';selectedActor='lian';selectedSlot=0;selectedSkill='lian_glimmer';selectedCardId='lian-card-1';data.battle.links=[{actorId:'lian',slot:0,skillId:'lian_glimmer',cardId:'lian-card-1',kind:'Strike',targetId:'anger'}];tone=()=>{};command=async(action,args)=>{globalThis.dragCardCall={action,...args};return true;};`);
  await env.click('skill-card:lian-card-2');
  env.gameEvents.pointerdown({clientX:113,clientY:756,pointerId:2,target:{closest:()=>({dataset:{actor:'lian',slot:'0'},disabled:false})}});
  env.events.pointermove({clientX:945,clientY:270,pointerId:2});env.setHit('intent1');await env.events.pointerup({clientX:945,clientY:270,pointerId:2});
  assert.equal(env.context.dragCardCall.cardId,'lian-card-2');assert.equal(env.context.dragCardCall.skillId,'lian_breakthrough');
  pass('same-slot drag uses a newly selected offered card when replacing an existing connection');

  env=environment();
  env.run(`tone=()=>{};selectedCardId='lian-card-2';globalThis.guardCardCall=null;command=async(action,args)=>{guardCardCall={action,...args};return true;};`);
  await env.click('skill:lian_guard');await env.context.linkTarget('lian');
  assert.equal(env.context.guardCardCall.cardId,'');assert.equal(env.context.guardCardCall.kind,'Guard');
  assert.equal(env.run('selectedCardId'),'');
  pass('defense remains available outside the card bag and sends no attack card identity');


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

  console.log('TOTAL ' + passed + ' regression cases passed');
}

main().catch(error => { console.error(error); process.exitCode = 1; });
