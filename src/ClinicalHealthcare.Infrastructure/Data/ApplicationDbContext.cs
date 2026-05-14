using ClinicalHealthcare.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicalHealthcare.Infrastructure.Data;

/// <summary>
/// SQL Server DbContext — operational data (users, appointments, scheduling).
/// Connection string is sourced exclusively from the SQLSERVER_CONNECTION_STRING environment variable.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public DbSet<UserAccount>   UserAccounts   => Set<UserAccount>();
    public DbSet<Slot>          Slots          => Set<Slot>();
    public DbSet<Appointment>   Appointments   => Set<Appointment>();
    public DbSet<WaitlistEntry>    WaitlistEntries    => Set<WaitlistEntry>();
    public DbSet<IntakeRecord>     IntakeRecords     => Set<IntakeRecord>();
    public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
    public DbSet<AuditLog>         AuditLogs         => Set<AuditLog>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── UserAccount ──────────────────────────────────────────────────────
        modelBuilder.Entity<UserAccount>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(u => u.Role).HasMaxLength(32).IsRequired();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // AC-004 — unique index on Email
            e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("IX_UserAccounts_Email");
        });

        // ── Slot ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Slot>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.SlotTime).IsRequired();
            e.Property(s => s.DurationMinutes).IsRequired();
            // AC-002 — rowversion concurrency token (mapped from [Timestamp] attribute)
            e.Property(s => s.RowVersion).IsRowVersion();
        });

        // ── Appointment ──────────────────────────────────────────────────────
        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Status).HasConversion<int>().IsRequired();
            e.Property(a => a.BookedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasOne(a => a.Patient)
             .WithMany()
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Slot)
             .WithMany()
             .HasForeignKey(a => a.SlotId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── WaitlistEntry ────────────────────────────────────────────────────
        modelBuilder.Entity<WaitlistEntry>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Status).HasConversion<int>().IsRequired();
            e.Property(w => w.QueuedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasOne(w => w.Patient)
             .WithMany()
             .HasForeignKey(w => w.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(w => w.PreferredSlot)
             .WithMany()
             .HasForeignKey(w => w.PreferredSlotId)
             .OnDelete(DeleteBehavior.SetNull);

            // AC-002 — filtered partial unique index: one Active entry per patient
            // Status = 0 corresponds to WaitlistStatus.Active stored as int
            e.HasIndex(w => w.PatientId)
             .IsUnique()
             .HasFilter("[Status] = 0")
             .HasDatabaseName("UIX_WaitlistEntries_PatientId_Active");

            // Non-filtered index for history queries (Expired/Fulfilled entries)
            e.HasIndex(w => new { w.PatientId, w.Status })
             .HasDatabaseName("IX_WaitlistEntries_PatientId_Status");
        });

        // ── IntakeRecord ─────────────────────────────────────────────────────
        modelBuilder.Entity<IntakeRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Source).HasConversion<int>().IsRequired();
            e.Property(r => r.SubmittedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(r => r.ChiefComplaint).HasMaxLength(1000);
            e.Property(r => r.CurrentMeds).HasMaxLength(2000);
            e.Property(r => r.Allergies).HasMaxLength(1000);
            e.Property(r => r.MedicalHistory).HasMaxLength(4000);

            e.HasOne(r => r.Patient)
             .WithMany()
             .HasForeignKey(r => r.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            // Index for efficient "latest version per group" and history queries
            e.HasIndex(r => new { r.IntakeGroupId, r.Version })
             .IsUnique()
             .HasDatabaseName("UIX_IntakeRecords_GroupId_Version");

            e.HasIndex(r => new { r.IntakeGroupId, r.IsLatest })
             .HasDatabaseName("IX_IntakeRecords_GroupId_IsLatest");

            // AC-004 — default query filter: latest version only; soft-deleted records excluded.
            // Use .IgnoreQueryFilters() to access full version history or soft-deleted records.
            e.HasQueryFilter(r => r.IsLatest && !r.IsDeleted);
        });
        // ── UserAccount — PHI retention + soft-delete query filter ─────────
        modelBuilder.Entity<UserAccount>(e =>
        {
            e.Property(u => u.IsDeleted).HasDefaultValue(false);
            e.Property(u => u.RetainUntil).IsRequired(false);
            // Soft-delete filter — use .IgnoreQueryFilters() for admin/audit access.
            e.HasQueryFilter(u => !u.IsDeleted);
        });

        // ── WaitlistEntry — PHI retention + soft-delete query filter ────────
        modelBuilder.Entity<WaitlistEntry>(e =>
        {
            e.Property(w => w.IsDeleted).HasDefaultValue(false);
            e.Property(w => w.RetainUntil).IsRequired(false);
            // Soft-delete filter — use .IgnoreQueryFilters() for admin/audit access.
            e.HasQueryFilter(w => !w.IsDeleted);
        });

        // ── IntakeRecord — PHI retention columns ──────────────────────────
        modelBuilder.Entity<IntakeRecord>(e =>
        {
            e.Property(r => r.IsDeleted).HasDefaultValue(false);
            e.Property(r => r.RetainUntil).IsRequired(false);
            // HasQueryFilter for IsLatest is set above in the main IntakeRecord block.
            // IsDeleted is incorporated into that composite filter: IsLatest && !IsDeleted.
        });
        // ── ClinicalDocument ─────────────────────────────────────────────────
        modelBuilder.Entity<ClinicalDocument>(e =>
        {
            e.HasKey(d => d.Id);

            // AC-001 — EncryptedBlobPath as nvarchar(500); binary content NEVER stored in DB
            e.Property(d => d.OriginalFileName).HasMaxLength(500).IsRequired();
            e.Property(d => d.EncryptedBlobPath).HasColumnType("nvarchar(500)").IsRequired();

            // AC-002 — VirusScanResult defaults to Pending (0) at the database column level
            e.Property(d => d.VirusScanResult)
             .HasConversion<int>()
             .HasDefaultValue(VirusScanResult.Pending)
             .IsRequired();

            e.Property(d => d.OcrStatus)
             .HasConversion<int>()
             .HasDefaultValue(OcrStatus.Pending)
             .IsRequired();

            e.HasOne(d => d.Patient)
             .WithMany()
             .HasForeignKey(d => d.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.UploadedByStaff)
             .WithMany()
             .HasForeignKey(d => d.UploadedByStaffId)
             .OnDelete(DeleteBehavior.SetNull);

            // AC-003 — non-clustered index on PatientId for efficient patient document queries
            e.HasIndex(d => d.PatientId)
             .HasDatabaseName("IX_ClinicalDocuments_PatientId");

            // Optimistic concurrency — prevents lost updates from concurrent background workers
            e.Property(d => d.RowVersion).IsRowVersion();

            // PHI retention columns + soft-delete query filter
            e.Property(d => d.IsDeleted).HasDefaultValue(false);
            e.Property(d => d.RetainUntil).IsRequired(false);
            // Soft-delete filter — use .IgnoreQueryFilters() for admin/audit access.
            e.HasQueryFilter(d => !d.IsDeleted);
        });

        // ── AuditLog (AC-001) ─────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.EntityType).HasMaxLength(128).IsRequired();
            e.Property(a => a.Action).HasMaxLength(64).IsRequired();
            e.Property(a => a.BeforeValue).HasColumnType("nvarchar(max)");
            e.Property(a => a.AfterValue).HasColumnType("nvarchar(max)");
            e.Property(a => a.OccurredAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(a => a.CorrelationId).HasMaxLength(128);

            // Logical references only — no FK constraints so audit records outlive
            // the entities they reference (OWASP A09: security logging must not be lossy).

            // Composite index: look up all audits for a given entity by type + id
            e.HasIndex(a => new { a.EntityType, a.EntityId })
             .HasDatabaseName("IX_AuditLogs_EntityType_EntityId");

            // Index for time-range audit queries
            e.HasIndex(a => a.OccurredAt)
             .HasDatabaseName("IX_AuditLogs_OccurredAt");
        });
    }

    // ── AC-003: PHI soft-delete intercept ──────────────────────────────────

    /// <summary>
    /// Converts hard-delete operations on PHI entities to soft-deletes.
    /// Sets <c>IsDeleted = true</c> and <c>RetainUntil = UtcNow + 7 years</c>,
    /// then changes the entry state to <c>Modified</c> so no row is removed.
    /// Non-PHI entities (Slot, Appointment) proceed to hard-delete normally.
    /// </summary>
    public override int SaveChanges()
    {
        InterceptPhiDeletes();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        InterceptPhiDeletes();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void InterceptPhiDeletes()
    {
        var deletedPhi = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && IsPhiEntity(e.Entity))
            .ToList();

        foreach (var entry in deletedPhi)
        {
            entry.State = EntityState.Modified;
            entry.CurrentValues[nameof(UserAccount.IsDeleted)]   = true;
            entry.CurrentValues[nameof(UserAccount.RetainUntil)] = DateTimeOffset.UtcNow.AddYears(7);
        }
    }

    private static bool IsPhiEntity(object entity) =>
        entity is UserAccount or IntakeRecord or ClinicalDocument or WaitlistEntry;
}
