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
        await DrainOfflineBufferAsync(
            cancellationToken);

        await foreach (var crash in _channel.Reader.ReadAllAsync(
            cancellationToken))
        {
            var sent =
                await DispatchAsync(
                    crash,
                    cancellationToken);

            if (sent)
            {
                await DrainOfflineBufferAsync(
                    cancellationToken);
            }
        }
    }

    private async Task<bool> DispatchAsync(
        CrashEventRequest crash,
        CancellationToken cancellationToken)
    {
        try
        {
            await _sender.SendAsync(
                crash,
                cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _offlineBuffer.Enqueue(crash);
            return false;
        }
    }

    private async Task DrainOfflineBufferAsync(
        CancellationToken cancellationToken)
    {
        while (_offlineBuffer.TryDequeue(
            out var cachedCrash))
        {
            if (cachedCrash is null)
            {
                continue;
            }

            try
            {
                await _sender.SendAsync(
                    cachedCrash,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _offlineBuffer.Enqueue(cachedCrash);
                throw;
            }
            catch (Exception)
            {
                _offlineBuffer.Enqueue(cachedCrash);
                break;
            }
        }
    }
}