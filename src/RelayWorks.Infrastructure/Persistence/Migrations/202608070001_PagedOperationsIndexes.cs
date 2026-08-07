using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelayWorks.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RelayWorksDbContext))]
[Migration("202608070001_PagedOperationsIndexes")]
public sealed class PagedOperationsIndexes : Migration
{
    private static readonly string[] TenantStatusCreatedIdColumns = ["TenantId", "Status", "CreatedAtUtc", "Id"];
    private static readonly string[] TenantConnectionCreatedIdColumns = ["TenantId", "ConnectionId", "CreatedAtUtc", "Id"];
    private static readonly string[] TenantRunUpdatedIdColumns = ["TenantId", "RunId", "UpdatedAtUtc", "Id"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            "IX_IntegrationRuns_TenantId_Status_CreatedAtUtc_Id", "IntegrationRuns",
            TenantStatusCreatedIdColumns);
        migrationBuilder.CreateIndex(
            "IX_IntegrationRuns_TenantId_ConnectionId_CreatedAtUtc_Id", "IntegrationRuns",
            TenantConnectionCreatedIdColumns);
        migrationBuilder.CreateIndex(
            "IX_IntegrationRecordProjections_TenantId_RunId_UpdatedAtUtc_Id",
            "IntegrationRecordProjections", TenantRunUpdatedIdColumns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_IntegrationRuns_TenantId_Status_CreatedAtUtc_Id", "IntegrationRuns");
        migrationBuilder.DropIndex("IX_IntegrationRuns_TenantId_ConnectionId_CreatedAtUtc_Id", "IntegrationRuns");
        migrationBuilder.DropIndex("IX_IntegrationRecordProjections_TenantId_RunId_UpdatedAtUtc_Id",
            "IntegrationRecordProjections");
    }
}
