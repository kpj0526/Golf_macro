# Handoff — Code Claude → QA Codex

Per AGENT_ROLE_TREE.md §3.3. Code Claude has implemented + self-checked. **Formal
verification / acceptance is QA Codex's to run** (test plan below + `TWO-CONDITION-FEATURE.md` §6).
Diagnostic build never submits a reservation; no real credentials were used.

---
> **2026-09-09 — ARTIFACT READY, RETEST 5 (current).** Legacy credential machinery removed
> (`OriginalConfigPath` import, `CredentialBridge` file, `DiagnosticCredentialPreflight`,
> forced default ID, locked ID field). New: **two local account slots (계정 1 / 계정 2)**,
> each an editable ID + PW, plus an independent **계정** dropdown per 예약; 저장 persists both
> slots + selections + conditions to the DPAPI store; relaunch reloads them. 예약 2 re-logs-in
> with its own slot in the new tab. Diagnostic no-submit stub preserved. Self-check:
> booking `-p:Diagnostic=true|false` 0 errors; FeatureTests/StartValidation/UiShot compile
> 0 errors; **FeatureTests 90/90**; publish diagnostic+release 0 errors. **No implementation
> work remains.** Not run by Code Claude: StartValidation, UiShot, all manual checks.
> New artifact hashes (2026-09-09 rebuild — initial window widened to 1700×980 so the account
> slots + 계정 저장 + 예약 계정 dropdowns show un-clipped at first launch; UiShot render PASS):
> diagnostic `10afc93beaf71767374cf5c8efa5011cc8df7002df857fedb7a2c2ce273c18bd`,
> release `e6c9139921e0f2ecbc098ddf8203021140cfcd62c69e3efc5b19cd26eeb64577` (HOLD).
> QA diagnostic build path: `recovery/build/diagnostic/booking.exe`.
> Detail + QA steps in **`QA-READY-NOTICE.md` → RETEST 5**.
---
> **2026-09-09 — ARTIFACT READY, RETEST 4.** Password TextBox is now shown in **clear text
> in every build** (diagnostic + release), per operator request, for visual credential
> verification. `pwdTb.UseSystemPasswordChar = false`, no `#if` split. The box is empty at
> startup; no credential value is embedded, printed, or logged. Re-tested: **FeatureTests
> 105/105, StartValidation 10/10, UiShot PASS** (`shown in clear (all builds): True`, no
> plaintext password in dump), diagnostic `golflog.txt` still clean. New hashes:
> diagnostic `25084763bd7e102820cd6dfcc53083b6f5a0213c8c2512d53e2116cb2b149734`,
> release `74892fd2e8402a36291a36bd1a64a5ff62969a27df0c55f921f3e9bb47ce03e5` (HOLD).
> Detail + QA steps in **`QA-READY-NOTICE.md` → RETEST 4**.
---
> **2026-09-09 — ARTIFACT READY, RETEST 3 (security).** The credential fingerprint/hash
> and source path have been removed from every diagnostic (UI / `Trace` / `golflog.txt`);
> the in-memory equality pre-flight stays, but nothing secret-derived persists. Rebuilt +
> re-tested: **FeatureTests 105/105, StartValidation 10/10**, and a launched diagnostic exe's
> fresh `golflog.txt` contains no hash / no `fp=` / no source path. New artifact hashes and
> the full **hidden end-to-end diagnostic test** steps are in **`QA-READY-NOTICE.md` → RETEST 3**
> (that section supersedes the RETEST 2 `fp=…` / `source=…` / `QA 자격 지문` log strings).
> QA diagnostic: `recovery/build/diagnostic/booking.exe` sha256
> `73aae7b08325a00719b6f8c28c13ebf9668c0658448190a60b196971487fc754`.
> Customer release: still **HOLD**.
---

## 1. Change summary

Sun Valley recovery build, delta since the last accepted state:

1. **UI (minimal, original layout preserved).** Original `Form1` TableLayoutPanel + ListBoxes +
   time NumericUpDown panel + log box + Start/Stop/Setting kept as-is (same controls/fonts/styles).
   Added, in the original style: **`예약 2` cloned controls** (`courseLb2`/`starterLb2` ListBoxes,
   `dt2`), section labels `■ 예약 1` / `■ 예약 2`, time panel relabelled `예약1 희망` / `예약2 희망`,
   a **`예약 2 사용`** CheckBox (default OFF → 예약 2 controls disabled), a **`로그인 정보 저장`**
   CheckBox. Removed the dynamic `추가`/`삭제` buttons + `bookListBox`. Title → `2개 예약 설정 — Golf Catch`.
