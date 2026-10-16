// Exercises the actual Preview UI script against an isolated real HTTP host.
// A successful server response is consumed and then deliberately thrown away.
// No outcome, currency, character ownership or battle ID is supplied by the test.
// Run with a disposable host on 8128:
// node Tests/preview-response-loss-http.cjs http://127.0.0.1:8128 QA/.../response-loss-http.json
'use strict';
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const {environment}=require('./preview-regression.cjs');
const base=process.argv[2]||'http://127.0.0.1:8128';
assert.equal(base,'http://127.0.0.1:8128','Only the isolated 8128 QA host may be mutated.');
const output=process.argv[3]||'QA/SkillFlavor/2026-10-16/Browser/response-loss-http.json';
const records=[];
async function request(action,fields={}){
 const response=await fetch(base+(action?'/api/action':'/api/state'),action?{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({action,...fields})}:{});
 const snapshot=await response.json();assert.equal(response.ok,true,snapshot.error||action);return snapshot;
}
function ui(snapshot){const env=environment();env.run('data=normalizeState('+JSON.stringify(snapshot)+');tone=()=>{};showResult();');return env;}
function loseOneSuccessfulReply(env,action){
 let dropped=false;const calls=[];
 env.context.fetch=async(url,options)=>{
  const response=await fetch(base+url,options),snapshot=await response.json(),name=options?.body?JSON.parse(options.body).action:'state';
  calls.push({action:name,status:response.status});
  if(name===action&&!dropped&&response.ok){dropped=true;throw Error('QA response lost after the server saved '+action);}
  return{ok:response.ok,json:async()=>snapshot};
 };
 return calls;
}
async function win(){
 let state=await request();
 for(let i=0;i<40&&state.battle.outcome==='Ongoing';i++){
  await request('recommend');state=await request('resolve',{grades:{}});
  if(state.battle.outcome==='Ongoing')state=await request('next');
 }
 assert.equal(state.battle.outcome,'Victory');return state;
}
const walletKeys=['coins','stardust','lanternFragments','memoryFragments','characterShards'];
function wallet(state){return Object.fromEntries(walletKeys.map(key=>[key,state.progress[key]]));}
function delta(before,after,expected){for(const key of walletKeys)assert.equal(after.progress[key]-before.progress[key],expected[key]||0,key+' must be paid exactly once');}
async function campaignLoss(){
 let state=await request();assert.equal(state.application,'RequiemLantern');assert.equal(state.runtimeRevision,'skill-flavor-v16');
 assert.deepEqual(state.recruitment.party,['lian','minato','heidel','mare']);
 const stages=state.campaign.chapters.flatMap(chapter=>chapter.stages),first=stages.find(stage=>stage.number==='1-1'),stage=stages.find(stage=>stage.number==='1-2');
 if(!first.cleared){await request('stage-enter',{stageId:first.id});await request('stage-story',{stageId:first.id,expectedPhase:'BeforeStory'});await request('stage-exit');}
 await request('stage-enter',{stageId:stage.id});await request('stage-story',{stageId:stage.id,expectedPhase:'BeforeStory'});
 const before=await win(),id=before.battle.battleId,env=ui(before),calls=loseOneSuccessfulReply(env,'reward');
 await env.context.retryBattleReward();assert.equal(env.run('battleRewardSavePending'),true);assert.ok(env.game.innerHTML.includes('보상 저장 다시 시도'));
 const saved=await request();assert.ok(saved.progress.claimedBattles.includes(id));assert.equal(saved.campaign.phase,'AfterStory');
 await env.context.retryBattleReward();assert.equal(env.run('battleRewardSavePending'),false);assert.equal(env.run('unclaimedBattleVictory()'),false);assert.ok(env.game.innerHTML.includes('후속 이야기 →'));
 const after=await request();assert.equal(after.progress.claimedBattles.filter(value=>value===id).length,1);assert.deepEqual(wallet(after),wallet(saved));assert.equal(after.activities.weeklyQuests[0].current,before.activities.weeklyQuests[0].current+1);
 env.context.routeCampaign();assert.equal(env.run('page'),'story');
 assert.deepEqual(calls,[{action:'reward',status:200},{action:'reward',status:400}]);
 records.push({scenario:'campaign reward committed, reply lost, retry 400 receipt restores after-story',battleId:id,calls,before:wallet(before),after:wallet(after),weeklyVictories:after.activities.weeklyQuests[0].current,claimedOccurrences:1});
 await request('stage-story',{stageId:stage.id,expectedPhase:'AfterStory'});await request('stage-exit');
}
async function dungeonLoss(practice){
 await request('dungeon-enter',{dungeonId:'coast',practice});const before=await win(),id=before.battle.battleId,env=ui(before),calls=loseOneSuccessfulReply(env,'dungeon-reward');
 await env.context.claimDungeonReward();assert.equal(env.run('page'),'result');assert.equal(env.run('data.dungeon.activeId'),'coast');
 const saved=await request();assert.equal(saved.dungeon.activeId,null);assert.ok(saved.progress.claimedBattles.includes(id));
 await env.context.claimDungeonReward();assert.equal(env.run('page'),'dungeons');assert.equal(env.run('data.dungeon.activeId'),null);assert.equal(env.run('unclaimedBattleVictory()'),false);assert.equal(env.modalHtml,'');
 const after=await request();assert.deepEqual(wallet(after),wallet(saved));assert.equal(after.progress.claimedBattles.filter(value=>value===id).length,1);
 delta(before,after,practice?{}:before.dungeon.active.reward);
 assert.equal(after.activities.weeklyDungeonRewardsClaimed,before.activities.weeklyDungeonRewardsClaimed+(practice?0:1));
 assert.equal(after.activities.weeklyQuests[0].current,before.activities.weeklyQuests[0].current+(practice?0:1));
 if(practice)assert.deepEqual(after.activities,before.activities);
 assert.deepEqual(calls,[{action:'dungeon-reward',status:200},{action:'dungeon-reward',status:400},{action:'state',status:200}]);
 records.push({scenario:practice?'practice committed, reply lost, exited session recovered without rewards or quest progress':'dungeon reward committed, reply lost, exited session recovered without second payout',battleId:id,calls,before:wallet(before),after:wallet(after),weeklyDungeonClaims:after.activities.weeklyDungeonRewardsClaimed,claimedOccurrences:1});
}
async function materialLoss(){
 await request('material-enter',{actorId:'sera'});const before=await win(),id=before.battle.battleId,env=ui(before),calls=loseOneSuccessfulReply(env,'material-reward');
 assert.equal(before.materials.entries.find(entry=>entry.characterId==='sera').owned,false);
 await env.context.claimMaterialReward();assert.equal(env.run('page'),'result');assert.equal(env.run('data.materialDungeon.activeCharacterId'),'sera');
 const saved=await request();assert.equal(saved.materialDungeon.activeCharacterId,null);assert.ok(saved.progress.claimedBattles.includes(id));
 await env.context.claimMaterialReward();assert.equal(env.run('page'),'materials');assert.equal(env.run('data.materialDungeon.activeCharacterId'),null);assert.equal(env.run('unclaimedBattleVictory()'),false);assert.equal(env.modalHtml,'');
 const after=await request();assert.deepEqual(wallet(after),wallet(before));assert.equal(after.progress.claimedBattles.filter(value=>value===id).length,1);
 const count=value=>value.materials.entries.find(entry=>entry.characterId==='sera').count;
 assert.equal(count(after)-count(before),3);assert.equal(count(after),count(saved));assert.equal(after.activities.weeklyDungeonRewardsClaimed,before.activities.weeklyDungeonRewardsClaimed);assert.equal(after.activities.weeklyQuests[0].current,before.activities.weeklyQuests[0].current+1);
 assert.deepEqual(calls,[{action:'material-reward',status:200},{action:'material-reward',status:400},{action:'state',status:200}]);
 records.push({scenario:'unowned character material saved, reply lost, exited session recovered without second drop or weekly quota use',battleId:id,calls,materialCharacter:'sera',beforeCount:count(before),afterCount:count(after),weeklyDungeonClaims:after.activities.weeklyDungeonRewardsClaimed,claimedOccurrences:1});
}
async function main(){
 await campaignLoss();await dungeonLoss(false);await dungeonLoss(true);await materialLoss();
 const report={status:'PASS',runtimeRevision:'skill-flavor-v16',isolatedPort:8128,scenarios:records,uiUsesActualAppScript:true,lossInjectedAfterServerCompletedRequest:true};
 fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(output,JSON.stringify(report,null,2)+'\n');
 console.log('PASS actual HTTP response loss: campaign reward, dungeon reward, practice and material drop, each committed once and recovered through retry snapshots.');
}
main().catch(error=>{console.error(error);process.exitCode=1;});
