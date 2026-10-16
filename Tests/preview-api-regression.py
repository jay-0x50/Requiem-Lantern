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
def action_slots(data):
    battle=data['battle'];round_number=battle['round']
    expected=[1+int(round_number>=index+2 and (not battle['isBoss'] or index<2)) for index,_ in enumerate(battle['allies'])]
    assert [actor['slotCount'] for actor in battle['allies']]==expected
    assert [actor['unlockedActionSlots'] for actor in battle['allies']]==expected
    assert [actor['maxActionSlots'] for actor in battle['allies']]==[1 if battle['isBoss'] and index>=2 else 2 for index,_ in enumerate(battle['allies'])]
def four_party(data,ids=('lian','minato','heidel','mare')):
    allies=data['battle']['allies']
    assert [actor['id'] for actor in allies]==list(ids)
    action_slots(data)
    cards=[card for actor in allies for card in actor['skillHand']]
    assert len(cards)==len({card['id'] for card in cards})==8
    for actor in allies:
        assert len(actor['skillHand'])==2
        skills={skill['id']:skill for skill in actor['skills']}
        assert all(skill['slots']==1 for skill in skills.values())
        bag=[card['skillId'] for card in actor['skillHand']]+actor['drawPile']+actor['discardPile']
        assert len(bag)==5
        numbers=[skills[skill_id]['number'] for skill_id in bag]
        assert numbers.count(1)==3 and numbers.count(2)==2 and numbers.count(3)==0
def hands(data):
    return {actor['id']:actor['skillHand'] for actor in data['battle']['allies']}
def fresh_hands(previous,data):
    assert not data['battle']['links']
    old_ids={card['id'] for hand in previous.values() for card in hand}
    for actor in data['battle']['allies']:
        assert len(actor['skillHand'])==(2 if actor['alive'] else 0)
        assert all(card['id'] not in old_ids for card in actor['skillHand'])
