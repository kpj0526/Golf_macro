#define TRACE
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;

namespace booking;

public class Form1 : Form
{
	private List<string> monitorDates = new List<string>();

	private DateTime expireDate = new DateTime(2026, 6, 30, 0, 0, 0);

	private (string, string)[] idDefault = new(string, string)[10];

	private int[] opendays;

	private int[] opentimes;

	private string[][] courses;

	private List<string[]> starters;

	private int managerId = -1;

	// Recovery: original buttons kept; the dynamic 추가/삭제 pair is dropped (index 3 = 저장).
	private string[] btnNames = new string[6] { "Start", "Stop", "Setting", "저장", "", "" };

	// 예약1: 희망 시(h)/분(m)   |   예약2: 희망 시/분
	private string[] timeNames = new string[4] { "s1Hour", "s1Min", "s2Hour", "s2Min" };

	private bool headless;

	public bool testMode;

	private FormKakaoLogin loginFrm;

	public CoreData coreData;

	private string bookDate;

	private int startTime = 800;

	private int endTime = 1230;

	private bool monitor;

	private string clubStr;

	private ListBox clubLb;

	private ListBox courseLb;

	private ListBox starterLb;

	private RichTextBox tbLog;

	private DateTimePicker dt;

	// ---- Recovery: 예약 2 controls, cloned from the 예약 1 controls (same style) ----
	private ListBox courseLb2;

	private ListBox starterLb2;

	private DateTimePicker dt2;

	private CheckBox chkR2;   // "예약 2 사용", default OFF

	// 희망 시각의 UI 기본값 (nupArr 초기화에만 사용; 판정은 항상 nupArr에서 읽음)
	private int start1 = 900;

	// 시작-시간 범위 검증 결과 (통합 테스트에서 확인). null = 통과.
	internal string lastStartError;

	// 통합 테스트에서 MessageBox 억제.
	internal bool suppressStartDialogs;

	private List<bookInfo> bookList;

	private dayPool workPool;

	private TextBox idTb;    // 계정 1 ID

	private TextBox pwdTb;   // 계정 1 PW

	private TextBox acct2IdTb;   // 계정 2 ID
	private TextBox acct2PwTb;   // 계정 2 PW

	private ComboBox acctCbo1;   // 예약 1 계정 선택 (계정 1 / 계정 2)
	private ComboBox acctCbo2;   // 예약 2 계정 선택 (계정 1 / 계정 2)

	private CheckBox chkSaveCred;   // "계정 저장" (DPAPI). default ON.

	private Task<bool>[] ts;

	private CancellationTokenSource[] cts;

	private ChromeDriver[] driver;

	public bool stopClicked;

	private int releaseVersion = -1;

	private bool expire = true;

	// [0..1] 예약1 희망 h,m     [2..3] 예약2 희망 h,m
	private NumericUpDown[] nupArr = new NumericUpDown[4];

	private bool gotStart = true;

	private Tuple<string, string> idPwdStartup;

	private bool noEventHandler;

	private IContainer components;

	private readonly bool qaAutoStart;

	public Form1(bool qaAutoStart = false)
	{
		this.qaAutoStart = qaAutoStart;
		InitializeComponent();
		setDefine();
		coreData = new CoreData(releaseVersion);
		coreData.getIds(ref idDefault, ref opendays, ref opentimes, ref courses, ref starters);
		setDefine();
		if (idPwdStartup != null)
		{
			idDefault[releaseVersion] = (idPwdStartup.Item1, idPwdStartup.Item2);
		}
		_ = releaseVersion;
		coreData.threadNum = 1;
		bookList = new List<bookInfo>();
		coreData.lockDelay = 0;
		Assembly.Load("System.Configuration.ConfigurationManager");
		Trace.Listeners.Add(new TextWriterTraceListener("golflog.txt"));
		Trace.AutoFlush = true;
		Trace.WriteLine("Program Start " + DateTime.Now.ToString() + " " + (expire ? ((object)expireDate) : "No Expire"));
	}

	private void setDefine()
	{
		releaseVersion = 3;
		idDefault[3] = ("", ""); // no hard-coded login: ID/PW come from the saved DPAPI pair or the user
		expire = false;
	}

	private club initClass(ChromeDriver drv, int threadIndex)
	{
		club club2 = null;
		Trace.WriteLine("initClass");
		if (clubStr == "lakewood")
		{
			club2 = new lakewood(drv, coreData.userId, coreData.passwd, bookDate, startTime, endTime, -1);
		}
		else if (clubStr == "newspring")
		{
			club2 = new newSpring(managerId);
		}
		else if (clubStr == "daemyung")
		{
			club2 = new daeMyung(managerId);
		}
		else if (clubStr == "sunvalley")
		{
			club2 = new sunValley();
		}
		else if (clubStr == "shinla")
		{
			club2 = new shinla();
		}
		else if (clubStr == "vista")
		{
			club2 = new vista();
		}
		else if (clubStr == "jisan")
		{
			club2 = new jisan();
		}
		else if (clubStr == "seowon")
		{
			club2 = new seowon();
		}
		else if (clubStr == "midas")
		{
			club2 = new midas();
		}
		else if (clubStr == "asiana")
		{
			club2 = new asiana();
		}
		else if (clubStr == "rexfield")
		{
			club2 = new rexfield();
		}
		else if (clubStr == "plaza")
		{
			club2 = new plaza(managerId);
		}
		else if (clubStr == "seolhae")
		{
			club2 = new seolhae(managerId);
		}
		else if (clubStr == "century")
		{
			club2 = new century(managerId);
		}
		else if (clubStr == "newkorea")
		{
			club2 = new newKorea(managerId);
		}
		else if (clubStr == "newseoul")
		{
			club2 = new newSeoul(managerId);
		}
		else if (clubStr == "maestro")
		{
			club2 = new maestro(managerId);
		}
		else if (clubStr == "sky72")
		{
			club2 = new sky72(managerId);
		}
		else if (clubStr == "teecloud")
		{
			club2 = new teecloud(managerId);
		}
		else if (clubStr == "gwacheon")
		{
			club2 = new gwacheon(managerId);
		}
		else if (clubStr == "balios")
		{
			club2 = new balios(managerId);
		}
		Trace.WriteLine("Init2");
		club2.setValues(drv, threadIndex, coreData, bookList, ref workPool);
		return club2;
	}

