using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Savvy.Watchdog;
using Savvy.Watchdog.Alerting;
using Xunit;

namespace Savvy.UnitTests;

public class WatchdogLifecycleTests
{
    private sealed class RecordingNotifier : IAlertNotifier
    {
        public List<AlertLevel> Sent { get; } = new();
        public Task SendAsync(AlertLevel level, string title, string message, CancellationToken ct = default)
        {
            Sent.Add(level);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    [Fact]
    public async Task Graceful_shutdown_sends_watchdog_offline_alert()
    {
        var notifier = new RecordingNotifier();
        var options = Options.Create(new WatchdogOptions
        {
            HealthUrl = "http://localhost:5064/health/ready",
            RequestTimeoutSeconds = 1
        });

        var worker = new HealthMonitorWorker(
            options, new StubHttpClientFactory(), notifier, NullLogger<HealthMonitorWorker>.Instance);

        // Simulate a graceful stop (as Ctrl+C / SIGTERM triggers). ExecuteAsync was never started,
        // so base.StopAsync returns immediately; we only assert our shutdown notification fired.
        await worker.StopAsync(CancellationToken.None);

        Assert.Contains(AlertLevel.WatchdogStopped, notifier.Sent);
    }
}
