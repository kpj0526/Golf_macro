using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using booking;

namespace StartValidation;

// Integration-level test of the real WinForms Start path: the Start-button handler
// (setUIStart) must reject start > end for every ENABLED reservation BEFORE any
// ChromeDriver / browser is created, and keep the macro closed.
internal static class Program
{
	private static int _pass;
	private static int _fail;

	private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
	private const BindingFlags PB = BindingFlags.Public | BindingFlags.Instance;

	[STAThread]
	private static int Main()
	{
		Application.EnableVisualStyles();

		// remove any local DPAPI password store so the run is deterministic
		try
		{
			string pw = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GolfCatch", "pw.dat");
			if (System.IO.File.Exists(pw)) System.IO.File.Delete(pw);
		}
		catch { }

		// --- 1. 예약 1 : start 13:00 > end 12:00  -> Start blocked, no browser ---
		Case("예약 1 start 13:00 / end 12:00 -> blocked, no driver", f =>
		{
			SetId(f, "acct_test");
			SetCourse(f, "courseLb", 0);
			SetNup(f, 0, 13); SetNup(f, 1, 0);   // 예약1 시작 13:00
			SetNup(f, 2, 12); SetNup(f, 3, 0);   // 예약1 종료 12:00
			InvokeSetUIStart(f);
			string err = LastError(f);
			bool blocked = err != null && err.Contains("예약 1") && (err.Contains("시작") && err.Contains("종료"));
			bool noDriver = GetField(f, "driver") == null && GetField(f, "ts") == null;
			return (blocked && noDriver, "err=\"" + err + "\"  driverNull=" + (GetField(f, "driver") == null) + " tsNull=" + (GetField(f, "ts") == null));
		});

		// --- 2. 예약 1 valid, 예약 2 enabled with start 15:00 > end 09:00 -> blocked naming 예약 2 ---
		Case("예약 2 enabled, start 15:00 / end 09:00 -> blocked naming 예약 2, no driver", f =>
		{
			SetId(f, "acct_test");
			SetCourse(f, "courseLb", 0);
			SetNup(f, 0, 9); SetNup(f, 1, 0);   // 예약1 09:00
			SetNup(f, 2, 12); SetNup(f, 3, 0);  // 예약1 12:00  (valid)
			SetCheck(f, "chkR2", true);
			SetCourse(f, "courseLb2", 0);
			SetNup(f, 4, 15); SetNup(f, 5, 0);  // 예약2 시작 15:00
			SetNup(f, 6, 9);  SetNup(f, 7, 0);  // 예약2 종료 09:00
			InvokeSetUIStart(f);
			string err = LastError(f);
			bool blocked = err != null && err.Contains("예약 2");
			bool noDriver = GetField(f, "driver") == null;
			return (blocked && noDriver, "err=\"" + err + "\"  driverNull=" + noDriver);
		});

		// --- 3. 예약 2 enabled but time not entered (0:00/0:00) -> blocked, no driver ---
		Case("예약 2 enabled, times 00:00 -> blocked (모두 입력), no driver", f =>
		{
			SetId(f, "acct_test");
			SetCourse(f, "courseLb", 0);
			SetNup(f, 0, 9); SetNup(f, 1, 0); SetNup(f, 2, 12); SetNup(f, 3, 0);
			SetCheck(f, "chkR2", true);
			SetCourse(f, "courseLb2", 0);
			// nupArr[4..7] left at 0
			InvokeSetUIStart(f);
			string err = LastError(f);
			return (err != null && err.Contains("예약 2") && GetField(f, "driver") == null, "err=\"" + err + "\"");
		});

		// --- 4. ValidateStart() directly: all-valid -> null (uses the SAME parser rule) ---
		Case("ValidateStart() returns null for a valid single reservation", f =>
		{
			SetCourse(f, "courseLb", 0);
			SetNup(f, 0, 9); SetNup(f, 1, 0); SetNup(f, 2, 10); SetNup(f, 3, 30);
			SetCheck(f, "chkR2", false);
			string r = (string)f.GetType().GetMethod("ValidateStart", NP).Invoke(f, null);
			return (r == null, "ValidateStart=" + (r ?? "null"));
		});

		// --- 5. start == end is allowed (inclusive) ---
		Case("ValidateStart() allows start == end", f =>
		{
			SetCourse(f, "courseLb", 0);
			SetNup(f, 0, 9); SetNup(f, 1, 30); SetNup(f, 2, 9); SetNup(f, 3, 30);
			SetCheck(f, "chkR2", false);
			string r = (string)f.GetType().GetMethod("ValidateStart", NP).Invoke(f, null);
			return (r == null, "ValidateStart=" + (r ?? "null"));
		});

		// --- 6. the start path uses the SAME parser rule: Form1.ParseCondition(ConditionString(slot))
		//        returns null for start>end, and ValidateStart() blocks in lockstep. ---
		Case("start-path rule == ParseCondition(ConditionString) (start>end)", f =>
		{
			SetCourse(f, "courseLb", 0);
			SetNup(f, 0, 13); SetNup(f, 1, 0); SetNup(f, 2, 12); SetNup(f, 3, 0);
			SetCheck(f, "chkR2", false);
			string line = (string)f.GetType().GetMethod("ConditionString", NP).Invoke(f, new object[] { 1 });
			object bi = f.GetType().GetMethod("ParseCondition", NP).Invoke(f, new object[] { line });
			string r = (string)f.GetType().GetMethod("ValidateStart", NP).Invoke(f, null);
			return (bi == null && r != null, "line=" + line + " parse=" + (bi == null ? "null" : "obj") + " validate=" + (r ?? "null"));
		});

		// --- 7. both builds: ID / password fields are editable and unmasked at startup;
		//        the removed legacy/original-credential machinery is gone. ---
		Case("startup: ID/PW fields editable + unmasked, no legacy credential machinery", f =>
		{
			var idTb = (TextBox)GetField(f, "idTb");
			var pwdTb = (TextBox)GetField(f, "pwdTb");
			var chk = (CheckBox)GetField(f, "chkSaveCred");
			bool editable = idTb.Enabled && !idTb.ReadOnly && pwdTb.Enabled && !pwdTb.ReadOnly && chk.Enabled;
			bool unmasked = !pwdTb.UseSystemPasswordChar;
			bool noPreflight = f.GetType().GetMethod("DiagnosticCredentialPreflight", NP) == null;
			object cd = f.GetType().GetField("coreData").GetValue(f);
			bool noBridge = cd.GetType().GetMethod("loadOriginalMacroCredentials") == null
				&& cd.GetType().GetField("originalConfigPath") == null;
			return (editable && unmasked && noPreflight && noBridge,
				"editable=" + editable + " unmasked=" + unmasked + " noPreflight=" + noPreflight + " noBridge=" + noBridge);
		});

		// --- 8. 저장 button persists the currently typed ID + password pair; after
		//        close/relaunch a fresh Form1 loads exactly that pair. ---
		Case("저장 persists typed ID/PW pair; relaunch loads it", f =>
		{
			ClearStore();
			SetId(f, "acct_one");
			((TextBox)GetField(f, "pwdTb")).Text = "pw_one_#42";
			SetCourse(f, "courseLb", 0);
			SetNup(f, 0, 9); SetNup(f, 1, 0); SetNup(f, 2, 10); SetNup(f, 3, 30);
			SetCheck(f, "chkR2", false);
			SetCheck(f, "chkSaveCred", true);
			ClickSave(f);
			string id2 = null, pw2 = null;
			using (var g = NewLoadedForm()) { id2 = ((TextBox)GetField(g, "idTb")).Text; pw2 = ((TextBox)GetField(g, "pwdTb")).Text; }
			return (id2 == "acct_one" && pw2 == "pw_one_#42", "reloaded id=\"" + id2 + "\" pw=\"" + pw2 + "\"");
		});

		// --- 9. account rotation: the LAST manually saved pair wins on relaunch;
		//        unchecking 로그인 정보 저장 + 저장 clears the store. ---
		Case("account rotation: last saved pair wins; uncheck+save clears", f =>
		{
			ClearStore();
			SavePair(f, "acct_A", "pw_A_x");
			SavePair(f, "acct_B", "pw_B_y");
			string idR, pwR;
			using (var g = NewLoadedForm()) { idR = ((TextBox)GetField(g, "idTb")).Text; pwR = ((TextBox)GetField(g, "pwdTb")).Text; }
			bool lastWins = idR == "acct_B" && pwR == "pw_B_y";

			SetId(f, "acct_B"); ((TextBox)GetField(f, "pwdTb")).Text = "pw_B_y";
			SetCourse(f, "courseLb", 0); SetNup(f, 0, 9); SetNup(f, 1, 0); SetNup(f, 2, 10); SetNup(f, 3, 30);
			SetCheck(f, "chkR2", false); SetCheck(f, "chkSaveCred", false);
			ClickSave(f);
			bool cleared;
			using (var g = NewLoadedForm())
				cleared = ((TextBox)GetField(g, "idTb")).Text == "" && ((TextBox)GetField(g, "pwdTb")).Text == "";
			return (lastWins && cleared, "lastWins=" + lastWins + " clearedAfterUncheck=" + cleared);
		});

		// --- 10. saved credentials never land in golflog.txt or the app config. ---
		Case("saved credentials never written to golflog / config", f =>
		{
			ClearStore();
			SetId(f, "acct_secret_ID"); ((TextBox)GetField(f, "pwdTb")).Text = "pw_secret_VALUE";
			SetCourse(f, "courseLb", 0); SetNup(f, 0, 9); SetNup(f, 1, 0); SetNup(f, 2, 10); SetNup(f, 3, 30);
			SetCheck(f, "chkR2", false); SetCheck(f, "chkSaveCred", true);
			ClickSave(f);
			System.Diagnostics.Trace.Flush();
			string log = ""; try { log = System.IO.File.ReadAllText("golflog.txt"); } catch { }
			string cfg = "";
			try
			{
				foreach (var p in System.IO.Directory.GetFiles(AppContext.BaseDirectory, "*.config"))
					cfg += System.IO.File.ReadAllText(p);
			}
			catch { }
			bool clean = !log.Contains("acct_secret_ID") && !log.Contains("pw_secret_VALUE")
				&& !cfg.Contains("acct_secret_ID") && !cfg.Contains("pw_secret_VALUE");
			ClearStore();
			return (clean, "golflogLen=" + log.Length + " cfgHasCreds=" + (cfg.Contains("pw_secret_VALUE")));
		});

		Console.WriteLine();
		Console.WriteLine("================ " + _pass + " passed, " + _fail + " failed ================");
		return _fail == 0 ? 0 : 1;
	}

