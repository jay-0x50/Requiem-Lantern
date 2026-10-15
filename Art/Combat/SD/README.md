# 전투 전용 SD 캐릭터

2026-10-15 갱신 · built-in image_gen 제작

리안·세라·미나토·노아·하이델·카이의 원화를 참조해 큰 머리와 짧은 팔다리를 가진 전투용 SD 그림을 만들었다. 머리색·의상·장비를 유지하며, 메인·캐릭터·대화 화면의 기존 원화는 그대로 사용한다.

## 저장 경로

최종 게임 자산은 `RequiemLantern/Assets/Resources/Combat/`의 다음 파일이다.

| 인물 | 투명 PNG | 좌표 JSON |
|---|---|---|
| 리안 | LianSDActions.png | LianSDActionsLayout.json |
| 세라 | SeraSDActions.png | SeraSDActionsLayout.json |
| 미나토 | MinatoSDActions.png | MinatoSDActionsLayout.json |
| 노아 | NoaSDActions.png | NoaSDActionsLayout.json |
| 하이델 | HeidelSDActions.png | HeidelSDActionsLayout.json |
| 카이 | KaiSDActions.png | KaiSDActionsLayout.json |

모두 1536×1024 RGBA, 6개 포즈다. 위쪽 왼쪽부터 대기·준비·공격, 아래쪽 왼쪽부터 후속 동작·복귀·피격 순서다. 모든 아군은 오른쪽을 향한다. 원래 비율의 Actions 시트는 보존한다.

Unity와 브라우저는 대기 그림과 공격 재생에서 같은 SD 시트·좌표를 사용한다. Unity 1920×1080 기준 아군 몸 높이는 220px이다. 하단 4열에는 캐릭터마다 카드 두 장과 원화 초상을 표시한다. 적은 기존 자산을 작은 크기로 표시한다.

## 좌표와 검사

`atlasWidth/atlasHeight`는 PNG 크기, `bodyHeight`는 대기 머리~발 높이다. `frames`는 각 포즈의 PNG 왼쪽 위 기준 표시 사각형과 발·무기 접점을 담는다. 리안과 미나토의 일부 무기·의상이 균등한 512px 셀을 넘으므로 제공 JSON의 사각형을 사용해야 한다. 프레임마다 몸 크기에 맞춰 다시 확대하지 말고 `표시 몸 높이 / bodyHeight` 배율을 유지한다.

`build-layouts.py`는 PNG 알파를 읽어 좌표 JSON과 `validation.json`을 작성한다. 래스터를 편집하지 않는다. 최종 36개 영역의 경계 알파는 최대 6/255이고 불투명한 신체·무기 잘림이 없다. 세라·하이델·카이는 생성 도구로 시트 간격을 넓혀 포즈 혼입을 해결했다.

## 제작 기록

- 참조: `Art/Characters/Cutouts/{lian,sera,minato,noa,heidel}.png`.
- 초기 프롬프트와 생성 출처: `imagegen-prompts.json`.
- 최종 채택 출처와 세라·하이델 간격 보정 프롬프트: `selection.json`.
- 카이의 원화 참조·생성·간격 보정 프롬프트와 채택 근거: `kai-generation.json`. 카이는 기본 ★4 전투 편성에 추가했으며 원화는 기존 `Resources/Art/Kai.png`를 사용한다.
- 실행: built-in image_gen, 투명 배경 옵션. 최종 PNG는 기본 생성 위치에서 게임 Resources 폴더로 복사했다.

6개 핵심 자세를 연결하는 방식이다. 연속 관절 리깅과 중간 프레임은 후속 제작 범위이며, AI 그림의 얼굴·장식 세부선은 원화와 일부 다를 수 있다.