2. **예약 2 optional + validation.** OFF ⇒ run 예약 1 only, normal finish. ON ⇒ 골프장·날짜·희망
   시간 all required; any missing ⇒ blocked before start with a clear message.
3. **Sequential gate** (unchanged design): login once → 예약 1 → only if 예약 1 returns a server
   confirmation ID **and** reservation-history verification → 예약 2, opened in a **new tab** with
   예약 2's course. Any 예약 1 shortfall ⇒ 예약 2 skipped, run ends. Diagnostic build never submits,
   so it always ends after 예약 1 ("no confirmation ID") — intended.
4. **Nearest-time selection** (unchanged): min |minute-diff|, tie → earlier time, policy+delta logged.
5. **False-preopening fix** (unchanged): `BookingDiagnostics.ResolveWithRowProbe` — a `.btn.btn-res`
   probe overrides a stale `오픈전입니다` cell title; bounded retries otherwise.
6. **Password auto-fill via DPAPI (CurrentUser), password only.** `CredentialStore.cs`:
   `%LOCALAPPDATA%\GolfCatch\pw.dat`. ID keeps its original built-in default (never stored/migrated).
   `로그인 정보 저장` ON ⇒ save on Start/저장; OFF ⇒ delete the store. `비밀번호` box always masked.
   **No credential in any config / log / diagnostic / exception.** No read of the original package config.

## 2. Files changed (`recovery/src/booking/` unless noted)

| File | Change |
|---|---|
| `Form1.cs` | Restored to the decompiled-original layout + minimal additions: `예약 2` cloned ListBoxes/date, `예약1/예약2 희망` time pairs, `예약 2 사용` + `로그인 정보 저장` checkboxes, section labels; removed 추가/삭제/`bookListBox`; `pwdTb.UseSystemPasswordChar=true`; `bookMain()` sequential gate + new-tab-for-예약2; `start()` reads the two control-groups + 예약 2 all-or-nothing validation; helpers `ConditionString`/`ParseCondition`/`SaveConditions`/`SetR2Enabled`/`PrefillFromConfig`; title string |
| `CredentialStore.cs` | **new** — DPAPI (CurrentUser) **password-only** store: `SavePassword`/`TryLoadPassword`/`Clear` |
| `CoreData.cs` | `configLoad` no longer reads/writes `Id`/`Password`; loads password from `CredentialStore`; `savePassword`/`clearPassword`; `configSaveConditions` writes only `Condition1`/`Condition2`/`UserCode` |
| `booking.csproj` | + `System.Security.Cryptography.ProtectedData` 9.0.0 |
| `App.config` | removed `Id`/`Password` keys (credentials are DPAPI-only) |
| `TeeSelector.cs` `BookOutcome.cs` `ConditionParser.cs` `SequentialGate.cs` `BookingDiagnostics.cs` `bookInfo.cs` `sunValley.cs` `club.cs` | as in the prior handoff (nearest-time, gate, false-preopening, `bookRequest`) — unchanged this round |
| `recovery/tests/FeatureTests/*` | + group 6 (DPAPI password store, **synthetic** password only) |
| `recovery/tests/UiShot/*` | render harness updated for the new control tree |
| `recovery/docs/TWO-CONDITION-FEATURE.md`, `fix-false-preopening.diff` | updated |

Original package: **untouched** — `booking.exe` md5 `001e53d3e347fbaf1935b749fb5cc57e`,
`booking.dll.config` `0dc0639e…`, `booking.pdb` `9c148501…`, `golflog.txt` `5bf0c751…` (129 bytes).

## 3. Code Claude self-check (commands + output)

```
cd recovery/src/booking
dotnet build   -c Release -p:Diagnostic=true    → exit 0, 0 errors
dotnet build   -c Release -p:Diagnostic=false   → exit 0, 0 errors
cd recovery/tests/FeatureTests
dotnet run     -c Release                       → 67 passed, 0 failed (exit 0)
```
Not yet re-run by Code Claude this round (QA Codex to run): `dotnet publish` of the diagnostic
single-file, the `UiShot` render, and the manual QA plan.

## 4. Change → acceptance-criterion mapping

