# Unity 대화 UX 읽기 감사

2026-10-15. 최신 Windows 소환 기능 빌드와 현재 Unity 소스, 기존 native QA 화면을 기준으로 확인했다. 이 감사에서는 게임 실행·재빌드·코드 수정·플레이어 저장 접근을 하지 않았다. 아래 개선안은 아직 구현하지 않은 제안이다.

## 결론

원화 크기, 발화자와 청자의 밝기 차이, 하단 대화창은 관계 대화를 보여 주는 기반이 있다. 1-7 식탁 장면은 두 인물의 농담과 반응을 한 화면에서 읽기 좋은 기존 사례다. 현재 본문이 잘려 보인다는 근거는 찾지 못했다. 긴 이야기를 추가하기 전에 우선 보완할 부분은 **대사마다 같은 자동 재생 간격, 최근 다섯 줄에 제한된 로그, 중간 읽기 위치를 잃는 복귀, 데이터에만 남은 감정·음악 큐**다.

## 구체적인 관측 4개

### 1. 자동 재생에 감정별 여백이 없다

- 현재 타이핑은 초당 36 UTF-16 코드 단위이며, 자동 진행은 본문 길이에 90을 더한 시점이다. 따라서 모든 문장은 완전히 나온 뒤 약 2.5초가 지나면 다음으로 넘어간다. 문장 부호·침묵·농담의 반응·마지막 독백을 구분하지 않는다.
- 현재 중앙값 33자 대사는 약 0.92초 타이핑 + 2.5초, 최장 72자는 약 2초 + 2.5초다. 수치는 코드에서 계산한 값이며 실제 읽기 속도를 측정한 결과는 아니다.
- 로그가 열리면 업데이트가 멈추는 점, 첫 클릭/Space로 전문을 펼치고 다음 입력으로 넘기는 점은 유지할 가치가 있다. `StartStory`는 새 전/후 대화 묶음마다 자동을 끈다.
- 제안: 기본 속도는 유지하면서 필요한 문장에만 완문 후 머무는 시간·문장 부호 쉼·장면 끝 여백을 명시한다. 농담은 앞말보다 동료의 반응 뒤에, 고백·상실은 결론 뒤에 시간을 준다. 매 문장에 연출을 넣는 방식은 피한다.
- 근거: [LanternApp.cs:58](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Presentation/LanternApp.cs:58>), [LanternViews.cs:65](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Presentation/LanternViews.cs:65>), [LanternViews.cs:112](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Presentation/LanternViews.cs:112>).

### 2. 로그가 관계의 앞뒤 맥락을 남기기에 짧고, 현재 문장을 미리 공개한다

- 기록창은 현재 대화 묶음의 최근 5개 항목만 표시한다. 스크롤·앞쪽 기록 탐색이 없다. 현재 대화 묶음은 4~19행이며, 전투 전/후가 새 `StartStory`로 나뉘면 이전 로그도 지운다.
- 현재 문장은 `DrawStory`에서 타이핑하기 전에 전문을 로그에 넣는다. 타이핑 중 기록을 열면 아직 나오지 않은 고백·반전의 뒷부분까지 볼 수 있다.
- 제안: 현재 전투 전/후를 포함한 읽은 대화 묶음을 탐색할 수 있게 하고, 로그에는 실제 공개한 범위 또는 이미 완문한 문장만 넣는다. 긴 대본에서는 스크롤과 마스킹을 함께 둬야 패널 밖 텍스트를 방지할 수 있다.
- 근거: [LanternViews.cs:106](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Presentation/LanternViews.cs:106>), [LanternViews.cs:113](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Presentation/LanternViews.cs:113>).

### 3. 긴 대화의 중간 복귀와 건너뛰기 범위가 드러나지 않는다

- `건너뛰기`는 한 번의 입력으로 현재 전/후 대화 묶음 끝을 처리한다. 전투 전이면 전투로, 전투 후/스토리 전용이면 기존 완료 흐름으로 이동한다. 별도의 장면 범위 안내는 없다.
- 이야기를 읽다 Escape로 거점에 간 뒤 같은 스테이지를 다시 열면 `storyIndex=0`으로 시작한다. 현재 대화 중간의 읽기 위치를 저장하거나 재개하는 경로는 없다. 완료한 스테이지는 기존 기록실에서 다시 읽을 수 있으므로 영구적으로 대본이 사라지는 문제와는 구분해야 한다.
- 제안: 우선 현재 UI에서 건너뛰는 단위를 분명히 표시하고, 장면을 길게 확장할 때는 마지막 읽기 위치에서 이어 읽기 또는 읽은 문장으로 돌아가는 방법을 제공한다. 전투/보상 게이트는 그대로 유지한다.
- 근거: [LanternViews.cs:95](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Presentation/LanternViews.cs:95>), [LanternApp.cs:65](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Presentation/LanternApp.cs:65>), [LanternCampaign.cs:141](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Presentation/LanternCampaign.cs:141>).

