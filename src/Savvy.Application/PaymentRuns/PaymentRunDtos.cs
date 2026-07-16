using System.ComponentModel.DataAnnotations;
using Savvy.Domain.Entities;

namespace Savvy.Application.PaymentRuns;

/// <summary>Create a payment run for a practice over a UTC period, applying the supplied fee rules.
/// Idempotent on <see cref="BusinessReference"/>.</summary>
public record CreatePaymentRunRequest
{
    [Required] public DateTime PeriodStartUtc { get; init; }
    [Required] public DateTime PeriodEndUtc { get; init; }

    /// <summary>Fee as a fraction of gross (0.15 = 15%).</summary>
    [Range(0, 1)] public decimal FeePercentage { get; init; }

    /// <summary>Flat fee added per timesheet.</summary>
    [Range(0, 100000)] public decimal FixedFeePerTimesheet { get; init; }

    [StringLength(3, MinimumLength = 3)] public string? Currency { get; init; }

    [Required, StringLength(100)] public string BusinessReference { get; init; } = null!;
}

public record PaymentRunLineItemResponse
{
    public int TimesheetId { get; init; }
    public int ClinicianId { get; init; }
    public decimal Hours { get; init; }
    public decimal Rate { get; init; }
    public decimal Gross { get; init; }
    public decimal Fee { get; init; }
    public decimal Net { get; init; }

    public static PaymentRunLineItemResponse From(PaymentRunLineItem li) => new()
    {
        TimesheetId = li.TimesheetId,
        ClinicianId = li.ClinicianId,
        Hours = li.Hours,
        Rate = li.Rate,
        Gross = li.Gross,
        Fee = li.Fee,
        Net = li.Net
    };
}

public record PaymentRunResponse
{
    public Guid PublicId { get; init; }
    public int PracticeId { get; init; }
    public DateTime PeriodStartUtc { get; init; }
    public DateTime PeriodEndUtc { get; init; }
    public decimal FeePercentage { get; init; }
    public decimal FixedFeePerTimesheet { get; init; }
    public string Currency { get; init; } = null!;
    public decimal GrossTotal { get; init; }
    public decimal FeeTotal { get; init; }
    public decimal NetTotal { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<PaymentRunLineItemResponse> LineItems { get; init; } = [];

    public static PaymentRunResponse From(PaymentRun run) => new()
    {
        PublicId = run.PublicId,
        PracticeId = run.PracticeId,
        PeriodStartUtc = run.PeriodStartUtc,
        PeriodEndUtc = run.PeriodEndUtc,
        FeePercentage = run.FeePercentage,
        FixedFeePerTimesheet = run.FixedFeePerTimesheet,
        Currency = run.Currency,
        GrossTotal = run.GrossTotal,
        FeeTotal = run.FeeTotal,
        NetTotal = run.NetTotal,
        CreatedAtUtc = run.CreatedAtUtc,
        LineItems = run.LineItems
            .OrderBy(li => li.Id)
            .Select(PaymentRunLineItemResponse.From)
            .ToList()
    };
}

/// <summary>The run plus whether it was newly created (201) or an idempotent replay (200).</summary>
public record PaymentRunResult(PaymentRunResponse Run, bool Created);
