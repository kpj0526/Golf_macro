using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using OpenQA.Selenium;
using booking;

namespace FeatureTests;

internal static class Program
{
	private static int _pass;
	private static int _fail;

	private static void Main()
	{
		Section("1. Nearest-time selection + earlier tie-break (TeeSelector)");
		NearestTimeTests();

		Section("2. False-preopening regression (BookingDiagnostics)");
		FalsePreopeningTests();

		Section("3. Two-condition parsing / fixed-slot rules (ConditionParser)");
		ConditionParserTests();

		Section("4. Sequential condition-2 continuation policy (SequentialGate + BookOutcome)");
		SequentialGateTests();

		Section("5. Diagnostic no-submit contract (BookOutcome under DIAGNOSTIC_BUILD)");
		DiagnosticNoSubmitTests();

		Section("6. DPAPI credential store + legacy migration (SYNTHETIC credentials only)");
		CredentialStoreTests();

		Section("7. Sun Valley scheduled single-check policy (no browser / no submission)");
		SunValleyScheduleTests();

		Section("8. Reservation-history fallback confirmation match (ReservationHistoryMatch)");
		ReservationHistoryMatchTests();

		Section("9. Legacy Windows-login auto-run cleanup (HKCU Run value removal only)");
		LegacyAutoRunCleanupTests();

		Section("10. Two-account execution policy (isolated parallel sessions)");
		ReservationExecutionPolicyTests();

		Console.WriteLine();
		Console.WriteLine("================ " + _pass + " passed, " + _fail + " failed ================");
		Environment.Exit(_fail == 0 ? 0 : 1);
	}

	private static void ReservationExecutionPolicyTests()
	{
		var accounts = new CredentialStore.AccountData();
		accounts.Id[0] = "first-account";
		accounts.Id[1] = "second-account";
		accounts.Res1Slot = 0;
		accounts.Res2Slot = 1;
		Expect("different account IDs -> parallel isolated sessions",
			ReservationExecutionPolicy.UseParallelSessions(true, accounts), "");

		accounts.Res2Slot = 0;
		Expect("same selected account -> sequential session",
			!ReservationExecutionPolicy.UseParallelSessions(true, accounts), "");

		accounts.Res2Slot = 1;
		accounts.Id[1] = "FIRST-ACCOUNT";
		Expect("same ID in two slots -> sequential session",
			!ReservationExecutionPolicy.UseParallelSessions(true, accounts), "");

		accounts.Id[1] = "second-account";
		Expect("one reservation -> sequential session",
			!ReservationExecutionPolicy.UseParallelSessions(false, accounts), "");
	}

	// ---------------------------------------------------------------- group 9
	// Real HKCU access, but confined to a throwaway "Software\GolfCatchTest_<guid>"
	// subtree that is deleted in the finally block. Never touches the real Run key.
	private static void LegacyAutoRunCleanupTests()
	{
		string baseName = "Software\\GolfCatchTest_" + Guid.NewGuid().ToString("N");
		string runPath = baseName + "\\Run";
		try
		{
			// A) stale value present -> deleted; unrelated values untouched; nothing created
			using (RegistryKey k = Registry.CurrentUser.CreateSubKey(runPath))
			{
				k.SetValue(LegacyAutoRunCleanup.LegacyValueName, "\"C:\\old\\booking.exe\" --auto-run", RegistryValueKind.String);
				k.SetValue("SomeOtherApp", "keep me", RegistryValueKind.String);
			}
			bool removed = LegacyAutoRunCleanup.Run(runPath);
			using (RegistryKey k = Registry.CurrentUser.OpenSubKey(runPath))
			{
				Expect("stale GolfCatchBooking value is deleted",
					removed && k != null && k.GetValue(LegacyAutoRunCleanup.LegacyValueName) == null,
					"removed=" + removed);
				Expect("unrelated Run values are left intact",
					k != null && (string)k.GetValue("SomeOtherApp") == "keep me", "");
				Expect("no new value was created",
					k != null && k.GetValueNames().Length == 1,
					"names=" + (k == null ? "(null)" : string.Join(",", k.GetValueNames())));
			}

			// B) run again -> nothing to do
			Expect("second run reports nothing removed", !LegacyAutoRunCleanup.Run(runPath), "");

			// C) missing Run key -> false, and the key is NOT materialised
			string missingPath = baseName + "\\NoSuchRun";
			bool r = LegacyAutoRunCleanup.Run(missingPath);
			using RegistryKey mk = Registry.CurrentUser.OpenSubKey(missingPath);
			Expect("missing key -> false and key not created", !r && mk == null, "r=" + r + " keyExists=" + (mk != null));
		}
		finally
		{
			try { Registry.CurrentUser.DeleteSubKeyTree(baseName, throwOnMissingSubKey: false); } catch { }
		}
	}

