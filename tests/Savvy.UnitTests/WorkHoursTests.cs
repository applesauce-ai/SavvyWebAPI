using Savvy.Domain.Calculations;
using Xunit;

namespace Savvy.UnitTests;

public class WorkHoursTests
{
    private static DateTime Utc(int h, int m = 0) => new(2026, 7, 14, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void Full_eight_hours_no_break()
    {
        Assert.Equal(8.00m, WorkHours.ComputeHours(Utc(8), Utc(16), 0));
    }

    [Fact]
    public void Subtracts_unpaid_break()
    {
        // 8h - 30min = 7.5h
        Assert.Equal(7.50m, WorkHours.ComputeHours(Utc(8), Utc(16), 30));
    }

    [Fact]
    public void Rounds_to_two_dp_away_from_zero()
    {
        // 20 minutes = 0.333... hours -> 0.33
        Assert.Equal(0.33m, WorkHours.ComputeHours(Utc(8), Utc(8, 20), 0));
        // 50 minutes = 0.833... hours -> 0.83
        Assert.Equal(0.83m, WorkHours.ComputeHours(Utc(8), Utc(8, 50), 0));
    }

    [Fact]
    public void Exact_half_minute_midpoint_rounds_away_from_zero()
    {
        // 8h 0.5min = 8.00833.. no; craft an exact .005 hours midpoint:
        // 0.3 minutes = 0.005 hours exactly -> rounds to 0.01 (away from zero).
        var start = Utc(8);
        var end = start.AddSeconds(18); // 18s = 0.3 min = 0.005 h
        Assert.Equal(0.01m, WorkHours.ComputeHours(start, end, 0));
    }
}
