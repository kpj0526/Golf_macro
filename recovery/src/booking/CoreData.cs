using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using OpenCvSharp;

namespace booking;

public class CoreData
{
	public string userCode;

	public string userId;

	public string passwd;

	public string serverIp;

	public string lastTimeStamp;

	public string delay;

	public DateTime openDate;

	public string[] bookArray;

	public int threadNum = 3;

	public int lockDelay = 100;

	public int runNum;

	private int release;

	private bool useServer;

	// --- Recovery: bounded navigation/state-machine settings (App.config appSettings) ---
	public bool diagnosticMode =
#if DIAGNOSTIC_BUILD
		true;
#else
		false;
#endif

	public int maxRefresh = 40;

	public int refreshDelayMs = 1500;

	public int maxWaitSeconds = 180;

	public string diagnosticsDir = "diagnostics";

	// Recovery feature: reservation-history page for the sequential-gate confirmation.
	// Best-effort default; override via App.config key "HistoryUrl" and validate live.
	public string historyUrl = "https://www.sunvalley.co.kr/mypage/reservation";

	// Recovery feature: exactly two booking conditions, "course,yyyymmdd,desiredHHMM,starter".
	// Populated from App.config keys "Condition1" / "Condition2" (UI can overwrite them).
	public string condition1 = "";

	public string condition2 = "";

	// Local-only integration test switch.  When true, Sun Valley navigation is
	// limited to the supplied file:// dummy site and never uses a live URL.
	public bool dummyTestMode;

	public string dummyBaseUrl = "";

	public string[] clubList = new string[21]
	{
		"lakewood", "newspring", "daemyung", "sunvalley", "shinla", "vista", "jisan", "seowon", "midas", "asiana",
		"rexfield", "plaza", "seolhae", "century", "newkorea", "newseoul", "maestro", "sky72", "teecloud", "gwacheon",
		"balios"
	};

	// SECURITY: the original binary embedded 21 real third-party golf-site credential
	// pairs here. They have been removed for the recovery build. Operators supply their
	// own ID/Password through the UI (or the placeholder App.config keys) at run time.
	private (string, string)[] idList = new (string, string)[21]
	{
		("", ""), ("", ""), ("", ""), ("", ""), ("", ""), ("", ""), ("", ""),
		("", ""), ("", ""), ("", ""), ("", ""), ("", ""), ("", ""), ("", ""),
		("", ""), ("", ""), ("", ""), ("", ""), ("", ""), ("", ""), ("", "")
	};

	private int[] openDayList = new int[21]
	{
		25, 14, 28, 21, 21, 30, 21, 21, 21, 28,
		21, 21, 21, 21, 30, 30, 21, 21, 28, 7,
		30
	};

	private int[] openTimeList = new int[21]
	{
		900, 900, 900, 900, 900, 900, 900, 900, 900, 900,
		900, 900, 900, 900, 930, 1000, 830, 900, 1000, 1000,
		930
	};

