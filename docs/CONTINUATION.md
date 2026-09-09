# Golf Catch — 작업 인수인계

마지막 갱신: 2026-09-09

이 문서는 다음 작업자가 현재 상태를 빠르게 이어받기 위한 요약이다. 계정 ID, 비밀번호, 로컬 저장소, 진단 로그는 의도적으로 기록하지 않는다.

## 현재 구현 상태

- Sun Valley 예약 오픈 시각 정책을 구현했다.
  - 설악썬밸리: 대상일 기준 2주 전 월요일 09:00
  - 썬밸리CC: 평일은 2주 전 월요일 09:30, 주말은 2주 전 금요일 09:30
  - 동원썬밸리: 위와 동일한 날짜, 10:00
  - 오픈 시각이 지나면 1분 뒤 한 번만 확인한다.
- 예약 1과 예약 2에 각각 시작 시간과 종료 시간을 설정할 수 있다. 종료 시간이 시작 시간보다 빠르면 Start 전에 막아야 한다.
- 예약 2는 예약 1이 서버에서 확정된 경우에만 순차 실행하는 구조다.
- 예약 1/예약 2마다 계정 선택 드롭다운이 있다. 계정 1과 계정 2의 ID·비밀번호는 사용자가 직접 입력하고 `저장`으로 로컬에 저장한다.
- 비밀번호 입력칸은 사용자 요구에 따라 평문으로 표시한다.
- 계정 정보는 DPAPI(CurrentUser)로 `%LOCALAPPDATA%\\GolfCatch\\pw.dat`에 저장한다. 소스, 배포 설정, 로그에는 계정값을 넣지 않는다.
- QA 진단 빌드는 마지막 실제 예약 확정 클릭을 차단한다. 고객용 릴리스는 별도 빌드다.

## 최근 검증 상태

- FeatureTests: 90 passed, 0 failed
- StartValidation 및 UiShot: 컴파일 성공
- 진단/릴리스 publish: 각각 0 errors
- 계정 슬롯과 최신 UI가 반영된 실행 파일 경로:
  - QA 진단: `recovery/build/diagnostic/booking.exe`
  - 고객용 릴리스: `recovery/build/release/booking.exe`

현재 요청된 다음 단계는 **QA 진단 빌드로 브라우저를 숨긴 채 실제 로그인부터 최종 예약 확정 직전까지 한 번만 검증하는 것**이다. 실제 예약 확정 클릭은 하지 않는다.

## QA 범위

1. 프로그램 실행 후 계정 1/계정 2 입력 및 저장이 가능한지 확인한다.
2. 종료 후 재실행해 저장값과 예약별 계정 선택이 유지되는지 확인한다.
3. 선택한 계정으로 자동 로그인이 되는지 확인한다.
4. 유효한 미래 테스트 날짜로 예약 화면, 코스, 시간 선택을 진행한다.
5. 최종 예약 확정 버튼을 누르기 직전에서 멈춘다. 진단 빌드에서는 어떤 경우에도 실제 예약을 생성하면 안 된다.

## 고객 전달 전 체크

- QA 진단 빌드가 아니라 `recovery/build/release`에서 새로 패키징한다.
- 소스, 테스트, `diagnostics`, `recovery/build/diagnostic`, 브라우저 드라이버 임시 파일, 로그, 로컬 계정 저장소를 고객 패키지에 넣지 않는다.
- 고객 계정은 고객 PC에서 직접 입력하고 `저장`한다.
- GitHub 릴리스에 고객용 ZIP을 올린 뒤 `tools/install_golf_catch.py`로 설치 경로를 제공한다.

## Python 설치 도구

`tools/install_golf_catch.py`는 GitHub의 최신 릴리스에서 `GolfCatch-win-x64.zip` 자산을 받아 `%LOCALAPPDATA%\\GolfCatch`에 안전하게 푼다. 아직 릴리스 자산이 없으면 오류를 내며, 로컬 빌드나 계정 정보를 업로드하지 않는다.

```powershell
py tools/install_golf_catch.py
```

다른 설치 위치는 다음처럼 지정한다.

```powershell
py tools/install_golf_catch.py --install-dir D:\\GolfCatch
```
