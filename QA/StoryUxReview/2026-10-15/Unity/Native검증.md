# Unity 최종 검수 · 마레와 대화 읽기

2026-10-15. 루트가 만든 최종 Windows 빌드에서 격리된 QA 저장 경로로 두 실행을 순차 진행했다. 두 프로세스 모두 exit 0으로 종료했다. 플레이어 저장과 PlayerPrefs는 변경하지 않았다. 새 기능에 필요한 좁은 검수이며 기존 전체 전투/소환 검수는 반복하지 않았다.

## 결과 요약

| 항목 | 결과 | 근거 |
|---|---|---|
| 최종 canonical 본문 | **690행 PASS** | 실제 Unity `TextGenerator`, 번들 `NotoSansKRRegular`, 32 Canvas px, 너비 1550 / 높이 128 |
| 본문 줄수·높이 | **최대 2줄 / preferredHeight 최대 92px** | 모든 최종 본문이 설정된 영역에 들어감. 오프라인 글자수 추정으로 대체하지 않음 |
| 읽은 범위의 로그 | PASS | 과거 첫 문장 포함, 현재 문장의 공개된 7자만 포함, 아직 나오지 않은 나머지 제외 |
| 스크롤·마스킹 | PASS | 긴 로그의 처음/최근으로 실제 `ScrollRect` 이동, 마스크 안 표시 |
| 로그·포커스·일시정지 | PASS | 로그 중 입력·타이핑·자동 시간 정지. 닫을 때 현재 글자수 유지/hold 0. Focus/Pause 콜백으로 진행 정지와 복귀 hold 0 확인 |
| 감정 대사 자동 진행 | PASS | canonical의 4.8초 행: 4.79초에 유지, 경계 이후 1회 진행. 수동 진행은 즉시. 자동과 Space가 같은 프레임이어도 한 줄만 진행 |
| 인물 위치 | PASS | 1-4의 두 인물 화자 교대에서 좌/우 위치 유지 |
| 실제 전투·완료 게이트 | PASS | 1-2 전 대화 마지막 `전투 시작` → 실제 전투 승리 → 후 대화 마지막 `기항지 완료` → 스테이지 완료. 전 대화만으로 완료·보상 처리되지 않음 |
| 다시 읽기 | PASS | 전/후 경계 `이어서 읽기`, 마지막 `항해 기록으로`. 전투 재진입·추가 보상 없이 기록 화면 복귀 |
| 신규 ★4 마레 | PASS | 신규 4인 리안/미나토/하이델/마레. 마레 보유·별수·전용 원화, 전용 SD 6포즈의 발 고정/비균일 하단 셀 확인 |
| 마레 맞불·실타격 | PASS | 실제 손패로 연결, 아군 이름·적/예고 번호·아군/적 기대위력 라벨. 실제 Core 피해 이벤트에서 마레 갈고리 SD·접촉 효과음 1회·접촉 시 표시 HP 1회 반영·원래 SD 복귀 |

기존 저장 호환과 소환 풀은 `RecruitmentTests`에서 별도로 통과했다. v1의 카이 소유·현재 편성·성장·재화·천장·확정·RNG·쿠폰·완료 기록을 보존하고 마레만 한 번 추가한다. v2는 재지급/강제 편성 변경을 하지 않는다. 신규 소환의 ★4 풀은 미나토/하이델/마레이며 기존 보유 카이는 계속 사용할 수 있다. Core 총 87검사와 신규 기본 파티의 25개 전투 seed 0 전승을 확인했다.

## 실행과 상세 자료

- `--mare-smoke`: [결과](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/MareCompanion/2026-10-15/Unity/smoke-result.txt>), 새 캡처 3장.
- `--story-reading-smoke`: [결과](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/StoryUxReview/2026-10-15/Unity/smoke-result.txt>), 새 캡처 4장.
- [실제 조판 요약](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/StoryUxReview/2026-10-15/Unity/layout-summary.txt>), [690행별 조판 수치](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/StoryUxReview/2026-10-15/Unity/canonical-layout.tsv>).

## 대표 사진과 해석 범위

- [마레 원화·보유 기록](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/MareCompanion/2026-10-15/Unity/01-mare-owned-four-star.png>): 원화와 전투 SD 분리, 실제 신규 기본 소유 상태.
- [마레와 소유자가 명시된 맞불 상세](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/MareCompanion/2026-10-15/Unity/02-mare-party-and-named-clash.png>): 현재 Native는 예측 목록을 새로 만들지 않고 기존 상세의 배정/후보 두 곳에 라벨을 붙였다.
- [마레 실제 갈고리 접촉](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/MareCompanion/2026-10-15/Unity/03-mare-real-hook-contact.png>): 실제 Core 피해 이벤트를 독립적으로 재생한 연출 검수다. 이 fixture에서는 스킬바를 그대로 둔 채 해당 배우/피해/효과음만 확인했다.
- [최신 읽은 범위](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/StoryUxReview/2026-10-15/Unity/01-log-latest-read-prefix.png>) / [맨 앞 기록](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/StoryUxReview/2026-10-15/Unity/02-log-first-lines.png>): 같은 1-3 장면에서 앞뒤로 스크롤. 마지막 항목의 부분 문장은 아직 타이핑하지 않은 글자를 숨긴 정상 상태다.
- [고정된 좌우 화자](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/StoryUxReview/2026-10-15/Unity/03-stable-speaker-sides.png>): 1-4 원문을 독립 대화 fixture로 그려 위치를 검사했다. fixture에는 진행 중 Campaign이 없어 마지막 안내가 `이야기 마치기`다. 실제 혼합 스테이지의 전투 진입 문구는 별도의 실제 1-2 흐름에서 검증했다.
- [실제 후 대화 완료 안내](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/StoryUxReview/2026-10-15/Unity/04-after-story-completion-prompt.png>): 실제 1-2 승리 뒤 대화이며 `기항지 완료`와 기존 보상 게이트를 확인했다.

## 검수 한계

포커스/일시정지는 실제 게임 컴포넌트의 Unity 콜백을 검수 코드에서 호출했다. OS 창 전환 자체를 자동으로 조작하거나 별도 사용자 입력을 주입한 검수로 표현하지 않는다. TextGenerator 수치는 1920 기준 Canvas 영역이며 실제 캡처는 1600×900이다. 음악 재생은 이번 범위에 포함하지 않았다.
