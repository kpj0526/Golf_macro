#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace booking;

internal abstract class club
{
	public ChromeDriver drv;

	public int clubIndex;

	public string id;

	public string pwd;

	public int threadIndex;

	public string date;

	public int startTime;

	public int endTime;

	public int course;

	private int bookOpenHour = 900;

	public Form1 frm;

	public List<bookInfo> bookReqs;

	public int teeInterval;

	public dayPool workPool;

	public int[] indexTeeTable;

	public int teeIndex = -1;

	public int increment = 1;

	public int searchStartTime;

	public mqttClient mq;

	public int managerId = -1;

	public Dictionary<string, int> fails;

	// --- Recovery: bounded navigation/state-machine knobs (populated from CoreData/App.config) ---
	public bool diagnosticMode;

	public int maxRefresh = 40;

	public int refreshDelayMs = 1500;

	public TimeSpan maxWait = TimeSpan.FromSeconds(180.0);

	public string diagnosticsDir = "diagnostics";

	// Recovery feature: account reservation-history page, used by the sequential gate to
	// confirm condition 1 before condition 2 starts. Set from CoreData/App.config.
	public string historyUrl = "";

	public bool dummyTestMode;

	public string dummyBaseUrl = "";

	public abstract bool login(Form1 frm);

	/// <summary>
	/// Prepares the browser session for reservation 2.  The same account reuses its
	/// authenticated session; a different account must first leave that session.
	/// </summary>
	public virtual bool prepareReservation2Session(Form1 frm, bool sameAccount)
	{
		return sameAccount;
	}

	/// <summary>
	/// Recovery feature: book one explicit request and report a rich outcome
	/// (nearest-time selection + submit + confirmation-id + history verification).
	/// Used by the sequential two-condition orchestrator. Only sunValley implements it.
	/// </summary>
	public virtual BookOutcome bookRequest(bookInfo req, bool deferHistoryVerification = false)
	{
		return new BookOutcome
		{
			Result = OpResult.Fail,
			Detail = "bookRequest is not implemented for " + GetType().Name
		};
	}

	/// <summary>
	/// Completes a deliberately deferred post-submit verification.  The shared-session
	/// Sun Valley path uses this after reservation 2 so an already-accepted reservation 1
	/// does not lose the next tee time while its history page is loading.
	/// </summary>
	public virtual void completeDeferredVerification(BookOutcome outcome, bookInfo req)
	{
	}

	/// <summary>
	/// Optionally load reservation 2 into an idle tab before the opening rush.  This is
	/// only used for the same-account sequential path; no reservation is submitted here.
	/// </summary>
	public virtual bool preloadReservationPage(bookInfo req)
	{
		return false;
	}

	public virtual void logout(Form1 frm)
	{
	}

	public virtual void setValues(ChromeDriver drv_p, int threadIndex_p, CoreData cd, List<bookInfo> bookReqs_p, ref dayPool _workPool)
	{
		id = cd.userId;
		pwd = cd.passwd;
		diagnosticMode = cd.diagnosticMode;
		maxRefresh = cd.maxRefresh;
		refreshDelayMs = cd.refreshDelayMs;
		maxWait = TimeSpan.FromSeconds(cd.maxWaitSeconds);
		diagnosticsDir = cd.diagnosticsDir;
		historyUrl = cd.historyUrl;
		dummyTestMode = cd.dummyTestMode;
		dummyBaseUrl = cd.dummyBaseUrl;
		drv = drv_p;
		clubIndex = _workPool.clubIndex;
		threadIndex = threadIndex_p;
		bookReqs = bookReqs_p;
		Trace.WriteLine("Constructor : " + threadIndex + " taskId " + Task.CurrentId + " bookking count " + bookReqs.Count);
		workPool = _workPool;
		indexTeeTable = new int[bookReqs.Count];
		Trace.WriteLine("setValues Done");
	}

	public abstract IWebElement compare(IWebElement row, bookInfo req);

	public abstract OpResult bookOne();

	public abstract bool monitorOne(string date);

