namespace booking;

public enum OpResult
{
	Success = 0,
	NotOpen = 1,
	NoSpace = 2,
	NoProperTime = 3,
	ReserveFail = 4,
	Overbook = 5,
	FalalError = 6,
	BookOneSuccess = 7,
	DuplicateFail = 8,
	DuplicateLogin = 9,
	FoundSlot = 10,
	OneFinish = 11,
	Fail = 1000
}
