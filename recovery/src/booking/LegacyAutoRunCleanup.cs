using System;
using Microsoft.Win32;

namespace booking;

/// <summary>
/// One-time cleanup for the removed "Windows 로그인 후 자동 실행" feature.
///
/// Builds up to and including v0.4.0 wrote a per-user startup entry
///   HKCU\Software\Microsoft\Windows\CurrentVersion\Run  value "GolfCatchBooking"
///     = "&lt;booking.exe&gt;" --auto-run
/// The feature (and the --auto-run path) is gone, but a PC updated from such a
/// build still carries that stale value, which keeps launching booking.exe on
/// every Windows login. On the app's first manual launch this deletes ONLY that
/// one named value.
///
/// It never creates the Run key or any value, never writes anything, and does
/// not bring back --auto-run. Deleting a value that is not there is a no-op, so
/// calling this on every manual launch is harmless: after the first launch the
/// stale value is gone and later calls do nothing.
/// </summary>
internal static class LegacyAutoRunCleanup
{
	// HKCU-relative path; overridable only so the unit test can target a scratch key.
	internal const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	// The exact value name older builds registered.
	internal const string LegacyValueName = "GolfCatchBooking";

	/// <summary>
	/// Remove the stale HKCU Run value a previous version may have left.
	/// Returns true when a value was found and deleted; false when there was
	/// nothing to do. Best-effort: any error is swallowed and returns false so a
	/// locked or access-denied Run key can never block a normal manual launch.
	/// </summary>
	public static bool Run(string runKeyPath = RunKeyPath)
	{
		try
		{
			// writable:true but WITHOUT create: if the Run key does not exist we
			// leave it that way rather than materialising it.
			using RegistryKey key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
			if (key == null)
			{
				return false;
			}
			if (key.GetValue(LegacyValueName) == null)
			{
				return false;
			}
			key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
