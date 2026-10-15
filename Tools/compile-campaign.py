"""Export authored main-story stages to the shared C# catalog and planning document."""
import json
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
ART={"리안":"Lian","세라":"Sera","미나토":"Minato","하이델":"Heidel","노아":"Noa","마레":"Mare","오르페":"Orphe","로웬":"Rowan","엘렌":"Ellen","이브":"Eve"}
IDS={"카이":"kai",**{name:art.lower() for name,art in ART.items()}}
KINDS={"Story":"스토리","Battle":"전투","StoryBattle":"스토리 + 전투"}
FOCUS={"lian":"리안","sera":"세라","noa":"노아","heidel":"하이델","minato":"미나토"}
def cs(value):return json.dumps(value,ensure_ascii=False)
def story_line(line):
    assert isinstance(line,list) and 2<=len(line)<=4,'Story rows require 2 to 4 items.'
    speaker,text=line[:2]
    assert isinstance(speaker,str) and isinstance(text,str) and text.strip(),'Story speaker/text must be strings with nonempty text.'
    cue=line[2] if len(line)>=3 else ''
    assert isinstance(cue,str),'Story music cue must be a string; use an empty string before timing metadata.'
    delay=None
    if len(line)==4:
        metadata=line[3]
        assert isinstance(metadata,dict) and set(metadata)=={'autoDelay'},'Story metadata permits only autoDelay.'
        delay=metadata['autoDelay']
        assert type(delay) in (int,float) and math.isfinite(delay) and 1<=delay<=6,'autoDelay must be a finite number between 1 and 6 seconds.'
    return speaker,text,cue,delay

