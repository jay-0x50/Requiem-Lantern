"""Run isolated real PreviewHost HTTP campaign checks without touching player saves."""
from pathlib import Path
import json, subprocess, time, urllib.request, urllib.error
ROOT=Path(__file__).resolve().parent.parent
PORT=8129
SAVE=ROOT/'Logs/ApiTests/progress.xml'
SAVE.parent.mkdir(parents=True,exist_ok=True)
# Each invocation uses its own save, leaving any prior run intact.
SAVE=SAVE.with_name('progress-'+str(time.time_ns())+'.xml')
HOST=ROOT/'PreviewHost/bin/live/PreviewHost.dll'
def call(action=None,expect=200,**fields):
    payload=json.dumps({'action':action,**fields}).encode() if action else None
    req=urllib.request.Request(f'http://127.0.0.1:{PORT}/api/'+('action' if action else 'state'),
      data=payload,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=5) as r: status=r.status; data=json.load(r)
    except urllib.error.HTTPError as e: status=e.code; data=json.load(e)
    assert status==expect,(action,status,data.get('error'))
    return data
def stage(data,key):return next(s for c in data['campaign']['chapters'] for s in c['stages'] if s['id']==key)
log=(SAVE.parent/'host.log').open('w',encoding='utf-8')
process=subprocess.Popen(['C:/Program Files/dotnet/dotnet.exe',str(HOST),str(ROOT),str(SAVE),str(PORT)],
  stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
try:
    data=None
    for _ in range(50):
        if process.poll() is not None:raise RuntimeError('QA host exited')
        try:data=call();break
        except urllib.error.URLError:time.sleep(.2)
    assert data and data['runtimeRevision']=='skill-deck-v2'
    assert sum(len(c['stages']) for c in data['campaign']['chapters'])==50
    assert [s['id'] for c in data['campaign']['chapters'] for s in c['stages'] if s['unlocked']]==['c1s01']
    data=call('stage-enter',400,stageId='c1s02');assert data['campaign']['activeStage'] is None
    data=call('stage-enter',stageId='c1s01');assert data['campaign']['phase']=='BeforeStory'
    data=call('resolve',400);assert data['campaign']['phase']=='BeforeStory'
    data=call('stage-story',400,stageId='c1s02',expectedPhase='BeforeStory')
    data=call('stage-story',stageId='c1s01',expectedPhase='BeforeStory')
    assert stage(data,'c1s01')['cleared'] and stage(data,'c1s02')['unlocked']
    assert data['progress']['coins']==data['progress']['lanternFragments']==0
    data=call('stage-story',400,stageId='c1s01',expectedPhase='BeforeStory')
    call('stage-exit')
    data=call('stage-enter',stageId='c1s02');assert data['campaign']['phase']=='BeforeStory'
    data=call('stage-story',stageId='c1s02',expectedPhase='BeforeStory');assert data['campaign']['phase']=='Battle'
    actor=next(a for a in data['battle']['allies'] if a['id']=='lian')
    hand=actor['skillHand'];assert len(hand)==2 and len(actor['drawPile'])==3
    assert not any(s['skillId']=='lian_crescendo' for s in hand)
    card=hand[0];intent=data['battle']['intents'][0]['id']
    data=call('link',400,actorId='lian',slot=0,skillId=card['skillId'],cardId='invalid-card',kind='Clash',targetId=intent)
    assert not data['battle']['links'] and next(a for a in data['battle']['allies'] if a['id']=='lian')['skillHand']==hand
    data=call('link',actorId='lian',slot=0,skillId=card['skillId'],cardId=card['id'],kind='Clash',targetId=intent)
    assert data['battle']['links'][0]['cardId']==card['id']
    enemy=data['battle']['enemies'][0]['id']
    data=call('link',400,actorId='lian',slot=1,skillId=card['skillId'],cardId=card['id'],kind='Strike',targetId=enemy)
    assert len(data['battle']['links'])==1 and data['battle']['links'][0]['cardId']==card['id']
    data=call('clear');assert not data['battle']['links']
    assert next(a for a in data['battle']['allies'] if a['id']=='lian')['skillHand']==hand
    before=data['plannedCost']
    data=call('link',400,actorId='lian',slot=0,skillId='lian_crescendo',kind='Clash',targetId=data['battle']['intents'][0]['id'])
    assert data['plannedCost']==before and not stage(data,'c1s02')['cleared']
    for _ in range(12):
        call('recommend');data=call('resolve')
        if data['battle']['outcome']!='Ongoing':break
        data=call('next')
    assert data['battle']['outcome']=='Victory'
    data=call('reward');assert data['campaign']['phase']=='AfterStory'
    assert not stage(data,'c1s02')['cleared'] and not stage(data,'c1s03')['unlocked']
    assert data['progress']['coins']==40 and data['progress']['lanternFragments']==2
    call('reward',400)
    call('stage-story',400,stageId='c1s02',expectedPhase='BeforeStory')
    data=call('stage-story',stageId='c1s02',expectedPhase='AfterStory')
    assert stage(data,'c1s02')['cleared'] and stage(data,'c1s03')['unlocked']
    call('stage-exit');data=call('stage-replay',stageId='c1s02')
    assert data['campaign']['replay'] and data['campaign']['phase']=='BeforeStory'
    data=call('stage-story',stageId='c1s02',expectedPhase='BeforeStory')
    assert data['campaign']['phase']=='Complete' and data['progress']['coins']==40
    call('stage-exit')
    data=call('synchronize',400,actorId='lian')
    assert next(c for c in data['progress']['characters'] if c['id']=='lian')['syncLevel']==0
    print('PASS: real HTTP stage locks, story completion, two-card hand, explicit card identity/reservation/clear without reroll, battle guards, S3 lock, victory/after-story rewards, stale requests, replay and insufficient synchronization funds.')
finally:
    process.terminate()
    try:process.wait(timeout=5)
    except subprocess.TimeoutExpired:process.kill();process.wait()
    log.close()
