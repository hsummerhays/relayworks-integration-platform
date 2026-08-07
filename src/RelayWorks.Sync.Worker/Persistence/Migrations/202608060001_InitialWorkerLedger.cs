using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelayWorks.Sync.Worker.Persistence.Migrations;

[DbContext(typeof(WorkerLedgerDbContext))]
[Migration("202608060001_InitialWorkerLedger")]
public sealed class InitialWorkerLedger : Migration
{
    private static readonly string[] RunIdStateColumns = ["RunId", "State"];
    private static readonly string[] IdempotencyColumns =
        ["TenantId", "ConnectionId", "Operation", "SourceRecordId", "SourceVersion"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("InboxMessages", table => new
        {
            MessageId = table.Column<Guid>("uniqueidentifier", nullable: false),
            ProcessedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_InboxMessages", x => x.MessageId));
        migrationBuilder.CreateTable("OutboxMessages", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false),
            Type = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            Payload = table.Column<string>("nvarchar(max)", nullable: false),
            OccurredAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
            DispatchedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));
        migrationBuilder.CreateTable("RecordDeliveries", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
            ConnectionId = table.Column<Guid>("uniqueidentifier", nullable: false), RunId = table.Column<Guid>("uniqueidentifier", nullable: false),
            Operation = table.Column<string>("nvarchar(80)", maxLength: 80, nullable: false), SourceRecordId = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            SourceVersion = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false), CanonicalFingerprint = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: false),
            EmployeeReference = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false), ProjectReference = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            State = table.Column<string>("nvarchar(40)", maxLength: 40, nullable: false), AttemptCount = table.Column<int>("int", nullable: false),
            DestinationReference = table.Column<string>("nvarchar(300)", maxLength: 300, nullable: true), ErrorCode = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: true),
            ErrorMessage = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true), UpdatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
            RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false)
        }, constraints: table => table.PrimaryKey("PK_RecordDeliveries", x => x.Id));
        migrationBuilder.CreateIndex("IX_OutboxMessages_DispatchedAtUtc", "OutboxMessages", "DispatchedAtUtc");
        migrationBuilder.CreateIndex("IX_RecordDeliveries_RunId_State", "RecordDeliveries", RunIdStateColumns);
        migrationBuilder.CreateIndex("IX_RecordDeliveries_Idempotency", "RecordDeliveries",
            IdempotencyColumns, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("InboxMessages"); migrationBuilder.DropTable("OutboxMessages"); migrationBuilder.DropTable("RecordDeliveries");
    }
}