def main():
    data=json.loads((ROOT/'기획서/메인스토리/campaign.json').read_text(encoding='utf-8'))
    chapters=data['chapters'];assert data['schemaVersion']==1
    assert [chapter['focusCharacterId'] for chapter in chapters]==list(FOCUS)
    ids=set();counts={k:0 for k in KINDS};lines_total=0
    code=['// Generated from 기획서/메인스토리/campaign.json by Tools/compile-campaign.py.','using System;','using System.Collections.Generic;','using System.Linq;','namespace RequiemLantern.Core','{','    public static class CampaignCatalog','    {','        public static readonly List<ChapterData> Chapters = new List<ChapterData>','        {']
    doc=['# 메인 스토리 · 캐릭터 중심의 장과 스테이지 구성','', '갱신일: 2026-10-15 · 초안 v0.4','',
         '메인 스토리는 1~5장으로 구분하고, 각 장에 10개 스테이지를 둔다. 번호는 1-1, 1-2 … 5-10이다. 스테이지 수는 JSON 데이터에서 늘릴 수 있으며 화면과 진행 규칙은 목록 길이에 따라 동작한다.','',
         '스토리·전투·스토리와 전투를 구분한다. 혼합 스테이지는 전투 전 대화 → 전투 → 전투 후 대화 → 완료로 이어진다. 전투에서 패배하면 다음 스테이지가 열리지 않고, 후속 대화가 남아 있으면 그것을 마친 뒤 진행이 열린다. 스토리 전용 구간은 전투 없이 완료되며 전투 보상을 지급하지 않는다.','',
         '메인스토리의 한 장이 곧 한 캐릭터의 이야기다. 1장 리안, 2장 세라, 3장 노아, 4장 하이델, 5장 미나토를 중심으로 과거의 갈등이 현재의 원정 목적·인간과 군체의 대립·장 결전에 이어진다. 합류 이유와 기억의 대가, 증오와 미련, 새로운 결의는 해당 장의 스테이지 대화에서 드러난다.','',
         '중심 인물 혼자 진행하는 개인편이 아니다. 카이와 동료들이 함께 이동하고 서로의 선택에 답하며, 그 과정에서 영야·잔영·교단·관측소와 등불지기의 비밀이 순차적으로 드러난다. 다음 장은 앞 장의 결과를 이어받는다.','',
         '스토리 진입은 작전실의 메인 스테이지로 통일한다. 캐릭터 화면은 해당 인물이 중심인 메인 장으로 안내하고, 기록실은 완료한 메인 스테이지를 재생한다. 이전 독립 캐릭터 대본은 개발 참고 자료로 보관하며 별도의 게임 콘텐츠로 배포하지 않는다.','',
         '이동 거점은 등대 보급선을 개조한 귀항호다. 등대 요새는 귀환 거점으로 남고, 귀항호는 연안의 기항지와 상륙 원정을 잇는다. 항해도 노드는 바다 이동뿐 아니라 정박 뒤의 거리·의무실·시계탑 등 임무 동선도 포함한다. 2-1 도입에서 배의 이름과 귀환 신호를 동료 대화로 소개한다.','',
         '일반 전투는 적 2~3명을 배치하고, 첫 튜토리얼 1-2와 보스는 1명을 배치한다. 적 수는 스테이지의 `enemyCount`를 원본으로 공용 카탈로그에 전달한다. 각 적은 독립 체력·부위·균열·정신력·예고선을 갖고, 모든 적을 쓰러뜨려야 승리한다. 예고선은 원래 공격 대상을 표시하며, 다른 동료의 공격 스킬로 대리 맞불을 연결할 수 있다. 속도 제한을 추가하지 않고 기존 행동칸·손패·불씨 비용을 사용한다. 단일 공격의 맞불에서 패하면 대리자가 맞고, 전체 공격은 패배 시 생존 아군 전체가 피해를 받는다.','',
         '현재 전투는 공용 프로토타입 규칙에 단계별 적 수치·예고선 유형·파티·인간/군체 외형을 적용한다. 보스의 최종 다중 페이즈와 전용 음악은 추가 제작 대상이며, 대본의 음악 큐는 향후 음원 연결용이다.','',
         '실제 전투는 저장된 보유 캐릭터 4인 편성을 사용한다. 기본 지급은 리안 ★5와 미나토·하이델·마레 ★4이며, 세라·노아는 모집으로 획득한다. 카이는 이야기의 등불지기이자 조율자로 남는다. 아래의 서사 속 동행과 원본 JSON의 party는 각 장면에서 함께하는 인물의 참고 기록이다. 등장인물의 이야기 합류는 소유 여부와 별개이며, 미보유 인물이 있어도 본편을 읽고 진행할 수 있다. 모집·편성·기존 저장 이관은 08_픽업_모집_보유편성_테스트쿠폰.md를 따른다.','',
         '자동 재생은 대사가 완전히 표시된 뒤 기본 2.6초를 기다린다. 원본 행은 `[화자, 대사, 선택 음악 큐, 선택 메타데이터]` 형식이다. 메타데이터는 네 번째 항목 `{autoDelay: 초}`만 허용하고 1~6초 범위로 검증한다. 음악 없이 메타데이터를 넣을 때 세 번째 항목은 빈 문자열이다. 아래의 자동 대기는 완문 표시 뒤의 시간이며, 직접 넘기기는 즉시 가능하다.','',
         '원본: `메인스토리/campaign.json`. `python Tools/compile-campaign.py`로 공용 C# 카탈로그와 이 문서를 갱신한다.','',
         '## 스테이지 목록','', '| 번호 | 스테이지 | 종류 | 함께 움직이는 인물 | 사건과 목적 |', '|---|---|---|---|---|']
    for ci,ch in enumerate(chapters,1):
        assert ch['number']==ci and len(ch['stages'])>0
        focus=ch['focusCharacterId'];assert focus in FOCUS and ch['focusStorySummary'].strip()
        code.extend(['            new ChapterData {',f'                Number={ci}, Title={cs(ch["title"])}, Summary={cs(ch["summary"])},',f'                FocusCharacterId={cs(focus)}, FocusStorySummary={cs(ch["focusStorySummary"])},','                Stages=new List<StageData> {'])
        for ni,s in enumerate(ch['stages'],1):
            assert s['number']==f'{ci}-{ni}' and s['chapter']==ci and s['id'] not in ids
            ids.add(s['id']);counts[s['kind']]+=1
            assert s['kind'] in KINDS and len(s['participants'])>=1
            if s['kind']=='Story':assert s['before'] and not s['after'] and not s['party'] and s['enemyCount']==0
            elif s['kind']=='Battle':assert not s['before'] and not s['after'] and s['enemyName']
            else:assert s['before'] and s['after'] and s['enemyName']
            if s['kind']!='Story':
                assert 1<=len(s['party'])<=4 and len(s['party'])==len(set(s['party'])) and set(s['party']).issubset(s['participants'])
                assert all(id in FOCUS for id in s['party'])
                assert isinstance(s['enemyCount'],int) and 1<=s['enemyCount']<=3
                assert s['enemyCount']==1 if s['isBoss'] or s['number']=='1-2' else s['enemyCount']>=2
            code.append('                    new StageData {')
            for field,key in [('Id','id'),('Number','number'),('Title','title'),('Summary','summary'),('Location','location'),('EnemyName','enemyName'),('EnemyArt','enemyArt'),('BattleProfile','battleProfile'),('MusicId','musicId')]:
                code.append(f'                        {field}={cs(s[key])},')
            code.append(f'                        Chapter={ci}, EnemyCount={s["enemyCount"]}, Kind=StageKind.{s["kind"]}, IsBoss={str(s["isBoss"]).lower()}, HumanEnemy={str(s["humanEnemy"]).lower()},')
            for field,key in [('Participants','participants'),('Party','party')]:code.append(f'                        {field}=new string[] {{'+','.join(cs(v) for v in s[key])+'},')
            for field,key in [('Before','before'),('After','after')]:
                code.extend([f'                        {field}=new List<StoryLine> {{'])
                main='';other=''
                for line in s[key]:
                    speaker,text,cue,delay=story_line(line)
                    if speaker in IDS:assert IDS[speaker] in s['participants'],(s['number'],speaker)
                    current=ART.get(speaker,'')
                    if current and current!=main:other=main;main=current
                    background='keep' if any(w in s['location'] for w in ('등대','식탁','야영')) else 'ruins'
                    if (ci,ni)==(1,1):background='black'
                    caption=f'제{ci}장 · {FOCUS[focus]} · {s["number"]} · {s["title"]}'
                    timing='' if delay is None else f', AutoDelay={float(delay):g}f'
                    code.append(f'                            new StoryLine({cs(speaker)}, {cs(text)}, {cs(background)}, {cs(main)}, true) {{ Location={cs(s["location"])}, Caption={cs(caption)}, ListenerPortrait={cs(other if other!=main else "")}, MusicCue={cs(cue)}{timing} }},')
                    lines_total+=1
                code.append('                        },')
            code.append('                    },')
            names=[name for name,id in IDS.items() if id in s['participants']]
            doc.append(f'| {s["number"]} | {s["title"]} | {KINDS[s["kind"]]} | {" · ".join(names)} | {s["summary"]} |')
        code.extend(['                }','            },'])
    code.extend(['        };','        public static StageData Get(string id) { return Chapters.SelectMany(c=>c.Stages).FirstOrDefault(s=>string.Equals(s.Id,id,StringComparison.OrdinalIgnoreCase)); }','    }','}',''])
    doc.extend(['','## 대본과 전투 연결',''])
    for ch in chapters:
        doc.extend([f'### 제{ch["number"]}장 · {ch["title"]}','',f'중심 인물: **{FOCUS[ch["focusCharacterId"]]}**', '',ch['focusStorySummary'],'',ch['summary'],''])
        for s in ch['stages']:
            doc.extend([f'#### {s["number"]} · {s["title"]}', '',f'종류: **{KINDS[s["kind"]]}** · 장소: {s["location"]}', '',s['summary'],''])
            for key,label in [('before','진입 대화'),('after','전투 후 대화')]:
                if not s[key]:continue
                doc.extend([f'**{label}**',''])
                for line in s[key]:
                    _,_,cue,delay=story_line(line)
                    if cue:doc.extend([f'음악 큐: `{cue}`',''])
                    if delay is not None:doc.extend([f'자동 대기: **{delay:g}초** (대사 완문 표시 뒤)',''])
                    doc.extend([f'**{line[0] or "서술"}**: {line[1]}',''])
            if s['kind']!='Story':doc.extend([f'**전투:** 적 {s["enemyCount"]}명 · {s["enemyName"]} / {"인간" if s["humanEnemy"] else "군체"} · 프로필 `{s["battleProfile"]}` · {"보스" if s["isBoss"] else "일반/중간 전투"}.', '', f'**서사 속 동행 ({len(s["party"])}명):** {" · ".join(FOCUS[id] for id in s["party"])}.',''])
    doc.insert(4,f'규모: {len(chapters)}장 · {len(ids)}스테이지 · 대사/서술 {lines_total}행. 스토리 {counts["Story"]}, 전투 {counts["Battle"]}, 스토리+전투 {counts["StoryBattle"]}.')
    doc.insert(5,'')
    (ROOT/'RequiemLantern/Assets/Scripts/Core/CampaignCatalog.cs').write_text('\n'.join(code),encoding='utf-8')
    (ROOT/'기획서/06_메인스토리_장과_스테이지.md').write_text('\n'.join(doc)+'\n',encoding='utf-8')
    print(f'PASS: {len(chapters)} chapters, {len(ids)} stages, {lines_total} lines; kinds={counts}.')
if __name__=='__main__':main()
