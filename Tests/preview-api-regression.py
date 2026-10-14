"""Run isolated real PreviewHost HTTP campaign checks without touching player saves."""
from pathlib import Path
import argparse, json, struct, subprocess, time, urllib.request, urllib.error
ROOT=Path(__file__).resolve().parent.parent
PORT=8129
SAVE=ROOT/'Logs/ApiTests/progress.xml'
SAVE.parent.mkdir(parents=True,exist_ok=True)
# Each invocation uses its own save, leaving any prior run intact.
SAVE=SAVE.with_name('progress-'+str(time.time_ns())+'.xml')
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--host',type=Path,default=ROOT/'PreviewHost/bin/live/PreviewHost.dll',help='Built PreviewHost DLL; use a separate QA output when the live DLL is in use.')
HOST=parser.parse_args().host.resolve()
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
def four_party(data,ids=('lian','sera','minato','noa')):
    allies=data['battle']['allies']
    assert [actor['id'] for actor in allies]==list(ids)
    assert sum(actor['slotCount'] for actor in allies)==8
    cards=[card for actor in allies for card in actor['skillHand']]
    assert len(cards)==len({card['id'] for card in cards})==8
    for actor in allies:
        assert len(actor['skillHand'])==2
        skills={skill['id']:skill for skill in actor['skills']}
        bag=[card['skillId'] for card in actor['skillHand']]+actor['drawPile']+actor['discardPile']
        assert len(bag)==5
        numbers=[skills[skill_id]['number'] for skill_id in bag]
        assert numbers.count(1)==3 and numbers.count(2)==2 and numbers.count(3)==0
def combat_art_routes():
    for name in ['Lian','Sera','Minato','Noa','Heidel']:
        atlas_size=None
        for suffix,mime in [('SDActions.png','image/png'),('SDActionsLayout.json','application/json')]:
            filename=name+suffix
            with urllib.request.urlopen(f'http://127.0.0.1:{PORT}/combat/{filename}',timeout=5) as response:
                assert response.headers.get_content_type()==mime
                asset=response.read()
            assert asset==(ROOT/'RequiemLantern/Assets/Resources/Combat'/filename).read_bytes()
            if suffix.endswith('.png'):
                assert asset[:8]==b'\x89PNG\r\n\x1a\n'
                atlas_size=struct.unpack('>II',asset[16:24])
            if suffix.endswith('.json'):
                layout=json.loads(asset)
                assert layout['bodyHeight']>0 and layout['atlasWidth']>0 and layout['atlasHeight']>0 and len(layout['frames'])==6
                assert atlas_size==(layout['atlasWidth'],layout['atlasHeight'])
                for frame in layout['frames']:
                    assert frame['width']>0 and frame['height']>0
                    assert 0<=frame['x']<frame['x']+frame['width']<=atlas_size[0]
                    assert 0<=frame['y']<frame['y']+frame['height']<=atlas_size[1]
                    assert frame['x']<=frame['footX']<=frame['x']+frame['width']
                    assert frame['y']<=frame['footY']<=frame['y']+frame['height']
        # Original illustrations keep their separate menu/story route.
        with urllib.request.urlopen(f'http://127.0.0.1:{PORT}/art/{name}.png',timeout=5) as response:
            assert response.headers.get_content_type()=='image/png'
            assert response.read()==(ROOT/'RequiemLantern/Assets/Resources/Art'/f'{name}.png').read_bytes()
