using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RelayWorks.Infrastructure.Persistence;
using RelayWorks.Sync.Worker.Persistence;

namespace RelayWorks.Migrations;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("[Migrations] Starting RelayWorks database migration and SQL bootstrap runner...");

        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var controlPlaneCs = Environment.GetEnvironmentVariable("ConnectionStrings__RelayWorks");
        var workerCs = Environment.GetEnvironmentVariable("ConnectionStrings__WorkerLedger");
        var controlPrincipalName = Environment.GetEnvironmentVariable("SqlBootstrap__ControlPlanePrincipalName") ?? "id-relayworks-dev-control";
        var controlPrincipalObjectId = Environment.GetEnvironmentVariable("SqlBootstrap__ControlPlanePrincipalObjectId");
        var workerPrincipalName = Environment.GetEnvironmentVariable("SqlBootstrap__WorkerPrincipalName") ?? "id-relayworks-dev-worker";
        var workerPrincipalObjectId = Environment.GetEnvironmentVariable("SqlBootstrap__WorkerPrincipalObjectId");

        if (string.IsNullOrWhiteSpace(controlPlaneCs))
        {
            Console.Error.WriteLine("[Migrations] Error: ConnectionStrings__RelayWorks environment variable is missing.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(workerCs))
        {
            Console.Error.WriteLine("[Migrations] Error: ConnectionStrings__WorkerLedger environment variable is missing.");
            return 1;
        }

        TokenCredential credential = string.IsNullOrWhiteSpace(clientId)
            ? new DefaultAzureCredential()
            : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));

        try
        {
            Console.WriteLine("[Migrations] Migrating Control Plane database (RelayWorksDbContext)...");
            var controlOptions = new DbContextOptionsBuilder<RelayWorksDbContext>()
                .UseSqlServer(controlPlaneCs)
                .Options;

            using (var controlDb = new RelayWorksDbContext(controlOptions))
            {
                var pending = await controlDb.Database.GetPendingMigrationsAsync();
                Console.WriteLine($"[Migrations] Pending Control Plane migrations: {string.Join(", ", pending)}");
                await controlDb.Database.MigrateAsync();
                Console.WriteLine("[Migrations] Control Plane migrations applied successfully.");
            }

            Console.WriteLine("[Migrations] Migrating Worker Ledger database (WorkerLedgerDbContext)...");
            var workerOptions = new DbContextOptionsBuilder<WorkerLedgerDbContext>()
                .UseSqlServer(workerCs)
                .Options;

            using (var workerDb = new WorkerLedgerDbContext(workerOptions))
            {
                var pending = await workerDb.Database.GetPendingMigrationsAsync();
                Console.WriteLine($"[Migrations] Pending Worker migrations: {string.Join(", ", pending)}");
                await workerDb.Database.MigrateAsync();
                Console.WriteLine("[Migrations] Worker Ledger migrations applied successfully.");
            }

            if (!string.IsNullOrWhiteSpace(controlPrincipalObjectId))
            {
                Console.WriteLine($"[Migrations] Provisioning contained user '{controlPrincipalName}' ({controlPrincipalObjectId}) in Control Plane database...");
                await ProvisionContainedUserAsync(controlPlaneCs, controlPrincipalName, controlPrincipalObjectId, credential);
            }
            else
            {
                Console.WriteLine("[Migrations] Skipping Control Plane user bootstrap (no object ID supplied).");
            }

            if (!string.IsNullOrWhiteSpace(workerPrincipalObjectId))
            {
                Console.WriteLine($"[Migrations] Provisioning contained user '{workerPrincipalName}' ({workerPrincipalObjectId}) in Worker Ledger database...");
                await ProvisionContainedUserAsync(workerCs, workerPrincipalName, workerPrincipalObjectId, credential);
            }
            else
            {
                Console.WriteLine("[Migrations] Skipping Worker user bootstrap (no object ID supplied).");
            }

            Console.WriteLine("[Migrations] Database migration and bootstrap completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Migrations] Fatal error during migration/bootstrap: {ex}");
            return 1;
        }
    }

    private static async Task ProvisionContainedUserAsync(
        string connectionString,
        string principalName,
        string principalObjectId,
        TokenCredential credential)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sidHex = ConvertHexSid(principalObjectId);
        var sql = $@"
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '{principalName}')
BEGIN
    DROP USER [{principalName}];
END

CREATE USER [{principalName}] WITH SID = {sidHex}, TYPE = E;

ALTER ROLE db_datareader ADD MEMBER [{principalName}];
ALTER ROLE db_datawriter ADD MEMBER [{principalName}];
GRANT EXECUTE TO [{principalName}];
";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
        Console.WriteLine($"[Migrations] User [{principalName}] created/updated with SID = {sidHex}, db_datareader, db_datawriter, and EXECUTE.");
    }

    private static string ConvertHexSid(string guidString)
    {
        return $"0x{guidString.Replace("-", string.Empty).ToUpperInvariant()}";
    }
}
