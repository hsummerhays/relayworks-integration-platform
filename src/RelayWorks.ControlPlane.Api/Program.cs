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
using System.Text.Json;
using RelayWorks.Contracts.Connections;
using RelayWorks.Contracts.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using System.Diagnostics;

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
var applicationInsights = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("relayworks-control-plane"))
    .WithTracing(tracing => tracing.AddSource(TelemetryNames.ControlPlaneSource)
        .AddAspNetCoreInstrumentation(options => options.Filter = context =>
            !context.Request.Path.StartsWithSegments("/health"))
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics.AddMeter(TelemetryNames.ControlPlaneMeter)
        .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation());
if (!string.IsNullOrWhiteSpace(applicationInsights))
{
    telemetry.WithTracing(tracing => tracing.AddAzureMonitorTraceExporter(options => options.ConnectionString = applicationInsights));
    telemetry.WithMetrics(metrics => metrics.AddAzureMonitorMetricExporter(options => options.ConnectionString = applicationInsights));
}
var authenticationEnabled = builder.Configuration.GetValue("Authentication:Enabled", true);
if (authenticationEnabled)
{
    builder.Services.AddAuthentication().AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = authenticationEnabled
        ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireClaim("relayworks_tenant_id").Build()
        : new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
    options.AddPolicy("IntegrationOperator", policy =>
    {
        if (authenticationEnabled) { policy.RequireAuthenticatedUser(); policy.RequireClaim("relayworks_tenant_id"); }
        policy.RequireAssertion(context => !authenticationEnabled || context.User.IsInRole("Integration.Operator") ||
            context.User.IsInRole("Integration.Admin") || context.User.HasClaim("roles", "Integration.Operator") ||
            context.User.HasClaim("roles", "Integration.Admin"));
    });
    options.AddPolicy("IntegrationAdmin", policy =>
    {
        if (authenticationEnabled) { policy.RequireAuthenticatedUser(); policy.RequireClaim("relayworks_tenant_id"); }
        policy.RequireAssertion(context => !authenticationEnabled || context.User.IsInRole("Integration.Admin") ||
            context.User.HasClaim("roles", "Integration.Admin"));
    });
});
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

var runs = app.MapGroup("/api/integration-runs").WithTags("Integration Runs").RequireAuthorization();
runs.MapGet("/", async (
    TenantContext tenantContext,
    ListIntegrationRunsHandler handler,
    string? cursor,
    IntegrationRunStatus? status,
    Guid? connectionId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? pageSize,
    CancellationToken cancellationToken) =>
{
    if (!PageCursor.TryDecode(cursor, out var cursorTimestamp, out var cursorId))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["cursor"] = ["The cursor is invalid."] });
    if (fromUtc.HasValue && toUtc.HasValue && fromUtc >= toUtc)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["dateRange"] = ["fromUtc must be before toUtc."] });
    var size = Math.Clamp(pageSize ?? 25, 1, 100);
    var query = new IntegrationRunQuery(tenantContext.RequireTenantId(), status, connectionId,
        fromUtc, toUtc, size, string.IsNullOrWhiteSpace(cursor) ? null : cursorTimestamp,
        string.IsNullOrWhiteSpace(cursor) ? null : cursorId);
    return Results.Ok(await handler.HandleAsync(query, cancellationToken));
});

runs.MapPost("/", async (
    SubmitIntegrationRunRequest request,
    SubmitIntegrationRunHandler handler,
    RelayWorksDbContext db,
    TenantContext tenantContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var tenantId = tenantContext.RequireTenantId();
        var connection = await db.ConnectionProfiles.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == request.ConnectionId && x.TenantId == tenantId && x.IsActive, cancellationToken);
        if (connection is null) return Results.ValidationProblem(new Dictionary<string, string[]>
        { ["connectionId"] = ["An active connection profile is required for this tenant."] });
        var result = await handler.HandleAsync(
            new SubmitIntegrationRunCommand(
                tenantId,
                request.ConnectionId,
                request.Operation,
                request.IdempotencyKey,
                request.TotalRecords,
                connection.Snapshot()),
            cancellationToken);
        Activity.Current?.SetTag("relayworks.run_id", result.Run.Id);
        Activity.Current?.SetTag("relayworks.operation", request.Operation.ToString());

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
}).RequireAuthorization("IntegrationOperator");

