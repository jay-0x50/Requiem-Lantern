# Suno · 캐릭터 중심 메인 장 결전 BGM 프롬프트

갱신일: 2026-10-14 · 음악 기획 초안 v0.4 · 메인 장 결전 연계

기존 ★5 캐릭터 **리안·세라·노아**의 메인 1~3장 결전과 연결한다. 현재 대본은 [메인 장·스테이지](06_메인스토리_장과_스테이지.md)가 기준이다. 별도 개인편 완료 뒤에 재생하는 음악이 아니다. 아래 문장은 음악 생성용 프롬프트이며 가사는 포함하지 않는다.

참고한 전투 감정은 캐릭터의 과거가 보스전에서 터지고, 마지막에 자기 결의를 선택하는 흐름이다. 음악은 각 캐릭터의 성격과 이야기에서 새로 설계했다. 공통 장르로 세 곡을 만들기보다, 서로 다른 악기·리듬·감정 변화로 누가 중심인 전투인지 들리게 한다.

**실제 생성 결과:** 2026-10-14 Suno `v6-mini`로 L2·S2·N2 결전곡을 각각 두 후보씩, 총 여섯 곡 생성했다. 각 A 후보의 MP3를 로컬 감상용으로 받았다. 후보 링크·입력 프롬프트·파일 정보는 [생성 기록](../Art/Audio/Music/README.md)에 정리했다. 무료 계정의 비상업용 후보이며, 목소리 혼입·전투 믹스·루프 구간 검수와 정식 게임 연결은 남아 있다.

## 1. Suno에 넣는 방법

모델과 화면 버전에 따라 입력 항목이 달라진다. 2026-10-14 웹 `v6-mini` 고급 화면에는 가사 입력을 비우면 연주곡으로 둔다는 안내가 있고, 별도 Instrumental 스위치와 Exclude 입력은 보이지 않았다. 이 경우 사람의 목소리와 제외할 악기를 Styles에 함께 적는다. 이번 실행은 리안에 고급 모드, 세라·노아에 동일한 음악 조건을 넣은 심플 모드를 사용했다. 실제 입력은 1,000자 제한에 맞춰 원본의 감정·악기·리듬을 유지하며 압축했다.

1. Create에서 **Custom**을 선택한다.
2. **Instrumental을 켠다.** Lyrics는 비워 둔다.
3. 원하는 곡의 아래 `Styles` 블록을 붙여 넣는다.
4. Advanced Options의 **Exclude**에 공통 제외 목록과 캐릭터별 추가 제외 목록을 넣는다.
5. Title에는 곡 제목을 넣는다. 1페이즈, 결전, 여운을 각각 생성해 비교한다.

