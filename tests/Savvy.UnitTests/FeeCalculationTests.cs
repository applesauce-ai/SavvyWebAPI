using Savvy.Domain.Calculations;
using Xunit;

namespace Savvy.UnitTests;

public class FeeCalculationTests
{
    [Fact]
    public void Computes_gross_fee_and_net()
    {
        // 7.50h × 25.00 = 187.50 gross; fee = 187.50×0.15 + 5.00 = 33.125 -> 33.13; net = 154.37.
        var result = FeeCalculation.ComputeLine(hours: 7.50m, rate: 25.00m, feePercentage: 0.15m, fixedFee: 5.00m);

        Assert.Equal(187.50m, result.Gross);
        Assert.Equal(33.13m, result.Fee);   // away-from-zero on the .125 midpoint
        Assert.Equal(154.37m, result.Net);
    }

    [Fact]
    public void Net_equals_gross_minus_fee()
    {
        var r = FeeCalculation.ComputeLine(10m, 20m, 0.10m, 2.50m);
        Assert.Equal(r.Gross - r.Fee, r.Net);
    }

    [Fact]
    public void Zero_fees_gives_net_equal_to_gross()
    {
        var r = FeeCalculation.ComputeLine(8m, 30m, 0m, 0m);
        Assert.Equal(240.00m, r.Gross);
        Assert.Equal(0m, r.Fee);
        Assert.Equal(240.00m, r.Net);
    }

    [Fact]
    public void Fixed_fee_only()
    {
        var r = FeeCalculation.ComputeLine(2m, 10m, 0m, 7.50m);
        Assert.Equal(20.00m, r.Gross);
        Assert.Equal(7.50m, r.Fee);
        Assert.Equal(12.50m, r.Net);
    }

    [Fact]
    public void Gross_rounds_away_from_zero_at_line_level()
    {
        // 0.1 × 1.25 = 0.125 -> 0.13
        var r = FeeCalculation.ComputeLine(0.1m, 1.25m, 0m, 0m);
        Assert.Equal(0.13m, r.Gross);
    }
}
