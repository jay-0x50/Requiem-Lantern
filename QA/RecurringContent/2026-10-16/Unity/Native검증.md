# Unity 일일·주간 보급과 잔향 회수 검수

2026-10-16. 최종 Windows 빌드를 `--recurring-smoke`로 실행해 **exit 0 / PASS**, 새 화면 10장을 확인했다. 사용한 저장 파일은 이 폴더의 `recurring-progress.xml`이며, 게임의 기존 플레이어 저장과 PlayerPrefs는 변경하지 않았다. 소스 수정 없이 최종 빌드를 검수했다.

## 확인한 결과

| 항목 | 결과와 근거 |
|---|---|
| 거점 진입 | 원화가 유지되는 기존 거점에 보급 게시판·잔향 회수 버튼이 표시됨 |
| 의뢰 화면 | 일일 4개·주간 4개 목표, 진행 수치, 보상, 진행 중·수령 가능·수령 완료 상태가 구분됨 |
| 일일 체크인 | 거점 방문 뒤 수령 가능, 실제 버튼 수령·이미 실행한 버튼 재호출에도 한 번만 지급 |
| 해금 | 본편 1-2 전투와 후속 이야기 완료 전에는 세 출항 버튼 비활성, 직접 호출도 거절 |
| 본편 저장 실패 | 실제 1-2 승리에서 저장 경로를 막자 재화·의뢰 진행·캠페인 단계가 모두 복구됨. 저장 재시도 성공 뒤에만 후속 대화 진입 |
| 후속 이야기 저장 실패 | 마지막 대화 완료 저장이 실패하면 완료 표시가 추가되지 않음. 재시도 콜백이 유지되고 성공 뒤 1-2가 완료됨 |
| 실제 던전 전투 | 리안·미나토·하이델·마레, 기본 성장·미동기화 상태로 쉬움 2명·보통 3명·어려움 보스를 모두 실제 전투 엔진으로 승리. 적 HP나 전투 결과를 강제로 변경하지 않음 |
| 퇴각 | 실제 퇴각 창의 귀환 버튼 실행 뒤 공유 보상 횟수 유지, 던전 세션 종료 |
| 보상 수령 | 던전 결과 화면을 여는 것만으로 지급되지 않음. 수령 클릭 뒤 저장이 성공하면 지급·횟수 차감·귀항 |
| 수령 저장 실패 | 고의로 막은 QA 저장 경로에서 재화·퀘스트 지표·주간 횟수·수령 기록 복구. 승리 세션과 수령 버튼 유지, 정상 경로 재시도 성공 |
| 중복과 세션 분리 | 이전 수령 버튼을 재실행해도 추가 지급 없음. 세 난도가 공유 3회를 사용하며 본편 완료·최초 보상 목록은 변경되지 않음 |
| 주간 의뢰 | 던전 보상 3회 수령 목표가 완료되어 실제 의뢰 보상을 수령함 |
| 연습 | 공유 횟수 소진 후 출항 버튼에 연습 명시. 실제 연습 승리·완료 저장 뒤 모든 재화와 의뢰 진행 지표가 그대로 유지됨 |
| 기간 초기화 | 주입한 UTC 시계로 매일 오전 5시 KST에 일일만 초기화, 월요일 오전 5시 KST에 주간 목표·수령·공유 횟수 초기화 확인 |
| 시계 역행과 재실행 | 미래 기간을 확인한 뒤 기기 시계를 뒤로 돌려도 지난 기간이 다시 열리지 않음. 최종 XML 재로드 내용 일치 |
| 조판 | 실제 Unity TextGenerator로 목표 설명·보상·던전 설명·규칙·우측 안내의 높이를 검사. 10장 모두 잘림·겹침 없이 시각 확인 |

## 화면

1. [거점 진입 버튼](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/01-hub-supply-entry.png>)
2. [1-2 완료 전 해금 제한](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/02-locked-until-main-story.png>)
3. [일일 수령 완료와 진행 목표](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/03-daily-claimed-and-progress.png>)
4. [3난도와 주간 공유 보상](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/04-three-salvage-difficulties.png>)
5. [보통 난도 3명 전투](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/05-three-enemy-weekly-battle.png>)
6. [어려움 보스의 첫 턴 예고](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/06-weekly-boss-intents.png>)
7. [저장 실패 뒤 승리 화면과 재수령 버튼 유지](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/07-reward-save-failure-retry.png>)
8. [주간 던전 의뢰 수령과 남은 의뢰](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/08-weekly-quest-reward-claimed.png>)
9. [보상·의뢰 진행이 없는 연습 결과](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/09-practice-result.png>)
10. [다음 주 초기화된 목표](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/10-next-week-reset.png>)

02의 해금 안내, 03의 중복 수령 안내, 04의 희미한 이전 저장 실패 안내, 07의 저장 실패 문구는 검수에서 의도적으로 만든 상황이다. 04는 정상 저장 재시도 뒤 해금된 화면이며, 이전 알림이 사라지기 전에 캡처했다. 07은 재시도 버튼과 승리 세션을 보존하는 모습을 확인한 뒤 실제 정상 저장으로 복구했다.

## 실행 자료와 범위

- [실행 결과](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/smoke-result.txt>)
- [소스·실행 어셈블리·캡처 해시](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/RecurringContent/2026-10-16/Unity/native-result.json>)
- 빌드 로그: `Logs/unity-recurring-v14-build.log`, 성공 빌드. 로그 전체는 출력하지 않았다.

화면은 1600×900 창과 1920×1080 기준 Canvas에서 생성했다. 검수 코드는 실제 게임 컴포넌트의 버튼 이벤트와 실제 전투 엔진을 호출했다. 새 전투 애니메이션·소리·OS 입력 타이밍 검수로 해석하지 않는다. 초기화 시각은 별도 UTC 시계를 주입해 검사했으며 OS 시계는 변경하지 않았다.

첫 숨김 창 실행은 Unity가 캡처용 렌더 텍스처를 제공하지 않아 첫 화면에서 종료됐다. 일반 창으로 다시 실행해 위 검수를 모두 통과했으며, 최종 결과와 10장 캡처는 모두 재실행에서 생성됐다.
