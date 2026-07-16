namespace Savvy.Application.Common;

/// <summary>
/// The authenticated caller for the current request. Populated from JWT claims in the API
/// layer (Section 7). Application services depend on this to enforce practice/clinician
/// scoping — role attributes alone are not enough (a PracticeManager must not reach another
/// practice's data).
/// </summary>
public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    /// <summary>Internal integer user id (FK value), from the "uid" claim.</summary>
    int UserId { get; }

    /// <summary>External identifier (JWT subject).</summary>
    Guid PublicId { get; }

    /// <summary>Role name: "Admin", "PracticeManager", or "Clinician".</summary>
    string Role { get; }

    /// <summary>Practice the caller is scoped to; null for Admin.</summary>
    int? PracticeId { get; }

    bool IsAdmin { get; }
}
