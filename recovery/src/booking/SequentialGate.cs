namespace booking;

/// <summary>
/// Sequential two-condition policy.
///   - A normal booking outcome (including closed, not-open, no slot, or a diagnostic
///     no-submit result) must not prevent condition 2 from being checked.
///   - Condition 2 is skipped only when condition 1 ended with a transport/navigation
///     failure, because the shared browser session is not reliable in that case.
/// </summary>
internal static class SequentialGate
{
	public static bool ShouldRunCondition2(BookOutcome condition1)
	{
		return condition1 != null
			&& condition1.Result != OpResult.FalalError
			&& condition1.Result != OpResult.Fail;
	}

	public static string Decision(BookOutcome condition1, bool diagnosticMode)
	{
		if (condition1 == null)
		{
			return "Condition 1 produced no outcome -> Condition 2 SKIPPED, run terminated.";
		}
		if (ShouldRunCondition2(condition1))
		{
			return "Condition 1 result=" + condition1.Result
				+ " -> starting Condition 2 (condition 1 confirmation is not required).";
		}
		return "Condition 1 ended with a browser/navigation error (" + condition1.GateReason(diagnosticMode)
			+ ") -> Condition 2 SKIPPED, run terminated.";
	}
}
