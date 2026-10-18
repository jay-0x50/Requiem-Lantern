# 노트북·새 PC에서 개발 시작

게임 개발에 필요한 코드, 이미지, 전투 포즈, 효과음, 폰트, 씬, Unity 설정과 패키지 잠금 파일은 저장소에 들어 있습니다. Git LFS와 Codex는 필요하지 않습니다. 기본 실행에는 Blender·Figma·Python·Node.js도 필요하지 않습니다.

## 1. 한 번 설치할 도구

| 목적 | 필요한 도구 |
|---|---|
| 게임 편집·Play | Unity Hub와 **Unity 6000.6.0f1**. Hub에서 라이선스를 활성화합니다. |
| Windows 실행 파일 만들기 | 같은 Unity 버전의 Windows 빌드 모듈. Windows 에디터의 기본 Windows 빌드 타깃을 사용합니다. |
| 브라우저 검수 프리뷰·C# 테스트 | **.NET 10 SDK**. Runtime만 설치하면 빌드할 수 없습니다. |
| JavaScript 회귀 검사 | Node.js |
| HTTP 회귀 검사·대본 컴파일 | Python 3 |
| 캐릭터 Blender 원본 편집 | Blender 5.2.0 LTS |

Unity 버전은 `RequiemLantern/ProjectSettings/ProjectVersion.txt`를 기준으로 합니다. 패키지는 `Packages/manifest.json`과 `packages-lock.json`에서 고정하며, 첫 임포트에는 인터넷 연결이 필요할 수 있습니다.

## 2. 클론하고 게임 열기

```powershell
git clone https://github.com/jay-0x50/Requiem-Lantern.git
cd Requiem-Lantern
```

Windows에서는 **`Open-Unity.cmd`를 더블클릭**합니다. PowerShell 실행 정책을 PC 전체에서 변경할 필요는 없습니다. 실행기는 클론 위치를 기준으로 게임 폴더를 찾고, 프로젝트가 요구하는 Unity 버전으로 메인 씬을 엽니다.

Unity Hub를 직접 사용하면 **Add → 클론 폴더/RequiemLantern**을 선택합니다. 저장소 최상위 폴더가 아닌 `Assets`, `Packages`, `ProjectSettings`를 가진 하위 폴더입니다. 다른 OS에서도 이 하위 폴더를 같은 Unity 버전으로 엽니다. `.cmd` 실행기는 Windows용입니다.

첫 임포트가 끝나면 `Assets/Scenes/Main.unity`를 열고 **Play**를 누릅니다. 게임 UI는 실행 시 생성되므로 편집 상태의 씬은 카메라·조명 위주로 보입니다. 메인 씬이 바뀌었다면 **Requiem Lantern → Open Main Scene** 메뉴로 돌아갑니다.

### Unity를 다른 드라이브에 설치한 경우

실행기는 기본 Hub 설치 폴더, 지원되는 Hub 편집기 목록, PATH를 확인합니다. 찾지 못하면 Unity 실행 파일을 직접 지정합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Open-Unity.ps1 -UnityPath "E:\Unity\6000.6.0f1\Editor\Unity.exe"
```

또는 현재 터미널에 `UNITY_EDITOR_PATH`를 설정합니다. `Open-Unity`, `Build-Windows`, 소스 검사에서 같은 경로를 사용합니다.

```powershell
$env:UNITY_EDITOR_PATH = 'E:\Unity\6000.6.0f1\Editor\Unity.exe'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Open-Unity.ps1 -CheckOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Open-Unity.ps1
```

버전이 다른 에디터를 자동으로 열지 않습니다. Hub에서 **6000.6.0f1**을 선택하면 됩니다.

## 3. 실행 파일과 브라우저 프리뷰

- **`Build-Windows.cmd`**: 이 프로젝트를 Unity에서 닫은 뒤 실행합니다. 배치 빌드 후 `Builds/Windows/RequiemLantern.exe`를 실행합니다. 로그는 `Logs/unity-build-*.log`입니다. 이 스크립트도 `-UnityPath`와 `UNITY_EDITOR_PATH`를 지원합니다.
- **`Start-Preview.cmd`**: .NET 10 SDK 설치 후 실행하면 호스트를 빌드하고 `http://127.0.0.1:8127/`을 엽니다. Unity WebGL과 별개의 검수용 화면이며 같은 C# 전투 규칙을 사용합니다.

`Builds`, `Library`, `Temp`, `.NET bin/obj`는 PC마다 다시 생성하는 파일입니다. 클론에 없어도 정상입니다. 원화와 게임 자산을 재생성하는 제작 스크립트를 먼저 실행할 필요는 없습니다. 특히 `Tools/setup_project.py`는 초기 프로젝트 제작용 기록이며 새 PC 설정 절차가 아닙니다.

## 4. 기존 플레이 진행을 옮기려면

플레이 진행은 Git과 별도로 저장됩니다. 새 PC에서는 새 진행으로 시작합니다. 이어서 플레이하려면 양쪽 게임·프리뷰를 종료하고 아래 파일을 복사합니다. 대상에 진행이 있으면 먼저 별도로 백업합니다.

| 실행 환경 | 복사할 파일 |
|---|---|
| Unity Editor·Windows 게임 | `%USERPROFILE%/AppData/LocalLow/Requiem Lantern Studio/Requiem Lantern/progress.xml`과 있으면 `progress.xml.bak` |
| 브라우저 프리뷰 | 클론 폴더의 `PreviewHost/.local/progress.xml`과 있으면 `progress.xml.bak` |

Unity와 브라우저 진행은 각각 별도입니다. 저장 파일·쿠폰 사용 기록·PC 캐시는 저장소에 올리지 않습니다. 새 PC에서도 기존 테스트 쿠폰은 새 저장 기준으로 사용할 수 있습니다.

## 5. 노트북에서 수정한 작업 올리기

```powershell
git pull --ff-only
# 작업 후 Unity에서 씬과 자산 저장
git status
git add .
git commit -m "Describe the change"
git push origin main
```

다른 PC로 돌아왔을 때도 작업 시작 전에 `git pull --ff-only`로 최신 변경을 받습니다. 스크립트·자산·`.meta`는 함께 커밋합니다. Unity에서 자동 생성하는 `.csproj`, `Library`, 실행 빌드와 개인 저장은 `.gitignore`에서 제외합니다.
