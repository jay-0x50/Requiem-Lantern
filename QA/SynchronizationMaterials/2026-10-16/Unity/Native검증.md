# Unity 전용 재료와 기억 잠항 검수

2026-10-16. 최종 Windows 빌드에서 `--material-smoke`를 실행해 **exit 0 / PASS**를 확인했다. 새 캡처 10장을 모두 직접 열어 글자·재료 수량·버튼·상태 표시를 확인했다. 실제 Unity TextGenerator 높이 검사도 통과했다.

검수 저장은 이 폴더의 `material-progress.xml`만 사용했다. 실제 플레이어 `progress.xml`의 검수 전후 SHA256이 일치하며 PlayerPrefs는 변경하지 않았다.

## 결과

| 항목 | 결과 |
|---|---|
| 전용 재료 필요 | 리안 보유·등불 10개만 있는 상태에서 전용 재료 0/10 표시, 동기화 비활성 |
| 획득처 | 잔영 기록의 기억 잠항 버튼이 해당 인물의 재료를 선택해 열림 |
| 일곱 항로 | 보유·미보유와 보유량/필요량이 한 목록에 표시됨. 큰 원화·설명·승리 수령량 3개·무제한/무소모 구분 |
| 해금 | 1-2 완료 전 출항 비활성, 직접 호출도 거절. 실제 본편 1-2 전투와 후속 대화를 완료한 뒤 해금 |
| 실제 전투 | 기본 미동기화 리안·미나토·하이델·마레로 마레 항로 2승, 세라 항로 1승. 적 HP나 결과를 강제로 바꾸지 않음 |
| 명시적 수령 | 결과 화면 진입만으로 재료가 지급되지 않음. 수령 클릭 뒤 선택 재료만 3개 지급 |
| 저장 실패 | 재료 수령 저장 경로를 고의로 막으면 재료·의뢰 지표·수령 기록이 모두 복구되고 승리 세션·버튼 유지. 정상 저장 재시도 성공 |
| 중복 방지 | 이전 수령 버튼을 재실행해도 두 번 지급되지 않음 |
| 재출격·퇴각 | 수령 후 같은 항로로 다시 잠항 가능. 새 잠항에서 퇴각해도 재화·재료·진행도 변화 없음 |
| 동기화 | 마레 2승으로 전용 재료 6개 획득. 저장 실패에서는 재료/등불/동기화 상태 보존, 재시도에서 정확히 재료 6개와 등불 10개 소모·동기화 1·3스킬 개방 표시 |
| 미보유 선채집 | 미보유 세라의 전용 재료 3개 획득 가능, 캐릭터 소유나 동기화 권한은 부여되지 않음 |
| 신규 쿠폰 | `LANTERN-SYNC-MATERIALS` 자동 입력. 실패 저장은 코드·보관량 복원, 정상 저장은 7종 각각 정확히 +9,999,999개 지급, 같은 코드 재사용 거절 |
| 기존 진행 분리 | 재료 전투는 주간 던전 보상 횟수와 본편 완료·최초 보상 목록을 변경하지 않음. 승리 전투 의뢰에는 정상 집계 |
| 재실행 | 최종 XML 재로드 뒤 7종 보관량·동기화·쿠폰·미보유 상태 일치 |

## 화면

1. [전용 재료가 부족한 리안](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/01-roster-character-material-required.png>)
2. [해금 전 일곱 항로와 비활성 출항](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/02-seven-materials-before-unlock.png>)
3. [마레의 붉은 인양 고리 채집](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/03-mare-material-unlimited-route.png>)
4. [2명 적과 실제 기본 파티](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/04-two-enemy-material-battle.png>)
5. [저장 실패 뒤 동일 승리·수령 버튼 유지](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/05-material-reward-save-retry.png>)
6. [재료 6개를 모아 동기화 가능](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/06-mare-ready-to-synchronize.png>)
7. [전용 재료 6개·등불 10개 소모 뒤 완료](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/07-mare-synchronized-s3-unlocked.png>)
8. [미보유 세라의 재료 3개 선채집](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/08-unowned-sera-prefarmed-material.png>)
9. [전용 재료 쿠폰 입력과 세 종류의 채우기 버튼](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/09-dedicated-material-test-coupon.png>)
10. [일곱 재료의 쿠폰 지급 후 보관량](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/10-seven-inventories-persisted.png>)

02의 해금 거절 안내와 05의 저장 실패 안내는 의도적으로 검증한 상태다. 03·04에는 해금 전 안내가 사라지기 전의 희미한 알림이 남아 있다. 마지막 화면의 세라 재료 10,000,002개는 선채집 3개에 쿠폰 9,999,999개를 더한 정확한 값이다.

## 실행과 해석 범위

- [실행 결과](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/smoke-result.txt>)
- [어셈블리·소스·캡처 해시](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/native-result.json>)
- [플레이어 저장 전](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/player-save-before.json>) · [후](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/SynchronizationMaterials/2026-10-16/Unity/player-save-after.json>)
- 최종 성공 빌드 로그: `Logs/unity-sync-material-v15-build-final.log`.

1600×900 일반 창, 1920×1080 기준 Canvas에서 확인했다. 검수는 실제 게임 컴포넌트의 버튼 이벤트와 실제 전투 엔진을 코드로 호출한다. 새 애니메이션·효과음·OS 마우스 입력 타이밍을 검사했다는 의미는 아니다. 고정 UTC는 진행 서비스에만 주입했으며 시스템 시계는 바꾸지 않았다.

최초 빌드는 새 Core 스크립트의 33자리 메타 GUID 때문에 해당 파일이 무시되어 실패했다. 루트가 32자리 GUID로 수정한 뒤 최종 재빌드가 성공했으며, 위 결과는 모두 그 최종 빌드에서 생성됐다.
