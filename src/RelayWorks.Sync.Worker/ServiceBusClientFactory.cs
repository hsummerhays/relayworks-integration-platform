using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace RelayWorks.Sync.Worker;

public static class ServiceBusClientFactory
{
    public static ServiceBusClient Create(ServiceBusOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new ServiceBusClient(options.ConnectionString);
        }

        if (string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace))
        {
            throw new InvalidOperationException(
                "Configure ServiceBus:FullyQualifiedNamespace or ServiceBus:ConnectionString.");
        }

        return new ServiceBusClient(options.FullyQualifiedNamespace, new DefaultAzureCredential());
    }
}
