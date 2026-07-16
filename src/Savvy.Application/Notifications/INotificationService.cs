namespace Savvy.Application.Notifications;

/// <summary>
/// Raises operational notifications for business events. The Application layer knows only about
/// events; the transport (Discord/Teams webhook) is an Infrastructure concern. Implementations
/// must never throw — a failed notification must not fail the business operation.
/// </summary>
public interface INotificationService
{
    Task TimesheetSubmittedAsync(TimesheetSubmittedEvent notification, CancellationToken ct = default);
    Task PaymentRunCreatedAsync(PaymentRunCreatedEvent notification, CancellationToken ct = default);
}

public sealed record TimesheetSubmittedEvent(
    Guid TimesheetPublicId,
    int ShiftId,
    int ClinicianId,
    decimal Hours,
    string BusinessReference);

public sealed record PaymentRunCreatedEvent(
    Guid PaymentRunPublicId,
    int PracticeId,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    string Currency,
    decimal GrossTotal,
    decimal FeeTotal,
    decimal NetTotal,
    int LineItemCount,
    string BusinessReference);
