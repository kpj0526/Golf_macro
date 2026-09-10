# Windows 로그인 후 자동 실행 (`--auto-run`)

구현 완료 상태 메모 — 별도 QA / 별도 커밋 대상. (이 문서는 종료시간 삭제 커밋 `ec9f259` 와 무관.)

## 동작

1. UI 계정 영역에 **`Windows 로그인 후 자동 실행`** 체크박스 추가 (기본 OFF).
2. **저장** 버튼을 누를 때:
   * 체크됨  → `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` 에
     값 `GolfCatchBooking = "<booking.exe 전체경로>" --auto-run` 등록
   * 체크 해제 → 위 값 제거
   * 앱 시작 시 이 값의 존재 여부로 체크 상태를 복원
3. `booking.exe --auto-run` 으로 실행되면:
   * 창을 표시하지 않고(`ShowInTaskbar=false`, 최소화, `Visible=false`) 숨김 대기
   * **저장된 조건/계정만** 로드 — `configLoad()` + DPAPI 계정 저장소
   * 예약 1(및 사용 시 예약 2) 계정 ID/비밀번호가 저장돼 있고, `Condition1` 이 유효하게
     파싱되고, `ValidateStart()` 가 통과하면 → `headless` 로 자동 Start
   * 값 누락 · 검증 실패 → **브라우저를 열지 않고** 프로세스 종료
4. **중복 실행 방지**: `Program.Main` 에서 세션 단위 뮤텍스
   `Local\GolfCatch.booking.singleinstance` 를 잡는다. 이미 실행 중이면(수동/자동 무관)
   두 번째 실행은 즉시 종료.
5. **수동 실행**(`--auto-run` 없음)은 기존 동작 그대로. `--auto-run` 분기만 추가됨.

## 자격정보 취급

* ID·비밀번호는 **레지스트리 값에도, 명령줄 인수에도 절대 들어가지 않는다.**
  레지스트리에 저장되는 건 exe 경로와 `--auto-run` 문자열뿐.
* 자격정보는 기존 DPAPI(CurrentUser) 저장소(`%LOCALAPPDATA%\GolfCatch\pw.dat`)에서만
  런타임에 읽는다.

## 범위 / 한계 (설계상)

* HKCU\...\Run 항목이므로 **PC 가 부팅되고 해당 Windows 사용자가 대화형으로 로그인한
  뒤에만** 실행된다. 서비스도 예약 작업도 아니다 — 로그인 없이 부팅만으로는 실행되지
  않고, 다른 사용자 계정으로는 실행되지 않는다.
* 워치독이 없다. 사용자가 창을 강제로 닫거나 `booking.exe` 프로세스를 종료하면 자동
  실행은 그대로 멈추며, **다음 Windows 로그인 전까지 다시 시작되지 않는다.**
* 자동 실행은 진단 빌드에서도 동작하지만, 진단 빌드의 최종 예약 확정(`tryReserve`)은
  여전히 no-op 스텁이므로 예약이 생성되지 않는다.

## 변경 파일

| 파일 | 변경 |
|---|---|
| `recovery/src/booking/AutoRun.cs` | **신규** — HKCU\Run 등록/해제/조회, 인수·뮤텍스 상수 |
| `recovery/src/booking/Program.cs` | `--auto-run` 파싱, 단일 인스턴스 뮤텍스, `Form1(qaAutoStart, autoRun)` |
| `recovery/src/booking/Form1.cs` | `chkAutoRun` 체크박스, 로드 시 상태 복원, 저장 시 등록/해제 동기화, `autoRun` 시 숨김 + `RunAutoRun()` (조건/계정 검증 후 headless Start, 실패 시 종료), account 행 높이 116→140 |
