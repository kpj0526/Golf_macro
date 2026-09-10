using System;
using System.Collections.Generic;
using System.Text;
using OpenQA.Selenium;

namespace booking;

/// <summary>
/// Recovery feature: from every available tee row (for the requested starter), choose the
/// tee whose minute-of-day is closest to the desired time.
///   Primary key : smallest absolute minute difference from the desired time.
///   Tie-break 1 : earlier tee time.
///   Tie-break 2 : lower position in the row list (stable).
/// The desired time, the chosen slot and the delta are logged.
/// </summary>
internal static class TeeSelector
{
	public static int ToMinutes(int hhmm)
	{
		return hhmm / 100 * 60 + hhmm % 100;
	}

	public static string FromMinutes(int m)
	{
		return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
	}

	public static IWebElement PickNearest(IList<IWebElement> rows, bookInfo req, Action<string> log,
		out string chosenHHmm, out int deltaMinutes)
	{
		chosenHHmm = null;
		deltaMinutes = -1;

		// nearest-time target = the desired time (no selectable window / range filter)
		int target = ToMinutes(req.desiredTime);

		IWebElement best = null;
		int bestDelta = int.MaxValue;
		int bestMin = int.MaxValue;
		int bestIdx = int.MaxValue;
		int considered = 0;
		bool tieBrokenByEarlier = false;

		for (int i = 0; i < rows.Count; i++)
		{
			if (!TryParseRow(rows[i], out int mins, out string starter))
			{
				continue;
			}
			if (!string.IsNullOrEmpty(req.starter) && req.starter != "NA" &&
				!string.IsNullOrEmpty(starter) && starter != req.starter)
			{
				continue;
			}
			considered++;
			int d = Math.Abs(mins - target);

			bool isBetter = d < bestDelta
				|| (d == bestDelta && mins < bestMin)
				|| (d == bestDelta && mins == bestMin && i < bestIdx);

			if (isBetter)
			{
				if (d == bestDelta && best != null && mins < bestMin)
				{
					tieBrokenByEarlier = true;
				}
				best = rows[i];
				bestDelta = d;
				bestMin = mins;
				bestIdx = i;
			}
			else if (d == bestDelta && best != null)
			{
				// an equally-distant but later (or equal) candidate; policy keeps the earlier one
				tieBrokenByEarlier = true;
			}
		}

		if (best == null)
		{
			log("tee-select: no eligible tee (rows=" + rows.Count + ", starter=" + req.starter + ")");
			return null;
		}

		chosenHHmm = FromMinutes(bestMin);
		deltaMinutes = bestDelta;
		log("tee-select: desired=" + FromMinutes(target)
			+ " chosen=" + chosenHHmm
			+ " deltaMin=" + bestDelta
			+ " eligibleRows=" + considered
			+ (tieBrokenByEarlier ? " [equal-distance -> chose the EARLIER tee time]" : "")
			+ " (rule: min |delta| from desired, then earlier time, then list order)");
		return best;
	}

	private static bool TryParseRow(IWebElement row, out int minutes, out string starter)
	{
		minutes = 0;
		starter = null;
		try
		{
			string oc = row.GetAttribute("onclick");
			if (string.IsNullOrEmpty(oc))
			{
				return false;
			}
			string[] a = oc.Split(',');
			if (a.Length < 2)
			{
				return false;
			}
			// Sun Valley tee row onclick: field [1] = time e.g. '0850', field [3] = starter/course.
			string digits = Digits(a[1]);
			if (digits.Length < 3)
			{
				return false;
			}
			int hhmm = int.Parse(digits);
			minutes = hhmm / 100 * 60 + hhmm % 100;
			if (a.Length > 3)
			{
				starter = a[3].Replace("'", "").Trim();
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string Digits(string s)
	{
		StringBuilder sb = new StringBuilder();
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
