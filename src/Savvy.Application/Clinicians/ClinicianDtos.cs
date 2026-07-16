using System.ComponentModel.DataAnnotations;
using Savvy.Domain.Entities;

namespace Savvy.Application.Clinicians;

public record CreateClinicianRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = null!;
}

public record ClinicianResponse
{
    public Guid PublicId { get; init; }
    public string Email { get; init; } = null!;
    public string Role { get; init; } = null!;
    public int PracticeId { get; init; }

    public static ClinicianResponse From(User u) => new()
    {
        PublicId = u.PublicId,
        Email = u.Email,
        Role = u.Role.Name,
        PracticeId = u.PracticeId!.Value
    };
}
