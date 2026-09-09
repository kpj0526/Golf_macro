using System;
using System.IO;
using System.Text;
using OpenQA.Selenium;

namespace booking;

/// <summary>
/// Recovery add-on. Classifies the current booking page state and captures
/// screenshot + HTML + a structured note when a run cannot make progress,
/// so a stuck "refresh" loop produces actionable evidence instead of spinning silently.
/// </summary>
internal enum BookingPageState
{
	SlotsAvailable,   // date cell reports 잔여팀 -> proceed
	Closed,           // date cell reports 마감 -> nothing to do, move on
	NotOpenYet,       // date cell reports 오픈전 -> legitimate wait, bounded refresh
	DateCellMissing,  // expected date cell not found on the page
	RedirectedAway,   // browser is not on the reservation page (login / mypage / etc.)
	Unknown           // cell found but title text is unrecognised
}

internal static class BookingDiagnostics
{
	public static string ExpectedUrlFragment = "/reservation/golf";

	private static readonly string[] AwayFragments =
	{
		"/member/login", "/login", "/mypage", "/my-page", "myPage", "/member/", "/index"
	};

	public static BookingPageState Classify(IWebDriver drv, IWebElement dateCell, out string title, out string url)
	{
		title = null;
		url = SafeUrl(drv);

		if (!string.IsNullOrEmpty(url))
		{
			// Compare on the PATH only. The login URL is ".../member/login?returnURL=/reservation/golf",
			// whose query string contains the reservation fragment - matching the whole URL would
			// mis-classify the login bounce as "on the reservation page".
			string path = url;
			int q = path.IndexOf('?');
			if (q >= 0)
			{
				path = path.Substring(0, q);
			}
			int h = path.IndexOf('#');
			if (h >= 0)
			{
				path = path.Substring(0, h);
			}

			foreach (string frag in AwayFragments)
			{
				if (path.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return BookingPageState.RedirectedAway;
				}
			}
			if (path.IndexOf(ExpectedUrlFragment, StringComparison.OrdinalIgnoreCase) < 0)
			{
				// not on the reservation page and not a recognised "away" page either
				return BookingPageState.RedirectedAway;
			}
		}

		if (dateCell == null)
			return BookingPageState.DateCellMissing;

		try { title = dateCell.GetAttribute("title") ?? string.Empty; }
		catch (Exception) { return BookingPageState.DateCellMissing; }

		if (title.Contains("잔여팀")) return BookingPageState.SlotsAvailable;
		if (title.Contains("마감")) return BookingPageState.Closed;
		if (title.Contains("오픈전") || title.Contains("오픈 전")) return BookingPageState.NotOpenYet;
		return BookingPageState.Unknown;
	}

	public static string SafeUrl(IWebDriver drv)
	{
		try { return drv.Url; } catch (Exception) { return null; }
	}

	/// <summary>
	/// False-preopening fix (pure, unit-tested): the date-cell title is advisory only.
	/// When the title claims pre-opening / is unrecognised but a real reserve-row probe
	/// found rows, the date IS open. Any other state passes through unchanged.
	/// </summary>
	public static BookingPageState ResolveWithRowProbe(BookingPageState titleState, int probedRowCount)
	{
		if ((titleState == BookingPageState.NotOpenYet || titleState == BookingPageState.Unknown)
			&& probedRowCount > 0)
		{
			return BookingPageState.SlotsAvailable;
		}
		return titleState;
	}

	/// <summary>Capture screenshot + page HTML + a note. Returns the note path (or null on failure).</summary>
	public static string Capture(IWebDriver drv, string dir, string tag, string details)
	{
		try
		{
			Directory.CreateDirectory(dir);
			string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
			string baseName = Path.Combine(dir, stamp + "_" + Sanitize(tag));

			try
			{
				Screenshot shot = ((ITakesScreenshot)drv).GetScreenshot();
				shot.SaveAsFile(baseName + ".png");
			}
			catch (Exception) { }

			try { File.WriteAllText(baseName + ".html", drv.PageSource ?? string.Empty, Encoding.UTF8); }
			catch (Exception) { }

			var sb = new StringBuilder();
			sb.AppendLine("time    : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
			sb.AppendLine("tag     : " + tag);
			sb.AppendLine("url     : " + SafeUrl(drv));
			sb.AppendLine("expected: contains \"" + ExpectedUrlFragment + "\"");
			sb.AppendLine("details : " + details);
			File.WriteAllText(baseName + ".txt", sb.ToString(), Encoding.UTF8);
			return baseName + ".txt";
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static string Sanitize(string s)
	{
		if (string.IsNullOrEmpty(s)) return "capture";
		foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
		return s.Replace(' ', '_');
	}
}
