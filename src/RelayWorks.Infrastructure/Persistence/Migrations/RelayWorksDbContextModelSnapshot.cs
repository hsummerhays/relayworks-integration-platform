using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace RelayWorks.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RelayWorksDbContext))]
public sealed class RelayWorksDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.10")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        modelBuilder.Entity("RelayWorks.Domain.IntegrationRuns.IntegrationRun", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            entity.Property<int>("AcceptedRecords").HasColumnType("int");
            entity.Property<Guid>("ConnectionId").HasColumnType("uniqueidentifier");
            entity.Property<DateTimeOffset?>("CompletedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("Operation").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            entity.Property<int>("RejectedRecords").HasColumnType("int");
            entity.Property<string>("Status").IsRequired().HasMaxLength(40).HasColumnType("nvarchar(40)");
            entity.Property<Guid>("TenantId").HasColumnType("uniqueidentifier");
            entity.Property<int>("TotalRecords").HasColumnType("int");
            entity.HasKey("Id");
            entity.HasIndex("TenantId", "CreatedAtUtc");
            entity.HasIndex("TenantId", "ConnectionId", "CreatedAtUtc", "Id");
            entity.HasIndex("TenantId", "IdempotencyKey").IsUnique();
            entity.HasIndex("TenantId", "Status", "CreatedAtUtc", "Id");
            entity.ToTable("IntegrationRuns");
        });

        modelBuilder.Entity("RelayWorks.Infrastructure.Persistence.OutboxMessage", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            entity.Property<int>("AttemptCount").HasColumnType("int");
            entity.Property<DateTimeOffset?>("DispatchedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset>("OccurredAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("Payload").IsRequired().HasColumnType("nvarchar(max)");
            entity.Property<string>("Type").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.HasKey("Id");
            entity.HasIndex("DispatchedAtUtc");
            entity.ToTable("OutboxMessages");
        });

        modelBuilder.Entity("RelayWorks.Infrastructure.Persistence.IntegrationRecordProjection", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            entity.Property<string>("DestinationReference").HasMaxLength(300).HasColumnType("nvarchar(300)");
            entity.Property<string>("EmployeeReference").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("ErrorCode").HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("ErrorMessage").HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            entity.Property<string>("ProjectReference").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("ResolutionNotes").HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            entity.Property<Guid>("RunId").HasColumnType("uniqueidentifier");
            entity.Property<string>("SourceRecordId").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("SourceVersion").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("Status").IsRequired().HasMaxLength(40).HasColumnType("nvarchar(40)");
            entity.Property<Guid>("TenantId").HasColumnType("uniqueidentifier");
            entity.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("datetimeoffset");
            entity.HasKey("Id");
            entity.HasIndex("RunId", "SourceRecordId", "SourceVersion").IsUnique();
            entity.HasIndex("TenantId", "Status", "UpdatedAtUtc");
            entity.HasIndex("TenantId", "RunId", "UpdatedAtUtc", "Id");
            entity.ToTable("IntegrationRecordProjections");
        });

        modelBuilder.Entity("RelayWorks.Infrastructure.Persistence.ConnectionProfile", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            entity.Property<string>("ConfigurationVersion").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
            entity.Property<bool>("IsActive").HasColumnType("bit");
            entity.Property<int>("MaxConfirmedNoCommitRetries").HasColumnType("int");
            entity.Property<string>("Name").IsRequired().HasMaxLength(160).HasColumnType("nvarchar(160)");
            entity.Property<string>("Provider").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("SecretReference").IsRequired().HasMaxLength(300).HasColumnType("nvarchar(300)");
            entity.Property<bool>("SupportsIdempotencyKey").HasColumnType("bit");
            entity.Property<bool>("SupportsReadAfterWrite").HasColumnType("bit");
            entity.Property<Guid>("TenantId").HasColumnType("uniqueidentifier");
            entity.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("datetimeoffset");
            entity.HasKey("Id");
            entity.HasIndex("TenantId", "Name").IsUnique();
            entity.ToTable("ConnectionProfiles");
        });

        modelBuilder.Entity("RelayWorks.Infrastructure.Persistence.OperatorAuditRecord", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            entity.Property<string>("Action").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("ActorId").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("Detail").IsRequired().HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            entity.Property<DateTimeOffset>("OccurredAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("ResourceId").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("ResourceType").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<Guid>("TenantId").HasColumnType("uniqueidentifier"); entity.HasKey("Id");
            entity.HasIndex("TenantId", "OccurredAtUtc"); entity.ToTable("OperatorAuditRecords");
        });

        modelBuilder.Entity("RelayWorks.Infrastructure.Persistence.ConnectionTest", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            entity.Property<Guid>("ConnectionId").HasColumnType("uniqueidentifier");
            entity.Property<string>("ConfigurationVersion").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
            entity.Property<DateTimeOffset?>("CompletedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<long?>("DurationMilliseconds").HasColumnType("bigint");
            entity.Property<string>("FailureCategory").HasMaxLength(80).HasColumnType("nvarchar(80)");
            entity.Property<DateTimeOffset>("RequestedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("RequestedBy").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("SafeMessage").HasMaxLength(500).HasColumnType("nvarchar(500)");
            entity.Property<string>("Status").IsRequired().HasMaxLength(30).HasColumnType("nvarchar(30)");
            entity.Property<Guid>("TenantId").HasColumnType("uniqueidentifier"); entity.HasKey("Id");
            entity.HasIndex("TenantId", "ConnectionId", "RequestedAtUtc"); entity.ToTable("ConnectionTests");
        });
    }
}
