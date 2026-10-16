# 로컬 플레이 프리뷰 서버

Unity용 `Assets/Scripts/Core`의 동일 C# 코드를 직접 컴파일한다. Unity 라이선스가 없어도 전투와 저장 동작을 확인할 수 있다. 그래픽 UI는 루트의 `Preview` 폴더가 담당한다.

## 실행

루트 디렉터리의 PowerShell에서:

```powershell
dotnet build PreviewHost/PreviewHost.csproj -o PreviewHost/bin/live
dotnet PreviewHost/bin/live/PreviewHost.dll
```

브라우저에서 `http://127.0.0.1:8127/`에 접속한다. .NET 10 SDK 및 ASP.NET Core 10 런타임을 사용하며 외부 NuGet 패키지는 필요하지 않다. 서버는 loopback만 수신한다. 종료는 실행 터미널에서 Ctrl+C.

저장 위치는 `PreviewHost/.local/progress.xml`. Unity의 저장 경로와 분리되어 있다. 전투 중간 상태는 메모리에 유지되며 새로 실행하면 새 전투로 시작한다. 스토리 완료, 첫 클리어, 재화와 등탑 단계는 저장한다.

## API

- `GET /api/state`: 현재 상태, 추천 미리보기, 스토리, 진행 데이터.
- `POST /api/action`: JSON 요청, 변경 후 같은 상태를 반환. 실패는 HTTP 400과 `error`.
- `GET /art/<file>`: Unity `Resources/Art` 최상위 이미지.
- `GET /fonts/<file>`: Unity `Resources/Fonts` 최상위 폰트.
- `GET /...`: `Preview` 내부의 허용된 정적 파일.

JSON 키는 camelCase, enum은 문자열이다.

```json
{"action":"link","actorId":"lian","slot":0,"skillId":"lian_breakthrough","kind":"Clash","targetId":"1:anger_core"}
```

```json
{"action":"resolve","grades":{"lian:0":"Perfect","sera:0":"Good","sera:1":"Miss"}}
```

지원 action: `recommend`, `link`, `remove`, `clear`, `reuse`, `resolve`, `next`, `restart`, `lantern`, `prologue`, `reward`, `repair`, `stage-enter`, `stage-replay`, `stage-story`, `stage-exit`, `synchronize`.

모집·편성: `recruit`, `redeem-code`, `equip-character`.

주기 콘텐츠: `activity-check-in`, `quest-claim` (`questId`, `periodKey`), `dungeon-enter` (`dungeonId`: coast/reef/watch, `practice`), `dungeon-reward`, `dungeon-exit`. 상태는 `activities`에 기간·목표·보상·해금과 공유 주간 횟수, `dungeon`에 현재 원정·연습 여부를 제공한다. 의뢰 보상은 `activityReward`, 원정 보상은 `dungeonPrize`로 반환한다. 던전 승리는 명시 `dungeon-reward` 저장에 성공한 뒤 세션을 닫으며 실패하면 같은 승리를 유지한다. 일반 `reward`로 던전 보상을 대신 받을 수 없다.

승리 보상을 받지 못한 상태의 이탈은 UI에서 먼저 저장을 재시도한다. 본편 보상·후속 이야기 저장 실패도 진행 데이터와 캠페인 단계를 함께 복원한다. 본편과 던전은 동시에 진행할 수 없다.

스테이지 요청에는 `stageId`를 전달한다. `stage-story`에는 `expectedPhase`도 전달해 중복 완료와 오래된 화면의 요청을 막는다. `campaign` 응답은 5개 장의 스테이지 목록과 해금·완료 상태, 진행 중 스테이지, `BeforeStory / Battle / AfterStory / Complete` 단계, 현재 대본을 제공한다. 혼합 스테이지의 보상은 승리 때 지급하며 완료 표시는 후속 대화가 끝난 뒤 저장한다. 승리 후 종료했다면 재진입 시 후속 대화부터 이어진다.

`synchronize`는 `actorId`에 해당하는 보유 잔영의 동기화 1을 등불 조각 10개와 전용 재료(★5 10개 / ★4 6개)로 개방한다. `progress.characters`의 레벨·동기화 값과 `progress.syncMaterials`의 `{characterId,count}` 재고는 별도로 저장한다. 기존 완료된 동기화는 보존하며, 전투 중에는 동기화할 수 없다.