	private void Form1_Load(object sender, EventArgs e)
	{
		if (DateTime.Today > expireDate && expire)
		{
			MessageBox.Show("This version is expired. Please contact AICafe!!");
			return;
		}
		coreData.configLoad();
		initTable();
		// Both builds: the two account slots + per-reservation account selection are editable
		// at startup and pre-filled from the DPAPI store (empty on first run). No forced/legacy
		// credential loading, no locked fields.
		LoadAccountsToUi();
		chkSaveCred.Checked = coreData.credentialLoaded
			|| (string.IsNullOrEmpty(idTb.Text) && string.IsNullOrEmpty(pwdTb.Text));
		if (coreData.credentialLoaded)
		{
			logtxtBox("저장된 계정 정보를 불러왔습니다.");
		}
		Text = "2개 예약 설정 — Golf Catch";
		if (qaAutoStart)
		{
			BeginInvoke(new Action(RunQaAutoStart));
		}
	}

	private void RunQaAutoStart()
	{
#if DIAGNOSTIC_BUILD
		if (!coreData.diagnosticMode)
		{
			Trace.WriteLine("QA AUTO-START REFUSED: diagnostic mode is not active.");
			logtxtBox("QA AUTO-START REFUSED: diagnostic mode is not active.");
			return;
		}
		headless = true;
		if (string.IsNullOrWhiteSpace(pwdTb.Text))
		{
			Trace.WriteLine("QA AUTO-START BLOCKED: no persisted password is available.");
			logtxtBox("QA AUTO-START BLOCKED: no persisted password is available.");
			return;
		}
		if (string.IsNullOrWhiteSpace(coreData.condition1))
		{
			Trace.WriteLine("QA AUTO-START BLOCKED: Reservation 1 condition is not configured.");
			logtxtBox("QA AUTO-START BLOCKED: Reservation 1 condition is not configured.");
			return;
		}
		Trace.WriteLine("QA AUTO-START: diagnostic + headless; credential value is not logged.");
		logtxtBox("QA AUTO-START: diagnostic + headless; credential value is not logged.");
		setUIStart(qaNoCredentialWrite: true);
#else
		logtxtBox("QA AUTO-START REFUSED: this is not a DIAGNOSTIC_BUILD.");
#endif
	}

	private void ApplyDefaultIdPwd(bool applyPassword, bool forceDefaultId)
	{
		if (releaseVersion < 0 || releaseVersion >= idDefault.Length)
		{
			coreData.setIdPwd(releaseVersion, applyPassword, forceDefaultId);
			return;
		}
		(string, string) tuple = idDefault[releaseVersion];
		if ((string.IsNullOrEmpty(coreData.userId) | forceDefaultId) && !string.IsNullOrEmpty(tuple.Item1))
		{
			coreData.userId = tuple.Item1;
		}
		if (applyPassword || string.IsNullOrEmpty(coreData.passwd))
		{
			coreData.passwd = tuple.Item2;
		}
	}

	private void setUIStart(bool qaNoCredentialWrite = false)
	{
		stopClicked = false;
		clubStr = clubLb.Text;

		CredentialStore.AccountData acc = ReadAccountsFromUi();
		if (string.IsNullOrWhiteSpace(acc.Id[acc.Res1Slot]))
		{
			MessageBox.Show("예약 1 계정(" + (acc.Res1Slot + 1) + ")의 아이디를 입력하세요");
			return;
		}
		if (chkR2.Checked && string.IsNullOrWhiteSpace(acc.Id[acc.Res2Slot]))
		{
			MessageBox.Show("예약 2 계정(" + (acc.Res2Slot + 1) + ")의 아이디를 입력하세요");
			return;
		}

		// ===== 희망 시간 검증: 드라이버/브라우저 초기화 이전에 반드시 통과 =====
		string vErr = ValidateStart();
		if (vErr != null)
		{
			lastStartError = vErr;
			Trace.WriteLine("START BLOCKED (range validation): " + vErr);
			logtxtBox(vErr);
			if (!suppressStartDialogs)
			{
				MessageBox.Show(vErr, "예약 설정 오류");
			}
			return; // 매크로/브라우저 미실행
		}
		lastStartError = null;

		// Both builds: the login for 예약 1 uses that reservation's selected account slot;
		// honour the save checkbox (persists BOTH slots + the per-reservation selection).
		coreData.accounts = acc;
		coreData.applyAccount(acc.Res1Slot);
		if (!qaNoCredentialWrite)
		{
			if (chkSaveCred.Checked)
			{
				coreData.saveAccounts(acc);
			}
			else
			{
				coreData.clearPassword();
			}
		}
		SaveConditions();
		start();
	}

