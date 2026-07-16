using System.Net.Http;
using Microsoft.Extensions.Options;
using Savvy.Watchdog.Alerting;

namespace Savvy.Watchdog;

/// <summary>
/// Polls the API health endpoint on an interval and raises Discord alerts on down/recovery
/// transitions. Runs as a separate process so it can detect the API being entirely down (an
/// in-process check cannot report that the process it lives in has crashed).
/// </summary>
public sealed class HealthMonitorWorker : BackgroundService
{
    private readonly WatchdogOptions _options;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAlertNotifier _notifier;
    private readonly HealthStateTracker _tracker;
    private readonly ILogger<HealthMonitorWorker> _logger;

    public HealthMonitorWorker(
        IOptions<WatchdogOptions> options,
        IHttpClientFactory httpFactory,
        IAlertNotifier notifier,
        ILogger<HealthMonitorWorker> logger)
    {
        _options = options.Value;
        _httpFactory = httpFactory;
        _notifier = notifier;
        _logger = logger;
        _tracker = new HealthStateTracker(_options.FailureThreshold);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Watchdog polling {Url} every {Seconds}s (alert after {Threshold} consecutive failures)",
            _options.HealthUrl, _options.PollSeconds, _options.FailureThreshold);

        // Surface the Discord config state up front (without leaking the URL) so it's obvious
        // whether alerts will actually be delivered.
        var discordReady = _options.Discord.Enabled && !string.IsNullOrWhiteSpace(_options.Discord.WebhookUrl);
        _logger.LogInformation("Discord alerts: {State}",
            discordReady ? "ENABLED (webhook configured)" : "DISABLED (no webhook URL — set Watchdog:Discord:WebhookUrl)");

        // Announce that monitoring is now online.
        await _notifier.SendAsync(AlertLevel.WatchdogStarted, "🟢 Watchdog online",
            $"Monitoring {_options.HealthUrl} every {_options.PollSeconds}s.", stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollSeconds));

        do
        {
            var healthy = await ProbeAsync(stoppingToken);
            var outcome = _tracker.Observe(healthy);

            _logger.LogInformation("Health probe: {Result} (state={State})",
                healthy ? "OK" : "FAIL", outcome.State);

            if (outcome.ShouldAlert)
                await RaiseAsync(outcome.Level, stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// On graceful shutdown (Ctrl+C / SIGTERM) announce that monitoring is going offline. Note this
    /// only fires for a graceful stop — a hard kill or crash cannot self-report (see README/SOLUTION:
    /// true crash detection needs an external heartbeat / dead-man's switch).
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Independent short timeout so the offline notice sends promptly and never hangs shutdown.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
        await _notifier.SendAsync(AlertLevel.WatchdogStopped, "⚠️ Watchdog offline",
            $"Health monitoring for {_options.HealthUrl} has STOPPED. The API is no longer being watched.",
            cts.Token);

        await base.StopAsync(cancellationToken);
    }

    private async Task<bool> ProbeAsync(CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

            var client = _httpFactory.CreateClient("health");
            using var response = await client.GetAsync(_options.HealthUrl, cts.Token);
            return response.IsSuccessStatusCode; // 200 = healthy; 503 (unhealthy) or error = not
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Health probe threw");
            return false;
        }
    }

    private async Task RaiseAsync(AlertLevel level, CancellationToken ct)
    {
        var (title, message) = level == AlertLevel.Down
            ? ("🔴 Savvy API is DOWN", $"Health check at {_options.HealthUrl} is failing.")
            : ("🟢 Savvy API recovered", $"Health check at {_options.HealthUrl} is passing again.");

        _logger.LogWarning("ALERT [{Level}]: {Title}", level, title);
        await _notifier.SendAsync(level, title, message, ct);
    }
}
