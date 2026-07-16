namespace Savvy.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Entities.Shift"/>.
/// A shift becomes <see cref="Completed"/> once its timesheet is submitted.
/// </summary>
public enum ShiftStatus
{
    Open = 0,
    Completed = 1
}
