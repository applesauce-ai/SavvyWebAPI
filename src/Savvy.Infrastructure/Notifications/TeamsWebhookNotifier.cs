using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Savvy.Infrastructure.Notifications;

/// <summary>
/// MOCKUP — Microsoft Teams notifier, the Azure counterpart to <see cref="DiscordWebhookNotifier"/>.
/// Renders messages as a legacy "MessageCard" (Office 365 connector format) and posts to a Teams
/// incoming webhook. It is wired and swappable via <c>Notifications:Provider = "Teams"</c>, but is
/// intended as a demonstration of the provider-agnostic design rather than a hardened integration.
///
/// Note: Microsoft is phasing out connector-based MessageCard webhooks in favour of **Workflows**
/// (Power Automate) delivering **Adaptive Cards**. A production Teams integration would post an
/// Adaptive Card to a Workflow URL; the shape below is kept simple to illustrate the swap point.
/// </summary>
public sealed class TeamsWebhookNotifier : IWebhookNotifier
{
    private readonly HttpClient _http;
    private readonly NotificationsOptions _options;
    private readonly ILogger<TeamsWebhookNotifier> _logger;

    public TeamsWebhookNotifier(HttpClient http, IOptions<NotificationsOptions> options, ILogger<TeamsWebhookNotifier> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(WebhookMessage message, CancellationToken ct = default)
    {
        var url = _options.Teams.WebhookUrl;
        if (!_options.Enabled || string.IsNullOrWhiteSpace(url))
        {
            _logger.LogDebug("Notifications disabled/unconfigured — skipping Teams: {Title}", message.Title);
            return;
        }

        // MessageCard requires the "@type"/"@context" keys, so use a dictionary for the root.
        var payload = new Dictionary<string, object?>
        {
            ["@type"] = "MessageCard",
            ["@context"] = "http://schema.org/extensions",
            ["themeColor"] = Colour(message.Level),
            ["summary"] = message.Title,
            ["sections"] = new[]
            {
                new
                {
                    activityTitle = message.Title,
                    text = message.Summary,
                    facts = message.Fields.Select(f => new { name = f.Name, value = f.Value }).ToArray()
                }
            }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync(url, payload, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogError("Teams webhook returned {Status}", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post Teams notification");
        }
    }

    private static string Colour(NotificationLevel level) => level switch
    {
        NotificationLevel.Success => "2ECC71",
        NotificationLevel.Warning => "F1C40F",
        _ => "3498DB"
    };
}