	private string[][] courseList = new string[25][]
	{
		new string[1] { "NA" },
		new string[4] { "록키", "올림푸스", "알프스", "몽블랑" },
		new string[3] { "비발디파크 EAST", "비발디파크 WEST", "델피노" },
		new string[4] { "설악썬밸리", "썬밸리CC", "동원썬밸리", "여주썬밸리" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" }
	};

	private List<string[]> starterList = new List<string[]>
	{
		new string[3] { "산길-숲길", "물길-꽃길", "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[4] { "설악", "썬", "밸리", "NA" },
		new string[4] { "천마OUT", "천마IN", "화랑OUT", "화랑IN" },
		new string[5] { "NA", "Monti", "Bella", "Vista", "Lago" },
		new string[4] { "NA", "동코스", "서코스", "남코스" },
		new string[4] { "NA", "동코스", "서코스", "남코스" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[3] { "NA", "OUT", "IN" },
		new string[5] { "NA", "예술OUT", "예술IN", "문화OUT", "문화IN" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[3] { "NA", "OUT", "IN" },
		new string[3] { "NA", "OUT", "IN" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" },
		new string[1] { "NA" }
	};

	public CoreData(int rel)
	{
		release = rel;
		threadNum = 1;
		lockDelay = 0;
	}

	public void buildMsgList()
	{
	}

	public void getIds(ref (string, string)[] ids, ref int[] opendays, ref int[] opentimes, ref string[][] courses, ref List<string[]> starters)
	{
		ids = idList;
		opendays = openDayList;
		opentimes = openTimeList;
		courses = courseList;
		starters = starterList;
	}

	public int getIndexOfStarter(string starter)
	{
		return Array.IndexOf(starterList[release], starter);
	}

	public bool checkFileList()
	{
		return true;
	}

	public void setIdPwd(int rel, bool pwd = true, bool forceDefaultId = false)
	{
		release = rel;
		if ((userId == "") | forceDefaultId)
		{
			userId = idList[rel].Item1;
		}
		if (pwd || passwd == "")
		{
			passwd = idList[rel].Item2;
		}
	}

	public void setIdPwd(string id, string pwd)
	{
		userId = id;
		passwd = pwd;
	}

	public DateTime getOpenDate(string reqDate)
	{
		DateTime dateTime = new DateTime(int.Parse(reqDate.Substring(0, 4)), int.Parse(reqDate.Substring(4, 2)), int.Parse(reqDate.Substring(6, 2)), openTimeList[release] / 100, openTimeList[release] % 100, 0);
		if (openDayList[release] < 30)
		{
			return dateTime.AddDays(-openDayList[release]);
		}
		int num = DateTime.DaysInMonth(dateTime.Year, dateTime.Month - 1);
		if (dateTime.Day > num)
		{
			return dateTime.AddDays(-dateTime.Day + 1);
		}
		return dateTime.AddMonths(-1);
	}

	public void setFonts(Control.ControlCollection ctrs, int fontSize = 14)
	{
		foreach (Control ctr in ctrs)
		{
			ctr.Font = new Font("Consolas", fontSize);
		}
	}

	public void setFont(Control ctr, int fontSize = 14)
	{
		ctr.Font = new Font("Consolas", fontSize);
	}

	public void configSave(ListBox.ObjectCollection list)
	{
		string[] value = list.Cast<string>().ToArray();
		string[] two = value.Where(x => !string.IsNullOrWhiteSpace(x)).Take(2).ToArray();
		configSaveConditions(
			(two.Length > 0) ? two[0] : "",
			(two.Length > 1) ? two[1] : "");
	}

	// True when a saved account was loaded from the DPAPI store this run.
	public bool credentialLoaded;

	// Two local account slots (계정 1 / 계정 2) + the per-reservation slot selection.
	// Populated from the DPAPI store on load; never hard-coded, never logged.
	internal CredentialStore.AccountData accounts = new CredentialStore.AccountData();

	// Copy the chosen slot's ID/password into userId/passwd (used before each login).
	public void applyAccount(int slot)
	{
		slot = accounts.ClampSlot(slot);
		userId = accounts.Id[slot] ?? "";
		passwd = accounts.Pw[slot] ?? "";
	}

	// Recovery feature: persist ONLY the two booking conditions + user code.
	// Credentials are NEVER written to the config file (password lives in the DPAPI store).
	public void configSaveConditions(string c1, string c2)
	{
		Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
		SetSetting(configuration, "Condition1", c1 ?? "");
		SetSetting(configuration, "Condition2", c2 ?? "");
		SetSetting(configuration, "UserCode", userCode ?? "");
		configuration.Save();
		condition1 = c1 ?? "";
		condition2 = c2 ?? "";
	}

	public void configLoad()
	{
		Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
		string bl = ReadSetting(configuration, "BookList");
#if DIAGNOSTIC_BUILD
		if (string.IsNullOrEmpty(bl))
		{
			bl = ReadSetting(configuration, "BookLIst"); // original package spelling
		}
#endif
		if (!string.IsNullOrEmpty(bl))
		{
			bookArray = bl.Split(";");
		}
		userCode = ReadSetting(configuration, "UserCode") ?? "";
		userId = "";
		passwd = "";
		credentialLoaded = false;

		// Both builds: load the two saved account slots + per-reservation slot selection from
		// the DPAPI store. Nothing is hard-coded; nothing is read from this build's own config;
		// nothing is imported from the original package. If no account was saved yet, the slot
		// fields start empty and the user types + 저장s them.
		if (CredentialStore.TryLoad(out CredentialStore.AccountData ad))
		{
			accounts = ad;
			credentialLoaded = !string.IsNullOrEmpty(ad.Id[0]) || !string.IsNullOrEmpty(ad.Pw[0])
				|| !string.IsNullOrEmpty(ad.Id[1]) || !string.IsNullOrEmpty(ad.Pw[1]);
		}
		else
		{
			accounts = new CredentialStore.AccountData();
		}
		applyAccount(accounts.Res1Slot);

		LoadRecoverySettings(configuration);
	}

	// Persist both account slots + the per-reservation slot selection to the DPAPI store
	// (called from the 저장 button and from "계정 저장" on start). Nothing is logged.
	internal void saveAccounts(CredentialStore.AccountData d)
	{
		accounts = d;
		CredentialStore.Save(d);
		applyAccount(d.Res1Slot);
	}

	// Persist the currently typed ID + password pair into slot 0 (back-compat path).
	public void saveCredentials(string id, string password)
	{
		accounts.Id[0] = id ?? "";
		accounts.Pw[0] = password ?? "";
		CredentialStore.Save(accounts);
		userId = id ?? "";
		passwd = password ?? "";
	}

	// Save / clear the DPAPI-encrypted store.
	public void savePassword(string password)
	{
		CredentialStore.SavePassword(password);
		passwd = password ?? "";
	}

	public void clearPassword()
	{
		CredentialStore.Clear();
	}

	private void LoadRecoverySettings(Configuration configuration)
	{
		string s = ReadSetting(configuration, "DiagnosticMode");
		if (bool.TryParse(s, out var dm))
		{
			diagnosticMode = dm;
		}
#if DIAGNOSTIC_BUILD
		diagnosticMode = true; // diagnostic build: never let config re-enable submission
#endif
		if (int.TryParse(ReadSetting(configuration, "MaxRefresh"), out var mr) && mr > 0)
		{
			maxRefresh = mr;
		}
		if (int.TryParse(ReadSetting(configuration, "RefreshDelayMs"), out var rd) && rd >= 0)
		{
			refreshDelayMs = rd;
		}
		if (int.TryParse(ReadSetting(configuration, "MaxWaitSeconds"), out var mw) && mw > 0)
		{
			maxWaitSeconds = mw;
		}
		string dir = ReadSetting(configuration, "DiagnosticsDir");
		if (!string.IsNullOrWhiteSpace(dir))
		{
			diagnosticsDir = dir;
		}
		string hu = ReadSetting(configuration, "HistoryUrl");
		if (!string.IsNullOrWhiteSpace(hu))
		{
			 historyUrl = hu;
		}
		if (bool.TryParse(ReadSetting(configuration, "DummyTestMode"), out var dtm))
		{
			dummyTestMode = dtm;
		}
		string dbu = ReadSetting(configuration, "DummyBaseUrl");
		if (!string.IsNullOrWhiteSpace(dbu))
		{
			dummyBaseUrl = dbu.TrimEnd('/');
		}
		condition1 = ReadSetting(configuration, "Condition1") ?? "";
		#if DIAGNOSTIC_BUILD
		if (string.IsNullOrWhiteSpace(condition1) && bookArray != null && bookArray.Length > 0)
		{
			condition1 = bookArray[0] ?? "";
		}
		#endif
		condition2 = ReadSetting(configuration, "Condition2") ?? "";
	}

	private static void SetSetting(Configuration configuration, string key, string val)
	{
		try
		{
			if (configuration.AppSettings.Settings[key] == null)
			{
				configuration.AppSettings.Settings.Add(key, val);
			}
			else
			{
				configuration.AppSettings.Settings[key].Value = val;
			}
		}
		catch (Exception)
		{
		}
	}

	private static string ReadSetting(Configuration configuration, string key)
	{
		try
		{
			return configuration.AppSettings.Settings[key]?.Value;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public void testServer(mqttClient mq, int loopCnt, Form1 frm)
	{
		string filePath = "testCaptha.png";
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		for (int i = 0; i < loopCnt; i++)
		{
			frm.logtxtBox("Test AI Server  before sending " + DateTime.Now.Millisecond + "ms");
			if (!sendCaptcha(mq, filePath, frm))
			{
				frm.logtxtBox("Sending Captcha Error");
			}
			while (mq.waitAck)
			{
				Thread.Sleep(5);
			}
			frm.logtxtBox("Test AI Server  Got response " + DateTime.Now.Millisecond + "ms");
		}
		stopwatch.Stop();
		string receivedMessage = mq.ReceivedMessage;
		frm.logtxtBox(" Test AI Server " + receivedMessage + " time " + stopwatch.ElapsedMilliseconds + "ms");
	}

	public bool sendCaptcha(mqttClient mq, string filePath, Form1 frm)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (!File.Exists(filePath))
		{
			frm.logtxtBox("\n \n No File " + filePath);
			return false;
		}
		if (mq != null)
		{
			Mat obj = Cv2.ImRead(filePath, (ImreadModes)1);
			Mat val = new Mat();
			Mat val2 = new Mat();
			Cv2.CvtColor((InputArray)(obj), (OutputArray)(val), (ColorConversionCodes)6, 0);
			Cv2.Resize((InputArray)(val), (OutputArray)(val2), new OpenCvSharp.Size(200, 50), 0.0, 0.0, (InterpolationFlags)1);
			byte[] file = val2.ToBytes(".png", (int[])null);
			mq.publish(file);
			return true;
		}
		frm.logtxtBox("Sever is not connected");
		Thread.Sleep(10000);
		return false;
	}

	public bool sendReqSms(mqttClient mq, string phoneNum, Form1 frm)
	{
		if (mq != null)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(phoneNum);
			mq.publish(bytes);
			return true;
		}
		frm.logtxtBox("Sever is not connected");
		Thread.Sleep(10000);
		return false;
	}
}