def combat_art_routes():
    for name in ['Lian','Sera','Minato','Noa','Heidel','Kai','Mare']:
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
    assert data and data['runtimeRevision']=='skill-flavor-v16'
    assert all(1<=line['autoDelay']<=6 for line in data['story']['prologue'])
    four_party(data)
    assert data['battle']['ember']==3 and data['battle']['isBoss']
    assert len(data['battle']['intents'])==4
    assert [actor['slotCount'] for actor in data['battle']['allies']]==[1,1,1,1]
    original_hands={actor['id']:actor['skillHand'] for actor in data['battle']['allies']}
    # Every dock actor submits its own offered card without changing another hand.
    for actor in data['battle']['allies']:
        card=actor['skillHand'][0]
        skill=next(skill for skill in actor['skills'] if skill['id']==card['skillId'])
        target='mare' if skill['kind']=='Heal' else data['battle']['enemies'][0]['id']
        kind='Support' if skill['kind']=='Heal' else 'Strike'
        data=call('link',actorId=actor['id'],slot=0,skillId=card['skillId'],cardId=card['id'],kind=kind,targetId=target)
        assert len(data['battle']['links'])==1
        assert data['battle']['links'][0]['actorId']==actor['id'] and data['battle']['links'][0]['cardId']==card['id']
        data=call('clear')
        assert {unit['id']:unit['skillHand'] for unit in data['battle']['allies']}==original_hands
    # A locked second action must preserve the first reservation and both cards.
    lian=next(actor for actor in data['battle']['allies'] if actor['id']=='lian');first,second=lian['skillHand']
    data=call('link',actorId='lian',slot=0,skillId=first['skillId'],cardId=first['id'],kind='Strike',targetId='anger')
    before_link=data['battle']['links'];before_cost=data['plannedCost'];before_ember=data['battle']['ember']
    data=call('link',400,actorId='lian',slot=1,skillId=second['skillId'],cardId=second['id'],kind='Strike',targetId='anger')
    assert data['battle']['links']==before_link and data['plannedCost']==before_cost and data['battle']['ember']==before_ember
    data=call('link',400,actorId='lian',slot=1,skillId='lian_guard',kind='Guard',targetId='lian')
    assert data['battle']['links']==before_link and next(a for a in data['battle']['allies'] if a['id']=='lian')['skillHand']==original_hands['lian']
    data=call('clear')
    mare=next(actor for actor in data['battle']['allies'] if actor['id']=='mare')
    card=mare['skillHand'][0];intent=data['battle']['intents'][0]['id']
    foreign_card=next(actor for actor in data['battle']['allies'] if actor['id']=='lian')['skillHand'][0]
    data=call('link',400,actorId='mare',slot=0,skillId=card['skillId'],cardId=foreign_card['id'],kind='Clash',targetId=intent)
    assert not data['battle']['links']
    data=call('link',actorId='mare',slot=0,skillId=card['skillId'],cardId=card['id'],kind='Clash',targetId=intent)
    assert data['previews'][0]['actorId']=='mare'
    data=call('resolve')
    assert len(next(actor for actor in data['battle']['allies'] if actor['id']=='mare')['discardPile'])==1
    assert len(next(actor for actor in data['battle']['allies'] if actor['id']=='mare')['skillHand'])==1
    data=call('next');four_party(data);fresh_hands(original_hands,data)
    assert [actor['slotCount'] for actor in data['battle']['allies']]==[2,1,1,1]
    # Even unused offers expire. The previous round's exact identity cannot be linked again.
    current_hands=hands(data);stale=original_hands['lian'][0]
    data=call('link',400,actorId='lian',slot=0,skillId=stale['skillId'],cardId=stale['id'],kind='Strike',targetId='anger')
    assert not data['battle']['links'] and hands(data)==current_hands
    for action in ['recommend','clear','reuse','clear']:
        data=call(action);assert hands(data)==current_hands
        assert all(any(card['id']==link['cardId'] and card['skillId']==link['skillId'] for card in current_hands[link['actorId']]) for link in data['battle']['links'] if link['cardId'])
    data=call('link',actorId='lian',slot=1,skillId='lian_guard',kind='Guard',targetId='lian')
    assert data['battle']['links'][0]['slot']==1
    data=call('link',400,actorId='mare',slot=1,skillId='mare_evade',kind='Guard',targetId='mare')
    assert len(data['battle']['links'])==1 and data['battle']['links'][0]['actorId']=='lian'
    call('recommend');call('resolve');data=call('next');four_party(data);fresh_hands(current_hands,data)
    assert [actor['slotCount'] for actor in data['battle']['allies']]==[2,2,1,1]
    data=call('restart');four_party(data)
    # Guard-only turns leave the boss intact while its visible sequence expands.
    for round_number,count in [(1,4),(2,5),(3,6)]:
        assert data['battle']['round']==round_number
        intents=data['battle']['intents'];assert len(intents)==len({intent['id'] for intent in intents})==count
        assert sum(intent['kind']=='Area' for intent in intents)<=1
        assert all(intent['actorId']=='anger' and intent['flameCount']>=1 for intent in intents)
        assert all(intent['targetId']=='all' or any(actor['id']==intent['targetId'] and actor['alive'] for actor in data['battle']['allies']) for intent in intents)
        if round_number==3:break
        old=hands(data)
        for actor in data['battle']['allies']:
            defense=next(skill for skill in actor['skills'] if skill['number']==0)
            data=call('link',actorId=actor['id'],slot=0,skillId=defense['id'],kind='Guard',targetId=actor['id'])
        data=call('resolve');assert data['battle']['outcome']=='Ongoing'
        data=call('next');fresh_hands(old,data)
    extra=next(intent for intent in data['battle']['intents'] if intent['id']=='3:anger_core_2')
    interceptor=next(actor for actor in data['battle']['allies'] if actor['alive'] and actor['id']!=extra['targetId'] and any(next(skill for skill in actor['skills'] if skill['id']==card['skillId'])['kind']=='Attack' for card in actor['skillHand']))
    card=next(card for card in interceptor['skillHand'] if next(skill for skill in interceptor['skills'] if skill['id']==card['skillId'])['kind']=='Attack')
    data=call('link',actorId=interceptor['id'],slot=0,skillId=card['skillId'],cardId=card['id'],kind='Clash',targetId=extra['id'])
    linked=data['battle']['links'][0]
    assert linked['targetId']==extra['id'] and linked['intentActorId']=='anger' and linked['intentPartId']=='anger_core'
    assert next(intent for intent in data['battle']['intents'] if intent['id']==extra['id'])['targetId']==extra['targetId']
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
    assert all(s['enemyCount']==0 for s in all_stages if not s['hasBattle'])
    assert all(s['enemyCount']==1 for s in all_stages if s['isBoss'] or s['number']=='1-2')
    assert all(2<=s['enemyCount']<=3 for s in all_stages if s['hasBattle'] and not s['isBoss'] and s['number']!='1-2')
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
    assert not data['battle']['isBoss'] and len(data['battle']['enemies'])==1
    action_slots(data)
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
    assert next((c['syncLevel'] for c in data['progress']['characters'] if c['id']=='lian'),0)==0
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
    data=call('stage-story',stageId='c2s04',expectedPhase='BeforeStory');four_party(data)
    assert not data['battle']['isBoss'] and [actor['slotCount'] for actor in data['battle']['allies']]==[1,1,1,1]
    enemies=data['battle']['enemies'];intents=data['battle']['intents']
    assert [enemy['id'] for enemy in enemies]==['anger','enemy_2','enemy_3']
    assert len({part['id'] for enemy in enemies for part in enemy['parts']})==6
    assert len(intents)==len({intent['id'] for intent in intents})==4
    for intent in intents:
        owner=next(enemy for enemy in enemies if enemy['id']==intent['actorId'])
        assert any(part['id']==intent['partId'] for part in owner['parts'])
        assert intent['targetId']=='all' or any(ally['id']==intent['targetId'] for ally in data['battle']['allies'])
    fourth=next(actor for actor in data['battle']['allies'] if actor['id']=='heidel');card=fourth['skillHand'][0]
    # An owned ally intercepts the second enemy's attack aimed at a different ally.
    intent=next(intent for intent in intents if intent['actorId']=='enemy_2')
    assert intent['targetId']!='heidel'
    data=call('link',actorId='heidel',slot=0,skillId=card['skillId'],cardId=card['id'],kind='Clash',targetId=intent['id'])
    link=data['battle']['links'][0]
    assert link['actorId']=='heidel' and link['cardId']==card['id'] and link['intentActorId']=='enemy_2' and link['intentPartId']=='enemy_2_arm'
    assert next(i for i in data['battle']['intents'] if i['id']==intent['id'])['targetId']==intent['targetId']
    lian=next(actor for actor in data['battle']['allies'] if actor['id']=='lian');another=lian['skillHand'][0]
    data=call('link',400,actorId='lian',slot=0,skillId=another['skillId'],cardId=another['id'],kind='Clash',targetId=intent['id'])
    assert len(data['battle']['links'])==1 and data['battle']['links'][0]['actorId']=='heidel'
    data=call('resolve')
    take=next(event for event in data['battle']['events'] if event['type']=='Intercept' and event['intentId']==intent['id'])
    assert take['actorId']=='heidel' and take['targetId']=='enemy_2' and take['originalTargetId']==intent['targetId']
    win=next(event for event in data['battle']['events'] if event['type']=='ClashWin' and event['intentId']==intent['id'])
    hit=next(event for event in data['battle']['events'] if event['type']=='Damage' and event['intentId']==intent['id'])
    assert hit['actorId']==win['actorId'] and hit['targetId']==win['targetId']
    assert {hit['actorId'],hit['targetId']}=={'heidel','enemy_2'} and hit['originalTargetId']==intent['targetId']
    before_id=data['battle']['battleId'];data=call('restart');four_party(data)
    assert data['battle']['battleId']!=before_id and data['battle']['stageId']=='c2s04'
    assert len(data['battle']['enemies'])==3 and len({i['id'] for i in data['battle']['intents']})==4
    assert all(enemy['hp']==enemy['maxHp'] for enemy in data['battle']['enemies'])
    assert not stage(data,'c2s04')['cleared'] and not data['battle']['links']
    # A full normal encounter gains exactly one roster member's second action per turn.
    for round_number in range(1,6):
        assert data['battle']['round']==round_number
        action_slots(data)
        assert all(actor['alive'] for actor in data['battle']['allies'])
        assert all(len(actor['skillHand'])==2 for actor in data['battle']['allies'])
        if round_number==5:break
        previous_hands=hands(data)
        for actor in data['battle']['allies']:
            defense=next(skill for skill in actor['skills'] if skill['number']==0)
            data=call('link',actorId=actor['id'],slot=0,skillId=defense['id'],kind='Guard',targetId=actor['id'])
        data=call('resolve');assert data['battle']['outcome']=='Ongoing' and hands(data)==previous_hands
        data=call('next');fresh_hands(previous_hands,data)
    data=call('restart');four_party(data)
    assert [actor['slotCount'] for actor in data['battle']['allies']]==[1,1,1,1]
    print('PASS: HTTP normal R1-R5 roster progression, boss R1-R3 caps, locked-second-action atomic rejection, both offers replaced after used or defense-only rounds, stale CardId rejection and full restart.')
    combat_art_routes()
    print('PASS: v16 HTTP revision; starter ownership/active party; boss 4/5/6 actions on intact guard-only rounds and interception of an extra uniquely identified core action; fresh two-card offers per round with stable weighted bags and non-mutating recommendation/reuse; turn-based action progression with explicit boss metadata; four allies/eight distinct card choices; authored 2-3 enemy groups; exact enemy/part/intent identity; teammate interception and damage events; atomic card/slot/budget checks; original art and six SD PNG/layout routes; 24 combat WAVs; story cast metadata and stage locks; story completion, S3 lock, victory/after-story rewards, replay and synchronization funds.')
finally:
    process.terminate()
    try:process.wait(timeout=5)
    except subprocess.TimeoutExpired:process.kill();process.wait()
    log.close()
