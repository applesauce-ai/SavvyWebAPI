namespace Savvy.Infrastructure.Notifications;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Master switch. When false (or no webhook URL configured) notifications are no-ops.</summary>
    public bool Enabled { get; set; }

    /// <summary>Active provider: "Discord" (local) or "Teams" (Azure).</summary>
    public string Provider { get; set; } = "Discord";

    public WebhookTarget Discord { get; set; } = new();
    public WebhookTarget Teams { get; set; } = new();
}

public sealed class WebhookTarget
{
    /// <summary>Secret — supplied by the vault (mock Key Vault in dev, Azure Key Vault in prod).</summary>
    public string? WebhookUrl { get; set; }
}
