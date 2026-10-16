"""Exercise skill quotes, awakening and ally support through an isolated real host."""
from pathlib import Path
import argparse, json, subprocess, time, urllib.request, urllib.error, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--host',type=Path,default=ROOT/'PreviewHost/bin/skill-qa/PreviewHost.dll')
args=parser.parse_args()
QA=ROOT/'QA/SkillFlavor/2026-10-16/API';QA.mkdir(parents=True,exist_ok=True)
SAVE=QA/('progress-'+str(time.time_ns())+'.xml');PORT=8133
process=None;checks=[]
log=(QA/'host.log').open('w',encoding='utf-8')

def call(action=None,status=200,**fields):
    body=None if action is None else json.dumps(dict(action=action,**fields)).encode()
    request=urllib.request.Request(f'http://127.0.0.1:{PORT}/api/'+('state' if action is None else 'action'),data=body,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(request,timeout=10) as response:actual=response.status;result=json.load(response)
    except urllib.error.HTTPError as error:actual=error.code;result=json.load(error)
    assert actual==status,(action,actual,result.get('error'));return result

def stop():
    global process
    if process is None:return
    process.terminate()
    try:process.wait(timeout=5)
    except subprocess.TimeoutExpired:process.kill();process.wait()
    process=None

def start():
    global process
    process=subprocess.Popen(['C:/Program Files/dotnet/dotnet.exe',str(args.host.resolve()),str(ROOT),str(SAVE),str(PORT)],stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
    for _ in range(60):
        if process.poll() is not None:raise RuntimeError('Isolated host exited')
        try:return call()
        except urllib.error.URLError:time.sleep(.2)
    raise RuntimeError('Host startup timed out')

def unit(state,id):return next(u for u in state['battle']['allies']+state['battle']['enemies'] if u['id']==id)
def offered(state,id,number=None):
    actor=unit(state,id);hand={c['skillId']:c for c in actor['skillHand']}
    return [(s,hand[s['id']]) for s in actor['skills'] if s['id'] in hand and (number is None or s['number']==number)]
def link(state,id,skill,target,kind,card=None):
    fields=dict(actorId=id,slot=0,skillId=skill['id'],targetId=target,kind=kind)
    if card is not None:fields['cardId']=card['id']
    return call('link',**fields)
def safe_round(skip=(),lian_guard=True):
    state=call();call('clear')
    for actor in state['battle']['allies']:
        if actor['hp']<=0 or actor['id'] in skip or actor['id']=='heidel':continue
        if actor['id']=='lian' and not lian_guard:continue
        heal=next(((s,c) for s,c in offered(state,actor['id'],2) if s['kind']=='Heal'),None)
        wounded=min((a for a in state['battle']['allies'] if a['hp']>0),key=lambda a:a['hp']/a['maxHp'])
        if heal and wounded['hp']<wounded['maxHp']*.8:link(state,actor['id'],heal[0],wounded['id'],'Support',heal[1])
        else:link(state,actor['id'],next(s for s in actor['skills'] if s['number']==0),actor['id'],'Guard')
    state=call('resolve');assert state['battle']['outcome']=='Ongoing';return call('next')
def fight():
    state=call()
    for _ in range(50):
        if state['battle']['outcome']!='Ongoing':break
        call('recommend');state=call('resolve')
        if state['battle']['outcome']=='Ongoing':state=call('next')
    assert state['battle']['outcome']=='Victory';return state
def events(state,type,actor=None):return [e for e in state['battle']['events'] if e['type']==type and (actor is None or e['actorId']==actor)]

try:
    state=start();assert state['runtimeRevision']=='skill-flavor-v16'
    for actor in state['battle']['allies']:
        assert all(s['quote'] and s['animationKey'] for s in actor['skills'])
        assert actor['barrier']==actor['supportPower']==actor['awakeningStacks']==0
    assert unit(state,'lian')['awakeningMaxStacks']==3 and unit(state,'mare')['awakeningMaxStacks']==0
    call('stage-enter',stageId='c1s01');call('stage-story',stageId='c1s01',expectedPhase='BeforeStory');call('stage-exit')
    call('stage-enter',stageId='c1s02');call('stage-story',stageId='c1s02',expectedPhase='BeforeStory');state=fight()
    for actor in state['battle']['allies']:
        starts=events(state,'SkillStart',actor['id'])
        assert all(e['quote'] and e['skillId'] and e['targetId'] for e in starts)
    call('reward');call('stage-story',stageId='c1s02',expectedPhase='AfterStory');call('stage-exit')
    call('redeem-code',code='LANTERN-MATERIALS');call('redeem-code',code='LANTERN-SYNC-MATERIALS');call('synchronize',actorId='lian')
    state=call('material-enter',actorId='lian');baseline=state['progress']
    checks.append('Actual starter campaign emits named skill quotes and animation metadata; synchronization and farming still enter through saved progression.')

    for _ in range(9):
        if unit(state,'lian')['awakeningStacks']==3 and offered(state,'lian',3):break
        state=safe_round()
    assert unit(state,'lian')['awakeningStacks']==3 and offered(state,'lian',3)
    before=state['battle'];assert call()['battle']==before and call()['battle']==before
    call('recommend');assert unit(call(),'lian')['awakeningStacks']==3;call('clear');state=call()
    skill,card=offered(state,'lian',3)[0];target=max((a for a in state['battle']['allies'] if a['hp']>0 and a['id']!='lian'),key=lambda a:a['hp'])['id']
    state=link(state,'lian',skill,target,'Support',card)
    assert unit(state,'lian')['awakeningStacks']==3
    state=call('resolve');starts=events(state,'SkillStart','lian')
    assert len(starts)==1 and starts[0]['awakened'] and starts[0]['animationKey']=='lian_s3_awakened'
    assert starts[0]['quote'] and events(state,'AwakeningUsed','lian')
    assert unit(state,'lian')['awakeningStacks']==0
    support=events(state,'SupportApplied','lian');assert len(support)==1 and support[0]['targetId']==target and support[0]['barrier']==30
    assert not events(state,'Damage','lian')
    assert state['progress']==baseline and state['activities']['weeklyDungeonRewardsClaimed']==0
    checks.append('Real guard execution charges three stacks; reads/recommendations/planning do not spend them. The offered S3 spends once, emits its awakened quote, and grants ally barrier 30 instead of enemy damage.')

    state=call('restart')
    assert all(a['awakeningStacks']==a['barrier']==a['supportPower']==0 for a in state['battle']['allies'])
    assert unit(state,'lian')['syncLevel']==1 and state['progress']==baseline
    call('material-exit');stop()
    # Only this disposable XML supplies the legacy-owned protagonist, who is not recruitable.
    tree=ET.parse(SAVE);owned=tree.find('OwnedCharacters');ET.SubElement(owned,'string').text='kai';tree.find('ActiveParty')[2].text='kai';tree.write(SAVE,encoding='utf-8',xml_declaration=True)
    state=start();state=call('material-enter',actorId='kai');baseline=state['progress']
    for _ in range(6):
        if offered(state,'kai',2):break
        state=safe_round(skip=('kai',),lian_guard=False)
    skill,card=offered(state,'kai',2)[0];assert skill['canSupportAlly']
    state=link(state,'kai',skill,'lian','Support',card);state=call('resolve')
    assert unit(state,'lian')['supportPower']==3 and len(events(state,'SupportApplied','kai'))==1
    assert not events(state,'Damage','kai')
    state=call('next');state=safe_round(skip=('kai',))
    assert unit(state,'lian')['supportPower']==3 # A defense action does not consume an attack buff.
    call('clear');state=call();actor=unit(state,'lian');choices=[(s,c) for s,c in offered(state,'lian') if s['kind']=='Attack' and s['cost']<=state['battle']['ember']]
    skill,card=choices[0];assert actor['awakeningStacks']<3
    intent=next(i for i in state['battle']['intents'] if i['kind']!='Illusion' and not i['cancelled'])
    state=link(state,'lian',skill,intent['id'],'Clash',card);planned=state['battle']
    assert call()['battle']==planned and call()['battle']==planned
    assert unit(state,'lian')['supportPower']==3
    state=call('resolve');start_event=events(state,'SkillStart','lian')[0]
    assert start_event['targetId']==intent['actorId'] and start_event['basePower']==skill['basePower']+3
    assert not start_event['awakened'] and unit(state,'lian')['supportPower']==0
    rolls=events(state,'FlameRoll','lian');assert rolls and all(e['basePower']==skill['basePower']+3 for e in rolls)
    assert state['progress']==baseline
    checks.append('Legacy Kai can target an ally with his offered S2; next-attack power survives defense, previews do not consume it, and all clash rolls use one boosted action before consuming once.')

    state=call('restart');assert all(a['awakeningStacks']==a['barrier']==a['supportPower']==0 for a in state['battle']['allies'])
    call('material-exit');saved=call()['progress'];stop();state=start();assert state['progress']==saved
    assert all(a['awakeningStacks']==a['barrier']==a['supportPower']==0 for a in state['battle']['allies'])
    checks.append('Battle-only stacks, barriers and power buffs reset on restart/reload while saved growth, materials, party and quest progress remain unchanged.')

    report={'status':'PASS','runtimeRevision':'skill-flavor-v16','port':PORT,'checks':checks,'playerSaveModified':False,'legacyKaiFixture':'Only the isolated QA XML ownership/party was edited; combat outcomes and stacks were earned normally.','processStopped':True}
    (QA/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for check in checks:print('PASS:',check)
finally:
    stop();log.close()
