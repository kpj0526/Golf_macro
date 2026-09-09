# Sun Valley booking macro — recovery & diagnostic fix

## What this is
A rebuildable source project recovered from `booking.exe` + matched `booking.pdb`, with:
1. a bounded fix for the demonstrated Sun Valley "refresh 요청합니다" infinite loop,
2. the **false-preopening** fix (title `오픈전입니다` no longer hides real open tee rows),
3. the **two-condition sequential booking** feature with **nearest-time selection** and the
   **condition-1 confirmation gate** — see **`docs/TWO-CONDITION-FEATURE.md`** and
   **`docs/fix-false-preopening.diff`**.

The original package in the parent folder is untouched
(`booking.exe` md5 `001e53d3e347fbaf1935b749fb5cc57e`).

## Layout
```
recovery/
  lib/              booking.dll (extracted from the single-file exe), booking.pdb, deps.json, runtimeconfig.json
  src/decompiled/   raw ILSpy output (reference only)
  src/booking/      buildable project (decompiled + fixed + feature)
  build/diagnostic/ published self-contained win-x64 DIAGNOSTIC exe
  tests/FeatureTests/  57 pure unit/fixture tests (dotnet run -> 57 passed, 0 failed)
  tests/LiveProbe/     SAFE unauthenticated live probe (no creds, no clicks)
  tools/            chromedriver.exe (Chrome 152) — test aid for LiveProbe only
  diagnostics/      live-probe evidence + runtime capture output
  docs/
```

## Test / build status (latest)
| Item | Command | Result |
|---|---|---|
| Diagnostic build | `dotnet build -c Release -p:Diagnostic=true` | exit 0, 0 errors |
| Diagnostic publish | `dotnet publish -c Release -r win-x64 --self-contained true -p:Diagnostic=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ../../build/diagnostic` | exit 0 → `build/diagnostic/booking.exe` (207,243,259 B) |
| Feature unit tests | `cd tests/FeatureTests && dotnet run -c Release` | **57 passed, 0 failed** (exit 0) |
| Unauthenticated live probe | `tests/LiveProbe` (see feature doc §5) | exit 0; evidence in `diagnostics/20260909_095647_*` |
| No-submit verified | decompile built `booking.dll` `sunValley.tryReserve` | 3-line stub; no `ExecuteScript(...click...)`, no reserve click |

## Toolchain
- .NET SDK 10.0.400  (`winget install --id Microsoft.DotNet.SDK.10`)
- ilspycmd 11.0.0     (`dotnet tool install -g ilspycmd`)  — decompile only

## Build
```
cd recovery/src/booking
dotnet build   -c Release -p:Diagnostic=true                # diagnostic (default)
dotnet publish -c Release -r win-x64 --self-contained true -p:Diagnostic=true `
               -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
               -o ../../build/diagnostic
```
`-p:Diagnostic=true` defines `DIAGNOSTIC_BUILD`: the reservation-submit code in
`sunValley.tryReserve()` is **compiled out** and `diagnosticMode` is forced on.
`-p:Diagnostic=false` produces a behaviour-preserving build (submit path present) that
still has the bounded loop + diagnostics.

## The fix (sunValley.cs / BookingDiagnostics.cs / club.cs / CoreData.cs)
Original `bookOne()` loop: `while (num >= 0 && !frm.stopClicked)` — `num` never changes,
the deadline was discarded (`_ = DateTime.Now;`), and **every** page state that was not
`잔여팀`/`마감` fell through to `log("refresh") → GoToUrl → continue` with no cap.

New bounded state machine, each iteration:
1. `BookingDiagnostics.Classify(driver, dateCell)` →
   - `RedirectedAway`  (URL not `/reservation/golf`, or on `/member/login`,`/mypage`,…)
     → capture screenshot+HTML+note, `logtxtBox("STOP: not on reservation page …")`,
       return `FalalError` (stops the whole run — session/entry-flow broken).
   - `DateCellMissing` (expected `//*[@id='A<date>']/a` absent)
     → capture, `STOP`, return `FalalError`.
   - `Closed` (`마감`) → `finishDay`, move to next request (unchanged).
   - `NotOpenYet` (`오픈전`) / `Unknown` → the **only** refresh path, bounded by
     `MaxRefresh` (default 40) **and** `MaxWaitSeconds` (default 180). On exhaustion:
     capture, `STOP: gave up waiting …`, return `Fail`.
   - `SlotsAvailable` (`잔여팀`) → open date, collect `.btn.btn-res` rows, `find()` slot.
2. Slot found:
   - DIAGNOSTIC build / `DiagnosticMode=true`: capture `slot-found-DIAGNOSTIC`,
     `logtxtBox("DIAGNOSTIC: matching slot found … stopping before submit")`,
     return `FoundSlot`. **`tryReserve()` is never called and is compiled out.**
   - normal build: original `tryReserve()` path.

