using System.Threading.Channels;

namespace TraceX.Core;

public sealed class TraceXClient : IDisposable, IAsyncDisposable
{
    private int _hooksRegistered;
    private int _disposed;

    private readonly TraceXOptions _options;
    private readonly OfflineBuffer _offlineBuffer;
    private readonly TelemetryDispatcher _dispatcher;
    private readonly Channel<CrashEventRequest> _channel;
    private readonly HttpClient _httpClient;
    private readonly Task _dispatcherTask;

    internal event Action<Exception>? ExceptionCaptured;

    public TraceXClient(TraceXOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;

        _channel =
            Channel.CreateBounded<CrashEventRequest>(
                new BoundedChannelOptions(500)
                {
                    FullMode =
                        BoundedChannelFullMode.Wait,

                    SingleReader = true,
                    SingleWriter = false
                });

        _httpClient =
            new HttpClient();

        _offlineBuffer =
            new OfflineBuffer(
                GetOfflineBufferPath(options));

        var sender =
            new TelemetrySender(
                _httpClient,
                options);

        _dispatcher =
            new TelemetryDispatcher(
                _channel,
                sender,
                _offlineBuffer);

        _dispatcherTask =
            Task.Run(
                () => _dispatcher.RunAsync(
                    CancellationToken.None));
    }

    public void RegisterGlobalExceptionHooks()
    {
        if (Interlocked.Exchange(
                ref _hooksRegistered,
                1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException +=
            OnUnhandledException;

        TaskScheduler.UnobservedTaskException +=
            OnUnobservedTaskException;
    }

    private void OnUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        var exception =
            e.ExceptionObject as Exception;

        if (exception is null)
        {
            return;
        }

        if (e.IsTerminating)
        {
            var crash =
                CreateCrashEvent(
                    exception,
                    []);

            _offlineBuffer.Enqueue(crash);

            return;
        }

        CaptureException(exception);
    }

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        CaptureException(
            e.Exception);
    }

    public void CaptureException(
        Exception exception)
    {
        CaptureException(
            exception,
            []);
    }

    public void CaptureException(
        Exception exception,
        IReadOnlyList<CrashBreadcrumb> breadcrumbs)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(breadcrumbs);

        ExceptionCaptured?.Invoke(
            exception);

        var crash =
            CreateCrashEvent(
                exception,
                breadcrumbs);

        if (!_channel.Writer.TryWrite(
                crash))
        {
            _offlineBuffer.Enqueue(
                crash);
        }
    }

    public async Task FlushAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(
                ref _disposed,
                0,
                0) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();

        await _dispatcherTask.WaitAsync(
            cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -=
            OnUnhandledException;

        TaskScheduler.UnobservedTaskException -=
            OnUnobservedTaskException;

        _channel.Writer.TryComplete();

        try
        {
            _dispatcherTask
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation during disposal.
        }

        _httpClient.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(
                ref _disposed,
                0,
                0) != 0)
        {
            return;
        }

        try
        {
            await FlushAsync(
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation during async disposal.
        }

        Dispose();
    }

    private CrashEventRequest CreateCrashEvent(
        Exception exception,
        IReadOnlyList<CrashBreadcrumb> breadcrumbs)
    {
        var context =
            TraceXTelemetryContext.Create(
                _options);

        var crashBreadcrumbs =
            breadcrumbs
                .Select(static breadcrumb =>
                    new CrashBreadcrumb
                    {
                        Timestamp =
                            breadcrumb.Timestamp,

                        Type =
                            breadcrumb.Type,

                        Message =
                            breadcrumb.Message,

                        Data =
                            new Dictionary<string, string>(
                                breadcrumb.Data)
                    })
                .ToList();

        return new CrashEventRequest
        {
            OccurredAt =
                DateTime.UtcNow,

            Platform =
                "dotnet",

            Language =
                "csharp",

            ExceptionType =
                exception.GetType().FullName
                ?? exception.GetType().Name,

            ErrorMessage =
                TraceXDataSanitizer.Sanitize(
                    exception.Message),

            StackTrace =
                TraceXDataSanitizer.Sanitize(
                    exception.ToString()),

            Environment =
                new CrashEnvironment
                {
                    RuntimeVersion =
                        context.RuntimeVersion,

                    OsName =
                        context.OsName,

                    OsVersion =
                        context.OsVersion,

                    Hostname =
                        context.Hostname,

                    ProcessMemoryBytes =
                        context.ProcessMemoryBytes,

                    EnvironmentName =
                        context.EnvironmentName
                },

            Breadcrumbs =
                crashBreadcrumbs
        };
    }

    private static string GetOfflineBufferPath(
        TraceXOptions options)
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var directory =
            Path.Combine(
                localApplicationData,
                "TraceX",
                options.ProjectKey);

        return Path.Combine(
            directory,
            "offline-buffer.json");
    }
}