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
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<IntakeRecord>  IntakeRecords  => Set<IntakeRecord>();

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

            // AC-004 — default query filter: normal queries return only the latest version
            // Use .IgnoreQueryFilters() to access full version history
            e.HasQueryFilter(r => r.IsLatest);
        });
    }
}
