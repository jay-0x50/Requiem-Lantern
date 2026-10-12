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
  id, name: id, kind, cost: 0, slots: 1, speed: 10, hitPowers: [6]
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
        skills: [skill('lian_glimmer'), skill('lian_breakthrough'), skill('lian_guard', 'Guard')], parts: [] },
      { id: 'minato', name: '미나토', hp: 90, maxHp: 100, element: 'Tide',
        skills: [skill('minato_glimmer'), skill('minato_heal', 'Heal')], parts: [] }
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
  env.run("page='battle';showBattle=()=>{};tone=()=>{};command=async(action,args)=>{globalThis.record={action,...args};return true};selectedSkill='lian_breakthrough';");
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

  env.run("suppressClickUntil=0;selectedSkill='lian_breakthrough'");
  pointerDown(); pointerMove(1392.5, 611.5); env.setHit('anger'); await pointerUp(1392.5, 611.5);
  assert.equal(env.context.record.targetId, 'anger');
  pass('actual body target button wins over nearby core');

  env.context.record = null;
  env.run('suppressClickUntil=0');
  pointerDown(); pointerMove(172, 446); env.setHit(null); await pointerUp(172, 446);
  assert.equal(env.context.record, null);
  pass('attack snapping excludes allied units');

  env.run("suppressClickUntil=0;selectedActor='minato';selectedSlot=0;selectedSkill='minato_heal';");
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

  env = environment();
  env.run(`data.characterStories=[{id:'noa',name:'노아',title:'침묵에 남긴 이름',summary:'이름과 선택',recommendedAfter:'제3장 종료 후',lines:[
   {speaker:'노아',text:'열려 있어. 들어와.',portrait:'noa',background:'keep',location:'기록실',caption:'노아 · 6/6 · 돌아온 이름',musicCue:'bgm_noa_silent_bell_afterglow'}
  ]}];globalThis.storyCommands=[];command=async action=>{storyCommands.push(action);return true;};tone=()=>{};`);
  env.context.showCharacterStories();
  assert.ok(env.game.innerHTML.includes('후반부 내용'));assert.ok(env.game.innerHTML.includes('read-character:noa'));
  await env.click('read-character:noa');
  assert.equal(env.run('page'),'story');assert.equal(env.run('storyKey'),'character:noa');
  assert.ok(env.game.innerHTML.includes('노아 · 6/6 · 돌아온 이름'));assert.ok(env.game.innerHTML.includes('기록실'));
  assert.ok(!env.game.innerHTML.includes('bgm_noa_silent_bell_afterglow'));
  await env.click('storyskip');assert.equal(env.run('page'),'character-stories');
  assert.deepEqual(Array.from(env.context.storyCommands),[]);
  const markup=env.game.innerHTML;env.context.startStory('character:unknown','characterStories');
  assert.equal(env.game.innerHTML,markup);assert.equal(env.run('page'),'character-stories');
  pass('character story route shows chapter context, returns after skip and never marks prologue or rewards');

  console.log('TOTAL ' + passed + ' regression cases passed');
}

main().catch(error => { console.error(error); process.exitCode = 1; });