| Criterion | Where verified |
|---|---|
| exactly two fixed conditions, no add/remove, no 3rd | `Form1` has fixed `courseLb/dt/nupArr[0,1]` + `courseLb2/dt2/nupArr[2,3]`; no add UI exists — QA M1 + `UiShot` dump |
| each condition: 골프장 + 날짜 + 희망 시간, visible at startup | `UiShot` checks: 2 `DateTimePicker`, 4 `NumericUpDown`, `예약 1`/`예약 2` labels visible |
| nearest tee time, earlier tie-break, logged | `FeatureTests` group 1 (13 cases) |
| 예약 1 confirmation gate before 예약 2 | `FeatureTests` group 4; `bookMain` decompile |
| 예약 1 failure ⇒ 예약 2 skipped, run ends | `SequentialGate.ShouldRunCondition2` false-paths — `FeatureTests` group 4 |
| 예약 2 optional (empty ⇒ 예약 1 only) / partial ⇒ blocked | `Form1.start()` `chkR2` branch — QA manual (M-R2a/b/c below) |
| 예약 2 runs in a new tab with 예약 2 course | `bookMain` `SwitchTo().NewWindow(WindowType.Tab)` then `bookRequest(bookList[1])` → `GoToUrl(clubs[course2])` — QA manual (needs login) |
| false-preopening fix retained | `FeatureTests` group 2 (13 cases) |
| diagnostic no-submit retained | `FeatureTests` group 5; `sunValley.tryReserve` decompile = stub |
| password DPAPI-encrypted, auto-fill, ON/OFF, ID unchanged | `FeatureTests` group 6 (synthetic); `UiShot`: `비밀번호` masked, `로그인 정보 저장` present — QA manual (M-PW1..3) |
| no credential in config/log/diagnostic | `App.config` has no Id/Password; blob-opacity test; QA to grep `diagnostics/*` + built dll |

## 5. QA Codex — required checks (beyond `TWO-CONDITION-FEATURE.md` §6)

* **Build/publish**: `dotnet build -p:Diagnostic=true|false` (0 errors); `dotnet publish` diagnostic
  single-file → `build/diagnostic/booking.exe`.
* **UI render**: run `recovery/tests/UiShot`; confirm PNG + `RESULT: PASS` (title `2개 예약 설정`,
  `예약 1`/`예약 2` labels, 2 date pickers, 4 numeric-up-downs, `예약 2 사용` + `로그인 정보 저장`
  checkboxes, `비밀번호` masked, no plaintext password in the dump).
* **M-R2a** 예약 2 사용 OFF → Start → log `예약 2 미사용 - 예약 1만 실행`; run ends after 예약 1.
* **M-R2b** 예약 2 사용 ON, all three set → Start → `예약 1` then (real build only, after confirm)
  `예약 2` in a new tab.
* **M-R2c** 예약 2 사용 ON, 골프장 미선택 **or** 희망 시간 0:00 → Start → blocked with
  `예약 2 사용 시 골프장 · 날짜 · 희망 시간을 모두 입력하세요.` (no browser launched).
* **M-PW1** first run: type a password, `로그인 정보 저장` ON, Start; close; reopen → password
  auto-filled (masked), checkbox ON. Verify `%LOCALAPPDATA%\GolfCatch\pw.dat` exists and is not
  readable plaintext.
* **M-PW2** uncheck `로그인 정보 저장` (or Start with it off) → `pw.dat` deleted; next run password blank.
* **M-PW3** grep built `booking.dll`, `booking.dll.config`, `golflog.txt`, `diagnostics/*` after a
  run → the password never appears.
* **Regression**: `FeatureTests` 67/67; `sunValley.tryReserve` in the DIAGNOSTIC dll is the no-op stub.
* **Original package**: hashes in §2 unchanged; **no real reservation submitted or cancelled**.

## 6. Known gaps / assumptions

* Authenticated end-to-end (login → reservation page → date → `.btn.btn-res` → nearest pick →
  confirmation gate → 예약 2 new tab) still needs a Sun Valley **test** account — out of scope for
  the diagnostic build (no submit).
* `ExtractConfirmationId()` / `VerifyReservationHistory()` unvalidated against the live authenticated
  my-page; both fail safe (→ 예약 2 not started).
* `예약 1` date sits in a different cell from its course/starter (original layout) — visually
  separated but both visible; not changed per "keep original UI".