	// ---------------------------------------------------------------- group 7
	private static void SunValleyScheduleTests()
	{
		// Tue 2026-06-23 belongs to the week beginning Mon 2026-06-22.
		Schedule("Seorak weekday", 0, "20260623", new DateTime(2026, 6, 8, 9, 0, 0));
		Schedule("Iljuk weekday", 1, "20260623", new DateTime(2026, 6, 8, 9, 30, 0));
		Schedule("Dongwon weekday", 2, "20260623", new DateTime(2026, 6, 8, 10, 0, 0));

		// Sat 2026-06-27 uses Fri 2026-06-12 for Iljuk/Dongwon.
		Schedule("Seorak weekend still Monday", 0, "20260627", new DateTime(2026, 6, 8, 9, 0, 0));
		Schedule("Iljuk weekend Friday", 1, "20260627", new DateTime(2026, 6, 12, 9, 30, 0));
		Schedule("Dongwon weekend Friday", 2, "20260627", new DateTime(2026, 6, 12, 10, 0, 0));

		var unknown = new bookInfo(3, "NA", 0, "20260623", 900, 1000, 900);
		Expect("Yeoju has no assumed schedule", !SunValleySchedule.TryGetSingleCheckTime(unknown, out _, out _), "");

		DateTime at = new DateTime(2026, 6, 8, 9, 0, 0);
		Expect("before check time waits", SunValleySchedule.WaitRequired(at.AddSeconds(-1), at), "");
		Expect("at check time proceeds immediately", !SunValleySchedule.WaitRequired(at, at), "");
		Expect("after check time proceeds immediately", !SunValleySchedule.WaitRequired(at.AddMinutes(5), at), "");
	}

	private static void Schedule(string name, int course, string date, DateTime expected)
	{
		var req = new bookInfo(course, "NA", 0, date, 900, 1000, 900);
		bool configured = SunValleySchedule.TryGetSingleCheckTime(req, out DateTime actual, out _);
		Expect(name, configured && actual == expected, "actual=" + actual.ToString("yyyy-MM-dd HH:mm:ss"));
	}

