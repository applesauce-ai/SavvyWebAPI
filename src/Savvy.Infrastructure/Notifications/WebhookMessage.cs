namespace Savvy.Infrastructure.Notifications;

public enum NotificationLevel { Info, Success, Warning }

public sealed record NotificationField(string Name, string Value);

/// <summary>A platform-neutral chat message. Each <see cref="IWebhookNotifier"/> renders it into its
/// own webhook payload (Discord embed, Teams card, …).</summary>
public sealed record WebhookMessage(
    string Title,
    string Summary,
    NotificationLevel Level,
    IReadOnlyList<NotificationField> Fields);

/// <summary>
/// Posts a <see cref="WebhookMessage"/> to a chat platform. Provider-agnostic so Discord (local)
/// and Teams (Azure) are interchangeable via configuration. Implementations must swallow failures.
/// </summary>
public interface IWebhookNotifier
{
    Task SendAsync(WebhookMessage message, CancellationToken ct = default);
}
