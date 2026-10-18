"""Test character materials and real farming battles using an isolated HTTP host."""
from pathlib import Path
import argparse, json, shutil, subprocess, time, urllib.request, urllib.error, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent.parent
DOTNET=shutil.which('dotnet')
if not DOTNET: raise SystemExit('Install .NET 10 SDK and reopen the terminal before running HTTP checks.')
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--host',type=Path,default=ROOT/'PreviewHost/bin/material-qa/PreviewHost.dll')
args=parser.parse_args()
QA=ROOT/'QA/SkillFlavor/2026-10-16/MaterialAPI'
QA.mkdir(parents=True,exist_ok=True)
SAVE=QA/('progress-'+str(time.time_ns())+'.xml')
PORT=8132
process=None
checks=[]
log=(QA/'host.log').open('w',encoding='utf-8')

def call(action=None,status=200,**fields):
    body=None if action is None else json.dumps(dict(action=action,**fields)).encode()
    request=urllib.request.Request(f'http://127.0.0.1:{PORT}/api/'+('state' if action is None else 'action'),data=body,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(request,timeout=10) as response: actual=response.status; result=json.load(response)
    except urllib.error.HTTPError as error: actual=error.code; result=json.load(error)
    assert actual==status,(action,actual,result.get('error'))
    return result

def stop():
    global process
    if process is None:return
    process.terminate()
    try:process.wait(timeout=5)
    except subprocess.TimeoutExpired:process.kill();process.wait()
    process=None

def start():
    global process
    process=subprocess.Popen([DOTNET,str(args.host.resolve()),str(ROOT),str(SAVE),str(PORT)],stdout=log,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    for _ in range(60):
        if process.poll() is not None:raise RuntimeError('Isolated host exited')
        try:return call()
        except urllib.error.URLError:time.sleep(.2)
    raise RuntimeError('Isolated host did not become ready')

def rejected(before,action,**fields):
    result=call(action,400,**fields)
    assert result['progress']==before['progress'],action
    assert result['materialReward'] is None and result['couponReward'] is None
    return result

def failed_save(before,action,**fields):
    pending=Path(str(SAVE)+'.tmp');pending.mkdir()
    try:return rejected(before,action,**fields)
    finally:pending.rmdir()

def count(state,id):
    return next((s['count'] for s in state['progress']['syncMaterials'] if s['characterId']==id),0)

def entry(state,id):return next(e for e in state['materials']['entries'] if e['characterId']==id)
def growth(state,id):return next((c['syncLevel'] for c in state['progress']['characters'] if c['id']==id),0)
def wallet(state):return {k:state['progress'][k] for k in ['coins','stardust','lanternFragments','memoryFragments','characterShards']}
def quest(state,id):return next(q for q in state['activities']['dailyQuests']+state['activities']['weeklyQuests'] if q['id']==id)

def fight():
    state=call()
    for _ in range(50):
        if state['battle']['outcome']!='Ongoing':break
        call('recommend');state=call('resolve')
        if state['battle']['outcome']=='Ongoing':state=call('next')
    assert state['battle']['outcome']=='Victory',(state['battle']['stageName'],state['battle']['round'])
    return state

def farm(id):
    before=call();state=call('material-enter',actorId=id)
    assert state['materialDungeon']['activeCharacterId']==id
    assert state['battle']['stageId']=='material_'+id and len(state['battle']['enemies'])==2
    assert [u['id'] for u in state['battle']['allies']]==before['recruitment']['party']
    state=fight();state=call('material-reward')
    assert state['materialReward']=={'characterId':id,'materialName':entry(state,id)['materialName'],'count':3}
    assert count(state,id)==count(before,id)+3 and wallet(state)==wallet(before)
    assert not state['materialDungeon']['activeCharacterId']
    assert state['progress']['clearedStages']==before['progress']['clearedStages']
    assert state['progress']['rewardedStages']==before['progress']['rewardedStages']
    assert state['activities']['weeklyDungeonRewardsClaimed']==before['activities']['weeklyDungeonRewardsClaimed']
    return state

def collect_quest(id):
    state=call();q=quest(state,id);assert q['canClaim'],id
    return call('quest-claim',questId=id,periodKey=q['periodKey'])

try:
    state=start();assert state['runtimeRevision']=='skill-flavor-v16'
    assert not state['materials']['unlocked'] and len(state['materials']['entries'])==7
    assert len(set(e['materialName'] for e in state['materials']['entries']))==7
    assert all(e['count']==0 and e['dropCount']==3 for e in state['materials']['entries'])
    assert entry(state,'lian')['requiredCount']==10 and entry(state,'minato')['requiredCount']==6
    for action,fields in [('material-enter',{'actorId':'lian'}),('material-enter',{'actorId':'bad'}),('material-reward',{}),('synchronize',{'actorId':'lian'})]:rejected(state,action,**fields)
    checks.append('Fresh seven unique materials, rarity costs and locked/invalid requests preserve all progress.')

    call('stage-enter',stageId='c1s01');call('stage-story',stageId='c1s01',expectedPhase='BeforeStory');call('stage-exit')
    call('stage-enter',stageId='c1s02');call('stage-story',stageId='c1s02',expectedPhase='BeforeStory');fight();state=call('reward')
    assert not state['materials']['unlocked']
    rejected(state,'material-enter',actorId='lian')
    call('stage-story',stageId='c1s02',expectedPhase='AfterStory');state=call('stage-exit')
    assert state['materials']['unlocked']
    checks.append('Actual starter-party 1-2 victory and authored after-story unlock farming only after story completion.')

    state=call('material-enter',actorId='lian');before=state
    for action,fields in [('stage-enter',{'stageId':'c1s02'}),('dungeon-enter',{'dungeonId':'coast'}),('material-enter',{'actorId':'sera'}),('synchronize',{'actorId':'lian'}),('redeem-code',{'code':'LANTERN-SYNC-MATERIALS'}),('equip-character',{'actorId':'minato','slot':0})]:rejected(state,action,**fields)
    state=call('restart');assert state['battle']['stageId']=='material_lian' and state['battle']['round']==1
    state=call('material-exit');assert state['progress']==before['progress']
    checks.append('Campaign/weekly/material sessions cannot overlap; growth/inventory edits reject during farming, restart stays on its route and retreat gives nothing.')

    ids=[e['characterId'] for e in state['materials']['entries']]
    for id in ids:state=farm(id)
    for _ in range(3):state=farm('lian')
    assert count(state,'lian')==12 and all(count(state,id)==3 for id in ids if id!='lian')
    assert not entry(state,'sera')['owned'] and count(state,'sera')==3
    assert state['progress']['activities']['weeklyVictories']==10 and state['activities']['weeklyDungeonRewardsClaimed']==0
    rejected(state,'reward');rejected(state,'material-reward')
    checks.append('All seven routes award exactly three selected materials, including unowned pre-farming; repeat runs count actual quest victories without weekly quota or main rewards.')

    rejected(state,'synchronize',actorId='lian') # Target materials alone do not replace common lanterns.
    call('activity-check-in')
    for id in ['daily_checkin','daily_victory','daily_clash']:state=collect_quest(id)
    call('dungeon-enter',dungeonId='coast');fight();state=call('dungeon-reward')
    assert state['progress']['lanternFragments']>=10 and count(state,'lian')==12
    before=state;state=call('synchronize',actorId='lian')
    assert growth(state,'lian')==1 and count(state,'lian')==2
    assert state['progress']['lanternFragments']==before['progress']['lanternFragments']-10
    assert all(count(state,id)==count(before,id) for id in ids if id!='lian')
    rejected(state,'synchronize',actorId='lian');rejected(state,'synchronize',actorId='sera')
    checks.append('A coupon-free earned path pays ten Lian materials plus ten common lanterns once; other characters materials and ownership remain intact.')

    before=call();state=call('material-enter',actorId='minato');state=fight();battle_id=state['battle']['battleId']
    failed=failed_save(state,'material-reward')
    assert failed['materialDungeon']['activeCharacterId']=='minato' and failed['battle']['outcome']=='Victory'
    state=call('material-reward');assert count(state,'minato')==6
    assert state['progress']['claimedBattles'].count(battle_id)==1
    rejected(state,'material-reward')
    state=collect_quest('weekly_victory')
    before=state;failed_save(state,'synchronize',actorId='minato')
    assert growth(call(),'minato')==0 and count(call(),'minato')==6
    state=call('synchronize',actorId='minato');assert growth(state,'minato')==1 and count(state,'minato')==0
    assert state['progress']['lanternFragments']==before['progress']['lanternFragments']-10
    assert count(state,'lian')==2
    saved=state['progress'];stop();state=start();assert state['progress']==saved
    checks.append('Real save-path failures restore farming receipt, quest progress, synchronization costs and growth; same victory retries once and exact data survives restart.')

    state=call('redeem-code',code='LANTERN-MATERIALS')
    assert state['couponReward']['syncMaterials'] is None
    assert count(state,'lian')==2 and count(state,'minato')==0
    failed_save(state,'redeem-code',code='LANTERN-SYNC-MATERIALS')
    stop();tree=ET.parse(SAVE);stack=next(s for s in tree.find('SyncMaterials') if s.findtext('CharacterId')=='lian');stack.find('Count').text=str(2147483647-9999998);tree.write(SAVE,encoding='utf-8',xml_declaration=True)
    state=start();rejected(state,'redeem-code',code='LANTERN-SYNC-MATERIALS')
    assert 'LANTERN-SYNC-MATERIALS' not in state['progress']['redeemedCodes']
    stop();tree=ET.parse(SAVE);stack=next(s for s in tree.find('SyncMaterials') if s.findtext('CharacterId')=='lian');stack.find('Count').text='2';tree.write(SAVE,encoding='utf-8',xml_declaration=True)
    state=start();before=state;state=call('redeem-code',code=' lantern-sync-materials ')
    assert len(state['couponReward']['syncMaterials'])==7 and wallet(state)==wallet(before)
    assert all(count(state,id)==count(before,id)+9999999 for id in ids)
    rejected(state,'redeem-code',code='LANTERN-SYNC-MATERIALS')
    saved=state['progress'];stop();state=start();assert state['progress']==saved
    checks.append('Separate test coupon grants seven inventories atomically once; existing coupon grants no new material, real save failure/one-stack overflow grant nothing and retry persists.')

    # Simulate a real old XML that has synchronized characters but no new inventory field.
    stop();tree=ET.parse(SAVE);tree.getroot().remove(tree.find('SyncMaterials'));tree.write(SAVE,encoding='utf-8',xml_declaration=True)
    state=start();assert state['progress']['syncMaterials']==[]
    assert growth(state,'lian')==growth(state,'minato')==1
    for key in saved:
        if key!='syncMaterials':assert state['progress'][key]==saved[key],key
    assert entry(state,'lian')['synchronized'] and entry(state,'minato')['synchronized']
    checks.append('Legacy XML without the inventory loads with empty materials while preserving every existing field, including completed synchronization and unlocked third skills.')

    report={'status':'PASS','runtimeRevision':'skill-flavor-v16','port':PORT,'checks':checks,'playerSaveModified':False,'actualFarmWins':11,'processStopped':True}
    (QA/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for check in checks:print('PASS:',check)
finally:
    stop();log.close()
