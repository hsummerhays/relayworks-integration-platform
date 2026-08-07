using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using RelayWorks.Sync.Worker.Telemetry;

namespace RelayWorks.Sync.Worker.Resilience;

public sealed class ConnectionExecutionGate(IOptions<ConnectorResilienceOptions> options,
    TimeProvider timeProvider, IResilienceDelay delay)
{
    private readonly ConnectorResilienceOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, ConnectionState> _states = new();

    public async Task<T> ExecuteAsync<T>(Guid connectionId, string provider,
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var state = _states.GetOrAdd(connectionId, _ => new ConnectionState(_options, timeProvider.GetUtcNow()));
        var timer = Stopwatch.StartNew();
        await state.Concurrency.WaitAsync(cancellationToken);
        try
        {
            await AcquireTokenAsync(state, cancellationToken);
            timer.Stop();
            if (timer.ElapsedMilliseconds > 0)
            {
                WorkerTelemetry.ConnectorThrottled.Add(1, new("provider", provider));
                WorkerTelemetry.ThrottleWait.Record(timer.Elapsed.TotalMilliseconds, new("provider", provider));
            }
            return await operation(cancellationToken);
        }
        finally { state.Concurrency.Release(); }
    }

    private async Task AcquireTokenAsync(ConnectionState state, CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan wait;
            lock (state.Sync)
            {
                var now = timeProvider.GetUtcNow();
                var elapsed = Math.Max(0, (now - state.LastRefill).TotalSeconds);
                state.Tokens = Math.Min(_options.BurstCapacityPerConnection,
                    state.Tokens + elapsed * _options.RequestsPerSecondPerConnection);
                state.LastRefill = now;
                if (state.Tokens >= 1)
                {
                    state.Tokens -= 1;
                    return;
                }
                wait = TimeSpan.FromSeconds((1 - state.Tokens) /
                    Math.Max(1, _options.RequestsPerSecondPerConnection));
            }
            await delay.WaitAsync(wait, cancellationToken);
        }
    }

    private sealed class ConnectionState
    {
        public ConnectionState(ConnectorResilienceOptions options, DateTimeOffset now)
        {
            Concurrency = new SemaphoreSlim(Math.Max(1, options.MaxConcurrentRequestsPerConnection));
            Tokens = Math.Max(1, options.BurstCapacityPerConnection);
            LastRefill = now;
        }
        public object Sync { get; } = new();
        public SemaphoreSlim Concurrency { get; }
        public double Tokens { get; set; }
        public DateTimeOffset LastRefill { get; set; }
    }
}
