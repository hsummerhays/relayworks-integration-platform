using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelayWorks.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RelayWorksDbContext))]
[Migration("202608060001_InitialControlPlane")]
public sealed class InitialControlPlane : Migration
{
    private static readonly string[] TenantCreatedColumns = ["TenantId", "CreatedAtUtc"];
    private static readonly string[] TenantIdempotencyKeyColumns = ["TenantId", "IdempotencyKey"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IntegrationRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                TotalRecords = table.Column<int>(type: "int", nullable: false),
                AcceptedRecords = table.Column<int>(type: "int", nullable: false),
                RejectedRecords = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_IntegrationRuns", row => row.Id));

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                AttemptCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", row => row.Id));

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationRuns_TenantId_CreatedAtUtc",
            table: "IntegrationRuns",
            columns: TenantCreatedColumns);
        migrationBuilder.CreateIndex(
            name: "IX_IntegrationRuns_TenantId_IdempotencyKey",
            table: "IntegrationRuns",
            columns: TenantIdempotencyKeyColumns,
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_DispatchedAtUtc",
            table: "OutboxMessages",
            column: "DispatchedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IntegrationRuns");
        migrationBuilder.DropTable(name: "OutboxMessages");
    }
}
