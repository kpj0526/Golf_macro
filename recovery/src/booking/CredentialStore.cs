using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace booking;

/// <summary>
/// Two local account slots (계정 1 / 계정 2), each an ID + password the user types into the
/// UI, plus the per-reservation slot selection (예약 1 / 예약 2 each pick a slot). Everything
/// is persisted DPAPI-encrypted (CurrentUser) at %LOCALAPPDATA%\GolfCatch\pw.dat. Nothing is
/// hard-coded and nothing is migrated from the original package config. The plaintext
/// ID/password are NEVER written to any config file, log, diagnostic capture, exception
/// message, or source.
///
/// Decrypted blob = newline-separated:
///   GCSTOREv2
///   &lt;slot0 id&gt;
///   &lt;slot0 password&gt;
///   &lt;slot1 id&gt;
///   &lt;slot1 password&gt;
///   &lt;예약1 slot: 0|1&gt;
///   &lt;예약2 slot: 0|1&gt;
/// A password may contain anything except '\n'. Legacy blobs are still read:
///   "&lt;id&gt;\n&lt;password&gt;"  -> slot0, or a single line -> slot0 password.
/// </summary>
internal static class CredentialStore
{
	private const string Marker = "GCSTOREv2";
	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GolfCatch.recovery.pwstore.v1");

	public static string DefaultPath => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"GolfCatch", "pw.dat");

	internal sealed class AccountData
	{
		public string[] Id = { "", "" };
		public string[] Pw = { "", "" };
		public int Res1Slot;
		public int Res2Slot;

		public int ClampSlot(int s) => (s == 1) ? 1 : 0;
	}

	public static bool Exists(string path = null)
	{
		try { return File.Exists(path ?? DefaultPath); }
		catch (Exception) { return false; }
	}

	public static void Save(AccountData d, string path = null)
	{
		path = path ?? DefaultPath;
		string dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir))
		{
			Directory.CreateDirectory(dir);
		}
		string blob = string.Join("\n", new[]
		{
			Marker,
			d.Id[0] ?? "", d.Pw[0] ?? "",
			d.Id[1] ?? "", d.Pw[1] ?? "",
			d.ClampSlot(d.Res1Slot).ToString(),
			d.ClampSlot(d.Res2Slot).ToString(),
		});
		byte[] plain = Encoding.UTF8.GetBytes(blob);
		byte[] enc = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
		File.WriteAllBytes(path, enc);
		Array.Clear(plain, 0, plain.Length);
	}

	public static bool TryLoad(out AccountData d, string path = null)
	{
		d = null;
		try
		{
			path = path ?? DefaultPath;
			if (!File.Exists(path))
			{
				return false;
			}
			byte[] dec = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
			string blob = Encoding.UTF8.GetString(dec);
			Array.Clear(dec, 0, dec.Length);
			string[] ln = blob.Split('\n');
			var a = new AccountData();
			if (ln.Length > 0 && ln[0] == Marker)
			{
				a.Id[0] = At(ln, 1); a.Pw[0] = At(ln, 2);
				a.Id[1] = At(ln, 3); a.Pw[1] = At(ln, 4);
				a.Res1Slot = a.ClampSlot(ParseInt(At(ln, 5)));
				a.Res2Slot = a.ClampSlot(ParseInt(At(ln, 6)));
			}
			else if (ln.Length >= 2)
			{
				a.Id[0] = ln[0]; a.Pw[0] = string.Join("\n", ln, 1, ln.Length - 1);
			}
			else
			{
				a.Pw[0] = blob;
			}
			d = a;
			return true;
		}
		catch (Exception)
		{
			d = null;
			return false;
		}
	}

	private static string At(string[] a, int i) => (i >= 0 && i < a.Length) ? a[i] : "";
	private static int ParseInt(string s) => int.TryParse(s, out int v) ? v : 0;

	// ---- back-compat single-pair helpers (operate on slot 0) ---------------------------

	public static void SaveCredentials(string id, string password, string path = null)
	{
		AccountData d = TryLoad(out AccountData ex, path) ? ex : new AccountData();
		d.Id[0] = id ?? "";
		d.Pw[0] = password ?? "";
		Save(d, path);
	}

	public static bool TryLoadCredentials(out string id, out string password, string path = null)
	{
		if (TryLoad(out AccountData d, path))
		{
			id = d.Id[0];
			password = d.Pw[0];
			return true;
		}
		id = null;
		password = null;
		return false;
	}

	public static void SavePassword(string password, string path = null)
		=> SaveCredentials(TryLoadCredentials(out string exId, out _, path) ? exId : "", password, path);

	public static bool TryLoadPassword(out string password, string path = null)
		=> TryLoadCredentials(out _, out password, path);

	public static void Clear(string path = null)
	{
		try
		{
			path = path ?? DefaultPath;
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (Exception)
		{
		}
	}
}
