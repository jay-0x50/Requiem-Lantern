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

스테이지 요청에는 `stageId`를 전달한다. `stage-story`에는 `expectedPhase`도 전달해 중복 완료와 오래된 화면의 요청을 막는다. `campaign` 응답은 5개 장의 스테이지 목록과 해금·완료 상태, 진행 중 스테이지, `BeforeStory / Battle / AfterStory / Complete` 단계, 현재 대본을 제공한다. 혼합 스테이지의 보상은 승리 때 지급하며 완료 표시는 후속 대화가 끝난 뒤 저장한다. 승리 후 종료했다면 재진입 시 후속 대화부터 이어진다.

`synchronize`는 `actorId`에 해당하는 잔영의 동기화 1을 등불 조각 10개로 개방한다. `progress.characters`의 레벨·동기화 값은 별도로 저장한다. 전투 중에는 동기화할 수 없다.

격리 검수는 `dotnet PreviewHost.dll "프로젝트 경로" "격리 저장 경로" 8128`처럼 저장 경로와 포트를 추가로 지정한다. 기본 실행은 기존 8127과 개인 저장 파일을 사용한다.

상태 응답 구조:

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
