using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Savvy.Watchdog.Alerting;

/// <summary>
/// Posts alerts to a Discord channel via an incoming webhook. Discord expects a JSON body with a
/// "content" string and/or rich "embeds"; we send a coloured embed (red for down, green for
/// recovery). If disabled or no webhook URL is configured the alert is logged instead of sent, so
/// the watchdog is usable locally without a webhook.
/// </summary>
public sealed class DiscordWebhookNotifier : IAlertNotifier
{
    private const int ColourRed = 0xE74C3C;
    private const int ColourGreen = 0x2ECC71;
    private const int ColourBlue = 0x3498DB;
    private const int ColourAmber = 0xF1C40F;

    private readonly HttpClient _http;
    private readonly DiscordOptions _options;
    private readonly ILogger<DiscordWebhookNotifier> _logger;

    public DiscordWebhookNotifier(HttpClient http, IOptions<WatchdogOptions> options, ILogger<DiscordWebhookNotifier> logger)
    {
        _http = http;
        _options = options.Value.Discord;
        _logger = logger;
    }

    public async Task SendAsync(AlertLevel level, string title, string message, CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogWarning("Discord disabled — would alert [{Level}] {Title}: {Message}", level, title, message);
            return;
        }

        // Guard against stray whitespace/newlines pasted into the URL.
        var url = _options.WebhookUrl.Trim();

        if (!LooksLikeDiscordWebhook(url))
        {
            _logger.LogError(
                "Discord webhook URL looks malformed: {Redacted}. Expected https://discord.com/api/webhooks/<id>/<token> " +
                "(a token-less URL is what causes HTTP 405).", Redact(url));
            return;
        }

        var payload = new
        {
            username = _options.Username,
            embeds = new[]
            {
                new
                {
                    title,
                    description = message,
                    color = Colour(level),
                    timestamp = DateTimeOffset.UtcNow.ToString("o")
                }
            }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync(url, payload, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogError("Discord webhook returned {Status} for {Redacted}",
                    (int)response.StatusCode, Redact(url));
        }
        catch (Exception ex)
        {
            // Never let a failed alert crash the watchdog.
            _logger.LogError(ex, "Failed to post Discord alert to {Redacted}", Redact(url));
        }
    }

    /// <summary>True if the URL is a complete Discord webhook (…/webhooks/&lt;id&gt;/&lt;token&gt;).</summary>
    private static bool LooksLikeDiscordWebhook(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        var i = Array.IndexOf(segments, "webhooks");
        return i >= 0 && segments.Length > i + 2 && segments[i + 2].Length > 10; // id + non-trivial token
    }

    /// <summary>Host + structure only — never logs the token.</summary>
    private static string Redact(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "(unparseable url)";
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        var i = Array.IndexOf(segments, "webhooks");
        var id = i >= 0 && segments.Length > i + 1 ? segments[i + 1] : "?";
        var tokenLen = i >= 0 && segments.Length > i + 2 ? segments[i + 2].Length : 0;
        return $"{uri.Host}/.../webhooks/{id}/[token:{tokenLen} chars]";
    }

    private static int Colour(AlertLevel level) => level switch
    {
        AlertLevel.Down => ColourRed,
        AlertLevel.Recovered => ColourGreen,
        AlertLevel.WatchdogStopped => ColourAmber,
        _ => ColourBlue
    };
}

