using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;

namespace booking;

internal class sunValley : club
{
	private IWebElement target;

	private int teeTableCnt;

	private string lastConfirmationId;

	// Set true only when ConfirmViaHistory matched the reservation on the history page with
	// the STRICT criterion (date + tee time + course + starter). BookCore then keeps
	// HistoryVerified = true without re-running the weaker VerifyReservationHistory check.
	private bool lastHistoryStrictConfirmed;

	private const string loginUrl = "https://www.sunvalley.co.kr/member/login?returnURL=/reservation/golf";

	// Upper bounds rather than fixed delays: fast PCs continue immediately, while
	// slower PCs get time for the browser and site to reach the required state.
	private const int LoginFormTimeoutSeconds = 45;
	private const int LoginCompletionTimeoutSeconds = 60;
	private const int ReservationPageTimeoutSeconds = 45;
	private const int ConfirmationDialogTimeoutSeconds = 20;
	private const int PageContentTimeoutSeconds = 20;
	private const int CompletionAlertTimeoutSeconds = 20;

	private string[] clubs = new string[4] { "https://www.sunvalley.co.kr/reservation/golf?sel=J21", "https://www.sunvalley.co.kr/reservation/golf?sel=J23", "https://www.sunvalley.co.kr/reservation/golf?sel=J24", "https://www.sunvalley.co.kr/reservation/golf?sel=J25" };

	// The reservation site used to honour ?sel=Jxx directly.  It now sometimes
	// redirects an authenticated request back to the regional course picker.  Keep
	// the direct URL (it is still the fastest path), but complete that picker when
	// the calendar for the requested course was not loaded.
	private static readonly string[] courseCodes = new string[4] { "J21", "J23", "J24", "J25" };

	private static readonly string[] courseNames = new string[4] { "설악썬밸리", "썬밸리CC", "동원썬밸리", "여주썬밸리" };

	public override IWebElement compare(IWebElement tee, bookInfo req)
	{
		IWebElement result = null;
		try
		{
			string[] array = tee.GetAttribute("onclick").Split(',');
			frm.logtxtBox(array[1] + array[3] + " index " + teeIndex);
			int num = int.Parse(array[1].Replace("'", ""));
			if (searchStartTime == 0)
			{
				searchStartTime = num;
			}
			if (num < req.startTime)
			{
				teeIndex++;
				increment = 1;
			}
			else if (num > req.endTime)
			{
				teeIndex--;
				increment = -1;
			}
			else
			{
				teeIndex += increment;
				if (req.starter == "NA" || req.starter == array[3].Replace("'", ""))
				{
					result = tee;
				}
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + "compare " + ex);
		}
		return result;
	}