	// ---------------------------------------------------------------- group 8
	private static void ReservationHistoryMatchTests()
	{
		// a realistic history page carrying one matching row
		string page = "<html><body><h1>마이페이지 &gt; 예약내역</h1>"
			+ "<tr class='rsv'><td>2026-09-30 (20260930)</td><td>08:50 (0850)</td>"
			+ "<td>설악썬밸리</td><td>설악</td><td>SV-77</td><td>예약완료</td></tr></body></html>";

		Expect("LooksLikeHistoryPage: real page", ReservationHistoryMatch.LooksLikeHistoryPage(page), "");
		Expect("LooksLikeHistoryPage: empty -> false", !ReservationHistoryMatch.LooksLikeHistoryPage(""), "");
		Expect("LooksLikeHistoryPage: unsupported/error page -> false",
			!ReservationHistoryMatch.LooksLikeHistoryPage("<html><body>지원되지 않는 페이지입니다. 예약내역</body></html>"), "");
		Expect("LooksLikeHistoryPage: unrelated page -> false",
			!ReservationHistoryMatch.LooksLikeHistoryPage("<html><body>welcome to the front page, nothing here at all</body></html>"), "");

		// primary path: date + tee time + selected COURSE identify the row
		Expect("match: date + time(HH:MM) + course present",
			ReservationHistoryMatch.Matches(page, "20260930", "08:50", "설악", "설악썬밸리", out _), "");
		Expect("match: chosen time given as HHMM still hits",
			ReservationHistoryMatch.Matches(page, "20260930", "0850", "설악", "설악썬밸리", out _), "");

		Expect("no match: wrong date", !ReservationHistoryMatch.Matches(page, "20261001", "08:50", "설악", "설악썬밸리", out _), "");
		Expect("no match: wrong tee time", !ReservationHistoryMatch.Matches(page, "20260930", "09:10", "설악", "설악썬밸리", out _), "");
		Expect("no match: wrong course", !ReservationHistoryMatch.Matches(page, "20260930", "08:50", "설악", "여주썬밸리", out _), "");

		// starter is MANDATORY even when the course matches: date+time+course hit but the
		// selected starter is absent / different -> Fail (never a faked success).
		string pageWrongStarter = "<html><body>예약내역 2026-09-30 08:50 (0850) 동원썬밸리 스타터 썬 예약완료 padding</body></html>";
		Expect("no match: course hit but selected starter absent -> Fail",
			!ReservationHistoryMatch.Matches(pageWrongStarter, "20260930", "0850", "설악", "동원썬밸리", out _), "");
		Expect("no match: course hit, no starter token on page at all -> Fail",
			!ReservationHistoryMatch.Matches("<html><body>예약내역 2026-09-30 08:50 동원썬밸리 예약완료 padding padding</body></html>",
				"20260930", "08:50", "설악", "동원썬밸리", out _), "");
		Expect("match: course hit AND selected starter present -> success",
			ReservationHistoryMatch.Matches(pageWrongStarter, "20260930", "0850", "썬", "동원썬밸리", out _), "");

		Expect("no match: not a history page (uncertain -> never a faked success)",
			!ReservationHistoryMatch.Matches("<html><body>페이지를 찾을 수 없습니다 20260930 0850 설악 설악썬밸리</body></html>",
				"20260930", "08:50", "설악", "설악썬밸리", out _), "");
		Expect("no match: empty page", !ReservationHistoryMatch.Matches("", "20260930", "08:50", "설악", "설악썬밸리", out _), "");

		// no course name known -> the selected STARTER must be present instead
		string pageNoCourse = "<html><body>예약내역 2026-09-30 08:50 스타터 설악 예약완료 padding padding</body></html>";
		Expect("no-course path: starter present -> match",
			ReservationHistoryMatch.Matches(pageNoCourse, "20260930", "08:50", "설악", null, out _), "");
		Expect("no-course path: starter absent -> no match",
			!ReservationHistoryMatch.Matches("<html><body>예약내역 2026-09-30 08:50 예약완료 padding padding padding</body></html>",
				"20260930", "08:50", "설악", null, out _), "");
		Expect("no-course path: starter 'NA' not required -> match",
			ReservationHistoryMatch.Matches("<html><body>예약내역 2026-09-30 08:50 예약완료 padding padding padding</body></html>",
				"20260930", "08:50", "NA", null, out _), "");

		// date separators
		Expect("match: yyyy-mm-dd date form",
			ReservationHistoryMatch.Matches("<html><body>예약내역 2026-09-30 0850 설악썬밸리 done</body></html>",
				"20260930", "0850", "설악", "설악썬밸리", out _), "");
		Expect("match: yyyy.mm.dd date form",
			ReservationHistoryMatch.Matches("<html><body>예약내역 2026.09.30 08:50 설악썬밸리 done</body></html>",
				"20260930", "08:50", "설악", "설악썬밸리", out _), "");

		// ConditionParser now carries the course name for this check
		var bi = ConditionParser.Parse("설악썬밸리,20260930,0850,설악",
			new[] { "설악썬밸리", "썬밸리CC", "동원썬밸리", "여주썬밸리" }, new[] { "설악", "썬", "밸리", "NA" });
		Expect("ConditionParser fills bookInfo.courseName", bi != null && bi.courseName == "설악썬밸리", "courseName=" + bi?.courseName);
	}

