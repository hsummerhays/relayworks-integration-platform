using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelayWorks.Infrastructure.Persistence.Migrations;
[DbContext(typeof(RelayWorksDbContext))]
[Migration("202608060004_OperatorAudit")]
public sealed class OperatorAudit : Migration
{
    private static readonly string[] TenantOccurredColumns = ["TenantId", "OccurredAtUtc"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("OperatorAuditRecords", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
            ActorId = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false), Action = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            ResourceType = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false), ResourceId = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            Detail = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: false), OccurredAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_OperatorAuditRecords", x => x.Id));
        migrationBuilder.CreateIndex("IX_OperatorAuditRecords_TenantId_OccurredAtUtc", "OperatorAuditRecords", TenantOccurredColumns);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("OperatorAuditRecords");
}
