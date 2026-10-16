# Unity 스킬 대사·3스킬·각성·아군 지원 검수

2026-10-16. 최종 Windows 빌드에서 `--skill-flavor-smoke`를 실행해 **exit 0 / PASS / 새 캡처 19장**을 확인했다. 최종 19장을 모두 직접 열어 컷인의 얼굴과 글자, 접촉 위치, HP, 지원 문양, 툴팁, 간소화 화면, 취소 후 정리를 확인했다.

검수는 이 폴더의 `flavor-progress.xml`만 사용했다. 실제 플레이어 `progress.xml`의 검수 전후 SHA256이 일치한다. PlayerPrefs를 저장하는 코드는 호출하지 않았다.

## 확인한 동작

| 항목 | 결과 |
|---|---|
| 전원 대사 | 7명의 1·2·3스킬과 방어 계열, 총 28개 실제 행동에서 `SkillStart`가 정확히 1회 발생하고 대사와 애니메이션 키가 제공됨 |
| 대사 표시 | 일반 행동은 짧은 자막, 3스킬은 원화·스킬명·전용 대사의 컷인. 음성 녹음은 추가하지 않음 |
| 리안 | 방패를 앞세운 전진 강타. 각성 시 별도 이름·대사·강조 컷인 |
| 세라 | 빠른 준비 베기 뒤 마무리 타격. 각성 시 별도 컷인 |
| 노아 | 교차 베기와 보랏빛 SD 잔상. 각성 시 이름의 서명 컷인 |
| 미나토 | 지팡이를 들어 동료에게 치유 물결과 보호막 전달 |
| 하이델 | 제자리 반동 사격과 적 몸체의 서리 파열. 배우와 양쪽 이름표가 화면 안에 유지됨 |
| 카이 | 등불을 휘두르는 타격, 아군 지원은 별도 문양 |
| 마레 | 갈고리 당김과 무게를 실은 마무리 타격 |
| 실제 HP 접촉 | 준비 동작·컷인에서는 표시 HP가 그대로이며, 최종 `Damage` 접촉에서 정확히 한 번만 변경됨. 실제 접촉 효과음도 한 번. 연속 베기의 시각 효과에 별도 피해를 만들지 않음 |
| 치유·지원 | 치유 HP는 지원 접촉에서 한 번만 증가. 보호막·다음 공격 보너스는 실제 받는 동료의 이벤트 값으로 표시 |
| 각성 3종 | 리안·세라·노아의 3스택 각성 3스킬을 실제 실행해 소모 확인. 시각적 각성만 별도로 꾸며 내지 않음 |
| 잠긴 각성 | 동기화 0·각성 3스택에서 게이지와 툴팁 모두 동기화가 필요하다고 표시 |
| 아군 연결 | 리안 3스킬·카이 2스킬·미나토 3스킬이 살아 있는 아군을 유효한 지원 대상으로 인식하고 `Support` 연결 생성 |
| 연출 중 UI | 실제 Resolve 진입과 같은 계획 UI 숨김·하단 스킬바 후퇴를 적용. 원거리·간소화 접촉 중 임시 이름표와 충돌하는 고정 적 하단 정보는 숨긴 뒤 복구 |
| 간소화 | 원래 발 위치와 배율 1을 유지하며 피해 접촉은 정상 처리 |
| 취소·화면 이탈 | 컷인 도중과 3스킬 준비 동작 도중 허브로 이동하면 임시 배우·자막·연출 마커가 제거되고 효과음이 정지함 |
| 기존 맞불 | `Ignite`의 기존 적 예고 소유권과 양측 불 판정 패널이 유지됨 |

하이델 이름표는 실제 연출 레이어 변환을 적용한 사각형의 좌우 경계까지 검사한다. 원거리·간소화 접촉에서는 고정 적 하단 정보가 비활성인지도 자동 검사한다.

## 최종 화면 19장

