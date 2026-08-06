using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelayWorks.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RelayWorksDbContext))]
[Migration("202608060002_RecordProjections")]
public sealed class RecordProjections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("IntegrationRecordProjections", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false),
            RunId = table.Column<Guid>("uniqueidentifier", nullable: false),
            TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
            SourceRecordId = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            SourceVersion = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            EmployeeReference = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            ProjectReference = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
            Status = table.Column<string>("nvarchar(40)", maxLength: 40, nullable: false),
            ErrorCode = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: true),
            ErrorMessage = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true),
            DestinationReference = table.Column<string>("nvarchar(300)", maxLength: 300, nullable: true),
            ResolutionNotes = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true),
            UpdatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_IntegrationRecordProjections", x => x.Id));
        migrationBuilder.CreateIndex("IX_RecordProjection_Run_Source_Version", "IntegrationRecordProjections",
            new[] { "RunId", "SourceRecordId", "SourceVersion" }, unique: true);
        migrationBuilder.CreateIndex("IX_RecordProjection_Tenant_Status_Updated", "IntegrationRecordProjections",
            new[] { "TenantId", "Status", "UpdatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("IntegrationRecordProjections");
}
