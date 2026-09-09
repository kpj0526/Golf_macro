namespace booking;

internal class bookInfo
{
	public string date;

	public int startTime;

	public int endTime;

	public int course;

	public string starter;

	public int starterIndex;

	// Recovery feature: per-condition desired tee-off time as HHMM (e.g. 905 = 09:05).
	// The nearest-time selector picks the tee with the smallest absolute minute
	// difference from this value. Falls back to startTime when not supplied.
	public int desiredTime;

	public bookInfo(int course_p, string starter_p, int starter_index, string date_p, int start, int end)
		: this(course_p, starter_p, starter_index, date_p, start, end, 0)
	{
	}

	public bookInfo(int course_p, string starter_p, int starter_index, string date_p, int start, int end, int desired_hhmm)
	{
		date = date_p;
		startTime = start;
		endTime = end;
		course = course_p;
		starter = starter_p;
		starterIndex = starter_index;
		desiredTime = ((desired_hhmm > 0) ? desired_hhmm : start);
	}

	public override string ToString()
	{
		return "Req " + date + " desired " + desiredTime + " [win " + startTime + "-" + endTime + "] course " + course + " " + starter;
	}
}