var connections = app.MapGroup("/api/connections").WithTags("Connections").RequireAuthorization();
connections.MapGet("/", async (RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
    Results.Ok(await db.ConnectionProfiles.AsNoTracking().Where(x => x.TenantId == tenantContext.RequireTenantId())
        .OrderBy(x => x.Name).ToListAsync(cancellationToken)));
connections.MapPost("/", async (CreateConnectionProfileRequest request, RelayWorksDbContext db,
    TimeProvider timeProvider, TenantContext tenantContext, CancellationToken cancellationToken) =>
{
    try
    {
        var tenantId = tenantContext.RequireTenantId();
        var profile = ConnectionProfile.Create(request.Id, tenantId, request.Name, request.Provider,
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
}).RequireAuthorization("IntegrationAdmin");

connections.MapPost("/{connectionId:guid}/tests", async (Guid connectionId, RelayWorksDbContext db,
    TenantContext tenantContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.RequireTenantId();
    var connection = await db.ConnectionProfiles.AsNoTracking().SingleOrDefaultAsync(x =>
        x.Id == connectionId && x.TenantId == tenantId && x.IsActive, cancellationToken);
    if (connection is null) return Results.NotFound();
    var now = timeProvider.GetUtcNow(); var testId = Guid.NewGuid(); var messageId = Guid.NewGuid();
    var test = new ConnectionTest { Id = testId, TenantId = tenantId, ConnectionId = connectionId,
        ConfigurationVersion = connection.ConfigurationVersion, RequestedBy = tenantContext.RequireActorId(), RequestedAtUtc = now };
    var command = new ConnectionTestRequestedV1(messageId, testId, tenantId, connectionId, connection.Snapshot(), now);
    db.ConnectionTests.Add(test);
    db.OutboxMessages.Add(new OutboxMessage(messageId, nameof(ConnectionTestRequestedV1), JsonSerializer.Serialize(command), now));
    db.OperatorAuditRecords.Add(new OperatorAuditRecord { TenantId = tenantId, ActorId = tenantContext.RequireActorId(),
        Action = "ConnectionTestRequested", ResourceType = "ConnectionTest", ResourceId = testId.ToString(),
        Detail = connectionId.ToString(), OccurredAtUtc = now });
    await db.SaveChangesAsync(cancellationToken);
    Activity.Current?.SetTag("relayworks.test_id", testId);
    Activity.Current?.SetTag("connector.provider", connection.Provider);
    return Results.Accepted($"/api/connections/{connectionId}/tests/{testId}", test);
}).RequireAuthorization("IntegrationOperator");

connections.MapGet("/{connectionId:guid}/tests/{testId:guid}", async (Guid connectionId, Guid testId,
    RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
{
    var result = await db.ConnectionTests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == testId &&
        x.ConnectionId == connectionId && x.TenantId == tenantContext.RequireTenantId(), cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

connections.MapGet("/{connectionId:guid}/tests/latest", async (Guid connectionId,
    RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
{
    var result = await db.ConnectionTests.AsNoTracking().Where(x => x.ConnectionId == connectionId &&
        x.TenantId == tenantContext.RequireTenantId()).OrderByDescending(x => x.RequestedAtUtc)
        .FirstOrDefaultAsync(cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

runs.MapGet("/{runId:guid}/records", async (Guid runId, string? cursor, string? view, int? pageSize,
    RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
{
    if (!PageCursor.TryDecode(cursor, out var cursorTimestamp, out var cursorId))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["cursor"] = ["The cursor is invalid."] });
    var size = Math.Clamp(pageSize ?? 50, 1, 100);
    var query = db.IntegrationRecordProjections.AsNoTracking()
        .Where(x => x.RunId == runId && x.TenantId == tenantContext.RequireTenantId());
    query = view?.ToLowerInvariant() switch
    {
        "attention" => query.Where(x => x.Status == "Rejected" || x.Status == "UnknownOutcome"),
        "resolved" => query.Where(x => x.Status == "ManuallyResolved"),
        null or "" or "all" => query,
        _ => null!
    };
    if (query is null)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["view"] = ["Use all, attention, or resolved."] });
    if (!string.IsNullOrWhiteSpace(cursor))
        query = query.Where(x => x.UpdatedAtUtc < cursorTimestamp ||
            (x.UpdatedAtUtc == cursorTimestamp && x.Id.CompareTo(cursorId) < 0));
    var rows = await query.OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.Id)
        .Take(size + 1).ToListAsync(cancellationToken);
    var page = rows.Take(size).ToList();
    var next = rows.Count > size && page.Count > 0 ? PageCursor.Encode(page[^1].UpdatedAtUtc, page[^1].Id) : null;
    return Results.Ok(new PagedResult<IntegrationRecordProjection>(page, next, size));
});


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
    Activity.Current?.SetTag("relayworks.record_id", record.Id);
    Activity.Current?.SetTag("relayworks.reconciliation", "manual-resolution");
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(record);
}).WithTags("Reconciliation").RequireAuthorization("IntegrationOperator");

app.MapGet("/api/audit", async (RelayWorksDbContext db, TenantContext tenantContext, CancellationToken cancellationToken) =>
    Results.Ok(await db.OperatorAuditRecords.AsNoTracking()
        .Where(x => x.TenantId == tenantContext.RequireTenantId()).OrderByDescending(x => x.OccurredAtUtc)
        .Take(200).ToListAsync(cancellationToken))).WithTags("Audit").RequireAuthorization("IntegrationAdmin");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "control-plane" })).AllowAnonymous();
app.MapGet("/health/ready", async (RelayWorksDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    if (!await db.Database.CanConnectAsync(cancellationToken))
        return Results.Json(new { status = "unhealthy", dependency = "control-database" }, statusCode: 503);
    var pending = await db.OutboxMessages.CountAsync(x => x.DispatchedAtUtc == null, cancellationToken);
    var oldest = await db.OutboxMessages.Where(x => x.DispatchedAtUtc == null)
        .MinAsync(x => (DateTimeOffset?)x.OccurredAtUtc, cancellationToken);
    var lagSeconds = oldest.HasValue ? (timeProvider.GetUtcNow() - oldest.Value).TotalSeconds : 0;
    return lagSeconds > 60
        ? Results.Json(new { status = "degraded", dependency = "command-outbox", pending, lagSeconds }, statusCode: 503)
        : Results.Ok(new { status = "ready", pendingOutboxMessages = pending, outboxLagSeconds = lagSeconds });
}).AllowAnonymous();
app.Run();

public sealed record SubmitIntegrationRunRequest(
    Guid ConnectionId,
    IntegrationOperation Operation,
    string IdempotencyKey,
    int TotalRecords);

public sealed record ResolveReconciliationIssueRequest(string ResolutionNotes);

public sealed record CreateConnectionProfileRequest(Guid Id, string Name, string Provider,
    bool SupportsIdempotencyKey, bool SupportsReadAfterWrite, int MaxConfirmedNoCommitRetries,
    string SecretReference);
