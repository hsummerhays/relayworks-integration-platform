using Microsoft.EntityFrameworkCore;
using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Infrastructure.Persistence;

public sealed class RelayWorksDbContext(DbContextOptions<RelayWorksDbContext> options) : DbContext(options)
{
    public DbSet<IntegrationRun> IntegrationRuns => Set<IntegrationRun>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IntegrationRecordProjection> IntegrationRecordProjections => Set<IntegrationRecordProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var runs = modelBuilder.Entity<IntegrationRun>();
        runs.ToTable("IntegrationRuns");
        runs.HasKey(run => run.Id);
        runs.Property(run => run.IdempotencyKey).HasMaxLength(200);
        runs.Property(run => run.Operation).HasConversion<string>().HasMaxLength(80);
        runs.Property(run => run.Status).HasConversion<string>().HasMaxLength(40);
        runs.HasIndex(run => new { run.TenantId, run.IdempotencyKey }).IsUnique();
        runs.HasIndex(run => new { run.TenantId, run.CreatedAtUtc });

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("OutboxMessages");
        outbox.HasKey(message => message.Id);
        outbox.Property(message => message.Type).HasMaxLength(200);
        outbox.Property(message => message.Payload).HasColumnType("nvarchar(max)");
        outbox.HasIndex(message => message.DispatchedAtUtc);

        var records = modelBuilder.Entity<IntegrationRecordProjection>();
        records.ToTable("IntegrationRecordProjections");
        records.HasKey(x => x.Id);
        records.Property(x => x.SourceRecordId).HasMaxLength(200);
        records.Property(x => x.SourceVersion).HasMaxLength(100);
        records.Property(x => x.EmployeeReference).HasMaxLength(200);
        records.Property(x => x.ProjectReference).HasMaxLength(200);
        records.Property(x => x.Status).HasMaxLength(40);
        records.Property(x => x.ErrorCode).HasMaxLength(100);
        records.Property(x => x.ErrorMessage).HasMaxLength(2000);
        records.Property(x => x.DestinationReference).HasMaxLength(300);
        records.Property(x => x.ResolutionNotes).HasMaxLength(2000);
        records.HasIndex(x => new { x.RunId, x.SourceRecordId, x.SourceVersion }).IsUnique();
        records.HasIndex(x => new { x.TenantId, x.Status, x.UpdatedAtUtc });
    }
}
