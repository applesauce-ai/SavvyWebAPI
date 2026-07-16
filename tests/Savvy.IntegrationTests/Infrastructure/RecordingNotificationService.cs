using Savvy.Application.Notifications;

namespace Savvy.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for <see cref="INotificationService"/> that records the events raised instead of
/// posting to a webhook — so tests can assert which notifications fired.
/// </summary>
public sealed class RecordingNotificationService : INotificationService
{
    public List<TimesheetSubmittedEvent> Submitted { get; } = new();
    public List<PaymentRunCreatedEvent> PaymentRuns { get; } = new();
    public List<TimesheetHoursWarningEvent> Warnings { get; } = new();

    public Task TimesheetSubmittedAsync(TimesheetSubmittedEvent n, CancellationToken ct = default)
    {
        Submitted.Add(n);
        return Task.CompletedTask;
    }

    public Task PaymentRunCreatedAsync(PaymentRunCreatedEvent n, CancellationToken ct = default)
    {
        PaymentRuns.Add(n);
        return Task.CompletedTask;
    }

    public Task TimesheetHoursWarningAsync(TimesheetHoursWarningEvent n, CancellationToken ct = default)
    {
        Warnings.Add(n);
        return Task.CompletedTask;
    }
}
