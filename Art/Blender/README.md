# Blender 캐릭터 작업실

Blender 5.2.0 LTS로 제작하고 백그라운드 렌더로 검수했다.

## 열기

`requiem_lantern_cast.blend`를 열면 원화 기반 캐릭터 일곱 명과 작업 보드가 보인다. 3D 뷰에서 Numpad 0으로 정면 직교 카메라를 보고, Space를 누르면 4초 Idle 루프가 재생된다. Material Preview 또는 Rendered 표시 모드에서 원화가 보인다.

각 `{id}_RIG`를 선택하고 Action Editor에서 다음 액션을 고른다.

| 액션 | 프레임 | 용도 |
|---|---|---|
| `{id}_Idle` | 1~96, 97은 1과 동일 | 작은 호흡과 옷자락 움직임, 24fps 4초 루프 |
| `{id}_Resonance` | 1~49 | 공명 선택에 쓰는 작은 상체 반응 |
| `{id}_HitReact` | 1~25 | 피격에 쓰는 짧은 후퇴 반응 |

각 리그에는 ROOT, BREATH, HEAD, COAT_L, COAT_R 다섯 본과 1617개 정점이 있다. 발은 Idle 동안 고정된다. 단일 원화의 변형 폭을 작게 제한해 얼굴과 장식의 왜곡을 줄였다. 자유 시점 회전은 지원하지 않는다.

## 재생성

프로젝트 루트에서 실행한다.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'Art/Blender/Scripts/build_characters.py' -- --render --idle-frames
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'Art/Blender/Scripts/verify_characters.py'
```

스크립트는 Cutouts PNG를 읽어 작업 파일과 렌더를 다시 만든다. 생성된 `.blend`를 손으로 수정했다면 먼저 다른 이름으로 저장한다. 원본 이미지 폴더는 변경하지 않는다.

`requiem_lantern_pilot.blend`는 카이·리안·미나토 시험 제작 시점의 파일이다. 전체 제작 결과는 `requiem_lantern_cast.blend`다.

## 렌더 설정

- Eevee, Standard 색상 변환, 정면 Orthographic 카메라.
- 원화 텍스처는 Emission + Transparent 셰이더를 사용해 조명에 따른 색 변화가 적다.
- 개별 PNG는 투명 배경, 작업 보드는 남색 배경이다.
- 개별 수동 렌더 시 `PRESENTATION` 컬렉션을 숨기고 다른 캐릭터 컬렉션도 Render에서 숨긴다. 카메라를 해당 캐릭터에 맞춘 뒤 Film > Transparent를 켠다.
- 재현 가능한 일괄 PNG 출력은 build_characters.py의 `--render` 옵션이 처리한다.

## 검증

저장한 작업 파일을 다시 열어 일곱 리그, 35개 본, 21개 액션, 텍스처 포함, 정점 가중치 합, Idle 시작/종료 일치, 발 고정을 검사한다. `validation.json`의 결과를 확인한다.

이 파일은 정면용 2.5D 빌보드 리그이며 완전한 3D 캐릭터 모델은 아니다. 출시 원화 및 파츠 분리 요구사항은 `../Characters/README.md`에 기록했다.
