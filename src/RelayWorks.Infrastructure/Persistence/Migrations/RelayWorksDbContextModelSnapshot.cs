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
            entity.HasIndex("TenantId", "IdempotencyKey").IsUnique();
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
            entity.ToTable("IntegrationRecordProjections");
        });
    }
}
