using Azure.Messaging.ServiceBus;
using RelayWorks.Sync.Worker;
using RelayWorks.Sync.Worker.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection(ServiceBusOptions.SectionName));
builder.Services.AddSingleton(provider =>
    ServiceBusClientFactory.Create(
        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceBusOptions>>().Value));
builder.Services.AddDbContext<WorkerLedgerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WorkerLedger")));
builder.Services.AddScoped<TimeEntryProcessor>();
builder.Services.AddScoped<ITimeEntrySourceConnector, SimulatedFieldOperationsConnector>();
builder.Services.AddScoped<ITimeEntryDestinationConnector, SimulatedAccountingConnector>();
builder.Services.AddHostedService<IntegrationCommandWorker>();
builder.Services.AddHostedService<WorkerOutboxPublisher>();

await builder.Build().RunAsync();
