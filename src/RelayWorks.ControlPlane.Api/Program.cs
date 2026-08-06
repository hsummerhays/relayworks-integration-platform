using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using RelayWorks.Application.Abstractions;
using RelayWorks.Application.IntegrationRuns;
using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Infrastructure.IntegrationRuns;
using RelayWorks.Infrastructure.Messaging;
using RelayWorks.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<RelayWorksDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RelayWorks")));
builder.Services.AddScoped<IIntegrationRunRepository, SqlIntegrationRunRepository>();
builder.Services.AddScoped<SubmitIntegrationRunHandler>();
builder.Services.AddScoped<ListIntegrationRunsHandler>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
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

var runs = app.MapGroup("/api/integration-runs").WithTags("Integration Runs");
runs.MapGet("/", async (
    Guid? tenantId,
    ListIntegrationRunsHandler handler,
    CancellationToken cancellationToken) =>
    Results.Ok(await handler.HandleAsync(tenantId, cancellationToken)));

runs.MapPost("/", async (
    SubmitIntegrationRunRequest request,
    SubmitIntegrationRunHandler handler,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await handler.HandleAsync(
            new SubmitIntegrationRunCommand(
                request.TenantId,
                request.ConnectionId,
                request.Operation,
                request.IdempotencyKey,
                request.TotalRecords),
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

runs.MapGet("/{runId:guid}/records", async (Guid runId, RelayWorksDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await db.IntegrationRecordProjections.AsNoTracking()
        .Where(x => x.RunId == runId).OrderBy(x => x.SourceRecordId).ToListAsync(cancellationToken)));

runs.MapGet("/{runId:guid}/issues", async (Guid runId, RelayWorksDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await db.IntegrationRecordProjections.AsNoTracking()
        .Where(x => x.RunId == runId && (x.Status == "Rejected" || x.Status == "UnknownOutcome"))
        .OrderByDescending(x => x.Status == "UnknownOutcome").ThenBy(x => x.SourceRecordId)
        .ToListAsync(cancellationToken)));

app.MapPost("/api/reconciliation-issues/{id:guid}/resolve", async (Guid id,
    ResolveReconciliationIssueRequest request, RelayWorksDbContext db, TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    var record = await db.IntegrationRecordProjections.FindAsync([id], cancellationToken);
    if (record is null) return Results.NotFound();
    try { record.Resolve(request.ResolutionNotes, timeProvider.GetUtcNow()); }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
    { return Results.ValidationProblem(new Dictionary<string, string[]> { ["resolutionNotes"] = [exception.Message] }); }
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(record);
}).WithTags("Reconciliation");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "control-plane" }));
app.Run();

public sealed record SubmitIntegrationRunRequest(
    Guid TenantId,
    Guid ConnectionId,
    IntegrationOperation Operation,
    string IdempotencyKey,
    int TotalRecords);

public sealed record ResolveReconciliationIssueRequest(string ResolutionNotes);
