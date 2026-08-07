using Azure.Messaging.ServiceBus;
using RelayWorks.Sync.Worker;
using RelayWorks.Sync.Worker.Persistence;
using Microsoft.EntityFrameworkCore;
using RelayWorks.Sync.Worker.Secrets;
using RelayWorks.Contracts.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using RelayWorks.Sync.Worker.Resilience;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
var applicationInsights = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("relayworks-sync-worker"))
    .WithTracing(tracing => tracing.AddSource(TelemetryNames.WorkerSource).AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics.AddMeter(TelemetryNames.WorkerMeter, TelemetryNames.SecretMeter)
        .AddHttpClientInstrumentation().AddRuntimeInstrumentation());
if (!string.IsNullOrWhiteSpace(applicationInsights))
{
    telemetry.WithTracing(tracing => tracing.AddAzureMonitorTraceExporter(options => options.ConnectionString = applicationInsights));
    telemetry.WithMetrics(metrics => metrics.AddAzureMonitorMetricExporter(options => options.ConnectionString = applicationInsights));
}
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection(ServiceBusOptions.SectionName));
builder.Services.AddSingleton(provider =>
    ServiceBusClientFactory.Create(
        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceBusOptions>>().Value));
builder.Services.AddDbContext<WorkerLedgerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WorkerLedger")));
builder.Services.AddScoped<TimeEntryProcessor>();
builder.Services.AddScoped<ConnectionTestProcessor>();
builder.Services.AddOptions<ConnectorResilienceOptions>()
    .Bind(builder.Configuration.GetSection(ConnectorResilienceOptions.SectionName))
    .Validate(options => options.MaxConcurrentRequestsPerConnection > 0 &&
        options.RequestsPerSecondPerConnection > 0 && options.BurstCapacityPerConnection > 0 &&
        options.BaseRetryDelayMilliseconds > 0 && options.MaxRetryDelaySeconds > 0 &&
        options.CircuitFailureThreshold > 0 && options.CircuitBreakSeconds > 0,
        "Connector resilience values must be positive.")
    .ValidateOnStart();
builder.Services.AddSingleton<IResilienceDelay, SystemResilienceDelay>();
builder.Services.AddSingleton<ConnectionExecutionGate>();
builder.Services.AddSingleton<DestinationResilienceExecutor>();
builder.Services.AddScoped<ITimeEntrySourceConnector, SimulatedFieldOperationsConnector>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISecretValueProvider, KeyVaultSecretValueProvider>();
builder.Services.AddSingleton<ISecretLocatorRouter, ConfiguredSecretLocatorRouter>();
builder.Services.AddSingleton<CachedSecretResolver>();
builder.Services.AddScoped<ITimeEntryDestinationConnectorFactory, TimeEntryDestinationConnectorFactory>();
builder.Services.AddHostedService<IntegrationCommandWorker>();
builder.Services.AddHostedService<WorkerOutboxPublisher>();

await builder.Build().RunAsync();
