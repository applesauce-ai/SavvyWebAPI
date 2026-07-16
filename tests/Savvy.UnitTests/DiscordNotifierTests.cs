using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Savvy.Watchdog;
using Savvy.Watchdog.Alerting;
using Xunit;

namespace Savvy.UnitTests;

public class DiscordNotifierTests
{
    /// <summary>Records whether an HTTP call was attempted; fails the call if it is.</summary>
    private sealed class TrackingHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    private static DiscordWebhookNotifier Notifier(HttpMessageHandler handler, string? webhookUrl)
    {
        var options = Options.Create(new WatchdogOptions
        {
            Discord = new DiscordOptions { Enabled = true, WebhookUrl = webhookUrl }
        });
        return new DiscordWebhookNotifier(new HttpClient(handler), options, NullLogger<DiscordWebhookNotifier>.Instance);
    }

    [Fact]
    public async Task Token_less_url_is_not_posted_and_does_not_throw()
    {
        // This is the shape that produces Discord HTTP 405: /webhooks/<id> with no token.
        var handler = new TrackingHandler();
        var notifier = Notifier(handler, "https://discord.com/api/webhooks/000000000000000000");

        await notifier.SendAsync(AlertLevel.Down, "t", "m");

        Assert.False(handler.WasCalled); // guarded before any POST
    }

    [Fact]
    public async Task Complete_webhook_url_is_posted()
    {
        var handler = new TrackingHandler();
        var notifier = Notifier(handler,
            "https://discord.com/api/webhooks/000000000000000000/FAKE-unit-test-token-not-a-real-secret-0123456789ABCDEF");

        await notifier.SendAsync(AlertLevel.Down, "t", "m");

        Assert.True(handler.WasCalled);
    }
}
