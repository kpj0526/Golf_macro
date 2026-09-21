# 고객용 설치 안내

다운로드: <https://github.com/kpj0526/Golf_macro/releases/latest>

## 가장 쉬운 방법

1. GitHub Releases에서 `GolfCatch-win-x64.zip`을 내려받습니다.
2. ZIP을 원하는 폴더에 압축 해제합니다.
3. `booking.exe`를 실행합니다.
4. 예약 1/예약 2에 사용할 계정 ID와 비밀번호를 직접 입력하고 `저장`을 누릅니다.

계정 정보는 고객 PC의 현재 Windows 사용자 전용 암호화 저장소에 저장됩니다. ZIP, 설정 파일, 로그에는 포함되지 않습니다.

## Python 설치 도우미

Python이 설치되어 있다면 저장소 최상단에서 아래 한 줄만 실행하면 최신 고객용 ZIP을 받아 `%LOCALAPPDATA%\GolfCatch\app`에 설치합니다.

```powershell
py tools\install_golf_catch.py
```

기존 설치본이 있으면 삭제하지 않고, 같은 위치에 시간표시 백업 폴더를 만든 뒤 새 버전을 설치합니다. 계정 저장 파일은 상위 `GolfCatch` 폴더에 따로 보관되므로 업데이트해도 유지됩니다.

## 포함 파일

- `booking.exe`
- `App.config`
- `booking.dll.config`

진단 빌드, 테스트 사이트, 소스 코드, 로그, 계정 정보는 고객 ZIP에 포함하지 않습니다.
