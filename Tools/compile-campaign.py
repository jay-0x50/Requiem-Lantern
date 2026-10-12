"""Export authored main-story stages to the shared C# catalog and planning document."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
ART={"리안":"Lian","세라":"Sera","미나토":"Minato","하이델":"Heidel","노아":"Noa","오르페":"Orphe","로웬":"Rowan","엘렌":"Ellen","이브":"Eve"}
IDS={"카이":"kai",**{name:art.lower() for name,art in ART.items()}}
KINDS={"Story":"스토리","Battle":"전투","StoryBattle":"스토리 + 전투"}
def cs(value):return json.dumps(value,ensure_ascii=False)
def main():
    data=json.loads((ROOT/'기획서/메인스토리/campaign.json').read_text(encoding='utf-8'))
    chapters=data['chapters'];assert data['schemaVersion']==1
    ids=set();counts={k:0 for k in KINDS};lines_total=0
    code=['// Generated from 기획서/메인스토리/campaign.json by Tools/compile-campaign.py.','using System;','using System.Collections.Generic;','using System.Linq;','namespace RequiemLantern.Core','{','    public static class CampaignCatalog','    {','        public static readonly List<ChapterData> Chapters = new List<ChapterData>','        {']
    doc=['# 메인 스토리 · 장과 스테이지 구성','', '작성일: 2026-10-12 · 초안 v0.1','',
         '메인 스토리는 1~5장으로 구분하고, 각 장에 10개 스테이지를 둔다. 번호는 1-1, 1-2 … 5-10이다. 스테이지 수는 JSON 데이터에서 늘릴 수 있으며 화면과 진행 규칙은 목록 길이에 따라 동작한다.','',
         '스토리·전투·스토리와 전투를 구분한다. 혼합 스테이지는 전투 전 대화 → 전투 → 전투 후 대화 → 완료로 이어진다. 전투에서 패배하면 다음 스테이지가 열리지 않고, 후속 대화가 남아 있으면 그것을 마친 뒤 진행이 열린다. 스토리 전용 구간은 전투 없이 완료되며 전투 보상을 지급하지 않는다.','',
         '장마다 중심 사건은 있지만 여러 동료가 함께 움직인다. 이야기에서 전투 편성 인원과 동행 인원은 구분한다. 하이델·노아도 1·2·3스킬과 방어 행동을 가진 출격 캐릭터다. 스테이지별 1~3명의 전투 편성과 더 많은 동행 인물의 대화를 함께 구성한다.','',
         '리안의 사연은 1장, 세라의 사연은 2장, 노아의 사연은 3장의 메인 진행 안에 들어간다. 하이델의 관측소 과거는 4장에, 전체 동료들의 서로 다른 결의는 5장에 이어진다. 앞서 작성한 독립 캐릭터 편은 초안 참고 자료로 보관하고 게임의 기본 진입 동선을 메인 스테이지로 바꾼다.','',
         '현재 전투는 공용 프로토타입 규칙에 단계별 적 수치·예고선 유형·파티·인간/군체 외형을 적용한다. 보스의 최종 다중 페이즈와 전용 음악은 추가 제작 대상이며, 대본의 음악 큐는 향후 음원 연결용이다.','',
         '원본: `메인스토리/campaign.json`. `python Tools/compile-campaign.py`로 공용 C# 카탈로그와 이 문서를 갱신한다.','',
         '## 스테이지 목록','', '| 번호 | 스테이지 | 종류 | 함께 움직이는 인물 | 사건과 목적 |', '|---|---|---|---|---|']
    for ci,ch in enumerate(chapters,1):
        assert ch['number']==ci and len(ch['stages'])>0
        code.extend(['            new ChapterData {',f'                Number={ci}, Title={cs(ch["title"])}, Summary={cs(ch["summary"])},','                Stages=new List<StageData> {'])
        for ni,s in enumerate(ch['stages'],1):
            assert s['number']==f'{ci}-{ni}' and s['chapter']==ci and s['id'] not in ids
            ids.add(s['id']);counts[s['kind']]+=1
            assert s['kind'] in KINDS and len(s['participants'])>=1
            if s['kind']=='Story':assert s['before'] and not s['after'] and not s['party']
            elif s['kind']=='Battle':assert not s['before'] and not s['after'] and s['enemyName']
            else:assert s['before'] and s['after'] and s['enemyName']
            if s['kind']!='Story':assert 1<=len(s['party'])<=3 and set(s['party']).issubset(s['participants'])
            code.append('                    new StageData {')
            for field,key in [('Id','id'),('Number','number'),('Title','title'),('Summary','summary'),('Location','location'),('EnemyName','enemyName'),('EnemyArt','enemyArt'),('BattleProfile','battleProfile'),('MusicId','musicId')]:
                code.append(f'                        {field}={cs(s[key])},')
            code.append(f'                        Chapter={ci}, Kind=StageKind.{s["kind"]}, IsBoss={str(s["isBoss"]).lower()}, HumanEnemy={str(s["humanEnemy"]).lower()},')
            for field,key in [('Participants','participants'),('Party','party')]:code.append(f'                        {field}=new string[] {{'+','.join(cs(v) for v in s[key])+'},')
            for field,key in [('Before','before'),('After','after')]:
                code.extend([f'                        {field}=new List<StoryLine> {{'])
                main='';other=''
                for line in s[key]:
                    assert 2<=len(line)<=3 and line[1].strip()
                    speaker,text=line[:2];cue=line[2] if len(line)==3 else ''
                    if speaker in IDS:assert IDS[speaker] in s['participants'],(s['number'],speaker)
                    current=ART.get(speaker,'')
                    if current and current!=main:other=main;main=current
                    background='keep' if any(w in s['location'] for w in ('등대','식탁','야영')) else 'ruins'
                    if (ci,ni)==(1,1):background='black'
                    code.append(f'                            new StoryLine({cs(speaker)}, {cs(text)}, {cs(background)}, {cs(main)}, true) {{ Location={cs(s["location"])}, Caption={cs(s["number"]+" · "+s["title"])}, ListenerPortrait={cs(other if other!=main else "")}, MusicCue={cs(cue)} }},')
                    lines_total+=1
                code.append('                        },')
            code.append('                    },')
            names=[name for name,id in IDS.items() if id in s['participants']]
            doc.append(f'| {s["number"]} | {s["title"]} | {KINDS[s["kind"]]} | {" · ".join(names)} | {s["summary"]} |')
        code.extend(['                }','            },'])
    code.extend(['        };','        public static StageData Get(string id) { return Chapters.SelectMany(c=>c.Stages).FirstOrDefault(s=>string.Equals(s.Id,id,StringComparison.OrdinalIgnoreCase)); }','    }','}',''])
    doc.extend(['','## 대본과 전투 연결',''])
    for ch in chapters:
        doc.extend([f'### 제{ch["number"]}장 · {ch["title"]}','',ch['summary'],''])
        for s in ch['stages']:
            doc.extend([f'#### {s["number"]} · {s["title"]}', '',f'종류: **{KINDS[s["kind"]]}** · 장소: {s["location"]}', '',s['summary'],''])
            for key,label in [('before','진입 대화'),('after','전투 후 대화')]:
                if not s[key]:continue
                doc.extend([f'**{label}**',''])
                for line in s[key]:
                    if len(line)==3:doc.extend([f'음악 큐: `{line[2]}`',''])
                    doc.extend([f'**{line[0] or "서술"}**: {line[1]}',''])
            if s['kind']!='Story':doc.extend([f'**전투:** {s["enemyName"]} / {"인간" if s["humanEnemy"] else "군체"} · 프로필 `{s["battleProfile"]}` · {"보스" if s["isBoss"] else "일반/중간 전투"}.',''])
    doc.insert(4,f'규모: {len(chapters)}장 · {len(ids)}스테이지 · 대사/서술 {lines_total}행. 스토리 {counts["Story"]}, 전투 {counts["Battle"]}, 스토리+전투 {counts["StoryBattle"]}.')
    (ROOT/'RequiemLantern/Assets/Scripts/Core/CampaignCatalog.cs').write_text('\n'.join(code),encoding='utf-8')
    (ROOT/'기획서/06_메인스토리_장과_스테이지.md').write_text('\n'.join(doc)+'\n',encoding='utf-8')
    print(f'PASS: {len(chapters)} chapters, {len(ids)} stages, {lines_total} lines; kinds={counts}.')
if __name__=='__main__':main()
