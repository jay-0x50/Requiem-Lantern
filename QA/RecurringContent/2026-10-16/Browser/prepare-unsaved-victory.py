"""Prepares a real won 1-2 replay and blocks only this QA save's temp path.

The browser must fail reward persistence, retain the battle, then retry after
the empty progress.xml.tmp directory is removed by the QA runner.
"""
import json
from pathlib import Path
from urllib.request import Request, urlopen

BASE='http://127.0.0.1:8128'
HERE=Path(__file__).parent
def request(action=None, **fields):
    req=Request(BASE+'/api/action',json.dumps(dict(action=action,**fields)).encode(),{'Content-Type':'application/json'}) if action else Request(BASE+'/api/state')
    with urlopen(req,timeout=10) as response:
        value=json.load(response)
    assert not value.get('error'),value.get('error')
    return value

state=request()
assert not state['dungeon']['activeId']
stage=next(s for c in state['campaign']['chapters'] for s in c['stages'] if s['number']=='1-2')
state=request('stage-enter',stageId=stage['id'])
state=request('stage-story',stageId=stage['id'],expectedPhase='BeforeStory')
for _ in range(40):
    state=request('recommend')
    state=request('resolve',grades={})
    if state['battle']['outcome']!='Ongoing':
        break
    state=request('next')
assert state['battle']['outcome']=='Victory'
assert state['battle']['battleId'] not in state['progress']['claimedBattles']
before=dict(battleId=state['battle']['battleId'],phase=state['campaign']['phase'],coins=state['progress']['coins'],lantern=state['progress']['lanternFragments'],activities=state['activities'])
(HERE/'unsaved-before.json').write_text(json.dumps(before,ensure_ascii=False,indent=2),encoding='utf-8')
blocked=HERE/'progress.xml.tmp'
assert not blocked.exists()
blocked.mkdir()
print(json.dumps(dict(battleId=before['battleId'],phase=before['phase'],coins=before['coins'],blockedPath=str(blocked)),ensure_ascii=False))
