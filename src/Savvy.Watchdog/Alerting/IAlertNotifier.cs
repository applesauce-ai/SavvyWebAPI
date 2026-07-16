namespace Savvy.Watchdog.Alerting;

public enum AlertLevel
{
    /// <summary>The API became unreachable/unhealthy.</summary>
    Down,
    /// <summary>The API recovered.</summary>
    Recovered,
    /// <summary>The watchdog itself started monitoring.</summary>
    WatchdogStarted,
    /// <summary>The watchdog itself is shutting down — monitoring is going offline.</summary>
    WatchdogStopped
}

/// <summary>
/// Sends an operational alert to a chat channel. Provider-agnostic: the local implementation posts
/// to a Discord webhook; in Azure a Teams-webhook implementation would be registered instead, with
/// no change to the caller.
/// </summary>
public interface IAlertNotifier
{
    Task SendAsync(AlertLevel level, string title, string message, CancellationToken ct = default);
}