	// ---------------------------------------------------------------- group 1
	private static string Row(string hhmm, string starter) =>
		"javascript:bookingReg('0','" + hhmm + "','J21','" + starter + "','x')";

	private static void NearestTimeTests()
	{
		Nearest("exact match", L(Row("0840", "설악"), Row("0900", "설악"), Row("0920", "설악")), 900, "설악", "09:00", 0);
		Nearest("nearest is earlier", L(Row("0850", "설악"), Row("0920", "설악")), 900, "설악", "08:50", 10);
		Nearest("nearest is later", L(Row("0840", "설악"), Row("0905", "설악")), 900, "설악", "09:05", 5);
		Nearest("equal distance -> EARLIER", L(Row("0910", "설악"), Row("0850", "설악")), 900, "설악", "08:50", 10);
		Nearest("equal distance -> EARLIER (reversed input)", L(Row("0850", "설악"), Row("0910", "설악")), 900, "설악", "08:50", 10);
		Nearest("minute math, not raw HHMM", L(Row("0855", "설악"), Row("0945", "설악")), 900, "설악", "08:55", 5);
		Nearest("crosses the hour", L(Row("0955", "설악"), Row("1010", "설악")), 1000, "설악", "09:55", 5);
		Nearest("starter filter excludes wrong course", L(Row("0900", "썬"), Row("0910", "설악")), 900, "설악", "09:10", 10);
		Nearest("starter NA matches any", L(Row("0902", "썬"), Row("0930", "밸리")), 900, "NA", "09:02", 2);
		Nearest("skips unparseable rows", L("javascript:noop()", Row("0903", "설악"), "bad,,,"), 900, "설악", "09:03", 3);
		NearestNull("no eligible row (starter mismatch)", L(Row("0900", "썬"), Row("0930", "밸리")), 900, "설악");
		NearestNull("empty list", L(), 900, "NA");

		var retryReq = new bookInfo(0, "NA", 0, "20260521", 0, 2359, 900);
		var rejected = new HashSet<string> { "08:50" };
		var retrySlot = TeeSelector.PickNearest(L(Row("0850", "NA"), Row("0905", "NA")), retryReq,
			rejected, delegate { }, out string retryTime, out int retryDelta);
		Expect("concurrent retry skips rejected tee", retrySlot != null && retryTime == "09:05" && retryDelta == 5,
			"chosen=" + (retryTime ?? "null") + " delta=" + retryDelta);

		// desiredTime fallback: when desired==0 the ctor falls back to startTime
		var reqFallback = new bookInfo(0, "NA", 0, "20260521", 930, 2359, 0);
		Expect("desiredTime falls back to startTime", reqFallback.desiredTime == 930, "desiredTime=" + reqFallback.desiredTime);

		// no time window: a slot far from the desired time is still eligible (just farther)
		Nearest("no window filter: far slot still eligible",
			L(Row("0700", "설악"), Row("2000", "설악")), 900, "설악", "07:00", 120);
	}

	private static void Nearest(string name, List<IWebElement> rows, int desired, string starter, string wantTee, int wantDelta)
	{
		var req = new bookInfo(0, starter, 0, "20260521", 0, 2359, desired);
		var log = new List<string>();
		var el = TeeSelector.PickNearest(rows, req, log.Add, out string hhmm, out int delta);
		Expect(name, el != null && hhmm == wantTee && delta == wantDelta,
			"chosen=" + (hhmm ?? "null") + " delta=" + delta + " want " + wantTee + "/" + wantDelta);
	}

	private static void NearestNull(string name, List<IWebElement> rows, int desired, string starter)
	{
		var req = new bookInfo(0, starter, 0, "20260521", 0, 2359, desired);
		var el = TeeSelector.PickNearest(rows, req, delegate { }, out string hhmm, out int _);
		Expect(name, el == null, "chosen=" + (hhmm ?? "null"));
	}

