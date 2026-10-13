// Exercises the real presentation class with an isolated DOM and virtual clock.
// No live server, storage access or save mutations. Visual layout is checked separately.
// Run: node Tests/combat-fx-regression.cjs
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const source = fs.readFileSync(path.join(__dirname, '../Preview/combat-fx.js'), 'utf8');
const copy = value => JSON.parse(JSON.stringify(value));

class Node {
  constructor(tag = 'div', className = '', dataset = {}) {
    this.tagName = tag; this.children = []; this.dataset = dataset; this.textContent = '';
    this.style = {
      visibility: '', transform: '',
      setProperty(key, value) { this[key] = value; },
      removeProperty(key) { const value = this[key]; delete this[key]; return value; }
    };
    this.classes = new Set(className.split(/\s+/).filter(Boolean));
    this.classList = {
      add: (...names) => names.forEach(n => this.classes.add(n)),
      remove: (...names) => names.forEach(n => this.classes.delete(n)),
      contains: name => this.classes.has(name),
      toggle: (name, force) => {
        const add = force === undefined ? !this.classes.has(name) : force;
        if (add) this.classes.add(name); else this.classes.delete(name);
        return add;
      }
    };
  }
  set className(value) { this.classes = new Set(value.split(/\s+/).filter(Boolean)); }
  get className() { return [...this.classes].join(' '); }
  appendChild(node) { node.parent = this; this.children.push(node); return node; }
  append(...nodes) { nodes.forEach(node => this.appendChild(node)); }
  remove() { if (this.parent) this.parent.children = this.parent.children.filter(n => n !== this); this.parent = null; }
  descendants() { return this.children.flatMap(n => [n, ...n.descendants()]); }
  matches(selector) {
    if (selector.startsWith('.')) return this.classes.has(selector.slice(1));
    if (selector.startsWith('[data-')) {
      const match = selector.match(/^\[data-([\w-]+)(?:\^="([^"]*)")?\]$/);
      if (!match) return false;
      const key = match[1].replace(/-([a-z])/g, (_, c) => c.toUpperCase());
      return this.dataset[key] !== undefined && (match[2] === undefined || this.dataset[key].startsWith(match[2]));
    }
    return this.tagName === selector;
  }
  querySelectorAll(selector) {
    if (selector.includes(',')) return [...new Set(selector.split(',').flatMap(s => this.querySelectorAll(s.trim())))];
    const pieces = selector.split(/\s+/);
    if (pieces.length > 1) return this.querySelectorAll(pieces[0]).flatMap(n => n.querySelectorAll(pieces.slice(1).join(' ')));
    return this.descendants().filter(n => n.matches(selector));
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
  insertAdjacentHTML(_position, html) {
    if (!html.includes('combat-fx')) throw Error('Unexpected fixture HTML');
    this.appendChild(new Node('div', 'combat-fx'));
    const cursor = this.appendChild(new Node('div', 'drag-cursor'));
    cursor.appendChild(new Node('i')); cursor.appendChild(new Node('span'));
    const caption = this.appendChild(new Node('div', 'combat-caption'));
    caption.appendChild(new Node('div', 'caption-overline')); caption.appendChild(new Node('strong')); caption.appendChild(new Node('span'));
    caption.appendChild(new Node('div', 'playback-progress')).appendChild(new Node('i'));
  }
}

function battle() {
  return {
    round: 1,
    allies: [
      { id: 'lian', name: '리안', hp: 80, maxHp: 100, element: 'Radiance', parts: [] },
      { id: 'minato', name: '미나토', hp: 30, maxHp: 100, element: 'Tide', parts: [] }
    ],
    enemies: [{ id: 'anger', name: '분노', hp: 190, maxHp: 300, isEnemy: true, element: 'Shadow', rift: 70, maxRift: 100,
      parts: [{ id: 'anger_arm', name: '팔', hp: 40 }, { id: 'anger_core', name: '핵', hp: 60 }] }],
    intents: [{ id: '1:anger_arm', actorId: 'anger', partId: 'anger_arm' }, { id: '1:anger_core', actorId: 'anger', partId: 'anger_core' }],
    links: []
  };
}

function environment(motion = true, mockImages = false, initialSnapshot = battle()) {
  const root = new Node(), stage = root.appendChild(new Node('div', 'battle'));
  stage.appendChild(new Node('div', 'phase'));
  const initial = initialSnapshot;
  for (const u of [...initial.allies, ...initial.enemies]) {
    stage.appendChild(new Node('img', '', { unit: u.id })).src = '/art/' + u.id + '.png';
    stage.appendChild(new Node('span', '', { hpBar: u.id }));
    stage.appendChild(new Node('small', '', { hpText: u.id }));
    stage.appendChild(new Node('button', '', { action: 'target:' + u.id }));
  }
  for (const id of [...initial.intents.map(i => i.id), ...initial.enemies[0].parts.map(p => p.id)])
    stage.appendChild(new Node('button', '', { action: 'target:' + id }));
  for (const i of initial.intents) stage.appendChild(new Node('path', '', { intentLine: i.id }));
  let clock = 0, nextId = 0;
  const timers = new Map(), sounds = [], impacts = [], images = [];
  const context = {
    document: { createElement: tag => new Node(tag) },
    setTimeout: (run, delay) => { const id = ++nextId; timers.set(id, { run, due: clock + delay }); return id; },
    clearTimeout: id => timers.delete(id),
    fetch: () => { throw Error('FX must not make network requests'); },
    localStorage: { getItem: () => { throw Error('FX must not access saves'); }, setItem: () => { throw Error('FX must not write saves'); } },
    console
  };
  if (mockImages) context.Image = class {
    constructor() { images.push(this); }
    set src(value) { this.url = value; }
    get src() { return this.url; }
  };
  vm.createContext(context); vm.runInContext(source, context);
  const fx = new context.LanternCombatFX(root, { motion: () => motion, sound: (...args) => sounds.push(args), impact: (...args) => impacts.push(args), color: () => '#ffaa33' });
  fx.attach(initial);
  return {
    root, stage, fx, initial, timers, sounds, impacts, images,
    async until(predicate) {
      for (let guard = 0; guard < 5000; guard++) {
        for(let tick=0;tick<8;tick++)await Promise.resolve();
        if (predicate()) return;
        if (!timers.size) throw Error('No pending timer before the expected presentation phase');
        const [id, timer] = [...timers].sort((a, b) => a[1].due - b[1].due)[0];
        clock = timer.due; timers.delete(id); timer.run();
      }
      throw Error('Expected presentation phase was never reached');
    },
    async drain() {
      for (let guard = 0; guard < 5000; guard++) {
        for(let tick=0;tick<8;tick++)await Promise.resolve();
        if (!timers.size) { await Promise.resolve(); if (!timers.size) return; }
        const [id, timer] = [...timers].sort((a, b) => a[1].due - b[1].due)[0];
        clock = timer.due; timers.delete(id); timer.run();
      }
      throw Error('FX virtual clock did not settle');
    }
  };
}

let passed = 0;
function pass(name) { passed++; console.log('PASS ' + name); }
const event = (type, targetId, amount = 0, actorId = 'lian') => ({ type, targetId, amount, actorId, message: type, round: 1 });

async function main() {
  let env = environment();
  assert.deepEqual(copy(env.fx.point('lian')), { x: 172, y: 446 });
  assert.deepEqual(copy(env.fx.point('minato')), { x: 437, y: 446 });
  assert.deepEqual(copy(env.fx.point('anger')), { x: 1360, y: 410 });
  assert.deepEqual(copy(env.fx.point('1:anger_core')), { x: 945, y: 392 });
  assert.deepEqual(copy(env.fx.point('anger_arm')), { x: 1190, y: 550 });
  assert.equal(env.fx.point('unknown'), null); assert.equal(env.fx.point(''), null);
  pass('unit, part and colon-containing intent IDs have correct anchors');

  const original = JSON.stringify(env.initial);
  env.fx.applyEvent(event('Damage', 'lian', 200, 'anger'));
  env.fx.applyEvent(event('Heal', 'minato', 100, 'minato'));
  env.fx.applyEvent(event('PartBreak', 'anger_arm'));
  env.fx.applyEvent(event('Rift', 'anger', 15));
  assert.equal(env.fx.units.get('lian').hp, 0); assert.equal(env.fx.units.get('minato').hp, 100);
  assert.equal(env.fx.parts.get('anger_arm').hp, 0); assert.equal(env.fx.units.get('anger').rift, 0);
  assert.equal(JSON.stringify(env.initial), original);
  assert.equal(env.fx.portrait('lian').classList.contains('dead'), true);
  assert.equal(env.root.querySelectorAll('[data-hp-bar]')[0].style.width, '0%');
  pass('damage and healing clamp presentation HP without mutating the source snapshot');

  env = environment();
  for (const type of ['Ignite', 'Clash', 'ClashWin', 'Tie', 'Resonance', 'Lantern', 'WrapUp', 'BattleEnd'])
    env.fx.applyEvent(event(type, 'anger', 150));
  env.fx.applyEvent(event('Rift', 'anger', 15));
  assert.equal(env.fx.units.get('anger').hp, 190);
  assert.equal(env.fx.units.get('anger').rift, 0);
  pass('summary counts, ignition percentages and rift gauge rewards never become HP damage');

  env = environment();
  env.fx.applyEvent(event('Cancel', '1:anger_arm', 0, 'anger'));
  env.fx.applyEvent(event('Cancel', '', 0, 'minato'));
  assert.equal(env.fx.cancelled.size, 1);
  assert.equal(env.fx.target('1:anger_arm').classList.contains('intent-cancelled'), true);
  assert.equal(env.fx.target('1:anger_core').classList.contains('intent-cancelled'), false);
  assert.equal(env.root.querySelectorAll('[data-intent-line]')[0].classList.contains('intent-cancelled'), true);
  assert.equal(env.root.querySelectorAll('[data-intent-line]')[1].classList.contains('intent-cancelled'), false);
  pass('cancellation only removes the authoritative forecast and its line');

  env = environment();
  env.fx.drag('lian', ['anger_arm', '1:anger_arm'], { x: 945, y: 270 }, '1:anger_arm');
  assert.equal(env.fx.target('1:anger_arm').classList.contains('drop-focus'), true);
  assert.equal(env.fx.target('minato').classList.contains('drop-ready'), false);
  env.fx.drag('lian', [], null, null);
  assert.equal(env.root.querySelectorAll('.drop-ready,.drop-focus').length, 0);
  assert.equal(env.root.querySelector('.drag-cursor').classList.contains('visible'), false);
  pass('drag feedback highlights eligible targets and clears on cancellation');

  env = environment();
  const tie = env.fx.play([
    event('Ignite', '1:anger_arm', 100), event('Tie', 'anger'), event('Stalemate', 'anger', 0)
  ], copy(env.initial));
  await env.drain(); assert.equal(await tie, true);
  assert.equal(env.fx.units.get('anger').hp, 190); assert.equal(env.fx.units.get('lian').hp, 80);
  assert.equal(env.impacts.filter(([kind]) => kind === 'hit').length, 0);
  assert.equal(env.impacts.filter(([kind]) => kind === 'clash').length, 1);
  pass('equal-power equal-speed clash plays one weapon clash without inventing damage or a hit sound');

  env = environment();
  const applied = [], baseApply = env.fx.applyEvent.bind(env.fx);
  env.fx.applyEvent = e => { applied.push(e.type + ':' + e.targetId); baseApply(e); };
  const final = copy(env.initial); final.allies[0].hp = 67; final.allies[1].hp = 50;
  final.enemies[0].hp = 170; final.enemies[0].rift = 48; final.enemies[0].parts[1].hp = 44;
  const playing = env.fx.play([
    event('Damage', 'lian', 13, 'anger'), event('Damage', 'anger', 20), event('Heal', 'minato', 20, 'minato'), event('WrapUp', '', 3, '')
  ], final, { fast: true });
  await env.drain(); assert.equal(await playing, true);
  assert.deepEqual(applied, ['Damage:lian', 'Damage:anger', 'Heal:minato', 'WrapUp:']);
  assert.equal(env.fx.units.get('lian').hp, 67); assert.equal(env.fx.units.get('minato').hp, 50);
  assert.equal(env.fx.units.get('anger').rift, 48); assert.equal(env.fx.parts.get('anger_core').hp, 44);
  assert.equal(env.root.querySelector('.playback-progress i').style.width, '100%');
  pass('playback preserves server event order and finishes at authoritative HP, part and rift values');

  env = environment();
  let synced = false; const baseSync = env.fx.sync.bind(env.fx);
  env.fx.sync = b => { synced = true; baseSync(b); };
  const cancelled = env.fx.play([event('Damage', 'lian', 20, 'anger'), event('Damage', 'anger', 10)], copy(env.initial));
  env.fx.clear(); await env.drain();
  assert.equal(await cancelled, false); assert.equal(synced, false);
  assert.equal(env.fx.units.get('lian').hp, 80); assert.equal(env.fx.units.get('anger').hp, 190);
  assert.equal(env.timers.size, 0); assert.equal(env.root.querySelectorAll('.combat-fx,.combat-caption,.drag-cursor').length, 0);
  assert.equal(env.stage.classList.contains('resolving'), false);
  pass('clear settles pending playback and blocks stale damage or final-state synchronization');

  env = environment(false);
  const still = copy(env.initial); still.enemies[0].hp = 183;
  const reduced = env.fx.play([event('Damage', 'anger', 7)], still);
  await env.drain(); assert.equal(await reduced, true);
  assert.equal(env.fx.units.get('anger').hp, 183);
  assert.equal(env.root.querySelectorAll('.fx-spark').length, 0);
  env.fx.clear(); await env.drain(); assert.equal(env.timers.size, 0);
  pass('reduced motion still applies damage and cleans all pending effects');

  env = environment();
  env.fx.applyEvent(event('Damage', 'lian', 80, 'anger'));
  env.fx.applyEvent(event('PartBreak', 'anger_arm'));
  env.fx.attach(battle());
  assert.equal(env.fx.units.get('lian').hp, 80); assert.equal(env.fx.parts.get('anger_arm').hp, 40);
  assert.equal(env.root.querySelectorAll('.combat-fx').length, 1);
  pass('reattaching initializes a fresh presentation model and one effect layer');

  env = environment();
  const phases = [], damagePhases = [];
  const phaseBase = env.fx.setPosePhase.bind(env.fx), damageBase = env.fx.applyEvent.bind(env.fx);
  env.fx.setPosePhase = (pair, phase) => { phases.push(phase); return phaseBase(pair, phase); };
  env.fx.applyEvent = e => { if (e.type === 'Damage') damagePhases.push(env.stage.dataset.combatPhase); return damageBase(e); };
  const meleeFinal = copy(env.initial); meleeFinal.enemies[0].hp = 170;
  const melee = env.fx.play([event('Damage', 'anger', 20)], meleeFinal);
  await env.until(() => env.stage.dataset.combatPhase === 'windup');
  assert.equal(env.fx.units.get('anger').hp, 190);
  assert.equal(damagePhases.length, 0);
  await env.until(() => env.stage.dataset.combatPhase === 'contact');
  assert.equal(env.fx.units.get('anger').hp, 170);
  assert.deepEqual(damagePhases, ['contact']);
  await env.drain(); assert.equal(await melee, true);
  assert.deepEqual(phases, ['approach', 'windup', 'swing', 'contact', 'recoil', 'recover', 'return']);
  assert.deepEqual(damagePhases, ['contact']);
  assert.deepEqual(env.impacts.map(([kind]) => kind), ['swish', 'hit']);
  assert.equal(env.stage.dataset.combatPhase, '');
  assert.equal(env.root.querySelectorAll('.melee-fighter').length, 0);
  pass('melee moves through weapon phases and applies authoritative damage exactly once at contact');

  env = environment();
  const lianOriginal = env.fx.portrait('lian'), angerOriginal = env.fx.portrait('anger');
  lianOriginal.style.visibility = 'visible'; angerOriginal.style.visibility = 'hidden';
  const resting = env.fx.play([event('Damage', 'anger', 5)], copy(env.initial));
  await env.until(() => env.stage.dataset.combatPhase === 'windup');
  assert.equal(lianOriginal.style.visibility, 'hidden');
  assert.equal(env.root.querySelectorAll('.melee-fighter').length, 2);
  assert.equal(env.root.querySelectorAll('[data-unit-pose]').length, 2);
  await env.drain(); assert.equal(await resting, true);
  assert.equal(lianOriginal.style.visibility, 'visible'); assert.equal(angerOriginal.style.visibility, 'hidden');
  assert.equal(env.root.querySelectorAll('[data-unit-pose]').length, 0);
  pass('pose atlas fighters replace standing art during the exchange and restore each original visibility');

  env = environment();
  const originalPortrait = env.fx.portrait('lian');
  originalPortrait.src = '/art/Lian.png'; originalPortrait.style.transform = 'translateY(-3px)';
  const atlasFighter = env.fx.createFighter('lian', 'ally-side'); env.fx.meleePair = [atlasFighter];
  const anchor = { left: atlasFighter.wrapper.style.left, top: atlasFighter.wrapper.style.top };
  const expectedCells = ['0% 0%', '50% 0%', '100% 0%', '0% 100%', '50% 100%', '100% 100%'];
  for (let frame = 0; frame < 6; frame++) {
    env.fx.pose(atlasFighter, frame, 'test-pose');
    assert.equal(atlasFighter.sprite.dataset.frame, String(frame));
    assert.equal(atlasFighter.sprite.style.backgroundPosition, expectedCells[frame]);
    assert.equal(atlasFighter.wrapper.style.left, anchor.left); assert.equal(atlasFighter.wrapper.style.top, anchor.top);
  }
  assert.ok(atlasFighter.sprite.style.backgroundImage.includes('/combat/LianActions.png'));
  env.fx.releaseMelee();
  assert.equal(originalPortrait.src, '/art/Lian.png'); assert.equal(originalPortrait.style.transform, 'translateY(-3px)');
  assert.equal(originalPortrait.style.visibility, '');
  pass('all six atlas cells address the correct row without shifting the actor anchor or altering idle art');

  env = environment(true, true);
  // Deliberately unequal measured crops: neither a square grid nor a common crop origin.
  const measuredLayout = {
    atlasWidth: 1536, atlasHeight: 1024, bodyHeight: 320,
    frames: [
      { x: 23, y: 37, width: 420, height: 439, footX: 241, footY: 462 },
      { x: 541, y: 18, width: 433, height: 463, footX: 738, footY: 470 },
      { x: 1020, y: 42, width: 497, height: 441, footX: 1243, footY: 472 },
      { x: 4, y: 545, width: 479, height: 451, footX: 224, footY: 980 },
      { x: 555, y: 534, width: 418, height: 465, footX: 742, footY: 985 },
      { x: 1037, y: 558, width: 450, height: 435, footX: 1217, footY: 981 }
    ]
  };
  env.fx.options.layouts = { lian: measuredLayout, anger: measuredLayout };
  // A decoded image is authoritative for dimensions; metadata remains the fallback.
  env.images[0].naturalWidth = 2048; env.images[0].naturalHeight = 1365;
  env.images[0].onload(); env.images[3].onload();
  for (const [id, side, bodySize, atlasWidth, atlasHeight] of [
    ['lian', 'ally-side', 380, 2048, 1365], ['anger', 'enemy-side', 410, 1536, 1024]
  ]) {
    const fighter = env.fx.createFighter(id, side); env.fx.meleePair = [fighter];
    const wrapperAnchor = { left: fighter.wrapper.style.left, top: fighter.wrapper.style.top };
    const scale = bodySize / measuredLayout.bodyHeight;
    for (let frame = 0; frame < 6; frame++) {
      const crop = measuredLayout.frames[frame];
      env.fx.pose(fighter, frame, 'measured');
      const style = fighter.sprite.style;
      assert.equal(style.width, crop.width * scale + 'px');
      assert.equal(style.height, crop.height * scale + 'px');
      assert.equal(style.backgroundSize, `${atlasWidth * scale}px ${atlasHeight * scale}px`);
      assert.equal(style.backgroundPosition, `${-crop.x * scale}px ${-crop.y * scale}px`);
      const renderedFootX = parseFloat(style.left) + (crop.footX - crop.x) * scale;
      const renderedFootY = parseFloat(style.top) + (crop.footY - crop.y) * scale;
      assert.ok(Math.abs(renderedFootX - 240) < 1e-8, id + ' frame ' + frame + ' foot x shifted');
      assert.ok(Math.abs(renderedFootY - 440) < 1e-8, id + ' frame ' + frame + ' foot y shifted');
      assert.equal(fighter.wrapper.style.left, wrapperAnchor.left);
      assert.equal(fighter.wrapper.style.top, wrapperAnchor.top);
    }
    env.fx.releaseMelee();
  }
  pass('measured atlas crops preserve one foot anchor across all six poses and use decoded dimensions before metadata');

  const realLayouts = Object.fromEntries(['Lian', 'Sera', 'Minato', 'Anger'].map(name => [
    name.toLowerCase(), JSON.parse(fs.readFileSync(path.join(__dirname,
      '../RequiemLantern/Assets/Resources/Combat/' + name + 'ActionsLayout.json'), 'utf8').replace(/^\uFEFF/, ''))
  ]));
  for (const allyId of ['lian', 'sera', 'minato']) for (const reverse of [false, true]) {
    const snapshot = battle();
    snapshot.allies.splice(1, 0, { id: 'sera', name: '세라', hp: 90, maxHp: 100, element: 'Flame', parts: [] });
    env = environment(true, false, snapshot); env.fx.options.layouts = realLayouts;
    const actorId = reverse ? 'anger' : allyId, targetId = reverse ? allyId : 'anger';
    const label = actorId + ' vs ' + targetId;
    const createdEffects = [], makeNode = env.fx.node.bind(env.fx);
    env.fx.node = (classes, ...args) => { createdEffects.push(classes); return makeNode(classes, ...args); };
    const final = copy(snapshot);
    [...final.allies, ...final.enemies].find(unit => unit.id === targetId).hp -= 5;
    // The ally's forecast link is also the clash context for an enemy counterattack.
    const exchange = env.fx.play([
      event('Ignite', '1:anger_arm', 100, allyId), {...event('Clash',targetId,0,actorId),winnerStreak:1}, event('ClashWin',targetId,0,actorId), event('Damage', targetId, 5, actorId)
    ], final);
    await env.until(() => env.stage.dataset.combatPhase === 'contact');
    const contactPoints = [];
    for (const fighter of env.fx.meleePair) {
      const pose = fighter.id === 'anger' && allyId === 'sera' ? 3 : 2;
      assert.equal(fighter.sprite.dataset.frame, String(pose), label + ' contact pose for ' + fighter.id);
      const layout = realLayouts[fighter.id], frame = layout.frames[pose];
      assert.ok(Number.isFinite(frame.contactX) && Number.isFinite(frame.contactY), label + ' missing contact metadata');
      const scale = (fighter.id === 'anger' ? 410 : 380) / layout.bodyHeight;
      const contact = {
        x: parseFloat(fighter.wrapper.style.left) + (frame.contactX - frame.footX) * scale,
        y: parseFloat(fighter.wrapper.style.top) + 440 + (frame.contactY - frame.footY) * scale
      };
      assert.ok(Math.abs(contact.x - 1055) <= 1, label + ' weapon missed horizontal contact: ' + contact.x);
      assert.equal(fighter.wrapper.style.transform, 'translateX(-50%) rotate(0deg)');
      contactPoints.push(contact);
    }
    assert.equal(contactPoints.length, 2, label + ' requires both fighters');
    assert.ok(Math.abs(contactPoints[0].y - contactPoints[1].y) <= 40, label + ' weapon contact height mismatch');
    assert.equal(env.root.querySelectorAll('.fx-beam').length, 0, label + ' contact must be physical');
    await env.drain(); assert.equal(await exchange, true);
    assert.equal(createdEffects.some(classes => classes.split(/\s+/).includes('fx-beam')), false,
      label + ' must not generate a travelling beam in any phase');
  }
  pass('all four real atlas layouts align weapon contact in six directed clashes without travelling beams');

  env = environment(true, true);
  assert.deepEqual(env.images.map(image => image.src), [
    '/combat/LianActions.png', '/combat/SeraActions.png', '/combat/MinatoActions.png', '/combat/AngerActions.png'
  ]);
  const fallback = env.fx.createFighter('lian', 'ally-side'); env.fx.meleePair = [fallback];
  assert.equal(fallback.atlas, false);
  assert.equal(fallback.sprite.style.backgroundImage, "url('/art/lian.png')");
  assert.equal(fallback.sprite.style.backgroundSize, 'contain');
  env.images[0].onload();
  env.fx.pose(fallback, 3, 'contact');
  assert.equal(fallback.sprite.style.backgroundImage, "url('/art/lian.png')");
  assert.equal(fallback.sprite.style.backgroundPosition, 'center bottom');
  env.fx.releaseMelee();
  const loaded = env.fx.createFighter('lian', 'ally-side'); env.fx.meleePair = [loaded];
  assert.equal(loaded.atlas, true);
  assert.equal(loaded.sprite.style.backgroundImage, "url('/combat/LianActions.png')");
  env.fx.pose(loaded, 3, 'contact'); assert.equal(loaded.sprite.style.backgroundPosition, '0% 100%');
  env.fx.releaseMelee();
  env.images[3].onerror();
  const failed = env.fx.createFighter('anger', 'enemy-side'); env.fx.meleePair = [failed];
  env.fx.pose(failed, 5, 'recoil');
  assert.equal(failed.atlas, false);
  assert.equal(failed.sprite.style.backgroundImage, "url('/art/anger.png')");
  assert.equal(failed.sprite.style.backgroundPosition, 'center bottom');
  env.fx.releaseMelee();
  assert.equal(env.fx.portrait('lian').style.visibility, '');
  assert.equal(env.fx.portrait('anger').style.visibility, '');
  pass('pending and failed atlas loads preserve visible art; a newly loaded atlas starts at the next exchange');

  for (const [actor, target] of [['lian', 'anger'], ['anger', 'lian']]) {
    env = environment();
    const fatalFinal = copy(env.initial);
    [...fatalFinal.allies, ...fatalFinal.enemies].find(unit => unit.id === target).hp = 0;
    const fatal = env.fx.play([event('Damage', target, 500, actor)], fatalFinal);
    await env.until(() => env.stage.dataset.combatPhase === 'recover');
    const defender = env.fx.meleePair.find(fighter => fighter.id === target);
    assert.equal(env.fx.units.get(target).hp, 0);
    assert.equal(defender.sprite.dataset.frame, '5'); assert.equal(defender.wrapper.style.opacity, '.25');
    await env.until(() => env.stage.dataset.combatPhase === 'return');
    assert.equal(defender.sprite.dataset.frame, '5'); assert.equal(defender.wrapper.style.opacity, '.25');
    await env.drain(); assert.equal(await fatal, true);
    assert.equal(env.fx.portrait(target).classList.contains('dead'), true);
    assert.equal(env.fx.portrait(target).style.visibility, '');
    assert.equal(env.root.querySelectorAll('.melee-fighter').length, 0);
  }
  pass('defeated allies and enemies retain the hurt pose through recovery and return before dead idle restoration');

  for (const phase of ['windup', 'contact', 'recover']) {
    env = environment();
    const portrait = env.fx.portrait('lian'); portrait.style.visibility = 'visible';
    let appliedDamage = 0, finalSync = 0;
    const apply = env.fx.applyEvent.bind(env.fx), sync = env.fx.sync.bind(env.fx);
    env.fx.applyEvent = e => { if (e.type === 'Damage') appliedDamage++; return apply(e); };
    env.fx.sync = s => { finalSync++; return sync(s); };
    const round = env.fx.play([event('Damage', 'anger', 8)], copy(env.initial));
    await env.until(() => env.stage.dataset.combatPhase === phase);
    env.fx.clear(); await env.drain();
    assert.equal(await round, false); assert.equal(finalSync, 0);
    assert.equal(appliedDamage, phase === 'windup' ? 0 : 1);
    assert.equal(portrait.style.visibility, 'visible');
    assert.equal(env.root.querySelectorAll('.melee-fighter,[data-unit-pose]').length, 0);
    assert.equal(env.stage.classList.contains('melee-active'), false);
    assert.equal(env.stage.dataset.combatPhase, '');
    assert.equal(env.timers.size, 0);
  }
  pass('cancelling windup, contact or recovery restores idle art and never applies a late second hit');

  env = environment();
  const oldRound = env.fx.play([event('Damage', 'anger', 8)], copy(env.initial));
  await env.until(() => env.stage.dataset.combatPhase === 'contact');
  env.fx.clear(); env.fx.attach(battle());
  const newFinal = battle(); newFinal.enemies[0].hp = 180;
  const newRound = env.fx.play([event('Damage', 'anger', 10)], newFinal);
  for(let tick=0;tick<8;tick++)await Promise.resolve();
  assert.equal(await oldRound, false);
  assert.equal(env.root.querySelectorAll('.melee-fighter').length, 2);
  assert.equal(env.fx.portrait('lian').style.visibility, 'hidden');
  assert.equal(env.stage.classList.contains('melee-active'), true);
  await env.drain(); assert.equal(await newRound, true);
  assert.equal(env.fx.units.get('anger').hp, 180); assert.equal(env.fx.portrait('lian').style.visibility, '');
  pass('late cleanup from a cancelled exchange cannot remove a new encounter or reveal its hidden originals');

  env = environment(false);
  const reducedPhases = [], reducedBase = env.fx.setPosePhase.bind(env.fx);
  env.fx.setPosePhase = (pair, phase) => { reducedPhases.push(phase); return reducedBase(pair, phase); };
  const reducedFinal = copy(env.initial); reducedFinal.enemies[0].hp = 181;
  const reducedMelee = env.fx.play([event('Damage', 'anger', 9)], reducedFinal);
  await env.until(() => env.stage.dataset.combatPhase === 'contact');
  assert.equal(env.fx.units.get('anger').hp, 181);
  assert.equal(env.root.querySelectorAll('.fx-spark').length, 0);
  assert.ok(env.root.querySelectorAll('.melee-fighter').every(node => node.style.transform === ''));
  await env.drain(); assert.equal(await reducedMelee, true);
  assert.deepEqual(reducedPhases, ['approach', 'windup', 'swing', 'contact', 'recoil', 'recover', 'return']);
  assert.equal(env.root.querySelectorAll('.melee-fighter').length, 0);
  assert.equal(env.fx.portrait('lian').style.visibility, '');
  pass('reduced motion preserves weapon pose sequence, contact damage and idle restoration');


  env = environment();
  env.initial.allies[0].sanity=0;env.initial.enemies[0].sanity=0;env.fx.attach(env.initial);
  const roll={...event('FlameRoll','anger'),flameStates:[true,false,true],enemyFlameStates:[false,true],actorPower:12,targetPower:9.0000001,actorSanity:0,targetSanity:0,clashIndex:1,basePower:4,flamePower:4,enemyBasePower:5,enemyFlamePower:4};
  const sanityFinal=copy(env.initial);sanityFinal.allies[0].sanity=5;
  const rolling=env.fx.play([roll,event('Sanity','lian',5)],sanityFinal);
  await env.until(()=>env.root.querySelectorAll('.flame-roll').length===1);
  const rollCard=env.root.querySelector('.flame-roll');
  assert.equal(rollCard.querySelectorAll('.unknown').length,5);assert.equal(rollCard.querySelectorAll('.on').length,0);
  await env.until(()=>rollCard.classList.contains('decided'));
  assert.equal(rollCard.querySelectorAll('.on').length,3);assert.equal(rollCard.querySelectorAll('.off').length,2);
  assert.equal(rollCard.querySelectorAll('strong')[0].textContent,'12');assert.equal(rollCard.querySelectorAll('strong')[1].textContent,'9');
  assert.equal(env.fx.units.get('lian').sanity,0);assert.equal(env.fx.units.get('anger').hp,190);
  await env.drain();assert.equal(await rolling,true);assert.equal(env.fx.units.get('lian').sanity,5);
  pass('flame roll displays exact on/off states and both powers while sanity waits for final playback synchronization');

  env = environment();
  const contestEvents=[];
  for(let i=1;i<=4;i++){contestEvents.push({...roll,clashIndex:i});contestEvents.push({...event('Clash','anger'),clashIndex:i,winnerStreak:i===4?2:1});}
  contestEvents.push(event('ClashWin','anger'),event('Damage','anger',25));
  const clashFinal=copy(env.initial);clashFinal.enemies[0].hp=165;
  const appliedTypes=[],apply=env.fx.applyEvent.bind(env.fx);env.fx.applyEvent=e=>{appliedTypes.push(e.type);apply(e);};
  const contested=env.fx.play(contestEvents,clashFinal,{fast:true});
  await env.until(()=>env.impacts.filter(([kind])=>kind==='clash').length===4);
  assert.equal(env.fx.units.get('anger').hp,190);assert.equal(appliedTypes.filter(t=>t==='Damage').length,0);
  await env.drain();assert.equal(await contested,true);
  assert.equal(env.impacts.filter(([kind])=>kind==='clash').length,4);assert.equal(env.impacts.filter(([kind])=>kind==='hit').length,1);
  assert.equal(appliedTypes.filter(t=>t==='Damage').length,1);assert.equal(env.fx.units.get('anger').hp,165);
  assert.equal(env.root.querySelectorAll('.melee-fighter').length,0);
  pass('alternating contests clang without damage until the two-win verdict then strike exactly once');

  const human=battle();human.enemies[0].isHuman=true;human.enemies[0].portrait='Eve';
  env=environment(true,false,human);env.fx.portrait('anger').src='/art/Eve.png';
  const humanFighter=env.fx.createFighter('anger','enemy-side');
  assert.equal(humanFighter.atlas,false);assert.equal(humanFighter.layout,null);
  assert.equal(humanFighter.sprite.style.backgroundImage,"url('/art/Eve.png')");env.fx.releaseMelee();
  pass('human enemy retains its own portrait and never uses the Anger action atlas');

  const support=battle();support.allies[0].id='heidel';support.allies[0].portrait='Heidel';
  env=environment(true,false,support);env.fx.portrait('heidel').src='/art/Heidel.png';
  const supportFighter=env.fx.createFighter('heidel','ally-side');
  assert.equal(supportFighter.atlas,false);assert.ok(supportFighter.sprite.style.backgroundImage.includes('Heidel.png'));
  pass('support character with no atlas uses its own portrait instead of a different character');

  env=environment();
  const interruptedClash=env.fx.play([{...event('Clash','anger'),winnerStreak:1},event('Damage','anger',25)],clashFinal);
  await env.until(()=>env.stage.dataset.combatPhase==='contact');env.fx.clear();await env.drain();
  assert.equal(await interruptedClash,false);assert.equal(env.fx.units.get('anger').hp,190);
  assert.equal(env.root.querySelectorAll('.melee-fighter').length,0);assert.equal(env.fx.portrait('lian').style.visibility,'');
  pass('cancelling a pending weapon contest restores portraits and suppresses the future strike');


  env=environment();
  const reveal=env.fx.play([roll],copy(env.initial));
  await env.until(()=>env.root.querySelectorAll('.on').length===1);
  let lamps=env.root.querySelector('.flame-roll');
  assert.equal(lamps.querySelectorAll('.unknown').length,3);assert.equal(lamps.querySelectorAll('.off').length,1);
  assert.equal(lamps.querySelectorAll('strong')[0].textContent,'8');assert.equal(lamps.querySelectorAll('strong')[1].textContent,'5');
  assert.equal(env.fx.units.get('anger').hp,190);assert.ok(env.sounds.length>=2);
  await env.drain();assert.equal(await reveal,true);
  pass('both sides reveal one authoritative flame at a time and accumulate affinity-adjusted power without early damage');

  env=environment();
  const suspense=env.fx.play([{...roll,clashIndex:2,actorPower:9,targetPower:9},event('Damage','anger',8)],copy(env.initial));
  await env.until(()=>env.root.querySelectorAll('.flicker').length>0);
  assert.ok(env.stage.classList.contains('tension-roll'));env.fx.clear();await env.drain();
  assert.equal(await suspense,false);assert.equal(env.fx.units.get('anger').hp,190);
  assert.equal(env.root.querySelectorAll('.flame-roll').length,0);assert.equal(env.stage.classList.contains('tension-roll'),false);
  pass('cancelling during unrevealed flame suspense removes focus and suppresses the later attack');

  env=environment(false);
  const stillRoll=env.fx.play([roll],copy(env.initial));
  await env.until(()=>env.root.querySelectorAll('.flame-roll').length>0);
  assert.equal(env.root.querySelectorAll('.on').length,3);assert.equal(env.root.querySelectorAll('.off').length,2);
  assert.equal(env.root.querySelectorAll('.flicker').length,0);assert.equal(env.stage.classList.contains('tension-roll'),false);
  await env.drain();assert.equal(await stillRoll,true);
  pass('reduced motion shows the complete real flame decision without flicker or camera emphasis');

  env=environment();
  const closeRolls=Array.from({length:6},(_,index)=>({...roll,clashIndex:index+1,actorPower:9,targetPower:9}));
  const emphasis=env.fx.play(closeRolls,copy(env.initial));await env.drain();assert.equal(await emphasis,true);
  assert.equal(env.fx.tensionCount,2);assert.ok(env.fx.presentationElapsed<4000);
  pass('only two close decisions per round receive long suspense; the remaining rolls keep a brisk pace');

  for(const [id,style,extra] of [['lian','shield-blow','shield-impact'],['sera','double-slash','feint-cut'],['minato','staff-wave','staff-wave']]){
   const snapshot=battle();if(id==='sera')snapshot.allies.push({id:'sera',name:'세라',hp:100,maxHp:100,element:'Flame',parts:[]});
   env=environment(true,false,snapshot);let hits=0;const applyDamage=env.fx.applyEvent.bind(env.fx);
   env.fx.applyEvent=e=>{if(e.type==='Damage')hits++;applyDamage(e);};
   const distinct=env.fx.play([{...event('Damage','anger',12,id),skillId:id+'_skill',skillNumber:2}],copy(snapshot));
   if(id==='sera'){await env.until(()=>env.stage.dataset.combatPhase==='feint');assert.equal(hits,0);assert.equal(env.fx.units.get('anger').hp,190);}
   await env.until(()=>env.stage.dataset.combatPhase==='contact');assert.equal(env.stage.dataset.attackStyle,style);assert.equal(hits,1);
   assert.ok(env.root.querySelectorAll('.'+extra).length>0);await env.drain();assert.equal(await distinct,true);assert.equal(hits,1);
  }
  pass('Lian braces a heavy shield strike, Sera feints two slashes and Minato sends a staff ripple with one authoritative contact hit each');

  env=environment();
  const longEvents=Array.from({length:90},(_,index)=>({...event('Clash','anger'),clashIndex:index+1,winnerStreak:1}));
  const bounded=env.fx.play(longEvents,copy(env.initial));await env.drain();assert.equal(await bounded,true);
  assert.ok(env.fx.presentationElapsed<=28000);assert.equal(env.fx.units.get('anger').hp,190);
  pass('exceptionally long clash playback compresses its tail into the 28-second presentation budget without inventing damage');


  env=environment();
  const secretRoll={...roll,message:'리안 · 불꽃 2/3 · 위력 12'};
  const hiddenOutcome=env.fx.play([secretRoll],copy(env.initial));
  await env.until(()=>env.root.querySelectorAll('.unknown').length>0);
  assert.equal(env.root.querySelector('.combat-caption').querySelector('span').textContent,'불과 어둠의 위력을 판정합니다…');
  assert.ok(!env.root.querySelector('.combat-caption').querySelector('span').textContent.includes('위력 12'));
  await env.until(()=>env.root.querySelector('.flame-roll')?.classList.contains('decided'));
  assert.equal(env.root.querySelector('.combat-caption').querySelector('span').textContent,secretRoll.message);
  await env.drain();assert.equal(await hiddenOutcome,true);
  pass('the status banner withholds final flame counts and power until every actual flame is revealed');

  env=environment();
  const counterSnapshot=copy(env.initial);counterSnapshot.allies.push({id:'heidel',name:'하이델',hp:120,maxHp:120,element:'Frost',parts:[]});env=environment(true,false,counterSnapshot);
  const enemyRoll={...roll,actorId:'anger',targetId:'lian',flameStates:[true],enemyFlameStates:[],actorPower:7,clashIndex:0};
  const counter=env.fx.play([enemyRoll,{...event('Damage','anger',8,'heidel'),skillId:'heidel_counter',skillNumber:0}],copy(counterSnapshot));
  await env.until(()=>env.stage.dataset.combatPhase==='windup');
  assert.equal(env.root.querySelectorAll('.flame-roll').length,0);await env.drain();assert.equal(await counter,true);
  pass('a counterattack clears a previous enemy-only roll panel instead of showing the wrong attacker power');

  console.log('TOTAL ' + passed + ' combat FX regression cases passed');
}

main().catch(error => { console.error(error); process.exitCode = 1; });
