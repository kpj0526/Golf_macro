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
		Application.SetHighDpiMode(HighDpiMode.SystemAware);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new Form1(qaAutoStart));
		Environment.Exit(0);
	}
}
