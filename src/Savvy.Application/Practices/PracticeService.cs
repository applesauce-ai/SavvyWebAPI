using Microsoft.EntityFrameworkCore;
using Savvy.Application.Common;
using Savvy.Domain.Entities;

namespace Savvy.Application.Practices;

/// <summary>Practice administration. Admin-only (enforced by the controller).</summary>
public class PracticeService : IPracticeService
{
    private readonly ISavvyDbContext _db;

    public PracticeService(ISavvyDbContext db) => _db = db;

    public async Task<PracticeResponse> CreateAsync(CreatePracticeRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        if (await _db.Practices.AnyAsync(p => p.Name == name, ct))
            throw new ConflictException($"A practice named '{name}' already exists.");

        var practice = new Practice { Name = name };
        _db.Practices.Add(practice);
        await _db.SaveChangesAsync(ct);

        return PracticeResponse.From(practice);
    }

    public async Task<PracticeResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var practice = await _db.Practices.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Practice", id);

        return PracticeResponse.From(practice);
    }
}
