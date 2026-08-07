using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RelayWorks.Application.IntegrationRuns;
using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace RelayWorks.ControlPlane.Api.Tests;

public sealed class TenantIsolationTests : IAsyncLifetime
{
    private static readonly Guid VisibleTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private TestFactory? _factory;

    public async ValueTask InitializeAsync()
    {
        await _sql.StartAsync();

        _factory = new TestFactory(_sql.GetConnectionString());
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayWorksDbContext>();
        await db.Database.MigrateAsync();
        db.IntegrationRuns.Add(IntegrationRun.Create(VisibleTenant, Guid.NewGuid(),
            IntegrationOperation.TimeEntryExport, "visible", 1, DateTimeOffset.UtcNow));
        db.IntegrationRuns.Add(IntegrationRun.Create(Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.NewGuid(), IntegrationOperation.TimeEntryExport, "hidden", 1, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _sql.DisposeAsync();
    }

    [Fact]
    public async Task Run_listing_translates_to_sql_and_never_returns_another_tenants_rows()
    {
        var response = await _factory!.CreateClient().GetAsync("/api/integration-runs?pageSize=100",
            TestContext.Current.CancellationToken);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<IntegrationRunDto>>(
            JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page); Assert.Single(page.Items); Assert.Equal(VisibleTenant, page.Items[0].TenantId);
    }

    private sealed class TestFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<RelayWorksDbContext>>(); services.RemoveAll<RelayWorksDbContext>();
                services.AddDbContext<RelayWorksDbContext>(options => options.UseSqlServer(connectionString));
            });
        }
    }
}
