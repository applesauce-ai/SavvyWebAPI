namespace Savvy.Domain.Calculations;

/// <summary>
/// Pure hours calculation for a worked period. Kept in the domain and side-effect free so the
/// rounding behaviour is trivially unit-testable (see Section 7).
/// </summary>
public static class WorkHours
{
    /// <summary>
    /// Worked hours = (end − start − unpaid breaks), rounded to 2 decimal places using
    /// away-from-zero rounding (consistent with the payment-run money rounding).
    /// Callers are expected to have validated that the period is positive and that breaks do
    /// not exceed the worked span.
    /// </summary>
    public static decimal ComputeHours(DateTime workedStartUtc, DateTime workedEndUtc, int unpaidBreakMinutes)
    {
        var grossMinutes = (decimal)(workedEndUtc - workedStartUtc).TotalMinutes;
        var netMinutes = grossMinutes - unpaidBreakMinutes;
        var hours = netMinutes / 60m;
        return Math.Round(hours, 2, MidpointRounding.AwayFromZero);
    }
}
