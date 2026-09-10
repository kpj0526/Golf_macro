using System;
using Microsoft.Win32;

namespace booking;

/// <summary>
/// "Windows 로그인 후 자동 실행" support.
///
/// Scope / limits (by design):
///   * This registers a per-user value under
///     HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run.
///     Windows launches it ONLY after the machine has booted AND this same Windows
///     user has interactively logged in. It is not a service and not a scheduled
///     task: it never runs at boot without a login, and it never runs for a
///     different user.
///   * The registry value is exactly:  "&lt;full path to booking.exe&gt;" --auto-run
///     No ID, no password and no other credential-derived value is ever placed in
///     the registry value or on the command line. Credentials are read only from
///     the existing DPAPI (CurrentUser) store at run time.
///   * There is no watchdog. If the user (or anything) force-closes the window or
///     kills booking.exe, the automated run simply stops and is not restarted
///     until the next Windows login.
///   * A single-instance mutex (see Program.cs) means a second launch — manual
///     while an automated run is active, or vice-versa — exits immediately.
/// </summary>
internal static class AutoRun
{
	// Command-line switch that puts the app into unattended headless mode.
	public const string AutoRunArg = "--auto-run";

	// Session-local single-instance mutex name (per Windows user session).
	public const string SingleInstanceMutex = "Local\\GolfCatch.booking.singleinstance";

	private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
	private const string RunValueName = "GolfCatchBooking";

	/// <summary>Register "&lt;exePath&gt; --auto-run" under HKCU\...\Run. No credentials are written.</summary>
	public static void Enable(string exePath)
	{
		using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
			?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
		key.SetValue(RunValueName, "\"" + exePath + "\" " + AutoRunArg, RegistryValueKind.String);
	}

	/// <summary>Remove the HKCU\...\Run value if present.</summary>
	public static void Disable()
	{
		using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
		key?.DeleteValue(RunValueName, throwOnMissingValue: false);
	}

	/// <summary>True when the HKCU\...\Run value exists (regardless of which exe path it points at).</summary>
	public static bool IsEnabled()
	{
		try
		{
			using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
			return key?.GetValue(RunValueName) != null;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