	private bool buildTeeTable()
	{
		try
		{
			for (int i = 0; i < bookReqs.Count; i++)
			{
				bookInfo bookInfo2 = bookReqs[i];
				((WebDriver)drv).Navigate().GoToUrl(clubs[bookInfo2.course]);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@id='tabCourseALL']/div/div/table/tbody/tr"));
				List<int> list = new List<int>();
				foreach (IWebElement item in readOnlyCollection)
				{
					ReadOnlyCollection<IWebElement> readOnlyCollection2 = ((ISearchContext)item).FindElements(By.TagName("td"));
					list.Add(int.Parse(readOnlyCollection2[3].Text.Replace(":", "")));
				}
				frm.logtxtBox("buildTeeTable done : num " + list.Count);
				teeTableCnt = list.Count;
			}
		}
		catch (Exception)
		{
			return false;
		}
		return true;
	}

	public override bool login(Form1 _frm)
	{
		frm = _frm;
		return LoginWithStateWait();

		try
		{
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl(LoginUrl());
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='usrId']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='usrPwd']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='fnLogin']")));
				Thread.Sleep(500);
				try
				{
					string text = ((WebDriver)drv).SwitchTo().Alert().Text;
					if (!text.Contains("환영"))
					{
						frm.logtxtBox("T # " + threadIndex + "login fail " + text);
						return false;
					}
					((WebDriver)drv).SwitchTo().Alert().Accept();
				}
				catch (Exception)
				{
				}
				Thread.Sleep(500);
			}
		}
		catch (Exception)
		{
			return false;
		}
		return true;
	}

	// Login must be proven by the post-login page state.  The legacy implementation
	// waited a fixed one second and then treated an absent alert as success, which
	// fails silently on slow PCs and leaves only "Login Failed" in the outer log.
	private bool LoginWithStateWait()
	{
		IWebDriver driver2 = (IWebDriver)(object)drv;
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			frm.logtxtBox("T # " + threadIndex + " LOGIN: opening page; waiting up to " + LoginFormTimeoutSeconds + "s for usable fields.");
			((WebDriver)drv).Navigate().GoToUrl(LoginUrl());
			lock (WebDriverExtensions.lockObject)
			{
				if (!WaitForLoginForm(driver2, out IWebElement idBox, out IWebElement passwordBox, out IWebElement loginButton))
				{
					LoginFailure("login-form-timeout", "ID/password/login button did not become usable within " + LoginFormTimeoutSeconds + "s");
					return false;
				}
				idBox.Clear();
				passwordBox.Clear();
				idBox.SendKeys(id);
				passwordBox.SendKeys(pwd);
				frm.logtxtBox("T # " + threadIndex + " LOGIN: form ready after " + stopwatch.ElapsedMilliseconds + "ms; submitting.");
				WebDriverExtensions.clickLock(loginButton);
				if (!WaitForAuthenticatedPage(driver2, out string reason))
				{
					LoginFailure("login-not-completed", reason);
					return false;
				}
				frm.logtxtBox("T # " + threadIndex + " LOGIN: authenticated page confirmed after " + stopwatch.ElapsedMilliseconds + "ms.");
			}
			return true;
		}
		catch (Exception ex)
		{
			LoginFailure("login-exception", ex.GetType().Name + ": " + ex.Message);
			return false;
		}
	}

	private bool WaitForLoginForm(IWebDriver driver2, out IWebElement idBox, out IWebElement passwordBox, out IWebElement loginButton)
	{
		idBox = null;
		passwordBox = null;
		loginButton = null;
		DateTime deadline = DateTime.Now.AddSeconds(LoginFormTimeoutSeconds);
		while (!frm.stopClicked && DateTime.Now < deadline)
		{
			try
			{
				ReadOnlyCollection<IWebElement> ids = driver2.FindElements(By.Id("usrId"));
				ReadOnlyCollection<IWebElement> passwords = driver2.FindElements(By.Id("usrPwd"));
				ReadOnlyCollection<IWebElement> buttons = driver2.FindElements(By.Id("fnLogin"));
				if (ids.Count > 0 && passwords.Count > 0 && buttons.Count > 0
					&& ids[0].Displayed && ids[0].Enabled
					&& passwords[0].Displayed && passwords[0].Enabled
					&& buttons[0].Displayed && buttons[0].Enabled)
				{
					idBox = ids[0];
					passwordBox = passwords[0];
					loginButton = buttons[0];
					return true;
				}
			}
			catch (Exception)
			{
				// The document can be replaced while loading; poll the next state.
			}
			Thread.Sleep(150);
		}
		return false;
	}

	private bool WaitForAuthenticatedPage(IWebDriver driver2, out string reason)
	{
		reason = "authentication did not complete within " + LoginCompletionTimeoutSeconds + "s";
		DateTime deadline = DateTime.Now.AddSeconds(LoginCompletionTimeoutSeconds);
		while (!frm.stopClicked && DateTime.Now < deadline)
		{
			try
			{
				IAlert alert = null;
				try { alert = driver2.SwitchTo().Alert(); } catch (NoAlertPresentException) { }
				if (alert != null)
				{
					string message = alert.Text ?? "";
					alert.Accept();
					if (message.Contains("환영"))
						frm.logtxtBox("T # " + threadIndex + " LOGIN: success alert received; waiting for authenticated page.");
					else
					{
						reason = "site alert: " + message;
						return false;
					}
				}

				string url = BookingDiagnostics.SafeUrl(driver2) ?? "";
				bool stillShowingLoginForm = driver2.FindElements(By.Id("usrId")).Count > 0
					|| driver2.FindElements(By.Id("usrPwd")).Count > 0;
				if (!stillShowingLoginForm && url.IndexOf("/member/login", StringComparison.OrdinalIgnoreCase) < 0)
					return true;
			}
			catch (Exception ex)
			{
				reason = "while waiting for login completion: " + ex.GetType().Name + ": " + ex.Message;
			}
			Thread.Sleep(150);
		}
		if (frm.stopClicked)
			reason = "stopped by user while waiting for login completion";
		return false;
	}

	private void LoginFailure(string kind, string reason)
	{
		IWebDriver driver2 = (IWebDriver)(object)drv;
		string url = BookingDiagnostics.SafeUrl(driver2) ?? "(unavailable)";
		frm.logtxtBox("T # " + threadIndex + " LOGIN FAILED [" + kind + "]: " + reason + " url=" + url);
		BookingDiagnostics.Capture(drv, diagnosticsDir, kind, "reason=" + reason + " url=" + url);
	}

	// Wait for a replacement page to contain real HTML instead of assuming a fixed
	// browser-rendering time.  This is used after history-page navigation.
	private bool WaitForPageContent(IWebDriver driver2, int timeoutSeconds, out string page)
	{
		page = "";
		DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);
		while (!frm.stopClicked && DateTime.Now < deadline)
		{
			try
			{
				page = driver2.PageSource ?? "";
				if (page.Length >= 100)
					return true;
			}
			catch (Exception)
			{
				// The navigation may still be replacing the document.
			}
			Thread.Sleep(150);
		}
		return false;
	}

	// Wait for a server response alert.  It deliberately does not accept the alert;
	// the existing result handling below remains responsible for that decision.
	private bool WaitForAlert(int timeoutSeconds)
	{
		IWebDriver driver2 = (IWebDriver)(object)drv;
		DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);
		while (!frm.stopClicked && DateTime.Now < deadline)
		{
			try
			{
				driver2.SwitchTo().Alert();
				return true;
			}
			catch (NoAlertPresentException)
			{
				Thread.Sleep(150);
			}
		}
		return false;
	}

	public override bool prepareReservation2Session(Form1 _frm, bool sameAccount)
	{
		frm = _frm;
		if (sameAccount)
		{
			frm.logtxtBox("예약 2: 예약 1과 같은 계정이므로 기존 로그인 세션을 재사용합니다.");
			return true;
		}

		try
		{
			IWebDriver driver2 = (IWebDriver)(object)drv;
			// The header control is not consistently an anchor/button, but the site
			// itself wires #fnLogOut to this endpoint.  Use the endpoint directly so
			// the server invalidates its session (deleting browser cookies alone does
			// not do that).
			frm.logtxtBox("예약 2: 기존 계정의 서버 로그아웃을 실행합니다.");
			((WebDriver)drv).Navigate().GoToUrl("https://www.sunvalley.co.kr/member/logout");
			((WebDriver)drv).Navigate().GoToUrl(LoginUrl());
			for (int attempt = 0; attempt < 180; attempt++)
			{
				if (driver2.FindElements(By.Id("usrId")).Count > 0 && driver2.FindElements(By.Id("usrPwd")).Count > 0)
				{
					frm.logtxtBox("예약 2: 로그인 폼 확인 완료.");
					return true;
				}
				Thread.Sleep(250);
			}
			string url = BookingDiagnostics.SafeUrl(driver2);
			frm.logtxtBox("예약 2 세션 전환 실패: 로그아웃 후 로그인 폼이 표시되지 않았습니다. url=" + url);
			BookingDiagnostics.Capture(drv, diagnosticsDir, "reservation2-login-form-missing", "url=" + url);
			return false;
		}
		catch (Exception ex)
		{
			frm.logtxtBox("예약 2 세션 전환 오류: " + ex.GetType().Name + " - " + ex.Message);
			BookingDiagnostics.Capture(drv, diagnosticsDir, "reservation2-session-switch-error", ex.ToString());
			return false;
		}
	}

	private string LoginUrl()
	{
		return dummyTestMode ? dummyBaseUrl + "/login.html" : loginUrl;
	}

	private string ReservationUrl(int courseIndex)
	{
		return dummyTestMode ? dummyBaseUrl + "/reservation/golf.html" : clubs[courseIndex];
	}

	private bool NavigateToReservation(bookInfo req, string dateCellXPath)
	{
		((WebDriver)drv).Navigate().GoToUrl(ReservationUrl(req.course));
		if (dummyTestMode)
			return true;

		// A direct URL can land on /reservation/golf without selecting a course.
		// Prefer the site's course-code link and fall back to its visible tile label.
		string code = courseCodes[req.course];
		string name = courseNames[req.course];
		DateTime deadline = DateTime.Now.AddSeconds(ReservationPageTimeoutSeconds);
		bool selectionClicked = false;
		while (DateTime.Now < deadline && !frm.stopClicked)
		{
			if (((IWebDriver)(object)drv).FindElements(By.XPath(dateCellXPath)).Count != 0)
				return true;
			if (!selectionClicked)
			{
				IWebElement choice = null;
				ReadOnlyCollection<IWebElement> links = ((IWebDriver)(object)drv).FindElements(By.XPath("//a[contains(@href, 'sel=" + code + "') or @data-course='" + code + "' or @data-code='" + code + "']"));
				if (links.Count > 0)
					choice = links[0];
				else
				{
					ReadOnlyCollection<IWebElement> labels = ((IWebDriver)(object)drv).FindElements(By.XPath("//*[self::a or self::button][normalize-space(.)='" + name + "']"));
					if (labels.Count > 0)
						choice = labels[0];
				}
				if (choice != null)
				{
					frm.logtxtBox("T # " + threadIndex + " course picker redirect detected; selecting " + name + ".");
					WebDriverExtensions.clickLock(choice);
					selectionClicked = true;
				}
			}
			Thread.Sleep(200);
		}
		frm.logtxtBox("T # " + threadIndex + " STOP: requested reservation calendar did not load (" + name + ").");
		return false;
	}

	private (bool, string) setDatePath(string dateP)
	{
		int row = 0;
		int col = 0;
		int table = 0;
		bool posDate = GetPosDate(dateP, ref table, ref row, ref col);
		string text = ((table != 1) ? "B" : "A");
		text = "//*[@id='" + text + dateP + "']/a";
		return (posDate, text);
	}

	private void handleAlert()
	{
		try
		{
			((WebDriver)drv).SwitchTo().Alert().Accept();
		}
		catch (Exception)
		{
		}
	}

	// Recovery: authoritative "is this date actually open" signal.
	// Opens the date cell and returns the real reserve rows (.btn.btn-res) the booking
	// flow consumes. Used to override a stale / misleading cell title (e.g. "오픈전입니다")
	// before the pre-opening refresh branch runs.
	private ReadOnlyCollection<IWebElement> ProbeTeeRows(IWebElement dateCell)
	{
		try
		{
			if (dateCell != null)
			{
				try
				{
					WebDriverExtensions.clickLock(dateCell);
				}
				catch (Exception)
				{
				}
			}
			return ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@class='btn btn-res']"), 10);
		}
		catch (Exception)
		{
			return null;
		}
	}

	// Legacy dayPool-driven entry point (kept for compatibility; not used by the
	// sequential two-condition orchestrator).
	public override OpResult bookOne()
	{
		int num = workPool.allocDay();
		if (num < 0)
		{
			return OpResult.Success;
		}
		BookOutcome outcome = BookCore(bookReqs[num]);
		if (outcome.Result == OpResult.Success)
		{
			workPool.returnDay(num);
		}
		else
		{
			workPool.finishDay(num);
		}
		return outcome.Result;
	}

	// Recovery feature: explicit single-request booking with a rich outcome, used by the
	// sequential hard gate. Keeps the false-preopening fix, bounded retries, diagnostics
	// and diagnostic no-submit behaviour.
	public override BookOutcome bookRequest(bookInfo req)
	{
		return BookCore(req);
	}

	private bool WaitForSingleCheck(DateTime checkAt, string rule)
	{
		frm.logtxtBox("T # " + threadIndex + " waiting until " + checkAt.ToString("yyyy-MM-dd HH:mm:ss")
			+ " (" + rule + "; one calendar load only)");
		while (!frm.stopClicked && DateTime.Now < checkAt)
		{
			double remaining = (checkAt - DateTime.Now).TotalMilliseconds;
			Thread.Sleep((int)Math.Max(1, Math.Min(30000, Math.Ceiling(remaining))));
		}
		return !frm.stopClicked;
	}

	private BookOutcome BookCore(bookInfo bookInfo2)
	{
		BookOutcome outcome = new BookOutcome
		{
			DesiredTee = TeeSelector.FromMinutes(TeeSelector.ToMinutes(bookInfo2.desiredTime))
		};
		teeIndex = -1;
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2 + (diagnosticMode ? "  [DIAGNOSTIC MODE - will not submit]" : ""));
		DateTime checkAt = DateTime.MinValue;
		string scheduleRule = null;
		// Diagnostic runs must exercise navigation immediately; this build cannot submit.
		bool singleScheduledCheck = !dummyTestMode && !diagnosticMode && SunValleySchedule.TryGetSingleCheckTime(bookInfo2, out checkAt, out scheduleRule);
		if (singleScheduledCheck && !WaitForSingleCheck(checkAt, scheduleRule))
		{
			outcome.Result = OpResult.Fail;
			outcome.Detail = "stopClicked while waiting for scheduled single check";
			return outcome;
		}

		string dateCellXPath;
		try
		{
			dateCellXPath = setDatePath(bookInfo2.date).Item2;
			if (!NavigateToReservation(bookInfo2, dateCellXPath))
			{
				outcome.Result = OpResult.FalalError;
				outcome.Detail = "course picker did not open the requested reservation calendar";
				return outcome;
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " : GoToUrl Error " + bookInfo2.course + " " + bookInfo2.date + " Msg" + ex);
			BookingDiagnostics.Capture(drv, diagnosticsDir, "goto-url-error", "course=" + bookInfo2.course + " date=" + bookInfo2.date + " ex=" + ex.Message);
			handleAlert();
			outcome.Result = OpResult.Fail;
			outcome.Detail = "GoToUrl error: " + ex.Message;
			return outcome;
		}

		DateTime start = DateTime.Now;
		int refreshCount = 0;
		string lastTitle = null;
		string lastUrl = null;

		while (!frm.stopClicked)
		{
			IWebElement dateCell = ((IWebDriver)(object)drv).FindElement(By.XPath(dateCellXPath), 30);
			BookingPageState state = BookingDiagnostics.Classify((IWebDriver)(object)drv, dateCell, out lastTitle, out lastUrl);

			// --- unrecoverable: not on the reservation page (session lost / entry-flow change) ---
			if (state == BookingPageState.RedirectedAway)
			{
				string note = BookingDiagnostics.Capture(drv, diagnosticsDir, "redirected-away",
					"expected page containing \"" + BookingDiagnostics.ExpectedUrlFragment + "\", got url=" + lastUrl +
					" (login expired, entry-flow interstitial, or session not carried to reservation path)");
				frm.logtxtBox("T # " + threadIndex + " STOP: not on reservation page. url=" + lastUrl + "  evidence=" + note);
				outcome.Result = OpResult.FalalError;
				outcome.Detail = "redirected away from reservation page -> " + lastUrl;
				return outcome;
			}

			// --- unrecoverable: expected calendar cell absent (wrong date/course mapping or markup change) ---
			if (state == BookingPageState.DateCellMissing)
			{
				string note = BookingDiagnostics.Capture(drv, diagnosticsDir, "date-cell-missing",
					"xpath=" + dateCellXPath + " not found on url=" + lastUrl);
				frm.logtxtBox("T # " + threadIndex + " STOP: date cell not found. xpath=" + dateCellXPath + " url=" + lastUrl + "  evidence=" + note);
				outcome.Result = OpResult.FalalError;
				outcome.Detail = "date cell missing: " + dateCellXPath;
				return outcome;
			}

			// --- date is closed/full: nothing to wait for ---
			if (state == BookingPageState.Closed)
			{
				frm.logtxtBox("T # " + threadIndex + " " + lastTitle + " (마감) - date is full");
				outcome.Result = OpResult.Overbook;
				outcome.Detail = "date closed/full (마감)";
				return outcome;
			}

			// --- cell 'title' claims pre-opening, or is unrecognised ---
			// The date-cell title is NOT authoritative: Sun Valley has been observed serving
			// title="오픈전입니다" while real bookable tee-off rows were already present. Probe the
			// actual reserve rows (.btn.btn-res) before taking the pre-opening refresh branch.
			bool cellOpened = false;
			if (state == BookingPageState.NotOpenYet || state == BookingPageState.Unknown)
			{
				ReadOnlyCollection<IWebElement> probeRows = ProbeTeeRows(dateCell);
				cellOpened = true;
				int probedCount = (probeRows != null) ? probeRows.Count : 0;
				BookingPageState resolved = BookingDiagnostics.ResolveWithRowProbe(state, probedCount);
				if (resolved == BookingPageState.SlotsAvailable)
				{
					frm.logtxtBox("T # " + threadIndex + " cell title=\"" + lastTitle + "\" but " + probedCount +
						" tee row(s) present -> treating as OPEN (title not authoritative)");
					state = BookingPageState.SlotsAvailable;
					// fall through to the SlotsAvailable handler below
				}
				else
				{
					if (singleScheduledCheck)
					{
						string note = BookingDiagnostics.Capture(drv, diagnosticsDir, "single-check-not-open",
							"checked once at " + DateTime.Now.ToString("u") + "; rule=" + scheduleRule + "; title=\"" + lastTitle + "\" url=" + lastUrl);
						frm.logtxtBox("T # " + threadIndex + " STOP: not open after the one scheduled calendar load. evidence=" + note);
						outcome.Result = OpResult.NotOpen;
						outcome.Detail = "not open after one scheduled calendar load";
						return outcome;
					}
					// A diagnostic run must state the requested date's condition, rather
					// than poll or substitute an adjacent calendar date.  The real build
					// retains its bounded refresh behaviour for an actual opening window.
					if (diagnosticMode && state == BookingPageState.NotOpenYet)
					{
						string note = BookingDiagnostics.Capture(drv, diagnosticsDir, "date-not-open-DIAGNOSTIC",
							"requested date=" + bookInfo2.date + "; title=\"" + lastTitle + "\"; no tee rows. No adjacent date was considered.");
						frm.logtxtBox("T # " + threadIndex + " DIAGNOSTIC STOP: requested date " + bookInfo2.date +
							" is not open yet; no adjacent date searched. evidence=" + note);
						outcome.Result = OpResult.NotOpen;
						outcome.Detail = "requested date is not open yet; no adjacent date searched";
						return outcome;
					}
					refreshCount++;
					TimeSpan elapsed = DateTime.Now - start;
					if (refreshCount == 1 && state == BookingPageState.Unknown)
					{
						BookingDiagnostics.Capture(drv, diagnosticsDir, "unknown-cell-state",
							"title=\"" + lastTitle + "\" is not 잔여팀 / 마감 / 오픈전 and no .btn.btn-res rows on url=" + lastUrl);
					}
					if (refreshCount > maxRefresh || elapsed > maxWait)
					{
						string note = BookingDiagnostics.Capture(drv, diagnosticsDir, "refresh-limit",
							"state=" + state + " title=\"" + lastTitle + "\" (no tee rows) refreshCount=" + refreshCount + "/" + maxRefresh +
							" elapsed=" + (int)elapsed.TotalSeconds + "s/" + (int)maxWait.TotalSeconds + "s url=" + lastUrl);
						frm.logtxtBox("T # " + threadIndex + " STOP: gave up waiting (" + state + ", no tee rows). last title=\"" + lastTitle +
							"\" after " + refreshCount + " refresh / " + (int)elapsed.TotalSeconds + "s.  evidence=" + note);
						outcome.Result = OpResult.NotOpen;
						outcome.Detail = "bounded refresh exhausted with no .btn.btn-res rows (last title=\"" + lastTitle + "\")";
						return outcome;
					}
					frm.logtxtBox("T # " + threadIndex + " refresh " + refreshCount + "/" + maxRefresh + "  state=" + state + " title=\"" + lastTitle + "\" (no tee rows yet)");
					Thread.Sleep(refreshDelayMs);
					if (!NavigateToReservation(bookInfo2, dateCellXPath))
					{
						outcome.Result = OpResult.FalalError;
						outcome.Detail = "course picker did not open the requested reservation calendar during refresh";
						return outcome;
					}
					continue;
				}
			}

			// --- state == SlotsAvailable ---
			try
			{
				target = dateCell;
				if (!cellOpened)
				{
					WebDriverExtensions.clickLock(dateCell);
				}
				ReadOnlyCollection<IWebElement> teeButtons = ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@class='btn btn-res']"), 10);
				if (teeButtons == null || teeButtons.Count == 0)
				{
					BookingDiagnostics.Capture(drv, diagnosticsDir, "no-tee-buttons",
						"no '.btn.btn-res' rows after opening date (title=\"" + lastTitle + "\"). url=" + BookingDiagnostics.SafeUrl((IWebDriver)(object)drv));
					frm.logtxtBox("T # " + threadIndex + " no reservable rows after opening date");
					outcome.Result = OpResult.NoSpace;
					outcome.Detail = "no .btn.btn-res rows after opening date";
					return outcome;
				}

				// --- nearest desired-time selection ---
				string chosenHHmm;
				int deltaMin;
				IWebElement slot = TeeSelector.PickNearest(teeButtons, bookInfo2,
					msg => frm.logtxtBox("T # " + threadIndex + " " + msg), out chosenHHmm, out deltaMin);
				if (slot == null)
				{
					BookingDiagnostics.Capture(drv, diagnosticsDir, "no-nearest-match",
						"desired=" + outcome.DesiredTee + " but no eligible tee row parsed. rows=" + teeButtons.Count);
					frm.logtxtBox("T # " + threadIndex + " NoProperTime (rows=" + teeButtons.Count + ", desired=" + outcome.DesiredTee + ")");
					outcome.Result = OpResult.NoProperTime;
					outcome.Detail = "no eligible tee row for nearest-time selection";
					return outcome;
				}
				outcome.SlotFound = true;
				outcome.ChosenTee = chosenHHmm;
				outcome.DeltaMinutes = deltaMin;

				if (diagnosticMode)
				{
					string note = BookingDiagnostics.Capture(drv, diagnosticsDir, "slot-found-DIAGNOSTIC",
						"NEAREST SLOT for " + bookInfo2 + ": desired=" + outcome.DesiredTee + " chosen=" + chosenHHmm +
						" deltaMin=" + deltaMin + " (rows=" + teeButtons.Count + "). Final reservation NOT submitted (diagnostic mode).");
					frm.logtxtBox("T # " + threadIndex + " DIAGNOSTIC: nearest slot found (desired=" + outcome.DesiredTee +
						" chosen=" + chosenHHmm + " deltaMin=" + deltaMin + ") - stopping before submit.  evidence=" + note);
					outcome.Result = OpResult.FoundSlot;
					outcome.Detail = "diagnostic no-submit: slot located, not submitted";
					return outcome;
				}

				// --- real submission path (only in a non-diagnostic build) ---
				frm.logtxtBox("T # " + threadIndex + " submitting nearest slot " + chosenHHmm + " (deltaMin=" + deltaMin + ")");
				lastConfirmationId = null;
				lastHistoryStrictConfirmed = false;
				OpResult submitResult = tryReserve(slot, bookInfo2, chosenHHmm);
				outcome.Result = submitResult;
				outcome.Submitted = true;

				if (submitResult == OpResult.BookOneSuccess)
				{
					outcome.ConfirmationId = lastConfirmationId;
					if (string.IsNullOrEmpty(outcome.ConfirmationId))
					{
						frm.logtxtBox("T # " + threadIndex + " submit succeeded but NO server confirmation ID was captured");
						BookingDiagnostics.Capture(drv, diagnosticsDir, "no-confirmation-id",
							"submit alert indicated success but no confirmation/reservation number was parsed");
					}
					if (lastHistoryStrictConfirmed)
					{
						// Already confirmed on the history page by the strict criterion
						// (date + tee time + course + starter). Do NOT let the weaker
						// VerifyReservationHistory check downgrade that result.
						outcome.HistoryVerified = true;
						frm.logtxtBox("T # " + threadIndex + " HistoryVerified: 예약내역 엄격 기준(날짜·시간·코스·스타터) 일치로 확정 유지");
					}
					else
					{
						outcome.HistoryVerified = VerifyReservationHistory(bookInfo2, outcome.ConfirmationId, chosenHHmm);
					}
				}
				else
				{
					outcome.Detail = "submit result: " + submitResult;
				}
				return outcome;
			}
			catch (Exception ex2)
			{
				BookingDiagnostics.Capture(drv, diagnosticsDir, "book-exception",
					"course=" + bookInfo2.course + " date=" + bookInfo2.date + " ex=" + ex2);
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex2);
				outcome.Result = OpResult.Fail;
				outcome.Detail = "exception: " + ex2.Message;
				return outcome;
			}
		}

		outcome.Result = OpResult.Fail;
		outcome.Detail = "stopClicked before completion";
		return outcome;
	}

	// Recovery feature: pull a server confirmation / reservation number out of the
	// completion alert text or the completion page. Best-effort; returns null if none.
	private static string ExtractConfirmationId(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return null;
		}
		Match m = Regex.Match(s,
			"(?:예약\\s*번호|예약번호|접수\\s*번호|접수번호|reservation\\s*(?:no|number|id))\\s*[:：]?\\s*([A-Za-z0-9\\-]{4,})",
			RegexOptions.IgnoreCase);
		if (m.Success)
		{
			return m.Groups[1].Value;
		}
		Match m2 = Regex.Match(s, "\\b(\\d{6,})\\b");
		return m2.Success ? m2.Groups[1].Value : null;
	}

	// Recovery feature: confirm the just-made reservation appears in the account's
	// reservation history. A failure here (or any exception) returns false, which the
	// sequential gate treats as "condition not confirmed" -> condition 2 does not start.
	private string HistoryUrl()
	{
		return dummyTestMode
			? dummyBaseUrl + "/history.html"
			: (string.IsNullOrWhiteSpace(historyUrl) ? "https://www.sunvalley.co.kr/mypage/reservation" : historyUrl);
	}

	private bool VerifyReservationHistory(bookInfo req, string confId, string chosenHHmm)
	{
		string url = HistoryUrl();
		try
		{
			((WebDriver)drv).Navigate().GoToUrl(url);
			if (!WaitForPageContent((IWebDriver)(object)drv, PageContentTimeoutSeconds, out string page))
				frm.logtxtBox("T # " + threadIndex + " reservation-history page did not finish loading within " + PageContentTimeoutSeconds + "s.");
			string d = req.date;
			string dDash = d.Substring(0, 4) + "-" + d.Substring(4, 2) + "-" + d.Substring(6, 2);
			string dDot = d.Substring(0, 4) + "." + d.Substring(4, 2) + "." + d.Substring(6, 2);
			bool dateHit = page.Contains(d) || page.Contains(dDash) || page.Contains(dDot);
			bool idHit = !string.IsNullOrEmpty(confId) && page.Contains(confId);
			bool timeHit = !string.IsNullOrEmpty(chosenHHmm) &&
				(page.Contains(chosenHHmm) || page.Contains(chosenHHmm.Replace(":", "")));
			bool ok = idHit || (dateHit && timeHit);
			BookingDiagnostics.Capture(drv, diagnosticsDir, ok ? "history-verified" : "history-not-found",
				"url=" + url + " confId=" + (confId ?? "(none)") + " dateHit=" + dateHit + " timeHit=" + timeHit + " idHit=" + idHit);
			frm.logtxtBox("T # " + threadIndex + " reservation-history check: " + (ok ? "CONFIRMED" : "NOT FOUND") +
				" (idHit=" + idHit + " dateHit=" + dateHit + " timeHit=" + timeHit + ")");
			return ok;
		}
		catch (Exception ex)
		{
			BookingDiagnostics.Capture(drv, diagnosticsDir, "history-check-error", "url=" + url + " ex=" + ex.Message);
			frm.logtxtBox("T # " + threadIndex + " reservation-history check ERROR: " + ex.Message);
			return false;
		}
	}

	private OpResult tryReserve(IWebElement btn, bookInfo req, string chosenHHmm)
	{
#if DIAGNOSTIC_BUILD
		BookingDiagnostics.Capture(drv, diagnosticsDir, "tryReserve-blocked-DIAGNOSTIC",
			"tryReserve() reached in a DIAGNOSTIC build - reservation submission is compiled out; no click performed.");
		frm.logtxtBox("T # " + threadIndex + " DIAGNOSTIC build: tryReserve() blocked, no reservation submitted.");
		return OpResult.FoundSlot;
#else
		OpResult result = OpResult.Fail;
		try
		{
			((WebDriver)drv).ExecuteScript("arguments[0].click();", new object[1] { btn });
			frm.logtxtBox("T # " + threadIndex + " submit button clicked");
			IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='golfTimeDiv2']/div[3]/div/div[1]/button"), ConfirmationDialogTimeoutSeconds);
			if (val == null)
			{
				// Confirm UI changed / did not open. We have not clicked "확정" so a booking is
				// unlikely, but per policy re-check the history briefly before giving up.
				frm.logtxtBox("T # " + threadIndex + " 확정 버튼을 찾지 못함 (확인 UI 변경 가능) - 예약내역 재확인");
				return ConfirmViaHistory(req, chosenHHmm, "확정 버튼 없음(확인 UI 변경)");
			}
			try
			{
				WebDriverExtensions.clickLock(val);
				frm.logtxtBox("T # " + threadIndex + " reserve button clicked " + DateTime.Now.ToString("HH:mm:ss.ffffff"));
				WaitForAlert(CompletionAlertTimeoutSeconds);
				string text;
				try
				{
					text = ((WebDriver)drv).SwitchTo().Alert().Text;
					((WebDriver)drv).SwitchTo().Alert().Accept();
				}
				catch (Exception alertEx)
				{
					// No completion alert: the site may have navigated to an unsupported page
					// or the confirm UI changed. The 확정 click DID happen, so a reservation may
					// exist -> re-check the history (delayed retries) before deciding.
					frm.logtxtBox("T # " + threadIndex + " 확정 후 완료 alert 없음 (" + alertEx.GetType().Name
						+ ") - 예약내역으로 재확인");
					handleAlert();
					return ConfirmViaHistory(req, chosenHHmm, "완료 alert 없음 / 지원되지 않는 페이지");
				}

				frm.logtxtBox("T # " + threadIndex + " " + text);
				if (text.Contains("동일한 일자") || text.Contains("횟수를 초과"))
				{
					((WebDriver)drv).Navigate().Back();
					return OpResult.DuplicateFail;
				}
				if (text.Contains("다른 곳에서"))
				{
					((WebDriver)drv).Navigate().Back();
					return OpResult.DuplicateLogin;
				}
				if (text.Contains("예약이 완료"))
				{
					lastConfirmationId = ExtractConfirmationId(text);
					if (string.IsNullOrEmpty(lastConfirmationId))
					{
						try
						{
							lastConfirmationId = ExtractConfirmationId(((IWebDriver)(object)drv).PageSource);
						}
						catch (Exception)
						{
						}
					}
					frm.logtxtBox("T # " + threadIndex + " reservation complete. confirmationId=" + (lastConfirmationId ?? "(not found)"));
					((WebDriver)drv).Navigate().Back();
					return OpResult.BookOneSuccess;
				}
				// Alert present but its wording is neither a known success nor a known failure
				// (confirm-UI text changed). Do not guess from the alert - re-check the history.
				frm.logtxtBox("T # " + threadIndex + " 확인 alert 문구를 해석하지 못함 - 예약내역으로 재확인");
				return ConfirmViaHistory(req, chosenHHmm, "알 수 없는 확인 alert");
			}
			catch (Exception ex)
			{
				// The 확정 click itself failed -> reservation almost certainly not made. Keep Fail.
				frm.logtxtBox("T # " + threadIndex + " found slot and trying to book " + ex);
				handleAlert();
			}
		}
		catch (Exception ex2)
		{
			frm.logtxtBox("thread # " + threadIndex + " Reservation Buttone click, but Failed " + ex2);
			handleAlert();
		}
		return result;
