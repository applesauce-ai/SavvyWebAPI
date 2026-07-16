namespace Savvy.Domain.Calculations;

/// <summary>The computed money amounts for one payment-run line.</summary>
public readonly record struct LineAmounts(decimal Gross, decimal Fee, decimal Net);

/// <summary>
/// Pure payment math. Rounding is applied at the line level (2dp, away-from-zero) and the run
/// totals are the sum of the rounded line amounts — see PROJECT_PLAN §5/§7. Keeping this here
/// makes the rounding contract unit-testable (Section 7).
/// </summary>
public static class FeeCalculation
{
    /// <param name="feePercentage">Fee as a fraction of gross, e.g. 0.15 = 15%.</param>
    /// <param name="fixedFee">Flat fee added per timesheet/line.</param>
    public static LineAmounts ComputeLine(decimal hours, decimal rate, decimal feePercentage, decimal fixedFee)
    {
        var gross = Round(hours * rate);
        var fee = Round(gross * feePercentage + fixedFee);
        var net = gross - fee; // gross and fee are already 2dp, so net is exact
        return new LineAmounts(gross, fee, net);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
