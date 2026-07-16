using Savvy.Watchdog;
using Savvy.Watchdog.Alerting;
using Xunit;

namespace Savvy.UnitTests;

public class HealthStateTrackerTests
{
    [Fact]
    public void First_healthy_probe_does_not_alert()
    {
        var tracker = new HealthStateTracker(failureThreshold: 2);
        var outcome = tracker.Observe(healthy: true);

        Assert.Equal(HealthState.Up, outcome.State);
        Assert.False(outcome.ShouldAlert);
    }

    [Fact]
    public void Alerts_down_only_after_threshold_consecutive_failures()
    {
        var tracker = new HealthStateTracker(failureThreshold: 2);
        tracker.Observe(true); // Up

        var first = tracker.Observe(false);   // 1 failure — below threshold
        Assert.False(first.ShouldAlert);
        Assert.Equal(HealthState.Up, first.State);

        var second = tracker.Observe(false);  // 2 failures — threshold reached
        Assert.True(second.ShouldAlert);
        Assert.Equal(AlertLevel.Down, second.Level);
        Assert.Equal(HealthState.Down, second.State);
    }

    [Fact]
    public void Does_not_alert_repeatedly_while_down()
    {
        var tracker = new HealthStateTracker(failureThreshold: 1);
        tracker.Observe(true);
        var down = tracker.Observe(false); // threshold 1 -> Down immediately
        Assert.True(down.ShouldAlert);

        var stillDown = tracker.Observe(false);
        Assert.False(stillDown.ShouldAlert); // no repeat alerts
        Assert.Equal(HealthState.Down, stillDown.State);
    }

    [Fact]
    public void Alerts_recovery_after_being_down()
    {
        var tracker = new HealthStateTracker(failureThreshold: 1);
        tracker.Observe(true);
        tracker.Observe(false); // Down

        var recovered = tracker.Observe(true);
        Assert.True(recovered.ShouldAlert);
        Assert.Equal(AlertLevel.Recovered, recovered.Level);
        Assert.Equal(HealthState.Up, recovered.State);

        var steady = tracker.Observe(true);
        Assert.False(steady.ShouldAlert); // steady healthy -> no alert
    }

    [Fact]
    public void A_single_failure_then_recovery_never_alerts_with_threshold_two()
    {
        var tracker = new HealthStateTracker(failureThreshold: 2);
        tracker.Observe(true);
        var blip = tracker.Observe(false);   // transient
        var back = tracker.Observe(true);    // recovered before threshold

        Assert.False(blip.ShouldAlert);
        Assert.False(back.ShouldAlert); // never declared Down, so no recovery alert either
    }
}