### 4. 감정 전환을 위한 음악 큐가 Unity 연출에 연결되어 있지 않다

- 현재 대본에는 비어 있지 않은 `MusicCue` 9개와 스테이지 `MusicId` 5개가 있다. 생성 카탈로그와 데이터 필드는 존재하지만 Unity Presentation/Editor에는 이 값을 읽는 호출이 없다. 본 감사는 실제 음원을 들은 검수가 아니라 데이터 소비 경로를 읽은 결과다.
- 배경도 생성기에서 장소 이름에 따른 `keep`/`ruins`, 1-1의 `black`으로 분류한다. 현재 원화와 밝기 차이는 좋지만 대본만 늘리면 서로 다른 장소와 감정이 같은 시각·음향 리듬으로 이어질 수 있다.
- 제안: 기존 큐를 먼저 소비하는 최소 연결을 만들고, 핵심 고백·보스 직전·회복 장면에만 음악 진입/감쇠/침묵을 배치한다. 평소의 작은 농담이나 식사 대화를 반복되는 물건·말버릇과 연결하고, 이후 상실 장면에서 그 의미가 바뀌게 쓰면 과도한 효과 없이도 관계의 축적을 보여 줄 수 있다. 이 문단의 서사 방향은 편집 제안이며 코드 결함 판정은 아니다.
- 근거: [PrototypeData.cs:107](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/RequiemLantern/Assets/Scripts/Core/PrototypeData.cs:107>), [Tools/compile-campaign.py:54](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/Tools/compile-campaign.py:54>), Presentation/Editor 대상 `MusicCue|MusicId|BossMusic|BGM|bgm` 검색 결과 소비 경로 없음.

## 대사 길이·줄수 기준

아래 크기는 1920×1080 기준 Canvas 좌표다. 1600×900 창에서는 약 0.833배, 1280×720에서는 약 0.667배로 보인다. 긴 대사를 넣기 위해 본문 글꼴부터 작게 줄이지 않는 것이 좋다.

| 항목 | 현재 글꼴·영역 | 작성 기준 |
|---|---|---|
| 본문 | `NotoSansKRRegular`, 32, 1550×128. 발화는 좌측, 서술은 중앙 정렬 | 한 카드에 1~2줄. 보통 공백 포함 30~70자, 초안 검토 상한 약 90자. 90자는 레이아웃의 보장 한도가 아니며 실제 조판으로 판정한다. 3줄이 필요하면 감정/행동 단위로 다음 카드로 나눈다. |
| 화자 | 27, 1500×48 | 1줄. 짧은 호칭을 유지하고 설명을 화자명에 붙이지 않는다. |
| 장소 | 31, 467×66 | 가급적 1줄. 긴 부제·상세 설명은 본문/장 캡션으로 옮긴다. |
| 장·스테이지 캡션 | 13, 460×23 | 짧은 정보 1줄. 감정 전달에 필요한 문장은 이 작은 영역에 두지 않는다. |
| 기록 본문 | 27, 1360×590, 항목 간 빈 줄 | 현 구조는 최근 5개만 표시하며 높이 제한을 강제하지 않는다. 대본이 길어지면 스크롤/마스킹을 함께 검수해야 한다. |

- 현재 `Label`은 가로 `Wrap`, 세로 **`Overflow`**다. 명시적인 `maxLines`나 본문 클리핑은 없다. 표의 2줄은 안전한 작성 목표이며 현재 엔진이 강제하는 제한이 아니다.
- 번들 TTF 오프라인 확인: unitsPerEm=1000, hhea ascent=1160/descent=-288/lineGap=0. 32 크기의 글꼴 행 높이는 대략 46~48 Canvas px로 추정되므로 128 높이에는 2줄을 안전 기준으로 잡는다. 이 값은 Unity `TextGenerator`의 실제 결과를 대신하지 않는다.
- Pillow로 동일 TTF의 advance를 읽어 1550 너비에 문자 단위로 줄바꿈한 참고 측정: 현재 616행의 길이는 최소 3자/중앙 33자/95백분위 56자/최대 72자. 613행은 1줄, 3행은 2줄로 추정되었다. Unity의 단어 경계·금칙 문자 조판 때문에 정확한 줄수는 달라질 수 있다.
- 기존 최장 사례: 1-8 진입 서술 72자(약 1651px), 2-5 진입 서술 69자(약 1565px), 5-9 전투 후 서술 68자(약 1578px). 확장 대본에서 이 세 장면을 비교 기준으로 삼을 수 있다.
- 명시적인 줄바꿈은 호흡을 나눌 때만 사용한다. 빈 줄을 넣어 세로 여백을 만드는 방식은 현 본문 영역에 맞지 않는다. 현재 타이핑이 문자열 부분절단을 사용하므로 새로운 rich-text 태그나 이모지를 넣을 경우 별도의 표시 검수가 필요하다.

