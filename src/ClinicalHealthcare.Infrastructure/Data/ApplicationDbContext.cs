using Microsoft.EntityFrameworkCore;

namespace ClinicalHealthcare.Infrastructure.Data;

/// <summary>
/// SQL Server DbContext — operational data (users, appointments, scheduling).
/// Connection string is sourced exclusively from the SQLSERVER_CONNECTION_STRING environment variable.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Entity configurations registered here as features are added (AC-005 slices)
    }
}
