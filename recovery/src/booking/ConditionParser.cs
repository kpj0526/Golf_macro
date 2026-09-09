using System;

namespace booking;

/// <summary>
/// Recovery feature (pure, unit-tested): parse one fixed condition slot.
///
/// Current format (5 fields):  "course,yyyymmdd,startHHMM,endHHMM,starter"
///   start/end : inclusive tee-off time window, HHMM (non-digits stripped). start &lt;= end.
///
/// Legacy format (4 fields):   "course,yyyymmdd,desiredHHMM,starter"
///   Migrated: startTime = desired ; endTime = desired + 2h (clamped to 23:59).
///
/// course  : a Sun Valley course name; unknown -> index 0
/// date    : 8 digits yyyymmdd (any separators stripped)
/// starter : starter name; looked up in starterNames (may be -1 / "NA")
/// Returns null for an unset ("&lt;...&gt;"), blank, or malformed line (incl. start &gt; end).
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

			int start, end;
			string starter;

			if (p.Length >= 5)
			{
				// 5-field: start, end, starter
				if (!TryHHMM(p[2], out start) || !TryHHMM(p[3], out end))
				{
					return null;
				}
				if (ToMinutes(start) > ToMinutes(end))
				{
					return null; // start must not be later than end
				}
				starter = p[4].Trim();
			}
			else
			{
				// 4-field legacy: single desired time -> migrate to [desired, desired+2h]
				if (!TryHHMM(p[2], out int desired))
				{
					return null;
				}
				start = desired;
				int endMin = Math.Min(23 * 60 + 59, ToMinutes(desired) + LegacyWindowMinutes);
				end = FromMinutes(endMin);
				starter = p[3].Trim();
			}

			int starterIdx = (starterNames != null) ? Array.IndexOf(starterNames, starter) : -1;

			// desiredTime target for the nearest-time pick = the window start.
			return new bookInfo(courseIdx, starter, starterIdx, date, start, end, start);
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