## 대본 확장 후 좁은 검증 방법

1. 원본 `기획서/메인스토리/campaign.json`에서 공백 포함 길이·명시 개행·3줄 후보를 먼저 찾는다. 같은 번들 TTF로 너비를 계산하는 검사는 빠른 선별용으로만 쓴다.
2. 다음 실제 Unity 검수 때 생성기/컴파일을 마친 뒤 `TextGenerator`와 실제 Text 설정으로 줄수, preferred height, glyph bounds를 확인한다. 본문 목표는 2줄 이내이며 높이와 계속 표시/하단 진행 번호의 겹침도 본다. 글자수만으로 PASS 처리하지 않는다.
3. 1600×900과 1280×720에서 가장 긴 발화·서술, 강제 2줄, 긴 장소명, 감정 여운 문장, 5개 긴 로그를 실제 화면으로 본다. 문구 변경마다 전체 전투 검수를 반복할 필요는 없다.
4. 문장 중 클릭/Space → 전문 공개 → 다음 문장, 자동 켠 상태의 로그 열기/닫기, 타이핑 중 로그의 미공개 범위, 전투 전·후 스킵 단위, Escape 후 재진입을 좁게 검수한다. 음악을 연결한다면 큐 진입 1회/중복 재생 방지/로그 중 처리/화면 이탈 정지도 함께 확인한다.

이번 감사에서는 위 검증을 위한 새 smoke나 런타임 코드를 추가하지 않았다.

## 기존 native 대표 화면 추천

외부 검토용 최신 Browser 캡처를 루트가 별도로 준비 중이다. 아래는 **이미 있는** Unity 이미지 추천이며, 시점과 테스트 알림을 감추지 않고 적었다.

| 화면 | 추천 파일 | 활용과 주의 |
|---|---|---|
| 타이틀 | [01-title-click-to-start.png](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/ClickToStart/2026-10-15/Unity/01-title-click-to-start.png>) | 현재 타이틀 레이아웃과 같은 구성. 클릭 투 스타트와 별도 설정 확인. |
| 메인/거점 | [05-returning-player-hub.png](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/ClickToStart/2026-10-15/Unity/05-returning-player-hub.png>) | 기존 원화·이야기 목표·여정/기록 구성 확인. 소환 도입 **이전** 캡처여서 하단 첫 버튼이 `기록실`; 현재 소스/최종 빌드는 `잔영 소환`이다. 최신 거점 증거로 오인하지 않는다. |
| 스토리 | [15-campaign-ensemble.png](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/VoyageCombat/2026-10-15/Unity/15-campaign-ensemble.png>) | 1-7 식탁, 미나토의 반응과 세라의 청자 원화. 대화창·발화자 대비·농담 연출 검토에 가장 유용하며 현재 대화 UI와 같은 구성이다. |
| 스테이지 | [07-saved-sortie-party.png](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/Recruitment/2026-10-15/Unity/07-saved-sortie-party.png>) | 최신 저장 편성·완료+다음 노드·항해도. 상단에 직전 **의도적인 저장 실패 검수** 알림이 남아 있으므로 정상 플레이에서 생긴 저장 장애로 해석하지 않는다. 알림 없는 항로 전체 구성 참고는 VoyageCombat의 `31-voyage-full-chart.png`. |
| 전투 | [08-acquired-party-in-battle.png](<D:/Project/레퀴엠 랜턴 (Requiem Lantern)/QA/Recruitment/2026-10-15/Unity/08-acquired-party-in-battle.png>) | 최신 카이 SD·획득 세라 편성·3적·작은 의도 타일. 상단 알림은 **전투 중 편성 시도를 차단하는 검수**에서 발생했다. 일반 화면의 상시 안내가 아니다. |

정상 게임 화면만을 외부에 보여 줄 목적이라면 QA 알림이 없는 최신 Browser 캡처를 우선 사용하는 편이 정확하다. 이 감사를 위해 기존 이미지를 수정하거나 새 Unity 화면을 만들지는 않았다.
