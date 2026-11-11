using LabTrack.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Api.Infrastructure;

public sealed class LabTrackDbContext(DbContextOptions<LabTrackDbContext> options) : DbContext(options)
{
    public DbSet<LabSample> Samples => Set<LabSample>();
    public DbSet<TestResult> Results => Set<TestResult>();
    public DbSet<SampleEvent> Events => Set<SampleEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var sample = modelBuilder.Entity<LabSample>();
        sample.HasKey(x => x.Id);
        sample.HasIndex(x => x.AccessionNumber).IsUnique();
        sample.Property(x => x.AccessionNumber).HasMaxLength(40).IsRequired();
        sample.Property(x => x.PatientReference).HasMaxLength(40).IsRequired();
        sample.Property(x => x.SpecimenType).HasMaxLength(60).IsRequired();
        sample.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        sample.Property(x => x.Revision).IsConcurrencyToken();
        sample.HasMany(x => x.Results).WithOne().HasForeignKey(x => x.LabSampleId).OnDelete(DeleteBehavior.Cascade);
        sample.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.LabSampleId).OnDelete(DeleteBehavior.Cascade);

        var result = modelBuilder.Entity<TestResult>();
        result.HasKey(x => x.Id);
        result.HasIndex(x => new { x.LabSampleId, x.AnalyteCode }).IsUnique();
        result.Property(x => x.AnalyteCode).HasMaxLength(30).IsRequired();
        result.Property(x => x.AnalyteName).HasMaxLength(80).IsRequired();
        result.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        result.Property(x => x.Value).HasPrecision(12, 4);
        result.Property(x => x.ReferenceLow).HasPrecision(12, 4);
        result.Property(x => x.ReferenceHigh).HasPrecision(12, 4);

        var ev = modelBuilder.Entity<SampleEvent>();
        ev.HasKey(x => x.Id);
        ev.Property(x => x.EventType).HasMaxLength(30).IsRequired();
        ev.Property(x => x.Description).HasMaxLength(240).IsRequired();
        ev.HasIndex(x => new { x.LabSampleId, x.OccurredAt });
    }
}
