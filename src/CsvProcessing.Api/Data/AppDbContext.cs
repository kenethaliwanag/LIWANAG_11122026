using CsvProcessing.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CsvProcessing.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProcessingRun> ProcessingRuns => Set<ProcessingRun>();

    public DbSet<ColumnAggregate> ColumnAggregates => Set<ColumnAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessingRun>(entity =>
        {
            entity.ToTable("ProcessingRuns");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.FileName).IsRequired().HasMaxLength(260);
            entity.Property(r => r.Status).HasConversion<int>();
            entity.Property(r => r.ErrorMessage).HasMaxLength(500);

            entity.Property(r => r.UploadedAt).HasConversion(UtcTicksConverter.Instance);
            entity.Property(r => r.CompletedAt).HasConversion(UtcTicksConverter.Instance);

            entity.HasMany(r => r.Aggregates)
                  .WithOne(a => a.ProcessingRun)
                  .HasForeignKey(a => a.ProcessingRunId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ColumnAggregate>(entity =>
        {
            entity.ToTable("ColumnAggregates");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.ColumnName).IsRequired().HasMaxLength(260);
            entity.HasIndex(a => a.ProcessingRunId);
        });
    }
}
