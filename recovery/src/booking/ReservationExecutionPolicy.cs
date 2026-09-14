using System;

namespace booking;

internal static class ReservationExecutionPolicy
{
	public static bool UseParallelSessions(bool hasSecondReservation, CredentialStore.AccountData accounts)
	{
		if (!hasSecondReservation || accounts == null)
			return false;

		int firstSlot = accounts.ClampSlot(accounts.Res1Slot);
		int secondSlot = accounts.ClampSlot(accounts.Res2Slot);
		string firstId = (accounts.Id[firstSlot] ?? "").Trim();
		string secondId = (accounts.Id[secondSlot] ?? "").Trim();
		return firstId.Length != 0 && secondId.Length != 0
			&& !string.Equals(firstId, secondId, StringComparison.OrdinalIgnoreCase);
	}
}
