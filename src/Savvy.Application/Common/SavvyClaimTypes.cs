namespace Savvy.Application.Common;

/// <summary>
/// Custom JWT claim types shared between token issuance (Section 7) and
/// <see cref="ICurrentUserContext"/>. Standard claims (subject, role) use their
/// framework constants; these cover Savvy-specific values.
/// </summary>
public static class SavvyClaimTypes
{
    /// <summary>Internal integer user id.</summary>
    public const string UserId = "uid";

    /// <summary>Integer practice id the user is scoped to (absent for Admin).</summary>
    public const string PracticeId = "practiceId";
}
