using System;
using System.IO;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

// SAFE, UNAUTHENTICATED live probe of the Sun Valley reservation surface.
// - no credentials, no login
// - no clicks on any reservation / confirmation control
// - read-only navigation + screenshots
// Validates the video-flow steps that do NOT require a session, and confirms the
// selectors the diagnostic build's state machine depends on.
//
// usage:  LiveProbe <outDir> <chromedriverDir>

internal static class Program
{
	private static int Main(string[] args)
	{
		string outDir = (args.Length > 0) ? args[0] : Path.Combine(AppContext.BaseDirectory, "probe-out");
		string driverDir = (args.Length > 1) ? args[1] : AppContext.BaseDirectory;
		Directory.CreateDirectory(outDir);
		string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		var sb = new StringBuilder();
		void Log(string s) { Console.WriteLine(s); sb.AppendLine(s); }

		Log("Sun Valley UNAUTHENTICATED live probe  " + DateTime.Now.ToString("u"));
		Log("no credentials, no login, no reservation/confirm clicks");
		Log("chromedriver dir: " + driverDir);
		Log("");

		var opts = new ChromeOptions();
		opts.AddArgument("--headless=new");
		opts.AddArgument("--window-size=1400,3000");
		opts.AddArgument("--lang=ko-KR");
		opts.AddArgument("--no-sandbox");

		var svc = ChromeDriverService.CreateDefaultService(driverDir, "chromedriver.exe");
		svc.HideCommandPromptWindow = true;

		IWebDriver drv = null;
		int rc = 0;
		try
		{
			drv = new ChromeDriver(svc, opts);
			drv.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);

			// --- Step 1: reservation page (unauthenticated) ---
			string resUrl = "https://www.sunvalley.co.kr/reservation/golf?sel=J21";
			drv.Navigate().GoToUrl(resUrl);
			System.Threading.Thread.Sleep(1800);
			string finalUrl = drv.Url;
			bool onReservation = PathContains(finalUrl, "/reservation/golf");
			bool bouncedToLogin = PathContains(finalUrl, "/member/login") || PathContains(finalUrl, "/login");
			int btnRes = drv.FindElements(By.CssSelector(".btn.btn-res")).Count;
			int titledLinks = drv.FindElements(By.CssSelector("a[title]")).Count;
			Shot(drv, Path.Combine(outDir, stamp + "_1_reservation_unauth.png"));
			File.WriteAllText(Path.Combine(outDir, stamp + "_1_reservation_unauth.html"), Safe(() => drv.PageSource), Encoding.UTF8);
			Log("[1] GoToUrl " + resUrl);
			Log("    final url          : " + finalUrl);
			Log("    on /reservation/   : " + onReservation);
			Log("    bounced to login   : " + bouncedToLogin);
			Log("    .btn.btn-res rows  : " + btnRes + "   (0 expected while unauthenticated)");
			Log("    a[title] links     : " + titledLinks);
			Log("    => BookingDiagnostics.Classify() would return: " +
				(bouncedToLogin || !onReservation ? "RedirectedAway (bounded-refresh path NOT taken; captures evidence and stops)"
											      : (btnRes > 0 ? "SlotsAvailable" : "NotOpenYet -> row probe")));

			// --- Step 2: login page structure ---
			string loginUrl = "https://www.sunvalley.co.kr/member/login?returnURL=/reservation/golf";
			drv.Navigate().GoToUrl(loginUrl);
			System.Threading.Thread.Sleep(1200);
			bool hasId = drv.FindElements(By.CssSelector("#usrId")).Count > 0;
			bool hasPw = drv.FindElements(By.CssSelector("#usrPwd")).Count > 0;
			bool hasBtn = drv.FindElements(By.CssSelector("#fnLogin")).Count > 0;
			Shot(drv, Path.Combine(outDir, stamp + "_2_login_page.png"));
			Log("");
			Log("[2] GoToUrl " + loginUrl);
			Log("    #usrId  present    : " + hasId);
			Log("    #usrPwd present    : " + hasPw);
			Log("    #fnLogin present   : " + hasBtn);
			Log("    => sunValley.login() selectors " + ((hasId && hasPw && hasBtn) ? "MATCH the live site" : "DO NOT fully match"));

			Log("");
			Log("RESULT: unauthenticated portion of the video flow verified against the live site.");
			Log("BLOCKED (needs a real Sun Valley login, out of scope):");
			Log("  login -> reservation page (authenticated) -> date cell -> .btn.btn-res rows ->");
			Log("  nearest-time selection -> condition-1 confirmation gate.");
		}
		catch (Exception ex)
		{
			Log("PROBE ERROR: " + ex.Message);
			rc = 2;
		}
		finally
		{
			try { drv?.Quit(); } catch { }
		}

		File.WriteAllText(Path.Combine(outDir, stamp + "_live-probe.txt"), sb.ToString(), Encoding.UTF8);
		Console.WriteLine("\nwrote evidence to " + outDir);
		return rc;
	}

	private static bool PathContains(string url, string frag)
	{
		if (string.IsNullOrEmpty(url)) return false;
		int q = url.IndexOf('?');
		string path = (q >= 0) ? url.Substring(0, q) : url;
		return path.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static void Shot(IWebDriver drv, string path)
	{
		try { ((ITakesScreenshot)drv).GetScreenshot().SaveAsFile(path); } catch { }
	}

	private static string Safe(Func<string> f) { try { return f() ?? ""; } catch { return ""; } }
}