	// delete the real DPAPI store so a case starts from a known empty state
	private static void ClearStore()
	{
		try
		{
			string pw = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GolfCatch", "pw.dat");
			if (System.IO.File.Exists(pw)) System.IO.File.Delete(pw);
		}
		catch { }
	}

	// a fresh Form1 with Form1_Load run (simulates a close/relaunch)
	private static Form1 NewLoadedForm()
	{
		Form1 g = new Form1();
		SetField(g, "suppressStartDialogs", true);
		g.GetType().GetMethod("Form1_Load", NP).Invoke(g, new object[] { null, EventArgs.Empty });
		return g;
	}

	// invoke the real 저장 button handler (Btn_Click with a button named after btnNames[3])
	private static void ClickSave(Form1 f)
	{
		var btnNames = (string[])f.GetType().GetField("btnNames", NP).GetValue(f);
		var b = new Button { Name = btnNames[3] };
		f.GetType().GetMethod("Btn_Click", NP).Invoke(f, new object[] { b, EventArgs.Empty });
	}

	private static void SavePair(Form1 f, string id, string pw)
	{
		SetId(f, id);
		((TextBox)GetField(f, "pwdTb")).Text = pw;
		SetCourse(f, "courseLb", 0);
		SetNup(f, 0, 9); SetNup(f, 1, 0); SetNup(f, 2, 10); SetNup(f, 3, 30);
		SetCheck(f, "chkR2", false);
		SetCheck(f, "chkSaveCred", true);
		ClickSave(f);
	}

