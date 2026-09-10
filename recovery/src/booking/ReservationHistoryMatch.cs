using System;

namespace booking;

/// <summary>
/// Recovery feature (pure, unit-tested): decide, from the raw text of the account's
/// reservation-history page, whether a booking for one specific request is present.
///
/// Used only by the SunValley final-confirmation FALLBACK: if the completion alert is
/// missing / unreadable, or the site navigated to an unsupported page, or the confirm UI
/// changed, the caller re-loads the reservation history a few times and asks this whether
/// the just-attempted reservation now shows up. A match requires the page to look like a
/// real history page AND the requested date, the chosen tee time, the selected starter and
/// (when known) the selected course to all appear. Anything less is treated as uncertain by
/// the caller — never a faked success.
/// </summary>
internal static class ReservationHistoryMatch
{
	private static readonly string[] PositiveMarkers =
	{
		"예약내역", "예약 내역", "나의 예약", "마이페이지", "예약확인", "예약 확인", "reservation"
	};

	private static readonly string[] ErrorMarkers =
	{
		"페이지를 찾을 수 없", "찾을 수 없습니다", "지원되지 않", "지원하지 않", "잘못된 접근",
		"오류가 발생", "not found", "error 404", "bad request", "access denied"
	};

	/// <summary>True when <paramref name="pageSource"/> plausibly is the reservation-history page.</summary>
	public static bool LooksLikeHistoryPage(string pageSource)
	{
		if (string.IsNullOrWhiteSpace(pageSource) || pageSource.Length < 40)
		{
			return false;
		}
		foreach (string bad in ErrorMarkers)
		{
			if (pageSource.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return false;
			}
		}
		foreach (string good in PositiveMarkers)
		{
			if (pageSource.IndexOf(good, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// True when the history page shows a row for this exact request.
	/// <paramref name="yyyymmdd"/> is 8 digits; <paramref name="chosenHHmm"/> is "HH:MM"
	/// (or "HHMM"). Requires ALL of: a real history page, the requested date, the chosen
	/// tee time, the selected course (when a course name is known) AND the selected starter
	/// (whenever the starter is a real name, i.e. not null/blank/"NA"). Course and starter
	/// are each mandatory when present - a course match does NOT waive the starter check.
	/// </summary>
	public static bool Matches(string pageSource, string yyyymmdd, string chosenHHmm,
		string starter, string courseName, out string detail)
	{
		bool pageOk = LooksLikeHistoryPage(pageSource);
		bool dateHit = pageOk && DateHit(pageSource, yyyymmdd);
		bool timeHit = pageOk && TimeHit(pageSource, chosenHHmm);
		bool haveCourse = !string.IsNullOrWhiteSpace(courseName);
		bool haveStarter = !string.IsNullOrWhiteSpace(starter) && starter != "NA";
		bool courseHit = haveCourse && pageOk && pageSource.IndexOf(courseName, StringComparison.Ordinal) >= 0;
		bool starterHit = haveStarter && pageOk && pageSource.IndexOf(starter, StringComparison.Ordinal) >= 0;

		bool ok = pageOk && dateHit && timeHit
			&& (!haveCourse || courseHit)
			&& (!haveStarter || starterHit);

		detail = "pageOk=" + pageOk + " date=" + dateHit + " time=" + timeHit
			+ " course=" + (haveCourse ? courseHit.ToString() : "n/a")
			+ " starter=" + (haveStarter ? starterHit.ToString() : "n/a");
		return ok;
	}

	private static bool DateHit(string page, string yyyymmdd)
	{
		if (string.IsNullOrEmpty(yyyymmdd) || yyyymmdd.Length != 8)
		{
			return false;
		}
		string y = yyyymmdd.Substring(0, 4), m = yyyymmdd.Substring(4, 2), d = yyyymmdd.Substring(6, 2);
		return page.Contains(yyyymmdd)
			|| page.Contains(y + "-" + m + "-" + d)
			|| page.Contains(y + "." + m + "." + d)
			|| page.Contains(y + "/" + m + "/" + d);
	}

	private static bool TimeHit(string page, string chosenHHmm)
	{
		if (string.IsNullOrWhiteSpace(chosenHHmm))
		{
			return false;
		}
		string colon = chosenHHmm.Contains(":") ? chosenHHmm : Insert(chosenHHmm);
		string digits = chosenHHmm.Replace(":", "");
		return page.Contains(colon) || (digits.Length == 4 && page.Contains(digits));
	}

	private static string Insert(string hhmm)
		=> (hhmm != null && hhmm.Length == 4) ? hhmm.Substring(0, 2) + ":" + hhmm.Substring(2, 2) : hhmm;
}
