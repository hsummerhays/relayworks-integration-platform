using Microsoft.EntityFrameworkCore;
using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Infrastructure.Persistence;

public sealed class RelayWorksDbContext(DbContextOptions<RelayWorksDbContext> options) : DbContext(options)
{
    public DbSet<IntegrationRun> IntegrationRuns => Set<IntegrationRun>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
    }
}
