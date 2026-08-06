using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelayWorks.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RelayWorksDbContext))]
[Migration("202608060003_ConnectionProfiles")]
public sealed class ConnectionProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("ConnectionProfiles", table => new
        {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false),
            TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
            Name = table.Column<string>("nvarchar(160)", maxLength: 160, nullable: false),
            Provider = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            SupportsIdempotencyKey = table.Column<bool>("bit", nullable: false),
            SupportsReadAfterWrite = table.Column<bool>("bit", nullable: false),
            MaxConfirmedNoCommitRetries = table.Column<int>("int", nullable: false),
            SecretReference = table.Column<string>("nvarchar(300)", maxLength: 300, nullable: false),
            ConfigurationVersion = table.Column<string>("nvarchar(32)", maxLength: 32, nullable: false),
            IsActive = table.Column<bool>("bit", nullable: false),
            UpdatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_ConnectionProfiles", x => x.Id));
        migrationBuilder.CreateIndex("IX_ConnectionProfiles_TenantId_Name", "ConnectionProfiles",
            new[] { "TenantId", "Name" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("ConnectionProfiles");
}