#endif
	}

#if !DIAGNOSTIC_BUILD
	// Fallback final-confirmation check. Reached only when the completion alert was absent,
	// unreadable, or the confirm UI/page changed AFTER the 확정 click. Re-loads the account's
	// reservation history a few times (growing delays for server propagation) and, if a row
	// for THIS request (requested date + chosen tee time + selected starter + selected course)
	// is present on a real history page, treats the reservation as created and logs a clear
	// "예약내역으로 확인된 완료". We only get here when the site did NOT reject the click as a
	// duplicate, so a matching row present now is the one just made. If nothing matches after
	// the retries the outcome is genuinely uncertain -> Fail (never a faked success).
	private OpResult ConfirmViaHistory(bookInfo req, string chosenHHmm, string reason)
	{
		string url = HistoryUrl();
		frm.logtxtBox("T # " + threadIndex + " 완료 확인 폴백 (" + reason + ") - 예약내역 재확인 시작: " + url);
		int[] delaysMs = { 1200, 2500, 4000, 6000 };
		string lastDetail = "(no attempt)";
		for (int i = 0; i < delaysMs.Length; i++)
		{
			try
			{
				Thread.Sleep(delaysMs[i]);
				((WebDriver)drv).Navigate().GoToUrl(url);
				Thread.Sleep(600);
			}
			catch (Exception navEx)
			{
				lastDetail = "load-failed: " + navEx.Message;
				frm.logtxtBox("T # " + threadIndex + " 예약내역 로드 실패 " + (i + 1) + "/" + delaysMs.Length + ": " + navEx.Message);
				continue;
			}
			string page;
			try { page = ((IWebDriver)(object)drv).PageSource ?? ""; }
			catch (Exception) { page = ""; }

			if (ReservationHistoryMatch.Matches(page, req.date, chosenHHmm, req.starter, req.courseName, out lastDetail))
			{
				lastHistoryStrictConfirmed = true; // 날짜·시간·코스·스타터 엄격 일치
				if (string.IsNullOrEmpty(lastConfirmationId))
				{
					lastConfirmationId = ExtractConfirmationId(page);
				}
				BookingDiagnostics.Capture(drv, diagnosticsDir, "history-confirmed-completion",
					"reason=" + reason + " attempt=" + (i + 1) + "/" + delaysMs.Length + " " + lastDetail
					+ " confId=" + (lastConfirmationId ?? "(none)"));
				frm.logtxtBox("T # " + threadIndex + " 예약내역으로 확인된 완료: 요청일 " + req.date
					+ " 티타임 " + chosenHHmm + " 스타터 " + (req.starter ?? "NA")
					+ " 코스 " + (req.courseName ?? "(미상)")
					+ " confId=" + (lastConfirmationId ?? "(없음)")
					+ " (완료 alert 미확인, 폴백 재확인 " + (i + 1) + "회차)");
				return OpResult.BookOneSuccess;
			}
			frm.logtxtBox("T # " + threadIndex + " 예약내역 재확인 " + (i + 1) + "/" + delaysMs.Length
				+ ": 일치 항목 없음 (" + lastDetail + ")");
		}
		BookingDiagnostics.Capture(drv, diagnosticsDir, "history-confirm-failed",
			"reason=" + reason + " - no matching reservation row after " + delaysMs.Length + " delayed retries; " + lastDetail);
		frm.logtxtBox("T # " + threadIndex + " 예약내역에서 확인 실패 - 예약 생성 여부 불확실. 실패로 처리합니다.");
		return OpResult.Fail;
	}
#endif

	public override bool monitorOne(string date_p)
	{
		try
		{
			string attribute = ((WebDriver)drv).FindElement(By.XPath("//*[@id='" + date_p + "']//a")).GetAttribute("title");
			if (!attribute.Contains("마감") && !attribute.Contains("오픈전"))
			{
				return true;
			}
		}
		catch (Exception)
		{
			return false;
		}
		return false;
	}
}
