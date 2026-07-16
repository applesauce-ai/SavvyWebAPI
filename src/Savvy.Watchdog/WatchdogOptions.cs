namespace Savvy.Watchdog;

/// <summary>Configuration for the health watchdog.</summary>
public sealed class WatchdogOptions
{
    public const string SectionName = "Watchdog";

    /// <summary>Health endpoint to poll (readiness includes the DB check).</summary>
    public string HealthUrl { get; set; } = "http://localhost:5064/health/ready";

    /// <summary>Seconds between polls.</summary>
    public int PollSeconds { get; set; } = 15;

    /// <summary>Consecutive failures before an alert fires (debounces transient blips).</summary>
    public int FailureThreshold { get; set; } = 2;

    /// <summary>Per-request timeout in seconds when calling the health endpoint.</summary>
    public int RequestTimeoutSeconds { get; set; } = 5;

    public DiscordOptions Discord { get; set; } = new();
}

public sealed class DiscordOptions
{
    /// <summary>When false, alerts are logged but not sent (useful for local runs without a webhook).</summary>
    public bool Enabled { get; set; }

    /// <summary>Discord webhook URL. Secret — supplied via user-secrets / environment, never committed.</summary>
    public string? WebhookUrl { get; set; }

    public string Username { get; set; } = "Savvy Watchdog";
}
