using ClinicalHealthcare.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicalHealthcare.Infrastructure.Data;

/// <summary>
/// SQL Server DbContext — operational data (users, appointments, scheduling).
/// Connection string is sourced exclusively from the SQLSERVER_CONNECTION_STRING environment variable.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public DbSet<UserAccount>  UserAccounts  => Set<UserAccount>();
    public DbSet<Slot>         Slots         => Set<Slot>();
    public DbSet<Appointment>  Appointments  => Set<Appointment>();

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
    }
}
