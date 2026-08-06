using Azure.Messaging.ServiceBus;
using RelayWorks.Sync.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection(ServiceBusOptions.SectionName));
builder.Services.AddSingleton(provider =>
    ServiceBusClientFactory.Create(
        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceBusOptions>>().Value));
builder.Services.AddSingleton<TimeEntryProcessor>();
builder.Services.AddSingleton<ITimeEntrySourceConnector, SimulatedFieldOperationsConnector>();
builder.Services.AddSingleton<ITimeEntryDestinationConnector, SimulatedAccountingConnector>();
builder.Services.AddHostedService<IntegrationCommandWorker>();

await builder.Build().RunAsync();
