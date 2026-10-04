using System.Threading.Channels;

namespace TraceX.Core;

public sealed class TraceXClient
{
    private int _hooksRegistered;

    private readonly TraceXOptions _options;

    internal event Action<Exception>? ExceptionCaptured;

    public TraceXClient(TraceXOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
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
        ArgumentNullException.ThrowIfNull(exception);

        ExceptionCaptured?.Invoke(exception);

        // Crash capture will be connected to the telemetry dispatcher later.
    }
}