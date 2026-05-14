namespace ClinicalHealthcare.Infrastructure.Entities;

/// <summary>
/// Status values for a <see cref="WaitlistEntry"/>.
/// The filtered unique index on <c>(PatientId) WHERE Status = 0</c> relies on
/// <c>Active = 0</c> being the integer value stored in the database.
/// </summary>
public enum WaitlistStatus
{
    Active    = 0,
    Fulfilled = 1,
    Expired   = 2
}

/// <summary>
/// Represents a patient's position on the appointment waitlist.
/// A patient may have at most one <c>Active</c> entry at any time; this is
/// enforced by a filtered partial unique index on <c>(PatientId) WHERE [Status] = 0</c>.
/// </summary>
public sealed class WaitlistEntry
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    /// <summary>
    /// Optional preferred slot the patient is waiting for.
    /// Null means "any available slot".
    /// </summary>
    public int? PreferredSlotId { get; set; }

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Active;

    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserAccount? Patient { get; set; }
    public Slot? PreferredSlot { get; set; }
}
