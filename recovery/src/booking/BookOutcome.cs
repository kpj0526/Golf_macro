namespace booking;

/// <summary>
/// Recovery feature: rich per-condition result used by the sequential hard gate.
/// Condition 2 may start ONLY when condition 1's outcome satisfies <see cref="GateSatisfied"/>.
/// </summary>
internal class BookOutcome
{
	public OpResult Result = OpResult.Fail;

	public bool SlotFound;

	public string DesiredTee;   // "HH:mm"

	public string ChosenTee;    // "HH:mm"

	public int DeltaMinutes = -1;

	public bool Submitted;      // a real reservation submit was attempted (never true in a DIAGNOSTIC build)

	public string ConfirmationId; // server confirmation / reservation number; null if none

	public bool HistoryVerified;  // the reservation was found in the account's reservation history

	public string Detail;

	/// <summary>
	/// The hard gate. True only when a reservation was actually submitted, a server
	/// confirmation ID came back, AND it was confirmed in the reservation history.
	/// In a DIAGNOSTIC build nothing is submitted, so this is always false and
	/// condition 2 is always skipped by policy.
	/// </summary>
	public bool GateSatisfied => Submitted && !string.IsNullOrEmpty(ConfirmationId) && HistoryVerified;

	public string GateReason(bool diagnosticMode)
	{
		if (GateSatisfied)
		{
			return "confirmed";
		}
		if (diagnosticMode)
		{
			return "diagnostic no-submit build: no reservation submitted, so no server confirmation ID exists";
		}
		if (!SlotFound)
		{
			return "no matching tee slot (" + (Detail ?? Result.ToString()) + ")";
		}
		if (!Submitted)
		{
			return "reservation not submitted (" + (Detail ?? Result.ToString()) + ")";
		}
		if (string.IsNullOrEmpty(ConfirmationId))
		{
			return "no server confirmation ID returned after submit";
		}
		if (!HistoryVerified)
		{
			return "reservation not found in reservation history";
		}
		return Result.ToString();
	}
}
