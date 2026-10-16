"""Unlocks salvage and material dives through real stage API commands on the isolated 8128 QA save.

No progress fields, owned characters, levels, or outcome are supplied by the test.
The browser separately verifies claims and salvage entry/result interactions.
"""
import json
from pathlib import Path
from urllib.request import Request, urlopen

BASE = 'http://127.0.0.1:8128'
HERE = Path(__file__).parent
def request(action=None, **fields):
    if action:
        raw = json.dumps(dict(action=action, **fields)).encode()
        req = Request(BASE + '/api/action', raw, {'Content-Type':'application/json'})
    else:
        req = Request(BASE + '/api/state')
    with urlopen(req, timeout=10) as response:
        state = json.load(response)
    if state.get('error'):
        raise RuntimeError(state['error'])
    return state

state = request()
party = state['recruitment']['party']
assert party == ['lian','minato','heidel','mare'], party
stage = next(s for c in state['campaign']['chapters'] for s in c['stages'] if s['number']=='1-2')
if not stage['cleared']:
    state = request('stage-enter', stageId=stage['id'])
    state = request('stage-story', stageId=stage['id'], expectedPhase='BeforeStory')
    rounds = []
    for _ in range(40):
        state = request('recommend')
        state = request('resolve', grades={})
        rounds.append(dict(round=state['battle']['round'],outcome=state['battle']['outcome']))
        if state['battle']['outcome'] != 'Ongoing':
            break
        state = request('next')
    assert state['battle']['outcome']=='Victory', rounds
    state = request('reward')
    state = request('stage-story', stageId=stage['id'], expectedPhase='AfterStory')
    (HERE/'unlock-evidence.json').write_text(json.dumps(dict(party=party,rounds=rounds,activities=state['activities'],materials=state['materials']),ensure_ascii=False,indent=2),encoding='utf-8')
assert state['activities']['dungeonUnlocked'] is True
state=request('stage-exit')
print(json.dumps(dict(unlocked=state['activities']['dungeonUnlocked'],party=state['recruitment']['party'],weeklyClaims=state['activities']['weeklyDungeonRewardsClaimed']),ensure_ascii=False))
