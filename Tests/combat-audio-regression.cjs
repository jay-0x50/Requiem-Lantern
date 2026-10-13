// Real audio-bank code with isolated Web Audio/fetch stubs; no hardware claim.
// Run: node Tests/combat-audio-regression.cjs
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const app=fs.readFileSync(path.join(__dirname,'../Preview/app.js'),'utf8');
const source=app.slice(app.indexOf('// Shared sampled combat audio.'),app.indexOf('function toast('));

function environment(options={}){
 const requests=[],contexts=[],pending=[],decodes=[];
 class AudioContext{
  constructor(){this.currentTime=4.25;this.state='running';this.destination={};this.gains=[];this.sources=[];contexts.push(this);}
  createGain(){const gain={gain:{value:0,events:[],setValueAtTime(value,time){this.events.push({value,time});},linearRampToValueAtTime(value,time){this.events.push({value,time});}},connect(target){this.target=target;},disconnect(){}};this.gains.push(gain);return gain;}
  createBufferSource(){const source={connect(target){this.target=target;},disconnect(){this.disconnected=true;},start(time){this.started=time;},stop(time){this.stopCalls=(this.stopCalls||0)+1;this.stopped=time;}};this.sources.push(source);return source;}
  createOscillator(){throw Error('Combat audio must not synthesize an oscillator fallback');}
  resume(){this.state='running';return Promise.resolve();}
  decodeAudioData(bytes){decodes.push(bytes.url);const buffer={key:bytes.url.split('/').pop(),duration:.38};return options.pending?new Promise(done=>pending.push(()=>done(buffer))):Promise.resolve(buffer);}
 }
 const context={AudioContext,console,fetch:async url=>{requests.push(url);if(options.throwFetch)throw Error('missing sample');return{ok:!options.failed?.(url),arrayBuffer:async()=>({url})};}};
 vm.createContext(context);vm.runInContext('let context,combatAudio;let settings={sound:true};'+source,context);
 return{requests,contexts,pending,decodes,run:code=>vm.runInContext(code,context),async preload(){this.run('unlockCombatAudio()');await this.run('combatAudio.loading');},async ticks(){for(let n=0;n<8;n++)await Promise.resolve();}};
}