	// ---------------------------------------------------------------- group 2
	private static void FalsePreopeningTests()
	{
		// The pure resolver: title says pre-open/unknown but a row probe found rows -> OPEN.
		Expect("오픈전 + rows present -> SlotsAvailable",
			BookingDiagnostics.ResolveWithRowProbe(BookingPageState.NotOpenYet, 3) == BookingPageState.SlotsAvailable, "");
		Expect("Unknown title + rows present -> SlotsAvailable",
			BookingDiagnostics.ResolveWithRowProbe(BookingPageState.Unknown, 1) == BookingPageState.SlotsAvailable, "");
		Expect("오픈전 + NO rows -> stays NotOpenYet (bounded refresh)",
			BookingDiagnostics.ResolveWithRowProbe(BookingPageState.NotOpenYet, 0) == BookingPageState.NotOpenYet, "");
		Expect("Closed is never overridden by a row probe",
			BookingDiagnostics.ResolveWithRowProbe(BookingPageState.Closed, 5) == BookingPageState.Closed, "");
		Expect("RedirectedAway is never overridden by a row probe",
			BookingDiagnostics.ResolveWithRowProbe(BookingPageState.RedirectedAway, 5) == BookingPageState.RedirectedAway, "");
		Expect("SlotsAvailable passes through",
			BookingDiagnostics.ResolveWithRowProbe(BookingPageState.SlotsAvailable, 0) == BookingPageState.SlotsAvailable, "");

		// Classify(): title text drives the first read; URL drives away-detection.
		var onRes = "https://www.sunvalley.co.kr/reservation/golf?sel=J21";
		Expect("Classify: title 잔여팀 -> SlotsAvailable",
			BookingDiagnostics.Classify(new FakeDriver(onRes), new FakeEl(title: "잔여팀 3"), out _, out _) == BookingPageState.SlotsAvailable, "");
		Expect("Classify: title 마감 -> Closed",
			BookingDiagnostics.Classify(new FakeDriver(onRes), new FakeEl(title: "마감"), out _, out _) == BookingPageState.Closed, "");
		Expect("Classify: title 오픈전입니다 -> NotOpenYet (advisory only)",
			BookingDiagnostics.Classify(new FakeDriver(onRes), new FakeEl(title: "오픈전입니다"), out _, out _) == BookingPageState.NotOpenYet, "");
		Expect("Classify: unrecognised title -> Unknown",
			BookingDiagnostics.Classify(new FakeDriver(onRes), new FakeEl(title: "예약대기"), out _, out _) == BookingPageState.Unknown, "");
		Expect("Classify: url on /member/login -> RedirectedAway",
			BookingDiagnostics.Classify(new FakeDriver("https://www.sunvalley.co.kr/member/login?returnURL=/reservation/golf"), new FakeEl(title: "잔여팀"), out _, out _) == BookingPageState.RedirectedAway, "");
		Expect("Classify: null date cell on reservation page -> DateCellMissing",
			BookingDiagnostics.Classify(new FakeDriver(onRes), null, out _, out _) == BookingPageState.DateCellMissing, "");

		// End-to-end regression statement: 오픈전입니다 + real rows must NOT refresh.
		var titleState = BookingDiagnostics.Classify(new FakeDriver(onRes), new FakeEl(title: "오픈전입니다"), out _, out _);
		var afterProbe = BookingDiagnostics.ResolveWithRowProbe(titleState, /* .btn.btn-res rows found */ 4);
		Expect("REGRESSION: 오픈전입니다 + 4 real rows resolves to SlotsAvailable (no pre-opening refresh)",
			afterProbe == BookingPageState.SlotsAvailable, "afterProbe=" + afterProbe);
	}

	// ---------------------------------------------------------------- group 3
	private static readonly string[] Courses = { "설악썬밸리", "썬밸리CC", "동원썬밸리", "여주썬밸리" };
	private static readonly string[] Starters = { "설악", "썬", "밸리", "NA" };

