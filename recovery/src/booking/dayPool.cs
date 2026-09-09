namespace booking;

internal class dayPool
{
	private int[] status;

	private int numDays;

	private int curIndex;

	public int clubIndex;

	private object lockObject = new object();

	public dayPool(int clubIndex_, int days)
	{
		clubIndex = clubIndex_;
		numDays = days;
		status = new int[days];
	}

	public int allocDay()
	{
		int num = 0;
		int num2 = -1;
		lock (lockObject)
		{
			while (num < numDays)
			{
				if (status[curIndex] == 0)
				{
					num2 = curIndex;
					status[curIndex] = 1;
					curIndex++;
					break;
				}
				num++;
				if (++curIndex >= numDays)
				{
					curIndex = 0;
				}
			}
			if (num2 >= 0 && curIndex == numDays)
			{
				curIndex = 0;
			}
		}
		return num2;
	}

	public void finishDay(int index)
	{
		status[index] = 1000;
	}

	public void returnDay(int index)
	{
		lock (lockObject)
		{
			status[index] = 0;
		}
	}

	public bool checkDone(int index)
	{
		return status[index] == 1000;
	}
}
