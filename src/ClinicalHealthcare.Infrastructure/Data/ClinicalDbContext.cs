using Microsoft.EntityFrameworkCore;

namespace ClinicalHealthcare.Infrastructure.Data;

/// <summary>
/// PostgreSQL DbContext — clinical data (intake forms, clinical documents, coding records).
/// Connection string is sourced exclusively from the POSTGRES_CONNECTION_STRING environment variable.
/// </summary>
public class ClinicalDbContext : DbContext
{
    public ClinicalDbContext(DbContextOptions<ClinicalDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Entity configurations registered here as clinical features are added
    }
}
