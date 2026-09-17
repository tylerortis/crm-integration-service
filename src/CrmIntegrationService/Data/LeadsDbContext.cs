using CrmIntegrationService.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CrmIntegrationService.Data;

public sealed class LeadsDbContext(DbContextOptions<LeadsDbContext> options) : DbContext(options)
{
    public DbSet<Lead> Leads => Set<Lead>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no native DateTimeOffset; storing ticks lets "due" queries compare and sort in SQL.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lead>(entity =>
        {
            entity.ToTable("leads");
            entity.Property(l => l.OfferSlug).HasMaxLength(64);
            entity.Property(l => l.Email).HasMaxLength(254);
            entity.Property(l => l.Name).HasMaxLength(100);
            entity.Property(l => l.LastError).HasMaxLength(500);
            entity.Property(l => l.ForwardStatus).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(l => new { l.OfferSlug, l.Email }).IsUnique();
            entity.HasIndex(l => new { l.ForwardStatus, l.NextAttemptAt });
        });
    }
}