	private void Btn_Click(object sender, EventArgs e)
	{
		Button button = sender as Button;
		if (button.Name == btnNames[0])
		{
			setUIStart();
		}
		else if (button.Name == btnNames[1])
		{
			if (cts != null)
			{
				CancellationTokenSource[] array = cts;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].Cancel();
				}
			}
			stopClicked = true;
		}
		else if (button.Name == btnNames[2])
		{
			string value = Interaction.InputBox(
				"Chrome 창 표시: 0, 숨김 실행: 1",
				"설정",
				headless ? "1" : "0").Trim();
			if (value == "0" || value == "1")
			{
				headless = value == "1";
				logtxtBox("Chrome " + (headless ? "숨김 실행" : "표시 실행") + "으로 설정됨");
			}
			else if (!string.IsNullOrEmpty(value))
			{
				MessageBox.Show("0 또는 1만 입력하세요.", "설정");
			}
		}
		else if (button.Name == btnNames[3])
		{
			// 저장도 실행과 동일한 검증을 통과해야 함 (잘못된 범위를 config에 남기지 않음).
			string sErr = ValidateStart();
			if (sErr != null)
			{
				lastStartError = sErr;
				logtxtBox(sErr);
				if (!suppressStartDialogs)
				{
					MessageBox.Show(sErr, "예약 설정 오류");
				}
				return;
			}
			CredentialStore.AccountData acc = ReadAccountsFromUi();
			coreData.accounts = acc;
			coreData.applyAccount(acc.Res1Slot);
			if (chkSaveCred.Checked)
			{
				// 계정 1/계정 2 슬롯 + 예약별 계정 선택을 모두 로컬 저장 (재실행 시 그대로 로드).
				coreData.saveAccounts(acc);
			}
			else
			{
				coreData.clearPassword();
			}
			SaveConditions();
			logtxtBox("저장: [예약1] " + ConditionString(1) + " (계정 " + (acc.Res1Slot + 1) + ")"
				+ "   [예약2] " + (chkR2.Checked ? (ConditionString(2) + " (계정 " + (acc.Res2Slot + 1) + ")") : "(미사용)")
				+ "   계정 저장=" + chkSaveCred.Checked);
		}
	}

	private void Lb_SelectionChanged(object sender, EventArgs e)
	{
		ListBox listBox = sender as ListBox;
		if (listBox != null && listBox.Name == "clubs")
		{
			courseLb.Items.Clear();
			ListBox.ObjectCollection items = courseLb.Items;
			object[] items2 = courses[clubLb.SelectedIndex];
			items.AddRange(items2);
			dt.Value = DateTime.Today.AddDays(opendays[clubLb.SelectedIndex]);
		}
	}

	// ---- Recovery helpers: two fixed conditions bound directly to the visible controls ----

	// 희망 시각은 항상 화면 컨트롤(nupArr)에서 직접 읽는다 -> 캐시된 값과 어긋날 수 없음.
	private int SlotDesired(int slot)
	{
		int b = (slot == 1) ? 0 : 2;
		return (int)nupArr[b].Value * 100 + (int)nupArr[b + 1].Value;
	}

	// "course,yyyymmdd,desiredHHMM,starter"
	private string ConditionString(int slot)
	{
		ListBox course = (slot == 1) ? courseLb : courseLb2;
		ListBox starter = (slot == 1) ? starterLb : starterLb2;
		DateTimePicker date = (slot == 1) ? dt : dt2;
		string c = course.SelectedItem?.ToString();
		string s = starter.SelectedItem?.ToString();
		if (string.IsNullOrWhiteSpace(c)) { c = "NA"; }
		if (string.IsNullOrWhiteSpace(s)) { s = "NA"; }
		return c + "," + date.Value.ToString("yyyyMMdd") + "," + SlotDesired(slot) + "," + s;
	}

	private bookInfo ParseCondition(string s)
	{
		return ConditionParser.Parse(s, courses[releaseVersion], starters[releaseVersion]);
	}

	private static string Hhmm(int hhmm)
	{
		return (hhmm / 100).ToString("00") + ":" + (hhmm % 100).ToString("00");
	}

	// The one and only range/format check. Reuses ConditionParser.Parse (the same rule the
	// persistence layer and the parser enforce): Parse() returns null when start > end.
	// Returns null when every enabled reservation is valid, otherwise a Korean message that
	// names the reservation. Called BEFORE any driver/browser initialization.
	internal string ValidateStart()
	{
		string e1 = ValidateOne(1, "예약 1");
		if (e1 != null)
		{
			return e1;
		}
		if (chkR2 != null && chkR2.Checked)
		{
			string e2 = ValidateOne(2, "예약 2");
			if (e2 != null)
			{
				return e2;
			}
		}
		return null;
	}

	private string ValidateOne(int slot, string label)
	{
		ListBox course = (slot == 1) ? courseLb : courseLb2;
		if (course.SelectedIndex < 0)
		{
			return label + ": 골프장을 선택하세요.";
		}
		int desired = SlotDesired(slot);
		if (slot == 2 && desired <= 0)
		{
			return label + " 사용 시 골프장 · 날짜 · 희망 시간을 모두 입력하세요.";
		}
		// SAME parse as persistence.
		bookInfo bi = ParseCondition(ConditionString(slot));
		if (bi == null)
		{
			return label + ": 희망 시간 값 형식이 올바르지 않습니다.";
		}
		return null;
	}

	// Persist ONLY the two conditions + user code. Credentials are never auto-saved.
	private void SaveConditions()
	{
		coreData.configSaveConditions(ConditionString(1), chkR2.Checked ? ConditionString(2) : "");
	}

	private void SetR2Enabled(bool on)
	{
		courseLb2.Enabled = on;
		starterLb2.Enabled = on;
		dt2.Enabled = on;
		nupArr[2].Enabled = on;
		nupArr[3].Enabled = on;
	}

	private bool start()
	{
		monitor = false;

		// 방어적 재검증 (setUIStart 에서 이미 통과했지만 단일 소스로 한 번 더).
		string vErr = ValidateStart();
		if (vErr != null)
		{
			lastStartError = vErr;
			logtxtBox(vErr);
			if (!suppressStartDialogs)
			{
				MessageBox.Show(vErr, "예약 설정 오류");
			}
			return false;
		}
		lastStartError = null;

		bookDate = dt.Value.ToShortDateString().Replace("-", "");
		bookList.Clear();

		bookInfo c1 = ParseCondition(ConditionString(1));
		bookList.Add(c1);
		logtxtBox("예약 1 희망 티타임 " + Hhmm(SlotDesired(1)) + " (가능한 슬롯 중 가장 가까운 시간을 선택)");

		if (!chkR2.Checked)
		{
			logtxtBox("예약 2 미사용 - 예약 1만 실행합니다.");
		}
		else
		{
			bookInfo c2 = ParseCondition(ConditionString(2));
			bookList.Add(c2);
			logtxtBox("예약 2 희망 티타임 " + Hhmm(SlotDesired(2))
				+ " - 예약 1 결과와 관계없이(브라우저/페이지 오류 제외) 순차 진행.");
		}

		SaveConditions();

		// 예약 1 로그인은 예약 1에 선택된 계정 슬롯으로 (방어적 재적용).
		coreData.applyAccount(coreData.accounts.Res1Slot);

		Trace.WriteLine("QA AUTO-START: Reservation 1 configuration accepted; initializing ChromeDriver.");
		workPool = new dayPool(releaseVersion, bookList.Count);

		// 순차 정책 -> 브라우저/워커 1개
		coreData.threadNum = 1;
		driver = (ChromeDriver[])(object)new ChromeDriver[coreData.threadNum];
		DisableIncompatibleLocalChromeDriver();
		string directoryName = Path.GetDirectoryName(EnsureChromeDriverReadyInteractive());
		for (int i = 0; i < coreData.threadNum; i++)
		{
			ChromeOptions val = new ChromeOptions();
			// The current Sun Valley course picker leaves background resources open.
			// Do not block navigation on those resources; sunValley waits explicitly
			// for the calendar or course-picker DOM that it needs.
			val.PageLoadStrategy = PageLoadStrategy.None;
			if (headless)
			{
				((ChromiumOptions)val).AddArguments(new string[1] { "headless" });
			}
			((ChromiumOptions)val).AddArguments(new string[1] { "--window-size=1920,4000" });
			ChromeDriverService val2 = ChromeDriverService.CreateDefaultService(directoryName, "chromedriver.exe");
			((DriverService)val2).HideCommandPromptWindow = true;
			driver[i] = new ChromeDriver(val2, val);
		}
		cts = new CancellationTokenSource[coreData.threadNum];
		for (int j = 0; j < cts.Length; j++)
		{
			cts[j] = new CancellationTokenSource();
		}
		WebDriverExtensions.threadNum = coreData.threadNum;
		WebDriverExtensions.delay1 = coreData.lockDelay;
		WebDriverExtensions.frm = this;
		ts = new Task<bool>[coreData.threadNum];
		for (int k = 0; k < ts.Length; k++)
		{
			ts[k] = new Task<bool>(bookMain, k, cts[k].Token);
			ts[k].Start();
			Thread.Sleep(100);
		}
		logtxtBox("Initialization Done");
		return true;
	}

	private string EnsureChromeDriverReadyInteractive()
	{
		int chromeMajorVersion = GetChromeMajorVersion();
		if (chromeMajorVersion <= 0)
		{
			throw new Exception("Chrome browser version check failed.");
		}
		string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chromedriver.exe");
		// A matching bundled driver is sufficient.  In particular, the local dummy
		// test mode must not require a network lookup or an interactive version prompt.
		if (File.Exists(text))
		{
			int localMajor = FileVersionInfo.GetVersionInfo(text).FileMajorPart;
			if (localMajor == chromeMajorVersion)
			{
				logtxtBox("chromedriver ready: " + (FileVersionInfo.GetVersionInfo(text).FileVersion ?? ""));
				return text;
			}
		}
		string latestChromeDriverRelease = GetLatestChromeDriverRelease(chromeMajorVersion);
		string value = Interaction.InputBox($"Detected Chrome major version: {chromeMajorVersion}\nEnter ChromeDriver version to use.", "ChromeDriver Version", latestChromeDriverRelease).Trim();
		if (string.IsNullOrWhiteSpace(value))
		{
			value = latestChromeDriverRelease;
		}
		if (File.Exists(text))
		{
			string text2 = FileVersionInfo.GetVersionInfo(text).FileVersion ?? "";
			if (text2.StartsWith(value, StringComparison.OrdinalIgnoreCase))
			{
				logtxtBox("chromedriver ready: " + text2);
				return text;
			}
		}
		DownloadChromeDriverToPath(value, text);
		int fileMajorPart = FileVersionInfo.GetVersionInfo(text).FileMajorPart;
		if (fileMajorPart != chromeMajorVersion)
		{
			throw new Exception($"Chrome v{chromeMajorVersion}, downloaded driver v{fileMajorPart}. Please input matching version.");
		}
		logtxtBox("chromedriver downloaded: v" + FileVersionInfo.GetVersionInfo(text).FileVersion);
		return text;
	}

	private string GetLatestChromeDriverRelease(int chromeMajor)
	{
		using HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(30L);
		return httpClient.GetStringAsync($"https://googlechromelabs.github.io/chrome-for-testing/LATEST_RELEASE_{chromeMajor}").GetAwaiter().GetResult()
			.Trim();
	}

	private void DownloadChromeDriverToPath(string releaseVersion, string driverExe)
	{
		string requestUri = "https://storage.googleapis.com/chrome-for-testing-public/" + releaseVersion + "/win64/chromedriver-win64.zip";
		string value = releaseVersion.Replace('.', '_');
		string text = Path.Combine(Path.GetTempPath(), $"chromedriver_{value}_{Guid.NewGuid():N}.zip");
		string text2 = Path.Combine(Path.GetTempPath(), $"chromedriver_{value}_{Guid.NewGuid():N}");
		string directoryName = Path.GetDirectoryName(driverExe);
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			throw new Exception("Invalid booking.exe directory.");
		}
		Directory.CreateDirectory(directoryName);
		try
		{
			logtxtBox("Downloading chromedriver " + releaseVersion);
			using (HttpClient httpClient = new HttpClient())
			{
				httpClient.Timeout = TimeSpan.FromMinutes(2L);
				byte[] result = httpClient.GetByteArrayAsync(requestUri).GetAwaiter().GetResult();
				File.WriteAllBytes(text, result);
			}
			Directory.CreateDirectory(text2);
			ZipFile.ExtractToDirectory(text, text2, overwriteFiles: true);
			string text3 = Path.Combine(text2, "chromedriver-win64", "chromedriver.exe");
			if (!File.Exists(text3))
			{
				throw new Exception("Downloaded chromedriver archive is invalid.");
			}
			File.Copy(text3, driverExe, overwrite: true);
		}
		finally
		{
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
			catch
			{
			}
			try
			{
				if (Directory.Exists(text2))
				{
					Directory.Delete(text2, recursive: true);
				}
			}
			catch
			{
			}
		}
	}

	private void DisableIncompatibleLocalChromeDriver()
	{
		try
		{
			int chromeMajorVersion = GetChromeMajorVersion();
			if (chromeMajorVersion <= 0)
			{
				return;
			}
			foreach (string item in new string[2]
			{
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chromedriver.exe"),
				Path.Combine(Environment.CurrentDirectory, "chromedriver.exe")
			}.Distinct<string>(StringComparer.OrdinalIgnoreCase))
			{
				if (File.Exists(item))
				{
					int fileMajorPart = FileVersionInfo.GetVersionInfo(item).FileMajorPart;
					if (fileMajorPart > 0 && fileMajorPart != chromeMajorVersion)
					{
						string text = Path.Combine(Path.GetDirectoryName(item) ?? ".", $"chromedriver_v{fileMajorPart}_old_{DateTime.Now:yyyyMMddHHmmss}.exe");
						File.Move(item, text);
						logtxtBox($"Incompatible chromedriver moved: v{fileMajorPart} -> {text}");
					}
				}
			}
		}
		catch (Exception ex)
		{
			logtxtBox("DisableIncompatibleLocalChromeDriver error: " + ex.Message);
		}
	}

	private int GetChromeMajorVersion()
	{
		string[] array = new string[4]
		{
			GetChromePathFromRegistry(RegistryHive.CurrentUser),
			GetChromePathFromRegistry(RegistryHive.LocalMachine),
			"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
			"C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe"
		};
		foreach (string text in array)
		{
			if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
			{
				return FileVersionInfo.GetVersionInfo(text).FileMajorPart;
			}
		}
		return -1;
	}

	private string GetChromePathFromRegistry(RegistryHive hive)
	{
		try
		{
			using RegistryKey registryKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
			using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe");
			return registryKey2?.GetValue(string.Empty)?.ToString();
		}
		catch
		{
			return null;
		}
	}

	// ---------------------------------------------------------------------------
	// Sequential two-condition run.
	//   login once -> 예약 1 -> 예약 2 (새 탭)
	//   예약 1의 마감/오픈전/시간없음/진단 no-submit은 예약 2를 막지 않는다.
	//   브라우저·페이지 이동 오류일 때만 공유 세션이 신뢰할 수 없으므로 예약 2를 중단한다.
	// ---------------------------------------------------------------------------
	private bool bookMain(object index)
	{
		int num = (int)index;
		ChromeDriver val = driver[num];
		club client;
		logtxtBox("bookMain Start");
		try
		{
			client = initClass(val, num);
			logtxtBox("Login Start");
			if (!client.login(this))
			{
				logtxtBox("Login Failed");
				logtxtBox("RUN TERMINATED: login failed.");
				SafeQuit(val);
				return false;
			}
			logtxtBox("Login Done");
		}
		catch (Exception ex)
		{
			logtxtBox("bookMain Fail" + ex);
			SafeQuit(val);
			return false;
		}

		if (bookList.Count == 0)
		{
			logtxtBox("No conditions configured. RUN TERMINATED.");
			SafeQuit(val);
			return false;
		}

		logtxtBox("========== 예약 1 ==========  " + bookList[0]);
		BookOutcome o1 = client.bookRequest(bookList[0]);
		logtxtBox(DescribeOutcome(1, o1));
		logtxtBox(SequentialGate.Decision(o1, client.diagnosticMode));
		if (!SequentialGate.ShouldRunCondition2(o1))
		{
			logtxtBox("==> 예약 2 건너뜀. 실행 종료 (예약 1 브라우저/페이지 오류).");
			logtxtBox(SummarizeRun(o1, null, null));
			SafeQuit(val);
			return false;
		}

		if (bookList.Count < 2 || stopClicked)
		{
			logtxtBox("예약 2 없음. 실행 완료.");
			logtxtBox(SummarizeRun(o1, null, stopClicked ? "사용자가 실행을 중지함" : null));
			SafeQuit(val);
			return true;
		}

		try
		{
			((IWebDriver)(object)val).SwitchTo().NewWindow(WindowType.Tab);
			logtxtBox("예약 2: 새 탭에서 진행");
		}
		catch (Exception)
		{
		}

		// The two tabs share browser cookies.  Reuse a same-account session; otherwise
		// explicitly leave it and verify a fresh login form before signing in.
		int r1slot = coreData.accounts.ClampSlot(coreData.accounts.Res1Slot);
		int r2slot = coreData.accounts.ClampSlot(coreData.accounts.Res2Slot);
		bool sameAccount = r1slot == r2slot;
		if (!client.prepareReservation2Session(this, sameAccount))
		{
			logtxtBox("예약 2 세션 전환 실패. 실행 종료.");
			logtxtBox(SummarizeRun(o1, null, "예약 2 세션 전환 실패"));
			SafeQuit(val);
			return false;
		}
		if (!sameAccount)
		{
			coreData.applyAccount(r2slot);
			client.id = coreData.userId;
			client.pwd = coreData.passwd;
			logtxtBox("예약 2 계정 " + (r2slot + 1) + " 로 로그인");
			if (!client.login(this))
			{
				logtxtBox("예약 2 로그인 실패. 실행 종료.");
				logtxtBox(SummarizeRun(o1, null, "예약 2 로그인 실패"));
				SafeQuit(val);
				return false;
			}
		}

		logtxtBox("========== 예약 2 ==========  " + bookList[1]);
		BookOutcome o2 = client.bookRequest(bookList[1]);
		logtxtBox(DescribeOutcome(2, o2));
		logtxtBox(o2.GateSatisfied
			? ("예약 2 확정 (confirmationId=" + o2.ConfirmationId + ").")
			: ("예약 2 미확정: " + o2.GateReason(client.diagnosticMode) + "."));
		logtxtBox(SummarizeRun(o1, o2, null));
		logtxtBox("실행 완료.");
		SafeQuit(val);
		return true;
	}

	private static string DescribeOutcome(int n, BookOutcome o)
	{
		return "예약 " + n + " -> result=" + o.Result
			+ " slotFound=" + o.SlotFound
			+ ((o.ChosenTee != null) ? (" desired=" + o.DesiredTee + " chosen=" + o.ChosenTee + " deltaMin=" + o.DeltaMinutes) : "")
			+ " submitted=" + o.Submitted
			+ " confirmationId=" + (o.ConfirmationId ?? "(none)")
			+ " historyVerified=" + o.HistoryVerified
			+ ((o.Detail != null) ? ("  detail=" + o.Detail) : "");
	}

	private static bool IsInfrastructureError(BookOutcome outcome)
	{
		return outcome == null || outcome.Result == OpResult.Fail || outcome.Result == OpResult.FalalError;
	}

	private static string OutcomeSummary(int number, BookOutcome outcome)
	{
		if (outcome == null)
		{
			return "예약 " + number + " 결과 없음";
		}
		return "예약 " + number + "=" + outcome.Result
			+ (string.IsNullOrEmpty(outcome.Detail) ? "" : " (" + outcome.Detail + ")");
	}

	// The final line is intentionally a user-facing result, not merely a control-flow
	// decision.  It makes closed dates, no available times, and actual browser errors
	// distinguishable in the log without reading the preceding Selenium detail.
	private static string SummarizeRun(BookOutcome o1, BookOutcome o2, string infrastructureError)
	{
		if (!string.IsNullOrEmpty(infrastructureError))
		{
			return "실행 요약: 오류 - " + infrastructureError + ". " + OutcomeSummary(1, o1);
		}
		if (IsInfrastructureError(o1) || (o2 != null && IsInfrastructureError(o2)))
		{
			return "실행 요약: 오류 - " + OutcomeSummary(1, o1)
				+ (o2 == null ? "" : ", " + OutcomeSummary(2, o2));
		}
		if (o1 != null && o2 != null && o1.Result == OpResult.Overbook && o2.Result == OpResult.Overbook)
		{
			return "실행 요약: 모두 마감 - 두 예약 대상 날짜에 예약 가능한 티타임이 없습니다.";
		}
		if (o1 != null && o2 != null && o1.Result == OpResult.NotOpen && o2.Result == OpResult.NotOpen)
		{
			return "실행 요약: 모두 오픈 전 - 인접 날짜는 탐색하지 않았습니다.";
		}
		return "실행 요약: " + OutcomeSummary(1, o1)
			+ (o2 == null ? "" : ", " + OutcomeSummary(2, o2));
	}

	private void SafeQuit(ChromeDriver d)
	{
		try
		{
			Thread.Sleep(500);
			((WebDriver)d).Quit();
		}
		catch (Exception)
		{
		}
	}

	private bool monitorSpace(ChromeDriver drv, club client)
	{
		foreach (string monitorDate in monitorDates)
		{
			if (client.monitorOne(monitorDate))
			{
				return true;
			}
		}
		return false;
	}

	private void initTable()
	{
		// Fit the complete two-condition layout on a standard 1080p desktop at
		// first launch.  ClientSize avoids the title-bar/border difference that
		// previously clipped the lower reservation block on some displays.
		base.ClientSize = new Size(1700, 980);
		base.MinimumSize = new Size(1040, 860);
		base.StartPosition = FormStartPosition.CenterScreen;
		int num = 5;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Padding = new Padding(20, 20, 20, 20),
			ColumnCount = 4,
			RowCount = num,
			BorderStyle = BorderStyle.FixedSingle,
			Dock = DockStyle.Fill
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
		// 겹침 방지: 예약 1 / 예약 2 블록 행에 충분한 높이 확보
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));   // 0 buttons
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 280f));  // 1 예약 1 block (제목+희망 시간+골프장/날짜)
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 116f));  // 2 account (계정 1 / 계정 2 + 저장 체크가 안 잘리게)
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 290f));  // 3 예약 2 block
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));   // 4 저장
		for (int i = 0; i < 3; i++)
		{
			Button button = new Button
			{
				Size = new Size(100, 50),
				Name = btnNames[i],
				Text = btnNames[i]
			};
			button.Click += Btn_Click;
			tableLayoutPanel.Controls.Add(button, i, 0);
		}
		// ---- account row: two local slots (계정 1 / 계정 2), each editable ID + PW ----
		// Passwords shown in clear text in every build, per operator request. No credential
		// is ever embedded, printed, or logged; slot values live only in the DPAPI store.
		idTb = new TextBox { Width = 120 };
		pwdTb = new TextBox { Width = 120, UseSystemPasswordChar = false };
		acct2IdTb = new TextBox { Width = 120 };
		acct2PwTb = new TextBox { Width = 120, UseSystemPasswordChar = false };
		chkSaveCred = new CheckBox { Text = "계정 저장", AutoSize = true, Checked = true };
		chkSaveCred.CheckedChanged += delegate
		{
			if (!chkSaveCred.Checked)
			{
				coreData.clearPassword();
			}
		};

		FlowLayoutPanel acctCell = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
		acctCell.Controls.Add(AcctRow("계정 1", idTb, pwdTb));
		acctCell.Controls.Add(AcctRow("계정 2", acct2IdTb, acct2PwTb));
		acctCell.Controls.Add(chkSaveCred);
		coreData.setFonts(acctCell.Controls, 10);

		acctCbo1 = MakeAcctCombo();
		acctCbo2 = MakeAcctCombo();

		clubLb = new ListBox { Name = "clubs", Dock = DockStyle.Fill, BorderStyle = BorderStyle.Fixed3D };
		tableLayoutPanel.Controls.Add(acctCell, 0, 2);
		tableLayoutPanel.SetColumnSpan(acctCell, 2);
		tableLayoutPanel.Controls.Add(clubLb, 2, 2);

		// ---- 예약 1 / 예약 2 : identical structure (같은 컨트롤·스타일·정렬) ----
		courseLb = new ListBox { Name = "courses", Size = new Size(150, 100), BorderStyle = BorderStyle.Fixed3D };
		starterLb = new ListBox { Name = "starter", Size = new Size(150, 100), BorderStyle = BorderStyle.Fixed3D };
		dt = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 120 };
		courseLb.SelectedIndexChanged += Lb_SelectionChanged;
		starterLb.SelectedIndexChanged += Lb_SelectionChanged;

		courseLb2 = new ListBox { Name = "courses2", Size = new Size(150, 100), BorderStyle = BorderStyle.Fixed3D, Enabled = false };
		starterLb2 = new ListBox { Name = "starter2", Size = new Size(150, 100), BorderStyle = BorderStyle.Fixed3D, Enabled = false };
		dt2 = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 120, Enabled = false };

		// 4개 시간 입력: 예약1 희망 h/m  +  예약2 희망 h/m
		for (int i = 0; i < 4; i++)
		{
			nupArr[i] = MakeTimeNup((i % 2 == 0) ? 23 : 59, timeNames[i]);
		}
		for (int i = 2; i < 4; i++)
		{
			nupArr[i].Enabled = false; // 예약2 시간은 '예약 2 사용' 체크 시 활성화
		}
		SetNup(nupArr[0], start1 / 100); SetNup(nupArr[1], start1 % 100); // 예약1 희망 기본 09:00
		// nupArr[2..3] = 0 -> 예약2 시간 미입력 상태

		chkR2 = new CheckBox { Text = "예약 2 사용", AutoSize = true, Checked = false, Margin = new Padding(16, 4, 0, 0) };
		chkR2.CheckedChanged += delegate { SetR2Enabled(chkR2.Checked); };

		buildClubList(clubLb, courseLb, starterLb);
		courseLb2.Items.AddRange(courses[releaseVersion]);
		courseLb2.SelectedIndex = -1;
		starterLb2.Items.AddRange(starters[releaseVersion]);
		starterLb2.SelectedIndex = 0;
		dt2.Value = DateTime.Today.AddDays(opendays[releaseVersion]);

		// 예약 1 : 희망 시간을 구역 '위쪽'에 (제목 바로 아래)
		tableLayoutPanel.Controls.Add(BuildResBlock("■ 예약 1", null, courseLb, starterLb, dt, nupArr[0], nupArr[1], timeFirst: true, acct: acctCbo1), 0, 1);
		tableLayoutPanel.SetColumnSpan(tableLayoutPanel.GetControlFromPosition(0, 1), 3);
		// 예약 2 : 희망 시간을 구역 '아래쪽'에 (골프장/날짜 다음)
		tableLayoutPanel.Controls.Add(BuildResBlock("■ 예약 2", chkR2, courseLb2, starterLb2, dt2, nupArr[2], nupArr[3], timeFirst: false, acct: acctCbo2), 0, 3);
		tableLayoutPanel.SetColumnSpan(tableLayoutPanel.GetControlFromPosition(0, 3), 3);

		// 저장 버튼 (원래 위치 유지: 하단 행)
		Button save = new Button { Size = new Size(100, 50), Name = btnNames[3], Text = btnNames[3] };
		save.Click += Btn_Click;
		tableLayoutPanel.Controls.Add(save, 0, 4);

		tbLog = new RichTextBox { Text = "Log Box", Dock = DockStyle.Fill, ReadOnly = true };
		tableLayoutPanel.Controls.Add(tbLog, 3, 0);
		tableLayoutPanel.SetRowSpan(tbLog, num);
		base.Controls.Add(tableLayoutPanel);

		// 계정 슬롯/선택은 편집 가능 (계정 전환 시 사용자가 직접 덮어쓰고 저장).
		LoadAccountsToUi();

		PrefillFromConfig();
	}

	private NumericUpDown MakeTimeNup(int max, string name)
	{
		NumericUpDown n = new NumericUpDown { Name = name, Width = 52, Minimum = 0m, Maximum = max };
		n.ValueChanged += numericUpDown_ValueChanged;
		return n;
	}

	// 한 예약 블록: [제목(+체크)] + [희망 시간 줄] + [골프장·스타터·날짜 줄].
	// timeFirst=true  -> 희망 시간 줄을 제목 바로 아래(위쪽)에   (예약 1)
	// timeFirst=false -> 희망 시간 줄을 골프장/날짜 다음(아래쪽)에 (예약 2)
	// 예약1의 시간은 상단, 예약2의 시간은 하단이라 공용 행처럼 보이지 않는다.
	private Control BuildResBlock(string title, CheckBox chk, ListBox course, ListBox starter,
		DateTimePicker date, NumericUpDown sH, NumericUpDown sM, bool timeFirst,
		ComboBox acct = null)
	{
		FlowLayoutPanel titleRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
		titleRow.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 4, 0, 0) });
		if (chk != null)
		{
			titleRow.Controls.Add(chk);
		}

		FlowLayoutPanel fields = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
		fields.Controls.Add(new Label { Text = "골프장", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
		fields.Controls.Add(course);
		fields.Controls.Add(new Label { Text = "스타터", AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
		fields.Controls.Add(starter);
		fields.Controls.Add(new Label { Text = "날짜", AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
		fields.Controls.Add(date);
		if (acct != null)
		{
			fields.Controls.Add(new Label { Text = "계정", AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
			fields.Controls.Add(acct);
		}

		FlowLayoutPanel startRow = TimeRow("희망 시간", sH, sM);

		FlowLayoutPanel block = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(2, 6, 2, 12) };
		block.Controls.Add(titleRow);
		if (timeFirst)
		{
			block.Controls.Add(startRow);
			block.Controls.Add(fields);
		}
		else
		{
			block.Controls.Add(fields);
			block.Controls.Add(startRow);
		}
		coreData.setFonts(titleRow.Controls, 10);
		coreData.setFonts(fields.Controls, 10);
		coreData.setFonts(startRow.Controls, 10);
		return block;
	}

	private static FlowLayoutPanel AcctRow(string caption, TextBox idBox, TextBox pwBox)
	{
		FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 1, 0, 1) };
		row.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 6, 4, 0), Width = 46 });
		row.Controls.Add(new Label { Text = "ID", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
		row.Controls.Add(idBox);
		row.Controls.Add(new Label { Text = "PW", AutoSize = true, Margin = new Padding(8, 6, 2, 0) });
		row.Controls.Add(pwBox);
		return row;
	}

	private ComboBox MakeAcctCombo()
	{
		ComboBox c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 84 };
		c.Items.AddRange(new object[] { "계정 1", "계정 2" });
		c.SelectedIndex = 0;
		return c;
	}

	// Copy the persisted account slots + per-reservation selection into the UI controls.
	private void LoadAccountsToUi()
	{
		var a = coreData.accounts ?? new CredentialStore.AccountData();
		idTb.Text = a.Id[0] ?? "";
		pwdTb.Text = a.Pw[0] ?? "";
		acct2IdTb.Text = a.Id[1] ?? "";
		acct2PwTb.Text = a.Pw[1] ?? "";
		acctCbo1.SelectedIndex = a.ClampSlot(a.Res1Slot);
		acctCbo2.SelectedIndex = a.ClampSlot(a.Res2Slot);
	}

	// Read the current UI account slots + per-reservation selection.
	private CredentialStore.AccountData ReadAccountsFromUi()
	{
		var a = new CredentialStore.AccountData();
		a.Id[0] = idTb.Text; a.Pw[0] = pwdTb.Text;
		a.Id[1] = acct2IdTb.Text; a.Pw[1] = acct2PwTb.Text;
		a.Res1Slot = a.ClampSlot(acctCbo1.SelectedIndex);
		a.Res2Slot = a.ClampSlot(acctCbo2.SelectedIndex);
		return a;
	}

	private static FlowLayoutPanel TimeRow(string caption, NumericUpDown h, NumericUpDown m)
	{
		FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
		row.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 6, 6, 0), Width = 62 });
		row.Controls.Add(h);
		row.Controls.Add(new Label { Text = "시", AutoSize = true, Margin = new Padding(2, 6, 10, 0) });
		row.Controls.Add(m);
		row.Controls.Add(new Label { Text = "분", AutoSize = true, Margin = new Padding(2, 6, 0, 0) });
		return row;
	}

	// Load the two conditions from config into the visible controls.
	private void PrefillFromConfig()
	{
		bookInfo b1 = ParseCondition(coreData.condition1);
		if (b1 != null)
		{
			if (b1.course >= 0 && b1.course < courseLb.Items.Count) { courseLb.SelectedIndex = b1.course; }
			int s1 = Array.IndexOf(starters[releaseVersion], b1.starter);
			if (s1 >= 0 && s1 < starterLb.Items.Count) { starterLb.SelectedIndex = s1; }
			SetDate(dt, b1.date);
			SetNup(nupArr[0], b1.desiredTime / 100); SetNup(nupArr[1], b1.desiredTime % 100);
		}

		bookInfo b2 = ParseCondition(coreData.condition2);
		if (b2 != null)
		{
			chkR2.Checked = true;
			SetR2Enabled(true);
			if (b2.course >= 0 && b2.course < courseLb2.Items.Count) { courseLb2.SelectedIndex = b2.course; }
			int s2 = Array.IndexOf(starters[releaseVersion], b2.starter);
			if (s2 >= 0 && s2 < starterLb2.Items.Count) { starterLb2.SelectedIndex = s2; }
			SetDate(dt2, b2.date);
			SetNup(nupArr[2], b2.desiredTime / 100); SetNup(nupArr[3], b2.desiredTime % 100);
		}
		RefreshEcho();
	}

	private static void SetDate(DateTimePicker d, string yyyymmdd)
	{
		try
		{
			d.Value = new DateTime(int.Parse(yyyymmdd.Substring(0, 4)), int.Parse(yyyymmdd.Substring(4, 2)), int.Parse(yyyymmdd.Substring(6, 2)));
		}
		catch (Exception)
		{
		}
	}

	private void SetNup(NumericUpDown n, int v)
	{
		noEventHandler = true;
		n.Value = Math.Min((int)n.Maximum, Math.Max((int)n.Minimum, v));
		noEventHandler = false;
	}

	private void RefreshEcho()
	{
		// No on-screen echo control (kept UI minimal). Summary is written to the log on 저장.
	}

	private void buildClubList(ListBox lb, ListBox cLb, ListBox startLb_)
	{
		if (releaseVersion >= 0)
		{
			lb.Items.Add(coreData.clubList[releaseVersion]);
			lb.SelectedIndex = 0;
			dt.Value = DateTime.Today.AddDays(opendays[releaseVersion]);
			lb.Enabled = false;
			ListBox.ObjectCollection items3 = cLb.Items;
			object[] clubList = courses[releaseVersion];
			items3.AddRange(clubList);
			cLb.SelectedIndex = 0;
			ListBox.ObjectCollection items4 = startLb_.Items;
			clubList = starters[releaseVersion];
			items4.AddRange(clubList);
			startLb_.SelectedIndex = 0;
		}
	}

	private void numericUpDown_ValueChanged(object sender, EventArgs e)
	{
		// 희망 시간 값은 저장/실행 시 nupArr 에서 직접 읽으므로 여기서 캐싱할 필요가 없다.
		if (noEventHandler)
		{
			return;
		}
		RefreshEcho();
	}

	public void logtxtBox(string str)
	{
		string tmp = DateTime.Now.ToLongTimeString() + " " + str;
		if (tbLog.InvokeRequired)
		{
			tbLog.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate
			{
				tbLog.AppendText(Environment.NewLine + tmp);
				tbLog.ScrollToCaret();
			});
		}
		else
		{
			tbLog.AppendText(Environment.NewLine + tmp);
			tbLog.ScrollToCaret();
		}
	}

	public void getMessageBoxOK()
	{
		if (tbLog.InvokeRequired)
		{
			BeginInvoke((System.Windows.Forms.MethodInvoker)delegate
			{
				MessageBox.Show("계속 할까요?");
				gotStart = true;
			});
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(818, 379);
		base.Name = "Form1";
		this.Text = "Form1";
		base.Load += new System.EventHandler(Form1_Load);
		base.ResumeLayout(false);
	}
}