Config knobs (App.config `appSettings`, all optional, safe defaults):
`DiagnosticMode`, `MaxRefresh`, `RefreshDelayMs`, `MaxWaitSeconds`, `DiagnosticsDir`.

## Run (operator, diagnostic)
1. Install Google Chrome (any recent stable).
2. Copy `build/diagnostic/` somewhere writable. Edit `App.config` only if you want to
   change the caps. Do **not** put real credentials in `App.config`.
3. Launch `booking.exe`. Enter the Sun Valley site **ID / Password** in the UI fields
   (they are never persisted by the diagnostic build unless you press 저장).
4. Sun Valley is the only club; pick course, starter, date, time window; press **추가**.
5. Press **Start**. When ChromeDriver-version prompt appears, accept the suggested value.
6. Watch the log box + `diagnostics/` folder.

Expected diagnostic outcomes (any one, then it stops — it never books):
| Log line | Meaning | Evidence file |
|---|---|---|
| `DIAGNOSTIC: matching slot found … stopping before submit` | login+nav+slot discovery all OK | `*_slot-found-DIAGNOSTIC.{png,html,txt}` |
| `STOP: not on reservation page. url=…/member/login…` | login failed or session not carried | `*_redirected-away.*` |
| `STOP: date cell not found. xpath=…` | wrong date/course mapping or calendar markup changed | `*_date-cell-missing.*` |
| `STOP: gave up waiting (NotOpenYet) … after N refresh / Ns` | date genuinely not open yet (bounded, no infinite loop) | `*_refresh-limit.*` |
| `no reservable rows after opening date` | cell said 잔여팀 but no `.btn.btn-res` | `*_no-tee-buttons.*` |

## Live validation done so far (credential-free)
- `GET /reservation/golf?sel=J21` unauthenticated → **302 → `/member/login?returnURL=/reservation/golf`**
  (server-side). Confirms the original loop's mechanism and that `Classify()` returns
  `RedirectedAway` for it.
- Login page still exposes `usrId` / `usrPwd` / `fnLogin` → `sunValley.login()` selectors current.
- Authenticated calendar/slot validation is **blocked**: needs real Sun Valley credentials,
  which are out of scope. See `diagnostics/*_live-probe.txt`.

## QA test plan
| # | Case | Setup | Pass criteria |
|---|---|---|---|
| 1 | Build reproducible | clean clone, `dotnet build -p:Diagnostic=true` | 0 errors; `booking.exe` produced |
| 2 | Submit path absent in diag build | `ilspycmd -t booking.sunValley bin/.../booking.dll` | `tryReserve` body is the "compiled out" stub; no `ExecuteScript("arguments[0].click()")`, no reserve-button `clickLock` |
| 3 | Wrong login (bad pwd) | UI with invalid pwd, Start | log `Login Failed`; no browser hang; run ends |
| 4 | Redirect detection | valid login, then site bounces to mypage (or invalidate cookie) | log `STOP: not on reservation page`; `redirected-away` artifact written; process idle, not looping |
| 5 | Not-open date | valid login, date beyond open window | ≤ `MaxRefresh` refresh lines then `STOP: gave up waiting`; `refresh-limit` artifact; elapsed ≈ `MaxWaitSeconds` |
| 6 | Slot discovery | valid login, nearest **open** future date, wide time window | log `DIAGNOSTIC: matching slot found`; `slot-found-DIAGNOSTIC` artifact with screenshot showing the tee list; **no reservation created** (verify in the Sun Valley account) |
| 7 | Caps honoured | set `MaxRefresh=3`, `MaxWaitSeconds=10` | stops at 3 refreshes / ~10 s |
| 8 | No credential leakage | inspect `booking.dll`/exe strings, `App.config`, `golflog.txt` after a run | none of the original embedded account credentials present |
| 9 | Original package intact | hash `../booking.exe`, `../booking.dll.config` before/after | unchanged |

## Known remaining blockers
1. **Authenticated live validation** (QA #4–#6) needs Sun Valley test credentials.
2. `booking.dll.config` in the original uses `BookLIst` (typo) with a **past** date
   `20260330`; the video request `20260521` may be beyond the site's open horizon on the
   run date. The operator must set a valid, in-window future date. The diagnostic build
   will not submit for a stale/past request; the bounded loop reports `refresh-limit`.
3. Other venue classes (`asiana`, `century`, … 20 of them) still contain the original
   unbounded patterns — only `sunValley` was fixed per scope.
4. WinForms designer round-trip not restored (code-defined UI works at runtime).
