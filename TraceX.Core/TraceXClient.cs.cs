namespace TraceX.Core;

public sealed class TraceXClient
{
    private bool _hooksRegistered;

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
        // Crash capture will be connected to the telemetry dispatcher later.
    }
}