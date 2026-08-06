using System.Text.Json.Serialization;
using RelayWorks.Application.Abstractions;
using RelayWorks.Application.IntegrationRuns;
using RelayWorks.Infrastructure.IntegrationRuns;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IIntegrationRunRepository, InMemoryIntegrationRunRepository>();
builder.Services.AddScoped<SubmitIntegrationRunHandler>();
builder.Services.AddScoped<ListIntegrationRunsHandler>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

var runs = app.MapGroup("/api/integration-runs").WithTags("Integration Runs");

runs.MapGet("/", async (
    ListIntegrationRunsHandler handler,
    CancellationToken cancellationToken) =>
    Results.Ok(await handler.HandleAsync(cancellationToken)));

runs.MapPost("/", async (
    SubmitIntegrationRunRequest request,
    SubmitIntegrationRunHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new SubmitIntegrationRunCommand(
            request.SourceSystem,
            request.DestinationSystem,
            request.IdempotencyKey,
            request.TotalRecords),
        cancellationToken);

    return result.IsDuplicate
        ? Results.Ok(result)
        : Results.Created($"/api/integration-runs/{result.Run.Id}", result);
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public sealed record SubmitIntegrationRunRequest(
    string SourceSystem,
    string DestinationSystem,
    string IdempotencyKey,
    int TotalRecords);
