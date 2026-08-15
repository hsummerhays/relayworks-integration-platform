using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelayWorks.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RelayWorksDbContext))]
[Migration("202608090001_ConnectionAuthType")]
public sealed class ConnectionAuthType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AuthType",
            table: "ConnectionProfiles",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "ApiKey");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AuthType",
            table: "ConnectionProfiles");
    }
}
