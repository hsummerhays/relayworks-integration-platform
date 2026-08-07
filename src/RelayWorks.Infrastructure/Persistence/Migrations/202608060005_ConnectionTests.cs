using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelayWorks.Infrastructure.Persistence.Migrations;
[DbContext(typeof(RelayWorksDbContext))]
[Migration("202608060005_ConnectionTests")]
public sealed class ConnectionTests : Migration
{
    private static readonly string[] TenantConnectionRequestedColumns = ["TenantId", "ConnectionId", "RequestedAtUtc"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("ConnectionTests", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false), TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
            ConnectionId = table.Column<Guid>("uniqueidentifier", nullable: false), ConfigurationVersion = table.Column<string>("nvarchar(32)", maxLength: 32, nullable: false),
            Status = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false), FailureCategory = table.Column<string>("nvarchar(80)", maxLength: 80, nullable: true),
            SafeMessage = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: true), RequestedBy = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            RequestedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false), CompletedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: true),
            DurationMilliseconds = table.Column<long>("bigint", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_ConnectionTests", x => x.Id));
        migrationBuilder.CreateIndex("IX_ConnectionTests_Tenant_Connection_Requested", "ConnectionTests", TenantConnectionRequestedColumns);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("ConnectionTests");
}
