using System;

namespace booking;

/// <summary>
/// Recovery feature (pure, unit-tested): parse one fixed condition slot.
///
/// Format (4 fields):  "course,yyyymmdd,desiredHHMM,starter"
///   desired : wanted tee-off time, HHMM (non-digits stripped).
///
/// A 5-field value "course,yyyymmdd,startHHMM,endHHMM,starter" saved by an older build is
/// still accepted: only the start field is read (as the desired time); the end field is
/// ignored.
///
/// course  : a Sun Valley course name; unknown -> index 0
/// date    : 8 digits yyyymmdd (any separators stripped)
/// starter : starter name; looked up in starterNames (may be -1 / "NA")
/// Returns null for an unset ("&lt;...&gt;"), blank, or malformed line.
/// </summary>
internal static class ConditionParser
{
	// legacy single-time -> compatible window width (minutes)
	public const int LegacyWindowMinutes = 120;

	public static bookInfo Parse(string line, string[] courseNames, string[] starterNames)
	{
		if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("<"))
		{
			return null;
		}
		try
		{
			string[] p = line.Split(',');
			if (p.Length < 4)
			{
				return null;
			}

			string courseName = p[0].Trim();
			int courseIdx = (courseNames != null) ? Array.IndexOf(courseNames, courseName) : -1;
			if (courseIdx < 0)
			{
				courseIdx = 0;
			}

			string date = DigitsOnly(p[1]);
			if (date.Length != 8 || !int.TryParse(date, out _))
			{
				return null;
			}

			int desired;
			string starter;

			if (p.Length >= 5)
			{
				// 5-field value from an older build: read the start field only, ignore the end.
				if (!TryHHMM(p[2], out desired))
				{
					return null;
				}
				starter = p[4].Trim();
			}
			else
			{
				if (!TryHHMM(p[2], out desired))
				{
					return null;
				}
				starter = p[3].Trim();
			}

			int starterIdx = (starterNames != null) ? Array.IndexOf(starterNames, starter) : -1;

			// No time window: the desired time is the nearest-time target.
			return new bookInfo(courseIdx, starter, starterIdx, date, desired, desired, desired);
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static int ToMinutes(int hhmm) => hhmm / 100 * 60 + hhmm % 100;

	public static int FromMinutes(int m) => m / 60 * 100 + m % 60;

	private static bool TryHHMM(string s, out int hhmm)
	{
		hhmm = 0;
		string d = DigitsOnly(s);
		if (d.Length == 0 || !int.TryParse(d, out int v))
		{
			return false;
		}
		if (v < 0 || v > 2359 || v % 100 > 59)
		{
			return false;
		}
		hhmm = v;
		return true;
	}

	private static string DigitsOnly(string s)
	{
		if (s == null)
		{
			return "";
		}
		var sb = new System.Text.StringBuilder();
		foreach (char c in s)
		{
			if (c >= '0' && c <= '9')
			{
				sb.Append(c);
			}
		}
		return sb.ToString();
	}
}