	private static void ConditionParserTests()
	{
		// ---- canonical 4-field: course,date,desiredHHMM,starter ----
		var m = ConditionParser.Parse("설악썬밸리,20260521,905,설악", Courses, Starters);
		Expect("4-field: desired parsed",
			m != null && m.course == 0 && m.date == "20260521" && m.desiredTime == 905
			&& m.starter == "설악" && m.starterIndex == 0, Dump(m));
		Expect("4-field: startTime/endTime both equal desired",
			m != null && m.startTime == 905 && m.endTime == 905, Dump(m));
		var mLate = ConditionParser.Parse("설악썬밸리,20260521,2250,설악", Courses, Starters);
		Expect("4-field: no window / no clamp",
			mLate != null && mLate.desiredTime == 2250 && mLate.endTime == 2250, Dump(mLate));
		var sep = ConditionParser.Parse("여주썬밸리,2026-05-21,09:05,밸리", Courses, Starters);
		Expect("4-field: separators stripped",
			sep != null && sep.course == 3 && sep.date == "20260521" && sep.desiredTime == 905 && sep.starter == "밸리", Dump(sep));

		// ---- 5-field value from an older build: read the start field only, ignore the end ----
		var a = ConditionParser.Parse("설악썬밸리,20260521,0900,1130,설악", Courses, Starters);
		Expect("5-field: start read as desired, end ignored",
			a != null && a.course == 0 && a.date == "20260521" && a.desiredTime == 900
			&& a.startTime == 900 && a.endTime == 900 && a.starter == "설악", Dump(a));
		var gt = ConditionParser.Parse("설악썬밸리,20260521,1200,1000,설악", Courses, Starters);
		Expect("5-field: obsolete end field ignored even when start > 'end'", gt != null && gt.desiredTime == 1200, Dump(gt));
		var be = ConditionParser.Parse("설악썬밸리,20260521,0900,1075,설악", Courses, Starters);
		Expect("5-field: invalid end field ignored", be != null && be.desiredTime == 900, Dump(be));
		Expect("5-field: invalid start/desired field -> null",
			ConditionParser.Parse("설악썬밸리,20260521,2400,2500,설악", Courses, Starters) == null, "");

		var c = ConditionParser.Parse("UnknownCourse,20260521,0900,NA", Courses, Starters);
		Expect("unknown course -> index 0", c != null && c.course == 0, Dump(c));
		var d = ConditionParser.Parse("설악썬밸리,20260521,0900,없는스타터", Courses, Starters);
		Expect("unknown starter -> index -1 but still parses", d != null && d.starterIndex == -1 && d.starter == "없는스타터", Dump(d));

		Expect("blank -> null", ConditionParser.Parse("", Courses, Starters) == null, "");
		Expect("unset placeholder -> null", ConditionParser.Parse("<조건 미설정>", Courses, Starters) == null, "");
		Expect("too few fields (3) -> null", ConditionParser.Parse("설악썬밸리,20260521,900", Courses, Starters) == null, "");
		Expect("bad date length -> null", ConditionParser.Parse("설악썬밸리,202605,0900,설악", Courses, Starters) == null, "");
		Expect("non-numeric desired -> null", ConditionParser.Parse("설악썬밸리,20260521,abc,설악", Courses, Starters) == null, "");
		Expect("null course-name table tolerated", ConditionParser.Parse("설악썬밸리,20260521,0900,설악", null, null) != null, "");
	}

