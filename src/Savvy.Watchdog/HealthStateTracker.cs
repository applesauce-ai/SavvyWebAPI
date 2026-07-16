using Savvy.Watchdog.Alerting;

namespace Savvy.Watchdog;

public enum HealthState { Unknown, Up, Down }

/// <summary>The outcome of observing one health probe.</summary>
public readonly record struct ProbeOutcome(HealthState State, bool ShouldAlert, AlertLevel Level);

/// <summary>
/// Pure state machine deciding when to alert. Debounces transient failures via a threshold, and
/// only alerts on genuine transitions (Up→Down, Down→Up) — never repeatedly while the state is
/// steady. Kept side-effect free so it is fully unit-testable.
/// </summary>
public sealed class HealthStateTracker
{
    private readonly int _failureThreshold;
    private int _consecutiveFailures;

    public HealthState State { get; private set; } = HealthState.Unknown;

    public HealthStateTracker(int failureThreshold)
        => _failureThreshold = Math.Max(1, failureThreshold);

    public ProbeOutcome Observe(bool healthy)
    {
        if (healthy)
        {
            _consecutiveFailures = 0;

            // Alert recovery only if we had previously declared the API Down.
            var recovered = State == HealthState.Down;
            State = HealthState.Up;
            return new ProbeOutcome(State, recovered, AlertLevel.Recovered);
        }

        _consecutiveFailures++;

        // Declare Down (and alert once) when the threshold is reached and we aren't already Down.
        if (_consecutiveFailures >= _failureThreshold && State != HealthState.Down)
        {
            State = HealthState.Down;
            return new ProbeOutcome(State, true, AlertLevel.Down);
        }

        return new ProbeOutcome(State, false, AlertLevel.Down);
    }
}
