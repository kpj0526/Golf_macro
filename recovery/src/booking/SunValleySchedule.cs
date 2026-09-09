using System;

namespace booking;

// Pure scheduling policy, kept separate so it can be tested without a browser,
// login, or any reservation submission.
internal static class SunValleySchedule
{
	// "Two weeks before Monday/Friday" is calendar-week based, rather than a
	// fixed number of days back from the requested play date.
	public static bool TryGetSingleCheckTime(bookInfo request, out DateTime checkAt, out string rule)
	{
		DateTime requested = new DateTime(
			int.Parse(request.date.Substring(0, 4)),
			int.Parse(request.date.Substring(4, 2)),
			int.Parse(request.date.Substring(6, 2)));
		DateTime weekMonday = requested.AddDays(-(((int)requested.DayOfWeek + 6) % 7));
		bool weekend = requested.DayOfWeek == DayOfWeek.Saturday || requested.DayOfWeek == DayOfWeek.Sunday;
		DateTime openAt;

		switch (request.course)
		{
		case 0: // Seorak: all dates, two-weeks-before Monday 09:00
			openAt = weekMonday.AddDays(-14).AddHours(9);
			rule = "Seorak: two-weeks-before Monday 09:00";
			break;
		case 1: // Iljuk: weekday Monday, weekend Friday, both 09:30
			openAt = weekMonday.AddDays(weekend ? -10 : -14).AddHours(9).AddMinutes(30);
			rule = "Iljuk: " + (weekend ? "two-weeks-before Friday" : "two-weeks-before Monday") + " 09:30";
			break;
		case 2: // Dongwon: weekday Monday, weekend Friday, both 10:00
			openAt = weekMonday.AddDays(weekend ? -10 : -14).AddHours(10);
			rule = "Dongwon: " + (weekend ? "two-weeks-before Friday" : "two-weeks-before Monday") + " 10:00";
			break;
		default: // Yeoju: no operator-confirmed rule
			checkAt = DateTime.MinValue;
			rule = null;
			return false;
		}

		// Relaxed operator policy: one calendar load one minute after the opening.
		checkAt = openAt.AddMinutes(1);
		return true;
	}

	public static bool WaitRequired(DateTime now, DateTime checkAt)
	{
		return now < checkAt;
	}
}