log=(SAVE.parent/'host.log').open('w',encoding='utf-8')
process=subprocess.Popen(['C:/Program Files/dotnet/dotnet.exe',str(HOST),str(ROOT),str(SAVE),str(PORT)],
  stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
try:
    data=None
    for _ in range(50):
        if process.poll() is not None:raise RuntimeError('QA host exited')
        try:data=call();break
        except urllib.error.URLError:time.sleep(.2)
    assert data and data['runtimeRevision']=='four-party-sd-v6'
    four_party(data)
    assert data['battle']['ember']==3
    original_hands={actor['id']:actor['skillHand'] for actor in data['battle']['allies']}
    # Every dock actor submits its own offered card without changing another hand.
    for actor in data['battle']['allies']:
        card=actor['skillHand'][0]
        skill=next(skill for skill in actor['skills'] if skill['id']==card['skillId'])
        target='noa' if skill['kind']=='Heal' else data['battle']['enemies'][0]['id']
        kind='Support' if skill['kind']=='Heal' else 'Strike'
        data=call('link',actorId=actor['id'],slot=0,skillId=card['skillId'],cardId=card['id'],kind=kind,targetId=target)
        assert len(data['battle']['links'])==1
        assert data['battle']['links'][0]['actorId']==actor['id'] and data['battle']['links'][0]['cardId']==card['id']
        data=call('clear')
        assert {unit['id']:unit['skillHand'] for unit in data['battle']['allies']}==original_hands
    noa=next(actor for actor in data['battle']['allies'] if actor['id']=='noa')
    card=noa['skillHand'][0];intent=data['battle']['intents'][0]['id']
    foreign_card=next(actor for actor in data['battle']['allies'] if actor['id']=='lian')['skillHand'][0]
    data=call('link',400,actorId='noa',slot=0,skillId=card['skillId'],cardId=foreign_card['id'],kind='Clash',targetId=intent)
    assert not data['battle']['links']
    data=call('link',actorId='noa',slot=0,skillId=card['skillId'],cardId=card['id'],kind='Clash',targetId=intent)
    assert data['previews'][0]['actorId']=='noa'
    data=call('resolve')
    assert len(next(actor for actor in data['battle']['allies'] if actor['id']=='noa')['discardPile'])==1
    assert len(next(actor for actor in data['battle']['allies'] if actor['id']=='noa')['skillHand'])==1
    data=call('next');four_party(data)
    data=call('restart');four_party(data)
    assert data['progress']['coins']==data['progress']['lanternFragments']==0
    assert 'characterStories' not in data
    assert [c['focusCharacterId'] for c in data['campaign']['chapters']]==['lian','sera','noa','heidel','minato']
    assert all(c['focusStorySummary'] for c in data['campaign']['chapters'])
    for family in ['clash','hit_cut','hit_blunt','hit_heavy','swing','brace','ignite','break']:
        for variant in range(1,4):
            filename=f'{family}_{variant:02d}.wav'
            with urllib.request.urlopen(f'http://127.0.0.1:{PORT}/audio/combat/{filename}',timeout=5) as response:
                assert response.headers.get_content_type()=='audio/wav'
                audio=response.read()
            assert audio[:4]==b'RIFF' and audio[8:12]==b'WAVE'
            assert audio==(ROOT/'RequiemLantern/Assets/Resources/Audio/Combat'/filename).read_bytes()
    assert sum(len(c['stages']) for c in data['campaign']['chapters'])==50
    all_stages=[s for chapter in data['campaign']['chapters'] for s in chapter['stages']]
    established=[s for s in all_stages if s['hasBattle'] and (s['chapter']>2 or s['chapter']==2 and int(s['number'].split('-')[1])>=4)]
    assert len(established)==19 and all(len(s['party'])==len(set(s['party']))==4 for s in established)
    assert all(set(s['party']).issubset(s['participants']) for s in established)
    assert stage(data,'c1s02')['party']==['lian'] and stage(data,'c1s04')['party']==['lian','sera']
    assert stage(data,'chapter1_beacon_defense')['party']==['lian','sera','minato']
    assert stage(data,'c2s04')['party']==['lian','sera','minato','heidel']
    assert all('noa' not in s['party'] for s in all_stages if s['chapter']<3 or s['chapter']==3 and int(s['number'].split('-')[1])<6)
    assert [s['id'] for c in data['campaign']['chapters'] for s in c['stages'] if s['unlocked']]==['c1s01']
    data=call('stage-enter',400,stageId='c1s02');assert data['campaign']['activeStage'] is None
    data=call('stage-enter',stageId='c1s01');assert data['campaign']['phase']=='BeforeStory'
    assert all('리안' in line['caption'] and '1-1' in line['caption'] for line in data['campaign']['currentStory'])
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
    # Reach the first four-person main-story battle through actual stage gates.
    target_index=next(index for index,s in enumerate(all_stages) if s['id']=='c2s04')
    for next_stage in all_stages[2:target_index]:
        key=next_stage['id'];data=call('stage-enter',stageId=key)
        if data['campaign']['phase']=='BeforeStory':data=call('stage-story',stageId=key,expectedPhase='BeforeStory')
        if data['campaign']['phase']=='Battle':
            for _ in range(40):
                call('recommend');data=call('resolve')
                if data['battle']['outcome']!='Ongoing':break
                data=call('next')
            assert data['battle']['outcome']=='Victory',(key,data['battle']['round'],data['battle']['outcome'])
            data=call('reward')
            if data['campaign']['phase']=='AfterStory':data=call('stage-story',stageId=key,expectedPhase='AfterStory')
        assert data['campaign']['phase']=='Complete' and stage(data,key)['cleared']
        call('stage-exit')
    data=call('stage-enter',stageId='c2s04');assert data['campaign']['phase']=='BeforeStory'
    data=call('stage-story',stageId='c2s04',expectedPhase='BeforeStory');four_party(data,('lian','sera','minato','heidel'))
    fourth=next(actor for actor in data['battle']['allies'] if actor['id']=='heidel');card=fourth['skillHand'][0]
    data=call('link',actorId='heidel',slot=0,skillId=card['skillId'],cardId=card['id'],kind='Clash',targetId=data['battle']['intents'][0]['id'])
    assert data['battle']['links'][0]['actorId']=='heidel' and data['battle']['links'][0]['cardId']==card['id']
    before_id=data['battle']['battleId'];data=call('restart');four_party(data,('lian','sera','minato','heidel'))
    assert data['battle']['battleId']!=before_id and data['battle']['stageId']=='c2s04'
    assert not stage(data,'c2s04')['cleared'] and not data['battle']['links']
    print('PASS: actual HTTP four-person card bindings, staged story progression to 2-4 and four-person campaign factory/restart.')
    combat_art_routes()
    print('PASS: v6 HTTP revision; four allies/eight distinct cards and slots; all four actor card bindings and fourth-actor clash/consumption/refill; original art and five SD PNG/layout routes; 24 combat WAVs; staged joins, all nineteen four-person parties and actual 2-4 factory/restart; stage locks, story completion, card reservation/clear, S3 lock, victory/after-story rewards, stale requests, replay and synchronization funds.')
finally:
    process.terminate()
    try:process.wait(timeout=5)
    except subprocess.TimeoutExpired:process.kill();process.wait()
    log.close()
