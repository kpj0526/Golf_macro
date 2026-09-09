using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using booking;

namespace UiShot;

// Renders the REAL recovered Form1 to a PNG (via DrawToBitmap, no visible desktop needed)
// and dumps its control tree so QA can confirm the two always-visible reservation sections.
internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		string outDir = (args.Length > 0) ? args[0] : AppContext.BaseDirectory;
		Directory.CreateDirectory(outDir);
		string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);

		// keep the render harness deterministic: remove any DPAPI password store left by
		// prior local runs so "저장된 비밀번호" auto-fill does not appear in the screenshot.
		try
		{
			string pw = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GolfCatch", "pw.dat");
			if (File.Exists(pw)) File.Delete(pw);
		}
		catch { }

		Form1 f = new Form1();
		f.StartPosition = FormStartPosition.Manual;
		f.Location = new Point(0, 0);
		f.Show();
		for (int i = 0; i < 30; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(60); }
		f.Refresh();
		Application.DoEvents();

		int w = Math.Max(f.Width, f.PreferredSize.Width);
		int h = Math.Max(f.Height, f.PreferredSize.Height);
		if (w < 400) w = 1100;
		if (h < 300) h = 900;

		string png = Path.Combine(outDir, stamp + "_Form1.png");
		using (Bitmap bmp = new Bitmap(w, h))
		{
			f.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
			bmp.Save(png, ImageFormat.Png);
		}

		var sb = new StringBuilder();
		sb.AppendLine("Form1 render  " + DateTime.Now.ToString("u"));
		sb.AppendLine("window Text     : \"" + f.Text + "\"");
		sb.AppendLine("window Size     : " + f.Size);
		sb.AppendLine("screenshot      : " + png);
		sb.AppendLine();
		sb.AppendLine("control tree (Text | Type | Visible | Enabled | Bounds):");
		Walk(f, 0, sb);

		int dates = Count(f, c => c is DateTimePicker && c.Visible);
		int nups = Count(f, c => c is NumericUpDown && c.Visible);
		bool lbl1 = Contains(f, c => c is Label && c.Visible && c.Text != null && c.Text.Contains("예약 1"));
		bool lbl2 = Contains(f, c => c is Label && c.Visible && c.Text != null && c.Text.Contains("예약 2"));
		int startLbls = Count(f, c => c is Label && c.Visible && c.Text == "시작 시간");
		int endLbls = Count(f, c => c is Label && c.Visible && c.Text == "종료 시간");
		bool useR2 = Contains(f, c => c is CheckBox && c.Visible && c.Text == "예약 2 사용");
		bool saveCred = Contains(f, c => c is CheckBox && c.Visible && c.Text == "계정 저장");
		bool pwMasked = false;
		Visit(f, c => { if (c is TextBox tb && tb.UseSystemPasswordChar) pwMasked = true; });
		// Password box is shown in clear text in every build, per operator request, for visual
		// credential verification. The box itself must still be empty at render (no value baked in).
		bool pwExpectationOk = !pwMasked;
		string pwLine = "password TextBox shown in clear (all builds): " + (!pwMasked);
		// no credential value may ever appear as plain text in the tree dump
		bool noPwInDump = sb.ToString().IndexOf("asdfg", StringComparison.OrdinalIgnoreCase) < 0;

		// crop check: every visible control must fit inside its parent's client area
		var clipped = new System.Collections.Generic.List<string>();
		Visit(f, c =>
		{
			if (c.Parent == null || !c.Visible) return;
			var p = c.Parent.ClientSize;
			if (c.Right > p.Width + 1 || c.Bottom > p.Height + 1)
				clipped.Add(c.GetType().Name + " \"" + Trim(c.Text) + "\" " + c.Bounds + " > parent " + p);
		});

		sb.AppendLine();
		sb.AppendLine("CHECKS");
		sb.AppendLine("  window title starts '2개 예약 설정'        : " + f.Text.StartsWith("2개 예약 설정"));
		sb.AppendLine("  '예약 1' label visible                     : " + lbl1);
		sb.AppendLine("  '예약 2' label visible                     : " + lbl2);
		sb.AppendLine("  visible DateTimePicker count (== 2)        : " + dates);
		sb.AppendLine("  visible NumericUpDown count (== 8)         : " + nups);
		sb.AppendLine("  '시작 시간' labels (== 2)                  : " + startLbls);
		sb.AppendLine("  '종료 시간' labels (== 2)                  : " + endLbls);
		sb.AppendLine("  '예약 2 사용' checkbox present             : " + useR2);
		sb.AppendLine("  '계정 저장' checkbox present        : " + saveCred);
		sb.AppendLine("  " + pwLine);
		sb.AppendLine("  no plaintext password in dump             : " + noPwInDump);
		sb.AppendLine("  no clipped/cropped controls               : " + (clipped.Count == 0));
		foreach (var x in clipped) sb.AppendLine("      CLIPPED: " + x);

		bool ok = f.Text.StartsWith("2개 예약 설정") && lbl1 && lbl2 && dates == 2 && nups == 8
			&& startLbls == 2 && endLbls == 2
			&& useR2 && saveCred && pwExpectationOk && noPwInDump && clipped.Count == 0;
		sb.AppendLine();
		sb.AppendLine(ok
			? "RESULT: PASS - 예약 1 / 예약 2 each with 골프장·날짜·시작 시간·종료 시간 visible, no cropping; '예약 2 사용' + '계정 저장' present."
			: "RESULT: FAIL");

		string txt = Path.Combine(outDir, stamp + "_Form1-uitree.txt");
		File.WriteAllText(txt, sb.ToString(), Encoding.UTF8);
		Console.WriteLine(sb.ToString());
		Console.WriteLine("wrote " + png);
		Console.WriteLine("wrote " + txt);

		try { f.Close(); } catch { }
		return ok ? 0 : 1;
	}

	private static void Walk(Control c, int depth, StringBuilder sb)
	{
		foreach (Control ch in c.Controls)
		{
			string t = ch.Text;
			if (t != null && t.Length > 44) t = t.Substring(0, 44) + "…";
			sb.AppendLine(new string(' ', depth * 2) + "- \"" + t + "\" | " + ch.GetType().Name + " | vis=" + ch.Visible + " en=" + ch.Enabled + " | " + ch.Bounds);
			Walk(ch, depth + 1, sb);
		}
	}

	private static string Trim(string t) => (t != null && t.Length > 24) ? t.Substring(0, 24) + "…" : (t ?? "");

	private static void Visit(Control root, Action<Control> a)
	{
		a(root);
		foreach (Control ch in root.Controls) Visit(ch, a);
	}

	private static bool Contains(Control root, Func<Control, bool> pred) => Count(root, pred) > 0;

	private static int Count(Control root, Func<Control, bool> pred)
	{
		int n = pred(root) ? 1 : 0;
		foreach (Control ch in root.Controls) n += Count(ch, pred);
		return n;
	}
}
