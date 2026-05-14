namespace ClinicalHealthcare.Infrastructure.Entities;

/// <summary>
/// Represents a registered user account (patient, staff, or admin).
/// Email uniqueness is enforced at the database level via a unique index.
/// </summary>
public sealed class UserAccount
{
    public int Id { get; set; }

    /// <summary>Unique email address — unique index enforced in OnModelCreating.</summary>
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Role values: "patient" | "staff" | "admin"</summary>
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
