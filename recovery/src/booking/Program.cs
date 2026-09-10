using System;
using System.Windows.Forms;

namespace booking;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		bool qaAutoStart = false;
		foreach (string arg in args)
		{
			if (string.Equals(arg, "--qa-auto-start", StringComparison.OrdinalIgnoreCase))
				qaAutoStart = true;
		}

		// One-time migration: delete a stale "Windows 로그인 후 자동 실행" (HKCU\...\Run
		// "GolfCatchBooking") entry left by a build that still had the feature.
		// Creates nothing and does not re-enable auto-run.
		LegacyAutoRunCleanup.Run();

		Application.SetHighDpiMode(HighDpiMode.SystemAware);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new Form1(qaAutoStart));
		Environment.Exit(0);
	}
}