	public int openTimeCheck()
	{
		int num = int.Parse(DateTime.Now.ToString("HHmm"));
		return bookOpenHour - num;
	}

	public void sleepLongToOpen(int openTime = 900)
	{
		frm.logtxtBox("Long Sleep");
		string text;
		while (true)
		{
			text = DateTime.Now.ToString("HHmm");
			int num = int.Parse(text) - openTime;
			if (num > -10 && num < 30)
			{
				break;
			}
			frm.logtxtBox("sleepLongToOpen " + text + " " + openTime + " " + num);
			Thread.Sleep(30000);
		}
		frm.logtxtBox("expired " + text);
	}

	public void sleepShortToOpen(DateTime opentime, int marginMilli = 0)
	{
		frm.logtxtBox("Sleep to open " + opentime);
		opentime = opentime.Subtract(TimeSpan.FromMilliseconds(marginMilli));
		DateTime now;
		while ((now = DateTime.Now) < opentime)
		{
			Thread.Sleep(1);
		}
		frm.logtxtBox("expired " + now.Millisecond);
	}

	public void sleepToOpen(double startMilli = 3.01, double endMilli = 59.99)
	{
		frm.logtxtBox("Sleep to 00 second");
		string text;
		while (true)
		{
			text = DateTime.Now.ToString("ss.ffff");
			float num = float.Parse(text);
			if (!((double)num < endMilli) || !((double)num > startMilli))
			{
				break;
			}
			Thread.Sleep(1);
		}
		frm.logtxtBox("expired " + text);
	}

	public bool GetPosDate(string date, ref int table, ref int row, ref int col)
	{
		int day = int.Parse(date.Substring(6, 2));
		int num = int.Parse(date.Substring(4, 2));
		DateTime dt = new DateTime(int.Parse(date.Substring(0, 4)), num, day);
		row = GetWeekOfMonth(dt);
		col = (int)(dt.DayOfWeek - 0 + 1);
		table = ((num == DateTime.Today.Month) ? 1 : 2);
		if (num - DateTime.Today.Month == 2)
		{
			return true;
		}
		return false;
	}

	private static int GetWeekOfMonth(DateTime dt)
	{
		DateTime dateTime = dt;
		int num = (dateTime.Day - 1) % 7;
		int dayOfWeek = (int)dateTime.DayOfWeek;
		double num2 = Math.Ceiling((double)dateTime.Day / 7.0);
		if (num > dayOfWeek)
		{
			num2++;
		}
		return Convert.ToInt32(num2);
	}

	public int checkTime(string str, int startTime, int endTime)
	{
		if (!int.TryParse(str, out var result))
		{
			return -1;
		}
		if (result < startTime)
		{
			return 1;
		}
		if (result > endTime)
		{
			return 2;
		}
		return 0;
	}

	public IWebElement find(ReadOnlyCollection<IWebElement> teeList, bookInfo req)
	{
		IWebElement val = null;
		frm.logtxtBox("T # " + threadIndex + " tees count " + teeList.Count + " index " + teeIndex);
		int count = teeList.Count;
		if (teeIndex < 0)
		{
			teeIndex = ((count > 30) ? (count / 3) : (count / 2));
		}
		else if (teeIndex >= count)
		{
			teeIndex = count - 1;
		}
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		searchStartTime = 0;
		for (int i = 0; i < 2; i++)
		{
			do
			{
				if (dictionary.ContainsKey(teeIndex) || teeIndex < 0 || teeIndex >= teeList.Count)
				{
					frm.logtxtBox("Not found in a way");
					break;
				}
				dictionary.Add(teeIndex, 0);
				IWebElement row = teeList[teeIndex];
				val = compare(row, req);
			}
			while (val == null);
			if (val != null || searchStartTime < req.startTime || searchStartTime > req.endTime)
			{
				break;
			}
			frm.logtxtBox("reverse searching");
			teeIndex = dictionary.ElementAt(0).Key;
			teeIndex--;
			increment = -1;
		}
		return val;
	}
}