Custom의 Instrumental과 Styles 입력은 [Suno 공식 Custom 안내](https://help.suno.com/en/articles/3197377), Exclude 입력은 [공식 제외 요소 안내](https://help.suno.com/en/articles/3161921)에서 확인했다. 확인일은 2026-10-12다.

**모든 곡에 사용하는 Exclude:**

```text
vocals, singing, lyrics, spoken words, narration, rap, choir, chanting, humming, wordless vocals, vocal chops, sampled human voices
```

가사가 없는 합창도 이번 음악에서는 제외한다. 캐릭터의 목소리와 전투 대사가 들어갈 자리를 악기로 남긴다. 출력에 목소리가 섞이면 Instrumental과 Exclude를 확인한 뒤 다시 생성한다.

아래 BPM·조성·구조·길이·루프는 **제작 목표**다. 생성 결과가 정확히 일치하거나 별도 생성곡이 같은 멜로디를 공유한다고 보장하지 않는다. 결과를 듣고 선정한 뒤 루프 지점과 전환을 편집한다. 우선 결전곡부터 캐릭터마다 2~4개 후보를 비교하고, 선택한 곡의 악기와 모티프를 기준으로 다른 페이즈를 맞춘다.

Creative Sliders가 보이면 첫 시도는 Weirdness 25~35%, Style Influence 70~85%를 **시험값**으로 사용한다. 이는 공식 권장 수치가 아니라 이번 편성의 일관성을 확인하기 위한 시작점이다. 슬라이더 의미는 [Suno 공식 Creative Sliders 안내](https://help.suno.com/en/articles/6141377)를 참고한다.

## 2. 세 캐릭터의 음악 구분

| 캐릭터 | 결전곡 | 성격을 들려주는 악기 | 박자·속도 목표 | 감정의 변화 |
|---|---|---|---|---|
| 리안 | **문을 열어 두는 사람 / The Door Left Open** | 낮은 첼로, 절제된 피아노, 호른, 작은 유리종 | 4/4 · 100 BPM | 긴 의무와 기다림 → 살아 있는 사람을 지키는 결의 |
| 세라 | **이번 술래는 여기까지 / The Game Ends Here** | 장난감 건반, 아코디언, 거친 기타, 피아노, 살아 있는 드럼 | 6/8 · 150 BPM | 아이들과의 작별 → 옛 친구의 봉쇄를 막는 결의 |
| 노아 | **내 이름을 불러 / Call My Name** | 베이스 클라리넷, 준비된 피아노, 금속 타악, 전자 베이스 | 4/4 · 112 BPM | 친구가 부르는 명령 → 스스로 고르는 대답과 리듬 |

리안에게 무조건 웅장한 영웅가를, 세라에게 단순히 밝은 화염 록을, 노아에게 계속 음산하기만 한 앰비언트를 주지 않는다. 각각의 약한 부분이 마지막에도 들리게 한다.

## 3. 리안 · 돌아올 사람을 위한 불

**메인 1장 결전:** 1-9 닫힌 문을 여는 불 / 분노 · 등대 방어군. 1-8 인간 로웬과의 대립을 이어받는다. 이전 초안의 ‘문을 닫는 파수꾼’은 독립 보스로 배치하지 않는다.

리안의 분노는 크게 외치는 분노보다 같은 자리를 떠나지 못하는 긴장에 가깝다. 낮은 첼로의 짧은 네 음형을 ‘문을 지키는 발걸음’으로 잡는다. 1페이즈에서는 끝을 닫아 반복하고, 결전에서는 같은 윤곽의 선율이 더 긴 숨으로 이어지게 한다. 마지막에도 가족을 잃은 슬픔은 남는다.

### L1. 1페이즈 · 열세 번의 겨울 / Thirteen Winters

- 키: `bgm_lian_last_watch_phase1`
- 시작: 1-9에서 등대 방어군이 귀환로의 빛을 삼키며 나타나는 순간.
- 목표: 약 2~3분. 구조 신호와 검 충돌음을 덮지 않는 절제된 압박.

**Styles:**

```text
Instrumental dark chamber-orchestral boss battle score, 100 BPM, 4/4, D minor. A restrained guardian standing watch through thirteen winters. Low solo cello carries a memorable original four-note motif; muted piano repeats an unfinished answer. Tight viola ostinato, dry snare taps, soft low brass and occasional small glass-bell accents. Controlled, stubborn and lonely rather than explosive. Begin with two bars of bare cello and pulse, then build a steady tactical battle groove. Brief openings between phrases for sword impacts and dialogue. The motif circles back without fully resolving. Intimate and cold, clear midrange, modest sub bass, no giant trailer drops. Instrumental only, no human voices. Maintain a stable pulse and a loop-friendly central passage.
```

추가 Exclude: `epic trailer hits, EDM drops, oversized sub bass, constant cymbal wash`

### L2. 결전 · 문을 열어 두는 사람 / The Door Left Open

- 키: `bgm_lian_last_watch_phase2`
- 전환 대사: “하지만 기다리던 사람이라고 해서, 지금 오는 사람을 밀어낼 수는 없어.”
- 전환 행동: 방패로 열린 문을 고정하고, 카이가 구조 신호에 연결한 뒤 다음 전술 입력을 연다.
- 목표: 약 3분. 따뜻함이 생기되 전투 긴장은 유지.

**Styles — 리안의 엔딩 보스곡을 하나만 만들면 이 블록 사용:**

```text
Instrumental cinematic chamber-orchestral battle theme, 100 BPM, 4/4, D minor with brief warm relative-major colors. A quiet guardian finally chooses the living instead of guarding an empty doorway. A distinctive original four-note cello motif grows into a long, resolute horn melody, answered by warm piano and luminous upper strings. Measured snare rhythm, muscular string ostinato, restrained brass, one delicate glass-bell accent at the emotional turn. Start with a short breath of exposed cello, then enter a firm battle pulse immediately. Build toward a heartfelt melodic peak without becoming a cheerful victory march. Grief remains audible beneath courage. Leave short rhythmic gaps for close weapon clashes. Clear melodic identity, natural orchestral dynamics, controlled bass and a sustained central section suitable for looping. Entirely instrumental, no vocals, choir or spoken words.
```

추가 Exclude: `cheerful victory fanfare, epic trailer braams, EDM drops, constant cymbal wash`

### L3. 전투 후 · 마지막 음의 빈칸 / The Missing Last Note

- 키: `bgm_lian_last_watch_afterglow`
- 시작: 승리 연출의 검 소리가 끝나고, 귀환 장면으로 넘어갈 때.
- 목표: 45~75초. 장면 끝에 자연스럽게 정지할 수 있는 짧은 곡.

**Styles:**

```text
Instrumental intimate piano and solo cello epilogue, slow free phrasing around 70 BPM. A guardian returns home and discovers one note of an old melody is missing. A tender original four-note motif pauses before its final note, leaving a deliberate breath of silence. Felt piano, warm close cello, a single distant glass-bell shimmer, no percussion. Sadness held gently, modest relief, a door opened to returning friends. Small room ambience, natural decay, sparse arrangement, short complete ending rather than a dramatic swell. No vocals, humming, choir or spoken words.
```

## 4. 세라 · 웃음이 끝난 자리

**엔딩 보스:** 엘렌 — 봉쇄선의 집행관 / 회색 인장 연합의 인간 집행관

군체 ‘끝나지 않는 술래’는 중간 결전으로 남긴다. 아래 전용 음악은 살아 있는 옛 친구와 맞서는 마지막 전투에 사용한다.

세라의 음악은 둘이 아이들의 식탁을 꾸미던 짧은 건반 모티프로 시작한다. 엘렌의 아코디언은 같은 기억을 자기 규칙의 화음으로 반복하고, 세라의 기타는 그 안에서 앞으로 나아가는 선율로 답한다. 살아 있는 친구를 막는 분노와 그 친구를 좋아했던 마음이 함께 남게 한다. 아이들과의 작별도 여전히 이 곡의 바탕이다.

### S1. 1페이즈 · 남겨 둔 한 자리 / A Place Reserved for One

- 키: `bgm_sera_sealed_route_phase1`
- 시작: 엘렌이 회색 인장을 찍고 경비병에게 통로 봉쇄를 명령할 때.
- 목표: 약 2~3분. 함께 차리던 식탁의 기억과, 지금은 사람을 가두는 친구의 규칙이 겹친다.

**Styles:**

```text
Instrumental dark theatrical waltz and chamber-rock boss score, urgent 6/8 at 150 BPM, A minor. A fighter faces a former friend who once cared for children beside her and now commands a sealed refugee route. A short original toy-piano motif recalls a shared dinner table; dry accordion chords answer it with increasingly rigid authority, supported by tense pizzicato strings. Uneasy live drums, restrained distorted guitar and sharp piano accents turn playfulness into pressure. Worn and theatrical, with grief underneath, not a comedy or a carnival parade. A brief bare keyboard opening leads quickly into a persistent battle rhythm. Occasionally strip down to the small motif before the band returns. Avoid literal children's voices and laughter. Clear attack transients, room for combat dialogue, controlled bright frequencies, a repeating middle passage. Purely instrumental, no vocals or choir.
```

추가 Exclude: `children laughing, children's voices, circus comedy, happy carnival music, novelty sound effects`

### S2. 결전 · 이번 술래는 여기까지 / The Game Ends Here

- 키: `bgm_sera_sealed_route_phase2`
- 전환 대사: “그래서 이번에는 눈앞에 있는 사람부터 데리고 나갈 거야.”
- 전환 행동: 세라가 자신에게만 열린 통행을 거부하고, 피난민의 구조등을 호위한 뒤 다음 전술 입력을 열 때.
- 목표: 약 3분. 눈물을 숨기던 힘이 아니라 충분히 울고도 움직이는 힘.

**Styles — 세라의 엔딩 보스곡을 하나만 만들면 이 블록 사용:**

```text
Instrumental theatrical alternative-rock boss finale, driving 6/8 at 150 BPM, A minor with bittersweet open harmonies. A grieving fighter confronts a beloved former friend who has chosen to imprison refugees, and opens the route even though she still cherishes their shared past. A small original toy-piano melody is transformed into a fierce expressive electric-guitar lead, supported by passionate acoustic piano, dry accordion accents, live bass and dynamic drums. Begin with one exposed keyboard phrase and a brief silence, then a decisive band entrance. Strong melodic hooks, surging compound rhythm, rough human energy and a cathartic bridge that keeps sorrow audible. Powerful without sounding triumphant or carefree. The final refrain becomes the fighter's clear answer to her old friend's commanding accordion rhythm, while tenderness remains beneath anger. Preserve rhythmic gaps for blade impacts; avoid excessive cymbal hiss and heavy sub bass. Long energetic central battle passage, emotionally clear instrumental storytelling. No singing, choir, humming, sampled speech or human voices.
```

추가 Exclude: `happy party rock, children laughing, vocal guitar-talkbox, EDM festival drop, constant cymbal wash`

### S3. 전투 후 · 자기 몫의 접시 / A Place at the Table

- 키: `bgm_sera_sealed_route_afterglow`
- 시작: 이름표를 식기와 함께 보관하고, 따뜻한 수프를 받는 장면.
- 목표: 45~75초. 눈물에서 일상으로 돌아오는 작은 숨.

**Styles:**

```text
Instrumental bittersweet acoustic epilogue in gentle 6/8, approximately 72 BPM. A fighter returns to a warm dinner table after freeing refugees and detaining the former friend she still cares about. Soft piano carries a simple original melody once played on a toy keyboard; fingerpicked acoustic guitar and a restrained warm accordion answer it. Sparse upright bass, no dramatic drums. Fragile, relieved and still grieving, with one small genuine smile. Intimate natural room sound, short phrases, audible breathing space between instrumental lines, a quiet complete ending. No vocals, humming, choir, laughter or spoken words.
```

## 5. 노아 · 침묵에 남긴 이름

**엔딩 보스:** 이브 — 침묵의 기록관 / 철종 교단의 인간 집행관

군체 ‘이름 없는 철종’이 무너진 뒤에도 이브의 명령은 남는다. 아래 전용 음악은 친했던 친구의 지휘를 멈추는 마지막 전투에 사용한다.

노아의 냉소에는 자신이 다시 누군가를 믿게 될까 두려워하는 마음이 있다. 1페이즈의 종과 금속 타악은 정확히 같은 위치에서 반복되어 이브가 내리는 교단의 명령처럼 들린다. 결전에서는 전자 리듬과 베이스 클라리넷이 그 규칙 사이로 들어가 자기 박자를 고른다. 종은 악기 색채로만 사용하고 실제 제안선 경고음과 구별한다.

### N1. 1페이즈 · 지워진 이름 / Erased Names

- 키: `bgm_noa_answering_eve_phase1`
- 시작: 군체의 안식 거래가 첫 예고선으로 나타날 때.
- 목표: 약 2~3분. 차가운 의식과 거래의 압박.

**Styles:**

```text
Instrumental noir electronic and chamber-industrial boss score, 112 BPM, 4/4, E minor. A fugitive faces the close friend who once protected her and now signs orders to erase people's names and punish their refusal. Hollow low bell strikes carry the former friend's rigid command rhythm; prepared piano and sparse bass clarinet trade a short original motif once shared in a quiet records room, with restrained bowed metal. Dry electronic percussion, restrained sub bass and narrow cold synth textures. Patient, calculating and uneasy, with concealed regret rather than supernatural grandeur. Begin with a single low bell and enter a precise tactical pulse within two bars. Keep the bell infrequent enough to leave room for gameplay warnings. No harsh noise wall, no giant drop. A stable looping central section, clear rhythmic spaces and a cold unresolved tension. Entirely instrumental, no choir, chanting, whispers or any human voice.
```

추가 Exclude: `whispering, ritual chants, cathedral choir, continuous bell alarms, harsh noise wall, massive sub drops`

### N2. 결전 · 내 이름을 불러 / Call My Name

- 키: `bgm_noa_answering_eve_phase2`
- 전환 대사: “네가 날 지켜 준 건 사실이야. 오늘 내가 막아야 하는 사람이 된 것도 사실이고.”
- 전환 행동: 노아가 이브의 지휘선과 직접 맞붙기로 정하고, 카이의 빛실로 동료의 호위가 이어질 때. 이름을 부르고 응답하는 대사는 결전곡 안의 여백에서 이어진다.
- 목표: 약 3분. 환한 승리보다 스스로 선택한 관계의 온기.

**Styles — 노아의 엔딩 보스곡을 하나만 만들면 이 블록 사용:**

```text
Instrumental dark noir breakbeat and chamber-electronic boss finale, 112 BPM, 4/4, E minor. A guarded fugitive admits her responsibility, stops her old friend's coercive orders and chooses to answer the companions waiting for her, without erasing the kindness that once existed. An expressive bass-clarinet lead turns a short original chromatic question into a clear melodic answer. Prepared piano, tight syncopated electronic drums, articulate synth bass, restrained bowed metal and a subtle warm cello layer. The first bars carry rigid low-bell pulses; the living groove gradually steps between them and takes control. Cunning, intimate and defiant, with vulnerability beneath the cool exterior. Build toward a memorable melodic peak through rhythmic confidence, not brighter volume alone. Sparse bell punctuation, clean midrange and room for spoken combat dialogue. A sustained central groove suitable for battle looping. Purely instrumental, no vocals, choir, chanting, whispers or sampled speech.
```

추가 Exclude: `ritual chants, whispered voices, continuous bell alarms, cheerful pop chorus, EDM festival drop, harsh noise wall`

### N3. 전투 후 · 노크에 대답하다 / Answering the Knock

- 키: `bgm_noa_answering_eve_afterglow`
- 시작: 기록실 장부에 자신의 이름을 쓰고, 동료의 노크에 응답할 때.
- 목표: 45~75초. 그림자가 사라진 평온이 아니라 그림자와 함께 살아갈 수 있는 여유.

**Styles:**

```text
Instrumental intimate noir chamber epilogue, approximately 68 BPM with relaxed phrasing. A former exile preserves both her old friend's kindness and the evidence of that friend's harmful orders, then writes her own name and answers a companion's knock. Warm low bass clarinet, soft felt piano, restrained cello and one faint metallic resonance. A small original chromatic motif ends with a gentle clear answer while one unresolved inner note remains. Wary tenderness, modest trust, quiet responsibility. Minimal percussion or none, close natural room ambience, short reflective phrases and a complete soft ending. No vocals, humming, choir, whispers, sampled speech or human voices.
```

## 6. 인간 보스에서 공유하던 선율을 다루는 방법

세라와 엘렌은 피아노·장난감 건반과 아코디언이 나누던 식탁의 모티프를 공유한다. 지금의 엘렌은 아코디언 화음을 규칙처럼 반복하고, 세라의 기타는 그 규칙에 답한다. 노아와 이브는 컵 두 개를 놓던 기록실의 피아노·베이스 클라리넷 대화를 공유하되, 이브의 손종은 호위와 집행 명령의 박자로 바뀐다.

악기의 과거 대화를 실제로 같은 선율로 맞추려면 선정곡을 기준으로 편집·작곡 정리를 한다. 독립 생성곡만으로 동일 멜로디를 보장하지 않는다. 인간 적을 제압한 순간에도 공유하던 선율이 한 번 남아, 단순한 환호보다 갈등의 여운을 이어 준다.

## 7. 게임에 넣을 때의 전환 기준

| 상태 | 재생 대상 | 전환 조건 | 필요한 편집 |
|---|---|---|---|
| 보스 등장·1페이즈 | 각 `_phase1` | 전투 진입 후 보스의 첫 예고선 공개 | 도입 뒤 반복 가능한 구간 지정 |
| 결의 장면 | 1페이즈를 낮추거나 잠시 비움 | 해당 캐릭터의 전환 대사 | 대사 앞뒤에 약 0.5~1초 여백 확보 |
| 결전 | 각 `_phase2` | 결의가 끝난 뒤 다음 전술 입력이 열리는 시점 | 공통 박자 경계에서 약 0.5~1.0초 크로스페이드 |
| 보스 처치 | 결전곡의 긴 잔향만 남기고 전환 준비 | 마지막 타격·사망 연출이 끝남 | 처치와 큰 다운비트가 어긋나지 않게 종료 큐 준비 |
| 귀환·엔딩 대화 | 각 `_afterglow` | 귀환 장면 진입 | 짧은 완결형 파일, 반복 필요 없음 |
| 재시도 | `_phase1`부터 | 패배 후 보스전을 다시 시작 | 이미 본 결의 장면 건너뛰기 여부와 음악 큐 함께 초기화 |

페이즈 음악의 중간 부분은 반복되고, 도입과 엔딩은 한 번만 재생되도록 편집한다. Suno 원본의 앞뒤를 그대로 이어 붙여 무음이나 클릭이 생기지 않는지 확인한다. 별도 생성곡의 조성·BPM이 맞지 않으면 박자 맞춤 전환을 강행하지 않고, 짧은 대사 여백 뒤 전환한다.

**필요한 파일 9개:**

이전 `bgm_sera_endless_seeker_*`, `bgm_noa_silent_bell_*` 키로 이미 만든 곡은 해당 군체의 중간 결전 음악으로 보관할 수 있다. 아래의 새 키는 인간 엔딩 보스용이며 기존 파일을 덮어쓰지 않는다.

```text
bgm_lian_last_watch_phase1.wav
bgm_lian_last_watch_phase2.wav
bgm_lian_last_watch_afterglow.wav
bgm_sera_sealed_route_phase1.wav
bgm_sera_sealed_route_phase2.wav
bgm_sera_sealed_route_afterglow.wav
bgm_noa_answering_eve_phase1.wav
bgm_noa_answering_eve_phase2.wav
bgm_noa_answering_eve_afterglow.wav
```

이 목록은 제작·납품할 파일 이름이며 현재 음원 파일이 존재한다는 뜻은 아니다. 대본의 `MusicCue`와 위 키가 같으므로 음원을 받은 뒤 연결할 수 있다. 별도로 도입·루프·종료를 분리한다면 `_intro`, `_loop`, `_outro`를 붙인다.

## 8. 후보곡을 고르는 기준

- 세 곡을 섞어 들어도 리안의 절제, 세라의 가면과 폭발, 노아의 냉소와 선택이 구별되는가.
- 가사·합창·허밍·사람의 목소리가 들어오지 않았는가.
- 결전 20~30초 안에 중심 악기와 기억할 만한 선율이 나타나는가.
- 대사와 검의 ‘챙’ 소리가 묻히지 않는가. 기타 고음과 금속 타악이 충돌음을 계속 덮으면 다른 후보를 선택하거나 편집한다.
- 보스전이 길어져도 루프가 매번 거대한 도입으로 돌아가지 않는가.
- 캐릭터의 결의가 단순히 음량 증가로만 표현되지 않았는가. 세라·노아 곡에서는 옛 친구와 나누던 악기 대화와 현재의 대립이 함께 들리는가.
- 처치 후 여운이 무조건적인 환호로 바뀌지 않는가. 잃어버린 사람과 기억의 대가가 남아 있는가.

첫 검수는 각 캐릭터의 결전곡 3개를 서로 비교한 뒤, 실제 전투 효과음과 대화를 겹쳐 듣는 순서로 진행한다. 선정 결과에는 Suno 생성 버전, 사용한 프롬프트, 수정한 구간과 루프 지점을 함께 기록한다.
