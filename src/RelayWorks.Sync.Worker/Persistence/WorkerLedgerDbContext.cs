using Microsoft.EntityFrameworkCore;

namespace RelayWorks.Sync.Worker.Persistence;

public sealed class WorkerLedgerDbContext(DbContextOptions<WorkerLedgerDbContext> options) : DbContext(options)
{
    public DbSet<RecordDelivery> RecordDeliveries => Set<RecordDelivery>();
    public DbSet<WorkerInboxMessage> InboxMessages => Set<WorkerInboxMessage>();
    public DbSet<WorkerOutboxMessage> OutboxMessages => Set<WorkerOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var deliveries = modelBuilder.Entity<RecordDelivery>();
        deliveries.ToTable("RecordDeliveries");
        deliveries.HasKey(x => x.Id);
        deliveries.Property(x => x.Operation).HasMaxLength(80);
        deliveries.Property(x => x.SourceRecordId).HasMaxLength(200);
        deliveries.Property(x => x.SourceVersion).HasMaxLength(100);
        deliveries.Property(x => x.CanonicalFingerprint).HasMaxLength(64);
        deliveries.Property(x => x.EmployeeReference).HasMaxLength(200);
        deliveries.Property(x => x.ProjectReference).HasMaxLength(200);
        deliveries.Property(x => x.State).HasConversion<string>().HasMaxLength(40);
        deliveries.Property(x => x.DestinationReference).HasMaxLength(300);
        deliveries.Property(x => x.ErrorCode).HasMaxLength(100);
        deliveries.Property(x => x.ErrorMessage).HasMaxLength(2000);
        deliveries.Property(x => x.RowVersion).IsRowVersion();
        deliveries.HasIndex(x => new { x.TenantId, x.ConnectionId, x.Operation, x.SourceRecordId, x.SourceVersion }).IsUnique();
        deliveries.HasIndex(x => new { x.RunId, x.State });

        modelBuilder.Entity<WorkerInboxMessage>().ToTable("InboxMessages").HasKey(x => x.MessageId);
        var outbox = modelBuilder.Entity<WorkerOutboxMessage>();
        outbox.ToTable("OutboxMessages");
        outbox.HasKey(x => x.Id);
        outbox.Property(x => x.Type).HasMaxLength(200);
        outbox.Property(x => x.Payload).HasColumnType("nvarchar(max)");
        outbox.HasIndex(x => x.DispatchedAtUtc);
    }
}