	// ---------------------------------------------------------------- group 4
	private static void SequentialGateTests()
	{
		var confirmed = new BookOutcome { Result = OpResult.BookOneSuccess, SlotFound = true, Submitted = true, ConfirmationId = "SV-1234567", HistoryVerified = true };
		var noId = new BookOutcome { Result = OpResult.BookOneSuccess, SlotFound = true, Submitted = true, ConfirmationId = null, HistoryVerified = false };
		var notInHistory = new BookOutcome { Result = OpResult.BookOneSuccess, SlotFound = true, Submitted = true, ConfirmationId = "SV-9", HistoryVerified = false };
		var notSubmitted = new BookOutcome { Result = OpResult.FoundSlot, SlotFound = true, Submitted = false };
		var noSlot = new BookOutcome { Result = OpResult.NoProperTime, SlotFound = false };
		var redirected = new BookOutcome { Result = OpResult.FalalError, SlotFound = false, Detail = "redirected away" };
		var browserFailure = new BookOutcome { Result = OpResult.Fail, SlotFound = false, Detail = "navigation error" };

		Expect("confirmed -> condition 2 RUNS", SequentialGate.ShouldRunCondition2(confirmed), "");
		Expect("submitted but no confirmation id -> condition 2 RUNS", SequentialGate.ShouldRunCondition2(noId), "");
		Expect("submitted + id but not in history -> condition 2 RUNS", SequentialGate.ShouldRunCondition2(notInHistory), "");
		Expect("slot found but not submitted (diagnostic) -> condition 2 RUNS", SequentialGate.ShouldRunCondition2(notSubmitted), "");
		Expect("no matching slot -> condition 2 RUNS", SequentialGate.ShouldRunCondition2(noSlot), "");
		Expect("redirected / fatal -> condition 2 SKIPPED", !SequentialGate.ShouldRunCondition2(redirected), "");
		Expect("browser/navigation failure -> condition 2 SKIPPED", !SequentialGate.ShouldRunCondition2(browserFailure), "");
		Expect("null outcome -> condition 2 SKIPPED", !SequentialGate.ShouldRunCondition2(null), "");

		Expect("BookOutcome.GateSatisfied only when submitted+id+history", confirmed.GateSatisfied && !noId.GateSatisfied && !notInHistory.GateSatisfied, "");
		Expect("GateReason(diagnostic) explains no-submit", notSubmitted.GateReason(true).Contains("no reservation submitted"), notSubmitted.GateReason(true));
		Expect("GateReason(real) explains missing confirmation id", noId.GateReason(false).Contains("no server confirmation ID"), noId.GateReason(false));
		Expect("GateReason(real) explains not-in-history", notInHistory.GateReason(false).Contains("reservation history"), notInHistory.GateReason(false));
		Expect("Decision(confirmed) says starting Condition 2", SequentialGate.Decision(confirmed, false).Contains("starting Condition 2"), "");
		Expect("Decision(noSlot) says starting Condition 2", SequentialGate.Decision(noSlot, false).Contains("starting Condition 2"), "");
	}

	// ---------------------------------------------------------------- group 5
	private static void DiagnosticNoSubmitTests()
	{
		// In a DIAGNOSTIC build the booking path never sets Submitted=true, so the gate
		// Diagnostic mode does not submit, but it must still check condition 2.
		var diagOutcome = new BookOutcome { Result = OpResult.FoundSlot, SlotFound = true, ChosenTee = "08:50", DeltaMinutes = 10, Submitted = false, ConfirmationId = null, HistoryVerified = false };
		Expect("diagnostic outcome: slot found", diagOutcome.SlotFound, "");
		Expect("diagnostic outcome: NOT submitted", !diagOutcome.Submitted, "");
		Expect("diagnostic outcome: gate NOT satisfied", !diagOutcome.GateSatisfied, "");
		Expect("diagnostic outcome: condition 2 will run", SequentialGate.ShouldRunCondition2(diagOutcome), "");
		Expect("diagnostic gate reason names diagnostic no-submit", diagOutcome.GateReason(true).Contains("diagnostic no-submit"), diagOutcome.GateReason(true));

#if DIAGNOSTIC_BUILD
		Expect("compiled with DIAGNOSTIC_BUILD", true, "");
#else
		Expect("compiled with DIAGNOSTIC_BUILD", false, "test project must define DIAGNOSTIC_BUILD");
#endif
	}

