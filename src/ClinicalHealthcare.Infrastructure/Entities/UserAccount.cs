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

    // ── Profile fields (TASK_012) ─────────────────────────────────────────────
    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;

    // ── PHI retention (AC-002 / TASK_011) ────────────────────────────────────

    /// <summary>Soft-delete flag. True means the account is pending retention expiry.</summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Date after which the record may be purged under the PHI 7-year retention policy.
    /// Null until the account is soft-deleted.
    /// </summary>
    public DateTimeOffset? RetainUntil { get; set; }

    // ── Email verification (AC-002 / TASK_012) ───────────────────────────────

    /// <summary>
    /// SHA-256 hash of the one-time email verification token.
    /// Null after the token has been consumed or was never generated.
    /// </summary>
    public string? VerificationTokenHash { get; set; }

    /// <summary>UTC expiry of the verification token (24 hours from generation).</summary>
    public DateTime? VerificationTokenExpiry { get; set; }
}