	// ---------------- harness ----------------
	private static void Case(string name, Func<Form1, (bool ok, string detail)> body)
	{
		Form1 f = null;
		try
		{
			f = new Form1();
			SetField(f, "suppressStartDialogs", true);
			// build the UI headlessly
			f.GetType().GetMethod("Form1_Load", NP).Invoke(f, new object[] { null, EventArgs.Empty });
			var (ok, detail) = body(f);
			Report(name, ok, detail);
			// safety: if a driver was somehow created, that itself is a failure
			if (GetField(f, "driver") != null)
			{
				Report(name + "  [driver must stay null]", false, "driver != null");
			}
		}
		catch (Exception ex)
		{
			Report(name, false, "EXCEPTION " + (ex.InnerException ?? ex));
		}
		finally
		{
			try { f?.Dispose(); } catch { }
		}
	}

	private static void InvokeSetUIStart(Form1 f)
	{
		var m = f.GetType().GetMethod("setUIStart", NP);
		var ps = m.GetParameters();
		m.Invoke(f, ps.Length == 0 ? null : new object[] { false });
	}

	private static string LastError(Form1 f) => (string)GetField(f, "lastStartError");

	private static object GetField(Form1 f, string name)
		=> f.GetType().GetField(name, NP)?.GetValue(f);

	private static void SetField(Form1 f, string name, object v)
		=> f.GetType().GetField(name, NP).SetValue(f, v);

	private static void SetId(Form1 f, string id)
		=> ((TextBox)GetField(f, "idTb")).Text = id;

	private static void SetCourse(Form1 f, string field, int idx)
		=> ((ListBox)GetField(f, field)).SelectedIndex = idx;

	private static void SetCheck(Form1 f, string field, bool on)
		=> ((CheckBox)GetField(f, field)).Checked = on;

	private static void SetNup(Form1 f, int i, int val)
	{
		var arr = (NumericUpDown[])GetField(f, "nupArr");
		arr[i].Value = Math.Min((int)arr[i].Maximum, Math.Max((int)arr[i].Minimum, val));
	}

	private static void Report(string name, bool ok, string detail)
	{
		Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + name + (string.IsNullOrEmpty(detail) ? "" : ("   [" + detail + "]")));
		if (ok) _pass++; else _fail++;
	}
}
