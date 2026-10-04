using System.Threading.Channels;

namespace TraceX.Core;

public sealed class TelemetryDispatcher
{
    private readonly Channel<CrashEventRequest> _channel;
    private readonly TelemetrySender _sender;
    private readonly OfflineBuffer _offlineBuffer;

    public TelemetryDispatcher(
        Channel<CrashEventRequest> channel,
        TelemetrySender sender,
        OfflineBuffer offlineBuffer)
    {
        _channel = channel;
        _sender = sender;
        _offlineBuffer = offlineBuffer;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        await foreach (var crash in _channel.Reader.ReadAllAsync(
            cancellationToken))
        {
            await DispatchAsync(
                crash,
                cancellationToken);
        }
    }

    private async Task DispatchAsync(
        CrashEventRequest crash,
        CancellationToken cancellationToken)
    {
        try
        {
            await _sender.SendAsync(
                crash,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            _offlineBuffer.Enqueue(crash);
        }
    }
}