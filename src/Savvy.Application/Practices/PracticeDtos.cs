using System.ComponentModel.DataAnnotations;
using Savvy.Domain.Entities;

namespace Savvy.Application.Practices;

public record CreatePracticeRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = null!;
}

public record PracticeResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;

    public static PracticeResponse From(Practice p) => new() { Id = p.Id, Name = p.Name };
}
