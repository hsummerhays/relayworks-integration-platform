using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using RelayWorks.Application.Abstractions;
using RelayWorks.Application.IntegrationRuns;
using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Infrastructure.IntegrationRuns;
using RelayWorks.Infrastructure.Messaging;
using RelayWorks.Infrastructure.Persistence;
using RelayWorks.ControlPlane.Api;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<RelayWorksDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RelayWorks")));
builder.Services.AddScoped<IIntegrationRunRepository, SqlIntegrationRunRepository>();
builder.Services.AddScoped<SubmitIntegrationRunHandler>();
builder.Services.AddScoped<ListIntegrationRunsHandler>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();
var authenticationEnabled = builder.Configuration.GetValue("Authentication:Enabled", true);
if (authenticationEnabled)
{
    builder.Services.AddAuthentication().AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
    builder.Services.AddAuthorization(options => options.FallbackPolicy =
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
}
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection(ServiceBusOptions.SectionName));
if (builder.Configuration.GetValue("ServiceBus:Enabled", true))
{
    builder.Services.AddSingleton(provider =>
        ServiceBusClientFactory.Create(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceBusOptions>>().Value));
    builder.Services.AddHostedService<OutboxPublisher>();
    builder.Services.AddHostedService<IntegrationResultConsumer>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseExceptionHandler();
app.UseCors();
app.UseHttpsRedirection();
if (authenticationEnabled) { app.UseAuthentication(); app.UseAuthorization(); }

var runs = app.MapGroup("/api/integration-runs").WithTags("Integration Runs");
runs.MapGet("/", async (
    TenantContext tenantContext,
    ListIntegrationRunsHandler handler,
    CancellationToken cancellationToken) =>
    Results.Ok(await handler.HandleAsync(tenantContext.RequireTenantId(), cancellationToken)));

runs.MapPost("/", async (
    SubmitIntegrationRunRequest request,
    SubmitIntegrationRunHandler handler,
    RelayWorksDbContext db,
    TenantContext tenantContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (request.TenantId != tenantContext.RequireTenantId()) return Results.Forbid();
        var connection = await db.ConnectionProfiles.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == request.ConnectionId && x.TenantId == request.TenantId && x.IsActive, cancellationToken);
        if (connection is null) return Results.ValidationProblem(new Dictionary<string, string[]>
        { ["connectionId"] = ["An active connection profile is required for this tenant."] });
        var result = await handler.HandleAsync(
            new SubmitIntegrationRunCommand(
                request.TenantId,
                request.ConnectionId,
                request.Operation,
                request.IdempotencyKey,
                request.TotalRecords,
                connection.Snapshot()),
            cancellationToken);

        return result.IsDuplicate
            ? Results.Ok(result)
            : Results.Created($"/api/integration-runs/{result.Run.Id}", result);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        });
    }
});

var connections = app.MapGroup("/api/connections").WithTags("Connections");
connections.MapGet("/", async (RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
    Results.Ok(await db.ConnectionProfiles.AsNoTracking().Where(x => x.TenantId == tenantContext.RequireTenantId())
        .OrderBy(x => x.Name).ToListAsync(cancellationToken)));
connections.MapPost("/", async (CreateConnectionProfileRequest request, RelayWorksDbContext db,
    TimeProvider timeProvider, TenantContext tenantContext, CancellationToken cancellationToken) =>
{
    try
    {
        if (request.TenantId != tenantContext.RequireTenantId()) return Results.Forbid();
        var profile = ConnectionProfile.Create(request.Id, request.TenantId, request.Name, request.Provider,
            request.SupportsIdempotencyKey, request.SupportsReadAfterWrite,
            request.MaxConfirmedNoCommitRetries, request.SecretReference, timeProvider.GetUtcNow());
        db.ConnectionProfiles.Add(profile);
        db.OperatorAuditRecords.Add(new OperatorAuditRecord { TenantId = profile.TenantId,
            ActorId = tenantContext.RequireActorId(), Action = "ConnectionCreated", ResourceType = "ConnectionProfile",
            ResourceId = profile.Id.ToString(), Detail = profile.Provider, OccurredAtUtc = timeProvider.GetUtcNow() });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/connections/{profile.Id}", profile);
    }
    catch (ArgumentException exception)
    { return Results.ValidationProblem(new Dictionary<string, string[]> { ["connection"] = [exception.Message] }); }
});

runs.MapGet("/{runId:guid}/records", async (Guid runId, RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
    Results.Ok(await db.IntegrationRecordProjections.AsNoTracking()
        .Where(x => x.RunId == runId && x.TenantId == tenantContext.RequireTenantId()).OrderBy(x => x.SourceRecordId).ToListAsync(cancellationToken)));

runs.MapGet("/{runId:guid}/issues", async (Guid runId, RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
    Results.Ok(await db.IntegrationRecordProjections.AsNoTracking()
        .Where(x => x.RunId == runId && x.TenantId == tenantContext.RequireTenantId() && (x.Status == "Rejected" || x.Status == "UnknownOutcome"))
        .OrderByDescending(x => x.Status == "UnknownOutcome").ThenBy(x => x.SourceRecordId)
        .ToListAsync(cancellationToken)));

app.MapPost("/api/reconciliation-issues/{id:guid}/resolve", async (Guid id,
    ResolveReconciliationIssueRequest request, RelayWorksDbContext db, TimeProvider timeProvider,
    TenantContext tenantContext,
    CancellationToken cancellationToken) =>
{
    var record = await db.IntegrationRecordProjections.FindAsync([id], cancellationToken);
    if (record is null) return Results.NotFound();
    if (record.TenantId != tenantContext.RequireTenantId()) return Results.NotFound();
    try { record.Resolve(request.ResolutionNotes, timeProvider.GetUtcNow()); }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
    { return Results.ValidationProblem(new Dictionary<string, string[]> { ["resolutionNotes"] = [exception.Message] }); }
    db.OperatorAuditRecords.Add(new OperatorAuditRecord { TenantId = record.TenantId,
        ActorId = tenantContext.RequireActorId(), Action = "ReconciliationResolved",
        ResourceType = "IntegrationRecord", ResourceId = record.Id.ToString(),
        Detail = request.ResolutionNotes.Trim(), OccurredAtUtc = timeProvider.GetUtcNow() });
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(record);
}).WithTags("Reconciliation");

app.MapGet("/api/audit", async (RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
    Results.Ok(await db.OperatorAuditRecords.AsNoTracking()
        .Where(x => x.TenantId == tenantContext.RequireTenantId()).OrderByDescending(x => x.OccurredAtUtc)
        .Take(200).ToListAsync(cancellationToken))).WithTags("Audit");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "control-plane" })).AllowAnonymous();
app.Run();

public sealed record SubmitIntegrationRunRequest(
    Guid TenantId,
    Guid ConnectionId,
    IntegrationOperation Operation,
    string IdempotencyKey,
    int TotalRecords);

public sealed record ResolveReconciliationIssueRequest(string ResolutionNotes);

public sealed record CreateConnectionProfileRequest(Guid Id, Guid TenantId, string Name, string Provider,
    bool SupportsIdempotencyKey, bool SupportsReadAfterWrite, int MaxConfirmedNoCommitRetries,
    string SecretReference);
