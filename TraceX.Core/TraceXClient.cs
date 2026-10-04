using System.Threading.Channels;

namespace TraceX.Core;

public sealed class TraceXClient
{
    private bool _hooksRegistered;

    private readonly Channel<CrashEventRequest> _telemetryChannel =
        Channel.CreateBounded<CrashEventRequest>(
            new BoundedChannelOptions(500)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

    private readonly OfflineBuffer _offlineBuffer;
    private readonly TelemetryDispatcher _dispatcher;

    public TraceXClient(TraceXOptions options)
    {
        var httpClient = new HttpClient();

        var sender = new TelemetrySender(
            httpClient,
            options);

        _offlineBuffer = new OfflineBuffer();

        _dispatcher = new TelemetryDispatcher(
            _telemetryChannel,
            sender,
            _offlineBuffer);

        _ = Task.Run(
            () => _dispatcher.RunAsync(CancellationToken.None));
    }

    public void RegisterGlobalExceptionHooks()
    {
        if (_hooksRegistered)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _hooksRegistered = true;
    }

    private void OnUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;

        if (exception is null)
        {
            return;
        }

        CaptureException(exception);
    }

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        CaptureException(e.Exception);

        e.SetObserved();
    }

    public void CaptureException(Exception exception)
    {
        var crash = new CrashEventRequest
        {
            OccurredAt = DateTime.UtcNow,
            Platform = "dotnet",
            Language = "csharp",
            ExceptionType =
                exception.GetType().FullName ??
                exception.GetType().Name,
            ErrorMessage = exception.Message,
            StackTrace = exception.ToString()
        };

        _telemetryChannel.Writer.TryWrite(crash);
    }
}