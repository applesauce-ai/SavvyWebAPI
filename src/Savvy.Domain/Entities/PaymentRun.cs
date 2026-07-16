namespace Savvy.Domain.Entities;

/// <summary>
/// A computed run of payments for a practice over a UTC period, applying the fee rules
/// supplied on the request. Totals are persisted as a snapshot (they must not drift if
/// rates change later). <see cref="BusinessReference"/> is the idempotency key.
/// </summary>
public class PaymentRun
{
    public int Id { get; set; }

    /// <summary>Non-guessable external identifier used in API routes.</summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    public int PracticeId { get; set; }
    public Practice Practice { get; set; } = null!;

    /// <summary>Inclusive UTC period the run covers.</summary>
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>Fee rules captured at run time (see PROJECT_PLAN §1: supplied per request).</summary>
    public decimal FeePercentage { get; set; }
    public decimal FixedFeePerTimesheet { get; set; }

    /// <summary>Caller-supplied idempotency key. Unique across payment runs.</summary>
    public string BusinessReference { get; set; } = null!;

    /// <summary>ISO 4217 currency code (e.g. "GBP").</summary>
    public string Currency { get; set; } = "GBP";

    public decimal GrossTotal { get; set; }
    public decimal FeeTotal { get; set; }
    public decimal NetTotal { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PaymentRunLineItem> LineItems { get; set; } = new List<PaymentRunLineItem>();
}