let passed=0;const pass=label=>{passed++;console.log('PASS '+label);};
async function main(){
 let env=environment();
 assert.equal(env.requests.length,0);assert.equal(env.contexts.length,0);
 env.run('unlockCombatAudio();unlockCombatAudio();');await env.run('combatAudio.loading');
 assert.equal(env.contexts.length,1);assert.equal(env.requests.length,24);assert.equal(env.decodes.length,24);assert.equal(new Set(env.requests).size,24);
 env.run('unlockCombatAudio()');await env.run('combatAudio.loading');assert.equal(env.requests.length,24);
 pass('gesture unlock preloads and decodes each shared sample exactly once across repeated clicks');

 const bank=env.run('combatAudio');
 for(let i=0;i<4;i++)assert.equal(env.run("combatSound('clash',0)"),true);
 assert.deepEqual(env.contexts[0].sources.map(s=>s.buffer.key),['clash_01.wav','clash_02.wav','clash_03.wav','clash_01.wav']);
 assert.ok(env.contexts[0].sources.every(s=>s.started===4.25));
 pass('cached variants cycle without immediate repeats and start at the current contact time');
 bank.stop();

 const cases=[['sera',1,25,false,'hit_cut'],['noa',3,8,false,'hit_cut'],['minato',3,30,false,'hit_blunt'],['lian',1,10,false,'hit_blunt'],['lian',2,10,false,'hit_heavy'],['heidel',1,18,false,'hit_heavy'],['enemy_warden',2,9,true,'hit_heavy'],['enemy_warden',1,9,true,'hit_blunt']];
 for(const [actorId,skillNumber,amount,isEnemy,family] of cases){
  assert.equal(env.run(`combatSound('hit',${amount},${JSON.stringify({actorId,skillNumber,isEnemy})})`),true);
  assert.ok(env.contexts[0].sources.at(-1).buffer.key.startsWith(family+'_'));bank.stop();
 }
 assert.equal(env.run("combatFamily('clash',90,{actorId:'sera',skillNumber:3})"),'clash');
 pass('weapon clashes, cut attacks, blunt attacks and strong shield/enemy hits select distinct sample families');

 env=environment({pending:true});env.run('unlockCombatAudio()');await env.ticks();
 assert.equal(env.pending.length,24);assert.equal(env.run("combatSound('hit',20,{actorId:'lian',skillNumber:2})"),false);assert.equal(env.contexts[0].sources.length,0);
 env.pending.forEach(done=>done());await env.run('combatAudio.loading');assert.equal(env.contexts[0].sources.length,0);
 assert.equal(env.run("combatSound('hit',20,{actorId:'lian',skillNumber:2})"),true);
 pass('unfinished decoding skips the contact sound and never replays it late when loading finishes');

 env=environment({failed:url=>url.includes('/clash_')||url.endsWith('hit_cut_01.wav')});await env.preload();
 assert.equal(env.run("combatSound('clash')"),false);assert.equal(env.contexts[0].sources.length,0);
 for(let i=0;i<3;i++)env.run("combatSound('hit',8,{actorId:'sera'})");
 assert.deepEqual(env.contexts[0].sources.map(s=>s.buffer.key),['hit_cut_02.wav','hit_cut_03.wav','hit_cut_02.wav']);
 pass('missing samples remain silent or reuse only available variants from the same family without tonal fallback');

 env=environment();await env.preload();env.run("combatSound('clash');settings.sound=false;");
 const priorStops=env.contexts[0].sources[0].stopCalls||0;
 assert.equal(env.run("combatSound('hit',30,{actorId:'lian'})"),false);assert.equal(env.contexts[0].sources.length,1);
 env.run('combatAudio.stop()');assert.equal(env.run('combatAudio.voices.size'),0);assert.ok(env.contexts[0].sources[0].stopCalls>priorStops);assert.equal(env.contexts[0].sources[0].stopped,undefined);assert.equal(env.contexts[0].sources[0].disconnected,true);
 env.run('settings.sound=true;context.state="suspended";');assert.equal(env.run("combatSound('clash')"),false);
 pass('mute rejects new combat sounds and cancellation immediately stops active tails; a suspended context never plays');

 env=environment();await env.preload();for(let i=0;i<8;i++)assert.equal(env.run("combatSound('hit',30,{actorId:'lian'})"),true);
 assert.equal(env.run("combatSound('clash')"),false);assert.equal(env.contexts[0].sources.length,8);
 const master=env.contexts[0].gains[0].gain.value,currentGain=s=>s.target.target.gain.events.at(-1).value;
 assert.equal(master,.8);assert.ok(env.contexts[0].sources.every(s=>currentGain(s)===.58/8));
 assert.ok(Math.abs(master*env.contexts[0].sources.reduce((sum,s)=>sum+currentGain(s),0)-.464)<1e-9);
 env.contexts[0].sources[0].onended();assert.equal(env.run('combatAudio.voices.size'),7);
 assert.ok(env.contexts[0].sources.slice(1).every(s=>currentGain(s)===.58/7));assert.equal(env.run("combatSound('clash')"),true);
 assert.ok(master*env.contexts[0].sources.slice(1).reduce((sum,s)=>sum+currentGain(s),0)<.464);
 env.run('combatAudio.stop()');assert.equal(env.run('combatAudio.voices.size'),0);
 pass('eight-voice cap and per-active-voice balancing bound the worst peak at .464 and rebalance surviving tails when a sample ends');

 env=environment();await env.preload();env.run("combatSound('ignite',0,{level:.25})");
 assert.equal(env.contexts[0].sources[0].target.target.gain.events.at(-1).value,.05);env.run('combatAudio.stop();');
 env.run("combatSound('hit',30,{actorId:'lian',level:99})");assert.equal(env.contexts[0].sources[1].target.target.gain.events.at(-1).value,.58);
 pass('optional level keeps flame clicks quiet and clamps metadata so it cannot amplify combat above the family limit');

 console.log('TOTAL '+passed+' combat audio regression cases passed');
}
main().catch(error=>{console.error(error);process.exitCode=1;});