	// ---------------------------------------------------------------- group 6
	private static void CredentialStoreTests()
	{
		// SYNTHETIC password only — never a real account. Password-only store; no migration.
		string tmp = Path.Combine(Path.GetTempPath(), "gc_pwtest_" + Guid.NewGuid().ToString("N") + ".dat");
		try
		{
			Expect("store absent initially", !CredentialStore.Exists(tmp), "");
			Expect("TryLoadPassword on empty -> false", !CredentialStore.TryLoadPassword(out _, tmp), "");

			string syn = "SYN p@ss éü 가나다 !#$ \t end";
			CredentialStore.SavePassword(syn, tmp);
			Expect("store exists after SavePassword", CredentialStore.Exists(tmp), "");

			bool ok = CredentialStore.TryLoadPassword(out string pw, tmp);
			Expect("TryLoadPassword round-trips (unicode/specials/whitespace)", ok && pw == syn, "len=" + (pw?.Length));

			// on-disk blob must not contain the plaintext password
			string rawAscii = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(tmp));
			Expect("encrypted blob does not contain plaintext password", rawAscii.IndexOf("SYN p@ss", StringComparison.Ordinal) < 0, "");
			Expect("encrypted blob does not contain 가나다", rawAscii.IndexOf("가나다", StringComparison.Ordinal) < 0, "");

			// overwrite
			CredentialStore.SavePassword("SECOND_pw", tmp);
			CredentialStore.TryLoadPassword(out string pw2, tmp);
			Expect("SavePassword overwrites", pw2 == "SECOND_pw", "pw2=" + pw2);

			CredentialStore.Clear(tmp);
			Expect("Clear removes the store", !CredentialStore.Exists(tmp), "");
			Expect("TryLoadPassword after Clear -> false", !CredentialStore.TryLoadPassword(out _, tmp), "");

			// empty password still round-trips (store present, value "")
			CredentialStore.SavePassword("", tmp);
			bool ok3 = CredentialStore.TryLoadPassword(out string pw3, tmp);
			Expect("empty password round-trips", ok3 && pw3 == "", "pw3=\"" + pw3 + "\"");
		}
		finally
		{
			try { File.Delete(tmp); } catch { }
		}
	}

	// ---------------------------------------------------------------- harness / fakes
	private static List<IWebElement> L(params string[] onclicks)
	{
		var l = new List<IWebElement>();
		foreach (var o in onclicks) l.Add(new FakeEl(onclick: o));
		return l;
	}

	private static string Dump(bookInfo b) => b == null ? "null" : b.ToString();

	private static void Section(string s) { Console.WriteLine(); Console.WriteLine("== " + s + " =="); }

	private static void Expect(string name, bool ok, string detail)
	{
		Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + name + (string.IsNullOrEmpty(detail) ? "" : ("   [" + detail + "]")));
		if (ok) _pass++; else _fail++;
	}

	internal sealed class FakeEl : IWebElement
	{
		private readonly string _onclick;
		private readonly string _title;
		public FakeEl(string onclick = null, string title = null) { _onclick = onclick; _title = title; }
		public string GetAttribute(string name) => name == "onclick" ? _onclick : (name == "title" ? _title : null);
		public string GetDomAttribute(string name) => GetAttribute(name);
		public string GetDomProperty(string name) => null;
		public string TagName => "a";
		public string Text => "";
		public bool Enabled => true;
		public bool Selected => false;
		public Point Location => Point.Empty;
		public Size Size => Size.Empty;
		public bool Displayed => true;
		public void Clear() { }
		public void Click() { }
		public ISearchContext GetShadowRoot() => null;
		public string GetCssValue(string propertyName) => null;
		public IWebElement FindElement(By by) => null;
		public ReadOnlyCollection<IWebElement> FindElements(By by) => new ReadOnlyCollection<IWebElement>(new List<IWebElement>());
		public void SendKeys(string text) { }
		public void Submit() { }
	}

	internal sealed class FakeDriver : IWebDriver
	{
		public FakeDriver(string url) { Url = url; }
		public string Url { get; set; }
		public string Title => "";
		public string PageSource => "";
		public string CurrentWindowHandle => "w";
		public ReadOnlyCollection<string> WindowHandles => new ReadOnlyCollection<string>(new List<string> { "w" });
		public void Close() { }
		public void Quit() { }
		public IOptions Manage() => null;
		public INavigation Navigate() => null;
		public ITargetLocator SwitchTo() => null;
		public IWebElement FindElement(By by) => null;
		public ReadOnlyCollection<IWebElement> FindElements(By by) => new ReadOnlyCollection<IWebElement>(new List<IWebElement>());
		public void Dispose() { }
	}
}
