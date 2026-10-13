"""Export the archived character draft as Markdown. Gameplay uses compile-campaign.py."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "기획서/캐릭터스토리/character-stories.json"


def main():
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    assert data["schemaVersion"] == 1
    stories = data["characters"]
    assert [s["id"] for s in stories] == ["lian", "sera", "noa"]
    doc = ["# 보관용 캐릭터 서사 초안 · 리안 / 세라 / 노아", "",
           "2026-10-13 정리. 이 문서는 이전 독립편의 개발 참고 자료다.", "",
           "**게임에서 한 캐릭터의 이야기는 메인스토리 한 장으로 진행한다.** 리안은 1장, 세라는 2장, 노아는 3장의 중심 인물이다. 합류 이유·과거·증오와 미련·결전·귀환을 메인 스테이지에 통합했다. 다른 동료의 대화와 세계관 진행도 같은 장에서 이어진다.", "",
           "현재 플레이 대본과 보스 배치는 [메인 장·스테이지](06_메인스토리_장과_스테이지.md)가 기준이다. 아래 초안의 보스명·장면 순서·음악 전환은 현재 구현과 다를 수 있다. 별도 캐릭터 메뉴와 독립 대본 카탈로그는 게임에서 사용하지 않는다.", "",
           "보관 원본: `캐릭터스토리/character-stories.json`. 이 도구는 이 Markdown만 갱신한다. 현재 게임 대본은 `메인스토리/campaign.json`을 수정하고 `python Tools/compile-campaign.py`로 내보낸다.", "",
           "음악: [가사 없는 메인 장 결전 BGM 프롬프트](04_Suno_엔딩보스_BGM_프롬프트.md).", ""]
    count = 0
    for story in stories:
        boss = story["boss"]
        doc.extend([f'## {story["name"]} · 이전 초안 {story["title"]}', "", story["summary"], "",
                    f'**합류 이유:** {story["joinReason"]}', "", f'**과거:** {story["past"]}', "",
                    f'**증오와 미련:** {story["resentment"]}', "",
                    f'**초안의 결전:** {boss["name"]} / {boss["kind"]} / {boss["faction"]}', "",
                    f'**빛의 대가:** {story["cost"]}', "", f'**이후 변화:** {story["ending"]}', ""])
        for person in story.get("supportingCast", []):
            doc.extend([f'**{person["name"]}:** {person["relationship"]}', ""])
        for index, scene in enumerate(story["scenes"], 1):
            doc.extend([f'### {index}. {scene["title"]}', "", f'장소: {scene["location"]}', ""])
            for line in scene["lines"]:
                assert 2 <= len(line) <= 3 and line[1].strip()
                if len(line) == 3:
                    doc.extend([f'초안 음악 큐: `{line[2]}`', ""])
                doc.extend([f'**{line[0] or "서술"}**: {line[1]}', ""])
                count += 1
    (ROOT / "기획서/03_캐릭터스토리_리안_세라_노아.md").write_text("\n".join(doc) + "\n", encoding="utf-8")
    print(f"PASS: archived draft Markdown only; {len(stories)} characters, {count} lines; no runtime catalog exported.")


if __name__ == "__main__":
    main()
