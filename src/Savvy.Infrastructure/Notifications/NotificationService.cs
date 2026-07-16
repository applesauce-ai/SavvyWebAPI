using Savvy.Application.Notifications;

namespace Savvy.Infrastructure.Notifications;

/// <summary>
/// Maps business events to platform-neutral <see cref="WebhookMessage"/>s and hands them to the
/// configured <see cref="IWebhookNotifier"/>. Never throws — notifications are best-effort and must
/// not affect the business transaction.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IWebhookNotifier _notifier;

    public NotificationService(IWebhookNotifier notifier) => _notifier = notifier;

    public Task TimesheetSubmittedAsync(TimesheetSubmittedEvent n, CancellationToken ct = default)
    {
        var message = new WebhookMessage(
            Title: "🧾 Timesheet submitted",
            Summary: $"A timesheet was logged for shift #{n.ShiftId}.",
            Level: NotificationLevel.Info,
            Fields: new[]
            {
                new NotificationField("Hours", n.Hours.ToString("0.00")),
                new NotificationField("Clinician", $"#{n.ClinicianId}"),
                new NotificationField("Reference", n.BusinessReference),
                new NotificationField("Id", n.TimesheetPublicId.ToString())
            });

        return _notifier.SendAsync(message, ct);
    }

    public Task PaymentRunCreatedAsync(PaymentRunCreatedEvent n, CancellationToken ct = default)
    {
        var period = $"{n.PeriodStartUtc:yyyy-MM-dd} → {n.PeriodEndUtc:yyyy-MM-dd}";
        var message = new WebhookMessage(
            Title: "💷 Payment run created",
            Summary: $"A payment run was created for practice #{n.PracticeId} ({n.LineItemCount} timesheet(s)).",
            Level: NotificationLevel.Success,
            Fields: new[]
            {
                new NotificationField("Period", period),
                new NotificationField("Gross", $"{n.GrossTotal:0.00} {n.Currency}"),
                new NotificationField("Fee", $"{n.FeeTotal:0.00} {n.Currency}"),
                new NotificationField("Net", $"{n.NetTotal:0.00} {n.Currency}"),
                new NotificationField("Reference", n.BusinessReference),
                new NotificationField("Id", n.PaymentRunPublicId.ToString())
            });

        return _notifier.SendAsync(message, ct);
    }
}
