using System;
using System.Threading;
using System.Windows.Forms;

namespace booking;

internal static class Program
{
	// Held for the whole process lifetime so the single-instance lock stays owned.
	private static Mutex _singleInstance;

	[STAThread]
	private static void Main(string[] args)
	{
		bool qaAutoStart = false;
		bool autoRun = false;
		foreach (string arg in args)
		{
			if (string.Equals(arg, "--qa-auto-start", StringComparison.OrdinalIgnoreCase))
				qaAutoStart = true;
			if (string.Equals(arg, AutoRun.AutoRunArg, StringComparison.OrdinalIgnoreCase))
				autoRun = true;
		}

		// Single instance per Windows user session: if the app is already running
		// (manual or an automated run), this launch exits immediately.
		_singleInstance = new Mutex(initiallyOwned: true, AutoRun.SingleInstanceMutex, out bool createdNew);
		if (!createdNew)
		{
			Environment.Exit(0);
			return;
		}

		Application.SetHighDpiMode(HighDpiMode.SystemAware);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new Form1(qaAutoStart, autoRun));
		Environment.Exit(0);
	}
}
