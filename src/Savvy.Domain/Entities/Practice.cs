namespace Savvy.Domain.Entities;

/// <summary>
/// A healthcare practice. PracticeManagers and Clinicians are scoped to a practice;
/// shifts and payment runs belong to one.
/// </summary>
public class Practice
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<PaymentRun> PaymentRuns { get; set; } = new List<PaymentRun>();
}