전용 재료는 `materials` 스냅샷의 `entries`에서 이름·보유량·필요량·소유·동기화 가능 여부를 확인한다. `material-enter` (`actorId`)로 기억 잠항에 입장하고, 실제 승리 후 `material-reward` 저장이 성공하면 전용 재료 3개를 지급한 뒤 세션을 종료한다. `material-exit`은 무보상 퇴각이다. 현재 세션은 `materialDungeon`, 수령 결과는 `materialReward`에 반환한다. 일반 `reward`로 대신 받을 수 없다. 본편·잔향 회수·기억 잠항은 동시에 진행할 수 없다. 미보유 재료도 미리 채집할 수 있으며, 주간 횟수와 재화 소모는 없다. 실제 승리 수령 기록은 전투 의뢰에 반영한다.

전용 재료 테스트 쿠폰 `LANTERN-SYNC-MATERIALS`는 기존 두 쿠폰과 독립적으로 저장당 한 번, 7종 각각 9,999,999개를 지급한다. 이 결과에만 `couponReward.syncMaterials` 배열을 반환한다. 기존 쿠폰에는 해당 필드가 null이다. 모든 종류의 보관 한도를 먼저 검사하며 저장 실패 시 재고·쿠폰 사용 기록을 함께 복원한다.

격리 검수는 `dotnet PreviewHost.dll "프로젝트 경로" "격리 저장 경로" 8128`처럼 저장 경로와 포트를 추가로 지정한다. 기본 실행은 기존 8127과 개인 저장 파일을 사용한다.

초기화 경계 검수에만 네 번째 인자로 작업공간 `QA/` 아래의 시계 파일을 전달할 수 있다. 파일에는 UTC ISO 시각을 쓰며 서비스가 현재 시각 대신 읽는다. 예: `dotnet PreviewHost.dll "프로젝트 경로" "격리 저장 경로" 8131 "프로젝트 경로/QA/RecurringContent/2026-10-16/API/clock.txt"`. 일반 실행은 해당 인자가 없고 실제 `DateTime.UtcNow`를 사용한다. API로 시각을 바꾸는 기능은 없다.

상태 응답 구조:

스킬 대사와 전용 연출은 `SkillStart` 이벤트의 `quote`, `animationKey`, `awakened`, `skillId/skillName/skillNumber`로 전달한다. `SkillStart.targetId`는 실제 대상 유닛이며 맞불의 예고 식별자는 `intentId`에 둔다. 기존 점화 화면과 호환되도록 `Ignite.targetId`는 연결한 예고선·부위 식별자를 유지한다. 후속 피해·치유·방어·점화에는 같은 행동 복사본의 메타데이터를 사용한다. 클라이언트가 위력이나 각성 여부를 제출하지 않는다.

`battle.allies`의 `awakeningStacks/awakeningMaxStacks/awakeningName/awakeningDescription`, `barrier`, `supportPower`는 전투 상태다. `AwakeningCharge/Ready/Used`, `SupportApplied`, `ShieldAbsorb` 이벤트로 상태 표시를 갱신한다. `CanSupportAlly` 공격 스킬은 `link`의 `kind: Support`와 살아 있는 아군 `targetId`를 사용하며 기존 카드·행동·불씨 검증을 통과해야 한다. 각성과 다음 공격 강화는 실제 유효 행동에서만 소비하며 `previews`를 조회해도 변하지 않는다.

```text
battle: BattleState
progress: ProgressData
plannedCost / availableEmber: number
previews: [{actorId, slot, preview: ClashPreview}]
story: {prologue, firstMeal, epilogue}
warning / error: string or null
reward: RewardData or null
```

`resolve` 뒤 `battle.phase`가 `WrapUp`이면 `next`로 다음 라운드를 연다. `Finished`이고 `battle.outcome`이 `Victory`이면 `reward`로 한 번 보상을 저장한다. `restart`는 진행 데이터를 유지하며 새 전투를 만든다.
