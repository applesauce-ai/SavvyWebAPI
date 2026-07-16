using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Savvy.Infrastructure.Notifications;

/// <summary>Renders messages as Discord embeds and posts them to a Discord incoming webhook.</summary>
public sealed class DiscordWebhookNotifier : IWebhookNotifier
{
    private readonly HttpClient _http;
    private readonly NotificationsOptions _options;
    private readonly ILogger<DiscordWebhookNotifier> _logger;

    public DiscordWebhookNotifier(HttpClient http, IOptions<NotificationsOptions> options, ILogger<DiscordWebhookNotifier> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(WebhookMessage message, CancellationToken ct = default)
    {
        var url = _options.Discord.WebhookUrl;
        if (!_options.Enabled || string.IsNullOrWhiteSpace(url))
        {
            _logger.LogDebug("Notifications disabled/unconfigured — skipping Discord: {Title}", message.Title);
            return;
        }

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = message.Title,
                    description = message.Summary,
                    color = Colour(message.Level),
                    fields = message.Fields.Select(f => new { name = f.Name, value = f.Value, inline = true }).ToArray(),
                    timestamp = DateTimeOffset.UtcNow.ToString("o")
                }
            }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync(url, payload, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogError("Discord webhook returned {Status}", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post Discord notification");
        }
    }

    private static int Colour(NotificationLevel level) => level switch
    {
        NotificationLevel.Success => 0x2ECC71,
        NotificationLevel.Warning => 0xF1C40F,
        _ => 0x3498DB
    };
}