| 구분 | 캡처 |
|---|---|
| 리안 3스킬 | [00 컷인](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/00-lian-s3-cut-in.png>) · [01 접촉](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/01-lian-s3-contact.png>) |
| 세라·노아 | [02 세라 접촉](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/02-sera-s3-contact.png>) · [03 노아 접촉](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/03-noa-s3-contact.png>) |
| 미나토·하이델 | [04 미나토 치유](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/04-minato-s3-contact.png>) · [05 하이델 접촉과 이름표](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/05-heidel-s3-contact.png>) |
| 카이·마레 | [06 카이 접촉](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/06-kai-s3-contact.png>) · [07 마레 접촉](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/07-mare-s3-contact.png>) |
| 각성 컷인 | [08 리안](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/08-lian-awakened-cut-in.png>) · [09 세라](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/09-sera-awakened-cut-in.png>) · [10 노아](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/10-noa-awakened-cut-in.png>) |
| 아군 지원 | [11 리안 각성 보호막 30](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/11-lian-ally-support.png>) · [12 카이 다음 공격 +3](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/12-kai-ally-support.png>) · [13 미나토 치유](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/13-minato-ally-support.png>) |
| 게이지·설명 | [14 각성 3스택·동기화 잠금 툴팁](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/14-ready-gauge-sync-lock-tooltip.png>) |
| 간소화·취소 | [15 원위치 접촉](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/15-reduced-motion-contact.png>) · [16 정리된 허브](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/16-cancelled-clean-hub.png>) |
| 일반 대사·맞불 | [17 짧은 일반 대사](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/17-normal-skill-quote.png>) · [18 양측 불 판정](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/18-clash-flame-ownership.png>) |

## 실패에서 보강한 내용

1. 최초 실행은 28개 실제 행동의 대사 데이터를 확인한 뒤 첫 리안 컷인에서 `Flavor presentation never exposed its required phase: lian SkillStart`로 실패했다. 같은 프레임의 반복 화면 구성 뒤 큰 초기 렌더링 프레임이 짧은 컷인을 건너뛰었을 가능성을 확인했으나, 그 프레임의 정확한 발생 원인까지 입증한 것은 아니다. 검수 화면 구성 사이에 프레임을 양보하고 연출의 프레임당 진행량을 최대 0.05초로 제한했다. 실패 진단에는 관측 마커·최대 불투명도·프레임 시간을 추가했다. 이후 실제 실행이 통과했다.
2. 첫 기능 통과 화면의 육안 검사에서 하이델의 제자리 사격에 근접 전투 카메라가 적용되어 본체가 화면 밖으로 밀리는 문제를 발견했다. 두 배우의 중간 지점을 기준으로 약하게 확대하도록 교정했다.
3. 최초 캡처 코드는 실제 실행 진입의 계획 UI 숨김·스킬바 후퇴를 생략했다. 정상 Resolve에 이미 있는 처리를 검수에도 적용해 최종 그림을 실제 연출 상태와 맞췄다.
4. 다음 육안 검사에서 원거리 이름표의 우측 잘림과 간소화 접촉의 고정 적 정보 중첩을 발견했다. 원거리 이름표를 발아래 중앙에 배치하고 고정 하단 정보를 접촉 동안 숨겼다. 실제 변환 후 경계·고정 정보 비활성 검사도 추가했다. 최종 재빌드·재실행에서 자동 검사와 19장 전수 육안 검수를 모두 통과했다.

현재 PNG 19장은 마지막 교정까지 포함한 최종 실행 결과다. 이전 기능 통과 화면을 최종 증거로 사용하지 않았다.

## 실행·증거와 범위

- 최종 성공 빌드: `Logs/unity-skill-flavor-v16-build-final-accepted.log`.
- 최종 실행 로그: `native-skill-flavor-smoke-final-accepted.log`.
- [PASS 결과](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/smoke-result.txt>) · [어셈블리·전체 Core/Presentation 소스·캡처 SHA256](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/native-result.json>).
- [플레이어 저장 전](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/player-save-before.json>) · [후·동일 여부](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SkillFlavor/2026-10-16/Unity/player-save-after.json>).

1600×900 일반 창, 1920×1080 기준 Canvas에서 실제 Windows 게임 컴포넌트와 전투 엔진을 실행했다. 검수는 모든 스킬을 보기 위해 격리 저장에 7명 보유·동기화 1을 부여하고, 사용할 카드·각성 변형의 3스택·적 HP 600을 고정했다. 사건은 실제 엔진이 생성했으며 화면에도 같은 생산용 연출 코루틴을 사용했다. 이는 자연스러운 획득·성장 과정이나 전투 난도 균형의 증거가 아니다. OS 마우스 입력 타이밍을 검증한 것도 아니다.

최종 게임은 검수 종료 시 정상 종료했으며, 이후 C# 소스를 변경하지 않았다.
