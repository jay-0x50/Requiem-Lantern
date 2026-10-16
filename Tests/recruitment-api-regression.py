"""Exercise recruitment, coupons, persistence and formation against an isolated real host."""
from pathlib import Path
import argparse, json, subprocess, time, urllib.request, urllib.error, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--host',type=Path,default=ROOT/'PreviewHost/bin/recruitment-qa/PreviewHost.dll')
args=parser.parse_args()
QA=ROOT/'QA/RecurringContent/2026-10-16/RecruitmentAPI'
QA.mkdir(parents=True,exist_ok=True)
SAVE=QA/('progress-'+str(time.time_ns())+'.xml')
PORT=8130
process=None
log=(QA/'host.log').open('a',encoding='utf-8')
checks=[]

def call(action=None,status=200,**fields):
    body=None if action is None else json.dumps(dict(action=action,**fields)).encode()
    request=urllib.request.Request(f'http://127.0.0.1:{PORT}/api/'+('state' if action is None else 'action'),data=body,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(request,timeout=8) as response: actual=response.status; result=json.load(response)
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
    process=subprocess.Popen(['C:/Program Files/dotnet/dotnet.exe',str(args.host.resolve()),str(ROOT),str(SAVE),str(PORT)],stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
    for _ in range(60):
        if process.poll() is not None:raise RuntimeError('Isolated host exited')
        try:return call()
        except urllib.error.URLError:time.sleep(.2)
    raise RuntimeError('Isolated host did not become ready')

def unchanged(before,action,**fields):
    result=call(action,400,**fields)
    assert result['progress']==before['progress']
    assert result['recruitmentResult'] is None and result['couponReward'] is None
    return result

try:
    data=start();assert data['runtimeRevision']=='recurring-v14'
    starter=['lian','minato','heidel','mare']
    assert data['progress']['activeParty']==data['progress']['ownedCharacters']==starter
    rarity={character['id']:character['rarity'] for character in data['recruitment']['characters']}
    assert [character['id'] for character in data['recruitment']['characters'] if character['rarity']==4 and character['recruitable']]==['minato','heidel','mare']
    assert not next(character for character in data['recruitment']['characters'] if character['id']=='kai')['recruitable']
    assert sorted(rarity[id] for id in starter)==[4,4,4,5]
    assert [ally['id'] for ally in data['battle']['allies']]==starter
    for action,fields in [('recruit',dict(bannerId='sera',count=1)),('recruit',dict(bannerId='bad',count=10)),('recruit',dict(bannerId='sera',count=2)),('equip-character',dict(actorId='sera',slot=1)),('synchronize',dict(actorId='noa')),('redeem-code',dict(code='UNKNOWN'))]:
        unchanged(data,action,**fields)
    checks.append('Fresh 1x5-star/3x4-star grant; invalid, insufficient and unowned requests preserve all progress.')

    data=call('redeem-code',code='  lantern-test-9999999  ')
    assert data['progress']['stardust']==data['progress']['coins']==9999999
    assert data['couponReward']['stardust']==9999999
    unchanged(data,'redeem-code',code='LANTERN-TEST-9999999')
    data=call('redeem-code',code='LANTERN-MATERIALS')
    assert all(data['progress'][key]==9999999 for key in ['lanternFragments','memoryFragments','characterShards'])
    unchanged(data,'redeem-code',code='lantern-materials')
    checks.append('Both coupons normalize input, grant the exact amounts and reject reuse without changes.')

    before=data['progress'];data=call('recruit',bannerId='sera',count=10)
    result=data['recruitmentResult'];assert result['cost']==1600 and len(result['pulls'])==10
    assert data['progress']['stardust']==before['stardust']-1600
    assert 0<=data['progress']['recruitPity']<80
    saved=data['progress'];stop();data=start();assert data['progress']==saved
    checks.append('Ten pulls charge once and persist ownership, pity, guarantee, currency, coupon use and random stream across restart.')

    # Force a filesystem save failure, rather than merely mocking a failed service result.
    pending=Path(str(SAVE)+'.tmp');pending.mkdir()
    try:
        unchanged(data,'recruit',bannerId='noa',count=1)
        rejected=unchanged(data,'synchronize',actorId='lian')
        assert all(ally['syncLevel']==0 for ally in rejected['battle']['allies'])
    finally:pending.rmdir()
    assert call()['progress']==saved
    checks.append('A real save-path failure rolls back draw/RNG/pity/currency and synchronization/materials, restores battle growth and returns no result.')

    stop();tree=ET.parse(SAVE);tree.find('RecruitPity').text='79';tree.find('PickupGuaranteed').text='true';tree.write(SAVE,encoding='utf-8',xml_declaration=True)
    data=start();assert data['recruitment']['pity']==79 and data['recruitment']['pickupGuaranteed']
    data=call('recruit',bannerId='noa',count=1);pull=data['recruitmentResult']['pulls'][0]
    assert pull['characterId']=='noa' and pull['rarity']==5 and pull['isPickup']
    assert data['progress']['recruitPity']==0 and not data['progress']['pickupGuaranteed']
    data=call('equip-character',actorId='noa',slot=1)
    assert data['recruitment']['party']==['lian','noa','heidel','mare']
    saved=data['progress'];stop();data=start();assert data['progress']==saved
    checks.append('The 80th guaranteed five-star honors the newly selected banner and persists the acquired/edited party.')

    call('stage-enter',stageId='c1s01');call('stage-story',stageId='c1s01',expectedPhase='BeforeStory');call('stage-exit')
    call('stage-enter',stageId='c1s02');data=call('stage-story',stageId='c1s02',expectedPhase='BeforeStory')
    assert [ally['id'] for ally in data['battle']['allies']]==['lian','noa','heidel','mare']
    for action,fields in [('recruit',dict(bannerId='sera',count=1)),('redeem-code',dict(code='UNKNOWN')),('equip-character',dict(actorId='minato',slot=1))]:unchanged(data,action,**fields)
    checks.append('The actual campaign uses the saved owned party; inventory changes are rejected during an active expedition.')
    report={'status':'PASS','runtimeRevision':'recurring-v14','port':PORT,'checks':checks,'playerSaveModified':False,'processStopped':True}
    (QA/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for check in checks:print('PASS:',check)
finally:
    stop();log.close()
