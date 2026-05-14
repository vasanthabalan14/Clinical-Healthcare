namespace ClinicalHealthcare.Infrastructure.Entities;

/// <summary>
/// Finite-state machine (FSM) states for an <see cref="Appointment"/>.
/// Valid transitions are enforced by <c>AppointmentFsmInterceptor</c>:
/// <list type="bullet">
///   <item>Scheduled → Arrived</item>
///   <item>Scheduled → Cancelled</item>
///   <item>Scheduled → NoShow</item>
///   <item>Arrived   → Completed</item>
/// </list>
/// All other transitions are invalid and will throw <see cref="InvalidOperationException"/>.
/// </summary>
public enum AppointmentStatus
{
    Scheduled  = 0,
    Arrived    = 1,
    Completed  = 2,
    Cancelled  = 3,
    NoShow     = 4
}

/// <summary>
/// Represents a patient appointment linked to a <see cref="Slot"/>.
/// Status transitions are enforced by <c>AppointmentFsmInterceptor</c> at save time.
/// </summary>
public sealed class Appointment
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public int SlotId { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public DateTime BookedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserAccount? Patient { get; set; }
    public Slot? Slot { get; set; }
}
