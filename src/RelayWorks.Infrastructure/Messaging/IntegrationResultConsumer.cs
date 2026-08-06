using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RelayWorks.Application.Abstractions;
using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Infrastructure.Messaging;

public sealed class IntegrationResultConsumer(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IOptions<ServiceBusOptions> options,
    TimeProvider timeProvider,
    ILogger<IntegrationResultConsumer> logger) : IHostedService, IAsyncDisposable
{
    private ServiceBusProcessor? _processor;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = serviceBusClient.CreateProcessor(
            options.Value.EventsTopic,
            options.Value.EventsSubscription,
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 4 });
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;
        await _processor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null) await _processor.StopProcessingAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationRunRepository>();

        if (args.Message.Subject == nameof(IntegrationRunCompletedV1))
        {
            var result = args.Message.Body.ToObjectFromJson<IntegrationRunCompletedV1>();
            if (result is null) throw new InvalidOperationException("Completion event payload was empty.");
            var run = await repository.FindByIdAsync(result.RunId, args.CancellationToken);
            if (run is null)
            {
                logger.LogWarning("Completion received for unknown run {RunId}", result.RunId);
                await args.DeadLetterMessageAsync(args.Message, "UnknownRun", cancellationToken: args.CancellationToken);
                return;
            }

            if (run.Status is RelayWorks.Domain.IntegrationRuns.IntegrationRunStatus.Completed
                or RelayWorks.Domain.IntegrationRuns.IntegrationRunStatus.CompletedWithErrors)
            {
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            if (run.Status == RelayWorks.Domain.IntegrationRuns.IntegrationRunStatus.Pending) run.Start();
            run.Complete(result.AcceptedRecords, result.RejectedRecords, result.OccurredAtUtc);
            await repository.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        if (args.Message.Subject == nameof(IntegrationRunFailedV1))
        {
            var result = args.Message.Body.ToObjectFromJson<IntegrationRunFailedV1>();
            if (result is null) throw new InvalidOperationException("Failure event payload was empty.");
            var run = await repository.FindByIdAsync(result.RunId, args.CancellationToken);
            if (run is null)
            {
                await args.DeadLetterMessageAsync(args.Message, "UnknownRun", cancellationToken: args.CancellationToken);
                return;
            }

            if (run.Status == RelayWorks.Domain.IntegrationRuns.IntegrationRunStatus.Failed)
            {
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            run.Fail(timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        await args.DeadLetterMessageAsync(args.Message, "UnsupportedEventType", cancellationToken: args.CancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus result consumer failed at {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null) await _processor.DisposeAsync();
    }
}
