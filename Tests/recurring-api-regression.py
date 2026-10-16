"""Exercise recurring income against a real isolated host and an explicit QA clock."""
from pathlib import Path
import argparse, json, subprocess, time, urllib.request, urllib.error

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--host',type=Path,default=ROOT/'PreviewHost/bin/recurring-qa/PreviewHost.dll')
args=parser.parse_args()
QA=ROOT/'QA/SkillFlavor/2026-10-16/RecurringAPI'
QA.mkdir(parents=True,exist_ok=True)
SAVE=QA/('progress-'+str(time.time_ns())+'.xml')
CLOCK=QA/'clock.txt'
PORT=8131
process=None
checks=[]
log=(QA/'host.log').open('w',encoding='utf-8')

def set_time(value):
    CLOCK.write_text(value,encoding='utf-8')

def call(action=None,status=200,**fields):
    body=None if action is None else json.dumps(dict(action=action,**fields)).encode()
    req=urllib.request.Request(f'http://127.0.0.1:{PORT}/api/'+('state' if action is None else 'action'),data=body,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=10) as response:
            actual=response.status;result=json.load(response)
    except urllib.error.HTTPError as error:
        actual=error.code;result=json.load(error)
    assert actual==status,(action,actual,result.get('error'))
    return result

def start():
    global process
    process=subprocess.Popen(['C:/Program Files/dotnet/dotnet.exe',str(args.host.resolve()),str(ROOT),str(SAVE),str(PORT),str(CLOCK)],stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
    for _ in range(80):
        if process.poll() is not None:raise RuntimeError('The isolated host exited. See QA host.log.')
        try:return call()
        except urllib.error.URLError:time.sleep(.15)
    raise RuntimeError('Host startup timed out')

def stop():
    global process
    if process is None:return
    process.terminate()
    try:process.wait(timeout=5)
    except subprocess.TimeoutExpired:process.kill();process.wait()
    process=None

def unchanged(before,action,**fields):
    result=call(action,400,**fields)
    assert result['progress']==before['progress'],action
    assert result['activityReward'] is None and result['dungeonPrize'] is None
    return result

def failed_save(before,action,**fields):
    pending=Path(str(SAVE)+'.tmp');pending.mkdir()
    try:return unchanged(before,action,**fields)
    finally:pending.rmdir()

def quest(data,id):
    return next(q for q in data['activities']['dailyQuests']+data['activities']['weeklyQuests'] if q['id']==id)

def claim(data,id):
    q=quest(data,id)
    return call('quest-claim',questId=id,periodKey=q['periodKey'])

def fight(defend=True):
    data=call()
    if defend:
        for actor in data['battle']['allies']:
            defense=next(s for s in actor['skills'] if s['number']==0)
            data=call('link',actorId=actor['id'],slot=0,skillId=defense['id'],kind='Guard',targetId=actor['id'])
        data=call('resolve')
        if data['battle']['outcome']=='Ongoing':data=call('next')
    for _ in range(50):
        if data['battle']['outcome']!='Ongoing':break
        call('recommend');data=call('resolve')
        if data['battle']['outcome']=='Ongoing':data=call('next')
    assert data['battle']['outcome']=='Victory',(data['battle']['stageName'],data['battle']['round'])
    return data

def tutorial_battle():
    call('stage-enter',stageId='c1s02')
    call('stage-story',stageId='c1s02',expectedPhase='BeforeStory')
    return fight()

def finish_tutorial():
    data=call('reward')
    assert data['campaign']['phase']=='AfterStory'
    call('stage-story',stageId='c1s02',expectedPhase='AfterStory')
    return call('stage-exit')

try:
    set_time('2026-10-16T19:59:59Z') # Saturday 04:59:59 KST, still Friday's period.
    data=start()
    assert data['runtimeRevision']=='skill-flavor-v16'
    assert data['activities']['dailyKey']=='2026-10-16' and data['activities']['weeklyKey']=='2026-10-12'
    assert data['activities']['nextDailyResetUtc']=='2026-10-16T20:00:00Z'
    assert not data['activities']['dungeonUnlocked']
    unchanged(data,'dungeon-enter',dungeonId='coast',practice=False)
    unchanged(data,'quest-claim',questId='daily_victory',periodKey='2026-10-16')
    checks.append('Fresh period keys and KST 05:00 boundary; incomplete quests and dungeon unlock reject without changes.')

    data=call('activity-check-in');saved=data['progress']
    assert quest(data,'daily_checkin')['canClaim']
    assert call('activity-check-in')['progress']==saved
    failed_save(data,'quest-claim',questId='daily_checkin',periodKey='2026-10-16')
    data=claim(data,'daily_checkin')
    assert data['activityReward']['stardust']==20 and data['progress']['stardust']==20
    unchanged(data,'quest-claim',questId='daily_checkin',periodKey='2026-10-16')
    checks.append('Check-in is idempotent. A real save failure restores all quest state/currency; retry pays once.')

    call('stage-enter',stageId='c1s01')
    before=call();failed=failed_save(before,'stage-story',stageId='c1s01',expectedPhase='BeforeStory')
    assert failed['campaign']['phase']=='BeforeStory'
    call('stage-story',stageId='c1s01',expectedPhase='BeforeStory');call('stage-exit')
    before=tutorial_battle()
    assert not before['activities']['dungeonUnlocked'] and quest(before,'daily_victory')['current']==0
    failed=failed_save(before,'reward')
    assert failed['campaign']['phase']=='Battle' and failed['battle']['outcome']=='Victory' and failed['reward'] is None
    data=finish_tutorial()
    assert data['activities']['dungeonUnlocked'] and quest(data,'daily_victory')['canClaim']
    assert quest(data,'daily_defense')['current']==2
    checks.append('Real campaign victory records server battle metrics only at saved rewards; failed story/reward saves restore phase and progress. The after-story gate controls dungeon unlock.')

    before=data['progress']['activities']
    call('stage-replay',stageId='c1s02');call('stage-story',stageId='c1s02',expectedPhase='BeforeStory');data=call('stage-exit')
    assert data['progress']['activities']==before
    call('dungeon-enter',dungeonId='coast',practice=False)
    data=call('dungeon-exit');assert data['activities']['weeklyDungeonRewardsClaimed']==0
    checks.append('Story replay and dungeon retreat grant no activity progress or weekly charge.')

    set_time('2026-10-16T20:00:00Z');data=call()
    assert data['activities']['dailyKey']=='2026-10-17' and data['activities']['weeklyKey']=='2026-10-12'
    assert quest(data,'daily_victory')['current']==0 and quest(data,'weekly_victory')['current']==1
    unchanged(data,'quest-claim',questId='daily_checkin',periodKey='2026-10-16')
    data=call('activity-check-in');data=claim(data,'daily_checkin')
    set_time('2026-10-16T19:00:00Z');data=call()
    assert data['activities']['dailyKey']=='2026-10-17'
    unchanged(data,'quest-claim',questId='daily_checkin',periodKey='2026-10-17')
    checks.append('Daily reset preserves weekly progress. Stale-period claims and clock rollback cannot reopen paid periods.')

    set_time('2026-10-18T19:59:59Z');data=call('activity-check-in')
    for index in range(3):
        call('dungeon-enter',dungeonId='coast',practice=False)
        before=fight()
        unchanged(before,'reward')
        if index==0:
            failed=failed_save(before,'dungeon-reward')
            assert failed['dungeon']['activeId']=='coast' and failed['battle']['outcome']=='Victory'
        previous=before['progress'];data=call('dungeon-reward')
        assert data['dungeon']['activeId'] is None
        assert data['dungeonPrize']['stardust']==80 and data['progress']['stardust']==previous['stardust']+80
        assert data['activities']['weeklyDungeonRewardsClaimed']==index+1
        assert not any(id.startswith('weekly_') for id in data['progress']['clearedStages']+data['progress']['rewardedStages'])
        unchanged(data,'dungeon-reward')
    unchanged(data,'dungeon-enter',dungeonId='reef',practice=False)
    assert quest(data,'weekly_dungeon')['canClaim']
    checks.append('Three real dungeon victories share one weekly quota and pay exactly once; save failure preserves the same victory for retry, with no main-story completion pollution.')

    for _ in range(18):
        if all(q['current']>=q['target'] for q in data['activities']['weeklyQuests']):break
        tutorial_battle();data=finish_tutorial()
    assert all(q['current']>=q['target'] for q in data['activities']['weeklyQuests'])
    for q in data['activities']['dailyQuests']+data['activities']['weeklyQuests']:
        assert q['canClaim']
        previous=data['progress'];data=claim(data,q['id'])
        assert data['activityReward']==q['reward']
        for key in ['stardust','coins','lanternFragments','memoryFragments','characterShards']:
            assert data['progress'][key]==previous[key]+q['reward'][key]
        unchanged(data,'quest-claim',questId=q['id'],periodKey=q['periodKey'])
    saved=data['progress'];stop();data=start();assert data['progress']==saved
    checks.append('All eight objectives become completable through real combat/check-in. Exact five-currency payouts, claims, quota and progress persist across restart.')

    call('dungeon-enter',dungeonId='watch',practice=True);before=fight()
    before_progress=before['progress'];set_time('2026-10-18T20:00:00Z');data=call()
    assert data['activities']['dailyKey']==data['activities']['weeklyKey']=='2026-10-19'
    assert data['activities']['weeklyDungeonRewardsClaimed']==0
    assert all(q['current']==0 and not q['claimed'] for q in data['activities']['dailyQuests']+data['activities']['weeklyQuests'])
    current=data['progress'];data=call('dungeon-reward')
    assert all(data['dungeonPrize'][key]==0 for key in ['stardust','coins','lanternFragments','memoryFragments','characterShards'])
    assert data['progress']['activities']==current['activities']
    assert all(data['progress'][key]==current[key] for key in ['stardust','coins','lanternFragments','memoryFragments','characterShards'])
    unchanged(data,'quest-claim',questId='weekly_dungeon',periodKey='2026-10-12')
    set_time('2026-10-18T19:59:59Z');data=call()
    assert data['activities']['weeklyKey']=='2026-10-19'
    call('dungeon-enter',dungeonId='watch',practice=False);fight();data=call('dungeon-reward')
    assert data['activities']['weeklyDungeonRewardsClaimed']==1 and data['dungeonPrize']['stardust']==120
    assert quest(data,'weekly_victory')['current']==1
    checks.append('Monday 05:00 resets both periods and the shared quota. A practice run stays rewardless across reset; backward time cannot restore the old week, and the fresh week can earn rewards normally.')

    report={'status':'PASS','runtimeRevision':'skill-flavor-v16','port':PORT,'checks':checks,'isolatedSave':True,'playerSaveModified':False,'clockSource':'Explicit QA clock file; production uses DateTime.UtcNow','processStopped':True}
    (QA/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for check in checks:print('PASS:',check)
finally:
    stop();log.close()
